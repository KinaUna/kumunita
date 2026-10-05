# M26 — Sorting — design (Part 1)

> **Milestone M26.** The one canonical **sorting** contract (a pure Core
> `SortSpec` value object + a per-surface **closed allowlist** of sort keys)
> threaded as an **additive, default-preserving** `SortSpec?` through the
> **18 paged list/search seams** M7 (ADR 0090) paged, plus **one shared Web
> `_Sort` control** (`SortViewModel`) whose current values ride the existing
> `_Pager` links (the M7 D7 `FilterParams` mechanism), and a **closed `sort.*`
> kw-l set** × en/de/fr/da. **Part 2** (the exact C# shapes of `SortSpec` /
> `SortKeys`, the per-surface allowlists confirmed against the models, the
> Web `_Sort` contract, the pinned test names, the U17 acceptance gate, and
> the drift-guard) is written by **U2**.
>
> **Sealed-unit register:** `docs/plans-milestones/plan-m26-sorting.md`; the
> scratch log is `docs/plans-milestones/in-progress/m26-handoff-notes.md`
> (one `## U#` section per unit, appended, never rewritten — created here,
> by U1).
>
> **Status.** **In progress (U1).** The invariants **C-SORT·1–8** and the
> FACES **F1–F12** in this file are **frozen once written** (the M25
> `C-UP·1–7` / F1–F10 precedent; the M7 C-M7·1–7 precedent). The 18-surface
> catalog in §6 is a **proposal** — U2 confirms it against the Core models
> before any seam is changed.
>
> **Scope of this file:** what this milestone is; the existing surface it
> rides; the design decisions (locked here in Part 1, shaped in Part 2); the
> invariants (C-SORT·1–8); the FACES (F1–F12); the 18-surface catalog; the
> parts affected; and the risks. **No code in this unit — no build, no
> tests.**

## 1. Context

M7 (ADR 0090, 2026-09-26) shipped **pagination** (the `?page=` param + the
`HasMore` signal on every paged Core seam + the `_Pager` partial +
`PagedViewModel` + the filter-reset rule) and **filtering** (the existing
filters, frozen, made to paginate correctly). **M3** shipped the actual
**ordering** — and it has been **hardcoded in Core** ever since: every paged
list seam does a fixed `OrderBy`/`OrderByDescending` over the *already-
authorized* candidate set (posts `OrderByDescending(p => p.Created)`, events
upcoming `OrderBy(Start)`, projects `OrderByDescending(Created)`, …).

