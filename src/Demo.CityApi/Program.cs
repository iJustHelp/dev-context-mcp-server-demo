using Demo.Cities;
using Demo.CityApi.Caching;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddDemoCities();
builder.Services.AddSingleton<ICityCacheSchemaInitializer, CityCacheSchemaInitializer>();
builder.Services.AddTransient<IGeocodingCacheRepository, GeocodingCacheRepository>();

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

app.Run();

public partial class Program;
