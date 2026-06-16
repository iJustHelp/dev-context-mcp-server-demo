using Demo.Cities;
using Microsoft.AspNetCore.Mvc;
using OpenMeteo.Api.Client;
using STI.City.Core.Geocoding;

namespace STI.City.API.Endpoints
{
    public static class CityEndpoints
    {
        private static Dictionary<string, object?> BuildExtensions(HttpContext httpContext) => new() { ["traceId"] = httpContext.TraceIdentifier };

        public static void MapCityEndpoints(this WebApplication app)
        {
            app.MapGet("/city", (ICityService cityService) => Results.Ok(cityService.GetCityNames()))
                .WithName("GetCities")
                .Produces<IEnumerable<string>>(StatusCodes.Status200OK);

            app.MapGet("/city/usa", (IUsaCityService usaCityService) => Results.Ok(usaCityService.GetCityNames()))
                .WithName("GetUsaCities")
                .Produces<IEnumerable<string>>(StatusCodes.Status200OK);

            app.MapGet("/city/{cityName}/location", async (
                string cityName,
                ICityGeocodingService geocodingService,
                HttpContext httpContext) =>
            {
                var outcome = await geocodingService.GetAsync(cityName, httpContext.RequestAborted).ConfigureAwait(false);
                return outcome.Status switch
                {
                    CityGeocodingStatus.Success => Results.Ok(new CityLocationResponse
                    {
                        CityName = outcome.Record!.DisplayName,
                        Country = outcome.Record.Country,
                        Latitude = outcome.Record.Latitude,
                        Longitude = outcome.Record.Longitude
                    }),
                    CityGeocodingStatus.CityNotFound => Results.Problem("City not found", statusCode: StatusCodes.Status404NotFound, extensions: BuildExtensions(httpContext)),
                    CityGeocodingStatus.GeocodingNotFound => Results.Problem("Geocoding result not found", statusCode: StatusCodes.Status404NotFound, extensions: BuildExtensions(httpContext)),
                    CityGeocodingStatus.ServiceUnavailable => Results.Problem("Open-Meteo service unavailable", statusCode: StatusCodes.Status502BadGateway, extensions: BuildExtensions(httpContext)),
                    _ => Results.Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError, extensions: BuildExtensions(httpContext))
                };
            })
            .WithName("GetCityLocation")
            .Produces<CityLocationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status502BadGateway);

            app.MapGet("/city/{cityName}/population", async (
                string cityName,
                ICityGeocodingService geocodingService,
                HttpContext httpContext) =>
            {
                var outcome = await geocodingService.GetAsync(cityName, httpContext.RequestAborted).ConfigureAwait(false);
                return outcome.Status switch
                {
                    CityGeocodingStatus.Success when outcome.Record?.Population is not null => Results.Ok(new CityPopulationResponse
                    {
                        CityName = outcome.Record.DisplayName,
                        Country = outcome.Record.Country,
                        Population = outcome.Record.Population.Value
                    }),
                    CityGeocodingStatus.Success => Results.Problem("Population not found", statusCode: StatusCodes.Status404NotFound, extensions: BuildExtensions(httpContext)),
                    CityGeocodingStatus.CityNotFound => Results.Problem("City not found", statusCode: StatusCodes.Status404NotFound, extensions: BuildExtensions(httpContext)),
                    CityGeocodingStatus.GeocodingNotFound => Results.Problem("Geocoding result not found", statusCode: StatusCodes.Status404NotFound, extensions: BuildExtensions(httpContext)),
                    CityGeocodingStatus.ServiceUnavailable => Results.Problem("Open-Meteo service unavailable", statusCode: StatusCodes.Status502BadGateway, extensions: BuildExtensions(httpContext)),
                    _ => Results.Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError, extensions: BuildExtensions(httpContext))
                };
            })
            .WithName("GetCityPopulation")
            .Produces<CityPopulationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status502BadGateway);
        }
    }
}
