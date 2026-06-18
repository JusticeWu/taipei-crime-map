using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TaipeiCrimeMap.Application.Commands;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.Results;
using TaipeiCrimeMap.Domain.Services;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Application.Tests.Handlers;

public class ImportCsvCommandHandlerTests
{
    private readonly ICsvParser _csvParser;
    private readonly ICrimeRepository _respository;
    private readonly ImportCsvCommandHandler _handler;

    public ImportCsvCommandHandlerTests()
    {
        _csvParser = Substitute.For<ICsvParser>();
        _respository = Substitute.For<ICrimeRepository>();

        _handler = new ImportCsvCommandHandler(_csvParser, _respository,
            NullLogger<ImportCsvCommandHandler>.Instance);
    }

    /// <summary>
    /// 測試 HandleAsync 方法是否正確回傳成功匯入的案件數量、跳過的案件數量、檔案路徑和案類名稱
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnCorrectSuccessCount()
    {
        // Arrange
        var cases = new List<TheftCase>();

        for (int i = 0; i < 3; i++)
        {
            cases.Add(TheftCase.Create(
                caseNumber: Random.Shared.Next(100000, 999999),
                caseType: CaseType.Residential,
                district: District.ParseFrom("內湖區"),
                occurredDate: TaiwanDate.Parse("1130101"),
                timeSlot: null,
                rawLocation: "臺北市內湖區成功路五段31號"));
        }

        var parseResult = new CsvParseResult(cases, SkippedCount: 2);

        _csvParser.Parse(Arg.Any<string>(), Arg.Any<CaseType>())
            .Returns(parseResult);

        _respository.AddRangeAsync(Arg.Any<IEnumerable<TheftCase>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var command = new ImportCsvCommand("data/raw/test.csv", CaseType.Residential);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.SuccessCount.Should().Be(3);
        result.SkippedCount.Should().Be(2);
        result.FilePath.Should().Be("data/raw/test.csv");
        result.CaseType.Should().Be("住宅竊盜");
    }

    /// <summary>
    /// 測試 HandleAsync 方法是否正確呼叫 CsvParser 的 Parse 方法，並傳入正確的檔案路徑和案類參數
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldCallParseWithCorrectArguments()
    {
        // Arrange
        var parseResult = new CsvParseResult(new List<TheftCase>(), SkippedCount: 0);

        _csvParser.Parse(Arg.Any<string>(), Arg.Any<CaseType>())
            .Returns(parseResult);

        _respository.AddRangeAsync(Arg.Any<IEnumerable<TheftCase>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var command = new ImportCsvCommand("data/raw/test.csv", CaseType.Car);

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _csvParser.Received(1).Parse("data/raw/test.csv", CaseType.Car);
    }

    /// <summary>
    /// 測試 HandleAsync 方法是否正確呼叫 ICrimeRepository 的 AddRangeAsync 方法，並傳入解析後的案件列表
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldCallAddRangeAsync_WithParsedCases()
    {
        // Arrange
        var cases = new List<TheftCase>();

        for (int i = 0; i < 3; i++)
        {
            cases.Add(TheftCase.Create(
                caseNumber: Random.Shared.Next(100000, 999999),
                caseType: CaseType.Residential,
                district: District.ParseFrom("內湖區"),
                occurredDate: TaiwanDate.Parse("1130101"),
                timeSlot: null,
                rawLocation: "臺北市內湖區成功路五段31號"));
        }

        var parseResult = new CsvParseResult(cases, SkippedCount: 0);

        _csvParser.Parse(Arg.Any<string>(), Arg.Any<CaseType>())
            .Returns(parseResult);

        _respository.AddRangeAsync(Arg.Any<IEnumerable<TheftCase>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var command = new ImportCsvCommand("data/raw/test.csv", CaseType.Residential);

        // Act
        await _handler.HandleAsync(command);

        // Assert
        await _respository.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<TheftCase>>(c => c.SequenceEqual(cases)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 測試 HandleAsync 方法在解析結果沒有任何案件時，是否正確回傳成功匯入的案件數量為 0，並且跳過的案件數量為 SkippedCount
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNoCases_ShouldReturnZeroSuccessCount()
    {
        // Arrange
        var parseResult = new CsvParseResult(new List<TheftCase>(), SkippedCount: 5);

        _csvParser.Parse(Arg.Any<string>(), Arg.Any<CaseType>())
            .Returns(parseResult);

        _respository.AddRangeAsync(Arg.Any<IEnumerable<TheftCase>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var command = new ImportCsvCommand("data/raw/test.csv", CaseType.Residential);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.SuccessCount.Should().Be(0);
        result.SkippedCount.Should().Be(5);
    }
}
