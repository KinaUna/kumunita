# ADR 0145 — Sorting: the one canonical sort contract (M26)

Status: Accepted
Date: 2026-10-06
Builds on: 0090 (M7 pagination & filtering — the `?page=` param + the
`HasMore` signal + the `_Pager` partial + `PagedViewModel` + the `FilterParams`
pager-carry mechanism M26 rides); 0091 (M8 search — its frozen relevance
ordering is a named non-decision of this ADR); 0015 (the `kw-l` registry —
the closed `sort.*` set is additive on it)

## Context

M7 (ADR 0090) shipped **pagination** and **filtering** over the paged Core
seams. **M3** shipped the actual **ordering** — and it has been **hardcoded
in Core** ever since: every paged list seam does a fixed
`OrderBy`/`OrderByDescending` over the *already-authorized* candidate set
(posts `OrderByDescending(p => p.Created)`, events upcoming `OrderBy(Start)`,
projects `OrderByDescending(Created)`, …).

A resident reading the community feed, the tag browsing list, or the search
results has **no way to choose the order** — the only order is the
hardcoded one, on every one of the 18 paged list/search surfaces. **M26
makes that order user-selectable** — the M7 sibling that pagination and
filtering shipped without.

This ADR records the decision; `docs/design/m26-sorting-design.md` (parts
1–2) freezes the invariants **C-SORT·1–8**, the FACES **F1–F12**, the 18
locked allowlists (corrections C-1…C-3), the **exact C#** of every seam this
ADR points at, and the pinned test names — the design doc is the authority on
shape; this ADR is the authority on *why these shapes, and why nothing else*.

## Decision

- **The contract is a pure Core value object, not an HTTP type (D-SORT·1,
  C-SORT·3).** `record SortSpec(string Key, bool Descending)` + the pure
  `SortKeys.Parse(key, dir, allowedKeys, defaultKey, defaultDir)` in a new
  `Kumunita.Core.Query` namespace (ADR 0006-D, the M25 `C-UP·3`
  precedent) — **no HTTP, no DI registration** (a value object + a static
  parser; `AddKumunitaCore` gains **zero** new registrations, C-SORT·7). The
  `?sort=`/`?dir=` query-param parsing and the view mapping are **Web-only**
  (the `SortViewModel` + the `_Sort` partial). The Core change per seam is an
  **optional parameter + a `switch` over the closed key set** — no seam
  signature is broken.

- **A per-surface closed allowlist (D-SORT·2, C-SORT·1).** Each surface
  declares a **closed** set of sort keys (a `switch` over the key set in
  Core). A raw `?sort=` string is **mapped** against the allowlist → a known
  key or the **default**; it is **never** a property/reflection lookup from
  the raw string (no sort-injection — R1). A key **not** in the allowlist →
  the surface's default key (F4); an invalid/absent `?dir=` → the key's
  default direction (F5). **No error, no 500** (F4/F5).

- **Default-preserving (D-SORT·3, C-SORT·2).** An **absent/`null`**
  `SortSpec` reproduces the surface's **current hardcoded order exactly** —
  no unsorted read's output changes (the U3–U9 pinned tests enforce
  byte-for-byte). When non-null, the seam orders by the spec's key/direction
  **plus a stable tie-breaker** (C-SORT·5). The `Skip/Take` / `HasMore`
  contract is **untouched**.

- **Sort is a display facet, never a gate (D-SORT·4, C-SORT·4).**
  Re-ordering **never** changes *which* rows are authorized — the
  `CanSeeAsync` call, the `AccessAudit` row, and the `HiddenCount` shape are
  all **untouched** (the ADR 0064 `?view=` display-selector precedent + the
  M7 C-M7·2 "display-only" pin). A signed-in and an anonymous viewer on a
  visible surface get the **same** sort behavior (F10); a sorted read emits
  **zero** new `AccessAudit` rows vs. an unsorted read (F11).

- **One shared Web `_Sort` control, riding the M7 `_Pager` (D-SORT·5,
  C-SORT·8).** A `SortViewModel` + a `_Sort` partial (a **link set** of the
  surface's allowed keys — one link per key, no asc/desc toggle links, no
  form/JS — the `_Pager` precedent) render the surface's allowed keys
  (F9 — no dead options). The current `?sort=`/`?dir=` values are **carried
  on the pager links** via the existing `PagedViewModel.FilterParams`
  mechanism (the M7 D7 mechanism — **no new pager idiom**), added only when
  the request carried a non-blank `?sort=` (an unsorted read's pager links
  stay byte-identical, C-SORT·2). The list-surface view model gains a
  `SortViewModel? Sort` field: `null` renders nothing.

