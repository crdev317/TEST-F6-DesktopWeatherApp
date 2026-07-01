## Parent

#95088

## Type

AFK — autonomously deliverable.

## What to build

`WeatherViewModel`: holds the Current Conditions for a Location and drives a load state machine. `Load(Location)` moves `State` Idle → Loading, calls the Weather Provider client, maps the result into `Conditions`, then → Loaded; a failure moves it → Error with a display message. Exposes `Conditions`, `State`, `LocationName`, and an error message for the view to bind. No Retry in F1 (deferred to F4) — recovery is re-selecting the candidate.

Tested with a fake Weather Provider at the seam.

**Security (added by the security pass):** the Error-state display message must be the fixed neutral copy only — it must never surface the raw exception text/stack trace or the request URL (which carries the Location's coordinates), per the Technical-Context &quot;no stack trace in UI&quot; / &quot;don't expose personal location beyond the request&quot; principles.

## Acceptance criteria

- [ ] `Load` drives State Idle → Loading → Loaded on success and exposes the mapped `CurrentConditions` (incl. Condition text).
- [ ] `LocationName` reflects the loaded Location.
- [ ] A provider failure drives State → Error with an inline message; no crash, no Retry affordance.
- [ ] Tier-1 ViewModel tests (fake seam) pass.

### Security acceptance criteria

- [ ] On a weather-load failure (network / timeout / HTTP / JSON), the Error-state message equals the fixed neutral copy and contains no exception type name, no stack trace, and no request URL or coordinate echo. (Test: induce the failure via the fake seam and assert the message string contains none of those.)

## Context references

- **Plan**: `docs/superpowers/plans/2026-06-29-feature1-current-weather.md` (Task 7)
- **Spec**: `docs/superpowers/specs/2026-06-29-feature1-current-weather-design.md`
- `business-domain-context.md` (Context.MD), `Technical-Context.MD` (security principles: no raw stack trace in UI; don't expose personal location beyond the request)
- ADR: `docs/adr/0001-persist-location-only-never-cache-weather.md`

## Blocked by

- #95146 (Weather Provider client) — and transitively #95144 (domain records).

## Blocks

- &quot;MainViewModel — activation handoff&quot;.