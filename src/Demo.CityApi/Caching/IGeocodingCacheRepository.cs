namespace Demo.CityApi.Caching;

public interface IGeocodingCacheRepository
{
    Task<GeocodingCacheEntry?> GetByCityNameAsync(
        string cityName,
        CancellationToken cancellationToken = default);

    Task<GeocodingCacheEntry> InsertAsync(
        GeocodingCacheEntry entry,
        CancellationToken cancellationToken = default);
}
