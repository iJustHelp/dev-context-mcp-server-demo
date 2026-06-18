using STI.City.Core.Models;
using STI.City.Data.Repositories;

namespace STI.City.Tests.Integration;

public sealed class SqliteCacheProbe(string connectionString)
{
    public Task<GeocodingCacheRecord?> GetAsync(string key) =>
        new SimpleRepoGeocodingCacheRepository(connectionString).GetAsync(key);

    public Task SeedAsync(GeocodingCacheRecord record) =>
        new SimpleRepoGeocodingCacheRepository(connectionString).UpsertAsync(record);

    public async Task<long> CountAsync()
    {
        await using var connection = new SqliteConnection(connectionString);
        return await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM GeocodingCache");
    }
}
