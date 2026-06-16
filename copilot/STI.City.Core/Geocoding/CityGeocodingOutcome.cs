namespace STI.City.Core.Geocoding
{
    public sealed class CityGeocodingOutcome
    {
        public CityGeocodingStatus Status { get; init; }

        public GeocodingCacheRecord? Record { get; init; }

        public static CityGeocodingOutcome Success(GeocodingCacheRecord record) => new() { Status = CityGeocodingStatus.Success, Record = record };

        public static CityGeocodingOutcome CityNotFound() => new() { Status = CityGeocodingStatus.CityNotFound };

        public static CityGeocodingOutcome GeocodingNotFound() => new() { Status = CityGeocodingStatus.GeocodingNotFound };

        public static CityGeocodingOutcome ServiceUnavailable() => new() { Status = CityGeocodingStatus.ServiceUnavailable };
    }
}
