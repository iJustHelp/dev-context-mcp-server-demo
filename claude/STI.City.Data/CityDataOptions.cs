namespace STI.City.Data;

/// <summary>Configuration for the data layer (SQLite cache + Open-Meteo client).</summary>
public sealed class CityDataOptions
{
    /// <summary>ADO.NET connection string for the SQLite cache (required).</summary>
    public required string ConnectionString { get; init; }

    /// <summary>Base address for the Open-Meteo geocoding API.</summary>
    public string OpenMeteoBaseAddress { get; init; } = "https://geocoding-api.open-meteo.com/";
}
