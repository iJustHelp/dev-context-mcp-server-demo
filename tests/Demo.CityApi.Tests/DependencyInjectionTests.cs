using Demo.CityApi.Caching;
using Demo.Cities;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.CityApi.Tests;

public sealed class DependencyInjectionTests(CityApiFactory factory)
    : IClassFixture<CityApiFactory>
{
    [Fact]
    public void CityServicesResolve()
    {
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICityService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUsaCityService>());
        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<IGeocodingCacheRepository>());
        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<ICityCacheSchemaInitializer>());
    }
}
