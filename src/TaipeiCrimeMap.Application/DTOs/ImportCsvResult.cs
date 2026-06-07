namespace TaipeiCrimeMap.Application.DTOs;

public record ImportCsvResult
{
    public int SuccessCount { get; init; }
    public int SkippedCount { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string CaseType { get; init; } = string.Empty;
}