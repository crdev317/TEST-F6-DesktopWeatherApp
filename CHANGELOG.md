# Changelog

All notable changes to TEST-F6-DesktopWeatherApp are recorded here. The **why** matters as
much as the **what**.

## [Unreleased] - 2026-07-01

### Added
- **MainViewModel — shell-mediated activation handoff** (Story #95149) — `MainViewModel : ObservableObject`
  (CommunityToolkit.Mvvm), the shell that **owns** the two child ViewModels (`Search`, `Weather`) and
  mediates the **activation handoff** between them. It is the **sole subscriber** to
  `SearchViewModel.LocationSelected`; on a selection it flips `ViewState` from `Empty` to `Weather`
  (a new `WeatherViewState` enum) and calls `WeatherViewModel.Load(location)`. Starts in the `Empty`
  state (first-run search prompt). It also exposes the in-flight load as an awaitable `LastActivation`
  so callers/tests can await activation completion.
  - **Approach A — shell-mediated composition, chosen over child-to-child wiring** — the two children
    **never reference each other**; all coupling flows through the shell. Why: keeping the search and
    weather sides mutually ignorant means either can be tested (and later re-composed) in isolation, and
    the handoff rule (select → activate → fresh fetch) lives in exactly one place instead of being smeared
    across the children.
  - Proven by a **Tier-1 integration test** over the *real* child ViewModels wired to fake seams
    (`FakeGeocoder`, `FakeWeatherProvider`) — the in-process activation integration point named in the
    Spec, exercised here rather than as a taxonomy seam: starts `Empty`; a `SelectCommand` on the search
    child drives the shell to `Weather` and the weather child to `Loaded` with the selected Location's name.
- **Tier-2 live Open-Meteo contract tests** (Story #95151) — `OpenMeteoLiveTests`
  (`tests/WeatherApp.Tests/Live/OpenMeteoLiveTests.cs`), the first realisation of the Tier-2
  tier the testing standard planned for. Two `[Fact]`s make **one real disposable call each** to
  the live Open-Meteo geocoding (`OpenMeteoGeocoder.Search("London", …)`) and forecast
  (`OpenMeteoWeatherProvider.GetCurrent(…)`) endpoints and assert only on the **deterministic
  response envelope** — the geocoder returns candidates with non-empty name/country; the provider
  returns a non-empty `Condition` (proving the `current` block parsed and the total WMO map fired).
  - **Shape, never value** — deliberately never asserts on volatile weather (temperature, wind, the
    specific Condition) — why: the point is to confirm the recorded Tier-1 fixtures still match the
    real contract (fields present, types/nullability), catching provider-side drift, not to pin
    weather that changes minute to minute (per Technical-Context "test the contract, not the live
    service").
  - **Tier trait gates them out of every-commit runs** — the class carries `[Trait("Tier", "Live")]`
    so the every-commit Tier-1 run excludes them via `dotnet test --filter Tier!=Live`; the scheduled
    live run is `dotnet test --filter Tier=Live` — why: live calls have real cost/flakiness and must
    not gate every commit.
- **Weather ViewModel — fresh-fetch load state machine** (Story #95148) — `WeatherViewModel : ObservableObject`
  (CommunityToolkit.Mvvm) holding the **Current Conditions** for a Location and driving a load state
  machine `Idle → Loading → Loaded` on success, `Loading → Error` on a provider failure — the states
  modelled by a new `WeatherLoadState` enum. `Load(Location)` always fetches fresh from the injected
  `IWeatherProvider` (ADR-0001: never cache weather) and exposes `Conditions`, `LocationName`, `State`
  and `ErrorMessage` as bindable properties.
  - **Neutral copy only on failure** (security acceptance criterion) — a provider failure sets the
    fixed line *"Couldn't load weather for {name}."* and **never** surfaces the exception type, stack
    trace, or the request URL (which carries the Location's coordinates), per the Technical-Context
    "no raw stack trace in UI" / "don't expose personal location beyond the request" principles.
    Why proven separately: a clean signature can still leak — the guard is a behaviour, not a type.
  - Proven by **Tier-1 ViewModel tests** with a fake Weather Provider at the seam: success sets
    `Conditions` + `Loaded`, the state sequence is observed as `Loading → Loaded`, a failure yields
    `Error` + neutral message with `Conditions` cleared, and a **leak-inducing fake-seam test** feeds
    an exception whose text carries the type name, stack frame, URL and coordinates and asserts none
    of them reach `ErrorMessage`.
  - **Scope of this slice** — Current Conditions only. The rest of the Weather ViewModel contract —
    the 7-day Forecast, the **Updated-at** stamp, refresh-failure keep-last-good, and the retry
    affordance — remains later Feature-1 work.
- **Search ViewModel — debounced, sequence-guarded Location Search** — `SearchViewModel : ObservableObject`
  (CommunityToolkit.Mvvm) orchestrating the **Location Search**: owns `Query`, a `Candidates`
  collection, a `SearchMessage` (zero-results / error line) and `SelectCommand`, and raises
  `LocationSelected(Location)` on explicit selection (the activation handoff to the Weather side).
  - **Debounce** — typing schedules the Geocoder call through an injected `IDebounceScheduler`
    after a **300 ms** delay, so rapid typing collapses to a single call. The clock is a seam
    (`DebounceScheduler`, backed by a `System.Timers.Timer`) so Tier-1 tests fire it synchronously
    with a fake — no real time, no real network. The production scheduler marshals the action onto
    the `SynchronizationContext` captured at `Schedule` time (the UI thread), so post-await state
    mutations stay on the UI thread.
  - **Min-length short-circuit** — a `Query` of **fewer than 2 characters** clears `Candidates`
    and makes no Geocoder call — why: single-character queries are noise and waste a request.
  - **Sequence guard (guard-on-arrival)** — each debounced run takes a monotonically increasing
    `searchSeq` and cancels any prior in-flight token; results are applied **only if the run is
    still the latest** — why: debounced continuations resume on arbitrary ThreadPool threads and
    out-of-order Geocoder responses would otherwise render stale candidates over a newer query.
    The guard's shared counter is accessed under an explicit memory barrier
    (`Interlocked.Increment` on write, `Volatile.Read` on the arrival/exception checks) — a plain
    `++`/`!=` risked a torn/stale read across threads that could make the guard misfire.
  - **Neutral copy only on failure** (security acceptance criterion) — any Geocoder failure
    surfaces the fixed line *"Couldn't search right now — check your connection and try again."*
    and **never** the exception text/stack trace or the request URL (which carries the query),
    per the Technical-Context "no raw stack trace in UI" / "don't expose personal data beyond the
    request" principles. A stale failure (superseded by a newer search) is dropped silently.
  - Zero candidates gives a *"No places found for …"* message (not an error), leaving the current
    view otherwise untouched.
  - Proven by **Tier-1 ViewModel tests** with a fake Geocoder at the seam and a manual
    (fake) debounce clock: debounce collapse, min-length short-circuit, stale-response drop
    (latest-query-wins), zero-results copy, neutral failure copy, and `SelectCommand` raising
    `LocationSelected` with the correct `Location`.
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
- The Geocoder (Seam 1), the Weather Provider's Current-Conditions path (Seams 2 & 3), the
  Search ViewModel (over the faked Geocoder + debounce-clock seams), the Weather ViewModel's
  fresh-fetch load state machine (Current Conditions, over the faked Weather Provider seam), and the
  MainViewModel activation handoff (shell wiring the two children) are now in. Still to come: the
  7-day daily Forecast, the Location Store, the rest of the Weather ViewModel (Updated-at, refresh
  keep-last-good, retry), and the WPF shell (host wiring, DI & XAML views that bind the MainViewModel)
  — those remain separate Feature-1 stories that depend on this core (see `Roadmap.md`).
- `coverlet.collector` is present in the test project for code-coverage collection; recorded
  in `Technical-Context.MD` Packages-in-use.
