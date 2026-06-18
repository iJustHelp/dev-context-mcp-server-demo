using STI.City.Core.Models;

namespace STI.City.Core.Services;

/// <summary>
/// Explicit outcome of a geocoding lookup. The transport layer maps each
/// status to the documented HTTP contract.
/// </summary>
public enum CityGeocodingStatus
{
    /// <summary><see cref="CityGeocodingResult.Record"/> carries the shared data.</summary>
    Success,

    /// <summary>The trimmed route value is empty or not in <c>ICityService</c>.</summary>
    CityNotFound,

    /// <summary>Open-Meteo returned no exact city-name match.</summary>
    GeocodingNotFound,

    /// <summary>Open-Meteo failed or timed out on a cache miss.</summary>
    ServiceUnavailable,
}

/// <summary>
/// Result of <see cref="ICityGeocodingService.GetGeocodingAsync"/>. A
/// successful result always carries a non-null <see cref="Record"/>.
/// </summary>
public sealed record CityGeocodingResult(CityGeocodingStatus Status, GeocodingCacheRecord? Record)
{
    public static CityGeocodingResult Success(GeocodingCacheRecord record) =>
        new(CityGeocodingStatus.Success, record);

    public static readonly CityGeocodingResult CityNotFound =
        new(CityGeocodingStatus.CityNotFound, null);

    public static readonly CityGeocodingResult GeocodingNotFound =
        new(CityGeocodingStatus.GeocodingNotFound, null);

    public static readonly CityGeocodingResult ServiceUnavailable =
        new(CityGeocodingStatus.ServiceUnavailable, null);
}
