using TaipeiCrimeMap.Application.Interfaces;

namespace TaipeiCrimeMap.Infrastructure.Timing;

/// <summary>
/// Timing:Enabled = false 時的空實作，不做任何事，零效能損耗。
/// </summary>
public sealed class NullTimingTracker : ITimingTracker
{
    public static readonly NullTimingTracker Instance = new();

    public IDisposable Track(string stageName) => NullDisposable.Instance;
    public void LogSummary() { }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }
}
