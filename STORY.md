## Parent

#95088

## Type

AFK — autonomously deliverable.

## What to build

`MainViewModel`: the shell that owns the two child ViewModels and mediates the activation handoff (Approach A — shell-mediated composition). It subscribes to `SearchViewModel.LocationSelected`; on selection it sets the active Location, flips `ViewState` from Empty to Weather, and calls `WeatherViewModel.Load(loc)`. The two child ViewModels never reference each other. Starts in the Empty state.

Tested with the real child ViewModels wired to fake seams (this is the in-process activation integration point named in the Spec — covered here, not as a taxonomy seam).

## Acceptance criteria

- [ ] Starts in `ViewState` = Empty.
- [ ] A `LocationSelected` from the search child sets the active Location, flips `ViewState` to Weather, and triggers `WeatherViewModel.Load`.
- [ ] After load, the weather child reaches Loaded with the selected Location's name.
- [ ] Tier-1 integration test over the wired ViewModels passes; full suite green.

## Context references

- **Plan**: `docs/superpowers/plans/2026-06-29-feature1-current-weather.md` (Task 8)
- **Spec**: `docs/superpowers/specs/2026-06-29-feature1-current-weather-design.md`
- `business-domain-context.md` (Context.MD), `Technical-Context.MD`
- ADR: `docs/adr/0001-persist-location-only-never-cache-weather.md`

## Blocked by

- #95147 (SearchViewModel) and #95148 (WeatherViewModel).

## Blocks

- &quot;WPF shell — host wiring, DI &amp; views&quot;.