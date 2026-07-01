using System.Net.Http.Json;
using WeatherApp.Core.Domain;

namespace WeatherApp.Core.Geocoding;

/// Geocoder backed by Open-Meteo's geocoding API. The HttpClient's BaseAddress
/// is configured at registration (https://geocoding-api.open-meteo.com/).
public sealed class OpenMeteoGeocoder : IGeocoder
{
    private const int MaxResults = 10;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;

    public OpenMeteoGeocoder(HttpClient http, TimeSpan? timeout = null)
    {
        _http = http;
        _timeout = timeout ?? DefaultTimeout;
    }

    public async Task<IReadOnlyList<LocationCandidate>> Search(string query, CancellationToken ct)
    {
        // Bound the call with a finite timeout, linked to the caller's token, so a
        // hung endpoint or a cancelled caller fails closed rather than hanging (AC2).
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_timeout);
        var linked = timeoutCts.Token;

        // URL-encode the query so query-significant characters (&, #, =, CRLF, …)
        // cannot inject or forge extra query parameters (security AC1).
        var url = $"v1/search?name={Uri.EscapeDataString(query)}&count={MaxResults}&language=en&format=json";
        using var response = await _http.GetAsync(url, linked);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<GeocodingResponse>(cancellationToken: linked);
        if (dto?.Results is null)
            return Array.Empty<LocationCandidate>();

        return dto.Results
            .Select(r => new LocationCandidate(r.Name, r.Admin1, r.Country, r.Latitude, r.Longitude))
            .ToList();
    }
}
