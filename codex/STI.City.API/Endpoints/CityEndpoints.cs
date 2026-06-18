using Demo.Cities;
using STI.City.API.Contracts;
using STI.City.Core.Services;

namespace STI.City.API.Endpoints;

public static class CityEndpoints
{
    public static IEndpointRouteBuilder MapCityEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/city", GetAllCities)
            .WithName("GetCities")
            .Produces<IReadOnlyList<string>>();

        var group = routes.MapGroup("/city");

        group.MapGet("/usa", GetUsaCities)
            .WithName("GetUsaCities")
            .Produces<IReadOnlyList<string>>();

        group.MapGet("/{cityName}/location", GetLocationAsync)
            .WithName("GetCityLocation")
            .Produces<CityLocationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        group.MapGet("/{cityName}/population", GetPopulationAsync)
            .WithName("GetCityPopulation")
            .Produces<CityPopulationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return routes;
    }

    private static IResult GetAllCities(ICityService cityService) =>
        Results.Ok(cityService.GetCityNames().Select(name => name.ToCityName()).ToList());

    private static IResult GetUsaCities(IUsaCityService usaCityService) =>
        Results.Ok(usaCityService.GetCityNames().Select(name => name.ToCityName()).ToList());

    private static async Task<IResult> GetLocationAsync(
        string cityName,
        ICityGeocodingService geocodingService,
        HttpContext httpContext)
    {
        var result = await geocodingService
            .GetGeocodingAsync(cityName, httpContext.RequestAborted)
            .ConfigureAwait(false);

        return result.Status switch
        {
            CityGeocodingStatus.Success => Results.Ok(new CityLocationResponse(
                result.Record!.DisplayName,
                result.Record.Country,
                result.Record.Latitude,
                result.Record.Longitude)),
            CityGeocodingStatus.CityNotFound => CityNotFound(),
            CityGeocodingStatus.GeocodingNotFound => GeocodingNotFound(),
            CityGeocodingStatus.ServiceUnavailable => ServiceUnavailable(),
            _ => InternalError(),
        };
    }

    private static async Task<IResult> GetPopulationAsync(
        string cityName,
        ICityGeocodingService geocodingService,
        HttpContext httpContext)
    {
        var result = await geocodingService
            .GetGeocodingAsync(cityName, httpContext.RequestAborted)
            .ConfigureAwait(false);

        return result.Status switch
        {
            CityGeocodingStatus.Success => result.Record!.Population is { } population
                ? Results.Ok(new CityPopulationResponse(
                    result.Record.DisplayName,
                    result.Record.Country,
                    population))
                : PopulationNotFound(),
            CityGeocodingStatus.CityNotFound => CityNotFound(),
            CityGeocodingStatus.GeocodingNotFound => GeocodingNotFound(),
            CityGeocodingStatus.ServiceUnavailable => ServiceUnavailable(),
            _ => InternalError(),
        };
    }

    private static IResult CityNotFound() =>
        Results.Problem(title: "City not found", statusCode: StatusCodes.Status404NotFound);

    private static IResult GeocodingNotFound() =>
        Results.Problem(title: "Geocoding result not found", statusCode: StatusCodes.Status404NotFound);

    private static IResult PopulationNotFound() =>
        Results.Problem(title: "Population not found", statusCode: StatusCodes.Status404NotFound);

    private static IResult ServiceUnavailable() =>
        Results.Problem(title: "Geocoding service unavailable", statusCode: StatusCodes.Status502BadGateway);

    private static IResult InternalError() =>
        Results.Problem(title: "Internal server error", statusCode: StatusCodes.Status500InternalServerError);
}
