using Demo.CityApi.Caching;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Demo.CityApi.Tests;

internal sealed class TemporarySqliteDatabase : IAsyncDisposable
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"demo-city-cache-{Guid.NewGuid():N}.db");

    public TemporarySqliteDatabase()
    {
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Pooling = false,
        }.ToString();

        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [
                    $"ConnectionStrings:{CityCacheConfiguration.ConnectionStringName}"
                ] = ConnectionString,
            })
            .Build();

        Initializer = new CityCacheSchemaInitializer(Configuration);
        Repository = new GeocodingCacheRepository(Configuration);
    }

    public string ConnectionString { get; }

    public IConfiguration Configuration { get; }

    public ICityCacheSchemaInitializer Initializer { get; }

    public IGeocodingCacheRepository Repository { get; }

    public async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public ValueTask DisposeAsync()
    {
        DeleteFiles(_databasePath);
        return ValueTask.CompletedTask;
    }

    internal static void DeleteFiles(string databasePath)
    {
        SqliteConnection.ClearAllPools();

        foreach (var path in new[]
        {
            databasePath,
            $"{databasePath}-shm",
            $"{databasePath}-wal",
        })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
