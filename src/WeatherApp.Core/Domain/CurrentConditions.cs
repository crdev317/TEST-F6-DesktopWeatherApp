namespace WeatherApp.Core.Domain;

/// Present-moment weather for the active Location, in fixed metric units.
public sealed record CurrentConditions(
    double TemperatureC,
    double WindSpeedKmh,
    string Condition);
