# Roadmap

**Product:** Desktop Weather App — search a place, see its current weather and forecast (Windows / WPF / Open-Meteo).
**Last reviewed:** 2026-06-29

## Sequencing

Features are listed in delivery order. Each Feature gets its own `/brainstorming` session, Spec, and Plan. Features 1–4 are the PRD v1 core; Features 5–6 are the PRD's explicit post-v1 follow-ons.

---

## Feature 1: See the current weather for a place you search 🔫 *tracer bullet*

**Status:** **Published to ADO** — Feature [#95088](https://dev.azure.com/Enate/4158c5e2-092d-482d-a445-7e910ffbe775/_workitems/edit/95088), 2026-06-29 (Spec → Description, Plan → Implementation Plan field).

The app opens to an empty state with a search prompt. The user types a place name, the **Geocoder** returns candidates, and the user explicitly picks one — making it the active **Location**. The app then fetches and shows that Location's **Current Conditions** (temperature, condition, wind) in fixed metric units.

**Out of scope:** the 7-day daily **Forecast**; persistence across restarts; manual refresh and the **Updated-at** stamp; polished failure states (keep-last-good, retry affordances). F1 carries only the minimal inline "couldn't load" / "no places found" messages needed to not crash.

**Dependencies:** None (this is the tracer bullet).

**Why first:** it threads every architectural layer end-to-end — XAML View → Search ViewModel → **Geocoder** (Open-Meteo HTTP seam #1) → Weather ViewModel → **Weather Provider client** (Open-Meteo HTTP seam #2) → domain model → View — plus DI/host wiring. It exercises *both* external seams and the full MVVM spine and produces something a user can watch work. The candidate-pick stays in (it's real product on the critical path); only a throwaway auto-pick would be thinner, and that was already rejected.

---

## Feature 2: See the 7-day daily Forecast for the active Location

**Status:** **Published to ADO** — Feature [#95248](https://dev.azure.com/Enate/4158c5e2-092d-482d-a445-7e910ffbe775/_workitems/edit/95248), 2026-07-02 (Spec → Description, Plan → Implementation Plan field).

The weather view gains a 7-day daily **Forecast** strip — per-day high/low and condition — fetched from the **Weather Provider client** alongside Current Conditions. Completes the core "what's it like now *and* this week" value.

**Out of scope:** hourly Forecast (Feature 5); persistence; refresh.

**Dependencies:** Feature 1 (specifically: the active Location, the Weather Provider client, and the weather view it extends). Independent of Features 3–4, so its order relative to them is flexible.

---

## Feature 3: The app remembers your place across restarts

The active **Location**'s identity (coordinates + name) is persisted via the **Location Store**, so relaunching restores the last place and immediately fetches its weather fresh, rather than opening to the empty state. The first-run empty state from Feature 1 becomes the fallback shown only when nothing is saved.

**Out of scope:** a collection of saved/favourite locations (explicitly excluded — exactly one active Location); manual refresh and Updated-at (Feature 4); caching the weather payload (forbidden by ADR-0001).

**Dependencies:** Feature 1 (specifically: the active Location concept and the activation→fetch path it restores into). Independent of Feature 2.

**Why here:** it makes ADR-0001 real in code — persist the Location's identity only, never the weather, and always re-fetch on restore. It is also the first Windows-OS-touching code (local persistence), where the testing standard's Windows platform matrix first applies.

---

## Feature 4: Refresh on demand, with visible freshness and graceful failure

The weather view gains a manual **refresh** control and an always-visible **Updated-at** stamp (the app's last-successful-fetch time). When a refresh fails, the last-good weather stays on screen — stamped with its Updated-at time — rather than blanking, with an inline note that the refresh failed. Fresh-load failures (a just-selected Location that can't load) get their proper error + **Retry** affordance. Closes out the PRD v1 core.

**Out of scope:** background polling / auto-refresh (explicitly excluded — refresh is on-activation + manual only); caching across restarts (ADR-0001).

**Dependencies:** Feature 1 (the activation→fetch path and weather view). Interacts with Feature 3 (on launch-restore the Updated-at reflects that restore-time fetch) but does not strictly require it.

---

## Feature 5: See an hourly Forecast for the near term *(post-v1)*

The Forecast view gains an hourly breakdown (next ~24–48h) alongside the daily strip, fetched from the **Weather Provider client**'s hourly array. The glossary's **Forecast** term already permits hour-level breakdown, so no vocabulary change is needed — this realises the half of the term v1 deferred.

**Out of scope:** configurable horizon length; charts/graphs (a plain hourly list suffices).

**Dependencies:** Feature 2 (specifically: it extends the Forecast view and the same Weather Provider client call, requesting the hourly block too).

---

## Feature 6: Choose your units *(post-v1)*

A user-facing preference to switch between metric (°C, km/h) and imperial (°F, mph), persisted so it survives restarts and applied to both Current Conditions and the Forecast. Open-Meteo takes units as request params, so this is a stored choice fed into the client calls — not a conversion the app writes.

**Out of scope:** per-quantity unit mixing (e.g. °C with mph); locale auto-detection of units (the preference is explicit).

**Dependencies:** Feature 1 (the metric display it makes configurable) and Feature 3 (it reuses the local-state persistence pattern from the Location Store, introducing a second piece of persisted state — a Settings/preferences concept).
