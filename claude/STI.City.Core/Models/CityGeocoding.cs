namespace STI.City.Core.Models;

/// <summary>
/// The geocoding details cached for a single city. One record exists per
/// <see cref="NormalizedName"/> (FR-11); both the location and population
/// endpoints read from the same record (FR-10).
/// </summary>
public sealed record CityGeocoding
{
    /// <summary>Normalized city name — the cache key (VR-3, FR-11).</summary>
    public required string NormalizedName { get; init; }

    /// <summary>Human-readable name from the geocoding result.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Country from the geocoding result.</summary>
    public string? Country { get; init; }

    /// <summary>Latitude served by <c>/location</c>.</summary>
    public double Latitude { get; init; }

    /// <summary>Longitude served by <c>/location</c>.</summary>
    public double Longitude { get; init; }

    /// <summary>Population served by <c>/population</c> (nullable upstream).</summary>
    public int? Population { get; init; }

    /// <summary>When the result was fetched/persisted.</summary>
    public DateTimeOffset RetrievedAtUtc { get; init; }
}
