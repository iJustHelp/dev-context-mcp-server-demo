using Microsoft.Extensions.Configuration;

namespace STI.City.API.Configuration;

/// <summary>
/// Validates the required SQLite cache connection string at startup.
/// </summary>
public static class CacheConfiguration
{
    public const string ConnectionStringName = "CityCache";

    /// <summary>
    /// Returns the configured cache connection string, throwing when it is
    /// absent or blank so the application fails fast.
    /// </summary>
    public static string GetRequiredConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} must be configured and non-blank.");
        }

        return connectionString;
    }
}
