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
        results.Cases.Should().HaveCount(3);
        results.Cases[0].CaseNumber.Should().Be(1);
        results.Cases[0].CaseType.Should().Be(CaseType.Residential);
        results.Cases[0].OccurredDate.Should().NotBeNull();
        results.Cases[0].OccurredDate.Year.Should().Be(113);
        results.Cases[0].District!.Name.Should().Be("大安區");
        results.Cases[0].RawLocation.Should().Be("臺北市大安區測試路1號");
    }

    [Fact]
    public void Parse_FileNotFound_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "not_exist.csv");

        // Act
        Action act = () => _parser.Parse(filePath, CaseType.Residential);

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Parse_WithEmptyRows_ShouldSkipRows()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "empty_rows.csv");

        // Act
        var results = _parser.Parse(filePath, CaseType.Residential);

        // Assert
        results.Cases.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_WithWrongCaseType_ShouldSkipMismatchedRows()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "wrong_casetype.csv");

        // Act
        var results = _parser.Parse(filePath, CaseType.Residential);

        // Assert
        results.Cases.Should().HaveCount(1);
        results.Cases[0].CaseType.Should().Be(CaseType.Residential);
    }

    [Fact]
    public void Parse_WithMissingLocation_ShouldSkipRows()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "missing_location.csv");

        // Act
        var results = _parser.Parse(filePath, CaseType.Residential);

        // Assert
        results.Cases.Should().HaveCount(1);
        results.Cases[0].CaseNumber.Should().Be(2);
    }

    [Fact]
    public void Parse_Cp950EncodedCsv_ShouldReturnCorrectCases()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "cp950_snatching.csv");

        // Act
        var results = _parser.Parse(filePath, CaseType.Snatching);

        // Assert
        results.Cases.Should().HaveCount(2);
        results.Cases[0].CaseType.Should().Be(CaseType.Snatching);
        results.Cases[0].CaseNumber.Should().Be(1);
    }

    [Fact]
    public void Parse_WithInvalidDate_ShouldImportWithIsDataCompleteFalse()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "invalid_date_and_no_timeslot.csv");

        // Act
        var results = _parser.Parse(filePath, CaseType.Residential);

        // Assert
        results.Cases.Should().HaveCount(2);

        results.Cases[0].OccurredDate.IsDataComplete.Should().BeFalse();
        results.Cases[0].IsDataComplete.Should().BeFalse();

        results.Cases[1].TimeSlot.Should().BeNull();
        results.Cases[1].IsDataComplete.Should().BeFalse();
    }
}