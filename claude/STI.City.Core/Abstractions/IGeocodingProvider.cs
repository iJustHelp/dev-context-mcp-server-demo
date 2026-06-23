using STI.City.Core.Models;

namespace STI.City.Core.Abstractions;

/// <summary>
/// Upstream geocoding lookup (Open-Meteo). Decouples the orchestration logic from
/// the concrete <c>OpenMeteo.Api.Client</c> types.
/// </summary>
public interface IGeocodingProvider
{
    /// <summary>
    /// Looks up geocoding details for the canonical <paramref name="cityName"/>.
    /// Returns the selected result, or <c>null</c> when the provider has no matching
    /// result (FR-13). Throws <see cref="Exceptions.GeocodingUnavailableException"/>
    /// when the upstream call fails (FR-14).
    /// </summary>
    /// <remarks>
    /// <see cref="CityGeocoding.NormalizedName"/> and <see cref="CityGeocoding.RetrievedAtUtc"/>
    /// on the returned value are assigned by the orchestrator before caching.
    /// </remarks>
    Task<CityGeocoding?> FindAsync(string cityName, CancellationToken cancellationToken = default);
}
