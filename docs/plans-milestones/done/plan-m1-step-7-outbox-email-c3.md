# M1 step-7 fix — durable OutboxEmail via Wolverine transactional outbox (true C3) — sealed unit register

## Understanding

`IMailerStage.StageAsync` (implemented by `OutboxEmailStager`) only does `session.Store(new OutboxEmail{...})` inside the caller's Marten session — it never tells Wolverine there is a message to route. `Kumunita.Web/SideEffects/OutboxEmailHandler.Handle(OutboxEmail, ISmtpSender)` is a **Wolverine message handler**: it only fires when an `OutboxEmail` **envelope** reaches the durable local queue, which requires the message to be enqueued into the Postgres-backed `IMessageStore` (registered by `.IntegrateWithWolverine()` in `Kumunita.Web/Program.cs`) **in the same scope/transaction as a Marten session** so Wolverine's transactional middleware holds the envelope commit until that session's `SaveChangesAsync` commits. That "enqueued envelope + document committed in one Postgres transaction" is the real C3 guarantee this change is restoring (repo's own pattern: `docs/wolverine/marten-integration-outbox.md`, `snippets/wolverine-durable-email-handler.cs` — `yield return`/enqueue-then-middleware-commits, not a post-hoc `bus.PublishAsync`, which `IMessageBus.PublishAsync` alone would not give us because it commits in a separate transaction from the caller's hand-opened session).

## Root cause (confirmed against the installed packages, not from memory)

1. `OutboxEmailStager.StageAsync` — `src/Kumunita.Core/Identity/IMailerStage.cs:56-74` — is literally `session.Store(new OutboxEmail{...}); return Task.CompletedTask;`. No enqueue, no publish.
2. `Wolverine.IMessageBus.PublishAsync<T>` does exist (confirmed: `T:Wolverine.IMessageBus` in `wolverinefx/6.33.0/.../Wolverine.xml`), but a bare `PublishAsync` from `IdentityService`/`FirstBootSeeder` (which `OpenSession` their own Marten sessions, not inside a Wolverine handler scope) commits the envelope in a **separate** connection/transaction from the domain write — that is *not* true C3 for the envelope, and it's the anti-pattern the repo's own design-doc snippet explicitly avoids ("a command handler does not send email; it publishes an `OutboxEmail` message (Wolverine transactional outbox — only dispatched if the commit succeeds)").
3. `IMessageBus.PublishAsync` also **asserts the Wolverine host has started** (`WolverineRuntime.AssertHasStarted`, per the comment already in `Program.cs:324-325` next to the existing `AuditPurgeTick` publish). `FirstBootSeeder` runs **before** `app.StartAsync()` today (`Program.cs:279` vs `:315`) — so a naive "fix" that just adds `PublishAsync` to the stager would **break first-boot** the moment it fires. Any correct fix must move the first-boot email publish to *after* host start.

## Assumptions / decisions (already accepted by the user — do not re-litigate in-unit)

