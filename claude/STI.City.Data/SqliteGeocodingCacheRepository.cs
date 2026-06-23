using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using STI.City.Core.Abstractions;
using STI.City.Core.Models;

namespace STI.City.Data;

/// <summary>
/// SQLite-backed <see cref="IGeocodingCacheRepository"/> using Dapper over
/// <c>Microsoft.Data.Sqlite</c>. <c>NormalizedName</c> is the primary key, which
/// enforces a single record per city (FR-11); <see cref="UpsertAsync"/> uses an
/// <c>ON CONFLICT</c> upsert so re-fetching a city replaces rather than duplicates.
/// </summary>
public sealed class SqliteGeocodingCacheRepository : IGeocodingCacheRepository
{
    private const string TableName = "GeocodingCache";

    private readonly string _connectionString;

    public SqliteGeocodingCacheRepository(CityDataOptions options)
    {
        _connectionString = options.ConnectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var createTable = new CommandDefinition(
            $"""
            CREATE TABLE IF NOT EXISTS {TableName} (
                NormalizedName TEXT    NOT NULL PRIMARY KEY,
                DisplayName    TEXT    NOT NULL,
                Country        TEXT    NULL,
                Latitude       REAL    NOT NULL,
                Longitude      REAL    NOT NULL,
                Population     INTEGER NULL,
                RetrievedAtUtc TEXT    NOT NULL
            );
            """,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(createTable);
    }

    public async Task<CityGeocoding?> GetAsync(string normalizedName, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var query = new CommandDefinition(
            $"""
            SELECT NormalizedName, DisplayName, Country, Latitude, Longitude, Population, RetrievedAtUtc
            FROM {TableName}
            WHERE NormalizedName = @normalizedName;
            """,
            new { normalizedName },
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<CacheRow>(query);
        return row?.ToModel();
    }

    public async Task UpsertAsync(CityGeocoding record, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var upsert = new CommandDefinition(
            $"""
            INSERT INTO {TableName}
                (NormalizedName, DisplayName, Country, Latitude, Longitude, Population, RetrievedAtUtc)
            VALUES
                (@NormalizedName, @DisplayName, @Country, @Latitude, @Longitude, @Population, @RetrievedAtUtc)
            ON CONFLICT(NormalizedName) DO UPDATE SET
                DisplayName    = excluded.DisplayName,
                Country        = excluded.Country,
                Latitude       = excluded.Latitude,
                Longitude      = excluded.Longitude,
                Population     = excluded.Population,
                RetrievedAtUtc = excluded.RetrievedAtUtc;
            """,
            new
            {
                record.NormalizedName,
                record.DisplayName,
                record.Country,
                record.Latitude,
                record.Longitude,
                record.Population,
                RetrievedAtUtc = record.RetrievedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(upsert);
    }

    /// <summary>Row shape used for Dapper materialization (timestamp stored as ISO-8601 text).</summary>
    private sealed class CacheRow
    {
        public string NormalizedName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? Country { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public long? Population { get; init; }
        public string RetrievedAtUtc { get; init; } = string.Empty;

        public CityGeocoding ToModel() => new()
        {
            NormalizedName = NormalizedName,
            DisplayName = DisplayName,
            Country = Country,
            Latitude = Latitude,
            Longitude = Longitude,
            Population = Population is null ? null : checked((int)Population.Value),
            RetrievedAtUtc = DateTimeOffset.Parse(RetrievedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        };
    }
}
