using Demo.Cities;
using Microsoft.Extensions.Logging;
using Moq;
using OpenMeteo.Api.Client;
using STI.City.Core.Models;
using STI.City.Core.Repositories;
using STI.City.Core.Services;

namespace STI.City.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CityGeocodingService"/> following the company
/// unit-test template: strict mocks for every injected collaborator, a single
/// constructed target, MethodUnderTest_Condition_ExpectedResult naming, and
/// explicit interaction verification (including forbidden calls).
/// </summary>
public sealed class CityGeocodingServiceTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ICityService> _cityService = new(MockBehavior.Strict);
    private readonly Mock<IOpenMeteoClient> _openMeteoClient = new(MockBehavior.Strict);
    private readonly Mock<IGeocodingCacheRepository> _cacheRepository = new(MockBehavior.Strict);
    private readonly Mock<TimeProvider> _timeProvider = new(MockBehavior.Strict);
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

    [Fact]
    public async Task GetGeocodingAsync_BlankCityName_ReturnsCityNotFoundWithoutAnyLookup()
    {
        // Purpose: a blank route value short-circuits before city, cache, or upstream access.
        // arrange
        // act
        var actual = await _target.GetGeocodingAsync("   ", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.CityNotFound, actual.Status);
        Assert.Null(actual.Record);
        _cityService.Verify(c => c.GetCityNames(), Times.Never);
        AssertNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_UnrecognizedCity_ReturnsCityNotFoundWithoutCacheOrUpstream()
    {
        // Purpose: an unknown city is rejected after city lookup but before cache or upstream calls.
        // arrange
        _cityService.Setup(c => c.GetCityNames()).Returns(new[] { "London", "Tokyo" });

        // act
        var actual = await _target.GetGeocodingAsync("Atlantis", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.CityNotFound, actual.Status);
        _cityService.Verify(c => c.GetCityNames(), Times.Once);
        _cacheRepository.Verify(
            r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _openMeteoClient.Verify(
            c => c.SearchLocationsAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        AssertNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_CacheHit_ReturnsCachedRecordWithoutUpstream()
    {
        // Purpose: a cache hit returns immediately and never calls Open-Meteo.
        // arrange
        var cached = Record("NEW YORK", "New York", population: 8_804_190);
        _cityService.Setup(c => c.GetCityNames()).Returns(new[] { "New York" });
        _cacheRepository
            .Setup(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        // act
        var actual = await _target.GetGeocodingAsync("New York", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.Success, actual.Status);
        Assert.Same(cached, actual.Record);
        _cityService.Verify(c => c.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()), Times.Once);
        _openMeteoClient.Verify(
            c => c.SearchLocationsAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        AssertNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_MixedCaseAndWhitespaceInput_UsesCanonicalNameAndNormalizedKey()
    {
        // Purpose: matching is trimmed/case-insensitive; the canonical spelling drives the key and query.
        // arrange
        _cityService.Setup(c => c.GetCityNames()).Returns(new[] { "New York" });
        _cacheRepository
            .Setup(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseWith(Location("New York", population: 1)));
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(FixedNow);
        _cacheRepository
            .Setup(r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // act
        var actual = await _target.GetGeocodingAsync("  nEw yOrk  ", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.Success, actual.Status);
        Assert.Equal("New York", actual.Record!.DisplayName);
        Assert.Equal("NEW YORK", actual.Record.NormalizedCityName);
        _cityService.Verify(c => c.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()), Times.Once);
        _openMeteoClient.Verify(
            c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _timeProvider.Verify(t => t.GetUtcNow(), Times.Once);
        _cacheRepository.Verify(
            r => r.UpsertAsync(
                It.Is<GeocodingCacheRecord>(x => x.NormalizedCityName == "NEW YORK" && x.DisplayName == "New York"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        AssertNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_MultipleUpstreamResults_SelectsFirstExactNameMatch()
    {
        // Purpose: selection is deterministic — the first exact OrdinalIgnoreCase name match wins.
        // arrange
        _cityService.Setup(c => c.GetCityNames()).Returns(new[] { "New York" });
        _cacheRepository
            .Setup(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseWith(
                Location("Newer York", population: 99),
                Location("new york", population: 11),
                Location("New York", population: 22)));
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(FixedNow);
        _cacheRepository
            .Setup(r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // act
        var actual = await _target.GetGeocodingAsync("New York", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.Success, actual.Status);
        Assert.Equal(11, actual.Record!.Population);
        _cityService.Verify(c => c.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()), Times.Once);
        _openMeteoClient.Verify(
            c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _timeProvider.Verify(t => t.GetUtcNow(), Times.Once);
        _cacheRepository.Verify(
            r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()), Times.Once);
        AssertNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_NoExactUpstreamMatch_ReturnsGeocodingNotFoundWithoutPersisting()
    {
        // Purpose: when no upstream name matches exactly the result is treated as missing.
        // arrange
        _cityService.Setup(c => c.GetCityNames()).Returns(new[] { "New York" });
        _cacheRepository
            .Setup(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseWith(Location("Newark", population: 1)));

        // act
        var actual = await _target.GetGeocodingAsync("New York", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.GeocodingNotFound, actual.Status);
        _cacheRepository.Verify(
            r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _cityService.Verify(c => c.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()), Times.Once);
        _openMeteoClient.Verify(
            c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        AssertNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_EmptyUpstreamResults_ReturnsGeocodingNotFound()
    {
        // Purpose: an empty successful upstream response is a not-found, not a service failure.
        // arrange
        _cityService.Setup(c => c.GetCityNames()).Returns(new[] { "New York" });
        _cacheRepository
            .Setup(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseWith());

        // act
        var actual = await _target.GetGeocodingAsync("New York", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.GeocodingNotFound, actual.Status);
        _cacheRepository.Verify(
            r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _cityService.Verify(c => c.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()), Times.Once);
        _openMeteoClient.Verify(
            c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        AssertNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_NullUpstreamPopulation_PersistsRecordWithNullPopulation()
    {
        // Purpose: a missing upstream population is mapped and persisted as null.
        // arrange
        _cityService.Setup(c => c.GetCityNames()).Returns(new[] { "New York" });
        _cacheRepository
            .Setup(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseWith(Location("New York", population: null)));
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(FixedNow);
        _cacheRepository
            .Setup(r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // act
        var actual = await _target.GetGeocodingAsync("New York", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.Success, actual.Status);
        Assert.Null(actual.Record!.Population);
        Assert.Equal(FixedNow, actual.Record.RetrievedAtUtc);
        _cityService.Verify(c => c.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()), Times.Once);
        _openMeteoClient.Verify(
            c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _timeProvider.Verify(t => t.GetUtcNow(), Times.Once);
        _cacheRepository.Verify(
            r => r.UpsertAsync(
                It.Is<GeocodingCacheRecord>(x => x.Population == null), It.IsAny<CancellationToken>()),
            Times.Once);
        AssertNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_CacheMiss_PropagatesCancellationTokenToCollaborators()
    {
        // Purpose: the request cancellation token flows to the cache and upstream calls.
        // arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        _cityService.Setup(c => c.GetCityNames()).Returns(new[] { "New York" });
        _cacheRepository
            .Setup(r => r.GetAsync("NEW YORK", token))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync("New York", It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<Format?>(), token))
            .ReturnsAsync(ResponseWith(Location("New York", population: 5)));
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(FixedNow);
        _cacheRepository
            .Setup(r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), token))
            .Returns(Task.CompletedTask);

        // act
        var actual = await _target.GetGeocodingAsync("New York", token);

        // assert
        Assert.Equal(CityGeocodingStatus.Success, actual.Status);
        _cityService.Verify(c => c.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("NEW YORK", token), Times.Once);
        _openMeteoClient.Verify(
            c => c.SearchLocationsAsync("New York", It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<Format?>(), token),
            Times.Once);
        _timeProvider.Verify(t => t.GetUtcNow(), Times.Once);
        _cacheRepository.Verify(r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), token), Times.Once);
        AssertNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_UpstreamApiException_ReturnsServiceUnavailableAndLogsWarning()
    {
        // Purpose: an unsuccessful upstream response maps to ServiceUnavailable and is logged as a warning.
        // arrange
        SetupCacheMissForNewYork();
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("boom", 503, "body", null!, null!));

        // act
        var actual = await _target.GetGeocodingAsync("New York", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.ServiceUnavailable, actual.Status);
        VerifyWarningLogged(Times.Once());
        VerifyCacheMissUpstreamCalledOnceNoPersist();
        AssertNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_UpstreamTransportFailure_ReturnsServiceUnavailableAndLogsWarning()
    {
        // Purpose: a transport failure maps to ServiceUnavailable and is logged as a warning.
        // arrange
        SetupCacheMissForNewYork();
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network down"));

        // act
        var actual = await _target.GetGeocodingAsync("New York", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.ServiceUnavailable, actual.Status);
        VerifyWarningLogged(Times.Once());
        VerifyCacheMissUpstreamCalledOnceNoPersist();
        AssertNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_UpstreamTimeout_ReturnsServiceUnavailableAndLogsWarning()
    {
        // Purpose: an upstream timeout (no caller cancellation) maps to ServiceUnavailable with a warning.
        // arrange
        SetupCacheMissForNewYork();
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("timeout"));

        // act
        var actual = await _target.GetGeocodingAsync("New York", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.ServiceUnavailable, actual.Status);
        VerifyWarningLogged(Times.Once());
        VerifyCacheMissUpstreamCalledOnceNoPersist();
        AssertNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_CallerCancellation_PropagatesOperationCanceled()
    {
        // Purpose: cancellation requested by the caller propagates and is not mapped to ServiceUnavailable.
        // arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var token = cts.Token;
        _cityService.Setup(c => c.GetCityNames()).Returns(new[] { "New York" });
        _cacheRepository
            .Setup(r => r.GetAsync("NEW YORK", token))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync("New York", It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<Format?>(), token))
            .ThrowsAsync(new OperationCanceledException(token));

        // act
        var actual = await Assert.ThrowsAsync<OperationCanceledException>(
            () => _target.GetGeocodingAsync("New York", token));

        // assert
        Assert.NotNull(actual);
        VerifyWarningLogged(Times.Never());
        _cacheRepository.Verify(
            r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _cityService.Verify(c => c.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("NEW YORK", token), Times.Once);
        _openMeteoClient.Verify(
            c => c.SearchLocationsAsync("New York", It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<Format?>(), token),
            Times.Once);
        AssertNoOtherCalls();
    }

    [Fact]
    public async Task GetGeocodingAsync_PersistenceFailure_PropagatesException()
    {
        // Purpose: a repository (SQLite) failure during upsert propagates so the pipeline can return 500.
        // arrange
        _cityService.Setup(c => c.GetCityNames()).Returns(new[] { "New York" });
        _cacheRepository
            .Setup(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseWith(Location("New York", population: 5)));
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(FixedNow);
        _cacheRepository
            .Setup(r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sqlite failure"));

        // act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _target.GetGeocodingAsync("New York", CancellationToken.None));

        // assert
        Assert.Equal("sqlite failure", actual.Message);
        _cityService.Verify(c => c.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()), Times.Once);
        _openMeteoClient.Verify(
            c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _timeProvider.Verify(t => t.GetUtcNow(), Times.Once);
        _cacheRepository.Verify(
            r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()), Times.Once);
        AssertNoOtherCalls();
    }

    private void SetupCacheMissForNewYork()
    {
        _cityService.Setup(c => c.GetCityNames()).Returns(new[] { "New York" });
        _cacheRepository
            .Setup(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
    }

    private void VerifyCacheMissUpstreamCalledOnceNoPersist()
    {
        _cityService.Verify(c => c.GetCityNames(), Times.Once);
        _cacheRepository.Verify(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()), Times.Once);
        _openMeteoClient.Verify(
            c => c.SearchLocationsAsync(
                "New York", It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _cacheRepository.Verify(
            r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void VerifyWarningLogged(Times times) =>
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

    private void AssertNoOtherCalls()
    {
        _cityService.VerifyNoOtherCalls();
        _openMeteoClient.VerifyNoOtherCalls();
        _cacheRepository.VerifyNoOtherCalls();
        _timeProvider.VerifyNoOtherCalls();
    }

    private static GeocodingCacheRecord Record(string key, string display, long? population) =>
        new(key, display, "United States", 40.7128, -74.006, population, FixedNow);

    private static LocationResult Location(string name, long? population) => new()
    {
        Name = name,
        Country = "United States",
        Latitude = 40.7128,
        Longitude = -74.006,
        Population = population is null ? null : (int)population.Value,
    };

    private static GeocodingResponse ResponseWith(params LocationResult[] results) =>
        new() { Results = results.ToList() };
}
