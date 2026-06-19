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

public class GeocodeMissingCommandHandlerTests
{
    private readonly ICrimeRepository _repository;
    private readonly IGeocodingService _geocodingService;
    private readonly GeocodeMissingCommandHandler _handler;

    public GeocodeMissingCommandHandlerTests()
    {
        _repository = Substitute.For<ICrimeRepository>();
        _geocodingService = Substitute.For<IGeocodingService>();

        _handler = new GeocodeMissingCommandHandler(
            _repository,
            _geocodingService,
            NullLogger<GeocodeMissingCommandHandler>.Instance);
    }

    private static TheftCase CreateCase(int caseNumber, string rawLocation) => TheftCase.Create(
        caseNumber: caseNumber,
        caseType: CaseType.Residential,
        district: District.ParseFrom("內湖區"),
        occurredDate: TaiwanDate.Parse("1130101"),
        timeSlot: null,
        rawLocation: rawLocation);

    [Fact]
    public async Task HandleAsync_NoCases_ReturnsEmptyResult()
    {
        _repository.GetCasesWithMissingCoordinatesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<TheftCase>());
        _repository.CountMissingCoordinatesAsync(Arg.Any<CancellationToken>())
            .Returns(0);

        var result = await _handler.HandleAsync(new GeocodeMissingCommand(null, 0));

        result.TotalProcessed.Should().Be(0);
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ReusesExistingCoordinate_MarksSuccess()
    {
        var theftCase = CreateCase(1001, "臺北市內湖區成功路五段31號");
        var existingCoord = GeoCoordinate.Create(25.07, 121.57);

        _repository.GetCasesWithMissingCoordinatesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<TheftCase> { theftCase });
        _repository.FindCoordinateByRawLocationAsync(theftCase.RawLocation, Arg.Any<CancellationToken>())
            .Returns(existingCoord);
        _repository.CountMissingCoordinatesAsync(Arg.Any<CancellationToken>())
            .Returns(0);

        var result = await _handler.HandleAsync(new GeocodeMissingCommand(null, 0));

        result.TotalProcessed.Should().Be(1);
        result.SuccessCount.Should().Be(1);
        result.ReusedCount.Should().Be(1);
        result.FailedCount.Should().Be(0);
        result.Items.Should().HaveCount(1);
        result.Items[0].Success.Should().BeTrue();
        result.Items[0].Latitude.Should().Be(25.07);
        result.Items[0].Longitude.Should().Be(121.57);

        await _geocodingService.DidNotReceive()
            .GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_GeocodingSucceeds_ReturnsCoordinateInItem()
    {
        var theftCase = CreateCase(1002, "臺北市信義區市府路1號");
        var apiCoord = GeoCoordinate.Create(25.03, 121.56);

        _repository.GetCasesWithMissingCoordinatesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<TheftCase> { theftCase });
        _repository.FindCoordinateByRawLocationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((GeoCoordinate?)null);
        _geocodingService.GeocodeAsync(theftCase.RawLocation, Arg.Any<CancellationToken>())
            .Returns(apiCoord);
        _repository.CountMissingCoordinatesAsync(Arg.Any<CancellationToken>())
            .Returns(0);

        var result = await _handler.HandleAsync(new GeocodeMissingCommand(null, 0));

        result.TotalProcessed.Should().Be(1);
        result.SuccessCount.Should().Be(1);
        result.ReusedCount.Should().Be(0);
        result.Items[0].Success.Should().BeTrue();
        result.Items[0].Latitude.Should().Be(25.03);
        result.Items[0].CaseNumber.Should().Be(1002);
    }

    [Fact]
    public async Task HandleAsync_GeocodingFails_ReturnsFailureReason()
    {
        var theftCase = CreateCase(1003, "無法定位的地址");

        _repository.GetCasesWithMissingCoordinatesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<TheftCase> { theftCase });
        _repository.FindCoordinateByRawLocationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((GeoCoordinate?)null);
        _geocodingService.GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((GeoCoordinate?)null);
        _repository.CountMissingCoordinatesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        var result = await _handler.HandleAsync(new GeocodeMissingCommand(null, 0));

        result.TotalProcessed.Should().Be(1);
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(1);
        result.RemainingCount.Should().Be(1);
        result.Items[0].Success.Should().BeFalse();
        result.Items[0].FailureReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HandleAsync_MaxCount_LimitsCases()
    {
        _repository.GetCasesWithMissingCoordinatesAsync(5, Arg.Any<CancellationToken>())
            .Returns(new List<TheftCase>());
        _repository.CountMissingCoordinatesAsync(Arg.Any<CancellationToken>())
            .Returns(0);

        await _handler.HandleAsync(new GeocodeMissingCommand(5, 0));

        await _repository.Received(1)
            .GetCasesWithMissingCoordinatesAsync(5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_MixedResults_ReportsCorrectCounts()
    {
        var successCase = CreateCase(1004, "臺北市大安區忠孝東路四段");
        var failCase = CreateCase(1005, "不存在的地址");
        var apiCoord = GeoCoordinate.Create(25.04, 121.55);

        _repository.GetCasesWithMissingCoordinatesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<TheftCase> { successCase, failCase });
        _repository.FindCoordinateByRawLocationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((GeoCoordinate?)null);
        _geocodingService.GeocodeAsync(successCase.RawLocation, Arg.Any<CancellationToken>())
            .Returns(apiCoord);
        _geocodingService.GeocodeAsync(failCase.RawLocation, Arg.Any<CancellationToken>())
            .Returns((GeoCoordinate?)null);
        _repository.CountMissingCoordinatesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        var result = await _handler.HandleAsync(new GeocodeMissingCommand(null, 0));

        result.TotalProcessed.Should().Be(2);
        result.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        result.Items.Should().HaveCount(2);
        result.Items[0].Success.Should().BeTrue();
        result.Items[1].Success.Should().BeFalse();
    }
}
