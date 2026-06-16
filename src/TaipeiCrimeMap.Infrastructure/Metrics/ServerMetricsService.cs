using System.Diagnostics;

namespace TaipeiCrimeMap.Infrastructure.Metrics;

public sealed class ServerMetricsService
{
    private readonly Process _process;
    private TimeSpan _lastCpuTime;
    private DateTime _lastSampleTime;
    private readonly object _cpuLock = new();

    public ServerMetricsService()
    {
        _process = Process.GetCurrentProcess();
        _process.Refresh();
        _lastCpuTime = _process.TotalProcessorTime;
        _lastSampleTime = DateTime.UtcNow;
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
                  / elapsedSeconds / Environment.ProcessorCount * 100.0
                : 0.0;

            _lastCpuTime = currentCpuTime;
            _lastSampleTime = now;
        }

        var uptime = DateTime.UtcNow - _process.StartTime.ToUniversalTime();

        return new ServerMetrics(
            CpuPercent: Math.Round(Math.Max(0.0, cpuPercent), 1),
            MemoryMb: Math.Round(_process.WorkingSet64 / 1_048_576.0, 1),
            GcMemoryMb: Math.Round(GC.GetTotalMemory(false) / 1_048_576.0, 1),
            Uptime: FormatUptime(uptime),
            ThreadCount: _process.Threads.Count,
            ConnectionCount: connectionCount);
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
