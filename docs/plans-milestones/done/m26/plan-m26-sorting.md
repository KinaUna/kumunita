# M26 — Sorting — sealed unit register

> **In progress.** This is the **plan** for M26 (Sorting), split into
> **sealed units** sized for a **~32K-context fresh agent one at a time**,
> exactly like `done/m25/plan-m25-upload-limits.md` and
> `done/m3/plan-m3-posts-components.md`. The **primary** reference tier — the
> exact C# seams every unit codes against — is the design doc
> `docs/design/m26-sorting-design.md` (authored in U1/U2, **not yet
> implemented**). The **secondary** tier is this file (unit registry +
> deliverables + exit criteria). The **scratch** tier is
> `docs/plans-milestones/in-progress/m26-handoff-notes.md` (one appended
> `## U#` section per unit, never rewritten; created by U1, moved to
> `done/m26/` by the close unit U18).
>
> **What this is:** the README/Milestones M26 promise — **"feeds, lists &
> search results are sortable by various properties in increasing or
> decreasing order (the M7 sibling that pagination and filtering shipped
> without)."** It is a **contract + wiring** lane over the *existing* paged
> Core seams: it does **not** add a new document, a new bounded context, a new
> `AccessAction`, or a new schema surface. It **adds** a pure Core `SortSpec`
> value object + a per-surface **closed allowlist** of sort keys, threads an
> **additive, default-preserving** `SortSpec?` parameter through every paged
> list/search seam, ships **one shared `_Sort` control** that rides the M7
> `_Pager` links, and closes a **`sort.*` kw-l set** × en/de/fr/da.
>
> **The one thing every unit must respect:** `Core` stays **HTTP-free**
> (ADR 0006-D, C-SORT·3) — the sort is a **pure** value object on
> `(key, direction)`; the `?sort=`/`?dir=` query-param parsing and the
> `ActionResult`/view mapping are **Web-only**. And **sorting is a display
> facet, never a gate** (C-SORT·4): it re-orders the *already-authorized*
> candidate set and emits **zero** new `AccessAudit` row / `AccessAction`.
> And the **single-in-progress milestone** contract (C-SORT·6): M26's close
> (U18) flips M26 → `StatusDone` and **promotes M27 → `StatusNext`**.

## Understanding

M7 (ADR 0090) shipped **pagination** (`?page=` + the `HasMore` signal + the
`_Pager` partial + `PagedViewModel`) and the filter reset. **M3** shipped the
actual ordering, and it has been **hardcoded in Core** ever since — every
paged list seam does a fixed `OrderBy`/`OrderByDescending` over the *already-
authorized* candidate set:

| Surface group | Core seam (all paged, `Skip/Take` over authorized candidates) | Current order (hardcoded) |
|---|---|---|
| Post feeds | `PostService.ListFeedAsync` / `ListAllFeedAsync` / `ListGroupFeedAsync` → `FeedResult` | `OrderByDescending(p => p.Created)` |
| Events | `EventService.ListUpcomingAsync` / `ListPastAsync` / `ListGroupEventsAsync` → `EventPage` / `GroupEventFeedResult` | upcoming `OrderBy(Start)`; past `OrderByDescending(Start)` |
| Projects | `ProjectService.ListTodosAsync` / `ListBoardsAsync` / `ListProjectsAsync` / `ListGoalsAsync` → `TodoPage`/`BoardPage`/`ProjectPage`/`GoalPage` | `OrderByDescending(Created)` |
| Misc lists | `AnnouncementService.ListVisiblePagedAsync` → `AnnouncementPage`; `DocumentService.ListAsync` → `DocumentListResult`; `InventoryService.ListItemsAsync` → `ItemPage` | `OrderByDescending(Created)` |
| Tags + people | `TagService.ListPostsByTagPagedAsync`/`ListPagesByTagPagedAsync`; `ProfileFindService.FindPeopleByTagAsync`/`FindPeopleByBioAsync` → `TagPostPage`/`TagPagePage`/`ProfileTagPage`/`ProfileBioPage` | posts `OrderByDescending(Created)`; pages `OrderBy(Created)`; people by display name |
| Search | `SearchService.SearchSurfaceAsync` → `SearchSurfacePage` (per-surface `OrderByDescending(...Created)`) | `OrderByDescending(Created)` per surface |

**M26 makes that order user-selectable.** Concretely:

- **A pure Core `SortSpec`.** `record SortSpec(string Key, bool Descending)` + a
  pure `SortKeys.Parse(raw, allowedKeys, defaultKey, defaultDir)` that maps a
  raw `?sort=` string against a surface's **closed allowlist** → a `SortSpec`,
  falling back to the surface's **default key/direction** on an unknown key or
  invalid direction (never a property/reflection lookup from the raw string —
  C-SORT·1).
- **Additive, default-preserving.** Each paged seam gains an optional
  `SortSpec? sort = null` parameter. **`null` reproduces the current hardcoded
  order exactly** (C-SORT·2 — behavior-preserving). When non-null, the seam
  orders by the spec's key/direction **plus a stable tie-breaker** (C-SORT·5).
  The Core change is an *optional parameter + a `switch` over the closed key
  set*; no seam signature is broken (the existing `HasMore`/paging contract is
  untouched).
- **One shared Web control.** A `SortViewModel` + a `_Sort` partial (mirroring
  the `_Pager` partial) render a `<select>` (or link set) of the surface's
  allowed keys × the two directions. The current `?sort=`/`?dir=` values are
  carried on the **pager links** via the existing `PagedViewModel.FilterParams`
  mechanism (C-SORT·8) so prev/next preserve the sort, exactly like M7's
  filters.
- **Closed kw-l labels.** The sort control's option labels (`sort.*`) close a
  new kw-l set × en/de/fr/da with an English floor (the M25 `documents.*` /
  `a11y.*` precedent), so the control is i18n-complete.

**What is *not* M26 (named non-decisions, pinned in the design doc):**
- **The people catalog** (`DirectoryController.Index` →
  `DirectoryService.ListAsync`) — **not paged** today (loads the whole visible
  set, no `?page=`); M26 does **not** page it, so it is **out of scope** for
  M26's sort surface (it is a non-paged catalog, not a feed/list). If paging
  lands later, sorting follows then.
- **The events calendar day/week/month views** (`Event/Calendar.cshtml` +
  `?view=`) — **display-only** temporal layouts where the *position is the
  content* (a day is a day, a column is a column); not a sortable list.
- **Kanban column order** (`Projects/BoardDetail`) — the card order within a
  column is **content the user sets** (the `Order` field), not a list sort.
- **The bookmarks list** (`BookmarksController.Index` → `IBookmarkService.ListAsync`)
  — **personal, grouped, not paged** (the M17 `BookmarkListResult.Groups`
  shape); a personal read, not a community feed/list.
- **Any relevance scoring.** M8 search relevance is **frozen**; M26 sorts the
  *surface's* date/title keys, never invents a relevance score.
- **Any `AccessAudit` / `AccessAction` / schema / bounded-context change.**
  Sorting is a display facet (C-SORT·4/7).

**The one thing to internalize before writing code:** the **default-preserving**
rule (C-SORT·2). **No existing surface's order changes** when a viewer does not
choose a sort. The moment an unsorted read's output differs from today's, that
is a regression — the pinned tests (U3–U9) exist to catch it. And the
**single-in-progress** contract: U1 enforces the M25-`StatusDone`/M26-
`StatusNext` precondition (already true today), and U18 flips M26 → `StatusDone`
and promotes M27.

## Assumptions

- **Scope (per the README/Milestones M26 title + the user's 2026-10-05
  decision):** In: a pure Core `SortSpec` + `SortKeys.Parse`; an additive
  `SortSpec?` on **all 18 paged list/search seams** (post feeds, events,
  projects, announcements, documents, inventory, tags, people-find, search);
  one shared Web `_Sort` control + `SortViewModel` carried on the M7 `_Pager`
  links; a closed `sort.*` kw-l set × en/de/fr/da; pinned Core + Web tests; the
  ADR 0145 + OPS/README/Milestones sync. **Out (named non-decisions):** the
  people catalog, calendar day/week/month, kanban column order, bookmarks,
  relevance scoring, and any schema/authorization/context change.
- **Two query params:** `?sort=<key>` (the surface-allowed key) and
  `?dir=<asc|desc>`. Absent `dir` → the key's **default direction** (defined in
  the allowlist). Absent `sort` → the surface's **default key + direction**
  (the current hardcoded order). This matches M7's separate-pair query
  convention and rides the `FilterParams` mechanism.
