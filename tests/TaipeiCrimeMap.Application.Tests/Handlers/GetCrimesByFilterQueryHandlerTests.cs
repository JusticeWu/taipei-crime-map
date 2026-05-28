using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Application.Queries;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Exceptions;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Tests.Handlers;

public class GetCrimesByFilterQueryHandlerTests
{
    private readonly Mock<ICrimeRepository> _repositoryMock;
    private readonly GetCrimesByFilterQueryHandler _handler;

    public GetCrimesByFilterQueryHandlerTests()
    {
        _repositoryMock = new Mock<ICrimeRepository>();

        _handler = new GetCrimesByFilterQueryHandler(
            _repositoryMock.Object, 
            NullLogger<GetCrimesByFilterQueryHandler>.Instance);
    }

    /// <summary>
    /// 測試當提供的過濾條件正確時，是否正確回傳對應的案件 DTOs
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnMappingDtos()
    {
        // Arrange
        var cases = new List<TheftCase>
        {
            TheftCase.Create(
                caseNumber: "001",
                caseType: CaseType.Residential,
                district: District.ParseFrom("內湖區"),
                occurredDate: TaiwanDate.Parse("1130101"),
                timeSlot: TimeSlot.Parse("18-20"),
                rawLocation: "臺北市內湖區成功路五段31號")
        };

        _repositoryMock.Setup(
            r => r.GetByFilterAsync(
                It.IsAny<CrimeFilter>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cases);

        var query = new GetCrimesByFilterQuery(CaseType: CaseType.Residential);

        // Act
        var results = await _handler.HandleAsync(query);

        // Assert
        results.Should().HaveCount(1);
        results[0].CaseNumber.Should().Be("001");
        results[0].CaseType.Should().Be("住宅竊盜");
        results[0].District.Should().Be("內湖區");
        results[0].OccurredDate.Should().Be("2024-01-01");
        results[0].TimeSlot.Should().Be("18~20");
        results[0].RawLocation.Should().Be("臺北市內湖區成功路五段31號");
    }

    /// <summary>
    /// 測試當提供的時段格式不正確時，是否拋出 DomainException 並包含錯誤的時段字串
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithInvalidTimeSlot_ShouldThrowDomainException()
    {
        // Arrange
        var query = new GetCrimesByFilterQuery(RawTimeSlot: "test");

        // Act
        var act = async () => await _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*test*");
    }

    /// <summary>
    /// 測試當沒有提供任何過濾條件時，是否正確呼叫 Repository 的 GetByFilterAsync 方法
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithNoFilter_ShouldCallGetByFilterAsync()
    {
        // Arrange
        _ = _repositoryMock.Setup(
            r => r.GetByFilterAsync(
                It.IsAny<CrimeFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TheftCase>());

        var query = new GetCrimesByFilterQuery();

        // Act
        await _handler.HandleAsync(query);

        // Assert
        _repositoryMock.Verify(
            r => r.GetByFilterAsync(
                It.IsAny<CrimeFilter>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// 測試當提供行政區名過濾條件時，是否正確將行政區名轉換為 District 物件並傳遞給 Repository
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithDistrictFilter_ShouldPassCorrectFilterToRepository()
    {
        // Arrange
        _repositoryMock.Setup(
            r => r.GetByFilterAsync(
                It.IsAny<CrimeFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TheftCase>());

        var query = new GetCrimesByFilterQuery(DistrictName: "大安區");

        // Act
        await _handler.HandleAsync(query);

        // Assert
        _repositoryMock.Verify(
            r => r.GetByFilterAsync(
                It.Is<CrimeFilter>(f => f.District != null && f.District.Name == "大安區"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}