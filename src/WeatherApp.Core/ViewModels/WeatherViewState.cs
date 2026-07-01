namespace WeatherApp.Core.ViewModels;

/// The body the shell shows beneath the always-visible search box:
/// Empty (search prompt) before any Location is active, Weather once one is loaded.
public enum WeatherViewState { Empty, Weather }
