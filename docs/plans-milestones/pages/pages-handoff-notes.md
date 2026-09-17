# Pages lane (`PG`) — handoff notes (scratch tier)

> **Three-tier contract.** This lane has three written surfaces, in order of
> authority:
>
> 1. **Primary — the design doc** `docs/design/pages-design.md`. The source
>    of truth for *what* to build and *why* — the `Page` / `PageTranslation`
>    field sets, the hierarchy + audience + translation + mount-point
>    decisions, the standing matrix, the absorb-migration ordering. Every
>    decision marked **[DECIDED — ADR 0039]** is locked.
> 2. **Secondary — the register** `docs/plans-milestones/pages/plan-pages.md`.
>    Source of truth for *which* unit, in *which* order, touches *which*
>    files, the exit criteria, and the drift-pause policy.
> 3. **Scratch — this file.** A running log. Each unit **appends** a `## U#`
>    section to the end. It is never rewritten or reordered — later agents
>    read it top-to-bottom to see what the earlier agents actually did,
>    including anything that drifted from the plan.
>
> **Do not edit an earlier `## U#` section.** If you find a mistake, add a new
> section and note it. Append, don't amend.

## Protocol (every agent, every unit)

- This file + your **unit plan** (`docs/plans-milestones/pages/pages-uNN-plan.md`)
  is the whole context you need. **Do not** scan the whole repo.
- Read the unit plan's "entry reads" (a small fixed set), do the work, hit the
  unit's build gate, **then** append a `## U#` section here *before* doing
  anything else. The section must record:
  - **what you built** (files + the one-line purpose of each),
  - **what you verified** (the build command + its green result — remember:
    tests run via `dotnet exec` of the DLL, **not** `dotnet test`),
  - **any drift from the plan** (a deviation, an extra file, a skipped step) —
    say so explicitly; silence means "no drift,"
  - **what the next agent must know** that isn't already in the plan (a seam
    that turned out different, a test that needed a tweak, a follow-on to
    track).
- If you hit a real blocker (a failing build you can't resolve, a missing
  file, an ambiguous requirement, a standing-matrix cell the `PageService`
  can't express without a new ADR), **stop and say so** in a
  `## U# — BLOCKED` section instead of guessing.
- **Sequencing invariant (drift-pause (e)):** U07 (the destructive
  `LocalizedPage` retirement) runs **only after** U01–U06 are green. A fresh
  agent picking up this lane starts at **U01** (U00 is already done by lock).

## Lane open

- **Lane:** `PG` (Pages) — a named lane (the `ML`/`GP`/`RC`/`RE` convention),
  **not** a roadmap renumber. M4/M5/M6 stay Events / Projects / Portability.
- **ADR:** **0039** (`docs/adr/0039-pages-hierarchy-audience-translations.md`),
  **Accepted** 2026-09-17. Adds the `Page` + `PageTranslation` docs, a new
  `Kumunita.Core.Pages` context, the `PageToAuditableResource` adapter, the
  `MountPoint` string, and the absorb/retire of `LocalizedPage`. Amends
  0005/0018/0022/0026/0027/0037; additive on 0001-B/0006/0036/0025/0034.
- **Locked decisions** (the 4 the user signed off, all now **[DECIDED]** in the
  design doc):
  1. **Absorb** the existing static-page lane (`LocalizedPage`) into one tree —
     not a parallel lane. `/about`/`/terms`/`/help` become ordinary pages.
  2. **Pages default public** (`Audience = null`) — the one place pages differ
     from posts (posts default community-visible, ADR 0036).
  3. **`MountPoint`** = one nullable string column on `Page` + one resolver
     (`GetByMountPointAsync`) for UI slots (`footer/community`, `help/account`).
  4. **Delete = soft-delete** (`IsDeleted` flag + `CanSeeAsync` filter, ADR
     0024 shape).
- **U00 (sign-off + ADR + roadmap trio) is DONE by lock** (this session):
  ADR 0039 Accepted; `Milestones.cs` gains the `PG` row (`StatusPlanned`,
  after `RE`, before `M4`); README Roadmap gains the `PG` row;
  `MilestonesTests.cs` `Ids` array gains `"PG"`; build + Web tests (203) green.
- **Next unit: U01** (`Page` + `PageTranslation` docs + registration + DI —
  zero behavior).

## Log

_(The first `## U1` section appears when the U01 agent ships the `Page` /
`PageTranslation` docs + registration + DI. U00 was done by the lock, not as a
fresh-agent unit, so it has no `## U0` log section here.)_
