using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using STI.City.Core.Models;
using STI.City.Data.Repositories;
using STI.City.Data.Schema;

namespace STI.City.Tests.Cache;

/// <summary>
/// Integration tests for the SimpleRepo-backed SQLite cache repository. Each
/// test uses an isolated database file, so these intentionally use real
/// persistence rather than mocks.
/// </summary>
public sealed class GeocodingCacheRepositoryTests : IDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"city-cache-repo-{Guid.NewGuid():N}.db");

    private readonly IConfiguration _configuration;
    private readonly SimpleRepoGeocodingCacheRepository _repository;
    private readonly GeocodingCacheSchemaInitializer _schemaInitializer;

    public GeocodingCacheRepositoryTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CityCache"] = $"Data Source={_databasePath}",
            })
            .Build();

        _repository = new SimpleRepoGeocodingCacheRepository(_configuration);
        _schemaInitializer = new GeocodingCacheSchemaInitializer(_configuration);
    }

    [Fact]
    public async Task InitializeAsync_RunRepeatedly_DoesNotThrow()
    {
        // Purpose: schema initialization is idempotent.
        // arrange / act
        await _schemaInitializer.InitializeAsync();
        await _schemaInitializer.InitializeAsync();

        // assert
        Assert.Equal(0, CountRows());
    }

    [Fact]
    public async Task GetAsync_MissingKey_ReturnsNull()
    {
        // Purpose: a cache miss returns null (distinct from a SQLite failure).
        // arrange
        await _schemaInitializer.InitializeAsync();

        // act
        var actual = await _repository.GetAsync("NEW YORK");

        // assert
        Assert.Null(actual);
    }

    [Fact]
    public async Task GetAsync_BeforeSchemaInitialized_ThrowsSqliteException()
    {
        // Purpose: SQLite failures surface as exceptions, not as cache misses.
        // arrange / act / assert
        await Assert.ThrowsAsync<SqliteException>(() => _repository.GetAsync("NEW YORK"));
    }

    [Fact]
    public async Task UpsertThenGet_RoundTripsAllFieldsIncludingNullPopulation()
    {
        // Purpose: every field, including null population and the UTC timestamp, round-trips.
        // arrange
        await _schemaInitializer.InitializeAsync();
        var retrievedAt = new DateTimeOffset(2026, 6, 17, 8, 30, 15, TimeSpan.Zero);
        var record = new GeocodingCacheRecord(
            "NEW YORK", "New York", "United States", 40.7128, -74.006, null, retrievedAt);

        // act
        await _repository.UpsertAsync(record);
        var actual = await _repository.GetAsync("NEW YORK");

        // assert
        Assert.NotNull(actual);
        Assert.Equal(record.NormalizedCityName, actual!.NormalizedCityName);
        Assert.Equal(record.DisplayName, actual.DisplayName);
        Assert.Equal(record.Country, actual.Country);
        Assert.Equal(record.Latitude, actual.Latitude);
        Assert.Equal(record.Longitude, actual.Longitude);
        Assert.Null(actual.Population);
        Assert.Equal(retrievedAt, actual.RetrievedAtUtc);
    }

    [Fact]
    public async Task UpsertThenGet_RoundTripsPopulation()
    {
        // Purpose: a present population round-trips as a 64-bit integer.
        // arrange
        await _schemaInitializer.InitializeAsync();
        var record = new GeocodingCacheRecord(
            "NEW YORK", "New York", "United States", 40.7128, -74.006, 8_804_190, DateTimeOffset.UtcNow);

        // act
        await _repository.UpsertAsync(record);
        var actual = await _repository.GetAsync("NEW YORK");

        // assert
        Assert.Equal(8_804_190, actual!.Population);
    }

    [Fact]
    public async Task UpsertAsync_RepeatedForSameKey_LeavesExactlyOneUpdatedRow()
    {
        // Purpose: the primary key + atomic upsert keep one row per normalized city name.
        // arrange
        await _schemaInitializer.InitializeAsync();
        var first = new GeocodingCacheRecord(
            "NEW YORK", "New York", "United States", 40.0, -74.0, 1, DateTimeOffset.UtcNow);
        var second = new GeocodingCacheRecord(
            "NEW YORK", "New York", "United States", 41.0, -75.0, 2, DateTimeOffset.UtcNow);

        // act
        await _repository.UpsertAsync(first);
        await _repository.UpsertAsync(second);
        var actual = await _repository.GetAsync("NEW YORK");

        // assert
        Assert.Equal(1, CountRows());
        Assert.Equal(2, actual!.Population);
        Assert.Equal(41.0, actual.Latitude);
    }

    private int CountRows()
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM GeocodingCache;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" })
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
