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

## U1

**Built:** `docs/design/file-attachments-design.md` **Part 1** — the three-tier
contract header (with the **Amends ADR 0025 + ADR 0011** line), `## Context`
(the two existing consumers + the "files cannot be attached anywhere yet"
open question), `## Scope` (In / Out named deferrals), the
`## Invariants (pinned for the ATT lane)` table with **C-ATT·1–C-ATT·10**
(each with a "Pinned where" unit column), the `## FACES (pinned for the ATT
lane)` table with **F1–F9** (each with a "Pinned by" test-name column, copied
from the register), and `## Assumptions` (copied from the register).

**Verified:** no code, no build (doc-only unit — the unit gate is "file
exists with all sections"). Re-read top-to-bottom: **10 invariants** present
exactly (C-ATT·1 through C-ATT·10), **9 FACES** present exactly (F1–F9), the
Amends line names **both** ADR 0025 and ADR 0011, and **no C# code blocks**
have leaked in (only inline-code identifiers like `IMediaStore`,
`AttachmentIds`, which are prose references, not signatures). The §2.* /
exact-C# / serve-5-step / test-list sections are **absent**, as U1's unit plan
directs (they are U2's Part 2).

**Drift:** the register
(`docs/plans-milestones/plan-file-attachments.md`) states the ten invariants
in a **slightly different id-grouping and wording** than U1's unit plan did
(e.g. the register's C-ATT·3 is "no new `AccessAction`" whereas the unit
plan's C-ATT·3 is "`IMediaStore` is unchanged"). I followed the **unit plan's
wording and id** (the unit plan is authoritative for "what to do"), and added
an explicit note under the invariants table in the design doc flagging the
divergence so a later unit doesn't silently reconcile one against the other.
No other drift.

**Next agent (U2) must know:**
- U2 authors **Part 2** into the **same** file
  (`docs/design/file-attachments-design.md`), immediately after Part 1 — the
  exact C# seams (§2.1–2.2), the serve-route 5-step ordering (§2.4), the
  pinned seam-test names (§2.5), the acceptance gate (§2.6), and the
  drift-guard (§2.7). Do **not** rewrite Part 1.
- **F8's "Pinned by" cell is intentionally left as "_(test to be named in
  U2 Part 2)_"** — U2 must either fold F8 into the F3 test
  (`AttachServe_F3_Orphan404`) or name a new one in §2.5 and back-reference
  it. Do not leave it un-named.
- **The register's own invariant wording differs from Part 1's** (see
  Drift). If U2's Part 2 §2.7 drift-guard cites invariant ids, cite the
  **Part 1 ids** (C-ATT·1–10 as written here), not the register's
  re-grouped list — the design doc is the pinned record.
- The register's U8 deliverable says the default attachment allowlist is
  "pinned in the design doc §2.2" — that's U2's job, not done yet.
- The unit plan's "Pinned where" column in the invariants table points at
  U1/U3–U12 — U2's §2.7 drift-guard should re-state which invariants are
  code-enforced (C-ATT·3/4/5/6/7/8) vs. doc-stated (C-ATT·1/2/9/10) so the
  U12 ADR reconciliation knows what to cite where.
