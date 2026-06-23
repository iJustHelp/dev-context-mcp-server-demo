using System.Collections.Concurrent;
using STI.City.Core.Abstractions;
using STI.City.Core.Exceptions;
using STI.City.Core.Models;

namespace STI.City.Tests.Api;

/// <summary>Configurable in-process geocoding provider for endpoint tests.</summary>
public sealed class FakeGeocodingProvider : IGeocodingProvider
{
    public int CallCount { get; private set; }
    public Func<string, CityGeocoding?> OnFind { get; set; } = _ => null;
    public bool ThrowUnavailable { get; set; }

    public Task<CityGeocoding?> FindAsync(string cityName, CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (ThrowUnavailable)
        {
            throw new GeocodingUnavailableException("upstream unavailable", new InvalidOperationException());
        }

        return Task.FromResult(OnFind(cityName));
    }
}

/// <summary>In-memory cache so endpoint tests do not touch SQLite.</summary>
public sealed class InMemoryGeocodingCacheRepository : IGeocodingCacheRepository
{
    private readonly ConcurrentDictionary<string, CityGeocoding> _store = new();

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<CityGeocoding?> GetAsync(string normalizedName, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(normalizedName, out var record) ? record : null);

    public Task UpsertAsync(CityGeocoding record, CancellationToken cancellationToken = default)
    {
        _store[record.NormalizedName] = record;
        return Task.CompletedTask;
    }
}
