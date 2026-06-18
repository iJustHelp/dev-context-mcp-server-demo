namespace STI.City.Core.Services;

/// <summary>
/// Exposes the single cached geocoding lookup used by both the location and
/// population endpoints.
/// </summary>
public interface ICityGeocodingService
{
    Task<CityGeocodingResult> GetGeocodingAsync(
        string cityName,
        CancellationToken cancellationToken = default);
}
