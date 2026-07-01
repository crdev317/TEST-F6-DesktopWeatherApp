using WeatherApp.Core.Domain;
using WeatherApp.Core.Weather;

namespace WeatherApp.Tests.Fakes;

/// A fake Weather Provider at the seam: either returns preset Conditions or
/// throws, so a test can drive the WeatherViewModel load state machine into
/// Loaded or Error without touching the real Open-Meteo boundary.
public sealed class FakeWeatherProvider : IWeatherProvider
{
    private readonly Func<Location, CurrentConditions> _factory;
    private readonly bool _throws;
    private readonly Exception _thrown;

    private FakeWeatherProvider(Func<Location, CurrentConditions> factory, bool throws, Exception? thrown = null)
    {
        _factory = factory;
        _throws = throws;
        _thrown = thrown ?? new HttpRequestException("boom");
    }

    public static FakeWeatherProvider Returning(CurrentConditions c) => new(_ => c, false);
    public static FakeWeatherProvider Throwing() => new(_ => throw new HttpRequestException("boom"), true);

    /// Throws an exception whose text leaks everything the Error-state message must
    /// NOT surface: the exception type name, a stack-trace-shaped line, the request
    /// URL and the raw coordinates. Lets a test prove the message stays neutral.
    public static FakeWeatherProvider ThrowingWith(Exception ex) => new(_ => throw ex, true, ex);

    public Task<CurrentConditions> GetCurrent(Location location, CancellationToken ct)
        => _throws ? throw _thrown : Task.FromResult(_factory(location));
}
