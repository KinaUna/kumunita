# File-attachments lane — handoff notes (scratch tier)

> **Three-tier contract.** This lane has three written surfaces, in order of
> authority:
>
> 1. **Primary — the design doc** `docs/design/file-attachments-design.md`
>    (authored in U1 Part 1 + U2 Part 2). When it exists, it is the source of
>    truth for *what* to build and *why* — invariants, FACES, the exact C#
>    seams.
> 2. **Secondary — the register** `docs/plans-milestones/plan-file-attachments.md`.
>    Source of truth for *which* unit, in *which* order, touches *which* files,
>    and the handoff protocol.
> 3. **Scratch — this file.** A running log. Each unit **appends** a `## U#`
>    section to the end. It is never rewritten or reordered — later agents
>    read it top-to-bottom to see what the earlier agents actually did,
>    including anything that drifted from the plan.
>
> **Do not edit an earlier `## U#` section.** If you find a mistake, add a new
> section and note it. Append, don't amend.

## Protocol (every agent, every unit)

- This file + your **unit plan** (`in-progress/file-attachments-uNN-plan.md`)
  is the whole context you need. **Do not** scan the whole repo.
- Read the unit plan's "entry reads" (a small fixed set), do the work, hit the
  unit's build gate, **then** append a `## U#` section here *before* doing
  anything else. The section must record:
  - **what you built** (files + the one-line purpose of each),
  - **what you verified** (the build command + its green result),
  - **any drift from the plan** (a deviation, an extra file, a skipped step) —
    say so explicitly; silence means "no drift,"
  - **what the next agent must know** that isn't already in the plan (a seam
    that turned out different, a test that needed a tweak, a follow-on to
    track).
- If you hit a real blocker (a failing build you can't resolve, a missing
  file, an ambiguous requirement), **stop and say so** in a `## U# — BLOCKED`
  section instead of guessing.

## Log

_(The first `## U1` section appears when the U1 agent ships the design doc
Part 1. There is no `## U0` for this lane — the plan author's notes, if any,
live in the master plan's assumptions, not here.)_
