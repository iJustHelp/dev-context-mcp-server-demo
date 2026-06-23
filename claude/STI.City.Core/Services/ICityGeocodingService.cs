using STI.City.Core.Models;

namespace STI.City.Core.Services;

/// <summary>
/// Application service behind the four endpoints: the two city lists and the
/// cache-aside geocoding lookup shared by <c>/location</c> and <c>/population</c>.
/// </summary>
public interface ICityGeocodingService
{
    /// <summary>City names for <c>GET /city</c> (FR-1, FR-7).</summary>
    IReadOnlyList<string> GetCityNames();

    /// <summary>U.S. city names for <c>GET /city/usa</c> (FR-2, FR-7).</summary>
    IReadOnlyList<string> GetUsaCityNames();

    /// <summary>
    /// Resolves the geocoding record for <paramref name="cityName"/> using the
    /// cache-aside strategy (FR-6, FR-8…FR-14). The returned <see cref="GeocodingLookup.Status"/>
    /// tells the endpoint which HTTP status to produce.
    /// </summary>
    Task<GeocodingLookup> GetGeocodingAsync(string cityName, CancellationToken cancellationToken = default);
}
