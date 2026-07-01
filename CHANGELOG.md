# Changelog

All notable changes to TEST-F6-DesktopWeatherApp are recorded here. The **why** matters as
much as the **what**.

## [Unreleased] - 2026-07-01

### Added
- **Open-Meteo Weather Provider (Seams 2 & 3)** — `OpenMeteoWeatherProvider : IWeatherProvider`,
  a typed `HttpClient` over Open-Meteo's forecast API. `GetCurrent(Location, CancellationToken)`
  issues `GET v1/forecast` requesting the `current` block (`temperature_2m,weather_code,wind_speed_10m`)
  with fixed metric unit params (`temperature_unit=celsius`, `wind_speed_unit=kmh`) and maps the
  `current` block into a domain `CurrentConditions`, routing `weather_code` through `WmoConditionMap`
  (Seam 2). This slice covers **Current Conditions only**; the 7-day daily Forecast remains a later
  Feature-1 slice.
  - **Invariant coordinate formatting (Seam 3, host-OS/locale)** — latitude/longitude are serialised
    with `CultureInfo.InvariantCulture` so the decimal separator is always `.` regardless of host
    locale — why: a comma-decimal host (e.g. `de-DE`) would otherwise emit `latitude=51,5` and
    corrupt the query. Proven by a locale-forcing regression test that forces `CurrentCulture` to
    `de-DE` and asserts the wire form stays `latitude=51.5085`.
  - **Fail-closed timeout** (security acceptance criterion) — the call is bounded by a **finite
    timeout (default 10s) linked to the caller's `CancellationToken`**, so a hung endpoint or a
    cancelled caller fails closed to the caller's load-failure path (`OperationCanceledException`)
    rather than hanging indefinitely.
  - A non-2xx response (or provider `error:true`) surfaces as `HttpRequestException` via
    `EnsureSuccessStatusCode`, and a `current`-less response throws `InvalidOperationException` —
    both routed to the caller's load-failure path.
  - Proven by **Tier-1 recorded-replay** over a live-captured fixture (`forecast-london.json`,
    2026-06-29) plus the Seam-3 locale-forcing test, an error-status test, and timeout/cancellation
    fail-closed tests, all via the stub `HttpMessageHandler`.
- **Open-Meteo Geocoder (Seam 1)** — `OpenMeteoGeocoder : IGeocoder`, a typed `HttpClient`
  over Open-Meteo's geocoding API (`v1/search`) resolving a query string to an
  `IReadOnlyList<LocationCandidate>`. It hides HTTP/JSON behind the domain contract and maps
  the provider's `results` array into `LocationCandidate` records. An **absent `results` key**
  (Open-Meteo's zero-match shape) is treated as an empty list rather than an error, so callers
  never have to distinguish "no key" from "empty array". Proven by **Tier-1 recorded-replay**
  over four live-captured fixtures (many-candidate, single, zero-results, error-400) plus
  absent-`admin1` tolerance and request-parameter assertions.
- **Security hardening on the Geocoder** (security acceptance criteria):
  - The search query is **URL-encoded** (`Uri.EscapeDataString`) so query-significant characters
    (`&`, `#`, `=`, CRLF) travel as a single `name` value and cannot inject or forge extra query
    parameters — why: an un-encoded caller string could otherwise smuggle `count=999` or split the
    request line.
  - The call is bounded by a **finite timeout linked to the caller's `CancellationToken`**, so a
    hung endpoint or a cancelled caller **fails closed** to the error path rather than hanging.
- **Pure, I/O-free domain core** (`WeatherApp.Core`) — the substrate every later Feature-1
  slice (Geocoder, Weather Provider client, ViewModels) binds to, built first so those slices
  have concrete types to consume:
  - `Location(Name, Latitude, Longitude)` — the single active place weather is shown for.
  - `LocationCandidate(Name, Admin1?, Country, Latitude, Longitude)` — a Location Search result.
    `Admin1` (region) is **nullable** because the Geocoder omits it for some places.
  - `CurrentConditions(TemperatureC, WindSpeedKmh, Condition)` — present-moment weather in
    fixed metric units.
  - All three are immutable `sealed record` types.
- **`WmoConditionMap.ToCondition(int)`** — maps Open-Meteo WMO weather codes to human-readable
  condition labels. Deliberately **pure and total**: an unrecognised code returns `"Unknown"`
  rather than throwing, so downstream code never has to guard the mapping call.
- **Tier-1 unit tests** for the domain records and the WMO map (known codes plus the
  unknown-code fallback), establishing the first `dotnet test`-green coverage in the repo.

### Notes
- The Geocoder (Seam 1) and the Weather Provider's Current-Conditions path (Seams 2 & 3) are now in.
  Still to come: the 7-day daily Forecast, the Location Store, and the ViewModels — those remain
  separate Feature-1 stories that depend on this core (see `Roadmap.md`).
- `coverlet.collector` is present in the test project for code-coverage collection; recorded
  in `Technical-Context.MD` Packages-in-use.
