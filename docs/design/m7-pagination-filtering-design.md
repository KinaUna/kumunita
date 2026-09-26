# M7 — Pagination & filtering — design (Part 1 + Part 2)

> **Milestone M7.** The one canonical paging contract (a `HasMore` signal on
> every paged Core seam, honest `Total` where a total is already computed,
> one shared `_Pager` partial, one `PagedViewModel` model) wired into
> **every** paged list surface — the 7 routes that already page in Core
> (8 pager instances) + the 2 routes that gain paging in this milestone
> (3 paged sections). **ADR 0090** is this milestone's decision record —
> **authored in this unit (U00)**, **Accepted 2026-09-26**; every decision
> D1–D10 below is **locked** (the `[PROPOSED]` markers in the lane plan
> `plan-m7-pagination-filtering.md` are the pre-lock shape and are retired
> by this lock — the ADR 0087 precedent). The sealed-unit register is
> `docs/plans-milestones/in-progress/m7/plan-m7-pagination-filtering.md`;
> the scratch log is
> `docs/plans-milestones/in-progress/m7/m7-handoff-notes.md` (one `## U#`
> section per unit, appended, never rewritten).
>
> **Status.** **LOCKED.** The decisions D1–D10 are locked in **ADR 0090
> (Accepted, 2026-09-26)**; **Part 2** (the exact C# shapes of every seam,
> the pinned test names, and the drift-guard) follows in the "Seams &
> contracts" section below.
>
> **Roadmap confirm (U00):** `M7` is the single `StatusNext` on
> `Milestones.cs` (the ADR 0086 / 0087 / 0088 / 0089 named-lane ADRs all
> record this); M7 is a **milestone**, not a named lane — the close unit
> (U05) flips `Milestones.cs` / the README Roadmap / `MilestonesTests.cs`
> (the AGENTS.md doc↔code parity contract).
>
> **Scope of this file:** what this milestone is; the existing surface it
> reuses — *verified against the actual files*; the design decisions
> (D1–D10); the invariants (C-M7·1…7); the FACES (F1–F8); the parts
> affected; and the risks. **Part 2:** the exact C# shapes of every seam,
> the pinned test names, the per-surface filter inventory (D9), and the
> drift-guard.

## 1. What this milestone is

The app already *pages in Core* — `PostService`, `EventService`, and
`ProjectService` all do `Skip((page-1)*30).Take(30)` on a per-service
`PageSize = 30` constant (verified: `PostService.cs:66`,
`EventService.cs:38`, `ProjectService.cs:39`) — but the paging surface is
**absent and dishonest** in two independent ways:

1. **The pager is absent.** Every paged feed (community feed, all-sections
   feed, group feed, events feed, group events, projects landing, to-dos,
   boards) accepts `?page=` but **no view renders prev/next**. The Web view
   models carry `CurrentPage` (M4/M5 did that) but the partial that renders
   the pager does not exist. A resident with 31 posts in "Safety" sees the
   first 30 and has **no way to the last one**.
2. **The paging signal is wrong or missing.** `FeedResult.Total` is
   documented as "total" but *assigned* the page's visible count
   (`PostService.cs` — `Total: visible.Count` in all three `ListFeed*`
   methods); the bare-list seams (`ListUpcomingAsync`, `ListTodosAsync`, …)
   return `IReadOnlyList<T>` — the caller **cannot distinguish** "30 rows,
   page 1 of 3" from "30 rows, last page". And three surfaces (the
   announcements list, tag-by-tag posts, tag-by-tag pages) load **all**
   rows at all.

M7 fixes the *contract* and ships the *one* pager UI every surface shares.
The existing filters (community picker on `/events`, assignee / unassigned /
blocked on `/projects/todos`, community picker on `/projects/boards` +
`/projects`) are **frozen as-is** (D9) — M7 does not add filters; it
guarantees they **reset to page 1 on change** (D7). Text search is **M8**.

**The one thing every unit must respect:** this milestone is **additive on
the read lane and a UI lane.** It reuses the frozen per-service `PageSize`
constants, the existing `Skip/Take` shape, and the `CanSeeAsync` audit
shape (C-M3·3) — the only Core changes are the `HasMore` signal, the
`Total` correction, and two new paged read overloads. **No new filters, no
new routes, no new documents, no new adapters, no new `AccessAction`, no
new bounded context** (D3 / D9 / C-M7·2). **No code in this unit — no
build, no tests.**

## 2. The existing surface this milestone reuses (verified)

Read directly from the actual files (not assumed), each a **frozen seam**
M7 amends additively:

1. **`FeedResult`** (`Kumunita.Core/Posts/FeedResult.cs`) —
   `(IReadOnlyList<Post> Visible, int HiddenCount, int Page, int Total)`.
   The `Total` doc-comment says "total"; the assignment says page-visible
   count — the D2 correction target. The D1 `HasMore` field lands here.
2. **`PostService`** (`Kumunita.Core/Posts/PostService.cs`) —
   `PageSize = 30` (`:66`); `ListFeedAsync(string componentId, string
   actorId, int page)`, `ListAllFeedAsync(IReadOnlyCollection<string>
   componentIds, string actorId, int page)`, `ListGroupFeedAsync(string
   groupId, string actorId, int page)` — all three `Skip/Take`-paged, all
   three `Total: visible.Count`, all three with the `candidates.Count == 0`
   early return **before** the `CanSeeAsync` call (the D8 / C-M7·5 pin).
3. **`IEventService` / `EventService`** (`Kumunita.Core/Events/`) —
   `PageSize = 30` (`EventService.cs:38`); `ListUpcomingAsync(string?
   componentId, string actorId, int page, CancellationToken)` → bare
   `IReadOnlyList<Event>` (the D3 `out bool` target);
   `ListGroupEventsAsync(string groupId, string actorId, int page,
   CancellationToken)` → `GroupEventFeedResult(Visible, HiddenCount, Page,
   Total)` (in `GroupEventDraft.cs` — the D1 field-add target).
