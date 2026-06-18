using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace STI.City.Data.Schema;

/// <summary>
/// Creates the <c>GeocodingCache</c> table idempotently during startup, before
/// the application accepts requests.
/// </summary>
public sealed class GeocodingCacheSchemaInitializer
{
    public const string ConnectionStringName = "CityCache";

    private const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS GeocodingCache (
            NormalizedCityName TEXT NOT NULL PRIMARY KEY,
            DisplayName        TEXT NOT NULL,
            Country            TEXT NOT NULL,
            Latitude           REAL NOT NULL CHECK (Latitude BETWEEN -90 AND 90),
            Longitude          REAL NOT NULL CHECK (Longitude BETWEEN -180 AND 180),
            Population         INTEGER NULL CHECK (Population IS NULL OR Population >= 0),
            RetrievedAtUtc     TEXT NOT NULL
        );
        """;

    private readonly string _connectionString;

    public GeocodingCacheSchemaInitializer(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} is not configured.");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            new CommandDefinition(CreateTableSql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
