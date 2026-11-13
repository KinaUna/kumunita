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
  - docs/archive/lanes/improve-sec/plan-improve-sec.md
  - docs/archive/lanes/improve-sec/improve-sec-handoff-notes.md
updated: 2026-11-09
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

## Current in-progress lane

**IMPROVE-UX** — closed on 2026-11-13 — shipped and closed; the register, the `x`/`r`/`a`/`o`-prompt unit prompts, and the handoff notes moved to the archive at [`archive/lanes/improve-ux/`](../archive/lanes/improve-ux/) (register [plan-improve-ux](../archive/lanes/improve-ux/plan-improve-ux.md), closing log [improve-ux-handoff-notes](../archive/lanes/improve-ux/improve-ux-handoff-notes.md)).

**Latest lane: `CODE-HYGIENE`** — the code-hygiene lane (units `U00`–`U14`,
proposed 2026-11-06) **shipped and closed on 2026-11-09** — the close unit
`CH14` verified the six close criteria green and moved the register, the
`u*-prompt.md` unit prompts, and the handoff notes to the archive at
[`archive/lanes/code-hygiene/`](../archive/lanes/code-hygiene/) (register
[plan-code-hygiene](../archive/lanes/code-hygiene/plan-code-hygiene.md), closing
log
[code-hygiene-handoff-notes.md](../archive/lanes/code-hygiene/code-hygiene-handoff-notes.md)).
Its design doc stays a living
[`docs/design/code-hygiene-design.md`](../design/code-hygiene-design.md)
(per D10 — design docs stay living through lane close); the two M2 successor-scope
reds remain carried by `improve-sec` (IS21–IS23 / IS31–IS32).
When a new lane starts, its register + unit prompts land here first and move to
that archive folder at its close.

The lane before it, `IMPROVE-SEC` — the security & privacy integration lane
(units `IS00`…`IS45`, proposed 2026-11-06) — **shipped on 2026-11-09**; the
close unit `IS45` landed the six-member "shipped" flip and moved its register,
the `is*-prompt.md` unit prompts, and the handoff notes to the archive at
[`archive/lanes/improve-sec/`](../archive/lanes/improve-sec/) (register
[plan-improve-sec](../archive/lanes/improve-sec/plan-improve-sec.md), closing
log
[improve-sec-handoff-notes.md](../archive/lanes/improve-sec/improve-sec-handoff-notes.md)).

The lane before it, `IMPROVE-CODE-3`
(`C3-00`–`C3-08`), **shipped on 2026-11-06** — it finished the two lines its
predecessor held and landed the six-member "shipped" flip — and its register,
the `C2-00`–`C3-08` unit prompts, and the two handoff notes
(`improve-code-2-handoff-notes.md`, `improve-code-3-handoff-notes.md`) all now
live in the archive at
[`archive/lanes/improve-code/`](../archive/lanes/improve-code/) (register
[plan-improve-code-3](../archive/lanes/improve-code/plan-improve-code-3.md),
closing log
[improve-code-3-handoff-notes.md](../archive/lanes/improve-code/improve-code-3-handoff-notes.md)).

The full code-quality history — `IMPROVE-CODE` (round 1), `IMPROVE-CODE-2`
(round 2), and `IMPROVE-CODE-3` (round 3) — is archived together under
[`archive/lanes/improve-code/`](../archive/lanes/improve-code/).

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
