using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Domain.Repositories;

public interface ICrimeRepository
{
    Task AddAsync(TheftCase theftCase, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<TheftCase> theftCases, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TheftCase>> GetByFilterAsync(CrimeFilter filter, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<TheftCase> Cases, int Total)> GetPagedByFilterAsync(CrimeFilter filter, int page, int pageSize, string? sortBy = null, string? sortOrder = null, CancellationToken cancellationToken = default);
    Task<TheftCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TheftCase?> GetByCaseNumberAsync(int caseNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TheftCase>> GetByRadiusAsync(GeoCoordinate center, double radiusKm, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(string District, int Count)>> GetDistrictCountsAsync(CrimeFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// 統計圖表用：依篩選條件分別計算「行政區分布」與「時段分布」彙總計數
    /// </summary>
    Task<(IReadOnlyList<(string District, int Count)> DistrictCounts, IReadOnlyList<(string TimeSlot, int Count)> TimeSlotCounts)> GetStatsByFilterAsync(
        CrimeFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得座標尚未補齊（Latitude 或 Longitude 為 NULL）的案件，依案件編號排序，最多 batchSize 筆
    /// </summary>
    Task<IReadOnlyList<TheftCase>> GetCasesWithMissingCoordinatesAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢相同 RawLocation 且座標已存在的案件，回傳其座標供複用（節省 Geocoding API 配額）
    /// </summary>
    Task<GeoCoordinate?> FindCoordinateByRawLocationAsync(string rawLocation, CancellationToken cancellationToken = default);

    /// <summary>
    /// 寫入單一案件的座標
    /// </summary>
    Task UpdateCoordinateAsync(Guid id, GeoCoordinate coordinate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 依 RawLocation 更新座標（管理用途，可一次更新多筆相同地點的案件），回傳受影響筆數
    /// </summary>
    Task<int> UpdateCoordinateByLocationAsync(string rawLocation, double latitude, double longitude, CancellationToken cancellationToken = default);

    /// <summary>
    /// 計算座標尚未補齊（Latitude 或 Longitude 為 NULL）的案件數量
    /// </summary>
    Task<int> CountMissingCoordinatesAsync(CancellationToken cancellationToken = default);

    Task<int> UpdateCaseFieldsAsync(int caseNumber, int caseType, string? occurrenceDateRaw, string? timeSlotRaw, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(string Label, int Year, int Count)>> GetCombinedTrendAsync(
        string dimension, CrimeFilter filter, int topN = 5, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(int Year, int Count)>> GetGroupedYearlyTrendAsync(
        IReadOnlyList<string> districts, IReadOnlyList<int> caseTypes, int minHour, int maxHour,
        CancellationToken cancellationToken = default);
}
