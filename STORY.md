## Parent

#95088

## Type

AFK — autonomously deliverable.

## What to build

The `WeatherProvider` deep module: a typed `HttpClient` against the Open-Meteo forecast API. Signature: `GetCurrent(Location loc, CancellationToken ct)` returning a `CurrentConditions`. Implements **Seam 2** (`GET /v1/forecast?latitude=...&amp;longitude=...&amp;current=temperature_2m,weather_code,wind_speed_10m&amp;temperature_unit=celsius&amp;wind_speed_unit=kmh`, no auth) — maps the `current` block to `CurrentConditions`, mapping `weather_code` through the WMO map. Also implements **Seam 3** (host-OS/locale): coordinates are serialised with `CultureInfo.InvariantCulture` so the decimal separator is always `.` regardless of host locale (a `de-DE` host must not emit `latitude=51,5`).

Proven by recorded-replay over a fixture captured live 2026-06-29, plus a locale-forcing regression test.

**Security (added by the security pass):** bound the forecast call with a finite timeout and honour the `CancellationToken`, failing closed to the caller's load-failure path rather than hanging.

## Acceptance criteria

- [ ] `current` block maps to `CurrentConditions` (temperature, wind, Condition via the WMO map).
- [ ] Outbound request carries `temperature_unit=celsius` and `wind_speed_unit=kmh`.
- [ ] `weather_code` parsed and mapped; grid-snapped response coords tolerated (no request-equals-response coord assertion).
- [ ] Non-2xx / `error:true` surfaced as the caller's load-failure path.
- [ ] **Seam 3 test**: forcing `CultureInfo.CurrentCulture` to `de-DE` for the call, the outbound query still contains `latitude=51.5085` (dot-decimal), proving invariant formatting.
- [ ] Tier-1 recorded-replay tests pass via the stub handler.

### Security acceptance criteria

- [ ] A forecast call that exceeds a finite timeout, or whose `CancellationToken` is cancelled, fails closed — surfaced as the inline &quot;couldn't load weather&quot; error, never an indefinite hang or unhandled exception. (Test: a delaying/cancelling stub handler asserts the error path.)

## Context references

- **Plan**: `docs/superpowers/plans/2026-06-29-feature1-current-weather.md` (Task 5)
- **Spec**: `docs/superpowers/specs/2026-06-29-feature1-current-weather-design.md` (Seams 2 and 3)
- `business-domain-context.md` (Context.MD), `Technical-Context.MD` (security principles: never expose personal location beyond the request)
- ADR: `docs/adr/0001-persist-location-only-never-cache-weather.md`

## Blocked by

- #95144 (Domain records + WMO map — needs `Location`, `CurrentConditions`, `WmoConditionMap`).

## Blocks

- &quot;WeatherViewModel&quot; (#95148) and &quot;Tier-2 live contract tests&quot; (#95151).