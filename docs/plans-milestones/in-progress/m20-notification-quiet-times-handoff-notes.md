# M20 — Notification quiet times — rolling handoff notes

> The cross-unit memory (register §drift-guard, tier 3). Every unit appends a
> short `## U##` section before it moves its own plan to `done/`. A unit reads
> the `## U##` sections before it; it does not re-derive what an earlier unit
> already settled (a D# amendment, the key set, a seam shape, the deferred
> idempotency-key form).

## U00

- **Delivered (docs-only, no build/test exit):** authored
  `docs/design/m20-notification-quiet-times-design.md` (the 14-section design
  doc, §9 "Seams & contracts (Part 2)" carries the exact C# for U02–U08),
  `docs/adr/0121-notification-quiet-times.md` (Status: **Accepted**, the
  Context/Decision/Consequences/Amendments/Supersedes shape), and one ADR index
  row appended after the 0120 row in `docs/adr/README.md`.
- **The [PROPOSED] set locked as-is — no amendment** (D1–D9, C-M20·1…C-M20·7,
  FACES F1–F5, the named trade, the six GATE acceptance tests GATE-1…GATE-6, the
  closed 15-key `kw-l` set, the §drift-guard). Recorded in the design doc's
  §1.a ("None") — unlike M19's D2 array→`[Flags]`, no representation amendment
  was needed; the D2 `NotificationQuietSchedule` doc and the D6
  `LocaleSettings.QuietCheckMinutes` field are authored verbatim as the
  register's prose blocks.
- **The three cross-unit facts the later units read from here:**
  1. **The §gate (GATE-1…GATE-6)** — the milestone's acceptance criterion; each
     is a pinned test in the named unit's deliverables (design doc §11).
  2. **The closed `kw-l` key set (15 keys × en/de/fr/da = 60 strings)** —
     U06 authors the **complete** set (resident `settings.quiet_*` + admin
     `admin.quiet_*`); U06/U07 consume; U07 adds none (design doc §10).
  3. **The deferred idempotency-key form**
     `notification:{kind}:{source-id}:deferred` — the **distinct** deferred key
     (different from the emit-time `notification:{kind}:{source-id}`) so the
     outbox dedup (F10) does not collide; the flush job (U04/U05) stages a
     cleared row with this key so a cleared email is delivered **exactly once**
     (C-M20·4). U03 records it; U04/U05 consume it (design doc §6 + §7).
- **Open questions:** none. The register's [PROPOSED] set was internally
  consistent and locked verbatim.
- **Next unit entry point:** **U01** (`in-progress/m20-u01.md`) — open M20
  (`Milestones.cs` flip to `StatusNext`, `MilestonesTests` re-pin,
  README/STATUS parity). U01 is the **only** unit that opens the milestone; it
  exits on `Kumunita.Web.Tests` (the `MilestonesTests` pin). It reads the design
  doc's §0 + the register's Unit-map row for U01.

## U01 — open M20 (the milestone flip to "In progress")

- **M20 opened:** `src/Kumunita.Web/Milestones.cs` M20 `StatusPlanned` →
  `StatusNext` (one-line change; M21/M22 stay `StatusPlanned`, M0–M19 stay
  `StatusDone` — the register's "M20 is the first of the remaining horizon"
  framing, the inverse of M19-which-was-last).
- **`MilestonesTests` re-pinned:** `No_Milestone_Is_InProgress_After_M19`
  (asserted **zero** `StatusNext`) replaced with
  `M20_Is_The_Single_InProgress_Milestone` (asserts M20 is the single
  `StatusNext`, M19 done, M21 + M22 still planned) — exactly the register's
  unit-map row; the other three pins (`Roadmap_Covers_M0_Through_M22_…`,
  `Shipped_Milestones_Are_Marked_Done`, `No_Milestone_Has_Blank_Title`)
  untouched — their id order + done-list through M19 were already correct.
- **README/STATUS parity:** README `## Status` — "The next horizon is planned:
  M20 …" → "**M20 is in progress** — notification quiet times (… ADR 0121);
  the remaining planned horizon is M21 … M22 …"; README Roadmap M20 row
  `**Planned.**` → `**In progress.** (ADR 0121)`; `docs/STATUS.md` line-40
  tail — "M19 is the last shipped milestone; the planned horizon is **M20** …
  None of M20–M22 has begun." → "**M20 is in progress** … M19 remains the
  last **shipped** milestone; the still-planned horizon is **M21** … and
  **M22** …" — both now name the ADR (0121) as the flip's provenance, per the
  M16/M17/M18 flip precedent (each names its ADR on the in-progress row).
- **Open questions:** none. The register's U01 deliverable set (3 files) was
  implemented verbatim; no D# was touched (U01 is a docs/flip unit — the
  drift-guard's "only U01/U08 touch `Milestones.cs`/`MilestonesTests`" rule is
  the only rule it was in scope to exercise, and it held).
- **Exit gate:** `dotnet build Kumunita.slnx -c Debug` green (6 pre-existing
  `xUnit1051`/`CS8602` warnings in `Bookmark*`/`AdminGuests` tests — none in
  `MilestonesTests`); `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\
  Kumunita.Web.Tests.dll` green — **Total 669, Failed 0** (the re-pinned
  `M20_Is_The_Single_InProgress_Milestone` plus the full pre-existing suite).
- **Next unit entry point:** **U02** (`in-progress/m20-u02.md`) — Core: the
  `NotificationQuietSchedule` doc + the `M6DocTypes` registration + the pure
  `QuietScheduleEvaluator.IsQuietNow` + the two owner-scope `NotificationService`
  seams (`GetQuietScheduleAsync` / `SetQuietScheduleAsync`). Reads the design
  doc's §4 (the doc shape) + §6 (the evaluator) + the ADR 0121; exits on
  `Kumunita.Core.Tests`.
