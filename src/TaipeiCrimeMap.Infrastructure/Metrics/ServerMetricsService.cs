using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace TaipeiCrimeMap.Infrastructure.Metrics;

public sealed class ServerMetricsService : IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Process _process;
    private TimeSpan _lastCpuTime;
    private DateTime _lastSampleTime;
    private readonly object _cpuLock = new();
    private readonly ISubscriber? _subscriber;
    private readonly IConnectionMultiplexer? _primaryRedis;
    private readonly IConnectionMultiplexer? _secondaryRedis;
    private readonly ILogger<ServerMetricsService> _logger;

    // 識別資訊
    private readonly string _hostId;
    private readonly string _appEnvironment;

    // 靜態硬體資訊
    private readonly int _cpuCores;
    private readonly long _totalMemoryMb;
    private readonly string _osDescription;
    private readonly string _dotNetVersion;

    // 活躍 WebSocket 連線數（由 Handler 透過 Add/RemoveConnection 維護）
    private int _connectionCount;

    // 背景任務
    private CancellationTokenSource? _cts;
    private Task? _publishLoop;
    private Task? _subscriptionTask;

    // 發布日誌節流：訂閱者為 0 時每次都 log；否則每 10 秒 log 一次
    private DateTime _lastPublishInfoLogTime = DateTime.MinValue;

    // 廣播頻道：每個 WebSocket 連線各持有一個 writer，收到 Garnet 訊息時廣播給所有連線
    private readonly ConcurrentDictionary<Guid, ChannelWriter<byte[]>> _clientChannels = new();

    public string HostId => _hostId;

    public ServerMetricsService()
        : this(null, null, NullLogger<ServerMetricsService>.Instance) { }

    public ServerMetricsService(IConnectionMultiplexer? redis)
        : this(redis, null, NullLogger<ServerMetricsService>.Instance) { }

    public ServerMetricsService(IConnectionMultiplexer? redis, ILogger<ServerMetricsService> logger)
        : this(redis, null, logger) { }

    public ServerMetricsService(
        IConnectionMultiplexer? redis,
        IConnectionMultiplexer? secondaryRedis,
        ILogger<ServerMetricsService> logger)
    {
        _primaryRedis = redis;
        _secondaryRedis = secondaryRedis;
        _subscriber = redis?.GetSubscriber();
        _logger = logger;

        _hostId = System.Environment.MachineName;
        _appEnvironment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        _process = Process.GetCurrentProcess();
        _process.Refresh();
        _lastCpuTime = _process.TotalProcessorTime;
        _lastSampleTime = DateTime.UtcNow;

        _cpuCores = System.Environment.ProcessorCount;
        _totalMemoryMb = ReadTotalMemoryMb();
        _osDescription = RuntimeInformation.OSDescription;
        _dotNetVersion = RuntimeInformation.FrameworkDescription;
    }

    // ── IHostedService ──────────────────────────────────────────────────────

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ServerMetricsService 背景發布已啟動，HostId: {HostId}", _hostId);
        _cts = new CancellationTokenSource();
        _publishLoop = RunPublishLoopAsync(_cts.Token);
        _subscriptionTask = RunSubscriptionsAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var cts = _cts;
        _cts = null;
        if (cts is not null)
        {
            await cts.CancelAsync();
            cts.Dispose();
        }

        var tasks = new List<Task>();
        if (_publishLoop is not null) tasks.Add(_publishLoop);
        if (_subscriptionTask is not null) tasks.Add(_subscriptionTask);

        if (tasks.Count > 0)
        {
            try { await Task.WhenAll(tasks).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        foreach (var writer in _clientChannels.Values)
            writer.TryComplete();
        _clientChannels.Clear();
    }

    // ── Connection tracking（由 WebSocketHandler 呼叫） ─────────────────────

    public void AddConnection() => Interlocked.Increment(ref _connectionCount);
    public void RemoveConnection() => Interlocked.Decrement(ref _connectionCount);

    // ── Client channel management（廣播 Garnet 訊息給 WebSocket 連線） ──────

    public (Guid Id, ChannelReader<byte[]> Reader) RegisterClientChannel()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = true,
        });
        _clientChannels[id] = channel.Writer;
        return (id, channel.Reader);
    }

    public void UnregisterClientChannel(Guid id)
    {
        if (_clientChannels.TryRemove(id, out var writer))
            writer.TryComplete();
    }

    // ── Metrics ─────────────────────────────────────────────────────────────

    public ServerMetrics GetMetrics(int connectionCount)
    {
        _process.Refresh();

        double cpuPercent;
        lock (_cpuLock)
        {
            var now = DateTime.UtcNow;
            var currentCpuTime = _process.TotalProcessorTime;
            var elapsedSeconds = (now - _lastSampleTime).TotalSeconds;

            cpuPercent = elapsedSeconds > 0
                ? (currentCpuTime - _lastCpuTime).TotalSeconds
                  / elapsedSeconds / System.Environment.ProcessorCount * 100.0
                : 0.0;

            _lastCpuTime = currentCpuTime;
            _lastSampleTime = now;
        }

        var uptime = DateTime.UtcNow - _process.StartTime.ToUniversalTime();

        return new ServerMetrics(
            HostId: _hostId,
            Environment: _appEnvironment,
            CpuCores: _cpuCores,
            TotalMemoryMb: _totalMemoryMb,
            OsDescription: _osDescription,
            DotNetVersion: _dotNetVersion,
            CpuPercent: Math.Round(Math.Max(0.0, cpuPercent), 1),
            MemoryMb: Math.Round(_process.WorkingSet64 / 1_048_576.0, 1),
            GcMemoryMb: Math.Round(GC.GetTotalMemory(false) / 1_048_576.0, 1),
            Uptime: FormatUptime(uptime),
            ThreadCount: _process.Threads.Count,
            ConnectionCount: connectionCount);
    }

    public async Task PublishAsync(ServerMetrics metrics, CancellationToken ct = default)
    {
        if (_subscriber is null) return;
        try
        {
            var json = JsonSerializer.Serialize(metrics, JsonOptions);
            var subscriberCount = await _subscriber.PublishAsync(
                RedisChannel.Literal($"metrics:{_hostId}"),
                json);

            var now = DateTime.UtcNow;
            if (subscriberCount == 0 || (now - _lastPublishInfoLogTime).TotalSeconds >= 10)
            {
                _logger.LogInformation(
                    "已發布指標到 Garnet, channel: metrics:{HostId}, 訂閱者數量: {SubscriberCount}",
                    _hostId, subscriberCount);
                _lastPublishInfoLogTime = now;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "發布指標到 Garnet 失敗: metrics:{HostId}", _hostId);
        }
    }

    // ── Private ─────────────────────────────────────────────────────────────

    private async Task RunPublishLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var metrics = GetMetrics(Volatile.Read(ref _connectionCount));
                await PublishAsync(metrics, ct);
                await Task.Delay(1000, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task RunSubscriptionsAsync(CancellationToken ct)
    {
        var tasks = new List<Task>();
        if (_primaryRedis is not null)
            tasks.Add(SubscribeGarnetAsync(_primaryRedis, ct));
        if (_secondaryRedis is not null)
            tasks.Add(SubscribeGarnetAsync(_secondaryRedis, ct));

        if (tasks.Count > 0)
            await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task SubscribeGarnetAsync(IConnectionMultiplexer redis, CancellationToken ct)
    {
        var endpoint = string.Join(",", redis.GetEndPoints().Select(e => e.ToString()));
        ISubscriber? sub = null;
        try
        {
            sub = redis.GetSubscriber();
            await sub.SubscribeAsync(
                RedisChannel.Pattern("metrics:*"),
                (_, message) =>
                {
                    if (!message.IsNullOrEmpty)
                        BroadcastToClients((string)message!);
                });

            _logger.LogInformation("已訂閱 Garnet: {Endpoint} pattern: metrics:*", endpoint);
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Garnet 訂閱例外: {Endpoint}", endpoint);
        }
        finally
        {
            if (sub is not null)
            {
                try { await sub.UnsubscribeAsync(RedisChannel.Pattern("metrics:*")); }
                catch { }
            }
        }
    }

    private void BroadcastToClients(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        foreach (var writer in _clientChannels.Values)
            writer.TryWrite(bytes);
    }

    private static long ReadTotalMemoryMb()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                            return kb / 1024;
                    }
                }
            }
            catch { /* fallthrough */ }
        }

        return (long)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1_048_576.0);
    }

    private static string FormatUptime(TimeSpan span)
    {
        if (span.TotalDays >= 1)
            return $"{(int)span.TotalDays}d {span.Hours}h {span.Minutes}m";
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes}m {span.Seconds}s";
        return $"{span.Minutes}m {span.Seconds}s";
    }
}
