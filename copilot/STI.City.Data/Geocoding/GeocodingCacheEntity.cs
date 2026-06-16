using Dapper;
using Formula.SimpleRepo;
using STI.City.Core.Geocoding;

namespace STI.City.Data.Geocoding
{
    [Table("GeocodingCache")]
    [InsertSuffix("ON CONFLICT(NormalizedCityName) DO UPDATE SET DisplayName = excluded.DisplayName, Country = excluded.Country, Latitude = excluded.Latitude, Longitude = excluded.Longitude, Population = excluded.Population, RetrievedUtc = excluded.RetrievedUtc")]
    public sealed class GeocodingCacheEntity
    {
        [Key]
        public string NormalizedCityName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public long? Population { get; set; }

        public DateTime RetrievedUtc { get; set; }

        public GeocodingCacheRecord ToRecord() => new()
        {
            NormalizedCityName = NormalizedCityName,
            DisplayName = DisplayName,
            Country = Country,
            Latitude = Latitude,
            Longitude = Longitude,
            Population = Population,
            RetrievedUtc = RetrievedUtc
        };

        public static GeocodingCacheEntity FromRecord(GeocodingCacheRecord record) => new()
        {
            NormalizedCityName = record.NormalizedCityName,
            DisplayName = record.DisplayName,
            Country = record.Country,
            Latitude = record.Latitude,
            Longitude = record.Longitude,
            Population = record.Population,
            RetrievedUtc = record.RetrievedUtc
        };
    }
}
