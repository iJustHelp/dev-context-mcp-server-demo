namespace STI.City.API.Endpoints
{
    public sealed class CityLocationResponse
    {
        public string CityName { get; init; } = string.Empty;

        public string Country { get; init; } = string.Empty;

        public double Latitude { get; init; }

        public double Longitude { get; init; }
    }
}
