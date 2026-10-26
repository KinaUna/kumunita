# F12 — OBS-06: route alerts to a human — Part-0 design pin

> **Fix:** F12 (report row **OBS-06**) — kind **`code+ops`**.
> **Status:** this is the **Part-0 design pin** (F12.U1). It is **design-only**:
> **no code, no build, no `.cs` touched, no `<PackageReference>`, no migration,
> no `.slnx` change, no build run** (the unit's exit contract). F12.U2 lands
> the `SupportEmailAlertSink` Web binding, F12.U3 the pinned test.
> The close-flip (six members) happens at **F22**, the lane's last unit —
> **not** F12.
> **Why it's a FAIL today:** the F11 alert seam (`IAlertSink`) fires the
> `AlertEvent` — but the **only binding is `NoopAlertSink`**, which does
> nothing. The terminal signal an operator gets is a **poll** of `/health`
> that returns `degraded` *if and when they happen to look* (the
> `EmailDeadLetter` count, OPS §8). There is **no push, no destination, no
> "a message has been dead for ~24 h" that reaches a human.** F12 is the fix
> that gives the F11 seam a **real channel**: the one that **reaches a human**.
> **The F11 dependency (honored):** F12 **depends on F11** (JOB-02, the
> `IAlertSink` seam). F11 is CODE-COMPLETE — the `IAlertSink` seam + the
> `AlertEvent` payload + the `NoopAlertSink` default binding + the
> `HandleFault` trigger wiring are all in place (`AlertSink.cs`,
> `DependencyInjection.cs:380`, `OutboxEmailHandler.HandleFault`). **F12 does
> NOT change any of that** — it **adds a second binding** (the channel that
> reaches a human) that **wins DI resolution** over the `NoopAlertSink`
> default (the F10.U2 / F11 registration-ordering precedent). The F11 open item
> (a) — *"point the sink at the F12 human channel … until F12 lands, F11's
> alert fires to the `NoopAlertSink` default (a no-op) — NOT to a human"* — is
> the exact gap this pin closes.

> **Repo doctrine applied (from `AGENTS.md` + ADR 0006 + the F10/F11 precedent):**
> *"Core stays HTTP-free"* (ADR 0006) — so the **seam + payload**
> (`IAlertSink`, `AlertEvent`) are **Core** (already landed, F11), and the
> **channel** (the thing that delivers to a human) is a **Web + owner-ops**
> concern. This is the **exact** `ISmtpSender` Core I/O-seam precedent: Core
> owns the interface + impl (`SmtpSender.cs`), the Web owns the registration
> + the transport split. F12's channel **reuses the Core `ISmtpSender`** — the
> same split, not a new seam. **No `<PackageReference>` is added in F12** (the
> SMTP transport is MailKit, already a Core dependency; the channel is a Web
> class that calls the existing Core `ISmtpSender`).

---

## 1. Current state (grounded, not assumed)

- **The `IAlertSink` seam is F11's, and it is in place.** `src/Kumunita.Core/Identity/AlertSink.cs`
  holds the **three** types, all Core (ADR 0006):
  - `public interface IAlertSink { Task ReportAsync(AlertEvent alert, CancellationToken ct = default); }`
    — the **one-payload + ct** shape (the `ISmtpSender.SendAsync(OutboxEmail, ct)`
    precedent, `SmtpSender.cs:41`). Best-effort-at-the-boundary contract: a
    sink/channel failure must **never** propagate into the caller's domain
    write path (the `EmailDeadLetter` row is the load-bearing operator signal,
    OPS §8; the alert is a *copy*, not the *signal*).
  - `public sealed record AlertEvent(string Kind, string Subject, string? Detail, int? Attempts);`
    — the **closed** payload list. `Kind` is a **`string`**, NOT an `enum` —
    F11's value is `"email.dead-letter"`; a new kind (F18's `"mail.bounce-alarm"`)
    is **open** without a type change.
  - `public sealed class NoopAlertSink : IAlertSink` — the **default** binding;
    `ReportAsync` is a pure `Task.CompletedTask` no-op (no network, no throw,
    no state — the off-by-default / F10 DSN-gated no-op idiom).
