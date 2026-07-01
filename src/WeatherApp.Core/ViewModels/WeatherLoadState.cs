namespace WeatherApp.Core.ViewModels;

/// The load state machine for a Location's Current Conditions:
/// Idle → Loading → Loaded on success; Loading → Error on failure.
public enum WeatherLoadState { Idle, Loading, Loaded, Error }
