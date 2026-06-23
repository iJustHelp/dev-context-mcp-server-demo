using Demo.Cities;
using STI.City.Core.Abstractions;
using STI.City.Core.Exceptions;
using STI.City.Core.Models;
using STI.City.Core.Time;

namespace STI.City.Core.Services;

/// <summary>
/// Cache-aside orchestration for the city detail endpoints.
///
/// Flow (per the functional spec):
/// <list type="number">
///   <item>Resolve the requested name against <c>Demo.Cities</c>; unknown → 404, no upstream call (FR-6).</item>
///   <item>Check the SQLite cache by normalized name; hit → serve without calling Open-Meteo (FR-8).</item>
///   <item>Miss → call the provider. Empty result → 404 (FR-13); failure → 502 (FR-14).</item>
///   <item>Otherwise persist the single record and return it (FR-9, FR-10, FR-11).</item>
/// </list>
/// </summary>
public sealed class CityGeocodingService : ICityGeocodingService
{
    private readonly ICityCatalog _catalog;
    private readonly IGeocodingCacheRepository _cache;
    private readonly IGeocodingProvider _provider;
    private readonly IClock _clock;

    public CityGeocodingService(
        ICityCatalog catalog,
        IGeocodingCacheRepository cache,
        IGeocodingProvider provider,
        IClock clock)
    {
        _catalog = catalog;
        _cache = cache;
        _provider = provider;
        _clock = clock;
    }

    public IReadOnlyList<string> GetCityNames() => _catalog.GetAllCityNames();

    public IReadOnlyList<string> GetUsaCityNames() => _catalog.GetUsaCityNames();

    public async Task<GeocodingLookup> GetGeocodingAsync(string cityName, CancellationToken cancellationToken = default)
    {
        // FR-6: city must exist in Demo.Cities; otherwise 404 and no upstream call.
        var canonicalName = _catalog.ResolveCanonicalName(cityName);
        if (canonicalName is null)
        {
            return GeocodingLookup.CityNotFound();
        }

        // VR-3 / FR-11: the normalized name is the cache key (one record per city).
        var normalizedName = Extensions.ToCityName(canonicalName);

        // FR-8: cache-aside read — a hit never calls Open-Meteo.
        var cached = await _cache.GetAsync(normalizedName, cancellationToken);
        if (cached is not null)
        {
            return GeocodingLookup.Found(cached);
        }

        // FR-9 / FR-13 / FR-14: cache miss → call the provider.
        CityGeocoding? result;
        try
        {
            result = await _provider.FindAsync(canonicalName, cancellationToken);
        }
        catch (GeocodingUnavailableException)
        {
            return GeocodingLookup.UpstreamUnavailable();
        }

        if (result is null)
        {
            return GeocodingLookup.NoGeocodingResult();
        }

        // FR-9 / FR-11: persist exactly one record keyed by the normalized name.
        var record = result with
        {
            NormalizedName = normalizedName,
            RetrievedAtUtc = _clock.UtcNow,
        };

        await _cache.UpsertAsync(record, cancellationToken);
        return GeocodingLookup.Found(record);
    }
}
