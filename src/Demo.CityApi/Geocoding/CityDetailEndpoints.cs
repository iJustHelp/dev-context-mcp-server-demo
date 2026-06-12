using Microsoft.AspNetCore.Http.HttpResults;

namespace Demo.CityApi.Geocoding;

public static class CityDetailEndpoints
{
    public static IEndpointRouteBuilder MapCityDetailEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/city/{cityName}/location",
            GetLocationAsync);
        endpoints.MapGet(
            "/city/{cityName}/population",
            GetPopulationAsync);

        return endpoints;
    }

    private static async Task<Results<Ok<LocationResponse>, ProblemHttpResult>>
        GetLocationAsync(
            string cityName,
            IGeocodingService geocodingService,
            CancellationToken cancellationToken)
    {
        var result = await geocodingService.GetAsync(
            cityName,
            cancellationToken);

        return result.Status switch
        {
            GeocodingLookupStatus.Success => TypedResults.Ok(
                new LocationResponse(
                    result.Entry!.DisplayCityName,
                    result.Entry.Latitude,
                    result.Entry.Longitude)),
            GeocodingLookupStatus.UpstreamUnavailable => UpstreamUnavailable(),
            _ => NotFound(),
        };
    }

    private static async Task<Results<Ok<PopulationResponse>, ProblemHttpResult>>
        GetPopulationAsync(
            string cityName,
            IGeocodingService geocodingService,
            CancellationToken cancellationToken)
    {
        var result = await geocodingService.GetAsync(
            cityName,
            cancellationToken);

        if (result.Status == GeocodingLookupStatus.UpstreamUnavailable)
        {
            return UpstreamUnavailable();
        }

        if (result.Status != GeocodingLookupStatus.Success
            || result.Entry!.Population is null)
        {
            return NotFound();
        }

        return TypedResults.Ok(
            new PopulationResponse(
                result.Entry.DisplayCityName,
                result.Entry.Population.Value));
    }

    private static ProblemHttpResult NotFound() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "City data not found.");

    private static ProblemHttpResult UpstreamUnavailable() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "Open-Meteo is unavailable.");

    public sealed record LocationResponse(
        string City,
        double Latitude,
        double Longitude);

    public sealed record PopulationResponse(
        string City,
        long Population);
}
