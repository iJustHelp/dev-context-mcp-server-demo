using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace STI.City.Data.Schema;

public sealed class GeocodingCacheSchemaInitializer(string connectionString)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        var command = new CommandDefinition(
            """
            CREATE TABLE IF NOT EXISTS GeocodingCache (
                NormalizedCityName TEXT NOT NULL PRIMARY KEY,
                DisplayName TEXT NOT NULL,
                Country TEXT NOT NULL,
                Latitude REAL NOT NULL CHECK (Latitude >= -90 AND Latitude <= 90),
                Longitude REAL NOT NULL CHECK (Longitude >= -180 AND Longitude <= 180),
                Population INTEGER NULL CHECK (Population IS NULL OR Population >= 0),
                RetrievedAtUtc TEXT NOT NULL
            )
            """,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command).ConfigureAwait(false);
    }
}

public static class GeocodingCacheSchemaInitializerExtensions
{
    public static Task InitializeGeocodingCacheSchemaAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default) =>
        services.GetRequiredService<GeocodingCacheSchemaInitializer>()
            .InitializeAsync(cancellationToken);
}