- **Stable ordering (C-SORT·5).** Every ordered query ends with a **unique
  tie-breaker** (`.ThenBy(x => x.Id)` — every doc has a stable string `Id`) so
  a `HasMore` window never shuffles rows between the candidate count and the
  rendered page.
- **Per-key default directions differ by surface** (e.g. events-upcoming
  defaults to `start` **ascending**, events-past to `start` **descending**);
  the allowlist encodes the default direction **per sub-surface**, so
  C-SORT·2 (default-preserving) holds for each.
- **People/name sorts** use `StringComparer.OrdinalIgnoreCase` (the
  `DocumentFolderService` / `TagService` display-name precedent).
- **Nullable sort keys** (e.g. `TodoItem.Due`) pin a defined null-placement
  (nulls last) in the allowlist — a detail U6 locks in the design doc.

## Approach

Four tracks, sequenced — the Core seam before any Web control (the M7 U00→U04
ordering invariant). **Track A (Core):** `SortSpec` + `SortKeys` (pure), then
`SortSpec?` threaded through each paged seam with a closed-key `switch`, one
surface-group per unit, each with pinned Core tests (unsorted-default pin +
each allowed key/dir + invalid-key fallback). **Track B (Web):** the
`SortViewModel` + `_Sort` partial + the pager-carry rule, then each list
surface's view-model `Sort` field + `@Html.Partial("_Sort", ...)`, one
surface-group per unit, each with pinned Web tests. **Track C (i18n):** the
`sort.*` kw-l set × en/de/fr/da + a resolution pin. **Track D (close):** ADR
0145 + OPS/README/Milestones sync + the M26→M27 milestone flip.

Every unit ends with **build green** (and its pinned tests passing). The last
unit (U18) appends the final handoff section and the M26→M27 handoff note so
M27's start precondition (M26 `StatusDone`) is honest.

## Workflow — handoff protocol for fresh-context agents

This milestone is executed as a sequence of **sealed units** (U1–U18 below),
one unit per fresh agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary — the design doc** (`docs/design/m26-sorting-design.md`) — the
  exact C# seams U3–U15 must match (authored U1/U2; **locked** once written).
- **Secondary — this file** (`docs/plans-milestones/plan-m26-sorting.md`) —
  the unit registry with each unit's deliverables and exit criteria.
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/m26-handoff-notes.md`) — one section per
  unit, appended (never rewritten). Each unit writes exactly one short section
  before it exits; the next unit reads only that section + its own entry-read
  list.

**Per-unit template** (each `U` below follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file list, 3–5
files <~300 lines each, no full-repo scan; the design-doc section cited is
named); **Deliverables** (a closed set of new/modified files, ≤ ~4 files /
~600 LOC, no misc cleanups); **Exit** (`dotnet build` green for the touched
projects + the pinned tests pass; handoff-note entry appended *before* any
follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §drift-guard;
(3) never introduces a test whose exact name is not in the §pinned-test list;
(4) never opens a *new* Core seam beyond the additive `SortSpec?` parameter U2
pins — no new interface, no new document, no new `AccessAction`; (5) never
changes an unsorted read's output (C-SORT·2 — default-preserving) outside the
§allowlist pin; (6) if entry reads reveal the design doc or the milestone
contract is out of date, the unit **pauses** and records `## U<m> — Drift
pause` in the handoff note.

**Running the tests (test-runner quirk — read before U3–U15).** Both test
projects use **xunit.v3**; on this machine `dotnet test` / VS Test Explorer
reliably reports "No tests found to run" — that is a **runner bug, not a
failure**. The reliable path (per `AGENTS.md`):

```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```

`Kumunita.Core.Tests` takes ~20 s (Testcontainers `postgres:18`; clean up with
`docker container prune` if killed).

---

## Units (18 total)

### U1 — Start + design doc Part 1 (+ milestone-start precondition)
- **Goal:** author `docs/design/m26-sorting-design.md` Part 1 — **Context,
  Scope (in/out incl. the named non-decisions), Invariants (C-SORT·1–C-SORT·8),
  FACES (F1–F12), and the 18-surface catalog table** — and **create the
  handoff note** with its first section. **Before writing, enforce the
  C-SORT·6 precondition** (below). **No code, no build.**
- **Precondition check (C-SORT·6):** read `src/Kumunita.Web/Milestones.cs`.
  Confirm M25 is **`StatusDone`** AND M26 is **`StatusNext`**. If either is
  false, **STOP and report BLOCKED** (M26 cannot begin until M25 closes).
  Record the exact M25/M26 status in the handoff note; **do not** author the
  design doc. (This is the drift-guard for the single-in-progress contract —
  do not "fix" the milestones yourself in this unit.)
- **Entry reads:** `docs/plans-milestones/done/m25/plan-m25-upload-limits.md`
  (the FACES/invariant template + the three-tier handoff contract to emulate),
  `docs/adr/0090-pagination-and-filtering.md` (the `HasMore` / `_Pager` /
  `FilterParams` / D7 filter-carry contract M26 rides), `docs/design/m7-
  pagination-filtering-design.md` (the D1/D5/D7 decision style to emulate),
  `src/Kumunita.Core/Posts/FeedResult.cs` + `src/Kumunita.Web/Models/
  PagedViewModel.cs` + `src/Kumunita.Web/Views/Shared/_Pager.cshtml` (the
  existing paging shape M26's `SortSpec`/`_Sort` mirror),
  `src/Kumunita.Web/Milestones.cs` (the M25/M26 status; the C-SORT·6
  precondition), `src/Kumunita.Web/Controllers/PostsController.cs` (a
  representative list action that already builds a `Pager`).
- **Deliverables (2 files, new):**
  - `docs/design/m26-sorting-design.md` (Part 1, ~220 lines). Sections:
    - `## Context` — M7 shipped paging + filters; ordering has been hardcoded
      in Core since M3; M26 makes the order user-selectable over the 18 paged
      list/search surfaces; what M26 does *not* do (the non-decisions).
    - `## Scope` — **In:** the `SortSpec` value object + `SortKeys.Parse`; the
      additive `SortSpec?` on the 18 seams; the per-surface closed allowlists;
      the `SortViewModel` + `_Sort` partial + the pager-carry rule; the
      `sort.*` kw-l set × en/de/fr/da; the pinned Core + Web tests; the ADR
      0145 + OPS/README/Milestones sync. **Out (named non-decisions):** the
      people catalog (non-paged), the calendar day/week/month (display-only),
      kanban column order (content), the bookmarks list (personal/unpaged),
      relevance scoring (M8 frozen), and any schema/authorization/context
      change.
    - `## Invariants (pinned for M26)` — the 8 invariants (ids/names **frozen**
      once written):
      - **C-SORT·1** — **closed allowlist per surface**: each surface declares a
        closed set of sort keys; `?sort=` outside the allowlist (or a malformed
        `?dir=`) → the surface's **default key/direction**; never a
        property/reflection lookup from the raw string (no sort-injection).
      - **C-SORT·2** — **default-preserving**: an absent/`null` `SortSpec`
        reproduces the surface's **current hardcoded order exactly** — no
        unsorted read's output changes.
      - **C-SORT·3** — **`Core` stays HTTP-free** (ADR 0006-D): `SortSpec` +
        `SortKeys.Parse` are **pure** Core; the `?sort=`/`?dir=` parsing and
        the view mapping are Web-only.
      - **C-SORT·4** — **sort is a display facet, never a gate**: re-ordering
        never changes *which* rows are authorized; **zero** new `AccessAudit`
        row, **zero** new `AccessAction`, the `CanSeeAsync` call is untouched.
      - **C-SORT·5** — **stable ordering**: every ordered query ends with a
        unique tie-breaker (`.ThenBy(Id)`) so a `HasMore` window never shuffles
        rows.
      - **C-SORT·6** — **single-in-progress milestone contract**: M26 starts
        only after M25 is `StatusDone`; M26's close (U18) promotes M27.
      - **C-SORT·7** — **no new bounded context / document / schema**:
        `SortSpec` is a value object (no Marten doc, no `DocTypes` surface, no
        `SchemaBootstrap` change).
      - **C-SORT·8** — **sort rides the pager**: the current `?sort=`/`?dir=`
        values are carried on the `_Pager` links (the M7 D7 `FilterParams`
        mechanism) so prev/next preserve the sort.
    - `## FACES (pinned, 12)` — F1–F12, each bound to an invariant (names
      **frozen** once written):
      - **F1** `?sort=` absent → the surface's current order is preserved
        exactly (behavior-preserving) — C-SORT·2
      - **F2** `?sort=<allowed key>&dir=asc` → sorted by that key ascending — C-SORT·1
      - **F3** `?sort=<allowed key>&dir=desc` → sorted by that key descending — C-SORT·1
      - **F4** `?sort=<key-not-in-allowlist>` → the surface's default key
        (not an error / 500) — C-SORT·1
      - **F5** `?dir=` invalid/absent → the key's default direction — C-SORT·1
      - **F6** sorting re-orders only the authorized set (a hidden row stays
        hidden regardless of sort) — C-SORT·4
      - **F7** a stable tie-breaker pins order across a `HasMore` window (no
        page shuffle) — C-SORT·5
      - **F8** the pager's prev/next links carry the current `?sort=`/`?dir=` — C-SORT·8
      - **F9** the sort control offers exactly the surface's allowed keys (no
        dead options) — C-SORT·1
      - **F10** a signed-in and an anonymous viewer on a visible surface get the
        same sort behavior (sort is not an authorization axis) — C-SORT·4
      - **F11** no new `AccessAudit` row is emitted by a sorted read vs. an
        unsorted read — C-SORT·4
      - **F12** the `sort.*` labels resolve in en/de/fr/da (the kw-l closed
        set) — the i18n floor
  - `docs/plans-milestones/in-progress/m26-handoff-notes.md` — the first
    section `## U1 — design doc Part 1`.
