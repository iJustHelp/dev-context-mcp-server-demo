using System.Net;
using System.Net.Http.Json;

namespace Demo.CityApi.Tests;

public sealed class CityEndpointsTests(CityApiFactory factory)
    : IClassFixture<CityApiFactory>
{
    [Fact]
    public async Task GetCityReturnsExactOrderedCityNames()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city");
        var cityNames = await response.Content.ReadFromJsonAsync<string[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(cityNames);
        Assert.Equal(
            ["Berlin", "London", "Paris", "Tokyo", "Toronto"],
            cityNames);
    }

    [Fact]
    public async Task GetUsaCityReturnsExactOrderedCityNames()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city/usa");
        var cityNames = await response.Content.ReadFromJsonAsync<string[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(cityNames);
        Assert.Equal(
            ["Chicago", "Houston", "Los Angeles", "New York", "Philadelphia", "Phoenix"],
            cityNames);
    }
}
