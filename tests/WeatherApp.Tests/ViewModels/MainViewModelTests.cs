using FluentAssertions;
using WeatherApp.Core.Domain;
using WeatherApp.Core.ViewModels;
using WeatherApp.Tests.Fakes;

namespace WeatherApp.Tests.ViewModels;

public class MainViewModelTests
{
    private static MainViewModel Build(out FakeGeocoder geo, out FakeWeatherProvider provider)
    {
        geo = new FakeGeocoder();
        provider = FakeWeatherProvider.Returning(new CurrentConditions(20.4, 12.2, "Mainly clear"));
        var search = new SearchViewModel(geo, new ManualDebounceScheduler());
        var weather = new WeatherViewModel(provider);
        return new MainViewModel(search, weather);
    }

    [Fact]
    public void Starts_in_empty_state()
    {
        var vm = Build(out _, out _);
        vm.ViewState.Should().Be(WeatherViewState.Empty);
    }

    [Fact]
    public async Task Selecting_a_candidate_activates_location_and_loads_weather()
    {
        var vm = Build(out _, out _);

        // Simulate the user picking a candidate in the child SearchViewModel; the
        // shell is the sole subscriber to LocationSelected and mediates the handoff.
        vm.Search.SelectCommand.Execute(new LocationCandidate("London", "England", "United Kingdom", 51.5, -0.12));
        await vm.LastActivation; // awaitable exposed for tests

        vm.ViewState.Should().Be(WeatherViewState.Weather);
        vm.Weather.State.Should().Be(WeatherLoadState.Loaded);
        vm.Weather.LocationName.Should().Be("London");
    }
}
