using STI.City.Core.Models;

namespace STI.City.Core.Abstractions;

/// <summary>
/// Cache-aside store for geocoding records (FR-8…FR-11). Keyed by the normalized
/// city name, with at most one record per key.
/// </summary>
public interface IGeocodingCacheRepository
{
    /// <summary>Ensures the backing store exists (e.g. creates the table).</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the cached record for <paramref name="normalizedName"/>, or <c>null</c> on a miss.</summary>
    Task<CityGeocoding?> GetAsync(string normalizedName, CancellationToken cancellationToken = default);

    /// <summary>Inserts or replaces the record, keeping a single row per normalized name (FR-11).</summary>
    Task UpsertAsync(CityGeocoding record, CancellationToken cancellationToken = default);
}