- **A closed `sort.*` kw-l set × en/de/fr/da (D-SORT·6, F12).** The control's
  option labels close a **closed 8-key set** — `sort.created`,
  `sort.modified`, `sort.title`, `sort.size`, `sort.name`, `sort.start`,
  `sort.due`, `sort.status` — exactly the union of the U10–U15 surfaces'
  allowlists the `_Sort` partial emits, registered on the
  `KnownTranslationKeys` registry with an English floor (the `pagination.*` /
  `documents.*` / `a11y.*` precedent), pinned closed by
  `SortKwL_Resolves_En_De_Fr_Da` + `ClosedSet_Has_Exactly_8_Keys`.

- **Stable ordering (D-SORT·8, C-SORT·5).** Every ordered query (a
  non-`null` spec) ends with a **unique** tie-breaker
  (`.ThenBy(x => x.Id)`) so a `HasMore` window never shuffles rows (F7).

- **No new document / context / schema (D-SORT·7, C-SORT·7).** `SortSpec` is
  a **value object** — no Marten doc, no `DocTypes` surface, no
  `SchemaBootstrap` change (ADR 0004 §B / ADR 0006-D discipline); **zero**
  new `AccessAction`, **zero** new bounded context, **zero** new index.

### The 8 invariants (C-SORT·1–8, frozen)

- **C-SORT·1 — closed allowlist per surface.** Each surface declares a
  **closed** set of sort keys; a raw `?sort=` outside the allowlist (or a
  malformed `?dir=`) → the surface's **default** key/direction; **never** a
  property/reflection lookup from the raw string (no sort-injection).
- **C-SORT·2 — default-preserving.** An absent/`null` `SortSpec` reproduces
  the surface's **current hardcoded order exactly** — no unsorted read's
  output changes.
- **C-SORT·3 — `Core` stays HTTP-free (ADR 0006-D).** `SortSpec` +
  `SortKeys.Parse` are **pure** Core; the `?sort=`/`?dir=` parsing and the
  view mapping are **Web-only**.
- **C-SORT·4 — sort is a display facet, never a gate.** Re-ordering never
  changes *which* rows are authorized; **zero** new `AccessAudit` row,
  **zero** new `AccessAction`, the `CanSeeAsync` call is **untouched**.
- **C-SORT·5 — stable ordering.** Every ordered query ends with a **unique
  tie-breaker** (`.ThenBy(Id)`) so a `HasMore` window never shuffles rows.
- **C-SORT·6 — single-in-progress milestone contract.** M26 started **only**
  after M25 is `StatusDone` (confirmed at U1); M26's close (U18) promotes
  M27 — `Milestones.cs` is flipped by the close unit, not by this ADR.
- **C-SORT·7 — no new bounded context / document / schema.** `SortSpec` is a
  **value object** (no Marten doc, no `DocTypes` surface, no
  `SchemaBootstrap` change).
- **C-SORT·8 — sort rides the pager.** The current `?sort=`/`?dir=` values
  are carried on the `_Pager` links (the M7 D7 `FilterParams` mechanism) so
  prev/next **preserve the sort**.

### The 12 FACES (F1–F12, frozen)

- **F1** `?sort=` **absent** → the surface's current order is preserved
  **exactly** (behavior-preserving) — C-SORT·2
- **F2** `?sort=<allowed key>&dir=asc` → sorted by that key **ascending** —
  C-SORT·1
- **F3** `?sort=<allowed key>&dir=desc` → sorted by that key
  **descending** — C-SORT·1
- **F4** `?sort=<key-not-in-allowlist>` → the surface's **default key**
  (not an error / 500) — C-SORT·1
- **F5** `?dir=` **invalid/absent** → the key's **default direction** —
  C-SORT·1
- **F6** sorting re-orders **only the authorized set** (a hidden row stays
  hidden regardless of sort) — C-SORT·4
- **F7** a **stable tie-breaker** pins order across a `HasMore` window (no
  page shuffle) — C-SORT·5
- **F8** the pager's prev/next links **carry the current** `?sort=`/`?dir=`
  — C-SORT·8
- **F9** the sort control offers **exactly** the surface's allowed keys (no
  dead options) — C-SORT·1
- **F10** a **signed-in** and an **anonymous** viewer on a visible surface
  get the **same** sort behavior (sort is **not** an authorization axis) —
  C-SORT·4
- **F11** **no new** `AccessAudit` row is emitted by a **sorted** read vs.
  an **unsorted** read — C-SORT·4
- **F12** the `sort.*` labels **resolve** in **en/de/fr/da** (the closed 8-
  key kw-l set) — the i18n floor

### The 18-surface catalog (locked allowlists, confirmed against the models)

