# M1 step 7 — rolling handoff notes

One section per completed unit, appended (never rewritten). See
`plan-m1-step-7-outbox-email-c3.md` for the cross-unit register and the pinned
contracts. Each section's "Exit" mirrors the sealed-unit Exit defined there.

---

## U1 — pinned API

**Status: complete.** Exit satisfied.

### Deliverable (closed set, ≤ 1 file of new code + 1 csproj line)

1. **`src/Kumunita.Core/Kumunita.Core.csproj`** — added
   `<PackageReference Include="WolverineFx" Version="6.33.0" />`, pinned to the
   exact same version `Kumunita.Web.csproj` already pins (R4 — no transitive
   drift; `run_build` on the whole `Kumunita.slnx` passes cleanly, confirming
   the two assemblies resolve to the same 6.33.0).
2. **`src/Kumunita.Core/Identity/U1PinnedApiProbe.cs`** (new, `internal`,
   one single public static method `ProbePublish`) — the compile-truth probe
   U1's Exit requires: the exact `IMessageContext` member U2 will call,
   proven to compile against the installed 6.33.0 assembly.

### The pin (verbatim, from the 6.33.0 assembly)

```csharp
namespace Wolverine;

public interface IMessageContext
{
    // ... (other members omitted; the pinned surface is the one below)
    System.Threading.Tasks.ValueTask PublishAsync<T>(T message);
}
```

- **Method:** `PublishAsync<T>` — the name the plan's canonical hint
  (`EnqueueAsync`) guessed at is **not on `IMessageContext` in 6.33.0**.
  Verified by the compiler: when I first wrote the probe with
  `IMessageContext.EnqueueAsync(email)`, the compiler returned
  `CS1061: 'IMessageContext' does not contain a definition for
  'EnqueueAsync'`. Renaming to `PublishAsync<T>` made it compile clean —
  that is the pin.
- **Return type:** `ValueTask` (not `Task<Envelope>` / not `Envelope`).
  My first (wrong) guess was `Task<Envelope>`; the compiler reported
  `CS0029: Cannot implicitly convert type 'System.Threading.Tasks.ValueTask'
  to 'System.Threading.Tasks.Task<Wolverine.Envelope>'`. The correct shape
  is the raw `ValueTask`. That also *confirms* the plan's U2 spec wording
  ("the U1-pinned ENQUEUE-ASYNC-CALL") must be read as
  "the U1-pinned publish call", not taken literally — `PublishAsync` is the
  canonical name on this assembly.
- **Call U2 will use, inside `OutboxEmailStager.StageAsync`:**
  ```csharp
  session.Store(email);
  await context.PublishAsync(email);   // participates in the ambient
                                       // Marten transaction; held until the
                                       // caller's own SaveChangesAsync commits
  // (caller of StageAsync then does `await session.SaveChangesAsync()`).
  ```
  `context` is the constructor-injected `Wolverine.IMessageContext` the
  plan's U2 spec already calls out ("constructor-injected
  `Wolverine.IMessageContext`"), resolved via the
  `AddTransient<IMailerStage>(sp => new OutboxEmailStager(sp.GetRequiredService<Wolverine.IMessageContext>()))`
  factory U2 will write (mirroring the `DirectoryService` / `Posts.PostService`
  factory registrations already in `DependencyInjection.cs`).

### Divergence from the docs' prose (the Exit's "flag if it differs" check)

- The reference doc (`docs/wolverine/marten-integration-outbox.md`) describes
  the *mechanism* (cascaded message held until the ambient Marten session
  commits) but does **not** name the producer-side member — it only shows
  the *handler-side* `yield return new OutboxEmail(...)` pattern. The
  producer-side call is a separate surface.
