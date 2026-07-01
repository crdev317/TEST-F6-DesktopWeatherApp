# TEST-F6-DesktopWeatherApp

A Windows desktop weather app: search for a place by name, pick it, and see its current
conditions plus a 7-day daily forecast. The app remembers the last place you chose and
re-fetches it fresh on the next launch. Weather data comes from [Open-Meteo](https://open-meteo.com)
(keyless, free). See `PRD.md` for the product spec and `Technical-Context.MD` for the
engineering contract.

## Stack

WPF on .NET 8 (C#), MVVM via `CommunityToolkit.Mvvm`, `IHttpClientFactory` for HTTP,
`System.Text.Json` for serialisation, and `Microsoft.Extensions.*` for DI/config/logging/hosting.

## Solution layout

- `src/WeatherApp.Core/` — class library (net8.0): domain, geocoder, weather-provider client, stores.
- `src/WeatherApp/` — WPF shell (net8.0-windows): XAML views and the generic host.
- `tests/WeatherApp.Tests/` — xUnit test project (net8.0); `Fixtures/**/*.json` copy to output for Tier-1 recorded-replay tests.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) (LTS)
- Windows (the WPF shell targets `net8.0-windows`)

## Build, test, run

```sh
dotnet restore WeatherApp.sln
dotnet build WeatherApp.sln
dotnet test WeatherApp.sln --filter Tier!=Live   # every-commit Tier-1 (recorded-replay)
dotnet run --project src/WeatherApp/WeatherApp.csproj
```

Tests come in two tiers. **Tier-1** (recorded-replay, every commit) runs fully offline against
recorded fixtures. **Tier-2** (`dotnet test WeatherApp.sln --filter Tier=Live`) makes real,
disposable calls to the live Open-Meteo endpoints to confirm the recorded fixtures still match the
real contract — run on a schedule, not on every commit. Plain `dotnet test WeatherApp.sln` runs both.

Restore uses the repo-local `nuget.config` (pins nuget.org) so it is reproducible in CI.
