namespace TaipeiCrimeMap.Application.DTOs;

/// <summary>
/// Slim DTO for point-mode map rendering — only fields required for
/// display, statistics, charts, and the marker popup. Omits id,
/// caseNumber, and isDataComplete to reduce payload size.
/// </summary>
public record PointCrimeDto(
    double? Latitude,
    double? Longitude,
    string? CaseType,
    string? District,
    string? OccurredDate,
    string? TimeSlot,
    string? RawLocation);
