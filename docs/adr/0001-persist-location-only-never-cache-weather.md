---
status: accepted
---

# Persist the Location only; never cache the weather payload

We persist the active **Location**'s identity (coordinates + name) across restarts, but we **never** persist or cache the **Current Conditions** / **Forecast** payload. The weather is fetched fresh on every activation (Location selected, or launch restoring the persisted Location) and on explicit user refresh. We chose this so the glossary's defining claim — Current Conditions are the weather *"right now"* — stays true: a persisted payload replayed on the next launch would be presenting stale data as the present moment.

## Considered Options

- **Persist Location only, always re-fetch (chosen).** Freshness and glossary integrity at the cost of more Open-Meteo calls and no offline display.
- **Cache the last weather payload across restarts.** Instant display on launch and offline-friendly, but it would show stale data labelled as "right now" — directly contradicting the glossary — and invites the next engineer to "optimise" freshness away.

## Consequences

- On every launch the user briefly sees a load state while fresh data arrives; there is no offline mode.
- A *failed refresh* is the one time stale data is shown — but it is kept only **in-session** and stamped with its **Updated-at** time (the app's last-successful-fetch time), so it is never mislabelled as the present moment. It is still not persisted across restarts.
- More frequent calls to the Weather Provider; acceptable given Open-Meteo is keyless and free, and there is no background polling.
