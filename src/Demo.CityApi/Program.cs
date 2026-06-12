using Demo.Cities;
using Demo.CityApi.Caching;
using Demo.CityApi.Geocoding;
using OpenMeteo.Api.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddDemoCities();
builder.Services.AddOpenMeteoApiClient();
builder.Services.AddSingleton<ICityCacheSchemaInitializer, CityCacheSchemaInitializer>();
builder.Services.AddTransient<IGeocodingCacheRepository, GeocodingCacheRepository>();
builder.Services.AddTransient<IGeocodingService, CacheAsideGeocodingService>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

await app.Services
    .GetRequiredService<ICityCacheSchemaInitializer>()
    .InitializeAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapGet("/city", (ICityService cityService) =>
    TypedResults.Ok(cityService.GetCityNames()));

app.MapGet("/city/usa", (IUsaCityService cityService) =>
    TypedResults.Ok(cityService.GetCityNames()));

app.MapCityDetailEndpoints();

app.Run();

public partial class Program;
