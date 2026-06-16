using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OpenMeteo.Api.Client;
using STI.City.API.Contracts;
using STI.City.Core.Models;
using STI.City.Core.Repositories;

namespace STI.City.Tests.Integration;

/// <summary>
/// Stage 5: end-to-end HTTP coverage for section 11 of <c>design/spec.md</c>,
/// exercising the real pipeline, DI wiring, and SQLite cache with deterministic
/// package doubles and an isolated database per test.
/// </summary>
public sealed class CityApiTests
{
    [Fact]
    public async Task GetCity_ReturnsExactPackageListAndOrder()
    {
        using var factory = new CityApiFactory();
        factory.CityService.Setup(c => c.GetCityNames()).Returns(["Chicago", "London", "Tokyo"]);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/city");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<List<string>>();
        Assert.Equal(["Chicago", "London", "Tokyo"], body);
    }

    [Fact]
    public async Task GetCityUsa_ReturnsExactPackageListAndOrder()
    {
        using var factory = new CityApiFactory();
        factory.UsaCityService.Setup(c => c.GetCityNames()).Returns(["Chicago", "New York", "Seattle"]);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/city/usa");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<string>>();
        Assert.Equal(["Chicago", "New York", "Seattle"], body);
    }

    [Theory]
    [InlineData("/city/New%20York/location")]   // encoded space
    [InlineData("/city/new%20york/location")]    // mixed case
    [InlineData("/city/%20New%20York%20/location")] // surrounding whitespace
    public async Task LocationEndpoint_ResolvesCanonicalCity_AcrossEncodingCaseAndWhitespace(string path)
    {
        using var factory = new CityApiFactory();
        SetupCities(factory, "New York");
        SetupUpstream(factory, "New York", Location("New York", "United States", 40.7128, -74.006, 8_804_190));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CityLocationResponse>();
        Assert.Equal("New York", body!.CityName);
        Assert.Equal("United States", body.Country);
        Assert.Equal(40.7128, body.Latitude);
        Assert.Equal(-74.006, body.Longitude);
    }

