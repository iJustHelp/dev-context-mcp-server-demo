namespace STI.City.Core.Services;

/// <summary>
/// Resolves a supported city and supplies one cached geocoding record that
/// serves both the location and population endpoints.
/// </summary>
public interface ICityGeocodingService
{
    Task<CityGeocodingResult> GetCityGeocodingAsync(
        string cityName,
        CancellationToken cancellationToken = default);
}
