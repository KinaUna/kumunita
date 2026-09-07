# U4 plan — M1 step 7: fix the first-boot ordering trap (`Program.cs`)

Sealed unit U4 of `plan-m1-step-7-outbox-email-c3.md`. Follows U1 (pinned API),
U2 (stager reworked), U3 (call sites verified — 0 files changed).

## Goal

Move `SchemaBootstrap.ApplyAsync` (the thing that runs `FirstBootSeeder`, whose
email-staging half now does a real Wolverine `IMessageContext.PublishAsync`) to
run **after** `app.StartAsync()` in `Kumunita.Web/Program.cs`, so first boot no
longer trips the `WolverineRuntime.AssertHasStarted` constraint the repo's own
`AuditPurgeTick` comment documents — matching the existing, already-correct
`AuditPurgeTick` block rather than contradicting it.

## Entry reads (done, all confirmed)

1. `src/Kumunita.Web/Program.cs` lines 270–340 — the exact sequence today:
   `builder.Build()` (L275) → `SchemaBootstrap.ApplyAsync` (L279, **before**
   pipeline) → pipeline setup (L281–312) → `app.StartAsync()` (L315) →
   `AuditPurgeTick` publish block (L317–331, with the `AssertHasStarted`
   comment at L324–328) → `WaitForShutdownAsync` (L333–340).
2. `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (whole file, 118 lines) —
   `ApplyAsync` creates its own async scope off the root `IServiceProvider`,
   resolves `IDocumentStore` / `AppDbContext` / `IMailerStage` etc., applies
   migrations, runs the seeder only on a pristine DB. **Nothing inside it
   requires the host to be not-yet-started** — safe to call post-`StartAsync`.
3. `src/Kumunita.Web/SideEffects/OutboxEmailHandler.cs` +
   `docs/wolverine/marten-integration-outbox.md` (via the pinned contract in
   the plan file) — confirms the seeder's `IMessageContext.PublishAsync` is
   part of the same started-host machinery as the `AuditPurgeTick` publish.
4. `docs/plans-milestones/m1-step-7-handoff-notes.md` — the U1/U2/U3 sections:
   stager now constructor-injects `Wolverine.IMessageContext` and calls
   `PublishAsync`; `IdentityService.RegisterAsync` and
   `FirstBootSeeder.SeedAdminAsync` already stage-then-commit correctly, so the
   *only* remaining trap is host-start ordering — exactly what this unit fixes.

## Deliverables (closed set, 1 file)

1. `src/Kumunita.Web/Program.cs` — move the single line
   `await SchemaBootstrap.ApplyAsync(app.Services);` (plus its 2-line
   rationale comment) from its current position (line 279, before the
   pipeline/`StartAsync`) to immediately **after** `await app.StartAsync();`
   (line 315) and **before** the `AuditPurgeTick` block. One-line move, not a
   rewrite — the HTTP pipeline middleware order is untouched.
2. This file.
3. Appended `## U4 — boot order fixed` section in
   `docs/plans-milestones/m1-step-7-handoff-notes.md`, before unit exit.

## Exit

- `run_build` green on `Kumunita.Web` (and, since Core is a project
  dependency and shares the build, a whole-solution pass as well — no new
  warnings).
- Handoff-note `## U4 — boot order fixed` records: the exact before/after line
  numbers of the moved block, and a one-line confirmation that the
  `AuditPurgeTick` block's relative position to `StartAsync` did **not**
  change (it stays strictly after `StartAsync`, and now strictly after
  `ApplyAsync` as well).

## Risks & notes

- **R2 (load-bearing, from the plan):** without this move, first boot on a
  pristine DB would succeed in staging the `OutboxEmail` row but crash the
  envelope commit at host-start assert time. This unit closes that trap.
- `SchemaBootstrap.ApplyAsync` still runs on **every** boot (not just first
  boot) — the pristine/forward-only split is inside it (`DbBootstrap
  .IsPristineAsync` gates only the seeder). Moving it to post-start does not
  change which work runs, only *when* it runs relative to host start — and
  since it's a boot-time (not per-request) call, running it after `StartAsync`
  adds negligible time to the "ready to serve" window; the trade-off is
  required by the C3-envelope invariant.
- **PowerShell encoding trap (repo instruction):** all file writes in this
  unit use the editor/file tools, not terminal here-strings — here-strings
  plus a non-column-0 closing delimiter hang the interactive shell, and
  `$variables` don't even reliably survive between Copilot-issued terminal
  commands (microsoft/vscode#286106). Keep terminal usage to `run_build`
  only.