A resident reading the community feed, the tag browsing list, or the search
results has **no way to choose the order** — newest-first is the only order,
on every surface. M26 makes that order **user-selectable** over the **18
paged list/search surfaces** (the register's catalog, §6 below): post feeds,
events, projects, announcements, documents, inventory, tag browsing,
people-find, and search.

**What M26 is, in one sentence:** a pure Core `SortSpec` value object + a
per-surface **closed allowlist** of sort keys + an additive `SortSpec?`
parameter on each of the 18 seams (default-preserving — `null` reproduces
today's hardcoded order **exactly**) + one shared Web `_Sort` control riding
the M7 `_Pager` links + a closed `sort.*` kw-l label set × en/de/fr/da.

**The one thing every unit must respect (C-SORT·2, default-preserving):**
**no existing surface's order changes** when a viewer does not choose a
sort. The moment an unsorted read's output differs from today's, that is a
regression — the pinned tests (U3–U9) exist to catch it. And the
**single-in-progress** contract (C-SORT·6): M26 begins **only** with M25
`StatusDone` / M26 `StatusNext` (checked at U1 — see the handoff note), and
**U18** closes M26 (`StatusDone`) and promotes M27 (`StatusNext`).

**What M26 is *not* (the named non-decisions, pinned here):**

- **The people catalog** (`DirectoryController.Index` →
  `DirectoryService.ListAsync`) — **not paged** today (loads the whole
  visible set, no `?page=`); a non-paged catalog, not a feed/list. M26 does
  **not** page it; if paging lands later, sorting follows then.
- **The events calendar day/week/month views** (`Event/Calendar.cshtml` +
  `?view=`) — **display-only** temporal layouts where the *position is the
  content* (a day is a day, a column is a column); not a sortable list.
- **Kanban column order** (`Projects/BoardDetail`) — the card order within a
  column is **content the user sets** (the `Order` field), not a list sort.
- **The bookmarks list** (`BookmarksController.Index` →
  `IBookmarkService.ListAsync`) — **personal, grouped, not paged** (the M17
  `BookmarkListResult.Groups` shape); a personal read, not a community
  feed/list.
- **Any relevance scoring.** M8 search relevance is **frozen**; M26 sorts
  the *surface's* date/title keys, never invents a relevance score.
- **Any `AccessAudit` / `AccessAction` / schema / bounded-context change.**
  Sorting is a display facet (C-SORT·4 / C-SORT·7).

## 2. Scope

**In** (this milestone, in the order units land it):

- **The pure Core `SortSpec` value object** (`record SortSpec(string Key,
  bool Descending)`) + the **pure `SortKeys.Parse`** (raw `?sort=` string ×
  a surface's **closed allowlist** → a `SortSpec`, falling back to the
  surface's **default key/direction** on an unknown key or invalid
  direction — **never** a property/reflection lookup from the raw string).
  HTTP-free, per C-SORT·3 (ADR 0006-D).
- **The additive `SortSpec?` on the 18 paged seams** — each gains an
  **optional** parameter (Part 2 pins the exact position per seam); the
  existing `HasMore`/`page` contract is **untouched**; `null` → the
  surface's current hardcoded order **exactly** (C-SORT·2).
- **The per-surface closed allowlists** — each surface declares a **closed
  set** of sort keys (a `switch` over the key set in Core, per C-SORT·1);
  Part 2 confirms each against the Core models.
- **The `SortViewModel` + the `_Sort` partial** — one shared control (a
  `<select>` or link set of the surface's allowed keys × the two
  directions), mirroring the `_Pager` partial; the current `?sort=`/`?dir=`
  values carried on the **pager links** via `PagedViewModel.FilterParams`
  (the M7 D7 mechanism, C-SORT·8).
- **The `sort.*` kw-l set × en/de/fr/da** — the control's option labels
  close a new kw-l set with an English floor (the `pagination.*` /
  `documents.*` precedent), registered on the `KnownTranslationKeys`
  registry and covered by `KnownTranslationKeys_ParityTests`.
- **The pinned Core + Web tests** (Part 2 pins the exact names; the seam is
  the U3–U9 test-bearing units) + **the U17 acceptance gate**.
- **ADR 0145** (Sorting: user-selectable order over the 18 paged
  list/search seams; the pure `SortSpec` + per-surface allowlists; the
  default-preserving rule; the named non-decisions; the ADR 0090
  relationship) + **OPS / README Roadmap / `Milestones.cs` sync** (the
  `MilestonesTests.cs` parity contract).

**Out (the named non-decisions above):** the people catalog (non-paged);
the calendar day/week/month views (display-only); kanban column order
(content); the bookmarks list (personal/unpaged); relevance scoring (M8
frozen); any schema/authorization/context change.

## 3. The existing surface this milestone rides (verified)

Read directly from the actual files (not assumed), each a **frozen seam**
M26 amends additively (an **optional** `SortSpec?` parameter; Part 2 pins
the exact shapes):

1. **`FeedResult`** (`Kumunita.Core/Posts/FeedResult.cs`) —
   `(Visible, HiddenCount, Page, Total, HasMore)` — the record-shaped
   result the post feeds return; the M3 `OrderByDescending(p => p.Created)`
   sits in `PostService` **before** the `Skip/Take`. M26's `SortSpec?`
   lands in the `PostService` seam signature, not on `FeedResult`.
2. **`EventPage` / `GroupEventFeedResult` / `TodoPage` / `BoardPage` /
   `GoalPage` / `ProjectPage` / `AnnouncementPage` / `DocumentListResult` /
   `ItemPage` / `TagPostPage` / `TagPagePage` / `ProfileTagPage` /
   `ProfileBioPage` / `SearchSurfacePage`** — the per-surface page records
   (ADR 0090 D3 / the register's §Understanding table); the hardcoded
   `OrderBy`/`OrderByDescending` lives in each service, over the
   already-authorized candidate set. M26 changes **the order clause**,
   never the record shape (C-SORT·7).
3. **`PagedViewModel`** (`Kumunita.Web/Models/PagedViewModel.cs`) — the
   `FilterParams` dictionary the `_Pager` links carry as query pairs (M7
   D7). **M26 rides this verbatim:** `?sort=`/`?dir=` enter
   `FilterParams` the same way the M7 filters do (C-SORT·8) — **no new
   pager mechanism**.
4. **`_Pager.cshtml`** (`Kumunita.Web/Views/Shared/_Pager.cshtml`) — the
   `PagerLink(page)` builder (page first, then `FilterParams`). The `_Sort`
   control's links reuse the same builder shape (Part 2 pins it); the
   `kw-l` registry + the `EffectiveLanguageCode` resolution is the i18n
   floor the `sort.*` set adopts.
5. **`PostsController.cs`** (representative) — `Pager =
   (feed.HasMore || page > 1) ? PagedViewModel.ForRoute($"/community/{id}",
   page, 30, feed.HasMore) : null` — the per-surface pager wiring M26
   extends with the `sort`/`dir` filter params + the `SortViewModel`.
6. **`Milestones.cs`** — **C-SORT·6 confirmed (U1):** M25 `StatusDone`,
   M26 `StatusNext` (the precondition holds; M27 `StatusPlanned`).
7. **`KnownTranslationKeys`** (`Kumunita.Core.Localization`) — the `kw-l`
   registry with the four seeded languages (`en` / `de` / `fr` / `da`); the
   `sort.*` set is additive on it (F12).
8. **`PostgresFixture`** (`tests/Kumunita.Core.Tests`, Testcontainers
   `postgres:18`) — the Core pinned-test harness (U3–U9);
   `Kumunita.Web.Tests` uses NSubstitute (no Postgres) for the Web pins.

## 4. The design decisions (locked here in Part 1, shaped in Part 2)

### 4.1 A pure Core `SortSpec`, not an HTTP type (D-SORT·1)

`record SortSpec(string Key, bool Descending)` + the pure
`SortKeys.Parse(raw, allowed, defaultKey, defaultDir)` — **no HTTP, no
`IFormFile`, no `ActionResult`** (C-SORT·3, ADR 0006-D, the M25 `C-UP·3`
precedent). The `?sort=`/`?dir=` query-param parsing and the view mapping
are **Web-only** (the `SortViewModel` + the `_Sort` partial). The Core
change is an **optional parameter + a `switch` over the closed key set** —
no seam signature is broken. *(C-SORT·1, C-SORT·3.)*

### 4.2 A per-surface **closed** allowlist (D-SORT·2)

Each surface declares a **closed** set of sort keys (a `switch` over the
key set in Core). A raw `?sort=` string is **mapped** against the allowlist
→ a known key or the **default**; it is **never** a property/reflection
lookup (C-SORT·1, sort-injection pin). A key **not** in the allowlist →
the surface's default key (F4); an invalid/absent `?dir=` → the key's
default direction (F5). **No error, no 500** (F4/F5). *(C-SORT·1.)*

### 4.3 Default-preserving (D-SORT·3)

An **absent/`null`** `SortSpec` reproduces the surface's **current**
hardcoded order **exactly** — no unsorted read's output changes
(C-SORT·2, the regression-pin the U3–U9 tests enforce). When non-null, the
seam orders by the spec's key/direction **plus a stable tie-breaker**
(C-SORT·5). The Core `OrderBy`/`OrderByDescending` clause is **replaced**
by a `switch` over the closed key set — the `Skip/Take` / `HasMore`
contract is **untouched**. *(C-SORT·2, C-SORT·5.)*

### 4.4 Sort is a **display facet, never a gate** (D-SORT·4)

Re-ordering **never** changes *which* rows are authorized — the
`CanSeeAsync` call, the `AccessAudit` row, and the `HiddenCount` shape are
all **untouched** (C-SORT·4, the ADR 0064 `?view=` display-selector
precedent + the M7 C-M7·2 "display-only" pin). A signed-in and an anonymous
viewer on a visible surface get the **same** sort behavior (F10); a sorted
read emits **zero** new `AccessAudit` rows vs. an unsorted read (F11).
*(C-SORT·4.)*

### 4.5 **One** shared Web `_Sort` control, riding the M7 `_Pager` (D-SORT·5)

A `SortViewModel` + a `_Sort` partial (mirroring the `_Pager` partial)
render the surface's allowed keys × the two directions (a `<select>` or a
link set). The current `?sort=`/`?dir=` values are **carried on the pager
links** via the existing `PagedViewModel.FilterParams` mechanism (C-SORT·8,
the M7 D7 mechanism — **no new pager idiom**). The control offers **exactly**
the surface's allowed keys (F9 — no dead options). A new paged surface
drops in one `<partial name="_Sort">` line + the `FilterParams` entry; it
does not re-invent a sort control. *(C-SORT·1, C-SORT·8.)*

### 4.6 A **closed** `sort.*` kw-l set × en/de/fr/da (D-SORT·6)

The control's option labels (`sort.<key>`) close a **new** kw-l set × the
four seeded languages with an English floor (the `pagination.*` /
`documents.*` / `a11y.*` precedent), registered on the
`KnownTranslationKeys` registry and covered by
`KnownTranslationKeys_ParityTests` (F12). The `sort.*` set is **additive**
on the existing registry; a new surface's new key adds a new `sort.<key>`
row, not a new registry. *(F12 — the i18n floor.)*

### 4.7 **No** new document / context / schema (D-SORT·7)

`SortSpec` is a **value object** (no Marten doc, no `DocTypes` surface, no
`SchemaBootstrap` change) — the ADR 0004 §B / ADR 0006-D discipline
(C-SORT·7). **Zero** new `AccessAction`, **zero** new `AccessVia`, **zero**
new bounded context, **zero** new index (C-SORT·4 / C-SORT·7, the M7
"zero new authorization surface" precedent).

### 4.8 **Stable** ordering (D-SORT·8)

Every ordered query ends with a **unique** tie-breaker (`.ThenBy(Id)`) so a
`HasMore` window never shuffles rows (C-SORT·5, the M7 "one honest signal"
precedent — a shuffled page is a *wrong* `HasMore` signal). When the
requested key is `Id` itself, the tie-breaker is a no-op. *(C-SORT·5.)*

## 5. Invariants (pinned for M26)

The **8 invariants**, each with a one-line M26 note (the exact ids/names are
**frozen** once written):

- **C-SORT·1 — closed allowlist per surface.** Each surface declares a
  **closed** set of sort keys; a raw `?sort=` outside the allowlist (or a
  malformed `?dir=`) → the surface's **default** key/direction; **never** a
  property/reflection lookup from the raw string (no sort-injection).
- **C-SORT·2 — default-preserving.** An absent/`null` `SortSpec` reproduces
  the surface's **current hardcoded order exactly** — no unsorted read's
  output changes (the U3–U9 tests pin this).
- **C-SORT·3 — `Core` stays HTTP-free (ADR 0006-D).** `SortSpec` +
  `SortKeys.Parse` are **pure** Core; the `?sort=`/`?dir=` parsing and the
  view mapping are **Web-only**.
- **C-SORT·4 — sort is a display facet, never a gate.** Re-ordering never
  changes *which* rows are authorized; **zero** new `AccessAudit` row,
  **zero** new `AccessAction`, the `CanSeeAsync` call is **untouched**.
- **C-SORT·5 — stable ordering.** Every ordered query ends with a **unique
  tie-breaker** (`.ThenBy(Id)`) so a `HasMore` window never shuffles rows.
- **C-SORT·6 — single-in-progress milestone contract (MilestonesTests).**
  M26 starts **only** after M25 is `StatusDone`; M26's close (U18) promotes
  M27. *(Checked at U1 — see the handoff note.)*
- **C-SORT·7 — no new bounded context / document / schema.** `SortSpec` is a
  **value object** (no Marten doc, no `DocTypes` surface, no
  `SchemaBootstrap` change).
- **C-SORT·8 — sort rides the pager.** The current `?sort=`/`?dir=` values
  are carried on the `_Pager` links (the M7 D7 `FilterParams` mechanism) so
  prev/next **preserve the sort**.

## 6. FACES (pinned, 12)

The **12 FACES**, each bound to an invariant (the exact names are **frozen**
once written):

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
- **F12** the `sort.*` labels **resolve** in **en/de/fr/da** (the kw-l
  closed set) — the i18n floor

## 7. The 18-surface catalog

**A proposal** (from the register's `Understanding` table) — **U2 confirms
against the Core models** before any seam is changed. "Current order" is
the **hardcoded** `OrderBy`/`OrderByDescending` in the service today;
"sort keys (proposal)" is the **closed allowlist** M26 will offer (each
key is a `sort.<key>` kw-l label).

| # | Surface | Core seam (paged) | Result record | Current order (hardcoded) | Sort keys (proposal) |
|---|---|---|---|---|---|
| 1 | Community post feed | `PostService.ListFeedAsync` | `FeedResult` | `OrderByDescending(p => p.Created)` | `created` (newest first), `created` asc (oldest first) |
| 2 | All-sections post feed | `PostService.ListAllFeedAsync` | `FeedResult` | `OrderByDescending(p => p.Created)` | `created` (newest first), `created` asc (oldest first) |
| 3 | Group post feed | `PostService.ListGroupFeedAsync` | `FeedResult` | `OrderByDescending(p => p.Created)` | `created` (newest first), `created` asc (oldest first) |
| 4 | Events (upcoming) | `EventService.ListUpcomingAsync` | `EventPage` | `OrderBy(Start)` | `start` (earliest first), `start` desc (latest first), `created` |
| 5 | Events (past) | `EventService.ListPastAsync` | `EventPage` | `OrderByDescending(Start)` | `start` (latest first), `start` asc (earliest first), `created` |
| 6 | Group events | `EventService.ListGroupEventsAsync` | `GroupEventFeedResult` | `OrderBy(Start)` | `start` (earliest first), `start` desc (latest first), `created` |
| 7 | Todos | `ProjectService.ListTodosAsync` | `TodoPage` | `OrderByDescending(Created)` | `created`, `due` (if present) |
| 8 | Boards | `ProjectService.ListBoardsAsync` | `BoardPage` | `OrderByDescending(Created)` | `created`, `name` |
| 9 | Goals | `ProjectService.ListGoalsAsync` | `GoalPage` | `OrderByDescending(Created)` | `created`, `name` |
| 10 | Projects | `ProjectService.ListProjectsAsync` | `ProjectPage` | `OrderByDescending(Created)` | `created`, `name` |
| 11 | Announcements | `AnnouncementService.ListVisiblePagedAsync` | `AnnouncementPage` | `OrderByDescending(Created)` | `created` |
| 12 | Documents | `DocumentService.ListAsync` | `DocumentListResult` | `OrderByDescending(Created)` | `created`, `name` |
| 13 | Inventory | `InventoryService.ListItemsAsync` | `ItemPage` | `OrderByDescending(Created)` | `created`, `name` |
| 14 | Tag → posts | `TagService.ListPostsByTagPagedAsync` | `TagPostPage` | `OrderByDescending(Created)` | `created` |
| 15 | Tag → pages | `TagService.ListPagesByTagPagedAsync` | `TagPagePage` | `OrderBy(Created)` | `created` |
| 16 | People → by tag | `ProfileFindService.FindPeopleByTagAsync` | `ProfileTagPage` | by display name | `name` |
| 17 | People → by bio | `ProfileFindService.FindPeopleByBioAsync` | `ProfileBioPage` | by display name | `name` |
| 18 | Search (per surface) | `SearchService.SearchSurfaceAsync` | `SearchSurfacePage` | `OrderByDescending(...Created)` per surface | `created` (the surface's date key) |

**The catalog is the U2 confirmation target** — Part 2 pins the **exact**
sort keys per surface (some surfaces may gain or lose a key; e.g. `due`
on todos only if the model has a due-date field; `name` on a surface only
if the model has a name/title field). The `created` key is the **default**
on every surface (C-SORT·2 — the current hardcoded order).

## 8. Parts affected

**Core (U2–U6):** `Kumunita.Core` — the `SortSpec` + `SortKeys` value
objects (a new `Kumunita.Core.Sorting` module or an existing context, per
the M25 `Kumunita.Core.Usage` precedent — Part 2 pins it); the 18 seams
(3 in `PostService`, 3 in `EventService`, 4 in `ProjectService`, 1 in
`AnnouncementService`, 1 in `DocumentService`, 1 in `InventoryService`,
2 in `TagService`, 2 in `ProfileFindService`, 1 in `SearchService`) each
gain an **optional** `SortSpec?` parameter + a `switch` over the closed key
set (Part 2 pins the exact per-seam signature + the exact sort-key
`switch`). **No new bounded context, no new document, no new `AccessAction`**
(C-SORT·7).

**Web (U7–U11):** `Kumunita.Web` — the `SortViewModel` + the `_Sort`
partial (mirroring the `_Pager` partial); the 18 controllers (the 9
`PostsController` / `GroupsController` / `EventController` /
`ProjectsController` / `AnnouncementController` / `DocumentController` /
`InventoryController` / `TagController` / `ProfileFindController` /
`SearchController` actions that already build a `PagedViewModel`) each
parse `?sort=`/`?dir=` → a `SortSpec` (via `SortKeys.Parse`) + pass it to
the seam + add the `sort`/`dir` filter params to the `PagedViewModel`
(the C-SORT·8 mechanism) + render the `_Sort` partial. **The `sort.*`
kw-l set** (the 18 surfaces' keys, Part 2 pins the exact set) registered on
the `KnownTranslationKeys` registry + covered by
`KnownTranslationKeys_ParityTests` (F12).

**Tests (U3–U9, U17):** `tests/Kumunita.Core.Tests` — the per-surface
Core seam tests (the `SortSpec`-null / `SortSpec`-non-null / the tie-breaker
/ the F4/F5 default-fallback cases; the `PostgresFixture` harness);
`tests/Kumunita.Web.Tests` — the per-surface Web controller tests (the
`?sort=`/`?dir=` parsing / the `FilterParams` carry / the `_Sort`
rendering; the NSubstitute harness). **The U17 acceptance gate** (the
three-test shape — closed-loop / handoff / part-vs-whole — per the M25
`§2.5` precedent).

**Docs (U12–U18):** `docs/design/m26-sorting-design.md` (Part 2, U2;
the "M26 — Closed (recorded)" section, U18); **ADR 0145** (U13);
`docs/OPS.md` (no new env vars — M26 is pure + additive, no deploy-time
knob; the `sort.*` kw-l set is the only new surface); `README.md` (the M26
Roadmap entry); `Milestones.cs` + `MilestonesTests.cs` (the C-SORT·6
contract, U18).

## 9. Risks & mitigations

- **R1 — sort-injection (a raw string → a property lookup).** **Mitigation:**
  C-SORT·1 (the closed allowlist; a `switch` over the key set in Core;
  **never** a property/reflection lookup) + F4 (an unknown key → the
  default, not an error).
- **R2 — a regression in an unsorted read's order (C-SORT·2 violated).**
  **Mitigation:** the U3–U9 pinned tests pin the **null** `SortSpec` →
  current order **exactly**; a `null` `SortSpec` is the **default** case in
  every seam (the `switch` falls through to the current `OrderBy` clause).
- **R3 — a page shuffle across a `HasMore` window (C-SORT·5 violated).**
  **Mitigation:** D-SORT·8 (the stable tie-breaker `.ThenBy(Id)` on every
  ordered query) + F7 (the pinned test).
- **R4 — the pager dropping the sort (C-SORT·8 violated).**
  **Mitigation:** the `?sort=`/`?dir=` values enter
  `PagedViewModel.FilterParams` (the M7 D7 mechanism, the `_Pager`'s
  `PagerLink` builder) — **no new pager idiom** (D-SORT·5).
- **R5 — a new `AccessAudit` row (C-SORT·4 violated).**
  **Mitigation:** the `CanSeeAsync` call + the `AccessAudit` row + the
  `HiddenCount` shape are **untouched** (the ADR 0064 `?view=`
  display-selector precedent + the M7 C-M7·2 "display-only" pin) + F11
  (the pinned test).
- **R6 — a new document / context / schema (C-SORT·7 violated).**
  **Mitigation:** D-SORT·7 (the `SortSpec` is a value object; no Marten
  doc, no `DocTypes` surface, no `SchemaBootstrap` change; the ADR 0004 §B
  / ADR 0006-D discipline).
- **R7 — an unresolvable `sort.*` label (F12 violated).**
  **Mitigation:** the `sort.*` set is registered on the `KnownTranslationKeys`
  registry + covered by `KnownTranslationKeys_ParityTests` (the
  `pagination.*` / `documents.*` / `a11y.*` precedent) + the English
  floor (the `kw-l` TagHelper's `key=` + the fallback string).

## 10. Drift-guard (frozen once Part 2 lands)

The **8 invariants** (C-SORT·1–8), the **12 FACES** (F1–F12), the 18-surface
catalog (Part 2 pins the exact sort keys per surface), the `SortSpec` +
`SortKeys` shape, the per-seam `SortSpec?` parameter position, the
`SortViewModel` + the `_Sort` partial shape, the `sort.*` kw-l set, the
pinned test names (U3–U9, U17), and the **C-SORT·6 milestone contract**
(M25 `StatusDone` / M26 `StatusNext` at start — **confirmed at U1**; M26
`StatusDone` + M27 `StatusNext` at close, U18) — all frozen pins. Any
mismatch (e.g. a Core model lacks a field a sort key proposes, or a
`Milestones.cs` status disagrees with the C-SORT·6 contract) is a
**`## U<m> — Drift pause`** per the M25 unit-series rule §6 (the handoff
note is the scratch log).

## 11. Next unit (U2)

Append `## Seams & contracts (Part 2, written by U2)` to this file:
**the exact C# shapes** (U3–U9) — the `SortSpec` + `SortKeys.Parse` exact
signatures, the per-seam `SortSpec?` parameter position, the per-surface
allowlists **confirmed against the Core models**, the Web `_Sort` contract
+ the pager-carry rule, the `sort.*` kw-l set (the exact key set per
surface), the **pinned test names** (U3–U9, U17), the **U17 acceptance
gate**, and the **drift-guard** (this §10, expanded per the M25 `§2.6`
precedent). See `m26-u02.md`.
