# PRD — Desktop Weather App

> Vocabulary follows `business-domain-context.md` (**Location**, **Location Search**, **Geocoder**, **Current Conditions**, **Forecast**, **Weather Provider**). Respects `docs/adr/0001-persist-location-only-never-cache-weather.md`.

## Problem Statement

A person wants to know the weather for a place they care about — what it's doing right now and what the next week looks like — without opening a browser, wading through ads, or signing into anything. They want to type a place name, see the weather, and have the app remember that place next time they open it.

## Solution

A Windows desktop app that does exactly one thing well: you search for a place by name, pick it from the matches, and see its **Current Conditions** plus a 7-day daily **Forecast**. The app remembers the place you last chose and shows it again — freshly fetched — the next time you launch. It always tells you how current the data is, and when something goes wrong it says so plainly in-app rather than failing silently or crashing.

## Requirements

1. As a user, I want to search for a place by typing its name, so that I don't have to know its coordinates.
2. As a user, I want to see a list of matching places when my search is ambiguous (e.g. "Paris"), so that I can choose the one I mean.
3. As a user, I want each candidate to show enough detail to tell them apart (place name, region/admin area, country), so that I can distinguish Paris, France from Paris, Texas.
4. As a user, I want to explicitly pick a candidate to make it the active **Location**, so that the app never guesses wrong on my behalf.
5. As a user, I want a single result to still be presented for me to confirm rather than auto-selected, so that selecting a Location is always a deliberate act.
6. As a user, I want a clear "no places found" message when my search matches nothing, so that I understand the search ran and simply found nothing.
7. As a user, I want a failed or empty search to leave my current weather view untouched, so that a mistyped search doesn't destroy the place I was already looking at.
8. As a user, I want to see the **Current Conditions** for the active Location (temperature, weather condition, wind), so that I know what it's like right now.
9. As a user, I want to see a 7-day daily **Forecast** (per-day high/low and condition), so that I can see the week ahead at a glance.
10. As a user, I want temperatures and wind shown in metric units (°C, km/h), so that the readings are consistent and predictable.
11. As a user, I want the app to remember the **Location** I last selected and show it again on the next launch, so that I don't have to search for my usual place every time.
12. As a user, I want the app to fetch fresh weather every time it shows a Location (on launch-restore and on selection), so that I'm never shown stale data presented as the present moment.
13. As a user, I want a manual refresh control, so that I can pull the latest conditions on demand (e.g. "is it still raining?").
14. As a user, I want to see an **Updated-at** time on the weather, so that I always know how current the displayed data is.
15. As a user, I want the **Updated-at** time to reflect when the app last successfully fetched, so that "freshness" means "when the app last updated," which is what I expect.
16. As a first-time user, I want an empty state with a clear prompt to search, so that I know how to start when nothing is saved yet.
17. As a user, I want a failed refresh to keep showing my last good weather (clearly stamped with its Updated-at time) rather than blanking the screen, so that a momentary network blip doesn't wipe out useful information.
18. As a user, I want a clear in-app error (with a retry option) when weather can't be loaded for a freshly selected Location, so that I understand the load failed and can try again.
19. As a user, I want a clear in-app message when the **Location Search** itself can't reach the **Geocoder**, so that I know it's a connectivity problem and not a missing place.
20. As a user, I want all errors shown as plain in-app messages — never a stack trace, crash dialog, or silent failure — so that the app always feels trustworthy.
21. As a user, I want loading states to be visible while data is being fetched, so that I know the app is working and not frozen.
22. As a user, I want concise, neutral copy throughout, so that the app states the weather plainly without decoration.

## Implementation Decisions

