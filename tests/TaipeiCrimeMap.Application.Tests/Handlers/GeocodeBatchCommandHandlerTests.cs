using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaipeiCrimeMap.Application.Commands;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.Services;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Tests.Handlers;

public class GeocodeBatchCommandHandlerTests
{
    private readonly Mock<ICrimeRepository> _repositoryMock;
    private readonly Mock<IGeocodingService> _geocodingServiceMock;
    private readonly GeocodeBatchCommandHandler _handler;

    public GeocodeBatchCommandHandlerTests()
    {
        _repositoryMock = new Mock<ICrimeRepository>();
        _geocodingServiceMock = new Mock<IGeocodingService>();

        _handler = new GeocodeBatchCommandHandler(
            _repositoryMock.Object,
            _geocodingServiceMock.Object,
            NullLogger<GeocodeBatchCommandHandler>.Instance);
    }

    private static TheftCase CreateCase(string caseNumber, string rawLocation) => TheftCase.Create(
        caseNumber: caseNumber,
        caseType: CaseType.Residential,
        district: District.ParseFrom("內湖區"),
        occurredDate: TaiwanDate.Parse("1130101"),
        timeSlot: null,
        rawLocation: rawLocation);

    /// <summary>
    /// 相同 RawLocation 已有座標時，應直接複用，不呼叫 Geocoding API
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenRawLocationHasExistingCoordinate_ShouldReuseWithoutCallingApi()
    {
        // Arrange
        var theftCase = CreateCase("CASE-001", "臺北市內湖區成功路五段31號");
        var existingCoordinate = GeoCoordinate.Create(25.07, 121.57);

        _repositoryMock.Setup(r => r.GetCasesWithMissingCoordinatesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TheftCase> { theftCase });

        _repositoryMock.Setup(r => r.FindCoordinateByRawLocationAsync(theftCase.RawLocation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCoordinate);

        _repositoryMock.Setup(r => r.CountMissingCoordinatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var command = new GeocodeBatchCommand(10);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.ProcessedCount.Should().Be(1);
        result.ReusedCount.Should().Be(1);
        result.ApiCallCount.Should().Be(0);
        result.FailedCount.Should().Be(0);
        result.RemainingCount.Should().Be(0);

        theftCase.Coordinate.Should().Be(existingCoordinate);

        _geocodingServiceMock.Verify(
            g => g.GeocodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        _repositoryMock.Verify(
            r => r.UpdateCoordinateAsync(theftCase.Id, existingCoordinate, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 沒有可複用座標時，應呼叫 Geocoding API 並寫入結果
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNoExistingCoordinate_ShouldCallApiAndUpdate()
    {
        // Arrange
        var theftCase = CreateCase("CASE-002", "臺北市信義區市府路1號");
        var apiCoordinate = GeoCoordinate.Create(25.03, 121.56);

        _repositoryMock.Setup(r => r.GetCasesWithMissingCoordinatesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TheftCase> { theftCase });

        _repositoryMock.Setup(r => r.FindCoordinateByRawLocationAsync(theftCase.RawLocation, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeoCoordinate?)null);

        _geocodingServiceMock.Setup(g => g.GeocodeAsync(theftCase.RawLocation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiCoordinate);

        _repositoryMock.Setup(r => r.CountMissingCoordinatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var command = new GeocodeBatchCommand(10);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.ProcessedCount.Should().Be(1);
        result.ReusedCount.Should().Be(0);
        result.ApiCallCount.Should().Be(1);
        result.FailedCount.Should().Be(0);
        result.RemainingCount.Should().Be(0);

        theftCase.Coordinate.Should().Be(apiCoordinate);

        _repositoryMock.Verify(
            r => r.UpdateCoordinateAsync(theftCase.Id, apiCoordinate, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Geocoding API 失敗時，應記錄失敗並跳過該筆，不中止整批
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenGeocodingFails_ShouldSkipAndContinue()
    {
        // Arrange
        var failedCase = CreateCase("CASE-003", "無法定位的地址");
        var successCase = CreateCase("CASE-004", "臺北市信義區市府路1號");
        var apiCoordinate = GeoCoordinate.Create(25.03, 121.56);

        _repositoryMock.Setup(r => r.GetCasesWithMissingCoordinatesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TheftCase> { failedCase, successCase });

        _repositoryMock.Setup(r => r.FindCoordinateByRawLocationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeoCoordinate?)null);

        _geocodingServiceMock.Setup(g => g.GeocodeAsync(failedCase.RawLocation, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeoCoordinate?)null);

        _geocodingServiceMock.Setup(g => g.GeocodeAsync(successCase.RawLocation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiCoordinate);

        _repositoryMock.Setup(r => r.CountMissingCoordinatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new GeocodeBatchCommand(10);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.ProcessedCount.Should().Be(2);
        result.ReusedCount.Should().Be(0);
        result.ApiCallCount.Should().Be(2);
        result.FailedCount.Should().Be(1);
        result.RemainingCount.Should().Be(1);

        failedCase.Coordinate.Should().BeNull();
        successCase.Coordinate.Should().Be(apiCoordinate);

        _repositoryMock.Verify(
            r => r.UpdateCoordinateAsync(failedCase.Id, It.IsAny<GeoCoordinate>(), It.IsAny<CancellationToken>()), Times.Never);

        _repositoryMock.Verify(
            r => r.UpdateCoordinateAsync(successCase.Id, apiCoordinate, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// BatchSize 為 0 或負數時，應使用預設值 10 呼叫 Repository
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task HandleAsync_WhenBatchSizeNotPositive_ShouldUseDefaultBatchSize(int batchSize)
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetCasesWithMissingCoordinatesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TheftCase>());

        _repositoryMock.Setup(r => r.CountMissingCoordinatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var command = new GeocodeBatchCommand(batchSize);

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _repositoryMock.Verify(
            r => r.GetCasesWithMissingCoordinatesAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }
}
