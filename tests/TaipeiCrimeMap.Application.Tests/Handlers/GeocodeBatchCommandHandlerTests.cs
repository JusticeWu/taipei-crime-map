using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TaipeiCrimeMap.Application.Commands;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.Services;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Tests.Handlers;

public class GeocodeBatchCommandHandlerTests
{
    private readonly ICrimeRepository _repository;
    private readonly IGeocodingService _geocodingService;
    private readonly GeocodeBatchCommandHandler _handler;

    public GeocodeBatchCommandHandlerTests()
    {
        _repository = Substitute.For<ICrimeRepository>();
        _geocodingService = Substitute.For<IGeocodingService>();

        _handler = new GeocodeBatchCommandHandler(
            _repository,
            _geocodingService,
            NullLogger<GeocodeBatchCommandHandler>.Instance);
    }

    private static TheftCase CreateCase(int caseNumber, string rawLocation) => TheftCase.Create(
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
        var theftCase = CreateCase(1001, "臺北市內湖區成功路五段31號");
        var existingCoordinate = GeoCoordinate.Create(25.07, 121.57);

        _repository.GetCasesWithMissingCoordinatesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<TheftCase> { theftCase });

        _repository.FindCoordinateByRawLocationAsync(theftCase.RawLocation, Arg.Any<CancellationToken>())
            .Returns(existingCoordinate);

        _repository.CountMissingCoordinatesAsync(Arg.Any<CancellationToken>())
            .Returns(0);

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

        await _geocodingService.DidNotReceive()
            .GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        await _repository.Received(1)
            .UpdateCoordinateAsync(theftCase.Id, existingCoordinate, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 沒有可複用座標時，應呼叫 Geocoding API 並寫入結果
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNoExistingCoordinate_ShouldCallApiAndUpdate()
    {
        // Arrange
        var theftCase = CreateCase(1002, "臺北市信義區市府路1號");
        var apiCoordinate = GeoCoordinate.Create(25.03, 121.56);

        _repository.GetCasesWithMissingCoordinatesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<TheftCase> { theftCase });

        _repository.FindCoordinateByRawLocationAsync(theftCase.RawLocation, Arg.Any<CancellationToken>())
            .Returns((GeoCoordinate?)null);

        _geocodingService.GeocodeAsync(theftCase.RawLocation, Arg.Any<CancellationToken>())
            .Returns(apiCoordinate);

        _repository.CountMissingCoordinatesAsync(Arg.Any<CancellationToken>())
            .Returns(0);

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

        await _repository.Received(1)
            .UpdateCoordinateAsync(theftCase.Id, apiCoordinate, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Geocoding API 失敗時，應記錄失敗並跳過該筆，不中止整批
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenGeocodingFails_ShouldSkipAndContinue()
    {
        // Arrange
        var failedCase = CreateCase(1003, "無法定位的地址");
        var successCase = CreateCase(1004, "臺北市信義區市府路1號");
        var apiCoordinate = GeoCoordinate.Create(25.03, 121.56);

        _repository.GetCasesWithMissingCoordinatesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<TheftCase> { failedCase, successCase });

        _repository.FindCoordinateByRawLocationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((GeoCoordinate?)null);

        _geocodingService.GeocodeAsync(failedCase.RawLocation, Arg.Any<CancellationToken>())
            .Returns((GeoCoordinate?)null);

        _geocodingService.GeocodeAsync(successCase.RawLocation, Arg.Any<CancellationToken>())
            .Returns(apiCoordinate);

        _repository.CountMissingCoordinatesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

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

        await _repository.DidNotReceive()
            .UpdateCoordinateAsync(failedCase.Id, Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>());

        await _repository.Received(1)
            .UpdateCoordinateAsync(successCase.Id, apiCoordinate, Arg.Any<CancellationToken>());
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
        _repository.GetCasesWithMissingCoordinatesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<TheftCase>());

        _repository.CountMissingCoordinatesAsync(Arg.Any<CancellationToken>())
            .Returns(0);

        var command = new GeocodeBatchCommand(batchSize);

        // Act
        await _handler.HandleAsync(command);

        // Assert
        await _repository.Received(1)
            .GetCasesWithMissingCoordinatesAsync(10, Arg.Any<CancellationToken>());
    }
}
