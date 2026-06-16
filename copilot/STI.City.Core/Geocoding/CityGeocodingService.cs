using Demo.Cities;
using OpenMeteo.Api.Client;

namespace STI.City.Core.Geocoding
{
    public sealed class CityGeocodingService : ICityGeocodingService
    {
        private readonly ICityService _cityService;
        private readonly IUsaCityService _usaCityService;
        private readonly IOpenMeteoClient _openMeteoClient;
        private readonly IGeocodingCacheRepository _cacheRepository;
        private readonly TimeProvider _timeProvider;

        public CityGeocodingService(
            ICityService cityService,
            IUsaCityService usaCityService,
            IOpenMeteoClient openMeteoClient,
            IGeocodingCacheRepository cacheRepository,
            TimeProvider timeProvider)
        {
            _cityService = cityService;
            _usaCityService = usaCityService;
            _openMeteoClient = openMeteoClient;
            _cacheRepository = cacheRepository;
            _timeProvider = timeProvider;
        }

        public async Task<CityGeocodingOutcome> GetAsync(string cityName, CancellationToken cancellationToken = default)
        {
            if (cityName is null)
            {
                throw new ArgumentNullException(nameof(cityName));
            }

            var normalizedCityName = cityName.Trim();
            if (string.IsNullOrEmpty(normalizedCityName))
            {
                return CityGeocodingOutcome.CityNotFound();
            }

            var canonicalName = FindCanonicalCityName(normalizedCityName);
            if (canonicalName is null)
            {
                return CityGeocodingOutcome.CityNotFound();
            }

            var normalizedCacheKey = NormalizeCacheKey(canonicalName);
            var cached = await _cacheRepository.GetAsync(normalizedCacheKey, cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                return CityGeocodingOutcome.Success(cached);
            }

            try
            {
                var geocoding = await _openMeteoClient.SearchLocationsAsync(canonicalName, 10, "en", null, cancellationToken).ConfigureAwait(false);
                var match = geocoding.Results?.FirstOrDefault(result => string.Equals(result.Name, canonicalName, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    return CityGeocodingOutcome.GeocodingNotFound();
                }

                var record = new GeocodingCacheRecord
                {
                    NormalizedCityName = normalizedCacheKey,
                    DisplayName = canonicalName,
                    Country = match.Country,
                    Latitude = match.Latitude,
                    Longitude = match.Longitude,
                    Population = match.Population,
                    RetrievedUtc = _timeProvider.GetUtcNow().UtcDateTime
                };

                await _cacheRepository.UpsertAsync(record, cancellationToken).ConfigureAwait(false);
                return CityGeocodingOutcome.Success(record);
            }
            catch (ApiException)
            {
                return CityGeocodingOutcome.ServiceUnavailable();
            }
        }

        private string? FindCanonicalCityName(string trimmedCity)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            return _cityService.GetCityNames().FirstOrDefault(name => comparer.Equals(name.Trim(), trimmedCity))
                   ?? _usaCityService.GetCityNames().FirstOrDefault(name => comparer.Equals(name.Trim(), trimmedCity));
        }

        private static string NormalizeCacheKey(string canonicalName)
            => canonicalName.Trim().ToUpperInvariant();
    }
}
