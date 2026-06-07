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
        var (cases, _) = await GetPagedByFilterAsync(filter, page: 1, pageSize: int.MaxValue, cancellationToken);
        return cases;
    }

    public async Task<(IReadOnlyList<TheftCase> Cases, int Total)> GetPagedByFilterAsync(
        CrimeFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
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
                PageSize       = pageSize
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
        await conn.ExecuteAsync(InsertSql, ToRow(theftCase));
    }

    public async Task AddRangeAsync(IEnumerable<TheftCase> theftCases, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        await conn.ExecuteAsync(InsertSql, theftCases.Select(ToRow));
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM theft_cases");
    }

    // ── INSERT SQL ──────────────────────────────────────────────────────

    private const string InsertSql = """
        INSERT INTO theft_cases (
            id, case_number, case_type, district,
            occurred_date_raw, occurred_date, occurred_year,
            time_slot_raw, time_slot_start, time_slot_end,
            raw_location, latitude, longitude,
            imported_at, created_at)
        VALUES (
            @Id, @CaseNumber, @CaseType, @District,
            @OccurredDateRaw, @OccurredDate, @OccurredYear,
            @TimeSlotRaw, @TimeSlotStart, @TimeSlotEnd,
            @RawLocation, @Latitude, @Longitude,
            @ImportedAt, @CreatedAt)
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
}
