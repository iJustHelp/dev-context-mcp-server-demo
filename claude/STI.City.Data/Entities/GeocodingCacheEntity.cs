using Dapper;

namespace STI.City.Data.Entities;

/// <summary>
/// SQLite persistence shape for the <c>GeocodingCache</c> table.
/// <see cref="RetrievedAtUtc"/> is stored as a UTC ISO-8601 string.
/// </summary>
[Table("GeocodingCache")]
public sealed class GeocodingCacheEntity
{
    [Key]
    public string NormalizedCityName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public long? Population { get; set; }

    public string RetrievedAtUtc { get; set; } = string.Empty;
}

/// <summary>
/// Constraints model required by <c>Formula.SimpleRepo.RepositoryBase</c>.
/// The repository performs its reads and the atomic upsert through Dapper, so
/// no scoped constraints are declared.
/// </summary>
public sealed class GeocodingCacheConstraints
{
}
