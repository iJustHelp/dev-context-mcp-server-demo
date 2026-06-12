using Microsoft.Data.Sqlite;

namespace Demo.CityApi.Caching;

public sealed class CityCacheSchemaInitializer(IConfiguration configuration)
    : ICityCacheSchemaInitializer
{
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS GeocodingCache (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            NormalizedCityName TEXT NOT NULL,
            DisplayCityName TEXT NOT NULL,
            Country TEXT NOT NULL,
            Latitude REAL NOT NULL,
            Longitude REAL NOT NULL,
            Population INTEGER NULL,
            RetrievedAtUtc TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS IX_GeocodingCache_NormalizedCityName
            ON GeocodingCache (NormalizedCityName);
        """;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString(
            CityCacheConfiguration.ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{CityCacheConfiguration.ConnectionStringName}' is required.");
        }

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = SchemaSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
