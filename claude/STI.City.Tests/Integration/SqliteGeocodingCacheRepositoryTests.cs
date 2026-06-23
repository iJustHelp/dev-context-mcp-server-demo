using Microsoft.Data.Sqlite;
using STI.City.Core.Models;
using STI.City.Data;
using Xunit;

namespace STI.City.Tests.Integration;

/// <summary>
/// Integration tests against a real SQLite database (temp file), per the repository
/// guidance that SQLite persistence is exercised with a real implementation.
/// </summary>
public sealed class SqliteGeocodingCacheRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteGeocodingCacheRepository _repository;

    public SqliteGeocodingCacheRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"city-cache-{Guid.NewGuid():N}.db");
        var options = new CityDataOptions { ConnectionString = $"Data Source={_dbPath}" };
        _repository = new SqliteGeocodingCacheRepository(options);
        _repository.InitializeAsync().GetAwaiter().GetResult();
    }

    private static CityGeocoding Sample(string normalized = "paris", int? population = 2_165_000) => new()
    {
        NormalizedName = normalized,
        DisplayName = "Paris",
        Country = "France",
        Latitude = 48.8566,
        Longitude = 2.3522,
        Population = population,
        RetrievedAtUtc = new DateTimeOffset(2026, 6, 23, 10, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public async Task GetAsync_returns_null_on_miss()
    {
        Assert.Null(await _repository.GetAsync("unknown"));
    }

    [Fact]
    public async Task UpsertAsync_then_GetAsync_round_trips_all_fields()
    {
        var record = Sample();

        await _repository.UpsertAsync(record);
        var loaded = await _repository.GetAsync("paris");

        Assert.NotNull(loaded);
        Assert.Equal(record, loaded);
    }

    [Fact]
    public async Task GetAsync_preserves_null_population()
    {
        await _repository.UpsertAsync(Sample(population: null));

        var loaded = await _repository.GetAsync("paris");

        Assert.NotNull(loaded);
        Assert.Null(loaded!.Population);
    }

    [Fact] // FR-11: at most one record per normalized name
    public async Task UpsertAsync_twice_keeps_single_updated_record()
    {
        await _repository.UpsertAsync(Sample(population: 1));
        await _repository.UpsertAsync(Sample(population: 999));

        var loaded = await _repository.GetAsync("paris");
        Assert.Equal(999, loaded!.Population);

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM GeocodingCache WHERE NormalizedName = 'paris';";
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        Assert.Equal(1, count);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
