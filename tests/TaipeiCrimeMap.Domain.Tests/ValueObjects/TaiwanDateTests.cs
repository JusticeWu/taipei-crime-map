using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Domain.Tests.ValueObjects;


public class TaiwanDateTests
{
    [Fact]
    public void Parse_ValidSevenDigit_ReturnsCorrectDate()
    {
        // Arrange
        var taiwanDate = TaiwanDate.Parse("1040104");

        // Act
        var result = taiwanDate.OccurredOn;

        // Assert
        Assert.Equal(new DateOnly(2015, 1, 4), result);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsIncompleteDate()
    {
        // Arrange & Act
        var taiwanDate = TaiwanDate.Parse("");

        // Assert
        Assert.Null(taiwanDate.OccurredOn);
        Assert.False(taiwanDate.IsDataComplete);
    }

    [Fact]
    public void Parse_InvalidSixDigit_ReturnsIncompleteDate()
    {
        // Arrange & Act
        var taiwanDate = TaiwanDate.Parse("104104");

        // Assert
        Assert.False(taiwanDate.IsDataComplete);
    }

    [Fact]
    public void Parse_InvalidEightDigit_ReturnsIncompleteDate()
    {
        // Arrange & Act
        var taiwanDate = TaiwanDate.Parse("01040104");

        // Assert
        Assert.False(taiwanDate.IsDataComplete);
    }

    [Fact]
    public void Parse_ROCYearBeforeDataRange_ReturnsInCompleteDate()
    {
        // Arrange & Act
        var taiwanDate = TaiwanDate.Parse("1030104");

        // Assert
        Assert.False(taiwanDate.IsDataComplete);
    }

    [Fact]
    public void Parse_ROCYearAfterDataRange_ReturnsInCompleteDate()
    {
        // Arrange & Act
        var taiwanDate = TaiwanDate.Parse("2000104");

        // Assert
        Assert.False(taiwanDate.IsDataComplete);
    }

    [Fact]
    public void Parse_InvalidDigit_ReturnsIncompleteDate()
    {
        // Arrange & Act
        var taiwanDate = TaiwanDate.Parse("abcdefg");

        // Assert
        Assert.False(taiwanDate.IsDataComplete);
    }

    [Fact]
    public void Parse_MonthOutOfRange_ReturnsInCompleteDate()
    {
        // Arrange & Act
        var taiwanDate = TaiwanDate.Parse("1041304");

        // Assert
        Assert.False(taiwanDate.IsDataComplete);
    }

    [Fact]
    public void Parse_DayOutOfRange_ReturnsInCompleteDate()
    {
        // Arrange & Act
        var taiwanDate = TaiwanDate.Parse("1040431");

        // Assert
        Assert.False(taiwanDate.IsDataComplete);
    }
}
