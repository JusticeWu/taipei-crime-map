using TaipeiCrimeMap.Domain.Common;
using TaipeiCrimeMap.Domain.Events;
using TaipeiCrimeMap.Domain.Exceptions;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Domain.Aggregates;

public sealed class TheftCase : AggregateRoot
{
    public string CaseNumber { get; private set; }
    public CaseType? CaseType { get; private set; }
    public District? District { get; private set; }
    public TaiwanDate OccurredDate { get; private set; }
    public TimeSlot? TimeSlot { get; private set; }
    public string RawLocation { get; private set; }
    public GeoCoordinate? Coordinate { get; private set; }

    public bool IsDataComplete =>
        CaseType is not null &&
        OccurredDate is not null && OccurredDate.IsDataComplete &&
        TimeSlot is not null && TimeSlot.StartHour.HasValue && TimeSlot.EndHour.HasValue &&
        District is not null && District.IsValid() &&
        RawLocation is not null && Coordinate is not null;

    public DateTimeOffset ImportedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Parameterless ctor for EF / serialisation
    private TheftCase()
    {
        CaseNumber = string.Empty;
        RawLocation = string.Empty;
        District = null!;
        OccurredDate = null!;
        TimeSlot = null!;
    }

    private TheftCase(
        Guid id,
        string caseNumber,
        CaseType? caseType,
        District? district,
        TaiwanDate occurredDate,
        TimeSlot? timeSlot,
        string rawLocation,
        GeoCoordinate? coordinate,
        DateTimeOffset importedAt) : base(id)
    {
        CaseNumber = caseNumber;
        CaseType = caseType;
        District = district;
        OccurredDate = occurredDate;
        TimeSlot = timeSlot;
        RawLocation = rawLocation;
        Coordinate = coordinate;
        ImportedAt = importedAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static TheftCase Create(
        string caseNumber,
        CaseType? caseType,
        District? district,
        TaiwanDate occurredDate,
        TimeSlot? timeSlot,
        string rawLocation,
        GeoCoordinate? coordinate = null,
        DateTimeOffset? importedAt = null)
    {
        if (string.IsNullOrWhiteSpace(rawLocation))
            throw new DomainException("RawLocation cannot be empty.");

        if (occurredDate is null)
            throw new DomainException("OccurredDate cannot be null.");

        var theftCase = new TheftCase(
            Guid.NewGuid(),
            caseNumber,
            caseType,
            district,
            occurredDate,
            timeSlot,
            rawLocation,
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
