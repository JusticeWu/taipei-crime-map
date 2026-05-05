using TaipeiCrimeMap.Domain.Common;

namespace TaipeiCrimeMap.Domain.ValueObjects;

public sealed class District : ValueObject
{
    private static readonly IReadOnlyList<string> _valid =
    [
        "中正區", "大同區", "中山區", "松山區", "大安區", "萬華區",
        "信義區", "士林區", "北投區", "內湖區", "南港區", "文山區"
    ];

    public static IReadOnlyList<string> ValidDistricts => _valid;

    public string Name { get; }

    private District(string name) => Name = name;

    // EF / ORM parameterless ctor
    private District() => Name = string.Empty;

    /// <summary>
    /// Attempts to extract a Taipei district from an arbitrary location string.
    /// Returns null when no valid district is found.
    /// </summary>
    public static District? ParseFrom(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return null;

        // Normalise 台/臺 so both city-prefix variants work
        var normalised = location
            .Replace("台北市", "臺北市")
            .Replace("台北", "臺北");

        // Strip leading 臺北市 prefix if present
        var stripped = normalised.StartsWith("臺北市")
            ? normalised["臺北市".Length..]
            : normalised;

        foreach (var district in _valid)
        {
            if (stripped.Contains(district) || normalised.Contains(district))
                return new District(district);
        }

        return null;
    }

    public bool IsValid() => _valid.Contains(Name);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
    }

    public override string ToString() => Name;
}
