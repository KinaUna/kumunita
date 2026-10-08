# `docs/plans-milestones/` — the lane register + handoff notes

This folder holds the **three-tier contract** (primary: the design doc,
secondary: the register, scratch: the handoff notes) for every shipped
milestone (`M1`–`M28`) and named lane (`PG`, `GP`, `ML`, `LS`, `SP`, `TZ`,
`DF`, `TR`, `RC`, `GU`, `GA`, `RE`, `TG`, `ATT`, …). The three tiers are
described in each lane's register header; the conventions below apply
across all of them.

- **`in-progress/`** — unit plans for the *current* in-progress milestone
  (the single `StatusNext` row in `src/Kumunita.Web/Milestones.cs`).
  Empty when the current milestone is between units or just closed.
- **`done/`** — the register + handoff notes + unit plans for every
  shipped milestone / named lane, one folder per lane (the `M27` / `M28`
  / `site` / `pl` / `improve` shapes are the common layouts —
  the M-lettered milestones use `done/mNN/`, the named lanes use
  `done/<lane-slug>/`).
- **`done/improve/`** — the **`IMPROVE`** lane's register
  (`plan-improve.md`) + handoff notes + the audit harness
  (`improve-check.ps1`, `improve-report.ps1`) + the audit ledger
  (`improve-audit.md`). See `done/improve/plan-improve.md` for the lane's
  Definition of Done (the inverse of a capability lane: the codebase is
  measurably *smaller* or *more integrated*, not *bigger*).

## TL;DR convention

**Every `*-handoff-notes.md` under `done/` over 600 lines must carry a
`> **TL;DR:**` blockquote in its first 20 lines.** The block is 3–7
lines of blockquote and answers three questions in the note's own
vocabulary:

1. **What did this lane ship, in one sentence?** (the capability, the
   ADR number, and the green-gate count the lane's close recorded).
2. **What is the one seam it created or changed?** (the bounded-context
   / service seam / doc-type surface / test-pinned contract — the named
   thing a future lane will build on or touch).
3. **What is the one thing a future reader must not re-litigate?**
   (the locked decision / the drift note that was reconciled / the
   deferral that was named — usually the note's `## Summary` or `##
   Close` section's highest-load-bearing line).

The rest of the file is *untouched* — the TL;DR is a **pure addition**
at the top, not a rewrite. The `improve-check.ps1` gate (d)
enforces this: any `*-handoff-notes.md` under `done/` over 600 lines
without a `TL;DR` in its first 20 lines *fails the close* (the 23-note
U00-time baseline is grandfathered by name; the gate fails on any
*new* file crossing 600 without a TL;DR, or any *baseline* file that
grows). The U02 unit of the IMPROVE lane added TL;DRs to the 8 largest
of the 23 baseline notes; the remaining 15 are U02's "record and
accept" backlog (see `improve-audit.md`).

**Why this matters:** a new agent's first read of a 1 800-line handoff
note used to be *the entire file*; the TL;DR is the "read the abstract,
decide if this is the right lane, then read the rest" path (the
"public surface is permanent integration cost" principle from
`docs/philosophy/in-code.md`, applied to the doc layer). The same
convention applies to the **design-doc** tier too, closed by U07 of the
IMPROVE lane: any `docs/design/*.md` over **400 lines** carries a
`> **Abstract:**` blockquote in its first 15 lines answering three
questions in the doc's own vocabulary — *what question does this design
settle?* · *what is the one contract it creates (the seam name)?* ·
*what is explicitly out of scope?* The `improve-check.ps1` **gate (g)**
enforces this: any `docs/design/*.md` over 400 lines without an Abstract
in its first 15 lines *fails the close* (the 38-doc U00-time baseline is
grandfathered by name; the gate fails on any *new* design doc over 400
without an Abstract). U07 added Abstracts to the 5 largest of that
baseline (`m13` · `m3b` · `m18` · `m20` · `m5`); the remaining 38 are the
"record and accept" backlog (see `improve-audit.md`). **TL;DR for
handoff notes, Abstract for design docs — both are the 10-second "is this
the right read" gate**, applied at their tier.