4. **`IProjectService` / `ProjectService`** (`Kumunita.Core/Projects/`) —
   `PageSize = 30` (`:39`); the five paged bare-list seams:
   `ListTodosAsync(string? componentId, string? assigneeId, string actorId,
   int page, bool unassignedOnly = false, string? projectId = null, bool
   blockedOnly = false, CancellationToken ct = default)`,
   `ListPickerTodosAsync(string actorId, int page, CancellationToken)`,
   `ListBoardsAsync(string? componentId, string actorId, int page, string?
   projectId = null, CancellationToken)`, `ListGoalsAsync(string?
   componentId, string actorId, int page, CancellationToken)`,
   `ListProjectsAsync(string? componentId, string? goalId, string actorId,
   int page, CancellationToken)` — all the D3 `out bool` targets.
5. **`IAnnouncementService` / `AnnouncementService`**
   (`Kumunita.Core/Announcements/`) — `ListVisibleAsync(string? actorId,
   IReadOnlySet<string> roles)` → bare `IReadOnlyList<Announcement>`,
   **not paged** (loads every visible row; in-memory visibility filter,
   `OrderByDescending(Created)`; **no `AccessAudit` lane** — announcements
   are not audience-restricted — the C-M7·1 vacuous-satisfaction pin).
6. **`ITagService` / `TagService`** (`Kumunita.Core/Tags/`) —
   `ListPostsByTagAsync(string slug, string actorId)` →
   `IReadOnlyList<Post>` and `ListPagesByTagAsync(string slug, string
   actorId)` → `IReadOnlyList<Page>`, **not paged** (in-memory
   `LoadActorReadableContentAsync` filter, `OrderBy(Created)`). Two
   **different element types** — the D6 paged overloads return two
   different records (the §7.6 refinement).
7. **The 9 paged routes (verified signatures):**
   - `GET /community/{componentId}` — `PostsController.Index` →
     `ListFeedAsync`
   - `GET /community` — all-sections feed → `ListAllFeedAsync`
   - `GET /groups/{id}` — `GroupsController.Detail` → `ListGroupFeedAsync`
     **+** `ListGroupEventsAsync` (**two** paged sections, one route)
   - `GET /events` — `EventController.Index(string? componentId, int page
     = 1)` → `ListUpcomingAsync`
   - `GET /projects` — `ProjectsController.ProjectsIndex(string?
     componentId, int page = 1)` → `ListGoalsAsync` **+**
     `ListProjectsAsync` (**two** paged sections, one route)
   - `GET /projects/todos` — `TodosIndex(string? componentId, string?
     assigneeId, bool unassignedOnly = false, bool blockedOnly = false, int
     page = 1)` → `ListTodosAsync`
   - `GET /projects/boards` — `BoardsIndex(string? componentId, int page
     = 1)` → `ListBoardsAsync`
   - **Newly paged:** `GET /announcements` — `Index()` → (U01)
     `ListVisiblePagedAsync`; `GET /tags/{slug}` — `ByTag(string slug)` →
     (U01) `ListPostsByTagPagedAsync` **+** `ListPagesByTagPagedAsync`
     (**two** paged sections, one route)
8. **`KnownTranslationKeys.cs`** (`Kumunita.Core.Localization`) — the
   `kw-l` registry (ADR 0015) with the four seeded languages
   (`en` / `de` / `fr` — ADR 0042; `da` — ADR 0045, disabled by default) +
   the ADR 0052 warm-boot baseline backfill that covers new keys'
   non-`en` rows automatically. The two `pagination.*` keys are additive
   on it; `KnownTranslationKeys_ParityTests` covers them once registered.
9. **`PostgresFixture`** (`tests/Kumunita.Core.Tests`, Testcontainers
   `postgres:18`) — the harness the Core pinned tests run over
   (`PostServiceTests` shape); `Kumunita.Web.Tests` uses NSubstitute (no
   Postgres) for the Web pins (`EventControllerTests` shape).

## 3. The design decisions (locked)

Locked by **ADR 0090 (Accepted, 2026-09-26)**; the `[PROPOSED]` markers in
the lane plan are retired by this lock.

### 3.1 The contract is signal-based, not count-based (D1)

Every paged seam returns a **`bool HasMore`** alongside the rows (or already
does, via a `FeedResult`-shaped record). `HasMore = true` iff the page's
*candidate* set filled the page (`candidates.Count == PageSize`) — a full
page means "more might exist", a partial page means "this is the last one".
The Web computes `HasNextPage` from `HasMore` alone; it **never divides
`Total` by `PageSize`**. **Rationale:** the candidate set is audited
*after* paging (one `CanSeeAsync` per page-visit, C-M3·3), so a *visible*
total is unknowable without auditing the whole set — and auditing the whole
set to compute a page count would leak "how many hidden posts exist in this
component" through the audit lane (C-M3·2 / C5). `HasMore` is the only
honest signal. *(C-M7·4.)*

### 3.2 `FeedResult.Total` is corrected to the candidate count (D2)

The field's doc-comment says "total"; the assignment says page-visible
count. U01 corrects the assignment to the **candidate-set count** — the
pre-decision count, one `CountAsync` over the same filtered query **before**
the `Skip/Take` — and updates the doc-comment to
"candidate-set count (pre-decision, C-M7·7); never a viewer-facing total
(D1)". The two views that render `Total` as a viewer-facing count
(`Posts/Index.cshtml` "N posts in this community; M shown to you" and
`Groups/Detail.cshtml`, the same phrasing for the group posts and group
events sections) are fixed in U03 to **stop** doing so: the `HiddenCount`
field already signals hidden *existence*; the *count* of hidden posts is
the leak, and it goes. **Rationale:** a *second* field next to a *wrong*
field is a trap for the next agent; one correct field + the view fix is not.
A viewer-facing "total posts in this community" number would require
auditing the whole component (the D1 leak), so the only honest move is to
stop showing one. *(C-M7·7.)*

