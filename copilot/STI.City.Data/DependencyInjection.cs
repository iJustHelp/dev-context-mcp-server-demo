using Formula.SimpleRepo;
using Microsoft.Extensions.DependencyInjection;
using STI.City.Core.Geocoding;
using STI.City.Data.Geocoding;
using System.Reflection;

namespace STI.City.Data
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCityData(this IServiceCollection services)
        {
            RepositoryConfiguration.AddRepositoriesInAssembly(services, Assembly.GetExecutingAssembly());
            services.AddTransient<IGeocodingCacheRepository, SimpleRepoGeocodingCacheRepository>();
            return services;
        }
    }
}