- **The `NoopAlertSink` is the ONLY binding today.** `src/Kumunita.Core/DependencyInjection.cs:380`
  — `services.AddTransient<IAlertSink, NoopAlertSink>();` (registered in
  `AddKumunitaCore`, the Core default). A repo-wide `grep -rn "IAlertSink" src/`
  shows the **only** registration is that one line + the `OutboxEmailHandler.HandleFault`
  method-injected **parameter** (`IAlertSink? alertSink = null`, nullable so a
  missing sink is a no-op — the off-by-default idiom). **There is no second
  binding.** F12 is the fix that adds the one.
- **The F10 `SENTRY_DSN` is unset by default (the standing deferral).**
  `src/Kumunita.Web/Program.cs:370-400` — the `if (!string.IsNullOrWhiteSpace(builder.Configuration["SENTRY_DSN"]))`
  block (the `UseSentry(o => …)` registration + the `SentryPiiScrubber`
  `SetBeforeSend` callback) is **gated**: unset DSN → the block is **not
  executed**, the pipeline is **byte-identical** to before the seam (the F10
  "OFF by default" idiom). So the F10 Sentry channel is a **no-op by default**
  and reaches a **dashboard**, not a human. **This is the standing deferral
  F12 must not depend on** (pin §2).
- **The `/health` degraded gate is the always-on signal (UNCHANGED).** The
  `EmailDeadLetter` count drives `/health`'s **degraded** state (OPS §8,
  `HealthController`). F11 and F12 are **additive** (a push alongside the poll);
  they **do not change** the gate. The `NoopAlertSink` default is the **floor**
  (the "no external telemetry by default" idiom honored by construction); the
  F12 channel is the **additive push** (pin §3).
- **The F11 open item (a) names the F12 channel as the one that replaces the no-op.**
  The F11.U3 handoff note: *"point the sink at the F12 human channel … until
  F12 lands, F11's alert fires to the `NoopAlertSink` default (a no-op) — NOT to a
  human."* **F12 is the unit that fills that open item** — it adds the one
  channel binding that **reaches a human**.
