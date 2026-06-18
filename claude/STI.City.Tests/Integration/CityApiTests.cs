using System.Net;
using System.Net.Http.Json;
using Moq;
using OpenMeteo.Api.Client;
using STI.City.API.Contracts;
using STI.City.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace STI.City.Tests.Integration;

/// <summary>
/// HTTP integration tests covering section 11 of the specification: full
/// pipeline, dependency wiring, real SQLite cache behavior, and failure maps.
/// </summary>
public sealed class CityApiTests
{
    private const string NewYorkKey = "NEW YORK";
    private static readonly string[] GlobalCities = { "New York", "London", "Tokyo" };
    private static readonly string[] UsaCities = { "Chicago", "New York", "Seattle" };

    [Fact]
    public async Task GetCities_ReturnsExactPackageListAndOrder()
    {
        using var factory = new CityApiFactory();
        factory.CityService.Setup(c => c.GetCityNames()).Returns(GlobalCities);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/city");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var actual = await response.Content.ReadFromJsonAsync<string[]>();
        Assert.Equal(GlobalCities, actual);
    }

    [Fact]
    public async Task GetUsaCities_ReturnsExactPackageListAndOrder()
    {
        using var factory = new CityApiFactory();
        factory.UsaCityService.Setup(c => c.GetCityNames()).Returns(UsaCities);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/city/usa");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = await response.Content.ReadFromJsonAsync<string[]>();
        Assert.Equal(UsaCities, actual);
    }

