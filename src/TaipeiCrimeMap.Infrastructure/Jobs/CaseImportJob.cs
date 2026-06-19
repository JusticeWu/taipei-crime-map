using System.Text.Json.Serialization;

namespace TaipeiCrimeMap.Infrastructure.Jobs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CaseImportJobStatus
{
    Pending,
    Success,
    Failure
}

public sealed class CaseImportJob
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public int CaseNumber { get; set; }
    public string CaseType { get; set; } = string.Empty;
    public int OccurrenceDate { get; set; }
    public string? TimeSlot { get; set; }
    public string? RawLocation { get; set; }
    public CaseImportJobStatus Status { get; set; } = CaseImportJobStatus.Pending;
    public bool HasCoordinate { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset NextRetryAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
