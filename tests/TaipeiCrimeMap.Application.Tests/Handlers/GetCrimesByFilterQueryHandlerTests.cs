using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
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
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly GetCrimesByFilterQueryHandler _handler;

    public GetCrimesByFilterQueryHandlerTests()
    {
        _repositoryMock = new Mock<ICrimeRepository>();
        _cacheMock = new Mock<IDistributedCache>();

        // 預設：快取未命中、寫入不拋錯
        _cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        _cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new GetCrimesByFilterQueryHandler(
            _repositoryMock.Object,
            _cacheMock.Object,
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
        _repositoryMock.Setup(
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

    /// <summary>
    /// 相同查詢條件第二次呼叫，Repository 應只被呼叫一次（快取命中）
    /// </summary>
    [Fact]
    public async Task HandleAsync_SecondCallWithSameQuery_ShouldReturnCachedResult()
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

        // 模擬 IDistributedCache 的寫入與讀取行為
        var stored = new Dictionary<string, byte[]>();
        _cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string key, CancellationToken _) =>
                Task.FromResult<byte[]?>(stored.TryGetValue(key, out var v) ? v : null));
        _cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (key, value, _, _) => stored[key] = value)
            .Returns(Task.CompletedTask);

        var query = new GetCrimesByFilterQuery(CaseType: CaseType.Residential);

        // Act
        var firstResult = await _handler.HandleAsync(query);
        var secondResult = await _handler.HandleAsync(query);

        // Assert
        secondResult.Should().BeEquivalentTo(firstResult);
        _repositoryMock.Verify(
            r => r.GetByFilterAsync(
                It.IsAny<CrimeFilter>(),
                It.IsAny<CancellationToken>()),
            Times.Once);    // Repository 只被呼叫一次
    }

    /// <summary>
    /// 不同查詢條件，Repository 應該被呼叫二次（快取未命中）
    /// </summary>
    [Fact]
    public async Task HandleAsync_DifferentQueries_ShouldCallRepositoryTwice()
    {
        // Arrange
        _repositoryMock.Setup(
            r => r.GetByFilterAsync(
                It.IsAny<CrimeFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TheftCase>());

        var query1 = new GetCrimesByFilterQuery(DistrictName: "大安區");
        var query2 = new GetCrimesByFilterQuery(DistrictName: "內湖區");

        // Act
        await _handler.HandleAsync(query1);
        await _handler.HandleAsync(query2);

        // Assert
        _repositoryMock.Verify(
            r => r.GetByFilterAsync(
                It.IsAny<CrimeFilter>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));  // Repository 被呼叫兩次
    }

    /// <summary>
    /// 快取命中時，回傳資料應與序列化前一致（驗證 JSON 往返正確性）
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenCacheHit_ShouldReturnDeserializedData()
    {
        // Arrange
        var query = new GetCrimesByFilterQuery(CaseType: CaseType.Car);
        var cacheKey = $"crimes:filter:{query.CaseType}:{query.DistrictName}:{query.YearFrom}:{query.YearTo}:{query.RawTimeSlot}";

        var preloaded = new List<TaipeiCrimeMap.Application.DTOs.TheftCaseDto>
        {
            new() { CaseNumber = "cached-001", CaseType = "汽車竊盜", District = "信義區" }
        };
        var preloadedBytes = JsonSerializer.SerializeToUtf8Bytes(preloaded);

        _cacheMock
            .Setup(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preloadedBytes);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().HaveCount(1);
        result[0].CaseNumber.Should().Be("cached-001");
        _repositoryMock.Verify(
            r => r.GetByFilterAsync(It.IsAny<CrimeFilter>(), It.IsAny<CancellationToken>()),
            Times.Never);   // 快取命中，不查 DB
    }
}
