using Microsoft.Data.Sqlite;

namespace STI.City.Data
{
    public static class SqliteSchemaInitializer
    {
        public static async Task InitializeAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = """
CREATE TABLE IF NOT EXISTS GeocodingCache (
    NormalizedCityName TEXT NOT NULL PRIMARY KEY,
    DisplayName TEXT NOT NULL,
    Country TEXT NOT NULL,
    Latitude REAL NOT NULL,
    Longitude REAL NOT NULL,
    Population INTEGER NULL,
    RetrievedUtc TEXT NOT NULL
);
""";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
