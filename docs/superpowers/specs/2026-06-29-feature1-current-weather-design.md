# Feature 1 — See the current weather for a place you search (tracer bullet)

**Context references:**
- `business-domain-context.md` (the project's `Context.MD` — domain glossary)
- `Technical-Context.MD`
- `PRD.md`
- `Roadmap.md` → Feature: **F1 — See the current weather for a place you search** 🔫 tracer bullet
- `docs/adr/0001-persist-location-only-never-cache-weather.md` (constrains F1 to introduce no weather cache)

## Overview

The thinnest vertical slice that proves the whole system end-to-end. The app opens to an empty state with a search prompt. The user types a place name; after a short debounce the **Geocoder** is queried and returns **Location** candidates; the user explicitly picks one, making it the active **Location**; the app fetches and shows that Location's **Current Conditions** (temperature, wind speed, **Condition**) in fixed metric units.

It threads every architectural layer — XAML View → `SearchViewModel` → **Geocoder** (Open-Meteo geocoding) → `MainViewModel` → `WeatherViewModel` → **Weather Provider client** (Open-Meteo forecast) → domain model → View — plus generic-host DI/logging wiring, exercising *both* external HTTP seams and the full MVVM spine.

## Architecture & components

Single WPF window on the .NET generic host (`Microsoft.Extensions.Hosting` + DI + logging). `MainWindow.DataContext = MainViewModel`. The search box is **persistently visible** at the top. `MainViewModel.ViewState ∈ {Empty, Weather}` governs the body beneath it — **Empty** (search prompt) before any Location is active, **Weather** once a Location is loaded. The **candidate list is a separate panel** owned by `SearchViewModel`, shown under the search box whenever `SearchViewModel` has candidates or a search message (zero-results / error) — it floats over the current body and is cleared on selection. So candidate display is driven by search state, not by `ViewState`.

| Unit | Responsibility | Conceptual interface | Depends on |
|---|---|---|---|
| **Geocoder** (deep) | Resolve a query to Location candidates | `Task<IReadOnlyList<LocationCandidate>> Search(string query, CancellationToken ct)` | typed `HttpClient` (geocoding host) |
| **Weather Provider client** (deep) | Fetch a Location's current weather | `Task<CurrentConditions> GetCurrent(Location loc, CancellationToken ct)` | typed `HttpClient` (forecast host), WMO map |
| **WMO Condition map** (pure) | WMO weather code → Condition text | `string ToCondition(int wmoCode)` | — |
| **SearchViewModel** | Debounced Location Search, candidate list, raise selection | `Query`, `Candidates`, `SelectCommand`; raises `LocationSelected(Location)` | Geocoder, injected debounce clock |
| **WeatherViewModel** | Hold Current Conditions for a Location | `Task Load(Location loc)`; `Conditions`, `State` | Weather Provider client |
| **MainViewModel** (shell) | Mediate activation; own `ViewState` | subscribes to `SearchViewModel.LocationSelected` → sets active Location → `await WeatherViewModel.Load(loc)` | both ViewModels |
| **Views (XAML)** | Empty / candidate list / weather / inline status | data-bound, no logic | ViewModels |

**Domain types** (immutable records):
- `LocationCandidate` — `Name` (string, non-null), `Admin1` (string?, **nullable**), `Country` (string, non-null), `Latitude` (double), `Longitude` (double).
- `Location` — `Name` (string), `Latitude` (double), `Longitude` (double). The active one.
- `CurrentConditions` — `TemperatureC` (double), `WindSpeedKmh` (double), `Condition` (string).

**Activation handoff (Approach A — shell-mediated composition):** `SearchViewModel` raises a `LocationSelected` event; `MainViewModel` is the sole subscriber, sets the active Location, flips `ViewState = Weather`, and calls `WeatherViewModel.Load`. The two child ViewModels never reference each other.

**Deliberately absent in F1** (Roadmap scope + ADR-0001): no **Location Store** / persistence (the app opens to empty state every launch), no weather caching of any kind, no manual refresh, no Updated-at stamp, no Retry button, no Forecast.

Two typed `HttpClient` registrations with distinct base hosts (`geocoding-api.open-meteo.com` vs `api.open-meteo.com`), matching the glossary's Geocoder-vs-Weather-Provider separation so the geocoding source can be swapped independently later.

## Data flow

**Startup** — host builds, DI resolves `MainViewModel` (+ children), window shows. No persisted Location → `ViewState = Empty`, search prompt renders.

**Search (debounced, sequence-guarded):**
1. User types → `SearchViewModel.Query` updates. If `Query.Length < 2`: clear `Candidates`, make no call.
2. Injected debounce clock starts/resets a 300 ms timer. On elapse, capture a monotonically increasing `searchSeq`, cancel any prior in-flight token, call `Geocoder.Search(query, ct)`.
3. On response: apply **only if this response's `searchSeq` is the latest**; otherwise drop it. Guard-on-arrival guarantees correctness; cancellation just saves work.

**Activation handoff:**
4. User clicks a candidate → `SelectCommand` → `SearchViewModel` raises `LocationSelected(Location)` built from the candidate's `Name` + `Latitude`/`Longitude`.
5. `MainViewModel` sets active Location, `ViewState = Weather`, `await WeatherViewModel.Load(loc)`.

**Weather load:**
6. `WeatherViewModel.Load` → `State = Loading` → `WeatherProvider.GetCurrent(loc, ct)` → map WMO code → Condition → build `CurrentConditions` → `State = Loaded`. View binds temperature / wind / Condition + the Location's name.

## Error handling (F1 scope)

Every failure surfaces as an **inline message** (Technical-Context); never a crash dialog, stack trace, or silent failure. All exceptions (network, timeout, non-2xx, JSON shape) are caught at the **ViewModel boundary** and turned into display state.

| Situation | F1 behaviour |
|---|---|
| Search returns zero candidates (**absent `results`**) | Inline "No places found for *'query'*." Not an error — an empty result. |
| Geocoder call fails (network/timeout/HTTP/JSON) | Inline "Couldn't search right now — check your connection and try again." Active view unchanged; user retries by editing the query. |
| Weather load fails | `WeatherViewModel.State = Error`; body shows "Couldn't load weather for *Location*." **No Retry button in F1** (Roadmap → F4); recovery is re-selecting the candidate or re-searching. |
| In-flight | Lightweight "Searching…" / "Loading…" so the app never looks frozen. |

## Testing

Per the Technical-Context standard — assert the **deterministic envelope**, never live weather values; the **Open-Meteo HTTP boundary is the recorded-replay seam**.

**Tier-1 (recorded-replay, every commit):**
- **Geocoder** — replay recorded geocoding JSON via an injected `HttpMessageHandler` stub (real `HttpClient` + `System.Text.Json` parse over recorded bytes = real local I/O on the parse side). Fixtures: many-candidate, single-candidate, **zero-results (absent `results` key)**, error envelope (HTTP 400 `{"error":true,...}`). Assert: candidate fields parse; **absent `results` → empty list**; **absent `admin1` tolerated** (nullable); request carries `name`/`count` params.
- **Weather Provider client** — same mechanism, recorded forecast JSON. Assert: `current` block → `CurrentConditions`; request carries **`temperature_unit=celsius` & `wind_speed_unit=kmh`**; `weather_code` parsed; error/non-2xx handled.
- **WMO Condition map** (pure) — known codes → expected labels; **unknown code → safe fallback** ("Unknown").

**Tier-1 ViewModel state-logic (fakes at seams, fake clock):**
- **SearchViewModel** — debounce fires once after interval; `<2` chars → no call; **sequence-guard** (out-of-order fake responses, only latest renders); zero-results message; Geocoder failure → inline error; `SelectCommand` raises `LocationSelected` with correct `Location`.
- **WeatherViewModel** — `Load` drives Loading→Loaded and maps Condition; failure → Error state.
- **MainViewModel** — `LocationSelected` sets active Location, flips `ViewState = Weather`, calls `WeatherViewModel.Load`.

**Tier-2 (live, scheduled, bounded):** one live call each to the real geocoding + forecast endpoints; assert the **fixtures still match the real contract** (fields present, types/nullability) — not the values.

**Tier-3:** the shipped app, run manually — search, pick, see weather.

**Platform matrix:** Windows is the target; **F1 has no filesystem/OS-touching code** (no Location Store until F3), so the OS-matrix obligation is light here — F3 picks it up.

## Seam inventory

Two external network-protocol seams. Both grounded against **live observation of the real Open-Meteo API on 2026-06-29** (read-only GETs, captured during brainstorming) — not model memory. The in-process `SearchViewModel → MainViewModel → WeatherViewModel` activation handoff is **not** a taxonomy seam (in-memory event/method call; code is the authority; covered by the `MainViewModel` integration test) and is deliberately excluded.

**Auth (first contact with Open-Meteo):** the service offers **no authentication** — the free API requires no key, token, or header. We send none. This decision is inherited by reference by both seams below and by every future Open-Meteo seam (F2, F5). Grounded against Open-Meteo's public docs and confirmed live: unauthenticated GETs returned 200 with data.

### Seam 1: Geocoder ↔ Open-Meteo geocoding API
- **(a) class:** network-protocol — **external**
- **(b) sides:** `Geocoder` (our typed `HttpClient`) ↔ Open-Meteo geocoding service (`geocoding-api.open-meteo.com/v1/search`)
- **(c) contract:** HTTPS `GET /v1/search?name=<q>&count=<n>&language=en&format=json`, no auth.
  - **Success (HTTP 200):** body is a JSON object. The candidate array is under key **`results`**, which is **ABSENT when there are zero matches** (observed: a zero-match query returned `{"generationtime_ms":0.54}` with **no `results` key**) — the client MUST treat absent `results` as an empty candidate list, never dereference it blindly.
  - **Each candidate object:** `name` (string, non-null), `latitude` (number, non-null), `longitude` (number, non-null), `country` (string, non-null), `admin1` (string, **nullable / may be absent** — region name), plus ignored fields (`id`, `elevation`, `feature_code`, `country_code`, `timezone`, `population`, `admin2`/`admin3`, `postcodes`). Unused fields are tolerated, not required.
  - **Error (HTTP 400):** `{"error":true,"reason":<string>}` (observed for a missing `name`). The client surfaces any non-2xx or `error:true` body as the inline "couldn't search" state. (We always send `name` ≥ 2 chars, so this is a defensive path.)
- **(d) proof:** Tier-1 recorded-replay test over fixtures captured from this live call — `many-candidate`, `single-candidate`, **`zero-results` (no `results` key)**, and `error-400` — asserting the parse, the absent-`results`→empty mapping, and absent-`admin1` tolerance. Tier-2 live call re-confirms the shape on schedule.
- **(e) authority:** Open-Meteo Geocoding API — live observation on 2026-06-29 (`GET geocoding-api.open-meteo.com/v1/search?name=London&count=3` → 3 candidates each with `name/latitude/longitude/country/admin1`; `name=zzzznotaplace` → no `results` key; `name` omitted → HTTP 400 `{"error":true,"reason":...}`). Cross-references the public docs at open-meteo.com/en/docs/geocoding-api.

### Seam 2: Weather Provider client ↔ Open-Meteo forecast API
- **(a) class:** network-protocol — **external**
- **(b) sides:** `WeatherProvider` (our typed `HttpClient`) ↔ Open-Meteo forecast service (`api.open-meteo.com/v1/forecast`)
- **(c) contract:** HTTPS `GET /v1/forecast?latitude=<lat>&longitude=<lon>&current=temperature_2m,weather_code,wind_speed_10m&temperature_unit=celsius&wind_speed_unit=kmh`, no auth.
  - **Success (HTTP 200):** JSON object containing a **`current`** object with `time` (iso8601 string, non-null), `temperature_2m` (number, non-null), `weather_code` (integer WMO code, non-null), `wind_speed_10m` (number, non-null) — all present because requested. A sibling **`current_units`** object echoes the units (`temperature_2m: "°C"`, `wind_speed_10m: "km/h"`), letting the test assert the metric params took effect. Returned `latitude`/`longitude` may be **grid-snapped** (observed: request 51.5085 → response 51.5) — not an error; we do not assert request==response coords.
  - **`weather_code`** is a WMO code mapped to a **Condition** by the pure WMO map; an unrecognised code maps to "Unknown" (the map never throws).
  - **Error:** non-2xx / `{"error":true,"reason":...}` envelope handled as the inline "couldn't load weather" state.
- **(d) proof:** Tier-1 recorded-replay test over a fixture captured from this live call, asserting the `current`→`CurrentConditions` map, the metric unit params on the outbound request, and WMO-code handling. Tier-2 live call re-confirms on schedule.
- **(e) authority:** Open-Meteo Forecast API — live observation on 2026-06-29 (`GET api.open-meteo.com/v1/forecast?latitude=51.5085&longitude=-0.1257&current=temperature_2m,weather_code,wind_speed_10m&temperature_unit=celsius&wind_speed_unit=kmh` → `{"current_units":{"temperature_2m":"°C","weather_code":"wmo code","wind_speed_10m":"km/h"},"current":{"time":"2026-06-29T22:00","temperature_2m":20.4,"weather_code":1,"wind_speed_10m":12.2}}`). Cross-references the public docs at open-meteo.com/en/docs.

## Out of scope (F1)

7-day daily Forecast (F2); persistence / Location Store / launch-restore (F3); manual refresh, Updated-at stamp, keep-last-good, Retry button (F4); hourly Forecast (F5); unit preference (F6); weather icons; debounced-search niceties beyond the 300 ms/2-char rule; "feels like"/humidity/pressure/wind-direction fields.

## Feature-doc-gauntlet sign-off

- **Result:** fail
- **Date:** 2026-06-30
- **Reason:** feature-docs
- **Summary:** Two of three leaves passed (check-doc-adr-consistency, check-artefact-consistency). check-seam-cynicism found one root-cause gap: the design crosses a host-OS/runtime (locale) boundary that the Seam inventory does not carry as its own row.
- **Leaves:** check-seam-cynicism (fail), check-doc-adr-consistency (pass), check-artefact-consistency (pass)
- **Open findings (route to `/fix-feature-docs`, then re-run the full gauntlet):**
  - *(check-seam-cynicism — settleable in-session)* The Seam inventory states "Two external network-protocol seams" and excludes only the in-memory handoff, but `OpenMeteoWeatherProvider.GetCurrent` must serialise `Latitude`/`Longitude` culture-invariantly or a comma-decimal locale (e.g. de-DE → `51,5`) corrupts the forecast query string. The taxonomy lists host-OS/runtime as a distinct class; this boundary has no inventory row — its falsifiable contract ("coordinates are formatted with a '.' decimal separator regardless of host locale") and its own proof are folded into Seam 2 / Plan Task 5 rather than written as a seam. Fix: add a host-OS seam row whose (c) is the invariant-decimal wire assertion and whose (d) is a locale-forcing round-trip test (run the existing `latitude=51.5085` assertion under a de-DE `CultureInfo`).
  - *(check-seam-cynicism)* Spec Seam 2 (c) asserts the coordinate wire format only implicitly; the contract text should state that the outbound coordinate is invariant-culture-formatted with a '.' decimal separator and never the host locale's separator.
- **Non-gating observations (for human eyes, not blockers):**
  - *(check-doc-adr-consistency)* ADR-0001's in-session keep-last-good + Updated-at is a refresh-time behaviour deferred to F4; F1 (no refresh) showing a plain inline error is consistent with the ADR.
  - *(check-artefact-consistency)* Technical-Context's "log every outbound Open-Meteo call" is an *inferred default*, not an Overriding Principle; F1 logs none. Suggest a one-line decision: add logging to F1 or record it as deferred.
  - *(check-artefact-consistency)* PRD's spaced "Search ViewModel" / "Weather ViewModel" vs Spec/Plan's `SearchViewModel` / `WeatherViewModel` is cosmetic implementation-construct naming, not glossary drift.

> A sign-off that predates later substantive edits to the Spec or Plan is stale. `enate-to-stories` and `check-security-design` MUST refuse to break this Feature into stories until this section shows `Result: pass`.
