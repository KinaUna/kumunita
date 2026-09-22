# M1 step-7 — U1 — pin the exact Wolverine API surface

**Unit of:** `plan-m1-step-7-outbox-email-c3.md` (M1 step 7 — C3 fix)
**Goal:** prove, by compiling against the actual installed WolverineFx 6.33.0 assembly, the exact `Wolverine.IMessageContext` member the `OutboxEmailStager` will call to enqueue the `OutboxEmail` envelope tied to the caller's Marten session transaction.

## Entry reads (per the sealed-unit spec)

| File | Why | Result |
|---|---|---|
| `src/Kumunita.Web/SideEffects/OutboxEmailHandler.cs` | Confirms the consuming side (`Handle(OutboxEmail, ISmtpSender)`) is shape-stable — it does not depend on which *producer*-side API we pin. | `Handle` / `HandleFault` unchanged (plan rule 3: read-only). |
| `src/Kumunita.Web/Program.cs:230-273` | `UseWolverine` block: `UseDurableLocalQueues`, `PublishFaultEvents`, `OnException<SmtpException/TimeoutException>.RetryWithCooldown(backoff)`. Confirms `OutboxEmail` is a **local-only** message (no `/remote` endpoint for it is registered, no `UseTransport` call in this block), so the producer-side enqueue target is the durable local queue that `OutboxEmailHandler.Handle` listens on. | Confirmed. |
| `src/Kumunita.Web/Program.cs:317-331` | The `AuditPurgeTick` `bus.PublishAsync` block and its `WolverineRuntime.AssertHasStarted` comment (lines 324-325). Documents the first-boot ordering trap R1 warns about — `IMessageBus` (host-level) asserts `IHost.Started`; `IMessageContext` (per-scope, resolved from an already-started host DI scope) does not assert that. | Confirmed — this is exactly the asymmetry U1's exit is supposed to capture. |
| `snippets/wolverine-durable-email-handler.cs` | The repo's own canonical "durable email handler" shape (the `Handle` body that `OutboxEmailHandler` was copied from). No enqueue primitive in it (it *is* the consumer). | Confirmed — producer-side and consumer-side are separate; nothing in the snippet pins the producer API. |
| `docs/wolverine/marten-integration-outbox.md` | The *prose-level* guarantee — "messages cascaded/`yield return`d inside a Marten transaction are held until that Marten session's `SaveChangesAsync()` commits" — that the C3 rework is restoring. Confirms the mechanism exists but does **not** name the producer-side member on `IMessageContext`. | The prose describes the *behavior*, not the *method name*. The exact member has to be verified by compilation (U1's actual deliverable). |

## Deliverables (the closed set)

1. `src/Kumunita.Core/Kumunita.Core.csproj` — add `<PackageReference Include="WolverineFx" Version="6.33.0" />` pinned to the same version `Kumunita.Web.csproj` uses (R4 — do not let a transitive bump drift). The preceding comment block documents the new accepted Core→Wolverine reason and the four "Wolverine is a Web package" sites U5 will reconcile.
2. `src/Kumunita.Core/Identity/U1PinnedApiProbe.cs` (new, `internal static class`, **one single** public method `ProbePublish(IMessageContext, OutboxEmail) -> ValueTask`) that compiles the pinned call: `context.PublishAsync<OutboxEmail>(email)`.

Both were implemented before this file was written because the unit is complete — the plan captures the *sequence of decisions*, not a second implementation pass.

## Exit

- `run_build` green on `Kumunita.Core` (and, as a stronger guarantee, on the whole `Kumunita.slnx`). ✅ — verified via `dotnet build Kumunita.slnx -c Debug`: 0 warnings, 0 errors, all four projects (`Kumunita.Core`, `Kumunita.Web`, `Kumunita.Core.Tests`, `Kumunita.Web.Tests`) green.
- `docs/plans-milestones/done/m1-step-7-handoff-notes.md` — the `## U1 — pinned API` section appended, with the exact `IMessageContext` member + signature recorded verbatim. See the handoff note below / in that file for the record.

## Notes / decision log

- The plan text (`plan-m1-step-7-outbox-email-c3.md` line ~112) offered `IMessageContext.EnqueueAsync<T>(T)` as the *canonical* candidate. **U1's compiler verdict, against the actual 6.33.0 assembly:** the member is named **`PublishAsync<T>`** (not `EnqueueAsync`) and returns **`ValueTask`** (not `Task<Envelope>`). Both discrepancies are flagged in the handoff note per the Exit requirement ("explicitly flag if it differs from what the reference doc's prose implied").
- `EnqueueAsync` is **absent** from `IMessageContext` in 6.33.0 (CS1061 in a scratch probe). This is not the kind of "fall back to `IMessageBus.PublishAsync`" move R1 warns about: `IMessageContext.PublishAsync` is scoped per-message-bus and participates in the ambient Marten transaction via `.IntegrateWithWolverine()` without the host-started assertion that `IMessageBus.PublishAsync` has.
- U3's "verification-only" scope still stands, but it must re-verify the runtime semantics of `IMessageContext.PublishAsync` against a live booted host — what this unit has proved is only the compile-time surface (the exact member name, the return type, and the fact the Core-only compile unit resolves it cleanly).

## U1 Exit (checklist)

- [x] `run_build` green on `Kumunita.Core` with `WolverineFx` referenced (verified, also green on the whole solution).
- [x] Handoff-note section `## U1 — pinned API` appended to `docs/plans-milestones/done/m1-step-7-handoff-notes.md` before unit exit.
- [x] Pinned member + signature recorded verbatim (`IMessageContext.PublishAsync<T>(T) -> ValueTask`).
- [x] Divergence from the doc's prose named (doc names the *concept*, not the *member*; the canonical name `EnqueueAsync` offered as a hint is in fact absent).
- [x] Four "Wolverine is a Web package" comment sites listed so U5 knows exactly what to reconcile (named with file + line range in the handoff-note `## U1 — pinned API` section).
- [x] R1 escalation checked — NOT triggered: the surface EXISTS (`IMessageContext.PublishAsync<T>(T) -> ValueTask`), just under a different name and return type than the plan's canonical hint (`EnqueueAsync` / `Task<Envelope>`). U2 can proceed without inventing a substitute.
- [x] M1 step 7 U1 complete. (Note: the unit-level plan-tracking tool reported a ghost `step-3-investigate` entry after the finish_plan call that this agent could not locate or close — a plan-state tracking bug; all on-disk Exit artifacts are in place per this checklist.)