- **Exit:** design doc Part 1 exists with all sections (Context / Scope /
  Invariants / FACES / the 18-surface catalog); handoff note exists with the
  U1 section (5–7 lines): the **8 invariants** (by id) + the **12 FACES**
  (F1–F12) + the **C-SORT·6 precondition outcome** (M25 `StatusDone` / M26
  `StatusNext` confirmed — or the BLOCKED state + the exact status). **No
  code, no build.**
- **Next unit (U2):** append `## Seams & contracts (Part 2, written by U2)` —
  the exact C# shapes (U3–U9), the per-surface allowlists (proposed, confirmed
  against the models), the Web `_Sort` contract + the pager-carry rule, the
  pinned test names, the three-test acceptance gate, and the drift-guard. See
  `m26-u02.md`.

### U2 — Design doc Part 2 (seams, allowlists, Web contract, test list, gate, drift-guard)
- **Goal:** append `## Seams & contracts (Part 2, written by U2)` to the
  design doc — the exact C# shapes U3–U15 must match, the **18 per-surface
  closed allowlists** (each: key, property, comparator, default direction,
  tie-breaker), the **Web `_Sort` contract** + the **pager-carry rule**, the
  **pinned Core test names** (U3–U9), the **pinned Web test names** (U10–U15),
  the **U17 acceptance gate**, and the **drift-guard**. **No code, no build.**
  Confirm each allowlist's property name + nullability against the actual
  domain models (read them) before locking.
