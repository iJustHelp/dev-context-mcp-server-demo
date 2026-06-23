namespace STI.City.Core.Models;

/// <summary>Outcome of a city detail (location/population) lookup.</summary>
public enum GeocodingStatus
{
    /// <summary>A geocoding record is available (from cache or freshly fetched).</summary>
    Found,

    /// <summary>The requested name is not in <c>Demo.Cities</c> (FR-6) → 404.</summary>
    CityNotFound,

    /// <summary>Open-Meteo returned no matching result (FR-13) → 404.</summary>
    NoGeocodingResult,

    /// <summary>Open-Meteo failed and nothing was cached (FR-14) → 502.</summary>
    UpstreamUnavailable,
}

/// <summary>Result of <see cref="Services.ICityGeocodingService.GetGeocodingAsync"/>.</summary>
public sealed record GeocodingLookup(GeocodingStatus Status, CityGeocoding? Record)
{
    public static GeocodingLookup Found(CityGeocoding record) => new(GeocodingStatus.Found, record);
    public static GeocodingLookup CityNotFound() => new(GeocodingStatus.CityNotFound, null);
    public static GeocodingLookup NoGeocodingResult() => new(GeocodingStatus.NoGeocodingResult, null);
    public static GeocodingLookup UpstreamUnavailable() => new(GeocodingStatus.UpstreamUnavailable, null);
}
