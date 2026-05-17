using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Domain.Repositories;

public interface ICrimeRepository
{
    Task AddAsync(TheftCase theftCase, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<TheftCase> theftCases, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TheftCase>> GetByFilterAsync(CrimeFilter filter, CancellationToken cancellationToken = default);
    Task<TheftCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TheftCase?> GetByCaseNumberAsync(string caseNumber, CancellationToken cancellationToken = default);
    // Task<IReadOnlyList<TheftCase>> GetByRadiusAsync(GeoCoordinate center, double radiusKm, CancellationToken cancellationToken = default);
}
