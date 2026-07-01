using CommunityToolkit.Mvvm.ComponentModel;
using WeatherApp.Core.Domain;

namespace WeatherApp.Core.ViewModels;

/// Shell: owns the two child ViewModels and mediates the activation handoff
/// (Approach A — shell-mediated composition). The children never reference each
/// other. Starts in the Empty state.
public sealed partial class MainViewModel : ObservableObject
{
    public SearchViewModel Search { get; }
    public WeatherViewModel Weather { get; }

    [ObservableProperty] private WeatherViewState _viewState = WeatherViewState.Empty;

    /// Exposes the in-flight activation load so callers/tests can await it.
    public Task LastActivation { get; private set; } = Task.CompletedTask;

    public MainViewModel(SearchViewModel search, WeatherViewModel weather)
    {
        Search = search;
        Weather = weather;
        Search.LocationSelected += OnLocationSelected;
    }

    private void OnLocationSelected(Location location)
    {
        ViewState = WeatherViewState.Weather;
        LastActivation = Weather.Load(location);
    }
}
