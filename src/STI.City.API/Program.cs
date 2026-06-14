using Demo.Cities;
using Microsoft.Extensions.Options;
using OpenMeteo.Api.Client;
using Serilog;
using STI.City.API.Configuration;
using STI.City.API.Endpoints;
using STI.City.API.ErrorHandling;
using STI.City.Core;
using STI.City.Data;
using STI.City.Data.Geocoding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<InternalServerErrorExceptionHandler>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services
    .AddOptions<CityCacheOptions>()
    .BindConfiguration("ConnectionStrings")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.CityCache),
        "ConnectionStrings:CityCache must be configured.")
    .ValidateOnStart();
builder.Services.AddDemoCities();
builder.Services.AddOpenMeteoApiClient();
builder.Services.AddCityCore();
builder.Services.AddCityData();
builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

var app = builder.Build();

_ = app.Services.GetRequiredService<IOptions<CityCacheOptions>>().Value;
await app.Services
    .GetRequiredService<ICityCacheSchemaInitializer>()
    .InitializeAsync();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

app.MapCityEndpoints();

app.Run();

public partial class Program;