- **The `ISmtpSender` Core I/O-seam F12's channel reuses (the F10 precedent).**
  - `ISmtpSender` — the interface is **Core** (`src/Kumunita.Core/Identity/SmtpSender.cs:27`,
    `public interface ISmtpSender { Task SendAsync(OutboxEmail email, CancellationToken ct = default); }`),
    the impl (`SmtpSender`, MailKit-backed) is Core, the **registration is Core DI**
    (`DependencyInjection.cs`, `services.AddTransient<ISmtpSender, SmtpSender>();`).
    `SmtpSender.SendAsync` (line ~260) validates `Recipient` is non-empty
    (line ~265) and throws on transport failure (the `IAlertSink` boundary
    contract swallows that — pin §3).
  - `OutboxEmail` — the one-payload `SendAsync` takes
    (`src/Kumunita.Core/Identity/EmailMessages.cs:8`): `Id`, `IdempotencyKey`,
    `Recipient`, `Subject`, `Body`, `QueuedAt`. F12's channel **composes one of
    these** from the `AlertEvent` (pin §2c).
  - `CommunityOptions.SupportEmail` — the community's own support mailbox
    (`src/Kumunita.Core/CommunityOptions:16`, `public string? SupportEmail { get; set; }`),
    set by the seeder (the `SmtpSender` doc names it: "the resident-facing
    support mailbox per the seeder's `CommunityOptions.SupportEmail`"). **This
    is the human F12 routes to.** It is **not** an owner-ops gate the way
    `SENTRY_DSN` is — it is part of the platform's **own** email
    infrastructure (the same relay the durable outbox already uses).

---

## 2. The channel pin — **option (b): the `SupportEmail` SMTP channel** (F12.U2 lands it)

**The decision (pin §2.0): F12 pins the `SupportEmail` SMTP channel (option b),
NOT the Sentry channel (option a).** The plan's F12 row offers two options
("the F10 Sentry channel (`SentryAlertSink`, DSN-gated) **or** the community
`SupportEmail` SMTP (`ISmtpSender` already in Core) — **pin one**"). This pin
chooses **(b)** for the load-bearing reason below, and reuses the **existing
Core `ISmtpSender`** (not a new seam):

| Reason | The grounding |
|---|---|
| **(b) actually reaches a human** — which is F12's *purpose* | F12's report row is **OBS-06: "route alerts to a human."** The `SupportEmail` is the community's **own** support inbox — a real, in-repo, non-external destination that exists in **every functioning deployment** (the seeder sets it; the platform can send **no** email without it). Routing the alert there **reaches a human by default in practice.** The Sentry channel (a) reaches a **dashboard** (a third-party service the operator must have configured and must *look at*), **not** a human. **(b) closes OBS-06; (a) does not.** |
| **(a) is the external telemetry the idiom *defers*** | The "no external telemetry by default" standing deferral (F10/F11) is **specifically about Sentry** (a third-party error tracker). (a) would add a *second* DSN-gated no-op-by-default binding that still **does not reach a human** — it would just be a no-op that *could* reach a dashboard *if the owner configured Sentry*. That is the **F10 deferral re-asserted**, not F12's fix. **(b) is the platform's own email — first-class, not a telemetry deferral.** |
| **(b) reuses the existing Core `ISmtpSender` I/O-seam** | The `ISmtpSender` / `SmtpSender` / `OutboxEmail` / `CommunityOptions.SupportEmail` surface is **already in place** (F0/M1, the durable email lane). F12's channel is a **Web** class that composes an `OutboxEmail` and calls `ISmtpSender.SendAsync` — the **exact** "Core owns the interface + impl, Web owns the registration + transport split" the F10 precedent and the plan's F12 row name. **No new seam, no new package.** |
| **(b) is the channel F18 reuses** | F18 (MAIL-06, the mail-provider alarms, dep F12) needs the **same** channel pointed at the human. The `SupportEmail` SMTP channel is **kind-agnostic** (it routes **any** `AlertEvent`, pin §3) — a future `"mail.bounce-alarm"` kind rides the **same** binding. A Sentry binding would not (it is DSN-gated + external). |
| **DI ordering is identical for both** (the F10.U2 precedent) | Both are a **second** `IAlertSink` binding registered in `Program.cs` **after** `AddKumunitaCore` (which registers the `NoopAlertSink` default), so the F12 channel **wins DI resolution** when its gate is set — the F10.U2 / F11 registration-ordering precedent (pin §2d). The **gate** differs: (a) gates on `SENTRY_DSN`; (b) gates on `CommunityOptions.SupportEmail` non-empty. **(b)'s gate is met in every real deployment; (a)'s is the standing deferral.** |

> **Why the `NoopAlertSink` default is KEPT (not removed).** It is the **off-by-default floor**: an unconfigured instance (no `SupportEmail` set — a dev/scratch instance) resolves `IAlertSink` to `NoopAlertSink` → a no-op (the "no external telemetry by default" idiom honored by construction). A real deployment (the seeder set `SupportEmail`) resolves to the F12 channel → the alert **reaches the human**. The `NoopAlertSink` is the **floor**, the F12 channel is the **push** (pin §3). Removing the default would break the off-by-default idiom and every unconfigured-instance test that relies on a null/no-op sink.

### §2a — the F12 channel shape (the `SupportEmailAlertSink`, a **Web** class)

- **Name / location:** `SupportEmailAlertSink`, namespace **`Kumunita.Web.SideEffects`**
  (the same namespace as `OutboxEmailHandler` — the F11 `SentryAlertSink` location
  precedent; it is a Web channel, not a Core type). It is the **one** new `IAlertSink`
  impl.
- **Constructor (method-injected, the `SmtpSender` shape):**
  `ISmtpSender sender, Microsoft.Extensions.Options.IOptions<CommunityOptions> community,
  Microsoft.Extensions.Logging.ILogger<SupportEmailAlertSink> logger`.
  All three are **Core** types / BCL — the Web class depends on **no** new
  package (ADR 0006 — the channel is Web, its dependencies are Core).
- **`ReportAsync(AlertEvent alert, CancellationToken ct = default)`** — the
  **best-effort-at-the-boundary** impl (the `IAlertSink` contract, pin §1):
  - Compose **one** `OutboxEmail` from the `AlertEvent` (pin §2c):
    `Recipient = community.Value.SupportEmail`, `Subject = <operator-readable
    one-liner>` (the `AlertEvent.Kind` + `Subject`), `Body = <operator-readable
    detail>` (the `AlertEvent.Detail` + `Attempts`), `IdempotencyKey =
    $"alert:{alert.Kind}"` (the `Handle`/`SendAsync` guard requires `Recipient`
    non-empty, `SmtpSender.cs:265`; the key is a **U2 judgment call** — the pin
    fixes the contract, not the exact key scheme; it only needs to be non-empty).
  - **Guard:** if `community.Value.SupportEmail` is null/empty, **return
    `Task.CompletedTask`** (a no-op — the off-by-default floor realized by
    construction; belt-and-suspenders over the §2d registration gate).
  - **Call** `await sender.SendAsync(email, ct)`.
  - **Wrap in `try/catch`** (the `IAlertSink` contract, `AlertSink.cs` doc: "a
    sink/channel failure must NEVER propagate … the alert is a *copy*, not the
    *signal*"; the `OutboxEmailHandler.HandleFault` `try/catch` swallow
    precedent). A send failure is **swallowed** (a rethrow would turn an
    alert-failure into a dead-letter-write-failure, inverting the invariant).

### §2b — the `OutboxEmail` composition contract (the `AlertEvent` → `OutboxEmail` map)

| `AlertEvent` field | → `OutboxEmail` field | Note |
|---|---|---|
| — (not in the payload) | `Recipient` | **`community.Value.SupportEmail`** — the **human** (the seeder's resident-facing support mailbox). The guard (pin §2a) makes this a no-op if unset. |
| `Kind` + `Subject` | `Subject` | The operator-readable one-liner (e.g. `"Kumunita alert: email.dead-letter — <recipient>"`). **U2 judgment call** for the exact string; the pin fixes that `Kind` + `Subject` are present. |
| `Detail` + `Attempts` | `Body` | The operator-readable detail (the `LastError` context + the attempt count) — the OPS §7 re-queue input. **U2 judgment call** for the exact format; the pin fixes that `Detail` + `Attempts` are present. |
| — | `IdempotencyKey` | Non-empty (the `SendAsync`/`Handle` guard); **U2 judgment call** (e.g. `$"alert:{alert.Kind}"`). |
| — | `Id` | `Guid.NewGuid().ToString("N")` (the `OutboxEmail` doc shape). |
| — | `QueuedAt` | `DateTimeOffset.UtcNow` (the `OutboxEmail` doc shape). |

### §2c — the registration point + the DI gate (F12.U2 lands it in `Program.cs`)

- **The registration is a NEW block in `src/Kumunita.Web/Program.cs`** (NOT the
  F10 DSN-gated block — that is option (a)'s). It is placed **after
  `AddKumunitaCore()`** (line 322) — the F10 U2 / F9 U3 / M25 registration region
  — so it **wins DI resolution** over the `NoopAlertSink` default
  (`DependencyInjection.cs:380`) **when the gate is set** (the F10.U2
  registration-ordering precedent: the Web registration comes after
  `AddKumunitaCore`, so it wins).
- **The gate (the off-by-default lever):** `if (!string.IsNullOrWhiteSpace(communityOptions.SupportEmail))`
  (read from `builder.Configuration` / the `CommunityOptions` options — the
  **F10 DSN-gate idiom**, but the gate is the community's own `SupportEmail`,
  **not** an owner-ops external DSN). **Gate met (every real deployment) → the
  `SupportEmailAlertSink` is registered → it wins DI resolution → the alert
  reaches the human. Gate unmet (dev/scratch, no support mailbox) → NOT
  registered → the `NoopAlertSink` default fires → a no-op (off-by-default).**
- **The exact registration line (U2 judgment call on placement, the pin fixes the
  contract):** `services.AddTransient<IAlertSink, SupportEmailAlertSink>();` —
  a **second** `IAlertSink` binding (the `NoopAlertSink` default is **kept**,
  pin §3), registered **later** than the Core default so it **wins** (the F10.U2
  precedent). **No other `Program.cs` change** (the F10 Sentry block, the F9
  bot-check block, the M25 `UploadGate`, and every existing registration are
  **untouched**). **No other `DependencyInjection.cs` change** (the
  `NoopAlertSink` default is **kept** as-is at line 380).

### §2d — the DI ordering (the load-bearing leg of F12.U3's test)

- **Resolution order (MS DI, last-wins for a single `AddTransient` of the same
  service):** `AddKumunitaCore` registers `IAlertSink → NoopAlertSink` (Core
  default, `DependencyInjection.cs:380`). **Then** `Program.cs` (the F12 block,
  **after** line 322) registers `IAlertSink → SupportEmailAlertSink` (the gate
  set). **The F12 binding is registered LATER → it wins DI resolution** (the
  F10.U2 / F11 precedent, verbatim: "the `SentryAlertSink` (Web) is registered
  LATER in `Program.cs` … so it wins the DI resolution **only when** … set").
- **The F12.U3 load-bearing leg is this DI-ordering assert** (pin §4): with the
  gate set, resolving `IAlertSink` yields the **F12 binding** (a recording stub
  standing in for it), **NOT** the `NoopAlertSink`. This is a **DI-ordering
  assert, not a behavior assert** — the **behavior** (the `AlertEvent` is
  actually fired with the right payload) is **already covered** by F11.U3's
  `DeadLetter_FiresAlert` (which asserts the recording sink received the event
  with the exact `Kind`/`Subject`/`Detail`/`Attempts`). F12.U3's job is the
  **routing leg**: the F12 binding **wins** over the `NoopAlertSink` default.

---

## 3. The `AlertEvent` routing contract (what F12 does and does NOT change)

- **F12 routes EVERY `AlertEvent` kind to the configured channel.** The
  `SupportEmailAlertSink` is **kind-agnostic** (pin §2a — it composes an
  `OutboxEmail` from the `AlertEvent` fields, it does **not** switch on
  `Kind`). F11's `"email.dead-letter"` rides it **today**; F18's future
  `"mail.bounce-alarm"` rides the **same** binding (no new channel, no new
  seam — the `Kind`-as-`string` F11 pin §2b decision is the reason this works).
- **The `IAlertSink` interface is UNCHANGED.** F12 adds a **second** binding;
  it does **not** add a method, does **not** change the `ReportAsync` signature,
  does **not** change the best-effort-at-the-boundary contract. The
  `AlertEvent` payload is **unchanged** (the closed list is **F11's**, and
  F12's kind-agnostic routing is **why** the closed list is a `string` `Kind`,
  not an `enum`).
- **The `NoopAlertSink` default is KEPT.** It is the **off-by-default floor**
  (the "no external telemetry by default" idiom honored by construction) and the
  **dev/scratch-instance** binding (gate unmet). The F12 channel is the
  **additive push** (gate met). The two are **not** mutually exclusive in code —
  both registrations exist; DI ordering picks the F12 binding when the gate is
  set, the `NoopAlertSink` when it is not (pin §2d).
- **The `/health` degraded gate is UNCHANGED** (OPS §8) — F12 (like F11) is
  **additive** (a push alongside the poll); it does **not** touch the
  `EmailDeadLetter` row, the `/health` mapping, or the `EmailDeadLetterWriter`.
- **The `HandleFault` trigger is UNCHANGED** (F11's) — the `IAlertSink?
  alertSink = null` method-injected parameter, the `if (alertSink is not null)`
  guard, the `try/catch` swallow. F12 changes **what resolves for
  `IAlertSink`** (the DI binding), **not** the trigger. The **production**
  wiring that resolves `IAlertSink` is the **Web host's DI container**
  (`Program.cs` → the F12 binding), **not** the `HandleFault` parameter (which
  is **null** in the F11.U3 in-process test harness — the test injects its own
  stub). This is the F10/F11 split: the **test** drives the trigger with a
  stub; **production** resolves the real channel from DI.

---

## 4. The pinned test name + assertion contract (F12.U3 owns the file + harness)

> **Pinned test:** **`Alert_RoutedToConfiguredChannel`** in **`tests/Kumunita.Web.Tests/`**
> (a **new** file — the test is in `Kumunita.Web.Tests`, **NOT** `Kumunita.Core.Tests`
> — the F12 channel is a **Web** class, and `Kumunita.Core.Tests` does **not**
> reference `Kumunita.Web`. The F11.U3 precedent: `F11U3_DeadLetterAlertTests.cs`
> is a Web.Tests file for the same reason).

**The assertion contract (pin §4a):**

- **Leg-1 (the DI-ordering leg — the load-bearing leg):** with the F12 channel
  registered (the gate **set**), resolving `IAlertSink` from a DI container that
  has **both** the `NoopAlertSink` default (the `AddKumunitaCore` registration)
  **and** the F12 binding (the `Program.cs`-style registration) yields the
  **F12 binding** — **NOT** the `NoopAlertSink`. **This is a DI-ordering assert,
  NOT a behavior assert** (the behavior is F11.U3's `DeadLetter_FiresAlert`).
  The recording-stub `IAlertSink` (the F12 binding stand-in) is resolved, the
  `NoopAlertSink` is **not** (a `Assert.IsNotType<NoopAlertSink>(resolved)` /
  `Assert.IsType<RecordingAlertSink>(resolved)` shape — **U3 judgment call** on
  the exact assert, the pin fixes the **contract**: F12 binding wins).
- **Leg-2 (the routing leg, mirroring F11.U3 leg-2/leg-3):** an `AlertEvent`
  (a synthetic `"email.dead-letter"` — the **same** payload F11.U3 leg-3 asserts,
  so the pin is not re-deriving F11's contract) is fired through the resolved
  sink; the **recording sink received it** (the `AlertEvent` fields match — the
  routing works). **This leg is the "route to the configured channel" the test
  name promises.**
- **Leg-3 (the off-by-default floor, the §3 invariant):** with the gate
  **unset** (the `SupportEmail` unconfigured — the dev/scratch shape), resolving
  `IAlertSink` yields the **`NoopAlertSink`** default (the off-by-default floor
  is **kept**, pin §3). A no-op (a `Assert.IsType<NoopAlertSink>(resolved)` /
  "fired but sent nothing" shape — **U3 judgment call**, the pin fixes the
  **contract**: the default is the floor).

> **The load-bearing leg is leg-1** (the F12 binding **wins** over the
> `NoopAlertSink` default — the routing works because the DI ordering puts the
> F12 channel first). Legs 2 and 3 confirm the **contract** (the event is routed
> + the off-by-default floor is kept) but the **routing** is leg-1.
>
> **The harness gotcha (the F10 pin §4b / F11 pin §4b "pin fixes the contract,
> not the access path" idiom):** how the DI container is built for the
> leg-1/leg-3 asserts (a **hand-rolled** `ServiceCollection` with the two
> `AddTransient` calls in the **correct order** — the `NoopAlertSink` default
> **first** (the `AddKumunitaCore` shape), the F12 binding **second** (the
> `Program.cs` shape) — is a **U3 judgment call**; the pin fixes the **contract**
> (F12 binding registered later wins), not the **access path** (the exact
> `ServiceCollection` / `ServiceProvider` wiring). The `RecordingAlertSink`
> stub is the **same** one F11.U3 used (`F11U3_DeadLetterAlertTests.cs` — the
> F12 test **reuses** it, not re-derives it). **The `SupportEmailAlertSink` real
> class is NOT tested in F12.U3** (its `ISmtpSender.SendAsync` call is the F11
> "Core seam, Web channel" split — the **real** SMTP send is an **owner-ops**
> concern (pin §7), not a unit-test concern; the DI-ordering + routing contract
> is the F12 test's job, and it is covered by the **recording stub** standing in
> for the channel).

---

## 5. The file list (per unit)

- **F12.U1 (this pin):** `docs/design/prod-hardening-f12-alerts.md` (this file).
  **No `src/**` touched, no csproj touched, no `<PackageReference>` added, no
  build run.**
- **F12.U2 (the channel sink, ≤2 files, Web):**
  - `src/Kumunita.Web/SideEffects/SupportEmailAlertSink.cs` (**NEW**) — the
    `IAlertSink` impl (pin §2a): the `ISmtpSender`-backed, `OutboxEmail`-composing,
    `try/catch`-swallowing, `SupportEmail`-guarding Web class (the
    `SmtpSender.cs` / `OutboxEmailHandler.cs` in-the-WEB-project / Core-seam
    precedent).
  - `src/Kumunita.Web/Program.cs` — the **one** new gated registration block
    (pin §2c): the `if (SupportEmail non-empty) { AddTransient<IAlertSink,
    SupportEmailAlertSink>(); }` block, placed **after** `AddKumunitaCore()`
    (line 322). **No other `Program.cs` change.**
  - **NOT in scope:** the `NoopAlertSink` default (KEPT, `DependencyInjection.cs:380`
    — **untouched**), the `IAlertSink` interface (UNCHANGED), the `AlertEvent`
    payload (UNCHANGED), the `HandleFault` trigger (UNCHANGED, F11's), the
    `EmailDeadLetter` row / the `/health` degraded gate (UNCHANGED), **no new**
    `<PackageReference>` (the SMTP transport is MailKit, already a Core
    dependency; the `Kumunita.Core.csproj` is **untouched**, the
    `Kumunita.Web.csproj` is **untouched**), **no** new bounded context / DocTypes
    surface.
- **F12.U3 (the test, ≤1 file, Web.Tests):**
  - `tests/Kumunita.Web.Tests/F12U3_AlertRoutedToChannelTests.cs` (**NEW**) — the
    pinned `Alert_RoutedToConfiguredChannel` (pin §4a legs 1–3) + the
    `RecordingAlertSink` stub **reused** from F11.U3 (the `NoopAlertSink` is the
    **real** Core default, not a stub — the leg-3 assert resolves the **real**
    `NoopAlertSink`). **No `src/**` change.**

---

## 6. Build/test command (the repo test-runner quirk)

> **The repo doctrine (from `AGENTS.md` + the F11 handoff notes):** on this
> machine `dotnet test` discovery is **broken** (the xunit.v3 / VSTestBridge
> "No tests found to run" / "Zero tests ran / Exit code: 5" quirk) — the
> reliable path is to **build**, then run each test **assembly in-process**
> through xunit.v3's own runner. **F12.U1 does NOT run either** (design-pin
> unit; the exit contract is NO build, NO test run — the F11.U1 idiom).

```bash
dotnet build Kumunita.slnx -c Debug
dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll
```

- **F12.U2 / F12.U3 run the above** (U2: the channel sink + the `Program.cs`
  registration; U3: the test + the no-regression confirmation). **F12.U1 does
  NOT.**
- **`Kumunita.Web.Tests`** runs in well under a second. **`Kumunita.Core.Tests`**
  takes ~160–171 s (Testcontainers `postgres:18` boot; **slower than the
  documented ~20 s** — a machine-speed note, not a regression, per the F11.U3
  handoff note) and **leaves Docker containers behind if killed** — **clean up
  with `docker container prune -f` after** (the repo doctrine).

---

## 7. Open items (owner, NOT assumed done)

- **(a) Confirm a real recipient is configured on prod.** The F12 channel
  routes to `CommunityOptions.SupportEmail` (the seeder's resident-facing
  support mailbox). The owner must confirm the **prod** `SupportEmail` is set to
  a **real, monitored** address (a mailbox a human actually reads) — otherwise
  the gate (pin §2c) is met (the seeder sets it) but the **destination** is not
  monitored (the alert reaches a mailbox no one checks — OBS-06 still not
  effectively closed). **This is the F12 owner-ops step — recorded, not assumed.**
  (If the owner has **no** support mailbox on prod, the gate is **unmet** → the
  `NoopAlertSink` default fires → a no-op (the off-by-default floor, pin §3) —
  the `/health` degraded gate is the only signal, and OBS-06 is **not** closed.
  The owner must **either** set a real `SupportEmail` **or** accept the
  poll-only posture and record it.)
- **(b) The F10 `SENTRY_DSN` deferral STILL STANDS (option (a) not chosen).**
  The pin chose **(b)** (the `SupportEmail` SMTP channel), **NOT** the Sentry
  channel (option (a)). So the F10 `SENTRY_DSN` owner-ops deferral is **unchanged
  by F12** — it is still a **standing deferral** (unset → the F10 Sentry block
  is a no-op, the pipeline is byte-identical to before the seam). **If the owner
  later wants the Sentry channel too** (option (a), a **second** binding,
  DSN-gated), it is a **separate, additive** unit (register the
  `SentryAlertSink` inside the F10 DSN-gated block — the F11.U1 §2d
  `SentryAlertSink` design, which F11.U2 deliberately did **not** land). It is
  **NOT** part of F12 (F12 is the **human** channel; the Sentry channel is the
  **dashboard** channel the F10 deferral already covers). **The owner's
  awareness** of this split (F12 = the human channel / the `SupportEmail`;
  F10 = the external Sentry dashboard / the `SENTRY_DSN`) is required (the
  F10.U2 / F11.U3 open-item (b) idiom).

---

## 8. The invariants held (the F11 pin §7 idiom, F12-specific)

- **OFF by default (the floor):** the `NoopAlertSink` default is **kept**
  (`DependencyInjection.cs:380`) — an unconfigured instance (no `SupportEmail`)
  resolves to a no-op (no network, no throw, no state — the off-by-default
  idiom honored by construction). The F12 channel is the **additive push** (gate
  met), not a replacement of the floor.
- **Core stays HTTP-free** (ADR 0006) — the **seam + payload** are Core
  (F11's, **untouched**); the **channel** is a **Web** class (`Program.cs` + the
  `SupportEmailAlertSink`), depending on the **Core** `ISmtpSender` /
  `CommunityOptions` (the `ISmtpSender` Core I/O-seam / Web transport split).
- **The `IAlertSink` interface is UNCHANGED** (no new method, no signature
  change, no contract change). The `AlertEvent` payload is **UNCHANGED**
  (F11's closed list; F12's kind-agnostic routing is **why** it is a `string`
  `Kind`, not an `enum`).
- **The `HandleFault` trigger is UNCHANGED** (F11's — the `IAlertSink?
  alertSink = null` parameter, the `if (alertSink is not null)` guard, the
  `try/catch` swallow). F12 changes **what resolves for `IAlertSink`** (the DI
  binding), **not** the trigger.
- **The `/health` degraded gate is UNCHANGED** (OPS §8) — F12 is **additive**
  (a push alongside the poll); it does **not** touch the `EmailDeadLetter` row
  / the `/health` mapping / the `EmailDeadLetterWriter`.
- **Best-effort at the boundary** — the `SupportEmailAlertSink`'s
  `ISmtpSender.SendAsync` call is wrapped in `try/catch` (a sink/channel failure
  is **swallowed** — the `IAlertSink` contract, `AlertSink.cs` doc: "a
  sink/channel failure must NEVER propagate"; a rethrow is **FORBIDDEN**).
- **No new `<PackageReference>`** (the SMTP transport is MailKit, already a
  Core dependency; the `Kumunita.Core.csproj` is **untouched**, the
  `Kumunita.Web.csproj` is **untouched**).
- **No new bounded context / DocTypes surface** (the channel is a **Web** class
  reusing the **existing** Core `ISmtpSender` / `OutboxEmail` /
  `CommunityOptions.SupportEmail` — no new domain, no new doc, no new surface).
- **No migration, no `.slnx` change, no `.cs` change in U1** (the design-pin
  exit contract).
- **The F10 `Error_TrackerRegisteredWhenDsnSet` test stays green** (the F10
  Sentry block is **untouched**); the **F11.U3 `DeadLetter_FiresAlert` test
  stays green** (the `HandleFault` trigger + the `NoopAlertSink` default are
  **untouched**); the **F9.U4 `Signup_DisposableEmail_Rejected` test stays
  green** (the F9 bot-check block is **untouched**).
