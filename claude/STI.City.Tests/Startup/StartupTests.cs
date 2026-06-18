using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace STI.City.Tests.Startup;

/// <summary>
/// Startup tests proving the application fails fast when the required cache
/// connection string is missing or blank.
/// </summary>
public sealed class StartupTests
{
    [Fact]
    public void Startup_BlankCacheConnectionString_FailsFast()
    {
        // Purpose: a blank ConnectionStrings:CityCache must stop startup.
        using var factory = CreateFactory(connectionString: "   ");

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    [Fact]
    public void Startup_MissingCacheConnectionString_FailsFast()
    {
        // Purpose: a missing ConnectionStrings:CityCache must stop startup.
        using var factory = CreateFactory(connectionString: null);

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    private static WebApplicationFactory<Program> CreateFactory(string? connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                // Drop any existing value, then apply the test override.
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:CityCache"] = connectionString,
                });
            });
        });
}
