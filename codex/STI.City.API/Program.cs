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

builder.Host.UseSerilog((context, services, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

var cityCacheConnectionString = builder.Configuration.GetRequiredCityCacheConnectionString();

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddDemoCities();
builder.Services.AddOpenMeteoApiClient();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddCityCore();
builder.Services.AddCityData(cityCacheConnectionString);

var app = builder.Build();

await app.Services.InitializeGeocodingCacheSchemaAsync(app.Lifetime.ApplicationStopping);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.MapCityEndpoints();

app.Run();

public partial class Program;
