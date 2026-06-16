using Demo.Cities;
using Microsoft.Extensions.Logging;
using OpenMeteo.Api.Client;
using STI.City.Core.Models;
using STI.City.Core.Repositories;

namespace STI.City.Core.Services;

/// <summary>
/// Cache-aside geocoding workflow: resolve the city against the supported list,
/// return a cached record when present, otherwise query Open-Meteo, select the
/// exact-name match, persist it, and return it.
/// </summary>
public sealed class CityGeocodingService : ICityGeocodingService
{
    private readonly ICityService _cityService;
    private readonly IOpenMeteoClient _openMeteoClient;
    private readonly IGeocodingCacheRepository _cacheRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CityGeocodingService> _logger;

    public CityGeocodingService(
        ICityService cityService,
        IOpenMeteoClient openMeteoClient,
        IGeocodingCacheRepository cacheRepository,
        TimeProvider timeProvider,
        ILogger<CityGeocodingService> logger)
    {
        _cityService = cityService;
        _openMeteoClient = openMeteoClient;
        _cacheRepository = cacheRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<CityGeocodingResult> GetCityGeocodingAsync(
        string cityName,
        CancellationToken cancellationToken = default)
    {
        var trimmed = cityName?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return CityGeocodingResult.CityNotFound;
        }

        // Resolve to the canonical, package-provided spelling (case-insensitive).
        var canonicalCityName = _cityService.GetCityNames()
            .FirstOrDefault(name => string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (canonicalCityName is null)
        {
            return CityGeocodingResult.CityNotFound;
        }

        var normalizedCityName = canonicalCityName.Trim().ToUpperInvariant();

        // Cache hit takes precedence over the upstream service.
        var cached = await _cacheRepository.GetAsync(normalizedCityName, cancellationToken);
        if (cached is not null)
        {
            return CityGeocodingResult.Success(cached);
        }

        GeocodingResponse response;
        try
        {
            response = await _openMeteoClient.SearchLocationsAsync(
                canonicalCityName,
                count: null,
                language: "en",
                format: null,
                cancellationToken);
        }
        catch (ApiException exception)
        {
            _logger.LogWarning(
                exception,
                "Open-Meteo returned an error for {City} during geocoding.",
                canonicalCityName);
            return CityGeocodingResult.ServiceUnavailable;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Open-Meteo transport failure for {City} during geocoding.",
                canonicalCityName);
            return CityGeocodingResult.ServiceUnavailable;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A timeout (not client disconnection) is an upstream failure.
            _logger.LogWarning("Open-Meteo timed out for {City} during geocoding.", canonicalCityName);
            return CityGeocodingResult.ServiceUnavailable;
        }

        // Deterministic selection: first upstream result with an exact name match.
        var match = response.Results?
            .FirstOrDefault(result =>
                string.Equals(result.Name, canonicalCityName, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return CityGeocodingResult.GeocodingNotFound;
        }

        var record = new GeocodingCacheRecord
        {
            NormalizedCityName = normalizedCityName,
            DisplayName = canonicalCityName,
            Country = match.Country,
            Latitude = match.Latitude,
            Longitude = match.Longitude,
            Population = match.Population,
            RetrievedAtUtc = _timeProvider.GetUtcNow(),
        };

        await _cacheRepository.UpsertAsync(record, cancellationToken);
        return CityGeocodingResult.Success(record);
    }
}
