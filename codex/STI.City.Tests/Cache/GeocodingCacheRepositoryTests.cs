using STI.City.Core.Models;
using STI.City.Data.Repositories;
using STI.City.Data.Schema;

namespace STI.City.Tests.Cache;

public sealed class GeocodingCacheRepositoryTests
{
    [Fact]
    public async Task InitializeAsync_WhenCalledRepeatedly_CreatesSchemaIdempotently()
    {
        var connectionString = CreateConnectionString();
        var target = new GeocodingCacheSchemaInitializer(connectionString);

        await target.InitializeAsync();
        await target.InitializeAsync();

        await using var connection = new SqliteConnection(connectionString);
        var actual = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'GeocodingCache'");

        Assert.Equal(1, actual);
    }

    [Fact]
    public async Task UpsertAsync_WhenRecordIsInserted_RoundTripsAllFields()
    {
        var connectionString = CreateConnectionString();
        await new GeocodingCacheSchemaInitializer(connectionString).InitializeAsync();
        var target = new SimpleRepoGeocodingCacheRepository(connectionString);
        var expected = Record("NEW YORK", "New York", 8_804_190);

        await target.UpsertAsync(expected);
        var actual = await target.GetAsync("NEW YORK");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task UpsertAsync_WhenSameKeyIsWrittenTwice_LeavesOneUpdatedRow()
    {
        var connectionString = CreateConnectionString();
        await new GeocodingCacheSchemaInitializer(connectionString).InitializeAsync();
        var target = new SimpleRepoGeocodingCacheRepository(connectionString);

        await target.UpsertAsync(Record("NEW YORK", "New York", 100));
        await target.UpsertAsync(Record("NEW YORK", "New York", 200));

        var actual = await target.GetAsync("NEW YORK");
        await using var connection = new SqliteConnection(connectionString);
        var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM GeocodingCache");

        Assert.Equal(1, count);
        Assert.Equal(200, actual!.Population);
    }

    [Fact]
    public async Task GetAsync_WhenRecordDoesNotExist_ReturnsNull()
    {
        var connectionString = CreateConnectionString();
        await new GeocodingCacheSchemaInitializer(connectionString).InitializeAsync();
        var target = new SimpleRepoGeocodingCacheRepository(connectionString);

        var actual = await target.GetAsync("MISSING");

        Assert.Null(actual);
    }

    private static string CreateConnectionString() =>
        new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(Path.GetTempPath(), $"city-cache-{Guid.NewGuid():N}.db"),
        }.ToString();

    private static GeocodingCacheRecord Record(string key, string displayName, long? population) =>
        new(key, displayName, "United States", 40.7128, -74.006, population,
            new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.Zero));
}
