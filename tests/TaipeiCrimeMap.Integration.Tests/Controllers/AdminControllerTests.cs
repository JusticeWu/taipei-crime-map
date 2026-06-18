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
    public async Task BulkAddCases_WithValidData_ShouldReturn200WithMode()
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
        var mode = doc.RootElement.GetProperty("mode").GetString();
        mode.Should().BeOneOf("async", "sync");
        doc.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task BulkAddCases_WithMixedItems_ShouldReturn200()
    {
        var client = CreateAuthorizedClient(
            CustomWebApplicationFactory.AdminUsername,
            CustomWebApplicationFactory.AdminPassword);

        var items = new[]
        {
            new { caseNumber = 99902, caseType = "不存在的案類", occurrenceDate = 1150329, timeSlot = "06~08", rawLocation = "臺北市大安區" },
            new { caseNumber = 99903, caseType = "住宅竊盜", occurrenceDate = 1150401, timeSlot = "10~12", rawLocation = "臺北市中山區" },
        };

        var content = new StringContent(
            JsonSerializer.Serialize(items),
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/admin/cases/bulk", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("mode").GetString().Should().BeOneOf("async", "sync");
        doc.RootElement.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task UpdateCase_WithoutAuth_ShouldReturn401()
    {
        var body = new StringContent(
            JsonSerializer.Serialize(new { occurrenceDate = 1150401 }),
            Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Patch, "/api/admin/cases/99901/1") { Content = body };
        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateCase_WithNoFields_ShouldReturn400()
    {
        var client = CreateAuthorizedClient(
            CustomWebApplicationFactory.AdminUsername,
            CustomWebApplicationFactory.AdminPassword);

        var body = new StringContent(
            JsonSerializer.Serialize(new { }),
            Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Patch, "/api/admin/cases/99901/1") { Content = body };
        client.DefaultRequestHeaders.ToList().ForEach(h => req.Headers.TryAddWithoutValidation(h.Key, h.Value));
        var response = await client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCrimes_WithSortParams_ShouldReturn200()
    {
        var client = CreateAuthorizedClient(
            CustomWebApplicationFactory.AdminUsername,
            CustomWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync("/api/crime?pageSize=5&sortBy=caseNumber&sortOrder=desc");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("data").GetArrayLength().Should().BeGreaterThanOrEqualTo(0);
        doc.RootElement.GetProperty("totalPages").ValueKind.Should().Be(JsonValueKind.Number);
    }
}
