## Parent

#95088

## Type

AFK — autonomously deliverable.

## What to build

The pure, I/O-free core every other slice binds to: the three immutable domain records and the total WMO-code→Condition map.

- `LocationCandidate(Name, Admin1?, Country, Latitude, Longitude)` — `Admin1` (region) is **nullable** (the Geocoder omits it for some places).
- `Location(Name, Latitude, Longitude)` — the single active place.
- `CurrentConditions(TemperatureC, WindSpeedKmh, Condition)`.
- `WmoConditionMap.ToCondition(int)` — maps known WMO codes to labels; an unrecognised code returns &quot;Unknown&quot; and the map never throws (pure and total).

## Acceptance criteria

- [ ] The three records exist as immutable `sealed record` types with the fields and nullability above.
- [ ] `WmoConditionMap.ToCondition` returns the expected label for known codes (e.g. 0→Clear sky, 2→Partly cloudy, 45→Fog, 61→Slight rain, 71→Slight snowfall, 95→Thunderstorm).
- [ ] An unknown code returns &quot;Unknown&quot;; the map never throws.
- [ ] Tier-1 unit tests cover known codes + the unknown-code fallback; `dotnet test` green.

## Context references

- **Plan**: `docs/superpowers/plans/2026-06-29-feature1-current-weather.md`
- **Spec**: `docs/superpowers/specs/2026-06-29-feature1-current-weather-design.md`
- `business-domain-context.md` (Context.MD), `Technical-Context.MD`
- ADR: `docs/adr/0001-persist-location-only-never-cache-weather.md`

## Blocked by

- #95143 (Scaffold the solution).

## Blocks

- &quot;Geocoder client (Seam 1)&quot;, &quot;Weather Provider client (Seams 2 &amp; 3)&quot;, and transitively the ViewModels (all consume these types).