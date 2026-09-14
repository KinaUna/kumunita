# GU U10 — run + record GU acceptance gate + Web tests

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-controls.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

Two things: **(1)** the **Web data-shape tests** for the U07 view models
(`GuardianViewModelsTests`) — the VM projections are a **frozen contract**
(the U07 pin), so they get the same data-shape test discipline the other
lanes' VMs have. **(2)** **run the full GU gate** (the 11 Core seam tests
from U09 + the Web tests) via the reliable path and **record the result** in
the design doc's `### Run result (GU acceptance gate — <date>)` section. This
is the lane's **acceptance gate** (U01's pinned section) — the single place
that says "GU is green."

## Context you need (read these first, in this order)

1. `docs/design/guardian-controls-design.md` § **`## Pinned contract` →
   `### Acceptance gate (U10 records)`** (U01 pinned the gate's shape: the
   11 Core tests + the Web VM tests, all green, recorded with a date) — the
   *primary* source.
2. `docs/design/...` — the **Web VM test precedent**: grep
   `tests/Kumunita.Web.Tests/*ViewModels*Tests.cs` / `*ViewModel*Tests.cs`
   for the lane that already pins a `record` VM's **exact field projection**
   (the M2b `InvitationViewModel` / M2 `GroupViewModel` "exact N-field
   projection" tests). U10's `GuardianViewModelsTests` mirror **that** shape
   (assert the field set + the `record`-ness, not behavior — the VMs are
   data, not logic).
3. `src/Kumunita.Web/Models/GuardianViewModels.cs` (U07) — the **four** VMs
   to pin: `ChildAccountItem`, `MembershipEditorModel`,
   `PendingInvitationItem`, `AddChildForm`. The test asserts each VM's
   **exact** field set + types (the "drift-guard: no fields beyond the pin"
   rule the `GroupViewModel` doc-comment names).
4. `tests/Kumunita.Web.Tests/` — the Web test harness (the xunit.v3
   `Microsoft.Testing.Platform` shape; **no** `PostgresFixture` needed — the
   VM tests are pure data, no store). Mirror an existing
   `tests/Kumunita.Web.Tests/…Tests.cs` for the `using` set + the test-class
   header.
5. `AGENTS.md` § **Running the tests** — the **reliable path** (`dotnet build`
   then `dotnet exec <assembly>`; `dotnet test` / VS Test Explorer are
   broken on this machine). U10 runs **both** assemblies this way.

## Deliverables (2 files)

### 1. `tests/Kumunita.Web.Tests/GuardianViewModelsTests.cs` (new)

- A `public class GuardianViewModelsTests` (no fixture — pure data).
- One test **per VM** (4 tests) asserting the **exact field projection**
  (the lane's "exact N-field projection (…pin)" discipline):
  - `ChildAccountItem_Is_Exact_Three_Field_Projection` — `ChildId`,
    `DisplayName`, `Blocked` (types `string`, `string`, `bool`).
  - `MembershipEditorModel_Is_Exact_Four_Field_Projection` — `ChildId`,
    `GroupIds`, `CommunityIds`, `PendingInvitations` (types `string`,
    `IReadOnlyList<string>` ×3).
  - `PendingInvitationItem_Is_Exact_Three_Field_Projection` — `GroupId`,
    `GroupName`, `InvitedAt` (types `string`, `string`, `DateTimeOffset`).
  - `AddChildForm_Is_Form_Model_With_Three_Required_Fields` — `DisplayName`,
    `Email`, `Password` (all `[Required]`; `Email` is `[EmailAddress]`,
    `Password` is `[DataType(DataType.Password)]`).
  - Each test constructs the VM with a known value set and asserts `
    typeof(…).GetProperties()` (or the `record`'s `Deconstruct`/init args)
    returns **exactly** the pinned fields — no more, no less (the drift-guard).
    (Match the **exact** assertion idiom the existing VM tests use — read one
    first; the idiom is the precedent, not this prose.)
- The file's doc-comment: pins that these are the GU Web VM projections (the
  U07 pin) + references ADR 0028 §C (the five actions the VMs render).

### 2. `docs/design/guardian-controls-design.md` (modify — **append only**)

- Append the section (at the end of the file, after the **Pinned contract**):

```
### Run result (GU acceptance gate — <DATE>)

- **Core (11 pinned seam tests):** <pass/red per test — the U09 snapshot,
  re-run here as the gate>
- **Web (VM data-shape tests):** <pass/red per test>
- **Build:** `dotnet build Kumunita.slnx -c Debug` — <green/red>
- **Gate verdict:** <GREEN — GU is accepted / RED — drift pause, see the
  failing test + the seam it implicates>
```

  `<DATE>` is the run date (the day U10 runs). **Do not** rewrite anything
  above this line (the drift-guard: the design doc is append-only here).

## Run + record (the gate)

Run via the **reliable path** (AGENTS.md — one self-contained script, no
here-strings, no `$vars` surviving between calls):

```powershell
dotnet build Kumunita.slnx -c Debug ; dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll ; dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

(If any of the 11 Core tests are **red**, the gate is **RED** — do **not**
weaken an assertion to make it pass; the red is a drift signal against the
U02–U06 seam impl. Record the red + the implicated seam, and stop (a drift
pause). Clean up any leftover Postgres containers with `docker container
prune`.)

## Exit

`dotnet build Kumunita.slnx -c Debug` green. The 11 Core tests + the 4 Web VM
tests are **all green** (or the gate is recorded RED with the drift-signal
named). The design doc has the `### Run result (GU acceptance gate —
<DATE>)` section appended (the **append-only** drift-guard held). Handoff note
(append): 6–8 lines starting `## U10 — acceptance gate + Web tests` — (a) the
11 Core test results (one line each), (b) the 4 Web VM test results (one line
each), (c) the build line, (d) the **gate verdict** (GREEN/RED + the
implicated seam if RED), (e) the design-doc append confirmation (the section
name + the date), (f) the `docker container prune` note, (g) any compile
warnings.
