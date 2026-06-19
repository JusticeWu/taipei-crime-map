namespace TaipeiCrimeMap.Infrastructure.Jobs;

public sealed class AdaptiveConcurrencyController
{
    private int _currentLimit;
    public int MinLimit { get; }
    public int MaxLimit { get; }

    public int CurrentLimit => Volatile.Read(ref _currentLimit);

    public AdaptiveConcurrencyController(int initial, int minLimit = 1, int maxLimit = 20)
    {
        MinLimit = Math.Max(1, minLimit);
        MaxLimit = Math.Max(MinLimit, maxLimit);
        _currentLimit = Math.Clamp(initial, MinLimit, MaxLimit);
    }

    public int OnSuccess()
    {
        while (true)
        {
            var current = Volatile.Read(ref _currentLimit);
            var next = Math.Min(current + 1, MaxLimit);
            if (next == current) return current;
            if (Interlocked.CompareExchange(ref _currentLimit, next, current) == current)
                return next;
        }
    }

    public int OnTransientFailure()
    {
        while (true)
        {
            var current = Volatile.Read(ref _currentLimit);
            var next = Math.Max(current / 2, MinLimit);
            if (next == current) return current;
            if (Interlocked.CompareExchange(ref _currentLimit, next, current) == current)
                return next;
        }
    }
}
