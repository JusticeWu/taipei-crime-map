using TaipeiCrimeMap.Domain.Aggregates.TheftCase;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Domain.Tests.ValueObjects;

public class CrimeFilterTests
{
    [Fact]
    public void Constructor_WithNullCaseType_CreatesSuccessfully()
    {
        // Arrange & Act
        var filter = new CrimeFilter(caseType: null);

        // Assert
        Assert.Null(filter.CaseType);
    }

    [Fact]
    public void Constructor_WithValidCaseType_CreatesSuccessfully()
    {
        // Arrange & Act
        var filter = new CrimeFilter(caseType: CaseType.Residential);

        // Assert
        Assert.Equal(CaseType.Residential, filter.CaseType);
    }
}