using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Infrastructure.Csv;

namespace TaipeiCrimeMap.Infrastructure.Tests.Csv;

public class CsvParserTests
{
    private readonly CsvParser _parser;
    private readonly string _testDataPath;

    public CsvParserTests()
    {
        _parser = new CsvParser(NullLogger<CsvParser>.Instance);
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "Csv","TestData");
    }

    [Fact]
    public void Parse_ValidResidentialCsv_ShouldReturnCorrectCases()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "valid_residential.csv");

        // Act
        var results = _parser.Parse(filePath, CaseType.Residential);

        // Assert
        results.Should().HaveCount(3);
        results[0].CaseNumber.Should().Be("1");
        results[0].CaseType.Should().Be(CaseType.Residential);
        results[0].OccurredDate.Should().NotBeNull();
        results[0].OccurredDate.Year.Should().Be(113);
        results[0].District!.Name.Should().Be("大安區");
        results[0].RawLocation.Should().Be("臺北市大安區測試路1號");
    }
}