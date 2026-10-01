# ADR 0121 — Notification quiet times (per-resident quiet schedules on the M6 notification lane)

Status: Accepted
Date: 2026-09-30

The README roadmap names M20 exactly: "**per-resident quiet schedules
(allowed/blocked hours of day and days of week) on the M6 notification lane;
admin-set check cadence for pending notifications.**" "per-resident" is the
**owner-scope, per-recipient** standing (D2, D7); "allowed/blocked hours of day
and days of week" is a **schedule** with a *mode* over two independent axes
(D2, D3); "on the M6 notification lane" is the **email half of the two-part
notification** (D1, D4); "admin-set check cadence for pending notifications" is
a **durable re-check job + a `LocaleSettings` additive knob** (D5, D6, D8).
M20 adds a *time dimension* to the M6 email half, per resident, timezone-aware.

This ADR rides **frozen** seams, extending none of them:

- **The M6 notification lane (ADR 0076)** — the two-part notification (the
  **inbox row**, stored unconditionally, D7; the **email nudge**, the best-effort
  side effect staged via the frozen `IMailerStage`) and the frozen
  `NotificationService.EmitAsync` writer (the ADR 0084 per-target subscription
  gate + the ADR 0078 sample-suppression gate both *short-circuit with a
  `null` return before the inbox row is stored* — untouched).
- **The frozen `IMailerStage` stager (M1, ADR 0054 §3.6)** — the
  `StageAsync` idempotency guarantee is the **sole** email-side dedup
  (F10/§6.2); M20 adds **no** new method, the `OutboxEmail` row + the durable
  handler are **untouched** (C-M20·4).
- **The ADR 0019 effective-zone resolution** — "preference if present →
  instance default → `UTC` floor"; the instant is converted to the zone's
  wall-clock *first*, then the hour/day are read (the `kw-dt` TagHelper +
  `EventReminderService` shape) (D3, C-M20·2).
- **The §6.4 durable-job shape (ADR 0054 §3.6, `EventReminders`)** — the
  `TimeoutMessage` self-rescheduling tick + the thin Web adapter + the
  Wolverine-free Core service (D5).
- **The admin-set-runtime-field-on-singleton precedent (ADR 0050/0077/0105)** —
  the additive `LocaleSettings` field + the single audited write lane + the
  GlobalAdmin-gated `/admin` controller (D6, D8).
- **The ADR 0004 §B.1 additive-surface discipline** — every M20 schema change
  is an *additive* field (`Notification.EmailDeferred`,
  `LocaleSettings.QuietCheckMinutes`) or a *new* document
  (`NotificationQuietSchedule`); **zero migrations** (C-M20·1/3/6).

## Context

M6 (ADR 0076) already ships the two-part notification with the
"inbox durable, email best-effort" split (D7): the **inbox row** is a durable,
personal record (stored unconditionally — the `RecipientId` is the whole access
story, no `AccessAction`, no audit row on read), and the **email nudge** is a
best-effort side effect staged via the frozen `IMailerStage`. ADR 0019 already
ships the per-resident effective-timezone resolution (the
"preference → instance default → `UTC` floor" chain). The §6.4 durable-job
shape (ADR 0054 §3.6, `EventReminders`) and the admin-set-runtime-field-on-
singleton precedent (ADR 0050/0077/0105) are already in the repo.

What the M6 surface does *not* have is the **time dimension** on the email
half: a way for a resident to say "hold my notification *emails* during these
hours/days," evaluated in *their own* time zone, with the held email **not
dropped** but **released** when quiet lifts. The constraint that shapes the
decision is the same one that shaped ADR 0076/0019/0050: **compose the frozen
seams, never extend them.** The quiet schedule is a *per-resident document* on
the existing notification lane (D2), the verdict is a *pure function* of
(schedule, instant, effective zone) — **not** an authorization decision (D3),
the gate is an *additive* field + a *block* inside `EmitAsync` (D1, D4), the
release is a *durable job* (D5), and the cadence is an *additive singleton
field* + a *GlobalAdmin surface* (D6, D8). The one rule that keeps M20 safe
(C-M20·1, C-M20·5): **defer the email, never the inbox row; add zero new
authorization surface** — the ADR 0076 D7 inbox-durability guarantee, the frozen
`IMailerStage` seam, and the resident standing are byte-identical after M20.

