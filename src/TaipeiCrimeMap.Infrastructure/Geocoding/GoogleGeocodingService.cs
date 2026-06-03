using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaipeiCrimeMap.Domain.Services;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Infrastructure.Geocoding;

public sealed class GoogleGeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly GoogleMapsOptions _options;
    private readonly ILogger<GoogleGeocodingService> _logger;

    private const string BaseUrl = "https://maps.googleapis.com/maps/api/geocode/json";

    private int _dailyRequestCount;
    private DateOnly _quotaResetDate = DateOnly.FromDateTime(DateTime.UtcNow);
    private readonly object _quotaLock = new();

    public GoogleGeocodingService(HttpClient httpClient, IOptions<GoogleMapsOptions> options,
        ILogger<GoogleGeocodingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    private bool TryAcquireQuota()
    {
        if (_options.DailyQuotaLimit <= 0)
            return true;

        lock (_quotaLock)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (today > _quotaResetDate)
            {
                _dailyRequestCount = 0;
                _quotaResetDate = today;
            }

            if (_dailyRequestCount >= _options.DailyQuotaLimit)
                return false;

            _dailyRequestCount++;
            return true;
        }
    }

    public async Task<GeoCoordinate?> GeocodeAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        if (!TryAcquireQuota())
        {
            _logger.LogWarning("Geocoding daily quota ({Limit}) exceeded, skipping: {Address}",
                _options.DailyQuotaLimit, address);
            return null;
        }

        var url = $"{BaseUrl}?address={Uri.EscapeDataString(address)}&key={_options.ApiKey}&language=zh-TW&region=TW";

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var response = await _httpClient.GetFromJsonAsync<GeocodeApiResponse>(url, cts.Token);

            if (response is null || response.Status != "OK" || response.Results.Count == 0)
            {
                _logger.LogWarning("Geocoding fail: {Address}, API status: {Status}", 
                    address, response?.Status ?? "null");
                return null;
            }

            var location = response.Results[0].Geometry.Location;
            return GeoCoordinate.Create(location.Lat, location.Lng);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Geocoding timeout: {Address}", address);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Geocoding error: {Address}", address);
            return null;
        }
    }

    // Google API 回應的 DTO 
    private record GeocodeApiResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("results")] List<GeocodeResult> Results);

    private record GeocodeResult(
        [property: JsonPropertyName("geometry")] GeocodeGeometry Geometry);

    private record GeocodeGeometry(
        [property: JsonPropertyName("location")] GeocodeLocation Location);

    private record GeocodeLocation(
        [property: JsonPropertyName("lat")] double Lat,
        [property: JsonPropertyName("lng")] double Lng);
}