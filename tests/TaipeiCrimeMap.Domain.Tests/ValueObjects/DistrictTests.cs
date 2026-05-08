using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Domain.Tests.ValueObjects;

public class DistrictTests
{
    [Theory]
    [InlineData("臺北市中正區忠孝東路一段1號", "中正區")]
    [InlineData("臺北市大同區迪化街一段362號", "大同區")]
    [InlineData("臺北市中山區中山北路三段181號", "中山區")]
    [InlineData("臺北市松山區敦化北路340-9號", "松山區")]
    [InlineData("臺北市大安區新生南路二段1號", "大安區")]
    [InlineData("臺北市萬華區成都路10號", "萬華區")]
    [InlineData("臺北市信義區信義路五段7號", "信義區")]
    [InlineData("臺北市士林區福林路60號", "士林區")]
    [InlineData("臺北市北投區知行路360號", "北投區")]
    [InlineData("臺北市內湖區成功路五段31號", "內湖區")]
    [InlineData("臺北市南港區忠孝東路七段369號", "南港區")]
    [InlineData("臺北市文山區新光路二段30號", "文山區")]
    public void ParseFrom_AllTaipeiDistricts_ReturnsCorrectDistrict(string location, string expectedName)
    {
        // Arrange & Act
        var district = District.ParseFrom(location);

        // Assert
        Assert.Equal(expectedName, district?.Name);
    }

    [Theory]
    [InlineData("台北市內湖區成功路五段31號", "內湖區")]
    public void ParseFrom_VariantCharacter_ReturnsCorrectDistrict(string location, string expectedName)
    {
        // Arrange & Act
        var district = District.ParseFrom(location);

        // Assert
        Assert.Equal(expectedName, district?.Name);
    }

    [Fact]
    public void ParseFrom_EmptyString_ReturnsNull()
    {
        // Arrange & Act
        var district = District.ParseFrom("");

        // Assert
        Assert.Null(district);
    }

    [Fact]
    public void ParseFrom_InvalidDistrict_ReturnsNull()
    {
        // Arrange & Act
        var district = District.ParseFrom("臺北市金山區萬壽里海興路180之3號");

        // Assert
        Assert.Null(district);
    }
}