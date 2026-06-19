using Dapper;
using Formula.SimpleRepo;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using STI.City.Core.Models;
using STI.City.Core.Repositories;
using STI.City.Data.Entities;

namespace STI.City.Data.Repositories;

public sealed class SimpleRepoGeocodingCacheRepository(string connectionString)
    : RepositoryBase<GeocodingCacheEntity, object>(BuildConfiguration(connectionString)),
        IGeocodingCacheRepository
{
    public async Task<GeocodingCacheRecord?> GetAsync(
        string normalizedCityName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        var command = new CommandDefinition(
            commandText: """
            SELECT NormalizedCityName, DisplayName, Country, Latitude, Longitude, Population, RetrievedAtUtc
            FROM GeocodingCache
            WHERE NormalizedCityName = @NormalizedCityName
            """,
            parameters: new { NormalizedCityName = normalizedCityName },
            cancellationToken: cancellationToken);

        var entity = await connection.QuerySingleOrDefaultAsync<GeocodingCacheEntity>(command)
            .ConfigureAwait(false);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task UpsertAsync(
        GeocodingCacheRecord record,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        var command = new CommandDefinition(
            commandText: """
            INSERT INTO GeocodingCache (
                NormalizedCityName,
                DisplayName,
                Country,
                Latitude,
                Longitude,
                Population,
                RetrievedAtUtc)
            VALUES (
                @NormalizedCityName,
                @DisplayName,
                @Country,
                @Latitude,
                @Longitude,
                @Population,
                @RetrievedAtUtc)
            ON CONFLICT(NormalizedCityName) DO UPDATE SET
                DisplayName = excluded.DisplayName,
                Country = excluded.Country,
                Latitude = excluded.Latitude,
                Longitude = excluded.Longitude,
                Population = excluded.Population,
                RetrievedAtUtc = excluded.RetrievedAtUtc
            """,
            parameters: new
            {
                record.NormalizedCityName,
                record.DisplayName,
                record.Country,
                record.Latitude,
                record.Longitude,
                record.Population,
                RetrievedAtUtc = record.RetrievedAtUtc.UtcDateTime.ToString("O"),
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    private static GeocodingCacheRecord ToRecord(GeocodingCacheEntity entity) =>
        new(
            NormalizedCityName: entity.NormalizedCityName,
            DisplayName: entity.DisplayName,
            Country: entity.Country,
            Latitude: entity.Latitude,
            Longitude: entity.Longitude,
            Population: entity.Population,
            RetrievedAtUtc: DateTimeOffset.Parse(entity.RetrievedAtUtc, CultureInfo.InvariantCulture));

    private static IConfiguration BuildConfiguration(string connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CityCache"] = connectionString,
            })
            .Build();
}
