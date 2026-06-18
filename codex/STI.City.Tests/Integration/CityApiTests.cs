using System.Net.Http.Headers;
using System.Text.Json;
using Moq;
using OpenMeteo.Api.Client;
using STI.City.Core.Models;

namespace STI.City.Tests.Integration;

public sealed class CityApiTests
{
    [Fact]
    public async Task GetCity_WhenCalled_ReturnsPackageListInOrder()
    {
        await using var factory = CreateFactory();
        factory.CityService.Setup(s => s.GetCityNames()).Returns(new[] { "new york", "london" });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJson(response);
        var actual = await response.Content.ReadFromJsonAsync<string[]>();
        Assert.Equal(new[] { "New York", "London" }, actual);
        factory.CityService.Verify(s => s.GetCityNames(), Times.Once);
        factory.CityService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetUsaCity_WhenCalled_ReturnsUsaPackageListInOrder()
    {
        await using var factory = CreateFactory();
        factory.UsaCityService.Setup(s => s.GetCityNames()).Returns(new[] { "new york", "seattle" });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city/usa");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJson(response);
        var actual = await response.Content.ReadFromJsonAsync<string[]>();
        Assert.Equal(new[] { "New York", "Seattle" }, actual);
        factory.UsaCityService.Verify(s => s.GetCityNames(), Times.Once);
        factory.UsaCityService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetLocation_WhenCacheMiss_PersistsRecordAndReturnsLocation()
    {
        await using var factory = CreateFactory();
        SetupCities(factory);
        factory.OpenMeteoClient.Setup(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(Location("New York", 8_804_190)));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city/New%20York/location");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJson(response);
        Assert.Equal("New York", body.RootElement.GetProperty("cityName").GetString());
        Assert.Equal("United States", body.RootElement.GetProperty("country").GetString());
        Assert.Equal(40.7128, body.RootElement.GetProperty("latitude").GetDouble(), 4);
        Assert.Equal(-74.006, body.RootElement.GetProperty("longitude").GetDouble(), 4);
        Assert.Equal(1, await new SqliteCacheProbe(factory.ConnectionString).CountAsync());
        factory.OpenMeteoClient.Verify(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPopulation_WhenLocationAlreadyCached_UsesSameRecordWithoutSecondUpstreamCall()
    {
        await using var factory = CreateFactory();
        SetupCities(factory);
        factory.OpenMeteoClient.Setup(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(Location("New York", 8_804_190)));
        using var client = factory.CreateClient();

        using var first = await client.GetAsync("/city/New%20York/location");
        using var second = await client.GetAsync("/city/new%20york/population");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var body = await ReadJson(second);
        Assert.Equal(8_804_190, body.RootElement.GetProperty("population").GetInt64());
        factory.OpenMeteoClient.Verify(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLocation_WhenPopulationAlreadyCached_UsesSameRecordWithoutSecondUpstreamCall()
    {
        await using var factory = CreateFactory();
        SetupCities(factory);
        factory.OpenMeteoClient.Setup(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(Location("New York", 8_804_190)));
        using var client = factory.CreateClient();

        using var first = await client.GetAsync("/city/New%20York/population");
        using var second = await client.GetAsync("/city/New%20York/location");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        factory.OpenMeteoClient.Verify(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLocation_WhenCachePreseeded_DoesNotCallUpstream()
    {
        await using var factory = CreateFactory();
        SetupCities(factory);
        using var client = factory.CreateClient();
        await new SqliteCacheProbe(factory.ConnectionString).SeedAsync(Record("NEW YORK", "New York", 8_804_190));

        using var response = await client.GetAsync("/city/New%20York/location");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        factory.OpenMeteoClient.Verify(c => c.SearchLocationsAsync(
            It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<Format?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetLocation_WhenCityOnlyInUsaList_ResolvesFromMergedCityNames()
    {
        await using var factory = CreateFactory();
        factory.CityService.Setup(s => s.GetCityNames()).Returns(new[] { "London" });
        factory.UsaCityService.Setup(s => s.GetCityNames()).Returns(new[] { "Seattle" });
        factory.OpenMeteoClient.Setup(c => c.SearchLocationsAsync("Seattle", 10, "en", Format.Json, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(Location("Seattle", 733_919)));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city/Seattle/location");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        factory.OpenMeteoClient.Verify(c => c.SearchLocationsAsync("Seattle", 10, "en", Format.Json, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLocation_WhenUnknownCity_ReturnsNotFoundWithoutUpstream()
    {
        await using var factory = CreateFactory();
        SetupCities(factory);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city/Atlantis/location");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertProblemJson(response);
        factory.OpenMeteoClient.Verify(c => c.SearchLocationsAsync(
            It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<Format?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPopulation_WhenPopulationMissing_ReturnsNotFoundAfterCachingLocation()
    {
        await using var factory = CreateFactory();
        SetupCities(factory);
        factory.OpenMeteoClient.Setup(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(Location("New York", null)));
        using var client = factory.CreateClient();

        using var location = await client.GetAsync("/city/New%20York/location");
        using var population = await client.GetAsync("/city/New%20York/population");

        Assert.Equal(HttpStatusCode.OK, location.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, population.StatusCode);
        Assert.Equal(1, await new SqliteCacheProbe(factory.ConnectionString).CountAsync());
        factory.OpenMeteoClient.Verify(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLocation_WhenUpstreamHasNoExactMatch_ReturnsNotFound()
    {
        await using var factory = CreateFactory();
        SetupCities(factory);
        factory.OpenMeteoClient.Setup(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(Location("Newark", 1)));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city/New%20York/location");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertProblemJson(response);
    }

    [Fact]
    public async Task GetLocation_WhenUpstreamFailsOnCacheMiss_ReturnsBadGateway()
    {
        await using var factory = CreateFactory();
        SetupCities(factory);
        factory.OpenMeteoClient.Setup(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network down"));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city/New%20York/location");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        AssertProblemJson(response);
    }

    private static CityApiFactory CreateFactory()
    {
        var factory = new CityApiFactory();
        factory.UsaCityService.Setup(s => s.GetCityNames()).Returns(Array.Empty<string>());
        return factory;
    }

    private static void SetupCities(CityApiFactory factory)
    {
        factory.CityService.Setup(s => s.GetCityNames()).Returns(new[] { "new york", "London" });
    }

    private static void AssertJson(HttpResponseMessage response) =>
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

    private static void AssertProblemJson(HttpResponseMessage response) =>
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response)
    {
        AssertJson(response);
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static GeocodingCacheRecord Record(string key, string displayName, long? population) =>
        new(key, displayName, "United States", 40.7128, -74.006, population,
            new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.Zero));

    private static LocationResult Location(string name, long? population) => new()
    {
        Name = name,
        Country = "United States",
        Latitude = 40.7128,
        Longitude = -74.006,
        Population = population is null ? null : (int)population.Value,
    };

    private static GeocodingResponse Response(params LocationResult[] results) =>
        new() { Results = results.ToList() };
}
