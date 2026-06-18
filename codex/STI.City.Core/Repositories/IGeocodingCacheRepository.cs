using STI.City.Core.Models;

namespace STI.City.Core.Repositories;

public interface IGeocodingCacheRepository
{
    Task<GeocodingCacheRecord?> GetAsync(
        string normalizedCityName,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        GeocodingCacheRecord record,
        CancellationToken cancellationToken = default);
}
