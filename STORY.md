## Parent

#95088

## Type

AFK — autonomously deliverable.

## What to build

The `Geocoder` deep module: a typed `HttpClient` against the Open-Meteo geocoding API that resolves a query string to a read-only list of `LocationCandidate`. Signature: `Search(string query, CancellationToken ct)` returning an `IReadOnlyList` of `LocationCandidate`. Implements Seam 1's wire contract: `GET /v1/search?name=...&amp;count=...&amp;language=en&amp;format=json`, no auth. The candidate array lives under `results`, which is **absent for zero matches** — absent `results` MUST map to an empty list, never a null-deref. Each candidate has `name`/`latitude`/`longitude`/`country` (non-null) and `admin1` (nullable). A non-2xx or `error:true` body surfaces as the caller's error path.

Proven by recorded-replay: real `HttpClient` + `System.Text.Json` over recorded bytes via an injected `HttpMessageHandler` stub. Fixtures captured live 2026-06-29: many-candidate, single-candidate, zero-results (no `results` key), error-400.

**Security (added by the security pass):** URL-encode the search query when building the request so query-significant characters cannot inject or forge extra query parameters; bound the call with a finite timeout and honour the `CancellationToken`, failing closed to the caller's error path rather than hanging.

## Acceptance criteria

- [ ] `Search` parses candidate fields including nullable `admin1` (tolerated when absent).
- [ ] Absent `results` key maps to an empty list (the zero-results fixture).
- [ ] Outbound request carries `name` and `count` query params.
- [ ] Non-2xx / `error:true` body is surfaced as a failure the caller can show inline.
- [ ] Tier-1 recorded-replay tests over all four fixtures pass via the stub handler.

### Security acceptance criteria

- [ ] A search query containing query-significant characters (e.g. `London&amp;count=999`, a `#`, or whitespace/CRLF) is transmitted as a single URL-encoded `name` value — the outbound request carries no injected/forged extra query parameters and still sends exactly the intended `name`/`count`. (Test: a stub handler captures the outbound URI and asserts the query string.)
- [ ] A geocoding call that exceeds a finite timeout, or whose `CancellationToken` is cancelled, fails closed — surfaced as the inline &quot;couldn't search&quot; error, never an indefinite hang or unhandled exception. (Test: a delaying/cancelling stub handler asserts the error path.)

## Context references

- **Plan**: `docs/superpowers/plans/2026-06-29-feature1-current-weather.md` (Task 4)
- **Spec**: `docs/superpowers/specs/2026-06-29-feature1-current-weather-design.md` (Seam 1)
- `business-domain-context.md` (Context.MD), `Technical-Context.MD` (security principles: #1 No secrets; never expose personal location beyond the request)
- ADR: `docs/adr/0001-persist-location-only-never-cache-weather.md`

## Blocked by

- #95144 (Domain records + WMO map — needs `LocationCandidate`).

## Blocks

- &quot;SearchViewModel&quot; (#95147) and &quot;Tier-2 live contract tests&quot; (#95151).