- **True C3 for the envelope is in scope** (user's explicit choice over the smaller "store-then-publish" fix). This is the "B" shape: the durable-envelope commit happens in the same Postgres transaction as the domain write.
- **Core may now reference `WolverineFx` (6.33.0)** — breaking the previously-stated "Wolverine is a Web package" convention (referenced in comments in `Kumunita.Core.csproj:30-36`, `IMailerStage.cs`, `DependencyInjection.cs`). Those comments must be updated to match, not left as a lying invariant. The dependency is justified as of this change: Core needs `Wolverine.IMessageContext` (the method-injected, per-scope handler-context API that provides the enqueue-into-the-store primitive, the same one the repo's own `snippets/wolverine-durable-email-handler.cs` relies on) rather than the host-level `IMessageBus` (which additionally asserts host-started — exactly the trap above).
- **`IMailerStage.StageAsync`'s public signature is unchanged** (still takes the caller's `IDocumentSession`, `idempotencyKey`, `recipient`, `subject`, `body`, `CancellationToken`, returns `Task`) — the contract's *meaning* changes from "store only" to "store + enqueue-the-envelope-in-this-session's-transaction", and the docs say so. Call sites (`IdentityService.RegisterAsync`, `FirstBootSeeder.SeedAdminAsync`) do **not** change shape — only `IdentityService`/`FirstBootSeeder`'s *internal* session handling may need to change to satisfy the "enqueue must be held until the caller's own SaveChangesAsync" invariant (that's a Core-internal detail, unit U3's job).
- **No breaking change to `IIdentityService.RegisterAsync`'s return type or exception contract** (it still returns `Task<ThinPrincipal>`, still throws `InvalidOperationException` on a duplicate email — `AccountController.Signup`'s `catch (InvalidOperationException)` must keep working unchanged).
- **`OutboxEmailHandler.Handle/HandleFault` (the durable handler) is unchanged** — this fix makes existing code that was already correct-by-design (handler, retry policy, `PublishFaultEvents`, `Fault<OutboxEmail>`→`EmailDeadLetter`) actually get reached.
- **Test suite must stay green**: `tests/Kumunita.Core.Tests/SideEffectHarnessTests.cs`, `EmailDeadLetterCounterTests.cs`, `ClaimShapingInvariantBTests.cs` do **not** exercise `IMailerStage`/`OutboxEmailStager` directly (confirmed: no test references `OutboxEmailStager` or `StageAsync` by name) — they exercise the `ISmtpSender`/dead-letter shape and the claim mapping. They must still pass unmodified; do not "fix" them to fit the new stager.
- `.NET 10`, Razor Pages web project, per the workspace context — irrelevant to the Core/DI edits in this plan but keep in mind if any view/controller is touched (none are, in U1–U5).

## Pinned contracts (U1 pins these exactly; no later unit may deviate)

```csharp
// src/Kumunita.Core/Identity/IMailerStage.cs — signature unchanged, contract reworded:
public interface IMailerStage
{
    /// <summary>
    /// Stages the <see cref="OutboxEmail"/> row on <paramref name="session"/> AND enqueues the
    /// matching durable-message envelope into Wolverine's Postgres-backed IMessageStore in the
    /// same transaction as <paramref name="session"/> — i.e. the envelope is held until the
    /// caller's own SaveChangesAsync commits (Wolverine transactional-outbox guarantee,
    /// docs/wolverine/marten-integration-outbox.md). A caller whose SaveChangesAsync never
    /// commits (or throws) therefore never leaves a dispatched/queued email.
    /// </summary>
    Task StageAsync(
        Marten.IDocumentSession session,
        string idempotencyKey,
        string recipient,
        string subject,
        string body,
        CancellationToken ct = default);
}

// src/Kumunita.Core/Identity/IdentityService.cs — RegisterAsync body, unchanged outer shape:
//   var user = ...; await userManager.CreateAsync(user); await userManager.AddPasswordAsync(user, password);
//   var token = NewVerifyToken(user.Id, now);
//   await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
//   session.Store(new Profile {...});
//   session.Store(token);
//   await mailer.StageAsync(session, idempotencyKey: $"verify:{user.Id}:1",
//                           recipient: email, subject: "Verify your Kumunita account", body: ..., ct: default);
//   await session.SaveChangesAsync();   // <- THIS is the commit the enqueued envelope waits on
//   return new ThinPrincipal(user.Id, user.ExternalId, IsVerifiedResident: false, ThinPrincipal.NoRoles);
// No call-site changes — only the stager behind IMailerStage changes behavior.

// src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs — SeedAdminAsync keeps its exact signature and
// its exact body EXCEPT: the final SaveChangesAsync (and only the ordering of that one call
// relative to StageAsync) is the surface U3 may reorder, and U4 may split the outbox
// publish out of this static-class boot-time call into a post-StartAsync replay (see U4).
```

