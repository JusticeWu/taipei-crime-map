using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Infrastructure.Repositories;

public class InMemoryCrimeRepository : ICrimeRepository
{
    private readonly List<TheftCase> _cases = new();

    public Task AddAsync(TheftCase theftCase, CancellationToken cancellationToken = default)
    {
        _cases.Add(theftCase);

        return Task.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_cases.Count);
    }

    public Task AddRangeAsync(IEnumerable<TheftCase> theftCases, CancellationToken cancellationToken = default)
    {
        _cases.AddRange(theftCases);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TheftCase>> GetByFilterAsync(CrimeFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _cases.AsEnumerable();

        if (filter.CaseType.HasValue)
        {
            query = query.Where(c => c.CaseType == filter.CaseType.Value);
        }

        if (filter.District != null)
        {
            query = query.Where(c => c.District != null && c.District.Name == filter.District.Name);
        }

        if (filter.YearFrom.HasValue)
        {
            query = query.Where(c => c.OccurredDate.Year.HasValue && c.OccurredDate.Year >= filter.YearFrom.Value);
        }

        if (filter.YearTo.HasValue)
        {
            query = query.Where(c => c.OccurredDate.Year.HasValue && c.OccurredDate.Year <= filter.YearTo.Value);
        }

        if (filter.TimeSlot != null && filter.TimeSlot.StartHour.HasValue && filter.TimeSlot.EndHour.HasValue)
        {
            query = query.Where(c => c.TimeSlot != null
                && c.TimeSlot.StartHour == filter.TimeSlot.StartHour.Value
                && c.TimeSlot.EndHour == filter.TimeSlot.EndHour.Value);
        }

        return Task.FromResult<IReadOnlyList<TheftCase>>(query.ToList());
    }

    public Task<(IReadOnlyList<TheftCase> Cases, int Total)> GetPagedByFilterAsync(
        CrimeFilter filter, int page, int pageSize, string? sortBy = null, string? sortOrder = null, CancellationToken cancellationToken = default)
    {
        var query = _cases.AsEnumerable();

        if (filter.CaseType.HasValue)
            query = query.Where(c => c.CaseType == filter.CaseType.Value);

        if (filter.District != null)
            query = query.Where(c => c.District != null && c.District.Name == filter.District.Name);

        if (filter.YearFrom.HasValue)
            query = query.Where(c => c.OccurredDate.Year.HasValue && c.OccurredDate.Year >= filter.YearFrom.Value);

        if (filter.YearTo.HasValue)
            query = query.Where(c => c.OccurredDate.Year.HasValue && c.OccurredDate.Year <= filter.YearTo.Value);

        if (filter.TimeSlot != null && filter.TimeSlot.StartHour.HasValue && filter.TimeSlot.EndHour.HasValue)
            query = query.Where(c => c.TimeSlot != null
                && c.TimeSlot.StartHour == filter.TimeSlot.StartHour.Value
                && c.TimeSlot.EndHour == filter.TimeSlot.EndHour.Value);

        var all = query.ToList();
        var total = all.Count;
        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult<(IReadOnlyList<TheftCase>, int)>((paged, total));
    }

    public Task<TheftCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = _cases.FirstOrDefault(c => c.Id == id);

        return Task.FromResult<TheftCase?>(result);
    }

    public Task<TheftCase?> GetByCaseNumberAsync(int caseNumber, CancellationToken cancellationToken = default)
    {
        var result = _cases.FirstOrDefault(c => c.CaseNumber == caseNumber);

        return Task.FromResult<TheftCase?>(result);
    }

    public Task<IReadOnlyList<TheftCase>> GetByRadiusAsync(GeoCoordinate center, double radiusKm,
        CancellationToken cancellationToken = default)
    {
        var result = _cases.Where(c => c.Coordinate != null 
            && HaversineDistance(c.Coordinate, center) <= radiusKm).ToList();

        return Task.FromResult<IReadOnlyList<TheftCase>>(result);
    }

    /// <summary>
    ////// Haversine 公式計算大圓距離，能處理經緯度跨越 180 度換算線。
    /// </summary>
    /// <param name="a">起點座標</param>
    /// <param name="b">終點座標</param>
    /// <returns>兩點之間的距離（公里），若任一座標缺少經緯度則回傳 double.MaxValue</returns>
    private static double HaversineDistance(GeoCoordinate a, GeoCoordinate b)
    {
        // GeoCoordinate 本身可能是 null，在呼叫端已過濾，這裡加防禦性檢查
        if (a is null || b is null)
            return double.MaxValue;

        // 地球平均半徑（公里）
        const double R = 6371.0;

        // 將起點與終點的緯度轉換為弧度
        var lat1 = ToRad(a.Latitude);
        var lat2 = ToRad(b.Latitude);

        // 計算緯度差與經度差（弧度）
        var dLat = ToRad(b.Latitude - a.Latitude);
        var dLon = ToRad(b.Longitude - a.Longitude);

        // Haversine 公式核心：
        // h = sin²(dLat/2) + cos(lat1) × cos(lat2) × sin²(dLon/2)
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        // 將半正矢值轉換為球面距離（公里）
        return R * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
    }

    /// <summary>
    /// 將角度（degrees）轉換為弧度（radians）。
    /// 三角函數（Sin、Cos）需要弧度作為輸入。
    /// </summary>
    /// <param name="deg">角度值</param>
    /// <returns>對應的弧度值</returns>
    private static double ToRad(double deg) => deg * Math.PI / 180.0;

    public Task<IReadOnlyList<TheftCase>> GetCasesWithMissingCoordinatesAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var result = _cases
            .Where(c => c.Coordinate is null)
            .OrderBy(c => c.CaseNumber)
            .Take(batchSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<TheftCase>>(result);
    }

    public Task<GeoCoordinate?> FindCoordinateByRawLocationAsync(string rawLocation, CancellationToken cancellationToken = default)
    {
        var match = _cases.FirstOrDefault(c => c.RawLocation == rawLocation && c.Coordinate is not null);

        return Task.FromResult(match?.Coordinate);
    }

    public Task UpdateCoordinateAsync(Guid id, GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        var theftCase = _cases.FirstOrDefault(c => c.Id == id);
        theftCase?.UpdateCoordinate(coordinate);

        return Task.CompletedTask;
    }

    public Task<int> UpdateCoordinateByLocationAsync(string rawLocation, double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var coordinate = GeoCoordinate.Create(latitude, longitude);
        var matches = _cases.Where(c => c.RawLocation == rawLocation).ToList();
        foreach (var theftCase in matches)
        {
            theftCase.UpdateCoordinate(coordinate);
        }

        return Task.FromResult(matches.Count);
    }

    public Task<int> CountMissingCoordinatesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_cases.Count(c => c.Coordinate is null));
    }

    public Task<int> UpdateCaseFieldsAsync(int caseNumber, int caseType, string? occurrenceDateRaw, string? timeSlotRaw, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    public Task<IReadOnlyList<(string District, int Count)>> GetDistrictCountsAsync(
        CrimeFilter filter, CancellationToken cancellationToken = default)
    {
        var counts = _cases
            .Where(c => c.District?.Name is not null)
            .Where(c => !filter.CaseType.HasValue || c.CaseType == filter.CaseType)
            .Where(c => filter.District is null || c.District?.Name == filter.District.Name)
            .Where(c => !filter.YearFrom.HasValue || c.OccurredDate.Year >= filter.YearFrom)
            .Where(c => !filter.YearTo.HasValue   || c.OccurredDate.Year <= filter.YearTo)
            .GroupBy(c => c.District!.Name)
            .Select(g => (District: g.Key, Count: g.Count()))
            .ToList();

        return Task.FromResult<IReadOnlyList<(string District, int Count)>>(counts);
    }

    public Task<IReadOnlyList<(string Label, int Year, int Count)>> GetCombinedTrendAsync(
        string dimension, CrimeFilter filter, int topN = 5, CancellationToken cancellationToken = default)
    {
        var filtered = _cases
            .Where(c => !filter.CaseType.HasValue || c.CaseType == filter.CaseType)
            .Where(c => filter.District is null || c.District?.Name == filter.District.Name)
            .Where(c => !filter.YearFrom.HasValue || c.OccurredDate.Year >= filter.YearFrom)
            .Where(c => !filter.YearTo.HasValue   || c.OccurredDate.Year <= filter.YearTo)
            .Where(c => c.OccurredDate.Year.HasValue)
            .ToList();

        Func<TheftCase, string?> labelFn = dimension switch
        {
            "TimeSlotCaseType" => c => c.TimeSlot?.Normalize() is { } ts && c.CaseType is { } ct
                ? $"{ts} {ct.ToChineseName()}" : null,
            "DistrictTimeSlot" => c => c.District?.Name is { } d && c.TimeSlot?.Normalize() is { } ts
                ? $"{d} {ts}" : null,
            "DistrictCaseType" => c => c.District?.Name is { } d && c.CaseType is { } ct
                ? $"{d} {ct.ToChineseName()}" : null,
            _ => throw new ArgumentException($"Unknown dimension: {dimension}")
        };

        var topLabels = filtered
            .Select(c => labelFn(c))
            .Where(l => l is not null)
            .GroupBy(l => l!)
            .OrderByDescending(g => g.Count())
            .Take(topN)
            .Select(g => g.Key)
            .ToHashSet();

        var result = filtered
            .Select(c => new { Label = labelFn(c), Year = c.OccurredDate.Year!.Value + 1911 })
            .Where(x => x.Label is not null && topLabels.Contains(x.Label))
            .GroupBy(x => new { x.Label, x.Year })
            .Select(g => (Label: g.Key.Label!, Year: g.Key.Year, Count: g.Count()))
            .OrderBy(x => x.Label)
            .ThenBy(x => x.Year)
            .ToList();

        return Task.FromResult<IReadOnlyList<(string, int, int)>>(result);
    }

    public Task<IReadOnlyList<(int Year, int Count)>> GetGroupedYearlyTrendAsync(
        IReadOnlyList<string> districts, IReadOnlyList<int> caseTypes, int minHour, int maxHour,
        CancellationToken cancellationToken = default)
    {
        var districtSet = new HashSet<string>(districts);
        var caseTypeSet = new HashSet<int>(caseTypes);

        var result = _cases
            .Where(c => c.District?.Name is not null && districtSet.Contains(c.District.Name))
            .Where(c => c.CaseType.HasValue && caseTypeSet.Contains((int)c.CaseType.Value))
            .Where(c => c.TimeSlot?.StartHour is not null && c.TimeSlot.StartHour >= minHour && c.TimeSlot.StartHour < maxHour)
            .Where(c => c.OccurredDate.Year.HasValue)
            .GroupBy(c => c.OccurredDate.Year!.Value + 1911)
            .Select(g => (Year: g.Key, Count: g.Count()))
            .OrderBy(x => x.Year)
            .ToList();

        return Task.FromResult<IReadOnlyList<(int, int)>>(result);
    }

    public Task<IReadOnlyList<(string Key, int Year, int Count)>> GetYearlyTrendByDimensionAsync(
        IReadOnlyList<string> districts, IReadOnlyList<int> caseTypes,
        int? minHour, int? maxHour, string groupBy,
        CancellationToken cancellationToken = default)
    {
        var districtSet = new HashSet<string>(districts);
        var caseTypeSet = new HashSet<int>(caseTypes);

        var filtered = _cases
            .Where(c => c.District?.Name is not null && districtSet.Contains(c.District.Name))
            .Where(c => c.CaseType.HasValue && caseTypeSet.Contains((int)c.CaseType.Value))
            .Where(c => c.OccurredDate.Year.HasValue);

        if (minHour.HasValue && maxHour.HasValue)
            filtered = filtered.Where(c => c.TimeSlot?.StartHour is not null
                && c.TimeSlot.StartHour >= minHour && c.TimeSlot.StartHour < maxHour);

        Func<TheftCase, string?> keyFn = groupBy switch
        {
            "caseType" => c => c.CaseType?.ToChineseName(),
            "district" => c => c.District?.Name,
            "districtCaseType" => c => c.District?.Name is { } d && c.CaseType is { } ct
                ? $"{d}-{ct.ToChineseName()}" : null,
            _ => throw new ArgumentException($"Unknown groupBy: {groupBy}")
        };

        var result = filtered
            .Select(c => new { Key = keyFn(c), Year = c.OccurredDate.Year!.Value + 1911 })
            .Where(x => x.Key is not null)
            .GroupBy(x => new { x.Key, x.Year })
            .Select(g => (Key: g.Key.Key!, Year: g.Key.Year, Count: g.Count()))
            .OrderBy(x => x.Key).ThenBy(x => x.Year)
            .ToList();

        return Task.FromResult<IReadOnlyList<(string, int, int)>>(result);
    }

    public Task<(IReadOnlyList<(string District, int Count)> DistrictCounts, IReadOnlyList<(string TimeSlot, int Count)> TimeSlotCounts)> GetStatsByFilterAsync(
        CrimeFilter filter, CancellationToken cancellationToken = default)
    {
        var filtered = _cases
            .Where(c => !filter.CaseType.HasValue || c.CaseType == filter.CaseType)
            .Where(c => filter.District is null || c.District?.Name == filter.District.Name)
            .Where(c => !filter.YearFrom.HasValue || c.OccurredDate.Year >= filter.YearFrom)
            .Where(c => !filter.YearTo.HasValue   || c.OccurredDate.Year <= filter.YearTo)
            .ToList();

        var districtCounts = filtered
            .Where(c => c.District?.Name is not null)
            .GroupBy(c => c.District!.Name)
            .Select(g => (District: g.Key, Count: g.Count()))
            .ToList();

        var timeSlotCounts = filtered
            .Where(c => c.TimeSlot?.StartHour is not null && c.TimeSlot?.EndHour is not null)
            .GroupBy(c => c.TimeSlot!.Normalize())
            .Select(g => (TimeSlot: g.Key, Count: g.Count()))
            .ToList();

        return Task.FromResult<(IReadOnlyList<(string District, int Count)>, IReadOnlyList<(string TimeSlot, int Count)>)>(
            (districtCounts, timeSlotCounts));
    }
}