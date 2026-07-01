using FluentAssertions;
using WeatherApp.Core.Domain;
using WeatherApp.Core.Geocoding;
using WeatherApp.Core.Weather;

namespace WeatherApp.Tests.Live;

/// Tier-2 (live, scheduled): one real call each to the Open-Meteo geocoding and
/// forecast endpoints, confirming the recorded fixtures still match the live
/// contract — fields present, types/nullability. Never asserts on volatile
/// weather values (temperature, wind, the specific Condition), only on the
/// deterministic envelope. Tagged Tier=Live so the every-commit Tier-1 run
/// excludes them (`dotnet test --filter Tier!=Live`); the scheduled live run is
/// `dotnet test --filter Tier=Live`.
[Trait("Tier", "Live")]
public class OpenMeteoLiveTests
{
    [Fact]
    public async Task Geocoder_returns_candidates_for_London()
    {
        using var http = new HttpClient { BaseAddress = new Uri("https://geocoding-api.open-meteo.com/") };
        var geocoder = new OpenMeteoGeocoder(http);

        var results = await geocoder.Search("London", CancellationToken.None);

        // Shape, not value: candidates come back with non-empty name/country.
        results.Should().NotBeEmpty();
        results[0].Name.Should().NotBeNullOrEmpty();
        results[0].Country.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Weather_provider_returns_conditions_for_coords()
    {
        using var http = new HttpClient { BaseAddress = new Uri("https://api.open-meteo.com/") };
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());

        var conditions = await provider.GetCurrent(new Location("London", 51.5085, -0.1257), CancellationToken.None);

        // Shape, not value: a Condition is always mapped (the pure WMO map is
        // total), so a non-empty Condition proves the `current` block came back
        // with a parseable weather_code — never assert the specific weather.
        conditions.Condition.Should().NotBeNullOrEmpty();
    }
}