- **Entry reads:** U1's Part 1 (the invariant/FACE table is the primary
  source); the domain models for each surface group — `Posts/Post.cs`,
  `Events/Event.cs`, `Projects/TodoItem.cs`/`KanbanBoard.cs`/`Project.cs`/
  `ProjectGoal.cs`, `Announcements/Announcement.cs`, `Documents/Document.cs`,
  `Inventory/InventoryItem.cs`, `UserInfo/Profile.cs`, `Search/SearchModels.cs`
  (the exact sortable property names + nullability); `src/Kumunita.Web/Models/
  PagedViewModel.cs` + `Views/Shared/_Pager.cshtml` (the `FilterParams`
  mechanism the `_Sort` contract extends); `docs/design/m25-upload-limits-
  design.md` §Seams (the C#-shape transcription style to emulate).
- **Sub-sections to append (under `## Seams & contracts (Part 2, written by U2)`):**
  - **2.1 New Core-owned types (exact C#)** — a new namespace `Kumunita.Core.
    Query` (or `Kumunita.Core.Sorting`): `public sealed record SortSpec(string
    Key, bool Descending)` (Key = a lowercase surface key, e.g. `"created"`);
    `public static class SortKeys` with `public static SortSpec Parse(string?
    key, string? dir, IReadOnlySet<string> allowedKeys, string defaultKey,
    bool defaultDir)` → an allowed key else the default; `dir` of `"asc"`/
    `"desc"`/`null` → `!Descending`/`Descending`/`defaultDir`; anything else →
    default. **Pure, no I/O (C-SORT·3).**
  - **2.2 The 18 per-surface allowlists (proposed — confirm property names in
    the models).** Each entry: `key` (the `?sort=` value) → property,
    comparator, default dir; **tie-breaker** `.ThenBy(x => x.Id)` on all; the
    surface's **default key/dir** = its current order (C-SORT·2):
    - Post feeds (community/all/group): `created`(default,desc), `modified`,
      `title`(OrdinalIgnoreCase).
    - Events upcoming: `start`(default,asc), `created`(desc), `title`; past:
      `start`(default,desc), `created`(desc), `title`; group events: `start`
      (default,asc), `created`.
    - Todos: `created`(default,desc), `modified`, `title`, `due`(nulls last),
      `priority`, `status`.
    - Boards / Projects / Goals: `created`(default,desc), `modified`, `title`.
    - Announcements: `created`(default,desc), `modified`, `title`.
    - Documents: `created`(default,desc), `modified`, `title`, `size`
      (`SizeBytes`).
    - Inventory: `created`(default,desc), `modified`, `name`.
    - Tag posts: `created`(default,desc), `modified`, `title`; tag pages:
      `created`(default,asc), `modified`, `title`.
    - People by-tag / by-bio: `created`(default), `name`(display name,
      OrdinalIgnoreCase).
    - Search: `created`(default,desc) + the surface's `title`/`name` key.
  - **2.3 The additive Core seam rule (C-SORT·2/5).** For **each** of the 18
    seams, add `SortSpec? sort = null`; when `null` the existing `OrderBy…`
    line is **unchanged**; when non-null, replace the ordering with the
    allowlist `switch` + `.ThenBy(x => x.Id)`. **Pin the exact current
    `OrderBy…` line + surrounding context of each seam** (read them) so U3–U9
    edit the right lines. The `HasMore`/paging computation is **untouched**.
  - **2.4 The Web `_Sort` contract + the pager-carry rule (C-SORT·8).**
    `SortViewModel(string BaseUrl, string? CurrentKey, string? CurrentDir,
    IReadOnlyList<(string Key, string Dir)> Options, IReadOnlyDictionary<
    string,string> CarriedParams)`; a `_Sort` partial that renders a
    `<select name="sort">` (or a link set) of the surface's allowed keys ×
    asc/desc, preserving `page` + filters + the toggled sort/dir (exactly the
    `_Pager` `FilterParams` query-pair shape). The list-surface view model
    gains a `SortViewModel? Sort` field (a `null` renders nothing — the
    one-page/no-sort pin); the surface's existing `Pager` `FilterParams` gains
    the `sort`+`dir` pairs so **prev/next preserve the sort** (C-SORT·8).
  - **2.5 Pinned Core tests (exact names).** One file per surface group, e.g.
    `tests/Kumunita.Core.Tests/Query/SortSpecTests.cs` (U3) —
    `Parse_AllowedKey_Applies`, `Parse_UnknownKey_Defaults`,
    `Parse_InvalidDir_Defaults`, `Parse_NullKey_Defaults`,
    `Parse_DirAsc_DescendingFalse`, `Parse_DirDesc_DescendingTrue`; and per
    group (U4–U9) e.g. `PostFeed_SortSpecNull_CurrentOrder` (C-SORT·2 pin),
    `PostFeed_SortCreatedAsc`, `PostFeed_SortModifiedDesc`,
    `PostFeed_SortTitle_Ordinal`, `PostFeed_InvalidKey_DefaultOrder`,
    `PostFeed_StableTieBreakBy_Id` (C-SORT·5) — the analogous names for
    Events/Todos/Boards/Projects/Goals/Announcements/Documents/Inventory/
    Tags/People/Search.
  - **2.6 Pinned Web tests (exact names).** E.g. `tests/Kumunita.Web.Tests/
    SortControlTests.cs` (U10) — `CommunityFeed_SortControl_Renders_AllowedKeys`,
    `CommunityFeed_Pager_Carries_Sort_And_Dir`, `CommunityFeed_SortParam_
    DefaultsWhenAbsent`; the analogous names for each U11–U15 surface group.
  - **2.7 The U17 acceptance gate.** The three-test shape (closed-loop /
    handoff / part-vs-whole) over the M26 surface: (1) a sorted read is
    authorized identically to an unsorted read (C-SORT·4/6); (2) an unknown
    `?sort=` falls back to the default order without error (C-SORT·1); (3) the
    pager links carry the current sort (C-SORT·8).
  - **2.8 Drift-guard.** If U2's model reads reveal a surface's property set
    differs from §2.2's proposal, or a surface is discovered to be non-paged /
    unpaged-different from the catalog, the design doc's allowlist is
    corrected **in place** (U2 owns Part 2) and the discrepancy is noted; a
    later unit that finds the *locked* Part 2 wrong pauses with a `## U<m> —
    Drift pause`.
- **Exit:** the design doc Part 2 exists with all sub-sections (2.1–2.8);
  every allowlist's property name confirmed against the models. Handoff note
  (5–7 lines, `## U2 — design doc Part 2`): (a) the `SortSpec`/`SortKeys`
  shape, (b) the count of confirmed allowlists (18) + any model-confirmed
  correction, (c) the pinned Core test-file count + Web test-file count,
  (d) the U17 gate summary.

### U3 — Core foundation: `SortSpec` + `SortKeys.Parse` + Core tests
- **Goal:** add the **pure Core** `SortSpec` record + `SortKeys.Parse` (the
  closed-allowlist parser, C-SORT·1/3) in a new `Kumunita.Core.Query`
  namespace, with the **pinned pure tests** (U2 §2.5) — `Parse_AllowedKey_
  Applies`, `Parse_UnknownKey_Defaults`, `Parse_InvalidDir_Defaults`,
  `Parse_NullKey_Defaults`, `Parse_DirAsc_DescendingFalse`,
  `Parse_DirDesc_DescendingTrue`. **Build green + the 6 pure tests pass.**
- **Entry reads:** U1's Part 1 (C-SORT·1/3) + U2's §2.1 (the exact `SortSpec`/
  `SortKeys` C#); `src/Kumunita.Core/DependencyInjection.cs` (where a value
  type needs no registration — confirm none is required); a pure-Core test
  precedent (e.g. `tests/Kumunita.Core.Tests/` — an existing `*Tests.cs` over a
  pure function, the xunit.v3 + Testcontainers shape).
- **Deliverables (≤ 2 files, new):**
  - `src/Kumunita.Core/Query/SortSpec.cs` — `record SortSpec(string Key, bool
    Descending)` + `static class SortKeys` (the `Parse` per §2.1). **No
    dependencies, no I/O.** (Optionally split `SortKeys.cs` — keep it in this
    one namespace; do not register anything in DI.)
  - `tests/Kumunita.Core.Tests/Query/SortSpecTests.cs` — the 6 pinned pure
    tests.
- **Rules:** pure only (C-SORT·3) — no Marten, no HTTP, no DI. The `Key` is
  stored as the caller passed it (lowercase-normalized if U2 says so — follow
  §2.1). **Do not** touch any list seam in this unit (that is U4–U9).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green; `dotnet exec
  tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` passes
  (the 6 new pure tests + the existing suite). Handoff note (4–5 lines,
  `## U3 — Core SortSpec`): (a) the `SortSpec`/`SortKeys.Parse` location +
  the DI-confirmation (none needed), (b) the 6 test names that passed,
  (c) the `Key`-normalization note (lowercase / verbatim per §2.1).

### U4 — Core: post feeds (community, all-sections, group) + tests
- **Goal:** add the additive `SortSpec? sort = null` to
  `PostService.ListFeedAsync` / `ListAllFeedAsync` / `ListGroupFeedAsync`
  (+ the `IPostService`/seam signatures U2 §2.3 pin), applying the post-feed
  allowlist (§2.2: `created` default desc, `modified`, `title`) + the
  `.ThenBy(Id)` tie-breaker when `sort` is non-null, and the **pinned Core
  tests** (U2 §2.5, the `PostFeed_*` set). **Build green + the Post tests
  pass.**
- **Entry reads:** U2's §2.2 (post allowlist) + §2.3 (the seam rule + the
  pinned current `OrderBy` lines) + §2.5 (the `PostFeed_*` test names);
  `src/Kumunita.Core/Posts/PostService.cs` (the 3 seams — the exact
  `OrderByDescending(p => p.Created)` lines); `src/Kumunita.Core/Posts/Post.cs`
  (confirm `Created`/`Modified`/`Title` property names + nullability);
  `src/Kumunita.Core/Query/SortSpec.cs` (U3's `Parse`).
- **Deliverables (≤ 3 files):**
  - `src/Kumunita.Core/Posts/PostService.cs` — the 3 seams gain
    `SortSpec? sort = null`; `null` keeps the current `OrderByDescending(Created)`
    **exactly**; non-null applies the allowlist `switch` + `.ThenBy(Id)`.
  - The interface(s) that expose these 3 seams (the `IPostService`/seam U2 §2.3
    pin) — the matching `SortSpec? sort = null` parameter.
  - `tests/Kumunita.Core.Tests/Posts/PostSortTests.cs` — the pinned
    `PostFeed_*` tests (null-default pin, created asc/desc, modified, title
    Ordinal, invalid-key fallback, stable tie-break).
- **Rules:** **C-SORT·2** — the `null` path is byte-for-byte the current order;
  the `HasMore`/`Total` computation is **untouched**; no new audit row
  (C-SORT·4 — the `CanSeeAsync` call is unchanged). **Do not** change the
  authorization path.
- **Exit:** `dotnet build` green; the Core suite passes (the `PostFeed_*`
  tests + the existing suite). Handoff note (4–5 lines, `## U4 — Core post
  feeds`): (a) the 3 seams + the `SortSpec?` param, (b) the `PostFeed_*` test
  names that passed, (c) the C-SORT·2 null-preservation confirmation, (d) the
  tie-breaker pin.

### U5 — Core: events (upcoming, past, group) + tests
- **Goal:** add the additive `SortSpec? sort = null` to
  `EventService.ListUpcomingAsync` / `ListPastAsync` / `ListGroupEventsAsync`
  (+ the `IEventService` seam U2 §2.3 pin), applying the events allowlist
  (§2.2: upcoming `start` default **asc**, past `start` default **desc**,
  `created`, `title`) + the tie-breaker, and the **pinned Core tests** (U2
  §2.5, the `Event*_*` set). **Build green + the Events tests pass.**
- **Entry reads:** U2's §2.2 (events allowlist — note the **per-sub-surface
  default direction**) + §2.3 + §2.5 (the `Event*_*` test names);
  `src/Kumunita.Core/Events/EventService.cs` (the 3 seams — the `OrderBy(
  Start)` / `OrderByDescending(Start)` lines); `src/Kumunita.Core/Events/
  Event.cs` (confirm `Start`/`End`/`Title`/`Created` names);
  `src/Kumunita.Core/Query/SortSpec.cs`.
- **Deliverables (≤ 3 files):** `src/Kumunita.Core/Events/EventService.cs`
  (the 3 seams); the `IEventService` seam (the matching param);
  `tests/Kumunita.Core.Tests/Events/EventSortTests.cs` (the pinned
  `Event*_*` tests — incl. **upcoming-asc-default** vs **past-desc-default**
  + created + title + invalid-key fallback + tie-break).
- **Rules:** **C-SORT·2** — `null` keeps each sub-surface's current direction
  (upcoming asc, past desc — **do not** collapse them to one); `HasMore`
  untouched; no new audit row (C-SORT·4).
- **Exit:** `dotnet build` green; the Core suite passes (the `Event*_*` tests
  + the existing suite). Handoff note (4–5 lines, `## U5 — Core events`):
  (a) the 3 seams + the per-sub-surface default dirs, (b) the `Event*_*` test
  names that passed, (c) the upcoming-asc / past-desc default preservation.

### U6 — Core: projects (todos, boards, projects, goals) + tests
- **Goal:** add the additive `SortSpec? sort = null` to
  `ProjectService.ListTodosAsync` / `ListBoardsAsync` / `ListProjectsAsync` /
  `ListGoalsAsync` (+ the `IProjectService` seams U2 §2.3 pin), applying the
  projects allowlist (§2.2: `created` default desc, `modified`, `title`; todos
  additionally `due`(nulls last)/`priority`/`status`) + the tie-breaker, and
  the **pinned Core tests** (U2 §2.5, the `Project*_*` / `Todo*_*` set).
  **Build green + the Projects tests pass.**
- **Entry reads:** U2's §2.2 (projects + todos allowlist — the `due` nulls-last
  rule) + §2.3 + §2.5 (the `Project*_*`/`Todo*_*` test names);
  `src/Kumunita.Core/Projects/ProjectService.cs` (the 4 seams — the
  `OrderByDescending(Created)` lines); `src/Kumunita.Core/Projects/TodoItem.cs`
  + `KanbanBoard.cs` + `Project.cs` + `ProjectGoal.cs` (confirm the property
  names, esp. `Due` nullability + `Priority`/`Status` types);
  `src/Kumunita.Core/Query/SortSpec.cs`.
