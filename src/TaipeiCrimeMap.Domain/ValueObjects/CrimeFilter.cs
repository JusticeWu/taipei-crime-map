using TaipeiCrimeMap.Domain.Aggregates.TheftCase;
using TaipeiCrimeMap.Domain.Exceptions;

namespace TaipeiCrimeMap.Domain.ValueObjects;

/// <summary>
/// Immutable query filter for crime case searches.
/// Year values must not exceed YearTo when both are specified.
/// </summary>
public sealed class CrimeFilter
{
    public CaseType? CaseType { get; }
    public District? District { get; }
    public int? YearFrom { get; }
    public int? YearTo { get; }
    public TimeSlot? TimeSlot { get; }

    public CrimeFilter(
        CaseType? caseType = null,
        District? district = null,
        int? yearFrom = null,
        int? yearTo = null,
        TimeSlot? timeSlot = null)
    {
        if (yearFrom.HasValue && yearTo.HasValue && yearFrom > yearTo)
            throw new DomainException("YearFrom 不能大於 YearTo");

        CaseType = caseType;
        District = district;
        YearFrom = yearFrom;
        YearTo = yearTo;
        TimeSlot = timeSlot;
    }
}
