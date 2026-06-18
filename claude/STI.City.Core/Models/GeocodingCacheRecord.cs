namespace STI.City.Core.Models;

/// <summary>
/// Internal persistence/application record shared by the location and
/// population endpoints. One record is cached per normalized city name.
/// </summary>
public sealed record GeocodingCacheRecord(
    string NormalizedCityName,
    string DisplayName,
    string Country,
    double Latitude,
    double Longitude,
    long? Population,
    DateTimeOffset RetrievedAtUtc);
