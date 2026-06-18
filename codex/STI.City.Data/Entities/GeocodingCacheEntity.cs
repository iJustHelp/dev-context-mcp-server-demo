namespace STI.City.Data.Entities;

public sealed class GeocodingCacheEntity
{
    public string NormalizedCityName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public long? Population { get; set; }

    public string RetrievedAtUtc { get; set; } = string.Empty;
}
