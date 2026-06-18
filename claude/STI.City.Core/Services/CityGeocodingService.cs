using Demo.Cities;
using Microsoft.Extensions.Logging;
using OpenMeteo.Api.Client;
using STI.City.Core.Models;
using STI.City.Core.Repositories;

namespace STI.City.Core.Services;

/// <summary>
/// Validates the requested city, performs the shared cache-aside lookup, and
/// deterministically selects the upstream Open-Meteo result.
/// </summary>
public sealed class CityGeocodingService : ICityGeocodingService
{
    private const int GeocodingResultCount = 10;
    private const string GeocodingLanguage = "en";

    private readonly ICityService _cityService;
    private readonly IUsaCityService _usaCityService;
    private readonly IOpenMeteoClient _openMeteoClient;
    private readonly IGeocodingCacheRepository _cacheRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CityGeocodingService> _logger;

    public CityGeocodingService(
        ICityService cityService,
        IUsaCityService usaCityService,
        IOpenMeteoClient openMeteoClient,
        IGeocodingCacheRepository cacheRepository,
        TimeProvider timeProvider,
        ILogger<CityGeocodingService> logger)
    {
        _cityService = cityService;
        _usaCityService = usaCityService;
        _openMeteoClient = openMeteoClient;
        _cacheRepository = cacheRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<CityGeocodingResult> GetGeocodingAsync(
        string cityName,
        CancellationToken cancellationToken = default)
    {
        var trimmed = (cityName ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return CityGeocodingResult.CityNotFound;
        }

        // Search the merged set of general and U.S. city names so a city present
        // in either list resolves.
        var packageName = _cityService.GetCityNames()
            .Concat(_usaCityService.GetCityNames())
            .FirstOrDefault(name => string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (packageName is null)
        {
            return CityGeocodingResult.CityNotFound;
        }

        // Canonical display spelling comes from the package's ToCityName helper
        // (title case, e.g. "new york" -> "New York").
        var canonicalName = packageName.ToCityName();
        var normalizedKey = canonicalName.Trim().ToUpperInvariant();

        var cached = await _cacheRepository.GetAsync(normalizedKey, cancellationToken)
            .ConfigureAwait(false);
        if (cached is not null)
        {
            return CityGeocodingResult.Success(cached);
        }

        GeocodingResponse response;
        try
        {
            response = await _openMeteoClient
                .SearchLocationsAsync(canonicalName, GeocodingResultCount, GeocodingLanguage, Format.Json, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex,
                "Open-Meteo returned an unsuccessful response for {City} during {Operation}.",
                canonicalName, nameof(GetGeocodingAsync));
            return CityGeocodingResult.ServiceUnavailable;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnect: propagate cancellation, do not map to 502.
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex,
                "Open-Meteo timed out for {City} during {Operation}.",
                canonicalName, nameof(GetGeocodingAsync));
            return CityGeocodingResult.ServiceUnavailable;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "Open-Meteo transport failure for {City} during {Operation}.",
                canonicalName, nameof(GetGeocodingAsync));
            return CityGeocodingResult.ServiceUnavailable;
        }

        var match = response.Results?
            .FirstOrDefault(result =>
                string.Equals(result.Name, canonicalName, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return CityGeocodingResult.GeocodingNotFound;
        }

        var record = new GeocodingCacheRecord(
            NormalizedCityName: normalizedKey,
            DisplayName: canonicalName,
            Country: match.Country,
            Latitude: match.Latitude,
            Longitude: match.Longitude,
            Population: match.Population,
            RetrievedAtUtc: _timeProvider.GetUtcNow());

        // Persistence failures propagate so the pipeline can return 500.
        await _cacheRepository.UpsertAsync(record, cancellationToken).ConfigureAwait(false);

        return CityGeocodingResult.Success(record);
    }
}
