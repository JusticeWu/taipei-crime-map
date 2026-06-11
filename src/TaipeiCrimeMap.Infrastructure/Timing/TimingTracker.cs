using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TaipeiCrimeMap.Application.Interfaces;

namespace TaipeiCrimeMap.Infrastructure.Timing;

/// <summary>
/// 執行時間追蹤器（Timing:Enabled = true 時注入此實作）。
/// Scoped 生命週期，每個 HTTP 請求有獨立實例。
/// </summary>
public sealed class TimingTracker : ITimingTracker
{
    private readonly ILogger<TimingTracker> _logger;
    private readonly List<(string Stage, long ElapsedMs)> _records = new();
    private readonly object _lock = new();

    public TimingTracker(ILogger<TimingTracker> logger)
    {
        _logger = logger;
    }

    public IDisposable Track(string stageName)
    {
        return new StageTimer(stageName, elapsed =>
        {
            lock (_lock)
            {
                _records.Add((stageName, elapsed));
            }
        });
    }

    public void LogSummary()
    {
        List<(string Stage, long ElapsedMs)> snapshot;
        lock (_lock)
        {
            snapshot = new List<(string, long)>(_records);
        }

        if (snapshot.Count == 0) return;

        var total  = snapshot.Sum(r => r.ElapsedMs);
        var detail = string.Join(" | ", snapshot.Select(r => $"{r.Stage}={r.ElapsedMs}ms"));

        _logger.LogInformation(
            "[Timing] 總計={TotalMs}ms | {Detail}",
            total,
            detail);
    }

    private sealed class StageTimer : IDisposable
    {
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private readonly string _stageName;
        private readonly Action<long> _onStop;
        private bool _disposed;

        public StageTimer(string stageName, Action<long> onStop)
        {
            _stageName = stageName;
            _onStop    = onStop;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _sw.Stop();
            _onStop(_sw.ElapsedMilliseconds);
        }
    }
}
