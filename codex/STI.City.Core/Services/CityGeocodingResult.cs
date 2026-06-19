using STI.City.Core.Models;

namespace STI.City.Core.Services;

public enum CityGeocodingStatus
{
    Success,
    CityNotFound,
    GeocodingNotFound,
    ServiceUnavailable,
}

public sealed record CityGeocodingResult(
    CityGeocodingStatus Status,
    GeocodingCacheRecord? Record = null)
{
    public static CityGeocodingResult Success(GeocodingCacheRecord record) =>
        new(Status: CityGeocodingStatus.Success, Record: record);

    public static CityGeocodingResult CityNotFound { get; } =
        new(Status: CityGeocodingStatus.CityNotFound);

    public static CityGeocodingResult GeocodingNotFound { get; } =
        new(Status: CityGeocodingStatus.GeocodingNotFound);

    public static CityGeocodingResult ServiceUnavailable { get; } =
        new(Status: CityGeocodingStatus.ServiceUnavailable);
}
