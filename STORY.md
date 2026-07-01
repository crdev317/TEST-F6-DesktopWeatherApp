## Parent

#95088

## Type

AFK — autonomously deliverable.

## What to build

Scaffold the whole solution so every later slice has somewhere to land: a `net8.0` Core class library (`WeatherApp.Core`, no WPF), a `net8.0-windows` WPF shell (`WeatherApp`), and a `net8.0` xUnit test project (`WeatherApp.Tests`), wired into one `WeatherApp.sln` with project references (shell → Core, tests → Core) and the agreed packages (CommunityToolkit.Mvvm + Microsoft.Extensions.Http on Core; Microsoft.Extensions.Hosting on the shell; FluentAssertions + Moq on tests). Mark test JSON fixtures to copy to output. Prove the substrate runs with a trivial smoke test.

## Acceptance criteria

- [ ] `WeatherApp.sln` contains the three projects with the correct target frameworks (`net8.0`, `net8.0-windows`, `net8.0`).
- [ ] Project references wired: shell → Core, tests → Core.
- [ ] Packages added per Plan Task 1 Step 2; `Fixtures\**\*.json` set to `CopyToOutputDirectory=PreserveNewest`.
- [ ] A smoke test exists; `dotnet build &amp;&amp; dotnet test` succeeds with 1 passing test.

## Context references

- **Plan**: `docs/superpowers/plans/2026-06-29-feature1-current-weather.md`
- **Spec**: `docs/superpowers/specs/2026-06-29-feature1-current-weather-design.md`
- `business-domain-context.md` (Context.MD), `Technical-Context.MD`
- ADR: `docs/adr/0001-persist-location-only-never-cache-weather.md`

## Blocked by

None - can start immediately.

## Blocks

- Every other F1 story (this is the foundation). Directly unblocks &quot;Domain records + WMO Condition map&quot;.