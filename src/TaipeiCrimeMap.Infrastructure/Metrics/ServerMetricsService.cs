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
    private readonly IConnectionMultiplexer? _primaryRedis;
    private readonly IConnectionMultiplexer? _secondaryRedis;
    private readonly ISubscriber? _publisher;
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

    // 已連線 WebSocket 的訊息接收端（每個 WebSocket 連線各自一個）
    private readonly ConcurrentDictionary<ChannelWriter<byte[]>, byte> _sinks = new();

    // 背景任務
    private CancellationTokenSource? _cts;
    private Task? _publishLoop;
    private Task? _primarySubLoop;
    private Task? _secondarySubLoop;

    // 發布 log 節流（避免每秒都輸出，每 10 秒或訂閱者為 0 才 log Information）
    private DateTime _lastPublishLogTime = DateTime.MinValue;

    public string HostId => _hostId;

    public ServerMetricsService()
        : this(null, null, NullLogger<ServerMetricsService>.Instance) { }

    public ServerMetricsService(IConnectionMultiplexer? redis)
        : this(redis, null, NullLogger<ServerMetricsService>.Instance) { }

    public ServerMetricsService(IConnectionMultiplexer? redis, ILogger<ServerMetricsService> logger)
        : this(redis, null, logger) { }

    public ServerMetricsService(
        IConnectionMultiplexer? primaryRedis,
        IConnectionMultiplexer? secondaryRedis,
        ILogger<ServerMetricsService> logger)
    {
        _primaryRedis = primaryRedis;
        _secondaryRedis = secondaryRedis;
        _publisher = primaryRedis?.GetSubscriber();
        _logger = logger;

        _hostId = System.Environment.GetEnvironmentVariable("HOSTNAME") ?? "unknown";
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
        _logger.LogInformation("ServerMetricsService 已啟動，HostId: {HostId}", _hostId);
        _cts = new CancellationTokenSource();

        _publishLoop = RunPublishLoopAsync(_cts.Token);

        if (_primaryRedis is not null)
            _primarySubLoop = SubscribeToGarnetAsync(_primaryRedis, _cts.Token);

        if (_secondaryRedis is not null)
            _secondarySubLoop = SubscribeToGarnetAsync(_secondaryRedis, _cts.Token);

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

        var tasks = new List<Task>(3);
        if (_publishLoop is not null) tasks.Add(_publishLoop);
        if (_primarySubLoop is not null) tasks.Add(_primarySubLoop);
        if (_secondarySubLoop is not null) tasks.Add(_secondarySubLoop);

        if (tasks.Count > 0)
        {
            try { await Task.WhenAll(tasks).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    // ── Sink 管理（WebSocket 連線時呼叫） ────────────────────────────────────

    public void RegisterSink(ChannelWriter<byte[]> sink) => _sinks.TryAdd(sink, 0);
    public void UnregisterSink(ChannelWriter<byte[]> sink) => _sinks.TryRemove(sink, out _);

    // ── Connection tracking（由 WebSocketHandler 呼叫） ─────────────────────

    public void AddConnection() => Interlocked.Increment(ref _connectionCount);
    public void RemoveConnection() => Interlocked.Decrement(ref _connectionCount);

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
        if (_publisher is null) return;
        try
        {
            var json = JsonSerializer.Serialize(metrics, JsonOptions);
            var subscriberCount = await _publisher.PublishAsync(
                RedisChannel.Literal($"metrics:{_hostId}"),
                json);

            var now = DateTime.UtcNow;
            if (subscriberCount == 0 || (now - _lastPublishLogTime).TotalSeconds >= 10)
            {
                _logger.LogInformation(
                    "已發布指標到 Garnet, channel: metrics:{HostId}, 訂閱者數量: {SubscriberCount}",
                    _hostId, subscriberCount);
                _lastPublishLogTime = now;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "發布指標到 Garnet 失敗: metrics:{HostId}", _hostId);
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

    private async Task SubscribeToGarnetAsync(IConnectionMultiplexer redis, CancellationToken ct)
    {
        var endpoint = string.Join(",", redis.GetEndPoints().Select(e => e.ToString()));
        ISubscriber? sub = null;
        try
        {
            sub = redis.GetSubscriber();
            await sub.SubscribeAsync(
                RedisChannel.Pattern("metrics:*"),
                (channel, message) =>
                {
                    if (message.IsNullOrEmpty || _sinks.IsEmpty) return;
                    var bytes = Encoding.UTF8.GetBytes((string)message!);
                    foreach (var (writer, _) in _sinks)
                        writer.TryWrite(bytes);
                    _logger.LogDebug("收到 Garnet 訊息 from channel: {Channel}, 廣播給 {SinkCount} 個 WebSocket", (string?)channel ?? "unknown", _sinks.Count);
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
                try { await sub.UnsubscribeAsync(RedisChannel.Pattern("metrics:*")); } catch { }
        }
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
