namespace STI.City.API.Endpoints
{
    public sealed class CityPopulationResponse
    {
        public string CityName { get; init; } = string.Empty;

        public string Country { get; init; } = string.Empty;

        public long Population { get; init; }
    }
}
