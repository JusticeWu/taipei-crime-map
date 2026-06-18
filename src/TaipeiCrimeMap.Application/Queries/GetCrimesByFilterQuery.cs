using TaipeiCrimeMap.Domain.Aggregates;

namespace TaipeiCrimeMap.Application.Queries;

public record GetCrimesByFilterQuery(
    CaseType? CaseType = null,
    string? DistrictName = null,
    int? YearFrom = null,
    int? YearTo = null,
    string? RawTimeSlot = null,
    int Page = 1,
    int PageSize = 200,
    string? SortBy = null,
    string? SortOrder = null);