using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
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

    // 背景發布任務
    private CancellationTokenSource? _cts;
    private Task? _publishLoop;

    public string HostId => _hostId;

    public ServerMetricsService()
        : this(null, NullLogger<ServerMetricsService>.Instance) { }

    public ServerMetricsService(IConnectionMultiplexer? redis)
        : this(redis, NullLogger<ServerMetricsService>.Instance) { }

    public ServerMetricsService(IConnectionMultiplexer? redis, ILogger<ServerMetricsService> logger)
    {
        _subscriber = redis?.GetSubscriber();
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
        _logger.LogInformation("ServerMetricsService 背景發布已啟動，HostId: {HostId}", _hostId);
        _cts = new CancellationTokenSource();
        _publishLoop = RunPublishLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }

        if (_publishLoop is not null)
        {
            try { await _publishLoop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

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
        if (_subscriber is null) return;
        try
        {
            var json = JsonSerializer.Serialize(metrics, JsonOptions);
            await _subscriber.PublishAsync(
                RedisChannel.Literal($"metrics:{_hostId}"),
                json,
                CommandFlags.FireAndForget);
            _logger.LogDebug("已發布指標到 Garnet channel: metrics:{HostId}", _hostId);
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
