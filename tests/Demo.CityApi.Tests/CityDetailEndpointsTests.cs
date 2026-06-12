using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Demo.CityApi.Tests;

public sealed class CityDetailEndpointsTests
{
    [Fact]
    public async Task NewYorkLocationUsesDecodedCanonicalName()
    {
        var handler = new OpenMeteoStubHttpMessageHandler(
            OpenMeteoStubHttpMessageHandler.Json(
                GeocodingResponse(
                    Location("New York", 40.71427, -74.00597, 8_804_190))));
        using var factory = new GeocodingApiFactory(handler);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/city/New%20York/location");
        var payload = await response.Content.ReadFromJsonAsync<LocationPayload>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new LocationPayload("New York", 40.71427, -74.00597),
            payload);
        Assert.Equal(["new york"], handler.RequestedCityNames);
    }

    [Fact]
    public async Task MixedCaseAndWhitespaceUseCanonicalPackageName()
    {
        var handler = new OpenMeteoStubHttpMessageHandler(
            OpenMeteoStubHttpMessageHandler.Json(
                GeocodingResponse(
                    Location("Chicago", 41.85003, -87.65005, 2_746_388))));
        using var factory = new GeocodingApiFactory(handler);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/city/%20%20cHiCaGo%20%20/location");
        var payload = await response.Content.ReadFromJsonAsync<LocationPayload>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Chicago", payload?.City);
        Assert.Equal(["chicago"], handler.RequestedCityNames);
    }

    [Fact]
    public async Task UnsupportedAndMisspelledCitiesDoNotCallOpenMeteo()
    {
        var handler = new OpenMeteoStubHttpMessageHandler();
        using var factory = new GeocodingApiFactory(handler);
        using var client = factory.CreateClient();

        using var misspelled = await client.GetAsync(
            "/city/New%20Your/location");
        using var unsupported = await client.GetAsync(
            "/city/Atlantis/location");

        await AssertProblemAsync(misspelled, HttpStatusCode.NotFound);
        await AssertProblemAsync(unsupported, HttpStatusCode.NotFound);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task EmptyAndNonExactResultsReturnNotFound()
    {
        var handler = new OpenMeteoStubHttpMessageHandler(
            OpenMeteoStubHttpMessageHandler.Json(
                GeocodingResponse()),
            OpenMeteoStubHttpMessageHandler.Json(
                GeocodingResponse(
                    Location(
                        "Chicago Heights",
                        41.50615,
                        -87.6356,
                        27_480))));
        using var factory = new GeocodingApiFactory(handler);
        using var client = factory.CreateClient();

        using var empty = await client.GetAsync("/city/chicago/location");
        using var nonExact = await client.GetAsync("/city/chicago/location");

        await AssertProblemAsync(empty, HttpStatusCode.NotFound);
        await AssertProblemAsync(nonExact, HttpStatusCode.NotFound);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task DuplicateExactResultsUseFirstResult()
    {
        var handler = new OpenMeteoStubHttpMessageHandler(
            OpenMeteoStubHttpMessageHandler.Json(
                GeocodingResponse(
                    Location("Chicago", 1, 2, 100),
                    Location("CHICAGO", 3, 4, 200))));
        using var factory = new GeocodingApiFactory(handler);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city/chicago/location");
        var payload = await response.Content.ReadFromJsonAsync<LocationPayload>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new LocationPayload("Chicago", 1, 2), payload);
    }

    [Fact]
    public async Task LocationAndPopulationShareOneCachedRecord()
    {
        var handler = new OpenMeteoStubHttpMessageHandler(
            OpenMeteoStubHttpMessageHandler.Json(
                GeocodingResponse(
                    Location("Chicago", 41.85003, -87.65005, 2_746_388))),
            OpenMeteoStubHttpMessageHandler.Failure(
                new HttpRequestException("Open-Meteo unavailable.")));
        using var factory = new GeocodingApiFactory(handler);
        using var client = factory.CreateClient();

        using var locationResponse = await client.GetAsync(
            "/city/chicago/location");
        using var populationResponse = await client.GetAsync(
            "/city/CHICAGO/population");

        Assert.Equal(HttpStatusCode.OK, locationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, populationResponse.StatusCode);
        Assert.Equal(
            ["city", "latitude", "longitude"],
            await GetJsonPropertyNamesAsync(locationResponse));
        Assert.Equal(
            ["city", "population"],
            await GetJsonPropertyNamesAsync(populationResponse));

        var population = await populationResponse.Content
            .ReadFromJsonAsync<PopulationPayload>();
        Assert.Equal(
            new PopulationPayload("Chicago", 2_746_388),
            population);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task NullPopulationServesLocationButNotPopulation()
    {
        var handler = new OpenMeteoStubHttpMessageHandler(
            OpenMeteoStubHttpMessageHandler.Json(
                GeocodingResponse(
                    Location("Chicago", 41.85003, -87.65005, null))));
        using var factory = new GeocodingApiFactory(handler);
        using var client = factory.CreateClient();

        using var location = await client.GetAsync("/city/chicago/location");
        using var population = await client.GetAsync(
            "/city/chicago/population");

        Assert.Equal(HttpStatusCode.OK, location.StatusCode);
        await AssertProblemAsync(population, HttpStatusCode.NotFound);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task OpenMeteoFailureOnCacheMissReturnsBadGateway()
    {
        var handler = new OpenMeteoStubHttpMessageHandler(
            OpenMeteoStubHttpMessageHandler.Json(
                """{"reason":"temporary failure"}""",
                HttpStatusCode.ServiceUnavailable));
        using var factory = new GeocodingApiFactory(handler);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city/chicago/location");

        await AssertProblemAsync(response, HttpStatusCode.BadGateway);
        Assert.Equal(1, handler.RequestCount);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal((int)expectedStatusCode, problem?.Status);
    }

    private static async Task<string[]> GetJsonPropertyNamesAsync(
        HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);

        return document.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .Order()
            .ToArray();
    }

    private static string GeocodingResponse(params string[] locations) =>
        $$"""
        {
          "results": [{{string.Join(",", locations)}}],
          "generationtime_ms": 0.1
        }
        """;

    private static string Location(
        string name,
        double latitude,
        double longitude,
        int? population) =>
        $$"""
        {
          "id": 1,
          "name": "{{name}}",
          "latitude": {{latitude}},
          "longitude": {{longitude}},
          "country_code": "US",
          "timezone": "America/Chicago",
          "population": {{population?.ToString() ?? "null"}},
          "country": "United States"
        }
        """;

    private sealed record LocationPayload(
        string City,
        double Latitude,
        double Longitude);

    private sealed record PopulationPayload(
        string City,
        long Population);
}
