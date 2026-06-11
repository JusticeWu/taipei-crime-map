using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;

namespace TaipeiCrimeMap.Application.Handlers;

/// <summary>
/// 點位圖 popup 點擊後查詢單筆案件詳細資料。資料量小、命中率分散，不走快取。
/// </summary>
public class GetCrimeByIdQueryHandler
{
    private readonly ICrimeRepository _repository;

    public GetCrimeByIdQueryHandler(ICrimeRepository repository)
    {
        _repository = repository;
    }

    public async Task<CrimeDetailDto?> HandleAsync(
        GetCrimeByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var theftCase = await _repository.GetByIdAsync(query.Id, cancellationToken);
        if (theftCase is null) return null;

        return new CrimeDetailDto(
            Id: theftCase.Id,
            CaseType: theftCase.CaseType?.ToChineseName(),
            District: theftCase.District?.Name,
            TimeSlot: theftCase.TimeSlot?.Normalize(),
            RawLocation: theftCase.RawLocation,
            OccurredDate: theftCase.OccurredDate?.OccurredOn?.ToString("yyyy-MM-dd"));
    }
}
