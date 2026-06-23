using System.Linq;
using OpenMeteo.Api.Client;
using STI.City.Core.Abstractions;
using STI.City.Core.Exceptions;
using STI.City.Core.Models;

namespace STI.City.Data;

/// <summary>
/// <see cref="IGeocodingProvider"/> over <c>OpenMeteo.Api.Client.IOpenMeteoClient</c>.
///
/// One geocoding result supplies both coordinates and population (FR-3). An empty
/// <see cref="GeocodingResponse.Results"/> returns <c>null</c> → 404 (FR-13); a failed
/// call (Open-Meteo <see cref="ApiException"/> or a transport error) is surfaced as
/// <see cref="GeocodingUnavailableException"/> → 502 (FR-14).
/// </summary>
public sealed class OpenMeteoGeocodingProvider : IGeocodingProvider
{
    // Request a single best match; the BRD selects "the" result, so we take the first.
    private const int ResultCount = 1;
    private const string Language = "en";

    private readonly IOpenMeteoClient _client;

    public OpenMeteoGeocodingProvider(IOpenMeteoClient client)
    {
        _client = client;
    }

    public async Task<CityGeocoding?> FindAsync(string cityName, CancellationToken cancellationToken = default)
    {
        GeocodingResponse response;
        try
        {
            response = await _client.SearchLocationsAsync(
                cityName,
                count: ResultCount,
                language: Language,
                format: null,
                cancellationToken);
        }
        catch (ApiException ex)
        {
            throw new GeocodingUnavailableException(
                $"Open-Meteo geocoding failed for '{cityName}'.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new GeocodingUnavailableException(
                $"Open-Meteo geocoding could not be reached for '{cityName}'.", ex);
        }

        var result = response.Results?.FirstOrDefault();
        if (result is null)
        {
            return null;
        }

        return new CityGeocoding
        {
            // NormalizedName and RetrievedAtUtc are assigned by the orchestrator.
            NormalizedName = cityName,
            DisplayName = string.IsNullOrWhiteSpace(result.Name) ? cityName : result.Name,
            Country = result.Country,
            Latitude = result.Latitude,
            Longitude = result.Longitude,
            Population = result.Population,
            RetrievedAtUtc = default,
        };
    }
}