## Approach — three-part, in this order, each part independently verifiable by `run_build`

**Part 1 — Core plumbing (U1–U2):** add `WolverineFx` 6.33.0 to `Kumunita.Core.csproj`; rework `OutboxEmailStager` to resolve `Wolverine.IMessageContext` (the per-scope, method-injectable context — the same one `OutboxEmailHandler.Handle`'s sibling handlers rely on, and the one whose enqueue participates in the ambient Marten transaction) and enqueue the newly-stored `OutboxEmail` document as its own message envelope in that same session. Re-register `IMailerStage` in `DependencyInjection.cs` as a factory that supplies `IMessageContext` (it is scoped; resolved lazily per use, not cached as a singleton).

**Part 2 — Fix the first-boot ordering trap (U4):** move `SchemaBootstrap.ApplyAsync` (the thing that runs `FirstBootSeeder`) from **before** to **after** `app.StartAsync()` in `Kumunita.Web/Program.cs`, matching the existing `AuditPurgeTick` pattern (which is already correctly placed after `StartAsync` — do not touch that line). Concretely: the email-staging half of `FirstBootSeeder.SeedAdminAsync` (the `mailer.StageAsync` + its surrounding `SaveChangesAsync`) is extracted into a post-start step; the rest of the seeder (community row, account+roles, components, language catalog) may stay put or move with it, as long as **no `IMessageContext`/`IMessageBus` call fires before `StartAsync` returns**. This is a hard constraint, not a preference — `AuditPurgeTick`'s own comment (`Program.cs:324-325`) already documents `AssertHasStarted`.

**Part 3 — Verify (U3, U5):** every step ends with `run_build` green across `Kumunita.Core` + `Kumunita.Web` (+ the two test projects), then the pinned test suite (`run_tests` on `SideEffectHarnessTests`, `EmailDeadLetterCounterTests`, `ClaimShapingInvariantBTests`) must still pass unmodified.

## Key Files

- `src/Kumunita.Core/Identity/IMailerStage.cs` — the seam being recontracted (signature unchanged, docs + `OutboxEmailStager` behavior change).
- `src/Kumunita.Core/DependencyInjection.cs` — `AddTransient<IMailerStage, OutboxEmailStager>()` → factory that supplies `IMessageContext`.
- `src/Kumunita.Core/Kumunita.Core.csproj` — add `WolverineFx` 6.33.0 (pin to the exact same version `Kumunita.Web.csproj` already uses, do not bump).
- `src/Kumunita.Core/Identity/IdentityService.cs` — **no** call-site change expected; verify-only.
- `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` — may need its email-stage/commit ordering adjusted to satisfy the "enqueue-held-until-caller-commit" invariant.
- `src/Kumunita.Web/Program.cs` — the `ApplyAsync`→`StartAsync` ordering fix (move `ApplyAsync` after `StartAsync`), and the existing `AuditPurgeTick` block stays as-is (already correct).
- `src/Kumunita.Web/SideEffects/OutboxEmailHandler.cs` — **read-only** for this plan; it's the consuming half of the design and must not change.
- `tests/.../SideEffectHarnessTests.cs`, `EmailDeadLetterCounterTests.cs`, `ClaimShapingInvariantBTests.cs` — **read-only/verify-only**; must pass unmodified.
- `docs/plans-milestones/done/m1-step-7-handoff-notes.md` (new) — the rolling handoff log, one section per unit.

## Risks & open questions

- **R1 (highest impact):** `Wolverine.IMessageContext`'s exact public surface (method name/signature for "enqueue a message tied to a Marten session transaction") is **not verified yet in this repo against the installed 6.33.0 package** — the reference docs (`marten-integration-outbox.md`) describe the *concept* (transactional outbox, middleware-driven commit) and the repo's own handler snippets use `yield return` from inside a Wolverine-discovered handler. U1's **only** job is to pin the exact `IMessageContext` call by compiling a minimal, provably-correct usage; do NOT assume a member name until `run_build` confirms it. If `IMessageContext` turns out not to be the correct/available surface (e.g. it's internal, or the enqueue primitive requires being *inside* a Wolverine-discovered handler), stop and record an observation — do **not** fall back to a bare `IMessageBus.PublishAsync` outside a handler scope just to get the build to pass; that reintroduces the separate-transaction problem this whole plan exists to avoid. Escalate (stop, record_observation, ask) instead.
- **R2:** `IMailerStage` is also registered/consumed in `SchemaBootstrap.ApplyAsync` (the seeder path) — see `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs:59`. U4's reordering covers this; U3 alone (Core plumbing without Program.cs reordering) could make first-boot *succeed in staging but crash on the envelope commit* if the order weren't also fixed — so U4 is not optional/deferrable, it's load-bearing.
- **R3:** `IMessageContext` is **scoped** and only meaningful inside a request/host scope — `FirstBootSeeder` currently runs in a hand-created `CreateAsyncScope()` from the root provider (`SchemaBootstrap.ApplyAsync`). Verify (in U4) that resolving `IMessageContext` from that hand-created scope actually has a valid Wolverine runtime backing it, or adjust the scope to one Wolverine's own host DI setup knows about (e.g. resolve from `app.Services`-level scope post-`StartAsync`, not a detached `IServiceProvider.CreateAsyncScope()`).
- **R4:** `Kumunita.Core.csproj` gains a real runtime dependency on a non-Web assembly (`WolverineFx.dll`). Confirm via `run_build` that this does not create a version-skew with `WolverineFx.Marten` 6.33.0 already referenced by `Kumunita.Web` (they must stay at exactly 6.33.0/6.33.0 — do not let a transitive bump drift one of them).
- **R5:** No new *document shape* is being added (the `OutboxEmail` table already exists, registered in `M1DocTypes.Configure`), so no new migration/storage-feature step is expected — U1–U5 should not touch `M1DocTypes.cs`/`M3DocTypes.cs`/any `FeatureSchemaBase` subclass. If a unit "needs" a schema change here, that is a signal the approach has drifted; stop and record an observation.

