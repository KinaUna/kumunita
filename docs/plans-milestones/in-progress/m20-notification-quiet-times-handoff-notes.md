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

## U02 — the `NotificationQuietSchedule` doc + pure evaluator + owner-scope seams (Core)

- **Delivered (Core-only, 4 files):**
  1. `src/Kumunita.Core/Notifications/NotificationQuietSchedule.cs` — the doc
     + the `QuietScheduleMode` enum (`Blocked = 0` lean default / `Allowed = 1`),
     verbatim from the design doc §4 / the register's D2 block (one row per
     recipient, `RecipientId` identity, `Enabled` floor default `false`,
     `Hours`/`DaysOfWeek` `int[]` empty = all, `Updated?`).
  2. `src/Kumunita.Core/Notifications/QuietScheduleEvaluator.cs` — the
     **pure, static** `IsQuietNow(schedule, now, effectiveZone)` (D3): no
     session / IO; wall-clock-first (convert `now` → `effectiveZone` local,
     then read hour + day); empty axis = all; `Blocked` = quiet during the
     window, `Allowed` = the inverse; `null` / `Enabled == false` → `false`
     (the C-M20·3 floor).
  3. `src/Kumunita.Core/M6DocTypes.cs` — additive `.Identity(s => s.RecipientId)`
     pin for `NotificationQuietSchedule` (the `NotificationPreference` shape —
     a bare `Schema.For<>()` would fail Marten identity resolution).
  4. `src/Kumunita.Core/Notifications/NotificationService.cs` — the two
     **owner-scope** seams (D2, ADR 0019 shape, **no** `AccessAudit` row):
     `GetQuietScheduleAsync` (returns the row or `null`) and
     `SetQuietScheduleAsync` (store-or-update; `null` = delete/clear; stamps
     `Updated` on a real write). Placed with the preference lanes.
- **Tests (4 new pins, added to `Kumunita.Core.Tests/NotificationServiceTests.cs`):**
  `IsQuietNow_Verdict_Differs_Between_UTC2_And_UTC` (GATE-2, C-M20·2 — a
  `Blocked` {22,23} schedule flips between a fixed UTC+2 zone and UTC on the
  same instant; uses `TimeZoneInfo.CreateCustomTimeZone` for a deterministic
  no-DST offset), `IsQuietNow_Missing_Schedule_Is_Never_Quiet` (GATE-3, C-M20·3
  — null + disabled → false; enabled empty-list Blocked → quiet at any hour),
  `NotificationQuietSchedule_StoreAndLoad_RoundTrips` (store → read →
  clear→null), and `SetQuietScheduleAsync_Does_Not_Append_AccessAudit`
  (C-M20·7 — a write appends **no** audit row).
- **Cross-unit facts locked for the later units:**
  1. **The evaluator's effective-zone resolution is NOT done here** — U03's
     `QuietNowForAsync` resolves it via the ADR 0019 chain
     (`IUserInfoService.GetProfileAsync` `Profile.TimeZone` override →
     `ILocalizationService.GetDefaultTimezoneAsync()` → `UTC` floor) and passes
     the concrete `TimeZoneInfo` into `IsQuietNow`. U02's seam read
     (`GetQuietScheduleAsync`) does not resolve a zone (it is a pure read).
  2. **The `Notification.EmailDeferred` field is NOT added here** — that is
     U03's `EmitAsync` gate (D1/D4, C-M20·1). U02 touched no existing seam and
     did not suppress/drop the inbox row (the drift-guard held).
  3. **Zero new authorization surface (C-M20·5):** no `AccessAction` /
     `AccessVia` / `Decide()` branch / `IAuthorizationService` method added —
     the quiet verdict stays a pure function of (schedule, instant, zone).
- **Open questions:** none. One implementation note (recorded, not a drift):
  the delete-on-clear uses the codebase's synchronous `session.Delete<T>(id)`
  lane (the `AuditPurgeService`/`LocalizationService` precedent), committed by
  the trailing `SaveChangesAsync` — `IDocumentSession.DeleteAsync` is not
  available in this Marten binding.
