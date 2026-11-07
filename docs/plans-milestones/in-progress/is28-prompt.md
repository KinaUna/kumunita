---
id: is28-prompt
title: "IS28 prompt — Controller: scope, override gate, audit row"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS28 — Controller: scope, override gate, audit row

## What you are doing

You are the **twenty-eighth working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS27b are committed.

ADR 0299 (IS25) is the spec: the **content-free** instance export is the
default (IS27 made the old `ExportAsync(actor, ct)` delegate to
`ContentFree`). A **full** export (the complete M11 archive — every private
row, every PII field) must require an **active `AdminOverride`** (the
operator-granted break-glass path, ADR 0003), be audited `Via: BreakGlass`,
and be visible to the operator in the view. This unit wires the controller,
the service audit row, and the view to that rule.

## The one thing to get right

**`Full` is gated, `ContentFree` is not.** The controller reads an optional
`?scope=full` query parameter. Default (absent or `content-free`) →
`ExportAsync(actor, ContentFree, ct)`. `scope=full` → check
`BreakGlassProbe.HasActiveAsync(store, actor, ct)` first; **`Forbid()` if
false**, else `ExportAsync(actor, Full, ct)`. The service's **single** audit
row changes with the scope: `Action = "portability.export.content-free"` /
`"portability.export.full"`, `Via = Admin` / `BreakGlass` respectively.
Exactly one audit row is still emitted — the one-audit-row shape (C-M11·6)
is preserved.

**Do not widen `IAuthorizationService`** (ADR 0006). The gate calls
`BreakGlassProbe.HasActiveAsync` directly — that is the whole point of IS26
extracting it.

## Constraint that keeps existing tests green

`tests/Kumunita.Web.Tests/AdminPortabilityControllerTests.cs` (34 tests,
IS27b left them green) constructs the controller as:
`new AdminPortabilityController(portability, localization, null)`.
If you add `IDocumentStore` to the constructor (required for
`BreakGlassProbe.HasActiveAsync`), make it the **last** parameter with a
**`= null`** default so this call still compiles. In the test's `Export`
actions (the three existing ones), `scope` is absent → the `ContentFree`
path → no `IDocumentStore` needed → the tests pass unchanged.

## Deliverables (≤ 4 files)

1. **`src/Kumunita.Web/Controllers/AdminPortabilityController.cs`** —
   the `Export` action accepts `string? scope = null`. When
   `scope == "full"`: `actor = ActorId(User) ?? ""`; if
   `IDocumentStore` is null (test floor) **or**
   `!await BreakGlassProbe.HasActiveAsync(store, actor, ct)` →
   `return Forbid()`. Else call
   `portability.ExportAsync(actor, PortabilityExportScope.Full, ct)`.
   Default path: `portability.ExportAsync(actor, PortabilityExportScope.ContentFree, ct)`.
   The response headers (`X-Content-Type-Options`, `Content-Disposition`)
   are unchanged. Add `IDocumentStore? store = null` as the **last**
   constructor parameter (DI resolves it in production; `null` in the
   test floor).

2. **`src/Kumunita.Core/Portability/PortabilityService.cs`** — in the
   `ExportAsync(actorId, scope, ct)` body, the single audit row
   (currently `Action = "portability.export"`, `Via = Admin`) becomes:
   `Action = scope == Full ? "portability.export.full" : "portability.export.content-free"`,
   `Via = scope == Full ? AccessVia.BreakGlass : AccessVia.Admin`.
   One row, still emitted after the archive is built (a failed build
   throws before this point, so no audit row for a refused export).
   Update the doc comment on the overload to reflect the scope-aware
   `Action`/`Via`.

3. **`src/Kumunita.Web/Views/AdminPortability/Index.cshtml`** — the
   export section (lines ≈ 25–39) becomes **two** buttons:
   - **"Export (content-free)"** → `~/admin/portability/export` (the
     existing link; the default, no gate). A small text under it:
     "Structure, public content, and the admin's own data. No private
     messages, no restricted posts, no PII contact fields."
   - **"Full export (operator-gated)"** → `~/admin/portability/export?scope=full`.
     A small text: "The complete archive — every private row, every PII
     field. Requires an active operator-granted `AdminOverride`
     (break-glass). Audited `Via: BreakGlass`. Residents are notified."
   Both use `<kw-l>` keys. No inline handlers (P-8).

4. **`tests/Kumunita.Web.Tests/AdminPortabilityScopeTests.cs`** (new) —
   the three tests below.