    [Fact]
    public async Task GetLocation_EncodedCityName_ResolvesSuccessfully()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        SetupNewYorkUpstream(factory, population: 8_804_190);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/city/New%20York/location");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var actual = await response.Content.ReadFromJsonAsync<CityLocationResponse>();
        Assert.Equal("New York", actual!.CityName);
        Assert.Equal("United States", actual.Country);
        Assert.Equal(40.7128, actual.Latitude);
        Assert.Equal(-74.006, actual.Longitude);
    }

    [Fact]
    public async Task GetLocation_MixedCaseInput_ResolvesToCanonicalSpelling()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        SetupNewYorkUpstream(factory, population: 1);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/city/nEw%20yORk/location");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = await response.Content.ReadFromJsonAsync<CityLocationResponse>();
        Assert.Equal("New York", actual!.CityName);
    }

    [Fact]
    public async Task GetLocation_WhitespacePaddedInput_ResolvesToCanonicalSpelling()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        SetupNewYorkUpstream(factory, population: 1);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/city/%20New%20York%20/location");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = await response.Content.ReadFromJsonAsync<CityLocationResponse>();
        Assert.Equal("New York", actual!.CityName);
    }

    [Fact]
    public async Task GetLocation_UnknownCity_Returns404AndNoUpstreamCall()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        factory.OpenMeteoClient
            .Setup(c => c.SearchLocationsAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("upstream must not be called"));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/city/Atlantis/location");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        VerifyNoUpstreamCall(factory);
    }

    [Fact]
    public async Task GetLocation_CacheMiss_CallsUpstreamOnce_PersistsRecord()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        SetupNewYorkUpstream(factory, population: 8_804_190);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/city/New%20York/location");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        VerifyUpstreamCalled(factory, Times.Once());
        Assert.Equal(1, SqliteCacheProbe.CountRows(factory.ConnectionString));
        Assert.Equal(8_804_190, SqliteCacheProbe.GetPopulation(factory.ConnectionString, NewYorkKey));
    }

    [Fact]
    public async Task GetPopulation_CacheMiss_CallsUpstreamOnce_PersistsRecord()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        SetupNewYorkUpstream(factory, population: 8_804_190);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/city/New%20York/population");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = await response.Content.ReadFromJsonAsync<CityPopulationResponse>();
        Assert.Equal("New York", actual!.CityName);
        Assert.Equal(8_804_190, actual.Population);
        VerifyUpstreamCalled(factory, Times.Once());
        Assert.Equal(1, SqliteCacheProbe.CountRows(factory.ConnectionString));
    }

    [Fact]
    public async Task LocationThenPopulation_SharesOneCacheRecord_OneUpstreamCall()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        SetupNewYorkUpstream(factory, population: 8_804_190);
        var client = factory.CreateClient();

        var location = await client.GetAsync("/city/New%20York/location");
        var population = await client.GetAsync("/city/New%20York/population");

        Assert.Equal(HttpStatusCode.OK, location.StatusCode);
        Assert.Equal(HttpStatusCode.OK, population.StatusCode);
        VerifyUpstreamCalled(factory, Times.Once());
        Assert.Equal(1, SqliteCacheProbe.CountRows(factory.ConnectionString));
    }

    [Fact]
    public async Task PopulationThenLocation_SharesOneCacheRecord_OneUpstreamCall()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        SetupNewYorkUpstream(factory, population: 8_804_190);
        var client = factory.CreateClient();

        var population = await client.GetAsync("/city/New%20York/population");
        var location = await client.GetAsync("/city/New%20York/location");

        Assert.Equal(HttpStatusCode.OK, population.StatusCode);
        Assert.Equal(HttpStatusCode.OK, location.StatusCode);
        VerifyUpstreamCalled(factory, Times.Once());
        Assert.Equal(1, SqliteCacheProbe.CountRows(factory.ConnectionString));
    }

    [Fact]
    public async Task PreSeededCacheRow_ReturnsLocation_WithoutCallingUpstream()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        factory.OpenMeteoClient
            .Setup(c => c.SearchLocationsAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("upstream must not be called"));
        var client = factory.CreateClient();
        SqliteCacheProbe.Seed(
            factory.ConnectionString, NewYorkKey, "New York", "United States", 1.5, 2.5, 123);

        var response = await client.GetAsync("/city/New%20York/location");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = await response.Content.ReadFromJsonAsync<CityLocationResponse>();
        Assert.Equal(1.5, actual!.Latitude);
        VerifyNoUpstreamCall(factory);
    }

    [Fact]
    public async Task EmptyUpstreamResult_Returns404()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        factory.OpenMeteoClient
            .Setup(c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodingResponse { Results = new List<LocationResult>() });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/city/New%20York/location");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task MissingPopulation_LocationReturns200_PopulationReturns404_NoSecondUpstreamCall()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        SetupNewYorkUpstream(factory, population: null);
        var client = factory.CreateClient();

        var location = await client.GetAsync("/city/New%20York/location");
        var population = await client.GetAsync("/city/New%20York/population");

        Assert.Equal(HttpStatusCode.OK, location.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, population.StatusCode);
        Assert.Equal("application/problem+json", population.Content.Headers.ContentType?.MediaType);
        VerifyUpstreamCalled(factory, Times.Once());
    }

    [Fact]
    public async Task UpstreamFailureOnCacheMiss_Returns502ProblemDetails()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        factory.OpenMeteoClient
            .Setup(c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("upstream down", 503, "body", null!, null!));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/city/New%20York/location");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, SqliteCacheProbe.CountRows(factory.ConnectionString));
    }

    [Fact]
    public async Task CachedRecord_WithFailingUpstream_Returns200_WithoutCallingUpstream()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        factory.OpenMeteoClient
            .Setup(c => c.SearchLocationsAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("upstream down", 503, "body", null!, null!));
        var client = factory.CreateClient();
        SqliteCacheProbe.Seed(
            factory.ConnectionString, NewYorkKey, "New York", "United States", 40.7128, -74.006, 8_804_190);

        var response = await client.GetAsync("/city/New%20York/population");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = await response.Content.ReadFromJsonAsync<CityPopulationResponse>();
        Assert.Equal(8_804_190, actual!.Population);
        VerifyNoUpstreamCall(factory);
    }

    [Fact]
    public async Task RepeatedLookups_LeaveExactlyOneCacheRow()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        SetupNewYorkUpstream(factory, population: 8_804_190);
        var client = factory.CreateClient();

        await client.GetAsync("/city/New%20York/location");
        await client.GetAsync("/city/New%20York/population");
        await client.GetAsync("/city/new%20york/location");

        Assert.Equal(1, SqliteCacheProbe.CountRows(factory.ConnectionString));
    }

    [Fact]
    public async Task InternalSqliteFailure_Returns500_WithoutExposingExceptionDetails()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        var failingRepository = new Mock<IGeocodingCacheRepository>();
        failingRepository
            .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("secret-connection-failure"));
        factory.ConfigureExtraServices = services =>
        {
            services.RemoveAll<IGeocodingCacheRepository>();
            services.AddTransient(_ => failingRepository.Object);
        };
        var client = factory.CreateClient();

        var response = await client.GetAsync("/city/New%20York/location");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Internal server error", body);
        Assert.DoesNotContain("secret-connection-failure", body);
    }

    [Fact]
    public async Task LocationResponse_HasSpecifiedJsonFieldNamesAndContentType()
    {
        using var factory = new CityApiFactory();
        SetupCities(factory);
        SetupNewYorkUpstream(factory, population: 8_804_190);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/city/New%20York/location");

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"cityName\"", body);
        Assert.Contains("\"country\"", body);
        Assert.Contains("\"latitude\"", body);
        Assert.Contains("\"longitude\"", body);
    }

    private static void SetupCities(CityApiFactory factory)
    {
        factory.CityService.Setup(c => c.GetCityNames()).Returns(GlobalCities);
        factory.UsaCityService.Setup(c => c.GetCityNames()).Returns(UsaCities);
    }

    private static void SetupNewYorkUpstream(CityApiFactory factory, long? population)
    {
        factory.OpenMeteoClient
            .Setup(c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodingResponse
            {
                Results = new List<LocationResult>
                {
                    new()
                    {
                        Name = "New York",
                        Country = "United States",
                        Latitude = 40.7128,
                        Longitude = -74.006,
                        Population = population is null ? null : (int)population.Value,
                    },
                },
            });
    }

    private static void VerifyUpstreamCalled(CityApiFactory factory, Times times) =>
        factory.OpenMeteoClient.Verify(
            c => c.SearchLocationsAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()),
            times);

    private static void VerifyNoUpstreamCall(CityApiFactory factory) =>
        VerifyUpstreamCalled(factory, Times.Never());
}
