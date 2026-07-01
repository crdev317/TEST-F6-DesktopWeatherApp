## Parent

#95088

## Type

AFK — autonomously deliverable.

## What to build

Tier-2 live tests that make one real call each to the Open-Meteo geocoding and forecast endpoints and confirm the recorded fixtures still match the live contract — fields present, types/nullability — **never asserting on volatile weather values**. Tagged with a `Tier=Live` trait so they run only on schedule, not on every commit. Tier-1 runs exclude them (`dotnet test --filter Tier!=Live`); the live run is `dotnet test --filter Tier=Live`.

This depends only on the two HTTP clients, so it can be picked up in parallel with the ViewModel/shell work — it does not need the shell.

## Acceptance criteria

- [ ] A live geocoder test calls the real endpoint and asserts candidates come back with non-empty name/country (shape, not value).
- [ ] A live forecast test calls the real endpoint for known coords and asserts a non-empty Condition (shape, not value).
- [ ] Both carry the `Tier=Live` trait; the every-commit Tier-1 run excludes them.

## Context references

- **Plan**: `docs/superpowers/plans/2026-06-29-feature1-current-weather.md` (Task 10, step 1)
- **Spec**: `docs/superpowers/specs/2026-06-29-feature1-current-weather-design.md` (Testing — Tier-2)
- `business-domain-context.md` (Context.MD), `Technical-Context.MD`
- ADR: `docs/adr/0001-persist-location-only-never-cache-weather.md`

## Blocked by

- #95145 (Geocoder client) and #95146 (Weather Provider client).

## Blocks

- None.