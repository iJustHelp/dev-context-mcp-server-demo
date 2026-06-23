using STI.City.API.Endpoints;
using STI.City.Core.Abstractions;
using STI.City.Core.DependencyInjection;
using STI.City.Data;
using STI.City.Data.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// The SQLite cache connection string is mandatory; fail fast if it is missing.
var connectionString = builder.Configuration.GetConnectionString("CityCache");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:CityCache must be configured (for example, 'Data Source=city-cache.db').");
}

var dataOptions = new CityDataOptions { ConnectionString = connectionString };
var openMeteoBaseAddress = builder.Configuration["OpenMeteo:BaseAddress"];
if (!string.IsNullOrWhiteSpace(openMeteoBaseAddress))
{
    dataOptions = new CityDataOptions
    {
        ConnectionString = connectionString,
        OpenMeteoBaseAddress = openMeteoBaseAddress,
    };
}

builder.Services.AddCityCore();
builder.Services.AddCityData(dataOptions);
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Ensure the cache table exists before serving requests.
await using (var scope = app.Services.CreateAsyncScope())
{
    var cache = scope.ServiceProvider.GetRequiredService<IGeocodingCacheRepository>();
    await cache.InitializeAsync();
}

app.MapCityEndpoints();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
