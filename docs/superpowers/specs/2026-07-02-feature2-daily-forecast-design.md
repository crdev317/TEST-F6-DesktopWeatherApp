# Feature 2 — See the 7-day daily Forecast for the active Location

**Context references:**
- `Context.MD` (domain glossary; `business-domain-context.md` is its sibling copy)
- `Technical-Context.MD`
- `PRD.md` (requirement 9 — the 7-day daily Forecast; requirement 10 — metric units)
- `Roadmap.md` → Feature: **F2 — See the 7-day daily Forecast for the active Location**
- `docs/adr/0001-persist-location-only-never-cache-weather.md` (constrains F2 to fetch fresh, cache nothing)
- `docs/superpowers/specs/2026-06-29-feature1-current-weather-design.md` (the F1 spec whose Weather Provider seam and error model F2 extends)

## Overview

The weather view gains a 7-day daily **Forecast** strip — per-day high/low and **Condition** — for the active **Location**, fetched from the **Weather Provider** in the *same* request that already fetches **Current Conditions**. This completes the Weather Provider client module contract the PRD declares (`Location → (Current Conditions + 7-day daily Forecast)`); F1 built the `current` half, F2 adds the `daily` half.

**Decisions made in this brainstorm:**
- **Day range: today + 6 days.** The strip's first entry is today (labelled "Today"), carrying today's full high/low alongside the on-screen Current Conditions. Matches Open-Meteo's default `forecast_days=7` — no extra param.
- **Day boundary: Location-local days.** The request passes `timezone=auto` so Open-Meteo aggregates each day's high/low in the Location's own timezone — "Today" means the searched place's today, not the viewer's. Proven live (see Seam 1).
- **Fetch: one combined call (Approach A).** One HTTP round trip returns both halves; load stays atomic — one loading state, one error state, the F1 `WeatherLoadState` machine unchanged. Rejected: a separate second call (doubles traffic, adds a second error surface, diverges from the PRD's single-call module interface) and a shared-response two-method design (a held payload is caching-in-spirit, skating against ADR-0001).

## Architecture & components

No new modules, no new processes, no new external systems. Four existing units change; two domain types are added.

**Domain types (new, immutable records in `WeatherApp.Core`):**
- `DailyForecast` — one day of the Forecast: `Date` (`DateOnly`, non-null, the Location-local calendar day), `HighC` (double), `LowC` (double), `Condition` (string, non-null, via the existing WMO map).
- `WeatherReport` — the composite one fetch returns: `Current` (`CurrentConditions`, non-null) + `Daily` (`IReadOnlyList<DailyForecast>`, non-null, expected 7 entries). A code-level artifact, deliberately **not** a `Context.MD` term (decision in-session, 2026-07-02): it names the wire-fetch pairing, not a user-facing concept.

**Changed units:**

| Unit | Change |
|---|---|
| **Weather Provider client** (deep) | `IWeatherProvider.GetCurrent` is superseded by `Task<WeatherReport> GetWeather(Location, CancellationToken)` — a rename-and-extend; `WeatherViewModel` is its only consumer. The request gains `daily=temperature_2m_max,temperature_2m_min,weather_code&timezone=auto` alongside the existing `current` block and metric unit params. Raw provider JSON still never leaks upward. Invariant-culture coordinate formatting (F1 Seam 3) applies unchanged. |
| **WMO Condition map** (pure) | Reused as-is for each day's `weather_code`. No change. |
| **WeatherViewModel** | `Load` stores both halves of the `WeatherReport`; new `Forecast` property (`IReadOnlyList<DailyForecast>`) beside `Conditions`. The `WeatherLoadState` machine (`Idle → Loading → Loaded/Error`) is unchanged. |
| **Views (XAML)** | A horizontal 7-cell strip beneath Current Conditions, visible in the `Loaded` state. Each cell: day label, high/low in °C, Condition text. The **first entry is labelled "Today" positionally** (it *is* the Location's current day, guaranteed by `timezone=auto` — no system-clock comparison, so no clock/timezone dependency); subsequent entries show the abbreviated weekday name derived from their `DateOnly` (culture-formatted, display-only). No icons (PRD scope). |

**Untouched:** `SearchViewModel`, `MainViewModel` (shell), the Geocoder, the activation flow, DI shape (same two typed HttpClients).

## Data flow

Only the weather-load step of F1's flow changes:

1. Activation (candidate selected) → `MainViewModel` calls `WeatherViewModel.Load(location)` — unchanged.
2. `Load` → `State = Loading` → one `GET api.open-meteo.com/v1/forecast?latitude=<lat>&longitude=<lon>&current=temperature_2m,weather_code,wind_speed_10m&daily=temperature_2m_max,temperature_2m_min,weather_code&temperature_unit=celsius&wind_speed_unit=kmh&timezone=auto`.
3. The response's `current` object maps to `CurrentConditions` exactly as in F1. The `daily` object arrives as four **parallel arrays** (`time[]`, `temperature_2m_max[]`, `temperature_2m_min[]`, `weather_code[]`) zipped index-wise into `DailyForecast` entries: `time[i]` (ISO `yyyy-MM-dd` string) parsed to `DateOnly` **invariant-culture** (Seam 2); `weather_code[i]` through the WMO map (unknown code → "Unknown", never throws).
4. `State = Loaded`; the view renders Current Conditions plus the strip.

## Error handling

The F1 error model, unchanged in shape, with the daily block folded into the same atomic load. Every failure is an inline neutral message (Technical-Context) — never exception text, a stack trace, or the request URL (which carries coordinates).

| Situation | Behaviour |
|---|---|
| Network / timeout / non-2xx / `{"error":true,...}` envelope | `State = Error`, the fixed neutral line, same as F1. Whole load fails — no "current loaded but Forecast didn't" half-state. |
| `daily` arrays with **mismatched lengths**, or `daily` absent despite being requested | Malformed response → whole-load failure (`State = Error`). The strip is never zipped from ragged arrays. Rationale: it is one HTTP response — if the daily half is malformed the current half is equally suspect, and the PRD has no independent-Forecast-failure requirement (graceful-degradation work is F4's territory). |
| `daily` arrays equal-length but **not exactly 7 entries** | Tolerated — the strip renders the entries received. The contract expects 7 (provider default `forecast_days=7`); the count is not hard-coded into a crash. |
| Unknown `weather_code` on any day | That day's Condition = "Unknown"; the load still succeeds. |

## Testing

Per the Technical-Context standard: assert the **deterministic envelope**, never live weather values; the Open-Meteo HTTP boundary remains the recorded-replay seam. Feature-level coverage declaration:

**Tier-1 recorded-replay (every commit):**
- **Weather Provider client** — new fixture captured from the live current+daily call of 2026-07-02 (Seam 1 evidence). Assert: the daily parallel arrays zip into `DailyForecast` entries (date parse, high/low mapping, per-day WMO→Condition); the outbound request carries `daily=temperature_2m_max,temperature_2m_min,weather_code` and `timezone=auto` **and still** the `current` block + metric unit params (regression on the F1 half); mismatched-length daily arrays → error; absent `daily` → error; equal-length non-7 arrays → that many entries.
- **Daily date parse vs locale** — the Seam 2 proof: force a comma-decimal/non-ISO-default culture (e.g. `de-DE`) for the call and assert the parsed `DateOnly` values are identical to the invariant run.
- **WMO Condition map** — already covered by F1; daily reuses it. No new tests beyond any codes the daily fixture surfaces.
- **WeatherViewModel** (provider faked) — `Loaded` exposes both `Conditions` and `Forecast`; the failure path is unchanged (fixed neutral line, no exception text/URL).

**Tier-2 (live, scheduled, bounded):** extend the existing `OpenMeteoLiveTests` forecast call with the daily params; assert envelope only — `daily` present, four equal-length arrays, 7 entries, `daily_units` echoing `°C`, types/nullability — never values. Same call as before with more params, so the Tier-2 cost ceiling is unchanged (still one forecast call per run).

**Tier-3:** the shipped app, manual — search, pick, see Current Conditions and the week strip.

**Platform matrix:** F2 adds no filesystem/OS-touching code; the Windows-matrix obligation stays light (F3 picks it up). The two locale-sensitive touchpoints are covered: inbound date parse is pinned invariant (Seam 2); weekday labels are culture-formatted *display-only* output with no boundary on the far side (not a seam).

## Seam inventory

Two seams: the extended external network-protocol seam to the Open-Meteo forecast API (Seam 1 — superseding F1's Seam 2 contract), and the inbound host-OS/runtime locale seam on the daily date parse (Seam 2 — the inbound counterpart of F1's Seam 3). Both grounded by **live observation of the real Open-Meteo API on 2026-07-02** (two read-only GETs, captured during brainstorming with the human's approval) — not model memory. The in-process `WeatherViewModel → View` binding and the shell-mediated activation handoff are in-memory calls (code is the authority) and are deliberately excluded, as in F1.

**Auth:** inherited by reference from the F1 pin — Open-Meteo offers **no authentication**; we send none. Re-confirmed live 2026-07-02: unauthenticated GETs returned 200 with data.

### Seam 1: Weather Provider client ↔ Open-Meteo forecast API (current + daily)
- **(a) class:** network-protocol — **external**
- **(b) sides:** `WeatherProvider` (our typed `HttpClient`) ↔ Open-Meteo forecast service (`api.open-meteo.com/v1/forecast`)
- **(c) contract:** HTTPS `GET /v1/forecast?latitude=<lat>&longitude=<lon>&current=temperature_2m,weather_code,wind_speed_10m&daily=temperature_2m_max,temperature_2m_min,weather_code&temperature_unit=celsius&wind_speed_unit=kmh&timezone=auto`, no auth. Outbound coordinates invariant-formatted (F1 Seam 3, unchanged).
  - **Success (HTTP 200):** JSON object containing everything F1's Seam 2 pinned for `current`/`current_units`, **plus**:
    - **`daily`** (object, present because requested — treat absence as malformed → load failure): four **parallel arrays of equal length** — `time[]` (ISO `yyyy-MM-dd` strings, non-null), `temperature_2m_max[]` (numbers, non-null), `temperature_2m_min[]` (numbers, non-null), `weather_code[]` (integer WMO codes, non-null). Observed length **7** with no `forecast_days` param; the client requires equal lengths (else load failure) but tolerates a count other than 7.
    - **`daily[time][0]` is the Location's current calendar day** when `timezone=auto` is sent — the basis for the positional "Today" label. Proven by the cross-timezone observation below.
    - **`daily_units`** (object, non-null) echoes the units (`temperature_2m_max: "°C"`, `temperature_2m_min: "°C"`, `time: "iso8601"`) — the test's hook for asserting the metric params took effect on the daily half.
    - **`timezone`** (string, non-null, e.g. `"Europe/London"`, `"Asia/Tokyo"`) and **`utc_offset_seconds`** (integer) echo the resolved Location-local zone.
    - Returned `latitude`/`longitude` may be **grid-snapped** (observed: 51.5085/−0.1257 → 51.5/−0.25); we do not assert request==response coords.
  - **Error:** non-2xx / `{"error":true,"reason":<string>}` envelope → the inline "couldn't load weather" state (unchanged from F1).
- **(d) proof:** Tier-1 recorded-replay tests over fixtures captured from the 2026-07-02 live calls — current+daily happy path, mismatched-length daily (edited from the real payload), absent-`daily` — asserting the zip into `DailyForecast`, the outbound daily+timezone params, and both failure mappings. Tier-2 live call re-confirms the envelope on schedule.
- **(e) authority:** Open-Meteo Forecast API — live observation on 2026-07-02, two read-only GETs:
  - London (51.5085, −0.1257): HTTP 200 with `daily.time = ["2026-07-02" … "2026-07-08"]` (7 entries), `daily_units.temperature_2m_max = "°C"`, `timezone = "Europe/London"`, `current.time = "2026-07-02T10:30"` local.
  - Tokyo (35.6895, 139.6917): HTTP 200, `timezone = "Asia/Tokyo"`, `utc_offset_seconds = 32400`, `current.time = "2026-07-02T18:30"` — the same instant rendered Location-local (18:30 vs London's 10:30), proving `timezone=auto` aggregates in the Location's zone, with `daily.time[0] = "2026-07-02"` (Tokyo's current day).
  - Cross-references the public docs at open-meteo.com/en/docs (daily parameter set, `timezone=auto` behaviour).

### Seam 2: Daily date parse ↔ host locale
- **(a) class:** host-OS/runtime — **internal** (the boundary is the .NET runtime's culture-sensitive date parsing, not an external party)
- **(b) sides:** `OpenMeteoWeatherProvider.GetWeather` (mapping `daily.time[i]` strings into `DailyForecast.Date`) ↔ the .NET runtime's active `CultureInfo` on the host machine
- **(c) contract:** each `daily.time[i]` value (ISO `yyyy-MM-dd`, per Seam 1) is parsed to `DateOnly` **invariantly** — `DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture)` or equivalent — so the parsed date is **identical on every host locale**. A culture-default parse would make the accepted format and separators a function of `CurrentCulture`; the contract is that the host locale has **zero** influence on the parsed `Date`. (Weekday *display* labels are deliberately culture-formatted — that is presentation, not this seam.)
- **(d) proof:** Tier-1 test that forces a non-invariant culture (`CultureInfo.CurrentCulture = new CultureInfo("de-DE")` for the duration of the call) over the same recorded fixture and asserts the parsed `DailyForecast.Date` values equal the invariant run's — the locale is the only thing varied, mirroring the F1 Seam 3 test shape.
- **(e) authority:** *(internal seam — none required.)* Runtime behaviour cross-referenced at learn.microsoft.com (`DateOnly.ParseExact`, `CultureInfo.InvariantCulture`); the wire format being `yyyy-MM-dd` is established by the Seam 1 live observation of 2026-07-02.

## Out of scope (F2)

Hourly Forecast (F5); persistence / Location Store / launch-restore (F3); manual refresh, Updated-at stamp, keep-last-good, Retry (F4); unit preference (F6); weather icons; per-day extras (precipitation, sunrise/sunset, wind); configurable day count; independent Forecast-failure degradation (F4's graceful-failure work).

**Per-call request logging remains deferred** (F1 decision, 2026-06-30) — F2 changes the parameters of an existing call, not the logging posture; the deferral stands until a Feature needs it operationally.

## Feature-doc-gauntlet sign-off

- **Result:** fail
- **Date:** 2026-07-02
- **Summary:** Seam review raised three blockers (no F2 Plan so the seam proofs are unanchored; the positional-"Today" contract is asserted, not proven; the cited 2026-07-02 fixtures were never committed) and the consistency check raised two (the PRD still assigns the daily Forecast to Feature 1; no F2 Plan at the gate); the doc/ADR check passed.
- **Leaves:** check-seam-cynicism (fail), check-doc-adr-consistency (pass), check-artefact-consistency (fail)
- **Open findings:**
  1. *(check-seam-cynicism)* No F2 Plan exists (`docs/superpowers/plans/` holds only the F1 plan) — Seam 1 and Seam 2 each declare a (d) proof (Tier-1 recorded-replay fixtures, the de-DE locale-forcing test, the Tier-2 envelope extension) but no Plan Task carries them; both seams are Uncovered-in-the-Plan until `/writing-plans` runs.
  2. *(check-seam-cynicism)* Seam 1's sub-contract "`daily.time[0]` is the Location's current calendar day under `timezone=auto`" — the sole basis for the positional "Today" label — is not discriminated by the cited London/Tokyo observation: at that instant both locations (and UTC) shared calendar day 2026-07-02, so the evidence is equally consistent with "UTC's today" or "server's today". *Settleable in-session:* re-run the read-only GET against a pair currently on different calendar days (e.g. Pacific/Kiritimati UTC+14 vs Pacific/Midway UTC−11) and capture the observed `time[0]` divergence as the (e) evidence.
  3. *(check-seam-cynicism)* Seam 1 (d) cites "fixtures captured from the 2026-07-02 live calls", but no such fixture exists in the repo — `tests/WeatherApp.Tests/Fixtures/` holds only F1 payloads (`forecast-london.json` has `current` only, no `daily` block). *Settleable in-session:* re-issue the read-only current+daily GET and commit the raw response as the fixture.
  4. *(check-artefact-consistency)* PRD.md still says the 7-day daily Forecast is "a later Feature-1 slice" / "Feature 1" in two places, while the Roadmap and both specs assign it to Feature 2 — stale PRD narrative that, left as-is, gives an agent trusting the PRD (higher authority than Roadmap/Spec) the wrong feature ownership for exactly F2's scope. Update the two PRD sentences to name Feature 2 as a deliberate supersession.
  5. *(check-artefact-consistency)* No Plan exists for Feature 2 at the feature-docs gate — the gauntlet is defined to run after both `/brainstorming` and `/writing-plans`; `publish-feature` embeds the Plan and `enate-to-stories` halts without one. Run `/writing-plans` for F2, then re-run the gauntlet in full.

The Feature is **not cleared** for `/publish-feature` / `enate-to-stories`. Root-cause routing: findings 1 and 5 are the same root cause — `/writing-plans` has not run for F2 (that is the fix, not `/fix-feature-docs`); findings 2–4 feed `/fix-feature-docs` (2 and 3 are settleable in-session). Re-run `/feature-doc-gauntlet` in full afterwards.
