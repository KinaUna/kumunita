---
id: o04-prompt
title: "Prompt: IMPROVE-UX O04 — mail alarms also reach the GlobalAdmins' inbox"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/dev/testing-and-ci.md
  - docs/dev/razor-verification.md
lane: improve-ux
updated: 2026-11-12
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **O04** — when the T4 mail-alarm gate fires, the
> GlobalAdmins get an **inbox notification** (`ops.alarm` kind) linking to
> `/admin/status`, in addition to the existing e-mail alert. O04 is the
> **second leg of D8** (O01 = the read seam, O02 = the page, O03 = the
> dashboard notice, O04 = the inbox alarm). **O04 depends on O01** (the
> seam it reads — `IInstanceStatusService`) and O02 (the page it links
> to). **Who is it for:** the next coding agent (fresh context). **When
> to read the rest:** when starting O04 (O03 has shipped; the page + the
> notice exist, the inbox alarm does not).

# Fresh-context prompt: IMPROVE-UX O04

You are continuing the Kumunita repository on `development` (HEAD
`997b80c9`, **O03 just closed** — the dashboard notice). Before it:
**O01** delivered the Core read seam (`IInstanceStatusService` → the
`InstanceStatus` POCO + the pure `ConfigWarningsPolicy`) in
`src/Kumunita.Core/InstanceStatus/`; **O02** rendered that POCO as the
GlobalAdmin-gated `/admin/status` page; **O03** added the
GlobalAdmin-gated dashboard notice. O04 is the **fourth and last
visible piece of D8**: when the T4 mail-alarm gate is tripped
(`MailAlarmsService.EvaluateAsync` returns the `AlertEvent`), the
GlobalAdmins also get an **inbox notification** — so an operator is not
relying solely on e-mail (which may be exactly what failed — the E10
concern) to learn about trouble. **O04 depends on O01 (the seam it
reads) and O02 (the page it links to).** The plan card:

> O04 — Mail alarms also emit an inbox notification (`ops.alarm`) to
> GlobalAdmins, link `/admin/status`; tests.
> Read first: `MailAlarmsService.cs`, `SideEffects/SupportEmailAlertSink.cs`.
> **Depends:** O01. **Done when: alarm → inbox row.**

