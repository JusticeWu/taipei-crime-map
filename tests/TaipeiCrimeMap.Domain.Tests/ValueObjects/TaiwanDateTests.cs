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
    }

    [Theory]
    [InlineData("104104")]      // Six digit
    [InlineData("01040104")]    // Eight digit
    [InlineData("1030104")]     // ROC year before data range
    [InlineData("2000104")]     // ROC year after data range
    [InlineData("abcdefg")]     // Invalid digit
    [InlineData("1041304")]     // Month out of range
    [InlineData("1040431")]     // Day out of range
    public void Parse_InvalidInput_ReturnsIncompleteDate(string input)
    {
        // Arrange & Act
        var taiwanDate = TaiwanDate.Parse(input);

        // Assert
        Assert.False(taiwanDate.IsDataComplete);
    }
}