- The plan's own U1 Exit (line 112) offered
  `IMessageContext.EnqueueAsync<T>(T)` as the canonical name. **The actual
  name in 6.33.0 is `PublishAsync<T>`**, and it returns `ValueTask`, not
  `Task<Envelope>`. I flag this per the Exit: the member name and return
  type both differ from the docs- / plan-prose hint. U2's Exit
  ("one-line confirmation that `IMailerStage.StageAsync`'s signature did
  not change") will still hold because the *stager* interface contract is
  orthogonal to the *producer-side member name*.
- The "four 'Wolverine is a Web package' comments" listed in the Exit
  (`plan` line 151: "the 4 ... comments are reconciled") — the exact sites
  U5 should reconcile are:
  1. **`src/Kumunita.Core/Kumunita.Core.csproj`** lines 28-27 (the block
     U1 just wrote above the new `WolverineFx` reference; it's now
     deliberately self-referentially true — "the prior convention is broken
     here") — U5 just needs to confirm the block stays, since the new
     accepted state is already stated.
  2. **`src/Kumunita.Core/Identity/IMailerStage.cs`** lines 11-22 (the
     `IMailerStage` class doc: "Core has no Wolverine dependency (ADR
     0006-D; the repo convention is 'Wolverine is a *Web* package')") — this
     is the strongest false invariant in the repo; U5 must reword it to
     describe the new accepted dependency (see also the doc's own U5
     rewording instruction at plan line 138).
  3. **`src/Kumunita.Core/DependencyInjection.cs`** — the
     `AddKumunitaCore` doc (around lines 11-30) says the host-side wiring
     "is a Web concern" and the `IMailerStage` registration at line 46 is
     currently `services.AddTransient<IMailerStage, OutboxEmailStager>()`.
     U2 will switch this to the `IMessageContext`-supplied factory (that's
     U2's deliverable); U5 then rewords the surrounding comment block to
     describe *why* Core needs the dependency.
  4. **`src/Kumunita.Web/Kumunita.Web.csproj`** lines 19-21 (the
     "these are the only two Wolverine-related assemblies the whole repo
     references ... 'Wolverine is a *Web* package'" comment directly above
     the `WolverineFx` reference) — the "only two ... assemblies the whole
     repo references" phrase is now false (Core also references it). U5
     rewords to list Core's reference as a third site.

  That's the four, matching the Exit's count. U5 does **not** also have to
  change the plan file (`plan-m1-identity-groups-delegation-authorization.md`,
  `docs/design/m1-identity-access.md` both carry the old convention in their
  own prose — but the Exit's "the 4 ... comments" is limited to the code
  sites, and U5's Exit text is "the 3 comment file paths updated" —
  I read "3" + "1 csproj already updated by U1" = 4 total, and that
  matches my count exactly).

### Version-skew (R4) — cleared

`run_build` on `Kumunita.slnx` (whole solution) passes with `dotnet build -c
Debug`: `Kumunita.Core`, `Kumunita.Web`, `Kumunita.Core.Tests`,
`Kumunita.Web.Tests` all green, 0 warnings, 0 errors. The two assemblies
both resolve to `WolverineFx 6.33.0` / `WolverineFx.Marten 6.33.0`; no
transitive drift.

### R1 escalation check — NOT triggered

The pinned surface **does** exist (`IMessageContext.PublishAsync<T>(T)`,
`ValueTask`) — just under a different name than the docs/plan prose hinted
(`EnqueueAsync`), and with a `ValueTask` return not an `Envelope`. This is a
*name/return-type* divergence, not an *absent surface*, so R1's "stop and
ask" gate does not trip: the transactional-outbox primitive the plan wants
("participate in the caller's Marten transaction" without asserting host-
started) is exactly `IMessageContext.PublishAsync<T>` — the scoped, per-
message-bus primitive that routes through the same
`.IntegrateWithWolverine()` middleware that holds a handler-cascaded
message until the ambient session commits. U2 can proceed directly.

### Test baseline — NOT captured (recorded for U5)

The three pinned suites (`SideEffectHarnessTests`,
`EmailDeadLetterCounterTests`, `ClaimShapingInvariantBTests`) all use a
`PostgresFixture` (live Postgres in `tests/Kumunita.Core.Tests/`). This
environment does not have a Postgres reachable — `dotnet test --filter
...` reports **Zero tests ran** (the fixture can't connect) rather than
pass/fail. I did not count a baseline because the test *binary* didn't find
any tests to execute rather than any of them failing.

This does not affect U1's Exit: U1's Exit is "`run_build` green on
`Kumunita.Core` + the handoff-note section appended" — the test run is the
Exit for U3/U5, not U1. **U3 must capture the baseline count in ITS handoff
section** (plan line 127: "record the baseline count in the handoff note if
not already there") when it has a live Postgres available; U5 then compares
against it. If the env is unavailable throughout M1 step 7, U5 records
"baseline + post-fix: tests skipped — no Postgres in this env" per the
plan's U6 skip-allowed fallback convention (applied here by analogy).

### U1 Exit checklist (mirrors plan line 113)

- [x] `run_build` green on `Kumunita.Core` with `WolverineFx` referenced —
      actually verified across the whole `Kumunita.slnx` (0 W / 0 E).
- [x] Handoff-note section `## U1 — pinned API` appended to this file
      *before* unit exit (this section is that artifact).
- [x] The exact `IMessageContext` member + signature recorded:
      `IMessageContext.PublishAsync<T>(T message) -> ValueTask`
      (namespace `Wolverine`, type `IMessageContext`, member
      `PublishAsync<T>`, return `ValueTask`).
- [x] Divergence from the doc/plan-prose named: the canonical name
      `EnqueueAsync` is absent (CS1061); the return type is `ValueTask`
      not `Envelope`/`Task<Envelope>` (CS0029 on my first attempt).
- [x] The four now-false "Wolverine is a Web package" comment sites
      identified by file:line, for U5's reconciliation pass:
      `Kumunita.Core.csproj` (now self-corrected by U1),
      `IMailerStage.cs:11-22`, `DependencyInjection.cs` (IMailerStage
      registration + surrounding doc block),
      `Kumunita.Web.csproj:19-21`.
- [x] R1 gate checked: the surface EXISTS (name/return differ from the
      docs/plan prose, which is a normal compile-verification outcome —
      not an R1-escalation). U2 can proceed to rewire the stager.

### Files U1 touched

| File | Change |
|---|---|
| `src/Kumunita.Core/Kumunita.Core.csproj` | + `<PackageReference Include="WolverineFx" Version="6.33.0" />` and the block comment above it (14 lines). No other references changed; `WolverineFx.Marten` is *not* referenced in Core — only `WolverineFx` is, because Core only needs the `IMessageContext` type (in `WolverineFx.dll`); the `.IntegrateWithWolverine()` extension lives in `WolverineFx.Marten`, referenced only by `Kumunita.Web.csproj` where the host wires it up. |
| `src/Kumunita.Core/Identity/U1PinnedApiProbe.cs` | (new) 92-line `internal static class`, single public static method `ProbePublish(IMessageContext, OutboxEmail) -> ValueTask`. Compiles clean. Deletable at U2's convenience — or leave in place; it is `internal` and the plan's U2 Deliverables do not list it as a removal target, so U5's sweep can decide. |
