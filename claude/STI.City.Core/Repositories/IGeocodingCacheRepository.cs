using STI.City.Core.Models;

namespace STI.City.Core.Repositories;

/// <summary>
/// Abstracts geocoding cache retrieval and atomic insert/upsert. A cache miss
/// returns <c>null</c>; storage failures surface as exceptions so callers can
/// distinguish them from a miss.
/// </summary>
public interface IGeocodingCacheRepository
{
    Task<GeocodingCacheRecord?> GetAsync(
        string normalizedCityName,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        GeocodingCacheRecord record,
        CancellationToken cancellationToken = default);
}
