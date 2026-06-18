using Microsoft.Extensions.DependencyInjection;
using STI.City.Core.DependencyInjection;
using STI.City.Core.Services;

namespace STI.City.Tests.Services;

/// <summary>
/// Focused registration tests for <see cref="CoreServiceCollectionExtensions"/>.
/// These assert dependency-injection lifetimes and do not exercise the service.
/// </summary>
public sealed class CoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCityCore_RegistersGeocodingService_AsScoped()
    {
        // Purpose: the application geocoding service must be scoped per request.
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddCityCore();

        // assert
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ICityGeocodingService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(CityGeocodingService), descriptor.ImplementationType);
    }

    [Fact]
    public void AddCityCore_RegistersTimeProvider_AsSingleton()
    {
        // Purpose: a single system TimeProvider is shared across the application.
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddCityCore();

        // assert
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(TimeProvider));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }
}
