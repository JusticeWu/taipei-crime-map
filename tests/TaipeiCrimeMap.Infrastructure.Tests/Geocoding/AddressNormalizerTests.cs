using FluentAssertions;
using TaipeiCrimeMap.Infrastructure.Geocoding;
using Xunit;

namespace TaipeiCrimeMap.Infrastructure.Tests.Geocoding;

public class AddressNormalizerTests
{
    [Fact]
    public void Normalize_DoubleRoad_ReplacesWithSingleRoad()
    {
        AddressNormalizer.Normalize("臺北市信義路路100號").Should().Be("臺北市信義路100號");
    }

    [Fact]
    public void Normalize_YiDai_IsRemoved()
    {
        AddressNormalizer.Normalize("臺北市內湖區一帶100號").Should().Be("臺北市內湖區100號");
    }

    [Fact]
    public void Normalize_TrailingParentheses_AreRemoved()
    {
        AddressNormalizer.Normalize("景文街(景中街口)").Should().Be("景文街");
    }

    [Theory]
    [InlineData("31-60號", "45號")]
    [InlineData("31 - 60號", "45號")]
    [InlineData("31~60號", "45號")]
    [InlineData("31 ~ 60號", "45號")]
    [InlineData("31～60號", "45號")]
    public void Normalize_NumberRange_ReplacesWithAverage(string input, string expected)
    {
        AddressNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_NumberRange_OneToThirty_ReplacesWithFifteen()
    {
        AddressNormalizer.Normalize("1 - 30號").Should().Be("15號");
    }

    [Fact]
    public void Normalize_NumberRange_661To690_ReplacesWith675()
    {
        AddressNormalizer.Normalize("661~690號").Should().Be("675號");
    }

    [Fact]
    public void Normalize_CompositeScenario_AppliesAllRules()
    {
        AddressNormalizer.Normalize("中山路路31~60號(景中街口)一帶").Should().Be("中山路45號");
    }

    [Fact]
    public void Normalize_NullOrWhitespace_ReturnsAsIs()
    {
        AddressNormalizer.Normalize("").Should().Be("");
        AddressNormalizer.Normalize("   ").Should().Be("   ");
    }
}
