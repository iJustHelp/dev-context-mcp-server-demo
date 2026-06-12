using Demo.CityApi.Caching;
using Microsoft.Data.Sqlite;

namespace Demo.CityApi.Tests;

public sealed class GeocodingCacheRepositoryTests
{
    [Fact]
    public async Task SchemaInitializationCreatesTableAndUniqueIndex()
    {
        await using var database = new TemporarySqliteDatabase();

        await database.Initializer.InitializeAsync();

        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE (type = 'table' AND name = 'GeocodingCache')
               OR (type = 'index' AND name = 'IX_GeocodingCache_NormalizedCityName');
            """;

        var objectCount = (long)(await command.ExecuteScalarAsync())!;

        Assert.Equal(2, objectCount);
    }

    [Fact]
    public async Task RepeatedInitializationPreservesData()
    {
        await using var database = new TemporarySqliteDatabase();
        await database.Initializer.InitializeAsync();
        await database.Repository.InsertAsync(CreateEntry());

        await database.Initializer.InitializeAsync();

        var entry = await database.Repository.GetByCityNameAsync("chicago");
        Assert.NotNull(entry);
    }

    [Fact]
    public async Task RecordRoundTripsWithAllFieldsIntact()
    {
        await using var database = new TemporarySqliteDatabase();
        await database.Initializer.InitializeAsync();
        var expected = CreateEntry();

        var inserted = await database.Repository.InsertAsync(expected);
        var actual = await database.Repository.GetByCityNameAsync("CHICAGO");

        Assert.True(inserted.Id > 0);
        Assert.NotNull(actual);
        Assert.Equal(inserted.Id, actual.Id);
        Assert.Equal("CHICAGO", actual.NormalizedCityName);
        Assert.Equal(expected.DisplayCityName, actual.DisplayCityName);
        Assert.Equal(expected.Country, actual.Country);
        Assert.Equal(expected.Latitude, actual.Latitude);
        Assert.Equal(expected.Longitude, actual.Longitude);
        Assert.Equal(expected.Population, actual.Population);
        Assert.Equal(expected.RetrievedAtUtc, actual.RetrievedAtUtc);
    }

    [Fact]
    public async Task LookupUsesNormalizedCityName()
    {
        await using var database = new TemporarySqliteDatabase();
        await database.Initializer.InitializeAsync();
        await database.Repository.InsertAsync(CreateEntry());

        var entry = await database.Repository.GetByCityNameAsync("  chicago  ");

        Assert.NotNull(entry);
        Assert.Equal("CHICAGO", entry.NormalizedCityName);
    }

    [Fact]
    public async Task UniqueIndexPreventsDuplicateNormalizedNames()
    {
        await using var database = new TemporarySqliteDatabase();
        await database.Initializer.InitializeAsync();

        await InsertDirectlyAsync(database, "CHICAGO");

        var exception = await Assert.ThrowsAsync<SqliteException>(
            () => InsertDirectlyAsync(database, "CHICAGO"));

        Assert.Equal(2067, exception.SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task DuplicateInsertReturnsExistingRecord()
    {
        await using var database = new TemporarySqliteDatabase();
        await database.Initializer.InitializeAsync();
        var existing = await database.Repository.InsertAsync(CreateEntry());
        var duplicate = CreateEntry();
        duplicate.Country = "Different";

        var result = await database.Repository.InsertAsync(duplicate);

        Assert.Equal(existing.Id, result.Id);
        Assert.Equal(existing.Country, result.Country);
    }

    [Fact]
    public async Task NullablePopulationRoundTripsAsNull()
    {
        await using var database = new TemporarySqliteDatabase();
        await database.Initializer.InitializeAsync();
        var expected = CreateEntry();
        expected.Population = null;

        await database.Repository.InsertAsync(expected);
        var actual = await database.Repository.GetByCityNameAsync("CHICAGO");

        Assert.NotNull(actual);
        Assert.Null(actual.Population);
    }

    private static GeocodingCacheEntry CreateEntry()
    {
        return new GeocodingCacheEntry
        {
            NormalizedCityName = " chicago ",
            DisplayCityName = "Chicago",
            Country = "United States",
            Latitude = 41.85003,
            Longitude = -87.65005,
            Population = 2_746_388,
            RetrievedAtUtc = new DateTimeOffset(
                2026,
                6,
                12,
                12,
                30,
                45,
                TimeSpan.Zero),
        };
    }

    private static async Task InsertDirectlyAsync(
        TemporarySqliteDatabase database,
        string normalizedCityName)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO GeocodingCache (
                NormalizedCityName,
                DisplayCityName,
                Country,
                Latitude,
                Longitude,
                Population,
                RetrievedAtUtc)
            VALUES (
                $normalizedCityName,
                'Chicago',
                'United States',
                41.85003,
                -87.65005,
                NULL,
                '2026-06-12T12:30:45+00:00');
            """;
        command.Parameters.AddWithValue("$normalizedCityName", normalizedCityName);

        await command.ExecuteNonQueryAsync();
    }
}
