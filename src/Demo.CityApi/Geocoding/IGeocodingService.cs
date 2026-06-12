namespace Demo.CityApi.Geocoding;

public interface IGeocodingService
{
    Task<GeocodingLookupResult> GetAsync(
        string cityName,
        CancellationToken cancellationToken = default);
}
