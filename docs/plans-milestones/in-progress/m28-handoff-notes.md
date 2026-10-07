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

## U02 — evaluator + pure tests

- **Polarity (load-bearing):** `IsAllowedNow` returns `true` = the child MAY
  use the platform now — the **inverse** of M20's `IsQuietNow` (allowed is the
  permission, not the restriction). The floor (<c>null</c> / `Enabled ==
  false`) returns **`true`** (always allowed, C-M28·3) — the opposite of M20's
  floor (`false` = never quiet). `Blocked` → allowed iff OUTSIDE the window
  (`!windowMatches`); `Allowed` → allowed iff INSIDE the window
  (`windowMatches`). Wall-clock-first (ADR 0019) + empty = all (D2) identical
  to M20. Separate pure function in `Kumunita.Core.UserInfo` (no `Notifications`
  dependency).
- **7 tests + status (all PASS):** `F3_Missing_Schedule_Is_Always_Allowed`,
  `F3_Disabled_Schedule_Is_Always_Allowed`, `F1_Blocked_InWindow_Is_Restricted`,
  `F1_Blocked_OutOfWindow_Is_Allowed`, `F2_Allowed_InWindow_Is_Allowed`,
  `F2_Allowed_OutOfWindow_Is_Restricted`, `F4_Verdict_Differs_Between_Zones` —
  runner: `Total: 7, Errors: 0, Failed: 0`.
- **Deviation (1, recorded):** the unit plan's F4 spec (`Hours=[22]`, `now=
  21:30 UTC`) is internally inconsistent — 23 ∉ [22] so no verdict flip, the
  `NotEqual` assert would fail. Resolved in favor of the named M20 analog
  (`Hours=[22,23]`): 21:30 UTC = 23:30 UTC+2 (inside → restricted) vs 21:30
  UTC (outside → allowed) — a genuine zone-dependent flip (C-M28·6).
- **Build / warnings:** `dotnet build Kumunita.slnx -c Debug` — **0 Error(s)**
  (80 pre-existing warnings, none in the two new M28 files).

## U03 — 3 seams + impl + standing/audit

- **3 seam signatures (verbatim, ADR 0006-E ADDs on `IUserInfoService`):**
  `Task<GuardianTimeLimitSchedule?> GetChildTimeLimitAsync(string guardianId,
  string childId)` (guardian-gated read, no audit); `Task
  SetChildTimeLimitAsync(string guardianId, string childId,
  GuardianTimeLimitSchedule? schedule)` (guardian-gated write, one audit row;
  `null` deletes the row — the M20 clear idiom); `Task<GuardianTimeLimit
  Schedule?> GetActiveTimeLimitAsync(string childId)` (enforcement read,
  child-keyed, no guardian gate, no audit).
- **One-audit-row shape (write only):** `Action = "guardian.time-limit.set"`,
  `TargetKind = "profile"`, `TargetId = childId`, `Outcome = Allow`, `Via =`
  `AccessVia.Guardian` (link lane) **or** `AccessVia.Admin` (GlobalAdmin G·5
  valve) — the `guardian.suspend` / `guardian.unsuspend` shape. Reads emit
  **no** row (C-M28·2). One `SaveChangesAsync` per write (C3); `null` →
  `session.Delete<GuardianTimeLimitSchedule>(childId)` (the M20 clear idiom).
- **Gate shape (C-M28·4/C-M28·7):** active `GuardianLink` for (guardian,
  child) ∪ GlobalAdmin, deny-by-default → `UnauthorizedAccessException` (Web
  404). GlobalAdmin standing resolved **lazily** off the `IServiceProvider?`
  seam → `IIdentityService.GetBySubjectAsync` (the `NotificationService`
  emission-shape, no construction cycle — `IdentityService` already depends on
  `IUserInfoService`); `services == null` (the positional test harnesses) ⇒ no
  admin standing. A child as `guardianId` on their own `childId` is denied
  (C-M28·7 — no link as GuardianId, never a GlobalAdmin).
- **7 tests + status (all PASS):** `F5_NonGuardian_Set_Is_Denied`,
  `F5_Guardian_Set_EmitsOneAuditRow_ViaGuardian`,
  `F5_GlobalAdmin_Set_EmitsOneAuditRow_ViaAdmin`,
  `F5_Set_Is_StronglyConsistent_NextReadSeesNewValue`,
  `C_M28_7_Child_Cannot_Set_Own_TimeLimit`, `F5_Clear_SetsNull_DeletesRow`,
  `C_M28_5_IAuthorizationService_Surface_Count_Unchanged` — runner: `Total:
  7, Errors: 0, Failed: 0`. (The 7th is a **structural** pin: reflects
  `IAuthorizationService` and asserts the distinct public instance method names
  are exactly `CanAsync` / `CanSeeAsync` / `CanSeeGroupAsync` /
  `CanSeeGroupFeedAsync` — the frozen 4-method surface, C-M28·5 unchanged.)
- **Deviation (1, recorded):** the design-doc C-M28·5 prose says "the frozen
  4-method surface" — the interface actually carries 8 methods (4 distinct
  names × the standalone + `IDocumentSession`-overload two-form pattern). The
  structural pin asserts on **distinct method names == 4**, not total method
  count — that is the "4-method surface" reading and is stable to the two-form
  pattern. (No seam/signature change; C-M28·5 = zero *new* surface, held.)
- **Build / warnings:** `dotnet build Kumunita.slnx -c Debug` — **0 Warning
  (s), 0 Error(s)**. No compile warnings.
