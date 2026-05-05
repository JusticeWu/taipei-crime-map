using TaipeiCrimeMap.Domain.Aggregates.TheftCase;

namespace TaipeiCrimeMap.Domain.ValueObjects;

/// <summary>
/// Immutable query filter for crime case searches.
/// Year values must be between 2015 and 2026 (Gregorian), and YearFrom must not exceed YearTo.
/// </summary>
public sealed class CrimeFilter
{
    private const int MinYear = 2015;
    private const int MaxYear = 2026;

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
        if (yearFrom.HasValue && (yearFrom < MinYear || yearFrom > MaxYear))
            throw new ArgumentOutOfRangeException(nameof(yearFrom), $"YearFrom must be between {MinYear} and {MaxYear}.");

        if (yearTo.HasValue && (yearTo < MinYear || yearTo > MaxYear))
            throw new ArgumentOutOfRangeException(nameof(yearTo), $"YearTo must be between {MinYear} and {MaxYear}.");

        if (yearFrom.HasValue && yearTo.HasValue && yearFrom > yearTo)
            throw new ArgumentException("YearFrom must not be greater than YearTo.");

        CaseType = caseType;
        District = district;
        YearFrom = yearFrom;
        YearTo = yearTo;
        TimeSlot = timeSlot;
    }
}
