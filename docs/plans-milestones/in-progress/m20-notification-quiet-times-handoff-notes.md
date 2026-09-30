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
## U04 — the `NotificationFlushService` (§6.4 flush) + the `LocaleSettings.QuietCheckMinutes` field + the cadence seams (Core)

- **Delivered (Core-only, 3 source files + 2 test files):**
  1. `src/Kumunita.Core/Notifications/NotificationFlushService.cs` — the
     **Wolverine-free static** `NotificationFlushService` (the §6.4
     `EventReminderService` precedent verbatim). **Method name:
     `FlushDeferredAsync`** (the design-doc §7 authoritative name — NOT the
     plan file's `FlushAsync`; see "discrepancy" below). Signature, verbatim as
     implemented:
     `public static async Task<int> FlushDeferredAsync(IDocumentStore store,
     DateTimeOffset now, IMailerStage mailer, IUserInfoService userInfo,
     ILocalizationService localization, ITranslationProvider? translationProvider = null,
     CancellationToken ct = default)` → returns the count of emails delivered
     this run. Per run it (1) loads the bounded set of `Notification` rows with
     `EmailDeferred == true` (ordered by `Created`), (2) per row re-resolves the
     ADR 0019 effective zone (`userInfo.GetProfileAsync` → `Profile.TimeZone`
     override → `localization.GetDefaultTimezoneAsync()` → `TimeZoneInfo.Utc`
     floor) + re-evaluates the **pure** `QuietScheduleEvaluator.IsQuietNow` (U02,
     D3) at the **run** instant, (3) for each **no longer** quiet, stages the
     held email via the frozen `IMailerStage` under **U03's**
     `NotificationService.DeferredKey(kind, sourceId)` (the row's stored
     `Subject`/`Body` are the localized display values — reused, not re-derived)
     and flips the row `EmailDeferred = false`, (4) leaves each **still-quiet**
     row `EmailDeferred = true` (re-checked next run). Reads all rows/schedules
     **before** any write (the `EventReminderService` discipline), then opens
     **one** write session, stages + flips, commits **once** (C3). A recipient
     with no `Profile.Email` is still cleared (resolved, not re-tried forever —
     C-M20·4). The `translationProvider` param is **accepted to keep U05's
     call-site compiling** (design-doc §7) but **unused** — the row already
     carries the localized subject/body.
  2. `src/Kumunita.Core/Localization/LanguageCatalog.cs` — the additive
     `public int QuietCheckMinutes { get; set; } = 60;` on `LocaleSettings`
     (D6, the `IsSignupOpen`/`MessagingEnabled` additive-field precedent; ADR
     0004 §B.1; zero migration).
  3. **The cadence read/write seams — landed on `ILocalizationService` /
     `LocalizationService`, NOT on `NotificationService`** (the design-doc
     §7/§8 authoritative placement; see "discrepancy" below):
     `Task<int> GetQuietCheckMinutesAsync()` (a plain read — missing
     `LocaleSettings` row floors to `60`; **no** audit row) and
     `Task SetQuietCheckMinutesAsync(int minutes, string adminSubjectId)`
     (validates `[5, 1440]` → `ArgumentOutOfRangeException` **before** any
     write; load-or-creates the `LocaleSettings` singleton, sets
     `QuietCheckMinutes`, appends **exactly one** `AccessAudit` row
     `Action = "notification.quiet.cadence"` / `TargetKind = "notification.quiet"`
     / `TargetId = minutes` / `Via = Admin` / `Outcome = Allow`, commits; the
     Web boundary owns the GlobalAdmin standing check). Both are **frozen
     signature shapes** — note they take **no** `CancellationToken` (the
     `LocalizationService` read-lane idiom, matching
     `GetDefaultTimezoneAsync`/`SetDefaultTimezoneAsync`).
- **Tests (GATE-4 + GATE-6, 3 new pins):**
  - `tests/Kumunita.Core.Tests/NotificationFlushServiceTests.cs` (new file,
    `EventReminderServiceTests` idiom — real `PostgresFixture` `IDocumentStore`
    booting `M1DocTypes`+`M3DocTypes`+`M6DocTypes`, NSubstitute
    `IMailerStage` recording staged keys, NSubstitute `IUserInfoService` stubbing
    the planted `Profile`, NSubstitute `ILocalizationService` returning
    `GetDefaultTimezoneAsync().Returns("UTC")`):
    - `Flush_Cleared_Row_Stages_One_Email_Flips_Deferred` (**GATE-4**, C-M20·4)
      — a held row for a recipient with **no** schedule (the C-M20·3 floor:
      never quiet) → the flush stages the held email **exactly once** under
      `DeferredKey` (`notification:post.reply:reply-g4:deferred`) to the planted
      address and flips `EmailDeferred = false`; a **second** run stages
      **nothing** (still exactly one email total — idempotent).
    - `Flush_Still_Quiet_Row_Stays_Deferred_Across_Two_Runs` (**GATE-4**,
      C-M20·4) — a held row for a recipient with an always-quiet `Blocked`
      schedule (empty `Hours` = all hours, empty `DaysOfWeek` = all days) → the
      flush stages **nothing**, and the row stays `EmailDeferred = true` across
      two consecutive runs.
  - `tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` —
    `SetQuietCheckMinutes_Stores_Singleton_And_One_Audit_Row` (**GATE-6**,
    C-M20·6) — a missing `LocaleSettings` row reads back `60`
    (`GetQuietCheckMinutesAsync`); a valid write (`15`) persists (live on the
    next read) + appends **exactly one** `AccessAudit` row
    (`Via = Admin`, `Action = "notification.quiet.cadence"`, `TargetId = "15"`);
    out-of-range values (`0` and `99999`) throw `ArgumentOutOfRangeException`
    and write nothing (still exactly one cadence audit row, value unchanged).
- **Discrepancy resolved toward the design doc (per the U04 instruction):** the
  register / `m20-u04.md` plan named the flush method `FlushAsync` and placed the
  cadence seams on `NotificationService` (`SetQuietCheckMinutesAsync(int, string,
  CancellationToken)` + `GetQuietCheckMinutesAsync(CancellationToken)`). The
  design doc (the register's "authoritative C#") names it
  **`FlushDeferredAsync`** and places the cadence read/write seams on
  **`ILocalizationService`** (no `CancellationToken`, matching the existing
  `GetDefaultTimezoneAsync`/`SetDefaultTimezoneAsync` read-lane idiom). I
  implemented the **design-doc** shape (the user directed this resolution).
  `LocalizationService` is the **sole** `ILocalizationService` implementer, so
  the two additive interface members broke nothing.
- **Cross-unit facts locked for U05 (the Web host handler/tick):**
  1. **U05's `NotificationFlushHandler` calls
     `NotificationFlushService.FlushDeferredAsync(store, now, mailer, userInfo,
     localization, translationProvider, ct)`** — the exact signature above (note
     the param order: `mailer` before `userInfo` before `localization`, with the
     optional `translationProvider` before `ct`). It returns the delivered count
     for the log line. U05 **owns** the tick scheduling: it reads the cadence via
     **`ILocalizationService.GetQuietCheckMinutesAsync()`** (the U04 seam) and
     re-schedules the `NotificationFlushTick` at `now + QuietCheckMinutes`
     minutes. The handler does **not** re-run `EmitAsync` (D5's *Forbids* — the
     flush only stages the held email, D7/§drift-guard).
  2. **Zero new authorization surface (C-M20·5):** no `AccessAction` /
     `AccessVia` / `Decide()` branch / `IAuthorizationService` method added. The
     only new audit lane is the D6 cadence write (`Via = Admin`, the admin-plane
     precedent) — the flush itself writes **no** audit row (a side effect, the
     `EventReminderService` posture).
  3. **The flush never re-derives the deferred key** — it calls U03's
     `NotificationService.DeferredKey(kind, sourceId)` (the exact static form; a
     different form breaks the once-delivered guarantee, C-M20·4). It reuses the
     row's stored localized `Subject`/`Body` (the `translationProvider` param is
     accepted for the U05 call-site but unused).
- **Open questions:** none. All three Core deliverables + the three GATE pins
  are in; the register-vs-design-doc naming/placement discrepancy was resolved
  toward the design doc (recorded above + in the U05 entry point).
- **Exit gate:** `dotnet build Kumunita.slnx -c Debug` green (the 6 pre-existing
  `xUnit1051`/`CS8602` warnings in `Kumunita.Web.Tests` — none in the new
  files); `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\
  Kumunita.Core.Tests.dll` green — **Total 1060, Failed 0** (U03's 1057 + the
  three new M20 U04 pins — the two `NotificationFlushServiceTests` + the
  `LocalizationServiceTests` cadence pin; every existing Core pin still passing).
- **Next unit entry point:** **U05** (`in-progress/m20-u05.md`) — Web host: the
  `NotificationFlushHandler` (the §6.4 durable adapter over
  `NotificationFlushService.FlushDeferredAsync`) + the `NotificationFlushTick`
  self-rescheduling at `QuietCheckMinutes` (read via the U04
  `ILocalizationService.GetQuietCheckMinutesAsync()` seam). U05 is a **Web**
  unit — it does not re-derive the flush business logic, the deferred key, or
  the cadence field/seams (all U04's). It reads the design doc §7 (the handler
  adapter + the tick) + the U04 seam shapes above. Exits on
  `Kumunita.Web.Tests`. **STOP after U04 — do not chain into U05.**

## U05 — the `NotificationFlushHandler` + `NotificationFlushTick` + `Program.cs` seed (Web)

- **Delivered (Web-only, 3 files):**
  1. `src/Kumunita.Web/SideEffects/NotificationFlushHandler.cs` (new) — the
     **static** `NotificationFlushHandler.Handle` (the §6.4
     `EventReminderHandler`/`AuditPurgeHandler` shape) that calls U04's
     `NotificationFlushService.FlushDeferredAsync` (verbatim U04 signature —
     param order `mailer` → `userInfo` → `localization` → optional
     `translationProvider` → `ct`), then re-schedules at the **resolved admin
     cadence** via U04's `ILocalizationService.GetQuietCheckMinutesAsync()`
     (which floors a missing `LocaleSettings` row to 60 — the handler does
     **not** load the singleton or invent a floor constant). The handler does
     **not** inject `NotificationService` (the stale plan did) — it reads the
     cadence through the already-injected `ILocalizationService`. No re-run of
     `EmitAsync` (D5), no new auth surface (C-M20·5).
  2. Same file: `NotificationFlushTick` — the **one sanctioned divergence**
     from the three fixed-cadence §6.4 ticks: a record with **two explicit
     constructors** (a parameterless one — the first-boot seed at 60 min — and
     a `TimeSpan` one — the resolved-cadence re-schedule). `Wolverine.TimeoutMessage`
     is itself a **record** (verified: "Only records may inherit from records"),
     exposes a `TimeSpan` ctor + a settable **`DelayTime`** property (NOT
     `Delay` — the design-doc §7 sketch's `new() { Delay = cadence }` is a
     sketch; the real member is `DelayTime`), and the `TimeSpan` ctor populates
     `DelayTime` (probe-verified: `new Tick()` → 60 min, `new Tick(30 min)` →
     30 min). The two-ctor form (rather than a default-parameter one) is because
     C# requires default parameter values to be compile-time constants and
     `TimeSpan.FromMinutes(60)` is not one (CS1736, hit in the first attempt).
  3. `src/Kumunita.Web/Program.cs` — `await bus.PublishAsync(new
     NotificationFlushTick());` added to the post-`StartAsync` seed block (the
     parameterless 60-min form; idempotent; same constraints as the three
     existing §6.4 seeds — after `StartAsync`, in the `startupScope`).
  4. **3 Web pin tests** —
     `tests/Kumunita.Web.Tests/NotificationFlushHandlerTests.cs` (new,
     `PostgresFixture` integration shape, mirroring `NotificationsControllerTests`):
     - `Handle_With_Deferred_Row_Flushes_And_Reschedules_At_Resolved_Cadence`
       — a held row (no schedule = the C-M20·3 floor) is released: the mailer
       records one staged email under `DeferredKey` and the returned tick's
       `DelayTime` is 30 min (the cadence stubbed via the U04 seam).
     - `Handle_No_Deferred_Rows_Still_Reschedules` — no held rows: exactly one
       re-schedule tick at 45 min; the mailer records nothing.
     - `Program_Cs_Contains_NotificationFlushTick_Seed` — `Program.cs` source
       contains `PublishAsync(new NotificationFlushTick())`.
- **Discrepancy resolved toward U04's shipped code (per the U05 instruction):**
  the `m20-u05.md` plan still named the flush method `FlushAsync` and read the
  cadence via `NotificationService.GetQuietCheckMinutesAsync()` (and injected
  `NotificationService` for it). U04 shipped **`FlushDeferredAsync`** and placed
  the cadence seam on **`ILocalizationService`** (no `CancellationToken`). I
  consumed the **shipped** shape: the handler calls `FlushDeferredAsync` with
  the verbatim U04 param order and reads the cadence through the
  already-injected `ILocalizationService.GetQuietCheckMinutesAsync()`. The
  design doc §7 also uses a `DelayedFor` factory + mutable `Delay` — I resolved
  that toward the **sanctioned divergence** the plan/user direct (a
  resolvable-delay tick) and the real `TimeoutMessage` API (a `DelayTime`
  property, two explicit ctors). Net: the handler is functionally what the
  register's D5 intends (self-rescheduling at the resolved admin cadence); the
  one shape I deviated from is the tick's mechanism (two-ctor record vs. a
  `DelayedFor` factory), which is a presentation detail, not a D# change.
