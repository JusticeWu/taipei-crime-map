using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TaipeiCrimeMap.Application.DTOs;
using TaipeiCrimeMap.Domain.Aggregates;

namespace TaipeiCrimeMap.Integration.Tests.Controllers;

public class CrimeControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CrimeControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ImportCsv_WithValidRequest_ShouldReturn200()
    {
        // Arrange
        var projectRoot = GetProjectRootPath();
        var filePath = Path.Combine(projectRoot, "data", "raw", "臺北市住宅竊盜點位資訊-UTF8-BOM.csv");

        var request = new
        {
            FilePath = filePath,
            CaseType = (int)CaseType.Residential
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/crime/import", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ImportCsvResult>();
        result.Should().NotBeNull();
        result!.SuccessCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ImportCsv_WithNonExistentFile_ShouldReturn404()
    {
        // Arrange
        var request = new
        {
            FilePath = "data/raw/not_exist.csv",
            CaseType = (int)CaseType.Residential
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/crime/import", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCrimes_WithNoFilter_ShouldReturn200()
    {
        // Arrange：先匯入資料
        var projectRoot = GetProjectRootPath();
        var filePath = Path.Combine(projectRoot, "data", "raw", "臺北市住宅竊盜點位資訊-UTF8-BOM.csv");
        var importRequest = new { FilePath = filePath, CaseType = (int)CaseType.Residential };
        await _client.PostAsJsonAsync("/api/crime/import", importRequest);

        // Act
        var response = await _client.GetAsync("/api/crime");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCrimes_WithInvalidTimeSlot_ShouldReturn400()
    {
        // Act
        var response = await _client.GetAsync("/api/crime?rawTimeSlot=invalid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static string GetProjectRootPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.slnx").Any())
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}