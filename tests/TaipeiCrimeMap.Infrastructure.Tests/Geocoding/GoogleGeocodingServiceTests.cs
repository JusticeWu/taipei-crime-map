using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaipeiCrimeMap.Infrastructure.Geocoding;

namespace TaipeiCrimeMap.Infrastructure.Tests.Geocoding;

public class GoogleGeocodingServiceTests
{
    [Fact]
    public async Task GeocodeAsync_ValidAddress_ReturnsCoordinate()
    {
        // Arrange
        var responseJson = """
            {
                "status": "OK",
                "results" : [
                    {
                        "geometry": {
                            "location": {
                                "lat": 25.0339,
                                "lng": 121.5654
                            }
                        }
                    }
                ]
            }
            """;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        var geoService = CreateService(response);

        // Act
        var result = await geoService.GeocodeAsync("臺北市內湖區測試路");

        // Assert
        result.Should().NotBeNull();
        result!.Latitude.Should().BeApproximately(25.0339, 0.0001);
        result.Longitude.Should().BeApproximately(121.5654, 0.0001);
    }

    [Fact]
    public async Task GeocodeAsync_ApiReturnsZeroResults_ReturnsNull()
    {
        // Arrange
        var responseJson = """
        {
            "status": "OK",
            "results": []
        }
        """;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        var geoService = CreateService(response);

        // Act
        var result = await geoService.GeocodeAsync("臺北市內湖區測試路");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GeocodeAsync_ApiReturnsNonOkStatus_ReturnsNull()
    {
        // Arrange
        var responseJson = """
        {
            "status": "REQUEST_DENIED",
            "results": []
        }
        """;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        var geoService = CreateService(response);

        // Act
        var result = await geoService.GeocodeAsync("臺北市內湖區測試路");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GeocodeAsync_HttpRequestThrowsException_ReturnsNull()
    {
        // Arrange
        var exceptionThrowingHandler = new ExceptionThrowingHandler(new HttpRequestException("Network error"));
        var httpClient = new HttpClient(exceptionThrowingHandler);
        var options = Options.Create(new GoogleMapsOptions { ApiKey = "test-key" });
        var logger = NullLogger<GoogleGeocodingService>.Instance;
        var geoService = new GoogleGeocodingService(httpClient, options, logger);

        // Act
        var result = await geoService.GeocodeAsync("臺北市內湖區測試路");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GeocodeAsync_RequestCancelled_ReturnsNull()
    {
        // Arrange
        var exceptionThrowingHandler = new ExceptionThrowingHandler(new OperationCanceledException());
        var httpClient = new HttpClient(exceptionThrowingHandler);
        var options = Options.Create(new GoogleMapsOptions { ApiKey = "test-key" });
        var logger = NullLogger<GoogleGeocodingService>.Instance;
        var geoService = new GoogleGeocodingService(httpClient, options, logger);

        // Act
        var result = await geoService.GeocodeAsync("臺北市內湖區測試路");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GeocodeAsync_EmptyAddress_ReturnsNull()
    {
        // Arrange
        var geoService = CreateService(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var result = await geoService.GeocodeAsync("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GeocodeAsync_DailyQuotaExceeded_ReturnsNull()
    {
        // Arrange
        var responseJson = """
            {
                "status": "OK",
                "results": [{ "geometry": { "location": { "lat": 25.0339, "lng": 121.5654 } } }]
            }
            """;
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new GoogleMapsOptions { ApiKey = "test-key", DailyQuotaLimit = 1 });
        var logger = NullLogger<GoogleGeocodingService>.Instance;
        var geoService = new GoogleGeocodingService(httpClient, options, logger);

        // Act — first call consumes the quota, second call should be blocked
        var first = await geoService.GeocodeAsync("臺北市中正區測試路1號");
        var second = await geoService.GeocodeAsync("臺北市中正區測試路2號");

        // Assert
        first.Should().NotBeNull();
        second.Should().BeNull();
    }

    [Fact]
    public async Task GeocodeAsync_ZeroDailyQuotaLimit_NoRestriction()
    {
        // DailyQuotaLimit = 0 代表不限制，呼叫多次仍應成功
        var responseJson = """
            {
                "status": "OK",
                "results": [{ "geometry": { "location": { "lat": 25.0339, "lng": 121.5654 } } }]
            }
            """;
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new GoogleMapsOptions { ApiKey = "test-key", DailyQuotaLimit = 0 });
        var logger = NullLogger<GoogleGeocodingService>.Instance;
        var geoService = new GoogleGeocodingService(httpClient, options, logger);

        // Act
        var result = await geoService.GeocodeAsync("臺北市內湖區測試路");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GeocodeAsync_MonthlyQuotaExceeded_ReturnsNull()
    {
        // Arrange
        var responseJson = """
            {
                "status": "OK",
                "results": [{ "geometry": { "location": { "lat": 25.0339, "lng": 121.5654 } } }]
            }
            """;
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new GoogleMapsOptions { ApiKey = "test-key", MonthlyQuotaLimit = 1 });
        var logger = NullLogger<GoogleGeocodingService>.Instance;
        var geoService = new GoogleGeocodingService(httpClient, options, logger);

        // Act — first call consumes the monthly quota, second call should be blocked
        var first = await geoService.GeocodeAsync("臺北市中正區測試路1號");
        var second = await geoService.GeocodeAsync("臺北市中正區測試路2號");

        // Assert
        first.Should().NotBeNull();
        second.Should().BeNull();
    }

    [Fact]
    public async Task GeocodeAsync_ZeroMonthlyQuotaLimit_NoRestriction()
    {
        // MonthlyQuotaLimit = 0 代表不限制，呼叫多次仍應成功
        var responseJson = """
            {
                "status": "OK",
                "results": [{ "geometry": { "location": { "lat": 25.0339, "lng": 121.5654 } } }]
            }
            """;
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new GoogleMapsOptions { ApiKey = "test-key", MonthlyQuotaLimit = 0 });
        var logger = NullLogger<GoogleGeocodingService>.Instance;
        var geoService = new GoogleGeocodingService(httpClient, options, logger);

        // Act
        var result = await geoService.GeocodeAsync("臺北市內湖區測試路");

        // Assert
        result.Should().NotBeNull();
    }

    private static GoogleGeocodingService CreateService(HttpResponseMessage response)
    {
        var httpMessageHandler = new MockHttpMessageHandler(response);
        var httpClient = new HttpClient(httpMessageHandler);
        var options = Options.Create(new GoogleMapsOptions { ApiKey = "test-key" });
        var logger = NullLogger<GoogleGeocodingService>.Instance;

        return new GoogleGeocodingService(httpClient, options, logger);
    }
}

internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;

    public MockHttpMessageHandler(HttpResponseMessage response)
    {
        _response = response;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_response);
    }
}

internal sealed class ExceptionThrowingHandler : HttpMessageHandler
{
    private readonly Exception _exception;

    public ExceptionThrowingHandler(Exception exception)
    {
        _exception = exception;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        throw _exception;
    }
}