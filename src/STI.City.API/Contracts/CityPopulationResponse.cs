namespace STI.City.API.Contracts;

public sealed record CityPopulationResponse(
    string CityName,
    string Country,
    long Population);
