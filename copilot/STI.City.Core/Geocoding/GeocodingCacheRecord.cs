namespace STI.City.Core.Geocoding
{
    public sealed class GeocodingCacheRecord
    {
        public string NormalizedCityName { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string Country { get; init; } = string.Empty;

        public double Latitude { get; init; }

        public double Longitude { get; init; }

        public long? Population { get; init; }

        public DateTime RetrievedUtc { get; init; }
    }
}
