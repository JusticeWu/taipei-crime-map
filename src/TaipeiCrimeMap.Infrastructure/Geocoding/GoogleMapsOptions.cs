namespace TaipeiCrimeMap.Infrastructure.Geocoding;

public class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";
    public string ApiKey { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 5;
    public int BatchDelayMs { get; init; } = 50;

    /// <summary>
    /// 每日 API 呼叫上限；0 表示不限制
    /// </summary>
    public int DailyQuotaLimit { get; init; } = 0;
}