## Workflow — handoff protocol for fresh-context agents

Executed as **sealed units** (U1–U6 below), one unit per fresh agent (~64K context window).

**Shared state (three-tier contract), mirroring `plan-m3-posts-components.md`:**
- **Primary — this file** — the unit registry, the pinned contract, and what each unit's Exit is.
- **Secondary — the code itself, at the exact signatures pinned above.**
- **Scratch — `docs/plans-milestones/done/m1-step-7-handoff-notes.md`** — one appended section per unit (never rewritten), written **before** the unit exits.

**Per-unit template:** **Goal** (one sentence); **Entry reads** (3–5 files, <~300 lines each, no full-repo scan, cite the pinned section this unit implements); **Deliverables** (closed set, ≤ ~4 files / ~400 LOC, no unrelated cleanups); **Exit** (`run_build` green for the touched projects + the named `run_tests` green; handoff-note section appended *before* exiting).

**Unit-series rules:** (1) a unit never edits a file not in its own Deliverables list; (2) a unit never invents a new public seam/`IMailerStage` member beyond the pinned contract; (3) a unit never touches `OutboxEmailHandler.cs`, `AccountController.cs`, `AdminController.cs`, or any of the three pinned test files (read-only for this plan); (4) if a unit hits R1/R3 and the pinned `IMessageContext` surface doesn't work, it must **stop and record_observation + ask** — it may not substitute a different mechanism (e.g. bare `PublishAsync`, a new `IMessageStore` call, an event-sourcing append) without explicit sign-off; (5) every unit leaves the repo buildable (`run_build` green) before it is considered done.

---

## Units

