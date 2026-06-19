using Demo.Cities;
using Microsoft.Extensions.Logging;
using Moq;
using OpenMeteo.Api.Client;
using STI.City.Core.Models;
using STI.City.Core.Repositories;
using STI.City.Core.Services;

namespace STI.City.Tests.Services;

public sealed class CityGeocodingServiceTests
{
    private static readonly DateTimeOffset Now = new(
        year: 2026,
        month: 6,
        day: 18,
        hour: 12,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero);

    private readonly Mock<ICityService> _cityService = new(MockBehavior.Strict);
    private readonly Mock<IUsaCityService> _usaCityService = new(MockBehavior.Strict);
    private readonly Mock<IOpenMeteoClient> _openMeteoClient = new(MockBehavior.Strict);
    private readonly Mock<IGeocodingCacheRepository> _cacheRepository = new(MockBehavior.Strict);
    private readonly Mock<TimeProvider> _timeProvider = new(MockBehavior.Strict);
    private readonly Mock<ILogger<CityGeocodingService>> _logger = new();
    private readonly CityGeocodingService _target;

    public CityGeocodingServiceTests()
    {
        _usaCityService.Setup(s => s.GetCityNames()).Returns(Array.Empty<string>());
        _target = new CityGeocodingService(
            cityService: _cityService.Object,
            usaCityService: _usaCityService.Object,
            openMeteoClient: _openMeteoClient.Object,
            cacheRepository: _cacheRepository.Object,
            timeProvider: _timeProvider.Object,
            logger: _logger.Object);
    }

