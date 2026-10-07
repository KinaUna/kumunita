# M28 — Guardian time limits — rolling handoff notes

> **Scratch tier** of the lane's three-tier contract. One `## U#` section per
> unit, **appended** (never rewritten). Each unit writes exactly one short
> section before it exits; the next unit reads only that section + its own
> entry-reads list. The primary tier is
> `docs/design/m28-guardian-time-limits-design.md` (authored by U00); the
> secondary tier (this lane's register) is
> `docs/plans-milestones/plan-m28-guardian-time-limits.md`.

## U00 — design doc + ADR 0151

- **Lock:** the [PROPOSED] D1–D9 set locked as-is (the register's set, verbatim
  in `docs/design/m28-guardian-time-limits-design.md` + `docs/adr/0151-
  guardian-time-limits.md`). **ADR 0151 verified free** against
  `docs/adr/README.md` (index ran 0001–0150) and on disk (no `0151-*.md`); the
  index row is now added after the 0150 row.
- **Invariants (7, by id):** C-M28·1 (whole-platform sign-out, not a 403),
  ·2 (pure verdict, not an auth decision), ·3 (floor: missing/disabled = never
  restricted), ·4 (guardian standing live + GlobalAdmin valve + strong
  consistency), ·5 (zero new authorization surface), ·6 (child's effective zone,
  wall-clock-first), ·7 (no self-lane).
- **FACES (6, by id):** F1 (Blocked in-window restricted / out allowed),
  F2 (Allowed in-window allowed / out restricted), F3 (the floor), F4 (zone-
  dependent verdict), F5 (guardian write requires active link ∪ GlobalAdmin;
  non-guardian denied), F6 (enforcement signs out + distinct message).
- **Seams (3, verbatim):** `GetChildTimeLimitAsync(string guardianId, string
  childId)`, `SetChildTimeLimitAsync(string guardianId, string childId,
  GuardianTimeLimitSchedule? schedule)`, `GetActiveTimeLimitAsync(string
  childId)` — all on `IUserInfoService` (ADR 0006-E ADDs); the write is the
  one audited row (`guardian.time-limit.set`), the two reads are unaudited.
- **Counts:** 14 kw-l keys (13 `guardian.timelimit.*` + the single
  `account.time_limit.login_message`) × 4 languages = 56 strings; **20** seam
  tests (U02 7 pure + U03 7 standing/audit + U04 3 middleware + U06 3 surface)
  + 3 acceptance-gate tests (U07). The 20 test names are pinned in the design
  doc §6.
- **Refinement (1, recorded in design doc §1.a):** the register's D7 lists "14
  keys"; verified as **13** `guardian.timelimit.*` + the single
  `account.time_limit.login_message` = 14 total (matched the M20 §10 closed-set
  shape). No D# representation amendment.
- **Pointer:** ADR 0151 (Accepted, 2026-10-07; Amends 0028 + 0006-E;
  references 0121 / 0019 / 0004 §B.1). **M28 is the LAST milestone on the
  roadmap** — the roadmap is complete after U09's close flip (U09 owns the
  `Milestones.cs` / `MilestonesTests` reframe / `WhatsNew.cs` 0.43.0 row; U00
  touches none of them).
- **No code, no build, no test** (a docs unit, per the unit plan).

## U01 — doc + enum + M1DocTypes

- **Doc fields (5, defaults):** `ChildId` (string, identity — the `GuardianLink.ChildId` analog),
  `Enabled` (bool, default `false` = the floor, C-M28·3), `Mode`
  (`TimeLimitMode`, default `Blocked` — the lean default), `Hours` (int[], 0–23,
  empty = all), `DaysOfWeek` (int[], 0=Sun…6=Sat, empty = all), `Updated?`
  (DateTimeOffset?, display-only). Mirrors `NotificationQuietSchedule`
  verbatim (with `ChildId` in place of `RecipientId`).
- **Enum values (2):** `TimeLimitMode.Blocked = 0` (restricted DURING the
  window, the lean default), `TimeLimitMode.Allowed = 1` (restricted EXCEPT the
  window, the allow-list). Closed two-value enum (the ADR 0120 D4 idiom).
- **M1DocTypes line:** new registration at `M1DocTypes.cs:78` —
  `opts.Schema.For<UserInfo.GuardianTimeLimitSchedule>().Identity(s => s.ChildId);`
  added immediately after the `GuardianLink` line (`:69`); part of the GU
  surface (no new `*DocTypes`, no EF migration, ADR 0004 §B.1).
- **Build / warnings:** `dotnet build Kumunita.slnx -c Debug` — **0 Warning(s),
  0 Error(s)**. No compile warnings. (One intentional deviation from the
  design doc §2.2 verbatim: the class doc-comment's forward reference to
  `GuardianTimeLimitEvaluator.IsAllowedNow` (a U02 type, not yet authored) is
  plain `<c>` text rather than `<see cref>` to keep the build warning-free;
  the type/shape is identical. No other deviation.)