- **Deliverables (≤ 4 files):** `src/Kumunita.Core/Projects/ProjectService.cs`
  (the 4 seams); the `IProjectService` seams (the matching params);
  `tests/Kumunita.Core.Tests/Projects/ProjectSortTests.cs` (the pinned
  `Project*_*`/`Todo*_*` tests — incl. the `due`-nulls-last pin + priority/
  status + title + invalid-key fallback + tie-break).
- **Rules:** **C-SORT·2** — `null` keeps the current order; the `due` nulls-
  last rule applies **only** when sorting by `due`; `HasMore` untouched; no
  new audit row (C-SORT·4). **Do not** change the kanban **column** order
  (that is content, a named non-decision).
- **Exit:** `dotnet build` green; the Core suite passes (the `Project*_*`/
  `Todo*_*` tests + the existing suite). Handoff note (4–5 lines, `## U6 —
  Core projects`): (a) the 4 seams + the todo extra keys, (b) the test names
  that passed, (c) the `due`-nulls-last + priority/status pin, (d) the kanban
  column-order non-change note.

### U7 — Core: announcements + documents + inventory + tests
- **Goal:** add the additive `SortSpec? sort = null` to
  `AnnouncementService.ListVisiblePagedAsync`, `DocumentService.ListAsync`,
  `InventoryService.ListItemsAsync` (+ the `IAnnouncementService` /
  `IDocumentService` / `IInventoryService` seams U2 §2.3 pin), applying the
  allowlists (§2.2: announcements `created`/`modified`/`title`; documents
  `created`/`modified`/`title`/`size`; inventory `created`/`modified`/`name`)
  + the tie-breaker, and the **pinned Core tests** (U2 §2.5, the
  `Announcement*_*` / `Document*_*` / `Inventory*_*` set). **Build green + the
  tests pass.**
- **Entry reads:** U2's §2.2 (these 3 allowlists — note documents' `size`
  = `SizeBytes`, inventory's `name`) + §2.3 + §2.5 (the test names);
  `src/Kumunita.Core/Announcements/AnnouncementService.cs` +
  `src/Kumunita.Core/Documents/DocumentService.cs` +
  `src/Kumunita.Core/Inventory/InventoryService.cs` (the 3 seams — the
  `OrderByDescending(Created)` lines); `Announcement.cs` + `Document.cs`
  (+ `SizeBytes`) + `InventoryItem.cs` (+ `Name`) (confirm property names);
  `src/Kumunita.Core/Query/SortSpec.cs`.
- **Deliverables (≤ 4 files):** the 3 service files (the 3 seams); the 3
  interfaces (the matching params); `tests/Kumunita.Core.Tests/` — the pinned
  `Announcement*_*`/`Document*_*`/`Inventory*_*` tests (one file each or one
  shared `MiscListSortTests.cs`, per U2 §2.5).
- **Rules:** **C-SORT·2** — `null` keeps each surface's current order;
  documents' `size` sorts on `SizeBytes` (numeric); `HasMore` untouched; no
  new audit row (C-SORT·4). **Do not** touch document access control (ADR
  0122) or the inventory owner-kind/component filter (frozen).
- **Exit:** `dotnet build` green; the Core suite passes (the 3-group tests +
  the existing suite). Handoff note (4–5 lines, `## U7 — Core misc lists`):
  (a) the 3 seams + their extra keys, (b) the test names that passed, (c) the
  documents-`size` + inventory-`name` pin, (d) the frozen-filter non-change.

### U8 — Core: tags (posts, pages) + people (by-tag, by-bio) + tests
- **Goal:** add the additive `SortSpec? sort = null` to
  `TagService.ListPostsByTagPagedAsync` / `ListPagesByTagPagedAsync` and
  `ProfileFindService.FindPeopleByTagAsync` / `FindPeopleByBioAsync` (+ the
  `ITagService` / `IProfileFindService` seams U2 §2.3 pin), applying the
  allowlists (§2.2: tag posts `created`/`modified`/`title`; tag pages `created`
  **asc**/`modified`/`title`; people `created`/`name`) + the tie-breaker, and
  the **pinned Core tests** (U2 §2.5, the `Tag*_*` / `People*_*` set).
  **Build green + the tests pass.**
- **Entry reads:** U2's §2.2 (these 4 allowlists — note tag-pages' `created`
  **asc** default + people's `name` = display name, OrdinalIgnoreCase) + §2.3
  + §2.5 (the test names); `src/Kumunita.Core/Tags/TagService.cs` (the 2
  seams) + `src/Kumunita.Core/UserInfo/ProfileFindService.cs` (the 2 seams —
  the display-name ordering); `src/Kumunita.Core/UserInfo/Profile.cs` (confirm
  the display-name property) + `TagService`'s page/post models;
  `src/Kumunita.Core/Query/SortSpec.cs`.
- **Deliverables (≤ 4 files):** the 2 service files (the 4 seams); the 2
  interfaces (the matching params); `tests/Kumunita.Core.Tests/` — the pinned
  `Tag*_*`/`People*_*` tests (incl. the tag-pages `created`-asc-default pin +
  people `name` Ordinal pin + invalid-key fallback + tie-break).
- **Rules:** **C-SORT·2** — `null` keeps each surface's current order (tag
  pages' `created`-asc is the **current** order — preserve it exactly);
  people's `name` uses `StringComparer.OrdinalIgnoreCase`; `HasMore` untouched;
  no new audit row (C-SORT·4). **Do not** change the tag-slug resolve or the
  people `CanSeeAsync` gate (frozen).
- **Exit:** `dotnet build` green; the Core suite passes (the `Tag*_*`/
  `People*_*` tests + the existing suite). Handoff note (4–5 lines, `## U8 —
  Core tags + people`): (a) the 4 seams + their extra keys, (b) the test
  names that passed, (c) the tag-pages-asc-default + people-name-Ordinal pin,
  (d) the frozen tag/people gate non-change.

### U9 — Core: search (per-surface order) + tests
- **Goal:** add the additive `SortSpec? sort = null` to
  `SearchService.SearchSurfaceAsync` (+ the `ISearchService` seam U2 §2.3 pin),
  applying the search allowlist (§2.2: `created` default desc + the
  surface's `title`/`name` key) to the **per-surface** orderings (the 10
  `OrderByDescending(...Created)` sub-queries), + the tie-breaker, and the
  **pinned Core tests** (U2 §2.5, the `Search*_*` set). **Build green + the
  search tests pass.**
- **Entry reads:** U2's §2.2 (the search allowlist) + §2.3 + §2.5 (the
  `Search*_*` test names); `src/Kumunita.Core/Search/SearchService.cs` (the
  `SearchSurfaceAsync` + the per-surface `OrderByDescending(...Created)`
  lines, `:234`/`:262`/`:291`/`:320`/`:376`/`:415`/`:436`/`:461`/`:487`/`:512`/
  `:538`) + `src/Kumunita.Core/Search/SearchModels.cs` (the
  `SearchSurfacePage`/`SearchHit` shape); `src/Kumunita.Core/Query/SortSpec.cs`.
- **Deliverables (≤ 3 files):** `src/Kumunita.Core/Search/SearchService.cs`
  (the seam + the per-surface orderings); the `ISearchService` seam (the
  matching param); `tests/Kumunita.Core.Tests/Search/SearchSortTests.cs` (the
  pinned `Search*_*` tests — incl. the default-desc pin + `title`/`name` +
  invalid-key fallback + tie-break + **relevance-untouched** note).
- **Rules:** **C-SORT·2** — `null` keeps the per-surface
  `OrderByDescending(Created)` exactly; **relevance is NOT a sort key** (M8
  frozen — a named non-decision); `HasMore`/`MaxPerSurface` untouched; no new
  audit row (C-SORT·4).
- **Exit:** `dotnet build` green; the Core suite passes (the `Search*_*` tests
  + the existing suite). Handoff note (4–5 lines, `## U9 — Core search`):
  (a) the seam + the per-surface orderings touched, (b) the `Search*_*` test
  names that passed, (c) the relevance-untouched pin, (d) the per-surface
  default-desc preservation.

### U10 — Web foundation: `_Sort` partial + `SortViewModel` + pager-carry + community-feed reference + tests
- **Goal:** add the **shared Web sort control** — `SortViewModel` + the
  `_Sort` partial (mirroring the `_Pager` partial) + the **pager-carry rule**
  (the `sort`+`dir` pairs join the `PagedViewModel.FilterParams` so prev/next
  preserve the sort, C-SORT·8) — and wire it into **one reference surface**
  (the community post feed, `PostsController.Index`) + its view model's
  `Sort` field, with the **pinned Web tests** (U2 §2.6, the `CommunityFeed_*`
  set). **Build green + the reference Web tests pass.** This unit **establishes
  the pattern U11–U15 copy.**
