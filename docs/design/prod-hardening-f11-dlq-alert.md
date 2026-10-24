# F11 — JOB-02: alert on dead-letter exhaustion — Part-0 design pin

> **Fix:** F11 (report row **JOB-02**) — kind **`code+ops`**.
> **Status:** this is the **Part-0 design pin** (F11.U1). It is **design-only**:
> **no code, no build, no `.cs` touched, no `<PackageReference>`, no migration,
> no `.slnx` change, no build run** (the unit's exit contract). F11.U2 lands
> the `IAlertSink` Core seam + the `AlertEvent` payload + the `NoopAlertSink`
> default binding + the `HandleFault` trigger wiring, F11.U3 the pinned test.
> The close-flip (six members) happens at **F22**, the lane's last unit —
> **not** F11.
> **Why it's a FAIL today:** the durable email path's terminal failure
> (retries over ~24 h exhausted) writes the domain `EmailDeadLetter` row
> (`OutboxEmailHandler.HandleFault`, the `EmailDeadLetterWriter`) and the row
> drives `/health`'s **degraded** gate (OPS §8) — **but no alert fires**. The
> only signal an operator gets is a *poll* of `/health` that returns
> `degraded` *if and when they happen to look*. There is no push, no
> destination, no "a message has been dead for ~24 h" that reaches a human.
> **The F10 dependency (honored):** F11 **depends on F10** (OBS-02, the
> Sentry error tracker). F10 is CODE-COMPLETE — the `Sentry.AspNetCore 6.12.1`
> package + the DSN-gated `UseSentry` registration + the PII scrubber are
> in place, gated on `SENTRY_DSN` (unset by default → Sentry is a **no-op**).
> F11 **reuses the F10 Sentry channel as ONE of its sink bindings** (the
> Sentry-backed `SentryAlertSink`, DSN-gated) — but F11's **load-bearing**
> seam is a **dedicated `IAlertSink` Core interface**, NOT a direct Sentry
> call. That is the pin's central decision (§2). The F10 `SENTRY_DSN`
> owner-ops deferral **still stands**: unset → the Sentry-backed binding is a
> no-op, and the `IAlertSink` default binding (`NoopAlertSink`) is what fires
> (a structured log) — so **F11 closes by code regardless of `SENTRY_DSN`**.

> **Repo doctrine applied (from `AGENTS.md` + ADR 0006 + the F10 precedent):**
> *"Core stays HTTP-free"* (ADR 0006) — so the **seam + payload**
> (`IAlertSink`, `AlertEvent`) are **Core** (the domain event "an alert-worthy
> operational condition occurred" + its one-shot payload), and the **channel**
> (Sentry / SMTP / structured log) is a **Web + owner-ops** concern. This is
> the **exact** `ISmtpSender` Core I/O-seam precedent: Core owns the interface
> (`SmtpSender.cs`), the Web owns the registration + the transport. The
> **default** binding is **off-by-default / no-op** (the F10 DSN-gated
> no-op-when-unset idiom, the F9.U3 flag-off-deferred idiom) — an unconfigured
> instance is a structured log, not a network call. **No `<PackageReference>`
> is added in F11** (F10 already added `Sentry.AspNetCore`; the Sentry-backed
> binding reuses it — pin §6).

---

## 1. Current state (grounded, not assumed)

- **The real dead-letter / fault seam is grounded, not guessed.** The
  terminal-failure hook is `OutboxEmailHandler.HandleFault(Wolverine.Fault<OutboxEmail> fault,
  IDocumentSession session)` in `src/Kumunita.Web/SideEffects/OutboxEmailHandler.cs`.
  Its own XML doc names the chain: "Wolverine publishes a
  `Fault<OutboxEmail>` after the configured retry schedule is exhausted
  (config: `opts.PublishFaultEvents()` in `Program.cs`) … writes the domain
  `EmailDeadLetter` row via the shared Wolverine-free writer". The wiring is
  `opts.PublishFaultEvents()` at **`src/Kumunita.Web/Program.cs:793`** (the
  comment block above it, `Program.cs:766-768`: "3. `opts.PublishFaultEvents()`
  — required for the `Fault<OutboxEmail>` handler (`OutboxEmailHandler.HandleFault`)
  to fire after the retry schedule is exhausted. **Without this the dead-letter
  hook is dead code**."). The retry schedule is the six-cooldown
  `RetryWithCooldown(backoff)` list (`Program.cs:799-802`, 5+15+45+120+275+980
  min = ~24 h). **This is the ONE trigger point F11 hooks** — the
  `HandleFault` hook, immediately after the `EmailDeadLetter` row is written.
- **The domain dead-letter write is Core and Wolverine-free.**
  `src/Kumunita.Core/Identity/EmailDeadLetterWriter.cs` is the **single** writer
  of `EmailDeadLetter` rows ("deliberately Wolverine-free: the host's durable
  handler hands the final-failure details here … and this method stores the
  domain row on the caller's session"). `EmailDeadLetter` (the domain doc) is
  `src/Kumunita.Core/Identity/EmailMessages.cs:35` — its doc-comment: "A
  non-empty count drives `/health`'s **degraded** state (OPS §8); the operator
  re-queues or discards rows (OPS §7)". **F11 does NOT change this write** —
  it is the load-bearing operator signal (the `/health` degraded gate); F11 is
  **additive** (one `ReportAsync` call alongside it), pin §3.
- **`IAlertSink` does not exist yet.** A repo-wide
  `grep -rn "IAlertSink\|IAlertChannel\|IAlert\b\|AlertSink" src/` returns
  **zero** hits. The seam is a **new** Core interface — **F11.U2's job**, not
  F11.U1's. This pin fixes its **shape** (pin §2), not its **implementation**.
- **The Core I/O-seam precedent F11 mirrors** (the `ISmtpSender` /
  `IEmailDeadLetterCounter` pair):
  - `ISmtpSender` — the interface is **Core** (`src/Kumunita.Core/Identity/SmtpSender.cs:27`,
    `public interface ISmtpSender { Task SendAsync(OutboxEmail email, CancellationToken ct = default); }`),
    the impl (`SmtpSender`) is Core, the **registration is Core DI**
    (`src/Kumunita.Core/DependencyInjection.cs:369`,
    `services.AddTransient<ISmtpSender, SmtpSender>();`). The **one-payload + ct**
    method shape (`SendAsync(OutboxEmail, ct)`) is the **exact** shape the
    `IAlertSink` pin mirrors (pin §2a).
  - `IEmailDeadLetterCounter` — `src/Kumunita.Core/Identity/IEmailDeadLetterCounter.cs`
    ("Count seam for the `EmailDeadLetter` document (OPS.md §8 …)") — a **thin
    Core I/O seam the Web owns the registration of**, registered at
    `DependencyInjection.cs:356` (`services.AddTransient<IEmailDeadLetterCounter, EmailDeadLetterCounter>();`).
    The same "Core seam, Core default impl, additive Web channel" split F11 uses.
- **The F10 Sentry channel is the one **reusable** binding** (pin §2d). The
  `Sentry.AspNetCore 6.12.1` package is **already** in `src/Kumunita.Web/Kumunita.Web.csproj`
  (F10.U2), the `UseSentry(o => …)` registration is **already** in
  `src/Kumunita.Web/Program.cs` (F10.U2), gated on
  `if (!string.IsNullOrWhiteSpace(builder.Configuration["SENTRY_DSN"]))`. The
  `Sentry` types (`ISentryClient`, `SentryEvent`) are resolvable through the
  transitive graph (the F10.U3 handoff note confirms `ISentryClient` is the real
  registered SDK interface in 6.12.1 — **not** `ISentrySdk`/`ISentryEventSink`,
  which do not exist in 6.12.1). **F11's `SentryAlertSink` binding reuses this
  surface** (the `ISentryClient` / `Sentry.CaptureException` shape) — **no new
  package** (pin §6).
- **The Web-harness idiom for a seam + a Web static handler** (the
  `SideEffectHarnessTests` precedent the `OutboxEmailHandler` doc cites: "so
  the row shape and the `SaveChangesAsync` contract stay testable (see
  `SideEffectHarnessTests`)"). The `HandleFault` static handler is **invocable
  in-process** (a static method with method-injected parameters — no live
  message host required). **The F11.U3 test invokes `HandleFault` directly**
  with a **recording stub `IAlertSink`** + a real `IDocumentSession`
  (`PostgresFixture` seam), and asserts the sink was called — pin §4.

---

## 2. The alert-hook pin — the **dedicated `IAlertSink` Core seam** (F11.U2 lands it)

**The decision (pin §2.0): F11 pins the `IAlertSink` Core seam, NOT a direct
Sentry call.** The plan's F11 row offers two options ("reuse F10's Sentry
alert channel **or** a dedicated `IAlertSink` seam — pin one"). This pin chooses
the **`IAlertSink` seam** for three grounded reasons, and **reuses the F10
Sentry channel as one of the seam's bindings** (not instead of the seam):

| Reason | The grounding |
|---|---|
| **(a) ADR 0006 — Core stays HTTP-free.** The trigger point's *domain* payload is the `EmailDeadLetter` row (a Core type). A **direct** `Sentry.CaptureException(...)` call from the `HandleFault` path would make the **domain** dead-letter path **depend on a Web package** (`Sentry`) — inverting the `ISmtpSender` precedent (Core seam, Web channel). The `IAlertSink` seam keeps the **domain** in Core (the seam + payload) and the **channel** in Web/owner-ops. | `ISmtpSender` (Core) + `SmtpSender` registration (Core DI) + the Web binding; the F10 pin's own §7 "Core stays HTTP-free — the registration and the PII-scrubber are **Web** concerns." |
| **(b) Off-by-default must be a **code** guarantee, not a `SENTRY_DSN` dependency.** If F11's alert fired *only* via Sentry, then with `SENTRY_DSN` **unset** (the default, the standing deferral) **no alert fires** — the JOB-02 FAIL would close **only if the owner sets the DSN** (an owner-ops step, NOT a code guarantee). The `IAlertSink` seam with a `NoopAlertSink`/`LogAlertSink` **default** binding means F11 **closes by code** (the alert *path* is wired + tested), and the Sentry binding is the **additive, DSN-gated** channel (the F10 idiom). | F10 pin §7 "OFF by default — with `SENTRY_DSN` unset … the pipeline is **byte-identical**" (the no-op-when-unset idiom F11's default binding mirrors). |
| **(c) F12 + F18 reuse the same seam.** F12 (OBS-06, "route alerts to a human") and F18 (MAIL-06, "bounce/complaint/volume alarms") are **downstream** of F11 in the plan's dependency graph (F12 "dep: F10/F11", F18 "dep: F12"). They need **the same alert channel** pointed at a human / the mail provider. A **dedicated `IAlertSink` seam** is the **one** place F12/F18 bind their channels; a direct Sentry call would not generalize (a human channel is not Sentry). | The plan §F11 "reuses the F10 Sentry alert channel, **or a dedicated `IAlertSink` seam — pin**", and F12's "implement the `IAlertSink` chosen in F11 against this channel." |

**The `IAlertSink` seam signature** (pin §2a — the **one-payload + ct** shape,
the `ISmtpSender.SendAsync(OutboxEmail, ct)` precedent, F11.U2 lands it in
`Kumunita.Core/Identity/`):

```csharp
namespace Kumunita.Core.Identity;

/// <summary>
/// One-shot, alert-worthy operational event (a domain payload, NOT a persisted
/// doc — it is never stored, it is the alert's input). Core-owned (ADR 0006 —
/// the domain "something alert-worthy happened" is a domain fact, not a
/// transport concern); the channel that delivers it (Sentry / SMTP / log) is
/// a Web + owner-ops concern (the ISmtpSender Core-seam / Web-channel split).
/// </summary>
public sealed record AlertEvent(
    string Kind,        // operator-readable discriminator: "email.dead-letter" (F11);
                       // F18 adds "mail.bounce-alarm", F12 routes regardless of kind.
    string Subject,     // one-line operator summary (the dead-letter recipient / key).
    string? Detail,     // the last delivery failure / threshold context (nullable).
    int? Attempts);     // the retry count context (nullable; the EmailDeadLetter.Attempts).

/// <summary>
/// The alert seam (JOB-02). Core-owned (ADR 0006 — Core stays HTTP-free: the
/// seam never names Sentry/SMTP types, it only carries an AlertEvent). The
/// channel binding (the Sentry-backed SentryAlertSink, the F12 human-channel
/// sink, the off-by-default NoopAlertSink) is a Web + owner-ops concern.
/// </summary>
/// <summary>
/// Contract: ReportAsync is best-effort at the boundary — a sink/channel
/// failure must NEVER propagate into the caller's domain write path (the
/// EmailDeadLetter row is the load-bearing operator signal, OPS §8; the alert
/// is a *copy*, not the *signal*). F11.U2's default binding (NoopAlertSink)
/// honors this by construction (it does nothing); the Sentry-backed binding
/// wraps its channel call in try/catch (fail-open at the boundary, the
/// SmtpSender "throws on failure" is the *per-attempt* contract, NOT the
/// alert-boundary contract).
/// </summary>
public interface IAlertSink
{
    Task ReportAsync(AlertEvent alert, CancellationToken ct = default);
}
```

**The `AlertEvent` payload contract** (pin §2b — the **closed** list of fields,
the F9.U1 / F10 §3c closed-list precedent):

- **`Kind`** — a stable `string` discriminator. F11's one value is
  **`"email.dead-letter"`** (the trigger of pin §3). **F12 does not add a kind**
  (it routes *every* kind to a human); **F18 adds `"mail.bounce-alarm"`**.
  The `Kind` is a **`string`**, not an `enum` (a new enum is a frozen-seam
  re-shape, the F10 pin's "do not re-derive" discipline — a `string` keeps the
  seam open to F18's new kind without a type change).
- **`Subject`** — the operator-readable one-liner. For F11, the
  **`EmailDeadLetter.Recipient`** (falling back to the `IdempotencyKey`) — the
  "which address failed" the operator re-queues (OPS §7).
- **`Detail`** — the `EmailDeadLetter.LastError` (nullable). The last delivery
  failure the operator re-queues against (OPS §7).
- **`Attempts`** — the `EmailDeadLetter.Attempts` (nullable `int` — nullable so
  a non-retry alert, e.g. F18's volume alarm, can carry no attempt count).
- **NOT in the payload:** the `EmailDeadLetter.Id` (the doc's own id is a
  persisted-doc concern, not an alert concern — the alert's `Subject`/`Kind`
  identify the event; the row's `Id` stays in the `/health` / re-queue path).
  **No `DateTimeOffset`** (the alert's *time* is stamped by the **channel**
  (Sentry's `Received` / the log line / the human channel's receipt), NOT the
  domain — the domain `AlertEvent` is a **value** payload, not a record-with-a-timestamp).

**The default binding — `NoopAlertSink` (off-by-default, the F10 no-op idiom)**
(pin §2c, F11.U2 lands it):

- **`NoopAlertSink`** (Core, `Kumunita.Core/Identity/`) — the **default**
  `IAlertSink` binding. `ReportAsync` **does nothing** (a `Task.CompletedTask`
  return, a structured `ILogger` line at `Information` level is acceptable —
  **either** form is in-scope; the pin fixes the **behavior** (no network, no
  throw, no state) not the **form**). Registered as the default in
  `AddKumunitaCore` (`src/Kumunita.Core/DependencyInjection.cs`, the
  `services.AddTransient<IAlertSink, NoopAlertSink>();` line — the
  `DependencyInjection.cs:369` `ISmtpSender` registration precedent).
- **Why a no-op default (not a hard requirement to configure):** the F10
  standing owner deferral ("no external telemetry by default") is honored by
  construction — an unconfigured instance does **not** send any alert anywhere
  (the `/health` degraded gate is **the** always-on signal, unchanged; the
  alert is the **additive** push). **This is the frozen invariant** — the F10
  pin §7 "OFF by default … the pipeline is byte-identical" precedent, applied
  to the alert path.

**The Sentry-backed binding — `SentryAlertSink` (reuses F10, DSN-gated)**
(pin §2d, F11.U2 lands it, **optional** — the no-op default already closes F11
by code):

- **`SentryAlertSink`** (**Web**, `src/Kumunita.Web/`) — an `IAlertSink` whose
  `ReportAsync` calls the F10 Sentry surface (the `ISentryClient` /
  `Sentry.CaptureException` shape the F10.U3 handoff note confirms is the real
  6.12.1 registered SDK interface — **not** `ISentrySdk`, which does not exist
  in 6.12.1). It is registered in `src/Kumunita.Web/Program.cs` **inside the
  F10 `if (!string.IsNullOrWhiteSpace(builder.Configuration["SENTRY_DSN"]))`
  block** (the **same** DSN-gated region — the F10.U2 open-item (a)/(b) idiom:
  **unset → the Sentry binding is a no-op, the `NoopAlertSink` default is what
  fires**). It **wraps** the channel call in `try/catch` (a Sentry failure is
  swallowed, the `AlertEvent` is not lost to a throw — pin §2a contract).
- **This binding is OPTIONAL for F11's close** (the `NoopAlertSink` default
  + the `HandleFault` trigger wiring + the `DeadLetter_FiresAlert` test already
  close JOB-02 **by code**). The `SentryAlertSink` is the **additive,
  DSN-gated** channel the owner gets **if** they set `SENTRY_DSN` — the F10
  deferral idiom. **F11.U2 may land it or record it as a judgment call**; the
  pin fixes the **seam + the default + the trigger**, and the Sentry binding is
  the **optional** channel (pin §6 lists it as an in-scope file, but the
  **load-bearing** contract is the seam + default + trigger).

---

## 3. The trigger point — `OutboxEmailHandler.HandleFault` (F11.U2 wires it)

**The ONE trigger point** (pin §3.0): the `HandleFault` terminal-failure hook
in `src/Kumunita.Web/SideEffects/OutboxEmailHandler.cs`. **F11 adds the alert
call immediately AFTER the `EmailDeadLetterWriter.WriteAndCommitAsync(...)`
call** — the domain row (the load-bearing signal) is committed **first**, the
alert (the additive push) fires **second**:

```csharp
// F11 (JOB-02, U2) — the dead-letter alert (pin §3): fires AFTER the
// EmailDeadLetter row is committed (the /health degraded gate is the
// load-bearing signal, unchanged; the alert is an additive push). The IAlertSink
// is Core (ADR 0006 — the domain "an alert-worthy event happened" + its
// AlertEvent payload are Core; the channel — Sentry / human / log — is
// Web + owner-ops). Best-effort at the boundary: a sink failure never propagates
// (the row is already committed; the alert is a copy, not the signal).
public static async Task HandleFault(
    Wolverine.Fault<OutboxEmail> fault,
    IDocumentSession session,
    IAlertSink? alertSink = null)   // method-injected (the ISmtpSender Handle() precedent); nullable so the existing harness-construction sites stay valid
{
    var origin = fault.Message
        ?? throw new InvalidOperationException(
            "Fault envelope carried no original OutboxEmail; cannot write an EmailDeadLetter without it.");

    var lastError = fault.Exception?.Message ?? "unknown";
    var attempts  = fault.Attempts;

    var row = await EmailDeadLetterWriter.WriteAndCommitAsync(
        session, origin, lastError, attempts);

    // F11 — the alert (pin §3.1). Best-effort: swallow any sink failure (the
    // row is committed; the alert is a copy, not the signal — the /health
    // degraded gate is the always-on signal). A null sink (a harness-construction
    // site that did not supply one) is a no-op (the off-by-default idiom).
    if (alertSink is not null)
    {
        try
        {
            await alertSink.ReportAsync(new AlertEvent(
                Kind:      "email.dead-letter",
                Subject:   row.Recipient,          // the operator re-queues this (OPS §7)
                Detail:    row.LastError,
                Attempts:  row.Attempts), ct: default);
        }
        catch
        {
            // A sink/channel failure is swallowed (pin §2a contract — best-effort
            // at the boundary; the row is the load-bearing signal, the alert is a
            // copy). The owner sees the row in /health regardless.
        }
    }
}
```

**The pin §3.1 wiring contract:**

- **The alert fires AFTER the row commit** (the `WriteAndCommitAsync` call is
  unchanged; the `ReportAsync` call is additive, after it). **The `/health`
  degraded gate is UNCHANGED** (the `EmailDeadLetter` row count still drives
  it — OPS §8, ARCHITECTURE.md §6.2). **F11 is additive, not a re-shape.**
- **`IAlertSink? alertSink = null`** — the handler parameter is **nullable**
  (a method-injected parameter, the `Handle(OutboxEmail, ISmtpSender)`
  method-injection precedent in the same class). **Nullable** so the **existing
  harness-construction sites** (the `SideEffectHarnessTests` that invoke
  `HandleFault` with only the `session` parameter, if any) **stay valid** —
  a missing sink is a **no-op** (the off-by-default idiom, pin §2c). This is
  the **frozen** shape — F11.U2 must NOT make the parameter non-nullable (that
  would break the existing harness sites, the F10 pin's "the pipeline is
  byte-identical" discipline applied to the handler signature).
- **The `try/catch` around `ReportAsync`** — a sink/channel failure is
  **swallowed** (pin §2a contract: "a sink/channel failure must NEVER
  propagate into the caller's domain write path"). The row is **already
  committed**; the alert is a **copy**. **The `catch` does NOT rethrow** (a
  rethrow would turn an alert-failure into a dead-letter-write-failure —
  inverting the "row is the load-bearing signal" invariant).
- **The `AlertEvent` payload** for F11's trigger is the **closed** list of pin
  §2b: `Kind: "email.dead-letter"`, `Subject: row.Recipient`, `Detail:
  row.LastError`, `Attempts: row.Attempts`. **No other fields** (the
  `EmailDeadLetter.Id` is NOT in the payload, pin §2b).
- **The `ct: default`** — the `HandleFault` signature has **no** `CancellationToken`
  parameter today (a method-injected static handler); the pin uses
  `CancellationToken.None` via `ct: default` (the `ISmtpSender.SendAsync(…, ct)`
  shape, the `EmailDeadLetterWriter.Write(…, ct)` precedent). F11.U2 does **NOT**
  add a `ct` parameter to `HandleFault` (that would change the handler
  signature beyond the additive `IAlertSink?` parameter — the pin's additive-only
  discipline).

**The `AddKumunitaCore` default registration** (pin §3.2, F11.U2 lands it):

- **`src/Kumunita.Core/DependencyInjection.cs`** — the **one** new line in
  `AddKumunitaCore`: `services.AddTransient<IAlertSink, NoopAlertSink>();`
  (the **default** binding, the `DependencyInjection.cs:369` `ISmtpSender`
  registration precedent, the `DependencyInjection.cs:356`
  `IEmailDeadLetterCounter` registration precedent). **The `SentryAlertSink`
  (Web) is registered LATER, in `Program.cs`, inside the F10 DSN-gated block**
  (so it **replaces** the default **only when** `SENTRY_DSN` is set — the
  additive + flag-gated idiom, the F10.U2 registration ordering precedent:
  the Web registration comes **after** `AddKumunitaCore`, so it wins the DI
  resolution). **No other** `DependencyInjection.cs` change (the `ISmtpSender` /
  `IMailerStage` / `IEmailDeadLetterCounter` registrations are **untouched**).

---

## 4. The pinned test name + assertion contract (F11.U3 owns the test file + harness)

**The pinned test name** (the F10 pin §4 precedent, the F9.U4 / F1.U1
pinned-name precedent):

```
DeadLetter_FiresAlert
```

**The test file** (F11.U3 owns it): `tests/Kumunita.Web.Tests/F11U3_DeadLetterAlertTests.cs`
(a **new** file — the `F10U3_ErrorTrackerTests.cs` precedent, the
`tests/Kumunita.Web.Tests` project). **The test lives in `Kumunita.Web.Tests`,
NOT `Kumunita.Core.Tests`** — grounded, not assumed:
- The **trigger** under test is `OutboxEmailHandler.HandleFault` — a **Web**
  static class (`src/Kumunita.Web/SideEffects/OutboxEmailHandler.cs`).
  `Kumunita.Core.Tests` **does not reference `Kumunita.Web`** (the F10.U2 /
  F10.U3 handoff notes confirm: "Kumunita.Core.Tests does not reference
  Kumunita.Web — its `.csproj` has only a `ProjectReference` to
  `Kumunita.Core` + Testcontainers"). So the `HandleFault` type is **not
  visible** to `Kumunita.Core.Tests` → the test **must** be in
  `Kumunita.Web.Tests`.
- The **seam** under test (`IAlertSink`, `AlertEvent`) is **Core** — but the
  **trigger wiring** (the `HandleFault` → `ReportAsync` call) is **Web**, and
  that is the behavior `DeadLetter_FiresAlert` asserts (the **firing**, not
  just the seam's existence).

**The assertion contract** (pin §4a — the F10 pin §4a leg-1/leg-2/leg-3
precedent, the `SideEffectHarnessTests` harness idiom the
`OutboxEmailHandler` doc cites):

| # | Assertion | The shape | The pin |
|---|-----------|-----------|---------|
| 1 | **The `EmailDeadLetter` row is STILL written** (F11 is additive, not a re-shape). | Invoke `OutboxEmailHandler.HandleFault(fault, session)` (the `SideEffectHarnessTests` harness idiom — a real `PostgresFixture` `IDocumentSession`, a synthetic `Wolverine.Fault<OutboxEmail>` envelope with a `Message` + an `Exception` + an `Attempts` count). Assert: a **new** `EmailDeadLetter` row is in the session (the `session.Query<EmailDeadLetter>()` count went up by 1 — the `SideEffectHarnessTests` row-shape idiom the `OutboxEmailHandler` doc cites). | §4a leg-1 |
| 2 | **The `IAlertSink.ReportAsync` was called EXACTLY ONCE** with the pin §2b `AlertEvent`. | A **recording stub `IAlertSink`** (a `sealed class RecordingAlertSink : IAlertSink` that appends each `AlertEvent` to a `List<AlertEvent>` and returns `Task.CompletedTask`) passed as the `alertSink` parameter to `HandleFault`. Assert: the stub's list has **exactly one** entry (the `ReportAsync` was called once per dead-letter — the "one alert per dead-letter" contract). | §4a leg-2 |
| 3 | **The `AlertEvent` payload matches pin §2b exactly.** | Assert on the single recorded `AlertEvent`: `Kind == "email.dead-letter"` (the pin §2b `Kind`), `Subject == <the OutboxEmail.Recipient>` (the pin §2b `Subject` — the operator re-queues this), `Detail == <the fault.Exception.Message>` (the pin §2b `Detail`), `Attempts == <the fault.Attempts>` (the pin §2b `Attempts`). **A direct-property assert** (the F10 pin §4a leg-3 "a direct property assert" idiom). | §4a leg-3 |
| 4 | **A failing sink does NOT break the dead-letter write** (pin §2a / §3.1 best-effort-at-the-boundary contract). | A **throwing stub `IAlertSink`** (a `sealed class ThrowingAlertSink : IAlertSink` whose `ReportAsync` throws) passed as the `alertSink` parameter. Assert: the `EmailDeadLetter` row is **still** written (leg-1 holds) AND `HandleFault` **returns normally** (no exception propagates — the `try/catch` swallow, pin §3.1). **This is the load-bearing leg** (a sink failure must not turn a dead-letter into a handler failure). | §4a leg-4 |

**The harness gotcha** (pin §4b — the F10 pin §4b harness gotcha precedent):

- **The `Wolverine.Fault<OutboxEmail>` envelope construction** — the `Fault<T>`
  type is **Wolverine** (the `Wolverine.Fault<T>` record, the
  `OutboxEmailHandler.HandleFault` parameter). The **exact** construction shape
  (the `Message` / `Exception` / `Attempts` / `CorrelationId` / `SourceContext`
  members) is a **F11.U3 judgment call** — F11.U3 reads the Wolverine `Fault<T>`
  source (the `WolverineFx` package already in `Kumunita.Web.csproj`, the
  `OutboxEmailHandler`'s own usage at `fault.Message` / `fault.Exception` /
  `fault.Attempts`) to find the exact member names. **This is NOT a drift** —
  the pin fixes the **contract** (the four legs), the **envelope construction**
  is the implementation (the F10 pin §4b "the pin fixes the contract, not the
  access path" precedent).
- **The `PostgresFixture` `IDocumentSession`** — the `HandleFault` writes a
  real `EmailDeadLetter` row (leg-1), so the test needs a **real**
  `IDocumentSession` (the `PostgresFixture` seam-test shape the F1 pin
  (`tests/Kumunita.Core.Tests/PostgresFixture`) and the `SideEffectHarnessTests`
  harness use). **The `Kumunita.Web.Tests` project must have access to the
  `PostgresFixture`** (a `ProjectReference` to `Kumunita.Core.Tests` OR a
  shared fixture assembly — the **exact** wiring is a **F11.U3 judgment
  call**, the F10 pin §4b "the exact access idiom is a F10.U3 judgment call"
  precedent). **If the `PostgresFixture` is not reachable from
  `Kumunita.Web.Tests`** (a `ProjectReference` / `InternalsVisibleTo` issue),
  F11.U3 **reuses the `SideEffectHarnessTests` harness** (the
  `OutboxEmailHandler` doc's own cited test: "the failure/retry/dead-letter
  harness (M1 step 7) can verify the exact row shape and the same-tx-commit
  guarantee without spinning up a live message host" — the
  `SideEffectHarnessTests` shape is the **in-repo** harness that already
  exercises `HandleFault`-adjacent code, the **nearest** precedent).
- **The `SentryAlertSink` is NOT tested in F11.U3** (the `DeadLetter_FiresAlert`
  test asserts the **seam + trigger**, not the Sentry **channel** — the
  Sentry-backed binding is a **F10** concern (the F10.U3
  `Error_TrackerRegisteredWhenDsnSet` test already covers the Sentry
  registration + scrubber). **The `NoopAlertSink` default is implicitly
  covered** (a `HandleFault` call with **no** `alertSink` parameter — a
  `null` sink — is a **no-op**, pin §3.1; a leg that asserts "a null sink is a
  no-op" is **optional** (the `if (alertSink is not null)` guard is the
  frozen shape, the F10 pin §7 "the pipeline is byte-identical" idiom)).

---

## 5. The owner-ops open items (NOT assumed done — carry them into the `## F11.U1` section)

F11 is **`code+ops`** (the plan's F11 row). The code unit (U2 + U3) closes
JOB-02 **by code** (the seam + the trigger + the test). The **owner-ops**
steps are **open items**, NOT assumed done (the F10.U2 open-item idiom, the
F9.U3 open-item (a)/(b) idiom):

**(a) Point the sink at the F12 human channel** (the plan's F11 open item:
"**Open item (owner):** point the sink at the F12 human channel"). F12
(OBS-06, "route alerts to a human") is a **downstream** fix (F12 dep: F10/F11).
F11's `IAlertSink` seam is the **one** place F12 binds its human channel
(the F11 → F12 dependency the plan's dependency table encodes: F12 "the
channel (pin — e.g. the community `SupportEmail` SMTP for critical alerts,
or the F10 Sentry alert rule)"). **Until F12 lands, F11's alert fires to the
`NoopAlertSink` default (a structured log) or the Sentry-backed binding (if
`SENTRY_DSN` is set) — NOT to a human.** The owner's op (configuring the F12
human channel) is **recorded, not assumed**.

**(b) The F10 `SENTRY_DSN` owner-ops deferral still stands** (the F10.U2
open-item (a)/(b) idiom). If F11.U2 lands the `SentryAlertSink` binding
(pin §2d), it is **DSN-gated** (the F10 `if
(!string.IsNullOrWhiteSpace(builder.Configuration["SENTRY_DSN"]))` block):
**unset → the Sentry binding is a no-op, the `NoopAlertSink` default fires**
(a structured log — the off-by-default idiom, pin §2c). **Set → the
Sentry-backed binding fires** (the owner's Sentry dashboard receives the
`"email.dead-letter"` alert — the PII-scrubbing contract from F10 is **live**
on the alert's `Subject`/`Detail` fields — the F10 pin §3 PII-scrubbing
contract applies to the alert's content, NOT a new scrubber in F11). **The
owner's awareness** of the `SENTRY_DSN` knob is required (the F10.U2
open-item (b) idiom: "if the DSN is **already set** on prod … the rebuild
**enables** Sentry **immediately**"). **F11 does NOT change the DSN default**
(it stays unset, the standing deferral).

**(c) The F11 `NoopAlertSink` default is the always-on floor** (pin §2c). The
owner's **awareness** that the alert **defaults to a structured log** (not a
network call) is required — the "no external telemetry by default" standing
deferral is honored (the F10 pin §7 "OFF by default … the pipeline is
byte-identical" idiom). **The `/health` degraded gate is the always-on
signal** (OPS §8); the alert is the **additive** push (pin §3.0). The owner's
**awareness** of this split (row = signal, alert = push) is required (the
F10.U2 open-item (b) idiom).

---

## 6. The file list (F11.U2 + F11.U3) — the pin §6 scope

**F11.U2 lands** (the **seam + the default + the trigger** — the
**load-bearing** contract; the Sentry binding is **optional**):

| File | The change | The pin |
|------|-----------|---------|
| `src/Kumunita.Core/Identity/AlertSink.cs` (NEW) | The `IAlertSink` interface + the `AlertEvent` record + the `NoopAlertSink` default impl (all in **one** file, the `SmtpSender.cs` `ISmtpSender` + `SmtpSender` in-one-file precedent). Core (ADR 0006 — the seam + payload + default are Core; the channel is Web). | pin §2a/§2b/§2c |
| `src/Kumunita.Core/DependencyInjection.cs` | The **one** new line in `AddKumunitaCore`: `services.AddTransient<IAlertSink, NoopAlertSink>();` (the **default** binding, the `DependencyInjection.cs:369` `ISmtpSender` registration precedent). **No other** change (the `ISmtpSender` / `IMailerStage` / `IEmailDeadLetterCounter` registrations are **untouched**). | pin §3.2 |
| `src/Kumunita.Web/SideEffects/OutboxEmailHandler.cs` | The **additive** `IAlertSink? alertSink = null` parameter on `HandleFault` + the **`ReportAsync` call after the `WriteAndCommitAsync` call** (the pin §3.0 block, the **`try/catch` swallow**). **No other** change (the `Handle` method, the `fault.Message` / `fault.Exception` / `fault.Attempts` reads, the `EmailDeadLetterWriter` call are **untouched**). | pin §3.0/§3.1 |
| `src/Kumunita.Web/Program.cs` (OPTIONAL) | **IF** F11.U2 lands the `SentryAlertSink`: the **one** new line **inside the F10 `if (!string.IsNullOrWhiteSpace(builder.Configuration["SENTRY_DSN"]))` block**: `builder.Services.AddTransient<IAlertSink, SentryAlertSink>();` (the additive + flag-gated idiom, the F10.U2 registration-ordering precedent: the Web registration comes **after** `AddKumunitaCore`, so it wins the DI resolution **only when** the DSN is set). **IF** F11.U2 does NOT land the `SentryAlertSink`: **no `Program.cs` change** (the `NoopAlertSink` default is the binding, the off-by-default idiom). | pin §2d (optional) |
| `src/Kumunita.Web/SideEffects/SentryAlertSink.cs` (NEW, OPTIONAL) | **IF** F11.U2 lands the `SentryAlertSink`: the `IAlertSink` impl that calls the F10 Sentry surface (the `ISentryClient` / `Sentry.CaptureException` shape the F10.U3 handoff note confirms is the real 6.12.1 registered SDK interface — **not** `ISentrySdk`, which does not exist in 6.12.1). **IF** F11.U2 does NOT land it: **no file**. | pin §2d (optional) |

**F11.U3 lands** (the test file + harness):

| File | The change | The pin |
|------|-----------|---------|
| `tests/Kumunita.Web.Tests/F11U3_DeadLetterAlertTests.cs` (NEW) | The **pinned** `DeadLetter_FiresAlert` test (the §4a leg-1/leg-2/leg-3/leg-4 asserts) + the **`RecordingAlertSink`** + **`ThrowingAlertSink`** stubs + the **`PostgresFixture`** / **`SideEffectHarnessTests`** harness wiring (the §4b harness gotcha). **No other** test file (the `Kumunita.Core.Tests` project is **untouched** — the trigger is a **Web** static class, pin §4). | pin §4 |

**NOT in F11's scope** (the pin §6 boundary):

- **The `SentryAlertSink` is OPTIONAL** (pin §2d) — the **load-bearing**
  contract is the `IAlertSink` seam + the `NoopAlertSink` default + the
  `HandleFault` trigger + the `DeadLetter_FiresAlert` test (all of which close
  JOB-02 **by code** regardless of `SENTRY_DSN`). The `SentryAlertSink` is the
  **additive, DSN-gated** channel (the F10 deferral idiom). F11.U2 **may** land
  it or **record it as a judgment call** (the F10.U2 open-item (c) idiom:
  "the `Sentry.AspNetCore` version pin is 6.12.1 — this unit's judgment call,
  recorded here").
- **The F12 human channel** (OBS-06, "route alerts to a human") — F12 is a
  **downstream** fix (F12 dep: F10/F11). F11's `IAlertSink` seam is the **one**
  place F12 binds its channel; **F11 does NOT implement F12's channel** (a
  human-channel sink, a `SupportEmail` SMTP sink, an alert-rule sink). **No
  F12 channel code in F11.**
- **The F18 mail-provider alarms** (MAIL-06, "bounce/complaint/volume
  alarms") — F18 is a **downstream** fix (F18 dep: F12). F18 adds the
  `"mail.bounce-alarm"` `Kind` (the pin §2b `Kind` is a `string`, NOT an
  `enum` — open to F18's new kind without a type change). **F11 does NOT
  implement F18's alarm logic** (a provider-API poll, a threshold check, a
  volume counter). **No F18 alarm code in F11.**
- **The `WhatsNew.cs` / `Milestones.cs` / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `docs/adr/` close-flip** (the six members) — the
  close-flip happens at **F22**, the lane's **last** unit. **F11 is not the
  lane's last unit** (F12–F22 remain). **Do NOT run the close-flip** in
  F11.U1 / F11.U2 / F11.U3.
- **The `EmailDeadLetter` row / the `/health` degraded gate / the
  `EmailDeadLetterWriter` / the `ISmtpSender` / the `IMailerStage`** — F11 is
  **additive** (one `ReportAsync` call + one `IAlertSink?` parameter + one
  `AddTransient` line). **The dead-letter write is UNCHANGED** (pin §3.0: "the
  alert fires AFTER the row commit — the domain row (the load-bearing signal)
  is committed **first**, the alert (the additive push) fires **second**").
  **No `EmailDeadLetterWriter` change, no `EmailDeadLetter` doc change, no
  `ISmtpSender` change, no `IMailerStage` change.**
- **A new `<PackageReference>`** — **NONE** in F11. The `SentryAlertSink`
  (optional, pin §2d) reuses the F10 `Sentry.AspNetCore 6.12.1` package (already
  in `Kumunita.Web.csproj`). **The `Kumunita.Core.csproj` is untouched** (the
  `IAlertSink` / `AlertEvent` / `NoopAlertSink` are pure C#, no new package).
  **The `Kumunita.Web.csproj` is untouched** (the `SentryAlertSink` reuses the
  F10 `Sentry` types via the transitive graph — the F10.U3 handoff note
  confirms `ISentryClient` resolves via the transitive graph, no new
  `<PackageReference>`).

---

## 7. The invariants held (the F9.U1 / F10 pin §7 invariant checklist precedent)

- **OFF by default** (pin §2c) — the **default** `IAlertSink` binding is
  `NoopAlertSink` (a no-op / a structured log, **no network**, **no throw**,
  **no state**). An **unconfigured** instance (the default, the standing
  deferral) does **not** send any alert anywhere (the `/health` degraded gate
  is the **always-on** signal, unchanged; the alert is the **additive** push).
  **The owner's instance is unchanged** until they set `SENTRY_DSN` (the
  `SentryAlertSink` binding) or land F12 (the human channel) — the F10 pin §7
  "OFF by default … the pipeline is byte-identical" idiom, applied to the
  alert path.
- **Core stays HTTP-free** (ADR 0006) — the `IAlertSink` seam + the `AlertEvent`
  payload + the `NoopAlertSink` default are **Core** (the domain "an
  alert-worthy event happened" + its one-shot payload + the default no-op
  binding). The **channel** (Sentry / SMTP / log) is a **Web + owner-ops**
  concern (the `SentryAlertSink` is **Web**, the F12 human channel is **Web**).
  **The `IAlertSink` interface never names Sentry/SMTP types** (the seam is a
  pure domain seam, the `ISmtpSender` Core-seam / Web-channel split).
- **The dead-letter write is UNCHANGED** (pin §3.0) — the
  `EmailDeadLetterWriter.WriteAndCommitAsync` call is **untouched**; the
  `/health` degraded gate is **unchanged** (OPS §8); the `EmailDeadLetter` doc
  is **unchanged**. F11 is **additive** (one `ReportAsync` call after the row
  commit). **The row is the load-bearing signal; the alert is the additive
  push** (pin §3.1).
- **Best-effort at the boundary** (pin §2a / §3.1) — a sink/channel failure
  **never propagates** into the `HandleFault` domain write path (the `try/catch`
  swallow, pin §3.1). **A rethrow is FORBIDDEN** (a rethrow would turn an
  alert-failure into a dead-letter-write-failure — inverting the "row is the
  load-bearing signal" invariant). The `SmtpSender` "throws on failure" is the
  **per-attempt** contract (the `Handle` per-attempt send), **NOT** the
  alert-boundary contract (pin §2a).
- **The `HandleFault` signature is ADDITIVE-ONLY** (pin §3.1) — the **one** new
  parameter is `IAlertSink? alertSink = null` (a **nullable** method-injected
  parameter, the `Handle(OutboxEmail, ISmtpSender)` method-injection
  precedent). **A null sink is a no-op** (the off-by-default idiom, pin §2c).
  **The `Handle` method is UNCHANGED** (the per-attempt send is a different
  concern, the `ISmtpSender` seam, pin §1). **The `fault.Message` /
  `fault.Exception` / `fault.Attempts` reads are UNCHANGED** (the
  `OutboxEmailHandler`'s own usage, pin §1).
- **No new bounded context / DocTypes surface** (the F10 pin §7 invariant) —
  the `IAlertSink` seam + the `AlertEvent` payload are a **thin I/O seam**
  (the `ISmtpSender` / `IEmailDeadLetterCounter` Core I/O-seam precedent),
  **not** a new bounded context, **not** a new DocTypes surface, **not** a
  new persisted doc (the `AlertEvent` is a **value** payload, **never
  stored** — pin §2b).
- **No new `<PackageReference>`** (pin §6) — the `SentryAlertSink` (optional)
  reuses the F10 `Sentry.AspNetCore 6.12.1` package (already in
  `Kumunita.Web.csproj`). **The `Kumunita.Core.csproj` is untouched** (the
  `IAlertSink` / `AlertEvent` / `NoopAlertSink` are pure C#). **The
  `Kumunita.Web.csproj` is untouched** (the `SentryAlertSink` reuses the F10
  `Sentry` types via the transitive graph).
- **The F10 `Error_TrackerRegisteredWhenDsnSet` test stays green** (the F10
  pin §7 "The F7 frozen-invariant test stays green … the F9.U4 test stays
  green" idiom) — F11 is **additive** (a new seam + a new parameter + a new
  test file); the F10 Sentry registration + the F10 PII scrubber are
  **untouched**. **The F9.U4 `Signup_DisposableEmail_Rejected` test stays
  green** (the `Kumunita.Core` project is **additive-only**, the
  `AccountController.Signup` POST is **untouched**, the
  `IBotChallengeVerifier` seam is **untouched**).
- **The `Kumunita.Core.Tests` `PostgresFixture` parallelism flake** (the F10.U2
  open-item (d) idiom, the F10.U3 "RESOLVED this unit" note) — the F11.U3 test
  is in `Kumunita.Web.Tests` (NOT `Kumunita.Core.Tests`), so the F11.U3 test
  does **not** add to the `Kumunita.Core.Tests` `PostgresFixture` parallelism
  surface. **The `Kumunita.Core.Tests` flake is a pre-existing known issue**
  (the F10.U3 "RESOLVED this unit: the full `Kumunita.Core.Tests` assembly was
  re-run and came back `Failed: 0`" note). **F11.U3 does NOT re-run
  `Kumunita.Core.Tests`** (the F11.U3 test is in `Kumunita.Web.Tests` — the
  `Kumunita.Core` project is **additive-only**, a new `AlertSink.cs` file + one
  `AddTransient` line; the **existing** `Kumunita.Core.Tests` are **unchanged**
  → the F10.U3 "no regression" confirmation).

---

## 8. The build/test command (repo memory, the F9.U4 / F10 pin §8 precedent)

- `dotnet build Kumunita.slnx -c Debug` — **F11.U1 does NOT run this**
  (the unit's exit contract is **no build**). F11.U2 / F11.U3 run it.
- `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
  — the **F11.U3** test assembly (the `DeadLetter_FiresAlert` test is in
  `Kumunita.Web.Tests`, **not** `Kumunita.Core.Tests` — pin §4). **F11.U1 does
  NOT run this** (the unit's exit contract is **no test run**). **F11.U2 /
  F11.U3 run this** (the `Kumunita.Web.Tests` assembly is where the F11.U3
  test lives; the F10 pin §8 "F10.U2 / F10.U3 run it" precedent).
- `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
  — the `Kumunita.Core.Tests` assembly (Testcontainers Postgres, ~20 s,
  `docker container prune -f` after). **F11.U1 does NOT run this** (the unit's
  exit contract is **no test run**). **F11.U2 / F11.U3 run it** (the
  `Kumunita.Core` project is **additive-only** — a new `AlertSink.cs` file +
  one `AddTransient` line in `DependencyInjection.cs`; the **existing**
  `Kumunita.Core.Tests` are **unchanged** → a **no-regression** confirmation,
  the F10 pin §8 "F10.U2 / F10.U3 run it (the `Kumunita.Core` project is
  **untouched**, but the **solution** build touches it — the F9.U2 / F9.U3 /
  F9.U4 handoff notes all ran the `Kumunita.Core.Tests` assembly to confirm
  **no Core test broke**)" precedent). **The pre-existing `PostgresFixture`
  parallelism flake** (the F10.U2 open-item (d) idiom) is a **known** issue —
  a flake, **not** a F11 regression (the F10.U3 "RESOLVED this unit" note).

---

## 9. The pin's closed list (the F9.U1 / F10 pin §9 closed-list precedent)

- **The alert-hook decision** — pin §2.0 (the **`IAlertSink` Core seam**, NOT
  a direct Sentry call; the three grounded reasons: ADR 0006, off-by-default
  by code, F12/F18 reuse).
- **The `IAlertSink` seam signature** — pin §2a (the `IAlertSink` interface +
  the `AlertEvent` record + the `NoopAlertSink` default, the
  **one-payload + ct** shape, the `ISmtpSender.SendAsync(OutboxEmail, ct)`
  precedent).
- **The `AlertEvent` payload contract** — pin §2b (the **closed** list:
  `Kind` (a `string`, NOT an `enum` — open to F18's new kind), `Subject`,
  `Detail`, `Attempts`; NOT in the payload: the `EmailDeadLetter.Id`, a
  `DateTimeOffset`).
- **The `NoopAlertSink` default binding** — pin §2c (the off-by-default idiom,
  the F10 no-op-when-unset precedent).
- **The `SentryAlertSink` binding** — pin §2d (the **optional** DSN-gated
  channel, the F10 `ISentryClient` / `Sentry.CaptureException` shape, the
  F10.U3 handoff note's real 6.12.1 registered SDK interface).
- **The trigger point** — pin §3.0 (the `OutboxEmailHandler.HandleFault` hook,
  the `ReportAsync` call **after** the `WriteAndCommitAsync` call, the
  **`IAlertSink? alertSink = null`** nullable parameter, the **`try/catch`
  swallow**, the `AlertEvent` payload for F11's trigger:
  `Kind: "email.dead-letter"`, `Subject: row.Recipient`, `Detail: row.LastError`,
  `Attempts: row.Attempts`).
- **The `AddKumunitaCore` default registration** — pin §3.2 (the
  `services.AddTransient<IAlertSink, NoopAlertSink>();` line, the
  `DependencyInjection.cs:369` `ISmtpSender` registration precedent).
- **The pinned test name** — `DeadLetter_FiresAlert` (pin §4).
- **The test file** — `tests/Kumunita.Web.Tests/F11U3_DeadLetterAlertTests.cs`
  (pin §4, the `Kumunita.Web.Tests` project — the trigger is a **Web** static
  class).
- **The assertion contract** — pin §4a (leg-1: the row is still written;
  leg-2: `ReportAsync` called exactly once; leg-3: the `AlertEvent` payload
  matches pin §2b; leg-4: a failing sink does NOT break the dead-letter
  write).
- **The harness gotcha** — pin §4b (the `Wolverine.Fault<OutboxEmail>`
  envelope construction, the `PostgresFixture` / `SideEffectHarnessTests`
  harness wiring, the `SentryAlertSink` is NOT tested in F11.U3).
- **The owner-ops open items** — pin §5 (a) point the sink at the F12 human
  channel; (b) the F10 `SENTRY_DSN` deferral still stands; (c) the
  `NoopAlertSink` default is the always-on floor.
- **The file list** — pin §6 (F11.U2: `AlertSink.cs` (Core) +
  `DependencyInjection.cs` (Core) + `OutboxEmailHandler.cs` (Web) +
  `Program.cs` (Web, **optional**) + `SentryAlertSink.cs` (Web, **optional**);
  F11.U3: `F11U3_DeadLetterAlertTests.cs` (Web.Tests)).
- **The NOT-in-scope boundary** — pin §6 (the `SentryAlertSink` is
  **optional**; the F12 human channel is **not** in F11; the F18 mail-provider
  alarms are **not** in F11; the close-flip is at **F22**; the
  `EmailDeadLetter` row / the `/health` degraded gate / the
  `EmailDeadLetterWriter` / the `ISmtpSender` / the `IMailerStage` are
  **untouched**; **no new** `<PackageReference>`).
- **The invariants held** — pin §7 (OFF by default; Core stays HTTP-free;
  the dead-letter write is UNCHANGED; best-effort at the boundary; the
  `HandleFault` signature is ADDITIVE-ONLY; no new bounded context /
  DocTypes surface; no new `<PackageReference>`; the F10 / F9 tests stay
  green; the `PostgresFixture` parallelism flake is a known issue).

**The pin is the contract, not the code.** F11.U2 / F11.U3 implement the
contract; they do **not** re-derive it. A drift in F11.U2 / F11.U3 (the
`Wolverine.Fault<T>` envelope construction shape, the `ISentryClient` /
`Sentry.CaptureException` access path, the `PostgresFixture` /
`SideEffectHarnessTests` harness wiring) is a **judgment call** (the §4b
harness gotcha precedent, the F10 pin §4b "the pin fixes the contract, not
the access path" idiom), **not** a drift pause (the F9.U1 / F9.U2 / F9.U3 /
F9.U4 / F10.U1 / F10.U2 / F10.U3 handoff notes' "no drift pause" precedent).
**The ONE exception** (a drift **pause**, the F9.U1 unit-series rule 4):
**IF** the `OutboxEmailHandler.HandleFault` signature has changed since the
pin (a new required parameter, a removed `fault.Message` / `fault.Exception` /
`fault.Attempts` read, a changed `EmailDeadLetterWriter.WriteAndCommitAsync`
contract), F11.U2 records `## F11.U2 — Drift pause` in the handoff note and
**stops** (the unit-series rule 4, the F9.U1 / F10.U1 "no drift pause"
precedent — a **codebase divergence** from the pin, NOT a pin-vs-SDK-API
factual error, which is a **judgment call**).
