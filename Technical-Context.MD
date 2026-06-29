# Technical Context

Engineering-practice contract for code-writing agents in this project. Every implementation must respect what's written here. If a constraint conflicts with the user's request, flag the conflict — don't silently bypass it.

> Versions below are the baseline chosen at project init (2026-06-29). When the first feature lands real manifests (`package.json`, lockfile), reconcile this doc with what's actually installed.

## Overriding Principles
1. **The renderer is untrusted.** `contextIsolation: true`, `nodeIntegration: false`, `sandbox: true`, no remote module — on every `BrowserWindow`. The renderer runs web content; treating it as trusted is the classic Electron RCE path.
2. **All privileged work lives in the main process.** The renderer reaches it only through a typed, allow-listed preload IPC bridge, and every IPC payload is validated before use. No raw Node APIs are exposed to the renderer.
3. **No secrets in the repo or the renderer.** Open-Meteo is keyless, so today there is no API key to protect. If a keyed provider is ever added, the key lives in the main process (OS keychain), never the renderer, never `.env`, never `localStorage`.
4. **Lock down what the app can load.** Strict Content-Security-Policy; the renderer loads only local app content plus the Open-Meteo HTTPS origin; arbitrary navigation and new-window opens are blocked.
5. **SHA-pin every GitHub Action and run with least privilege.** Pin actions by commit SHA (never a floating tag); the workflow `GITHUB_TOKEN` starts at `contents: read` and widens deliberately. Supply-chain baseline from commit one.
6. **No breaking changes without an ADR.** The artefact authority order (ADR > Technical-Context > business-domain-context > PRD > Roadmap > Spec > Plan) is binding; a decision that overrides this doc must be recorded in `docs/adr/`.
7. **TypeScript `strict` is on, and no `any` at the IPC boundary.** Boundaries between main and renderer carry explicit, checked types.

## Frameworks
- **Electron 34** — desktop application shell (Node main process + Chromium renderer), cross-platform (Windows / macOS / Linux).
- **React 19** — renderer UI.
- **TypeScript 5.8** — language for both main and renderer, `strict` mode.
- **Vite 6 (electron-vite)** — dev server and bundler for main, preload, and renderer.
- **electron-builder** — packaging and producing distributable artifacts.

## 3rd-party tech
- **Open-Meteo** — weather data (current conditions + forecast) and geocoding. Keyless HTTPS API; no credential required.

## Packages in use
**HTTP:** native `fetch` (Node 20+ `undici`) — no axios unless a concrete need arises.
**State/UI:** React built-ins (`useState`, context) first; reach for a store library only when shared state genuinely warrants it.
**Logging:** `electron-log`
**Testing:** `vitest`, `@testing-library/react` (unit/component), `playwright` (Electron end-to-end).
**Lint/format:** `eslint`, `prettier`.

> Prefer the packages above before pulling in new dependencies. A new runtime dependency is a decision — justify it, and if it shifts an architectural boundary, record an ADR.

## Instrumentation
Logging via `electron-log`, written to the per-user log file (`userData/logs`) and mirrored to the console in development. Levels: `error` (failures the user is or should be told about), `warn` (degraded but recovered — e.g. a retried request), `info` (lifecycle: window open, fetch start/finish), `debug` (development detail, off in packaged builds). **Always logged:** every outbound Open-Meteo request and its outcome (including failures and timeouts), unhandled errors in both main and renderer, and any IPC payload that fails validation. No metrics or tracing backend at this stage.

## User Feedback Approach
In-app only — there are no OS notifications. The window surfaces state and errors through inline toasts/banners. Tone is terse and plain: no jargon, no stack traces in front of the user. Error copy is actionable and names the recovery, not the internals — e.g. "Couldn't reach the weather service — retrying…" rather than "fetch failed: ETIMEDOUT". Agent-generated user-facing strings default to this voice.

## Branching and Environments
- **Model:** trunk-based on a protected `main`. Direct commits to `main` are blocked; `main` only moves through a pull request that passes the `ci` check (and review, where required).
- **Branches:**
  - HITL work (planning docs, fixes, skill changes) — a branch → pull request → `main`.
  - AFK orchestrator work — one `story/<issue#>-<slug>` branch per Story; the orchestrator owns the branch lifecycle, the agent works only inside it. Squash-merged into `main` on `Approved`. One Story in flight at a time.
- **Environments:** this is a desktop app, not a deployed service. "Dev" is the local `electron-vite` run; releases are packaged distributables produced by `electron-builder`. CI runs on pull requests to and pushes to `main`.
- **Protected:** `main`.

> When the orchestrator's `feature/<id>-<slug>` integration buffer ships, this section and the branch-protection rules must be updated to match (see `CONTRIBUTING.md`). Until then, the two rules above are the whole story.

## Git Guardrails
- Never commit or push directly to `main` (it is protected; work goes through a PR).
- Never force-push to `main` or any shared branch, and never rewrite already-published history.
- Never run `git reset --hard`, `git clean -fd`, or `git branch -D` without explicit confirmation.
- Secrets are caught in CI by the gitleaks scan; do not introduce files that would defeat it (and never commit anything under `.env`, `.orchestrator/`, or build output — all gitignored).
- Hooks: the gitleaks secret scan runs in the `ci` workflow. `setup-pre-commit` and `git-guardrails-claude-code` are available but not yet installed in this repo.

## Testing & the ratchet
> Seeded from the Factory standard — **opt-out, not opt-in**. Trim a line only with a deliberate, recorded decision; don't start from nothing. Full standard: <https://github.com/kitcox-dev/enate-claude-skills/blob/main/docs/testing-and-the-ratchet-standard.md> · the *why*: <https://github.com/kitcox-dev/enate-claude-skills/blob/main/docs/agentic-e2e-testing-principles.md>.

- **Test the contract, not the LLM.** Assert on the deterministic envelope (output shape, state transition, side-effect ordering), never on generated text or chain-of-thought.
- **Every seam gets a real-IO test on at least one side.** Mock-on-both-sides only proves the mocks agree; defects live at integration seams, not inside modules.
- **Three tiers, each catching a class the others can't.** Tier 1 — recorded-replay (real local I/O, fakes at the seams, replayed LLM fixtures; , every commit); Tier 2 — live-LLM, disposable (real subprocess against a per-run throwaway substrate; bounded cost, scheduled); Tier 3 — live (real production substrate and subprocess; continuous).
- **Platform matrix is non-negotiable for OS-touching code.** Filesystem/subprocess code runs Tier 1 + Tier 2 on every OS it supports, not just the dev box's.
- **Plan test coverage at the Feature level, not the Story level.** Each Feature declares at kickoff which tiers it needs, which fixtures/substrates must exist, and a Tier-2 cost ceiling.
- **The ratchet — every Tier-3 defect pins a cheap-tier regression.** A live defect's fix lands with a Tier-1 (or Tier-2) regression that would have caught it, encoding the **class** of defect **bounded by the seam** where it surfaced — not the exact reproducer, and not a project-wide sweep. A Story that surfaced from a live defect carries that ratcheting fixture as an acceptance criterion.
- **Disposable substrate, not shared.** Tier-2 substrate is created per run and torn down; reproducibility comes from a seed (tarball/manifest/script), never persistent state.
- **Assert on observable behaviour, not internal state.** For Tier 2/3 the target is what an operator could verify from external artefacts — tracker state, commit shape, ordered log events, posted comments.
</content>
</invoke>
