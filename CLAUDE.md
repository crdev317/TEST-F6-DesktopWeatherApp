# CLAUDE.md — agent orientation

You are working in **TEST-F6-DesktopWeatherApp**, a product built with the **Enate SDLC Factory**.
This file is the *agent* front door (auto-loaded every session); `README.md` is the human one.

## Read this first — and follow the flow

This product is built by walking the Factory's **HITL → AFK** flow. **Before you act, read
the field guide and follow the flow it describes:**

➡️ **[Using the Enate SDLC Factory](https://github.com/kitcox-dev/enate-claude-skills/blob/main/docs/using-the-sdlc-factory.md)**

The guide is the source of truth for *which skill to fire when*. The single rule it hinges on,
which you must never break: **only a human moves a Story to `Agent Ready`** — that is the HITL→AFK
handoff; the orchestrator owns every other transition.

## The documentation fabric (load before you plan or build)

Authority order (lower wins): **ADR > Technical-Context > business-domain-context > PRD > Roadmap > Spec > Plan.**

- **`Technical-Context.MD`** — the engineering contract every code-writing agent must respect
  (principles, secure-coding baseline, branching, and the **Testing & the ratchet** standard).
- **`business-domain-context.md`** — the domain glossary (the project's language).
- **`PRD.md`** · **`Roadmap.md`** — product requirements; the ordered Feature list.
- **`docs/adr/`** — architectural decisions (highest authority).
- **`docs/superpowers/specs/`** · **`plans/`** — per-Feature Spec and Plan (the Plan carries
  the **Context references** an agent loads).

## Dev commands

Stack: **WPF on .NET 8 (C#)**, built and tested with the `dotnet` CLI. Solution: `WeatherApp.sln`.

- **Restore:** `dotnet restore WeatherApp.sln` (uses the repo-local `nuget.config` pinning nuget.org).
- **Build:** `dotnet build WeatherApp.sln`
- **Test:** `dotnet test WeatherApp.sln` (xUnit; Tier-1 recorded-replay tests read `Fixtures/**/*.json` copied to output).
- **Run:** `dotnet run --project src/WeatherApp/WeatherApp.csproj`

Layout: `src/WeatherApp.Core/` (class lib, net8.0), `src/WeatherApp/` (WPF shell, net8.0-windows),
`tests/WeatherApp.Tests/` (xUnit, net8.0).
