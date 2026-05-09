using TaipeiCrimeMap.Domain.Common;
using TaipeiCrimeMap.Domain.Events;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Domain.Aggregates.TheftCase;

public sealed class TheftCase : AggregateRoot
{
    public string CaseNumber { get; private set; }
    public CaseType? CaseType { get; private set; }
    public District District { get; private set; }
    public TaiwanDate OccurredDate { get; private set; }
    public TimeSlot TimeSlot { get; private set; }
    public GeoCoordinate? Coordinate { get; private set; }

    /// <summary>True when the case has been geocoded (Coordinate is populated).</summary>
    public bool IsDataComplete => Coordinate is not null;

    public DateTimeOffset ImportedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Parameterless ctor for EF / serialisation
    private TheftCase()
    {
        CaseNumber = string.Empty;
        District = null!;
        OccurredDate = null!;
        TimeSlot = null!;
    }

    private TheftCase(
        Guid id,
        string caseNumber,
        CaseType caseType,
        District district,
        TaiwanDate occurredDate,
        TimeSlot timeSlot,
        GeoCoordinate? coordinate,
        DateTimeOffset importedAt) : base(id)
    {
        CaseNumber = caseNumber;
        CaseType = caseType;
        District = district;
        OccurredDate = occurredDate;
        TimeSlot = timeSlot;
        Coordinate = coordinate;
        ImportedAt = importedAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static TheftCase Create(
        string caseNumber,
        CaseType caseType,
        District district,
        TaiwanDate occurredDate,
        TimeSlot timeSlot,
        GeoCoordinate? coordinate = null,
        DateTimeOffset? importedAt = null)
    {
        if (string.IsNullOrWhiteSpace(caseNumber))
            throw new ArgumentException("Case number cannot be empty.", nameof(caseNumber));

        ArgumentNullException.ThrowIfNull(district);
        ArgumentNullException.ThrowIfNull(occurredDate);
        ArgumentNullException.ThrowIfNull(timeSlot);

        var theftCase = new TheftCase(
            Guid.NewGuid(),
            caseNumber,
            caseType,
            district,
            occurredDate,
            timeSlot,
            coordinate,
            importedAt ?? DateTimeOffset.UtcNow);

        theftCase.AddDomainEvent(new TheftCaseCreatedEvent(theftCase.Id, caseNumber));
        return theftCase;
    }

    public void UpdateCoordinate(GeoCoordinate coordinate)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        Coordinate = coordinate;
    }
}
