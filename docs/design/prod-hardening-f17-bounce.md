# F17 — MAIL-03: bounce/complaint suppression — Part-0 design pin

> **F17.U1** design pin (Tier 1). F17.U2 and F17.U3 execute against this file.
> No code, no build, no test run — the pin is the deliverable.
>
> Plan entry: `plan-prod-hardening.md` F17 (MAIL-03, `code+ops`).
> Precedent: F16.U1 / F15.U1 / F14.U1 / F12.U1 design-pin idiom.

---

## §1 — The `SuppressedAddress` doc shape

A new POCO `SuppressedAddress` (namespace `Kumunita.Core.Notifications`, the
same namespace as `Notification` / `NotificationPreference` /
`NotificationService` — the mail-out bounded context).

**Pinned fields (closed list — no more, no fewer):**

| Field | Type | Notes |
|---|---|---|
| `Id` | `string` | The **lowercased** recipient email address. This is the document identity — one row per suppressed address. Marten's default string-Id convention (the `MediaObject` / `Notification` precedent: externally-assigned string key, no auto-generation). |
| `Reason` | `string` | One of three **closed** values: `"bounce"`, `"complaint"`, `"manual"`. The ingest (U2/U3) writes `"bounce"` or `"complaint"` from the provider callback; the owner's admin action (if one is added later) writes `"manual"`. The gate does not read this field — it is for operator visibility only. |
| `Created` | `DateTimeOffset` | `DateTimeOffset.UtcNow` at write time. |
| `Source` | `string` | The **provider name** that reported the bounce/complaint (e.g. `"mailpit"`, `"ses"`, `"postal"`) **or** the literal `"manual"` for a human-initiated suppression. Pin: the ingest handler sets this from the provider's callback envelope; the U2 manual-write path (if the owner adds one) sets `"manual"`. |

**No other fields.** The doc is intentionally minimal — a flat lookup table,
not a rich event log. Re-suppression of the same address is a **no-op**
(idempotency rule, §4): a `Store` on an existing `Id` overwrites `Reason`/
`Created`/`Source` with the latest values (Marten's `Store` semantics —
last-write-wins on a same-Id document).

### §1.1 — Doc-surface registration (PIN: `NotificationDocTypes`)

`SuppressedAddress` is registered on the **existing** `NotificationDocTypes`
surface — a one-line addition to `NotificationDocTypes.Configure(StoreOptions opts)`:

```csharp
// In NotificationDocTypes.Configure(StoreOptions opts), alongside the existing
// Schema.For<Notification>() / Schema.For<NotificationPreference>() lines:
opts.Schema.For<SuppressedAddress>();
```

**Rationale (grounded, not invented):**
- `NotificationDocTypes.cs` already exists (`src/Kumunita.Core/NotificationDocTypes.cs`)
  and hosts `Notification` + `NotificationPreference` — the mail-out bounded
  context's documents. `SuppressedAddress` is the same bounded context
  (recipient-level mail suppression).
- The gate is in `NotificationService` (namespace `Kumunita.Core.Notifications`)
  — the same assembly + namespace. Co-locating the doc on the same surface
  keeps the `NotificationService` test harness's boot setup unchanged (it
  already calls `NotificationDocTypes.Configure(opts)` — the F16.U3
  handoff-note's "GOTCHA 1" precedent: a doc on a surface the boot store
  omits → unmanaged-table error at runtime).
- **No new `*DocTypes.cs` file.** No new `Program.cs` wiring line. No new
  `using` in the test harness. This is the minimal additive choice.
- **NOT** on `CoreDocTypes` (that surface is Identity/UserInfo/AccessAudit —
  a different bounded context). **NOT** on `MediaDocTypes` (media catalog).

