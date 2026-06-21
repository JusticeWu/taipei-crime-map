using System.Net;
using FluentAssertions;

namespace TaipeiCrimeMap.Integration.Tests.Controllers;

public class HealthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HealthControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DbPing_WithoutApiKey_ShouldReturn401()
    {
        var response = await _client.GetAsync("/api/health/db-ping");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DbPing_WithWrongApiKey_ShouldReturn401()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health/db-ping");
        request.Headers.Add("X-API-Key", "wrong-key");
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DbPing_WithCorrectApiKey_ShouldReturn200()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health/db-ping");
        request.Headers.Add("X-API-Key", CustomWebApplicationFactory.HealthApiKey);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
