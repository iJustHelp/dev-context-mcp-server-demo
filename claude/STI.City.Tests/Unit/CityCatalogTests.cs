using Demo.Cities;
using Moq;
using STI.City.Core.Services;
using Xunit;

namespace STI.City.Tests.Unit;

public class CityCatalogTests
{
    private static readonly string[] AllCities = ["Berlin", "London", "New York", "Paris"];
    private static readonly string[] UsaCities = ["Boston", "New York"];

    private static CityCatalog CreateCatalog(
        out Mock<ICityService> cityService,
        out Mock<IUsaCityService> usaCityService)
    {
        cityService = new Mock<ICityService>(MockBehavior.Strict);
        cityService.Setup(s => s.GetCityNames()).Returns(AllCities);

        usaCityService = new Mock<IUsaCityService>(MockBehavior.Strict);
        usaCityService.Setup(s => s.GetCityNames()).Returns(UsaCities);

        return new CityCatalog(cityService.Object, usaCityService.Object);
    }

    [Fact] // FR-1, FR-7
    public void GetAllCityNames_preserves_package_order()
    {
        var catalog = CreateCatalog(out _, out _);

        Assert.Equal(AllCities, catalog.GetAllCityNames());
    }

    [Fact] // FR-2, FR-7
    public void GetUsaCityNames_returns_usa_list()
    {
        var catalog = CreateCatalog(out _, out _);

        Assert.Equal(UsaCities, catalog.GetUsaCityNames());
    }

    [Theory] // FR-5: case-insensitive matching to the canonical spelling
    [InlineData("new york", "New York")]
    [InlineData("PARIS", "Paris")]
    [InlineData("  London  ", "London")]
    public void ResolveCanonicalName_matches_case_insensitively(string input, string expected)
    {
        var catalog = CreateCatalog(out _, out _);

        Assert.Equal(expected, catalog.ResolveCanonicalName(input));
    }

    [Theory] // FR-6: unknown city resolves to null
    [InlineData("Atlantis")]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveCanonicalName_returns_null_for_unknown(string input)
    {
        var catalog = CreateCatalog(out _, out _);

        Assert.Null(catalog.ResolveCanonicalName(input));
    }
}
