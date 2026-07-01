using WeatherApp.Core.Domain;

namespace WeatherApp.Core.Geocoding;

public interface IGeocoder
{
    Task<IReadOnlyList<LocationCandidate>> Search(string query, CancellationToken ct);
}
