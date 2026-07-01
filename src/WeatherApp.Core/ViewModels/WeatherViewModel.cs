using CommunityToolkit.Mvvm.ComponentModel;
using WeatherApp.Core.Domain;
using WeatherApp.Core.Weather;

namespace WeatherApp.Core.ViewModels;

/// Holds the Current Conditions for a Location and drives the load state machine.
/// Always fetches fresh (ADR-0001: never cache weather).
public sealed partial class WeatherViewModel : ObservableObject
{
    private readonly IWeatherProvider _provider;

    public WeatherViewModel(IWeatherProvider provider) => _provider = provider;

    [ObservableProperty] private CurrentConditions? _conditions;
    [ObservableProperty] private string? _locationName;
    [ObservableProperty] private WeatherLoadState _state = WeatherLoadState.Idle;
    [ObservableProperty] private string? _errorMessage;

    public async Task Load(Location location)
    {
        LocationName = location.Name;
        State = WeatherLoadState.Loading;
        ErrorMessage = null;
        try
        {
            Conditions = await _provider.GetCurrent(location, CancellationToken.None);
            State = WeatherLoadState.Loaded;
        }
        catch (Exception)
        {
            // Fixed neutral copy only: never surface the raw exception text/stack
            // trace or the request URL (which carries the Location's coordinates).
            // See the Story's security AC and Technical-Context "no raw stack trace
            // in UI" / "don't expose personal location beyond the request".
            Conditions = null;
            ErrorMessage = $"Couldn't load weather for {location.Name}.";
            State = WeatherLoadState.Error;
        }
    }
}
