using Demo.Cities;
using STI.City.API.Contracts;
using STI.City.Core.Geocoding;

namespace STI.City.API.Endpoints;

public static class CityEndpoints
{
    public static IEndpointRouteBuilder MapCityEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/city")
            .WithTags("City");

        group.MapGet(
                string.Empty,
                (ICityService cityService) =>
                    TypedResults.Ok(cityService.GetCityNames()))
            .WithName("GetCities")
            .Produces<IReadOnlyList<string>>(StatusCodes.Status200OK);

        group.MapGet(
                "/usa",
                (IUsaCityService cityService) =>
                    TypedResults.Ok(cityService.GetCityNames()))
            .WithName("GetUsaCities")
            .Produces<IReadOnlyList<string>>(StatusCodes.Status200OK);

        group.MapGet(
                "/{cityName}/location",
                GetLocationAsync)
            .WithName("GetCityLocation")
            .Produces<CityLocationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet(
                "/{cityName}/population",
                GetPopulationAsync)
            .WithName("GetCityPopulation")
            .Produces<CityPopulationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> GetLocationAsync(
        string cityName,
        ICityGeocodingService geocodingService,
        HttpContext context)
    {
        var result = await geocodingService.GetAsync(
            cityName,
            context.RequestAborted);

        if (result.Outcome != CityGeocodingOutcome.Success)
        {
            return ToProblem(result.Outcome, context);
        }

        var record = result.Record ??
            throw new InvalidOperationException(
                "A successful geocoding result must contain a record.");

        return TypedResults.Ok(
            new CityLocationResponse(
                record.DisplayName,
                record.Country,
                record.Latitude,
                record.Longitude));
    }

    private static async Task<IResult> GetPopulationAsync(
        string cityName,
        ICityGeocodingService geocodingService,
        HttpContext context)
    {
        var result = await geocodingService.GetAsync(
            cityName,
            context.RequestAborted);

        if (result.Outcome != CityGeocodingOutcome.Success)
        {
            return ToProblem(result.Outcome, context);
        }

        var record = result.Record ??
            throw new InvalidOperationException(
                "A successful geocoding result must contain a record.");

        if (record.Population is null)
        {
            return Problem(
                StatusCodes.Status404NotFound,
                "Population not found",
                context);
        }

        return TypedResults.Ok(
            new CityPopulationResponse(
                record.DisplayName,
                record.Country,
                record.Population.Value));
    }

    private static IResult ToProblem(
        CityGeocodingOutcome outcome,
        HttpContext context) =>
        outcome switch
        {
            CityGeocodingOutcome.CityNotFound => Problem(
                StatusCodes.Status404NotFound,
                "City not found",
                context),
            CityGeocodingOutcome.GeocodingNotFound => Problem(
                StatusCodes.Status404NotFound,
                "Geocoding result not found",
                context),
            CityGeocodingOutcome.ServiceUnavailable => Problem(
                StatusCodes.Status502BadGateway,
                "Geocoding service unavailable",
                context),
            _ => throw new InvalidOperationException(
                $"Unsupported geocoding outcome: {outcome}.")
        };

    private static IResult Problem(
        int statusCode,
        string title,
        HttpContext context) =>
        TypedResults.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = context.TraceIdentifier
            });
}
