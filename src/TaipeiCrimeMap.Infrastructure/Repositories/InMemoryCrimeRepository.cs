using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;

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
}