The problem it fixes (E10 / D8): today a mail-alarm breach reaches the
operator **only** by e-mail (`SupportEmailAlertSink` → `ISmtpSender` →
the `Community:SupportEmail` address). But SMTP may be exactly what
failed (the E10 / D8 concern: "SMTP may be what failed, so email is
unreliable"). O03 made a red instance announce itself on the dashboard;
O04 makes the mail-alarm breach also land in the GlobalAdmins' inbox —
the **second channel** the D8 decision promises.

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** —
  the doctrine (the ratchet, the comment rule, the don't-pause-mid-task
  rule).
- **`plan-improve-ux.md`** (this lane) — the **O04 card** + the D8 decision
  row + the two Operator journeys ("First boot to healthy", "SMTP breaks")
  + DoD.
- **`src/Kumunita.Core/Notifications/MailAlarmsService.cs`** — the T4
  gate: `EvaluateAsync(store, alertSink, now, ct)` reads the window's
  bounce/complaint/dead-letter counts (ONE read session), calls
  `MailAlarmThresholdPolicy.Build(bounces, complaints, deadLetters)` →
  `AlertEvent?` (null = under threshold; non-null = breached). When
  breached, it calls `alertSink.ReportAsync(alert, ct)` and returns
  `true`. **O04's hook point.** Note the **best-effort at the boundary**
  contract: a `ReportAsync` failure is swallowed, never rethrown.
- **`src/Kumunita.Core/Notifications/MailAlarmThresholdPolicy.cs`** —
  the pure `Build` method: `AlertEvent(Kind: "mail.bounce-alarm",
  Subject: "SuppressedAddress ingest: {total} new bounce/complaint rows
  in {windowHours}h (threshold {alertThreshold})", Detail: "bounce:
  {bounces}, complaint: {complaints}; EmailDeadLetter count:
  {deadLetters}", Attempts: null)`. **No PII** — counts + reason
  breakdown only; the suppressed e-mails stay in the DB. The
  `AlertEvent.Subject` is a good notification body (one line, no PII,
  the operator knows what tripped).
- **`src/Kumunita.Web/SideEffects/MailAlarmsHandler.cs`** — the Wolverine
  tick handler: `Handle(MailAlarmsTick tick, IDocumentStore store,
  IAlertSink alertSink)` calls `MailAlarmsService.EvaluateAsync(store,
  alertSink, UtcNow)` and self-schedules the next tick (1 h). **O04's
  emission point** (the Web layer, DI access, the best-effort
  precedent). Note: `EvaluateAsync` returns `bool` (true = alarm fired)
  but does **not** expose the `AlertEvent`. O04 needs the `AlertEvent`
  for the notification body — see the work below.
- **`src/Kumunita.Web/SideEffects/SupportEmailAlertSink.cs`** — the
  existing `IAlertSink` (e-mail leg): composes an `OutboxEmail` from
  the `AlertEvent` (subject = `"Kumunita alert: {Kind} — {Subject}"`,
  body = the detail + attempts), sends via `ISmtpSender`, swallows
  failures. **The precedent for the notification leg's best-effort
  shape.** Note: it is **kind-agnostic** (does not switch on
  `AlertEvent.Kind`) — F11's `email.dead-letter` and F18's
  `mail.bounce-alarm` both ride it.
- **`src/Kumunita.Core/Identity/IdentityService.cs`
  `RequestStorageQuotaIncreaseAsync`** (IMPROVE-UX A14) — the
  **GlobalAdmin fan-out + best-effort notification** precedent O04
  mirrors exactly: `GetUsersInRoleAsync(GlobalAdmin)` → one
  `INotificationEmitter.EmitAsync` per admin →
  `idempotencyKey = "notification:{kind}:{subjectId}:{adminId}"` →
  `linkPath` → swallow + log on failure. **The shape O04 follows.**
- **`src/Kumunita.Core/Notifications/NotificationKinds.cs`** — the
  closed kind set + `Known` (the settings-toggle vocabulary). The
  admin-lane kinds (`AccountSignup`, `AccountVerified`, `StorageRequest`)
  are appended last (the ADR 0077 / A14 precedent — the recipient is a
  GlobalAdmin, not a resident). **O04's new kind joins this group.**
  Note: `Known` is a **closed set** (the settings page renders it);
  adding a kind to `Known` adds a toggle row — O04's `ops.alarm` is an
  **admin-lane** kind (like `StorageRequest`), so it joins the
  admin-lane cluster at the end of `Known`.
- **`src/Kumunita.Web/Program.cs` ~398-412** — the `IAlertSink`
  registration: `AddTransient<IAlertSink, SupportEmailAlertSink>()`
  (gated on `Community:SupportEmail` being set; the `NoopAlertSink`
  default from Core DI is the off-by-default floor). **O04 does NOT
  change this registration** (the e-mail leg is unchanged; the inbox
  leg is a new emission in the handler, not a new `IAlertSink`).
- **`src/Kumunita.Core/Notifications/INotificationEmitter.cs`** — the
  emission contract: `EmitAsync(session, recipientId, kind,
  idempotencyKey, body, targetId?, linkPath?, acceptPath?, declinePath?,
  ct)`. O04 uses the **full overload** (with `linkPath`).
- **`docs/dev/testing-and-ci.md`** — the xunit.v3 in-process runner
  (`dotnet exec …/Kumunita.Web.Tests.dll -class "…"`) and the Web test
  idiom.
- **`git status`** + the branch (O03's `997b80c9` must be intact; you
  are on `development`).

**The one thing to get right.** O04 is a **thin, best-effort inbox leg**
over the **existing** T4 gate. Three invariants are load-bearing:

1. **The T4 gate is unchanged.** O04 does **not** change the threshold,
   the window, the read session, or the `MailAlarmThresholdPolicy.Build`
   decision. It adds a **second channel** (the inbox notification)
   alongside the existing e-mail channel. The gate fires the same way;
   the breach is the same; only the *delivery* gains a leg.

2. **No PII in the notification body.** The `AlertEvent.Subject`
   (e.g. `"SuppressedAddress ingest: 7 new bounce/complaint rows in
   24h (threshold 5)"`) is counts + a threshold — **no e-mail
   addresses, no resident names, no connection strings**. The
   suppressed e-mails stay in the DB (the `SuppressedAddress`
   document). The notification body is the `AlertEvent.Subject`
   (or a short variant) — the operator sees *what* tripped and *how
   many*, not *who bounced*. The `linkPath` is `/admin/status` (the
   O02 page, where the full row set is rendered).

3. **Best-effort at the boundary (the A14 / F12 precedent).** A
   notification-emission failure (a missing emitter, a fresh instance
   with no GlobalAdmin, a session error) is **swallowed + logged**,
   never rethrown. The e-mail alert (the `SupportEmailAlertSink` leg)
   is the existing channel; the inbox leg is additive. The T4 gate's
   return value and the self-scheduling tick are **unchanged** — a
   failed inbox emission does not break the tick or re-fire the e-mail.

**⚠️ Do NOT touch O01's seam or its tests.** O04 is a Web-layer
emission (the handler) + one new `NotificationKinds` constant (Core, a
one-line add) + one line in `Known`. It must not change
`IInstanceStatusService`, `InstanceStatus`, `ConfigWarningsPolicy`, the
`MailAlarmThresholdPolicy`, the `MailAlarmsService` read session, or
any O01/O02/O03 file. It must not change the `AddInstanceStatus()`
registration or the `IAlertSink` registration.

The **work**:

1. **The kind** — add `OpsAlarm = "ops.alarm"` to
   `NotificationKinds.cs` (Core, the admin-lane cluster, with a
   `<summary>` following the A14 `StorageRequest` doc idiom: the
   recipient is a GlobalAdmin, the emitter is
   `MailAlarmsHandler.Handle`, the idempotency key is
   `notification:ops.alarm:{fireId}:{adminId}`, the `linkPath` is
   `/admin/status`). Add it to the **`Known`** list (the admin-lane
   cluster, after `StorageRequest`). **Do not** add it to
   `OptInKinds` (admin-lane kinds are always-on, not
   resident-toggleable).
   - `// TODO ❓` whether the kind should be `ops.alarm` or
     `mail.bounce-alarm` (the `AlertEvent.Kind` is already
     `"mail.bounce-alarm"`). The plan card says `ops.alarm` — use
     that. If the `AlertEvent.Kind` and the `NotificationKinds`
     constant diverge, that is fine (the `AlertEvent.Kind` is the
     e-mail subject's tag; the `NotificationKinds` constant is the
     inbox row's tag — the two channels are distinct).

2. **Expose the `AlertEvent` to the handler.** `MailAlarmsService.EvaluateAsync`
   currently returns `bool` (true = alarm fired, false = not). The
   handler needs the `AlertEvent` for the notification body. **Two
   options** (pick the smaller):
   - **(a) Change `EvaluateAsync` to return `AlertEvent?`** (null =
     under threshold / no breach; non-null = breached + the composed
     event). Update the one production caller
     (`MailAlarmsHandler.Handle` — the only call site in `src/`) and
     the Core test
     `tests/Kumunita.Core.Tests/MailAlarmsServiceTests.cs` (3 call
     sites: the `Assert.False(result)` / `Assert.True(result)` pins
     become `Assert.Null(result)` / `Assert.NotNull(result)`; the
     sink-count + `sink.LastAlert` assertions are unchanged). The
     internal logic is unchanged (the `alertSink.ReportAsync` call
     still happens inside `EvaluateAsync` before the return). This is
     the **cleaner** option — the handler gets the `AlertEvent` for
     free.
   - **(b) Keep the `bool` return** and have the handler re-compute
     the `AlertEvent` from the counts (a second read, or a
     re-computation from the `InstanceStatus` POCO). This is the
     **larger** option — a second DB read or a re-computation.
   - **Recommend (a).** It is a one-line signature change in Core +
     one-line update in the handler + the Core test update. The
     `AlertEvent` is already composed inside `EvaluateAsync` —
     returning it instead of a `bool` is a natural shape.

3. **The emission** — in `MailAlarmsHandler.Handle` (the Web-layer
   handler, the DI-access point): after `EvaluateAsync` returns the
   `AlertEvent` (non-null = the alarm fired), emit a `Notification` to
   each GlobalAdmin:
   - **List the GlobalAdmins** (the `IIdentityService
     .ListGlobalAdminsAsync()` read — the A14 / ADR 0077 precedent; a
     no-op for a fresh instance with no GlobalAdmin).
   - **Emit one notification per admin** (the A14 fan-out shape):
     `INotificationEmitter.EmitAsync(session, recipientId: adminId,
     kind: NotificationKinds.OpsAlarm,
     idempotencyKey: $"notification:ops.alarm:{DateTimeOffset.UtcNow:yyyyMMddHHmm}:{adminId}",
     body: alert.Subject, linkPath: "/admin/status", ct: ct)`.
     The body is the `AlertEvent.Subject` (the
     `"SuppressedAddress ingest: {total} new bounce/complaint rows in
     {windowHours}h (threshold {alertThreshold})"` — no PII, counts +
     threshold only). The `linkPath` is `/admin/status` (the O02 page —
     the "the page it links to" invariant).
   - **Best-effort** (the A14 / F12 boundary contract): wrap the
     emission in `try/catch`; a failure is swallowed + logged (a
     warning, the `logger.LogWarning` shape), never rethrown. The tick
     still self-schedules; the e-mail leg (the `alertSink.ReportAsync`
     inside `EvaluateAsync`) is unaffected.
   - **No audit row** (a notification is not an access decision — the
     A14 precedent).
   - **The idempotency key** includes a **fire-timestamp** (e.g.
     `yyyyMMddHHmm` — the minute the alarm fired) so that the
     self-scheduling tick (1 h cadence) does not re-emit the same
     breach within the same minute, but a *new* breach in the next
     window *does* emit. The A14 key is
     `notification:{kind}:{subjectId}:{adminId}` (stable per
     resident); O04's is `notification:ops.alarm:{fireMinute}:{adminId}`
     (stable per fire-minute, the "still broken, still loud" re-fire
     semantics — the T4 gate re-fires every tick while breached, so the
     notification re-fires every tick too, but the idempotency key
     dedupes within the same minute).
     - `// TODO ❓` whether the idempotency key should be per-tick
       (1 h) rather than per-minute (the tick cadence is 1 h, so a
       per-minute key is effectively per-tick — a breach that persists
       for 2 h produces 2 notifications, one per tick; a breach that
       persists for 5 min produces 1 notification). The plan card does
       not specify; the T4 "still broken, still loud" semantics
       suggest one-per-tick is correct. Default to **per-minute**
       (simpler, and the 1 h tick makes it effectively per-tick).

4. **The DI wiring** — `MailAlarmsHandler.Handle` currently receives
   `(MailAlarmsTick tick, IDocumentStore store, IAlertSink alertSink)`.
   O04 adds **`INotificationEmitter notifications`** and **`IIdentityService identity`**
   to the parameter list (Wolverine resolves both from DI — they are
   already registered; no new `PackageReference`). The handler's
   signature is the only DI change.

5. **Keys** — the `ops.alarm` kind's **`kw-l` label** (the notification
   settings page renders the kind label from the closed `Known` set;
   the A14 `StorageRequest` has a `notifications.storage_request` kw-l
   key). Add `notifications.ops_alarm` to the four-language
   `Keys/{en,de,fr,da}/notifications.json` files. **C02 four-language
   parity** (`C02_DataAsEmbeddedResourcesTests`) must stay green.
   - en: `"notifications.ops_alarm": "Instance alarm"`
   - de: `"notifications.ops_alarm": "Instanzalarm"`
   - fr: `"notifications.ops_alarm": "Alerte de l'instance"`
   - da: `"notifications.ops_alarm": "Instansalarm"`

**Tests** (Web.Tests; the `MailAlarmsHandler` / `A14`
`RequestStorageQuotaIncreaseAsync` idiom — direct-construction +
NSubstituted seams, no `RazorPage`/`RenderViewToString` harness):

- **the kind is in the closed set**: `NotificationKinds.OpsAlarm`
  equals `"ops.alarm"` and is present in `NotificationKinds.Known`
  (structural pin — the A14 / ADR 0077 admin-lane precedent).
- **the kind is NOT in `OptInKinds`**: admin-lane kinds are always-on,
  not resident-toggleable (the A14 precedent).
- **the handler emits to each GlobalAdmin when the alarm fires**:
  NSubstitute `INotificationEmitter` + `IIdentityService` (returns
  2 admin ids) + a breached `AlertEvent` → the emitter is called
  **twice** (one per admin), each with `kind = "ops.alarm"`,
  `linkPath = "/admin/status"`, and a body containing the
  `AlertEvent.Subject` (the counts, no PII).
- **the handler does NOT emit when the alarm is under threshold**:
  `EvaluateAsync` returns null / false → the emitter is **never**
  called (the `Received(0)` pin).
- **the handler emits to ZERO admins on a fresh instance**:
  `IIdentityService.ListGlobalAdminsAsync()` returns an empty list →
  the emitter is **never** called (the no-op, not a noise path — the
  A14 precedent).
- **best-effort: a failed emission does not break the tick**:
  `INotificationEmitter.EmitAsync` throws → the handler swallows it
  (the `try/catch`), the tick still self-schedules (the
  `MailAlarmsTick` re-yield is present in the return).
- **no PII in the body**: the notification body is the
  `AlertEvent.Subject` (counts + threshold) — assert the body does
  **not** contain any e-mail-address-shaped string (a simple
  `DoesNotContain("@")` or `DoesNotMatch(Regex)`) and does **not**
  contain any of the `ConfigWarning.Reasons` strings (the reasons live
  on the `/admin/status` page, not the notification — the O03 "no
  secret value" invariant).
- **the four-language parity** test stays green with O04's new
  `notifications.ops_alarm` key.

**Scope rules:**

- **The T4 gate is unchanged.** O04 does **not** change the threshold
  (`AlertThreshold = 5`), the window (`WindowHours = 24`), the read
  session, the `MailAlarmThresholdPolicy.Build` decision, or the
  `IAlertSink` e-mail leg. It adds a **second channel** (the inbox
  notification) alongside the existing e-mail channel.
- **Web + one Core line.** The emission is in
  `MailAlarmsHandler.Handle` (Web); the kind is a one-line add to
  `NotificationKinds.cs` (Core); the `Known` list gains one entry.
  **Do not change** `IInstanceStatusService`, `InstanceStatus`,
  `ConfigWarningsPolicy`, `MailAlarmsService`'s read session, the
  `MailAlarmThresholdPolicy`, or any O01/O02/O03 file.
- **No new doc type, migration, or `[DataClass]`** (the **84-count**
  `DataClassCoverageTests` pin must not move).
- **No POST, no form** (a notification is a read + an emit, not a
  write surface).
- **One commit** (the house rule): `feat(ux): mail alarms also notify
  the GlobalAdmins' inbox — the D8 second channel (IMPROVE-UX O04)`.
- **No ADR** unless a decision is genuinely open — D8 is already
  decided in the plan, and O04 adds an inbox leg over an existing gate,
  not a new decision. If O04 settles a design question the ADRs don't
  cover, write or amend an ADR in the same commit; otherwise do not
  fabricate one.
- **New strings** go to en/de/fr/da — **C02 four-language parity**
  must stay green.
- **The ux gate** (`tools/ux/ux-measure.py --check`) must not regress.
- ⚠️ **Known pre-existing reds (not yours):** the full Web suite has a
  small set of pre-existing failures that pre-date this lane (the two
  `SecGate.DataClassCoverageTests` +
  `ImproveHarnessTests.CodeCheck_Gate_Passes` — the IMPROVE-CODE Roslyn
  ratchet + ADR-index drift). Do **not** attribute these to O04 or try
  to fix them here; your O04 suite must be green.

**Before committing:** `dotnet build`, then run the narrowest tests via
the in-process runner (see `testing-and-ci.md`): your new O04
handler/kind tests + the **C02 four-language parity** test + the
**`DataClassCoverageTests`** (confirm the **84-count pin is un**broken —
it must not move) + the O02 `AdminStatusControllerTests` (confirm O04
didn't disturb the page) + the O03 `O03AdminStatusNoticeTests`
(confirm O04 didn't disturb the notice). If a test fails, read the
error, adjust, continue — a failed test is a recoverable step, not a
stopping point.

Then report: the `NotificationKinds` constant you added + its
position in `Known`, the `EvaluateAsync` signature change (if any) +
the handler emission shape (the idempotency key, the body, the
linkPath), the keys you added (en/de/fr/da), the new test(s) + count,
the commit hash, the confirmed-unbroken **84** registered-doc-type count
+ the C02 parity result, and the ux-gate result. Do not commit
unrelated work.
