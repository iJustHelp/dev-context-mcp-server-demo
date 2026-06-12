using Demo.Cities;
using Demo.CityApi.Caching;
using OpenMeteo.Api.Client;

namespace Demo.CityApi.Geocoding;

public sealed class CacheAsideGeocodingService(
    ICityService cityService,
    IUsaCityService usaCityService,
    IGeocodingCacheRepository cacheRepository,
    IOpenMeteoClient openMeteoClient,
    TimeProvider timeProvider)
    : IGeocodingService
{
    public async Task<GeocodingLookupResult> GetAsync(
        string cityName,
        CancellationToken cancellationToken = default)
    {
        var canonicalCityName = ResolveCanonicalCityName(cityName);
        if (canonicalCityName is null)
        {
            return GeocodingLookupResult.NotFound();
        }

        var cached = await cacheRepository.GetByCityNameAsync(
            canonicalCityName,
            cancellationToken);
        if (cached is not null)
        {
            return GeocodingLookupResult.Success(cached);
        }

        GeocodingResponse response;
        try
        {
            response = await openMeteoClient.SearchLocationsAsync(
                canonicalCityName,
                count: 10,
                language: "en",
                format: Format.Json,
                cancellationToken);
        }
        catch (ApiException)
        {
            return GeocodingLookupResult.UpstreamUnavailable();
        }
        catch (HttpRequestException)
        {
            return GeocodingLookupResult.UpstreamUnavailable();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return GeocodingLookupResult.UpstreamUnavailable();
        }

        var exactMatch = response.Results?.FirstOrDefault(result =>
            string.Equals(
                result.Name,
                canonicalCityName,
                StringComparison.OrdinalIgnoreCase));
        if (exactMatch is null)
        {
            return GeocodingLookupResult.NotFound();
        }

        var entry = new GeocodingCacheEntry
        {
            NormalizedCityName = canonicalCityName,
            DisplayCityName = exactMatch.Name,
            Country = exactMatch.Country,
            Latitude = exactMatch.Latitude,
            Longitude = exactMatch.Longitude,
            Population = exactMatch.Population,
            RetrievedAtUtc = timeProvider.GetUtcNow(),
        };

        var persisted = await cacheRepository.InsertAsync(entry, cancellationToken);
        return GeocodingLookupResult.Success(persisted);
    }

    private string? ResolveCanonicalCityName(string cityName)
    {
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return null;
        }

        var requestedCityName = cityName.Trim();

        return cityService
            .GetCityNames()
            .Concat(usaCityService.GetCityNames())
            .FirstOrDefault(candidate =>
                string.Equals(
                    candidate,
                    requestedCityName,
                    StringComparison.OrdinalIgnoreCase));
    }
}
