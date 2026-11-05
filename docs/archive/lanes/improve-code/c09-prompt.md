# C09 — Controllers off Marten

## What you are doing

You are the **tenth** agent on the **IMPROVE-CODE** lane (plan: `docs/plans-milestones/plan-improve-code.md`). Your unit is **C09**, the tenth of 14. It is a **thin-HTTP** unit (E10, *Distributed fragmentation*; ADR 0006 §D "route + authz + shape" — the controller is the seam, the service is the part): a large number of controllers (the plan's snapshot: **21 of 67**) query or write Marten directly, and the C00 ratchet's *controllers-on-Marten* gate currently **allowlists 34 of them** — a grandfathered list that lets the fragmentation stand. The C07/C08 splits already gave you the home for most of the work: `IPersonalNoteService` (Notes), `IBookmarkService` (Bookmarks), `IPageService` (Page), and the C08 `ITodoService` / `IBoardService` are interfaces with impls; `ModerationService` (Moderation) and `DocumentService` (Document) are **concrete classes already injected** into their controller; and `EscalationConfig` is a **gap** (no Core owner yet — the controller is the sole writer, the only Core hit being `Portability/ConfigExport`). **You move the Marten reads/writes out of the controller and behind the owning service** (its interface where one exists, the concrete class otherwise) — no new ADR (this is ADR 0006 §D's existing doctrine, not a new decision).

**The goal in one sentence:** a controller is a *thin seam* — it resolves the actor, calls a service method, and shapes the result; it does **not** open a document session. Whatever still has to touch Marten (e.g. pure diagnostics) goes on the allowlist **with a real reason**, and the allowlist **shrinks from 34 toward ≤ 5**.

**How this differs from C07/C08:** those split *one god part into siblings*. C09 pulls *domain work out of the thin seam and into the part it already implies* — the inverse direction, but the same "the controller never re-derives access" discipline (the `EventController` precedent: "route + authz + shape"). C07/C08 are shipped and in-flight inside the services you will now call through.

**You are NOT** doing C00–C08 (all shipped) or C10–C13. No new feature, **no route or rendered-HTML change**, no document-schema change, **no new ADR**. You do NOT split long Core methods (C10), do NOT clean Razor/TS/upload/picker-stub duplicates (C11), and do NOT turn the seam-test rule from report-only to a gate (C12). **The one SEC-adjacent exception (E11):** `ModerationController.IsInActorScopeAsync` reads `ModeratorAssignment` itself (an access decision outside `IAuthorizationService`) — you **move it verbatim** off the controller and into `ModerationService` (introduce an `IModerationService` seam if you want it interface-shaped) with a `// TODO ❓ SEC` marker, behaviour unchanged; routing it through `IAuthorizationService` is the **SEC lane's** decision, not yours.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, clean tree at `62f9bd80` (C08 just landed).
- The *controllers-on-Marten* gate: `tools/code/code-baseline.json` → `controllers_marten_allowlist` holds **34 entries**, all carrying the reason `"initial baseline (IMPROVE-CODE C00)"`. The gate (in `CodeMetrics`) **flags any controller that touches `IDocumentStore` / `IDocumentSession` / `IQuerySession` and is not allowlisted**. C09's job is to **shrink** that list — move the Marten work into services so the controllers drop off it — and re-reason the survivors.
- **Worst offenders (the E10 list):** `ModerationController` 22 Marten calls · `NotesController` 12 × `SaveChangesAsync` · `ProjectsController` 6 · `Bookmarks` 6 · `Page` / `Document` / `EscalationConfig` 5 each. These are your first targets; the rest of the 34 follow in the same pass.
- After C07/C08 the **owning services mostly exist** and are already DI-registered, in three shapes: (a) **interface + impl** — `IPersonalNoteService` (Notes), `IBookmarkService` (Bookmarks), `IPageService` (Page); (b) **concrete class already injected** into its controller — `ModerationService` (Moderation), `DocumentService` (Document) — so a seam may need to be **introduced as an `I*Service`** first (the ADR 0006 interface + impl convention) or added to the concrete class; (c) **a gap** — `EscalationConfig` has **no Core owner** (the controller is the sole writer; the only Core hit is `Portability/ConfigExport`), so C09 may need to *create* the owner there. **Confirm the real owner in the code before you write the seam** — a method that already exists is a re-point, not a new one.
- `EventController.Calendar` (303 lines / CC 32) is a *long method* that also happens to do calendar Marten reads — C09's scope names extracting `EventCalendarViewModelBuilder`; the broader long-method split is **C10's** (don't over-reach).
- Commit style: `feat(improve-code): C09 — <short description>` (conventional commits, em dash, no ADR in subject).
- Gates to keep green: `tools/code/code-check.ps1` (the ratchet, incl. the controllers-on-Marten gate) and `tools/docs/improve-check.ps1` (docs gates). Both are green now.

## Entry reads

- **ADR 0006 §D** (`docs/adr/0006-module-boundary-contracts.md`) — "the authorization path is unique" + the thin-HTTP / "route + authz + shape" seam doctrine; the E11 access-decision rule.
- The plan's **C09** section, **E10** (controllers doing domain work) and **E11** (access decisions outside `IAuthorizationService` → SEC handoff), and the **part-vs-whole rule** (`docs/philosophy/in-code.md` § "The ratchet, the split, the comment").
- **The controller-Marten report** — the gate's own list. Reproduce it: `pwsh tools/code/code-report.ps1` (or read `controllers_marten_allowlist` in the baseline), so you work the real set of 34, not the plan's "21" snapshot.
- `ModerationController` and `NotesController` (the two worst + the two that get their **first xunit test** this unit), and each other allowlisted controller you touch.
- The **owning services** where each controller's reads/writes land: `IPersonalNoteService` (Notes), `IBookmarkService` (Bookmarks), `IPageService` (Page) — interfaces + impls; `ModerationService` (Moderation), `DocumentService` (Document) — concrete classes already injected; and `EscalationConfig` — a **gap** (no Core owner; the only Core hit is `Portability/ConfigExport`, so confirm what should own it before you build a seam).
- `EventController` + its calendar read — the `EventCalendarViewModelBuilder` extraction target.
- The existing controller tests under `tests/Kumunita.Web.Tests/` — you are **adding** the first tests for `ModerationController` and `NotesController`.
- `tools/code/code-baseline.json` (the `controllers_marten_allowlist` entries you **shrink or re-reason**, plus the method/interface entries you may add as service seams grow).

## Do

**One controller (or one tight cluster of small ones) per commit**, in this order (each commit is a full unit: service seam added + controller re-pointed + tests touched/moved + baseline re-reasoned, and it is green):

1. **`NotesController`** → move the 12 `SaveChangesAsync` writes behind `IPersonalNoteService` (the owning write lanes). *Add the first xunit test for `NotesController`.*
2. **`ModerationController`** → move the 22 Marten reads behind `ModerationService` read seams (introduce an `IModerationService` seam if you want it interface-shaped); move `IsInActorScopeAsync` **verbatim** into `ModerationService` with `// TODO ❓ SEC` (behaviour unchanged). *Add the first xunit test for `ModerationController`.*
3. **`Bookmarks` / `Page` / `Document` / `EscalationConfig`** (the 5-each cluster) → re-point each to its owning service: `IBookmarkService`, `IPageService`, `DocumentService`; for `EscalationConfig` **confirm the owner** (it currently has none — the controller is the sole writer, so C09 may create the seam here).
4. **`ProjectsController`** (6) → re-point the surviving reads through the C08 `ITodoService` / `IBoardService` (the C08 split already created the owners — do not add new seams for something C08 already split out).
5. **Extract `EventCalendarViewModelBuilder`** for `EventController.Calendar` (303 / 32) — the one long-method item named in C09's scope.
6. **The rest of the 34** — work the full gate list down: each controller either (a) loses its Marten touch and **drops off** the allowlist, or (b) genuinely still needs it (e.g. pure diagnostics / a live-query you are not restructuring) and is **re-reasoned** with a specific reason replacing `"initial baseline (IMPROVE-CODE C00)"`.

Each commit:
- **Move the Marten work behind the owning service's interface** — add the read seam or write lane, and re-point the controller call. **Route + authz + shape stay in the controller**; the actor (subject + role set) is resolved as it is today and passed to the service (the C08 base-controller idiom). Do **not** re-derive standing in the controller — that's the service's (E11).
- **Re-point, don't rename:** when the method already exists on the service (the C07/C08 owners), just switch the caller. When you must add a service method, keep it part-vs-whole-legal (one aggregate's concern, ≥ 1 real caller).
- **IsInActorScopeAsync (E11):** move **verbatim** into `ModerationService` (introduce an `IModerationService` seam if you want it interface-shaped), keep the `ModeratorAssignment` read, add the `// TODO ❓ SEC` marker. **Do not** route it through `IAuthorizationService` — that's the SEC lane.
- **Tests:** the moved Marten read is now a **service** concern — the controller test mocks the service seam (the C07/C08 `Substitute.For<...>()` pattern, *not* a live store, where the seam is pure). Add the **first** xunit tests for `ModerationController` and `NotesController` (they currently have none — E12).
- **Baseline (the C09 proof):** remove each controller from `controllers_marten_allowlist` as it drops off the gate; **re-reason** the survivors (replace the generic C00 reason with a specific one). Run `--tighten` for the method/interface entries that shift (a service seam that lands over the 20-method interface / 80-line / CC-15 ceilings gets a fresh entry at its current value). **Shrink-only:** you may *reduce* the allowlist and add over-ceiling entries; you may **not** add a controller back or raise a `max` without an `adr:` reason.

## Do NOT do

- Do NOT **rename or re-sign** any existing service method — C09 adds seams and re-points callers; it does not reshape the C07/C08 interfaces (that's their territory).
- Do NOT **change any route** (path or verb) or any served HTML — a re-pointed controller returns the identical result shape; the Razor doctrine (U06 / `docs/dev/razor-verification.md`) still applies to anything you do render.
- Do NOT **re-route `IsInActorScopeAsync` through `IAuthorizationService`** or otherwise change the SEC decision — move it verbatim, mark it `// TODO ❓ SEC`, and leave the routing call to the SEC lane (E11).
- Do NOT **split long Core methods** (`AddKumunitaCore`, `DeleteCoreAsync`, the portability pipeline, `EmitAsync`) or split `EventController.Calendar` beyond the named `EventCalendarViewModelBuilder` — that's **C10**.
- Do NOT clean **Razor views, `rich-editor.ts`, upload clones, the `IcsWriter`↔`TodoIcsWriter` clone, or the picker-stub trio** — that's **C11**.
- Do NOT turn the **seam-test rule** from report-only into a gate — that's **C12**.
- Do NOT **add a controller back** to the allowlist, or raise any baseline `max` / `count` without an `adr:` reason — the ratchet is shrink-only (a raise without a reason fails CI).
- Do NOT touch `tools/docs/improve-check.ps1` or the docs gates.

## Exit criteria (all must pass before you finish)

1. **`controllers_marten_allowlist` shrinks from 34 to ≤ 5** — and every survivor carries a **specific** reason (no more blanket `"initial baseline (IMPROVE-CODE C00)"`). The gate's "controller on Marten without an allowlist entry" list is empty.
2. **`ModerationController` and `NotesController` each have at least one xunit test** that names them (E12) — the first tests they have ever had.
3. **`IsInActorScopeAsync`** now lives on the Moderation owner (the `ModerationService` seam, behind a `// TODO ❓ SEC`), and `ModerationController` no longer reads `ModeratorAssignment` itself — behaviour unchanged.
4. **No route (path/verb) or served HTML changed** — a controller you re-pointed returns the identical result shape.
5. **`EventCalendarViewModelBuilder`** extracted; `EventController.Calendar` no longer owns the calendar Marten reads + view-model shaping.
6. The moved Marten reads/writes resolve **through the owning service** at runtime (each re-pointed controller's live flow returns the same data as before).
7. `pwsh tools/code/code-check.ps1` → exit 0 (allowlist shrunk, no controller re-added, no max raised without an `adr:`).
8. `pwsh tools/docs/improve-check.ps1` → exit 0.
9. `dotnet build` clean; `dotnet test` (both test projects) green.

## Practical notes

- **Work the gate's real list, not the plan's snapshot.** The plan says "21 controllers"; the baseline allowlist currently holds **34** (C00 grandfathered a wider set, and C07/C08 added `BoardsController` / `ProjectsBaseController` / `TodosController`). Reproduce the list with `pwsh tools/code/code-report.ps1` and drive that to ≤ 5.
- **Owner-first: confirm the service before you add a seam.** After C07/C08, most Marten work has an obvious owner (`IPersonalNoteService` write lanes, `ModerationService` reads, the C08 `ITodoService`/`IBoardService`) — but three owners are concrete classes or gaps, not interfaces (`ModerationService`, `DocumentService`, `EscalationConfig`). A method that already exists on the service is a **re-point**, not a new seam — only add a seam when the controller does something no existing method covers, or create the owner where it's a genuine gap.
- **`--tighten` re-writes the allowlist for you** (it drops allowlisted files that no longer touch Marten and re-adds new ones). Run it after each controller commit, then **hand-edit the reasons** for the survivors (replace the generic C00 reason). The baseline uses `\u003C`/`\u003E` escapes — prefer `--tighten` + a targeted reason edit over a full textual rewrite (the same lesson from C07/C08).
- **The C08 base-controller idiom is your model.** `ProjectsBaseController` already resolves the actor (subject + role set) and passes it to service seams that "call through, not re-derive" — `NotesController` / `ModerationController` should end in that shape.
- **E11 is a move, not a decision.** Moving `IsInActorScopeAsync` off the controller into the Moderation owner does **not** change who may do what — the `ModeratorAssignment` read stays as-is under a `// TODO ❓ SEC`. If you are tempted to "fix" the standing while you're there, **stop** — that's the SEC lane and would scope-creep C09.
- **One controller per commit** keeps each diff reviewable and lets the allowlist shrink step by step — do not batch all 34 into one commit (the C07/C08 "one unit per commit" lesson).
- **Test the controller, not the store.** After the move, the controller test mocks the service seam (`Substitute.For<IModerationService>()` where you introduce that interface, or the concrete `ModerationService`/`IPersonalNoteService` owner) — the C07/C08 pattern — so the first `ModerationController` / `NotesController` tests do **not** need Testcontainers/Postgres for the seam you just moved. Reserve a live store only for a read that genuinely still hits Marten (a C10-scale leftover).
- The **xunit.v3 discovery quirk** (`dotnet test` → "Zero tests ran / Exit code 5") is in `docs/dev/testing-and-ci.md`; run a class with the in-process runner: `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll -class "Kumunita.Web.Tests.<Class>"`.
- **Terminal output can be swallowed** in this sandbox — redirect long runs to `.tmp/` and read the file; verify file existence with a read rather than trusting an empty terminal.
