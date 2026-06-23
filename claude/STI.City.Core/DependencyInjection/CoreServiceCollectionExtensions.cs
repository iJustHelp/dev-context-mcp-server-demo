using Demo.Cities;
using Microsoft.Extensions.DependencyInjection;
using STI.City.Core.Abstractions;
using STI.City.Core.Services;
using STI.City.Core.Time;

namespace STI.City.Core.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Demo.Cities services (<c>ICityService</c>, <c>IUsaCityService</c>),
    /// the system clock, the city catalog, and the geocoding orchestration service.
    /// </summary>
    public static IServiceCollection AddCityCore(this IServiceCollection services)
    {
        // Demo.Cities QA package: registers ICityService and IUsaCityService.
        services.AddDemoCities();

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICityCatalog, CityCatalog>();
        services.AddScoped<ICityGeocodingService, CityGeocodingService>();

        return services;
    }
}
