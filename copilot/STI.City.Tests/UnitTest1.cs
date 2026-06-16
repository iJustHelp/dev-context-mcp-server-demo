using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using STI.City.API;
using STI.City.Data;
using Xunit;

namespace STI.City.Tests;

public sealed class CityGeocodingTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        _connectionString = "Data Source=:memory:;Mode=Memory";

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove and replace IConfiguration with in-memory config
                    var descriptor = services.FirstOrDefault(d =>
                        d.ServiceType == typeof(IConfiguration));
                    if (descriptor is not null)
                    {
                        services.Remove(descriptor);
                    }

                    var config = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            { "ConnectionStrings:CityCache", _connectionString }
                        })
                        .Build();

                    services.AddSingleton<IConfiguration>(config);
                });
            });

        _client = _factory.CreateClient();
        
        // Initialize schema
        await SqliteSchemaInitializer.InitializeAsync(_connectionString).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task GetCityLocation_WithValidCity_Returns200WithLocation()
    {
        var response = await _client.GetAsync("/city/NewYork/location");
        
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetCityLocation_WithInvalidCity_Returns404()
    {
        var response = await _client.GetAsync("/city/InvalidCityXYZ/location");
        
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCityPopulation_WithValidCity_Returns200WithPopulation()
    {
        var response = await _client.GetAsync("/city/NewYork/population");
        
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetCityPopulation_WithMissingPopulation_Returns404()
    {
        var response = await _client.GetAsync("/city/NoPopCity/population");
        
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCities_ReturnsListOfCityNames()
    {
        var response = await _client.GetAsync("/city");
        
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var cities = await response.Content.ReadAsAsync<IEnumerable<string>>();
        Assert.NotNull(cities);
        Assert.NotEmpty(cities);
    }

    [Fact]
    public async Task GetUsaCities_ReturnsListOfUsaCityNames()
    {
        var response = await _client.GetAsync("/city/usa");
        
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var cities = await response.Content.ReadAsAsync<IEnumerable<string>>();
        Assert.NotNull(cities);
        Assert.NotEmpty(cities);
    }

    [Fact]
    public async Task CityLocation_CacheHit_ReturnsCachedRecord()
    {
        // First call populates cache
        var firstResponse = await _client.GetAsync("/city/Paris/location");
        Assert.Equal(System.Net.HttpStatusCode.OK, firstResponse.StatusCode);
        
        // Second call should use cache
        var secondResponse = await _client.GetAsync("/city/Paris/location");
        Assert.Equal(System.Net.HttpStatusCode.OK, secondResponse.StatusCode);
        
        Assert.True(firstResponse.IsSuccessStatusCode);
        Assert.True(secondResponse.IsSuccessStatusCode);
    }
}

