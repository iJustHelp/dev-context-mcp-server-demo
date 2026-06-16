using Demo.Cities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenMeteo.Api.Client;
using STI.City.Core.DependencyInjection;
using STI.City.Core.Models;
using STI.City.Core.Repositories;
using STI.City.Core.Services;

namespace STI.City.Tests.Services;

/// <summary>
/// Stage 3 coverage: city resolution, invariant normalization, cache-aside
/// behavior, deterministic upstream selection, and failure mapping.
/// </summary>
public sealed class CityGeocodingServiceTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 6, 16, 9, 0, 0, TimeSpan.Zero);

    private readonly Mock<ICityService> _cityService = new(MockBehavior.Strict);
    private readonly Mock<IOpenMeteoClient> _openMeteoClient = new(MockBehavior.Strict);
    private readonly Mock<IGeocodingCacheRepository> _cacheRepository = new(MockBehavior.Strict);
    private readonly TimeProvider _timeProvider;

    public CityGeocodingServiceTests()
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(c => c.GetUtcNow()).Returns(FixedNow);
        _timeProvider = clock.Object;
    }

    private CityGeocodingService CreateService() => new(
        _cityService.Object,
        _openMeteoClient.Object,
        _cacheRepository.Object,
        _timeProvider,
        NullLogger<CityGeocodingService>.Instance);

    private void HaveCities(params string[] names) =>
        _cityService.Setup(c => c.GetCityNames()).Returns(names);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyOrWhitespace_ReturnsCityNotFound_WithoutAnyLookup(string input)
    {
        var result = await CreateService().GetCityGeocodingAsync(input);

        Assert.Equal(CityGeocodingStatus.CityNotFound, result.Status);
        _cityService.Verify(c => c.GetCityNames(), Times.Never);
        _cacheRepository.VerifyNoOtherCalls();
        _openMeteoClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnknownCity_ReturnsCityNotFound_AndSkipsCacheAndUpstream()
    {
        HaveCities("London", "New York");

        var result = await CreateService().GetCityGeocodingAsync("Atlantis");

        Assert.Equal(CityGeocodingStatus.CityNotFound, result.Status);
        _cacheRepository.VerifyNoOtherCalls();
        _openMeteoClient.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("new york")]
    [InlineData("NEW YORK")]
    [InlineData("  New York  ")]
    public async Task MixedCaseOrPaddedInput_ResolvesToCanonicalSpelling_AndNormalizedKey(string input)
    {
        HaveCities("London", "New York");
        _cacheRepository
            .Setup(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync("New York", null, "en", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseWith(Location("New York", "United States", 40.7128, -74.006, 8_804_190)));
        _cacheRepository
            .Setup(r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().GetCityGeocodingAsync(input);

        Assert.Equal(CityGeocodingStatus.Success, result.Status);
        Assert.Equal("New York", result.Record!.DisplayName);
        Assert.Equal("NEW YORK", result.Record.NormalizedCityName);
        _openMeteoClient.Verify(
            c => c.SearchLocationsAsync("New York", null, "en", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CacheHit_ReturnsRecord_AndNeverCallsUpstream()
    {
        HaveCities("New York");
        var cached = Record("NEW YORK", "New York", "United States", 40.7128, -74.006, 8_804_190);
        _cacheRepository
            .Setup(r => r.GetAsync("NEW YORK", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var result = await CreateService().GetCityGeocodingAsync("New York");

        Assert.Equal(CityGeocodingStatus.Success, result.Status);
        Assert.Same(cached, result.Record);
        _openMeteoClient.VerifyNoOtherCalls();
        _cacheRepository.Verify(
            r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CacheMiss_CallsUpstreamOnce_PersistsSelectedResult_AndStampsTime()
    {
        HaveCities("New York");
        var token = new CancellationTokenSource().Token;
        _cacheRepository
            .Setup(r => r.GetAsync("NEW YORK", token))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync("New York", null, "en", null, token))
            .ReturnsAsync(ResponseWith(
                Location("Newark", "United States", 1, 1, 1),
                Location("New York", "United States", 40.7128, -74.006, 8_804_190)));
        GeocodingCacheRecord? persisted = null;
        _cacheRepository
            .Setup(r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), token))
            .Callback<GeocodingCacheRecord, CancellationToken>((record, _) => persisted = record)
            .Returns(Task.CompletedTask);

        var result = await CreateService().GetCityGeocodingAsync("New York", token);

        Assert.Equal(CityGeocodingStatus.Success, result.Status);
        // First exact-name match is selected, not the earlier non-matching result.
        Assert.Equal(40.7128, result.Record!.Latitude);
        Assert.Equal(8_804_190, result.Record.Population);
        Assert.Equal(FixedNow, result.Record.RetrievedAtUtc);
        Assert.NotNull(persisted);
        Assert.Equal("NEW YORK", persisted!.NormalizedCityName);
        _openMeteoClient.Verify(
            c => c.SearchLocationsAsync("New York", null, "en", null, token), Times.Once);
        _cacheRepository.Verify(r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), token), Times.Once);
    }

    [Fact]
    public async Task MissingPopulationFromUpstream_StillSucceeds_WithNullPopulation()
    {
        HaveCities("Atlantis");
        _cacheRepository
            .Setup(r => r.GetAsync("ATLANTIS", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync("Atlantis", null, "en", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseWith(Location("Atlantis", "Nowhere", 0, 0, population: null)));
        _cacheRepository
            .Setup(r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().GetCityGeocodingAsync("Atlantis");

        Assert.Equal(CityGeocodingStatus.Success, result.Status);
        Assert.Null(result.Record!.Population);
    }

    [Fact]
    public async Task NoExactNameMatch_ReturnsGeocodingNotFound_AndDoesNotPersist()
    {
        HaveCities("Springfield");
        _cacheRepository
            .Setup(r => r.GetAsync("SPRINGFIELD", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync("Springfield", null, "en", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseWith(Location("Springfields", "United States", 1, 1, 1)));

        var result = await CreateService().GetCityGeocodingAsync("Springfield");

        Assert.Equal(CityGeocodingStatus.GeocodingNotFound, result.Status);
        _cacheRepository.Verify(
            r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EmptyUpstreamResults_ReturnsGeocodingNotFound()
    {
        HaveCities("Nowhere");
        _cacheRepository
            .Setup(r => r.GetAsync("NOWHERE", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync("Nowhere", null, "en", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodingResponse { Results = new List<LocationResult>() });

        var result = await CreateService().GetCityGeocodingAsync("Nowhere");

        Assert.Equal(CityGeocodingStatus.GeocodingNotFound, result.Status);
    }

    [Fact]
    public async Task UpstreamApiException_OnCacheMiss_ReturnsServiceUnavailable()
    {
        HaveCities("Paris");
        _cacheRepository
            .Setup(r => r.GetAsync("PARIS", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync("Paris", null, "en", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("upstream down", 503, "body", null, null));

        var result = await CreateService().GetCityGeocodingAsync("Paris");

        Assert.Equal(CityGeocodingStatus.ServiceUnavailable, result.Status);
    }

    [Fact]
    public async Task UpstreamTransportFailure_ReturnsServiceUnavailable()
    {
        HaveCities("Paris");
        _cacheRepository
            .Setup(r => r.GetAsync("PARIS", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync("Paris", null, "en", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var result = await CreateService().GetCityGeocodingAsync("Paris");

        Assert.Equal(CityGeocodingStatus.ServiceUnavailable, result.Status);
    }

    [Fact]
    public async Task UpstreamTimeout_ReturnsServiceUnavailable()
    {
        HaveCities("Paris");
        _cacheRepository
            .Setup(r => r.GetAsync("PARIS", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync("Paris", null, "en", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("timeout"));

        // The request token is not cancelled, so this is a timeout, not a disconnect.
        var result = await CreateService().GetCityGeocodingAsync("Paris", CancellationToken.None);

        Assert.Equal(CityGeocodingStatus.ServiceUnavailable, result.Status);
    }

    [Fact]
    public async Task ClientCancellation_PropagatesAndIsNotTreatedAsServiceUnavailable()
    {
        HaveCities("Paris");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _cacheRepository
            .Setup(r => r.GetAsync("PARIS", cts.Token))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(c => c.SearchLocationsAsync("Paris", null, "en", null, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService().GetCityGeocodingAsync("Paris", cts.Token));

        _cacheRepository.Verify(
            r => r.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void AddCityCore_RegistersService_AsScoped()
    {
        var services = new ServiceCollection();

        services.AddCityCore();

        var descriptor = Assert.Single(
            services, d => d.ServiceType == typeof(ICityGeocodingService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    private static GeocodingResponse ResponseWith(params LocationResult[] results) =>
        new() { Results = results.ToList() };

    private static LocationResult Location(
        string name, string country, double latitude, double longitude, int? population) =>
        new()
        {
            Name = name,
            Country = country,
            Latitude = latitude,
            Longitude = longitude,
            Population = population,
        };

    private static GeocodingCacheRecord Record(
        string normalized, string display, string country, double lat, double lon, long? population) =>
        new()
        {
            NormalizedCityName = normalized,
            DisplayName = display,
            Country = country,
            Latitude = lat,
            Longitude = lon,
            Population = population,
            RetrievedAtUtc = FixedNow,
        };
}
