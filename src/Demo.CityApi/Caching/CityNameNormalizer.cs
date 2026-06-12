namespace Demo.CityApi.Caching;

public static class CityNameNormalizer
{
    public static string Normalize(string cityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cityName);

        return cityName.Trim().ToUpperInvariant();
    }
}
