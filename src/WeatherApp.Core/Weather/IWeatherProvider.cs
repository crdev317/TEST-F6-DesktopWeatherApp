using WeatherApp.Core.Domain;

namespace WeatherApp.Core.Weather;

public interface IWeatherProvider
{
    Task<CurrentConditions> GetCurrent(Location location, CancellationToken ct);
}
