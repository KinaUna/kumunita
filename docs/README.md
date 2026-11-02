---
id: docs-readme
title: "Docs map"
tier: living
kind: entry
status: current
audiences: [resident, community-admin, platform-maintainer, developer, agent]
related:
  - docs/DOCS-CONVENTIONS.md
  - docs/plans-milestones/improve-docs/plan-improve-docs.md
updated: 2026-11-02
---

> **Abstract:** the map of the docs layer — the target folder shape, the tier
> model, the per-audience entry pages, and the lane register that drives the
> current restructure. Read this first when you don't know where a topic
> lives; it is the seam the `IMPROVE-DOCS` lane keeps green as docs move.
> **Who is it for:** anyone landing in `docs/`. **When to read the rest:**
> when you need to find the right doc rather than grep.

# Docs map

This repo's docs are *differentiated* on purpose — one fact in one place —
and *linked* deliberately. This file is the map; the per-audience entry
pages under [`start/`](start/) are the doors in.

## Tier model

Three tiers, defined in [`DOCS-CONVENTIONS.md`](DOCS-CONVENTIONS.md):
**`living`** (current truth, updated in place — e.g. `README.md`,
`AGENTS.md`, `docs/start/*.md`); **`reference`** (stable reference,
correct as of a point in time — e.g. `adr/`, `design/`); **`archive`**
(lane history, frozen at the lane's close — e.g. `archive/lanes/<lane>/…`).

## Target folder shape

The shape the `IMPROVE-DOCS` lane is building toward (see the plan's
"Target shape" section for the full spec):

| Path | What lives there |
|---|---|
| [`start/`](start/) | per-audience entry pages (links + one-line reasons only) |
| `architecture/` | `ARCHITECTURE.md` split by section (the hub stays at `docs/ARCHITECTURE.md`) |
| `security/` | threat model, control map, audit checklists (split from `SECURITY.md` in `D08`) |
| `ops/` | one runbook per procedure (split from `OPS.md` in `D04`) |
| `deploy/coolify/` | the Coolify guide, one step per file (split from `COOLIFY.md` in `D05`) |
| `dev/` | running locally, tests, CI cheatsheet, Razor doctrine, terminal/browser quirks |
| `product/` | features, roadmap, deferred-by-design (from `README.md` in `D07`) |
| [`philosophy/`](philosophy/README.md) | the trust model and the integration argument (unchanged shape) |
| [`guides/`](guides/CONVENTIONS.md) | the in-app guide registry and writing conventions |
| [`adr/`](adr/README.md) | short decision records (~80-line cap, index generated from front-matter) |
| [`design/`](design/m3b-moderation.md) | living design references (the current truth per subsystem) |
| `archive/` | lane history (the former `plans-milestones/done/`), moved in `D02` |

Folders marked "split in `D…`" don't exist yet — the lanes are running in
order; the entry pages link to the *intended* paths so the seams stay green.

## Per-audience entry pages

The seam this lane keeps green. Each page is links and one-line reasons only —
no facts — so it cannot drift. As docs move in later units, the entry pages
are updated in the same commit.

| Audience | Entry page |
|---|---|
| Resident (a person in the neighborhood) | [`start/resident.md`](start/resident.md) |
| Community admin (a moderator / global admin) | [`start/community-admin.md`](start/community-admin.md) |
| Platform maintainer (operates the deployment) | [`start/platform-maintainer.md`](start/platform-maintainer.md) |
| Developer (writes code in this repo) | [`start/developer.md`](start/developer.md) |
| Agent (an AI assistant working in this repo) | [`start/agent.md`](start/agent.md) |

## Lane register

The unit-by-unit work driving the restructure is tracked in the lane
register, [`plans-milestones/improve-docs/plan-improve-docs.md`](plans-milestones/improve-docs/plan-improve-docs.md).
Each unit `D00`–`D14` is one small, self-documenting commit (or a short
series for the mechanical batches). `D00` is done; `D01` (this file + the
entry pages) is this commit.
