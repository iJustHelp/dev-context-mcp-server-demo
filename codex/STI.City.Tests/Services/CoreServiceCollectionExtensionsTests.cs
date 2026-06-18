using Microsoft.Extensions.DependencyInjection;
using STI.City.Core.DependencyInjection;
using STI.City.Core.Services;

namespace STI.City.Tests.Services;

public sealed class CoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCityCore_WhenCalled_RegistersGeocodingServiceAsScoped()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddCityCore();

        // assert
        var descriptor = Assert.Single(services, service =>
            service.ServiceType == typeof(ICityGeocodingService));
        Assert.Equal(typeof(CityGeocodingService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }
}
