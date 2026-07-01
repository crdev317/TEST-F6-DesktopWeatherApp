namespace WeatherApp.Core.Domain;

/// The single active place weather is shown for.
public sealed record Location(string Name, double Latitude, double Longitude);