    [Fact]
    public async Task UnknownCity_Returns404_AndMakesNoCacheOrUpstreamCall()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory, "London", "New York");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/city/Atlantis/location");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("City not found", await TitleOf(response));
        factory.OpenMeteo.Verify(
            c => c.SearchLocationsAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(0, await factory.CountCacheRowsAsync());
    }

    [Fact]
    public async Task LocationCacheMiss_CallsUpstreamOnce_PersistsOneRow()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory, "New York");
        SetupUpstream(factory, "New York", Location("New York", "United States", 40.7128, -74.006, 8_804_190));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/city/New%20York/location");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await factory.CountCacheRowsAsync());
        VerifyUpstreamCalled(factory, "New York", Times.Once());
    }

    [Fact]
    public async Task PopulationCacheMiss_CallsUpstreamOnce_ReturnsPopulation()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory, "New York");
        SetupUpstream(factory, "New York", Location("New York", "United States", 40.7128, -74.006, 8_804_190));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/city/New%20York/population");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CityPopulationResponse>();
        Assert.Equal("New York", body!.CityName);
        Assert.Equal(8_804_190, body.Population);
        Assert.Equal(1, await factory.CountCacheRowsAsync());
        VerifyUpstreamCalled(factory, "New York", Times.Once());
    }

    [Fact]
    public async Task LocationThenPopulation_MakesOneTotalUpstreamCall()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory, "New York");
        SetupUpstream(factory, "New York", Location("New York", "United States", 40.7128, -74.006, 8_804_190));
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/city/New%20York/location")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/city/New%20York/population")).StatusCode);

        VerifyUpstreamCalled(factory, "New York", Times.Once());
        Assert.Equal(1, await factory.CountCacheRowsAsync());
    }

    [Fact]
    public async Task PopulationThenLocation_MakesOneTotalUpstreamCall()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory, "New York");
        SetupUpstream(factory, "New York", Location("New York", "United States", 40.7128, -74.006, 8_804_190));
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/city/New%20York/population")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/city/New%20York/location")).StatusCode);

        VerifyUpstreamCalled(factory, "New York", Times.Once());
        Assert.Equal(1, await factory.CountCacheRowsAsync());
    }

    [Fact]
    public async Task PreSeededCacheRow_ReturnsWithoutCallingUpstream()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory, "New York");
        using var client = factory.CreateClient();
        await factory.SeedAsync(new GeocodingCacheRecord
        {
            NormalizedCityName = "NEW YORK",
            DisplayName = "New York",
            Country = "United States",
            Latitude = 40.7128,
            Longitude = -74.006,
            Population = 8_804_190,
            RetrievedAtUtc = DateTimeOffset.UtcNow,
        });

        var response = await client.GetAsync("/city/New%20York/location");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        factory.OpenMeteo.Verify(
            c => c.SearchLocationsAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EmptyUpstreamResult_Returns404()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory, "Springfield");
        SetupUpstream(factory, "Springfield"); // no results
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/city/Springfield/location");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Geocoding result not found", await TitleOf(response));
        Assert.Equal(0, await factory.CountCacheRowsAsync());
    }

    [Fact]
    public async Task MissingPopulation_LocationReturns200_PopulationReturns404_OneUpstreamCall()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory, "Atlantis");
        SetupUpstream(factory, "Atlantis", Location("Atlantis", "Nowhere", 1.0, 2.0, population: null));
        using var client = factory.CreateClient();

        var location = await client.GetAsync("/city/Atlantis/location");
        var population = await client.GetAsync("/city/Atlantis/population");

        Assert.Equal(HttpStatusCode.OK, location.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, population.StatusCode);
        Assert.Equal("Population not found", await TitleOf(population));
        VerifyUpstreamCalled(factory, "Atlantis", Times.Once());
    }

    [Fact]
    public async Task UpstreamFailureOnCacheMiss_Returns502()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory, "Paris");
        factory.OpenMeteo
            .Setup(c => c.SearchLocationsAsync("Paris", null, "en", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("upstream down", 503, "body", null, null));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/city/Paris/location");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Geocoding service unavailable", await TitleOf(response));
    }

    [Fact]
    public async Task CachedRecord_ReturnsOk_EvenWhenUpstreamWouldFail()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory, "Paris");
        // Upstream would fail, but a cache hit must never reach it.
        factory.OpenMeteo
            .Setup(c => c.SearchLocationsAsync("Paris", null, "en", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("upstream down", 503, "body", null, null));
        using var client = factory.CreateClient();
        await factory.SeedAsync(new GeocodingCacheRecord
        {
            NormalizedCityName = "PARIS",
            DisplayName = "Paris",
            Country = "France",
            Latitude = 48.8566,
            Longitude = 2.3522,
            Population = 2_140_000,
            RetrievedAtUtc = DateTimeOffset.UtcNow,
        });

        var response = await client.GetAsync("/city/Paris/location");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CityLocationResponse>();
        Assert.Equal("Paris", body!.CityName);
    }

    [Fact]
    public async Task RepeatedDetailRequests_LeaveExactlyOneCacheRow()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory, "New York");
        SetupUpstream(factory, "New York", Location("New York", "United States", 40.7128, -74.006, 8_804_190));
        using var client = factory.CreateClient();

        await client.GetAsync("/city/New%20York/location");
        await client.GetAsync("/city/New%20York/population");
        await client.GetAsync("/city/new%20york/location");

        Assert.Equal(1, await factory.CountCacheRowsAsync());
    }

    [Fact]
    public async Task SqliteFailure_Returns500_WithoutExposingDetails()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory, "Chicago");
        var failingRepository = new Mock<IGeocodingCacheRepository>(MockBehavior.Strict);
        failingRepository
            .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SqliteException("SECRET-INTERNAL-DETAIL", 1));
        factory.OverrideServices = services =>
        {
            services.RemoveAll<IGeocodingCacheRepository>();
            services.AddSingleton(failingRepository.Object);
        };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/city/Chicago/location");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Internal server error", await TitleOf(response));
        Assert.DoesNotContain("SECRET-INTERNAL-DETAIL", raw);
        Assert.DoesNotContain("SqliteException", raw);
    }

    [Fact]
    public async Task SuccessResponse_UsesSpecifiedJsonFieldNamesAndContentType()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory, "New York");
        SetupUpstream(factory, "New York", Location("New York", "United States", 40.7128, -74.006, 8_804_190));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/city/New%20York/location");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("New York", root.GetProperty("cityName").GetString());
        Assert.Equal("United States", root.GetProperty("country").GetString());
        Assert.Equal(40.7128, root.GetProperty("latitude").GetDouble());
        Assert.Equal(-74.006, root.GetProperty("longitude").GetDouble());
    }

    [Fact]
    public async Task ProblemResponses_IncludeTraceId()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory, "London");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/city/Atlantis/location");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.True(document.RootElement.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
    }

    private static void SetupCities(CityApiFactory factory, params string[] names) =>
        factory.CityService.Setup(c => c.GetCityNames()).Returns(names);

    private static void SetupUpstream(CityApiFactory factory, string query, params LocationResult[] results) =>
        factory.OpenMeteo
            .Setup(c => c.SearchLocationsAsync(query, null, "en", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodingResponse { Results = results.ToList() });

    private static void VerifyUpstreamCalled(CityApiFactory factory, string query, Times times) =>
        factory.OpenMeteo.Verify(
            c => c.SearchLocationsAsync(query, null, "en", null, It.IsAny<CancellationToken>()),
            times);

    private static LocationResult Location(
        string name, string country, double latitude, double longitude, int? population) =>
        new()
        {
            Name = name,
            Country = country,
            Latitude = latitude,
            Longitude = longitude,
            Population = population,
        };

    private static async Task<string?> TitleOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("title").GetString();
    }
}
