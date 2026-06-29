# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

**TEST-F6-DesktopWeatherApp** is a product built with the **Enate SDLC Factory**. The application
code has not landed yet — the repo currently holds only this orientation and its README. The
Factory conventions below apply from commit one and govern how every change is planned, branched,
and merged once code arrives.

This file is the *agent* front door (auto-loaded every session); `README.md` is the human one.

## Read this first — follow the Factory flow

The Factory is walked as a **HITL → AFK** flow, and the source of truth for *which skill to fire
when* lives in the sibling skills repo:

➡️ **[Using the Enate SDLC Factory](https://github.com/kitcox-dev/enate-claude-skills/blob/main/docs/using-the-sdlc-factory.md)**

The skills themselves (`/brainstorming`, `/writing-plans`, `/tdd`, `/triage`, the review gauntlets,
etc.) are defined in **`kitcox-dev/enate-claude-skills`** and surface as slash commands.

The one rule that must never break: **only a human moves a Story to `Agent Ready`** — that is the
HITL→AFK handoff. The orchestrator owns every other state transition.

## The documentation fabric (load before you plan or build)

When these documents exist, load them before planning or implementing. Authority order, lower wins:

> **ADR > Technical-Context > business-domain-context > PRD > Roadmap > Spec > Plan**

- **`Technical-Context.MD`** — the engineering contract every code-writing agent must respect
  (principles, secure-coding baseline, branching, and the **Testing & the ratchet** standard).
  Created by `/init-tech-context`.
- **`business-domain-context.md`** — the domain glossary (the project's language).
- **`PRD.md`** · **`Roadmap.md`** — product requirements and the ordered Feature list.
- **`docs/adr/`** — architectural decisions (highest authority).
- **`docs/superpowers/specs/`** · **`docs/superpowers/plans/`** — per-Feature Spec and Plan. The
  Plan carries the **Context references** an agent loads before building.

## Branching & merge rules

Direct commits to `main` are blocked. `main` only moves through a pull request that passes CI
(and review, where required).

- **HITL (human) work** — planning docs, fixes, skill changes, anything — happens on a branch,
  opened as a PR, and merged into `main`.
- **AFK (orchestrator) work** — one `story/<issue#>-<slug>` branch per Story. The agent works only
  inside that branch and performs no branch operations; the orchestrator owns the branch lifecycle.
  On `Approved`, the Story branch is **squash-merged into `main`**. One Story is in flight at a time.

> The feature-branch integration buffer (`feature/<id>-<slug>`) is **not** in use yet. Do not
> pre-adopt it; when it ships, this section and the branch-protection rules must be updated together
> (see `enate-sdlc-orchestrator` Roadmap Feature 12 / ADR-0010).

## CI

A single required check named **`ci`** gates `main` (defined in `.github/workflows/ci.yml` once
restored). It runs least-privilege (`contents: read`) and currently does secret scanning
(gitleaks) only. Pin GitHub Actions by **commit SHA**, not floating tags. When build / lint / test
land, add them as steps *behind the same `ci` job name* so the branch-protection rule never needs
revisiting.

## Dev commands

_Not set yet — the stack has not been chosen. These are filled by `/init-tech-context` or the first
feature build (install / test tiers / run / lint)._