    [Fact]
    public async Task GetGeocodingAsync_BlankCityName_ReturnsCityNotFound()
    {
        // Purpose: blank input stops before any dependency lookup.
        // arrange
        // act
        var actual = await _target.GetGeocodingAsync("   ", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.CityNotFound, actual.Status);
        _cityService.Verify(s => s.GetCityNames(), Times.Never);
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_UnknownCity_ReturnsCityNotFoundWithoutCacheOrUpstream()
    {
        // Purpose: unsupported cities do not hit SQLite or Open-Meteo.
        // arrange
        _cityService.Setup(s => s.GetCityNames()).Returns(new[] { "London" });

        // act
        var actual = await _target.GetGeocodingAsync("Atlantis", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.CityNotFound, actual.Status);
        _cityService.Verify(s => s.GetCityNames(), Times.Once);
        _usaCityService.Verify(s => s.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _openMeteoClient.Verify(c => c.SearchLocationsAsync(
            It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<Format?>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_CacheHit_ReturnsCachedRecordWithoutUpstream()
    {
        // Purpose: cache hits are returned without an Open-Meteo call.
        // arrange
        var cached = Record("NEW YORK", "New York", 100);
        _cityService.Setup(s => s.GetCityNames()).Returns(new[] { "New York" });
        _cacheRepository.Setup(r => r.GetAsync("NEW YORK", CancellationToken.None)).ReturnsAsync(cached);

        // act
        var actual = await _target.GetGeocodingAsync("new york", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.Success, actual.Status);
        Assert.Same(cached, actual.Record);
        _cityService.Verify(s => s.GetCityNames(), Times.Once);
        _usaCityService.Verify(s => s.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("NEW YORK", CancellationToken.None), Times.Once);
        _openMeteoClient.Verify(c => c.SearchLocationsAsync(
            It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<Format?>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_LowercasePackageName_UsesToCityNameForQueryAndCacheKey()
    {
        // Purpose: package-provided ToCityName canonicalizes display, key, and upstream query.
        // arrange
        _cityService.Setup(s => s.GetCityNames()).Returns(new[] { "new york" });
        _cacheRepository.Setup(r => r.GetAsync("NEW YORK", CancellationToken.None)).ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient.Setup(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, CancellationToken.None))
            .ReturnsAsync(Response(Location("New York", 200)));
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(Now);
        _cacheRepository.Setup(r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // act
        var actual = await _target.GetGeocodingAsync(" new york ", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.Success, actual.Status);
        Assert.Equal("New York", actual.Record!.DisplayName);
        Assert.Equal("NEW YORK", actual.Record.NormalizedCityName);
        _cityService.Verify(s => s.GetCityNames(), Times.Once);
        _usaCityService.Verify(s => s.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("NEW YORK", CancellationToken.None), Times.Once);
        _openMeteoClient.Verify(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, CancellationToken.None), Times.Once);
        _timeProvider.Verify(t => t.GetUtcNow(), Times.Once);
        _cacheRepository.Verify(r => r.UpsertAsync(
            It.Is<GeocodingCacheRecord>(x => x.DisplayName == "New York" && x.NormalizedCityName == "NEW YORK"),
            CancellationToken.None), Times.Once);
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_CityOnlyInUsaList_ResolvesFromMergedCityNames()
    {
        // Purpose: detail lookup uses city names returned by both package services.
        // arrange
        _cityService.Setup(s => s.GetCityNames()).Returns(new[] { "London" });
        _usaCityService.Setup(s => s.GetCityNames()).Returns(new[] { "Seattle" });
        _cacheRepository.Setup(r => r.GetAsync("SEATTLE", CancellationToken.None)).ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient.Setup(c => c.SearchLocationsAsync("Seattle", 10, "en", Format.Json, CancellationToken.None))
            .ReturnsAsync(Response(Location("Seattle", 300)));
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(Now);
        _cacheRepository.Setup(r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // act
        var actual = await _target.GetGeocodingAsync("Seattle", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.Success, actual.Status);
        Assert.Equal("Seattle", actual.Record!.DisplayName);
        _cityService.Verify(s => s.GetCityNames(), Times.Once);
        _usaCityService.Verify(s => s.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("SEATTLE", CancellationToken.None), Times.Once);
        _openMeteoClient.Verify(c => c.SearchLocationsAsync("Seattle", 10, "en", Format.Json, CancellationToken.None), Times.Once);
        _timeProvider.Verify(t => t.GetUtcNow(), Times.Once);
        _cacheRepository.Verify(r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), CancellationToken.None), Times.Once);
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_NoExactUpstreamMatch_ReturnsGeocodingNotFound()
    {
        // Purpose: only exact name matches are selected from upstream results.
        // arrange
        _cityService.Setup(s => s.GetCityNames()).Returns(new[] { "New York" });
        _cacheRepository.Setup(r => r.GetAsync("NEW YORK", CancellationToken.None)).ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient.Setup(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, CancellationToken.None))
            .ReturnsAsync(Response(Location("Newark", 200)));

        // act
        var actual = await _target.GetGeocodingAsync("New York", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.GeocodingNotFound, actual.Status);
        _cityService.Verify(s => s.GetCityNames(), Times.Once);
        _usaCityService.Verify(s => s.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("NEW YORK", CancellationToken.None), Times.Once);
        _openMeteoClient.Verify(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, CancellationToken.None), Times.Once);
        _cacheRepository.Verify(r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_UpstreamApiException_ReturnsServiceUnavailableAndLogsWarning()
    {
        // Purpose: Open-Meteo API failures become a service-unavailable outcome.
        // arrange
        SetupNewYorkCacheMiss();
        _openMeteoClient.Setup(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, CancellationToken.None))
            .ThrowsAsync(new ApiException(
                message: "bad gateway",
                statusCode: 502,
                response: string.Empty,
                headers: new Dictionary<string, IEnumerable<string>>(),
                innerException: null!));

        // act
        var actual = await _target.GetGeocodingAsync("New York", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.ServiceUnavailable, actual.Status);
        VerifyWarningLogged(Times.Once());
        _cityService.Verify(s => s.GetCityNames(), Times.Once);
        _usaCityService.Verify(s => s.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("NEW YORK", CancellationToken.None), Times.Once);
        _openMeteoClient.Verify(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, CancellationToken.None), Times.Once);
        _cacheRepository.Verify(r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_CallerCancellation_PropagatesOperationCanceledException()
    {
        // Purpose: caller cancellation is not translated to service unavailable.
        // arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _cityService.Setup(s => s.GetCityNames()).Returns(new[] { "New York" });
        _cacheRepository.Setup(r => r.GetAsync("NEW YORK", cts.Token)).ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient.Setup(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // act
        var actual = await Assert.ThrowsAsync<OperationCanceledException>(
            () => _target.GetGeocodingAsync("New York", cts.Token));

        // assert
        Assert.NotNull(actual);
        VerifyWarningLogged(Times.Never());
        _cityService.Verify(s => s.GetCityNames(), Times.Once);
        _usaCityService.Verify(s => s.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("NEW YORK", cts.Token), Times.Once);
        _openMeteoClient.Verify(c => c.SearchLocationsAsync("New York", 10, "en", Format.Json, cts.Token), Times.Once);
        VerifyNoOtherCalls();
    }

    private void SetupNewYorkCacheMiss()
    {
        _cityService.Setup(s => s.GetCityNames()).Returns(new[] { "New York" });
        _cacheRepository.Setup(r => r.GetAsync("NEW YORK", CancellationToken.None)).ReturnsAsync((GeocodingCacheRecord?)null);
    }

    private void VerifyWarningLogged(Times times) =>
        _logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    private void VerifyNoOtherCalls()
    {
        _cityService.VerifyNoOtherCalls();
        _usaCityService.VerifyNoOtherCalls();
        _openMeteoClient.VerifyNoOtherCalls();
        _cacheRepository.VerifyNoOtherCalls();
        _timeProvider.VerifyNoOtherCalls();
    }

    private static GeocodingCacheRecord Record(string key, string displayName, long? population) =>
        new(
            NormalizedCityName: key,
            DisplayName: displayName,
            Country: "United States",
            Latitude: 40.7128,
            Longitude: -74.006,
            Population: population,
            RetrievedAtUtc: Now);

    private static LocationResult Location(string name, long? population) => new()
    {
        Name = name,
        Country = "United States",
        Latitude = 40.7128,
        Longitude = -74.006,
        Population = population is null ? null : (int)population.Value,
    };

    private static GeocodingResponse Response(params LocationResult[] results) =>
        new() { Results = results.ToList() };
}
