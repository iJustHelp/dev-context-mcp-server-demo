using Demo.Cities;
using STI.City.Core.Abstractions;

namespace STI.City.Core.Services;

/// <summary>
/// <see cref="ICityCatalog"/> backed by the <c>Demo.Cities</c> services. The list
/// order returned by both services is preserved (FR-7); name resolution is
/// case-insensitive (FR-5).
/// </summary>
public sealed class CityCatalog : ICityCatalog
{
    private readonly ICityService _cityService;
    private readonly IUsaCityService _usaCityService;

    public CityCatalog(ICityService cityService, IUsaCityService usaCityService)
    {
        _cityService = cityService;
        _usaCityService = usaCityService;
    }

    public IReadOnlyList<string> GetAllCityNames() => _cityService.GetCityNames();

    public IReadOnlyList<string> GetUsaCityNames() => _usaCityService.GetCityNames();

    public string? ResolveCanonicalName(string cityName)
    {
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return null;
        }

        var query = cityName.Trim();
        foreach (var name in _cityService.GetCityNames())
        {
            if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }
}
