using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Domain.Services;

/// <summary>
/// Domain service that extracts a Taipei <see cref="District"/> from a raw address string.
/// Handles mixed 台/臺 usage and common prefix patterns such as "台北市" or "臺北市".
/// </summary>
public sealed class DistrictParser
{
    private static readonly IReadOnlyDictionary<string, string> _aliases =
        new Dictionary<string, string>
        {
            // 台 → 臺 variants that appear in district names if any arise in the future
            // Currently no district name itself contains 台/臺,
            // so aliases map city-level prefix forms only.
            ["台北市"] = "臺北市",
            ["台北"]   = "臺北",
        };

    /// <summary>
    /// Parses a <see cref="District"/> from <paramref name="location"/>.
    /// Returns <c>null</c> when no valid Taipei district can be identified.
    /// </summary>
    public District? Parse(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return null;

        var normalised = Normalise(location);

        // Strip city prefix in various forms
        foreach (var prefix in new[] { "臺北市", "臺北" })
        {
            if (normalised.StartsWith(prefix))
            {
                normalised = normalised[prefix.Length..];
                break;
            }
        }

        // Try each valid district name
        foreach (var name in District.ValidDistricts)
        {
            if (normalised.Contains(name))
                return District.ParseFrom(name)!;
        }

        return null;
    }

    private static string Normalise(string input)
    {
        var result = input;
        foreach (var (alias, canonical) in _aliases)
            result = result.Replace(alias, canonical);
        return result;
    }
}