**Stack** — WPF on .NET 8 (C#), MVVM via `CommunityToolkit.Mvvm`, `IHttpClientFactory` for HTTP, `System.Text.Json` for (de)serialisation, `Microsoft.Extensions.*` for DI/config/logging (per Technical-Context.MD). Open-Meteo is the **Weather Provider** and the **Geocoder** today (keyless, free).

**Modules:**

- **Geocoder** (deep) — interface: `query string → Location candidates` (zero, one, or many). Wraps Open-Meteo's geocoding endpoint (`geocoding-api.open-meteo.com`); hides HTTP/JSON; returns domain `Location` candidates carrying coordinates, name, admin region, and country. Named as its own concern, separate from the Weather Provider, so the geocoding source can be swapped independently.
- **Weather Provider client** (deep) — interface: `Location → (Current Conditions + 7-day daily Forecast)`. Wraps Open-Meteo's weather endpoint (`api.open-meteo.com`) requesting the current block plus the daily array, with **fixed metric unit params** (`temperature_unit=celsius`, `wind_speed_unit=kmh`). Returns the domain weather model; never exposes raw provider JSON upward.
- **Location Store** (deep) — interface: `save / load the single active Location`. Persists the active Location's **identity only** (coordinates + name) to local user/OS config. **Never persists any weather payload** (ADR-0001). There is no collection of saved locations — exactly one active Location.
- **Search ViewModel** — orchestrates a **Location Search**: invokes the Geocoder, exposes the candidate list, handles the zero-results message (preserving current state), and on explicit selection sets the active Location and triggers a weather fetch.
- **Weather ViewModel** — on activation (selection or launch-restore) and on manual refresh, fetches fresh from the Weather Provider client; holds Current Conditions, the 7-day Forecast, the **Updated-at** stamp, and loading/error state. Applies the rules: always fetch fresh; on refresh failure keep last-good + Updated-at; on fresh-load failure show error + retry.
- **Views (XAML)** — empty state with search prompt (first run); search box + candidate list; weather view (Current Conditions + 7-day daily strip + Updated-at); inline status line for errors and loading.

**Key behavioural contracts:**

- **Activation** = a Location becoming active, via either explicit candidate selection or launch-restore of the persisted Location. Every activation triggers a fresh fetch.
- **Persistence boundary** — only the Location's identity crosses into storage; the weather model never does (ADR-0001).
- **Updated-at** is the app's last-successful-fetch time, not a provider observation time; it is always displayed.
- **Refresh-failure** keeps the last good weather in-session only; it is never persisted across restarts.
- **Error surface** — all failures (Geocoder unreachable, Weather Provider unreachable, fresh-load failure) render as inline in-app messages per the Technical-Context User Feedback Approach.

## Testing Decisions

**What makes a good test here:** assert on the **deterministic envelope** — parsed/transformed output shape, state transitions, side-effect ordering — never on volatile live weather values (per Technical-Context "test the contract, not the live service"). The replayable seam is the **Open-Meteo HTTP API**: record real responses as fixtures rather than asserting on live data.

**Modules under test (all of them):**

- **Geocoder** — **Tier-1 recorded-replay** against recorded Open-Meteo geocoding fixtures: many-candidate parsing, single-candidate, zero-results (empty response), malformed/error response handling, and that candidates carry name/region/country/coordinates.
- **Weather Provider client** — **Tier-1 recorded-replay** against recorded weather fixtures: the current+daily transform into the domain model, correct metric unit params on the request, the 7-day daily shape, and error/empty-response handling.
- **Weather ViewModel** — unit tests over state logic with the Weather Provider client faked: activation→loading→loaded, refresh-failure keeps last-good + Updated-at, fresh-load failure shows error + retry, Updated-at set on successful fetch.
- **Search ViewModel** — unit tests with the Geocoder faked: zero/one/many candidate handling, zero-results preserves current state, explicit-select sets the active Location and triggers a fetch.
- **Location Store** — a **real-IO** round-trip test: save the active Location, load it back, assert identity is preserved and no weather payload is stored.

**Tiers / matrix:** Tier-1 (recorded-replay) on every commit; Tier-2 (live Open-Meteo, bounded/scheduled) confirms the recorded fixtures still match the real contract; platform matrix is **Windows** (the OS-touching code is the Location Store's local persistence). Coverage is planned at the Feature level per the testing standard.

**Prior art:** none yet — this is the first code in the repo. These Tier-1 recorded-replay tests at the HTTP seam become the prior art future Features reference.

## Out of Scope

- **Hourly Forecast** — deferred to a follow-on Feature (the glossary's Forecast term already permits hour-level breakdown; v1 ships daily only).
- **Unit preferences / settings surface** — fixed metric in v1; a persisted user unit preference (°C/°F toggle) is a follow-on Feature.
- **Saved / favourite locations** — explicitly excluded; there is exactly one active Location (per glossary).
- **Auto-detection of location** via IP/OS — rejected as the selection mechanism and for first-run; selection is always Location Search.
- **Background polling / auto-refresh** — refresh is on-activation + manual only.
- **Offline mode / weather caching** — no weather payload is persisted (ADR-0001); launch always fetches fresh, with no offline display.
- **Cross-platform** — Windows desktop only; no macOS/Linux, no mobile, no web.
- **Notifications / alerts / email** — the in-app WPF UI is the only feedback channel.

## Further Notes

- This PRD scopes the product as a whole; the grilled "search → Current Conditions + 7-day daily Forecast" slice is **Feature 1** when `/roadmap` breaks this down. Hourly Forecast and a units preference are the natural follow-on Features.
- ADR-0001 is the load-bearing architectural constraint — any future move toward caching or offline support must revisit it explicitly rather than quietly adding a cache.
- The Geocoder / Weather Provider split in the glossary means the geocoding source and weather source can diverge later without disturbing the domain language.
