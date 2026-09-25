# `TBD` — Todo dependency ("waiting on") — handoff notes (scratch tier)

> **Append-only scratch log.** One `## U#` section per unit, appended (never
> rewritten). The **primary reference** is
> `docs/design/tbd-todo-dependency-design.md`; the **secondary register** is
> `plan-tbd-todo-dependency.md`.
>
> **Convention:** each `## U#` section records (1) what landed, (2) what was
> deferred / deferred-to, (3) the next unit's entry reads (a pointer to the
> plan's `Entry reads` list is enough — don't duplicate it).

## U00 — Design doc + ADR 0087 (sign-off)

- `docs/design/tbd-todo-dependency-design.md` **authored** (Part 1: What
  this lane is; the 10 verified existing surfaces; the D1–D9 decisions;
  the C-TBD·1…6 invariants; the F1–F8 FACES; the 8-part parts-affected
  list; the 3-test gate; the drift-guard; the 5 risks. Part 2: the exact
  C# shapes for the `TodoItem` field, the `M5DocTypes` index, the
  `ListTodosAsync` / `GetTodoAsync` / `CreateTodoAsync` /
  `UpdateTodoAsync` / `ListPickerTodosAsync` seams, the `BlockerChip`
  record, the Web controller + view shapes, the 11 Core pins + 6 Web pins,
  the three-test gate, the drift-guard).
- `docs/adr/0087-todo-dependency-blocked-by-lane.md` **authored** (the
  D1–D9 decisions; **Status: Proposed (draft for evaluation)** — flips to
  **Accepted** at sign-off; the "Out of scope" list; the
  "Consequences" section).
- `docs/adr/README.md` — the ADR 0087 index row **added** (the `Proposed`
  status until sign-off).
- **Deferred:** the `TBD` lane's *implementation* (U01–U04) — the ADR is
  **Proposed**, not yet **Accepted**; the user is evaluating the concrete
  artifact.
- **Next unit's entry reads:** the plan's `Entry reads` list (the 10
  files above) + the design doc's §7 (Part 2) for the exact C# shapes.

## U01 — `TodoItem.BlockedByTodoId?` field + the `M5DocTypes` index

