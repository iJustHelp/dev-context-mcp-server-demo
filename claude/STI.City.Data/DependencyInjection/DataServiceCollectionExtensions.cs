using Microsoft.Extensions.DependencyInjection;
using OpenMeteo.Api.Client;
using STI.City.Core.Abstractions;

namespace STI.City.Data.DependencyInjection;

public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SQLite geocoding cache, the Open-Meteo typed client, and the
    /// geocoding provider that adapts it to <see cref="IGeocodingProvider"/>.
    /// </summary>
    public static IServiceCollection AddCityData(this IServiceCollection services, CityDataOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IGeocodingCacheRepository, SqliteGeocodingCacheRepository>();

        // Open-Meteo client via HttpClientFactory (registers IOpenMeteoClient).
        services.AddOpenMeteoApiClient(client => client.BaseAddress = new Uri(options.OpenMeteoBaseAddress));
        services.AddScoped<IGeocodingProvider, OpenMeteoGeocodingProvider>();

        return services;
    }
}
