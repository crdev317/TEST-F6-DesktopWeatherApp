## Parent

#95088

## Type

AFK — autonomously deliverable. (Build-checkable on Windows; XAML/host wiring has no unit tests by design — its runtime behaviour is verified by the separate Tier-3 manual story.)

## What to build

The WPF shell on the .NET generic host. Wire `Microsoft.Extensions.Hosting` with DI + logging in `App.xaml.cs`: two typed HttpClient registrations with distinct base hosts (geocoding vs forecast), the WMO map, the debounce scheduler, the three ViewModels, and the MainWindow. `App.xaml` carries no StartupUri — `OnStartup` resolves the MainWindow, sets its DataContext to a resolved `MainViewModel`, and shows it. Build `MainWindow.xaml`: a persistently-visible search box at top, a candidate panel + search-message area driven by `SearchViewModel`, and a body that switches between the Empty prompt and the Weather view (temperature in degC, Condition, wind in km/h) via `ViewState`. Add the small `IValueConverter`s the bindings need. Keep `MainWindow.xaml.cs` to `InitializeComponent` only — all state lives in the ViewModels.

**Security (added by the security pass):** the two typed HttpClients carry `https://` base addresses, and no code weakens TLS validation (no custom server-certificate callback that accepts any cert). The user's searched location/coordinates travel over this hop — it must stay encrypted and authenticated.

## Acceptance criteria

- [ ] Generic host + DI wired; the MainWindow and `MainViewModel` resolve from the container; no StartupUri.
- [ ] Two typed HttpClients registered with the geocoding vs forecast base hosts.
- [ ] Search box always visible; candidate list + search-message panel bind to `SearchViewModel`; double-click raises `SelectCommand`.
- [ ] Body switches Empty prompt vs Weather view off `ViewState`; weather view shows temperature, Condition, wind, and Loading/Error states.
- [ ] Value converters created and registered; `dotnet build` succeeds on Windows.

### Security acceptance criteria

- [ ] Both Open-Meteo typed HttpClients are registered with `https://` base addresses (asserted against the configured `BaseAddress.Scheme`, or by review of the registration diff).
- [ ] No code disables or weakens TLS certificate validation — no `ServerCertificateCustomValidationCallback` / `DangerousAcceptAnyServerCertificateValidator` that accepts any certificate anywhere in the HttpClient setup. (Verified by the security reviewer reading the diff.)

## Context references

- **Plan**: `docs/superpowers/plans/2026-06-29-feature1-current-weather.md` (Task 9)
- **Spec**: `docs/superpowers/specs/2026-06-29-feature1-current-weather-design.md`
- `business-domain-context.md` (Context.MD), `Technical-Context.MD` (security principles: Open-Meteo is an HTTPS API; don't expose personal location beyond the request)
- ADR: `docs/adr/0001-persist-location-only-never-cache-weather.md`

## Blocked by

- #95149 (MainViewModel — activation handoff).

## Blocks

- &quot;Tier-3 manual verification on Windows&quot;.