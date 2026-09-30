# M20 — Notification quiet times (design doc)

> **Milestone M20 — Notification quiet times.** The README M20 line, verbatim:
> "**per-resident quiet schedules (allowed/blocked hours of day and days of
> week) on the M6 notification lane; admin-set check cadence for pending
> notifications.**" M6 (ADR 0076) already ships the two-part notification:
> the **inbox row** (a durable, personal record — stored unconditionally, D7)
> and the **email nudge** (a best-effort side effect staged via the frozen
> `IMailerStage`). M20 adds a **time dimension** to the email half: a
> resident can say "hold my notification *emails* during these hours/days,"
> evaluated in **their own time zone** (ADR 0019).
>
> **A milestone over the frozen M6 notification lane + the frozen ADR 0019
> zone resolution.** Zero new bounded context, zero new `IAuthorizationService`
> surface, zero new `AccessAction`, zero new `AccessVia`, zero new adapter,
> zero new `Decide()` branch (C-M20·5, D1/D4). The **only** schema changes are
> **one additive `bool` on the existing `Notification` POCO** (`EmailDeferred`,
> D1 — the ADR 0004 §B.1 additive-field precedent) **+ one new document**
> (`NotificationQuietSchedule`, D2 — registered on `M6DocTypes`) **+ one new
> enum** (`QuietScheduleMode`, D2) **+ one additive `int` on the existing
> `LocaleSettings` singleton** (`QuietCheckMinutes`, D6 — the ADR 0050/0077/0105
> admin-set-runtime-field precedent). The closed `kw-l` key set (15 keys ×
> en/de/fr/da, §10) + the six GATE acceptance tests (GATE-1…GATE-6, §11) are
> locked in **ADR 0121 (Accepted, 2026-09-30)**.
>
> **The frozen notification writer, the frozen `IMailerStage` stager, the
> ADR 0019 zone resolution, and the ADR 0076 D7 inbox-durability guarantee are
> byte-identical** (C-M20·1/5). The quiet verdict is a **pure function** of
> (schedule, instant, effective zone) — not an authorization decision (D3).
>
> **Status.** **LOCKED.** The decisions D1–D9, the invariants C-M20·1…C-M20·7,
> the FACES F1–F5 + the named trade, the §10 `kw-l` key list, and the §11 gate
> test names are locked in **ADR 0121 (Accepted, 2026-09-30)**. The
> `[PROPOSED]` set in the register
> `docs/plans-milestones/in-progress/plan-m20-notification-quiet-times.md` is
> the locked set this doc restates **verbatim** (the U00 handoff entry records
> the lock). **No amendment was needed** — the register's [PROPOSED] set
> locked as-is (see §1.a).
>
> **The one thing every unit must respect:** M20 **defers the email, never the
> inbox row** (D1, C-M20·1) and **adds zero new authorization surface** (D3,
> C-M20·5). The quiet verdict is a pure function (D3) — the frozen
> `NotificationService` writer, the frozen `IMailerStage` stager, and the frozen
> ADR 0019 zone resolution are the **only** seams touched (an *additive* field
> on `Notification` + `LocaleSettings`, a new `NotificationQuietSchedule`
> document, and one **additive** read seam + one **additive** owner-scope write
> seam on `NotificationService`, and one **additive** audited write seam on the
> cadence). It does **not** add a branch to `Decide()`, a new
> `IAuthorizationService` signature, a new `AccessAction`, a new `AccessVia`,
> a new adapter, or a relational quiet table (C-M20·5, D1–D8, the §drift-guard
> below). Every register unit (U02's doc + evaluator + seams, U03's gate, U04's
> flush service + cadence, U05's tick, U06's resident section, U07's admin
> surface) enforces this at a different seam; the §drift-guard below is the
> **exact** set of traps that would break it.

## 0 — Scope & non-scope

**What M20 does (register's "Understanding", verbatim):** M20 = **notification
quiet times.** The roadmap line is the scope:
> "per-resident quiet schedules (allowed/blocked hours of day and days of
> week) on the M6 notification lane; admin-set check cadence for pending
> notifications."

M6 (ADR 0076) already ships the two-part notification: the **inbox row** (a
durable, personal record — stored unconditionally, D7) and the **email nudge**
(a best-effort side effect staged via the frozen `IMailerStage`, the
`event.reminder`-for-M4 carve-out aside). M20 adds a **time dimension** to the
email half: a resident can say "hold my notification *emails* during these
hours/days," evaluated in **their own time zone** (ADR 0019). Three things
follow from the roadmap sentence:

