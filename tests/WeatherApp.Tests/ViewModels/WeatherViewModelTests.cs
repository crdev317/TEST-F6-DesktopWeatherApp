using FluentAssertions;
using WeatherApp.Core.Domain;
using WeatherApp.Core.ViewModels;
using WeatherApp.Tests.Fakes;

namespace WeatherApp.Tests.ViewModels;

public class WeatherViewModelTests
{
    private static readonly Location London = new("London", 51.5, -0.12);

    [Fact]
    public async Task Load_success_sets_conditions_and_loaded_state()
    {
        var provider = FakeWeatherProvider.Returning(new CurrentConditions(20.4, 12.2, "Mainly clear"));
        var vm = new WeatherViewModel(provider);

        await vm.Load(London);

        vm.State.Should().Be(WeatherLoadState.Loaded);
        vm.Conditions!.TemperatureC.Should().Be(20.4);
        vm.Conditions!.Condition.Should().Be("Mainly clear");
        vm.LocationName.Should().Be("London");
    }

    [Fact]
    public async Task Load_drives_state_idle_to_loading_to_loaded()
    {
        var provider = FakeWeatherProvider.Returning(new CurrentConditions(20.4, 12.2, "Mainly clear"));
        var vm = new WeatherViewModel(provider);
        vm.State.Should().Be(WeatherLoadState.Idle); // starts Idle before any Load

        var observed = new List<WeatherLoadState>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WeatherViewModel.State)) observed.Add(vm.State);
        };

        await vm.Load(London);

        observed.Should().Equal(WeatherLoadState.Loading, WeatherLoadState.Loaded);
    }

    [Fact]
    public async Task Load_failure_sets_error_state_and_message()
    {
        var vm = new WeatherViewModel(FakeWeatherProvider.Throwing());

        await vm.Load(London);

        vm.State.Should().Be(WeatherLoadState.Error);
        vm.ErrorMessage.Should().Contain("Couldn't load weather for London");
        vm.Conditions.Should().BeNull();
    }

    // Security AC: the Error-state message must be the fixed neutral copy only —
    // never the raw exception type/stack trace or the request URL (which carries
    // the Location's coordinates). We induce a failure whose exception text leaks
    // all of those and assert none of them reach the display message.
    [Fact]
    public async Task Error_message_never_leaks_exception_url_or_coordinates()
    {
        var leaky = new HttpRequestException(
            "Response status code does not indicate success: 500 (Internal Server Error) " +
            "for https://api.open-meteo.com/v1/forecast?latitude=51.5&longitude=-0.12" +
            "&current=temperature_2m\n   at WeatherApp.Core.Weather.OpenMeteoWeatherProvider.GetCurrent()");
        var vm = new WeatherViewModel(FakeWeatherProvider.ThrowingWith(leaky));

        await vm.Load(London);

        vm.State.Should().Be(WeatherLoadState.Error);
        vm.ErrorMessage.Should().NotContain("HttpRequestException");   // no exception type name
        vm.ErrorMessage.Should().NotContain("at WeatherApp");          // no stack trace
        vm.ErrorMessage.Should().NotContain("open-meteo.com");         // no request URL
        vm.ErrorMessage.Should().NotContain("latitude");              // no coordinate echo
        vm.ErrorMessage.Should().NotContain("51.5");                  // no coordinate value
        vm.ErrorMessage.Should().NotContain("-0.12");
    }
}
