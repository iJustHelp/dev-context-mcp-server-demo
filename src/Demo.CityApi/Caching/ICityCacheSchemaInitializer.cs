namespace Demo.CityApi.Caching;

public interface ICityCacheSchemaInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
