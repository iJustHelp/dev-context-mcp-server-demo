using Microsoft.Extensions.Configuration;

namespace STI.City.API.Configuration;

public static class CacheConfiguration
{
    public const string ConnectionStringName = "CityCache";

    public static string GetRequiredCityCacheConnectionString(this IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} must be configured.");
        }

        return connectionString;
    }
}