**Boot wiring (U2's concern, not U1's):** `NotificationDocTypes.Configure(opts)`
is **already called** in `src/Kumunita.Web/Program.cs` (the schema-bootstrap
block, alongside `CoreDocTypes.Configure(opts)` / `PostDocTypes.Configure(opts)` /
`MediaDocTypes.Configure(opts)`). The one-line `Schema.For<SuppressedAddress>()`
addition to `NotificationDocTypes.Configure` is picked up automatically at the
next boot — **no `Program.cs` change needed** (the existing call already
invokes `NotificationDocTypes.Configure`).

### §1.2 — Index

**No business-key index required.** `Id` is the document identity (the
lowercased email) — Marten's default Id-unique mapping is the lookup path
(the `MediaObject` precedent: "no business-key index required" per
`MediaDocTypes.cs` doc-comment). The gate's read is a single `LoadAsync(id)`
by the lowercased email — O(1), no index needed.

---

## §2 — The gate insertion point (PIN: `NotificationService.EmitAsync`, before `StageAsync`)

### §2.1 — The exact seam (grounded, not invented)

The gate fires in **`NotificationService.EmitAsync`** (the ADR 0084 overload —
the one with `targetId`/`linkPath`/`acceptPath`/`declinePath`), **immediately
before the `await _mailer.StageAsync(...)` call** at
`src/Kumunita.Core/Notifications/NotificationService.cs:361`:

```csharp
// Current line 361 (the email nudge — the sole StageAsync call in this method):
await _mailer.StageAsync(session, idempotencyKey, profile.Email!, subject, emailBody, ct).ConfigureAwait(false);
```

**Pinned insertion (U2 implements; the pin describes the shape):**

```csharp
// (5c) F17 — bounce/complaint suppression gate (additive, after the
//      ADR 0078 sample-suppression gate, before the StageAsync call).
//      Read-only lookup: is this recipient's email on the SuppressedAddress
//      list? If yes, skip the email — the inbox row is already stored (D7 —
//      the inbox is the durable record; the email is the best-effort nudge).
//      No new audit row (the mail-out path's existing opt-out at line ~238
//      is unchanged; this is a parallel, additive gate).
//      No SaveChangesAsync here — the gate is a pure read (LoadAsync),
//      and the skip is a return (the caller's own SaveChangesAsync commits
//      the inbox row + any domain write it is performing).
if (profile is not null && !string.IsNullOrWhiteSpace(profile.Email))
{
    var suppressed = await session.LoadAsync<SuppressedAddress>(
        profile.Email.Trim().ToLowerInvariant(), ct).ConfigureAwait(false);
    if (suppressed is not null)
        return notification;   // F17 — suppressed recipient: inbox row stored, email skipped
}

await _mailer.StageAsync(session, idempotencyKey, profile.Email!, subject, emailBody, ct).ConfigureAwait(false);
```

### §2.2 — Why this exact point (grounded)

- **It is the per-recipient mail-out path.** The `NotificationService.EmitAsync`
  is the highest-volume recipient-facing email path in the repo (group posts,
  event RSVPs, reports, to-dos, group invitations — all funnel through here).
  The other `StageAsync` call sites (`IdentityService.cs:182/244` verification
  emails, `EventReminderService.cs:221` reminders, `NotificationFlushService.cs:170`
  deferred-email flush) are **lower-volume** and serve **known-good** recipients
  (verification = the user just typed their own email; reminders = an
  already-registered user; flush = a deferred email for a recipient who
  already opted in). The pin gates the **primary** path; the others can adopt
  the same `LoadAsync` check in a follow-up if the owner wants full coverage
  (recorded as an open item, not assumed).
- **It is additive, not a re-shape.** The existing gates in `EmitAsync`
  (ADR 0084 per-target subscription gate at step 0, ADR 0078 sample-suppression
  gate at step 2a, dedup at step 1) are **untouched**. The F17 gate is a new
  step (5c) inserted **after** the inbox row is stored (step 3) and the
  deferred-quiet check (step 5) and **before** the `StageAsync` call (step 5's
  final line). The inbox row is always stored (D7 — the inbox is the durable
  record); the email is the best-effort nudge.
