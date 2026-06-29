# Technical Context

Engineering-practice contract for code-writing agents in this project. Every implementation must respect what's written here. If a constraint conflicts with the user's request, flag the conflict — don't silently bypass it.

> Stack chosen at init (2026-06-29): **WPF on .NET 8 (LTS), C#**, consuming **Open-Meteo**. Reconcile this doc with reality whenever the stack moves.

## Overriding Principles
1. **No secrets in source.** API keys, credentials, and tokens are never committed — enforced by the gitleaks secret-scan in the `ci` gate. (Open-Meteo needs no key today; the rule stands for anything added later. Secrets belong in user/OS config or a secret store, never in the repo or `.env` committed to git.)

## Frameworks
- **WPF on .NET 8 (LTS), C#** — the Windows desktop application: XAML views with the MVVM pattern. Built and tested with the `dotnet` CLI.

## 3rd-party tech
- **Open-Meteo** — weather + forecast data provider. Free HTTPS JSON API, **no API key required**.
- **Azure DevOps** (org `Enate`, project `4158c5e2-092d-482d-a445-7e910ffbe775`) — where this repo's work items (Stories/Features) live. Declared in `.factory.yml`.
- **GitHub** — repository host and CI (GitHub Actions, the `ci` workflow).

## Packages in use
The default set a code-writing agent reaches for first — prefer these before pulling in a new dependency.

**HTTP:** `System.Net.Http.HttpClient` (via `IHttpClientFactory`) — the Open-Meteo client
**JSON:** `System.Text.Json` — request/response (de)serialisation; no Newtonsoft unless a concrete gap forces it
**MVVM:** `CommunityToolkit.Mvvm` — `ObservableObject`, `RelayCommand`, source-generated bindable properties
**DI / config / logging:** `Microsoft.Extensions.DependencyInjection`, `.Configuration`, `.Logging`, `.Hosting` (generic host)
**Testing:** `xUnit` (test runner), `FluentAssertions` (assertions), `Moq` (unit-level fakes at seams)

## Instrumentation
Logging is via `Microsoft.Extensions.Logging` to the **console / stdout (debug output)** — no log file, no cloud telemetry at this stage. ERROR level is always logged. Every outbound Open-Meteo call is logged with its outcome (request target + success/failure + status). Never log secrets (none today) or full personal location data beyond what the request needs.

> _Inferred default from the "console/stdout only" choice — correct if you want file rotation or cloud telemetry (e.g. Application Insights) later._

## User Feedback Approach
The **in-app WPF UI is the only feedback channel** — there is no email/Slack/notification surface. Copy is **concise and neutral**: state the condition (current weather, forecast) plainly. Errors are surfaced **in-app** (a status line / inline message), never as an unhandled-exception dialog or a raw stack trace, and never silently swallowed. When generating user-facing copy, default to short, plain sentences over decorative phrasing.

> _Inferred default for a desktop weather app — correct if the intended tone or channels differ._

## Branching and Environments
- **Model:** trunk-based — `main` is the single integration branch; all work lands via PR.
- **Branches:** HITL work on a feature branch → PR → `main`; AFK work on one `story/<issue#>-<slug>` branch per Story (the orchestrator owns the branch lifecycle; the agent performs no branch operations), squash-merged into `main` on `Approved`.
- **Environments:** local developer machine only — this is a desktop app with no server tiers (no dev/staging/prod deployment). Releases are distributed as built artifacts when that need arrives.
- **Protected:** `main` (no direct pushes; PR + passing `ci` required; linear history; conversation resolution required; no force-push; no deletion).

See `CONTRIBUTING.md` for the full branching rules.

## Git Guardrails
- **Never push directly to `main`** — it is protected; all changes go through a PR that passes `ci`.
- **Never force-push** a shared branch or rewrite published history (`git push --force`, `git rebase` of pushed commits, `git reset --hard` onto a shared ref) without explicit confirmation.
- **Never delete branches** you don't own; in AFK runs the agent performs **no** branch operations — the orchestrator owns the branch lifecycle.
- **Secret-scan gate:** the `ci` workflow runs gitleaks over full history on every PR/push to `main`; a leaked secret fails the build. CI also runs least-privilege (`GITHUB_TOKEN` starts read-only) with all actions SHA-pinned.
- **No pre-commit hooks installed yet** — run `/setup-pre-commit` or `/git-guardrails-claude-code` if you want commit-time enforcement locally.

## Testing & the ratchet
> Seeded from the Factory standard — **opt-out, not opt-in**. Trim a line only with a deliberate, recorded decision; don't start from nothing. Full standard: <https://github.com/kitcox-dev/enate-claude-skills/blob/main/docs/testing-and-the-ratchet-standard.md> · the *why*: <https://github.com/kitcox-dev/enate-claude-skills/blob/main/docs/agentic-e2e-testing-principles.md>.
>
> **This product has no LLM** — so "test the contract, not the LLM" reads as **test the deterministic envelope, not the live API payload**, and the replayable seam is the **Open-Meteo HTTP API** (record real responses as fixtures rather than asserting on live data). Everything else applies unchanged.

- **Test the contract, not the live service.** Assert on the deterministic envelope (parsed/transformed output shape, state transition, side-effect ordering), never on volatile live weather values.
- **Every seam gets a real-IO test on at least one side.** The Open-Meteo HTTP boundary is the primary seam; mock-on-both-sides only proves the mocks agree.
- **Three tiers, each catching a class the others can't.** Tier 1 — recorded-replay (real local I/O, fakes at the seams, replayed HTTP fixtures; every commit); Tier 2 — live API, disposable (real call against the real Open-Meteo endpoint; bounded cost, scheduled); Tier 3 — live (the shipped app against the real service; continuous/manual).
- **Platform matrix is non-negotiable for OS-touching code.** Filesystem/process code runs Tier 1 + Tier 2 on every OS it supports — here, Windows is the target, so the matrix is Windows.
- **Plan test coverage at the Feature level, not the Story level.** Each Feature declares at kickoff which tiers it needs, which fixtures/substrates must exist, and a Tier-2 cost ceiling.
- **The ratchet — every Tier-3 defect pins a cheap-tier regression.** A live defect's fix lands with a Tier-1 (or Tier-2) regression that would have caught it, encoding the **class** of defect **bounded by the seam** where it surfaced — not the exact reproducer, and not a project-wide sweep. A Story that surfaced from a live defect carries that ratcheting fixture as an acceptance criterion.
- **Disposable substrate, not shared.** Tier-2 runs hit a per-run throwaway context; reproducibility comes from a seed (recorded fixtures/manifest), never persistent state.
- **Assert on observable behaviour, not internal state.** For Tier 2/3 the target is what a user/operator could verify from external artefacts — rendered UI state, logged request outcomes, output shape.
