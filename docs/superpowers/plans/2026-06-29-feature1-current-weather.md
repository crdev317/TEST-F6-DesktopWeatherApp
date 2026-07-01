# Feature 1 — Current Weather (tracer bullet) Implementation Plan

> **For agentic workers:** Do NOT implement this plan directly. It must first pass `/feature-doc-gauntlet` in a clean session, then be broken into stories by `/enate-to-stories`; AFK implementation happens per-story from there. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the thinnest end-to-end slice: search a place by name → pick a candidate → see its Current Conditions (temperature, wind, Condition) in metric units.

**Architecture:** WPF (.NET 8) on the generic host with MVVM. A `WeatherApp.Core` class library (`net8.0`) holds the domain records, the two Open-Meteo HTTP clients (Geocoder, Weather Provider), the pure WMO→Condition map, the debounce abstraction, and the three ViewModels — all testable without WPF. A thin `WeatherApp` WPF project (`net8.0-windows`) holds `App`, host wiring, and XAML views. Activation is shell-mediated: `SearchViewModel` raises `LocationSelected`, `MainViewModel` sets the active Location and calls `WeatherViewModel.Load`.

**Tech Stack:** WPF/.NET 8, C#; `CommunityToolkit.Mvvm`; `IHttpClientFactory` (`Microsoft.Extensions.Http`) + `System.Text.Json` / `System.Net.Http.Json`; `Microsoft.Extensions.{DependencyInjection,Hosting,Logging}`; tests with `xUnit` + `FluentAssertions` + `Moq`.

