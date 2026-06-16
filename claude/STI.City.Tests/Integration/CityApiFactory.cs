using Demo.Cities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OpenMeteo.Api.Client;
using STI.City.Core.Models;
using STI.City.Data.Repositories;

namespace STI.City.Tests.Integration;

/// <summary>
/// Integration test host with deterministic package doubles, an isolated SQLite
/// database per instance, and the production exception handler enabled.
/// </summary>
public sealed class CityApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"city-it-{Guid.NewGuid():N}.db");

    public Mock<ICityService> CityService { get; } = new(MockBehavior.Strict);

    public Mock<IUsaCityService> UsaCityService { get; } = new(MockBehavior.Strict);

    public Mock<IOpenMeteoClient> OpenMeteo { get; } = new(MockBehavior.Strict);

    /// <summary>Optional extra service overrides applied during host build (e.g. a failing repository).</summary>
    public Action<IServiceCollection>? OverrideServices { get; set; }

    public string ConnectionString => $"Data Source={_dbPath};Pooling=False";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CityCache"] = ConnectionString,
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICityService>();
            services.AddSingleton(CityService.Object);

            services.RemoveAll<IUsaCityService>();
            services.AddSingleton(UsaCityService.Object);

            services.RemoveAll<IOpenMeteoClient>();
            services.AddSingleton(OpenMeteo.Object);

            OverrideServices?.Invoke(services);
        });
    }

    /// <summary>Inserts a row directly into the isolated cache database using the real repository.</summary>
    public Task SeedAsync(GeocodingCacheRecord record) =>
        new SimpleRepoGeocodingCacheRepository(BuildConfiguration()).UpsertAsync(record);

    public async Task<long> CountCacheRowsAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM GeocodingCache;";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CityCache"] = ConnectionString,
            })
            .Build();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of an isolated temp database.
        }
    }
}
