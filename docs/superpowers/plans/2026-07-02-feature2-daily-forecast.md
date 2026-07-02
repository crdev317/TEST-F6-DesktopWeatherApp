# Feature 2 — 7-day daily Forecast Implementation Plan

> **For agentic workers:** Do NOT implement this plan directly. It must first pass `/feature-doc-gauntlet` in a clean session, then be broken into stories by `/enate-to-stories`; AFK implementation happens per-story from there. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The weather view gains a 7-day daily **Forecast** strip (per-day high/low and **Condition**, today + 6, Location-local days) fetched in the same Open-Meteo call as **Current Conditions**.

**Architecture:** One combined `current`+`daily` GET (with `timezone=auto`) behind the Weather Provider client. Two new domain records — `DailyForecast` (one day) and `WeatherReport` (the composite one fetch returns). `IWeatherProvider.GetCurrent` is superseded by `GetWeather` via an **additive migration** (add the new method, move consumers, delete the old one last) so every commit compiles and stays green. `WeatherViewModel` gains a `Forecast` property; its `WeatherLoadState` machine is unchanged; load stays atomic (a malformed `daily` block fails the whole load). The view adds a horizontal strip with a positional "Today" label (no system-clock comparison).

**Tech Stack:** WPF/.NET 8, C#; `CommunityToolkit.Mvvm`; typed `HttpClient` + `System.Text.Json` / `System.Net.Http.Json`; tests with `xUnit` + `FluentAssertions` (all already in use — no new dependencies).