> **U00 lock note (the (A) refinement).** The lane plan's pinned test #4
> body and the D2/F8/C-M7·7 text disagreed on this field's value (test
> body: `Total: 30, "the page's 30"`; D2 + F8 + C-M7·7: `Total: 31`, the
> component's candidate count). The (A) reading — **component candidate
> count, `Total = 31`** — is locked: it is the majority of the plan's own
> text (D2 + F8 + C-M7·7 + the test's own *name*), it is what C-M7·7
> defines ("post paging-bounds **excluded**"), and its security profile is
> the narrower one under the C-M7·7 rendering ban (it is the *correct*
> pre-decision diagnostic field; (B) would have left a *wrong-as-a-count*
> field under the same ban). The test #4 body is corrected accordingly —
> recorded in the § drift log.

### 3.3 The bare-list seams get `out bool hasMore` (D3)

`ListUpcomingAsync`, `ListTodosAsync`, `ListPickerTodosAsync`,
`ListBoardsAsync`, `ListGoalsAsync`, `ListProjectsAsync` all gain
`out bool hasMore` as the **last** parameter (before
`CancellationToken`). The record-shaped seams (`FeedResult`,
`GroupEventFeedResult`) get a `HasMore` **field**, not an `out` (records
don't do `out`). **Rationale:** a new `PagedResult<T>` record per surface
is seven new types for one `bool`; the `out` parameter is the .NET idiom
for "one extra signal" and keeps every call site a one-token edit.

### 3.4 Page size stays 30, stays per-service (D4)

The `PageSize = 30` constants in `PostService`, `EventService`,
`ProjectService` are the contract; U01 does **not** unify them into a
shared constant (that is a refactor, not a lane). The two newly-paged
services (`AnnouncementService`, `TagService`) adopt the same
`PageSize = 30` per-service shape. The `_Pager` partial takes `PageSize` as
a view-model input (the controller passes the service's constant) so the
partial is surface-agnostic. *(C-M7·3.)*

### 3.5 One `_Pager` partial, one `PagedViewModel` (D5)

The pager is a Bootstrap-5 `pagination` list: **prev / next only** (no
page-number window — a neighborhood doesn't need "page 7 of 40"). The
labels are localized via the `kw-l` registry — two new keys × the four
seeded languages — with the locked `en` strings: **`pagination.prev` =
"Newer"** (toward the newer rows — the feeds are newest-first /
earliest-start-first, so *Prev* steps back toward the head) and
**`pagination.next` = "Older"** (toward the older rows). The pager's links
carry the *current* filter values as query pairs (the D7 hidden-input
idiom). `PagedViewModel` is a small record (the exact C# in §7.7) with a
`ForRoute` factory. **No `TotalPages`** — D1 forbids computing it. The
partial renders **nothing** when `!HasPrevious && !HasNext` (the one-page
surface shows no pager — the F2 pin).

### 3.6 The growth lists page; the bounded lists don't (D6)

**Paged (U01 + U04):** `ListVisibleAsync` (announcements — a site with
years of pinned + community announcements can exceed 30),
`ListPostsByTagAsync` and `ListPagesByTagAsync` (a tag like "garden" can
accumulate). **Not paged (explicitly):** `ListMyDraftsAsync` (an author's
drafts are bounded by the author's patience — a long draft list is a
*curation* problem, not a paging one), `GetTreeAsync` (the page tree is a
tree, not a feed), `GetComponentsAsync` (four to ten rows, by
construction), `GetProfilesAsync` (the directory is M2's surface — if it
ever needs paging, that is a dedicated lane), the RSVP list
(`GetRsvpsAsync` — bounded by the event's audience), the membership list
(`GetGroupMembersAsync` — same), the notification inbox (M6's surface), the
home page's truncated feed (`FeedRowsPerColumn` — a *truncation*, not a
paged feed). **Rationale:** paging every list is a *ceremony* tax (the
platform's principles say "boring where it can be"); paging the *growth*
lists is the lane. *(C-M7·4.)*

### 3.7 Filters reset to page 1; the pager preserves them (D7)

Every existing filter form (`/events` community picker; `/projects/todos`
component / assignee / unassigned / blocked pickers; `/projects/boards` +
`/projects` community pickers) submits with **no `page` param** (the
controller floors to 1 — the existing `if (page < 1) page = 1;` shape in
every service). The `_Pager` partial, by contrast, carries the *current*
filter values in its links so prev/next don't drop them. **Rationale:**
"I changed the filter and landed on an empty page 3" is the classic pager
bug; the two rules together are the whole fix. *(C-M7·6.)*

### 3.8 An oversized page is empty, no audit row (D8)

`?page=99` returns **zero rows** and **no `CanSeeAsync` call** — the
existing early-return shape in `PostService.ListFeedAsync` (and the
`ListUpcomingAsync` / `ListGroupEventsAsync` / `ProjectService` analogs)
runs **before** any decision and **before** the D2 `CountAsync`, so it
emits `Total: 0, HasMore: false`. **Rationale:** auditing an empty page
would emit an aggregate row with `visibleCount = 0, hiddenCount = 0` that
names a component the viewer can see — a zero-information audit row is
noise, and the C3 "one row per decision" pin is cleaner when *no decision
ran*. *(C-M7·5.)*

> **U00 lock note (the test #3 body).** The lane plan's test #3 body said
> "Total: candidateCount" for the empty page; D8 + C-M7·5 (and the early
> return running before the `CountAsync`) pin **`Total: 0`** — the D8
> shape is locked, the test body corrected. Recorded in the § drift log.

### 3.9 No new filters in M7 (D9)

The filter set is **frozen** at what exists per surface (the inventory in
§8 is the locked text). "Pagination and filtering" as a milestone name
refers to *making the existing filters paginate correctly* (D7) — adding
new filters (by-author, by-tag on the community feed, date-range) is scope
creep that belongs to M8 (search) or a dedicated lane. The by-tag browse
*already exists* (ADR 0044); M7 just pages it.

### 3.10 The test home is split by seam (D10)

Core-seam tests (the `HasMore` signal, the `Total` correction, the
empty-page pin) go in `Kumunita.Core.Tests` (the `PostgresFixture` harness
— the M3 `PostServiceTests` shape). Web-shape tests (the `PagedViewModel`
factory, the wiring, the filter-reset rule) go in `Kumunita.Web.Tests` (the
NSubstitute controller-harness shape — the M4 `EventControllerTests`
precedent). **No e2e** (the Playwright harness is for editor/JS surfaces;
a pager is server-rendered markup, covered by the view-model + partial
tests).

## 4. Invariants (pinned for M7)

- **C-M7·1 — One page, one aggregate audit row.** A paged visit emits
  exactly the same audit shape as an unpaged one: one `CanSeeAsync` over
  the *page's* candidate set, one aggregate row (C-M3·3). Paging narrows
  the *candidate set before the decision pass*, never the visible set
  after it. (Extends C-M3·3 to paged visits. Vacuously satisfied for the
  two audit-lane-less surfaces — announcements, tags — which emit no row
  at all.)
- **C-M7·2 — Paging is display-only, never an access input.** A page
  number never appears in an `IAuthorizationService` call, an `Audience`
  evaluation, or an `AccessAudit` row's identity. The `?page=` query is a
  *display* parameter (the ADR 0064 `?view=` display-selector precedent,
  C-DWM·3).
- **C-M7·3 — Page size is a per-service constant, 30.** Not
  configuration, not user-selectable, not a query parameter. Changing it
  is a code change + a design-doc note, not a deploy.
- **C-M7·4 — `HasMore` is the sole paging signal.** No `TotalPages`, no
  `Count()` over the visible set, no client-side "is this the last page?"
  inference. A surface that can't compute `HasMore` is *not paged* (the
  D6 not-paged list is the pin).
- **C-M7·5 — An empty page is a no-decision.** `?page=N` beyond the last
  page returns zero rows, `HasMore: false`, `Total: 0`, and **no**
  `CanSeeAsync` call (D8). The existing early-return shape is the pin.
- **C-M7·6 — A filter change resets to page 1; a pager step preserves the
  filter.** (D7.) The two rules together are the filter/paging contract;
  a surface that violates either is a Web-contract break.
- **C-M7·7 — The candidate count (`Total`, D2) is pre-decision.** It
  counts the rows the *query* matched (post filter, **post paging-bounds
  excluded** — the component's candidate count, e.g. 31), not the rows the
  *decision* allowed and not the page's window (30). It is a
  diagnostic/operational number, not a viewer-facing "X posts" count — the
  view never renders `Total` as a count (U03's view-text removals are the
  pin).

## 5. FACES (pinned, 8)

- **F1** a page whose candidate set filled 30 rows renders a *Next* link
  (and a *Prev* link iff `page > 1`) — C-M7·4, C-M7·6
- **F2** a page whose candidate set was < 30 rows renders *Prev* only (iff
  `page > 1`) — no Next, no "page 2 of N" (there is no N — D1); a one-page
  surface renders **no pager at all** — C-M7·4
- **F3** a paged feed with an active filter (community, assignee, …) keeps
  the filter across prev/next (the `_Pager`'s link query pairs) — C-M7·6
- **F4** submitting the filter form resets `page` to 1 (or omits it — the
  controller floors) — C-M7·6
- **F5** `?page=99` → zero rows, no `AccessAudit` row, `HasMore: false`,
  `Total: 0` — C-M7·5
- **F6** a site with 31+ visible announcements shows 30 + a Next link; the
  31st is reachable — D6, C-M7·1
- **F7** a tag with 31+ visible posts / pages shows 30 + a Next link (both
  sections on the one `/tags/{slug}` page) — D6, C-M7·1
- **F8** a component with 31 posts (10 hidden by the decision): page 1's
  30-row window holds 20 visible + 10 hidden; the visit reports `Total =
  31` (the component's candidate count, post-filter, pre-decision — not
  the page's 30), `Visible.Count = 20`, `HiddenCount = 10`,
  `HasMore = true` — D2, C-M7·7

## 6. Parts affected

- **`Kumunita.Core`** (U01): `FeedResult` (+`HasMore`, corrected `Total`
  comment); `PostService.ListFeed*` ×3 (candidate count + `HasMore` + the
  empty-page pin); `GroupEventFeedResult` (+`HasMore`);
  `EventService.ListUpcomingAsync` (`out bool hasMore`);
  `IEventService` (signature); `ProjectService.List*Async` ×5
  (`out bool hasMore`); `IProjectService` (signatures);
  `AnnouncementService.ListVisiblePagedAsync` + `AnnouncementPage` (new);
  `IAnnouncementService` (new seam); `TagService.ListPostsByTagPagedAsync`
  + `ListPagesByTagPagedAsync` + `TagPostPage` / `TagPagePage` (new);
  `ITagService` (new seams). **No document changes, no schema changes.**
- **The U01 call-site compile pass** (§7.2a, locked): the one-token `out`
  fix at the ~5 Web-controller call sites (`EventController.Index`,
  `ProjectsController` ×5 reads) + the ~20 existing-test call sites
  (`EventServiceTests` / `ProjectServiceTests` real-service calls;
  `EventControllerTests` / `ProjectsControllerTests` NSubstitute setups +
  `Received` assertions). Mechanical, no behavior change; the **only**
  Web/test touch in U01 (the pager wiring + D2 view-text stay in U03/U04).
- **`Kumunita.Web`** (U02–U04): `Models/PagedViewModel.cs` (new);
  `Views/Shared/_Pager.cshtml` (new); `KnownTranslationKeys.cs` (the two
  `pagination.*` keys × 4 languages); the 7 existing routes' VMs /
  controllers / views (the `_Pager` drop-in, 8 instances) + the 2
  newly-paged routes' (3 sections); the two D2 view-text removals
  (`Posts/Index.cshtml`, `Groups/Detail.cshtml`); the D7 filter-form
  verification on the 5 filter forms.
- **The test files** — `tests/Kumunita.Core.Tests/M7PaginationSeamTests.cs`
  (new, 12 tests — U01); `tests/Kumunita.Web.Tests/PagedViewModelTests.cs`
  (new, 4 tests — U02); `tests/Kumunita.Web.Tests/M7PagerWiringTests.cs`
  (new, 3 tests — U03); `tests/Kumunita.Web.Tests/M7NewlyPagedTests.cs`
  (new, 3 tests — U04).
- **The close (U05):** README Roadmap + `Milestones.cs` + `MilestonesTests.cs`
  flip; the `ARCHITECTURE.md` "shape of the code" note.

## 7. Part 2 — Seams & contracts (exact shapes)

### 7.1 `FeedResult` (D1 + D2, U01)

```csharp
public sealed record FeedResult(
    IReadOnlyList<Post> Visible,
    int HiddenCount,
    int Page,
    int Total,                 // candidate-set count (pre-decision, C-M7·7); never a viewer-facing total (D1)
    bool HasMore);             // D1 — the sole paging signal: candidates.Count == PageSize
```

### 7.2 `PostService.ListFeedAsync` / `ListAllFeedAsync` /
`ListGroupFeedAsync` (U01 — all three, the same shape)

In each method, **before** the `Skip/Take`:

```csharp
int candidateCount = await session
    .Query<Post>()
    .Where(<the same filter expression>)      // C-M7·7 — pre-decision, post-filter
    .CountAsync()
    .ConfigureAwait(false);
```

The `candidates.Count == 0` early return (C-M7·5 — runs before
`CanSeeAsync` **and** before the `CountAsync`) becomes:

```csharp
return new FeedResult(Visible: Array.Empty<Post>(), HiddenCount: 0, Page: page, Total: 0, HasMore: false);
```

The normal return becomes `Total: candidateCount, HasMore: candidates.Count == PageSize`.
**No other change** to any of the three methods — the `CanSeeAsync` call,
the visible-set filter, and the audit row are untouched (C-M7·1).

### 7.2a Call-site compile pass (U01, locked)

The `out`-parameter change (D3) is a **source-level break** for every
existing call site of the seven `out`-gained methods, and the U01 exit is
`dotnet build Kumunita.slnx -c Debug` green (the **whole** solution — the
plan's per-unit exit). The two clauses only both hold if U01 also adds the
one-token `out _` (or `out var hasMore`) fix at each existing call site.
**Locked:** U01's deliverables include this compile pass — the **only**
permitted Web/test touch in U01, and it is mechanical (one token per
line, no behavior change, no other file):

- **Web controllers (5 call sites, 2 files):** `EventController.Index`
  (`ListUpcomingAsync`); `ProjectsController` — `TodosIndex`
  (`ListTodosAsync` ×3: the feed, the `page: 1` parent-candidate and
  picker reads), `BoardsIndex` (`ListBoardsAsync`), `ProjectsIndex`
  (`ListGoalsAsync` + `ListProjectsAsync`), the `ListPickerTodosAsync`
  picker read.
- **Existing Core tests (the real-service call sites, ~10 lines):**
  `tests/Kumunita.Core.Tests/EventServiceTests.cs` (`ListUpcomingAsync`
  call sites), `tests/Kumunita.Core.Tests/ProjectServiceTests.cs`
  (`ListTodosAsync` / `ListGoalsAsync` / `ListProjectsAsync` call sites).
- **Existing Web tests (the NSubstitute setups + `Received`
  assertions, ~10 lines):** `tests/Kumunita.Web.Tests/
  EventControllerTests.cs` (`ListUpcomingAsync` setups),
  `tests/Kumunita.Web.Tests/ProjectsControllerTests.cs`
  (`ListTodosAsync` / `ListBoardsAsync` / `ListProjectsAsync` /
  `ListPickerTodosAsync` setups + `Received` assertions).

The authoritative break set is whatever `dotnet build Kumunita.slnx`
reports (the compiler is the pin — the list above is the U00-verified
shape). **No** other Web or test change in U01: the pager wiring (U03),
the newly-paged wiring (U04), and the D2 view-text removals stay in
their units. **Not broken** (verified): no test constructs `FeedResult`
/ `GroupEventFeedResult` (`new` / deconstruction — only property reads),
`PostService.ListFeed*` is called only through `FeedResult`-shaped
results (its signature is unchanged — only the record gained a field),
and the new overloads (`ListVisiblePagedAsync` / the tag paged seams)
have no existing callers.

### 7.3 `IEventService.ListUpcomingAsync` (D3, U01)

```csharp
Task<IReadOnlyList<Event>> ListUpcomingAsync(
    string? componentId, string actorId, int page,
    out bool hasMore,                       // D1/D3 — candidates.Count == PageSize
    CancellationToken ct = default);
```

`EventService.ListUpcomingAsync` implements it: `hasMore =
candidates.Count == PageSize` (the page's candidate list — the D1 pin); the
`candidates.Count == 0` early return sets `hasMore = false` (C-M7·5).

### 7.4 `GroupEventFeedResult` (D1 field-add, U01)

```csharp
public sealed record GroupEventFeedResult(
    IReadOnlyList<Event> Visible,
    int HiddenCount,
    int Page,
    int Total,
    bool HasMore);     // D1 — Allow: candidates.Count == PageSize; Deny: false
```

`ListGroupEventsAsync`'s signature is **unchanged** — the record gained a
field, not the method. The Allow branch assigns
`HasMore: candidates.Count == PageSize`; the Deny branch and the
0-candidate branch assign `HasMore: false`.

### 7.5 `IProjectService` — the five `out bool` seams (D3, U01)

```csharp
Task<IReadOnlyList<TodoItem>> ListTodosAsync(
    string? componentId, string? assigneeId, string actorId, int page,
    bool unassignedOnly = false, string? projectId = null, bool blockedOnly = false,
    out bool hasMore,                       // D1/D3 — candidates.Count == PageSize
    CancellationToken ct = default);

Task<IReadOnlyList<TodoItem>> ListPickerTodosAsync(
    string actorId, int page,
    out bool hasMore,
    CancellationToken ct = default);

Task<IReadOnlyList<KanbanBoard>> ListBoardsAsync(
    string? componentId, string actorId, int page, string? projectId = null,
    out bool hasMore,
    CancellationToken ct = default);

Task<IReadOnlyList<ProjectGoal>> ListGoalsAsync(
    string? componentId, string actorId, int page,
    out bool hasMore,
    CancellationToken ct = default);

Task<IReadOnlyList<Project>> ListProjectsAsync(
    string? componentId, string? goalId, string actorId, int page,
    out bool hasMore,
    CancellationToken ct = default);
```

`ProjectService` implements each: `hasMore = candidates.Count == PageSize`
(the method's pre-`CanSeeAsync` page list); the 0-candidate branch sets
`hasMore = false`.

### 7.6 The two newly-paged seams (D6, U01)

**Announcements** — a new seam **alongside** the existing `ListVisibleAsync`
(the banner + admin surfaces keep the non-paged read):

```csharp
public sealed record AnnouncementPage(
    IReadOnlyList<Announcement> Items,
    bool HasMore);

Task<AnnouncementPage> ListVisiblePagedAsync(
    string? actorId, IReadOnlySet<string> roles, int page,
    CancellationToken ct = default);
```

`AnnouncementService` adopts a `PageSize = 30` constant (the D4 shape);
`ListVisiblePagedAsync` applies the **same** in-memory visibility filter
as `ListVisibleAsync`, then `Skip((page - 1) * PageSize).Take(PageSize)`
over the ordered candidate list; `HasMore = candidates.Count == PageSize`.
**No audit row** (announcements have no audit lane — the existing
`ListVisibleAsync` doc-comment pin; C-M7·1 vacuously satisfied).

**Tags** — two new seams, **two records** (the element types differ —
`Post` vs `Page`):

```csharp
public sealed record TagPostPage(IReadOnlyList<Post> Items, bool HasMore);
public sealed record TagPagePage(IReadOnlyList<Page> Items, bool HasMore);

Task<TagPostPage> ListPostsByTagPagedAsync(
    string slug, string actorId, int page, CancellationToken ct = default);

Task<TagPagePage> ListPagesByTagPagedAsync(
    string slug, string actorId, int page, CancellationToken ct = default);
```

`TagService` adopts a `PageSize = 30` constant (the D4 shape); each
overload applies the same in-memory readable-content filter as the
existing seam, then `Skip/Take` over the ordered candidate list;
`HasMore = candidates.Count == PageSize`. The existing
`ListPostsByTagAsync` / `ListPagesByTagAsync` are **unmodified** (the
non-paged call sites keep working; the paged overloads are the new lane).

> **U00 lock note (the two tag records).** The lane plan's § Seams first
> named a single `TagPage(IReadOnlyList<object> Items, bool HasMore)` and
> then self-corrected to two records (`TagPostPage` / `TagPagePage`)
> because the two methods return different element types. The **two-record
> shape is the lock** (a single `object`-element record would erase the
> element type at every call site); the design doc pins the two records
> from the start — no U01 drift pause is needed on this point.

### 7.7 `PagedViewModel` (D5, U02 — `Kumunita.Web/Models/`)

```csharp
/// <summary>
/// The one shared pager view model (ADR 0090 D5). <see cref="HasPrevious"/>
/// = <c>page &gt; 1</c>; <see cref="HasNext"/> = the seam's <c>HasMore</c>
/// (D1 — the sole paging signal; there is no <c>TotalPages</c>).
/// <see cref="FilterParams"/> are the current filter values the pager's
/// links carry as query pairs (D7 — the pager preserves the filter);
/// empty by default. A null of this type on a section's VM field means
/// "one page" — the partial renders nothing (F2).
/// </summary>
public sealed record PagedViewModel(
    int CurrentPage,
    int PageSize,
    bool HasPrevious,
    bool HasNext,
    string BaseUrl,                                        // the route, no query
    IReadOnlyDictionary<string, string> FilterParams);

public static PagedViewModel ForRoute(
    string baseUrl, int page, int pageSize, bool hasMore,
    IReadOnlyDictionary<string, string>? filterParams = null)
    => new(
        CurrentPage: page,
        PageSize: pageSize,
        HasPrevious: page > 1,
        HasNext: hasMore,
        BaseUrl: baseUrl,
        FilterParams: filterParams ?? new Dictionary<string, string>());
```

`_Pager.cshtml` (U02, `Views/Shared/`): `@model PagedViewModel`; renders
**nothing** when `!HasPrevious && !HasNext` (F2); otherwise a
`<nav aria-label="pagination">` with a Bootstrap-5 `ul.pagination` — a
*Prev* `<li>` (disabled iff `!HasPrevious`) linking
`BaseUrl?page=CurrentPage-1` + the `FilterParams` as `?key=value` pairs,
and a *Next* `<li>` (disabled iff `!HasNext`) linking
`BaseUrl?page=CurrentPage+1` + the same pairs. Labels: `<kw-l
key="pagination.prev">` / `<kw-l key="pagination.next">`. Plain `href`
links (GET navigation — the server-rendered idiom; no JS).

### 7.8 The `kw-l` keys (U02)

Two new keys × the four seeded languages (`en` / `de` / `fr` / `da`):
**`pagination.prev` = "Newer"**, **`pagination.next` = "Older"** (the feeds
are newest-first / earliest-start-first — *Prev* steps toward the head of
the list, *Next* toward the tail). The ADR 0052 warm-boot baseline
backfill covers the non-`en` rows. `KnownTranslationKeys_ParityTests`
still passes once registered.

## 8. Per-surface filter inventory (D9, frozen)

Every paged surface in the app, one row each. "Filter params" = the query
params the surface's **filter form** submits (all frozen — D9); the `_Pager`
links carry exactly those (F3); a filter submission carries **no** `page`
(F4, D7).

| Surface | Route | Filter params (frozen) | D7 application |
|---|---|---|---|
| Community feed | `GET /community/{componentId}` | — (the component is the route) | no filter form; pager links carry `?page=N` only |
| All-sections feed | `GET /community` | — | no filter form |
| Group posts (section) | `GET /groups/{id}` | — (the group is the route) | no filter form; pager section-scoped (`PagerPosts`) |
| Group events (section) | `GET /groups/{id}` | — | no filter form; pager section-scoped (`PagerEvents`) |
| Events feed | `GET /events` | `componentId` | form submits without `page`; `_Pager` carries `componentId` |
| Projects landing (goals section) | `GET /projects` | `componentId` | form submits without `page`; `_Pager` section-scoped (`PagerGoals`) carries `componentId` |
| Projects landing (projects section) | `GET /projects` | `componentId` | same; `_Pager` section-scoped (`PagerProjects`) |
| To-dos feed | `GET /projects/todos` | `componentId`, `assigneeId`, `unassignedOnly`, `blockedOnly` | form submits without `page`; `_Pager` carries all four |
| Boards feed | `GET /projects/boards` | `componentId` | form submits without `page`; `_Pager` carries `componentId` |
| Announcements list (newly paged) | `GET /announcements` | — (no filter form) | no filter form; pager links carry `?page=N` only |
| Tag-by-tag posts (section, newly paged) | `GET /tags/{slug}` | — (the tag is the route) | no filter form; pager section-scoped (`PagerPosts`) |
| Tag-by-tag pages (section, newly paged) | `GET /tags/{slug}` | — | no filter form; pager section-scoped (`PagerPages`) |

**Not paged (D6, frozen as unpaged):** the home page feed
(`GET /`, `FeedRowsPerColumn` truncation — a truncation, not a paged
feed), `/my/drafts` (`ListMyDraftsAsync`), the page tree
(`GetTreeAsync`), the components list (`GetComponentsAsync`), the
directory (`GetProfilesAsync`), an event's RSVPs (`GetRsvpsAsync`), a
group's members (`GetGroupMembersAsync`), the notification inbox (M6's
surface). If any of these ever needs paging, that is a dedicated lane —
not this one.

## 9. Pinned seam tests (exact names)

**`tests/Kumunita.Core.Tests/M7PaginationSeamTests.cs`** (U01, 12 tests):

1. `F1_FullPage_HasMoreTrue` — a component with 31 visible posts:
   `ListFeedAsync(page: 1)` returns `HasMore: true`, `Total: 31`.
2. `F2_PartialPage_HasMoreFalse` — a component with 5 posts:
   `ListFeedAsync(page: 1)` returns `HasMore: false`, `Total: 5`.
3. `F5_OversizedPage_EmptyAndNoAuditRow` — `ListFeedAsync(page: 99)`
   returns `Visible.Count: 0, HasMore: false, Total: 0` and **zero**
   `AccessAudit` rows (C-M7·5 — the early return ran before
   `CanSeeAsync` **and** before the `CountAsync`).
4. `F8_TotalIsCandidateCount_NotPageCount` — 31 posts, 10 hidden:
   page 1 returns `Total: 31` (the component's candidate count — the
   page's window holds 30), `Visible.Count: 20`, `HiddenCount: 10`,
   `HasMore: true` (D2 / the (A) lock, C-M7·7).
5. `C_M7_1_OneAggregateRowPerPageVisit` — 31 posts: page 1 **and** page 2
   each emit exactly **one** `AccessAudit` row (TargetKind `"post"`);
   page 2's row names the same component.
6. `C_M7_5_ListUpcomingAsync_OversizedPage_NoAuditRow` —
   `ListUpcomingAsync(page: 99)` shape: zero rows, `hasMore: false`,
   zero `AccessAudit` rows.
7. `ListUpcomingAsync_FullPage_HasMoreTrue` — 31 upcoming events:
   page 1 → 30 rows, `hasMore: true`.
8. `ListUpcomingAsync_PartialPage_HasMoreFalse` — 5 upcoming events:
   page 1 → 5 rows, `hasMore: false`.
9. `ListTodosAsync_FullPage_HasMoreTrue` — 31 visible to-dos:
   page 1 → 30 rows, `hasMore: true`.
10. `ListTodosAsync_OversizedPage_HasMoreFalseAndEmpty` — page 99 →
    zero rows, `hasMore: false`.
11. `ListVisiblePagedAsync_Announcements_Page1_Full_HasMoreTrue` — 31
    visible announcements: page 1 → 30 items, `HasMore: true`; page 2 →
    1 item, `HasMore: false`.
12. `ListPostsByTagPagedAsync_FullPage_HasMoreTrue` — a tag with 31
    visible posts: page 1 → 30 items, `HasMore: true`; page 2 → 1 item,
    `HasMore: false`.

**`tests/Kumunita.Web.Tests/PagedViewModelTests.cs`** (U02, 4 tests):
`ForRoute_Page1_Full_HasNextTrue_HasPrevFalse`,
`ForRoute_Page2_Partial_HasNextFalse_HasPrevTrue`,
`ForRoute_FilterParams_Preserved`, `ForRoute_Page1_NotFull_RendersNothing`
— the exact bodies in the lane plan's U02 deliverables.

**`tests/Kumunita.Web.Tests/M7PagerWiringTests.cs`** (U03, 3 tests):
`PostsFeed_Page2_HasMore_PagerPresent`,
`PostsFeed_Page1_Partial_PagerAbsent`,
`EventsFeed_FilterParam_PreservedInPager` — the exact bodies in the lane
plan's U03 deliverables.

**`tests/Kumunita.Web.Tests/M7NewlyPagedTests.cs`** (U04, 3 tests):
`F6_AnnouncementsList_Page2_HasMore_PagerPresent`,
`F7_TagByTag_Page1_Full_BothSectionsPaged`,
`F4_FilterForm_SubmitsWithoutPage` — the exact bodies in the lane plan's
U04 deliverables.

**No other test names exist for M7** (unit-series rule 3).

## 10. Risks & mitigations

- **The `out` parameter is a source-level break for every call site.**
  Every existing caller of the seven `out`-gained methods must add the
  `out` token (U01's compile pass finds them; the Web call sites are the
  U03/U04 wiring — the plan's sequencing puts U01's Core change and the
  Web fixes in the same build). Mitigation: the build gate per unit;
  there are no *other* consumers (Kumunita.Web is the sole Web
  consumer).
- **A `CountAsync` per feed visit** (D2) is one extra query per page on
  the three `ListFeed*` methods. At one-neighborhood scale this is
  negligible (an indexed count over the same filtered set); the
  alternative — re-deriving `Total` from the visible set — is the
  original bug.
- **The D2 leak** (a viewer-facing total = the hidden-count leak) is
  closed by the U03 view-text removals + the C-M7·7 rendering ban; the
  field stays as an honest *diagnostic* number.
- **A stale pager on a filter change** (the classic "empty page 3" bug)
  is closed by the D7 two-rule contract (F4 reset + F3 preserve) and
  pinned by the U04 `F4_FilterForm_SubmitsWithoutPage` test + the
  `name="page"` grep on the 5 filter forms.

## M7 — Drift log

A unit that finds this doc out of date appends a `## U<m> — Drift pause`
line here and stops (unit-series rule 6).

- **U00 — 2026-09-26.** Two lane-plan test bodies locked against the
  invariant text (both in the (A) / D8 direction the majority of the plan
  supports): (1) test #4 body — `Total: 30, "the page's 30"` →
  **`Total: 31`** (the component's candidate count — D2 + F8 + C-M7·7 +
  the test's own name all agree; the (B) page-local reading was rejected
  on its security profile — a *wrong-as-a-count* field under the C-M7·7
  rendering ban — and its cost of re-opening D2/F8/C-M7·7); (2) test #3
  body — `Total: candidateCount` → **`Total: 0`** (D8's early return runs
  before the `CountAsync` — the C-M7·5 pin). Also locked: the two tag
  page records (`TagPostPage` / `TagPagePage`) over the single `TagPage`
  the plan's § Seams first named (the two element types make the single
  record wrong). **(5)** Locked: the U01 deliverables include the
  **call-site compile pass** (new §7.2a) — the D3 `out bool` change is a
  source-level break at the ~5 Web-controller call sites + ~20
  existing-test call sites, so U01's "whole-solution build green" exit and
  "no other Web/test change in U01" only both hold with the one-token
  `out` fix at each. The compiler (`dotnet build Kumunita.slnx`) is the
  authoritative break set; the §7.2a list is the U00-verified shape. The
  lane plan text is the pre-lock shape; this doc + ADR 0090 are the lock.
- **U01 — 2026-09-26. (CS1988 — the §7.5 pin is C#-illegal on `async`
  seams; resolved by the page-record pivot.)** The §7.5 / D3 pin of
  **`out bool hasMore`** on the six async list seams is **CS1988-illegal**:
  C# forbids `ref` / `in` / `out` parameters on `async` methods, so a seam
  `Task<IReadOnlyList<T>> ListXAsync(..., out bool hasMore)` cannot compile
  as-is. This is a hard language rule, not a project choice — the only
  C#-legal + idiomatic way to carry a second return value off an `async`
  seam is a **record return**. U01 therefore pivots every paged seam to a
  **page-record return** `(Items, HasMore)`, consistent with the pre-existing
  `FeedResult` / `GroupEventFeedResult` / `AnnouncementPage` / `TagPostPage`
  / `TagPagePage` conventions (the `FeedResult.HasMore` "sole paging signal"
  shape D1 already pins). New records introduced by U01: `EventPage`
  (`Events`), and `TodoPage` / `BoardPage` / `GoalPage` / `ProjectPage`
  (`Projects`). The D1 / D2 / D3 / D8 semantics are **unchanged** — only the
  *vehicle* for the signal moved from an `out` parameter to a record field
  (`HasMore`). `out bool` is also NSubstitute-hostile (by-ref is unsupported
  upstream, issue #992); the record return is mockable cleanly (a plain value
  type) — so the Web NSubstitute test suite needed only a one-line
  `.Returns(new TodoPage(...))` wrap per seam, not a new mocking path. This
  is the *sole* U01 deviation from the §7.5 literal seam signatures; it is a
  compiler-forced shape change, not a semantic one — `Total`, `HasMore`, the
  D8 early-return no-decision, and the C3 audit-row pins all hold verbatim
  (the 12 pinned seam tests pass).
