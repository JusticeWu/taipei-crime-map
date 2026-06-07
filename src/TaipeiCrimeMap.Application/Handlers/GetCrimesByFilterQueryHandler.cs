using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Exceptions;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Handlers;

public class GetCrimesByFilterQueryHandler
{
    private readonly ICrimeRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<GetCrimesByFilterQueryHandler> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public GetCrimesByFilterQueryHandler(
        ICrimeRepository repository,
        IDistributedCache cache,
        ILogger<GetCrimesByFilterQueryHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TheftCaseDto>> HandleAsync(
        GetCrimesByFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var district = string.IsNullOrWhiteSpace(query.DistrictName)
            ? null
            : District.ParseFrom(query.DistrictName);

        var timeSlot = string.IsNullOrWhiteSpace(query.RawTimeSlot)
            ? null
            : TimeSlot.Parse(query.RawTimeSlot);

        if (!string.IsNullOrWhiteSpace(query.RawTimeSlot) && timeSlot?.StartHour is null)
            throw new DomainException($"時段格式不正確：{query.RawTimeSlot}");

        var filter = new CrimeFilter(
            caseType: query.CaseType,
            district: district,
            yearFrom: query.YearFrom,
            yearTo: query.YearTo,
            timeSlot: timeSlot);

        var cacheKey = $"crimes:filter:{query.CaseType}:{query.DistrictName}:{query.YearFrom}:{query.YearTo}:{query.RawTimeSlot}";

        var cachedBytes = await _cache.GetAsync(cacheKey, cancellationToken);
        if (cachedBytes is not null)
        {
            _logger.LogInformation("快取命中：{CacheKey}", cacheKey);
            return JsonSerializer.Deserialize<List<TheftCaseDto>>(cachedBytes)!;
        }

        _logger.LogInformation("查詢案件: 條件：{Query}", query);
        var cases = await _repository.GetByFilterAsync(filter, cancellationToken);
        _logger.LogInformation("查詢完成，共 {Count} 筆", cases.Count);

        var result = cases.Select(MapToDto).ToList();

        await _cache.SetAsync(
            cacheKey,
            JsonSerializer.SerializeToUtf8Bytes(result),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration },
            cancellationToken);

        return result;
    }

    private static TheftCaseDto MapToDto(TheftCase c) => new()
    {
        Id = c.Id,
        CaseNumber = c.CaseNumber,
        CaseType = c.CaseType?.ToChineseName(),
        District = c.District?.Name,
        OccurredDate = c.OccurredDate?.OccurredOn?.ToString("yyyy-MM-dd"),
        TimeSlot = c.TimeSlot?.Normalize(),
        RawLocation = c.RawLocation,
        Latitude = c.Coordinate?.Latitude,
        Longitude = c.Coordinate?.Longitude,
        IsDataComplete = c.IsDataComplete
    };
}