### U1 — pin the exact `Wolverine` API surface (compilation-truth over docs-truth)
- **Goal:** prove, by compiling against the real 6.33.0 packages, the exact type/method the stager will call to "enqueue an `OutboxEmail` envelope tied to a Marten session's transaction" — do not proceed on the basis of the reference doc's prose alone.
- **Entry reads:** `src/Kumunita.Web/SideEffects/OutboxEmailHandler.cs` (the existing `IMessageContext`/session-free `Handle(OutboxEmail, ISmtpSender)` shape — confirms the consuming side), `src/Kumunita.Web/Program.cs:230-273` (the existing `UseWolverine` block — what's already wired: `PublishFaultEvents`, `UseDurableLocalQueues`, the retry policy), `snippets/wolverine-durable-email-handler.cs` (the reference `yield return` pattern), `docs/wolverine/marten-integration-outbox.md` (the "held until SaveChangesAsync commits" guarantee being restored, prose-level, to be confirmed or corrected by the next step).
- **Deliverables (≤ 1 file, exploratory, no public API change yet):** a throwaway scratch `static` method (or a minimal test in `tests/Kumunita.Core.Tests/`, deletable afterwards, if a throwaway-in-`src/` file is undesirable) that references `Wolverine.IMessageContext` and attempts the enqueue call shape `OutboxEmailStager` will use — compile it with `run_build` against `Kumunita.Core` (after adding the `WolverineFx` package ref, see below). The deliverable of *this unit* is a **verified, exact member name + signature** (e.g. `IMessageContext.EnqueueAsync<T>(T message)`, or a `Publish`/`Enqueue`/`Schedule`-named method that actually exists on the installed assembly) recorded verbatim in the handoff notes — plus the one-line `Kumunita.Core.csproj` `<PackageReference Include="WolverineFx" Version="6.33.0" />` addition (R4: same version pin as `Kumunita.Web.csproj`), so U2 compiles against it directly.
- **Exit:** `run_build` green on `Kumunita.Core` with `WolverineFx` referenced; handoff-note section `## U1 — pinned API` records the exact `IMessageContext` member + signature used, and explicitly flags if it differs from what the reference doc's prose implied (and which of the repo's 4 "Wolverine is a Web package" comments this now supersedes — list the file paths that U5 will update). **If the pinned surface doesn't exist / doesn't work as described: STOP, do not build a substitute — record_observation and stop here, hand back.**

