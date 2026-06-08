namespace TaipeiCrimeMap.Application.DTOs;

public record PagedResult<T>(
    IReadOnlyList<T> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);
