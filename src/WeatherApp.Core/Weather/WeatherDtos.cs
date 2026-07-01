using System.Text.Json.Serialization;

namespace WeatherApp.Core.Weather;

internal sealed class ForecastResponse
{
    [JsonPropertyName("current")] public CurrentBlock? Current { get; set; }
}

internal sealed class CurrentBlock
{
    [JsonPropertyName("temperature_2m")] public double Temperature { get; set; }
    [JsonPropertyName("weather_code")] public int WeatherCode { get; set; }
    [JsonPropertyName("wind_speed_10m")] public double WindSpeed { get; set; }
}
