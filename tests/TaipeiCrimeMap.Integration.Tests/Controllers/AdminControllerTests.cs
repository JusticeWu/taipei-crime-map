using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;

namespace TaipeiCrimeMap.Integration.Tests.Controllers;

public class AdminControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdminControllerTests(CustomWebApplicationFactory factory)
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
    public async Task ClearCache_WithoutAuthorization_ShouldReturn401()
    {
        // Act
        var response = await _client.PostAsync("/api/admin/cache/clear", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().Should().Contain("Basic realm=\"TaipeiCrimeMap Admin\"");
    }

    [Fact]
    public async Task ClearCache_WithAuthorization_ShouldReturn200()
    {
        // Arrange
        var client = CreateAuthorizedClient(CustomWebApplicationFactory.AdminUsername, CustomWebApplicationFactory.AdminPassword);

        // Act
        var response = await client.PostAsync("/api/admin/cache/clear", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
