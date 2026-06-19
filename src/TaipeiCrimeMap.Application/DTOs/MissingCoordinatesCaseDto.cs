namespace TaipeiCrimeMap.Application.DTOs;

public record MissingCoordinatesCaseDto
{
    public Guid Id { get; init; }
    public string? CaseType { get; init; }
    public int CaseNumber { get; init; }
    public string RawLocation { get; init; } = string.Empty;
}
