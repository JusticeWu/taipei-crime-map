using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Domain.Services;

public interface IGeocodingService
{
    Task<GeoCoordinate?> GeocodeAsync(string address, CancellationToken cancellationToken = default);
}