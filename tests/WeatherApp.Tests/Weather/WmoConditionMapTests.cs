using FluentAssertions;
using WeatherApp.Core.Weather;

namespace WeatherApp.Tests.Weather;

public class WmoConditionMapTests
{
    private readonly WmoConditionMap _map = new();

    // The full WMO weather-code set Open-Meteo can emit (Seam 2). The six codes
    // named in the story acceptance criteria (0, 2, 45, 61, 71, 95) are included.
    [Theory]
    [InlineData(0, "Clear sky")]
    [InlineData(1, "Mainly clear")]
    [InlineData(2, "Partly cloudy")]
    [InlineData(3, "Overcast")]
    [InlineData(45, "Fog")]
    [InlineData(48, "Depositing rime fog")]
    [InlineData(51, "Light drizzle")]
    [InlineData(53, "Moderate drizzle")]
    [InlineData(55, "Dense drizzle")]
    [InlineData(56, "Light freezing drizzle")]
    [InlineData(57, "Dense freezing drizzle")]
    [InlineData(61, "Slight rain")]
    [InlineData(63, "Moderate rain")]
    [InlineData(65, "Heavy rain")]
    [InlineData(66, "Light freezing rain")]
    [InlineData(67, "Heavy freezing rain")]
    [InlineData(71, "Slight snowfall")]
    [InlineData(73, "Moderate snowfall")]
    [InlineData(75, "Heavy snowfall")]
    [InlineData(77, "Snow grains")]
    [InlineData(80, "Slight rain showers")]
    [InlineData(81, "Moderate rain showers")]
    [InlineData(82, "Violent rain showers")]
    [InlineData(85, "Slight snow showers")]
    [InlineData(86, "Heavy snow showers")]
    [InlineData(95, "Thunderstorm")]
    [InlineData(96, "Thunderstorm with slight hail")]
    [InlineData(99, "Thunderstorm with heavy hail")]
    public void Maps_known_codes_to_labels(int code, string expected)
        => _map.ToCondition(code).Should().Be(expected);

    [Theory]
    [InlineData(12345)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(4)] // a gap between defined WMO codes
    public void Maps_unknown_code_to_safe_fallback(int unknownCode)
        => _map.ToCondition(unknownCode).Should().Be("Unknown");
}