- **Cross-unit facts locked for U06/U07:**
  1. **U06 (resident UI) + U07 (admin UI) do NOT touch the handler or the
     tick.** U06 consumes only U02's two owner-scope seams
     (`GetQuietScheduleAsync`/`SetQuietScheduleAsync`); U07 consumes U04's
     `GetQuietCheckMinutesAsync`/`SetQuietCheckMinutesAsync`. The resident
     page shows only the resident's own blocked/allowed schedule, **not** the
     admin's flush cadence (the `QuietCheckMinutes` knob is admin-facing).
  2. **The `NotificationFlushTick` is a record** (not a plain class) — any
     downstream test that constructs one must use the no-arg or `TimeSpan`
     ctor; its delay is readable via the inherited `DelayTime` property.
  3. **Zero new authorization surface (C-M20·5):** no `AccessAction` /
     `AccessVia` / `Decide()` branch / `IAuthorizationService` method added —
     the handler is a side-effect adapter (the `EventReminderHandler` posture);
     the flush writes **no** `AccessAudit` row.
- **Open questions:** none. The design-doc §7 sketch (`DelayedFor` + `Delay`)
  was resolved toward the real `TimeoutMessage` API (`DelayTime` + two explicit
  ctors) — a presentation detail recorded here, not a D# amendment.
- **Exit gate:** `dotnet build Kumunita.slnx -c Debug` green (0 warnings in
  the new files; a transient slnx `.tmp/` folder reference to my throwaway
  probe projects was removed — it was an artifact of this unit's verification,
  not part of the deliverable); `dotnet exec
  tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` green —
  **Total 672, Failed 0** (U01's 669 + the three new M20 U05 pins; every
  existing Web pin still passing).
- **Next unit entry point:** **U06** (`in-progress/m20-u06.md`) — the 5th
  `/settings/quiet` resident section (D7) + view + **the closed 15-key `kw-l`
  set** × en/de/fr/da (U06 authors the **complete** set — resident
  `settings.quiet_*` + admin `admin.quiet_*` — U07 consumes the admin keys and
  adds none). U06 does **not** touch the handler, the tick, `Milestones.cs`, or
  `MilestonesTests`. It reads the design doc §7 (the resident section) + §10
  (the closed `kw-l` key set) + U02's two owner-scope seams. Exits on
  `Kumunita.Web.Tests`. **STOP after U05 — do not chain into U06.**