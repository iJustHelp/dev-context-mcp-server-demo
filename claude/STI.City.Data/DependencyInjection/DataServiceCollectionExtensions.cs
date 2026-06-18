using Microsoft.Extensions.DependencyInjection;
using STI.City.Core.Repositories;
using STI.City.Data.Repositories;
using STI.City.Data.Schema;

namespace STI.City.Data.DependencyInjection;

public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SQLite schema initializer and the SimpleRepo-backed cache
    /// repository. The repository uses the transient lifetime expected by
    /// <c>Formula.SimpleRepo</c>.
    /// </summary>
    public static IServiceCollection AddCityData(this IServiceCollection services)
    {
        services.AddSingleton<GeocodingCacheSchemaInitializer>();
        services.AddTransient<IGeocodingCacheRepository, SimpleRepoGeocodingCacheRepository>();
        return services;
    }
}
