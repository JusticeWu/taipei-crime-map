using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Infrastructure.Repositories;

public class SqlServerCrimeRepository : ICrimeRepository
{
    private readonly string _connectionString;

    public SqlServerCrimeRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    public async Task<TheftCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<TheftCaseRow>(
            "SELECT * FROM theft_cases WHERE id = @Id",
            new { Id = id });
        return row?.ToDomain();
    }

    public async Task<TheftCase?> GetByCaseNumberAsync(string caseNumber, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<TheftCaseRow>(
            "SELECT * FROM theft_cases WHERE case_number = @CaseNumber",
            new { CaseNumber = caseNumber });
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<TheftCase>> GetByDistrictAsync(District district, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<TheftCaseRow>(
            "SELECT * FROM theft_cases WHERE district = @District",
            new { District = district.Name });
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<TheftCase>> GetByFilterAsync(CrimeFilter filter, CancellationToken cancellationToken = default)
    {
        var (cases, _) = await GetPagedByFilterAsync(filter, page: 1, pageSize: int.MaxValue, cancellationToken: cancellationToken);
        return cases;
    }

    public async Task<(IReadOnlyList<TheftCase> Cases, int Total)> GetPagedByFilterAsync(
        CrimeFilter filter, int page, int pageSize, string? sortBy = null, string? sortOrder = null, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        var rows = (await conn.QueryAsync<TheftCaseRow>(
            "sp_get_theft_cases_by_filter",
            new
            {
                CaseType       = filter.CaseType.HasValue ? (int?)filter.CaseType.Value : null,
                District       = filter.District?.Name,
                YearFrom       = filter.YearFrom,
                YearTo         = filter.YearTo,
                TimeSlotStart  = filter.TimeSlot?.StartHour,
                TimeSlotEnd    = filter.TimeSlot?.EndHour,
                Page           = page,
                PageSize       = pageSize,
                SortBy         = sortBy,
                SortOrder      = sortOrder
            },
            commandType: CommandType.StoredProcedure)).ToList();

        var total = rows.Count > 0 ? rows[0].TotalCount : 0;
        return (rows.Select(r => r.ToDomain()).ToList(), total);
    }

    public async Task<IReadOnlyList<TheftCase>> GetByRadiusAsync(
        GeoCoordinate center, double radiusKm, CancellationToken cancellationToken = default)
    {
        var latDelta = radiusKm / 111.0;
        var lngDelta = radiusKm / (111.0 * Math.Cos(center.Latitude * Math.PI / 180.0));

        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<TheftCaseRow>(
            """
            SELECT * FROM theft_cases
            WHERE latitude  BETWEEN @LatMin AND @LatMax
              AND longitude BETWEEN @LngMin AND @LngMax
            """,
            new
            {
                LatMin = center.Latitude - latDelta,
                LatMax = center.Latitude + latDelta,
                LngMin = center.Longitude - lngDelta,
                LngMax = center.Longitude + lngDelta
            });

        return rows
            .Select(r => r.ToDomain())
            .Where(t => t.Coordinate is not null &&
                        CalculateDistanceKm(center, t.Coordinate) <= radiusKm)
            .ToList();
    }

    public async Task AddAsync(TheftCase theftCase, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        await conn.ExecuteAsync(UpsertSql, ToRow(theftCase));
    }

    public async Task AddRangeAsync(IEnumerable<TheftCase> theftCases, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        await conn.ExecuteAsync(UpsertSql, theftCases.Select(ToRow));
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM theft_cases");
    }

    public async Task<IReadOnlyList<TheftCase>> GetCasesWithMissingCoordinatesAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<TheftCaseRow>(
            """
            SELECT TOP (@BatchSize) *
            FROM theft_cases
            WHERE latitude IS NULL OR longitude IS NULL
            ORDER BY case_number
            """,
            new { BatchSize = batchSize });

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<GeoCoordinate?> FindCoordinateByRawLocationAsync(string rawLocation, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<CoordinateRow>(
            """
            SELECT TOP 1 latitude, longitude
            FROM theft_cases
            WHERE raw_location = @RawLocation
              AND latitude IS NOT NULL AND longitude IS NOT NULL
            """,
            new { RawLocation = rawLocation });

        return row is null ? null : GeoCoordinate.Create(row.Latitude!.Value, row.Longitude!.Value);
    }

    public async Task UpdateCoordinateAsync(Guid id, GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE theft_cases SET latitude = @Latitude, longitude = @Longitude WHERE id = @Id",
            new { Id = id, coordinate.Latitude, coordinate.Longitude });
    }

    public async Task<int> UpdateCoordinateByLocationAsync(string rawLocation, double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        return await conn.ExecuteAsync(
            "UPDATE theft_cases SET latitude = @Latitude, longitude = @Longitude WHERE raw_location = @RawLocation",
            new { RawLocation = rawLocation, Latitude = latitude, Longitude = longitude });
    }

    public async Task<int> CountMissingCoordinatesAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM theft_cases WHERE latitude IS NULL OR longitude IS NULL");
    }

    public async Task<int> UpdateCaseFieldsAsync(string caseNumber, int caseType, string? occurrenceDateRaw, string? timeSlotRaw, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        var sets = new List<string>();
        var param = new DynamicParameters();
        param.Add("CaseNumber", caseNumber);
        param.Add("CaseType", caseType);

        if (occurrenceDateRaw is not null)
        {
            var td = TaiwanDate.Parse(occurrenceDateRaw);
            sets.Add("occurred_date_raw = @OccurredDateRaw");
            sets.Add("occurred_date = @OccurredDate");
            sets.Add("occurred_year = @OccurredYear");
            param.Add("OccurredDateRaw", td.RawValue);
            param.Add("OccurredDate", td.OccurredOn);
            param.Add("OccurredYear", td.Year);
        }

        if (timeSlotRaw is not null)
        {
            var ts = TimeSlot.Parse(timeSlotRaw);
            sets.Add("time_slot_raw = @TimeSlotRaw");
            sets.Add("time_slot_start = @TimeSlotStart");
            sets.Add("time_slot_end = @TimeSlotEnd");
            param.Add("TimeSlotRaw", ts.RawValue);
            param.Add("TimeSlotStart", ts.StartHour);
            param.Add("TimeSlotEnd", ts.EndHour);
        }

        if (sets.Count == 0) return 0;

        var sql = $"UPDATE theft_cases SET {string.Join(", ", sets)} WHERE case_number = @CaseNumber AND case_type = @CaseType";
        return await conn.ExecuteAsync(sql, param);
    }

    public async Task<IReadOnlyList<(string District, int Count)>> GetDistrictCountsAsync(
        CrimeFilter filter, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT district, COUNT(*) AS weight
            FROM theft_cases WITH (NOLOCK)
            WHERE district IS NOT NULL
              AND (@CaseType IS NULL OR case_type = @CaseType)
              AND (@District IS NULL OR district  = @District)
              AND (@YearFrom IS NULL OR occurred_year >= @YearFrom - 1911)
              AND (@YearTo   IS NULL OR occurred_year <= @YearTo   - 1911)
            GROUP BY district
            """;

        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<DistrictCountRow>(sql, new
        {
            CaseType = filter.CaseType.HasValue ? (int?)filter.CaseType.Value : null,
            District = filter.District?.Name,
            YearFrom = filter.YearFrom,
            YearTo   = filter.YearTo,
        });

        return rows.Select(r => (r.District, r.Weight)).ToList();
    }

    public async Task<(IReadOnlyList<(string District, int Count)> DistrictCounts, IReadOnlyList<(string TimeSlot, int Count)> TimeSlotCounts)> GetStatsByFilterAsync(
        CrimeFilter filter, CancellationToken cancellationToken = default)
    {
        const string districtSql = """
            SELECT district, COUNT(*) AS count
            FROM theft_cases WITH (NOLOCK)
            WHERE district IS NOT NULL
              AND (@CaseType IS NULL OR case_type = @CaseType)
              AND (@District IS NULL OR district  = @District)
              AND (@YearFrom IS NULL OR occurred_year >= @YearFrom - 1911)
              AND (@YearTo   IS NULL OR occurred_year <= @YearTo   - 1911)
            GROUP BY district
            """;

        const string timeSlotSql = """
            SELECT
                RIGHT('0' + CAST(time_slot_start AS VARCHAR(2)), 2) + '~' +
                RIGHT('0' + CAST(time_slot_end   AS VARCHAR(2)), 2) AS time_slot,
                COUNT(*) AS count
            FROM theft_cases WITH (NOLOCK)
            WHERE time_slot_start IS NOT NULL AND time_slot_end IS NOT NULL
              AND (@CaseType IS NULL OR case_type = @CaseType)
              AND (@District IS NULL OR district  = @District)
              AND (@YearFrom IS NULL OR occurred_year >= @YearFrom - 1911)
              AND (@YearTo   IS NULL OR occurred_year <= @YearTo   - 1911)
            GROUP BY time_slot_start, time_slot_end
            """;

        var parameters = new
        {
            CaseType = filter.CaseType.HasValue ? (int?)filter.CaseType.Value : null,
            District = filter.District?.Name,
            YearFrom = filter.YearFrom,
            YearTo   = filter.YearTo,
        };

        var districtTask = Task.Run(async () =>
        {
            await using var conn = CreateConnection();
            return await conn.QueryAsync<StatsDistrictRow>(districtSql, parameters);
        }, cancellationToken);

        var timeSlotTask = Task.Run(async () =>
        {
            await using var conn = CreateConnection();
            return await conn.QueryAsync<StatsTimeSlotRow>(timeSlotSql, parameters);
        }, cancellationToken);

        await Task.WhenAll(districtTask, timeSlotTask);
        var districtRows = await districtTask;
        var timeSlotRows = await timeSlotTask;

        return (
            districtRows.Select(r => (r.District, r.Count)).ToList(),
            timeSlotRows.Select(r => (r.TimeSlot, r.Count)).ToList());
    }

    // ── INSERT SQL ──────────────────────────────────────────────────────

    private const string UpsertSql = """
        MERGE theft_cases AS target
        USING (SELECT @CaseNumber AS case_number, @CaseType AS case_type) AS source
            ON target.case_number = source.case_number AND target.case_type = source.case_type
        WHEN MATCHED THEN
            UPDATE SET
                district          = @District,
                occurred_date_raw = @OccurredDateRaw,
                occurred_date     = @OccurredDate,
                occurred_year     = @OccurredYear,
                time_slot_raw     = @TimeSlotRaw,
                time_slot_start   = @TimeSlotStart,
                time_slot_end     = @TimeSlotEnd,
                raw_location      = @RawLocation,
                imported_at       = @ImportedAt
        WHEN NOT MATCHED THEN
            INSERT (id, case_number, case_type, district,
                    occurred_date_raw, occurred_date, occurred_year,
                    time_slot_raw, time_slot_start, time_slot_end,
                    raw_location, latitude, longitude,
                    imported_at, created_at)
            VALUES (@Id, @CaseNumber, @CaseType, @District,
                    @OccurredDateRaw, @OccurredDate, @OccurredYear,
                    @TimeSlotRaw, @TimeSlotStart, @TimeSlotEnd,
                    @RawLocation, @Latitude, @Longitude,
                    @ImportedAt, @CreatedAt);
        """;

    // ── Row ↔ Domain 轉換 ────────────────────────────────────────────────

    private sealed record TheftCaseRow
    {
        public Guid Id { get; init; }
        public string CaseNumber { get; init; } = string.Empty;
        public int TotalCount { get; init; }
        public int? CaseType { get; init; }
        public string? District { get; init; }
        public string OccurredDateRaw { get; init; } = string.Empty;
        public DateOnly? OccurredDate { get; init; }
        public int? OccurredYear { get; init; }
        public string? TimeSlotRaw { get; init; }
        public int? TimeSlotStart { get; init; }
        public int? TimeSlotEnd { get; init; }
        public string RawLocation { get; init; } = string.Empty;
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public DateTimeOffset ImportedAt { get; init; }
        public DateTimeOffset CreatedAt { get; init; }

        public TheftCase ToDomain() => TheftCase.Reconstitute(
            id: Id,
            caseNumber: CaseNumber,
            caseType: CaseType.HasValue ? (CaseType?)CaseType.Value : null,
            district: District is not null ? Domain.ValueObjects.District.ParseFrom(District) : null,
            occurredDate: TaiwanDate.Parse(OccurredDateRaw),
            timeSlot: TimeSlotRaw is not null ? TimeSlot.Parse(TimeSlotRaw) : null,
            rawLocation: RawLocation,
            coordinate: Latitude.HasValue && Longitude.HasValue
                               ? GeoCoordinate.Create(Latitude.Value, Longitude.Value)
                               : null,
            importedAt: ImportedAt);
    }

    private static object ToRow(TheftCase t) => new
    {
        t.Id,
        t.CaseNumber,
        CaseType = t.CaseType.HasValue ? (int?)t.CaseType.Value : null,
        District = t.District?.Name,
        OccurredDateRaw = t.OccurredDate.RawValue,
        OccurredDate = t.OccurredDate.OccurredOn,
        OccurredYear = t.OccurredDate.Year,
        TimeSlotRaw = t.TimeSlot?.RawValue,
        TimeSlotStart = t.TimeSlot?.StartHour,
        TimeSlotEnd = t.TimeSlot?.EndHour,
        t.RawLocation,
        Latitude = t.Coordinate?.Latitude,
        Longitude = t.Coordinate?.Longitude,
        t.ImportedAt,
        t.CreatedAt
    };

    private static double CalculateDistanceKm(GeoCoordinate a, GeoCoordinate b)
    {
        const double R = 6371.0;
        var dLat = (b.Latitude - a.Latitude) * Math.PI / 180.0;
        var dLng = (b.Longitude - a.Longitude) * Math.PI / 180.0;
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(a.Latitude * Math.PI / 180.0) *
                   Math.Cos(b.Latitude * Math.PI / 180.0) *
                   Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
    }

    private sealed record DistrictCountRow
    {
        public string District { get; init; } = string.Empty;
        public int Weight { get; init; }
    }

    private sealed record StatsDistrictRow
    {
        public string District { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    private sealed record StatsTimeSlotRow
    {
        public string TimeSlot { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    private sealed record CoordinateRow
    {
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
    }
}
