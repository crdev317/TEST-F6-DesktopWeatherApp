## Parent

#95088

## Type

AFK — autonomously deliverable.

## What to build

`SearchViewModel` plus its injected debounce clock (`IDebounceScheduler` + a real `DebounceScheduler`). Owns `Query`, `Candidates`, a search message (zero-results / error), and `SelectCommand`; raises `LocationSelected(Location)`. Behaviour: typing updates `Query`; **fewer than 2 characters** clears `Candidates` and makes no call; after a 300 ms debounce it captures a monotonically increasing `searchSeq`, cancels any prior in-flight token, and calls `Geocoder.Search`; on response it applies results **only if this is the latest `searchSeq`** (guard-on-arrival), else drops them. Zero results gives &quot;No places found&quot;; a Geocoder failure gives an inline &quot;Couldn't search right now…&quot;. `SelectCommand` builds a `Location` from the chosen candidate and raises `LocationSelected`.

Tested with a fake Geocoder at the seam and a manual (fake) debounce clock — no real time, no real network.

**Security (added by the security pass):** the inline search-failure message must be the fixed neutral copy only — it must never surface the raw exception text/stack trace or the request URL (which carries the query), per the Technical-Context &quot;no stack trace in UI&quot; / &quot;don't expose personal data beyond the request&quot; principles.

## Acceptance criteria

- [ ] Debounce fires the search once after the interval; rapid typing collapses to one call.
- [ ] A `Query` of fewer than 2 characters clears candidates and makes no Geocoder call.
- [ ] Sequence-guard: with out-of-order fake responses, only the latest `searchSeq` renders.
- [ ] Zero candidates gives a &quot;No places found&quot; message (not an error).
- [ ] A Geocoder failure gives an inline &quot;Couldn't search…&quot; message; the active view is unchanged.
- [ ] `SelectCommand` raises `LocationSelected` with the correct `Location` (name + coords).
- [ ] Tier-1 ViewModel tests (fake seam, fake clock) pass.

### Security acceptance criteria

- [ ] On any Geocoder failure (network / timeout / HTTP / JSON), the surfaced `SearchMessage` equals the fixed neutral copy and contains no exception type name, no stack trace, and no request URL or query echo. (Test: induce each failure via the fake seam and assert the message string contains none of those.)

## Context references

- **Plan**: `docs/superpowers/plans/2026-06-29-feature1-current-weather.md` (Task 6)
- **Spec**: `docs/superpowers/specs/2026-06-29-feature1-current-weather-design.md`
- `business-domain-context.md` (Context.MD), `Technical-Context.MD` (security principles: no raw stack trace in UI; don't expose personal data beyond the request)
- ADR: `docs/adr/0001-persist-location-only-never-cache-weather.md`

## Blocked by

- #95145 (Geocoder client) — and transitively #95144 (domain records).

## Blocks

- &quot;MainViewModel — activation handoff&quot; (#95149).