using Demo.Cities;
using OpenMeteo.Api.Client;
using Serilog;
using STI.City.API.Configuration;
using STI.City.API.Endpoints;
using STI.City.API.Infrastructure;
using STI.City.Core.DependencyInjection;
using STI.City.Data.DependencyInjection;
using STI.City.Data.Schema;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) => loggerConfiguration
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] =
            System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Package services through their verified DI extensions.
builder.Services.AddDemoCities();
builder.Services.AddOpenMeteoApiClient();

// Application service, repository, TimeProvider, and schema initializer.
builder.Services.AddCityCore();
builder.Services.AddCityData();

var app = builder.Build();

// Fail fast when the cache connection string is missing or blank. Validated
// against the built configuration so test/host overrides are honored.
CacheConfiguration.GetRequiredConnectionString(app.Configuration);

// Initialize the SQLite schema before accepting requests.
using (var scope = app.Services.CreateScope())
{
    var schemaInitializer = scope.ServiceProvider.GetRequiredService<GeocodingCacheSchemaInitializer>();
    await schemaInitializer.InitializeAsync();
}

app.UseExceptionHandler();

app.MapCityEndpoints();

app.Run();

/// <summary>Exposed so the integration test project can host the API.</summary>
public partial class Program;
