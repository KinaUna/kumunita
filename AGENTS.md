---
id: agents
title: "AGENTS.md"
tier: living
kind: entry
status: current
audiences: [agent, developer]
related: [README.md, docs/DOCS-CONVENTIONS.md]
updated: 2026-11-15
---

> **Abstract:** the agent entry — the must-knows (up-front reads, shape of the code, the one thing to internalize, conventions, don't-pause, doc-sync), each a short paragraph + link out.
> **Who is it for:** an AI agent or developer in this repo.
> **When to read the rest:** at session start, before the first code or doc change.

# AGENTS.md — Kumunita

Kumunita is a **self-hosted community platform for one neighborhood** — one community per deployment (its own container + Postgres); no multi-tenant data model.

> **This file is loaded into every agent session, so its length is a per-session cost.** It carries only the must-knows; the working doctrine lives in [`docs/dev/`](docs/dev/README.md) and the shared doc rules in [`docs/DOCS-CONVENTIONS.md`](docs/DOCS-CONVENTIONS.md).

## Commands

- `pwsh tools/check.ps1` — the full loop (build → tsc → web → core → docs → code → devx).
- `pwsh tools/check.ps1 -Fast` — the loop minus the slow core step.
- `pwsh tools/check.ps1 -Doctor` — the environment self-check (toolchain, Docker, base ref).

## Start here

The few every change needs — read up front:

- [`README.md`](README.md) — what the platform is, current status, principles, tech stack, running.
- [`docs/dev/branch-flow.md`](docs/dev/branch-flow.md) — the branch flow, which branch CI watches, owner-ops ledger (ADR 0307 D1).
- [`docs/DOCS-CONVENTIONS.md`](docs/DOCS-CONVENTIONS.md) — the shared doc rules (front-matter, abstract, ADR cap, linking); read before your first doc change.

## Read when

Read when the trigger fires (each a full doc, still linked):

- **Where a concern lives:** [`docs/dev/code-map.md`](docs/dev/code-map.md) — context → folder, DocTypes, service, controllers, views, tests, ADRs.
- **Stack / data model / CQRS-lite & Wolverine:** [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).
- **Persistence / schema:** **ADR [0004](docs/adr/0004-data-persistence-and-schema-evolution.md)** — before persistence or schema.
- **Cross-context calls:** **ADR [0006](docs/adr/0006-module-boundary-contracts.md)** — before cross-context calls.
- **Privacy / access control:** [`docs/SECURITY.md`](docs/SECURITY.md) — privacy-first.
- **Razor / views:** [`docs/dev/razor-verification.md`](docs/dev/razor-verification.md). **Tests / CI:** [`docs/dev/testing-and-ci.md`](docs/dev/testing-and-ci.md).
- **Live-server debugging:** [`docs/dev/live-server-debugging.md`](docs/dev/live-server-debugging.md). **Sandbox / terminal / browser quirks:** [`docs/dev/agent-environment-quirks.md`](docs/dev/agent-environment-quirks.md).

## The shape of the code

- **`Kumunita.Core`** — domain + services, one bounded context per folder (each self-registering in its own `DependencyInjection.cs`); the full context list is [`docs/dev/code-map.md`](docs/dev/code-map.md). The `Announcements` module (`PostDocTypes`) is a feature module, not a top-level context.
- **`Kumunita.Web`** — Razor Pages / MVC (server-rendered), `Program.cs` bootstrap, `Milestones.cs` (home-page roadmap — the canonical list), `Models/` + `Security/` (view-models + claim shaping).
- **Persistence** (ADR [0004](docs/adr/0004-data-persistence-and-schema-evolution.md) §B): **Marten** owns the domain documents + versioned schema (`CoreDocTypes` / `PostDocTypes` surfaces). **EF Core is only for ASP.NET Identity tables** — no domain data in EF.

## The one thing to internalize before writing code here

This repo uses **Marten and Wolverine** on **.NET 10**; the idiomatic patterns in training data are often **older major versions** of both. Before writing Marten schema/migration or Wolverine handler code, check the actual APIs in the current `.csproj` — do not copy a "common" pattern from memory. The ADRs encode this project's *specific* choices (CQRS-lite, documents + projections, no event sourcing, a single durable email handler rather than a saga) — follow them.

## Conventions to preserve

- Server-rendered Razor Pages / MVC over Blazor or SPA patterns.
- Audit-by-default: access to audience-restricted content is always logged.
- Thin token, fat authorization: "may this actor see that resource?" is resolved by the `Authorization` service per request, not in an identity claim.
- Keep `Milestones.cs` and the roadmap docs together — `Milestones.cs` is the **canonical list** (it renders the home page); [`docs/product/roadmap.md`](docs/product/roadmap.md) + [`docs/STATUS.md`](docs/STATUS.md) point at it, not restate it.
- The code-shape rules — the **ratchet** (baseline only goes down; raising needs an `adr:` reason), the **part-vs-whole test**, and the **comment rule** (what/when/throws + ADR number) — live in [`docs/philosophy/in-code.md`](docs/philosophy/in-code.md#the-ratchet-the-split-the-comment), enforced by [`tools/code/code-check.ps1`](tools/code/code-check.ps1).
- Security/privacy (IMPROVE-SEC, [ADR 0298](docs/adr/0298-data-classes-in-code.md)): a new registered doc type carries `[DataClass]`; a new read of an audience-bearing type is decided in-method or marked `// SEC-READ:`; log templates never carry a PII placeholder; views never carry an inline `on*` handler or `<script>` block. Enforced by a `SecGate` test in [`tests/Kumunita.Web.Tests/SecGate/`](tests/Kumunita.Web.Tests/SecGate/).

## Don't pause mid-task to check in

Partway through a multi-step task, if a tool batch returns with no error, keep going — don't stop to announce the next step and wait for a "continue." Stop only on a real blocker (an error, a missing file, an ambiguous requirement, or something destructive) — say what happened and what you need. **A failed tool call is a recoverable step, not a stopping point**: read the error, adjust, continue; if the *same* command failed twice, change approach.

**The flip side — long-running things in flight.** When a known-long test suite, build, or `docker compose up` is running in the background, say so *proactively* in the same turn — name the duration (per-step budget in `check.ps1` output) and that you'll report on completion.

## Keeping the docs in sync with the code

This repo is deliberate about doc ↔ code parity. Keep these pairs together when you change behavior:

- The **roadmap** and `src/Kumunita.Web/Milestones.cs` — `Milestones.cs` is the **source of truth** (the home page renders it; `MilestonesTests.cs` pins order + shipped statuses); [`docs/product/roadmap.md`](docs/product/roadmap.md) + [`docs/STATUS.md`](docs/STATUS.md) point at it, not restate it.
- **`src/Kumunita.Web/WhatsNew.cs`** (the "What's new" registry, ADR [0110](docs/adr/0110-platform-versioning-and-whats-new.md), pinned by `WhatsNewTests.cs`) — the platform's version history. When a milestone **or a named lane ships**, add its version (newest-first; or extend the newest row). "Shipped" = the close unit ran, not "in progress."
- A **new capability out of M-letter order** gets a *named lane* with a short ID — **not** a renumber. Precedent: media (ADR [0011](docs/adr/0011-media-and-file-storage.md)), group posts (`GP`, ADR [0013](docs/adr/0013-group-posts-membership-lane.md)), multilingual (`ML`, ADR [0005](docs/adr/0005-multilingual-support.md)).
- The **bounded-context / persistence layout** in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) + ADR [0004](docs/adr/0004-data-persistence-and-schema-evolution.md)/[0006](docs/adr/0006-module-boundary-contracts.md) — new contexts / doc-type surfaces / `DependencyInjection.cs` changes reflect in the relevant ADR, not the design doc.
- A **new capability that settles a design question** gets an ADR under `docs/adr/` (numbered after the current highest), not a prose note in the README.
- Lane history lives in **`docs/archive/lanes/<lane>/`**. Doc rules: [`docs/DOCS-CONVENTIONS.md`](docs/DOCS-CONVENTIONS.md); the working doctrine lives in [`docs/dev/`](docs/dev/README.md) — link, don't restate.
