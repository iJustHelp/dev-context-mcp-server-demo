using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using STI.City.Core.Abstractions;

namespace STI.City.Tests.Api;

/// <summary>
/// Hosts the real API (real Demo.Cities catalog and endpoint wiring) but swaps the
/// geocoding provider and cache for in-process fakes, so endpoint status mapping can
/// be asserted without a network or database.
/// </summary>
public sealed class CityApiFactory : WebApplicationFactory<Program>
{
    public FakeGeocodingProvider Provider { get; } = new();
    public InMemoryGeocodingCacheRepository Cache { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Satisfy the startup connection-string check without using SQLite.
        builder.UseSetting("ConnectionStrings:CityCache", "Data Source=:memory:");
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGeocodingProvider>();
            services.RemoveAll<IGeocodingCacheRepository>();
            services.AddSingleton<IGeocodingProvider>(Provider);
            services.AddSingleton<IGeocodingCacheRepository>(Cache);
        });
    }
}