**Context references:**
- Spec: `docs/superpowers/specs/2026-06-29-feature1-current-weather-design.md`
- `business-domain-context.md` (the project's `Context.MD`)
- `Technical-Context.MD` (Overriding Principles that apply: **#1 No secrets in source**)
- ADRs: `docs/adr/0001-persist-location-only-never-cache-weather.md`

> An AFK Developer Agent picking up this plan MUST load every file in the Context references block before writing code.

---

## File structure

```
WeatherApp.sln
src/
  WeatherApp.Core/                       (net8.0 class library — no WPF)
    WeatherApp.Core.csproj
    Domain/LocationCandidate.cs
    Domain/Location.cs
    Domain/CurrentConditions.cs
    Geocoding/IGeocoder.cs
    Geocoding/OpenMeteoGeocoder.cs
    Geocoding/GeocodingDtos.cs            (internal JSON DTOs)
    Weather/IWeatherProvider.cs
    Weather/OpenMeteoWeatherProvider.cs
    Weather/WeatherDtos.cs                (internal JSON DTOs)
    Weather/WmoConditionMap.cs
    Time/IDebounceScheduler.cs
    Time/DebounceScheduler.cs
    ViewModels/SearchViewModel.cs
    ViewModels/WeatherViewModel.cs
    ViewModels/MainViewModel.cs
    ViewModels/WeatherViewState.cs        (enum: Empty, Weather)
    ViewModels/WeatherLoadState.cs        (enum: Idle, Loading, Loaded, Error)
  WeatherApp/                            (net8.0-windows — WPF shell)
    WeatherApp.csproj
    App.xaml / App.xaml.cs                (generic host + DI)
    MainWindow.xaml / MainWindow.xaml.cs
tests/
  WeatherApp.Tests/                       (net8.0)
    WeatherApp.Tests.csproj
    TestHttp/StubHttpMessageHandler.cs
    Fixtures/geocoding-london.json
    Fixtures/geocoding-single.json
    Fixtures/geocoding-zero.json
    Fixtures/geocoding-error-400.json
    Fixtures/forecast-london.json
    Weather/WmoConditionMapTests.cs
    Geocoding/OpenMeteoGeocoderTests.cs
    Weather/OpenMeteoWeatherProviderTests.cs
    ViewModels/SearchViewModelTests.cs
    ViewModels/WeatherViewModelTests.cs
    ViewModels/MainViewModelTests.cs
    Fakes/FakeGeocoder.cs
    Fakes/FakeWeatherProvider.cs
    Fakes/ManualDebounceScheduler.cs
```

---

## Task 1: Scaffold the solution, projects, and a smoke test

**Files:**
- Create: `WeatherApp.sln`, `src/WeatherApp.Core/WeatherApp.Core.csproj`, `src/WeatherApp/WeatherApp.csproj`, `tests/WeatherApp.Tests/WeatherApp.Tests.csproj`, `tests/WeatherApp.Tests/SmokeTest.cs`

- [ ] **Step 1: Create the solution and projects**

```bash
dotnet new sln -n WeatherApp
dotnet new classlib -n WeatherApp.Core -o src/WeatherApp.Core -f net8.0
dotnet new wpf -n WeatherApp -o src/WeatherApp -f net8.0-windows
dotnet new xunit -n WeatherApp.Tests -o tests/WeatherApp.Tests -f net8.0
rm src/WeatherApp.Core/Class1.cs tests/WeatherApp.Tests/UnitTest1.cs
dotnet sln add src/WeatherApp.Core src/WeatherApp tests/WeatherApp.Tests
dotnet add src/WeatherApp reference src/WeatherApp.Core
dotnet add tests/WeatherApp.Tests reference src/WeatherApp.Core
```

- [ ] **Step 2: Add packages**

```bash
dotnet add src/WeatherApp.Core package CommunityToolkit.Mvvm
dotnet add src/WeatherApp.Core package Microsoft.Extensions.Http
dotnet add src/WeatherApp package Microsoft.Extensions.Hosting
dotnet add tests/WeatherApp.Tests package FluentAssertions
dotnet add tests/WeatherApp.Tests package Moq
```

(`System.Net.Http.Json` and `System.Text.Json` are in-box for `net8.0` — no package needed.)

- [ ] **Step 3: Mark test fixtures to copy to output (so tests can read the JSON files)**

Add to `tests/WeatherApp.Tests/WeatherApp.Tests.csproj` inside the `<Project>`:

```xml
  <ItemGroup>
    <None Include="Fixtures\**\*.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 4: Write a smoke test**

`tests/WeatherApp.Tests/SmokeTest.cs`:

```csharp
namespace WeatherApp.Tests;

public class SmokeTest
{
    [Fact]
    public void Solution_builds_and_tests_run() => Assert.True(true);
}
```

- [ ] **Step 5: Build and test**

Run: `dotnet build && dotnet test`
Expected: build succeeds; 1 test passes.

- [ ] **Step 6: Commit**

```bash
git add WeatherApp.sln src/ tests/
git commit -m "chore: scaffold WeatherApp solution (Core lib, WPF shell, test project)"
```

---

## Task 2: Domain records

**Files:**
- Create: `src/WeatherApp.Core/Domain/LocationCandidate.cs`, `Location.cs`, `CurrentConditions.cs`

- [ ] **Step 1: Write the records**

`src/WeatherApp.Core/Domain/LocationCandidate.cs`:

```csharp
namespace WeatherApp.Core.Domain;

/// A candidate returned by a Location Search. Admin1 (region) is nullable — the
/// Geocoder omits it for some places (see spec Seam 1).
public sealed record LocationCandidate(
    string Name,
    string? Admin1,
    string Country,
    double Latitude,
    double Longitude);
```

`src/WeatherApp.Core/Domain/Location.cs`:

```csharp
namespace WeatherApp.Core.Domain;

/// The single active place weather is shown for.
public sealed record Location(string Name, double Latitude, double Longitude);
```

`src/WeatherApp.Core/Domain/CurrentConditions.cs`:

```csharp
namespace WeatherApp.Core.Domain;

/// Present-moment weather for the active Location, in fixed metric units.
public sealed record CurrentConditions(
    double TemperatureC,
    double WindSpeedKmh,
    string Condition);
```

- [ ] **Step 2: Build**

Run: `dotnet build src/WeatherApp.Core`
Expected: succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/WeatherApp.Core/Domain
git commit -m "feat: add Location, LocationCandidate, CurrentConditions domain records"
```

---

## Task 3: WMO Condition map (pure)

**Covers:** the WMO-code→Condition mapping in the spec; backs Seam 2's `weather_code` handling.

**Files:**
- Create: `src/WeatherApp.Core/Weather/WmoConditionMap.cs`
- Test: `tests/WeatherApp.Tests/Weather/WmoConditionMapTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/WeatherApp.Tests/Weather/WmoConditionMapTests.cs`:

```csharp
using FluentAssertions;
using WeatherApp.Core.Weather;

namespace WeatherApp.Tests.Weather;

public class WmoConditionMapTests
{
    private readonly WmoConditionMap _map = new();

    [Theory]
    [InlineData(0, "Clear sky")]
    [InlineData(2, "Partly cloudy")]
    [InlineData(45, "Fog")]
    [InlineData(61, "Slight rain")]
    [InlineData(71, "Slight snowfall")]
    [InlineData(95, "Thunderstorm")]
    public void Maps_known_codes_to_labels(int code, string expected)
        => _map.ToCondition(code).Should().Be(expected);

    [Fact]
    public void Maps_unknown_code_to_safe_fallback()
        => _map.ToCondition(12345).Should().Be("Unknown");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter WmoConditionMapTests`
Expected: FAIL — `WmoConditionMap` does not exist.

- [ ] **Step 3: Implement**

`src/WeatherApp.Core/Weather/WmoConditionMap.cs`:

```csharp
using System.Collections.Generic;

namespace WeatherApp.Core.Weather;

/// Maps an Open-Meteo WMO weather code to a human-readable Condition.
/// Pure and total: an unrecognised code returns "Unknown"; never throws.
public sealed class WmoConditionMap
{
    private static readonly IReadOnlyDictionary<int, string> Map = new Dictionary<int, string>
    {
        [0] = "Clear sky",
        [1] = "Mainly clear",
        [2] = "Partly cloudy",
        [3] = "Overcast",
        [45] = "Fog",
        [48] = "Depositing rime fog",
        [51] = "Light drizzle",
        [53] = "Moderate drizzle",
        [55] = "Dense drizzle",
        [56] = "Light freezing drizzle",
        [57] = "Dense freezing drizzle",
        [61] = "Slight rain",
        [63] = "Moderate rain",
        [65] = "Heavy rain",
        [66] = "Light freezing rain",
        [67] = "Heavy freezing rain",
        [71] = "Slight snowfall",
        [73] = "Moderate snowfall",
        [75] = "Heavy snowfall",
        [77] = "Snow grains",
        [80] = "Slight rain showers",
        [81] = "Moderate rain showers",
        [82] = "Violent rain showers",
        [85] = "Slight snow showers",
        [86] = "Heavy snow showers",
        [95] = "Thunderstorm",
        [96] = "Thunderstorm with slight hail",
        [99] = "Thunderstorm with heavy hail",
    };

    public string ToCondition(int wmoCode) =>
        Map.TryGetValue(wmoCode, out var label) ? label : "Unknown";
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter WmoConditionMapTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/WeatherApp.Core/Weather/WmoConditionMap.cs tests/WeatherApp.Tests/Weather/WmoConditionMapTests.cs
git commit -m "feat: add WMO weather-code to Condition map"
```

---

## Task 4: Geocoder — recorded-replay (Seam 1)

**Covers Seam 1 (c) contract:** *HTTPS `GET /v1/search?name=&count=&language=en&format=json`, no auth; success body has candidates under `results`, which is **absent when there are zero matches** (treat as empty list); each candidate has `name`/`latitude`/`longitude`/`country` (non-null) and `admin1` (nullable); error is HTTP 400 `{"error":true,"reason":...}`.*
**(d) proof:** the recorded-replay tests below, over fixtures captured live from Open-Meteo on 2026-06-29.

**Files:**
- Create: `src/WeatherApp.Core/Geocoding/IGeocoder.cs`, `OpenMeteoGeocoder.cs`, `GeocodingDtos.cs`
- Create: `tests/WeatherApp.Tests/TestHttp/StubHttpMessageHandler.cs`
- Create fixtures: `tests/WeatherApp.Tests/Fixtures/geocoding-london.json`, `geocoding-single.json`, `geocoding-zero.json`, `geocoding-error-400.json`
- Test: `tests/WeatherApp.Tests/Geocoding/OpenMeteoGeocoderTests.cs`

- [ ] **Step 1: Add the captured fixtures**

`tests/WeatherApp.Tests/Fixtures/geocoding-london.json`:

```json
{"results":[{"id":2643743,"name":"London","latitude":51.50853,"longitude":-0.12574,"elevation":25.0,"feature_code":"PPLC","country_code":"GB","admin1_id":6269131,"timezone":"Europe/London","population":8961989,"country_id":2635167,"country":"United Kingdom","admin1":"England"},{"id":6058560,"name":"London","latitude":42.98339,"longitude":-81.23304,"country_code":"CA","timezone":"America/Toronto","population":422324,"country_id":6251999,"country":"Canada","admin1":"Ontario"}],"generationtime_ms":0.86}
```

`tests/WeatherApp.Tests/Fixtures/geocoding-single.json`:

```json
{"results":[{"id":2643743,"name":"London","latitude":51.50853,"longitude":-0.12574,"country":"United Kingdom","admin1":"England"}],"generationtime_ms":0.4}
```

`tests/WeatherApp.Tests/Fixtures/geocoding-zero.json` — **the absent-`results` trap, captured live**:

```json
{"generationtime_ms":0.54883957}
```

`tests/WeatherApp.Tests/Fixtures/geocoding-error-400.json`:

```json
{"error":true,"reason":"No value found (expected type 'String') at path 'name'."}
```

- [ ] **Step 2: Write the stub HTTP handler (test helper)**

`tests/WeatherApp.Tests/TestHttp/StubHttpMessageHandler.cs`:

```csharp
using System.Net;

namespace WeatherApp.Tests.TestHttp;

/// Replays a fixed response and captures the last request URI, so a typed
/// HttpClient really parses recorded bytes (real local I/O on the parse side).
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;
    public Uri? LastRequestUri { get; private set; }

    public StubHttpMessageHandler(HttpStatusCode status, string body)
    {
        _status = status;
        _body = body;
    }

    public static HttpClient ClientReturning(HttpStatusCode status, string body, out StubHttpMessageHandler handler)
    {
        handler = new StubHttpMessageHandler(status, body);
        return new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        return Task.FromResult(new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json"),
        });
    }
}
```

- [ ] **Step 3: Write the failing tests**

`tests/WeatherApp.Tests/Geocoding/OpenMeteoGeocoderTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using WeatherApp.Core.Geocoding;
using WeatherApp.Tests.TestHttp;

namespace WeatherApp.Tests.Geocoding;

public class OpenMeteoGeocoderTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public async Task Parses_multiple_candidates_with_fields()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("geocoding-london.json"), out _);
        var geocoder = new OpenMeteoGeocoder(http);

        var result = await geocoder.Search("London", CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("London");
        result[0].Country.Should().Be("United Kingdom");
        result[0].Admin1.Should().Be("England");
        result[0].Latitude.Should().BeApproximately(51.50853, 0.0001);
    }

    [Fact]
    public async Task Treats_absent_results_key_as_empty_list()
    {
        // Seam 1: a zero-match query omits the `results` key entirely.
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("geocoding-zero.json"), out _);
        var geocoder = new OpenMeteoGeocoder(http);

        var result = await geocoder.Search("zzzznotaplace", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Tolerates_absent_admin1()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK,
            """{"results":[{"name":"Nowhere","latitude":1.0,"longitude":2.0,"country":"X"}]}""", out _);
        var geocoder = new OpenMeteoGeocoder(http);

        var result = await geocoder.Search("Nowhere", CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Admin1.Should().BeNull();
    }

    [Fact]
    public async Task Sends_name_and_count_params()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("geocoding-single.json"), out var handler);
        var geocoder = new OpenMeteoGeocoder(http);

        await geocoder.Search("Paris", CancellationToken.None);

        handler.LastRequestUri!.Query.Should().Contain("name=Paris").And.Contain("count=");
    }

    [Fact]
    public async Task Throws_on_error_status()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.BadRequest, Fixture("geocoding-error-400.json"), out _);
        var geocoder = new OpenMeteoGeocoder(http);

        var act = () => geocoder.Search("", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test --filter OpenMeteoGeocoderTests`
Expected: FAIL — `OpenMeteoGeocoder`/`IGeocoder` do not exist.

- [ ] **Step 5: Implement the interface, DTOs, and client**

`src/WeatherApp.Core/Geocoding/IGeocoder.cs`:

```csharp
using WeatherApp.Core.Domain;

namespace WeatherApp.Core.Geocoding;

public interface IGeocoder
{
    Task<IReadOnlyList<LocationCandidate>> Search(string query, CancellationToken ct);
}
```

`src/WeatherApp.Core/Geocoding/GeocodingDtos.cs`:

```csharp
using System.Text.Json.Serialization;

namespace WeatherApp.Core.Geocoding;

internal sealed class GeocodingResponse
{
    // Nullable by design: Open-Meteo omits `results` on a zero-match query.
    [JsonPropertyName("results")] public List<GeocodingResult>? Results { get; set; }
}

internal sealed class GeocodingResult
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("latitude")] public double Latitude { get; set; }
    [JsonPropertyName("longitude")] public double Longitude { get; set; }
    [JsonPropertyName("country")] public string Country { get; set; } = "";
    [JsonPropertyName("admin1")] public string? Admin1 { get; set; }
}
```

`src/WeatherApp.Core/Geocoding/OpenMeteoGeocoder.cs`:

```csharp
using System.Net.Http.Json;
using WeatherApp.Core.Domain;

namespace WeatherApp.Core.Geocoding;

/// Geocoder backed by Open-Meteo's geocoding API. The HttpClient's BaseAddress
/// is configured at registration (https://geocoding-api.open-meteo.com/).
public sealed class OpenMeteoGeocoder : IGeocoder
{
    private const int MaxResults = 10;
    private readonly HttpClient _http;

    public OpenMeteoGeocoder(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<LocationCandidate>> Search(string query, CancellationToken ct)
    {
        var url = $"v1/search?name={Uri.EscapeDataString(query)}&count={MaxResults}&language=en&format=json";
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<GeocodingResponse>(cancellationToken: ct);
        if (dto?.Results is null)
            return Array.Empty<LocationCandidate>();

        return dto.Results
            .Select(r => new LocationCandidate(r.Name, r.Admin1, r.Country, r.Latitude, r.Longitude))
            .ToList();
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test --filter OpenMeteoGeocoderTests`
Expected: PASS (5 tests).

- [ ] **Step 7: Commit**

```bash
git add src/WeatherApp.Core/Geocoding tests/WeatherApp.Tests/Geocoding tests/WeatherApp.Tests/TestHttp tests/WeatherApp.Tests/Fixtures/geocoding-*.json
git commit -m "feat: add Open-Meteo Geocoder with recorded-replay tests (Seam 1)"
```

---

## Task 5: Weather Provider client — recorded-replay (Seam 2)

**Covers Seam 2 (c) contract:** *HTTPS `GET /v1/forecast?latitude=&longitude=&current=temperature_2m,weather_code,wind_speed_10m&temperature_unit=celsius&wind_speed_unit=kmh`, no auth; success body has a `current` object with `temperature_2m`/`weather_code`/`wind_speed_10m` (all non-null); the metric unit params must be on the request; non-2xx is an error.*
**Also covers Seam 3 (c) contract (host-OS/locale):** *the outbound `latitude`/`longitude` are formatted invariant-culture (`.` decimal), byte-identical across every host locale — never the host's separator.*
**(d) proof:** the recorded-replay tests below, over a fixture captured live on 2026-06-29 — plus the `Formats_coordinates_invariantly_under_comma_decimal_locale` test (Step 2) which forces a `de-DE` culture and asserts the wire form is unchanged (Seam 3 (d)).

**Files:**
- Create: `src/WeatherApp.Core/Weather/IWeatherProvider.cs`, `OpenMeteoWeatherProvider.cs`, `WeatherDtos.cs`
- Create fixture: `tests/WeatherApp.Tests/Fixtures/forecast-london.json`
- Test: `tests/WeatherApp.Tests/Weather/OpenMeteoWeatherProviderTests.cs`

- [ ] **Step 1: Add the captured fixture**

`tests/WeatherApp.Tests/Fixtures/forecast-london.json`:

```json
{"latitude":51.5,"longitude":-0.25,"generationtime_ms":0.21,"utc_offset_seconds":0,"timezone":"GMT","timezone_abbreviation":"GMT","elevation":23.0,"current_units":{"time":"iso8601","interval":"seconds","temperature_2m":"°C","weather_code":"wmo code","wind_speed_10m":"km/h"},"current":{"time":"2026-06-29T22:00","interval":900,"temperature_2m":20.4,"weather_code":1,"wind_speed_10m":12.2}}
```

- [ ] **Step 2: Write the failing tests**

`tests/WeatherApp.Tests/Weather/OpenMeteoWeatherProviderTests.cs`:

```csharp
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
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --filter OpenMeteoWeatherProviderTests`
Expected: FAIL — types do not exist.

- [ ] **Step 4: Implement the interface, DTOs, and client**

`src/WeatherApp.Core/Weather/IWeatherProvider.cs`:

```csharp
using WeatherApp.Core.Domain;

namespace WeatherApp.Core.Weather;

public interface IWeatherProvider
{
    Task<CurrentConditions> GetCurrent(Location location, CancellationToken ct);
}
```

`src/WeatherApp.Core/Weather/WeatherDtos.cs`:

```csharp
using System.Text.Json.Serialization;

namespace WeatherApp.Core.Weather;

internal sealed class ForecastResponse
{
    [JsonPropertyName("current")] public CurrentBlock? Current { get; set; }
}

internal sealed class CurrentBlock
{
    [JsonPropertyName("temperature_2m")] public double Temperature { get; set; }
    [JsonPropertyName("weather_code")] public int WeatherCode { get; set; }
    [JsonPropertyName("wind_speed_10m")] public double WindSpeed { get; set; }
}
```

`src/WeatherApp.Core/Weather/OpenMeteoWeatherProvider.cs`:

```csharp
using System.Globalization;
using System.Net.Http.Json;
using WeatherApp.Core.Domain;

namespace WeatherApp.Core.Weather;

/// Weather Provider backed by Open-Meteo's forecast API. BaseAddress is
/// configured at registration (https://api.open-meteo.com/).
public sealed class OpenMeteoWeatherProvider : IWeatherProvider
{
    private readonly HttpClient _http;
    private readonly WmoConditionMap _conditions;

    public OpenMeteoWeatherProvider(HttpClient http, WmoConditionMap conditions)
    {
        _http = http;
        _conditions = conditions;
    }

    public async Task<CurrentConditions> GetCurrent(Location location, CancellationToken ct)
    {
        // InvariantCulture so the decimal point is "." regardless of host locale
        // (a German locale would otherwise format 51,5 and corrupt the query).
        var lat = location.Latitude.ToString(CultureInfo.InvariantCulture);
        var lon = location.Longitude.ToString(CultureInfo.InvariantCulture);
        var url = $"v1/forecast?latitude={lat}&longitude={lon}" +
                  "&current=temperature_2m,weather_code,wind_speed_10m" +
                  "&temperature_unit=celsius&wind_speed_unit=kmh";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<ForecastResponse>(cancellationToken: ct);
        var current = dto?.Current
            ?? throw new InvalidOperationException("Forecast response had no `current` block.");

        return new CurrentConditions(
            current.Temperature,
            current.WindSpeed,
            _conditions.ToCondition(current.WeatherCode));
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter OpenMeteoWeatherProviderTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/WeatherApp.Core/Weather/IWeatherProvider.cs src/WeatherApp.Core/Weather/OpenMeteoWeatherProvider.cs src/WeatherApp.Core/Weather/WeatherDtos.cs tests/WeatherApp.Tests/Weather/OpenMeteoWeatherProviderTests.cs tests/WeatherApp.Tests/Fixtures/forecast-london.json
git commit -m "feat: add Open-Meteo Weather Provider with recorded-replay tests (Seam 2)"
```

---

## Task 6: Debounce scheduler + SearchViewModel

**Files:**
- Create: `src/WeatherApp.Core/Time/IDebounceScheduler.cs`, `DebounceScheduler.cs`
- Create: `src/WeatherApp.Core/ViewModels/SearchViewModel.cs`
- Create: `tests/WeatherApp.Tests/Fakes/FakeGeocoder.cs`, `ManualDebounceScheduler.cs`
- Test: `tests/WeatherApp.Tests/ViewModels/SearchViewModelTests.cs`

- [ ] **Step 1: Write the debounce abstraction (production + test fake)**

`src/WeatherApp.Core/Time/IDebounceScheduler.cs`:

```csharp
namespace WeatherApp.Core.Time;

/// Schedules a single pending action after a delay; a new Schedule cancels the
/// previous pending one. Injected so tests fire it synchronously (no real wait).
public interface IDebounceScheduler
{
    void Schedule(TimeSpan delay, Func<Task> action);
}
```

`src/WeatherApp.Core/Time/DebounceScheduler.cs`:

```csharp
using System.Timers;

namespace WeatherApp.Core.Time;

/// Production debounce backed by a System.Timers.Timer. The action is marshalled
/// by the caller (the ViewModel updates observable state, which CommunityToolkit
/// marshals to the UI thread via its synchronisation context).
public sealed class DebounceScheduler : IDebounceScheduler, IDisposable
{
    private System.Timers.Timer? _timer;

    public void Schedule(TimeSpan delay, Func<Task> action)
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = new System.Timers.Timer(delay.TotalMilliseconds) { AutoReset = false };
        _timer.Elapsed += async (_, _) => await action();
        _timer.Start();
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }
}
```

`tests/WeatherApp.Tests/Fakes/ManualDebounceScheduler.cs`:

```csharp
using WeatherApp.Core.Time;

namespace WeatherApp.Tests.Fakes;

/// Captures the last scheduled action so a test can fire it synchronously.
public sealed class ManualDebounceScheduler : IDebounceScheduler
{
    private Func<Task>? _pending;
    public int ScheduleCount { get; private set; }

    public void Schedule(TimeSpan delay, Func<Task> action)
    {
        ScheduleCount++;
        _pending = action;
    }

    public Task FireAsync() => _pending?.Invoke() ?? Task.CompletedTask;
}
```

- [ ] **Step 2: Write the fake Geocoder (controls completion order)**

`tests/WeatherApp.Tests/Fakes/FakeGeocoder.cs`:

```csharp
using WeatherApp.Core.Domain;
using WeatherApp.Core.Geocoding;

namespace WeatherApp.Tests.Fakes;

/// Returns a TaskCompletionSource per query so a test can complete calls out of
/// order (to exercise the sequence-guard), or throw, or return preset results.
public sealed class FakeGeocoder : IGeocoder
{
    private readonly Dictionary<string, TaskCompletionSource<IReadOnlyList<LocationCandidate>>> _pending = new();
    public List<string> Queries { get; } = new();

    public Task<IReadOnlyList<LocationCandidate>> Search(string query, CancellationToken ct)
    {
        Queries.Add(query);
        var tcs = new TaskCompletionSource<IReadOnlyList<LocationCandidate>>();
        _pending[query] = tcs;
        return tcs.Task;
    }

    public void Complete(string query, params LocationCandidate[] results)
        => _pending[query].SetResult(results);

    public void Fail(string query, Exception ex) => _pending[query].SetException(ex);
}
```

- [ ] **Step 3: Write the failing tests**

`tests/WeatherApp.Tests/ViewModels/SearchViewModelTests.cs`:

```csharp
using FluentAssertions;
using WeatherApp.Core.Domain;
using WeatherApp.Core.ViewModels;
using WeatherApp.Tests.Fakes;

namespace WeatherApp.Tests.ViewModels;

public class SearchViewModelTests
{
    private static (SearchViewModel vm, FakeGeocoder geo, ManualDebounceScheduler sched) Build()
    {
        var geo = new FakeGeocoder();
        var sched = new ManualDebounceScheduler();
        return (new SearchViewModel(geo, sched), geo, sched);
    }

    [Fact]
    public void Query_under_two_chars_schedules_no_search_and_clears()
    {
        var (vm, _, sched) = Build();
        vm.Candidates.Add(new LocationCandidate("Old", null, "X", 0, 0));

        vm.Query = "a";

        sched.ScheduleCount.Should().Be(0);
        vm.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_populates_candidates()
    {
        var (vm, geo, sched) = Build();
        vm.Query = "London";

        var fire = sched.FireAsync();
        geo.Complete("London", new LocationCandidate("London", "England", "United Kingdom", 51.5, -0.12));
        await fire;

        vm.Candidates.Should().ContainSingle().Which.Name.Should().Be("London");
        vm.SearchMessage.Should().BeNull();
    }

    [Fact]
    public async Task Zero_results_sets_message()
    {
        var (vm, geo, sched) = Build();
        vm.Query = "zzzz";

        var fire = sched.FireAsync();
        geo.Complete("zzzz"); // empty
        await fire;

        vm.Candidates.Should().BeEmpty();
        vm.SearchMessage.Should().Contain("No places found");
    }

    [Fact]
    public async Task Geocoder_failure_sets_error_message()
    {
        var (vm, geo, sched) = Build();
        vm.Query = "London";

        var fire = sched.FireAsync();
        geo.Fail("London", new HttpRequestException("boom"));
        await fire;

        vm.SearchMessage.Should().Contain("Couldn't search");
    }

    [Fact]
    public async Task Stale_response_is_dropped_latest_query_wins()
    {
        var (vm, geo, sched) = Build();

        vm.Query = "Lon";
        var fire1 = sched.FireAsync();   // search seq 1 in flight for "Lon"
        vm.Query = "Lond";
        var fire2 = sched.FireAsync();   // search seq 2 in flight for "Lond"

        // Complete the LATEST first, then the stale earlier one.
        geo.Complete("Lond", new LocationCandidate("London", "England", "United Kingdom", 51.5, -0.12));
        await fire2;
        geo.Complete("Lon", new LocationCandidate("Longview", "Texas", "United States", 32.5, -94.7));
        await fire1;

        vm.Candidates.Should().ContainSingle().Which.Admin1.Should().Be("England");
    }

    [Fact]
    public void Selecting_a_candidate_raises_LocationSelected()
    {
        var (vm, _, _) = Build();
        Location? selected = null;
        vm.LocationSelected += loc => selected = loc;

        vm.SelectCommand.Execute(new LocationCandidate("Paris", "Île-de-France", "France", 48.85, 2.35));

        selected.Should().Be(new Location("Paris", 48.85, 2.35));
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test --filter SearchViewModelTests`
Expected: FAIL — `SearchViewModel` does not exist.

- [ ] **Step 5: Implement SearchViewModel**

`src/WeatherApp.Core/ViewModels/SearchViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WeatherApp.Core.Domain;
using WeatherApp.Core.Geocoding;
using WeatherApp.Core.Time;

namespace WeatherApp.Core.ViewModels;

public sealed partial class SearchViewModel : ObservableObject
{
    private const int MinQueryLength = 2;
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly IGeocoder _geocoder;
    private readonly IDebounceScheduler _scheduler;

    private int _latestSeq;
    private CancellationTokenSource? _cts;

    public SearchViewModel(IGeocoder geocoder, IDebounceScheduler scheduler)
    {
        _geocoder = geocoder;
        _scheduler = scheduler;
    }

    public ObservableCollection<LocationCandidate> Candidates { get; } = new();

    /// Fired when the user explicitly selects a candidate (the activation handoff).
    public event Action<Location>? LocationSelected;

    [ObservableProperty] private string _query = "";
    [ObservableProperty] private string? _searchMessage;
    [ObservableProperty] private bool _isSearching;

    partial void OnQueryChanged(string value)
    {
        if (value.Length < MinQueryLength)
        {
            Candidates.Clear();
            SearchMessage = null;
            return;
        }
        _scheduler.Schedule(DebounceDelay, () => RunSearchAsync(value));
    }

    private async Task RunSearchAsync(string query)
    {
        var seq = ++_latestSeq;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        IsSearching = true;
        try
        {
            var results = await _geocoder.Search(query, ct);
            if (seq != _latestSeq) return; // stale — a newer search superseded this one
            Candidates.Clear();
            foreach (var c in results) Candidates.Add(c);
            SearchMessage = results.Count == 0 ? $"No places found for “{query}”." : null;
        }
        catch (Exception) when (seq == _latestSeq)
        {
            Candidates.Clear();
            SearchMessage = "Couldn't search right now — check your connection and try again.";
        }
        catch (Exception)
        {
            // stale failure — ignore
        }
        finally
        {
            if (seq == _latestSeq) IsSearching = false;
        }
    }

    [RelayCommand]
    private void Select(LocationCandidate candidate)
    {
        var location = new Location(candidate.Name, candidate.Latitude, candidate.Longitude);
        Candidates.Clear();
        SearchMessage = null;
        LocationSelected?.Invoke(location);
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test --filter SearchViewModelTests`
Expected: PASS (6 tests).

- [ ] **Step 7: Commit**

```bash
git add src/WeatherApp.Core/Time src/WeatherApp.Core/ViewModels/SearchViewModel.cs tests/WeatherApp.Tests/Fakes tests/WeatherApp.Tests/ViewModels/SearchViewModelTests.cs
git commit -m "feat: add SearchViewModel with debounced, sequence-guarded Location Search"
```

---

## Task 7: WeatherViewModel

**Files:**
- Create: `src/WeatherApp.Core/ViewModels/WeatherLoadState.cs`, `WeatherViewModel.cs`
- Create: `tests/WeatherApp.Tests/Fakes/FakeWeatherProvider.cs`
- Test: `tests/WeatherApp.Tests/ViewModels/WeatherViewModelTests.cs`

- [ ] **Step 1: Write the load-state enum**

`src/WeatherApp.Core/ViewModels/WeatherLoadState.cs`:

```csharp
namespace WeatherApp.Core.ViewModels;

public enum WeatherLoadState { Idle, Loading, Loaded, Error }
```

- [ ] **Step 2: Write the fake Weather Provider**

`tests/WeatherApp.Tests/Fakes/FakeWeatherProvider.cs`:

```csharp
using WeatherApp.Core.Domain;
using WeatherApp.Core.Weather;

namespace WeatherApp.Tests.Fakes;

public sealed class FakeWeatherProvider : IWeatherProvider
{
    private readonly Func<Location, CurrentConditions> _factory;
    private readonly bool _throws;

    private FakeWeatherProvider(Func<Location, CurrentConditions> factory, bool throws)
    {
        _factory = factory;
        _throws = throws;
    }

    public static FakeWeatherProvider Returning(CurrentConditions c) => new(_ => c, false);
    public static FakeWeatherProvider Throwing() => new(_ => throw new HttpRequestException("boom"), true);

    public Task<CurrentConditions> GetCurrent(Location location, CancellationToken ct)
        => _throws ? throw new HttpRequestException("boom") : Task.FromResult(_factory(location));
}
```

- [ ] **Step 3: Write the failing tests**

`tests/WeatherApp.Tests/ViewModels/WeatherViewModelTests.cs`:

```csharp
using FluentAssertions;
using WeatherApp.Core.Domain;
using WeatherApp.Core.ViewModels;
using WeatherApp.Tests.Fakes;

namespace WeatherApp.Tests.ViewModels;

public class WeatherViewModelTests
{
    private static readonly Location London = new("London", 51.5, -0.12);

    [Fact]
    public async Task Load_success_sets_conditions_and_loaded_state()
    {
        var provider = FakeWeatherProvider.Returning(new CurrentConditions(20.4, 12.2, "Mainly clear"));
        var vm = new WeatherViewModel(provider);

        await vm.Load(London);

        vm.State.Should().Be(WeatherLoadState.Loaded);
        vm.Conditions!.TemperatureC.Should().Be(20.4);
        vm.LocationName.Should().Be("London");
    }

    [Fact]
    public async Task Load_failure_sets_error_state_and_message()
    {
        var vm = new WeatherViewModel(FakeWeatherProvider.Throwing());

        await vm.Load(London);

        vm.State.Should().Be(WeatherLoadState.Error);
        vm.ErrorMessage.Should().Contain("Couldn't load weather for London");
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test --filter WeatherViewModelTests`
Expected: FAIL — `WeatherViewModel` does not exist.

- [ ] **Step 5: Implement WeatherViewModel**

`src/WeatherApp.Core/ViewModels/WeatherViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using WeatherApp.Core.Domain;
using WeatherApp.Core.Weather;

namespace WeatherApp.Core.ViewModels;

public sealed partial class WeatherViewModel : ObservableObject
{
    private readonly IWeatherProvider _provider;

    public WeatherViewModel(IWeatherProvider provider) => _provider = provider;

    [ObservableProperty] private CurrentConditions? _conditions;
    [ObservableProperty] private string? _locationName;
    [ObservableProperty] private WeatherLoadState _state = WeatherLoadState.Idle;
    [ObservableProperty] private string? _errorMessage;

    /// Always fetches fresh (ADR-0001: no cache). Catches all failures into Error state.
    public async Task Load(Location location)
    {
        LocationName = location.Name;
        State = WeatherLoadState.Loading;
        ErrorMessage = null;
        try
        {
            Conditions = await _provider.GetCurrent(location, CancellationToken.None);
            State = WeatherLoadState.Loaded;
        }
        catch (Exception)
        {
            Conditions = null;
            ErrorMessage = $"Couldn't load weather for {location.Name}.";
            State = WeatherLoadState.Error;
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test --filter WeatherViewModelTests`
Expected: PASS (2 tests).

- [ ] **Step 7: Commit**

```bash
git add src/WeatherApp.Core/ViewModels/WeatherLoadState.cs src/WeatherApp.Core/ViewModels/WeatherViewModel.cs tests/WeatherApp.Tests/Fakes/FakeWeatherProvider.cs tests/WeatherApp.Tests/ViewModels/WeatherViewModelTests.cs
git commit -m "feat: add WeatherViewModel (fresh fetch, error state)"
```

---

## Task 8: MainViewModel — the activation handoff (Approach A)

**Files:**
- Create: `src/WeatherApp.Core/ViewModels/WeatherViewState.cs`, `MainViewModel.cs`
- Test: `tests/WeatherApp.Tests/ViewModels/MainViewModelTests.cs`

- [ ] **Step 1: Write the view-state enum**

`src/WeatherApp.Core/ViewModels/WeatherViewState.cs`:

```csharp
namespace WeatherApp.Core.ViewModels;

public enum WeatherViewState { Empty, Weather }
```

- [ ] **Step 2: Write the failing test**

`tests/WeatherApp.Tests/ViewModels/MainViewModelTests.cs`:

```csharp
using FluentAssertions;
using WeatherApp.Core.Domain;
using WeatherApp.Core.ViewModels;
using WeatherApp.Tests.Fakes;

namespace WeatherApp.Tests.ViewModels;

public class MainViewModelTests
{
    private static MainViewModel Build(out FakeGeocoder geo, out FakeWeatherProvider provider)
    {
        geo = new FakeGeocoder();
        provider = FakeWeatherProvider.Returning(new CurrentConditions(20.4, 12.2, "Mainly clear"));
        var search = new SearchViewModel(geo, new ManualDebounceScheduler());
        var weather = new WeatherViewModel(provider);
        return new MainViewModel(search, weather);
    }

    [Fact]
    public void Starts_in_empty_state()
    {
        var vm = Build(out _, out _);
        vm.ViewState.Should().Be(WeatherViewState.Empty);
    }

    [Fact]
    public async Task Selecting_a_candidate_activates_location_and_loads_weather()
    {
        var vm = Build(out _, out _);

        // Simulate the user picking a candidate in the child SearchViewModel.
        vm.Search.SelectCommand.Execute(new LocationCandidate("London", "England", "United Kingdom", 51.5, -0.12));
        await vm.LastActivation; // awaitable exposed for tests

        vm.ViewState.Should().Be(WeatherViewState.Weather);
        vm.Weather.State.Should().Be(WeatherLoadState.Loaded);
        vm.Weather.LocationName.Should().Be("London");
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test --filter MainViewModelTests`
Expected: FAIL — `MainViewModel` does not exist.

- [ ] **Step 4: Implement MainViewModel**

`src/WeatherApp.Core/ViewModels/MainViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using WeatherApp.Core.Domain;

namespace WeatherApp.Core.ViewModels;

/// Shell: owns the two child ViewModels and mediates the activation handoff
/// (Approach A). The children never reference each other.
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
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter MainViewModelTests`
Expected: PASS (2 tests).

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test`
Expected: all tests pass (smoke + map + geocoder + weather + 3 ViewModels).

- [ ] **Step 7: Commit**

```bash
git add src/WeatherApp.Core/ViewModels/WeatherViewState.cs src/WeatherApp.Core/ViewModels/MainViewModel.cs tests/WeatherApp.Tests/ViewModels/MainViewModelTests.cs
git commit -m "feat: add MainViewModel shell with activation handoff"
```

---

## Task 9: WPF shell — host wiring, DI, and views

> No unit tests for XAML/host wiring (UI layer); this is validated by the Tier-3 manual run in Task 10. Keep code-behind empty beyond `InitializeComponent` — all state lives in the ViewModels.

**Files:**
- Modify: `src/WeatherApp/WeatherApp.csproj` (enable host), `App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`

- [ ] **Step 1: Wire the generic host with DI in App.xaml.cs**

`src/WeatherApp/App.xaml.cs`:

```csharp
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WeatherApp.Core.Geocoding;
using WeatherApp.Core.Time;
using WeatherApp.Core.ViewModels;
using WeatherApp.Core.Weather;

namespace WeatherApp;

public partial class App : Application
{
    private readonly IHost _host = Host.CreateDefaultBuilder()
        .ConfigureServices(services =>
        {
            services.AddHttpClient<IGeocoder, OpenMeteoGeocoder>(c =>
                c.BaseAddress = new Uri("https://geocoding-api.open-meteo.com/"));
            services.AddHttpClient<IWeatherProvider, OpenMeteoWeatherProvider>(c =>
                c.BaseAddress = new Uri("https://api.open-meteo.com/"));

            services.AddSingleton<WmoConditionMap>();
            services.AddTransient<IDebounceScheduler, DebounceScheduler>();
            services.AddTransient<SearchViewModel>();
            services.AddTransient<WeatherViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<MainWindow>();
        })
        .Build();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = _host.Services.GetRequiredService<MainWindow>();
        window.DataContext = _host.Services.GetRequiredService<MainViewModel>();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.Dispose();
        base.OnExit(e);
    }
}
```

> Note: `AddHttpClient<IWeatherProvider, OpenMeteoWeatherProvider>` injects an `HttpClient` but `OpenMeteoWeatherProvider` also needs `WmoConditionMap`; the typed-client registration resolves additional constructor params from DI, so registering `WmoConditionMap` as a service (above) is sufficient.

- [ ] **Step 2: Ensure App.xaml has no StartupUri**

`src/WeatherApp/App.xaml` (remove any `StartupUri` attribute so `OnStartup` controls the window):

```xml
<Application x:Class="WeatherApp.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources/>
</Application>
```

- [ ] **Step 3: Build the MainWindow view**

`src/WeatherApp/MainWindow.xaml`:

```xml
<Window x:Class="WeatherApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:WeatherApp.Core.ViewModels;assembly=WeatherApp.Core"
        Title="Weather" Height="480" Width="380">
    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- Search box (always visible) -->
        <TextBox Grid.Row="0"
                 Text="{Binding Search.Query, UpdateSourceTrigger=PropertyChanged, Delay=0}"
                 FontSize="16" Padding="6"/>

        <!-- Candidate panel + search messages (driven by SearchViewModel) -->
        <StackPanel Grid.Row="1" Margin="0,8,0,0">
            <TextBlock Text="{Binding Search.SearchMessage}"
                       Visibility="{Binding Search.SearchMessage, Converter={StaticResource NullToCollapsed}}"
                       Foreground="#A33" TextWrapping="Wrap"/>
            <ListBox ItemsSource="{Binding Search.Candidates}" MaxHeight="180">
                <ListBox.ItemTemplate>
                    <DataTemplate>
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="{Binding Name}" FontWeight="Bold"/>
                            <TextBlock Text="{Binding Admin1}" Margin="6,0,0,0" Foreground="#666"/>
                            <TextBlock Text="{Binding Country}" Margin="6,0,0,0" Foreground="#999"/>
                        </StackPanel>
                    </DataTemplate>
                </ListBox.ItemTemplate>
                <ListBox.InputBindings>
                    <MouseBinding MouseAction="LeftDoubleClick"
                                  Command="{Binding Search.SelectCommand}"
                                  CommandParameter="{Binding SelectedItem, RelativeSource={RelativeSource AncestorType=ListBox}}"/>
                </ListBox.InputBindings>
            </ListBox>
        </StackPanel>

        <!-- Body: empty prompt OR weather -->
        <Grid Grid.Row="2" Margin="0,16,0,0">
            <TextBlock Text="Search for a place to see its weather."
                       Foreground="#888" FontSize="14"
                       Visibility="{Binding ViewState, Converter={StaticResource EmptyToVisible}}"/>

            <StackPanel Visibility="{Binding ViewState, Converter={StaticResource WeatherToVisible}}">
                <TextBlock Text="{Binding Weather.LocationName}" FontSize="22" FontWeight="Bold"/>
                <TextBlock Text="Loading…"
                           Visibility="{Binding Weather.State, Converter={StaticResource LoadingToVisible}}"/>
                <TextBlock Text="{Binding Weather.ErrorMessage}" Foreground="#A33" TextWrapping="Wrap"
                           Visibility="{Binding Weather.ErrorMessage, Converter={StaticResource NullToCollapsed}}"/>
                <StackPanel Visibility="{Binding Weather.State, Converter={StaticResource LoadedToVisible}}">
                    <TextBlock FontSize="40">
                        <Run Text="{Binding Weather.Conditions.TemperatureC, Mode=OneWay}"/><Run Text=" °C"/>
                    </TextBlock>
                    <TextBlock FontSize="16" Text="{Binding Weather.Conditions.Condition}"/>
                    <TextBlock FontSize="14" Foreground="#666">
                        <Run Text="Wind "/><Run Text="{Binding Weather.Conditions.WindSpeedKmh, Mode=OneWay}"/><Run Text=" km/h"/>
                    </TextBlock>
                </StackPanel>
            </StackPanel>
        </Grid>
    </Grid>
</Window>
```

> The `{StaticResource …}` converters (`NullToCollapsed`, `EmptyToVisible`, `WeatherToVisible`, `LoadingToVisible`, `LoadedToVisible`) are simple `IValueConverter`s. Create them in Step 4 and register them in `App.xaml`/`Window.Resources`.

- [ ] **Step 4: Add value converters**

`src/WeatherApp/Converters.cs`:

```csharp
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WeatherApp.Core.ViewModels;

namespace WeatherApp;

/// Visible when the bound value is non-null / non-empty string; else Collapsed.
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is null || (value is string s && string.IsNullOrEmpty(s))
            ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// Visible when ViewState == Empty.
public sealed class EmptyStateToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is WeatherViewState.Empty ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// Visible when ViewState == Weather.
public sealed class WeatherStateToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is WeatherViewState.Weather ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// Visible when WeatherLoadState matches the state named in ConverterParameter
/// (e.g. "Loading", "Loaded").
public sealed class LoadStateToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is WeatherLoadState state && p is string name
        && Enum.TryParse<WeatherLoadState>(name, out var want) && state == want
            ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
```

Register them in `src/WeatherApp/MainWindow.xaml` as `Window.Resources` (keys must match the `{StaticResource …}` names used in Step 3 — note `LoadingToVisible` / `LoadedToVisible` reuse the one `LoadStateToVisibleConverter` via `ConverterParameter`, so update those two bindings in Step 3 to pass `ConverterParameter=Loading` / `ConverterParameter=Loaded`):

```xml
    <Window.Resources>
        <local:NullToCollapsedConverter x:Key="NullToCollapsed"/>
        <local:EmptyStateToVisibleConverter x:Key="EmptyToVisible"/>
        <local:WeatherStateToVisibleConverter x:Key="WeatherToVisible"/>
        <local:LoadStateToVisibleConverter x:Key="LoadStateToVisible"/>
    </Window.Resources>
```

Add `xmlns:local="clr-namespace:WeatherApp"` to the `<Window>` element, and in Step 3 change the two load-state bindings to:
`Visibility="{Binding Weather.State, Converter={StaticResource LoadStateToVisible}, ConverterParameter=Loading}"` and `…ConverterParameter=Loaded`.

- [ ] **Step 5: Keep MainWindow.xaml.cs minimal**

`src/WeatherApp/MainWindow.xaml.cs`:

```csharp
using System.Windows;

namespace WeatherApp;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}
```

- [ ] **Step 6: Build the whole solution**

Run: `dotnet build`
Expected: succeeds (the WPF project builds on Windows).

- [ ] **Step 7: Commit**

```bash
git add src/WeatherApp
git commit -m "feat: wire WPF shell (generic host, DI, search + weather views)"
```

---

## Task 10: Tier-2 live tests + Tier-3 manual verification

**Files:**
- Create: `tests/WeatherApp.Tests/Live/OpenMeteoLiveTests.cs`

- [ ] **Step 1: Add Tier-2 live tests behind a trait (scheduled, not every-commit)**

`tests/WeatherApp.Tests/Live/OpenMeteoLiveTests.cs`:

```csharp
using FluentAssertions;
using WeatherApp.Core.Domain;
using WeatherApp.Core.Geocoding;
using WeatherApp.Core.Weather;

