using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Domain.Repositories;

public interface ICrimeRepository
{
    Task AddAsync(TheftCase theftCase, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<TheftCase> theftCases, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TheftCase>> GetByFilterAsync(CrimeFilter filter, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<TheftCase> Cases, int Total)> GetPagedByFilterAsync(CrimeFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<TheftCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TheftCase?> GetByCaseNumberAsync(string caseNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TheftCase>> GetByRadiusAsync(GeoCoordinate center, double radiusKm, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(string District, int Count)>> GetDistrictCountsAsync(CrimeFilter filter, CancellationToken cancellationToken = default);

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
    /// 計算座標尚未補齊（Latitude 或 Longitude 為 NULL）的案件數量
    /// </summary>
    Task<int> CountMissingCoordinatesAsync(CancellationToken cancellationToken = default);
}
