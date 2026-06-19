using Microsoft.Extensions.Logging;
using TaipeiCrimeMap.Application.Commands;
using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.Services;

namespace TaipeiCrimeMap.Application.Handlers;

public class GeocodeMissingCommandHandler
{
    private readonly ICrimeRepository _crimeRepository;
    private readonly IGeocodingService _geocodingService;
    private readonly ILogger<GeocodeMissingCommandHandler> _logger;

    public GeocodeMissingCommandHandler(
        ICrimeRepository crimeRepository,
        IGeocodingService geocodingService,
        ILogger<GeocodeMissingCommandHandler> logger)
    {
        _crimeRepository = crimeRepository;
        _geocodingService = geocodingService;
        _logger = logger;
    }

    public async Task<GeocodeMissingResult> HandleAsync(GeocodeMissingCommand command, CancellationToken cancellationToken = default)
    {
        var batchSize = command.MaxCount > 0 ? command.MaxCount.Value : int.MaxValue;
        var delayMs = command.DelayBetweenCallsMs;

        var cases = await _crimeRepository.GetCasesWithMissingCoordinatesAsync(batchSize, cancellationToken);

        var items = new List<GeocodeMissingItemResult>();
        var successCount = 0;
        var reusedCount = 0;
        var failedCount = 0;
        var apiCallCount = 0;

        foreach (var theftCase in cases)
        {
            var coordinate = await _crimeRepository.FindCoordinateByRawLocationAsync(theftCase.RawLocation, cancellationToken);

            if (coordinate is not null)
            {
                reusedCount++;
                successCount++;
                theftCase.UpdateCoordinate(coordinate);
                await _crimeRepository.UpdateCoordinateAsync(theftCase.Id, coordinate, cancellationToken);

                items.Add(new GeocodeMissingItemResult
                {
                    Id = theftCase.Id,
                    CaseType = theftCase.CaseType?.ToChineseName(),
                    CaseNumber = theftCase.CaseNumber,
                    RawLocation = theftCase.RawLocation,
                    Success = true,
                    Latitude = coordinate.Latitude,
                    Longitude = coordinate.Longitude,
                });
                continue;
            }

            if (apiCallCount > 0 && delayMs > 0)
                await Task.Delay(delayMs, cancellationToken);

            coordinate = await _geocodingService.GeocodeAsync(theftCase.RawLocation, cancellationToken);
            apiCallCount++;

            if (coordinate is null)
            {
                failedCount++;
                _logger.LogWarning("Geocoding 失敗，跳過：{CaseNumber} {RawLocation}",
                    theftCase.CaseNumber, theftCase.RawLocation);

                items.Add(new GeocodeMissingItemResult
                {
                    Id = theftCase.Id,
                    CaseType = theftCase.CaseType?.ToChineseName(),
                    CaseNumber = theftCase.CaseNumber,
                    RawLocation = theftCase.RawLocation,
                    Success = false,
                    FailureReason = "Geocoding 回傳 null（可能為 ZERO_RESULTS、配額已用盡、或地址無法辨識）",
                });
                continue;
            }

            successCount++;
            theftCase.UpdateCoordinate(coordinate);
            await _crimeRepository.UpdateCoordinateAsync(theftCase.Id, coordinate, cancellationToken);

            items.Add(new GeocodeMissingItemResult
            {
                Id = theftCase.Id,
                CaseType = theftCase.CaseType?.ToChineseName(),
                CaseNumber = theftCase.CaseNumber,
                RawLocation = theftCase.RawLocation,
                Success = true,
                Latitude = coordinate.Latitude,
                Longitude = coordinate.Longitude,
            });
        }

        var remainingCount = await _crimeRepository.CountMissingCoordinatesAsync(cancellationToken);

        _logger.LogInformation(
            "Geocode missing 完成：處理 {Total} 筆，成功 {Success}（複用 {Reused}），失敗 {Failed}，剩餘 {Remaining}",
            cases.Count, successCount, reusedCount, failedCount, remainingCount);

        return new GeocodeMissingResult
        {
            TotalProcessed = cases.Count,
            SuccessCount = successCount,
            ReusedCount = reusedCount,
            FailedCount = failedCount,
            RemainingCount = remainingCount,
            Items = items,
        };
    }
}
