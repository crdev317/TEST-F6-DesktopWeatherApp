using System.Text.Json.Serialization;

namespace WeatherApp.Core.Geocoding;

internal sealed class GeocodingResponse
{
    // Nullable by design: Open-Meteo omits `results` on a zero-match query.
    [JsonPropertyName("results")] public List<GeocodingResult>? Results { get; set; }
}

internal sealed class GeocodingResult
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("latitude")] public double Latitude { get; set; }
    [JsonPropertyName("longitude")] public double Longitude { get; set; }
    [JsonPropertyName("country")] public string Country { get; set; } = "";
    [JsonPropertyName("admin1")] public string? Admin1 { get; set; }
}
