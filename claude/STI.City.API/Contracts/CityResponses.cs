namespace STI.City.API.Contracts;

/// <summary>Stable JSON contract for the location endpoint.</summary>
public sealed record CityLocationResponse(
    string CityName,
    string Country,
    double Latitude,
    double Longitude);

/// <summary>Stable JSON contract for the population endpoint.</summary>
public sealed record CityPopulationResponse(
    string CityName,
    string Country,
    long Population);
