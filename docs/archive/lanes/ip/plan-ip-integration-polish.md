# IP — Integration Polish (seam deduplication, security completion, UX closure, docs parity)

> **This file is the register** — the lane map, the [PROPOSED] decision set,
> the invariants, the FACES, the §gate, the unit map, the workflow, the
> atomicity contract, and the §drift-guard. It is read first by **every** unit
> agent (U00–U07) before anything else. It is **not** a per-unit plan: each
> unit has its own self-contained file (`ip-uNN.md`) that a ~32K-context agent
> can execute without re-deriving this file.
>
> **IP is not a milestone.** It has no `Milestones.cs` entry, no `StatusNext`,
> no ADR. It is a **named improvement lane** (the M23 "named lane" convention —
> ADR 0006-E "a new capability that settles a design question gets an ADR; a
> lane that *strentheens an existing seam* does not"). IP **strentheens the
> seams that already exist** — it does not add a new document, a new context,
> a new `AccessAction`, a new `IAuthorizationService` method, a new `DocTypes`
> surface, or a new `Milestones.cs` row. Its **one** observable artifact is the
> **handoff notes** (the `ip-integration-polish-handoff-notes.md` file in
> `done/`), which record what was delivered and why.
>
> **IP's close is U07.** U07 moves all IP artifacts flat to `done/` and
> appends the final handoff section. **No unit before U07 touches
> `Milestones.cs` or `MilestonesTests`** (the roadmap already reads "M22 in
> progress" — it stays that way; IP is not a roadmap entry).

## Tiering (three documents, like M23)

| Tier | File | Who owns it | Lifetime |
| --- | --- | --- | --- |
| 1 — Register (this file) | `plan-ip-integration-polish.md` (at the `plans-milestones/` root) | U00 (author) + all units (read) | moves to `done/` at U07 |
| 2 — Per-unit plans | `in-progress/ip-uNN.md` | the `U##` agent | moves to `done/` when the unit is done |
| 3 — Rolling handoff notes | `in-progress/ip-integration-polish-handoff-notes.md` | every agent appends its `## U##` section | moves to `done/` at U07 |

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
  `tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  **or** `tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`.
  Both must be green.
- **A unit that needs *both* test assemblies is usually too big — split it.**
  The named exception is **U06** (the Login-view error-code mapping + the
  three `kw-l` keys): its pin is a single deliverable's two-sided seam
  (registry parity in `Core.Tests` + the view→registry consistency in
  `Web.Tests`), and the two assemblies are two angles on the *same* 3 keys
  + 1 view block — splitting them would leave neither half self-contained.
  See the §unit-series-rules "U06 is the named exception" note.
- **No build/test exit for U00** — it is docs-only. Its exit is "the files
  exist and the changes are in place."

> **Test-runner quirk (do not retry the broken paths):** `dotnet test` and VS
> Test Explorer discovery go wrong on this machine (xunit.v3 bridge). The
> reliable exit is the `dotnet build` + `dotnet exec … .dll` pair above.
> `Kumunita.Core.Tests` starts `postgres:18` via Testcontainers (~20 s) and
> leaves Docker containers behind if killed — `docker container prune` to clean
> up.

## Understanding (what IP is)

The fractal integration philosophy says: **quality is the quality of the
linkage, not the sum of the parts.** A platform is not good because each screen
works; it is good because the connections hold — a post reaches the right
people, a report reaches a moderator *with an audit trail*, a resident's private
detail stays private while the community still functions. Most failures are
**seam failures, not component failures.**

IP applies that rule to the codebase itself. The platform has M0 through M23
shipped — a rich, integrated surface. But the *seams between parts* have
accumulated small debts that are not feature gaps but **linkage debts**:

1. **The audit row is three copies of one contract.** `StoreAuditRow` is
   copy-pasted in `EventService`, `InventoryService`, and `ProjectService` with
   diverging signatures. Each independently re-decides `EffectivePrincipalId`.
   If one copy drifts, the audit seam breaks silently. The fix is the same as
   the philosophy: *make the contract one.*
2. **The standing-matrix is four copies of one mapping.** `TodoAuditViaFor`,
   `BoardAuditViaFor`, `GoalAuditViaFor`, and `ProjectAuditViaFor` are
   near-identical role→`AccessVia` mappers. The same drift risk.
3. **The security integration has open seams.** Rate limiting covers
   `login/signup/resend/report/setup` but not message send or post/reply
   create — the two highest-volume resident write surfaces. The `catch {}`
   blocks in `HomeController` and `MessagesController` swallow all exceptions
   (including DB failures) and render a null profile — a "degraded but
   signed-in" state that is indistinguishable from "no profile."
4. **The two most security-critical per-request gates have no unit tests.**
   `BlockedAccountMiddleware` and `PrivilegedStampMiddleware` are exercised
   only via e2e — a regression in the sign-out/redirect path would not be
   caught by the fast suites.
5. **The docs have a stale ADR range and a truncated value-chain table.**
   `ARCHITECTURE.md:84` says `0001–0053`; the actual range is `0001–0123`.
   The value-chain table stops at M17; M18–M23 are shipped but have no row.
6. **The `?error=` codes from the middleware land on the Login view with no
   user-facing copy mapping.** `blocked`, `account-removed`, `role-changed`
   all render whatever `Model.Error` is (likely a generic string). The
   resident's mental model of *why they were signed out* is broken at the
   seam with the platform's security model.

IP delivers **six small, atomic improvements** that each *strentheen an
existing seam* — none adds a new document, a new context, a new
`AccessAction`, a new `IAuthorizationService` method, a new `DocTypes`
surface, or a new `Milestones.cs` row. The value is not a new feature; it is
the **linkage holding** — the audit row is one contract, the standing matrix
is one mapping, the security integration is complete, the two most critical
gates are tested, the docs match the code, and the error codes are legible
to the resident.

## What IP is NOT

- **Not a new milestone, not a new ADR, not a new `Milestones.cs` entry.**
  IP is a *named improvement lane* (the M23 convention: "a lane that
  *strentheens an existing seam* does not get an ADR").
- **Not a new document, not a new context, not a new storage lane.** IP
  adds **one** new static helper class (`AccessAuditFactory` or
  `StandingMatrix`) in `Kumunita.Core.Authorization` — a *pure
  computation helper*, not a domain document, not registered in any
  `DocTypes` surface.
- **Not a new authorization surface.** IP adds **no** `AccessAction`,
  **no** `Decide()` branch, **no** `AccessVia` value, **no**
  `IAuthorizationService` method, **no** `TargetKind`. It **reuses** the
  **existing** `AccessAudit` type, the **existing** `AccessVia` enum, and
  the **existing** `AccessOutcome` enum.
- **Not a new notification lane.** IP stages **no** email, **no** Wolverine
  handler, **no** `NotificationService` call.
- **Not a new editor / tag-input / WYSIWYG subsystem.** IP reuses the
  existing `kw-l` key set and the existing `KnownTranslationKeys` closure.
- **Not a re-architecture.** IP does not re-shape a service, does not
  split a god service, does not introduce a new bounded context. It
  extracts *one* shared helper where three or four copies have drifted,
  adds *one* rate-limit policy where the security integration is incomplete,
  adds *one* `ILogger` call where a `catch {}` silently swallows, and adds
  *two* unit test files where the most critical gates have none.

## Decisions (D1–D6) — [PROPOSED], locked or amended by U00

> Each D# carries a `*Forbids:*` tail — the anti-pattern it rules out. U00
> locks these verbatim in the handoff notes, or amends them **in U00**
> (recorded in the handoff notes). A later unit may not amend a D#; if a
> D# is wrong once the code starts, that is a §drift-guard stop.

### D1 — `AccessAuditFactory` is a **pure static helper** in `Kumunita.Core.Authorization` — no new document, no new `DocTypes` surface, no new DI registration

`AccessAuditFactory` (a `public static class` in the `Kumunita.Core.Authorization`
namespace, alongside `AccessAudit` itself) provides **one** method:

```csharp
// in src/Kumunita.Core/Authorization/AccessAuditFactory.cs (new file)
public static class AccessAuditFactory
{
    /// <summary>
    /// Creates a single-target <see cref="AccessAudit"/> row (the C3 invariant —
    /// "always-on, in-transaction"). The row is NOT stored here; the caller
    /// stores it in their own session (the existing "caller's session" idiom).
    /// </summary>
    public static AccessAudit SingleTarget(
        string actorId,
        string action,
        string targetKind,
        string targetId,
        AccessVia via,
        AccessOutcome outcome = AccessOutcome.Allow)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorId,
            EffectivePrincipalId = actorId,  // the actor acts as themself (no delegation in write lanes)
            Action = action,
            TargetKind = targetKind,
            TargetId = targetId,
            Via = via,
            Outcome = outcome,
        };
}
```

The three `StoreAuditRow` call sites (`EventService:1697`, `InventoryService:507`,
`ProjectService:3498`) each **delete their private method** and **replace every
call** with `session.Store(AccessAuditFactory.SingleTarget(...))`. The method
signatures differ across the three (Inventory adds `outcome`, Project adds
`targetKind`); the factory's parameter list is the **union** of all three —
`actorId, action, targetKind, targetId, via, outcome = Allow`. A call site
that previously hardcoded `targetKind` (Event: `"event"`, Inventory:
`"inventory"`, Project: `"todo"`/`"board"`/etc.) now passes it as an argument.

*Forbids:* a new `AccessAudit` *document* (it already exists), a new
`*DocTypes` surface, a new DI registration (it's a static class), a new
`IAuthorizationService` method, or a factory that *stores* the row (the
caller stores it — the "caller's session" idiom is the ADR 0004 §B.1
invariant: the audit row commits atomically with the domain write).

### D2 — `StandingMatrix` is a **pure static helper** in `Kumunita.Core.Authorization` — no new document, no new DI registration

`StandingMatrix` (a `public static class` in the `Kumunita.Core.Authorization`
namespace) provides **one** method that captures the **common pattern**
across the four `*AuditViaFor` mappers:

```csharp
// in src/Kumunita.Core/Authorization/StandingMatrix.cs (new file)
public static class StandingMatrix
{
    /// <summary>
    /// Maps the actor's standing to the <see cref="AccessVia"/> audit tag.
    /// The pattern is uniform across todos, boards, goals, and projects:
    /// the actor is the **owner** of the resource → <see cref="AccessVia.Owner"/>;
    /// otherwise the actor qualified under an elevated role (GlobalAdmin) →
    /// <see cref="AccessVia.Admin"/>.
    /// </summary>
    public static AccessVia AuditVia(string actorId, string ownerId)
        => string.Equals(ownerId, actorId, StringComparison.Ordinal)
            ? AccessVia.Owner
            : AccessVia.Admin;
}
```

The four `*AuditViaFor` methods (`TodoAuditViaFor`, `BoardAuditViaFor`,
`GoalAuditViaFor`, `ProjectAuditViaFor` in `ProjectService.cs:905–963`,
and `AuditViaFor` in `EventService.cs:473`) each **delete their private
method** and **replace every call** with `StandingMatrix.AuditVia(...)`.
The method bodies are *near-identical* (the `string.Equals` → `Owner`/`Admin`
branch); the only difference is the resource's owner property name
(`todo.OwnerId`, `board.CreatedById`, `goal.CreatedById`, `project.CreatedById`,
`event.AuthorId`). The factory's parameter is `ownerId` — the caller passes
the resource's owner id.

*Forbids:* a new `AccessVia` value (it already exists), a new DI
registration (it's a static class), a `StandingMatrix` that *decides* the
standing (it only *maps* — the decision is already made by the caller's
standing check), or a `StandingMatrix` that *stores* the audit row (that's
`AccessAuditFactory`'s job, D1).

### D3 — Rate limiting on **message send** and **post/reply create** completes the security integration (SECURITY.md §5 control map, A2)

`Program.cs`'s `AddRateLimiter` block gains **two** policies:

- **`message`**: 20 per 15 minutes per IP (a direct-spam surface —
  `MessagesController` send is the highest-volume authenticated write).
- **`write`**: 30 per 15 minutes per IP (a general resident write-lane
  guard — post/reply create, bookmark toggle, attachment upload).

The `[EnableRateLimiting("message")]` attribute is added to the
`MessagesController` send action; `[EnableRateLimiting("write")]` is added
to the `PostsController` create-post and create-reply actions. The
partition key is the **resolved client IP** (the existing `AddWindow`
idiom — `context.Connection.RemoteIpAddress` behind the Caddy edge).

*Forbids:* a Postgres-backed or distributed rate limiter (the
"per-instance, in-memory" decision in SECURITY.md §6 is frozen), a
per-account partition (the per-IP partition is the SECURITY.md decision —
at dozens of users, a process-local counter is the correct weight), or a
CAPTCHA (deferred by documented decision, OPS.md §10).

### D4 — `catch {}` blocks in the read path **log the exception** before rendering the degraded state (the "degraded but signed-in" seam)

`HomeController.cs:197` and `MessagesController.cs:191` each have a
`catch { profile = null; }` (or equivalent) that swallows **all** exceptions
(including DB failures). The fix is **not** to rethrow (the "degraded but
signed-in" state is a valid product decision — a transient DB error on the
home page should not 500); the fix is to **log** the exception at `Warning`
level (the operator sees it) before rendering the null profile:

```csharp
catch (Exception ex) when (ex is not UnauthorizedAccessException)
{
    _logger.LogWarning(ex, "Home profile load failed; rendering degraded state.");
    profile = null;
}
```

The `when` clause excludes `UnauthorizedAccessException` (the expected
not-found / authorization shape — that is *not* a degraded state, it's a
normal "no profile" result). **Neither controller currently has an
`ILogger<T>` parameter** — U04 injects `ILogger<T>` into each controller's
constructor (DI auto-resolves it; no `Program.cs` change needed), and the
`catch` fix is one line (`_logger.LogWarning(ex, …)`) + the `when` clause.

*Forbids:* rethrowing (the "degraded but signed-in" state is a product
decision — a 500 on the home page for a transient DB error is worse than
a degraded render), a new `IErrorHandlingService` (over-engineering at
this scale), or removing the `catch` (the `profile = null` render is the
correct fallback).

### D5 — The two per-request security gates get **unit tests** (the "parts work, seams don't" anti-pattern)

`BlockedAccountMiddleware` and `PrivilegedStampMiddleware` are the two
most security-critical per-request gates (block enforcement + privilege
revocation). They are exercised only via e2e. The fix is **two** new test
files in `tests/Kumunita.Web.Tests/`:

- `BlockedAccountMiddlewareTests.cs` — tests the sign-out + redirect path
  when `Profile.Blocked` is true, and the pass-through when it's false.
- `PrivilegedStampMiddlewareTests.cs` — tests the sign-out + redirect path
  when the role set has changed, and the pass-through when it hasn't.

Both use an `HttpContext` with a stubbed `IUserInfoService` / `UserManager`
(the "resolve from `RequestServices`" shape makes this trivial to test —
the middleware's `RequestServices.GetRequiredService<T>` calls are the seam).

*Forbids:* a new `IMiddleware` interface (the middleware pattern is
ASP.NET Core standard — no abstraction needed), a test that *re-implements*
the middleware's logic (the test exercises the middleware's `InvokeAsync`,
not a copy of it), or a test that requires a running Postgres (the stubbed
services make it a pure unit test).

### D6 — The `?error=` codes from the middleware are mapped to **localized user-facing copy** in the Login view (the cognitive-integration seam — domains-of-integration §12)

The middleware redirects to `/Account/Login?error=blocked`,
`?error=account-removed`, or `?error=role-changed`. The Login view
(`Views/Account/Login.cshtml:13`) renders `Model.Error` — a generic string
that does not distinguish the three cases. The fix is a **localized** error
code → message table in the Login view:

| `?error=` code | User-facing message (en) | `kw-l` key |
| --- | --- | --- |
| `blocked` | "Your account has been temporarily suspended. Contact an administrator." | `account.login.error.blocked` |
| `account-removed` | "Your account has been removed. Contact an administrator." | `account.login.error.removed` |
| `role-changed` | "Your role has changed. Please sign in again." | `account.login.error.role_changed` |

The view renders the code-specific message when `?error=` matches a known
code, and falls back to `Model.Error` for unknown codes (forward-compatible).
The three `kw-l` keys are added to `KnownTranslationKeys` in all four
languages (en/de/fr/da) — the `KwLRegistryConsistencyTests` +
`KnownTranslationKeys_ParityTests` pin the closure.

*Forbids:* a new `IErrorMessageService` (the mapping is a 3-row table in the
view — no abstraction needed), a new `AccessAction` or `TargetKind` (the
error codes are *transport* codes, not authorization codes), or a
server-side error-page redirect (the `?error=` query-string is the existing
idiom — the fix is the *copy*, not the *mechanism*).

## Invariants (C-IP·1 … C-IP·5)

- **C-IP·1 — The audit row is one contract (D1, D2).** `AccessAuditFactory.SingleTarget` is the **single** factory for single-target `AccessAudit` rows; `StandingMatrix.AuditVia` is the **single** standing→`AccessVia` mapper. The three `StoreAuditRow` copies and the four `*AuditViaFor` copies are **deleted**. A pin test asserts the factory's field mapping + the matrix's two-branch shape.
- **C-IP·2 — Zero new authorization surface (D1, D2).** No new `AccessAction`, no `Decide()` branch, no `AccessVia` value, no `IAuthorizationService` method, no `TargetKind`. The two static helpers are **pure computation** — they do not query, store, or decide. A pin test asserts the `IAuthorizationService` seam + the `AccessVia` enum are unchanged.
- **C-IP·3 — The security integration is complete (D3, D4, D5).** Rate limiting covers the message send + the resident write lane (the two highest-volume surfaces). The `catch {}` blocks log before degrading. The two per-request security gates have unit tests. A pin test asserts the rate-limit policies exist + the middleware tests pass.
- **C-IP·4 — The docs match the code (U00).** The ADR range in `ARCHITECTURE.md:84` is corrected to `0001–0123` (or the hardcoded range is dropped). The value-chain table is extended to M18–M23. The `Milestones.cs` M23/M22 order has a one-line rationale comment. A pin: the files exist with the changes.
- **C-IP·5 — The error codes are legible to the resident (D6).** The three `?error=` codes (`blocked`, `account-removed`, `role-changed`) render distinct, localized messages in the Login view. The three `kw-l` keys are present in all four languages. A pin test asserts the `kw-l` parity + the view renders the code-specific message.

## FACES (F1–F4) + the named trade

- **F1 — The audit seam is one contract, not three copies.** *(C-IP·1, C-IP·2.)*
- **F2 — The security integration is complete: the highest-volume write surfaces are throttled, the degraded state is logged, and the two most critical gates are tested.** *(C-IP·3.)*
- **F3 — The docs match the code: the ADR range is current, the value-chain table is complete, and the roadmap order is explainable.** *(C-IP·4.)*
- **F4 — The resident can read *why* they were signed out: the three `?error=` codes render distinct, localized messages.** *(C-IP·5.)*

**The named trade.** IP buys *the linkage holding* — the audit row is one
contract, the standing matrix is one mapping, the security integration is
complete, the two most critical gates are tested, the docs match the code,
and the error codes are legible to the resident — in exchange for **no
new document, no new context, no new `AccessAction`, no new
`IAuthorizationService` method, no new `DocTypes` surface, no new
`Milestones.cs` entry, no new ADR**. IP is *the seams holding*, not a new
feature. The deferred lanes are deliberately *not* in IP: a god-service
split (`ProjectService` at 4,128 lines), a `BookmarkService` seam
abstraction, a `KnownTranslationKeys` split, or an `INotificationService`
interface (forbidden by ADRs 0076/0077/0083/0084/0117/0118) are larger
designs that need their own ADRs.

## §gate (acceptance tests, named)

- **GATE-1 — The audit factory + standing matrix are the single contract.**
  `AccessAuditFactory.SingleTarget` returns a row with all 9 fields set;
  `StandingMatrix.AuditVia` returns `Owner` for the owner, `Admin` for
  non-owners. The three `StoreAuditRow` copies and the four `*AuditViaFor`
  copies are **deleted** (a grep for `private static void StoreAuditRow`
  and `private static AccessVia *AuditViaFor` returns zero matches in
  `src/Kumunita.Core/`). *(C-IP·1, C-IP·2.)*
- **GATE-2 — The security integration is complete.** The `message` and
  `write` rate-limit policies exist in `Program.cs`; the
  `[EnableRateLimiting]` attributes are on the send/create actions;
  the `catch {}` blocks log at `Warning` + have a `when` clause;
  `BlockedAccountMiddlewareTests` + `PrivilegedStampMiddlewareTests` pass.
  *(C-IP·3.)*
- **GATE-3 — The docs match the code.** `ARCHITECTURE.md:84` does not
  contain `0001–0053`; the value-chain table includes M18–M23; the
  `Milestones.cs` M23/M22 order has a comment. *(C-IP·4.)*
- **GATE-4 — The error codes are legible.** The Login view renders
  `blocked` / `account-removed` / `role-changed` as distinct messages;
  the three `kw-l` keys are present in all four languages; the
  `KnownTranslationKeys_ParityTests` pass. *(C-IP·5.)*

## Workflow (every unit, 7 steps)

1. **Read this register** (the Understanding, the [PROPOSED] D# set, the
   invariants, the FACES, the §gate, the §drift-guard).
2. **Read the unit plan** (`ip-uNN.md`) — the Goal, the Entry reads, the
   Deliverables, the Exit.
3. **Read the Entry reads** named in the unit plan (4–8 files, the design-doc
   sections the unit implements, the ADR, the code seams it touches).
4. **Execute** the Deliverables (≤ 7 files; implement the locked C# / keys
   exactly; do not amend a D#).
5. **Run the Exit gate** — one `dotnet build Kumunita.slnx -c Debug` + one
   `dotnet exec` test assembly, both green (U00: no build/test).
6. **Append a `## U##` section** to the handoff notes (5 lines: what was
   delivered, any open question, the next unit's entry point, no new drift).
7. **Move the unit plan** `in-progress/ip-uNN.md` → `done/ip-uNN.md` (flat,
   directly under `done/` — the M13–M21 convention, **not** a `done/ip/`
   subfolder).

## Unit-series rules

- **Order is U00 → U07.** U00 (docs-only) must land first, because it
  corrects the stale ADR range and the truncated value-chain table — the
  other units cite `ARCHITECTURE.md` and the value-chain table. A later
  unit may read an earlier unit's code, but never re-derive a D#.
- **One concern per unit.** U01 owns the `AccessAuditFactory` + the three
  `StoreAuditRow` call sites. U02 owns the `StandingMatrix` + the four
  `*AuditViaFor` call sites. U03 owns the two rate-limit policies + the
  `[EnableRateLimiting]` attributes. U04 owns the two `catch {}` fixes.
  U05 owns the two middleware test files. U06 owns the Login view error-code
  mapping + the three `kw-l` keys. U07 owns the close. No unit both *reads*
  and *writes* a seam another unit owns.
- **One test-assembly exit per unit.** Core units (U01, U02) exit on
  Core.Tests; Web units (U03, U04, U05) exit on Web.Tests. The close unit
  (U07) exits on Web.Tests (it moves files, which is a no-op for the test
  assembly, but the `dotnet build` + `dotnet exec` is the exit gate).
  If a unit needs both, it is too big — split it.
- **U06 is the named exception** (the same pattern as U00's "no
  build/test"). U06 exits on **both** assemblies. Its pin is a single
  deliverable's *two-sided seam*: the `KnownTranslationKeys_ParityTests`
  (Core.Tests) pin the three new `account.login.error.*` keys are present
  in all four language dictionaries (a missing `de`/`fr`/`da` value
  fails there), and the `KwLRegistryConsistencyTests` (Web.Tests) pin the
  Login view's `kw-l` keys are registered in the registry (an unregistered
  key fails there). The two assemblies are two angles on the *same*
  deliverable — the three keys + the view block — and splitting them
  would leave neither half self-contained (the view unit's Web.Tests pin
  would fail until the registry unit landed; the registry unit has no
  Web.Tests pin at all). U06 is small (3 keys × 4 languages + 1 view
  block + 4 pin tests), well within the ~32K context budget.
- **No unit touches `Milestones.cs` or `MilestonesTests`.** IP is not a
  roadmap entry; the M22/M23 order is unchanged.
- **`kw-l` parity is authored by the unit that renders the key.** U06
  authors the three `account.login.error.*` keys (en/de/fr/da); no other
  unit adds a `kw-l` key. The `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests` pin the closure.

## §drift-guard

**A unit stops (does not improvise) when it hits any of:**

- A D# it needs is not in the [PROPOSED] set, or two D#s contradict —
  **stop, report to the user**; do not pick one silently. (A D# is locked
  by U00; a post-U00 D# change is a handoff-note line, and only U00 makes it.)
- A unit needs *both* test assemblies green to exit — **it is too big**;
  split it per the Atomicity contract rather than running both.
- A unit is about to **create a new `AccessAudit` document, a new `DocTypes`
  surface, a new DI registration for a helper, a new `AccessAction`, a new
  `Decide()` branch, a new `AccessVia` value, a new `IAuthorizationService`
  method, or a new `TargetKind`** — that violates C-IP·2 / D1 / D2; stop and
  report.
- A unit is about to **store the audit row inside `AccessAuditFactory`**
  (instead of returning it for the caller to store) — that violates D1
  (the "caller's session" idiom); stop and report.
- A unit is about to **add a Postgres-backed or distributed rate limiter**
  or a **CAPTCHA** — that violates D3 (the SECURITY.md §6 decisions are
  frozen); stop and report.
- A unit is about to **rethrow from the `catch {}` blocks** (instead of
  logging + degrading) — that violates D4 (the "degraded but signed-in"
  state is a product decision); stop and report.
- A unit is about to **add an `INotificationService` interface** or a
  **new `IMiddleware` abstraction** — that violates D5 / the ADRs that
  forbid the interface (0076/0077/0083/0084/0117/0118); stop and report.
- A unit is about to **render a `?error=` code as a new server-side
  redirect** (instead of the existing `?error=` query-string idiom) — that
  violates D6; stop and report.
- A unit is about to **touch `Milestones.cs` or `MilestonesTests`** — stop;
  IP is not a roadmap entry.
- A unit is about to **add a new `kw-l` key outside the three
  `account.login.error.*` keys** — stop; only U06 authors `kw-l` keys, and
  only the three in the locked set.

**The handoff-notes file is the cross-unit memory.** Every unit reads the
`## U##` sections before it and appends its own after; a unit does not
re-derive what an earlier unit already settled (a D# amendment, the factory
signature, the standing-matrix signature, the rate-limit policy names, the
`catch {}` shape, the test file names, the `kw-l` key set).

**The register is the map, not the code.** If a unit is tempted to "just
add a `Decide()` branch for the audit factory," "create a new
`AccessAuditRow` document," "add a Postgres-backed rate limiter," "rethrow
from the `catch {}`," "add an `INotificationService` interface," or "render
the error codes as server-side redirects," that is the §drift-guard firing —
IP's value is precisely that it does *not*: it makes **the audit row one
contract**, **the standing matrix one mapping**, **the security integration
complete**, **the two most critical gates tested**, **the docs match the
code**, and **the error codes legible to the resident** — with **zero new
authorization surface**.
