namespace STI.City.Core.Abstractions;

/// <summary>
/// Read access to the <c>Demo.Cities</c> catalog: the global and U.S. city lists,
/// plus case-insensitive resolution of a requested name to its canonical spelling.
/// </summary>
public interface ICityCatalog
{
    /// <summary>All city names, in the package-provided alphabetical order (FR-1, FR-7).</summary>
    IReadOnlyList<string> GetAllCityNames();

    /// <summary>U.S. city names, in the package-provided alphabetical order (FR-2, FR-7).</summary>
    IReadOnlyList<string> GetUsaCityNames();

    /// <summary>
    /// Returns the canonical (package-spelled) city name for <paramref name="cityName"/>
    /// matched case-insensitively (FR-5), or <c>null</c> when the city is unknown (FR-6).
    /// </summary>
    string? ResolveCanonicalName(string cityName);
}
