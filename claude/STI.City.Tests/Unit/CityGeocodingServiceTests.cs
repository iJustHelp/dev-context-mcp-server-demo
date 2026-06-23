using Demo.Cities;
using Moq;
using STI.City.Core.Abstractions;
using STI.City.Core.Exceptions;
using STI.City.Core.Models;
using STI.City.Core.Services;
using STI.City.Core.Time;
using Xunit;

namespace STI.City.Tests.Unit;

public class CityGeocodingServiceTests
{
    private const string Requested = "paris";
    private const string Canonical = "Paris";
    private static readonly string NormalizedKey = Extensions.ToCityName(Canonical);
    private static readonly DateTimeOffset Now = new(2026, 6, 23, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<ICityCatalog> _catalog = new(MockBehavior.Strict);
    private readonly Mock<IGeocodingCacheRepository> _cache = new(MockBehavior.Strict);
    private readonly Mock<IGeocodingProvider> _provider = new(MockBehavior.Strict);
    private readonly Mock<IClock> _clock = new();

    private CityGeocodingService CreateService()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        return new CityGeocodingService(_catalog.Object, _cache.Object, _provider.Object, _clock.Object);
    }

    private static CityGeocoding SampleResult() => new()
    {
        NormalizedName = Canonical, // provider's placeholder, overwritten by the service
        DisplayName = "Paris",
        Country = "France",
        Latitude = 48.8566,
        Longitude = 2.3522,
        Population = 2_165_000,
        RetrievedAtUtc = default,
    };

    [Fact] // AS-3 / AS-4 / FR-9: cache miss → provider call, persist, return Found
    public async Task GetGeocodingAsync_on_cache_miss_calls_provider_and_persists()
    {
        _catalog.Setup(c => c.ResolveCanonicalName(Requested)).Returns(Canonical);
        _cache.Setup(c => c.GetAsync(NormalizedKey, It.IsAny<CancellationToken>())).ReturnsAsync((CityGeocoding?)null);
        _provider.Setup(p => p.FindAsync(Canonical, It.IsAny<CancellationToken>())).ReturnsAsync(SampleResult());
        _cache.Setup(c => c.UpsertAsync(It.IsAny<CityGeocoding>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.GetGeocodingAsync(Requested);

        Assert.Equal(GeocodingStatus.Found, result.Status);
        Assert.NotNull(result.Record);
        Assert.Equal(NormalizedKey, result.Record!.NormalizedName);
        Assert.Equal(Now, result.Record.RetrievedAtUtc);
        Assert.Equal(48.8566, result.Record.Latitude);

        _cache.Verify(c => c.UpsertAsync(
            It.Is<CityGeocoding>(r => r.NormalizedName == NormalizedKey && r.RetrievedAtUtc == Now),
            It.IsAny<CancellationToken>()), Times.Once);
        _provider.Verify(p => p.FindAsync(Canonical, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact] // AS-5 / FR-8: cache hit → no upstream call
    public async Task GetGeocodingAsync_on_cache_hit_does_not_call_provider()
    {
        var cached = SampleResult() with { NormalizedName = NormalizedKey, RetrievedAtUtc = Now };
        _catalog.Setup(c => c.ResolveCanonicalName(Requested)).Returns(Canonical);
        _cache.Setup(c => c.GetAsync(NormalizedKey, It.IsAny<CancellationToken>())).ReturnsAsync(cached);

        var service = CreateService();
        var result = await service.GetGeocodingAsync(Requested);

        Assert.Equal(GeocodingStatus.Found, result.Status);
        Assert.Same(cached, result.Record);
        _provider.Verify(p => p.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.UpsertAsync(It.IsAny<CityGeocoding>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact] // AS-7 / FR-6: unknown city → 404 and no upstream/cache call
    public async Task GetGeocodingAsync_for_unknown_city_returns_CityNotFound()
    {
        _catalog.Setup(c => c.ResolveCanonicalName("atlantis")).Returns((string?)null);

        var service = CreateService();
        var result = await service.GetGeocodingAsync("atlantis");

        Assert.Equal(GeocodingStatus.CityNotFound, result.Status);
        Assert.Null(result.Record);
        _cache.Verify(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _provider.Verify(p => p.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact] // AS-8 / FR-13: empty upstream result → 404, nothing persisted
    public async Task GetGeocodingAsync_with_no_provider_result_returns_NoGeocodingResult()
    {
        _catalog.Setup(c => c.ResolveCanonicalName(Requested)).Returns(Canonical);
        _cache.Setup(c => c.GetAsync(NormalizedKey, It.IsAny<CancellationToken>())).ReturnsAsync((CityGeocoding?)null);
        _provider.Setup(p => p.FindAsync(Canonical, It.IsAny<CancellationToken>())).ReturnsAsync((CityGeocoding?)null);

        var service = CreateService();
        var result = await service.GetGeocodingAsync(Requested);

        Assert.Equal(GeocodingStatus.NoGeocodingResult, result.Status);
        _cache.Verify(c => c.UpsertAsync(It.IsAny<CityGeocoding>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact] // AS-9 / FR-14: upstream failure with no cache → 502, nothing persisted
    public async Task GetGeocodingAsync_when_provider_fails_returns_UpstreamUnavailable()
    {
        _catalog.Setup(c => c.ResolveCanonicalName(Requested)).Returns(Canonical);
        _cache.Setup(c => c.GetAsync(NormalizedKey, It.IsAny<CancellationToken>())).ReturnsAsync((CityGeocoding?)null);
        _provider.Setup(p => p.FindAsync(Canonical, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GeocodingUnavailableException("down", new InvalidOperationException()));

        var service = CreateService();
        var result = await service.GetGeocodingAsync(Requested);

        Assert.Equal(GeocodingStatus.UpstreamUnavailable, result.Status);
        _cache.Verify(c => c.UpsertAsync(It.IsAny<CityGeocoding>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact] // AS-6 / FR-10 / FR-11: location then population → one provider call, one record
    public async Task GetGeocodingAsync_called_twice_uses_cache_on_second_call()
    {
        CityGeocoding? stored = null;
        _catalog.Setup(c => c.ResolveCanonicalName(Requested)).Returns(Canonical);
        _cache.Setup(c => c.GetAsync(NormalizedKey, It.IsAny<CancellationToken>())).ReturnsAsync(() => stored);
        _provider.Setup(p => p.FindAsync(Canonical, It.IsAny<CancellationToken>())).ReturnsAsync(SampleResult());
        _cache.Setup(c => c.UpsertAsync(It.IsAny<CityGeocoding>(), It.IsAny<CancellationToken>()))
            .Callback<CityGeocoding, CancellationToken>((r, _) => stored = r)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var first = await service.GetGeocodingAsync(Requested);
        var second = await service.GetGeocodingAsync(Requested);

        Assert.Equal(GeocodingStatus.Found, first.Status);
        Assert.Equal(GeocodingStatus.Found, second.Status);
        _provider.Verify(p => p.FindAsync(Canonical, It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.UpsertAsync(It.IsAny<CityGeocoding>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
