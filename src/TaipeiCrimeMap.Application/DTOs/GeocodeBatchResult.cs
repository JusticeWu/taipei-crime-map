namespace TaipeiCrimeMap.Application.DTOs;

public record GeocodeBatchResult
{
    /// <summary>本次處理的案件數量</summary>
    public int ProcessedCount { get; init; }

    /// <summary>複用相同 RawLocation 已有座標的案件數量</summary>
    public int ReusedCount { get; init; }

    /// <summary>呼叫 Google Maps API 的次數</summary>
    public int ApiCallCount { get; init; }

    /// <summary>Geocoding 失敗而跳過的案件數量</summary>
    public int FailedCount { get; init; }

    /// <summary>剩餘尚未補齊座標的案件數量</summary>
    public int RemainingCount { get; init; }
}