1. **The quiet schedule is *per-resident*** — it is the resident's own
   preference (the ADR 0019 time-zone / ADR 0020 date-format "preference if
   present → instance default → floor" shape), written by the resident on their
   own settings surface (owner-scope, no audit row — the
   `SetProfileTimezoneAsync` personal-preference lane).
2. **"Allowed/blocked hours of day and days of week"** = a **schedule** with a
   *mode* (allow-list vs block-list) over two independent axes (a set of hours
   of day ∪ a set of days of week). The floor is **no schedule = never quiet**
   (every notification's email flows, exactly as today).
3. **"Admin-set check cadence for pending notifications"** = when an email is
   held (because it is quiet), it is **held, not dropped**, and a **durable
   background job** (the §6.4 `EventReminders` shape, ADR 0054 §3.6) periodically
   **re-checks** the held ones and **delivers the ones whose quiet window has
   lifted**. How often it checks is the **admin-set cadence** (a
   `LocaleSettings` additive knob, the ADR 0050/0077/0105 admin-set-runtime
   field precedent).

**What M20 is NOT (the register's "What M20 is NOT", verbatim):**

- **Not per-kind quiet.** In M20 one quiet schedule governs **all** M6 kinds
  for a resident (a `group.post` and an `account.signup` are held or released
  together). Per-kind quiet (hold *some* kinds, not others) is a future lane
  (D9). The existing `NotificationPreference.KindsEnabled` (the *which-kinds*
  email gate, ADR 0076/0084) is **untouched** — M20 governs *when*, that governs
  *which*; the two are orthogonal and both additive.
- **Not a platform-wide maintenance window.** M20's quiet is **per-resident**
  only. The admin sets the **check cadence** (a number), **not** a quiet
  window for everyone. A global "the site is quiet at 3 a.m." is a future lane
  (D9).
- **Not inbox deferral.** The **inbox row is always stored** (ADR 0076 D7 — the
  inbox is the durable record; the email is the best-effort nudge). M20 defers
  **only the email nudge**. A quiet recipient still sees the row in their inbox;
  they just don't get the *email* until quiet lifts. Dropping/suppressing the
  inbox row during quiet would violate D7 and is forbidden (C-M20·1).
- **Not a new authorization surface.** M20 rides the frozen `NotificationService`
  writer + the frozen `IMailerStage` stager + the frozen ADR 0019 timezone
  resolution. It adds **no** `AccessAction` / `AccessVia` / `Decide()` branch /
  adapter / `IAuthorizationService` method (C-M20·5). The quiet verdict is a
  pure function of (schedule, instant, effective zone) — not an authorization
  decision.
- **Not a replacement for the sample-suppression or subscription gates.** The
  ADR 0078 sample gate and the ADR 0084 per-target subscription gate both
  **short-circuit `EmitAsync` with a `null` return *before* the inbox row is
  stored** — they suppress *everything*. The M20 quiet gate is different: it
  lets the **inbox row be stored** and defers **only the email**. It sits
  *after* the two existing gates (which return `null`) and *after* the inbox
  row is stored, *before* the email is staged.

**The architectural decision (locked by U00):** M20 expresses quiet as a
**per-resident, email-only, timezone-aware schedule** (a new
`NotificationQuietSchedule` document + a pure `QuietScheduleEvaluator` + one
additive `Notification.EmailDeferred` flag + one additive
`LocaleSettings.QuietCheckMinutes` int), and applies the quiet verdict
**inside `NotificationService.EmitAsync`** at the point where the email would
be staged (D1, D4) — so the inbox row stays durable and the email is held,
delivered later by the §6.4 durable flush job (D5). This is D1 + D2 + D3 + D4
together — it is the whole point of the milestone, and it is what lets every
other surface (the ADR 0076 D7 inbox-durability guarantee, the frozen
`IMailerStage`, the ADR 0078/0084 gates, the resident standing) keep working
**unchanged**.

## 1 — Decisions (D1–D9)

**D1 — Quiet times defer the email nudge, never the inbox row.** The M20 gate
is a *time* gate on the **email half** of the M6 two-part notification, applied
**inside `NotificationService.EmitAsync`** at the point where the email would
be staged (the step after the inbox row is stored and after the existing D7
email-kind gate). When the recipient's quiet schedule is active at emit time:
the **inbox row is still stored** (D7, unchanged), the row is marked
`EmailDeferred = true`, and the email is **not staged**. When it is not active:
the existing path is unchanged (email staged as today).

*Forbids:* suppressing/dropping the inbox row during quiet (violates ADR 0076
D7), *dropping* the email (it must be **deferred**, not lost — the roadmap's
"pending notifications" implies held-then-delivered), or staging the email
during quiet.

**D2 — The quiet schedule is a per-resident document, floor = never quiet.**
A new document `Kumunita.Core.Notifications.NotificationQuietSchedule` (id =
the recipient's `SubjectId`, the `NotificationPreference` "one row per
recipient" shape — identity pinned to `RecipientId` in `M6DocTypes`, the ADR
0004 §B.1 additive-surface precedent). Shape (locked by U00's design doc §4):

```csharp
public sealed class NotificationQuietSchedule
{
    public string RecipientId { get; set; } = string.Empty;   // document identity (the SubjectId this schedule is FOR)
    public bool Enabled { get; set; } = false;                 // the master on/off; a disabled schedule is "never quiet" (the floor)
    public QuietScheduleMode Mode { get; set; } = QuietScheduleMode.Blocked;
    public int[] Hours { get; set; } = [];                      // hours of day (0–23); empty = all hours
    public int[] DaysOfWeek { get; set; } = [];                 // days of week (0=Sunday…6=Saturday); empty = all days
    public DateTimeOffset? Updated { get; set; }
}

public enum QuietScheduleMode
{
    Blocked = 0,   // the lean default: held DURING the listed hours/days
    Allowed = 1,   // quiet EXCEPT the listed hours/days (allow-list)
}
```

Two **owner-scope** seams on `NotificationService` (the `SetProfileTimezoneAsync`
single-write-lane shape — owner scope, **no** `AccessAudit` row, ADR 0019):
`GetQuietScheduleAsync(recipientId, ct)` (returns the row or `null` = never
quiet) and `SetQuietScheduleAsync(recipientId, schedule, ct)` (store-or-update;
`null` schedule = clear = never quiet). The Web boundary owns the owner check
(the signed-in subject must equal `recipientId`); the Core seam does not
re-check `User`.

*Forbids:* a single `bool IsQuiet` on `Profile`/`NotificationPreference`, a
free-form string schedule, a per-kind schedule in M20 (D9), or an audited
direct `IDocumentSession.Store` from a controller (the two service seams are
the only writes).

**D3 — "Is it quiet now?" is a pure, timezone-aware evaluator.**
`QuietScheduleEvaluator.IsQuietNow(NotificationQuietSchedule schedule,
DateTimeOffset now, TimeZoneInfo effectiveZone)` is a **pure function** (no
session, no HTTP, no IO — the `UsageCapturePolicy` / `IcsWriter` "pure,
closed, testable in isolation" discipline). It converts `now` to
`effectiveZone`'s **wall clock** (ADR 0019 — the instant is converted to the
zone's wall clock *first*, then the hour/day are read, exactly the `kw-dt`
TagHelper + `EventReminderService` resolution order), then evaluates the
schedule's `Mode`/`Hours`/`DaysOfWeek` against that wall clock. `null` schedule
→ `false` (never quiet, C-M20·3). An empty `Hours` array means "all hours"; an
empty `DaysOfWeek` array means "all days" (a schedule with only hours set is
quiet on every day during those hours). The effective zone is resolved by the
**caller** (the ADR 0019 chain: `Profile.TimeZone` override →
`LocaleSettings.DefaultTimezone` → `UTC` floor) and passed in — the evaluator
itself never touches a session.

*Forbids:* evaluating in `UTC` (ignores ADR 0019 — the same wall-clock schedule
is a different verdict in UTC+2 vs UTC, which is the *point*), taking a
client-supplied wall-clock string (trust boundary — the instant is the server's
`DateTimeOffset.UtcNow`, the zone is the resident's resolved setting), or an
impure evaluator that reads a session (it would be untestable in isolation).

**D4 — The gate lives in `EmitAsync`, after the inbox row, before the email.**
The exact insertion point in `NotificationService.EmitAsync` (the 9-arg
overload — the ADR 0084/0095 frozen-surface shape, **all existing emitters keep
compiling unchanged**): after the inbox row is stored (D7) and after the
existing D7 email-kind gate (`EmailEnabledForAsync`) resolves "email is
enabled," add the quiet gate:

```csharp
// (M20 — the quiet-hours gate, D4) — consulted AFTER the inbox row is stored
// and AFTER the D7 email-kind gate, BEFORE the email is staged. A quiet
// recipient keeps the inbox row (D7) but the email is DEFERRED, not dropped
// (D1): mark the row EmailDeferred and return — no IMailerStage.StageAsync.
if (await QuietNowForAsync(session, recipientId, now: DateTimeOffset.UtcNow, ct))
{
    notification.EmailDeferred = true;
    return notification;   // inbox row stored (D7); email held (D1)
}
```

`QuietNowForAsync(session, recipientId, now, ct)` is a small private helper that
(1) loads the recipient's `NotificationQuietSchedule`, (2) resolves the
effective zone via the ADR 0019 chain (`IUserInfoService` profile +
`ILocalizationService.GetDefaultTimezoneAsync()`), and (3) calls the pure
`QuietScheduleEvaluator.IsQuietNow` (D3). The **deferred idempotency key** is a
new, *distinct* stable key (the §6.2 per-email-key precedent):
`notification:{kind}:{source-id}:deferred` — **different** from the emit-time
key so the outbox dedup (F10) does **not** collide with an earlier emit-time
stage, and the same deferred key is what the flush job (D5) stages, so a
cleared email is delivered **exactly once** (C-M20·4).

*Forbids:* a gate that returns `null` (that would suppress the inbox row —
D7), a deferred key that reuses the emit-time key (F10 collision → double- or
zero-delivery), or staging the email during quiet.

**D5 — The pending flush is a §6.4 durable job (the `EventReminders` shape).**
The `NotificationFlushService` is a **Wolverine-free static class in
`Kumunita.Core`** (the `EventReminderService` precedent verbatim — the business
logic lives in Core so the harness tests the shape without a message host).
Per run it: (1) loads the bounded set of `Notification` rows with
`EmailDeferred = true` (the `ListInboxAsync` feed query shape, ordered by
`Created`), (2) for each, re-resolves the effective zone + re-evaluates the
pure `IsQuietNow` (D3) at the **run** instant, (3) for each that is **no
longer quiet**, stages the held email via the **frozen** `IMailerStage` with
the deferred idempotency key (D4) and flips that row's `EmailDeferred =
false`, (4) leaves each still-quiet row `EmailDeferred = true` (re-checked next
run — the re-runnable/idempotent §6.2 guarantee, the `EventReminderService`
"existing-row check" guard). It commits once per run (C3). The **Web**
`NotificationFlushHandler` + `NotificationFlushTick` is the thin adapter (the
`EventReminderHandler`/`EventReminderTick` `TimeoutMessage` self-rescheduling
shape): it injects the live `IDocumentStore` + frozen `IMailerStage` +
`ILocalizationService` + `ITranslationProvider` into the Core service, then
**re-schedules the next tick at the admin-set cadence** (D6). `Program.cs`
seeds the first tick (the `EventReminderHandler`/`AuditPurgeHandler`
`Program.cs` precedent).

*Forbids:* an in-memory timer (the `AuditPurge`/`EventReminder` durable
precedent — a Coolify redeploy must not drop a pending run), re-running the
whole `EmitAsync` in the flush (it only *stages the held email* — re-emitting
would double-store the inbox row, D7), or a flush that stages a still-quiet
email.

**D6 — The check cadence is an admin-set additive field on `LocaleSettings`.**
`LocaleSettings` (the ADR 0005 §B per-instance singleton) gains one **additive**
field (the ADR 0050 `IsSignupOpen` / ADR 0077 `NotifyAdminsOnSignup` / ADR 0105
`MessagingEnabled` admin-set-runtime-field-on-singleton precedent, ADR 0004
§B.1):

```csharp
/// <summary>
/// M20 (ADR 0121, D6) — how often the §6.4 notification-flush job re-checks
/// the held (deferred) notification emails, in **minutes**. An *additive*
/// field on the singleton (ADR 0004 §B.1), the same shape as
/// <see cref="IsSignupOpen"/> / <see cref="NotifyAdminsOnSignup"/>. Defaults
/// to <c>60</c> (a fresh instance re-checks held emails about once an hour);
/// a missing settings row floors to <c>60</c>. A GlobalAdmin tightens it
/// (e.g. <c>15</c> for snappier delivery) or loosens it (e.g. <c>360</c> to
/// reduce background churn) on <c>/admin/quiet</c> (D8).
/// </summary>
public int QuietCheckMinutes { get; set; } = 60;
```

A **GlobalAdmin-only** write lane (one audited write, the ADR 0050
`SetSignupOpenAsync` single-write-lane shape) on the Core service:
`SetQuietCheckMinutesAsync(int minutes, string adminSubjectId)` — validates the
value (a floor of, e.g., `5` minutes and a ceiling of, e.g., `1440`, so an
admin cannot set a zero/negative or absurd cadence), stores it, and appends
**exactly one** `AccessAudit` row (`Via = Admin`, the admin-plane audit
precedent). The read floor (missing row → `60`) is resolved in the Core seam.

*Forbids:* a hard-coded cadence with no admin control (the roadmap says
"admin-set"), a config-file-only knob (`appsettings.json`) that an admin cannot
change at runtime, a cadence written by a non-GlobalAdmin, or an unaudited
direct store from a controller.

**D7 — The resident surface is the 5th settings section, `/settings/quiet`.**
`LocaleController` (ADR 0080 — the 4-section split: `/settings/language`,
`/settings/timezone`, `/settings/dateformat`, `/settings/email-language`) gains
a **5th section** `/settings/quiet`, mirroring the ADR 0019 **time-zone**
section exactly (both are time-related, per-resident, owner-scope, no-audit-row,
folded into the same `LocaleSettingsViewModel` via the shared `BuildModel()`):
`GET /settings/quiet` (render the resident's schedule + the mode/hours/days
picker, pre-selected) and `POST /settings/quiet` (save/clear, owner-scope —
`KumunitaPrincipal.SubjectId(User)` must equal the actor; a `null` body clears
the schedule = never quiet). The `_SettingsTabs` sub-nav gains a "Quiet hours"
tab. All user-visible strings are `kw-l` keys (the closed set, §10 below, U06
authors them).

*Forbids:* a separate `QuietController` (the ADR 0080 precedent is to fold the
section into `LocaleController`, like the time-zone section), a surface that
lets a resident set *another* resident's schedule (owner-scope), or an
un-audited *auditable* write (it is a personal preference — **no** audit row,
ADR 0019/0020 shape, C-M20·7).

**D8 — The admin cadence surface is `/admin/quiet`, GlobalAdmin-gated.**
A dedicated `AdminQuietController` — **not** a new action on the fat
`AdminController` — mirroring `AdminSignupController` (ADR 0050):
`[Route("admin/quiet")]`, `[Authorize(Roles = Roles.GlobalAdmin)]`, a thin
read/set for the cadence (D6) over `SetQuietCheckMinutesAsync`. The GlobalAdmin
gate is checked **first** on both GET and POST (the ADR 0050 "gate is
authoritative on both the read and write surface" rule); a non-GlobalAdmin
lands a 404/redirect (the consistent admin-surface failure shape). Affordances
on `/admin` index are hidden-not-disabled for non-GlobalAdmin actors.

*Forbids:* a new action on the fat `AdminController`, a self-service cadence
path, or an admin surface that writes the cadence without a GlobalAdmin standing
check (the Web boundary owns that check).

**D9 — Deferred lanes (each a future ADR, not part of M20).**

1. **Per-kind quiet** — hold *some* M6 kinds (e.g. only `group.post`) but not
   others. M20's one-schedule-governs-all is the lean default; per-kind quiet
   rides the existing `NotificationPreference.KindsEnabled` machinery + the
   D4 gate and needs its own design (it touches the ADR 0084 opt-in/opt-out
   table).
2. **A platform-wide maintenance window** — the admin sets a site-wide "quiet at
   3 a.m." for *everyone*. M20 is per-resident only; the global window is a
   future lane (it would be a `LocaleSettings` additive field + a gate in
   `EmitAsync`, but it is a different policy surface).
3. **Inbox deferral** — hiding the *inbox row* (not just the email) during
   quiet. M20 defers **only the email** (D1, C-M20·1 — the inbox is always
   durable, ADR 0076 D7); a "don't even show me the row until morning" is a
   larger design (it re-opens the D7 inbox-as-durable-record guarantee).
4. **Quiet for the *sender's* timezone, or a shared household schedule** — M20
   is strictly the *recipient's* own zone/schedule (D3); sender-zone or
   shared/household quiet is a future lane.
5. **Delivery-time prediction / "will arrive at ~7 a.m." UX** — M20 delivers
   when the next check runs after quiet lifts (the admin-set cadence, D6); a
   "next delivery time" estimate in the UI is a future UX lane.

*Forbids:* a unit implementing any of these inside an M20 unit.

### 1.a — Amendments

- **2026-09-30 — None.** The register's [PROPOSED] set (D1–D9) locked as-is.
  No D# required a representation amendment (unlike M19's D2 array→`[Flags]`).
  The D2 `NotificationQuietSchedule` document is authored verbatim as the
  register's prose block (a `bool` master + a `QuietScheduleMode` enum + two
  `int[]` axes); the D6 `LocaleSettings.QuietCheckMinutes` is authored verbatim
  as the register's prose block (an `int`, default `60`). Status remains
  **Accepted**.

## 2 — Invariants (C-M20·1 … C-M20·7)

- **C-M20·1 — The inbox row is always durable (ADR 0076 D7).** A quiet recipient
  **still gets the inbox row**; only the email is deferred. A test asserts
  `EmitAsync` during quiet stores the `Notification` row **and** sets
  `EmailDeferred = true` **and** stages **no** email, and that the recipient
  sees the row in their inbox (the `ListInboxAsync` feed) immediately.
- **C-M20·2 — The quiet verdict is pure + timezone-aware (D3).** The evaluator
  takes `(schedule, now, effectiveZone)` and returns a `bool`; no session, no
  HTTP, no IO. A test pins a schedule + an instant and asserts the verdict
  **differs** between the resident's UTC+2 zone and UTC (the same wall-clock
  schedule is a different verdict — the point of ADR 0019).
- **C-M20·3 — Floor = never quiet (D2).** A recipient with **no**
  `NotificationQuietSchedule` row is never quiet (every notification's email
  flows, exactly as pre-M20). A test asserts a missing schedule evaluates to
  "not quiet" and the emit path is byte-identical to today for that recipient.
- **C-M20·4 — Deferral is never loss + idempotent (D4/D5).** A deferred email
  is staged **exactly once** when it clears (the deferred idempotency key, the
  F10/§6.2 guarantee); a still-quiet row stays deferred across flush runs. A
  test asserts a cleared row stages one email (not zero, not two) and a
  still-quiet row is untouched across two consecutive flush runs.
- **C-M20·5 — Zero new authorization surface.** M20 adds **no** `AccessAction`
  / `AccessVia` / `Decide()` branch / `IAuthorizationService` method /
  adapter. The quiet gate + the flush ride the frozen `NotificationService`
  writer + the frozen `IMailerStage` stager + the frozen ADR 0019 zone
  resolution. A pin test asserts the `ClaimTypes.All` set is unchanged and
  there is no new `AccessVia`/`AccessAction` member.
- **C-M20·6 — The cadence floors to a default + is GlobalAdmin-only (D6).**
  `LocaleSettings.QuietCheckMinutes` is additive (default `60`); a missing
  row floors to `60`; a non-GlobalAdmin write is refused (one audited write
  lane, `Via = Admin`). A test asserts the floor + the GlobalAdmin-only write
  + the single `AccessAudit` row.
- **C-M20·7 — The resident surface is owner-scope, no audit row (D7, ADR
  0019).** A resident sets **their own** schedule; a non-owner write is refused;
  the write appends **no** `AccessAudit` row (the `SetProfileTimezoneAsync`
  personal-preference shape). A test asserts owner-scope + no-audit.

## 3 — FACES (F1–F5) + the named trade

- **F1 — A resident sets their quiet schedule** (D2, D7): pick the mode
  (allowed/blocked), the hours of day, and the days of week — floor = never
  quiet.
- **F2 — A quiet recipient still gets the inbox row, but the email is held**
  (D1, D4, C-M20·1): the row appears in the inbox immediately; the email does
  not arrive until quiet lifts.
- **F3 — When quiet lifts (and the next check runs), the held email is
  delivered** (D5, C-M20·4): the flush job stages the now-clear emails exactly
  once.
- **F4 — An admin sets the check cadence** (D6, D8, C-M20·6): how often the
  flush re-checks held emails (default 60 min, floors to 60 on a missing row).
- **F5 — Everything is timezone-aware to the recipient's own zone** (D3,
  C-M20·2, ADR 0019): "quiet at 10 p.m." means 10 p.m. *in their zone*, not
  UTC.

**The named trade.** M20 buys *quiet hours that respect the recipient's own
time zone and never lose an email* with **zero new authorization surface + one
additive doc (`NotificationQuietSchedule`) + one additive `Notification` field
(`EmailDeferred`) + one additive `LocaleSettings` int (`QuietCheckMinutes`) +
one §6.4 durable job**, in exchange for a **coarse, per-resident, all-kinds**
schedule (D9): a resident gets "held at night" (or "held these hours these
days"), **not** "hold only group posts," **not** "hold for the whole site," and
**not** "hide the inbox row too." The per-kind quiet (D9·1), the global
maintenance window (D9·2), and the inbox deferral (D9·3) are deliberately *not*
in M20: they are larger policy designs that need their own ADRs, and M20's value
(a resident who wants their phone to stop pinging them at 3 a.m. — in *their*
time zone — while nothing is lost and their inbox stays the durable record) is
delivered by the single per-resident, email-only, timezone-aware schedule.

## 4 — The `NotificationQuietSchedule` document + `QuietScheduleMode` enum (exact C#)

The exact C# for U02 to implement (the §9 block is the authoritative pin).
Namespace `Kumunita.Core.Notifications`.

```csharp
namespace Kumunita.Core.Notifications;

/// <summary>
/// M20 (ADR 0121, D2) — the quiet-schedule mode: the *polarity* of the
/// schedule. <see cref="Blocked"/> (the lean default) = "quiet DURING the
/// listed hours/days" (a block-list — the intuitive "quiet at night"
/// reading). <see cref="Allowed"/> = "quiet EXCEPT the listed hours/days"
/// (an allow-list — the email flows during the listed windows, held
/// otherwise). A closed two-value enum: a typo or a wrong value is impossible
/// (the ADR 0120 D4 "closed, enumerated set" idiom), and a new mode is a new
/// enum value (an additive append, never a free-form string).
/// </summary>
public enum QuietScheduleMode
{
    /// <summary>Held DURING the listed hours/days (the lean default).</summary>
    Blocked = 0,
    /// <summary>Held EXCEPT the listed hours/days (the allow-list reading).</summary>
    Allowed = 1,
}

/// <summary>
/// M20 (ADR 0121, D2) — the per-resident notification-quiet schedule. One
/// document per recipient (id = the recipient's <see cref="RecipientId"/> —
/// the <see cref="NotificationPreference"/> "one row per recipient" shape;
/// identity pinned to <c>RecipientId</c> in <c>M6DocTypes</c>).
/// <see cref="Enabled"/> is the master on/off: <c>false</c> = "never quiet"
/// (the floor — every notification's email flows, exactly as pre-M20,
/// C-M20·3). When <see cref="Enabled"/> is <c>true</c>, the quiet verdict is
/// the pure <see cref="QuietScheduleEvaluator.IsQuietNow"/> over
/// <see cref="Mode"/> / <see cref="Hours"/> / <see cref="DaysOfWeek"/> in the
/// recipient's own effective zone (D3, C-M20·2). <see cref="Hours"/> (0–23)
/// and <see cref="DaysOfWeek"/> (0=Sunday…6=Saturday) are independent axes; an
/// empty array means "all" (a schedule with only hours set is quiet on every
/// day during those hours).
/// </summary>
public sealed class NotificationQuietSchedule
{
    /// <summary>Document identity — the recipient's subject id (one schedule
    /// per recipient; the same id as <see cref="Notification.RecipientId"/>).</summary>
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>
    /// The master on/off (D2, C-M20·3). <c>false</c> = "never quiet" — the
    /// floor: every notification's email flows, exactly as pre-M20, regardless
    /// of <see cref="Mode"/> / <see cref="Hours"/> / <see cref="DaysOfWeek"/>.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// The schedule's mode (D2): <see cref="QuietScheduleMode.Blocked"/>
    /// (held DURING the listed hours/days, the lean default) vs
    /// <see cref="QuietScheduleMode.Allowed"/> (held EXCEPT the listed
    /// hours/days, the allow-list).
    /// </summary>
    public QuietScheduleMode Mode { get; set; } = QuietScheduleMode.Blocked;

    /// <summary>
    /// The hours of day (0–23) the mode applies to. **Empty = the mode applies
    /// to ALL hours** (the "all hours" reading — a schedule with only
    /// days-of-week set is quiet on those days all day).
    /// </summary>
    public int[] Hours { get; set; } = [];

    /// <summary>
    /// The days of week (0=Sunday…6=Saturday) the mode applies to. **Empty =
    /// the mode applies to ALL days** (a schedule with only hours set is quiet
    /// on every day during those hours).
    /// </summary>
    public int[] DaysOfWeek { get; set; } = [];

    /// <summary>The instant the resident last saved/cleared this schedule (for
    /// display; not a gate).</summary>
    public DateTimeOffset? Updated { get; set; }
}
```

**The `M6DocTypes` registration** (U02 — the `NotificationQuietSchedule`
document is Marten-native with a conventional `RecipientId` identity, like
`NotificationPreference`; the additive-surface delta is applied idempotently at
boot — ADR 0004 §B.1, zero migration):

```csharp
// In M6DocTypes.Configure(StoreOptions opts), alongside NotificationPreference:
opts.Schema.For<Notifications.NotificationQuietSchedule>().Identity(s => s.RecipientId);
```

## 5 — The pure `QuietScheduleEvaluator` + the owner-scope read/write seams (exact C#)

The exact `QuietScheduleEvaluator.IsQuietNow` signature (D3) + the two
owner-scope seams on `NotificationService` (D2). Namespace
`Kumunita.Core.Notifications`.

```csharp
// In Kumunita.Core/Notifications/QuietScheduleEvaluator.cs (U02):

/// <summary>
/// M20 (ADR 0121, D3) — the pure "is it quiet now?" evaluator. A **pure
/// function** (no session, no HTTP, no IO — the <c>IcsWriter</c> /
/// <c>UsageCapturePolicy</c> "pure, closed, testable in isolation" discipline):
/// it takes the resolved (schedule, instant, effective zone) and returns a
/// <c>bool</c>. It converts <paramref name="now"/> to
/// <paramref name="effectiveZone"/>'s **wall clock** (ADR 0019 — the instant is
/// converted to the zone's wall clock *first*, then the hour/day are read,
/// exactly the <c>kw-dt</c> TagHelper + <c>EventReminderService</c> resolution
/// order), then evaluates the schedule's <c>Mode</c>/<c>Hours</c>/
/// <c>DaysOfWeek</c> against that wall clock. <c>null</c> / <c>Enabled ==
/// false</c> → <c>false</c> (never quiet, C-M20·3). An empty <c>Hours</c> means
/// "all hours"; an empty <c>DaysOfWeek</c> means "all days".
/// </summary>
public static bool IsQuietNow(
    NotificationQuietSchedule? schedule,
    DateTimeOffset now,
    TimeZoneInfo effectiveZone)
{
    // The floor: no schedule, or the master off → never quiet (C-M20·3).
    if (schedule is null || !schedule.Enabled)
        return false;

    // ADR 0019 — wall-clock-first: convert the instant to the zone's local
    // time, THEN read the hour + day (the kw-dt TagHelper shape).
    var wall = TimeZoneInfo.ConvertTime(now, effectiveZone);

    // An empty axis = "all" (the D2 "empty = all hours / all days" reading).
    var hourMatches   = schedule.Hours.Length     == 0 || schedule.Hours.Contains(wall.Hour);
    var dayMatches    = schedule.DaysOfWeek.Length == 0 || schedule.DaysOfWeek.Contains((int)wall.DayOfWeek);
    var windowMatches = hourMatches && dayMatches;   // the schedule's window contains the instant

    // Blocked = quiet DURING the window (the lean default); Allowed = quiet
    // EXCEPT the window (the allow-list).
    return schedule.Mode == QuietScheduleMode.Blocked
        ? windowMatches
        : !windowMatches;
}
```

The two **owner-scope** seams (D2 — the `SetProfileTimezoneAsync`
single-write-lane shape: owner scope, **no** `AccessAudit` row, ADR 0019).
U02 adds them to `NotificationService` (the frozen `IUserInfoService` +
`ILocalizationService` dependencies are already in the constructor — the
`GetProfileAsync` / `GetDefaultTimezoneAsync` chain D3's resolution rides):

```csharp
// In NotificationService.cs (U02) — the two owner-scope seams (D2):

/// <summary>
/// M20 (ADR 0121, D2) — the quiet-schedule READ (owner-scope, no audit row —
/// the <c>SetProfileTimezoneAsync</c> personal-preference shape, ADR 0019).
/// Returns the recipient's schedule, or <c>null</c> = "never quiet" (the
/// floor, C-M20·3). The Web boundary owns the owner check (the signed-in
/// subject must equal <paramref name="recipientId"/>); this seam does not
/// re-check <c>User</c>.
/// </summary>
public async Task<NotificationQuietSchedule?> GetQuietScheduleAsync(
    string recipientId, CancellationToken ct = default);

/// <summary>
/// M20 (ADR 0121, D2) — the quiet-schedule WRITE (owner-scope, **no** audit
/// row — a personal preference, C-M20·7 / ADR 0019). Store-or-update the
/// recipient's schedule; a <c>null</c> <paramref name="schedule"/> clears it
/// (the recipient's schedule is deleted = "never quiet", the floor). The Web
/// boundary owns the owner check; this seam does not re-check <c>User</c> and
/// appends **no** <c>AccessAudit</c> row (the <c>SetProfileTimezoneAsync</c>
/// shape — C-M20·7).
/// </summary>
public async Task SetQuietScheduleAsync(
    string recipientId, NotificationQuietSchedule? schedule, CancellationToken ct = default);
```

**The D4 private helper** (U03 — the exact gate's resolution, called from the
§6 gate block). U03 adds it to `NotificationService`:

```csharp
// In NotificationService.cs (U03) — the D4 gate's resolution helper:

/// <summary>
/// M20 (ADR 0121, D4) — the quiet-gate resolution: (1) load the recipient's
/// <see cref="NotificationQuietSchedule"/> (the D2 read seam), (2) resolve the
/// effective zone via the ADR 0019 chain (<see cref="IUserInfoService"/>
/// profile <c>TimeZone</c> override →
/// <see cref="ILocalizationService.GetDefaultTimezoneAsync()"/> → <c>UTC</c>
/// floor), and (3) call the pure <see cref="QuietScheduleEvaluator.IsQuietNow"/>
/// (D3). <c>now</c> is the emit instant (the caller passes
/// <c>DateTimeOffset.UtcNow</c>; tests pin it). A missing schedule / a
/// disabled schedule → <c>false</c> (never quiet, C-M20·3).
/// </summary>
private async Task<bool> QuietNowForAsync(
    IDocumentSession session, string recipientId, DateTimeOffset now, CancellationToken ct)
{
    var schedule = await session.LoadAsync<NotificationQuietSchedule>(recipientId, ct).ConfigureAwait(false);
    if (schedule is null || !schedule.Enabled)
        return false;                                   // the floor (C-M20·3)

    // ADR 0019 chain — resolve the effective zone (the EventReminderService
    // "recipient's Profile is the actor" shape; the platform default read
    // floors to the IANA id, then the UTC floor):
    var profile = await _userInfo.GetProfileAsync(recipientId).ConfigureAwait(false);
    var zoneId = (profile?.TimeZone is { Length: > 0 } z ? z
                 : await _localization.GetDefaultTimezoneAsync().ConfigureAwait(false))
                 ?? "UTC";
    var zone = TimeZoneInfo.TryFindSystemTimeZoneById(zoneId) ?? TimeZoneInfo.Utc;

    return QuietScheduleEvaluator.IsQuietNow(schedule, now, zone);
}
```

**The two pinned test names from GATE-2 / GATE-3** (U02's deliverable; the
§11 block is the authoritative pin):

1. `IsQuietNow_Verdict_Differs_Between_UTC2_And_UTC` (GATE-2, C-M20·2) — the
   same schedule + the same instant yields a **different** quiet verdict in the
   recipient's UTC+2 zone than in UTC (the point of ADR 0019).
2. `IsQuietNow_Missing_Schedule_Is_Never_Quiet` (GATE-3, C-M20·3) — a
   `null` schedule (and a `Enabled == false` schedule) evaluates to "not
   quiet"; the emit path is byte-identical to pre-M20 for that recipient.

## 6 — The `EmitAsync` quiet gate (D4) — the exact insertion point (exact C# diff)

The exact `Notification.EmailDeferred` additive field (D1) + the exact
`EmitAsync` gate block. U03 implements both.

**The additive field** (D1 — the ADR 0004 §B.1 additive-field precedent,
placed after `ReadAt` in `Notification.cs`):

```csharp
// In Notification.cs (U03), after the ReadAt property:

/// <summary>
/// M20 (ADR 0121, D1) — the quiet-hours deferral flag. <c>true</c> means the
/// inbox row is stored (D7 — the inbox is the durable record) but the email
/// nudge is **held** because the recipient was in their quiet window at emit
/// time (D1). The §6.4 flush job (D5) re-checks the row; when the quiet
/// window has lifted it stages the held email via the frozen
/// <see cref="IMailerStage"/> with the deferred idempotency key
/// (<c>notification:{kind}:{source-id}:deferred</c>, D4) and flips this flag
/// back to <c>false</c>. A <c>false</c> default means every pre-M20 row reads
/// as "not deferred" — no migration (ADR 0004 §B.1). **This is the ONLY
/// additive change to the M6 inbox row.**
/// </summary>
public bool EmailDeferred { get; set; }
```

**The exact `EmitAsync` gate block** (D4 — inserted **after** the existing D7
email-kind gate at step (4) / the `event.reminder` carve-out at step (4a),
**before** the email nudge at step (5)). The frozen 9-arg overload keeps its
signature; **all existing emitters keep compiling unchanged** (C-M20·5 — the
additive-only change is the field + this block):

```csharp
// In NotificationService.EmitAsync(...) — the 9-arg overload, U03. Insert
// IMMEDIATELY AFTER the (4a) event.reminder carve-out and BEFORE the (5)
// email-nudge block. (4)/(4a) are unchanged; the two existing null-returning
// gates (the ADR 0084 subscription gate at (0) and the ADR 0078 sample gate at
// (2a)) are unchanged and still return null *before* the inbox row is stored.
//
//   if (!await EmailEnabledForAsync(session, recipientId, kind, ct)...)
//       return notification;                 // (4) — unchanged
//   if (kind == NotificationKinds.EventReminder)
//       return notification;                 // (4a) — unchanged
//
//   ┌── M20 (D4) — the quiet-hours gate (NEW) ───────────────────────────┐
    // (M20 — the quiet-hours gate, D4) — consulted AFTER the inbox row is
    // stored (D7) and AFTER the D7 email-kind gate, BEFORE the email is
    // staged. A quiet recipient keeps the inbox row (D7) but the email is
    // DEFERRED, not dropped (D1): mark the row EmailDeferred and return —
    // no IMailerStage.StageAsync. A non-quiet recipient falls through to the
    // existing (5) email-nudge path unchanged (byte-identical to pre-M20,
    // C-M20·3).
    if (await QuietNowForAsync(session, recipientId, now: DateTimeOffset.UtcNow, ct).ConfigureAwait(false))
    {
        notification.EmailDeferred = true;
        return notification;   // inbox row stored (D7); email held (D1)
    }
    // └──────────────────────────────────────────────────────────────────┘
    // (5) The email nudge — the IMailerStage idempotency guarantee ...       // unchanged
    //     await _mailer.StageAsync(session, idempotencyKey, ...);             // unchanged
```

> **The deferred idempotency key (D4, the cross-unit fact):** the flush job
> (D5) stages a cleared row with the **distinct** key
> `notification:{kind}:{source-id}:deferred` (the `notification.{kind}.body`
> template already carries `Subject`/`Body`; the deferred key re-uses the row's
> stored `IdempotencyKey`'s `{kind}` + `SourceId`). This is **different** from
> the emit-time key (`notification:{kind}:{source-id}`) so the outbox dedup
> (F10) does **not** collide with an earlier emit-time stage, and the same
> deferred key is what the flush stages, so a cleared email is delivered
> **exactly once** (C-M20·4). U03 records this key form; U04/U05 consume it.

**The GATE-1 pin test name** (U03's deliverable; the §11 block is the
authoritative pin):

- `EmitAsync_When_Quiet_Stores_Row_Marks_Deferred_Stages_No_Email` (GATE-1,
  C-M20·1) — `EmitAsync` for a quiet recipient stores the `Notification` row
  (D7), sets `EmailDeferred = true`, and stages **no** email; the recipient's
  inbox (`ListInboxAsync`) shows the row immediately.

## 7 — The §6.4 flush job (D5) — the Core service + the Web adapter (exact C#)

The exact `NotificationFlushService` static-method signature (U04 — the Core
service) + the exact `NotificationFlushHandler`/`NotificationFlushTick` shape
(U05 — the Web adapter, mirroring `EventReminderHandler`/`EventReminderTick`).

**The Core service** (D5 — the `EventReminderService` precedent verbatim: a
Wolverine-free static class in `Kumunita.Core` so the harness tests the shape
without a message host). U04 implements:

```csharp
// In Kumunita.Core/Notifications/NotificationFlushService.cs (U04):

/// <summary>
/// M20 (ADR 0121, D5) — the §6.4 <c>NotificationFlush</c> job's business logic
/// (the <see cref="EventReminderService"/> precedent verbatim — a
/// <b>Wolverine-free</b> static class that the Web host's thin adapter
/// (<c>NotificationFlushHandler</c>, U05) calls with a live
/// <see cref="IDocumentStore"/>). Per run: (1) load the bounded set of
/// <see cref="Notification"/> rows with <c>EmailDeferred == true</c> (ordered
/// by <c>Created</c>), (2) for each, re-resolve the effective zone +
/// re-evaluate the pure <see cref="QuietScheduleEvaluator.IsQuietNow"/> (D3)
/// at the **run** instant, (3) for each that is **no longer quiet**, stage the
/// held email via the **frozen** <see cref="IMailerStage"/> with the deferred
/// idempotency key (<c>notification:{kind}:{source-id}:deferred</c>, D4) and
/// flip that row's <c>EmailDeferred = false</c>, (4) leave each still-quiet
/// row <c>EmailDeferred = true</c> (re-checked next run). It commits **once
/// per run** (C3).
/// </summary>
public static class NotificationFlushService
{
    /// <summary>
    /// Run one flush tick. <paramref name="now"/> is the injection point for
    /// tests (pin "now" so the quiet verdict is deterministic — the
    /// <see cref="EventReminderService"/> shape). A recipient with **no**
    /// schedule or a disabled schedule is **no longer quiet** (the floor,
    /// C-M20·3) → their held email is staged (the flush is the *release* path).
    /// A still-quiet row is left <c>EmailDeferred = true</c> (C-M20·4 — a
    /// still-quiet row is untouched across two consecutive flush runs).
    /// </summary>
    public static async Task<int> FlushDeferredAsync(
        IDocumentStore store,
        DateTimeOffset now,
        IMailerStage mailer,
        IUserInfoService userInfo,
        ILocalizationService localization,
        ITranslationProvider? translationProvider = null,
        CancellationToken ct = default);
}
```

> **The flush does NOT re-run `EmitAsync`** (D5's *Forbids* — re-emitting would
> double-store the inbox row, D7, C-M20·1). It **only stages the held email**
> for rows whose quiet has lifted, using the row's stored
> `Subject`/`Body`/`LinkPath`/`AcceptPath`/`DeclinePath` + the deferred
> idempotency key (D4). The frozen `IMailerStage` seam is **not** re-shaped (no
> new method); the `OutboxEmail` row + the durable handler are **untouched**.

**The Web adapter** (D5 — the `EventReminderHandler`/`EventReminderTick`
`TimeoutMessage` self-rescheduling shape, U05). The tick re-schedules at the
**admin-set cadence** (D6) read from `LocaleSettings`:

```csharp
// In Kumunita.Web/SideEffects/NotificationFlushHandler.cs (U05):

/// <summary>
/// M20 (ADR 0121, D5) — the §6.4 <c>NotificationFlush</c> job's Web adapter
/// (the <see cref="EventReminderHandler"/>/ <see cref="EventReminderTick"/>
/// shape verbatim). A thin, Wolverine-<b>free</b>-on-the-logic adapter: it
/// injects the live <see cref="IDocumentStore"/> + the <b>frozen</b>
/// <see cref="IMailerStage"/> stager + <see cref="ILocalizationService"/> +
/// <see cref="ITranslationProvider"/> into the Core
/// <see cref="NotificationFlushService"/> (D5), then re-schedules the next
/// tick at the admin-set cadence (D6). <see cref="Program"/> seeds the first
/// tick (the <c>EventReminderHandler</c>/<c>AuditPurgeHandler</c>
/// <c>Program.cs</c> precedent).
/// </summary>
public static class NotificationFlushHandler
{
    public static async Task<IEnumerable<object>> Handle(
        NotificationFlushTick tick,
        IDocumentStore store,
        IUserInfoService userInfo,
        ILocalizationService localization,
        ITranslationProvider translationProvider,
        IMailerStage mailer,
        ILifecycleClock? clock = null)   // optional now-injection (the EventReminder shape)
    {
        await NotificationFlushService.FlushDeferredAsync(
            store,
            DateTimeOffset.UtcNow,
            mailer,
            userInfo,
            localization,
            translationProvider);

        // Self-reschedule at the admin-set cadence (D6) — read from the
        // LocaleSettings singleton (the ADR 0050/0077/0105 shape), floors to
        // 60 minutes on a missing row (C-M20·6). The TimeoutMessage's delay is
        // NOT baked into the type (unlike EventReminderTick's fixed 1 day) —
        // the cadence is a runtime admin knob, so the next tick is scheduled
        // with an explicit DelayedFor(cadence).
        var cadence = await ResolveCadenceMinutesAsync(store);   // read seam, floors to 60
        return new[] { NotificationFlushTick.DelayedFor(TimeSpan.FromMinutes(cadence)) };
    }
}

/// <summary>
/// M20 (ADR 0121, D5) — the recurring message shape for the NotificationFlush
/// job. Unlike <see cref="EventReminderTick"/> (a fixed 1-day baked-in delay),
/// the flush cadence is the **admin-set** <see cref="LocaleSettings.QuietCheckMinutes"/>
/// (D6) — so this tick carries the delay at schedule time (a
/// <c>TimeoutMessage</c> with a per-run <c>DelayedFor</c>, the
/// <c>AuditPurgeTick</c> "delay is a schedule-time value" shape).
/// </summary>
public sealed record NotificationFlushTick : Wolverine.TimeoutMessage(TimeSpan.FromMinutes(60))
{
    /// <summary>Schedule the next flush at the admin-set cadence (D6).</summary>
    public static NotificationFlushTick DelayedFor(TimeSpan cadence) =>
        new() { Delay = cadence };
}
```

> **The GATE-4 pin test name** (U04's deliverable; the §11 block is the
> authoritative pin):
> - `Flush_Cleared_Row_Stages_One_Email_Flips_Deferred` +
>   `Flush_Still_Quiet_Row_Stays_Deferred_Across_Two_Runs` (GATE-4, C-M20·4) —
>   a cleared row stages **exactly one** email (not zero, not two) via the
>   deferred key; a still-quiet row is untouched across two consecutive flush
>   runs.

## 8 — The admin cadence: `LocaleSettings.QuietCheckMinutes` + the write seam + `/admin/quiet` (D6, D8)

The exact `LocaleSettings` additive field (D6) + the exact
`SetQuietCheckMinutesAsync` seam (the single audited write lane) + the
`AdminQuietController` actions (U07) + the admin `kw-l` keys they consume.

**The additive field** (D6 — already pinned in §D6 above; verbatim on the
`LocaleSettings` singleton, ADR 0005 §B):

```csharp
// In LocaleSettings.cs (U04), alongside IsSignupOpen / NotifyAdminsOnSignup:

/// <summary>
/// M20 (ADR 0121, D6) — how often the §6.4 notification-flush job re-checks
/// the held (deferred) notification emails, in **minutes**. An *additive*
/// field on the singleton (ADR 0004 §B.1), the same shape as
/// <see cref="IsSignupOpen"/> / <see cref="NotifyAdminsOnSignup"/>. Defaults
/// to <c>60</c>; a missing settings row floors to <c>60</c> (C-M20·6). A
/// GlobalAdmin tightens it (e.g. <c>15</c>) or loosens it (e.g. <c>360</c>)
/// on <c>/admin/quiet</c> (D8).
/// </summary>
public int QuietCheckMinutes { get; set; } = 60;
```

**The read + single audited write seam** (D6 — the ADR 0050
`IsSignupOpenAsync` / `SetSignupOpenAsync` shape verbatim). U04 adds these to
the Core Localization service (the same `LocaleSettings` singleton the
`GetDefaultTimezoneAsync` read seam already lives on):

```csharp
// In ILocalizationService.cs / LocalizationService.cs (U04), the ADR 0050
// IsSignupOpenAsync/SetSignupOpenAsync shape verbatim:

/// <summary>
/// M20 (ADR 0121, D6) — the quiet-check cadence READ (no audit row — the
/// <c>GetDefaultTimezoneAsync</c> plain-read shape). Floors to <c>60</c>
/// minutes on a missing/unset row (C-M20·6 — a missing settings row reads as
/// the default, never throws).
/// </summary>
Task<int> GetQuietCheckMinutesAsync();

/// <summary>
/// M20 (ADR 0121, D6) — the quiet-check cadence WRITE: the **single audited
/// write lane** (the exact <c>SetSignupOpenAsync</c> shape — load-or-create
/// the singleton, set <c>QuietCheckMinutes</c>, store exactly one
/// <c>AccessAudit</c> row <c>Via = Admin</c> in the same session,
/// C-M20·6). Validates the value (a floor of <c>5</c> minutes and a ceiling of
/// <c>1440</c>, so an admin cannot set a zero/negative or absurd cadence —
/// <c>ArgumentOutOfRangeException</c> thrown **before** any write → no audit
/// row for the blocked attempt, the <c>RemoveLanguageAsync</c> M·7 pin shape).
/// The Web boundary owns the GlobalAdmin standing check (the Core seam does not
/// re-check <c>User</c> — the ADR 0019/0020 split).
/// </summary>
Task SetQuietCheckMinutesAsync(int minutes, string adminSubjectId);
```

**The `AdminQuietController` actions** (D8 — U07, mirroring
`AdminSignupController` / `AdminTimezoneController` exactly). A dedicated
controller: the `AdminController` constructor is pinned by two Web-layer test
harnesses, so a new dependency there would break them (the ADR 0050
dedicated-controller rationale):

```csharp
using Kumunita.Core.Localization;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// M20 (ADR 0121, D8) — the <c>/admin/quiet</c> surface: the GlobalAdmin's
/// control plane over the §6.4 notification-flush cadence (D6). Mirrors
/// <see cref="AdminSignupController"/> (ADR 0050) +
/// <see cref="AdminTimezoneController"/> (ADR 0019) exactly:
/// <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/>-gated, a thin
/// read/set over the **one** matching <see cref="ILocalizationService"/> seam
/// pair (<see cref="ILocalizationService.GetQuietCheckMinutesAsync"/> /
/// <see cref="ILocalizationService.SetQuietCheckMinutesAsync"/>), and the
/// audit row is the **service's** (exactly one <c>AccessAudit</c>,
/// <c>Via = Admin</c> — the C-M20·6 single + audited write lane). A dedicated
/// controller: the <c>AdminController</c> constructor is pinned by two
/// Web-layer test harnesses, so a new dependency there would break them (the
/// ADR 0050 dedicated-controller rationale).
/// </summary>
[Route("admin/quiet")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminQuietController(ILocalizationService localization) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /admin/quiet</c> — the cadence control plane. Seeds the form with
    /// the current cadence (<see cref="ILocalizationService.GetQuietCheckMinutesAsync"/>
    /// — the C-M20·6 floor: a missing row renders the default <c>60</c>, not an
    /// error).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var minutes = await localization.GetQuietCheckMinutesAsync();
        return View(new QuietAdminViewModel { Minutes = minutes });
    }

    /// <summary>
    /// <c>POST /admin/quiet</c> — sets the cadence. Delegates to
    /// <see cref="ILocalizationService.SetQuietCheckMinutesAsync"/> (the single
    /// audited write lane — exactly one <c>AccessAudit</c> row, <c>Via =
    /// Admin</c>, C-M20·6). Success → a surfaced <c>TempData["info"]</c>
    /// (<c>admin.quiet.flash_saved</c>, the U06 key) + redirect (the change is
    /// live on the very next flush tick — data, not config, D6).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int minutes)
    {
        // Validation (5..1440) is the SEAM's (the ADR 0050 split: the Web
        // delegates, the Core seam validates + appends the audit row). A
        // rejected value throws before the write → the action surfaces the
        // validation-error shape (the ADR 0050 Save shape).
        try
        {
            await localization.SetQuietCheckMinutesAsync(
                minutes, ActorId(User) ?? string.Empty);
        }
        catch (ArgumentOutOfRangeException)
        {
            ModelState.AddModelError("Minutes", "The cadence must be between 5 and 1440 minutes.");
            return RedirectToAction(nameof(Index));
        }

        TempData["info"] = "Quiet-time cadence saved.";   // the U06 admin.quiet.flash_saved key
        return RedirectToAction(nameof(Index));
    }
}
```

> **The gate (D8, C-M20·6):** the controller does **not** `IDocumentSession`
> — it delegates the write to `SetQuietCheckMinutesAsync` (U04's single audited
> lane). The GlobalAdmin standing is enforced by
> `[Authorize(Roles = Roles.GlobalAdmin)]` at the boundary (the Web owns the
> standing check; the Core seam does not re-check `User` — the ADR 0050 split).
> The `/admin` index affordance is **hidden-not-disabled**: the
> `admin.quiet.title` link renders **only** for GlobalAdmin actors (a
> non-GlobalAdmin actor's `/admin` index renders no quiet link — the ADR 0050
> "affordances hidden, not just gated" rule).
>
> **The `kw-l` keys the surface consumes (U06's — §10 is the authoritative
> list):** `admin.quiet.title` (the `/admin` index nav item + page title),
> `admin.quiet.cadence_label` (the "Re-check held notifications every (minutes)"
> label), `admin.quiet.save` (the save button), `admin.quiet.flash_saved` (the
> `TempData["info"]` after a save). **U07 consumes these; it adds none** (the
> closed set is authored in U06).

## 9 — Seams & contracts (Part 2) (exact C#)

The exact C# for U02 (the `NotificationQuietSchedule` doc + the evaluator + the
two owner-scope seams), U03 (the `Notification.EmailDeferred` field + the
`EmitAsync` gate), U04 (the `NotificationFlushService` + the `LocaleSettings`
field + the cadence read/write seams), U05 (the Web handler/tick), U06 (the
`/settings/quiet` section), and U07 (the `AdminQuietController`). **This is the
section a later unit reads to implement; it is the authoritative C#** (the
prose sections above are the explanation).

### 9.1 The `NotificationQuietSchedule` doc + `QuietScheduleMode` enum (U02)

See §4 (the exact C# is verbatim above — the `QuietScheduleMode` enum
(`Blocked = 0`, `Allowed = 1`), the `NotificationQuietSchedule` document
(`RecipientId`, `Enabled`, `Mode`, `Hours`, `DaysOfWeek`, `Updated`), and the
`M6DocTypes` `Notifications.NotificationQuietSchedule` registration pinned to
`RecipientId`).

### 9.2 The pure `QuietScheduleEvaluator` + the owner-scope seams (U02)

See §5 (the exact `QuietScheduleEvaluator.IsQuietNow` is verbatim above — the
floor short-circuit, the ADR 0019 wall-clock-first conversion, the empty-axis =
"all" reading, the `Blocked`/`Allowed` polarity; the two
`NotificationService` ADDs `GetQuietScheduleAsync` + `SetQuietScheduleAsync`,
owner-scope, **no** `AccessAudit` row; the two pinned GATE-2 / GATE-3 test
names).

### 9.3 The D4 gate + the `QuietNowForAsync` helper (U03)

See §5 (the `QuietNowForAsync` helper is verbatim above — the D2 read seam +
the ADR 0019 zone chain + the pure evaluator) and §6 (the exact
`Notification.EmailDeferred` additive field + the exact `EmitAsync` gate block
inserted after the (4a) `event.reminder` carve-out, before the (5) email-nudge;
the deferred idempotency-key form
`notification:{kind}:{source-id}:deferred`; the GATE-1 pin test name).

### 9.4 The `NotificationFlushService` + the `LocaleSettings.QuietCheckMinutes` field + the cadence seams (U04)

See §7 (the `NotificationFlushService.FlushDeferredAsync` signature is verbatim
above — the bounded load, the re-resolve + re-evaluate, the release-via-frozen-
`IMailerStage` + flag-flip, the once-per-run commit; the GATE-4 pin test
names) and §8 (the exact `LocaleSettings.QuietCheckMinutes` additive field +
the `GetQuietCheckMinutesAsync` / `SetQuietCheckMinutesAsync` seams, the ADR
0050 shape, the C-M20·6 floor + the single `AccessAudit` row).

### 9.5 The Web `NotificationFlushHandler` + `NotificationFlushTick` (U05)

See §7 (the `NotificationFlushHandler.Handle` + the `NotificationFlushTick`
shape is verbatim above — the thin adapter that injects the live
`IDocumentStore` + frozen `IMailerStage` + `ILocalizationService` +
`ITranslationProvider` into the Core service, then re-schedules the next tick
at the admin-set cadence; the `Program.cs` first-tick seed; the `DelayedFor`
cadence shape distinct from `EventReminderTick`'s baked-in 1-day delay).

### 9.6 The `/settings/quiet` resident section (U06)

See §D7 (the 5th section on `LocaleController`, mirroring the ADR 0019
time-zone section exactly — `GET`/`POST /settings/quiet`, owner-scope, no
audit row, folded into the shared `LocaleSettingsViewModel` via `BuildModel()`,
the `_SettingsTabs` "Quiet hours" tab) and §10 (the resident `kw-l` keys the
section consumes). **U06 authors the COMPLETE M20 `kw-l` key set (resident +
admin)** (the locked §10 table); U07 consumes its admin keys and adds none.

### 9.7 The `AdminQuietController` actions (U07)

See §8 (the exact controller C# is verbatim above — the
`[Route("admin/quiet")]` + `[Authorize(Roles = Roles.GlobalAdmin)]` surface,
the thin `Index`/`Save` pair over `GetQuietCheckMinutesAsync` /
`SetQuietCheckMinutesAsync`, the hidden-not-disabled `/admin` index
affordance, the four admin `kw-l` keys it consumes).

## 10 — `kw-l` key list (the closed key set × en/de/fr/da)

The exact closed key set from the register (the §"closed `kw-l` key set"
table). Added to **all four** of the closed dictionaries in
`src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — `EnValues` (the
`en` floor, the canonical source text), `DeValues`, `FrValues`, `DaValues`
(the ADR 0005 / 0015 `en`-floor / per-key shape: the seeder materializes each
key's `en` row on first boot; the non-`en` rows are the localized text).
**U06 authors the COMPLETE set (resident + admin); U06/U07 consume.**

| # | Key | Surface | One-line description |
|---|---|---|---|
| 1 | `settings.quiet.title` | `/settings/quiet` | Section title (resident). |
| 2 | `settings.quiet.description` | `/settings/quiet` | Helper text under the title (resident). |
| 3 | `settings.quiet.enabled` | `/settings/quiet` | The master on/off label (resident). |
| 4 | `settings.quiet.mode_label` | `/settings/quiet` | "When should notification emails be held?" (resident). |
| 5 | `settings.quiet.mode_blocked` | `/settings/quiet` | "Held during the selected hours & days" (resident). |
| 6 | `settings.quiet.mode_allowed` | `/settings/quiet` | "Held except the selected hours & days" (resident). |
| 7 | `settings.quiet.hours_label` | `/settings/quiet` | "Hours of day" picker label (resident). |
| 8 | `settings.quiet.days_label` | `/settings/quiet` | "Days of week" picker label (resident). |
| 9 | `settings.quiet.save` | `/settings/quiet` | The save/clear button (resident). |
| 10 | `settings.quiet.flash_saved` | `/settings/quiet` | "Quiet hours saved" flash (resident). |
| 11 | `settings.quiet.flash_cleared` | `/settings/quiet` | "Quiet hours cleared" flash (resident). |
| 12 | `admin.quiet.title` | `/admin/quiet` | Page title, `/admin/quiet` (admin). |
| 13 | `admin.quiet.cadence_label` | `/admin/quiet` | "Re-check held notifications every (minutes)" (admin). |
| 14 | `admin.quiet.save` | `/admin/quiet` | The save button (admin). |
| 15 | `admin.quiet.flash_saved` | `/admin/quiet` | "Quiet-time cadence saved" flash (admin). |

**The closed set is 15 keys × 4 languages = 60 strings.** The
`KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` enforce the
4-language set + non-empty values; the ADR 0015 / 0052 warm-boot backfill seeds
them idempotently. U06 authors them; U06/U07 **consume** them (U07 does not
add a key).

## 11 — §gate (the six acceptance tests)

M20 is **done** when, and only when, all six of these are true (each is a
pinned test in the named unit's deliverables). These six gates are the
milestone's acceptance criterion; a unit that does not land its pinned test is
not done, and the milestone is not done until all six are green.

- **GATE-1 — Quiet defers the email, never the inbox.** `EmitAsync` for a
  quiet recipient stores the `Notification` row (D7), sets `EmailDeferred =
  true`, and stages **no** email; the recipient's inbox shows the row
  immediately. *(C-M20·1.)* Pinned test name:
  `EmitAsync_When_Quiet_Stores_Row_Marks_Deferred_Stages_No_Email` (U03).
- **GATE-2 — The verdict is timezone-aware.** The same schedule + the same
  instant yields a **different** quiet verdict in the recipient's UTC+2 zone
  than in UTC (ADR 0019 effective zone). *(C-M20·2.)* Pinned test name:
  `IsQuietNow_Verdict_Differs_Between_UTC2_And_UTC` (U02).
- **GATE-3 — Floor = never quiet.** A recipient with no
  `NotificationQuietSchedule` row is never quiet; their emit path is
  byte-identical to pre-M20. *(C-M20·3.)* Pinned test name:
  `IsQuietNow_Missing_Schedule_Is_Never_Quiet` (U02).
- **GATE-4 — Deferral is never loss + idempotent.** A deferred email stages
  **exactly once** when it clears (the deferred idempotency key); a still-quiet
  row stays deferred across two consecutive flush runs. *(C-M20·4.)* Pinned
  test names: `Flush_Cleared_Row_Stages_One_Email_Flips_Deferred` +
  `Flush_Still_Quiet_Row_Stays_Deferred_Across_Two_Runs` (U04).
- **GATE-5 — Zero new authorization surface.** The claim-shape / seam-surface
  pin passes: no new `AccessAction` / `AccessVia` / `Decide()` branch /
  adapter; the frozen `NotificationService` + `IMailerStage` seams are the
  only touchpoints; `ClaimTypes.All` is unchanged. *(C-M20·5.)* Pinned test
  name: the claim-shape / seam-surface pin (the U02/U03 assertion that
  `ClaimTypes.All` is unchanged and no `AccessVia`/`AccessAction` member is
  added).
- **GATE-6 — The cadence floors to the default + is GlobalAdmin-only.** A
  missing `LocaleSettings` row floors to `60`; a non-GlobalAdmin write is
  refused; the GlobalAdmin write appends exactly one `AccessAudit` row
  (`Via = Admin`). *(C-M20·6.)* Pinned test name:
  `SetQuietCheckMinutes_Stores_Singleton_And_One_Audit_Row` (U04) + the
  GlobalAdmin-gate Web pin (U07).

## 12 — §drift-guard

**A unit stops (does not improvise) when it hits any of:**

- A D# it needs is not in the [PROPOSED] set, or two D#s contradict — **stop,
  report to the user**; do not pick one silently. (A D# is locked by U00; a
  post-U00 D# change is a design-doc §1.a amendment + a handoff-note line, and
  only U00 makes it.)
- A unit needs *both* test assemblies green to exit — **it is too big**; split
  it per the Atomicity contract rather than running both.
- A deliverable would require a **new `IAuthorizationService` signature, a new
  `Decide()` branch, a new `AccessVia`/`AccessAction`, or a relational
  standing table** — that violates C-M20·5; stop and report (the whole point of
  M20 is zero new authorization surface — the quiet verdict is a pure function,
  not an authorization decision).
- The quiet gate is about to **suppress or drop the inbox row** (return `null`
  or skip the `session.Store`) — that violates C-M20·1 / ADR 0076 D7; stop and
  report (M20 defers the *email* only, never the inbox).
- A user-visible string that is **not** already in `KnownTranslationKeys` for
  all four languages is about to be rendered — add it to U06's closed set first;
  do not inline a string (the parity pin will fail, and that is the point).
- A unit other than **U01** or **U08** is about to touch `Milestones.cs` or
  `MilestonesTests.cs` — stop; only the two flip units own the roadmap.
- The flush job is about to **re-run `EmitAsync`** (or stage a still-quiet
  email) — that violates D5 / C-M20·4; stop and report (the flush only *stages
  the held email* for rows whose quiet has lifted).

**The handoff-notes file is the cross-unit memory.** Every unit reads the
`## U##` sections before it and appends its own after; a unit does not
re-derive what an earlier unit already settled (a D# amendment, the key set, a
seam shape, the deferred idempotency-key form).

**The register is the map, not the code.** If a unit is tempted to "just
suppress the row in quiet" or "add a `Decide()` branch for quiet," that is the
§drift-guard firing — M20's value is precisely that it does *not*: it defers
the email, keeps the inbox durable, adds zero authorization surface, and
respects the recipient's own time zone.

## 13 — §deferred (the D9 lanes)

The five deferred lanes verbatim (the register's D9), each with a one-line
"why it is deferred":

1. **Per-kind quiet** (hold *some* M6 kinds but not others) — deferred because
   M20's one-schedule-governs-all is the lean default; per-kind quiet rides the
   **existing** `NotificationPreference.KindsEnabled` machinery (ADR 0076/0084)
   + the D4 gate rather than a new per-kind schedule surface (the §drift-guard's
   "a per-kind schedule in M20" pin). A follow-up lane with its own ADR.
2. **A platform-wide maintenance window** (the admin sets a site-wide "quiet at
   3 a.m." for everyone) — deferred because M20 is **per-resident** only (D2);
   the global window is a *different policy surface* (a `LocaleSettings`
   additive field + a gate in `EmitAsync` that would suppress the *whole site* —
   the §drift-guard's "a platform-wide maintenance window" pin). A follow-up
   lane with its own ADR.
3. **Inbox deferral** (hiding the *inbox row*, not just the email, during
   quiet) — deferred because M20 defers **only the email** (D1, C-M20·1 — the
   inbox is always durable, ADR 0076 D7); a "don't even show me the row until
   morning" re-opens the D7 inbox-as-durable-record guarantee (the §drift-guard's
   "suppress or drop the inbox row" pin). A follow-up lane with its own ADR.
4. **Quiet for the *sender's* timezone, or a shared household schedule** —
   deferred because M20 is strictly the *recipient's* own zone/schedule (D3);
   sender-zone or shared/household quiet is a larger design (a new resolution
   source + a shared-schedule surface — the §drift-guard's "the recipient's own
   zone" pin). A follow-up lane with its own ADR.
5. **Delivery-time prediction / "will arrive at ~7 a.m." UX** — deferred because
   M20 delivers when the next check runs after quiet lifts (the admin-set
   cadence, D6); a "next delivery time" estimate in the UI is a future UX lane
   (a new read surface + a new `kw-l` namespace — the §drift-guard's "add it to
   U06's closed set first" pin). A follow-up lane with its own ADR.
