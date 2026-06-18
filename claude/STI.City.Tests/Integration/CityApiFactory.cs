using Demo.Cities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using OpenMeteo.Api.Client;

namespace STI.City.Tests.Integration;

/// <summary>
/// Hosts the API through <see cref="WebApplicationFactory{TEntryPoint}"/> with
/// deterministic package test doubles and an isolated SQLite database per
/// factory instance.
/// </summary>
public sealed class CityApiFactory : WebApplicationFactory<Program>
{
    public Mock<ICityService> CityService { get; } = new();

    public Mock<IUsaCityService> UsaCityService { get; } = new();

    public Mock<IOpenMeteoClient> OpenMeteoClient { get; } = new();

    /// <summary>Optional extra service overrides applied last (e.g. a failing repository).</summary>
    public Action<IServiceCollection>? ConfigureExtraServices { get; set; }

    public string DatabasePath { get; } =
        Path.Combine(Path.GetTempPath(), $"city-cache-api-{Guid.NewGuid():N}.db");

    public string ConnectionString => $"Data Source={DatabasePath}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CityCache"] = ConnectionString,
            }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICityService>();
            services.RemoveAll<IUsaCityService>();
            services.RemoveAll<IOpenMeteoClient>();

            services.AddSingleton(CityService.Object);
            services.AddSingleton(UsaCityService.Object);
            services.AddSingleton(OpenMeteoClient.Object);

            ConfigureExtraServices?.Invoke(services);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { DatabasePath, DatabasePath + "-wal", DatabasePath + "-shm" })
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup of the isolated test database.
            }
        }
    }
}
