using System.Net;
using System.Net.Http.Json;
using STI.City.Core.Models;
using Xunit;

namespace STI.City.Tests.Api;

public class CityApiEndpointsTests
{
    private static CityGeocoding Result(string displayName) => new()
    {
        NormalizedName = string.Empty, // assigned by the service
        DisplayName = displayName,
        Country = "Testland",
        Latitude = 12.34,
        Longitude = 56.78,
        Population = 1_000_000,
        RetrievedAtUtc = default,
    };

    private static async Task<string> FirstCityAsync(HttpClient client)
    {
        var cities = await client.GetFromJsonAsync<string[]>("/city");
        Assert.NotNull(cities);
        Assert.NotEmpty(cities!);
        return cities![0];
    }

    [Fact] // AS-1, FR-15
    public async Task GetCity_returns_json_array()
    {
        using var factory = new CityApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/city");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var cities = await response.Content.ReadFromJsonAsync<string[]>();
        Assert.NotNull(cities);
        Assert.NotEmpty(cities!);
    }

    [Fact] // AS-2: relies on the QA-only IUsaCityService being registered
    public async Task GetCityUsa_returns_json_array()
    {
        using var factory = new CityApiFactory();
        using var client = factory.CreateClient();

        var cities = await client.GetFromJsonAsync<string[]>("/city/usa");

        Assert.NotNull(cities);
        Assert.NotEmpty(cities!);
    }

    [Fact] // AS-3 / AS-5 / AS-6: success, then cache hit (provider called once)
    public async Task GetLocation_returns_coordinates_and_caches()
    {
        using var factory = new CityApiFactory();
        using var client = factory.CreateClient();
        var city = await FirstCityAsync(client);
        factory.Provider.OnFind = _ => Result(city);

        var first = await client.GetAsync($"/city/{Uri.EscapeDataString(city)}/location");
        var second = await client.GetAsync($"/city/{Uri.EscapeDataString(city)}/location");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var body = await first.Content.ReadFromJsonAsync<LocationDto>();
        Assert.Equal(city, body!.CityName);
        Assert.Equal("Testland", body.Country);
        Assert.Equal(12.34, body.Latitude);
        Assert.Equal(56.78, body.Longitude);

        Assert.Equal(1, factory.Provider.CallCount); // FR-8/FR-10: second request from cache
    }

    [Fact] // AS-4
    public async Task GetPopulation_returns_population()
    {
        using var factory = new CityApiFactory();
        using var client = factory.CreateClient();
        var city = await FirstCityAsync(client);
        factory.Provider.OnFind = _ => Result(city);

        var body = await client.GetFromJsonAsync<PopulationDto>($"/city/{Uri.EscapeDataString(city)}/population");

        Assert.Equal(city, body!.CityName);
        Assert.Equal(1_000_000, body.Population);
    }

    [Fact] // OQ-3: matched result with null population → 404
    public async Task GetPopulation_with_null_population_returns_404()
    {
        using var factory = new CityApiFactory();
        using var client = factory.CreateClient();
        var city = await FirstCityAsync(client);
        factory.Provider.OnFind = _ => Result(city) with { Population = null };

        var response = await client.GetAsync($"/city/{Uri.EscapeDataString(city)}/population");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact] // AS-7 / FR-6: unknown city → 404, no upstream call
    public async Task GetLocation_for_unknown_city_returns_404_without_calling_provider()
    {
        using var factory = new CityApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/city/Definitely%20Not%20A%20City%20123/location");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, factory.Provider.CallCount);
    }

    [Fact] // AS-8 / FR-13: empty upstream result → 404
    public async Task GetLocation_with_no_result_returns_404()
    {
        using var factory = new CityApiFactory();
        using var client = factory.CreateClient();
        var city = await FirstCityAsync(client);
        factory.Provider.OnFind = _ => null;

        var response = await client.GetAsync($"/city/{Uri.EscapeDataString(city)}/location");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact] // AS-9 / FR-14: upstream failure with no cache → 502
    public async Task GetLocation_on_upstream_failure_returns_502()
    {
        using var factory = new CityApiFactory();
        using var client = factory.CreateClient();
        var city = await FirstCityAsync(client);
        factory.Provider.ThrowUnavailable = true;

        var response = await client.GetAsync($"/city/{Uri.EscapeDataString(city)}/location");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact] // AS-10: case-insensitive / URL-encoded names match the same city
    public async Task GetLocation_matches_case_insensitively()
    {
        using var factory = new CityApiFactory();
        using var client = factory.CreateClient();
        var city = await FirstCityAsync(client);
        factory.Provider.OnFind = _ => Result(city);

        var response = await client.GetAsync($"/city/{Uri.EscapeDataString(city.ToUpperInvariant())}/location");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record LocationDto(string CityName, string? Country, double Latitude, double Longitude);

    private sealed record PopulationDto(string CityName, string? Country, int Population);
}
