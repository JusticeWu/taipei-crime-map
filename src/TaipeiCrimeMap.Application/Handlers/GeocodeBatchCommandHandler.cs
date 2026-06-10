using Microsoft.Extensions.Logging;
using TaipeiCrimeMap.Application.Commands;
using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.Services;

namespace TaipeiCrimeMap.Application.Handlers;

public class GeocodeBatchCommandHandler
{
    private readonly ICrimeRepository _crimeRepository;
    private readonly IGeocodingService _geocodingService;
    private readonly ILogger<GeocodeBatchCommandHandler> _logger;

    public GeocodeBatchCommandHandler(
        ICrimeRepository crimeRepository,
        IGeocodingService geocodingService,
        ILogger<GeocodeBatchCommandHandler> logger)
    {
        _crimeRepository = crimeRepository;
        _geocodingService = geocodingService;
        _logger = logger;
    }

    public async Task<GeocodeBatchResult> HandleAsync(GeocodeBatchCommand command, CancellationToken cancellationToken = default)
    {
        var batchSize = command.BatchSize > 0 ? command.BatchSize : 10;

        var cases = await _crimeRepository.GetCasesWithMissingCoordinatesAsync(batchSize, cancellationToken);

        var reusedCount = 0;
        var apiCallCount = 0;
        var failedCount = 0;

        foreach (var theftCase in cases)
        {
            var coordinate = await _crimeRepository.FindCoordinateByRawLocationAsync(theftCase.RawLocation, cancellationToken);

            if (coordinate is not null)
            {
                reusedCount++;
            }
            else
            {
                coordinate = await _geocodingService.GeocodeAsync(theftCase.RawLocation, cancellationToken);
                apiCallCount++;

                if (coordinate is null)
                {
                    failedCount++;
                    _logger.LogWarning("Geocoding 失敗，跳過：{CaseNumber} {RawLocation}",
                        theftCase.CaseNumber, theftCase.RawLocation);
                    continue;
                }
            }

            theftCase.UpdateCoordinate(coordinate);
            await _crimeRepository.UpdateCoordinateAsync(theftCase.Id, coordinate, cancellationToken);
        }

        var remainingCount = await _crimeRepository.CountMissingCoordinatesAsync(cancellationToken);

        _logger.LogInformation(
            "Geocoding 批次完成：處理 {ProcessedCount} 筆，複用 {ReusedCount} 筆，呼叫 API {ApiCallCount} 次，失敗 {FailedCount} 筆，剩餘 {RemainingCount} 筆",
            cases.Count, reusedCount, apiCallCount, failedCount, remainingCount);

        return new GeocodeBatchResult
        {
            ProcessedCount = cases.Count,
            ReusedCount = reusedCount,
            ApiCallCount = apiCallCount,
            FailedCount = failedCount,
            RemainingCount = remainingCount
        };
    }
}
