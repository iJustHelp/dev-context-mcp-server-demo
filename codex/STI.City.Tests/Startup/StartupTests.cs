using STI.City.API.Configuration;

namespace STI.City.Tests.Startup;

public sealed class StartupTests
{
    [Fact]
    public void GetRequiredCityCacheConnectionString_WhenMissing_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();

        var actual = Assert.Throws<InvalidOperationException>(
            () => configuration.GetRequiredCityCacheConnectionString());

        Assert.Contains("ConnectionStrings:CityCache", actual.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetRequiredCityCacheConnectionString_WhenBlank_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CityCache"] = " ",
            })
            .Build();

        var actual = Assert.Throws<InvalidOperationException>(
            () => configuration.GetRequiredCityCacheConnectionString());

        Assert.Contains("ConnectionStrings:CityCache", actual.Message, StringComparison.Ordinal);
    }
}
