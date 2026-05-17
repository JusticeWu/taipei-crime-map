using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Infrastructure.Repositories;

public class InMemoryCrimeRepository : ICrimeRepository
{
    private readonly List<TheftCase> _cases = new();

    public Task AddAsync(TheftCase theftCase, CancellationToken cancellationToken = default)
    {
        _cases.Add(theftCase);

        return Task.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_cases.Count);
    }

    public Task AddRangeAsync(IEnumerable<TheftCase> theftCases, CancellationToken cancellationToken = default)
    {
        _cases.AddRange(theftCases);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TheftCase>> GetByFilterAsync(CrimeFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _cases.AsEnumerable();

        if (filter.CaseType.HasValue)
        {
            query = query.Where(c => c.CaseType == filter.CaseType.Value);
        }

        if (filter.District != null)
        {
            query = query.Where(c => c.District != null && c.District.Name == filter.District.Name);
        }

        if (filter.YearFrom.HasValue)
        {
            query = query.Where(c => c.OccurredDate.Year.HasValue && c.OccurredDate.Year >= filter.YearFrom.Value);
        }

        if (filter.YearTo.HasValue)
        {
            query = query.Where(c => c.OccurredDate.Year.HasValue && c.OccurredDate.Year <= filter.YearTo.Value);
        }

        if (filter.TimeSlot != null && filter.TimeSlot.StartHour.HasValue && filter.TimeSlot.EndHour.HasValue)
        {
            query = query.Where(c => c.TimeSlot != null 
                && c.TimeSlot.StartHour == filter.TimeSlot.StartHour.Value 
                && c.TimeSlot.EndHour == filter.TimeSlot.EndHour.Value);
        }

        return Task.FromResult<IReadOnlyList<TheftCase>>(query.ToList());
    }
}