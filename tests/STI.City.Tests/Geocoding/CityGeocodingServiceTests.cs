using Demo.Cities;
using Microsoft.Extensions.Logging;
using Moq;
using OpenMeteo.Api.Client;
using STI.City.Core.Geocoding;

namespace STI.City.Tests.Geocoding;

public sealed class CityGeocodingServiceTests
{
    private static readonly DateTimeOffset RetrievedAtUtc =
        new(2026, 6, 13, 1, 15, 0, TimeSpan.Zero);

    private readonly Mock<ICityService> _cityService = new();
    private readonly Mock<IOpenMeteoClient> _openMeteoClient = new();
    private readonly Mock<IGeocodingCacheRepository> _cacheRepository = new();
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly Mock<ILogger<CityGeocodingService>> _logger = new();
    private readonly CityGeocodingService _target;

    public CityGeocodingServiceTests()
    {
        _target = new CityGeocodingService(
            _cityService.Object,
            _openMeteoClient.Object,
            _cacheRepository.Object,
            _timeProvider.Object,
            _logger.Object);
    }

    // Purpose: returns the cached record without calling Open-Meteo
    [Fact]
    public async Task GetAsync_CacheHit_ReturnsCachedRecord()
    {
        // arrange
        var expected = CreateRecord(population: 2_700_000);
        SetupCities();
        _cacheRepository
            .Setup(repository => repository.GetAsync(
                "CHICAGO",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // act
        var actual = await _target.GetAsync("  cHiCaGo  ");

        // assert
        Assert.Equal(CityGeocodingOutcome.Success, actual.Outcome);
        Assert.Equal(expected, actual.Record);
        _cityService.Verify(
            service => service.GetCityNames(),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync(
                "CHICAGO",
                CancellationToken.None),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(
                It.IsAny<GeocodingCacheRecord>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<Format?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _timeProvider.Verify(
            provider => provider.GetUtcNow(),
            Times.Never);
        VerifyNoOtherCalls();
    }

    // Purpose: rejects a blank city without calling dependencies
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetAsync_BlankCity_ReturnsCityNotFound(string cityName)
    {
        // arrange

        // act
        var actual = await _target.GetAsync(cityName);

        // assert
        Assert.Equal(CityGeocodingOutcome.CityNotFound, actual.Outcome);
        Assert.Null(actual.Record);
        VerifyDependenciesWereNotCalled();
        VerifyNoOtherCalls();
    }

    // Purpose: rejects an unknown city before cache and upstream lookups
    [Fact]
    public async Task GetAsync_UnknownCity_ReturnsCityNotFound()
    {
        // arrange
        SetupCities();

        // act
        var actual = await _target.GetAsync("Atlantis");

        // assert
        Assert.Equal(CityGeocodingOutcome.CityNotFound, actual.Outcome);
        Assert.Null(actual.Record);
        _cityService.Verify(
            service => service.GetCityNames(),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(
                It.IsAny<GeocodingCacheRecord>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<Format?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _timeProvider.Verify(
            provider => provider.GetUtcNow(),
            Times.Never);
        VerifyNoOtherCalls();
    }

    // Purpose: caches and returns the first exact upstream match
    [Fact]
    public async Task GetAsync_CacheMissWithExactMatch_ReturnsAndCachesRecord()
    {
        // arrange
        var response = CreateResponse(
            CreateLocation("Chicago Heights", 41, -87, 27_000),
            CreateLocation("CHICAGO", 41.85003, -87.65005, 2_600_000),
            CreateLocation("Chicago", 42, -88, 2_700_000));
        var expected = CreateRecord(population: 2_600_000);
        using var cancellation = new CancellationTokenSource();
        SetupCacheMiss(response, cancellation.Token);

        // act
        var actual = await _target.GetAsync("chicago", cancellation.Token);

        // assert
        Assert.Equal(CityGeocodingOutcome.Success, actual.Outcome);
        Assert.Equal(expected, actual.Record);
        VerifySuccessfulLookup(expected, cancellation.Token);
        VerifyNoOtherCalls();
    }

    // Purpose: preserves a missing population in the cached result
    [Fact]
    public async Task GetAsync_ExactMatchWithoutPopulation_ReturnsNullPopulation()
    {
        // arrange
        var response = CreateResponse(
            CreateLocation("Chicago", 41.85003, -87.65005, null));
        var expected = CreateRecord(population: null);
        SetupCacheMiss(response, CancellationToken.None);

        // act
        var actual = await _target.GetAsync("Chicago");

        // assert
        Assert.Equal(CityGeocodingOutcome.Success, actual.Outcome);
        Assert.Equal(expected, actual.Record);
        Assert.Null(actual.Record!.Population);
        VerifySuccessfulLookup(expected, CancellationToken.None);
        VerifyNoOtherCalls();
    }

    // Purpose: returns not found when Open-Meteo has no exact city match
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAsync_NoExactUpstreamMatch_ReturnsGeocodingNotFound(
        bool includeNonmatchingResult)
    {
        // arrange
        var response = includeNonmatchingResult
            ? CreateResponse(
                CreateLocation("Chicago Heights", 41, -87, 27_000))
            : CreateResponse();
        SetupCacheMiss(response, CancellationToken.None);

        // act
        var actual = await _target.GetAsync("Chicago");

        // assert
        Assert.Equal(
            CityGeocodingOutcome.GeocodingNotFound,
            actual.Outcome);
        Assert.Null(actual.Record);
        VerifyLookupWithoutPersistence(CancellationToken.None);
        VerifyNoOtherCalls();
    }

    // Purpose: maps an Open-Meteo API failure to service unavailable
    [Fact]
    public async Task GetAsync_ApiFailure_ReturnsServiceUnavailable()
    {
        // arrange
        var exception = new ApiException(
            "Unavailable",
            503,
            string.Empty,
            new Dictionary<string, IEnumerable<string>>(),
            new InvalidOperationException());
        SetupUpstreamException(exception, CancellationToken.None);

        // act
        var actual = await _target.GetAsync("Chicago");

        // assert
        Assert.Equal(
            CityGeocodingOutcome.ServiceUnavailable,
            actual.Outcome);
        VerifyFailedLookup(CancellationToken.None);
        VerifyWarning(exception, "Open-Meteo returned status");
        VerifyNoOtherCalls();
    }

    // Purpose: maps an Open-Meteo transport failure to service unavailable
    [Fact]
    public async Task GetAsync_TransportFailure_ReturnsServiceUnavailable()
    {
        // arrange
        var exception = new HttpRequestException("Network failed.");
        SetupUpstreamException(exception, CancellationToken.None);

        // act
        var actual = await _target.GetAsync("Chicago");

        // assert
        Assert.Equal(
            CityGeocodingOutcome.ServiceUnavailable,
            actual.Outcome);
        VerifyFailedLookup(CancellationToken.None);
        VerifyWarning(exception, "Open-Meteo transport failed");
        VerifyNoOtherCalls();
    }

    // Purpose: maps an uncancelled Open-Meteo timeout to service unavailable
    [Fact]
    public async Task GetAsync_UpstreamTimeout_ReturnsServiceUnavailable()
    {
        // arrange
        var exception = new TaskCanceledException("Timed out.");
        SetupUpstreamException(exception, CancellationToken.None);

        // act
        var actual = await _target.GetAsync("Chicago");

        // assert
        Assert.Equal(
            CityGeocodingOutcome.ServiceUnavailable,
            actual.Outcome);
        VerifyFailedLookup(CancellationToken.None);
        VerifyWarning(exception, "Open-Meteo timed out");
        VerifyNoOtherCalls();
    }

    // Purpose: propagates cancellation requested by the caller
    [Fact]
    public async Task GetAsync_RequestCancelled_PropagatesCancellation()
    {
        // arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var expected = new OperationCanceledException(cancellation.Token);
        SetupUpstreamException(expected, cancellation.Token);

        // act
        var actual = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _target.GetAsync("Chicago", cancellation.Token));

        // assert
        Assert.Same(expected, actual);
        VerifyFailedLookup(cancellation.Token);
        VerifyNoOtherCalls();
    }

    // Purpose: propagates a cache persistence exception
    [Fact]
    public async Task GetAsync_CachePersistenceFails_PropagatesException()
    {
        // arrange
        var expected = new InvalidOperationException(
            "Database unavailable.");
        var response = CreateResponse(
            CreateLocation("Chicago", 41.85003, -87.65005, 2_600_000));
        var expectedRecord = CreateRecord(population: 2_600_000);
        SetupCities();
        _cacheRepository
            .Setup(repository => repository.GetAsync(
                "CHICAGO",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(client => client.SearchLocationsAsync(
                "Chicago",
                null,
                "en",
                Format.Json,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        _timeProvider
            .Setup(provider => provider.GetUtcNow())
            .Returns(RetrievedAtUtc);
        _cacheRepository
            .Setup(repository => repository.UpsertAsync(
                It.IsAny<GeocodingCacheRecord>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        // act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _target.GetAsync("Chicago"));

        // assert
        Assert.Same(expected, actual);
        VerifySuccessfulLookup(expectedRecord, CancellationToken.None);
        VerifyNoOtherCalls();
    }

    // Purpose: propagates a cache lookup exception without calling upstream
    [Fact]
    public async Task GetAsync_CacheLookupFails_PropagatesException()
    {
        // arrange
        var expected = new InvalidOperationException(
            "Database unavailable.");
        SetupCities();
        _cacheRepository
            .Setup(repository => repository.GetAsync(
                "CHICAGO",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        // act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _target.GetAsync("Chicago"));

        // assert
        Assert.Same(expected, actual);
        _cityService.Verify(
            service => service.GetCityNames(),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync(
                "CHICAGO",
                CancellationToken.None),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(
                It.IsAny<GeocodingCacheRecord>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<Format?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _timeProvider.Verify(
            provider => provider.GetUtcNow(),
            Times.Never);
        VerifyNoOtherCalls();
    }

    private void SetupCities()
    {
        _cityService
            .Setup(service => service.GetCityNames())
            .Returns(["Chicago", "London", "Tokyo"]);
    }

    private void SetupCacheMiss(
        GeocodingResponse response,
        CancellationToken cancellationToken)
    {
        SetupCities();
        _cacheRepository
            .Setup(repository => repository.GetAsync(
                "CHICAGO",
                cancellationToken))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(client => client.SearchLocationsAsync(
                "Chicago",
                null,
                "en",
                Format.Json,
                cancellationToken))
            .ReturnsAsync(response);
        _timeProvider
            .Setup(provider => provider.GetUtcNow())
            .Returns(RetrievedAtUtc);
        _cacheRepository
            .Setup(repository => repository.UpsertAsync(
                It.IsAny<GeocodingCacheRecord>(),
                cancellationToken))
            .Returns(Task.CompletedTask);
    }

    private void SetupUpstreamException(
        Exception exception,
        CancellationToken cancellationToken)
    {
        SetupCities();
        _cacheRepository
            .Setup(repository => repository.GetAsync(
                "CHICAGO",
                cancellationToken))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(client => client.SearchLocationsAsync(
                "Chicago",
                null,
                "en",
                Format.Json,
                cancellationToken))
            .ThrowsAsync(exception);
    }

    private void VerifySuccessfulLookup(
        GeocodingCacheRecord expected,
        CancellationToken cancellationToken)
    {
        _cityService.Verify(
            service => service.GetCityNames(),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync(
                "CHICAGO",
                cancellationToken),
            Times.Once);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(
                "Chicago",
                null,
                "en",
                Format.Json,
                cancellationToken),
            Times.Once);
        _timeProvider.Verify(
            provider => provider.GetUtcNow(),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(
                It.Is<GeocodingCacheRecord>(record => record == expected),
                cancellationToken),
            Times.Once);
    }

    private void VerifyLookupWithoutPersistence(
        CancellationToken cancellationToken)
    {
        _cityService.Verify(
            service => service.GetCityNames(),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync(
                "CHICAGO",
                cancellationToken),
            Times.Once);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(
                "Chicago",
                null,
                "en",
                Format.Json,
                cancellationToken),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(
                It.IsAny<GeocodingCacheRecord>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _timeProvider.Verify(
            provider => provider.GetUtcNow(),
            Times.Never);
    }

    private void VerifyFailedLookup(CancellationToken cancellationToken)
    {
        VerifyLookupWithoutPersistence(cancellationToken);
    }

    private void VerifyDependenciesWereNotCalled()
    {
        _cityService.Verify(
            service => service.GetCityNames(),
            Times.Never);
        _cacheRepository.Verify(
            repository => repository.GetAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(
                It.IsAny<GeocodingCacheRecord>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<Format?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _timeProvider.Verify(
            provider => provider.GetUtcNow(),
            Times.Never);
    }

    private void VerifyWarning(
        Exception expectedException,
        string expectedMessage)
    {
        _logger.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains(
                        expectedMessage,
                        StringComparison.Ordinal)),
                It.Is<Exception?>(exception =>
                    ReferenceEquals(exception, expectedException)),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private void VerifyNoOtherCalls()
    {
        _cityService.VerifyNoOtherCalls();
        _openMeteoClient.VerifyNoOtherCalls();
        _cacheRepository.VerifyNoOtherCalls();
        _timeProvider.VerifyNoOtherCalls();
        _logger.VerifyNoOtherCalls();
    }

    private static GeocodingCacheRecord CreateRecord(long? population) =>
        new(
            "CHICAGO",
            "Chicago",
            "United States",
            41.85003,
            -87.65005,
            population,
            RetrievedAtUtc);

    private static GeocodingResponse CreateResponse(
        params LocationResult[] results) =>
        new()
        {
            Results = results
        };

    private static LocationResult CreateLocation(
        string name,
        double latitude,
        double longitude,
        int? population) =>
        new()
        {
            Name = name,
            Country = "United States",
            Latitude = latitude,
            Longitude = longitude,
            Population = population
        };
}