## Decision

**D1 — Quiet times defer the email nudge, never the inbox row.** The M20 gate
is a *time* gate on the **email half** of the M6 two-part notification, applied
**inside `NotificationService.EmitAsync`** at the point where the email would
be staged (the step after the inbox row is stored and after the existing D7
email-kind gate). When the recipient's quiet schedule is active at emit time:
the **inbox row is still stored** (D7, unchanged), the row is marked
`EmailDeferred = true`, and the email is **not staged**. When it is not active:
the existing path is unchanged (email staged as today). *Forbids:*
suppressing/dropping the inbox row during quiet (violates ADR 0076 D7),
*dropping* the email (it must be **deferred**, not lost), or staging the email
during quiet.

**D2 — The quiet schedule is a per-resident document, floor = never quiet.**
A new document `Kumunita.Core.Notifications.NotificationQuietSchedule` (id =
the recipient's `SubjectId`, the `NotificationPreference` "one row per
recipient" shape — identity pinned to `RecipientId` in `M6DocTypes`, the ADR
0004 §B.1 additive-surface precedent): a `bool Enabled` master (a disabled
schedule is "never quiet"), a `QuietScheduleMode` (`Blocked` lean default vs
`Allowed` allow-list), and two independent axes (`int[] Hours`, `int[]
DaysOfWeek`; empty = all). Two **owner-scope** seams on `NotificationService`
(`GetQuietScheduleAsync` / `SetQuietScheduleAsync`), the
`SetProfileTimezoneAsync` single-write-lane shape — owner scope, **no**
`AccessAudit` row (ADR 0019). *Forbids:* a single `bool IsQuiet` on
`Profile`/`NotificationPreference`, a free-form string schedule, a per-kind
schedule in M20 (D9), or an audited direct `IDocumentSession.Store` from a
controller (the two service seams are the only writes).

**D3 — "Is it quiet now?" is a pure, timezone-aware evaluator.**
`QuietScheduleEvaluator.IsQuietNow(schedule, now, effectiveZone)` is a **pure
function** (no session, no HTTP, no IO — the `IcsWriter` / `UsageCapturePolicy`
"pure, closed, testable in isolation" discipline). It converts `now` to
`effectiveZone`'s **wall clock** (ADR 0019 — the instant is converted to the
zone's wall clock *first*, then the hour/day are read, exactly the `kw-dt`
TagHelper + `EventReminderService` resolution order), then evaluates the
schedule's `Mode`/`Hours`/`DaysOfWeek` against that wall clock. `null` schedule
→ `false` (never quiet, C-M20·3). The effective zone is resolved by the
**caller** (the ADR 0019 chain) and passed in. *Forbids:* evaluating in `UTC`
(the same wall-clock schedule is a different verdict in UTC+2 vs UTC — the
*point*), taking a client-supplied wall-clock string, or an impure evaluator
that reads a session.

**D4 — The gate lives in `EmitAsync`, after the inbox row, before the email.**
The exact insertion point (the 9-arg overload — the ADR 0084/0095
frozen-surface shape, **all existing emitters keep compiling unchanged**):
after the inbox row is stored (D7) and after the existing D7 email-kind gate,
add the quiet gate — consult the schedule, and if active mark
`EmailDeferred = true` and return (no `IMailerStage.StageAsync`). The
**deferred idempotency key** is a new, *distinct* stable key:
`notification:{kind}:{source-id}:deferred` — **different** from the emit-time
key so the outbox dedup (F10) does not collide, and the same key the flush job
(D5) stages, so a cleared email is delivered **exactly once** (C-M20·4).
*Forbids:* a gate that returns `null` (that would suppress the inbox row —
D7), a deferred key that reuses the emit-time key (F10 collision), or staging
the email during quiet.

