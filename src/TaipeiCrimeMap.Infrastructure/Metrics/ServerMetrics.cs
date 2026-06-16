namespace TaipeiCrimeMap.Infrastructure.Metrics;

public sealed record ServerMetrics(
    double CpuPercent,
    double MemoryMb,
    double GcMemoryMb,
    string Uptime,
    int ThreadCount,
    int ConnectionCount);
