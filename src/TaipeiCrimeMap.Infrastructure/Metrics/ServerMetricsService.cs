using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using StackExchange.Redis;

namespace TaipeiCrimeMap.Infrastructure.Metrics;

public sealed class ServerMetricsService
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

    // 識別資訊
    private readonly string _hostId;
    private readonly string _appEnvironment;

    // 靜態硬體資訊
    private readonly int _cpuCores;
    private readonly long _totalMemoryMb;
    private readonly string _osDescription;
    private readonly string _dotNetVersion;

    public string HostId => _hostId;

    public ServerMetricsService() : this(null) { }

    public ServerMetricsService(IConnectionMultiplexer? redis)
    {
        _subscriber = redis?.GetSubscriber();

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
        }
        catch { /* Garnet 不可用時靜默跳過 */ }
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
