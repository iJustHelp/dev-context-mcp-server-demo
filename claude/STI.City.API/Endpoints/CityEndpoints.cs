using STI.City.API.Dtos;
using STI.City.Core.Models;
using STI.City.Core.Services;

namespace STI.City.API.Endpoints;

/// <summary>Maps the four City API routes and translates lookup outcomes to HTTP results.</summary>
public static class CityEndpoints
{
    public static IEndpointRouteBuilder MapCityEndpoints(this IEndpointRouteBuilder app)
    {
        // FR-1 / FR-7: all city names, alphabetical order preserved.
        app.MapGet("/city", (ICityGeocodingService service) =>
            Results.Ok(service.GetCityNames()))
            .WithName("GetCities");

        // FR-2 / FR-7: U.S. city names (QA-only IUsaCityService).
        app.MapGet("/city/usa", (ICityGeocodingService service) =>
            Results.Ok(service.GetUsaCityNames()))
            .WithName("GetUsaCities");

        // FR-3..FR-14: latitude/longitude for a city (cache-aside).
        app.MapGet("/city/{cityName}/location", async (
            string cityName,
            ICityGeocodingService service,
            CancellationToken cancellationToken) =>
        {
            var lookup = await service.GetGeocodingAsync(cityName, cancellationToken);
            return lookup.Status switch
            {
                GeocodingStatus.Found => Results.Ok(new CityLocationResponse(
                    lookup.Record!.DisplayName,
                    lookup.Record.Country,
                    lookup.Record.Latitude,
                    lookup.Record.Longitude)),
                _ => DetailProblem(lookup.Status, cityName),
            };
        }).WithName("GetCityLocation");

        // FR-3..FR-14: population for a city (shares the cached record with /location).
        app.MapGet("/city/{cityName}/population", async (
            string cityName,
            ICityGeocodingService service,
            CancellationToken cancellationToken) =>
        {
            var lookup = await service.GetGeocodingAsync(cityName, cancellationToken);

            if (lookup.Status != GeocodingStatus.Found)
            {
                return DetailProblem(lookup.Status, cityName);
            }

            // OQ-3: a matched result with no population is treated as missing data → 404.
            if (lookup.Record!.Population is not int population)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Population not available",
                    detail: $"No population is available for '{cityName}'.");
            }

            return Results.Ok(new CityPopulationResponse(
                lookup.Record.DisplayName,
                lookup.Record.Country,
                population));
        }).WithName("GetCityPopulation");

        return app;
    }

    private static IResult DetailProblem(GeocodingStatus status, string cityName) => status switch
    {
        GeocodingStatus.CityNotFound => Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "City not found",
            detail: $"'{cityName}' is not a known city."),

        GeocodingStatus.NoGeocodingResult => Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "No geocoding result",
            detail: $"No geocoding result was found for '{cityName}'."),

        GeocodingStatus.UpstreamUnavailable => Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "Geocoding service unavailable",
            detail: $"The geocoding service could not be reached for '{cityName}'."),

        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
    };
}
