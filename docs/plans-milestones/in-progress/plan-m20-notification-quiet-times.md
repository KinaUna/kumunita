# M20 — Notification quiet times (the register)

> **This file is the register** — the milestone map, the locked [PROPOSED]
> decision set, the invariants, the FACES, the unit map, the workflow, the
> atomicity contract, and the §drift-guard. It is read first by **every** unit
> agent (U00–U08) before anything else. It is **not** a per-unit plan: each
> unit has its own self-contained file (`m20-uNN.md`) that a ~32K-context agent
> can execute without re-deriving this file.
>
> **M20 is NOT the last milestone.** It is the first of the remaining planned
> horizon — after M20 come **M21 (document management)** and **M22
> (onboarding)**. M20 is currently `StatusPlanned` in `Milestones.All`; there is
> no `StatusNext` today (the `MilestonesTests.No_Milestone_Is_InProgress_After_M19`
> pin asserts exactly that). So M20 has **two** milestone-flip units, a
> difference from M19 (which was the last milestone and had no "open" flip):
> **U01 opens M20** (`StatusPlanned` → `StatusNext`, the pin flips to
> "M20 is the single in-progress milestone") and **U08 closes M20**
> (`StatusNext` → `StatusDone`, **M21** promoted to `StatusNext`, the pin
> re-pinned to "M21 is the single in-progress milestone"). No other unit touches
> `Milestones.cs` or `MilestonesTests`.

## Tiering (three documents, like M18/M19)

| Tier | File | Who owns it | Lifetime |
| --- | --- | --- | --- |
| 1 — Register (this file) | `in-progress/plan-m20-notification-quiet-times.md` | U00 (author) + all units (read) | moves to `done/` at U08 |
| 2 — Per-unit plans | `in-progress/m20-uNN.md` | the `U##` agent | moves to `done/` when the unit is done |
| 3 — Rolling handoff notes | `in-progress/m20-notification-quiet-times-handoff-notes.md` | every agent appends its `## U##` section | moves to `done/` at U08 |

The handoff-notes file is created by **U00** (at runtime, not by this authoring
pass). Every unit appends a short `## U##` section before it moves its own plan
to `done/`.

## Atomicity contract (sized for ~32K context)

Each unit is one self-contained step a fresh agent can complete and hand off:

- **Entry reads:** 4–8 files, named in the unit plan (with a one-line "why"
  each).
- **Deliverables:** ≤ 7 small files, named exactly (paths +, where locked, the
  exact C# / keys the design doc pins).
- **Exit gate:** **one** `dotnet build Kumunita.slnx -c Debug` **plus one**
  `dotnet exec` test assembly — either
  `tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` **or**
  `tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`. Both
  must be green.
- **A unit that needs *both* test assemblies is too big — split it.**
  (Core units exit on Core.Tests; Web units exit on Web.Tests. The flip units —
  U01 and U08 — exit on Web.Tests, because they flip `Milestones.cs`, which is
  pinned by `MilestonesTests` in `Kumunita.Web.Tests`.)
- **No build/test exit for U00** — it is docs-only (design doc + ADR + index
  row). Its exit is "the files exist and the [PROPOSED] set is locked or
  amended in-unit."

> **Test-runner quirk (do not retry the broken paths):** `dotnet test` and VS
> Test Explorer discovery go wrong on this machine (xunit.v3 bridge). The
> reliable exit is the `dotnet build` + `dotnet exec … .dll` pair above.
> `Kumunita.Core.Tests` starts `postgres:18` via Testcontainers (~20 s) and
> leaves Docker containers behind if killed — `docker container prune` to clean
> up.

## Unit map

| Unit | Title | Track | Exit test assembly |
| --- | --- | --- | --- |
| U00 | Design doc + ADR 0121 (the sign-off gate) | docs-only | none (no build/test) |
| U01 | **Open M20** — `Milestones.cs` flip to `StatusNext`, `MilestonesTests` re-pin, README/STATUS parity | Web + docs | `Kumunita.Web.Tests` |
| U02 | `NotificationQuietSchedule` doc + `M6DocTypes` reg + pure timezone-aware `IsQuietNow` evaluator + owner-scope read/write seams | Core | `Kumunita.Core.Tests` |
| U03 | `Notification.EmailDeferred` additive field + the `EmitAsync` quiet gate (defer email, keep inbox) | Core | `Kumunita.Core.Tests` |
| U04 | `NotificationFlushService` (the §6.4 pure flush) + `LocaleSettings.QuietCheckMinutes` additive field | Core | `Kumunita.Core.Tests` |
| U05 | `NotificationFlushHandler`/`Tick` (Web §6.4 adapter) + `Program.cs` first-tick seed + DI wiring | Web | `Kumunita.Web.Tests` |
| U06 | The 5th `/settings/quiet` resident section (D7) + view + **the closed `kw-l` key set** × en/de/fr/da (author) | Web | `Kumunita.Web.Tests` |
| U07 | The `/admin/quiet` cadence surface (D8) — consumes U06's admin keys | Web | `Kumunita.Web.Tests` |
| U08 | **Close M20** — flip `StatusDone`, promote M21 to `StatusNext`, re-pin `MilestonesTests`, README/STATUS/ARCHITECTURE parity, `done/` move | Web + docs | `Kumunita.Web.Tests` |

## Understanding (what M20 is)

**M20 = notification quiet times.** The roadmap line is the scope:
> "per-resident quiet schedules (allowed/blocked hours of day and days of
> week) on the M6 notification lane; admin-set check cadence for pending
> notifications."

