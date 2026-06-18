using System.Globalization;
using Dapper;
using Formula.SimpleRepo;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using STI.City.Core.Models;
using STI.City.Core.Repositories;
using STI.City.Data.Entities;

namespace STI.City.Data.Repositories;

/// <summary>
/// SQLite-backed cache repository built on <c>Formula.SimpleRepo</c>. Reads and
/// the atomic insert/upsert run through Dapper against the SimpleRepo-resolved
/// connection so concurrent misses converge on a single row.
/// </summary>
[Repo]
[ConnectionDetails(
    GeocodingCacheSchemaInitializerConnection,
    typeof(SqliteConnection),
    Dapper.SimpleCRUD.Dialect.SQLite)]
public sealed class SimpleRepoGeocodingCacheRepository
    : RepositoryBase<GeocodingCacheEntity, GeocodingCacheConstraints>, IGeocodingCacheRepository
{
    private const string GeocodingCacheSchemaInitializerConnection = "CityCache";

    private const string SelectSql = """
        SELECT NormalizedCityName, DisplayName, Country, Latitude, Longitude, Population, RetrievedAtUtc
        FROM GeocodingCache
        WHERE NormalizedCityName = @NormalizedCityName;
        """;

    private const string UpsertSql = """
        INSERT INTO GeocodingCache
            (NormalizedCityName, DisplayName, Country, Latitude, Longitude, Population, RetrievedAtUtc)
        VALUES
            (@NormalizedCityName, @DisplayName, @Country, @Latitude, @Longitude, @Population, @RetrievedAtUtc)
        ON CONFLICT(NormalizedCityName) DO UPDATE SET
            DisplayName    = excluded.DisplayName,
            Country        = excluded.Country,
            Latitude       = excluded.Latitude,
            Longitude      = excluded.Longitude,
            Population     = excluded.Population,
            RetrievedAtUtc = excluded.RetrievedAtUtc;
        """;

    private readonly string _connectionString;

    public SimpleRepoGeocodingCacheRepository(IConfiguration configuration)
        : base(configuration)
    {
        _connectionString = configuration.GetConnectionString(GeocodingCacheSchemaInitializerConnection)
            ?? throw new InvalidOperationException(
                $"ConnectionStrings:{GeocodingCacheSchemaInitializerConnection} is not configured.");
    }

    public async Task<GeocodingCacheRecord?> GetAsync(
        string normalizedCityName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var command = new CommandDefinition(
            SelectSql,
            new { NormalizedCityName = normalizedCityName },
            cancellationToken: cancellationToken);

        var entity = await connection
            .QuerySingleOrDefaultAsync<GeocodingCacheEntity>(command)
            .ConfigureAwait(false);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task UpsertAsync(
        GeocodingCacheRecord record,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var command = new CommandDefinition(
            UpsertSql,
            new
            {
                record.NormalizedCityName,
                record.DisplayName,
                record.Country,
                record.Latitude,
                record.Longitude,
                record.Population,
                RetrievedAtUtc = record.RetrievedAtUtc.UtcDateTime
                    .ToString("O", CultureInfo.InvariantCulture),
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    private static GeocodingCacheRecord ToRecord(GeocodingCacheEntity entity) => new(
        NormalizedCityName: entity.NormalizedCityName,
        DisplayName: entity.DisplayName,
        Country: entity.Country,
        Latitude: entity.Latitude,
        Longitude: entity.Longitude,
        Population: entity.Population,
        RetrievedAtUtc: DateTimeOffset.Parse(
            entity.RetrievedAtUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal));
}
