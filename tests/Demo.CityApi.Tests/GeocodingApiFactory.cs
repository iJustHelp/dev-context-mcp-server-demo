using Demo.CityApi.Caching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenMeteo.Api.Client;

namespace Demo.CityApi.Tests;

public sealed class GeocodingApiFactory(
    OpenMeteoStubHttpMessageHandler openMeteoHandler)
    : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"demo-city-geocoding-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Pooling = false,
        }.ToString();

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [
                    $"ConnectionStrings:{CityCacheConfiguration.ConnectionStringName}"
                ] = connectionString,
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOpenMeteoClient>();
            services
                .AddOpenMeteoApiClient()
                .ConfigurePrimaryHttpMessageHandler(() => openMeteoHandler)
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            TemporarySqliteDatabase.DeleteFiles(_databasePath);
        }
    }
}