**D5 — The pending flush is a §6.4 durable job (the `EventReminders` shape).**
The `NotificationFlushService` is a **Wolverine-free static class in
`Kumunita.Core`** (the `EventReminderService` precedent verbatim). Per run it:
loads the bounded set of `Notification` rows with `EmailDeferred = true`,
re-resolves the effective zone + re-evaluates the pure `IsQuietNow` at the
**run** instant, stages the now-clear ones via the **frozen** `IMailerStage`
with the deferred idempotency key (D4) and flips `EmailDeferred = false`, and
leaves the still-quiet ones `EmailDeferred = true` (re-checked next run). It
commits once per run (C3). The **Web** `NotificationFlushHandler` +
`NotificationFlushTick` is the thin adapter (the
`EventReminderHandler`/`EventReminderTick` `TimeoutMessage` self-rescheduling
shape), re-scheduling the next tick at the admin-set cadence (D6). `Program.cs`
seeds the first tick. *Forbids:* an in-memory timer (a Coolify redeploy must
not drop a pending run), re-running the whole `EmitAsync` (it would
double-store the inbox row, D7), or a flush that stages a still-quiet email.

**D6 — The check cadence is an admin-set additive field on `LocaleSettings`.**
`LocaleSettings` (the ADR 0005 §B per-instance singleton) gains one **additive**
`int QuietCheckMinutes` (default `60`; a missing row floors to `60`), the ADR
0050/0077/0105 admin-set-runtime-field-on-singleton precedent. A
**GlobalAdmin-only** write lane on the Core service:
`SetQuietCheckMinutesAsync(int minutes, string adminSubjectId)` — validates
(a floor of `5`, a ceiling of `1440`), stores it, and appends **exactly one**
`AccessAudit` row (`Via = Admin`). The read floor (missing row → `60`) is
resolved in the Core seam. *Forbids:* a hard-coded cadence, a config-file-only
knob, a cadence written by a non-GlobalAdmin, or an unaudited direct store from
a controller.

**D7 — The resident surface is the 5th settings section, `/settings/quiet`.**
`LocaleController` (ADR 0080 — the 4-section split) gains a **5th section**
`/settings/quiet`, mirroring the ADR 0019 **time-zone** section exactly
(per-resident, owner-scope, no-audit-row, folded into the same
`LocaleSettingsViewModel` via the shared `BuildModel()`): `GET` (render the
schedule + picker) and `POST` (save/clear, owner-scope — a `null` body clears =
never quiet). The `_SettingsTabs` sub-nav gains a "Quiet hours" tab. *Forbids:*
a separate `QuietController`, a surface that lets a resident set *another*
resident's schedule, or an un-audited *auditable* write (it is a personal
preference — **no** audit row, C-M20·7).

**D8 — The admin cadence surface is `/admin/quiet`, GlobalAdmin-gated.**
A dedicated `AdminQuietController` — **not** a new action on the fat
`AdminController` — mirroring `AdminSignupController` (ADR 0050):
`[Route("admin/quiet")]`, `[Authorize(Roles = Roles.GlobalAdmin)]`, a thin
read/set for the cadence (D6) over `SetQuietCheckMinutesAsync`. The GlobalAdmin
gate is checked **first** on both GET and POST; a non-GlobalAdmin lands a
404/redirect. Affordances on `/admin` index are hidden-not-disabled.
*Forbids:* a new action on the fat `AdminController`, a self-service cadence
path, or an admin surface that writes the cadence without a GlobalAdmin standing
check.

**D9 — Deferred lanes (each a future ADR, listed so a unit does not reach for
them).** (1) Per-kind quiet. (2) A platform-wide maintenance window. (3) Inbox
deferral. (4) Quiet for the sender's timezone / a shared household schedule.
(5) Delivery-time prediction / "will arrive at ~7 a.m." UX. *Forbids:* a unit
implementing any of these inside an M20 unit.

## Consequences

**Positive:**

- **A resident's quiet hours are real, timezone-aware, and never lose an
  email** (the README M20 row, end-to-end): a resident sets their schedule on
  `/settings/quiet` (D2, D7), a quiet recipient still gets the inbox row but
  the email is held (D1, D4 — GATE-1's
  `EmitAsync_When_Quiet_Stores_Row_Marks_Deferred_Stages_No_Email` pin), the
  held email is delivered exactly once when quiet lifts (D5 — GATE-4's
  `Flush_Cleared_Row_Stages_One_Email_Flips_Deferred` pin), and an admin sets
  the check cadence on `/admin/quiet` (D6, D8 — GATE-6's
  `SetQuietCheckMinutes_Stores_Singleton_And_One_Audit_Row` pin). No new
  authorization surface (C-M20·5).
