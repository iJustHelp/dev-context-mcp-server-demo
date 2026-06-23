namespace STI.City.Core.Exceptions;

/// <summary>
/// Raised by an <see cref="Abstractions.IGeocodingProvider"/> when the upstream
/// geocoding call fails (e.g. an Open-Meteo <c>ApiException</c>). The orchestrator
/// maps this to <c>502 Bad Gateway</c> when no cached result is available (FR-14).
/// </summary>
public sealed class GeocodingUnavailableException : Exception
{
    public GeocodingUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
