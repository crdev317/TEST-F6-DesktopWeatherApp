using System.Globalization;
using System.Net.Http.Json;
using WeatherApp.Core.Domain;

namespace WeatherApp.Core.Weather;

/// Weather Provider backed by Open-Meteo's forecast API. BaseAddress is
/// configured at registration (https://api.open-meteo.com/).
public sealed class OpenMeteoWeatherProvider : IWeatherProvider
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly WmoConditionMap _conditions;
    private readonly TimeSpan _timeout;

    public OpenMeteoWeatherProvider(HttpClient http, WmoConditionMap conditions, TimeSpan? timeout = null)
    {
        _http = http;
        _conditions = conditions;
        _timeout = timeout ?? DefaultTimeout;
    }

    public async Task<CurrentConditions> GetCurrent(Location location, CancellationToken ct)
    {
        // Bound the call with a finite timeout, linked to the caller's token, so a
        // hung endpoint or a cancelled caller fails closed rather than hanging — the
        // caller (WeatherViewModel) turns the thrown cancellation into its inline
        // "couldn't load weather" error rather than an indefinite hang (security AC).
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_timeout);
        var linked = timeoutCts.Token;

        // InvariantCulture so the decimal point is "." regardless of host locale
        // (a German locale would otherwise format 51,5 and corrupt the query — Seam 3).
        var lat = location.Latitude.ToString(CultureInfo.InvariantCulture);
        var lon = location.Longitude.ToString(CultureInfo.InvariantCulture);
        var url = $"v1/forecast?latitude={lat}&longitude={lon}" +
                  "&current=temperature_2m,weather_code,wind_speed_10m" +
                  "&temperature_unit=celsius&wind_speed_unit=kmh";

        using var response = await _http.GetAsync(url, linked);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<ForecastResponse>(cancellationToken: linked);
        var current = dto?.Current
            ?? throw new InvalidOperationException("Forecast response had no `current` block.");

        return new CurrentConditions(
            current.Temperature,
            current.WindSpeed,
            _conditions.ToCondition(current.WeatherCode));
    }
}
