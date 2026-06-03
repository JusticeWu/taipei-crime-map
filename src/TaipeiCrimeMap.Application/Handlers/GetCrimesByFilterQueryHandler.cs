using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
    private readonly IMemoryCache _cache;
    private readonly ILogger<GetCrimesByFilterQueryHandler> _logger;
    
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    public GetCrimesByFilterQueryHandler(ICrimeRepository repository, IMemoryCache cache, ILogger<GetCrimesByFilterQueryHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TheftCaseDto>> HandleAsync(GetCrimesByFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var district = string.IsNullOrWhiteSpace(query.DistrictName) ? null : District.ParseFrom(query.DistrictName);

        var timeSlot = string.IsNullOrWhiteSpace(query.RawTimeSlot) ? null : TimeSlot.Parse(query.RawTimeSlot);

        if (!string.IsNullOrWhiteSpace(query.RawTimeSlot) && timeSlot?.StartHour is null)
        {
            throw new DomainException($"時段格式不正確：{query.RawTimeSlot}");
        }

        var filter = new CrimeFilter(
            caseType: query.CaseType,
            district: district,
            yearFrom: query.YearFrom,
            yearTo: query.YearTo,
            timeSlot: timeSlot);

        var cacheKey = $"crimes:filter:{query.CaseType}:{query.DistrictName}:{query.YearFrom}:{query.YearTo}:{query.RawTimeSlot}";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<TheftCaseDto>? cached))
        {
            _logger.LogInformation("快取命中：{CacheKey}", cacheKey);
            return cached!;
        }

        _logger.LogInformation("查詢案件: 條件：{Query}", query);

        var cases = await _repository.GetByFilterAsync(filter, cancellationToken);

        _logger.LogInformation("查詢完成，共 {Count} 筆", cases.Count);

        var result = cases.Select(c => MapToDto(c)).ToList();

        _cache.Set(cacheKey, result, CacheDuration);

        return result;
    }

    private static TheftCaseDto MapToDto(TheftCase c)
    {
        return new TheftCaseDto
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
}