- **Entry reads:** U1's Part 1 (C-SORT·1/8) + U2's §2.4 (the `SortViewModel`
  contract + the `_Sort` partial + the pager-carry rule) + §2.6 (the
  `CommunityFeed_*` test names); `src/Kumunita.Web/Models/PagedViewModel.cs`
  (the `FilterParams` mechanism to extend) + `src/Kumunita.Web/Views/Shared/
  _Pager.cshtml` (the partial style + the `PagerLink` query-pair shape to
  mirror) + `Views/Shared/_Layout.cshtml` (the partial registration, if any);
  `src/Kumunita.Web/Controllers/PostsController.cs` (`Index` — the reference
  list action that already builds a `Pager`) + `src/Kumunita.Web/Models/
  FeedViewModel.cs` (the view model to add a `Sort` field to);
  `tests/Kumunita.Web.Tests/` (the controller test harness — the signed-in
  principal + the NSubstitute store shape).
- **Deliverables (≤ 5 files):**
  - `src/Kumunita.Web/Models/SortViewModel.cs` — per §2.4 (`BaseUrl`,
    `CurrentKey`, `CurrentDir`, `Options`, `CarriedParams`); a `ForRoute(...)`
    builder mirroring `PagedViewModel.ForRoute` that carries `page` + the
    existing filters + the toggled `sort`/`dir`.
  - `src/Kumunita.Web/Views/Shared/_Sort.cshtml` — the `<select name="sort">`
    (or link set) over the surface's allowed keys × asc/desc, preserving the
    current query pairs (the `_Pager` `PagerLink` shape); a `null` model
    renders nothing (the no-sort pin). Labels via `<kw-l key="sort.…">`.
  - `src/Kumunita.Web/Controllers/PostsController.cs` — `Index` builds a
    `SortViewModel` (the post allowlist options) + threads `?sort=`/`?dir=`
    into the `PostService.ListFeedAsync` `SortSpec` + adds the `sort`/`dir`
    pairs to the existing `Pager`'s `FilterParams` (C-SORT·8).
  - `src/Kumunita.Web/Models/FeedViewModel.cs` — the `SortViewModel? Sort`
    field.
  - `src/Kumunita.Web/Views/Posts/Index.cshtml` — render `@Html.Partial("_Sort",
    Model.Sort)` (the reference placement — beside/above the `_Pager`).
  - `tests/Kumunita.Web.Tests/SortControlTests.cs` — the pinned `CommunityFeed_*`
    tests (`_SortControl_Renders_AllowedKeys`, `_Pager_Carries_Sort_And_Dir`,
    `_SortParam_DefaultsWhenAbsent`).
- **Rules:** **C-SORT·3** — the `?sort=`/`?dir=` → `SortSpec` mapping is Web-
  only; the `SortViewModel` is the **one** shared control (U11–U15 reuse it —
  **do not** fork a per-surface partial); a `null` `Sort` renders nothing.
  The existing `Pager`/`HasMore` behavior is **untouched** (only the
  `FilterParams` gain the two pairs). **Do not** touch authorization.
- **Exit:** `dotnet build` green; `dotnet exec
  tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` passes
  (the 3 `CommunityFeed_*` tests + the existing suite). Handoff note (5–6
  lines, `## U10 — Web _Sort foundation`): (a) the `SortViewModel` + `_Sort`
  location, (b) the pager-carry rule (the `sort`/`dir` pairs in `FilterParams`),
  (c) the reference surface (community feed) + its `Sort` field, (d) the 3 test
  names that passed, (e) the "U11–U15 reuse this, do not fork" note.

### U11 — Web: post feeds (all-sections, group) + events (upcoming, past, group) + tests
- **Goal:** apply the U10 `_Sort` pattern to the remaining **post-feed**
  surfaces (all-sections feed, group posts) and the **events** surfaces
  (upcoming list, past list, group events) — the view-model `Sort` field +
  the `@Html.Partial("_Sort", ...)` + the pager-carry + the `SortSpec`
  threading — with the **pinned Web tests** (U2 §2.6, the `AllFeed_*` /
  `GroupPostFeed_*` / `Event*_*` set). **Build green + the Web tests pass.**
- **Entry reads:** U10's handoff note (the established pattern + the
  `SortViewModel.ForRoute` builder) + U2's §2.2 (post + events allowlists) +
  §2.4 + §2.6 (the test names); the U10 `SortViewModel` + `_Sort` (reuse,
  **do not fork**); `src/Kumunita.Web/Controllers/PostsController.cs` (the
  all-sections + group feed actions) + `src/Kumunita.Web/Controllers/
  GroupsController.cs` (the group-posts list) + `src/Kumunita.Web/Controllers/
  EventController.cs` (the upcoming/past list actions) + their view models
  (`FeedViewModel`, `GroupViewModel`, `EventViewModel`/the event list VM);
  the corresponding views.
- **Deliverables (≤ 6 files):** the controllers (the 5 actions thread
  `?sort=`/`?dir=` + build the `SortViewModel` + add the pager-carry pairs);
  the view models (the `Sort` field); the views (the `_Sort` partial render);
  `tests/Kumunita.Web.Tests/` — the pinned `AllFeed_*`/`GroupPostFeed_*`/
  `Event*_*` tests (reuse the U10 harness — one new file
  `PostEventSortWebTests.cs` or extend `SortControlTests.cs`).
- **Rules:** reuse U10's `SortViewModel`/`_Sort` verbatim (C-SORT·1 — each
  surface passes **its own** allowed-key `Options`); **C-SORT·8** — each
  surface's pager carries its own `sort`/`dir`; the `null`/no-sort pin is
  unchanged; authorization untouched (C-SORT·4).
- **Exit:** `dotnet build` green; the Web suite passes (the `AllFeed_*`/
  `GroupPostFeed_*`/`Event*_*` tests + the existing suite). Handoff note
  (4–5 lines, `## U11 — Web posts + events`): (a) the 5 surfaces wired, (b)
  the test names that passed, (c) the per-surface `Options` (allowed keys)
  note, (d) the pager-carry confirmation.

### U12 — Web: projects (todos, boards, projects, goals) + tests
- **Goal:** apply the U10 `_Sort` pattern to the **projects** surfaces (the
  todos list, the boards list, the projects list, the goals list) — the
  view-model `Sort` field + the `_Sort` render + the pager-carry + the
  `SortSpec` threading — with the **pinned Web tests** (U2 §2.6, the
  `Todo*_*`/`Board*_*`/`Project*_*`/`Goal*_*` set). **Build green + the Web
  tests pass.**
