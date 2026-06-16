using Microsoft.Extensions.DependencyInjection;

namespace STI.City.Core.Geocoding
{
    public static class CityGeocodingServiceCollectionExtensions
    {
        public static IServiceCollection AddCityGeocoding(this IServiceCollection services)
        {
            services.AddScoped<ICityGeocodingService, CityGeocodingService>();
            return services;
        }
    }
}
