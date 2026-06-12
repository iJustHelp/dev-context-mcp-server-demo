using System.Globalization;
using Dapper;
using Formula.SimpleRepo;
using Microsoft.Data.Sqlite;

namespace Demo.CityApi.Caching;

[ConnectionDetails(
    CityCacheConfiguration.ConnectionStringName,
    typeof(SqliteConnection),
    SimpleCRUD.Dialect.SQLite)]
[Table("GeocodingCache")]
public sealed class GeocodingCacheEntry
{
    [Key]
    public int Id { get; set; }

    public string NormalizedCityName { get; set; } = string.Empty;

    public string DisplayCityName { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public long? Population { get; set; }

    [Column("RetrievedAtUtc")]
    public string RetrievedAtUtcValue { get; set; } = string.Empty;

    [NotMapped]
    public DateTimeOffset RetrievedAtUtc
    {
        get => DateTimeOffset.Parse(
            RetrievedAtUtcValue,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
        set => RetrievedAtUtcValue = value
            .ToUniversalTime()
            .ToString("O", CultureInfo.InvariantCulture);
    }
}
