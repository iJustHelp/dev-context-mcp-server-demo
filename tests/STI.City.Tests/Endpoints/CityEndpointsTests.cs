using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Demo.Cities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using STI.City.API.Contracts;
using STI.City.Core.Geocoding;
using STI.City.Data.Geocoding;

namespace STI.City.Tests.Endpoints;

public sealed class CityEndpointsTests
{
    [Fact]
    public async Task Get_city_returns_package_order()
    {
        var cities = new[] { "Chicago", "London", "Tokyo" };
        using var mocks = new EndpointMocks();
        mocks.Cities
            .Setup(service => service.GetCityNames())
            .Returns(cities);
        await using var factory = new CityApiFactory(mocks);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city");
        var body = await response.Content.ReadFromJsonAsync<string[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(cities, body);
        mocks.Cities.Verify(service => service.GetCityNames(), Times.Once);
        mocks.Geocoding.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Get_usa_returns_usa_package_order_without_geocoding()
    {
        var cities = new[] { "Chicago", "Houston", "New York" };
        using var mocks = new EndpointMocks();
        mocks.UsaCities
            .Setup(service => service.GetCityNames())
            .Returns(cities);
        await using var factory = new CityApiFactory(mocks);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city/usa");
        var body = await response.Content.ReadFromJsonAsync<string[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(cities, body);
        mocks.UsaCities.Verify(service => service.GetCityNames(), Times.Once);
        mocks.Geocoding.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Location_returns_canonical_json_contract_for_encoded_city()
    {
        var record = CreateRecord(population: 8_804_190);
        using var mocks = new EndpointMocks();
        mocks.Geocoding
            .Setup(service => service.GetAsync(
                "New York",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CityGeocodingResult.Success(record));
        await using var factory = new CityApiFactory(mocks);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/city/New%20York/location");
        var body = await response.Content.ReadFromJsonAsync<
            CityLocationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            new CityLocationResponse(
                "New York",
                "United States",
                40.7128,
                -74.006),
            body);
        mocks.Geocoding.Verify(
            service => service.GetAsync(
                "New York",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Population_returns_shared_record_population()
    {
        var record = CreateRecord(population: 8_804_190);
        using var mocks = new EndpointMocks();
        mocks.Geocoding
            .Setup(service => service.GetAsync(
                "nEw yOrK",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CityGeocodingResult.Success(record));
        await using var factory = new CityApiFactory(mocks);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/city/nEw%20yOrK/population");
        var body = await response.Content.ReadFromJsonAsync<
            CityPopulationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            new CityPopulationResponse(
                "New York",
                "United States",
                8_804_190),
            body);
    }

    [Theory]
    [InlineData(CityGeocodingOutcome.CityNotFound, 404, "City not found")]
    [InlineData(
        CityGeocodingOutcome.GeocodingNotFound,
        404,
        "Geocoding result not found")]
    [InlineData(
        CityGeocodingOutcome.ServiceUnavailable,
        502,
        "Geocoding service unavailable")]
    public async Task Location_maps_service_outcomes_to_problem_details(
        CityGeocodingOutcome outcome,
        int expectedStatus,
        string expectedTitle)
    {
        using var mocks = new EndpointMocks();
        mocks.Geocoding
            .Setup(service => service.GetAsync(
                "Unknown",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResult(outcome));
        await using var factory = new CityApiFactory(mocks);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/city/Unknown/location");
        var problem = await ReadProblemAsync(response);

        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expectedTitle, problem.Title);
        Assert.Equal(expectedStatus, problem.Status);
        AssertTraceId(problem);
    }

    [Fact]
    public async Task Population_missing_returns_problem_without_second_lookup()
    {
        using var mocks = new EndpointMocks();
        mocks.Geocoding
            .Setup(service => service.GetAsync(
                "New York",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CityGeocodingResult.Success(CreateRecord(population: null)));
        await using var factory = new CityApiFactory(mocks);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/city/New%20York/population");
        var problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Population not found", problem.Title);
        AssertTraceId(problem);
        mocks.Geocoding.Verify(
            service => service.GetAsync(
                "New York",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Unexpected_failure_returns_sanitized_problem_details()
    {
        const string secret = "Data Source=secret.db";
        using var mocks = new EndpointMocks();
        mocks.Geocoding
            .Setup(service => service.GetAsync(
                "New York",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(secret));
        await using var factory = new CityApiFactory(mocks);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/city/New%20York/location");
        var content = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(
            content,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);
        Assert.Equal("Internal server error", problem?.Title);
        Assert.DoesNotContain(secret, content, StringComparison.Ordinal);
        Assert.DoesNotContain(
            nameof(InvalidOperationException),
            content,
            StringComparison.Ordinal);
        AssertTraceId(problem!);
    }

    private static GeocodingCacheRecord CreateRecord(long? population) =>
        new(
            "NEW YORK",
            "New York",
            "United States",
            40.7128,
            -74.006,
            population,
            new DateTimeOffset(
                2026,
                6,
                13,
                12,
                0,
                0,
                TimeSpan.Zero));

    private static CityGeocodingResult CreateResult(
        CityGeocodingOutcome outcome) =>
        outcome switch
        {
            CityGeocodingOutcome.CityNotFound =>
                CityGeocodingResult.CityNotFound(),
            CityGeocodingOutcome.GeocodingNotFound =>
                CityGeocodingResult.GeocodingNotFound(),
            CityGeocodingOutcome.ServiceUnavailable =>
                CityGeocodingResult.ServiceUnavailable(),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };

    private static async Task<ProblemDetails> ReadProblemAsync(
        HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<ProblemDetails>() ??
        throw new InvalidOperationException(
            "The response did not contain Problem Details.");

    private static void AssertTraceId(ProblemDetails problem)
    {
        Assert.True(problem.Extensions.TryGetValue("traceId", out var traceId));
        Assert.NotNull(traceId);
        Assert.False(string.IsNullOrWhiteSpace(traceId.ToString()));
    }

    private sealed class EndpointMocks : IDisposable
    {
        public Mock<ICityService> Cities { get; } =
            new(MockBehavior.Strict);

        public Mock<IUsaCityService> UsaCities { get; } =
            new(MockBehavior.Strict);

        public Mock<ICityGeocodingService> Geocoding { get; } =
            new(MockBehavior.Strict);

        public Mock<ICityCacheSchemaInitializer> SchemaInitializer
        {
            get;
        } = new(MockBehavior.Strict);

        public EndpointMocks()
        {
            SchemaInitializer
                .Setup(initializer => initializer.InitializeAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public void Dispose()
        {
            SchemaInitializer.Verify(
                initializer => initializer.InitializeAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    private sealed class CityApiFactory(EndpointMocks mocks)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:CityCache"] =
                            "Data Source=unused.db"
                    });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICityService>();
                services.RemoveAll<IUsaCityService>();
                services.RemoveAll<ICityGeocodingService>();
                services.RemoveAll<ICityCacheSchemaInitializer>();
                services.AddSingleton(mocks.Cities.Object);
                services.AddSingleton(mocks.UsaCities.Object);
                services.AddSingleton(mocks.Geocoding.Object);
                services.AddSingleton(mocks.SchemaInitializer.Object);
            });
        }
    }
}
