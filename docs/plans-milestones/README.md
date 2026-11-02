---
id: plans-milestones-readme
title: "Lane register + in-progress plans"
tier: living
kind: guide
status: current
audiences: [developer, agent, platform-maintainer]
related:
  - docs/archive/README.md
  - docs/DOCS-CONVENTIONS.md
updated: 2026-11-02
---

> **Abstract:** where the *current* lane's register and in-progress unit plans
> live, and where the *shipped* history went. Read this to know which folder
> holds an in-flight milestone; see
> [the archive](../archive/README.md) for everything that shipped.
> **Who is it for:** anyone working a milestone or lane in this repo.
> **When to read the rest:** before you create or look for a lane's plan files.

# `docs/plans-milestones/` — the current lane register

This folder holds the **in-progress** work only. Shipped-lane history
(the former `done/`) moved to [`docs/archive/lanes/`](../archive/lanes/) by
`D02` of the IMPROVE-DOCS lane — the path map and the conventions that used
to live here are in
[`docs/archive/README.md`](../archive/README.md).

## Layout

- **`improve-docs/`** (and any other in-flight lane) — the lane register
  (`plan-*.md`) + the unit plans for the *current* work. Each lane folder is
  named for the lane (the `m3b` / `group-posts` / `improve-docs` shapes are
  the common layouts — the M-lettered milestones use `mNN/`, the named lanes
  use `<lane-slug>/`).
- An **`in-progress/`** folder (re-created as needed) — unit plans for a
  milestone that is *actively* being built, one folder per in-flight unit,
  empty when the current milestone is between units or just closed.

## The three-tier contract (per lane)

Each lane keeps three tiers, described in the lane's register header:

1. **primary** — the design doc under `docs/design/` (the *current truth*
   for the subsystem, updated in place);
2. **secondary** — the register (`plan-*.md`) in this folder (the
   *what/why/how the lane ships* record);
3. **scratch** — the handoff note (`*-handoff-notes.md`) next to the register
   (the running log, frozen at the lane's close).

## Shipped history

Everything that shipped — every M-milestone and named lane — is under
[`docs/archive/lanes/<lane>/`](../archive/lanes/). The
[redirect map](../archive/REDIRECTS.md) carries the old
`plans-milestones/done/<lane>/…` path → new path for any reference you
encounter in older docs, comments, or bookmarks.
