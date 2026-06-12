using System.Collections;
using Formula.SimpleRepo;
using Microsoft.Data.Sqlite;

namespace Demo.CityApi.Caching;

public sealed class GeocodingCacheRepository(IConfiguration configuration)
    : RepositoryBase<GeocodingCacheEntry, GeocodingCacheEntry>(configuration),
        IGeocodingCacheRepository
{
    public async Task<GeocodingCacheEntry?> GetByCityNameAsync(
        string cityName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedCityName = CityNameNormalizer.Normalize(cityName);
        var entries = await GetAsync(new Hashtable
        {
            [nameof(GeocodingCacheEntry.NormalizedCityName)] = normalizedCityName,
        });

        cancellationToken.ThrowIfCancellationRequested();

        return entries.SingleOrDefault();
    }

    public async Task<GeocodingCacheEntry> InsertAsync(
        GeocodingCacheEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        entry.NormalizedCityName = CityNameNormalizer.Normalize(entry.NormalizedCityName);

        try
        {
            var id = await base.InsertAsync(entry);
            cancellationToken.ThrowIfCancellationRequested();

            if (id.HasValue)
            {
                entry.Id = id.Value;
            }

            return entry;
        }
        catch (SqliteException exception)
            when (exception.SqliteExtendedErrorCode == 2067)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var existing = await GetByCityNameAsync(
                entry.NormalizedCityName,
                cancellationToken);

            if (existing is not null)
            {
                return existing;
            }

            throw;
        }
    }
}
