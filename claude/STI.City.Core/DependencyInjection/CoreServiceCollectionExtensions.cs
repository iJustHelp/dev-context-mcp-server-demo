using Microsoft.Extensions.DependencyInjection;
using STI.City.Core.Services;

namespace STI.City.Core.DependencyInjection;

/// <summary>Registers Core application services.</summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddCityCore(this IServiceCollection services)
    {
        services.AddScoped<ICityGeocodingService, CityGeocodingService>();
        return services;
    }
}
