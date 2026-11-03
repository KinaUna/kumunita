---
id: docs-readme
title: "Docs map"
tier: living
kind: entry
status: current
audiences: [resident, community-admin, platform-maintainer, developer, agent]
related:
  - docs/DOCS-CONVENTIONS.md
  - docs/archive/lanes/improve-docs/plan-improve-docs.md
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
**`living`** (current truth, updated in place — e.g. [`README.md`](../README.md),
[`AGENTS.md`](../AGENTS.md), `docs/start/*.md`); **`reference`** (stable reference,
correct as of a point in time — e.g. `adr/`, `design/`); **`archive`**
(lane history, frozen at the lane's close — e.g. `archive/lanes/<lane>/…`).

## Target folder shape

The shape the `IMPROVE-DOCS` lane is building toward (see the plan's
"Target shape" section for the full spec):

| Path | What lives there |
|---|---|
| [`start/`](start/) | per-audience entry pages (links + one-line reasons only) |
| [`architecture/`](architecture/identity-access.md) | [`ARCHITECTURE.md`](ARCHITECTURE.md) split by section (the hub stays at [`docs/ARCHITECTURE.md`](ARCHITECTURE.md)) |
| [`security/`](security/why-first.md) | threat model, control map, audit checklists (the concern files, split from the [`SECURITY.md`](SECURITY.md) model hub in `D08`) |
| [`ops/`](ops/) | one runbook per procedure (split from the [`OPS.md`](OPS.md) hub in `D04`) |
| [`deploy/coolify/`](deploy/coolify/00-plan.md) | the Coolify guide, one step per file (split from the [`COOLIFY.md`](COOLIFY.md) hub in `D05`) |
| [`dev/`](dev/README.md) | the working doctrine — running locally, Razor verification, testing + CI, live-server debugging, sandbox quirks (split from [`AGENTS.md`](../AGENTS.md) in `D06`; running-locally in `D07`) |
| [`product/`](product/roadmap.md) | features, roadmap, deferred-by-design (split from [`README.md`](../README.md) in `D07`) |
| [`philosophy/`](philosophy/README.md) | the trust model and the integration argument (unchanged shape) |
| [`guides/`](guides/CONVENTIONS.md) | the in-app guide registry and writing conventions |
| [`adr/`](adr/README.md) | decision records (~250-line cap, index generated from front-matter) |
| [`design/`](design/m3b-moderation.md) | the current truth per subsystem (every doc `tier: reference` / `kind: design` + an Abstract, `D10`); the **six subsystem hubs** are the doors in — [`events`](design/events-subsystem.md), [`rich content`](design/rich-content-subsystem.md), [`notifications`](design/notifications-subsystem.md), [`storage metrics`](design/storage-metrics-subsystem.md), [`analytics`](design/analytics-subsystem.md), [`prod-hardening`](design/prod-hardening.md) |
| [`archive/`](archive/README.md) | lane history (the former `plans-milestones/done/`), moved in `D02` |

Folders still marked "split in `D…`" don't exist yet — the lanes are running in
order; the entry pages link to the *intended* paths so the seams stay green.
(`ops/` landed in `D04`; `deploy/coolify/` in `D05`; `dev/` in `D06`;
`product/` in `D07`; `security/` in `D08`; `architecture/` in `D09`; the
`design/` front-matter + abstracts + the six subsystem hubs in `D10`.)

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

The unit-by-unit work that drove the restructure is the `IMPROVE-DOCS` lane
register, [`archive/lanes/improve-docs/plan-improve-docs.md`](archive/lanes/improve-docs/plan-improve-docs.md)
(shipped 2026-11-02; all 15 units `D00`–`D14` complete). Each unit
`D00`–`D14` was one small, self-documenting commit (or a short
series for the mechanical batches): `D00` (conventions + harness), `D01`
(this map + the entry pages), `D02` (the archive move), `D03` (the archive
tier), `D04` (the ops split), `D05` (the Coolify split), `D06` (slimming
[`AGENTS.md`](../AGENTS.md) into [`dev/`](dev/README.md)), `D07` (slimming [`README.md`](../README.md) into
[`product/`](product/roadmap.md)), `D08` (splitting the security docs into
[`security/`](security/why-first.md) + the audit program + the VDP at the repo
root), `D09` (splitting [`ARCHITECTURE.md`](ARCHITECTURE.md) into
[`architecture/`](architecture/identity-access.md) + the hub), `D10` (landing the
[`design/`](design/m3b-moderation.md) front-matter + abstracts + the six subsystem
hubs), `D11` (the ADR cap + `adr-handoffs.md`), `D12` (the 5 960-link
linkify pass), `D13` (the philosophy fit-check), and `D14` (the close gate
in CI + the cold-read verification).
