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
    
}