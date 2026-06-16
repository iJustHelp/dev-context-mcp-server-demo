using Demo.Cities;
using OpenMeteo.Api.Client;
using STI.City.API.Endpoints;
using STI.City.Core.Geocoding;
using STI.City.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddDemoCities();
builder.Services.AddOpenMeteoApiClient();
builder.Services.AddCityData();
builder.Services.AddCityGeocoding();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddEndpointsApiExplorer();

var connectionString = builder.Configuration.GetConnectionString("CityCache");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Configuration value 'ConnectionStrings:CityCache' is required.");
}

var app = builder.Build();

await SqliteSchemaInitializer.InitializeAsync(connectionString);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await Results.Problem(detail: "An unexpected error occurred.").ExecuteAsync(context);
        });
    });
}

app.MapCityEndpoints();

app.Run();

public partial class Program { }
