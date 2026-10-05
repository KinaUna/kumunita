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

## Seams & contracts (Part 2, written by U2)

> *Part 2 is `how` — the exact C# shapes each later unit implements, the
> 18 per-surface **closed** allowlists **confirmed against the Core models**,
> the Web `_Sort` contract + the pager-carry rule, the pinned test names,
> the U17 acceptance gate, and the drift-guard. The invariant / FACES ids are
> those frozen in Part 1; the C# shapes below are **frozen once written**
> (see §2.8). All new Core types live in a **new namespace**
> `Kumunita.Core.Query` (a value object + a static parser — **no new bounded
> context**, C-SORT·7; the Part 1 §8 "new `Kumunita.Core.Sorting` module or
> an existing context" question is pinned here to `Kumunita.Core.Query`).*
> *Every allowlist below was read against the actual model file before
> locking; the three corrections to the Part 1 §7 proposal are recorded in
> §2.8 (they are locked here, not drift-pauses).*

### 2.1 New Core-owned types (exact C#) — a new namespace `Kumunita.Core.Query`

Both types are **HTTP-free** (C-SORT·3 / ADR 0006-D) and **pure**
(C-SORT·7 — a value object + a static parser; the ADR 0004 §B / ADR 0006-D
discipline — no Marten doc, no `DocTypes` surface, no `SchemaBootstrap`
change):

```csharp
namespace Kumunita.Core.Query;

/// <summary>
/// A resolved sort request (C-SORT·1/3/7). <see cref="Key"/> is a
/// **lowercase** surface key (e.g. <c>"created"</c>) — the raw <c>?sort=</c>
/// value is mapped against the surface's **closed allowlist** (D-SORT·2)
/// before this is ever constructed, so a seam never sees a raw string;
/// <see cref="Descending"/> is the **resolved** direction (C-SORT·1). A
/// value object: no I/O, no HTTP (C-SORT·3/7).
/// </summary>
public sealed record SortSpec(string Key, bool Descending);

/// <summary>
/// The **pure** parser (C-SORT·1/3): maps a raw <c>?sort=</c>/<c>?dir=</c>
/// pair against a surface's **closed** allowlist. **No reflection, no
/// property lookup, no I/O** (no sort-injection, R1) — the raw string is
/// matched by value, never resolved to a member.
/// </summary>
public static class SortKeys
{
    public static SortSpec Parse(
        string? key, string? dir,
        IReadOnlySet<string> allowedKeys,
        string defaultKey, bool defaultDir)
    {
        // C-SORT·1 / F4: an unknown/blank key → the surface's default key.
        var k = (key ?? string.Empty).Trim().ToLowerInvariant();
        var resolvedKey = k.Length > 0 && allowedKeys.Contains(k)
            ? k
            : defaultKey;

        // C-SORT·1 / F5: "asc" → ascending, "desc" → descending,
        // null/other → the key's default direction. Never an error.
        var descending = dir switch
        {
            "asc" => false,
            "desc" => true,
            _ => defaultDir,
        };

        return new SortSpec(resolvedKey, descending);
    }
}
```

- **No DI registration** (a value object + a static parser — a `using`
  import per service file is the whole wiring; U3 confirms
  `AddKumunitaCore` gains **zero** new registrations, C-SORT·7).
- The Web layer is the **only** caller of `SortKeys.Parse` (C-SORT·3) — the
  controller reads `?sort=`/`?dir=` from `Request.Query`, calls `Parse`
  against its surface's allowlist, and passes the resulting `SortSpec?` to
  the seam.

### 2.2 The 18 per-surface allowlists (locked — property names confirmed in the models)

**Comparator rules (all surfaces):**

- **Tie-breaker:** every ordered query — a non-`null` spec — ends with
  **`.ThenBy(x => x.Id)`** (C-SORT·5; when the key is `Id` itself the
  tie-breaker is a no-op, D-SORT·8).
- **Nullable dates** (`Modified`, `DueAt`): compare
  `x.Prop ?? DateTimeOffset.MinValue` (honest sentinel, no error).
  **Exception — `due` on Todos:** **nulls last in *both* directions** (the
  Part 1 "if present" pin, resolved): an undated to-do never floats above
  dated ones when the viewer chooses `dir=asc`
  (`.ThenBy(x => x.DueAt is null)` before the value compare).
- **Strings** (`Title`, `Name`, `DisplayName`, `Status`):
  **`StringComparer.OrdinalIgnoreCase`**, with `null → string.Empty` (the
  `Post.Title` / `SearchHit.Title` nulls and the `TodoItem.Status` label
  never break the sort).
- **`size`** (Documents): `SizeBytes` is `long`, **non-null** (the
  `Document` model) — compared directly.
- Each entry's **key's default direction** is shown; the surface's
  **default key/dir = its current hardcoded order** (C-SORT·2,
  D-SORT·3) — an **absent/`null`** `SortSpec` skips the `switch` entirely
  and leaves today's `OrderBy` line untouched.

| # | Surface (Core seam) | Model (confirmed) | Allowlist — `key` → property · comparator · key's default dir | Surface default (key, dir) |
|---|---|---|---|---|
| 1 | Community post feed (`PostService.ListFeedAsync`) | `Post` | `created` → `Created` (direct, non-null) · desc; `modified` → `Modified` (`?? MinValue`, nullable) · desc; `title` → `Title` (`?? ""`, OrdinalIgnoreCase, nullable in the model) · asc | `created`, desc |
| 2 | All-sections post feed (`PostService.ListAllFeedAsync`) | `Post` | as row 1 | `created`, desc |
| 3 | Group post feed (`PostService.ListGroupFeedAsync`) | `Post` | as row 1 | `created`, desc |
| 4 | Events upcoming (`EventService.ListUpcomingAsync`) | `Event` | `start` → `Start` (direct, non-null) · asc; `created` → `Created` · desc; `title` → `Title` (non-null) · asc | `start`, asc |
| 5 | Events past (`EventService.ListPastAsync`) | `Event` | `start` (non-null) · desc; `created` · desc; `title` · asc | `start`, desc |
| 6 | Group events (`EventService.ListGroupEventsAsync`) | `Event` | `start` · asc; `created` · desc | `start`, asc |
| 7 | Todos (`ProjectService.ListTodosAsync`) | `TodoItem` | `created` → `Created` · desc; `modified` → `Modified` (nullable) · desc; `title` → `Title` (non-null) · asc; `due` → `DueAt` (nullable, **nulls last both dirs**) · asc; `status` → `Status` (nullable string, `?? ""`) · asc | `created`, desc |
| 8 | Boards (`ProjectService.ListBoardsAsync`) | `KanbanBoard` | `created` · desc; `modified` (nullable) · desc; `title` (non-null) · asc | `created`, desc |
| 9 | Projects (`ProjectService.ListProjectsAsync`) | `Project` | `created` · desc; `modified` (nullable) · desc; `title` (non-null) · asc | `created`, desc |
| 10 | Goals (`ProjectService.ListGoalsAsync`) | `ProjectGoal` | `created` · desc; `modified` (nullable) · desc; `title` (non-null) · asc | `created`, desc |
| 11 | Announcements (`AnnouncementService.ListVisiblePagedAsync`) | `Announcement` | `created` · desc; `modified` (nullable) · desc; `title` (non-null) · asc | `created`, desc |
| 12 | Documents (`DocumentService.ListAsync`) | `Document` | `created` · desc; `modified` (nullable) · desc; `title` (non-null) · asc; `size` → `SizeBytes` (`long`, non-null) · asc | `created`, desc |
| 13 | Inventory (`InventoryService.ListItemsAsync`) | `InventoryItem` | `created` · desc; `modified` (nullable) · desc; `name` → `Name` (non-null) · asc | `created`, desc |
| 14 | Tag → posts (`TagService.ListPostsByTagPagedAsync`) | `Post` | `created` · **asc**; `modified` (nullable) · desc; `title` (nullable) · asc | `created`, **asc** (correction C-1, §2.8) |
| 15 | Tag → pages (`TagService.ListPagesByTagPagedAsync`) | `Page` | `created` · **asc**; `modified` (nullable) · desc; `title` (non-null) · asc | `created`, **asc** |
| 16 | People → by tag (`ProfileFindService.FindPeopleByTagAsync`) | `Profile` | `name` → `DisplayName` (non-null) · asc | `name`, asc (correction C-2, §2.8) |
| 17 | People → by bio (`ProfileFindService.FindPeopleByBioAsync`) | `Profile` | `name` → `DisplayName` · asc | `name`, asc (correction C-2, §2.8) |
| 18 | Search, per surface (`SearchService.SearchSurfaceAsync`) | `SearchHit` | `created` → `Created` (non-null in the projection — the people surface carries `DateTimeOffset.MinValue`, still sort-stable) · desc; `title` → `Title` (nullable, `?? ""`) · asc | `created`, desc |

**18/18 locked.** The three corrections to the Part 1 §7 proposal (todos
dropping a non-existent `priority` key; the people surfaces dropping a
non-existent `created` key; the tag→posts surface's default direction being
**asc** in the actual code) are recorded and locked in §2.8.

### 2.3 The additive Core seam rule (C-SORT·2/5)

For **each** of the 18 seams: add an **optional** trailing parameter
**`SortSpec? sort = null`** (after the existing optional filters, before
`CancellationToken ct = default` where one exists — a new caller compiles
unchanged, C-SORT·2). When `sort is null` the pinned `OrderBy…` line is
**byte-for-byte unchanged**; when non-null, the ordering is **replaced** by
the surface's closed-allowlist `switch` + `.ThenBy(x => x.Id)`;
**`Skip/Take`, `HasMore`, and the `CanSeeAsync`/audit lines are untouched**
(C-SORT·4):

```csharp
// shape (Todos row 7 shown; each surface's switch is its §2.2 row):
IQueryable<TodoItem> q = /* …frozen predicates, unchanged… */;
if (sort is null)
    q = q.OrderByDescending(t => t.Created);          // ← the pinned line, verbatim
else
{
    q = sort.Key switch
    {
        "created"  => sort.Descending ? q.OrderByDescending(t => t.Created)  : q.OrderBy(t => t.Created),
        "modified" => sort.Descending ? q.OrderByDescending(t => t.Modified ?? DateTimeOffset.MinValue)
                                      : q.OrderBy(t => t.Modified ?? DateTimeOffset.MinValue),
        "title"    => sort.Descending ? q.OrderByDescending(t => t.Title, StringComparer.OrdinalIgnoreCase)
                                      : q.OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase),
        "due"      => sort.Descending ? q.OrderByDescending(t => t.DueAt ?? DateTimeOffset.MinValue)
                                      : q.OrderBy(t => t.DueAt ?? DateTimeOffset.MinValue),
        "status"   => sort.Descending ? q.OrderByDescending(t => t.Status ?? "", StringComparer.OrdinalIgnoreCase)
                                      : q.OrderBy(t => t.Status ?? "", StringComparer.OrdinalIgnoreCase),
        _          => q.OrderByDescending(t => t.Created),   // C-SORT·1 — unreachable (Parse already fell back); the default is pinned anyway
    };
    q = q.ThenBy(t => t.Id);                                  // C-SORT·5
}
```

**Pinned current `OrderBy…` line + context per seam** (read verbatim; U3–U9
edit exactly these lines):

| # | Seam | Pinned current ordering (file:line, exact text) |
|---|---|---|
| 1 | `PostService.ListFeedAsync` | `src/Kumunita.Core/Posts/PostService.cs:97` — `.OrderByDescending(p => p.Created)` in the `session.Query<Post>()…Where(p => p.ComponentId == componentId && p.DeletedAt == null && !p.IsDraft)` chain, directly before `.Skip((page - 1) * PageSize)` |
| 2 | `PostService.ListAllFeedAsync` | `PostService.cs:174` — `.OrderByDescending(p => p.Created)` (the `componentIds.Contains(p.ComponentId)` chain) |
| 3 | `PostService.ListGroupFeedAsync` | `PostService.cs:1171` — `.OrderByDescending(p => p.Created)` (the `p.GroupId == groupId` chain) |
| 4 | `EventService.ListUpcomingAsync` | `src/Kumunita.Core/Events/EventService.cs:102` — `var candidates = await q.OrderBy(e => e.Start).Skip((page - 1) * PageSize).Take(PageSize)…` |
| 5 | `EventService.ListPastAsync` | `EventService.cs:152` — `await q.OrderByDescending(e => e.Start).Skip…` |
| 6 | `EventService.ListGroupEventsAsync` | `EventService.cs:1581` — `.OrderBy(e => e.Start)` (the group-event chain) |
| 7 | `ProjectService.ListTodosAsync` | `src/Kumunita.Core/Projects/ProjectService.cs:135` — `await q.OrderByDescending(t => t.Created).Skip((page - 1) * PageSize).Take(PageSize)…` |
| 8 | `ProjectService.ListBoardsAsync` | `ProjectService.cs:524` — `await q.OrderByDescending(b => b.Created).Skip…` |
| 9 | `ProjectService.ListGoalsAsync` | `ProjectService.cs:1831` — `await q.OrderByDescending(g => g.Created).Skip…` |
| 10 | `ProjectService.ListProjectsAsync` | `ProjectService.cs:2040` — `await q.OrderByDescending(p => p.Created).Skip…` |
| 11 | `AnnouncementService.ListVisiblePagedAsync` | `src/Kumunita.Core/Announcements/AnnouncementService.cs:130` — `.OrderByDescending(a => a.Created)` (the visible-paged chain) |
| 12 | `DocumentService.ListAsync` | `src/Kumunita.Core/Documents/DocumentService.cs:82` — `.OrderByDescending(d => d.Created)` |
| 13 | `InventoryService.ListItemsAsync` | `src/Kumunita.Core/Inventory/InventoryService.cs:86` — `await q.OrderByDescending(i => i.Created).Skip…` |
| 14 | `TagService.ListPostsByTagPagedAsync` | `src/Kumunita.Core/Tags/TagService.cs:531` — `.OrderBy(p => p.Created)` **on the in-memory** `readablePosts` list (LINQ-to-objects, not the query) — **ascending** |
| 15 | `TagService.ListPagesByTagPagedAsync` | `TagService.cs:578` — `.OrderBy(p => p.Created)` (in-memory, ascending) |
| 16 | `ProfileFindService.FindPeopleByTagAsync` | `src/Kumunita.Core/UserInfo/ProfileFindService.cs:48` — **no `OrderBy` exists**: the gated `visible` list goes straight to `Paged(visible, page)` — the sort, when non-null, inserts the `DisplayName` ordering **between** the gate and `Paged`; when null, the list is untouched (storage order) |
| 17 | `ProfileFindService.FindPeopleByBioAsync` | `ProfileFindService.cs:95` — same: no `OrderBy`, `Paged(visible, page)` after the gate |
| 18 | `SearchService.SearchSurfaceAsync` | `src/Kumunita.Core/Search/SearchService.cs:175` — ordering lives **in the surface helpers** (`:234`/`:262`/`:291`/`:320` the `OrderByDescending(…Created)` candidate queries; `:376`/`:415` the group lanes' in-memory `visibleAll.OrderByDescending(h => h.Created)`); the seam then `Skip/Take`s `visible` — M26 applies the `SearchHit` ordering **in-memory over `visible`**, before the `Skip/Take`, and **only** when `sort` is non-null |

The `HasMore` / paging computation is **untouched** on all 18 (D-SORT·3,
C-SORT·4) — a paged seam's `Skip/Take` and `HasMore`/page-full signal keep
their exact expressions.

### 2.4 The Web `_Sort` contract + the pager-carry rule (C-SORT·8)

A new `Kumunita.Web.Models.SortViewModel`, mirroring `PagedViewModel`
(file: `src/Kumunita.Web/Models/PagedViewModel.cs`):

```csharp
namespace Kumunita.Web.Models;

/// <summary>
/// The one shared sort-control view model (M26, D-SORT·5).
/// <see cref="Options"/> is the surface's **closed** allowlist — the
/// control offers **exactly** those keys (F9, no dead options);
/// <see cref="CurrentKey"/> / <see cref="CurrentDir"/> echo the request's
/// resolved values (a null pair = the viewer chose no sort);
/// <see cref="CarriedParams"/> is the query-pair set the control's links
/// preserve (the `_Pager` `FilterParams` shape, M7 D7) — `page` + the
/// surface's existing filters.
/// </summary>
public sealed record SortViewModel(
    string BaseUrl,
    string? CurrentKey,
    string? CurrentDir,
    IReadOnlyList<(string Key, string Dir)> Options,
    IReadOnlyDictionary<string, string> CarriedParams)
{
    public static SortViewModel ForRoute(
        string baseUrl, string? currentKey, string? currentDir,
        IReadOnlyList<(string Key, string Dir)> options,
        IReadOnlyDictionary<string, string>? carriedParams = null)
        => new(baseUrl, currentKey, currentDir, options,
            carriedParams ?? new Dictionary<string, string>());
}
```

- **The `_Sort` partial** (`Views/Shared/_Sort.cshtml`) renders a
  **link set** (the pinned form — the `<select>` alternative is
  deliberately not adopted: a link set is the `_Pager` precedent, needs no
  form/JS, and behaves identically for signed-in and anonymous, F10). Its
  `SortLink(key, dir)` builder is the **exact** `_Pager` `PagerLink` shape:
  `BaseUrl` + `?page=` first, then the carried filter pairs
  (`Uri.EscapeDataString` on both sides), then the **toggled** `sort=`/
  `dir=` pair — i.e. the `?sort=`/`?dir=` values enter the link exactly the
  way the M7 filters do.
- **The list-surface view model** gains a **`SortViewModel? Sort`** field:
  `null` renders nothing (the one-page / no-sort pin — the `_Pager`
  `null ⇒ one page ⇒ no partial` precedent, M7 D7/F2); a surface with a
  non-null `SortViewModel` renders the `_Sort` link set above its list.
- **The pager-carry rule (C-SORT·8):** every paged surface's existing
  `PagedViewModel` `FilterParams` **gains the `sort` + `dir` pairs** (only
  when the request carried them — an unsorted read's pager links stay
  byte-identical, C-SORT·2) so **prev/next preserve the sort**:
  `page=2&component=x&sort=created&dir=asc` stays on that sort across
  `HasMore` windows (F8).

### 2.5 Pinned Core tests (exact names)

`tests/Kumunita.Core.Tests/Query/SortSpecTests.cs` (**U3** — the 6 pure
parser pins, no Postgres needed):

1. `Parse_AllowedKey_Applies`
2. `Parse_UnknownKey_Defaults`
3. `Parse_InvalidDir_Defaults`
4. `Parse_NullKey_Defaults`
5. `Parse_DirAsc_DescendingFalse`
6. `Parse_DirDesc_DescendingTrue`

Per group (**U4–U9** — the `PostgresFixture` harness; the **naming
convention** is pinned here, the group file paths are pinned by U4–U9).
The Posts group is pinned in full as the pattern
(`tests/Kumunita.Core.Tests/Posts/PostSortTests.cs`, U4):

7. `PostFeed_SortSpecNull_CurrentOrder` (C-SORT·2 pin — the
   byte-for-byte default-order regression guard)
8. `PostFeed_SortCreatedAsc`
9. `PostFeed_SortModifiedDesc`
10. `PostFeed_SortTitle_Ordinal`
11. `PostFeed_InvalidKey_DefaultOrder`
12. `PostFeed_StableTieBreakBy_Id` (C-SORT·5)

The **analogous** names exist for the other 11 seam groups (Events
upcoming / Events past / Group events; Todos; Boards; Projects; Goals;
Announcements; Documents; Inventory; Tags (posts + pages); People (tag +
bio); Search) — each group pins the same pattern (the
`SortSpecNull_CurrentOrder` C-SORT·2 pin + its per-key pins + the
`InvalidKey_DefaultOrder` F4 pin + the `StableTieBreakBy_Id` C-SORT·5 pin;
groups with fewer keys pin fewer per-key tests, **always** including the
C-SORT·2 and C-SORT·5 pins).

**Core test-file count (pinned):** **13** — `SortSpecTests.cs` + 12 group
files (Posts, Events, Todos, Boards, Projects, Goals, Announcements,
Documents, Inventory, Tags, People, Search; the Events/Boards/Projects/
Goals groups' exact file paths land with U4–U9).

### 2.6 Pinned Web tests (exact names)

`tests/Kumunita.Web.Tests/SortControlTests.cs` (**U10** — the 3 shared
pins, NSubstitute harness):

1. `CommunityFeed_SortControl_Renders_AllowedKeys` (F9)
2. `CommunityFeed_Pager_Carries_Sort_And_Dir` (C-SORT·8 / F8)
3. `CommunityFeed_SortParam_DefaultsWhenAbsent` (F1)

The **analogous** names exist for each U11–U15 surface group (Events;
Projects (todos/boards/projects/goals); Announcements; Documents;
Inventory; Tags; People; Search — the exact group test-file paths land with
U11–U15; each group re-pins the 3-test pattern over its surface's
controller).

**Web test-file count (pinned):** **9** — `SortControlTests.cs` (U10) + one
file per U11–U15 surface group (8 groups: Events; Projects; Announcements;
Documents; Inventory; Tags; People; Search — the exact file names land with
U11–U15).

### 2.7 The U17 acceptance gate (three-test shape — closed-loop / handoff /
part-vs-whole)

1. **closed-loop:** a sorted read is **authorized identically** to an
   unsorted read (C-SORT·4/6) — a hidden row stays hidden **regardless of
   sort**; the `CanSeeAsync` call, the `AccessAudit` row, and the
   `HiddenCount` are all **untouched** by the `SortSpec` (F6/F11).
2. **handoff:** an **unknown** `?sort=` (e.g. `?sort=rating`) falls back to
   the surface's **default order without error** (C-SORT·1, F4) — no 500,
   no reflection, no property lookup (R1).
3. **part-vs-whole:** the pager links **carry the current sort**
   (C-SORT·8, F8) — prev/next on page N with `?sort=x&dir=y` land on page
   N±1 **with the same** `?sort=x&dir=y`, across a `HasMore` window, on
   **all 18** surfaces (a per-surface test passing in isolation does not
   prove the 18-surface consistency — the gate runs the whole).

### 2.8 Drift-guard (frozen once written)

**The locked Part 2 (corrections C-1…C-3 recorded and locked here, not
drift-pauses — U2 owned the model reads):**

- **C-1** (row 14, tag→posts): the Part 1 §7 catalog said
  `OrderByDescending(Created)`; the actual code is
  **`.OrderBy(p => p.Created)` — ascending** (`TagService.cs:531`).
  C-SORT·2 (default = current order) wins: the surface's default is
  **`created`, asc**.
- **C-2** (rows 16/17, people): `Profile` **has no `Created` field**
  (confirmed in `UserInfo/Profile.cs`; the search people surface pins
  `DateTimeOffset.MinValue` for exactly this reason,
  `SearchService.cs` `PeopleAsync`). The `created` key is **removed**;
  the people allowlist is **`name` (`DisplayName`) only**, default
  `name`/asc.
- **C-3** (row 7, todos): `TodoItem` **has no `Priority` property**
  (confirmed in `Projects/TodoItem.cs`). The `priority` key is
  **removed**; the todos allowlist is
  `created` / `modified` / `title` / `due` / `status`.

**The frozen set** (any later mismatch is a **`## U<m> — Drift pause`**,
unit-series rule §6 — the handoff note is the scratch log — **not** an
implementation choice):

- The 8 invariants (C-SORT·1–8), the 12 FACES (F1–F12) — Part 1.
- The `SortSpec` / `SortKeys.Parse` shapes + the `Kumunita.Core.Query`
  namespace (§2.1) + the **no-DI-registration** pin.
- The **18 locked allowlists** incl. corrections C-1…C-3, the comparator
  rules, and the tie-breaker (§2.2).
- The **18 pinned seam lines** + the additive `SortSpec?` rule (§2.3).
- The `SortViewModel` / `_Sort` link-set shape + the `Sort` field's
  null-renders-nothing pin + the `FilterParams` carry rule (§2.4).
- The pinned test names + the file counts (§2.5: 13 Core files; §2.6:
  9 Web files) + the U17 gate's three-test shape (§2.7).
- The **C-SORT·6 milestone contract**: M25 `StatusDone` / M26
  `StatusNext` at start (**confirmed at U1**); M26 `StatusDone` + M27
  `StatusNext` at close (U18).
