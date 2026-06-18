using Microsoft.Extensions.DependencyInjection;
using STI.City.Core.Services;

namespace STI.City.Core.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the application geocoding service (scoped) and the system
    /// <see cref="TimeProvider"/> (singleton). Package and repository
    /// registrations are added by their own extensions.
    /// </summary>
    public static IServiceCollection AddCityCore(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICityGeocodingService, CityGeocodingService>();
        return services;
    }
}
