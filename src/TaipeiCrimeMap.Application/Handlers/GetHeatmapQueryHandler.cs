using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Handlers;

public class GetHeatmapQueryHandler
{
    private readonly ICrimeRepository _repository;

    private static readonly IReadOnlyDictionary<string, (double Lat, double Lng)> Centroids =
        new Dictionary<string, (double, double)>
        {
            ["中正區"] = (25.0328, 121.5199),
            ["大同區"] = (25.0637, 121.5131),
            ["中山區"] = (25.0694, 121.5326),
            ["松山區"] = (25.0499, 121.5776),
            ["大安區"] = (25.0266, 121.5432),
            ["萬華區"] = (25.0333, 121.4981),
            ["信義區"] = (25.0330, 121.5654),
            ["士林區"] = (25.0934, 121.5241),
            ["北投區"] = (25.1317, 121.4988),
            ["內湖區"] = (25.0831, 121.5874),
            ["南港區"] = (25.0549, 121.6076),
            ["文山區"] = (24.9989, 121.5699),
        };

    public GetHeatmapQueryHandler(ICrimeRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<HeatmapPointDto>> HandleAsync(
        GetHeatmapQuery query,
        CancellationToken cancellationToken = default)
    {
        var district = string.IsNullOrWhiteSpace(query.DistrictName)
            ? null
            : District.ParseFrom(query.DistrictName);

        var filter = new CrimeFilter(
            caseType: query.CaseType,
            district: district,
            yearFrom: query.YearFrom,
            yearTo: query.YearTo);

        var counts = await _repository.GetDistrictCountsAsync(filter, cancellationToken);

        return counts
            .Where(c => Centroids.ContainsKey(c.District))
            .Select(c => new HeatmapPointDto(
                Lat:      Centroids[c.District].Lat,
                Lng:      Centroids[c.District].Lng,
                Weight:   c.Count,
                District: c.District))
            .ToList();
    }
}
