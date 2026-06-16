using STI.City.Core.Models;

namespace STI.City.Core.Services;

/// <summary>Outcome of a city geocoding lookup, mapped to HTTP results by the API.</summary>
public enum CityGeocodingStatus
{
    /// <summary><see cref="CityGeocodingResult.Record"/> holds the shared location and population data.</summary>
    Success,

    /// <summary>The trimmed city name is empty or not present in the supported city list.</summary>
    CityNotFound,

    /// <summary>Open-Meteo returned no result whose name exactly matches the canonical city.</summary>
    GeocodingNotFound,

    /// <summary>Open-Meteo failed or timed out on a cache miss.</summary>
    ServiceUnavailable,
}

/// <summary>
/// Explicit result of <see cref="ICityGeocodingService"/>. Persistence failures
/// and request cancellation propagate as exceptions rather than outcomes.
/// </summary>
public sealed record CityGeocodingResult
{
    private CityGeocodingResult(CityGeocodingStatus status, GeocodingCacheRecord? record)
    {
        Status = status;
        Record = record;
    }

    public CityGeocodingStatus Status { get; }

    /// <summary>The cached/retrieved record; non-null only when <see cref="Status"/> is <see cref="CityGeocodingStatus.Success"/>.</summary>
    public GeocodingCacheRecord? Record { get; }

    public static CityGeocodingResult Success(GeocodingCacheRecord record) =>
        new(CityGeocodingStatus.Success, record);

    public static readonly CityGeocodingResult CityNotFound =
        new(CityGeocodingStatus.CityNotFound, null);

    public static readonly CityGeocodingResult GeocodingNotFound =
        new(CityGeocodingStatus.GeocodingNotFound, null);

    public static readonly CityGeocodingResult ServiceUnavailable =
        new(CityGeocodingStatus.ServiceUnavailable, null);
}
