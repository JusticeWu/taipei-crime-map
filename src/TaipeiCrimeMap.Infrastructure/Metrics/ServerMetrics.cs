namespace TaipeiCrimeMap.Infrastructure.Metrics;

public sealed record ServerMetrics(
    // 識別資訊
    string HostId,
    string Environment,
    // 靜態硬體資訊（啟動時收集一次）
    int CpuCores,
    long TotalMemoryMb,
    string OsDescription,
    string DotNetVersion,
    // 即時動態指標（每秒更新）
    double CpuPercent,
    double MemoryMb,
    double GcMemoryMb,
    string Uptime,
    int ThreadCount,
    int ConnectionCount);