M6 (ADR 0076) already ships the two-part notification: the **inbox row**
(a durable, personal record — stored unconditionally, D7) and the **email
nudge** (a best-effort side effect staged via the frozen `IMailerStage`, the
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

Everything else about M20 is *what it is not* — see "What M20 is NOT" below and
the §deferred lanes (D9).

## What M20 is NOT

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

## Decisions (D1–D9) — [PROPOSED], locked or amended by U00

> Each D# carries a `*Forbids:*` tail — the anti-pattern it rules out. U00
> locks these verbatim in the design doc, or amends them **in U00** (recorded
> in the design doc's §1.a + the handoff notes). A later unit may not amend a
> D#; if a D# is wrong once the code starts, that is a §drift-guard stop.

### D1 — Quiet times **defer the email nudge, never the inbox row**

The M20 gate is a *time* gate on the **email half** of the M6 two-part
notification, applied **inside `NotificationService.EmitAsync`** at the point
where the email would be staged (the step after the inbox row is stored and
after the existing D7 email-kind gate). When the recipient's quiet schedule is
active at emit time: the **inbox row is still stored** (D7, unchanged), the
row is marked `EmailDeferred = true`, and the email is **not staged**. When it
is not active: the existing path is unchanged (email staged as today).

*Forbids:* suppressing/dropping the inbox row during quiet (violates ADR 0076
D7), *dropping* the email (it must be **deferred**, not lost — the roadmap's
"pending notifications" implies held-then-delivered), or staging the email
during quiet.

### D2 — The quiet schedule is a **per-resident document**, floor = never quiet

A new document `Kumunita.Core.Notifications.NotificationQuietSchedule` (id =
the recipient's `SubjectId`, the `NotificationPreference` "one row per
recipient" shape — identity pinned to `RecipientId` in `M6DocTypes`, the ADR
0004 §B.1 additive-surface precedent). Shape (locked by U00's design doc §4):

```csharp
public sealed class NotificationQuietSchedule
{
    public string RecipientId { get; set; } = string.Empty;   // document identity (the SubjectId this schedule is FOR)
    public bool Enabled { get; set; } = false;                 // the master on/off; a disabled schedule is "never quiet" (the floor)
    /// <summary>
    /// The schedule's *mode*: <c>Allowed</c> = "quiet EXCEPT these hours/days"
    /// (allow-list: the email flows during the listed windows, held otherwise)
    /// vs <c>Blocked</c> = "quiet DURING these hours/days" (block-list: held
    /// during the listed windows, flows otherwise). The lean default is
    /// <c>Blocked</c> (the intuitive "quiet at night" reading).
    /// </summary>
    public QuietScheduleMode Mode { get; set; } = QuietScheduleMode.Blocked;
    /// <summary>The hours of day (0–23) the mode applies to. Empty = the mode
    /// applies to ALL hours.</summary>
    public int[] Hours { get; set; } = [];
    /// <summary>The days of week (0=Sunday…6=Saturday) the mode applies to.
    /// Empty = the mode applies to ALL days.</summary>
    public int[] DaysOfWeek { get; set; } = [];
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

### D3 — "Is it quiet now?" is a **pure, timezone-aware** evaluator

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

### D4 — The gate lives in **`EmitAsync`**, after the inbox row, before the email

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

### D5 — The pending flush is a **§6.4 durable job** (the `EventReminders` shape)

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

### D6 — The check cadence is an **admin-set additive field** on `LocaleSettings`

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

### D7 — The resident surface is the **5th settings section**, `/settings/quiet`

`LocaleController` (ADR 0080 — the 4-section split: `/settings/language`,
`/settings/timezone`, `/settings/dateformat`, `/settings/email-language`) gains
a **5th section** `/settings/quiet`, mirroring the ADR 0019 **time-zone**
section exactly (both are time-related, per-resident, owner-scope, no-audit-row,
folded into the same `LocaleSettingsViewModel` via the shared `BuildModel()`):
`GET /settings/quiet` (render the resident's schedule + the mode/hours/days
picker, pre-selected) and `POST /settings/quiet` (save/clear, owner-scope —
`KumunitaPrincipal.SubjectId(User)` must equal the actor; a `null` body clears
the schedule = never quiet). The `_SettingsTabs` sub-nav gains a "Quiet hours"
tab. All user-visible strings are `kw-l` keys (the closed set, §8 below, U06
authors them).

*Forbids:* a separate `QuietController` (the ADR 0080 precedent is to fold the
section into `LocaleController`, like the time-zone section), a surface that
lets a resident set *another* resident's schedule (owner-scope), or an
un-audited *auditable* write (it is a personal preference — **no** audit row,
ADR 0019/0020 shape, C-M20·7).

### D8 — The admin cadence surface is **`/admin/quiet`, GlobalAdmin-gated**

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

### D9 — **Deferred lanes** (each a future ADR, not part of M20)

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

## Invariants (C-M20·1 … C-M20·7)

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

## FACES (F1–F5) + the named trade

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

## §gate (acceptance tests, named — U00 locks them in the design doc)

- **GATE-1 — Quiet defers the email, never the inbox.** `EmitAsync` for a
  quiet recipient stores the `Notification` row (D7), sets `EmailDeferred =
  true`, and stages **no** email; the recipient's inbox shows the row
  immediately. *(C-M20·1.)*
- **GATE-2 — The verdict is timezone-aware.** The same schedule + the same
  instant yields a **different** quiet verdict in the recipient's UTC+2 zone
  than in UTC (ADR 0019 effective zone). *(C-M20·2.)*
- **GATE-3 — Floor = never quiet.** A recipient with no
  `NotificationQuietSchedule` row is never quiet; their emit path is
  byte-identical to pre-M20. *(C-M20·3.)*
- **GATE-4 — Deferral is never loss + idempotent.** A deferred email stages
  **exactly once** when it clears (the deferred idempotency key); a still-quiet
  row stays deferred across two consecutive flush runs. *(C-M20·4.)*
- **GATE-5 — Zero new authorization surface.** The claim-shape / seam-surface
  pin passes: no new `AccessAction` / `AccessVia` / `Decide()` branch /
  adapter; the frozen `NotificationService` + `IMailerStage` seams are the
  only touchpoints; `ClaimTypes.All` is unchanged. *(C-M20·5.)*
- **GATE-6 — The cadence floors to the default + is GlobalAdmin-only.** A
  missing `LocaleSettings` row floors to `60`; a non-GlobalAdmin write is
  refused; the GlobalAdmin write appends exactly one `AccessAudit` row
  (`Via = Admin`). *(C-M20·6.)*

## Workflow (every unit, 7 steps)

1. **Read this register** (the Understanding, the [PROPOSED] D# set, the
   invariants, the FACES, the §gate, the §drift-guard).
2. **Read the unit plan** (`m20-uNN.md`) — the Goal, the Entry reads, the
   Deliverables, the Exit.
3. **Read the Entry reads** named in the unit plan (4–8 files, the design-doc
   sections the unit implements, the ADR, the code seams it touches).
4. **Execute** the Deliverables (≤ 7 files; implement the locked C# / keys
   exactly; do not amend a D#).
5. **Run the Exit gate** — one `dotnet build Kumunita.slnx -c Debug` + one
   `dotnet exec` test assembly, both green (U00: no build/test).
6. **Append a `## U##` section** to the handoff notes (5 lines: what was
   delivered, any open question, the next unit's entry point, no new drift).
7. **Move the unit plan** `in-progress/m20-uNN.md` → `done/m20-uNN.md` (flat,
   directly under `done/` — the M13–M19 convention, **not** a `done/m20/`
   subfolder).

## Unit-series rules

- **Order is U00 → U08.** U00 (docs + ADR) must land before any code unit,
  because the design doc's "Seams & contracts (Part 2)" section (§9) pins the
  exact C# U02–U08 implement. U01 opens the milestone (the flip) immediately
  after the design is locked, so the roadmap is accurate from the first code
  unit onward. A later unit may read an earlier unit's code, but never re-derive
  a D#.
- **One write lane per unit.** U02 owns the `NotificationQuietSchedule` doc +
  the `M6DocTypes` registration + the evaluator + the two owner-scope seams.
  U03 owns the `Notification.EmailDeferred` field + the `EmitAsync` gate. U04
  owns the `NotificationFlushService` + the `LocaleSettings` field + the
  cadence write seam. U05 owns the Web handler/tick + `Program.cs` + DI. U06
  owns the `/settings/quiet` section + the **full** `kw-l` set (author). U07
  owns the `/admin/quiet` surface (consumer only). No unit both *reads* and
  *writes* a seam another unit owns.
- **One test-assembly exit per unit.** A Core unit exits on Core.Tests; a Web
  unit exits on Web.Tests. The flip units (U01, U08) exit on Web.Tests (they
  flip `Milestones.cs`, pinned by `MilestonesTests`). If a unit needs both, it
  is too big — split it (see the Atomicity contract).
- **The milestone flips are bracketed by U01 (open) and U08 (close), and ONLY
  they touch `Milestones.cs` / `MilestonesTests`.** U01: `StatusPlanned` →
  `StatusNext` (M20) + replace `No_Milestone_Is_InProgress_After_M19` with
  `M20_Is_The_Single_InProgress_Milestone` + README/STATUS parity. U08:
  `StatusNext` → `StatusDone` (M20) + **promote M21** to `StatusNext` + re-pin
  to `M21_Is_The_Single_InProgress_Milestone` + README/STATUS/ARCHITECTURE
  parity + move all M20 artifacts flat to `done/`. No other unit touches the
  roadmap.
- **`kw-l` parity is authored in U06 and consumed by U06 + U07.** Every new
  user-visible string M20 introduces is a `KnownTranslationKeys` entry present,
  non-empty, in **all four** languages (en/de/fr/da); the
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` pin the
  closure. **U06 authors the COMPLETE M20 key set (resident + admin surfaces)**
  (the locked set below); U07 *consumes* its admin keys and **adds none**.

### The closed `kw-l` key set (D9/D7/D8) — locked by U00, authored by U06

| Key | Purpose (surface) |
| --- | --- |
| `settings.quiet.title` | Section title, `/settings/quiet` (resident) |
| `settings.quiet.description` | Helper text under the title (resident) |
| `settings.quiet.enabled` | The master on/off label (resident) |
| `settings.quiet.mode_label` | "When should notification emails be held?" (resident) |
| `settings.quiet.mode_blocked` | "Held during the selected hours & days" (resident) |
| `settings.quiet.mode_allowed` | "Held except the selected hours & days" (resident) |
| `settings.quiet.hours_label` | "Hours of day" picker label (resident) |
| `settings.quiet.days_label` | "Days of week" picker label (resident) |
| `settings.quiet.save` | The save/clear button (resident) |
| `settings.quiet.flash_saved` | "Quiet hours saved" flash (resident) |
| `settings.quiet.flash_cleared` | "Quiet hours cleared" flash (resident) |
| `admin.quiet.title` | Page title, `/admin/quiet` (admin) |
| `admin.quiet.cadence_label` | "Re-check held notifications every (minutes)" (admin) |
| `admin.quiet.save` | The save button (admin) |
| `admin.quiet.flash_saved` | "Quiet-time cadence saved" flash (admin) |

## §drift-guard

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
- A user-visible string that is **not** already in `KnownTranslationKeys` for all
  four languages is about to be rendered — add it to U06's closed set first; do
  not inline a string (the parity pin will fail, and that is the point).
- A unit other than **U01** or **U08** is about to touch `Milestones.cs` or
  `MilestonesTests.cs` — stop; only the two flip units own the roadmap.
- The flush job is about to **re-run `EmitAsync`** (or stage a still-quiet
  email) — that violates D5 / C-M20·4; stop and report (the flush only *stages
  the held email* for rows whose quiet has lifted).

**The handoff-notes file is the cross-unit memory.** Every unit reads the
`## U##` sections before it and appends its own after; a unit does not re-derive
what an earlier unit already settled (a D# amendment, the key set, a seam shape,
the deferred idempotency key form).

**The register is the map, not the code.** If a unit is tempted to "just
suppress the row in quiet" or "add a `Decide()` branch for quiet," that is the
§drift-guard firing — M20's value is precisely that it does *not*: it defers the
email, keeps the inbox durable, adds zero authorization surface, and respects
the recipient's own time zone.