| # | Surface (Core seam) | Locked allowlist (→ model property) | Surface default (key, dir) |
|---|---|---|---|
| 1 | Community post feed (`PostService.ListFeedAsync`) | `created`, `modified`, `title` | `created`, desc |
| 2 | All-sections post feed (`PostService.ListAllFeedAsync`) | `created`, `modified`, `title` | `created`, desc |
| 3 | Group post feed (`PostService.ListGroupFeedAsync`) | `created`, `modified`, `title` | `created`, desc |
| 4 | Events upcoming (`EventService.ListUpcomingAsync`) | `start`, `created`, `title` | `start`, asc |
| 5 | Events past (`EventService.ListPastAsync`) | `start`, `created`, `title` | `start`, desc |
| 6 | Group events (`EventService.ListGroupEventsAsync`) | `start`, `created` | `start`, asc |
| 7 | Todos (`ProjectService.ListTodosAsync`) | `created`, `modified`, `title`, `due`, `status` | `created`, desc |
| 8 | Boards (`ProjectService.ListBoardsAsync`) | `created`, `modified`, `title` | `created`, desc |
| 9 | Projects (`ProjectService.ListProjectsAsync`) | `created`, `modified`, `title` | `created`, desc |
| 10 | Goals (`ProjectService.ListGoalsAsync`) | `created`, `modified`, `title` | `created`, desc |
| 11 | Announcements (`AnnouncementService.ListVisiblePagedAsync`) | `created`, `modified`, `title` | `created`, desc |
| 12 | Documents (`DocumentService.ListAsync`) | `created`, `modified`, `title`, `size` | `created`, desc |
| 13 | Inventory (`InventoryService.ListItemsAsync`) | `created`, `modified`, `name` | `created`, desc |
| 14 | Tag → posts (`TagService.ListPostsByTagPagedAsync`) | `created`, `modified`, `title` | `created`, **asc** (C-1) |
| 15 | Tag → pages (`TagService.ListPagesByTagPagedAsync`) | `created`, `modified`, `title` | `created`, **asc** (C-1) |
| 16 | People → by tag (`ProfileFindService.FindPeopleByTagAsync`) | `name` (→ `DisplayName`) | `name`, asc (C-2) |
| 17 | People → by bio (`ProfileFindService.FindPeopleByBioAsync`) | `name` (→ `DisplayName`) | `name`, asc (C-2) |
| 18 | Search, per surface (`SearchService.SearchSurfaceAsync`) | `created`, `title` | `created`, desc |

The three model-confirmed corrections to the Part 1 proposal (locked in the
design doc §2.8): **C-1** — the tag→posts/tag→pages default direction is
**asc** (the actual code is `OrderBy(Created)`, not `OrderByDescending`);
**C-2** — the people surfaces have **no `created` key** (`Profile` has no
`Created` field — `name`/`DisplayName` only); **C-3** — todos have **no
`priority` key** (`TodoItem` has no `Priority` property — `status` instead).

### Named non-decisions (pinned)

- **The people catalog** (`DirectoryController.Index` →
  `DirectoryService.ListAsync`) — **not paged** today (loads the whole
  visible set, no `?page=`); a non-paged catalog, not a feed/list. M26 does
  **not** page it; if paging lands later, sorting follows then. (The
  people-**find** surfaces, rows 16/17, *are* paged and *are* in — `name`
  only.)
- **The events calendar day/week/month views** (`Event/Calendar.cshtml` +
  `?view=`) — **display-only** temporal layouts where the *position is the
  content* (a day is a day, a column is a column); not a sortable list.
- **Kanban column order** (`Projects/BoardDetail`) — the card order within a
  column is **content the user sets** (the `Order` field), not a list sort.
- **The bookmarks list** (`BookmarksController.Index` →
  `IBookmarkService.ListAsync`) — **personal, grouped, not paged** (the M17
  `BookmarkListResult.Groups` shape); a personal read, not a community
  feed/list.
- **Any relevance scoring.** M8 search relevance is **frozen**; the search
  control offers **only** `created` + `title` — a `?sort=relevance` request
  invents **no** order (it falls back to the surface default, F4).
- **Any `AccessAudit` / `AccessAction` / schema / bounded-context change.**
  Sorting is a display facet (C-SORT·4 / C-SORT·7).

## Consequences

- **Every paged list/search surface is order-selectable** with two query
  params (`?sort=<key>`, `?dir=asc|desc`) and one shared control — a new
  paged surface drops in one `<partial name="_Sort">` line + the
  `FilterParams` entry; it does not re-invent a sort control.
- **No existing surface's order changes** for a viewer who does not choose a
  sort — C-SORT·2 is pinned by the per-group
  `…_SortSpecNull_CurrentOrder` tests (13 Core test files: the 6 pure
  `SortSpecTests` + 12 group files over `PostgresFixture`; 9 Web test files
  over the NSubstitute harness).
- **The sort is invisible to the privacy model** — zero new `AccessAudit`
  rows, zero new `AccessAction`, one aggregate audit row per page-visit,
  unchanged (C-SORT·4; the M7 C-M7·1 / C-M7·2 discipline holds).
- **The operator config is unchanged** — sort is a per-request param, not a
  deploy-time knob; no new env var (OPS §Configuration reference notes it).
- **M27 (user-scoped portability) starts from the locked text:** the
  milestone flip (M26 → `StatusDone`, M27 → `StatusNext`) lands in the
  close unit U18 per C-SORT·6; this ADR's surface is frozen.
