namespace TaipeiCrimeMap.Application.DTOs;

public record GeocodeMissingResult
{
    public int TotalProcessed { get; init; }
    public int SuccessCount { get; init; }
    public int ReusedCount { get; init; }
    public int FailedCount { get; init; }
    public int RemainingCount { get; init; }
    public List<GeocodeMissingItemResult> Items { get; init; } = new();
}

public record GeocodeMissingItemResult
{
    public Guid Id { get; init; }
    public string? CaseType { get; init; }
    public int CaseNumber { get; init; }
    public string RawLocation { get; init; } = string.Empty;
    public bool Success { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? FailureReason { get; init; }
}