- **Entry reads:** U10's handoff note (the pattern) + U2's §2.2 (projects +
  todos allowlists — the todos' `due`/`priority`/`status` keys) + §2.4 + §2.6
  (the test names); the U10 `SortViewModel` + `_Sort` (reuse);
  `src/Kumunita.Web/Controllers/ProjectsController.cs` (the `TodosIndex`,
  `BoardsIndex`, `ProjectsIndex`, and the goals-list actions) + their view
  models (`ProjectTodoViewModels`, `ProjectBoardViewModels`,
  `ProjectViewModels`) + the views (`Projects/TodosIndex.cshtml`,
  `Projects/BoardIndex.cshtml`, `Projects/ProjectsIndex.cshtml`, and the goals
  view).
- **Deliverables (≤ 6 files):** `ProjectsController.cs` (the 4 list actions
  thread `?sort=`/`?dir=` + build the `SortViewModel` + add the pager-carry
  pairs); the view models (the `Sort` field); the views (the `_Sort` render);
  `tests/Kumunita.Web.Tests/` — the pinned `Todo*_*`/`Board*_*`/`Project*_*`/
  `Goal*_*` tests (one new file `ProjectSortWebTests.cs`).
- **Rules:** reuse U10's `SortViewModel`/`_Sort` verbatim; the todos' extra
  keys (`due`/`priority`/`status`) are offered in **this surface's** `Options`
  only (C-SORT·1/F9); the existing filters (assignee/project/blocked) are
  **frozen** and still carried on the pager; **do not** touch the kanban
  **column** order (named non-decision); authorization untouched (C-SORT·4).
- **Exit:** `dotnet build` green; the Web suite passes (the 4-group tests +
  the existing suite). Handoff note (4–5 lines, `## U12 — Web projects`):
  (a) the 4 surfaces wired, (b) the test names that passed, (c) the todos'
  extra-key `Options` pin, (d) the frozen-filter + kanban-column non-change.

### U13 — Web: announcements + documents + inventory + tests
- **Goal:** apply the U10 `_Sort` pattern to the **announcements**,
  **documents**, and **inventory** list surfaces — the view-model `Sort`
  field + the `_Sort` render + the pager-carry + the `SortSpec` threading —
  with the **pinned Web tests** (U2 §2.6, the `Announcement*_*`/
  `Document*_*`/`Inventory*_*` set). **Build green + the Web tests pass.**
- **Entry reads:** U10's handoff note (the pattern) + U2's §2.2 (these 3
  allowlists — documents' `size`, inventory's `name`) + §2.4 + §2.6 (the test
  names); the U10 `SortViewModel` + `_Sort` (reuse);
  `src/Kumunita.Web/Controllers/AnnouncementController.cs` (`Index`) +
  `DocumentController.cs` (`Index`) + `InventoryController.cs` (`List`) +
  their view models (`AnnouncementViewModels`, the document list VM,
  `InventoryViewModels`) + the views (`Announcement/Index.cshtml`,
  `Document/Index.cshtml`, `Inventory/List.cshtml`).
- **Deliverables (≤ 6 files):** the 3 controllers (the 3 list actions thread
  `?sort=`/`?dir=` + build the `SortViewModel` + add the pager-carry pairs);
  the view models (the `Sort` field); the views (the `_Sort` render);
  `tests/Kumunita.Web.Tests/` — the pinned `Announcement*_*`/`Document*_*`/
  `Inventory*_*` tests (one new file `MiscListSortWebTests.cs`).
- **Rules:** reuse U10's `SortViewModel`/`_Sort` verbatim; documents' `size`
  (=`SizeBytes`) + inventory's `name` are offered in **their** `Options` only
  (C-SORT·1/F9); the existing filters are **frozen** and still carried on the
  pager; authorization untouched (C-SORT·4) — **do not** touch document access
  control (ADR 0122) or the inventory owner-kind filter.
- **Exit:** `dotnet build` green; the Web suite passes (the 3-group tests +
  the existing suite). Handoff note (4–5 lines, `## U13 — Web misc lists`):
  (a) the 3 surfaces wired, (b) the test names that passed, (c) the
  documents-`size`/inventory-`name` `Options` pin, (d) the frozen-filter
  non-change.

### U14 — Web: tags (posts, pages) + people (by-tag, by-bio) + tests
- **Goal:** apply the U10 `_Sort` pattern to the **tag** surfaces (by-tag
  posts, by-tag pages) and the **people-find** surfaces (find-people by tag,
  by bio) — the view-model `Sort` field + the `_Sort` render + the pager-
  carry + the `SortSpec` threading — with the **pinned Web tests** (U2 §2.6,
  the `Tag*_*`/`People*_*` set). **Build green + the Web tests pass.**
- **Entry reads:** U10's handoff note (the pattern) + U2's §2.2 (these 4
  allowlists — tag-pages' `created`-**asc** default + people's `name`) + §2.4
  + §2.6 (the test names); the U10 `SortViewModel` + `_Sort` (reuse);
  `src/Kumunita.Web/Controllers/TagController.cs` (the by-tag posts + pages
  actions) + the people-find surface controller (the `FindPeopleByTag`/
  `FindPeopleByBio` actions — the M23 surface) + their view models
  (`TagViewModels` + the people VM) + the views (`Tag/ByTag.cshtml` + the
  people-find view).
- **Deliverables (≤ 6 files):** `TagController.cs` + the people-find controller
  (the 4 list actions thread `?sort=`/`?dir=` + build the `SortViewModel` +
  add the pager-carry pairs); the view models (the `Sort` field); the views
  (the `_Sort` render); `tests/Kumunita.Web.Tests/` — the pinned `Tag*_*`/
  `People*_*` tests (one new file `TagPeopleSortWebTests.cs`).
- **Rules:** reuse U10's `SortViewModel`/`_Sort` verbatim; the tag-pages'
  `created`-asc default + people's `name` (display name, OrdinalIgnoreCase)
  are offered/ordered per their surface (C-SORT·1/F9 + C-SORT·2); the
  tag-slug + people `CanSeeAsync` gates are **frozen** (C-SORT·4);
  authorization untouched.
- **Exit:** `dotnet build` green; the Web suite passes (the `Tag*_*`/
  `People*_*` tests + the existing suite). Handoff note (4–5 lines, `## U14 —
  Web tags + people`): (a) the 4 surfaces wired, (b) the test names that
  passed, (c) the tag-pages-asc + people-name pin, (d) the frozen tag/people
  gate non-change.

### U15 — Web: search + tests
- **Goal:** apply the U10 `_Sort` pattern to the **search** surface
  (`/search` — the single paged surface over the ten content surfaces) — the
  view-model `Sort` field + the `_Sort` render + the pager-carry + the
  `SortSpec` threading into `SearchService.SearchSurfaceAsync` — with the
  **pinned Web tests** (U2 §2.6, the `Search*_*` set). **Build green + the
  search Web tests pass.**
- **Entry reads:** U10's handoff note (the pattern) + U2's §2.2 (the search
  allowlist — `created` default desc + `title`/`name`) + §2.4 + §2.6 (the test
  names); the U10 `SortViewModel` + `_Sort` (reuse);
  `src/Kumunita.Web/Controllers/SearchController.cs` (`Index`) + the search
  view model + the view (`Views/Search/Index.cshtml` or the search view);
  `src/Kumunita.Core/Search/ISearchService.cs` (the `SearchSurfaceAsync` seam
  the `SortSpec` threads into, from U9).
- **Deliverables (≤ 4 files):** `SearchController.cs` (the `Index` action
  threads `?sort=`/`?dir=` + builds the `SortViewModel` + adds the pager-carry
  pairs); the search view model (the `Sort` field); the search view (the
  `_Sort` render); `tests/Kumunita.Web.Tests/` — the pinned `Search*_*` Web
  tests (one new file `SearchSortWebTests.cs`).
- **Rules:** reuse U10's `SortViewModel`/`_Sort` verbatim; **relevance is NOT
  a sort option** (M8 frozen — the `Options` are the date/title keys only, a
  named non-decision); the existing group scope + the frozen filter are
  preserved + carried on the pager; authorization untouched (C-SORT·4).
- **Exit:** `dotnet build` green; the Web suite passes (the `Search*_*` tests
  + the existing suite). Handoff note (4–5 lines, `## U15 — Web search`):
  (a) the search surface wired, (b) the test names that passed, (c) the
  relevance-excluded `Options` pin, (d) the frozen scope/filter non-change.

### U16 — i18n: the `sort.*` kw-l set × en/de/fr/da + a resolution pin
- **Goal:** close the **`sort.*` kw-l set** (the sort control's option labels
  — the per-key names, the asc/desc labels, the control's accessible label)
  across **en/de/fr/da** with an English floor (the M25 `documents.*` /
  `a11y.*` precedent), wire it into the `_Sort` partial (F12), and pin a
  **resolution test** that the labels resolve in all four languages. **Build
  green + the kw-l resolution test passes.**
- **Entry reads:** U2's §2.4 (the `_Sort` contract's `<kw-l key="sort.…">`
  usage); the M25 kw-l precedent — `docs/plans-milestones/done/m25/` + the
  `documents.*` / `a11y.*` sets (how a closed set is seeded/registered + how
  the English floor is pinned); `src/Kumunita.Web/Views/Shared/_Sort.cshtml`
  (U10's partial — the `<kw-l>` keys to fill); the kw-l seed/registration
  site (the M25 `documents.*` registration file — locate it via the handoff
  note / `Kumunita.Core/Localization/`); `src/Kumunita.Core/Localization/
  ITranslationProvider.cs` (the resolution seam the test drives).
- **Deliverables (≤ 4 files):** the kw-l seed/registration file (the `sort.*`
  keys × en/de/fr/da — the key names matching the `<kw-l>` usage in the `_Sort`
  partial); the `_Sort` partial (if the key names need to be aligned to the
  closed set — a **label-only** edit, no logic change); a pinned test
  (`tests/Kumunita.Web.Tests/` or `Core.Tests/` — `SortKwL_Resolves_En_De_Fr_Da`,
  the F12 pin); a handoff-note section.
- **Rules:** **label-only** — no logic change to the `_Sort` control (C-SORT·
  3 is unaffected); the English floor is **always present** (a missing
  de/fr/da key degrades to en, the ML-UI floor rule); **do not** invent keys
  beyond the closed set (the `a11y.*`/`documents.*` closed-set precedent).
- **Exit:** `dotnet build` green; the Web (or Core) suite passes (the
  `SortKwL_Resolves_En_De_Fr_Da` test + the existing suite). Handoff note
  (4–5 lines, `## U16 — i18n sort.*`): (a) the `sort.*` key set (count + the
  key names), (b) the en/de/fr/da coverage confirmation, (c) the English-floor
  degradation note, (d) the test name that passed.

### U17 — ADR 0145 + OPS/README/Milestones sync
- **Goal:** author **ADR 0145 — Sorting: the one canonical sort contract**
  (the `SortSpec` + per-surface closed allowlists + the additive default-
  preserving seam + the shared `_Sort` control + the pager-carry + the
  closed kw-l set) and sync **OPS.md** (a note that sort is a per-request
  query param, no config), **README.md** (the M26 roadmap line → "Done",
  keeping M27 the next-in-progress), and confirm `Milestones.cs` is
  **unchanged** here (the status flip is U18). **Build green + both suites
  pass (the M27 gate).**
- **Entry reads:** U1's Part 1 (the invariants/FACES) + U2's Part 2 (the seams
  + allowlists + the Web contract) — the ADR's substance;
  `docs/adr/0090-pagination-and-filtering.md` (the sibling ADR's structure +
  the D-decision style to emulate) + `docs/adr/README.md` (the ADR index to
  append); `docs/OPS.md` (where a new per-request param is noted, if at all);
  `README.md` (the M26 roadmap line — the "In progress" → "Done" + the
  M27 "Planned" → "In progress" wording) + `src/Kumunita.Web/Milestones.cs`
  (read-only confirm — the flip is U18, **do not** change it here);
  the M25 ADR close (`docs/adr/0135-upload-limits.md` if present) for the
  close-style.
- **Deliverables (≤ 3 files):**
  - `docs/adr/0145-sorting.md` — Status: Accepted; the Context (M7 shipped
    paging + filters; ordering was hardcoded), the decision (the `SortSpec` +
    closed allowlists + additive seam + shared `_Sort` + pager-carry +
    closed kw-l), the 8 invariants (C-SORT·1–8), the 12 FACES (F1–F12), the
    18-surface catalog, and the named non-decisions (people catalog, calendar
    day/week/month, kanban column order, bookmarks, relevance scoring,
    schema/authorization/context change).
  - `docs/adr/README.md` — append the ADR 0145 row.
  - `docs/OPS.md` + `README.md` — the sync (sort is a per-request `?sort=`/
    `?dir=` param, no operator config; the M26 roadmap line → "Done").
- **Rules:** the ADR is the **contract of record** (the design doc is the
  reference tier; the ADR is the decision tier — the M7→ADR 0090 precedent).
  **Do not** flip `Milestones.cs` here (U18 owns it). **Do not** reorder
  `Milestones.All`.
- **Exit:** the ADR + index + OPS/README edits exist; `dotnet build` green;
  **both suites pass** (the M27 acceptance gate — the U2 §2.7 three-test
  shape). Handoff note (5–6 lines, `## U17 — ADR 0145 + docs sync`): (a) the
  ADR 0145 path + its decision summary, (b) the ADR index row, (c) the
  OPS/README sync (the M26 line → "Done"), (d) the `Milestones.cs`
  read-only-confirm (unchanged; U18 flips it), (e) the both-suites-green
  note.

### U18 — Close: milestone flip + design-doc close + move to `done/m26/`
- **Goal:** the **M26 close unit** (the M25/U12 and M3/U12 precedent): (1)
  flip `Milestones.cs` — M26 → `StatusDone` and **promote M27 →
  `StatusNext`** (the C-SORT·6 single-in-progress contract); (2) update
  `MilestonesTests.cs` to match (the done-list gains M26; the single-in-
  progress test is re-pointed to M27); (3) append the **"M26 — Closed
  (recorded)"** section to the design doc (last); (4) append the final
  handoff section; (5) **move** the plan + handoff note + the 18 unit plans
  to `docs/plans-milestones/done/m26/`; and (6) clean up any `.tmp/` scratch.
  **Exit: build green + `MilestonesTests` pass + both suites pass + the plan
  files are in `done/m26/`.**
- **Entry reads:** `src/Kumunita.Web/Milestones.cs` (the M26 `StatusNext` →
  `StatusDone` + the M27 `StatusPlanned` → `StatusNext` flip);
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the
  `Shipped_Milestones_Are_Marked_Done` done-list — add M26 — + the
  `M26_Is_The_Single_InProgress_Milestone` test — re-point to M27);
  `docs/design/m26-sorting-design.md` (append the "## M26 — Closed
  (recorded)" section **last**); `docs/plans-milestones/in-progress/
  m26-handoff-notes.md` (append the final `## U18 — close` section);
  `docs/plans-milestones/done/m25/` (the close-unit precedent — the
  `plan-*.md` + `*-handoff-notes.md` + unit plans all in `done/m25/`); the
  handoff note (U17's ADR-0145 + docs sync + the honest "Closed" close);
  `docs/plans-milestones/plan-m26-sorting.md` §*Running the tests* (the
  xunit.v3 runner quirk).
- **Deliverables (≤ 4 files + the moves):**
  1. `src/Kumunita.Web/Milestones.cs` — flip M26 → `StatusDone` + M27 →
     `StatusNext` (the C-SORT·6 close; the M26 title is unchanged). The
     **order** of `Milestones.All` is **unchanged** (`…, M25, M26, M27, M28`).
  2. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — add M26 to the
     `Shipped_Milestones_Are_Marked_Done` done-list; re-point the
     `M26_Is_The_Single_InProgress_Milestone` test so it asserts the single
     `StatusNext` is now **M27** (and M26 `StatusDone`), keeping the same
     two-test shape + the order assertion (unchanged). **Rename** the test
     method to reflect M27 (or keep the name + update the assertion — the
     order test
     `Roadmap_Covers_M0_Through_M28_Plus_Named_Lanes_In_Order` is untouched).
  3. `docs/design/m26-sorting-design.md` — append the
     `## M26 — Closed (recorded)` section **last** (mirroring the M25 close):
     the M7-reuse note (the `_Sort` rides the M7 `FilterParams` mechanism),
     the 5 named non-decisions, the total M26 test count (Core + Web), the
     ADR 0145 pointer, and the M27-handoff note.
  4. `docs/plans-milestones/in-progress/m26-handoff-notes.md` — append the
     final `## U18 — close` section (the M26 close: the milestone flip, the
     test count, the docs sync, the handoff to M27).
  5. **The moves (the "move to `done/`" step):**
     - `docs/plans-milestones/plan-m26-sorting.md` →
       `docs/plans-milestones/done/m26/plan-m26-sorting.md`
     - `docs/plans-milestones/in-progress/m26-handoff-notes.md` →
       `docs/plans-milestones/done/m26/m26-handoff-notes.md`
     - the **18 unit plans** `docs/plans-milestones/in-progress/m26-u01.md`
       … `m26-u18.md` → `docs/plans-milestones/done/m26/` (the completed unit
       register).
     The lane folder `done/m26/` matches the repo's `done/<lane>/` convention
     (`done/m3/`, `done/m25/`, …).
  6. **Scratch cleanup:** `Remove-Item -Force .tmp\*` (or whatever M26
     scratch exists) — mirror the M25 close.
- **Rules:** the **order** of `Milestones.All` is **unchanged**; only the two
  status values flip (C-SORT·6). Do not reorder. `MilestonesTests.cs` is the
  **pin** — it must pass after the flip. The moves are **the** "done" step —
  `in-progress/` must no longer hold the M26 files; `done/m26/` must hold the
  register + handoff note + all 18 unit plans. Do **not** re-shape the design
  doc outside the appended "Closed" section.
- **Exit (the reliable test path — both suites + the milestone pin):**
  ```powershell
  dotnet build Kumunita.slnx -c Debug
  dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
  dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
  ```
  Build green; the Web suite passes **including the re-pointed
  `MilestonesTests`**; the Core suite passes; the plan + handoff note + unit
  plans are in `done/m26/`; `in-progress/` no longer holds the M26 files.
  Handoff note (6–8 lines, `## U18 — close`): (a) the `Milestones.cs` flip
  (M26 → done, M27 → next) + the `MilestonesTests.cs` re-point, (b) the
  "Closed" section appended, (c) the files moved to `done/m26/`, (d) the
  **handoff to M27** (user-scoped portability — the next milestone; its start
  precondition is that M26 is `StatusDone` and M27 is `StatusNext`, per
  C-SORT·6).
- **Handoff to M27 (user-scoped portability).** M27 now begins with the same
  single-in-progress contract: M26 is `StatusDone`, M27 is `StatusNext`, and
  exactly one milestone is in progress. M27's own register + unit plans
  follow the same sealed-unit shape as this one (the register at
  `docs/plans-milestones/plan-m27-*.md`, the unit plans in
  `docs/plans-milestones/in-progress/`, the close moving them to
  `docs/plans-milestones/done/m27/`).
