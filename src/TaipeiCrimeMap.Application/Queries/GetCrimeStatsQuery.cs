using TaipeiCrimeMap.Domain.Aggregates;

namespace TaipeiCrimeMap.Application.Queries;

public record GetCrimeStatsQuery(
    CaseType? CaseType = null,
    string? DistrictName = null,
    int? YearFrom = null,
    int? YearTo = null);
