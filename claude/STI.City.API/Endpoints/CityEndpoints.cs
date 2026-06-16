using System.Diagnostics;
using Demo.Cities;
using STI.City.API.Contracts;
using STI.City.Core.Services;

namespace STI.City.API.Endpoints;

/// <summary>
/// Maps the four <c>/city</c> endpoints and translates service outcomes to the
/// HTTP contracts in <c>design/spec.md</c>. Handlers stay thin: no SQL, no
/// package-response traversal, and no cache-aside branching.
/// </summary>
public static class CityEndpoints
{
    public static RouteGroupBuilder MapCityEndpoints(this IEndpointRouteBuilder routes)
    {
        var city = routes.MapGroup("/city");

        city.MapGet("/", (ICityService cityService) =>
                TypedResults.Ok(cityService.GetCityNames()))
            .WithName("GetCities")
            .Produces<IReadOnlyList<string>>();

        // The literal "/usa" segment is matched ahead of the "{cityName}" routes.
        city.MapGet("/usa", (IUsaCityService usaCityService) =>
                TypedResults.Ok(usaCityService.GetCityNames()))
            .WithName("GetUsaCities")
            .Produces<IReadOnlyList<string>>();

        city.MapGet("/{cityName}/location", GetLocationAsync)
            .WithName("GetCityLocation")
            .Produces<CityLocationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        city.MapGet("/{cityName}/population", GetPopulationAsync)
            .WithName("GetCityPopulation")
            .Produces<CityPopulationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return city;
    }

    private static async Task<IResult> GetLocationAsync(
        string cityName,
        ICityGeocodingService geocodingService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await geocodingService.GetCityGeocodingAsync(cityName, cancellationToken);
        return result.Status switch
        {
            CityGeocodingStatus.Success => TypedResults.Ok(new CityLocationResponse(
                result.Record!.DisplayName,
                result.Record.Country,
                result.Record.Latitude,
                result.Record.Longitude)),
            CityGeocodingStatus.CityNotFound =>
                CityProblem(httpContext, StatusCodes.Status404NotFound, "City not found"),
            CityGeocodingStatus.GeocodingNotFound =>
                CityProblem(httpContext, StatusCodes.Status404NotFound, "Geocoding result not found"),
            CityGeocodingStatus.ServiceUnavailable =>
                CityProblem(httpContext, StatusCodes.Status502BadGateway, "Geocoding service unavailable"),
            _ => CityProblem(httpContext, StatusCodes.Status500InternalServerError, "Internal server error"),
        };
    }

    private static async Task<IResult> GetPopulationAsync(
        string cityName,
        ICityGeocodingService geocodingService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await geocodingService.GetCityGeocodingAsync(cityName, cancellationToken);
        return result.Status switch
        {
            // The same shared record powers location; only population may be missing.
            CityGeocodingStatus.Success => result.Record!.Population is { } population
                ? TypedResults.Ok(new CityPopulationResponse(
                    result.Record.DisplayName, result.Record.Country, population))
                : CityProblem(httpContext, StatusCodes.Status404NotFound, "Population not found"),
            CityGeocodingStatus.CityNotFound =>
                CityProblem(httpContext, StatusCodes.Status404NotFound, "City not found"),
            CityGeocodingStatus.GeocodingNotFound =>
                CityProblem(httpContext, StatusCodes.Status404NotFound, "Geocoding result not found"),
            CityGeocodingStatus.ServiceUnavailable =>
                CityProblem(httpContext, StatusCodes.Status502BadGateway, "Geocoding service unavailable"),
            _ => CityProblem(httpContext, StatusCodes.Status500InternalServerError, "Internal server error"),
        };
    }

    /// <summary>
    /// Builds a Problem Details result that carries the request trace identifier
    /// and never exposes internal exception details.
    /// </summary>
    internal static IResult CityProblem(HttpContext httpContext, int statusCode, string title) =>
        TypedResults.Problem(
            title: title,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier,
            });
}
