namespace STI.City.API.Dtos;

/// <summary>Response body for <c>GET /city/{cityName}/location</c>.</summary>
public sealed record CityLocationResponse(string CityName, string? Country, double Latitude, double Longitude);

/// <summary>Response body for <c>GET /city/{cityName}/population</c>.</summary>
public sealed record CityPopulationResponse(string CityName, string? Country, int Population);
