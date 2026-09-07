# U5 plan — M1 step 7: full-verification sweep + doc-comment reconciliation

Sealed unit U5 of `plan-m1-step-7-outbox-email-c3.md`. Follows U1 (pinned API),
U2 (stager reworked), U3 (call sites verified — 0 files changed), U4 (boot
order fixed).

## Goal

1. **Verify** the post-U1–U4 state: full build green across all 4 projects; the
   three pinned test suites (`SideEffectHarnessTests`, `EmailDeadLetterCounterTests`,
   `ClaimShapingInvariantBTests`) pass unmodified — record the pass counts as the
   baseline.
2. **Reconcile** the remaining "Wolverine is a Web package" comment site(s) that
   are now false after U1 added `WolverineFx` to `Kumunita.Core`.
   Comments only — no code.

## Entry reads (done, all confirmed)

1. `plan-m1-step-7-outbox-email-c3.md` lines 135-139 — the sealed U5 spec:
   Goal, Entry reads, Deliverables (≤ 4 files, comments-only), Exit.
2. `m1-step-7-handoff-notes.md` — U1's section (lines 82-108) listing the
   exact four comment sites; U2's section (lines 202-207) confirming it already
   reconciled site #2 (`IMailerStage.cs`) and #3 (`DependencyInjection.cs`
   registration comment); U3's section (line 326) recording the
   `ClaimShapingInvariantBTests` = 20/20 baseline; U4's section (lines 384-473)
   confirming the boot-order move.
3. `Kumunita.Core.csproj` lines 28-56 — U1 already reconciled site #1 (the
   comment says "intentionally broken here" and lists all four sites).
4. `IMailerStage.cs` lines 22-33 — U2 already reconciled site #2 (the class
   doc says "deliberately broken here" and describes the new dependency).
5. `DependencyInjection.cs` lines 47-53 — U2 already reconciled site #3
   (the `IMailerStage` factory comment describes the new dependency and
   points to `Kumunita.Core.csproj` + `IMailerStage.cs`).
6. `Kumunita.Web.csproj` lines 9-21 — **the one remaining false site**.
   Lines 18-21 state: "These are the only two Wolverine-related assemblies the
   whole repo references (per the repo's own convention — `IMailerStage.cs`
   and `DependencyInjection.cs` both call out 'Wolverine is a *Web*
   package')." — **false** after U1 added `WolverineFx` to Core.
7. `U1PinnedApiProbe.cs` — still exists (92 lines, internal static class,
   `ProbePublish` method). U2's handoff explicitly deferred the keep/delete
   decision to U5. This file is **not** in U5's sealed Deliverables list, so
   per unit-series rules it **stays in place**.

## Deliverables (closed set, 1 file edited)

1. **`src/Kumunita.Web/Kumunita.Web.csproj`** — reword lines 18-21 of the
   comment block above the `WolverineFx` / `WolverineFx.Marten` references:
   remove the now-false "only two … assemblies the whole repo references" and
   the outdated "Wolverine is a *Web* package" convention call-out;
   replace with an accurate statement that `Kumunita.Core` also references
   `WolverineFx` 6.33.0 directly (for the `IMessageContext` enqueue), both
   pinned to the same version (R4). Point back to the plan file.

   Sites 1-3 (Core.csproj, IMailerStage.cs, DependencyInjection.cs) were
   already reconciled by U1/U2 — no edit needed.

2. This file.

3. Appended `## U5 — verified + comments reconciled` section in
   `docs/plans-milestones/m1-step-7-handoff-notes.md`, before unit exit.

## Exit (mirrors plan line 139)

- `run_build` green on all 4 projects.
- `run_tests` on **all three** pinned suites — same pass counts as the
  pre-edit baseline:
  - `SideEffectHarnessTests`: 7/7
  - `EmailDeadLetterCounterTests`: 1/1
  - `ClaimShapingInvariantBTests`: 20/20
  - (Total: 28/28, matching U3's baseline for ClaimShaping + U3's note that
    the other two are env-dependent — they ran green here because Docker
    was available.)
- Handoff-note section `## U5 — verified + comments reconciled` records:
  the 3 comment file paths (sites 1-3 confirmed already reconciled by
  U1/U2, no edit; site 4 `Kumunita.Web.csproj` edited by U5) + the 3
  test-suite results.

## Risks & notes

- **Comments-only change** — the `Kumunita.Web.csproj` edit touches only an
  XML comment. No compile-time or runtime effect.
- **Test environment**: `SideEffectHarnessTests` and
  `EmailDeadLetterCounterTests` depend on `PostgresFixture` (Testcontainers
  `postgres:18`). U1's note said the environment "has no live Postgres"
  (Docker may have been unavailable at that time). Docker **was** available
  during this unit and both suites ran green.
- **PowerShell encoding trap** (repo instruction): all file writes use the
  editor/file tools, not terminal heredocs.