**Context references:**
- Spec: `docs/superpowers/specs/2026-07-02-feature2-daily-forecast-design.md`
- `Context.MD` (domain glossary; `business-domain-context.md` is its identical sibling)
- `Technical-Context.MD` (Overriding Principles that apply: **#1 No secrets in source** — F2 adds none; Open-Meteo stays keyless)
- ADRs: `docs/adr/0001-persist-location-only-never-cache-weather.md` (fetch fresh on every activation; the `WeatherReport` lives only in the ViewModel's in-session state, never persisted)
- F1 spec (the seams F2 extends): `docs/superpowers/specs/2026-06-29-feature1-current-weather-design.md`

> An AFK Developer Agent picking up this plan MUST load every file in the Context references block before writing code.

---

## Seam coverage map

| Spec seam | (c) contract named in | (d) proof written in |
|---|---|---|
| **Seam 1** — Weather Provider client ↔ Open-Meteo forecast API (current + daily) | Task 2 (happy path + request params), Task 3 (malformed/absent daily) | Task 2 Steps 1–2 (recorded-replay over the 2026-07-02 live capture), Task 3 (failure fixtures edited from the real payload), Task 6 (Tier-2 live envelope re-check) |
| **Seam 2** — Daily date parse ↔ host locale | Task 4 | Task 4 (de-DE culture-forcing test over the recorded fixture — the F1 Seam 3 test shape, pointed inbound) |

Both seams were **proven live during brainstorming on 2026-07-02** (read-only GETs, London + Tokyo — see the spec's Seam inventory (e) fields). Every task below encodes a *known* contract, not a guess. Auth: none — inherited by reference from the F1 pin, re-confirmed 2026-07-02.

## File structure

```
src/WeatherApp.Core/
  Domain/DailyForecast.cs                      (create — one day of the Forecast)
  Domain/WeatherReport.cs                      (create — the composite one fetch returns)
  Weather/IWeatherProvider.cs                  (modify — GetWeather added T2, GetCurrent removed T6)
  Weather/OpenMeteoWeatherProvider.cs          (modify — same)
  Weather/WeatherDtos.cs                       (modify — DailyBlock DTO)
  ViewModels/WeatherViewModel.cs               (modify — Forecast property, Load → GetWeather)
src/WeatherApp/
  Converters.cs                                (modify — DayLabelConverter)
  MainWindow.xaml                              (modify — the Forecast strip)
tests/WeatherApp.Tests/
  Domain/WeatherReportTests.cs                 (create)
  Fixtures/forecast-daily-london.json          (create — the 2026-07-02 live capture, verbatim)
  Fixtures/forecast-daily-ragged.json          (create — edited from the real payload: one array truncated)
  Fixtures/forecast-daily-three.json           (create — edited from the real payload: 3 equal-length days)
  Weather/OpenMeteoWeatherProviderTests.cs     (modify — GetWeather tests T2–T4; GetCurrent tests retired T6)
  ViewModels/WeatherViewModelTests.cs          (modify — Forecast exposure + failure clearing)
  Fakes/FakeWeatherProvider.cs                 (modify — implements GetWeather T2, Returning(WeatherReport) T5, GetCurrent dropped T6)
  Live/OpenMeteoLiveTests.cs                   (modify — forecast call asserts the daily envelope, T6)
```

`Fixtures/forecast-london.json` (the F1 capture, which has **no** `daily` block) is kept and becomes the absent-`daily` failure fixture in Task 3 — a real payload, not a hand-written one.

**Test commands** (from `CLAUDE.md`): every-commit Tier-1 run is `dotnet test WeatherApp.sln --filter Tier!=Live`. On a non-Windows dev box the WPF project (`net8.0-windows`) won't build — use `dotnet test tests/WeatherApp.Tests/WeatherApp.Tests.csproj --filter Tier!=Live` for Tasks 1–6 (the tests project references only `WeatherApp.Core`, `net8.0`); Task 7 (XAML) needs a Windows build.

---

## Task 1: Domain records — `DailyForecast` and `WeatherReport`

**Files:**
- Create: `src/WeatherApp.Core/Domain/DailyForecast.cs`
- Create: `src/WeatherApp.Core/Domain/WeatherReport.cs`
- Test: `tests/WeatherApp.Tests/Domain/WeatherReportTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using WeatherApp.Core.Domain;

namespace WeatherApp.Tests.Domain;

public class WeatherReportTests
{
    [Fact]
    public void Daily_forecast_records_carry_date_highs_lows_and_condition()
    {
        var day = new DailyForecast(new DateOnly(2026, 7, 2), 24.9, 16.9, "Overcast");

        day.Date.Should().Be(new DateOnly(2026, 7, 2));
        day.HighC.Should().Be(24.9);
        day.LowC.Should().Be(16.9);
        day.Condition.Should().Be("Overcast");
    }

    [Fact]
    public void Weather_report_pairs_current_conditions_with_the_daily_forecast()
    {
        var current = new CurrentConditions(21.6, 16.6, "Partly cloudy");
        var days = new List<DailyForecast> { new(new DateOnly(2026, 7, 2), 24.9, 16.9, "Overcast") };

        var report = new WeatherReport(current, days);

        report.Current.Should().Be(current);
        report.Daily.Should().HaveCount(1);
        report.Daily[0].Condition.Should().Be("Overcast");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/WeatherApp.Tests/WeatherApp.Tests.csproj --filter Tier!=Live`
Expected: FAIL to compile — `DailyForecast` and `WeatherReport` do not exist.

- [ ] **Step 3: Write the records**

`src/WeatherApp.Core/Domain/DailyForecast.cs`:

```csharp
namespace WeatherApp.Core.Domain;

/// One day of the Forecast for the active Location, in fixed metric units.
/// Date is the Location-local calendar day (the provider aggregates with
/// timezone=auto — spec Seam 1).
public sealed record DailyForecast(
    DateOnly Date,
    double HighC,
    double LowC,
    string Condition);
```

`src/WeatherApp.Core/Domain/WeatherReport.cs`:

```csharp
namespace WeatherApp.Core.Domain;

/// The composite one Weather Provider fetch returns: Current Conditions plus
/// the daily Forecast (expected 7 entries — today + 6). A code-level pairing,
/// not a glossary term (spec decision 2026-07-02). Never persisted (ADR-0001).
public sealed record WeatherReport(
    CurrentConditions Current,
    IReadOnlyList<DailyForecast> Daily);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/WeatherApp.Tests/WeatherApp.Tests.csproj --filter Tier!=Live`
Expected: PASS (all existing tests still green).

- [ ] **Step 5: Commit**

```bash
git add src/WeatherApp.Core/Domain/DailyForecast.cs src/WeatherApp.Core/Domain/WeatherReport.cs tests/WeatherApp.Tests/Domain/WeatherReportTests.cs
git commit -m "feat: add DailyForecast and WeatherReport domain records (F2)"
```

---

## Task 2: `GetWeather` — the combined current+daily fetch (Seam 1 happy path)

Adds `GetWeather` **beside** `GetCurrent` (removed in Task 6) so every consumer keeps compiling.

**Seam 1 (c) contract this task encodes** (spec Seam inventory, proven live 2026-07-02): `GET /v1/forecast?latitude=<lat>&longitude=<lon>&current=temperature_2m,weather_code,wind_speed_10m&daily=temperature_2m_max,temperature_2m_min,weather_code&temperature_unit=celsius&wind_speed_unit=kmh&timezone=auto`, no auth; success body carries a `daily` object of four equal-length parallel arrays (`time[]` ISO `yyyy-MM-dd` strings, `temperature_2m_max[]`, `temperature_2m_min[]` numbers, `weather_code[]` WMO ints — all non-null because requested, observed length 7), `daily[time][0]` = the Location's current day, plus the F1-pinned `current` block.

**Files:**
- Create: `tests/WeatherApp.Tests/Fixtures/forecast-daily-london.json`
- Modify: `src/WeatherApp.Core/Weather/WeatherDtos.cs`
- Modify: `src/WeatherApp.Core/Weather/IWeatherProvider.cs`
- Modify: `src/WeatherApp.Core/Weather/OpenMeteoWeatherProvider.cs`
- Modify: `tests/WeatherApp.Tests/Fakes/FakeWeatherProvider.cs`
- Test: `tests/WeatherApp.Tests/Weather/OpenMeteoWeatherProviderTests.cs`

- [ ] **Step 1: Add the recorded fixture — the real payload captured live on 2026-07-02**

`tests/WeatherApp.Tests/Fixtures/forecast-daily-london.json` (verbatim from `GET api.open-meteo.com/v1/forecast?latitude=51.5085&longitude=-0.1257&current=temperature_2m,weather_code,wind_speed_10m&daily=temperature_2m_max,temperature_2m_min,weather_code&temperature_unit=celsius&wind_speed_unit=kmh&timezone=auto` — see spec Seam 1 (e); the csproj's `Fixtures/**/*.json` glob copies it to output automatically):

```json
{"latitude":51.5,"longitude":-0.25,"generationtime_ms":0.4119873046875,"utc_offset_seconds":3600,"timezone":"Europe/London","timezone_abbreviation":"GMT+1","elevation":23.0,"current_units":{"time":"iso8601","interval":"seconds","temperature_2m":"°C","weather_code":"wmo code","wind_speed_10m":"km/h"},"current":{"time":"2026-07-02T10:30","interval":900,"temperature_2m":21.6,"weather_code":2,"wind_speed_10m":16.6},"daily_units":{"time":"iso8601","temperature_2m_max":"°C","temperature_2m_min":"°C","weather_code":"wmo code"},"daily":{"time":["2026-07-02","2026-07-03","2026-07-04","2026-07-05","2026-07-06","2026-07-07","2026-07-08"],"temperature_2m_max":[24.9,26.0,25.6,28.4,27.5,25.6,29.0],"temperature_2m_min":[16.9,15.1,16.2,16.2,16.1,19.3,19.2],"weather_code":[3,3,3,3,3,3,0]}}
```

- [ ] **Step 2: Write the failing tests**

Add to `tests/WeatherApp.Tests/Weather/OpenMeteoWeatherProviderTests.cs`:

```csharp
    // ---- F2: GetWeather (combined current + daily — spec Seam 1) ----

    [Fact]
    public async Task GetWeather_maps_current_and_daily_into_weather_report()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("forecast-daily-london.json"), out _);
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());

        var report = await provider.GetWeather(London, CancellationToken.None);

        report.Current.TemperatureC.Should().Be(21.6);
        report.Current.WindSpeedKmh.Should().Be(16.6);
        report.Current.Condition.Should().Be("Partly cloudy");     // weather_code 2

        report.Daily.Should().HaveCount(7);                        // today + 6
        report.Daily[0].Date.Should().Be(new DateOnly(2026, 7, 2)); // daily.time[0] = the Location's current day
        report.Daily[0].HighC.Should().Be(24.9);
        report.Daily[0].LowC.Should().Be(16.9);
        report.Daily[0].Condition.Should().Be("Overcast");         // weather_code 3
        report.Daily[6].Date.Should().Be(new DateOnly(2026, 7, 8));
        report.Daily[6].Condition.Should().Be("Clear sky");        // weather_code 0
    }

    [Fact]
    public async Task GetWeather_sends_daily_timezone_metric_params_and_invariant_coordinates()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("forecast-daily-london.json"), out var handler);
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());

        await provider.GetWeather(London, CancellationToken.None);

        var query = handler.LastRequestUri!.Query;
        query.Should().Contain("current=temperature_2m,weather_code,wind_speed_10m");   // the F1 half, regression
        query.Should().Contain("daily=temperature_2m_max,temperature_2m_min,weather_code");
        query.Should().Contain("timezone=auto");                                        // Location-local days
        query.Should().Contain("temperature_unit=celsius");
        query.Should().Contain("wind_speed_unit=kmh");
        query.Should().Contain("latitude=51.5085");                                     // InvariantCulture decimal point
    }
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/WeatherApp.Tests/WeatherApp.Tests.csproj --filter Tier!=Live`
Expected: FAIL to compile — `IWeatherProvider` has no `GetWeather`.

- [ ] **Step 4: Add the `daily` DTO**

`src/WeatherApp.Core/Weather/WeatherDtos.cs` becomes:

```csharp
using System.Text.Json.Serialization;

namespace WeatherApp.Core.Weather;

internal sealed class ForecastResponse
{
    [JsonPropertyName("current")] public CurrentBlock? Current { get; set; }
    [JsonPropertyName("daily")] public DailyBlock? Daily { get; set; }
}

internal sealed class CurrentBlock
{
    [JsonPropertyName("temperature_2m")] public double Temperature { get; set; }
    [JsonPropertyName("weather_code")] public int WeatherCode { get; set; }
    [JsonPropertyName("wind_speed_10m")] public double WindSpeed { get; set; }
}

/// Open-Meteo's daily block: four parallel arrays, index-aligned per day
/// (spec Seam 1). Nullability is part of the contract — any array may be
/// absent in a malformed response; the provider validates before zipping.
internal sealed class DailyBlock
{
    [JsonPropertyName("time")] public List<string>? Time { get; set; }
    [JsonPropertyName("temperature_2m_max")] public List<double>? TemperatureMax { get; set; }
    [JsonPropertyName("temperature_2m_min")] public List<double>? TemperatureMin { get; set; }
    [JsonPropertyName("weather_code")] public List<int>? WeatherCode { get; set; }
}
```

- [ ] **Step 5: Add `GetWeather` to the interface (keeping `GetCurrent` until Task 6)**

`src/WeatherApp.Core/Weather/IWeatherProvider.cs` becomes:

```csharp
using WeatherApp.Core.Domain;

namespace WeatherApp.Core.Weather;

public interface IWeatherProvider
{
    /// F1 shape — superseded by GetWeather; removed once all consumers migrate (F2 Task 6).
    Task<CurrentConditions> GetCurrent(Location location, CancellationToken ct);

    /// One combined fetch: Current Conditions + the 7-day daily Forecast,
    /// aggregated in the Location's own timezone (timezone=auto).
    Task<WeatherReport> GetWeather(Location location, CancellationToken ct);
}
```

- [ ] **Step 6: Implement `GetWeather` in the provider**

Add to `src/WeatherApp.Core/Weather/OpenMeteoWeatherProvider.cs` (below `GetCurrent`, which stays untouched until Task 6):

```csharp
    public async Task<WeatherReport> GetWeather(Location location, CancellationToken ct)
    {
        // Same fail-closed bounding as GetCurrent: finite timeout linked to the
        // caller's token, so a hung endpoint surfaces as the inline error state.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_timeout);
        var linked = timeoutCts.Token;

        // InvariantCulture so the decimal point is "." regardless of host locale
        // (F1 Seam 3, unchanged — proven by the de-DE test).
        var lat = location.Latitude.ToString(CultureInfo.InvariantCulture);
        var lon = location.Longitude.ToString(CultureInfo.InvariantCulture);
        var url = $"v1/forecast?latitude={lat}&longitude={lon}" +
                  "&current=temperature_2m,weather_code,wind_speed_10m" +
                  "&daily=temperature_2m_max,temperature_2m_min,weather_code" +
                  "&temperature_unit=celsius&wind_speed_unit=kmh&timezone=auto";

        using var response = await _http.GetAsync(url, linked);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<ForecastResponse>(cancellationToken: linked);
        var current = dto?.Current
            ?? throw new InvalidOperationException("Forecast response had no `current` block.");
        var daily = dto.Daily
            ?? throw new InvalidOperationException("Forecast response had no `daily` block.");

        var days = new List<DailyForecast>(daily.Time!.Count);
        for (var i = 0; i < daily.Time.Count; i++)
        {
            // Seam 2: invariant parse — the host locale has zero influence on the
            // parsed date (the wire format is pinned ISO yyyy-MM-dd, spec Seam 1).
            var date = DateOnly.ParseExact(daily.Time[i], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            days.Add(new DailyForecast(
                date,
                daily.TemperatureMax![i],
                daily.TemperatureMin![i],
                _conditions.ToCondition(daily.WeatherCode![i])));
        }

        var conditions = new CurrentConditions(
            current.Temperature,
            current.WindSpeed,
            _conditions.ToCondition(current.WeatherCode));
        return new WeatherReport(conditions, days);
    }
```

(The null-forgiving `!` on the arrays is deliberate here — the ragged/absent-array *validation* is Task 3's red-green cycle; this step implements only what the happy-path tests demand.)

- [ ] **Step 7: Implement `GetWeather` on the fake so the tests project compiles**

Add to `tests/WeatherApp.Tests/Fakes/FakeWeatherProvider.cs`:

```csharp
    /// A canned week (today + 6 from the F2 fixture's first day) so existing
    /// Returning(CurrentConditions) callers keep working against GetWeather.
    private static readonly IReadOnlyList<DailyForecast> CannedWeek =
        Enumerable.Range(0, 7)
            .Select(i => new DailyForecast(new DateOnly(2026, 7, 2).AddDays(i), 24.9, 16.9, "Overcast"))
            .ToList();

    public Task<WeatherReport> GetWeather(Location location, CancellationToken ct)
        => _throws ? throw _thrown : Task.FromResult(new WeatherReport(_factory(location), CannedWeek));
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test tests/WeatherApp.Tests/WeatherApp.Tests.csproj --filter Tier!=Live`
Expected: PASS — the two new tests green, every F1 test untouched and green.

- [ ] **Step 9: Commit**

```bash
git add src/WeatherApp.Core/Weather/ tests/WeatherApp.Tests/Fixtures/forecast-daily-london.json tests/WeatherApp.Tests/Weather/OpenMeteoWeatherProviderTests.cs tests/WeatherApp.Tests/Fakes/FakeWeatherProvider.cs
git commit -m "feat: GetWeather — combined current+daily Open-Meteo fetch (F2 Seam 1)"
```

---

## Task 3: The malformed-`daily` contract — atomic failure, tolerant count

**Seam 1 (c) contract this task encodes** (spec error-handling table): `daily` absent despite being requested, or any array missing, or arrays of unequal length ⇒ the **whole load fails** (`InvalidOperationException`, caught at the ViewModel boundary like every provider failure); equal-length arrays of a count other than 7 ⇒ **tolerated**, that many entries.

**Files:**
- Create: `tests/WeatherApp.Tests/Fixtures/forecast-daily-ragged.json`
- Create: `tests/WeatherApp.Tests/Fixtures/forecast-daily-three.json`
- Modify: `src/WeatherApp.Core/Weather/OpenMeteoWeatherProvider.cs`
- Test: `tests/WeatherApp.Tests/Weather/OpenMeteoWeatherProviderTests.cs`

- [ ] **Step 1: Add the failure fixtures — edited from the real 2026-07-02 payload, not hand-written**

`tests/WeatherApp.Tests/Fixtures/forecast-daily-ragged.json` — the London capture with `temperature_2m_min` truncated to 6 entries (the one edit):

```json
{"latitude":51.5,"longitude":-0.25,"generationtime_ms":0.4119873046875,"utc_offset_seconds":3600,"timezone":"Europe/London","timezone_abbreviation":"GMT+1","elevation":23.0,"current_units":{"time":"iso8601","interval":"seconds","temperature_2m":"°C","weather_code":"wmo code","wind_speed_10m":"km/h"},"current":{"time":"2026-07-02T10:30","interval":900,"temperature_2m":21.6,"weather_code":2,"wind_speed_10m":16.6},"daily_units":{"time":"iso8601","temperature_2m_max":"°C","temperature_2m_min":"°C","weather_code":"wmo code"},"daily":{"time":["2026-07-02","2026-07-03","2026-07-04","2026-07-05","2026-07-06","2026-07-07","2026-07-08"],"temperature_2m_max":[24.9,26.0,25.6,28.4,27.5,25.6,29.0],"temperature_2m_min":[16.9,15.1,16.2,16.2,16.1,19.3],"weather_code":[3,3,3,3,3,3,0]}}
```

`tests/WeatherApp.Tests/Fixtures/forecast-daily-three.json` — the London capture with all four arrays trimmed to the first 3 days (equal-length, non-7):

```json
{"latitude":51.5,"longitude":-0.25,"generationtime_ms":0.4119873046875,"utc_offset_seconds":3600,"timezone":"Europe/London","timezone_abbreviation":"GMT+1","elevation":23.0,"current_units":{"time":"iso8601","interval":"seconds","temperature_2m":"°C","weather_code":"wmo code","wind_speed_10m":"km/h"},"current":{"time":"2026-07-02T10:30","interval":900,"temperature_2m":21.6,"weather_code":2,"wind_speed_10m":16.6},"daily_units":{"time":"iso8601","temperature_2m_max":"°C","temperature_2m_min":"°C","weather_code":"wmo code"},"daily":{"time":["2026-07-02","2026-07-03","2026-07-04"],"temperature_2m_max":[24.9,26.0,25.6],"temperature_2m_min":[16.9,15.1,16.2],"weather_code":[3,3,3]}}
```

(The absent-`daily` case reuses `forecast-london.json` — the F1 live capture, which genuinely has no `daily` block.)

- [ ] **Step 2: Write the failing tests**

Add to `tests/WeatherApp.Tests/Weather/OpenMeteoWeatherProviderTests.cs`:

```csharp
    [Fact]
    public async Task GetWeather_throws_when_daily_block_is_absent()
    {
        // forecast-london.json is the F1 capture: a real payload with no `daily`.
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("forecast-london.json"), out _);
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());

        var act = () => provider.GetWeather(London, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*daily*");
    }

    [Fact]
    public async Task GetWeather_throws_when_daily_arrays_have_mismatched_lengths()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("forecast-daily-ragged.json"), out _);
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());

        var act = () => provider.GetWeather(London, CancellationToken.None);

        // Never a strip zipped from ragged arrays — the whole load fails (spec).
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*daily*");
    }

    [Fact]
    public async Task GetWeather_tolerates_an_equal_length_non_seven_daily_block()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("forecast-daily-three.json"), out _);
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());

        var report = await provider.GetWeather(London, CancellationToken.None);

        report.Daily.Should().HaveCount(3);   // renders what came; 7 is expected, not hard-coded
        report.Daily[2].Date.Should().Be(new DateOnly(2026, 7, 4));
    }
```

- [ ] **Step 3: Run tests to verify the new failure cases fail**

Run: `dotnet test tests/WeatherApp.Tests/WeatherApp.Tests.csproj --filter Tier!=Live`
Expected: `GetWeather_throws_when_daily_arrays_have_mismatched_lengths` FAILS (today it dies with `ArgumentOutOfRangeException` from the naive zip, not the contract's `InvalidOperationException`). The absent-`daily` and three-day tests may already pass — the guard still gets written as one piece.

- [ ] **Step 4: Add the validation guard**

In `src/WeatherApp.Core/Weather/OpenMeteoWeatherProvider.cs`, inside `GetWeather`, replace the block from `var days = new List<DailyForecast>(daily.Time!.Count);` down to the end of the `for` loop with:

```csharp
        if (daily.Time is null || daily.TemperatureMax is null || daily.TemperatureMin is null || daily.WeatherCode is null
            || daily.TemperatureMax.Count != daily.Time.Count
            || daily.TemperatureMin.Count != daily.Time.Count
            || daily.WeatherCode.Count != daily.Time.Count)
        {
            // Ragged or missing parallel arrays: the response is malformed — fail the
            // whole load (atomic, spec error-handling table); never zip ragged arrays.
            throw new InvalidOperationException("Forecast `daily` block was malformed (missing or unequal-length arrays).");
        }

        var days = new List<DailyForecast>(daily.Time.Count);
        for (var i = 0; i < daily.Time.Count; i++)
        {
            // Seam 2: invariant parse — the host locale has zero influence on the
            // parsed date (the wire format is pinned ISO yyyy-MM-dd, spec Seam 1).
            var date = DateOnly.ParseExact(daily.Time[i], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            days.Add(new DailyForecast(
                date,
                daily.TemperatureMax[i],
                daily.TemperatureMin[i],
                _conditions.ToCondition(daily.WeatherCode[i])));
        }
```

(All null-forgiving `!` operators on the daily arrays are now gone — the guard establishes non-null.)

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/WeatherApp.Tests/WeatherApp.Tests.csproj --filter Tier!=Live`
Expected: PASS — all three new tests green, everything else untouched.

- [ ] **Step 6: Commit**

```bash
git add src/WeatherApp.Core/Weather/OpenMeteoWeatherProvider.cs tests/WeatherApp.Tests/Fixtures/forecast-daily-ragged.json tests/WeatherApp.Tests/Fixtures/forecast-daily-three.json tests/WeatherApp.Tests/Weather/OpenMeteoWeatherProviderTests.cs
git commit -m "feat: fail closed on malformed daily block, tolerate non-7 counts (F2)"
```

---

## Task 4: Seam 2 (d) proof — daily dates parse invariantly under a foreign locale

**Seam 2 (c) contract this task encodes verbatim** (spec Seam inventory): *each `daily.time[i]` value (ISO `yyyy-MM-dd`) is parsed to `DateOnly` invariantly — `DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture)` — so the parsed date is identical on every host locale; the host locale has zero influence on the parsed `Date`.*

This is the F1 Seam 3 test shape (`Formats_coordinates_invariantly_under_comma_decimal_locale`) pointed **inbound**: locale is the only thing varied over the same recorded fixture.

**Files:**
- Test: `tests/WeatherApp.Tests/Weather/OpenMeteoWeatherProviderTests.cs`

- [ ] **Step 1: Write the pinning test**

Add to `tests/WeatherApp.Tests/Weather/OpenMeteoWeatherProviderTests.cs`:

```csharp
    // Seam 2 (host-OS/locale) (d) proof: the daily `time[]` dates must parse
    // identically on every host locale. Forcing de-DE (dd.MM.yyyy short-date
    // convention) and asserting the same DateOnly values as the invariant run
    // proves the parse is pinned to ISO yyyy-MM-dd, not host-locale-driven —
    // the inbound counterpart of the F1 coordinate-formatting test above.
    [Fact]
    public async Task GetWeather_parses_daily_dates_invariantly_under_a_foreign_locale()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("forecast-daily-london.json"), out _);
            var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());

            var report = await provider.GetWeather(London, CancellationToken.None);

            report.Daily.Select(d => d.Date).Should().Equal(
                new DateOnly(2026, 7, 2), new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 4),
                new DateOnly(2026, 7, 5), new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 7),
                new DateOnly(2026, 7, 8));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
```

- [ ] **Step 2: Run the test**

Run: `dotnet test tests/WeatherApp.Tests/WeatherApp.Tests.csproj --filter Tier!=Live`
Expected: PASS — this pins the contract Task 2 implemented (`ParseExact` + `InvariantCulture`). If it fails, the implementation drifted to culture-default parsing; fix the parse, not the test.

- [ ] **Step 3: Commit**

```bash
git add tests/WeatherApp.Tests/Weather/OpenMeteoWeatherProviderTests.cs
git commit -m "test: pin invariant daily-date parsing under a foreign locale (F2 Seam 2)"
```

---

## Task 5: `WeatherViewModel` exposes the Forecast

**Files:**
- Modify: `src/WeatherApp.Core/ViewModels/WeatherViewModel.cs`
- Modify: `tests/WeatherApp.Tests/Fakes/FakeWeatherProvider.cs`
- Test: `tests/WeatherApp.Tests/ViewModels/WeatherViewModelTests.cs`

- [ ] **Step 1: Give the fake a report-shaped factory**

`tests/WeatherApp.Tests/Fakes/FakeWeatherProvider.cs` gains a `Returning(WeatherReport)` factory (backed by a private `_report` field) so Forecast-aware tests control exactly what `GetWeather` returns. The full class becomes:

```csharp
using WeatherApp.Core.Domain;
using WeatherApp.Core.Weather;

namespace WeatherApp.Tests.Fakes;

/// A fake Weather Provider at the seam: either returns preset weather or
/// throws, so a test can drive the WeatherViewModel load state machine into
/// Loaded or Error without touching the real Open-Meteo boundary.
public sealed class FakeWeatherProvider : IWeatherProvider
{
    /// A canned week (today + 6 from the F2 fixture's first day) so
    /// Returning(CurrentConditions) callers keep working against GetWeather.
    private static readonly IReadOnlyList<DailyForecast> CannedWeek =
        Enumerable.Range(0, 7)
            .Select(i => new DailyForecast(new DateOnly(2026, 7, 2).AddDays(i), 24.9, 16.9, "Overcast"))
            .ToList();

    private readonly Func<Location, CurrentConditions> _factory;
    private readonly bool _throws;
    private readonly Exception _thrown;
    private WeatherReport? _report;

    private FakeWeatherProvider(Func<Location, CurrentConditions> factory, bool throws, Exception? thrown = null)
    {
        _factory = factory;
        _throws = throws;
        _thrown = thrown ?? new HttpRequestException("boom");
    }

    public static FakeWeatherProvider Returning(CurrentConditions c) => new(_ => c, false);

    /// Full-report fake for Forecast-aware tests: GetWeather returns exactly this report.
    public static FakeWeatherProvider Returning(WeatherReport report) =>
        new(_ => report.Current, false) { _report = report };

    public static FakeWeatherProvider Throwing() => new(_ => throw new HttpRequestException("boom"), true);

    /// Throws an exception whose text leaks everything the Error-state message must
    /// NOT surface: the exception type name, a stack-trace-shaped line, the request
    /// URL and the raw coordinates. Lets a test prove the message stays neutral.
    public static FakeWeatherProvider ThrowingWith(Exception ex) => new(_ => throw ex, true, ex);

    public Task<CurrentConditions> GetCurrent(Location location, CancellationToken ct)
        => _throws ? throw _thrown : Task.FromResult(_factory(location));

    public Task<WeatherReport> GetWeather(Location location, CancellationToken ct)
        => _throws ? throw _thrown
            : Task.FromResult(_report ?? new WeatherReport(_factory(location), CannedWeek));
}
```

- [ ] **Step 2: Write the failing tests**

Add to `tests/WeatherApp.Tests/ViewModels/WeatherViewModelTests.cs`:

```csharp
    // ---- F2: the 7-day daily Forecast ----

    [Fact]
    public async Task Load_success_exposes_the_daily_forecast()
    {
        var report = new WeatherReport(
            new CurrentConditions(21.6, 16.6, "Partly cloudy"),
            Enumerable.Range(0, 7)
                .Select(i => new DailyForecast(new DateOnly(2026, 7, 2).AddDays(i), 24.9, 16.9, "Overcast"))
                .ToList());
        var vm = new WeatherViewModel(FakeWeatherProvider.Returning(report));

        await vm.Load(London);

        vm.State.Should().Be(WeatherLoadState.Loaded);
        vm.Conditions!.Condition.Should().Be("Partly cloudy");
        vm.Forecast.Should().HaveCount(7);
        vm.Forecast![0].Date.Should().Be(new DateOnly(2026, 7, 2));
        vm.Forecast![0].HighC.Should().Be(24.9);
    }

    [Fact]
    public async Task Load_failure_clears_the_forecast_along_with_conditions()
    {
        // The atomic error state must never leave a strip on screen beside the
        // error message — the catch path nulls Forecast exactly as it nulls
        // Conditions (spec error-handling table).
        var vm = new WeatherViewModel(FakeWeatherProvider.Throwing());

        await vm.Load(London);

        vm.State.Should().Be(WeatherLoadState.Error);
        vm.Conditions.Should().BeNull();
        vm.Forecast.Should().BeNull();
    }
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/WeatherApp.Tests/WeatherApp.Tests.csproj --filter Tier!=Live`
Expected: FAIL to compile — `WeatherViewModel` has no `Forecast`.

- [ ] **Step 4: Migrate the ViewModel**

`src/WeatherApp.Core/ViewModels/WeatherViewModel.cs` becomes:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using WeatherApp.Core.Domain;
using WeatherApp.Core.Weather;

namespace WeatherApp.Core.ViewModels;

/// Holds the Current Conditions and 7-day daily Forecast for a Location and
/// drives the load state machine. Always fetches fresh (ADR-0001: never cache
/// weather). One atomic load: both halves arrive together or neither does.
public sealed partial class WeatherViewModel : ObservableObject
{
    private readonly IWeatherProvider _provider;

    public WeatherViewModel(IWeatherProvider provider) => _provider = provider;

    [ObservableProperty] private CurrentConditions? _conditions;
    [ObservableProperty] private IReadOnlyList<DailyForecast>? _forecast;
    [ObservableProperty] private string? _locationName;
    [ObservableProperty] private WeatherLoadState _state = WeatherLoadState.Idle;
    [ObservableProperty] private string? _errorMessage;

    public async Task Load(Location location)
    {
        LocationName = location.Name;
        State = WeatherLoadState.Loading;
        ErrorMessage = null;
        try
        {
            var report = await _provider.GetWeather(location, CancellationToken.None);
            Conditions = report.Current;
            Forecast = report.Daily;
            State = WeatherLoadState.Loaded;
        }
        catch (Exception)
        {
            // Fixed neutral copy only: never surface the raw exception text/stack
            // trace or the request URL (which carries the Location's coordinates).
            // See the Story's security AC and Technical-Context "no raw stack trace
            // in UI" / "don't expose personal location beyond the request".
            Conditions = null;
            Forecast = null;
            ErrorMessage = $"Couldn't load weather for {location.Name}.";
            State = WeatherLoadState.Error;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/WeatherApp.Tests/WeatherApp.Tests.csproj --filter Tier!=Live`
Expected: PASS — the two new tests green; every existing `WeatherViewModel` and `MainViewModel` test still green (the `Returning(CurrentConditions)` fake now feeds `GetWeather` with the canned week; the neutral-error-copy test is unaffected because the catch block's message is unchanged).

- [ ] **Step 6: Commit**

```bash
git add src/WeatherApp.Core/ViewModels/WeatherViewModel.cs tests/WeatherApp.Tests/Fakes/FakeWeatherProvider.cs tests/WeatherApp.Tests/ViewModels/WeatherViewModelTests.cs
git commit -m "feat: WeatherViewModel exposes the 7-day daily Forecast (F2)"
```

---

## Task 6: Retire `GetCurrent`; Tier-2 live test covers the daily envelope

Every consumer is on `GetWeather` now. Delete the superseded F1 method and repoint the surviving contract tests, so there is exactly one fetch path (DRY).

**Files:**
- Modify: `src/WeatherApp.Core/Weather/IWeatherProvider.cs`
- Modify: `src/WeatherApp.Core/Weather/OpenMeteoWeatherProvider.cs`
- Modify: `tests/WeatherApp.Tests/Fakes/FakeWeatherProvider.cs`
- Modify: `tests/WeatherApp.Tests/Weather/OpenMeteoWeatherProviderTests.cs`
- Modify: `tests/WeatherApp.Tests/Live/OpenMeteoLiveTests.cs`

- [ ] **Step 1: Remove `GetCurrent` from the interface**

`src/WeatherApp.Core/Weather/IWeatherProvider.cs` becomes:

```csharp
using WeatherApp.Core.Domain;

namespace WeatherApp.Core.Weather;

public interface IWeatherProvider
{
    /// One combined fetch: Current Conditions + the 7-day daily Forecast,
    /// aggregated in the Location's own timezone (timezone=auto).
    Task<WeatherReport> GetWeather(Location location, CancellationToken ct);
}
```

- [ ] **Step 2: Delete the `GetCurrent` method body from `OpenMeteoWeatherProvider`** (the whole method — `GetWeather` is the only fetch path; the class keeps its constructor, fields, and `GetWeather` unchanged).

- [ ] **Step 3: Delete `GetCurrent` from `FakeWeatherProvider`** (remove the `GetCurrent` method only; `Returning(CurrentConditions)`, `Returning(WeatherReport)`, `Throwing()`, `ThrowingWith(...)` and `GetWeather` all stay).

- [ ] **Step 4: Repoint the surviving provider contract tests and delete the superseded ones**

In `tests/WeatherApp.Tests/Weather/OpenMeteoWeatherProviderTests.cs`:

Delete these three tests, whose contracts are now owned by the `GetWeather` tests from Tasks 2–4: `Maps_current_block_to_conditions`, `Sends_metric_unit_params_and_coordinates`, and `Formats_coordinates_invariantly_under_comma_decimal_locale` (its Seam 3 contract is asserted by `GetWeather_sends_daily_timezone_metric_params_and_invariant_coordinates`, and the de-DE forcing lives on in `GetWeather_parses_daily_dates_invariantly_under_a_foreign_locale` — extend that test with the two coordinate assertions in the step below).

Extend `GetWeather_parses_daily_dates_invariantly_under_a_foreign_locale` — add these three lines at the end of the `try` block (change `out _` to `out var handler` in its stub setup), so F1 Seam 3 keeps its de-DE outbound proof:

```csharp
            var query = handler.LastRequestUri!.Query;
            query.Should().Contain("latitude=51.5085");    // "." not "," despite de-DE (F1 Seam 3)
            query.Should().NotContain("51,5");             // the locale-corrupted form must never appear
```

Rewrite the three remaining `GetCurrent` failure-path tests against `GetWeather`:

```csharp
    [Fact]
    public async Task GetWeather_throws_on_error_status()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.BadRequest, """{"error":true,"reason":"bad"}""", out _);
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());

        var act = () => provider.GetWeather(London, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // Security AC: a forecast call that exceeds the provider's finite timeout fails
    // closed rather than hanging indefinitely — surfaced to the caller's error path.
    [Fact]
    public async Task GetWeather_exceeding_the_finite_timeout_fails_closed()
    {
        var http = StubHttpMessageHandler.ClientThatHangs(out _);
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap(), timeout: TimeSpan.FromMilliseconds(50));

        var act = () => provider.GetWeather(London, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // Security AC: a call whose CancellationToken is cancelled fails closed to the
    // caller's error path — never an unhandled hang.
    [Fact]
    public async Task GetWeather_with_a_cancelled_token_fails_closed()
    {
        var http = StubHttpMessageHandler.ClientThatHangs(out _);
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => provider.GetWeather(London, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
```

(`Exceeding_the_finite_timeout_fails_closed` and `Cancelled_token_fails_closed` are replaced by these `GetWeather_`-named versions — delete the originals.)

- [ ] **Step 5: Point the Tier-2 live test at the full envelope**

In `tests/WeatherApp.Tests/Live/OpenMeteoLiveTests.cs`, replace `Weather_provider_returns_conditions_for_coords` with:

```csharp
    [Fact]
    public async Task Weather_provider_returns_current_and_seven_daily_entries()
    {
        using var http = new HttpClient { BaseAddress = new Uri("https://api.open-meteo.com/") };
        var provider = new OpenMeteoWeatherProvider(http, new WmoConditionMap());

        var report = await provider.GetWeather(new Location("London", 51.5085, -0.1257), CancellationToken.None);

        // Shape, not value (spec Seam 1 envelope): the current half still maps, and
        // the daily half arrives as 7 zippable entries whose Conditions all mapped —
        // proving the four parallel arrays were present, equal-length and parseable.
        // Same single forecast call as before (more params), so the Tier-2 cost
        // ceiling is unchanged. Never assert volatile temperatures or conditions.
        report.Current.Condition.Should().NotBeNullOrEmpty();
        report.Daily.Should().HaveCount(7);
        report.Daily.Should().OnlyContain(d => !string.IsNullOrEmpty(d.Condition));
        report.Daily[0].Date.DayNumber.Should().BeGreaterThan(new DateOnly(2026, 1, 1).DayNumber); // a real, parsed date
    }
```

- [ ] **Step 6: Run the Tier-1 suite**

Run: `dotnet test tests/WeatherApp.Tests/WeatherApp.Tests.csproj --filter Tier!=Live`
Expected: PASS — no remaining reference to `GetCurrent` anywhere in the solution (`grep -rn "GetCurrent" src/ tests/` returns nothing).

- [ ] **Step 7: Run the Tier-2 live test once, supervised** (network required; skip in a sandboxed run and note it in the PR)

Run: `dotnet test tests/WeatherApp.Tests/WeatherApp.Tests.csproj --filter Tier=Live`
Expected: PASS — 3 live tests (the 2 F1 ones + the reshaped forecast test).

- [ ] **Step 8: Commit**

```bash
git add src/WeatherApp.Core/Weather/ tests/WeatherApp.Tests/
git commit -m "refactor: retire GetCurrent — GetWeather is the single fetch path (F2)"
```

---

## Task 7: The Forecast strip in the view

XAML + a display-only converter; no ViewModel logic. The **first entry is labelled "Today" positionally** — it *is* the Location's current day (`timezone=auto`, spec Seam 1) — never by comparing to the system clock. Weekday labels are culture-formatted display output (deliberately not a seam — spec Seam 2 note).

**Files:**
- Modify: `src/WeatherApp/Converters.cs`
- Modify: `src/WeatherApp/MainWindow.xaml`

- [ ] **Step 1: Add the day-label converter**

Add to `src/WeatherApp/Converters.cs`:

```csharp
/// Day label for a Forecast-strip cell: index 0 is "Today" positionally (the
/// first daily entry IS the Location's current day — timezone=auto; no system
/// clock involved), later cells the abbreviated weekday name (culture-formatted,
/// display-only). Inputs: [ItemsControl.AlternationIndex, DailyForecast.Date].
public sealed class DayLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type t, object? p, CultureInfo c) =>
        values is [int index, DateOnly date]
            ? index == 0 ? "Today" : date.ToString("ddd", c)
            : string.Empty;
    public object[] ConvertBack(object value, Type[] t, object? p, CultureInfo c) => throw new NotSupportedException();
}
```

- [ ] **Step 2: Add the strip to the weather body**

In `src/WeatherApp/MainWindow.xaml`:

Add the converter resource to `<Window.Resources>`:

```xml
        <local:DayLabelConverter x:Key="DayLabel"/>
```

Inside the `Loaded`-state `StackPanel` (the one holding temperature / Condition / wind), after the wind `TextBlock`, add:

```xml
                    <!-- 7-day daily Forecast strip (F2): today + 6, Location-local days -->
                    <ItemsControl ItemsSource="{Binding Weather.Forecast}" AlternationCount="8" Margin="0,16,0,0">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <UniformGrid Rows="1"/>
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <StackPanel Margin="2">
                                    <TextBlock FontWeight="Bold" FontSize="11" HorizontalAlignment="Center">
                                        <TextBlock.Text>
                                            <MultiBinding Converter="{StaticResource DayLabel}">
                                                <Binding Path="(ItemsControl.AlternationIndex)" RelativeSource="{RelativeSource AncestorType=ContentPresenter}"/>
                                                <Binding Path="Date"/>
                                            </MultiBinding>
                                        </TextBlock.Text>
                                    </TextBlock>
                                    <TextBlock FontSize="12" HorizontalAlignment="Center">
                                        <Run Text="{Binding HighC, Mode=OneWay}"/><Run Text="°"/>
                                    </TextBlock>
                                    <TextBlock FontSize="12" Foreground="#666" HorizontalAlignment="Center">
                                        <Run Text="{Binding LowC, Mode=OneWay}"/><Run Text="°"/>
                                    </TextBlock>
                                    <TextBlock FontSize="10" Foreground="#666" HorizontalAlignment="Center"
                                               TextWrapping="Wrap" TextAlignment="Center"
                                               Text="{Binding Condition}"/>
                                </StackPanel>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
```

(`AlternationCount="8"` covers the expected 7 cells plus the tolerated-longer case never observed; a shorter list simply renders fewer cells.)

- [ ] **Step 3: Build the full solution (Windows)**

Run: `dotnet build WeatherApp.sln`
Expected: Build succeeded, 0 warnings from the new XAML/converter. (On a non-Windows box this project can't build — defer this step and Step 4 to the Windows verification pass and say so in the PR.)

- [ ] **Step 4: Tier-3 manual check (Windows)**

Run: `dotnet run --project src/WeatherApp/WeatherApp.csproj`
Expected: search a place, pick it — Current Conditions render as before, and beneath them a 7-cell strip: "Today" then six weekday labels, each with high°, low°, and a Condition. An unreachable network shows only the neutral error line — no half-rendered strip.

- [ ] **Step 5: Commit**

```bash
git add src/WeatherApp/Converters.cs src/WeatherApp/MainWindow.xaml
git commit -m "feat: 7-day daily Forecast strip in the weather view (F2)"
```

---

## Done means

- `dotnet test WeatherApp.sln --filter Tier!=Live` green on Windows (the full Tier-1 suite, including the new Seam 1/Seam 2 recorded-replay tests).
- `dotnet test WeatherApp.sln --filter Tier=Live` green (3 live tests; the forecast one now asserts the daily envelope).
- `grep -rn "GetCurrent" src/ tests/` returns nothing — one fetch path.
- Tier-3: the running app shows the strip per Task 7 Step 4.
- No weather payload persisted anywhere (ADR-0001) — F2 adds no storage code at all.
