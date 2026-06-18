namespace STI.City.Core.Services;

public interface ICityGeocodingService
{
    Task<CityGeocodingResult> GetGeocodingAsync(
        string cityName,
        CancellationToken cancellationToken = default);
}
