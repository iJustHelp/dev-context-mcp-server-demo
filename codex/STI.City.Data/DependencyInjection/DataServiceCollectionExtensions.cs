using Microsoft.Extensions.DependencyInjection;
using STI.City.Core.Repositories;
using STI.City.Data.Repositories;
using STI.City.Data.Schema;

namespace STI.City.Data.DependencyInjection;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddCityData(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton(new GeocodingCacheSchemaInitializer(connectionString));
        services.AddTransient<IGeocodingCacheRepository>(
            _ => new SimpleRepoGeocodingCacheRepository(connectionString));

        return services;
    }
}
