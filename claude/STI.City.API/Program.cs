using Demo.Cities;
using OpenMeteo.Api.Client;
using Serilog;
using STI.City.API.Configuration;
using STI.City.API.Endpoints;
using STI.City.Core.DependencyInjection;
using STI.City.Data.DependencyInjection;
using STI.City.Data.Schema;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

// Verified package dependency-injection extensions.
builder.Services.AddDemoCities();
builder.Services.AddOpenMeteoApiClient();

// Application services and the SQLite cache repository.
builder.Services.AddCityCore();
builder.Services.AddCityData();

var app = builder.Build();

// Validate configuration (fail fast) and initialize the SQLite schema before
// the application accepts requests. Reading the final configuration keeps the
// schema and repository pointed at the same database under test overrides.
var cacheConnectionString = app.Configuration.GetRequiredCacheConnectionString();
await GeocodingCacheSchemaInitializer.InitializeAsync(cacheConnectionString);

// Centralized, sanitized exception handling outside development.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp => errorApp.Run(context =>
        CityEndpoints
            .CityProblem(context, StatusCodes.Status500InternalServerError, "Internal server error")
            .ExecuteAsync(context)));
}

app.MapCityEndpoints();

app.Run();

/// <summary>Exposes the entry point to the integration test project.</summary>
public partial class Program;