## Entry reads (only these — no whole-file reads)

- `src/Kumunita.Web/Controllers/AdminPortabilityController.cs` (whole file,
  under 200 lines — the current `Export` action + the constructor).
- `sed -n '155,215p' src/Kumunita.Core/Portability/PortabilityService.cs`
  (the `ExportAsync(actorId, scope, ct)` body + the audit-row block).
- `src/Kumunita.Web/Views/AdminPortability/Index.cshtml` (lines 20–40,
  the export section).
- `sed -n '124,150p' tests/Kumunita.Web.Tests/AdminPortabilityControllerTests.cs`
  (the `Build()` helper — the constraint that must keep compiling).
- `src/Kumunita.Core/Authorization/BreakGlassProbe.cs` (whole file, under
  70 lines — the `HasActiveAsync` signature).
- `grep -n "enum AccessVia" -A12 src/Kumunita.Core/Authorization/Decision.cs`
  (confirm `BreakGlass` is a member).
- `docs/adr/0299-instance-export-operator-gated.md` (the Constraints section).
- The SEC protocol: `docs/plans-milestones/plan-improve-sec.md` per-unit
  contract (D8) — read only the entry reads, edit only the deliverables,
  no PII, pause on drift, never edit around a failing test.
- Test-runner form (xunit.v3 in-process `-class`, **not**
  `dotnet test --filter`): `docs/archive/lanes/sec/sec-handoff-notes.md` (SEC U3).

## Tests (3 new, `AdminPortabilityScopeTests`)

- `Export_Default_IsContentFree` — call `Export()` with no `scope`
  parameter; assert the service was called with
  `PortabilityExportScope.ContentFree`; assert the result is a
  `FileResult` (200, stream).
- `FullExport_WithoutOverride_Forbidden` — call `Export(scope: "full")`
  with a controller whose `IDocumentStore` is `null` (the test floor) **or**
  a substitute `IDocumentStore` whose `BreakGlassProbe` path returns `false`.
  Assert the result is a `ForbidResult` (403). No service call is made.
- `FullExport_WithOverride_AuditedBreakGlass` — use a real Marten boot
  (reuse the existing break-glass test setup — `grep -ln "BreakGlass"
  tests/Kumunita.Web.Tests/*.cs`) with an active `AdminOverride` row for
  the test actor; call `Export(scope: "full")`; assert the result is a
  `FileResult`; then read the `AccessAudit` row from the store and assert
  `Action == "portability.export.full"` and
  `Via == AccessVia.BreakGlass`.

## Verification

- [ ] Build green (`dotnet build`).
- [ ] `AdminPortabilityScopeTests` green (3/3) via the xunit.v3 runner form.
- [ ] `AdminPortabilityControllerTests` **still** green (34/34) — unchanged,
      but run it to prove no regression (the `Build()` helper must still
      compile; the `ContentFree` default path must not touch
      `IDocumentStore`).
- [ ] `PortabilityRoundTripTests` still green (13/13) — the service's
      audit-row change must not break the export path.
- [ ] No PII in code, tests or notes.
- [ ] One `## IS28` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
      (files, the public names changed, tests + pass count, next-unit notes,
      anything noticed — 3–8 lines per the per-unit contract).
- [ ] One commit.

## Definition of done (IS28)

- [ ] `Export()` with no scope → `ContentFree`, 200, audited
      `Action = "portability.export.content-free"`, `Via = Admin`.
- [ ] `Export(scope: "full")` without an active override → `Forbid` (403),
      no service call, no audit row.
- [ ] `Export(scope: "full")` with an active override → 200, audited
      `Action = "portability.export.full"`, `Via = BreakGlass`.
- [ ] View shows two buttons with the operator step explained.
- [ ] `IAuthorizationService` unchanged (ADR 0006).
- [ ] `AdminPortabilityControllerTests` 34/34; `PortabilityRoundTripTests`
      13/13; build green; no PII.
- [ ] One commit, one handoff section.
- [ ] **Next unit: IS29** (Resident transparency entry, D10) — an
      `INotificationEmitter` notice to every non-guest resident on a full
      export, kind `instance.full-export`, stating the date and that the
      operator authorised it, with no actor name. Deliverables ≤ 3: the
      emitter call in the full branch, the `NotificationKinds` constant +
      translation key (appended), and the tests
      `FullExport_NotifiesEveryResident`,
      `ContentFreeExport_NotifiesNoOne`.
