namespace TaipeiCrimeMap.Application.DTOs;

/// <summary>
/// Slim DTO for point-mode map rendering — only fields required for
/// display, statistics, and charts. Omits id, caseNumber, district,
/// timeSlot, rawLocation, and isDataComplete to reduce payload size.
/// </summary>
public record PointCrimeDto(
    double? Latitude,
    double? Longitude,
    string? CaseType,
    string? OccurredDate);
