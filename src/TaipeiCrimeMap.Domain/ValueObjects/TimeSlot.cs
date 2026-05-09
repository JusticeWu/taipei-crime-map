using TaipeiCrimeMap.Domain.Common;

namespace TaipeiCrimeMap.Domain.ValueObjects;

/// <summary>
/// Represents a crime occurrence time window, e.g. "00-02" means 00:00–02:00.
/// StartHour and EndHour are null when the raw value cannot be parsed.
/// </summary>
public sealed class TimeSlot : ValueObject
{
    public string RawValue { get; }
    public int? StartHour { get; }
    public int? EndHour { get; }

    private TimeSlot(string rawValue, int? startHour, int? endHour)
    {
        RawValue = rawValue;
        StartHour = startHour;
        EndHour = endHour;
    }

    public static TimeSlot Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new TimeSlot(raw ?? string.Empty, null, null);   // 合法輸入 

        var trimmed = raw.Trim();
        var separator = trimmed.Contains('~') ? '~' : '-';
        var parts = trimmed.Split(separator);

        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out var start) ||
            !int.TryParse(parts[1], out var end) ||
            start is < 0 or > 23 ||
            end is < 0 or > 24 ||
            start > end)
            return new TimeSlot(trimmed, null, null);

        return new TimeSlot(trimmed, start, end);
    }

    /// <summary>Returns a canonical "HH~HH" string, or RawValue when not parseable.</summary>
    public string Normalize() =>
        StartHour.HasValue && EndHour.HasValue
            ? $"{StartHour:D2}~{EndHour:D2}"
            : RawValue;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RawValue;
    }

    public override string ToString() => Normalize();
}
