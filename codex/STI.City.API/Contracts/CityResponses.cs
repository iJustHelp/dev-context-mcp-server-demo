namespace STI.City.API.Contracts;

public sealed record CityLocationResponse(
    string CityName,
    string Country,
    double Latitude,
    double Longitude);

public sealed record CityPopulationResponse(
    string CityName,
    string Country,
    long Population);
