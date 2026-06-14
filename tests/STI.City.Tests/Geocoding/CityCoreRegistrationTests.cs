using Microsoft.Extensions.DependencyInjection;
using STI.City.Core;
using STI.City.Core.Geocoding;

namespace STI.City.Tests.Geocoding;

public sealed class CityCoreRegistrationTests
{
    // Purpose: registers the geocoding service with a scoped lifetime
    [Fact]
    public void AddCityCore_Always_RegistersGeocodingServiceAsScoped()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        var actual = services.AddCityCore();

        // assert
        var descriptor = Assert.Single(
            actual,
            service => service.ServiceType ==
                typeof(ICityGeocodingService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(
            typeof(CityGeocodingService),
            descriptor.ImplementationType);
    }
}