- **Zero new authorization surface — the strongest form** (C-M20·5): M20 adds
  **no** new `AccessAction`, **no** new `AccessVia`, **no** new adapter,
  **no** branch in `Decide()`, **no** new `IAuthorizationService` method,
  **no** new claim *type*. The quiet verdict is a **pure function** (D3) — the
  frozen `NotificationService` writer + the frozen `IMailerStage` stager + the
  frozen ADR 0019 zone resolution are the **only** touchpoints. The ADR 0076 D7
  inbox-durability guarantee, the `AccessAudit` lane, and the resident standing
  are **byte-identical** after M20 (the GATE-5 pin).
- **Floor = never quiet, byte-identical to pre-M20** (C-M20·3): a recipient
  with no `NotificationQuietSchedule` row (or a disabled schedule) is never
  quiet; every notification's email flows exactly as today (GATE-3's
  `IsQuietNow_Missing_Schedule_Is_Never_Quiet` pin).
- **Zero migrations** (D2, D6): the one additive `Notification.EmailDeferred`
  `bool`, the one additive `LocaleSettings.QuietCheckMinutes` `int`, and the
  new `NotificationQuietSchedule` document are delta-detected and applied
  idempotently at boot (the ADR 0004 §B.1 additive-surface shape) — every
  existing row reads its pre-M20 state, unchanged.

**Neutral / cost:**

- **The named trade — a *coarse, per-resident, all-kinds, email-only* schedule
  for zero new authorization surface:** M20 buys *quiet hours that respect the
  recipient's own time zone and never lose an email* with **zero new
  authorization surface + one additive doc (`NotificationQuietSchedule`) + one
  additive `Notification` field (`EmailDeferred`) + one additive
  `LocaleSettings` int (`QuietCheckMinutes`) + one §6.4 durable job**, in
  exchange for a **coarse, per-resident, all-kinds** schedule (D9): a resident
  gets "held at night" (or "held these hours these days"), **not** "hold only
  group posts," **not** "hold for the whole site," and **not** "hide the inbox
  row too." The per-kind quiet (D9·1), the global maintenance window (D9·2),
  and the inbox deferral (D9·3) are deliberately *not* in M20: they are larger
  policy designs that need their own ADRs.
- **A held email is delivered on the *next* check after quiet lifts, not
  instantaneously** (D5, D6): the admin-set cadence (default 60 min) bounds
  the worst-case delay. A resident who wants snappier delivery has an admin
  tighten the cadence (e.g. 15 min); a resident who wants less background
  churn has an admin loosen it (e.g. 360 min).
- **The `Notification.EmailDeferred` flag is the *only* additive change to the
  M6 inbox row** (D1): the inbox row's shape is otherwise unchanged; the two
  existing `null`-returning gates (ADR 0084 subscription, ADR 0078 sample) are
  untouched and still short-circuit *before* the inbox row is stored.

