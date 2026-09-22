# U2 — rework OutboxEmailStager to use the U1-pinned API (signature unchanged)

**Status: complete (all steps executed; see the `## U2 — stager reworked` section in
`docs/plans-milestones/done/m1-step-7-handoff-notes.md` for the as-built record, `run_build`
results, and the files-touched table).** This file is the unit's own execution plan,
saved per the unit brief; the authoritative sealed-unit contract is still
`docs/plans-milestones/done/plan-m1-step-7-outbox-email-c3.md` (lines 115–121) — this file
mirrors it and does not supersede it.

## Entry reads (done)

- `src/Kumunita.Core/Identity/IMailerStage.cs` (whole file, 75 lines) — the seam + the
  current `OutboxEmailStager` (store-only, no enqueue).
- `src/Kumunita.Core/DependencyInjection.cs` (whole file, 110 lines) — where `IMailerStage`
  is registered today (`services.AddTransient<IMailerStage, OutboxEmailStager>()`, line 46),
  plus the factory-registration idiom to mirror (`DirectoryService`, `Posts.PostService`,
  `Moderation.ModerationService`).
- `src/Kumunita.Core/Identity/U1PinnedApiProbe.cs` — the U1 compile-verified pin:
  `Wolverine.IMessageContext.PublishAsync<T>(T message) -> ValueTask` (`EnqueueAsync` does
  not exist in 6.33.0 — CS1061; not `IMessageBus.PublishAsync`, which asserts host-started).
- `docs/plans-milestones/done/m1-step-7-handoff-notes.md` — the `## U1 — pinned API` section
  (U1's Exit, R1 escalation check: NOT triggered, and the exact call U2 should use).
- `src/Kumunita.Core/M1DocTypes.cs` (`OutboxEmail` shape, `M1DocTypes.Configure`) — read
  only, not changed (U2 must not alter the document shape — R5).

## Deliverable (closed set — only these 2 files were edited)

1. `src/Kumunita.Core/Identity/IMailerStage.cs`
   - `IMailerStage` + `StageAsync` + `OutboxEmailStager` XML docs reworded to the pinned
     contract (Core now depends on `WolverineFx`; `StageAsync` = store the row **and**
     enqueue the envelope in the ambient Marten transaction, held until the caller's own
     `SaveChangesAsync` commits) — replacing the now-false "Core has no Wolverine
     dependency (ADR 0006-D)" prose (one of the four comment sites U1 flagged; the other
     three remain U5's to reconcile, untouched by U2).
   - `OutboxEmailStager` gains a constructor-injected `Wolverine.IMessageContext`
     (`public OutboxEmailStager(Wolverine.IMessageContext messageContext)`, null-checked)
     and an `async Task` `StageAsync` body:
     `var email = new OutboxEmail { ... }; session.Store(email); await
     _messageContext.PublishAsync(email);` — the U1-pinned call, verbatim.
   - **`IMailerStage.StageAsync`'s public signature (parameter list, names, order, types,
     default value, return type) is byte-for-byte unchanged.**
2. `src/Kumunita.Core/DependencyInjection.cs`
   - `services.AddTransient<IMailerStage, OutboxEmailStager>();` →
     `services.AddTransient<IMailerStage>(sp => new OutboxEmailStager(sp
     .GetRequiredService<Wolverine.IMessageContext>()));` — the exact factory form the
     plan specifies, matching the file's existing factory-registration idiom (no new
     style invented).

Not touched, deliberately, per the unit-series rules ("a unit never edits a file not in
its own Deliverables list"): `U1PinnedApiProbe.cs` (left for U5's sweep — U1's note
explicitly defers the keep-vs-delete call to U5; still compiles clean and remains accurate
documentation of the pinned surface), `OutboxEmailHandler.cs`, `Program.cs`, the three
pinned test files, `M1DocTypes.cs`/`M3DocTypes.cs`/any `FeatureSchemaBase` subclass (R5).

## Exit (all hit)

- `run_build` green on `Kumunita.Core` — ✅ (`run_build` on
  `src/Kumunita.Core/Kumunita.Core.csproj`: build successful).
- `run_build` green on `Kumunita.Web` — ✅ (`run_build` on
  `src/Kumunita.Web/Kumunita.Web.csproj`: build successful).
- Full-solution `run_build` (`Kumunita.slnx`) also run as a belt-and-braces check — ✅
  build successful, 0 errors, no new warnings.
- Handoff-note section `## U2 — stager reworked` appended to
  `docs/plans-milestones/done/m1-step-7-handoff-notes.md` *before* unit exit — ✅ (the two file
  paths changed, the exact constructor signature added, the one-line confirmation that
  `IMailerStage.StageAsync`'s signature did not change, and the exact pinned call used).

## Steps (executed in order)

1. Reword the `IMailerStage` + `OutboxEmailStager` XML docs in
   `src/Kumunita.Core/Identity/IMailerStage.cs` to the U1-pinned contract. ✅
2. Add the constructor-injected `IMessageContext` + `async Task` `StageAsync` body
   (`session.Store(row); await _messageContext.PublishAsync(row);`) to `OutboxEmailStager`
   in the same file — public signature byte-for-byte unchanged. ✅
3. Switch `services.AddTransient<IMailerStage, OutboxEmailStager>();` in
   `src/Kumunita.Core/DependencyInjection.cs` to the plan's factory form resolving
   `Wolverine.IMessageContext` (mirroring the file's existing idiom), with a short comment
   noting Core's new direct `WolverineFx` dependency. ✅
4. `run_build` on `Kumunita.Core` + `Kumunita.Web` (+ full solution) — all green. ✅
5. Append the `## U2 — stager reworked` handoff-note section before exiting. ✅
