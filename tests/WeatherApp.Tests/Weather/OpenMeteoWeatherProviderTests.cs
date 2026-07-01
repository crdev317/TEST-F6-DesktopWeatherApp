using System.Globalization;
using System.Net;
using FluentAssertions;
using WeatherApp.Core.Domain;
using WeatherApp.Core.Weather;
using WeatherApp.Tests.TestHttp;

namespace WeatherApp.Tests.Weather;

public class OpenMeteoWeatherProviderTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static readonly Location London = new("London", 51.5085, -0.1257);

    [Fact]
    public async Task Maps_current_block_to_conditions()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("forecast-london.json"), out _);
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());

        var conditions = await provider.GetCurrent(London, CancellationToken.None);

        conditions.TemperatureC.Should().Be(20.4);
        conditions.WindSpeedKmh.Should().Be(12.2);
        conditions.Condition.Should().Be("Mainly clear"); // weather_code 1
    }

    [Fact]
    public async Task Sends_metric_unit_params_and_coordinates()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("forecast-london.json"), out var handler);
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());

        await provider.GetCurrent(London, CancellationToken.None);

        var query = handler.LastRequestUri!.Query;
        query.Should().Contain("temperature_unit=celsius");
        query.Should().Contain("wind_speed_unit=kmh");
        query.Should().Contain("latitude=51.5085");   // InvariantCulture decimal point
        query.Should().Contain("current=");
    }

    // Seam 3 (host-OS/locale) (d) proof: under a comma-decimal culture the default
    // double.ToString() would emit "51,5" and corrupt the query. Forcing de-DE and
    // asserting the "."-decimal wire form proves the formatting is invariant, not
    // host-locale-driven. The locale is the only thing varied vs the test above.
    [Fact]
    public async Task Formats_coordinates_invariantly_under_comma_decimal_locale()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("forecast-london.json"), out var handler);
            var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());

            await provider.GetCurrent(London, CancellationToken.None);

            var query = handler.LastRequestUri!.Query;
            query.Should().Contain("latitude=51.5085");    // "." not "," despite de-DE
            query.Should().Contain("longitude=-0.1257");
            query.Should().NotContain("51,5");             // the locale-corrupted form must never appear
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task Throws_on_error_status()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.BadRequest, """{"error":true,"reason":"bad"}""", out _);
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());

        var act = () => provider.GetCurrent(London, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // Security AC: a forecast call that exceeds the provider's finite timeout fails
    // closed rather than hanging indefinitely — surfaced to the caller's error path.
    [Fact]
    public async Task Exceeding_the_finite_timeout_fails_closed()
    {
        var http = StubHttpMessageHandler.ClientThatHangs(out _);
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap(), timeout: TimeSpan.FromMilliseconds(50));

        var act = () => provider.GetCurrent(London, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // Security AC: a call whose CancellationToken is cancelled fails closed to the
    // caller's error path — never an unhandled hang.
    [Fact]
    public async Task Cancelled_token_fails_closed()
    {
        var http = StubHttpMessageHandler.ClientThatHangs(out _);
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => provider.GetCurrent(London, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
