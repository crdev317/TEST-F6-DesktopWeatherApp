# Changelog

All notable changes to TEST-F6-DesktopWeatherApp are recorded here. The **why** matters as
much as the **what**.

## [Unreleased] - 2026-07-01

### Added
- **Pure, I/O-free domain core** (`WeatherApp.Core`) — the substrate every later Feature-1
  slice (Geocoder, Weather Provider client, ViewModels) binds to, built first so those slices
  have concrete types to consume:
  - `Location(Name, Latitude, Longitude)` — the single active place weather is shown for.
  - `LocationCandidate(Name, Admin1?, Country, Latitude, Longitude)` — a Location Search result.
    `Admin1` (region) is **nullable** because the Geocoder omits it for some places.
  - `CurrentConditions(TemperatureC, WindSpeedKmh, Condition)` — present-moment weather in
    fixed metric units.
  - All three are immutable `sealed record` types.
- **`WmoConditionMap.ToCondition(int)`** — maps Open-Meteo WMO weather codes to human-readable
  condition labels. Deliberately **pure and total**: an unrecognised code returns `"Unknown"`
  rather than throwing, so downstream code never has to guard the mapping call.
- **Tier-1 unit tests** for the domain records and the WMO map (known codes plus the
  unknown-code fallback), establishing the first `dotnet test`-green coverage in the repo.

### Notes
- No Geocoder, Weather Provider client, Location Store, or ViewModels yet — those are separate
  Feature-1 stories that depend on this core (see `Roadmap.md`).
- `coverlet.collector` is present in the test project for code-coverage collection; recorded
  in `Technical-Context.MD` Packages-in-use.