- **It is a read-only lookup.** `session.LoadAsync<SuppressedAddress>(id)` —
  no write, no new audit row, no `SaveChangesAsync` in the gate itself. The
  skip is a `return notification` (the inbox row is already stored; the
  caller's own `SaveChangesAsync` commits it). This matches the ADR 0078
  sample-suppression gate's shape (a `return null` short-circuit) — except
  F17 returns `notification` (not `null`) because the inbox row **is** stored
  (D7), only the email is skipped.
- **Core stays HTTP-free (ADR 0006-D).** The gate is in `Kumunita.Core`
  (`NotificationService`), a read-only `LoadAsync` on the caller's session.
  No HTTP types, no middleware. The ingest route (the provider webhook) is in
  `Kumunita.Web` — a separate concern, §3.

### §2.3 — The gate does NOT write a new audit row

The plan's invariant: "the gate is a read-only lookup … do NOT write a new
audit row — the mail-out path already has its own opt-out at
`NotificationService.cs:121`." Grounded: the ADR 0078 sample-suppression
gate (line ~238) is a `return null` (no inbox row, no email, no audit row).
The ADR 0084 per-target subscription gate (step 0) is a `return null` (same
shape). Neither writes an `AccessAudit` row. The F17 gate follows the same
shape: a read + a return, no write. **No `AccessAudit` row is created.**
The `SuppressedAddress` row itself is the record (written by the ingest, §3,
or a future manual lane — not by the gate).

---

## §3 — The provider-callback ingest (PIN: route + handler shape)

### §3.1 — The route (PIN: `POST /api/mail/suppress`)

The provider's bounce/complaint webhook posts to:

```
POST /api/mail/suppress
```

**Content-Type:** `application/json`

**Request body (the pin's shape — the provider's envelope is mapped to this):**

```json
{
  "email": "recipient@example.com",
  "reason": "bounce" | "complaint",
  "source": "ses" | "postal" | "mailpit" | "other-provider-name"
}
```

- `email` — the recipient's email address (the provider reports the address
  that bounced/complained). Required.
- `reason` — `"bounce"` or `"complaint"`. Required. (The ingest does not
  accept `"manual"` — that is a future admin-lane concern, not the
  provider-callback path.)
- `source` — the provider name (free-form string, lowercased for storage).
  Required.

**Response:** `204 No Content` on success (the `SuppressedAddress` row is
stored). `400 Bad Request` on a missing/invalid `email` or `reason`.

**Authentication (PIN: the same seam as the existing webhooks):** The route
requires a **shared-secret header** (e.g. `X-Suppression-Token: <secret>`),
where `<secret>` is read from the app's configuration
(`MailSuppression__IngestToken`). The token is **not** an ASP.NET Identity
credential — it is a shared secret between the provider's webhook and the
app, the same pattern as the existing `EscalationToken` / `GuestAccess`
token seams in the repo (the `EscalationDocTypes` / `GuestAccess` precedent:
a per-instance shared secret, not a per-user credential). The pin does not
prescribe the exact header name or the config key beyond `MailSuppression__IngestToken`
— U2 picks the exact name (a one-line config bind in `Program.cs` + a
middleware or attribute check on the route).

**CORS:** none (this is a server-to-server webhook, not a browser call).

**Rate limiting:** not required (the provider's webhook is not a
user-facing lane; the F8 `upload` rate-limit policy does not apply).

### §3.2 — The handler shape (PIN: `ISuppressionService.SuppressAsync`)

A new service `ISuppressionService` (interface) + `SuppressionService`
(implementation) in `Kumunita.Core.Notifications` (the same namespace as
`NotificationService` — the mail-out bounded context).

