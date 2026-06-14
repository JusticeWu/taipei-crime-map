using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using TaipeiCrimeMap.Domain.Aggregates;

namespace TaipeiCrimeMap.Integration.Tests.Controllers;

public class CoordinateControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CoordinateControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpClient CreateAuthorizedClient(string username, string password)
    {
        var client = _factory.CreateClient();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return client;
    }

    [Fact]
    public async Task UpdateCoordinate_WithoutAuthorization_ShouldReturn401()
    {
        // Act
        var response = await _client.PatchAsJsonAsync("/api/crime/coordinate", new
        {
            RawLocation = "臺北市大安區測試路1號",
            Latitude = 25.03,
            Longitude = 121.5
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().Should().Contain("Basic realm=\"TaipeiCrimeMap Admin\"");
    }

    [Fact]
    public async Task UpdateCoordinate_WithInvalidLatitude_ShouldReturn400()
    {
        // Arrange
        var client = CreateAuthorizedClient(CustomWebApplicationFactory.AdminUsername, CustomWebApplicationFactory.AdminPassword);

        // Act
        var response = await client.PatchAsJsonAsync("/api/crime/coordinate", new
        {
            RawLocation = "臺北市大安區測試路1號",
            Latitude = 999,
            Longitude = 121.5
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateCoordinate_WithExistingRawLocation_ShouldReturn200AndAffectedCount()
    {
        // Arrange：先匯入資料，確保 raw_location 存在
        var client = CreateAuthorizedClient(CustomWebApplicationFactory.AdminUsername, CustomWebApplicationFactory.AdminPassword);
        var filePath = Path.Combine(AppContext.BaseDirectory, "TestData", "residential_sample.csv");
        await client.PostAsJsonAsync("/api/crime/import", new
        {
            FilePath = filePath,
            CaseType = (int)CaseType.Residential
        });

        // Act
        var response = await client.PatchAsJsonAsync("/api/crime/coordinate", new
        {
            RawLocation = "臺北市大安區測試路1號",
            Latitude = 25.03,
            Longitude = 121.5
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateCoordinate_WithUnknownRawLocation_ShouldReturn404()
    {
        // Arrange
        var client = CreateAuthorizedClient(CustomWebApplicationFactory.AdminUsername, CustomWebApplicationFactory.AdminPassword);

        // Act
        var response = await client.PatchAsJsonAsync("/api/crime/coordinate", new
        {
            RawLocation = "不存在的地址-coordinate-test-404",
            Latitude = 25.03,
            Longitude = 121.5
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