### U2 — rework `OutboxEmailStager` to use the pinned API (signature unchanged)
- **Goal:** `OutboxEmailStager.StageAsync` now (a) `session.Store(new OutboxEmail{...})` exactly as today, *and* (b) enqueues the `OutboxEmail` envelope via the U1-pinned `IMessageContext` member, in the caller's session — no new public method, no changed `IMailerStage` signature.
- **Entry reads:** `src/Kumunita.Core/Identity/IMailerStage.cs` (whole file, 75 lines — the seam + the current `OutboxEmailStager`), `src/Kumunita.Core/DependencyInjection.cs` (whole file, 110 lines — where `IMailerStage` is registered today), the U1 handoff-note section (the exact `IMessageContext` member to call), `src/Kumunita.Core/Identity/M1DocTypes`-referenced `OutboxEmail` definition (already exists — `M1DocTypes.Configure` at `src/Kumunita.Core/M1DocTypes.cs` line ~68 — do **not** change it).
- **Deliverables (≤ 2 files):**
  - `src/Kumunita.Core/Identity/IMailerStage.cs` — `IMailerStage` doc reworded to the pinned contract above; `OutboxEmailStager` gains a constructor-injected `Wolverine.IMessageContext` and its `StageAsync` body becomes `Store(row)` + `U1-pinned-ENQUEUE-ASYNC-CALL(row, session)`. Interface `StageAsync` signature is byte-for-byte unchanged.
  - `src/Kumunita.Core/DependencyInjection.cs` — replace `services.AddTransient<IMailerStage, OutboxEmailStager>();` with the factory form `services.AddTransient<IMailerStage>(sp => new OutboxEmailStager(sp.GetRequiredService<Wolverine.IMessageContext>()));` — mirroring the existing factory-registration pattern already used for `DirectoryService`, `Posts.PostService`, `Moderation.ModerationService` in the same file (don't invent a new registration style).
- **Exit:** `run_build` green on `Kumunita.Core` + `Kumunita.Web`. Handoff-note section `## U2 — stager reworked`: the two file paths changed, the exact constructor signature added, a one-line confirmation that `IMailerStage.StageAsync`'s signature did not change.

### U3 — verify the two existing call sites still work, unmodified
- **Goal:** confirm `IdentityService.RegisterAsync` and `FirstBootSeeder.SeedAdminAsync` (their *call sites*, no edits expected) still satisfy "the enqueued envelope is held until **the caller's** `SaveChangesAsync` commits" — i.e. the caller's `SaveChangesAsync` is the transaction boundary the U1/U2 enqueue is waiting on, not a session the stager itself is managing. This is verification-only; if the call sites are already correctly ordered (StageAsync called *before* the caller's own `SaveChangesAsync`, in the caller's own session), **no code change is expected in this unit** — the deliverable is the confirmation, recorded in handoff notes.
- **Entry reads:** `src/Kumunita.Core/Identity/IdentityService.cs` lines 92–132 (`RegisterAsync`), `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` lines 143–218 (`SeedAdminAsync`), the U2 handoff-note section.
- **Deliverables (0 files expected; 1 file max if a reordering is genuinely required to protect the invariant, and only within `FirstBootSeeder.cs` if — and only if — the seeder's session lifetime currently outlives the point where the envelope-transaction could be abandoned):** at most `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` reordered so `mailer.StageAsync` runs strictly *before* `session.SaveChangesAsync()` in the same `using`-scoped session, and no change at all to `IdentityService.cs`.
- **Exit:** `run_build` green on all 4 projects. Handoff-note section `## U3 — call sites verified/reordered`: state explicitly "no change needed" or name the one reordered call in `FirstBootSeeder.SeedAdminAsync`. **Run** `run_tests` on `ClaimShapingInvariantBTests` (must pass unchanged — this is the cheapest canary that we haven't broken Core's DI surface).

### U4 — fix the first-boot ordering trap (`Program.cs`)
- **Goal:** move `SchemaBootstrap.ApplyAsync` to run **after** `app.StartAsync()`, so the seeder's (now real) `IMessageContext`/envelope activity happens in a host that's actually started — matching the existing, already-correct `AuditPurgeTick` block rather than contradicting it.
- **Entry reads:** `src/Kumunita.Web/Program.cs` lines 275–340 (the exact `Build()` → `ApplyAsync` → pipeline setup → `StartAsync()` → `AuditPurgeTick` → `WaitForShutdownAsync` sequence, including the existing `AssertHasStarted` comment), `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (whole file, 118 lines — confirm nothing inside `ApplyAsync` itself *requires* the host not-yet-started; nothing should, it resolves from a root-provided `IServiceProvider` scope), the U3 handoff-note section.
- **Deliverables (≤ 1 file):** `src/Kumunita.Web/Program.cs` — move the single `await SchemaBootstrap.ApplyAsync(app.Services);` line from its current position (line 279, before the pipeline/`StartAsync`) to immediately **after** `await app.StartAsync();` (line 315), keeping the `AuditPurgeTick` block after it (do not reorder those two — the existing comment at line 317-328 already treats "after StartAsync" as the required ordering for any publish/enqueue work). This is a one-line move, not a rewrite — everything else in `Program.cs` (pipeline middleware order) must stay exactly where it is.
- **Exit:** `run_build` green on `Kumunita.Web`. Handoff-note section `## U4 — boot order fixed`: the exact before/after line numbers, and a one-line confirmation that the `AuditPurgeTick` block's relative position to `StartAsync` did **not** change.

### U5 — full-verification sweep + doc-comment reconciliation
- **Goal:** prove the whole repo still builds and the pinned test suite still passes unmodified, and fix the 4 now-misleading "Wolverine is a Web package" comments named in U1's handoff section to reflect the new (accepted) Core→Wolverine dependency — comments only, no code.
- **Entry reads:** U1's handoff-note section (the exact file:line list of comments to update), `src/Kumunita.Core/Kumunita.Core.csproj` (the new `WolverineFx` line, to phrase the updated comment against), the 3 pinned test files (read-only, to confirm they don't reference `IMailerStage`/`OutboxEmailStager` — a sanity check, not an edit).
- **Deliverables (≤ 4 files, comments-only):** `src/Kumunita.Core/Kumunita.Core.csproj`, `src/Kumunita.Core/Identity/IMailerStage.cs`, `src/Kumunita.Core/DependencyInjection.cs` — the 3 comment sites U1/this section identify that currently assert Core has no Wolverine dependency, reworded to describe the new (accepted) dependency and *why* it exists now (the real-C3-for-the-envelope guarantee), each with a one-line pointer back to this plan file.
- **Exit:** `run_build` green on all 4 projects; `run_tests` on **all three** pinned suites (`SideEffectHarnessTests`, `EmailDeadLetterCounterTests`, `ClaimShapingInvariantBTests`) reports the same pass count as a clean pre-change baseline (record the baseline count in the handoff note if not already there). Handoff-note section `## U5 — verified + comments reconciled`: the 3 comment file paths updated + the 3 test-suite results (name → "N/N passing, no change needed").

### U6 (optional, only if U1–U5 all green) — a real end-to-end smoke check
- **Goal (explicitly optional, do not block U1–U5 on this):** if a local Postgres + the dev environment are available, run one real signup through `/signup`, and confirm via `psql` that (a) an `mt."OutboxEmail"` row exists with the expected `IdempotencyKey` (`verify:{userId}:1`), **and** (b) a corresponding envelope row exists in Wolverine's durable queue table in `mt` (the table name is whatever `.IntegrateWithWolverine()` created, discoverable via `\dt mt.*`), and (c) the email is actually dispatched (check the local SMTP sink / Mailpit if one is configured per `docker-compose.yml`), or (if send fails deliberately, e.g. SMTP down) that after the configured 6-cooldown schedule, a `mt."EmailDeadLetter"` row appears — i.e. the *entire* §6.2 retry/dead-letter path this whole change exists to restore, is now actually reachable, end to end. If the environment isn't available, this unit is simply skipped and recorded as such — do not fabricate a passing result.
- **Exit:** handoff-note section `## U6 — e2e (skipped or evidenced)`: either the `psql`/Mailpit evidence, or the word "skipped — environment unavailable" verbatim.

## Definition of done for this plan (all of U1–U5 green)

1. `run_build` green across `Kumunita.Core`, `Kumunita.Web`, `Kumunita.Core.Tests`, `Kumunita.Web.Tests`.
2. `SideEffectHarnessTests` / `EmailDeadLetterCounterTests` / `ClaimShapingInvariantBTests` all pass, unmodified.
3. `IMailerStage.StageAsync`'s public signature is byte-for-byte unchanged; `OutboxEmailHandler.Handle`/`HandleFault` are byte-for-byte unchanged.
4. `Kumunita.Web/Program.cs`'s `AuditPurgeTick` publish still runs after `app.StartAsync()`, and `SchemaBootstrap.ApplyAsync` now also runs after `app.StartAsync()` (no longer before it).
5. The 4 "Wolverine is a Web package" comments are reconciled with reality, each pointing back to this plan file.
6. `docs/plans-milestones/done/m1-step-7-handoff-notes.md` contains exactly one section per completed unit (U1–U5, plus U6 if run), in order, each appended (never rewritten).
