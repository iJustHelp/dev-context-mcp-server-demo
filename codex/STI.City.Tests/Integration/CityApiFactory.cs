using Demo.Cities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OpenMeteo.Api.Client;
using STI.City.Core.Repositories;
using STI.City.Data.DependencyInjection;
using STI.City.Data.Schema;

namespace STI.City.Tests.Integration;

public sealed class CityApiFactory : WebApplicationFactory<Program>
{
    public Mock<ICityService> CityService { get; } = new(MockBehavior.Strict);

    public Mock<IUsaCityService> UsaCityService { get; } = new(MockBehavior.Strict);

    public Mock<IOpenMeteoClient> OpenMeteoClient { get; } = new(MockBehavior.Strict);

    public string ConnectionString { get; } = new SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(Path.GetTempPath(), $"city-api-{Guid.NewGuid():N}.db"),
    }.ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CityCache"] = ConnectionString,
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICityService>();
            services.RemoveAll<IUsaCityService>();
            services.RemoveAll<IOpenMeteoClient>();
            services.RemoveAll<TimeProvider>();
            services.RemoveAll<IGeocodingCacheRepository>();
            services.RemoveAll<GeocodingCacheSchemaInitializer>();

            services.AddSingleton(CityService.Object);
            services.AddSingleton(UsaCityService.Object);
            services.AddSingleton(OpenMeteoClient.Object);
            services.AddSingleton<TimeProvider>(new FixedTimeProvider());
            services.AddCityData(ConnectionString);
        });
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(
                year: 2026,
                month: 6,
                day: 18,
                hour: 12,
                minute: 0,
                second: 0,
                offset: TimeSpan.Zero);
    }
}
