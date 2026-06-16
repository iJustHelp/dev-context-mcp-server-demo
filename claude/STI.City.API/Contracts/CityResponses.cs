namespace STI.City.API.Contracts;

/// <summary>Stable JSON contract for <c>GET /city/{cityName}/location</c>.</summary>
public sealed record CityLocationResponse(
    string CityName,
    string Country,
    double Latitude,
    double Longitude);

/// <summary>Stable JSON contract for <c>GET /city/{cityName}/population</c>.</summary>
public sealed record CityPopulationResponse(
    string CityName,
    string Country,
    long Population);
