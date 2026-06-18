using System.Globalization;
using Microsoft.Data.Sqlite;

namespace STI.City.Tests.Integration;

/// <summary>
/// Direct SQLite access used by integration tests to seed and inspect the
/// cache database independently of the application code under test.
/// </summary>
internal static class SqliteCacheProbe
{
    public static int CountRows(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM GeocodingCache;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public static long? GetPopulation(string connectionString, string normalizedKey)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Population FROM GeocodingCache WHERE NormalizedCityName = @key;";
        command.Parameters.AddWithValue("@key", normalizedKey);
        var value = command.ExecuteScalar();
        return value is null or DBNull ? null : Convert.ToInt64(value);
    }

    public static void Seed(
        string connectionString,
        string normalizedKey,
        string displayName,
        string country,
        double latitude,
        double longitude,
        long? population)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO GeocodingCache
                (NormalizedCityName, DisplayName, Country, Latitude, Longitude, Population, RetrievedAtUtc)
            VALUES
                (@key, @display, @country, @lat, @lon, @pop, @retrieved);
            """;
        command.Parameters.AddWithValue("@key", normalizedKey);
        command.Parameters.AddWithValue("@display", displayName);
        command.Parameters.AddWithValue("@country", country);
        command.Parameters.AddWithValue("@lat", latitude);
        command.Parameters.AddWithValue("@lon", longitude);
        command.Parameters.AddWithValue("@pop", (object?)population ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@retrieved",
            DateTimeOffset.UtcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }
}
