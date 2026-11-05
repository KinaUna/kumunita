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
  - docs/plans-milestones/plan-improve-code-3.md
  - docs/plans-milestones/plan-improve-code-2.md
updated: 2026-11-05
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

- **A lane folder per in-flight lane** — the lane register
  (`plan-*.md`) + the unit plans for the *current* work. Each lane folder is
  named for the lane (the `m3b` / `group-posts` shapes are the common
  layouts — the M-lettered milestones use `mNN/`, the named lanes
  use `<lane-slug>/`).
- An **`in-progress/`** folder (re-created as needed) — unit plans for a
  milestone that is *actively* being built, one folder per in-flight unit,
  empty when the current milestone is between units or just closed.

The in-flight lane is **`IMPROVE-CODE-3`** — the successor to
`IMPROVE-CODE-2`, which closed on 2026-11-05 **held** (DoD #3/#4 files `23 > 8`
and DoD #6 Marten-allowlist `20 > 5` fail; #1, #5, #6-concrete-edges, #7, #8
hold) and **deliberately did not land the six-member "shipped" flip** — its
register is
[plan-improve-code-3](plan-improve-code-3.md) and the running log (the
scratch tier) is
[improve-code-2-handoff-notes.md](improve-code-2-handoff-notes.md). Its whole
reason to exist is to finish the two held lines — the module surface (the
Marten allowlist `20 → ≤ 5`, unblocked by the god-service shrinks) and the
still-large files (`23 → ≤ 8`, including the `SampleDataSeeder` decomposition
and the 7 god-service splits) — and then land the six-member flip the two
predecessor lanes withheld.

The two predecessor lanes are both archived/closed with the gaps intact:
`IMPROVE-CODE-2` (register
[plan-improve-code-2](plan-improve-code-2.md), the first unit plan
[c2-00-prompt](c2-00-prompt.md), the running log
[improve-code-2-handoff-notes.md](improve-code-2-handoff-notes.md)) and
`IMPROVE-CODE`, which closed on 2026-11-05 **with unmet DoD** (DoD #1/#3/#4/#5/#6/#8
fail, #7 partial) and moved to
[`archive/lanes/improve-code/`](../archive/lanes/improve-code/) at that close.

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
