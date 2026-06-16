using Formula.SimpleRepo;
using Microsoft.Extensions.Configuration;
using STI.City.Core.Geocoding;

namespace STI.City.Data.Geocoding
{
    public sealed class SimpleRepoGeocodingCacheRepository : RepositoryBase<GeocodingCacheEntity, GeocodingCacheEntity>, IGeocodingCacheRepository
    {
        public SimpleRepoGeocodingCacheRepository(IConfiguration configuration)
            : base(configuration)
        {
        }

        public async Task<GeocodingCacheRecord?> GetAsync(string normalizedCityName, CancellationToken cancellationToken = default)
        {
            if (normalizedCityName is null)
            {
                throw new ArgumentNullException(nameof(normalizedCityName));
            }

            var entity = await GetAsync((object)normalizedCityName, null, null).ConfigureAwait(false);
            return entity is null ? null : entity.ToRecord();
        }

        public async Task UpsertAsync(GeocodingCacheRecord record, CancellationToken cancellationToken = default)
        {
            if (record is null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            var entity = GeocodingCacheEntity.FromRecord(record);
            await InsertAsync(entity, null, null).ConfigureAwait(false);
        }
    }
}
