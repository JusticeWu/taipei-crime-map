namespace TaipeiCrimeMap.Application.DTOs;

public record TheftCaseDto
{
    public Guid Id { get; init; }
    public int CaseNumber { get; init; }
    public string? CaseType { get; init; }
    public string? District { get; init; }
    public string? OccurredDate { get; init; }
    public string? TimeSlot { get; init; }
    public string RawLocation { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public bool IsDataComplete { get; init; }
}