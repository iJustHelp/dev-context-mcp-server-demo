using Demo.CityApi.Caching;

namespace Demo.CityApi.Geocoding;

public enum GeocodingLookupStatus
{
    Success,
    NotFound,
    UpstreamUnavailable,
}

public sealed record GeocodingLookupResult(
    GeocodingLookupStatus Status,
    GeocodingCacheEntry? Entry)
{
    public static GeocodingLookupResult Success(GeocodingCacheEntry entry) =>
        new(GeocodingLookupStatus.Success, entry);

    public static GeocodingLookupResult NotFound() =>
        new(GeocodingLookupStatus.NotFound, null);

    public static GeocodingLookupResult UpstreamUnavailable() =>
        new(GeocodingLookupStatus.UpstreamUnavailable, null);
}