- **Exit gate:** `dotnet build Kumunita.slnx -c Debug` green (6 pre-existing
  `xUnit1051`/`CS8602` warnings in `Kumunita.Web.Tests` — none in
  `NotificationServiceTests`); `dotnet exec tests\Kumunita.Core.Tests\bin\
  Debug\net10.0\Kumunita.Core.Tests.dll` green — **Total 1053, Failed 0** (the
  four new M20 pins + every existing M6 pin still passing).
- **Next unit entry point:** **U03** (`in-progress/m20-u03.md`) — the
  `Notification.EmailDeferred` additive field + the `EmitAsync` quiet gate
  (defer the email, keep the inbox row — D1/D4, C-M20·1), using the **deferred
  idempotency key** `notification:{kind}:{source-id}:deferred` (U00's locked
  fact #3) + the U02 seams/evaluator. Reads the design doc §6 (the exact gate
  diff) + §5 (the `QuietNowForAsync` helper). Exits on `Kumunita.Core.Tests`.

## U03 — the `Notification.EmailDeferred` field + the `EmitAsync` quiet gate (Core)

- **Delivered (Core-only, 2 source files + the test file):**
  1. `src/Kumunita.Core/Notifications/Notification.cs` — the additive
     `public bool EmailDeferred { get; set; }` (D1, placed after `ReadAt`, the
     `ReadAt?`/`LinkPath`/`AcceptPath` additive-field precedent — zero
     migration, ADR 0004 §B.1).
  2. `src/Kumunita.Core/Notifications/NotificationService.cs` — the
     **GATE-1** gate in `EmitAsync` (the 9-arg overload, **frozen signature —
     all existing emitters keep compiling**): inserted **after** the (4) D7
     email-kind gate and the (4a) `event.reminder` carve-out, **before** step
     (5) the email nudge. A quiet recipient keeps the inbox row (D7) and the
     row is marked `EmailDeferred = true`, then `return notification` (not
     `null` — C-M20·1; the gate never suppresses/drops the row); a non-quiet
     recipient is byte-identical to pre-M20 (C-M20·3). Plus the
     **`QuietNowForAsync`** private helper (D4) — loads the U02
     `NotificationQuietSchedule` on the caller's session, resolves the ADR 0019
     effective zone (`Profile.TimeZone` override →
     `ILocalizationService.GetDefaultTimezoneAsync()` → `TimeZoneInfo.Utc`
     floor; a blank/unknown id degrades to the UTC floor, **never throws into
     the caller's transaction**), and calls the pure
     `QuietScheduleEvaluator.IsQuietNow` (U02, D3) — so this is the *only*
     place a zone is resolved (U02's evaluator stays pure). Plus the
     **`DeferredKey(kind, sourceId)`** public static →
     `notification:{kind}:{sourceId}:deferred` (U00's locked fact #3; the
     **distinct** deferred key U04/U05 consume so the F10 outbox dedup does not
     collide with the emit-time key — a cleared email is delivered exactly once,
     C-M20·4).
  3. **One additive ctor seam** (not a deliverable in the unit plan, but
     required by it): a **trailing optional** `ILocalizationService?` ctor
     param (CS1736 idiom, last position) — the design-doc §5 helper needs the
     ADR 0019 platform-default zone read, which the pre-M20 `NotificationService`
     ctor did not inject. Because it is **optional and last**, all **nine**
     existing `new NotificationService(…)` sites (7 test harnesses + the DI
     lambda) keep compiling unchanged — I verified each. `DependencyInjection.cs`
     now passes `sp.GetRequiredService<Localization.ILocalizationService>()`
     (already registered transient). `IUserInfoService.GetProfileAsync` and
     `ILocalizationService.GetDefaultTimezoneAsync` take **no** `CancellationToken`
     (the interface shape) — the helper calls them with none.
- **Tests (4 new pins, `Kumunita.Core.Tests/NotificationServiceTests.cs`):**
  `EmitAsync_When_Quiet_Stores_Row_Marks_Deferred_Stages_No_Email` (**GATE-1**,
  C-M20·1 — a quiet-at-any-instant `Blocked` {all hours × all days} schedule:
  the row is stored, `EmailDeferred == true`, **no** email staged, the row is
  returned and visible via `ListInboxAsync`),
  `EmitAsync_NoSchedule_StagesEmailAsBefore` (**GATE-3**, C-M20·3 — no schedule:
  email staged under the **emit-time** key, `EmailDeferred == false`, the key
  does NOT end in `:deferred`),
  `EmitAsync_NonQuietWindow_StagesEmail` (a `Blocked` schedule on one hour 12h
  away from the current UTC hour — guaranteed not active: email staged,
  `EmailDeferred == false`, emit-time key used),
  `EmitAsync_KindDisabled_Still_SuppressesEmail` (a `KindsEnabled` list omitting
  the kind suppresses the email — the existing D7 gate is untouched, and because
  it sits **before** the quiet gate the row is `EmailDeferred == false`, proving
  the gate ordering D4).
- **Cross-unit facts locked for the later units:**
  1. **U04's flush reads `EmailDeferred == true` rows and stages them under
     `NotificationService.DeferredKey(kind, source)`** (the exact static form —
     do not re-derive it; a different form breaks the once-delivered guarantee,
     C-M20·4). The row's `SourceId` field is the `{source-id}` argument. The
     emit path **never** uses the deferred key — it stages under the emit-time
     key or not at all (the pins assert the staged key never ends in
     `:deferred` on the emit path).
  2. **The ADR 0019 zone resolution now lives in `QuietNowForAsync` (U03), not
     the evaluator (U02).** U02's `IsQuietNow` stays pure; U03 is the single
     place a `TimeZoneInfo` is resolved. The `ILocalizationService` is now an
     **optional** `NotificationService` ctor seam — a test that needs a
     non-UTC platform default passes a stand-in as the **last** ctor argument.
  3. **Zero new authorization surface (C-M20·5):** no `AccessAction` /
     `AccessVia` / `Decide()` branch / `IAuthorizationService` method added —
     the gate is a pure function of (schedule, instant, zone). The `EmailDeferred`
     field + the gate + `QuietNowForAsync` + `DeferredKey` are the only additions;
     the frozen `EmitAsync` 9-arg signature and `IMailerStage` seam are untouched.
- **Open questions:** none. The unit-plan's "6 Entry reads" + "2 files" set was
  implemented verbatim; the one ctor seam is a **necessary consequence** of the
  design-doc §5 helper (it names `ILocalizationService.GetDefaultTimezoneAsync`),
  landed additively + optionally so nothing else changed. No D# was amended.
- **Exit gate:** `dotnet build Kumunita.slnx -c Debug` green (the 6 pre-existing
  `xUnit1051`/`CS8602` warnings in `Kumunita.Web.Tests` — none in
  `NotificationServiceTests`); `dotnet exec tests\Kumunita.Core.Tests\bin\
  Debug\net10.0\Kumunita.Core.Tests.dll` green — **Total 1057, Failed 0**
  (U02's 1053 + the four new M20 gate pins; every existing M6 pin — the
  `EmitAsync` 6-arg/9-arg overloads, the ADR 0084/0095 pins, the ADR 0078
  sample-suppression pin — still passing).
- **Next unit entry point:** **U04** (`in-progress/m20-u04.md`) — Core: the
  `NotificationFlushService` (the §6.4 pure flush — reads `EmailDeferred ==
  true` rows, re-resolves + re-evaluates at the run instant, stages the now-clear
  ones under `NotificationService.DeferredKey(kind, source)` and flips them
  `false`, leaves the still-quiet ones) + the `LocaleSettings.QuietCheckMinutes`
  additive field + the cadence read/write seams. Reads the design doc §7 (the
  exact `FlushDeferredAsync` shape) + §8 (the cadence field + the ADR 0050
  `SetSignupOpenAsync` single-write-lane shape) + the ADR 0121; exits on
  `Kumunita.Core.Tests`.
