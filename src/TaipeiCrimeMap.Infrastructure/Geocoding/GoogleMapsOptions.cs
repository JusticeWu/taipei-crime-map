namespace TaipeiCrimeMap.Infrastructure.Geocoding;

public class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// 常見的 API 呼叫 timeout 預設值
    /// </summary>
    public int TimeoutSeconds { get; init; } = 5;

    /// <summary>
    /// 批次匯入時每筆請求間的延遲（毫秒），避免超過 QPS 限制
    /// </summary>
    public int BatchDelayMs { get; init; } = 50;
}