namespace TaipeiCrimeMap.Application.DTOs;

/// <summary>
/// Slim DTO for point-mode map rendering — only fields required for
/// display and map rendering. Omits caseNumber, district, timeSlot,
/// rawLocation, and isDataComplete to reduce payload size; those are
/// fetched on demand via /api/crime/points/{id} when a marker is clicked.
/// </summary>
public record PointCrimeDto(
    Guid Id,
    double? Latitude,
    double? Longitude,
    string? CaseType,
    string? OccurredDate);
