using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Domain.Tests.ValueObjects;

public class TimeSlotTests
{
    [Theory]
    [InlineData("08~18", 8, 18)]    // 正常範圍, ~ 分隔
    [InlineData("18~18", 18, 18)]   // 同時段
    [InlineData("08-18", 8, 18)]    // - 分隔
    [InlineData("22~24", 22, 24)]   // EndHour = 24
    public void Parse_ValidInput_ReturnsCorrectTimeSlot(string raw, int expectedStart, int expectedEnd)
    {
        // Arrange & Act
        var timeSlot = TimeSlot.Parse(raw);

        // Assert
        Assert.Equal(expectedStart, timeSlot.StartHour);
        Assert.Equal(expectedEnd, timeSlot.EndHour);
    }

    [Fact]
    public void Normalize_DashSeparated_ReturnsCorrectTimeSlot()
    {
        // Arrange & Act
        var timeSlot = TimeSlot.Parse("08-18");

        // Assert
        Assert.Equal("08~18", timeSlot.Normalize());
    }

    [Theory]
    [InlineData("-1~18")]    // StartHour < 0
    [InlineData("08~25")]    // EndHour > 24
    [InlineData("09~08")]    // 倒置
    [InlineData("")]         // 空字串
    [InlineData("abcdef")]   // 非數字
    [InlineData("08")]       // 無分隔符
    public void Parse_InvalidInput_ReturnsNullTimeSlot(string raw)
    {
        // Arrange & Act
        var timeSlot = TimeSlot.Parse(raw);

        // Assert
        Assert.Null(timeSlot.StartHour);
        Assert.Null(timeSlot.EndHour);
    }
}