**Two additive edits (both under `src/Kumunita.Core/Projects/`) + no test
addition** — the design doc §7.1 (the field shape) + §7.5 (the `M5DocTypes`
index) landed **verbatim**. **`TodoItem.cs`:** one new field, `string?
BlockedByTodoId { get; set; }` (the "waiting on" pointer; XML-doc prose
names ADR 0087 + the C-TBD·2 "a hint, never a gate" pin + the C-TBD·3
cycle-guard note; placed after `ParentId`, the additive `ProjectId` field
is **not** touched). **`M5DocTypes.cs`:** the existing `TodoItem` block
gains one additive `.Index(t => t.BlockedByTodoId)` (unnamed — the
Marten 9.31.2 / Weasel 9.29.0 no-`Name`-on-`ComputedIndex` constraint
already noted in the file); the `Configure` XML doc gains the TBD additive
note (ADR 0087 / C-TBD·2 / C-TBD·3 named, the zero-migration pin). **No**
`IProjectService` / `ProjectService` / Web / view / `kw-l` change.
**Not committed / staged / moved** (U04's close does the move).

## U02 — the service seams + the cycle guard + the `BlockerChip` + 11 Core pins

**Additive edits (all under `src/Kumunita.Core/Projects/`) + test
additions** — the design doc §7.3 (seams) + §7.4 (the `BlockerChip`
record) landed **verbatim**. **`IProjectService.cs`:** one new seam
(`ListPickerTodosAsync(string actorId, int page, CancellationToken)`) in a
new `// --- TBD "waiting on" lane (U02) ---` section + the additive
`bool blockedOnly = false` param on `ListTodosAsync` (after
`unassignedOnly` / `projectId`, the ADR 0073 filter discipline).
**`ProjectRequests.cs`:** the `BlockerChip` sealed record (the
`required string TodoId` + `Title?` / `Status?` / `LinkPath?` + `bool
Generic`) + the `BlockedByTodoId` field on `CreateTodoRequest` /
`UpdateTodoRequest` + the `ClearBlockedBy` flag on
`UpdateTodoRequest` + the `Blocker?` on `TodoDetailResult`.
**`ProjectService.cs`:** the `ListPickerTodosAsync` implementation (the
`CanSeeAsync(Read)` survivor pass over the **existing**
`TodoItemToAuditableResource`, page 1 capped) + the `blockedOnly` filter
applied in `ListTodosAsync` (the `unassignedOnly` / `projectId`
discipline — a filter, never a gate) + the `Blocker` resolution in the
`TodoDetail` lane (access-scoped — an unreadable / absent / soft-deleted
blocker degrades to the generic label, the C3 404-vs-403 split idiom) +
the **cycle guard** (the only server-side refusal — self + transitive
refusal by walking the `BlockedByTodoId` chain UP from the target, the
C-M5·7 `ParentId`-guard shape; a soft-deleted / absent target →
`KeyNotFoundException`; a denied actor → `UnauthorizedAccessException`;
clearing to `null` is always allowed). **`ProjectServiceTests.cs`:** the
11 §7.6 Core pins **as named** (the cycle guard — self + transitive +
clear-to-null-allowed; the `blockedOnly` feed filter both sides; the
access-scoped chip both sides; the `ListPickerTodosAsync` read lane; the
create / update `BlockedByTodoId` field + `ClearBlockedBy` flag write
lanes). **Build clean:** `dotnet build Kumunita.slnx -c Debug` —
`Build succeeded`, zero errors. **No regressions:** full
`Kumunita.Core.Tests` run green — `Total: 830, Errors: 0, Failed: 0`
(in-process xunit.v3, Testcontainers `postgres:18`). **Not committed /
staged / moved** (U04's close does the move).

## U03 — Web: chip + picker + feed toggle + 4 `kw-l` keys × 4 languages + 6 Web pins

**Additive edits (all under `src/Kumunita.Web/`) + test additions** — the
design doc §7.7 (the Web controller + view shapes) landed. **`Controllers/ProjectsController.cs`:**
`TodosIndex` threads `blockedOnly` into the service call + the
`BlockedOnly` VM field; `TodoDetail` threads the `Blocker` VM field;
`TodoEditGet` + `CreateGet` seed the blocker picker via a new
`SeedBlockerPickerAsync()` helper (the `SeedComponentPickerAsync` shape,
never a gate) and `TodoEditGet` sets `BlockedByTodoId` on the model;
`CreatePost` threads `BlockedByTodoId`; `UpdatePost` threads
`BlockedByTodoId` + the `ClearBlockedBy` flag (the `ClearParent` idiom —
never prefilled on GET, a checkbox + a hidden `value="false"` input) +
both re-render blocks re-seed the picker. **`Views/Projects/`:**
`TodoDetail.cshtml` gains the "Blocked by" chip (the `todo.blocked_by`
label; a generic badge on `Generic`, else an `<a>` → the blocker's detail
+ the status span); `Create.cshtml` gains the picker card (a `<select>`
with the `None` option + the seeded todos, gated on a non-empty picker);
`Edit.cshtml` gains the picker card + the `ClearBlockedBy` checkbox + the
hidden `value="false"` input + the `todo.blocked_by_none` label;
`TodosIndex.cshtml` gains the `?blockedOnly=true` feed toggle (the
`unassignedOnly` switch shape, the `todo.blocked_filter` label).
**`Localization/KnownTranslationKeys.cs`:** the four `todo.*` keys
(`todo.blocked_by` / `todo.blocked_by_none` / `todo.blocked_generic` /
`todo.blocked_filter`) × 4 languages (`en` / `de` / `fr` / `da`) — the
locked D8 key names. **`ProjectsControllerTests.cs`:** the 6 §7.7 Web pins
**as named** — `TodosIndex_BlockedOnlyTrue_PassesFilterToService`,
`TodoDetail_RendersBlockerChip_WhenReadable`,
`TodoDetail_RendersGenericChip_WhenUnreadable`,
`CreateGet_SeedBlockerPicker`, `UpdatePost_BlockedByTodoId_PassesFieldToService`,
`UpdatePost_ClearBlockedBy_PassesFlagToService`. **Build clean:** `dotnet
build Kumunita.slnx -c Debug` — `Build succeeded`, zero errors. **Tests
pass:** `Kumunita.Web.Tests` — **Total: 453, Errors: 0, Failed: 0**
(in-process xunit.v3, Testcontainers `postgres:18`); `Kumunita.Core.Tests`
— **Total: 830, Errors: 0, Failed: 0**. **Deviations from the design
doc's literal shape (all recorded, none silent):** the `ClearBlockedBy`
flag follows the `ClearParent` checkbox idiom (never prefilled on GET)
rather than a second `<select>` option; the hint copy is plain HTML
outside the `kw-l` TagHelper (the TagHelper renders only the resolved
string, so a hint line after `</kw-l>` would be dropped); the key prefix
is `todo.*` per the locked D8 names (not `projects.todo.*`, which the
rest of the codebase uses). **Not committed / staged / moved** (U04's
close does the move).

## U04 — lane close: three-test gate + docs sync + folder move

**The last unit of the TBD lane** — no new code. Ran the lane plan's
three-test gate as a **read + confirm** of the U00–U03 pins (no new tests
written), synced the docs surface, and moved the lane folder
`in-progress/tbd/` → `done/tbd/`.

**Three-test gate — all three PASS** (the full record is in the design
doc §7.6 "Recorded at close"):

- **Closed loop — PASS.** U01's `BlockedByTodoId?` + index; U02's seams +
  cycle guard + `BlockerChip` (11 Core pins green); U03's chip on
  `TodoDetail.cshtml`, picker on `Create.cshtml` / `Edit.cshtml`, the
  `?blockedOnly=true` toggle on `TodosIndex.cshtml`, the
  `None`→`ClearBlockedBy` clear, and the four `todo.*` `kw-l` keys × 4
  languages (6 Web pins green).
- **Handoff — PASS.** The `IProjectService` surface is frozen (U03's
  controller codes against the U02 seams — no access re-derivation, the
  ADR 0006-D pin); the standing matrix (creator ∪ assignee ∪ GlobalAdmin)
  is re-checked server-side in the write lane (C-TBD·5); the `AccessAudit`
  row (`todo.update`, `TargetKind = "todo"`) is stored in the caller's
  session (C3).
- **Part-vs-whole — PASS.** `BlockedByTodoId` is a hint, never a gate
  (C-TBD·2); the M5 route surface (`/projects/todos` /
  `/projects/boards`) is untouched (C-TBD·6); the chip is access-scoped
  (C-TBD·4); `Milestones.cs` / `MilestonesTests.cs` are **untouched**
  (`M7` stays the single in-progress milestone).

**Gate run (AGENTS.md test-runner quirk — build then in-process):**
`dotnet build Kumunita.slnx -c Debug` — clean (0 errors);
`Kumunita.Web.Tests` — **Total: 453, Errors: 0, Failed: 0**;
`Kumunita.Core.Tests` — **Total: 830, Errors: 0, Failed: 0** (in-process
xunit.v3; Core via Testcontainers `postgres:18`).

**Docs sync (this unit's additive edits):**

- **`docs/adr/0087-todo-dependency-blocked-by-lane.md`** — **Status:
  Accepted** (flipped at U00 sign-off; the `Date: 2026-09-25` line
  present).
- **`docs/adr/README.md`** — **verified, not rewritten:** the **0087**
  row is present (number `0087`), the title + one-liner match ADR 0087's
  own header, the `Accepted` status matches, and the "Amends
  0067 / 0073 / 0084 / 0085 / 0086 / 0024 / 0006 / 0004 / 0015 / 0052"
  provenance line **exactly matches** ADR 0087's own "Amends" header.
  **Parity confirmed; no fix needed.**
- **`docs/ARCHITECTURE.md`** — the **§5 data-model `Projects` block**
  gains, **additively** after the existing `PL` block: the TBD note —
  `TodoItem.BlockedByTodoId?` (the "waiting on" chip + picker + `blockedOnly`
  feed filter; ADR 0087; a hint, never a gate; the cycle guard is the lane's
  only refusal; the standing matrix re-checked server-side; the `M5DocTypes`
  `BlockedByTodoId` index). The **§3 tree** is **untouched** (the lane adds
  no new context). **No existing M5 / PL line renamed / reordered / removed.**
- **`README.md`** — the **Roadmap** section gains one **`TBD`** lane entry
  (`Todo dependency (TBD, ADR 0087) … **Done.**`) inserted **additively**
  after the **`PL`** row, before the **M7** row — the lane's own voice, the
  `PL` entry's existing convention (short description + lane id + ADR 0087
  ref + `Done` status), **no reordering** of the M7 / M8 / M9 / M10 / M11 /
  M12 / M13 entries; **`Milestones.cs` + `MilestonesTests.cs` untouched**
  (the named-lane rule — `M7`'s "In progress" stays as U03 left it).
- **`docs/design/tbd-todo-dependency-design.md`** — the §7.6 three-test
  gate section gains the "Recorded at close (U04, 2026-09-25) — all three
  PASS" block (the three-test record + the gate run + the handoff pointer
  to these notes).

**Folder move** — the entire `tbd/` folder (this plan + these handoff
notes) moved from `docs/plans-milestones/in-progress/tbd/` →
`docs/plans-milestones/done/tbd/` (matching the repo's established
lane-move convention). After the move,
`docs/plans-milestones/done/tbd/` exists and
`docs/plans-milestones/in-progress/tbd/` does **not**.