```csharp
// Kumunita.Core/Notifications/ISuppressionService.cs (U2 creates)
namespace Kumunita.Core.Notifications;

public interface ISuppressionService
{
    /// <summary>
    /// Suppress a recipient's email (idempotent — re-suppression overwrites
    /// Reason/Created/Source with the latest values, the §1 idempotency rule).
    /// One SaveChangesAsync (C3 single-write-lane).
    /// </summary>
    Task SuppressAsync(
        string email,          // the recipient's email (stored lowercased)
        string reason,         // "bounce" | "complaint"
        string source,         // the provider name (stored lowercased)
        CancellationToken ct = default);

    /// <summary>
    /// Read-only lookup: is this email suppressed? (The §2 gate calls this
    /// — or inlines the LoadAsync directly, U2's choice; the pin pins the
    /// shape, not the implementation detail.)
    /// </summary>
    Task<bool> IsSuppressedAsync(string email, CancellationToken ct = default);
}
```

**The gate (§2.1) may call `IsSuppressedAsync` or inline the `LoadAsync`** —
U2's choice. The pin pins the **shape** (a read-only lookup by lowercased
email, returning a boolean / a `SuppressedAddress?`) and the **idempotency
rule** (re-suppression is a no-op write — `Store` on an existing `Id`
overwrites the fields). U2 picks the implementation.

### §3.3 — The Web route (PIN: `Kumunita.Web/Controllers/MailSuppressionController.cs`)

A new `MailSuppressionController` (or a minimal-API endpoint — U2's choice;
the pin pins the **route** `POST /api/mail/suppress` and the **handler
contract** (the JSON body shape, the 204/400 responses, the shared-secret
auth), not the implementation style).

```csharp
// Kumunita.Web/Controllers/MailSuppressionController.cs (U2 creates)
[ApiController]
[Route("api/mail")]
public sealed class MailSuppressionController : ControllerBase
{
    private readonly ISuppressionService _suppression;
    // ...

    [HttpPost("suppress")]
    public async Task<IActionResult> Suppress([FromBody] SuppressRequest req, CancellationToken ct)
    {
        // 1. Validate: email non-empty, reason in {"bounce","complaint"}, source non-empty.
        // 2. Check the shared-secret header (MailSuppression__IngestToken).
        // 3. await _suppression.SuppressAsync(req.Email, req.Reason, req.Source, ct);
        // 4. return NoContent();   // 204
    }
}
```

**Core stays HTTP-free (ADR 0006-D):** `ISuppressionService` /
`SuppressionService` are in `Kumunita.Core` (no HTTP types, no
`IActionResult`, no `HttpContext`). The Web controller is the only place a
`IActionResult` / `HttpContext` appears. The DI registration
(`services.AddTransient<ISuppressionService, SuppressionService>()`) is in
`Kumunita.Core/DependencyInjection.cs` (the `AddKumunitaCore` block — the
same "AddTransient with the store injected" shape as the existing
`INotificationService` / `ISiteContentService` / `ISurfaceLabelsService`
registrations).

### §3.4 — The idempotency rule (PIN: last-write-wins)

Re-suppression of the same email address (same or different
reason/source) is a **no-op write** — `session.Store(new SuppressedAddress {
Id = email.ToLowerInvariant(), Reason = reason, Created = DateTimeOffset.UtcNow,
Source = source })` on an existing `Id` **overwrites** `Reason`/`Created`/
`Source` with the latest values (Marten's `Store` semantics — last-write-wins
on a same-Id document). **No duplicate row.** The gate's `LoadAsync(id)`
returns the single row with the latest values. This is the same idempotency
pattern as the `OutboxEmail.IdempotencyKey` dedup (the `NotificationService`
step 1 gate: a same-key row already stored → no second row, no second email)
— except here the key is the email address itself (the document `Id`), not a
separate idempotency-key field.

### §3.5 — The code+ops seam (PIN: the owner-ops step)

**The code unit (U2/U3) lands the ingest endpoint + the gate + the pinned
test.** The **owner-ops step** is:

