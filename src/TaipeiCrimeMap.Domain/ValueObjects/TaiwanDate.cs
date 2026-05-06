using TaipeiCrimeMap.Domain.Common;

namespace TaipeiCrimeMap.Domain.ValueObjects;

/// <summary>
/// Represents a Republic of China (民國) date encoded as a 7-digit string YYYMMDD.
/// Example: "1130101" → ROC year 113, January 1st → 2024-01-01.
/// OccurredOn and Year are null when the raw value cannot be parsed.
/// </summary>
public sealed class TaiwanDate : ValueObject
{
    private const int RocEpoch = 1911;
    private const int MinYear = 100; // ROC year 100 ≈ Gregorian 2011
    private const int MaxYear = 200; // ROC year 200 ≈ Gregorian 2111

    public string RawValue { get; }
    public bool IsDataComplete { get; }
    public DateOnly? OccurredOn { get; }
    public int? Year { get; }

    private TaiwanDate(string rawValue, DateOnly? occurredOn, int? year)
    {
        RawValue = rawValue;
        OccurredOn = occurredOn;
        Year = year;
        IsDataComplete = occurredOn.HasValue;
    }

    public static TaiwanDate Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new TaiwanDate(raw ?? string.Empty, null, null);

        var trimmed = raw.Trim();

        if (trimmed.Length != 7 || !int.TryParse(trimmed, out _))
            return new TaiwanDate(trimmed, null, null);

        if (!int.TryParse(trimmed[..3], out var rocYear) ||
            !int.TryParse(trimmed[3..5], out var month) ||
            !int.TryParse(trimmed[5..], out var day))
            return new TaiwanDate(trimmed, null, null);

        if (rocYear < MinYear || rocYear > MaxYear)
            return new TaiwanDate(trimmed, null, null);

        var gregorianYear = rocYear + RocEpoch;

        if (!DateOnly.TryParseExact(
                $"{gregorianYear:D4}-{month:D2}-{day:D2}",
                "yyyy-MM-dd",
                out var date))
            return new TaiwanDate(trimmed, null, null);

        return new TaiwanDate(trimmed, date, rocYear);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RawValue;
    }

    public override string ToString() =>
        OccurredOn.HasValue ? OccurredOn.Value.ToString("yyyy-MM-dd") : RawValue;
}