**Follow-on lanes (each its own ADR — the design doc's §deferred):** per-kind
quiet (D9·1); a platform-wide maintenance window (D9·2); inbox deferral (D9·3);
sender-zone / shared-household quiet (D9·4); delivery-time prediction UX (D9·5).

## Amendments

- **2026-09-30 — None.** The register's [PROPOSED] set (D1–D9) locked as-is.
  No D# required a representation amendment (unlike M19's D2 array→`[Flags]`).
  The D2 `NotificationQuietSchedule` document and the D6
  `LocaleSettings.QuietCheckMinutes` field are authored verbatim as the
  register's prose blocks. Status remains **Accepted**.

## Supersedes

- **None.** M20 **adds a time dimension to the M6 email half**; it does not
  supersede an earlier ADR. It *rides* ADR 0076 (the M6 two-part notification
  + D7 inbox-durability), ADR 0019 (the effective-zone chain), ADR 0054 §3.6
  (the §6.4 durable-job shape), ADR 0050 (the admin-settled policy + one
  audited write lane + `/admin` controller), ADR 0080 (the 4-section settings
  split M20 extends to a 5th), ADR 0004 §B.1 (additive-surface discipline), and
  ADR 0006 (zero new authorization surface). The ADR 0076 D7 inbox-durability
  guarantee, the frozen `NotificationService`/`IMailerStage` seams, the ADR
  0084 per-target subscription gate, and the ADR 0078 sample-suppression gate
  are **unchanged** (C-M20·1/5). This is stated explicitly so a later reader
  knows M20 is an **additive quiet-hours surface, not a correction** — no
  earlier ADR's decisions are revised or re-scoped by this one.

## Affected files

- `src/Kumunita.Core/Notifications/QuietScheduleMode.cs` — new (the two-value
  `QuietScheduleMode` enum) (D2).
- `src/Kumunita.Core/Notifications/NotificationQuietSchedule.cs` — new (the
  `NotificationQuietSchedule` document) (D2).
- `src/Kumunita.Core/Notifications/QuietScheduleEvaluator.cs` — new (the pure
  `IsQuietNow` evaluator) (D3).
- `src/Kumunita.Core/Notifications/Notification.cs` — the additive
  `EmailDeferred` `bool` (D1).
- `src/Kumunita.Core/Notifications/NotificationService.cs` — the
  `GetQuietScheduleAsync` / `SetQuietScheduleAsync` owner-scope seams (D2) +
  the `QuietNowForAsync` helper + the `EmitAsync` quiet gate (D4).
- `src/Kumunita.Core/Notifications/NotificationFlushService.cs` — new (the
  §6.4 flush business logic) (D5).
- `src/Kumunita.Core/M6DocTypes.cs` — the
  `Notifications.NotificationQuietSchedule` registration (pinned to
  `RecipientId`) (D2).
- `src/Kumunita.Core/Localization/LocaleSettings.cs` — the additive
  `QuietCheckMinutes` `int` (D6).
- `src/Kumunita.Core/Localization/ILocalizationService.cs` +
  `LocalizationService.cs` — the `GetQuietCheckMinutesAsync` +
  `SetQuietCheckMinutesAsync` seams (the single audited write lane) (D6).
- `src/Kumunita.Web/SideEffects/NotificationFlushHandler.cs` — new (the Web
  adapter + `NotificationFlushTick`) (D5); `src/Kumunita.Web/Program.cs` — the
  first-tick seed + DI wiring (D5).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` + the four locale
  files — the 15 `settings.quiet_*` / `admin.quiet_*` `kw-l` keys in
  en/de/fr/da (D7, D8).
- `src/Kumunita.Web/Controllers/LocaleController.cs` +
  `src/Kumunita.Web/Views/Locale/Quiet.cshtml` + the `_SettingsTabs` sub-nav —
  the 5th `/settings/quiet` resident section (D7, F1).
- `src/Kumunita.Web/Controllers/AdminQuietController.cs` +
  `src/Kumunita.Web/Views/AdminQuiet/Index.cshtml` + the `/admin` index
  affordance — the `/admin/quiet` GlobalAdmin cadence surface (D8, F4).
- `src/Kumunita.Web/Milestones.cs` + `README.md` + `docs/STATUS.md` +
  `docs/ARCHITECTURE.md` + `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the
  **two** milestone flips (U01 open: M20 → `StatusNext`; U08 close: M20 →
  `StatusDone` + M21 promoted to `StatusNext`).
- **New tests** (the GATE-1…6 pins): `tests/Kumunita.Core.Tests/` — the
  `IsQuietNow` evaluator pins (U02, GATE-2 / GATE-3), the `EmitAsync` quiet
  gate pin (U03, GATE-1), the `NotificationFlushService` pins (U04, GATE-4) +
  the `SetQuietCheckMinutesAsync` seam pin (U04, GATE-6); `tests/Kumunita.Web.Tests/`
  — the `AdminQuietController` GlobalAdmin-gate pins (U07, GATE-6) + the
  `/settings/quiet` owner-scope pins (U06, C-M20·7).
