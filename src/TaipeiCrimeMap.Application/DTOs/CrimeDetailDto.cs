namespace TaipeiCrimeMap.Application.DTOs;

/// <summary>
/// Detail fields for a single case, fetched on demand when a point-mode
/// marker popup is opened (not included in the slim PointCrimeDto list).
/// </summary>
public record CrimeDetailDto(
    Guid Id,
    string? CaseType,
    string? District,
    string? TimeSlot,
    string RawLocation,
    string? OccurredDate);