namespace WeatherApp.Tests.Live;

/// Tier-2: real calls to Open-Meteo. Confirm the fixtures still match the live
/// contract (fields present, types) — never assert on volatile weather values.
[Trait("Tier", "Live")]
public class OpenMeteoLiveTests
{
    [Fact]
    public async Task Geocoder_returns_candidates_for_London()
    {
        using var http = new HttpClient { BaseAddress = new Uri("https://geocoding-api.open-meteo.com/") };
        var geocoder = new OpenMeteoGeocoder(http);

        var results = await geocoder.Search("London", CancellationToken.None);

        results.Should().NotBeEmpty();
        results[0].Name.Should().NotBeNullOrEmpty();
        results[0].Country.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Weather_provider_returns_conditions_for_coords()
    {
        using var http = new HttpClient { BaseAddress = new Uri("https://api.open-meteo.com/") };
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());

        var conditions = await provider.GetCurrent(new Location("London", 51.5085, -0.1257), CancellationToken.None);

        conditions.Condition.Should().NotBeNullOrEmpty();   // shape, not value
    }
}
```

Run only on schedule: `dotnet test --filter Tier=Live`.
Tier-1 (every commit) excludes them: `dotnet test --filter Tier!=Live`.

- [ ] **Step 2: Tier-3 manual verification checklist (on Windows)**

Run: `dotnet run --project src/WeatherApp`
Verify:
1. App opens to the empty state with the "Search for a place…" prompt.
2. Typing `Lo` (2+ chars) shows candidates after a brief pause; typing one char shows none.
3. Typing `London` shows multiple candidates (UK / Canada / US) with region + country.
4. Double-clicking a candidate switches to the weather view showing temperature (°C), Condition text, and wind (km/h) for that place.
5. Typing gibberish (`zzzznotaplace`) shows "No places found".
6. Disconnecting the network and searching shows "Couldn't search right now…"; selecting a place with no network shows "Couldn't load weather for …".

- [ ] **Step 3: Commit**

```bash
git add tests/WeatherApp.Tests/Live/OpenMeteoLiveTests.cs
git commit -m "test: add Tier-2 live Open-Meteo contract tests"
```

---

## Self-review notes (for the gauntlet)

- **Spec coverage:** empty state (T9), debounced sequence-guarded search (T6), zero-results & Geocoder-failure messages (T6), explicit candidate pick → activation (T6/T8), fresh weather fetch + Condition (T5/T7), metric units (T5), error states without Retry (T7), no persistence/cache (nothing persists — ADR-0001 honoured). ✓
- **Seam coverage:** Seam 1 → Task 4 (contract verbatim in the task header; (d) recorded-replay incl. the absent-`results` and nullable-`admin1` cases). Seam 2 → Task 5 (contract verbatim; (d) recorded-replay + metric-param assertion). Seam 3 (host-OS/locale coordinate formatting) → Task 5 (contract in the task header; (d) the `Formats_coordinates_invariantly_under_comma_decimal_locale` test that forces `de-DE` and asserts the `.`-decimal wire form). Seams 1–2 (e)-grounded live on 2026-06-29 (captured as fixtures); Seam 3 (e)-grounded in the .NET CultureInfo docs + that same live call's `.`-decimal request. The in-process activation handoff (excluded as a taxonomy seam) is still covered by Task 8's integration test. ✓
- **Overriding Principle #1 (no secrets):** no keys/tokens anywhere; fixtures contain only public weather data. ✓
- **Host-OS/locale seam (Seam 3):** `InvariantCulture` on coordinate formatting (Task 5) pre-empts a locale-decimal-separator bug. This is now written as **Seam 3** in the Spec's inventory with a falsifiable (c) (invariant `.`-decimal wire form, byte-identical across locales) and its own (d) — the `de-DE`-forcing round-trip test in Task 5 — rather than only living in code. F3 (the first filesystem/OS-touching Feature) still carries the broader platform-matrix obligation.
- **fix-feature-docs (2026-06-30) — finding → fix → closure map** (answers the failed `/feature-doc-gauntlet` run recorded in the Spec sign-off; 2 findings → 1 root cause):
  - *Root cause:* the host-OS/locale coordinate-formatting boundary was crossed in code (Task 5 `InvariantCulture`) but never written as a falsifiable seam contract.
  - *Fix (joint):* Spec — added **Seam 3** (host-OS/runtime) with full (a)–(e), and tightened **Seam 2 (c)** to state the invariant `.`-decimal coordinate form; updated the inventory preamble from "Two … seams" to three. Plan — Task 5 header now covers Seam 3 (c); added the `Formats_coordinates_invariantly_under_comma_decimal_locale` test as Seam 3 (d); updated this self-review. *Closure:* the inventory now carries a Seam 3 row (grep `Seam 3`), and Seam 2 (c) states the invariant-decimal rule.
  - *Non-gating observation swept (human decision 2026-06-30):* per-call Open-Meteo request logging is **deferred** for F1 (recorded in the Spec's Out-of-scope) — the host logging substrate stays wired so it can be picked up cheaply later. The other observations (ADR-0001 keep-last-good is an F4 concern; cosmetic ViewModel naming) need no change.
```