> **Point the production mail provider's bounce/complaint webhook at
> `POST https://<instance>/api/mail/suppress`** with the shared-secret
> header set to the value of `MailSuppression__IngestToken` in the
> production configuration. Confirm the provider delivers at least one
> test bounce to the endpoint (the owner's provider console or a
> test-bounce tool).

**This step is recorded as an open item, NOT assumed done.** (Same pattern
as F16.U2/U3's "run ClamAV on the prod volume" open item, F10.U3's "set
`SENTRY_DSN` on the prod app" open item, F12.U3's "confirm a real
recipient is configured on prod" open item.) Until the owner runs this,
the suppression list is **empty** (no provider callbacks arriving) and the
gate is a no-op (no `SuppressedAddress` rows → `LoadAsync` returns `null` →
the email is sent). The `nosniff` + `attachment` serve mitigation (which
stays) is the active control.

---

## §4 — The pinned test (PIN: `SuppressedAddress_NotMailed`)

### §4.1 — The test name (UNRENAMED — the next audit run diffs against it)

**`SuppressedAddress_NotMailed`**

### §4.2 — The test shape (PIN: the three legs)

A new test file `tests/Kumunita.Core.Tests/F17U3_SuppressionGateTests.cs`
(U3 creates — the test is a **Core** test, not a Web test, because the gate
is in `Kumunita.Core` / `NotificationService`).

**Harness (the pin's shape, grounded in the F16.U3 / F15.U3 / F12.U3
precedent — direct-controller-construction over a real `PostgresFixture`
store):**

1. **Boot a real `DocumentStore`** with `CoreDocTypes.Configure(opts)` +
   `PostDocTypes.Configure(opts)` + `MediaDocTypes.Configure(opts)` +
   **`NotificationDocTypes.Configure(opts)`** (the §1.1 pin — the
   `SuppressedAddress` doc is on this surface; the F16.U3 GOTCHA-1
   precedent: a doc on a surface the boot store omits → unmanaged-table
   error).
2. **Plant a `SuppressedAddress` row** for the test recipient's email
   (e.g. `suppressed@example.com`):
   ```csharp
   await session.StoreAsync(new SuppressedAddress
   {
       Id = "suppressed@example.com",
       Reason = "bounce",
       Created = DateTimeOffset.UtcNow,
       Source = "test"
   }, ct);
   await session.SaveChangesAsync(ct);
   ```
3. **Construct a real `NotificationService`** (it is sealed — NSubstitute
   cannot proxy it, the F16.U3 precedent) with:
   - The real `DocumentStore` (the boot store).
   - A **recording `IMailerStage` stub** (a small private class that counts
     calls and stores nothing — the pin's "recording IMailerStage stub
     asserts 0 calls"):
     ```csharp
     private sealed class RecordingMailer : IMailerStage
     {
         public int StageAsyncCount { get; private set; }
         public Task StageAsync(IDocumentSession session, string idempotencyKey,
             string recipient, string subject, string body, CancellationToken ct = default)
         {
             StageAsyncCount++;
             return Task.CompletedTask;   // no-op: no row stored, no envelope enqueued
         }
     }
     ```
   - An `IUserInfoService` stub (NSubstitute) that returns a `Profile` with
     `Email = "suppressed@example.com"` for the test recipient.
   - A `ITranslationProvider` stub (NSubstitute) that returns fixed strings
     for the `notification.{kind}.subject` / `notification.{kind}.body` keys.
   - `VerificationOptions` with `SuppressForSampleAccountsInProduction = false`
     (the ADR 0078 sample-suppression gate is **disabled** — the test is
     driving the **F17** gate, not the ADR 0078 gate).
   - `ISubscriptionService` (or the `IsSubscriptionEnabledForAsync` seam)
     stubbed to return `true` (the ADR 0084 per-target gate is **disabled** —
     the test is driving the **F17** gate).
4. **Call `EmitAsync`** for the suppressed recipient:
   ```csharp
   await svc.EmitAsync(
       session,
       recipientId: "user-suppressed",
       kind: "group.post",
       idempotencyKey: "notification:group.post:test-suppressed",
       body: "A test post.",
       ct: ct);
   ```
5. **Assert the three legs:**

   | Leg | Assert | Why |
   |---|---|---|
   | **1. The email is NOT staged** | `Assert.Equal(0, mailer.StageAsyncCount)` | The gate fired: the `LoadAsync` returned the `SuppressedAddress` row → the `return notification` short-circuited **before** the `StageAsync` call. |
   | **2. The inbox row IS stored** | `var inbox = (await session.Query<Notification>().Where(n => n.IdempotencyKey == "notification:group.post:test-suppressed").FirstOrDefaultAsync(ct)); Assert.NotNull(inbox)` (query by `IdempotencyKey` — `LoadAsync` loads by the document `Id` (a Guid), not the key) | D7 — the inbox is the durable record; the email is the best-effort nudge. The gate skips the email, not the inbox row. |
   | **3. No `AccessAudit` row is written** | `Assert.Empty((await session.Query<AccessAudit>().Where(a => a.Action.Contains("suppress")).ToListAsync(ct)))` (or a more precise filter on the actor/subject) | The plan's invariant: the gate is a read-only lookup, no new audit row. (The ADR 0078 / ADR 0084 gates also write no audit row — the F17 gate follows the same shape.) |

**The test does NOT assert** the fail-open / no-row path (a recipient with
**no** `SuppressedAddress` row → the email **is** sent). That path is the
**default** (the gate is a no-op when the row is absent) and is implicitly
covered by every other existing `NotificationService` test (they all drive
the send path with no suppression row present). A separate
`SuppressedAddress_NotPresent_EmailStillSent` test is **optional** (U3's
choice) — the pin pins the `SuppressedAddress_NotMailed` name (the
UNRENAMED pin) and the three legs above.

### §4.3 — Why a Core test, not a Web test

The gate is in `Kumunita.Core` (`NotificationService.EmitAsync`). The
pinned test drives the **gate's behavior** (the `LoadAsync` lookup + the
`return notification` short-circuit), not the Web route (the ingest
endpoint). The ingest endpoint is a thin Web wrapper over
`ISuppressionService.SuppressAsync` — its correctness (the 204/400
responses, the shared-secret check) is a **different** concern (U2's
concern, not U3's pinned test). The pin's test name
(`SuppressedAddress_NotMailed`) is about the **gate**, not the ingest.
This matches the F16.U3 precedent: `InfectedUpload_NotServed` is a Web test
because the gate is in the Web `AttachmentController.Serve` method. Here
the gate is in Core, so the test is a Core test.

---

## §5 — Invariants (from `AGENTS.md` + ADR 0006 + the plan)

| # | Invariant | How F17 holds it |
|---|---|---|
| 1 | **Core stays HTTP-free (ADR 0006-D)** | The gate is in `Kumunita.Core` (`NotificationService`), a read-only `LoadAsync`. The ingest route is in `Kumunita.Web` (`MailSuppressionController`). `ISuppressionService` / `SuppressionService` have no HTTP types. |
| 2 | **C3 single-write-lane** | The `SuppressedAddress` write is one `SaveChangesAsync` (the `SuppressionService.SuppressAsync` method). The gate is a **read** (no write). The existing `EmitAsync` write path (the inbox row + the `StageAsync`) is **unchanged**. |
| 3 | **The existing per-recipient opt-out is unchanged** | The ADR 0078 sample-suppression gate (`NotificationService.cs:238`) and the ADR 0084 per-target subscription gate (step 0) are **untouched**. The F17 gate is a **new** step (5c) inserted after them. The dedup gate (step 1) is **untouched**. |
| 4 | **The mail-out path's existing error handling is untouched** | The `StageAsync` call site (line 361) is **unchanged** — the F17 gate is inserted **before** it, not in place of it. The `OutboxEmailStager.StageAsync` method is **untouched**. The `SmtpSender` / `OutboxEmailHandler` (the durable-send path) is **untouched**. |
| 5 | **The pinned test name is not a placeholder** | `SuppressedAddress_NotMailed` — a concrete, descriptive name. The next audit run diffs against it. |
| 6 | **The owner-ops step is recorded, not assumed done** | The provider-webhook wiring (§3.5) is an **open item** in the handoff note. Until the owner runs it, the suppression list is empty and the gate is a no-op. |
| 7 | **Server-rendered stack (no SPA)** | The ingest route is a minimal JSON endpoint (a webhook), not a user-facing page. The gate is a server-side read. No client-side JS. |
| 8 | **Audit-by-default** | The gate writes **no** `AccessAudit` row (the plan's invariant, §2.3). The `SuppressedAddress` row itself is the record (the operator can query it). This is consistent with the ADR 0078 / ADR 0084 gates (neither writes an audit row). |
| 9 | **No new bounded context** | `SuppressedAddress` is in the existing `Notifications` bounded context (namespace `Kumunita.Core.Notifications`, surface `NotificationDocTypes`). No new context, no new `DependencyInjection` feature registration. |
| 10 | **No `<PackageReference>`, no migration, no `.slnx` change** | The `SuppressedAddress` POCO is a new type in an existing project (`Kumunita.Core`). Marten's delta detection picks up the new doc at boot (ADR 0004 §B — the `Schema.For<T>()` addition is the migration; no hand-rolled `FeatureSchemaBase`). No new package. No solution change. |

---

## §6 — The pin's exit contract (U1's deliverable)

- **One new doc file:** `docs/design/prod-hardening-f17-bounce.md` (this file).
- **No `src/**` change.** No `<PackageReference>`. No migration. No `.slnx`
  change. No build. No test run.
- **The five pin targets are grounded in the real repo** (grep + read the
  actual `IMailerStage` / `StageAsync` / `NotificationService.cs:361` /
  `NotificationDocTypes.cs` / `DependencyInjection.cs` lines — not invented).
- **The handoff-note `## F17.U1` entry is appended** (what landed + the pin's
  five targets + the owner-ops open item re-stated).
- **F17.U1 is the FIRST unit of F17.** After it, F17.U2–U3 + F18–F22 remain.
  The lane stays **open** — no 6-member close-flip (that is F22, the lane's
  last unit).

---

## §7 — Open items (owner, recorded, NOT assumed done)

1. **Point the production mail provider's bounce/complaint webhook at
   `POST https://<instance>/api/mail/suppress`** with the shared-secret
   header (the `MailSuppression__IngestToken` value). Confirm the provider
   delivers at least one test bounce. **Until this runs, the suppression list
   is empty and the gate is a no-op.** (The §3.5 owner-ops step.)
2. **Optional: extend the gate to the other `StageAsync` call sites**
   (`IdentityService.cs:182/244` verification emails, `EventReminderService.cs:221`
   reminders, `NotificationFlushService.cs:170` deferred-email flush). The pin
   gates the **primary** per-recipient path (`NotificationService.EmitAsync`);
   the others are lower-volume and serve known-good recipients. If the owner
   wants full coverage, a follow-up unit adds the same `LoadAsync` check to
   each site. (Recorded, not assumed — the pin's minimum is the one gate.)
3. **Optional: a manual suppression lane** (an admin UI for the GlobalAdmin
   to suppress/unsuppress an address without a provider callback). The
   `SuppressedAddress` doc's `Reason = "manual"` / `Source = "manual"` fields
   are pinned for this, but the admin UI is **out of scope** for F17 (a
   follow-up unit or a separate lane). (Recorded, not assumed.)

---

*End of pin. F17.U2 (the suppression doc + gate) and F17.U3 (the ingest +
pinned test) execute against this file. A fresh clean-slate agent reads this
+ the F16.U3 handoff-note entry + the `NotificationService.cs` seam lines and
proceeds.*
