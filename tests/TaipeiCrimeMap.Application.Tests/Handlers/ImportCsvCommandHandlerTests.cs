using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
    private readonly Mock<ICsvParser> _csvParserMock;
    private readonly Mock<ICrimeRepository> _respositoryMock;
    private readonly ImportCsvCommandHandler _handler;

    public ImportCsvCommandHandlerTests()
    {
        _csvParserMock = new Mock<ICsvParser>();
        _respositoryMock = new Mock<ICrimeRepository>();
        
        _handler = new ImportCsvCommandHandler(_csvParserMock.Object, _respositoryMock.Object, 
            NullLogger<ImportCsvCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnCorrectSuccessCount()
    {
        // Arrange
        var cases = new List<TheftCase>();

        for (int i = 0; i < 3; i++)
        {
            cases.Add(TheftCase.Create(
                caseNumber: Guid.NewGuid().ToString(),
                caseType: CaseType.Residential,
                district: District.ParseFrom("內湖區"),
                occurredDate: TaiwanDate.Parse("1130101"),
                timeSlot: null,
                rawLocation: "臺北市內湖區成功路五段31號"));
        }

        var parseResult = new CsvParseResult(cases, SkippedCount: 2);

        _csvParserMock.Setup(p => p.Parse(It.IsAny<string>(), It.IsAny<CaseType>()))
            .Returns(parseResult);

        _respositoryMock.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<TheftCase>>(), It.IsAny<CancellationToken>()))
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
}