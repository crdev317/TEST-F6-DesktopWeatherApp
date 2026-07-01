namespace WeatherApp.Core.Domain;

/// A candidate returned by a Location Search. Admin1 (region) is nullable — the
/// Geocoder omits it for some places (see spec Seam 1).
public sealed record LocationCandidate(
    string Name,
    string? Admin1,
    string Country,
    double Latitude,
    double Longitude);
