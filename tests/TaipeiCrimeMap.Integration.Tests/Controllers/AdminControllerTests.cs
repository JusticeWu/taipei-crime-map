using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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

    [Fact]
    public async Task BulkAddCases_WithoutAuth_ShouldReturn401()
    {
        var content = new StringContent("[]", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/admin/cases/bulk", content);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BulkAddCases_WithValidData_ShouldReturnSucceeded()
    {
        var client = CreateAuthorizedClient(
            CustomWebApplicationFactory.AdminUsername,
            CustomWebApplicationFactory.AdminPassword);

        var items = new[]
        {
            new { caseNumber = 99901, caseType = "住宅竊盜", occurrenceDate = 1150329, timeSlot = "06~08", rawLocation = "臺北市大安區忠孝東路四段" },
        };

        var content = new StringContent(
            JsonSerializer.Serialize(items),
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/admin/cases/bulk", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("succeeded").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("failed").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task BulkAddCases_WithInvalidCaseType_ShouldReturnFailure()
    {
        var client = CreateAuthorizedClient(
            CustomWebApplicationFactory.AdminUsername,
            CustomWebApplicationFactory.AdminPassword);

        var items = new[]
        {
            new { caseNumber = 99902, caseType = "不存在的案類", occurrenceDate = 1150329, timeSlot = "06~08", rawLocation = "臺北市大安區" },
        };

        var content = new StringContent(
            JsonSerializer.Serialize(items),
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/admin/cases/bulk", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("succeeded").GetInt32().Should().Be(0);
        doc.RootElement.GetProperty("failed").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("failures")[0]
            .GetProperty("reason").GetString().Should().Contain("不存在的案類");
    }
}
