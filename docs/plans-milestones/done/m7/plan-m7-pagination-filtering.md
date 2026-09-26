# M7 — Pagination & filtering (sealed unit register)

> **In progress.** This is the **lane plan** (the secondary register tier) for
> milestone **M7** — pagination and filtering across the resident-facing
> list surfaces. The **primary reference tier** is the design doc
> `docs/design/m7-pagination-filtering-design.md` (authored **U00**; it locks
> the paging contract and the [PROPOSED] decisions into **ADR 0090**); the
> **scratch tier** is `docs/plans-milestones/in-progress/m7/m7-handoff-notes.md`
> (one appended `## U#` section per unit, never rewritten).
>
> **What this is:** the app already *pages in Core* — `PostService`,
> `EventService`, `ProjectService` all do `Skip((page-1)*30).Take(30)` —
> but **no surface shows a pager** (`?page=` is accepted, never offered),
> **`FeedResult.Total` is a lie** (it returns the *page's* count, not the
> candidate-set total — `PostService.cs` line 120:
> `return new FeedResult(..., Total: visible.Count)`), and **three list
> surfaces are unpaged at all** (announcements list, tag-by-tag posts,
> tag-by-tag pages). M7 ships the **one canonical paging contract** (one
> `HasMore` signal on every paged seam, honest totals where a total is
> already computed, one shared `_Pager` partial, one `PagedViewModel`
> model) and wires it into **every** paged list surface.
> **No new filters, no new routes, no new documents, no new adapters, no
> new `AccessAction`, no new bounded context.** Text search is **M8**.
>
> **Sizing:** units are sized for a **~32K-context fresh agent**, one at a
> time, each with its own closed exit criteria, in the M3 / TBD style.
> **U00 is the sign-off gate** — it authors the design doc and locks the
> decisions into **ADR 0090**; every later unit codes against the *locked*
> text. **Sequencing invariant:** the Core `HasMore` seam (U01) lands
> before any pager UI (U02–U04); the close (U05) is last so the README /
> `Milestones.cs` / ADR status are honest at ship time.

## Understanding

The platform is a single neighborhood — the lists are *small by design* —
but "small by design" is a *growth* assumption, not a *correctness*
property. Two things are wrong today, independent of scale:

1. **The pager is absent.** Every paged feed (community feed, all-sections
   feed, group feed, events, group events, projects, to-dos, goals, boards)
   accepts `?page=` but **no view renders prev/next**. A resident who has
   31 posts in "Safety" sees the first 30 and has **no way to the last
   one**. The Web view models carry `CurrentPage` (M4/M5 did that) but the
   partial that renders the pager does not exist.
2. **The paging signal is wrong or missing.** `FeedResult.Total` is
   documented as the total but is *assigned* the page's visible count —
   so even a pager that trusted `Total` would compute the wrong page
   count. The list-returning seams (`ListUpcomingAsync`,
   `ListTodosAsync`, …) return a bare `IReadOnlyList<T>` — the caller
   *can't distinguish* "30 rows, page 1 of 3" from "30 rows, last page".
   And three surfaces (announcements, tag-by-tag × 2) load **all** rows.

M7 fixes the *contract* and ships the *one* pager UI that every surface
shares. The existing filters (community picker on feeds, assignee /
unassigned / blocked / project on to-dos, goal / project on boards) are
**frozen as-is** — M7 does not add filters; it guarantees they **reset to
page 1 on change** (a filter form that preserves `?page=3` would strand a
viewer on an empty page). Text search is the M8 surface, not this one.

## Assumptions / decisions (each [PROPOSED], locked by ADR 0090 in U00)

- **D1 — the contract is signal-based, not count-based.** Every paged
  seam returns a **`bool HasMore`** alongside the rows (or already does,
  via a `FeedResult`-shaped record). `HasMore = true` iff the page's
  *candidate* set filled the page (`candidates.Count == PageSize`) — a
  full page means "more might exist", a partial page means "this is the
  last one". The Web computes `HasNextPage` from `HasMore` alone; it
  never divides `Total` by `PageSize`. **Rationale:** the candidate set
  is audited *after* paging (one `CanSeeAsync` per page-visit, C-M3·3),
  so a *visible* total is unknowable without auditing the whole set —
  and auditing the whole set to compute a page count would leak "how
  many hidden posts exist in this component" through the audit lane
  (C-M3·2 / C5). `HasMore` is the only honest signal.
- **D2 — `FeedResult.Total` is corrected to the candidate count, and the
  two views that render it as a viewer-facing count are fixed to stop
  doing so.** The field's doc-comment says "total"; the assignment says
  page-count; the two views (`Posts/Index.cshtml` line 190 "N posts in
  this community; M shown to you" and `Groups/Detail.cshtml` lines 346
  and 409, the same phrasing for the group posts and group events
  sections) render `Total` as a **viewer-facing count**. That rendering
  is a pre-existing audit-lane leak (the C-M3·2 / C5 pin, restated in
  C-M7·7): the "N in this community" number is indistinguishable from
  *how many hidden posts exist in this component*, and the current
  assignment (page-count) makes the number a lie as well as a leak.
  U01 corrects the assignment to the **candidate-set count** (the
  pre-decision count the service already has in hand, one `CountAsync`
  over the same filtered query) and updates the doc-comment to
  "candidate-set count (pre-decision, C-M7·7); never a viewer-facing
  total (D1)". U03 removes the "N in this community" phrasing from the
  two views (the `HiddenCount` field already signals hidden existence;
  the *count* of hidden posts is the leak, and it goes). **Rationale:**
  a *second* field next to a *wrong* field is a trap for the next
  agent; one correct field + the view fix is not. A viewer-facing
  "total posts in this community" number would require auditing the
  whole component (the D1 leak), so the only honest move is to stop
  showing one.
- **D3 — the bare-list seams get `out bool hasMore`, not a new record.**
  `ListUpcomingAsync`, `ListGroupEventsAsync` (already a record — gets a
  `HasMore` field), `ListTodosAsync`, `ListPickerTodosAsync`,
  `ListBoardsAsync`, `ListGoalsAsync`, `ListProjectsAsync` all gain
  `out bool hasMore` as the **last** parameter (before `CancellationToken`).
  **Rationale:** a new `PagedResult<T>` record per surface is seven new
  types for one `bool`; the `out` parameter is the .NET idiom for
  "one extra signal" and keeps every call site a one-token edit. The
  `FeedResult` record already exists and already carries the shape —
  it gets a `HasMore` *field*, not an `out` (records don't do `out`).
- **D4 — page size stays 30, stays per-service.** The `PageSize = 30`
  constants in `PostService`, `EventService`, `ProjectService` are the
  contract; U01 does **not** unify them into a shared constant (that is
  a refactor, not a lane). The `_Pager` partial takes `PageSize` as a
  view-model input (the controller passes the service's constant) so the
  partial is surface-agnostic. 30 is large enough for a neighborhood
  and small enough that a full page is a "there's more" signal, not a
  "firehose" signal.
- **D5 — one `_Pager` partial, one `PagedViewModel`.** The pager is a
  Bootstrap-5 `pagination` list: **prev / next only** (no page-number
  window — a neighborhood doesn't need "page 7 of 40"; prev/next is the
  whole UX), localized via the `kw-l` registry (two new keys × the
  seeded languages — `pagination.prev` / `pagination.next`), carrying the
  current query string (the filter-form idiom: the pager's links embed
  the *current* filter values as `?key=value` query pairs, so a paged
  feed with a community filter preserves the filter across pages).
  `PagedViewModel` is a small record: `CurrentPage`, `PageSize`,
  `HasPrevious`, `HasNext`, `BaseUrl` (the route, no query),
  `IReadOnlyDictionary<string, string> FilterParams` (the query pairs
  the `_Pager`'s links carry; empty by default), and a `static
  PagedViewModel ForRoute(string baseUrl, int page, int pageSize, bool
  hasMore, IReadOnlyDictionary<string, string>? filterParams = null)`
  factory. **No `TotalPages`** — D1 forbids computing it.
- **D6 — the three unpaged surfaces get paging; the small ones don't.**
  **Paged (U04):** `ListVisibleAsync` (announcements — a site with years
  of pinned + community announcements can exceed 30),
  `ListPostsByTagAsync` and `ListPagesByTagAsync` (a tag like "garden"
  can accumulate). **Not paged (explicitly, pinned in the design doc):**
  `ListMyDraftsAsync` (an author's drafts are bounded by the author's
  patience — a long draft list is a *curation* problem, not a paging
  one), `GetTreeAsync` (the page tree is a tree, not a feed),
  `GetComponentsAsync` (four to ten rows, by construction),
  `GetProfilesAsync` (the directory is M2's surface — a neighborhood's
  roster; if it ever needs paging, that is a dedicated lane), the RSVP
  list (`GetRsvpsAsync` — bounded by the event's audience), the
  membership list (`GetGroupMembersAsync` — same), the notification
  inbox (M6's surface; if it needs paging, that is M6's follow-on).
  **Rationale:** paging every list is a *ceremony* tax (the platform's
  principles say "boring where it can be"); paging the *growth* lists
  is the lane.
- **D7 — filters reset to page 1 on change; the pager preserves them.**
  Every existing filter form (the community picker on `/events`, the
  to-do feed's community / assignee / unassigned / blocked / project
  pickers) submits with `page=1` **or no `page`** (the controller floors
  to 1 — the existing `if (page < 1) page = 1;` shape). The `_Pager`
  partial, by contrast, carries the *current* filter values as hidden
  inputs so prev/next don't drop them. **Rationale:** "I changed the
  filter and landed on an empty page 3" is the classic pager bug; the
  two rules are the whole fix.
- **D8 — an oversized page is empty, no audit row.** `?page=99` returns
  **zero rows** and **no `CanSeeAsync` call** (the existing early-return
  shape in `PostService.ListFeedAsync` — `if (candidates.Count == 0)
  return ... HiddenCount: 0, Total: 0;` — is the pin; U01 preserves it
  and adds `HasMore: false`). **Rationale:** auditing an empty page
  would emit an aggregate row with `visibleCount = 0, hiddenCount = 0`
  that names a component the viewer can see — a zero-information audit
  row is noise, and the C3 "one row per decision" pin is cleaner when
  *no decision ran*.
- **D9 — no new filters in M7.** The filter set is **frozen** at what
  exists per surface. The design doc records the *complete* per-surface
  filter inventory (so M8 and any future filter lane start from the
  locked text). **Rationale:** "pagination and filtering" as a
  milestone name refers to *making the existing filters paginate
  correctly* (D7) — adding new filters (by-author, by-tag on the
  community feed, date-range) is scope creep that belongs to M8 (search)
  or a dedicated lane. The by-tag browse *already exists* (ADR 0044);
  M7 just pages it.
- **D10 — the test home is split by seam.** Core-seam tests (the
  `HasMore` signal, the `Total` correction, the empty-page pin) go in
  `Kumunita.Core.Tests` (the `PostgresFixture` harness — the M3
  `PostServiceTests` shape). Web-shape tests (the `PagedViewModel`
  factory, the `_Pager` partial's query preservation, the filter-reset
  rule) go in `Kumunita.Web.Tests` (the NSubstitute controller-harness
  shape — the M4 `EventControllerTests` precedent). **No e2e** (the
  Playwright harness is for editor/JS surfaces; a pager is server-rendered
  markup, covered by the view-model + partial tests).

## Invariants (pinned in U00's design doc, cited by id in U01–U05)

- **C-M7·1 — one page, one aggregate audit row.** A paged visit emits
  exactly the same audit shape as an unpaged one: one `CanSeeAsync`
  over the *page's* candidate set, one aggregate row (C-M3·3). Paging
  narrows the *candidate set before the decision pass*, never the
  visible set after it. (Extends C-M3·3 to paged visits.)
- **C-M7·2 — paging is display-only, never an access input.** A page
  number never appears in an `IAuthorizationService` call, an
  `Audience` evaluation, or an `AccessAudit` row's identity. The
  `?page=` query is a *display* parameter (the ADR 0064 `?view=`
  display-selector precedent, C-DWM·3).
- **C-M7·3 — page size is a per-service constant, 30.** Not
  configuration, not user-selectable, not a query parameter. Changing
  it is a code change + a design-doc note, not a deploy.
- **C-M7·4 — `HasMore` is the sole paging signal.** No `TotalPages`, no
  `Count()` over the visible set, no client-side "is this the last
  page?" inference. A surface that can't compute `HasMore` is *not
  paged* (D6's not-paged list is the pin).
- **C-M7·5 — an empty page is a no-decision.** `?page=N` beyond the last
  page returns zero rows, `HasMore: false`, and **no** `CanSeeAsync`
  call (D8). The existing early-return shape is the pin.
- **C-M7·6 — a filter change resets to page 1; a pager step preserves
  the filter.** (D7.) The two rules together are the filter/paging
  contract; a surface that violates either is a Web-contract break.
- **C-M7·7 — the candidate count (`Total`, D2) is pre-decision.** It
  counts the rows the *query* matched (post filter, post paging-bounds
  excluded), not the rows the *decision* allowed. It is a
  diagnostic/operational number, not a viewer-facing "X posts" count —
  the view never renders `Total` as a count (the design doc pins this).

## FACES (pinned in U00's design doc)

- **F1 — a full page offers a Next link.** A page whose candidate set
  filled 30 rows renders a *Next* link (and a *Prev* link iff
  `page > 1`). C-M7·4, C-M7·6.
- **F2 — a partial page offers no Next link.** A page whose candidate
  set was < 30 rows renders *Prev* only (iff `page > 1`) — no Next, no
  "page 2 of N" (there is no N — D1). C-M7·4.
- **F3 — the pager preserves the filter.** A paged feed with an active
  filter (community, assignee, …) keeps the filter across prev/next
  (the `_Pager`'s hidden inputs). C-M7·6.
- **F4 — a filter change lands on page 1.** Submitting the filter form
  resets `page` to 1 (or omits it — the controller floors). C-M7·6.
- **F5 — an oversized page is empty and unaudited.** `?page=99` → zero
  rows, no `AccessAudit` row. C-M7·5.
- **F6 — the announcements list pages.** A site with 31+ visible
  announcements shows 30 + a Next link; the 31st is reachable. D6,
  C-M7·1.
- **F7 — the tag-by-tag lists page.** A tag with 31+ visible posts /
  pages shows 30 + a Next link. D6, C-M7·1.
- **F8 — `FeedResult.Total` is the candidate count, not the page count.**
  A component with 31 posts (10 hidden by the decision): page 1's
  30-row window holds 20 visible + 10 hidden; the visit reports
  `Total = 31` (the *component's* candidate count, post-filter, not the
  page's 30), `Visible.Count = 20`, `HiddenCount = 10`, `HasMore =
  true`. D2, C-M7·7.

## Approach

Three tracks, sequenced. **Track A (Core):** U01 — the `HasMore` signal
on every paged seam + the `FeedResult.Total` correction + the
not-paged-surface paging (announcements list, tag-by-tag × 2 sections)
+ the seam tests. **Track B (Web):** U02 — the `PagedViewModel` +
`_Pager` partial + the `kw-l` keys; U03 — wire the pager into the
**existing** paged routes (7 routes, 8 pager instances, plus the D2
view-text removals); U04 — wire the pager into the **newly-paged**
routes (announcements list, tag-by-tag × 2 sections) + the
filter-reset pin (D7) on every filter form. **Track C (close):** U05 —
README / `Milestones.cs` flip, `ARCHITECTURE.md` note, the handoff-note
`## Summary`.

Every unit ends with **build green** (`dotnet build Kumunita.slnx -c
Debug`) and, for the test-bearing units, the **reliable test path**
(`dotnet exec tests\…\bin\Debug\net10.0\*.dll` — the AGENTS.md test-
runner quirk pin). The last unit (U05) appends the `## Summary` to the
handoff note.

## Workflow — handoff protocol for fresh-context agents

This milestone is executed as a sequence of **sealed units** (U00–U05
below), one unit per fresh agent with a **~32K context window**.

**Shared state (three-tier contract):**
- **Primary — the design doc** (`docs/design/m7-pagination-filtering-design.md`)
  — U00 authors it; it pins the exact C# signatures of every seam
  U01–U04 must match, the D1–D10 decisions, the invariants C-M7·1–7, the
  FACES F1–F8, the **pinned seam-test names**, and the per-surface filter
  inventory (D9). Every later unit's entry reads start there.
- **Secondary — this file**
  (`docs/plans-milestones/in-progress/m7/plan-m7-pagination-filtering.md`)
  — the unit registry with each unit's deliverables and exit criteria.
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/m7/m7-handoff-notes.md`). One
  section per unit, appended (never rewritten). Each unit writes exactly
  one short section before it exits; the next unit reads only that
  section + its own entry-read list.

**Per-unit template** (each `U` below follows this): **Goal** (one
sentence, one or two related deliverables); **Entry reads** (the minimal
file list, 3–6 files <~300 lines each, no full-repo scan); **Deliverables**
(a closed set of new/modified files, ≤ ~4 files / ~400 LOC, no misc
cleanups); **Exit** (`dotnet build` green for the touched projects;
handoff-note entry appended *before* any follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the § drift-guard
(a § `## M7 — Drift log` section U00 authors; a unit that finds the doc
out of date appends a `## U<m> — Drift pause` line there and stops);
(3) never introduces a test whose exact name is not in the design doc's
§ pinned-test list; (4) never opens a *new* seam on
`IAuthorizationService` / `IUserInfoService` / `IIdentityService` /
`IEventService` / `IProjectService` / `IAnnouncementService` /
`ITagService` beyond what U01's `HasMore` / `HasMore`-field / `out
bool` pins (the D1–D3 shape); (5) never re-shapes a *document* (no
`Post` / `Event` / `TodoItem` / `Announcement` field changes — this is a
read-lane and a UI lane, not a schema lane); (6) if entry reads reveal the
design doc is out of date, the unit pauses and records `## U<m> — Drift
pause` in the handoff note and the design doc's drift log.

---

## Units (6 total)

### U00 — Design doc + ADR 0090 (the sign-off gate)

- **Goal:** author `docs/design/m7-pagination-filtering-design.md` —
  **Context, Scope (in/out incl. the D6 not-paged list), the D1–D10
  decisions locked, the invariants C-M7·1–7, the FACES F1–F8, the
  exact C# seam signatures, the pinned seam-test names, the per-surface
  filter inventory (D9), the drift log** — and write **ADR 0090**
  (the next free ADR number; `docs/adr/README.md` is the index).
  **No code, no build.**
- **Entry reads:** this register (the D1–D10 + invariants + FACES are the
  primary source — U00 *re-states and refines* them into the design-doc
  shape, it does not re-derive them), `docs/design/m3-posts-design.md`
  (§ the FACES / invariant / seam-pin template to emulate),
  `docs/design/tbd-todo-dependency-design.md` (the **lane** design-doc
  shape — the D-decision + C-invariant + ADR-lock structure, the closer
  match to M7's shape than M3's milestone shape),
  `docs/adr/0064-events-calendar-dwm-views.md` (the `?view=`
  display-selector precedent — C-M7·2's closest analog),
  `docs/adr/README.md` (the ADR index — confirm 0090 is free),
  `src/Kumunita.Core/Posts/PostService.cs` (the `PageSize = 30` +
  `Skip/Take` + the `Total: visible.Count` bug + the
  `candidates.Count == 0` early-return — the three pins U01 fixes),
  `src/Kumunita.Core/Posts/FeedResult.cs` (the record shape D2 corrects),
  `src/Kumunita.Core/Events/EventService.cs` (the `ListUpcomingAsync`
  + `ListGroupEventsAsync` paging — the D3 `out bool` targets),
  `src/Kumunita.Core/Projects/ProjectService.cs` (the
  `ListTodosAsync` / `ListBoardsAsync` / `ListGoalsAsync` /
  `ListProjectsAsync` paging — the D3 `out bool` targets),
  `src/Kumunita.Core/Announcements/AnnouncementService.cs`
  (`ListVisibleAsync` — the D6 newly-paged target),
  `src/Kumunita.Core/Tags/TagService.cs` (`ListPostsByTagAsync` /
  `ListPagesByTagAsync` — the D6 newly-paged targets),
  `src/Kumunita.Web/Controllers/EventController.cs` (the
  `CurrentPage: page` VM shape + the community-filter form — the D7
  filter-reset target),
  `src/Kumunita.Web/Views/Event/Index.cshtml` (the filter-form markup
  the D7 fix touches + the *absence* of a pager — the gap U03 fills),
  `src/Kumunita.Web/Models/EventEditorModel.cs` (the
  `CurrentPage` field in a VM — the `PagedViewModel`'s precedent),
  `tests/Kumunita.Web.Tests/EventControllerTests.cs` (the NSubstitute
  controller-harness shape U02/U03's Web tests mirror),
  `tests/Kumunita.Core.Tests/PostServiceTests.cs` (the `PostgresFixture`
  seam-test shape U01's Core tests mirror),
  `docs/ARCHITECTURE.md` (the context list + the "shape of the code"
  section U05 will note).
- **Deliverables (2 files, 1 new + 1 append):**
  - `docs/design/m7-pagination-filtering-design.md` (~250–300 lines).
    Sections:
    - `## Context` — M6 shipped notifications; the lists have been
      silently truncating since M3; M7 makes the truncation *navigable*
      and the paging contract *honest*. The arrow: a neighborhood that
      grows past one page of posts still has every post reachable.
    - `## Scope` — **In:** the D1–D10 decisions; the `HasMore` signal on
      every paged seam; the `FeedResult.Total` correction (+ the two
      view-text fixes in `Posts/Index.cshtml` + `Groups/Detail.cshtml`
      that stop rendering `Total` as a viewer-facing count — D2);
      the `PagedViewModel` + `_Pager` partial; the pager wiring on the
      7 existing paged routes (8 pager instances — the group detail page
      renders two: one for the group posts, one for the group events);
      the paging on the 2 D6 newly-paged routes (the tag-by-tag route
      renders two pager instances: one for the posts, one for the
      pages);
      the filter-reset pin (D7) on every filter form; the seam + Web
      tests; the README / `Milestones.cs` flip. **Out:** new filters (D9 — the
      set is frozen; M8 search is the text-filter surface), a
      page-size selector (D3 — 30 is the constant), a page-number window
      (D5 — prev/next only), the not-paged list (D6 — drafts, tree,
      components, profiles, RSVPs, memberships, notifications), the
      Playwright e2e (D10 — server-rendered markup is covered by the
      VM + partial tests).
    - `## Decisions (D1–D10, locked)` — each D restated with its
      rationale (from this register) + the **exact** C# it pins
      (the D1 `HasMore` signal, the D2 `Total` correction, the D3
      `out bool` signatures, the D5 `PagedViewModel` record + the
      `_Pager` partial's contract, the D7 reset rule).
    - `## Invariants (C-M7·1–7)` — each with a one-line M7 note
      (from this register).
    - `## FACES (F1–F8)` — each bound to an invariant
      (from this register).
    - `## Seams (exact C#)` — the **complete** list of every seam
      U01–U04 touch, verbatim:
      - `FeedResult` — the new `HasMore` field + the corrected
        `Total` doc-comment (the exact record).
      - `PostService.ListFeedAsync` / `ListAllFeedAsync` /
        `ListGroupFeedAsync` — the three `FeedResult` returns with
        `HasMore: candidates.Count == PageSize` + `Total: candidateCount`
        (the `CountAsync` over the same filtered query).
      - `EventService.ListUpcomingAsync` — the `out bool hasMore`
        signature (the exact method signature).
      - `EventService.ListGroupEventsAsync` — the
        `GroupEventFeedResult` record with the new `HasMore` field
        (the exact record).
      - `ProjectService.ListTodosAsync` / `ListPickerTodosAsync` /
        `ListBoardsAsync` / `ListGoalsAsync` / `ListProjectsAsync` —
        the five `out bool hasMore` signatures (the exact method
        signatures).
      - `AnnouncementService.ListVisibleAsync` — the new paged
        signature: `Task<AnnouncementPage> ListVisibleAsync(string?
        actorId, IReadOnlySet<string> roles, int page, CancellationToken
        ct = default)` + the new `AnnouncementPage` record
        (`IReadOnlyList<Announcement> Items, bool HasMore`) (the exact
        record + the exact method).
      - `TagService.ListPostsByTagAsync` / `ListPagesByTagAsync` — the
        new paged signatures + the `TagPage` record (the exact record +
        the exact methods).
      - `PagedViewModel` — the exact record + the `ForRoute` factory
        (the exact C#, in `Kumunita.Web.Models`).
    - `## Per-surface filter inventory (D9, frozen)` — a table:
      surface → route → existing filter params → the D7 reset rule's
      application (e.g. `/events` → `componentId` → the form submits
      without `page`; the `_Pager` carries `componentId` as a hidden
      input). Every paged surface in the app, one row each.
    - `## Pinned seam tests (exact names)` — **12** tests, the exact
      names (see U01's deliverables for the list; U00 pins them here).
    - `## Drift log` — an empty `## M7 — Drift log` section (U00
      authors it empty; a later unit appends a `## U<m> — Drift pause`
      line per unit-series rule 6).
  - `docs/adr/0090-pagination-and-filtering.md` (~60–80 lines) — the
    ADR in the repo's ADR shape (the D1–D10 decisions, the C-M7·1–7
    invariants, the "consequences" section naming the M8 search
    surface as the follow-on filter lane). Append one line to
    `docs/adr/README.md`'s index (the ADR 0090 row).
- **Exit:** both files exist; the design doc has every section named
  above; ADR 0090's status is **Accepted** (the sign-off gate is the
  design doc + ADR together). **No build.** Handoff note (new file
  `docs/plans-milestones/in-progress/m7/m7-handoff-notes.md`): 6–8 lines
  starting `## U00 — design doc + ADR 0090`, listing (a) the D1–D10 ids
  (so U01 cites them), (b) the C-M7·1–7 ids, (c) the F1–F8 ids, (d) the
  12 pinned test names (by number), (e) the two file paths.

### U01 — Core: the `HasMore` signal + the `Total` correction + the 3 newly-paged seams + the seam tests

- **Goal:** the one Core contract change — every paged seam reports
  `HasMore`, `FeedResult.Total` is the candidate count, the 3 D6
  surfaces gain paging — and the 12 pinned seam tests. **No Web, no
  views, no partial.**
- **Entry reads:** `docs/design/m7-pagination-filtering-design.md` §
  Seams (the exact C# — the *primary* source) + § Pinned seam tests
  (the 12 names) + § Invariants (C-M7·1, C-M7·4, C-M7·5, C-M7·7),
  `docs/plans-milestones/in-progress/m7/m7-handoff-notes.md` (U00's
  section — the D/invariant/FACE ids to cite),
  `src/Kumunita.Core/Posts/PostService.cs` (the three `ListFeed*`
  methods + the `FeedResult` returns — the D2 + D1 fixes),
  `src/Kumunita.Core/Posts/FeedResult.cs` (the record — the D2 + D1
  target),
  `src/Kumunita.Core/Events/EventService.cs` (the
  `ListUpcomingAsync` + `ListGroupEventsAsync` — the D3 `out bool`
  targets + the `GroupEventFeedResult` record),
  `src/Kumunita.Core/Events/GroupEventDraft.cs` (the
  `GroupEventFeedResult` record — the D1 field add),
  `src/Kumunita.Core/Events/IEventService.cs` (the interface — the D3
  signature updates),
  `src/Kumunita.Core/Projects/ProjectService.cs` (the five
  `List*Async` methods — the D3 `out bool` targets),
  `src/Kumunita.Core/Projects/IProjectService.cs` (the interface —
  the D3 signature updates),
  `src/Kumunita.Core/Announcements/AnnouncementService.cs` +
  `IAnnouncementService.cs` (the `ListVisibleAsync` — the D6 new
  `ListVisiblePagedAsync` + the `AnnouncementPage` record),
  `src/Kumunita.Core/Tags/TagService.cs` + `ITagService.cs` (the
  `ListPostsByTagAsync` / `ListPagesByTagAsync` — the D6 new paged
  overloads + the `TagPage` record),
  `tests/Kumunita.Core.Tests/PostServiceTests.cs` (the
  `PostgresFixture` harness shape to mirror for the new tests),
  `tests/Kumunita.Core.Tests/PostgresFixture.cs` (the harness).
- **Deliverables (≤ 8 files — the closed set; no other file touched):**
  - `src/Kumunita.Core/Posts/FeedResult.cs` — add `bool HasMore`
    (the last field, after `Total`); correct `Total`'s doc-comment to
    "candidate-set count (pre-decision, C-M7·7); not a visible total
    (D1)". The record is now
    `(IReadOnlyList<Post> Visible, int HiddenCount, int Page, int Total,
    bool HasMore)`.
  - `src/Kumunita.Core/Posts/PostService.cs` — in **all three**
    `ListFeed*` methods: (a) compute `int candidateCount = await
    session.Query<Post>().Where(<same filter>).CountAsync()` **before**
    the `Skip/Take` (one extra query over the same filtered set — the
    C-M7·7 pin: pre-decision, post-filter); (b) assign `Total:
    candidateCount` (was `visible.Count`); (c) assign `HasMore:
    candidates.Count == PageSize`; (d) the `candidates.Count == 0`
    early-return becomes `Total: 0, HasMore: false` (the C-M7·5 pin).
    **No other change** to the method (the `CanSeeAsync` call, the
    visible-set filter, the audit row are all untouched — C-M7·1).
  - `src/Kumunita.Core/Events/GroupEventDraft.cs` — add `bool HasMore`
    to the `GroupEventFeedResult` record (the last field).
  - `src/Kumunita.Core/Events/EventService.cs` — (a)
    `ListUpcomingAsync` gains `out bool hasMore` (last param before
    `CancellationToken`); `hasMore = candidates.Count == PageSize`
    (the `candidates` is the pre-`CanSeeAsync` list — the D1 pin);
    (b) `ListGroupEventsAsync` assigns `HasMore` on the
    `GroupEventFeedResult` it already returns.
  - `src/Kumunita.Core/Events/IEventService.cs` — the two interface
    signatures updated to match (the `out bool hasMore` on
    `ListUpcomingAsync`; the `GroupEventFeedResult` return is
    unchanged — the record gained a field, not the method).
  - `src/Kumunita.Core/Projects/ProjectService.cs` — the five
    `List*Async` methods (`ListTodosAsync`, `ListPickerTodosAsync`,
    `ListBoardsAsync`, `ListGoalsAsync`, `ListProjectsAsync`) each gain
    `out bool hasMore` (last param before `CancellationToken`);
    `hasMore = candidates.Count == PageSize` (each method's
    `candidates` is its pre-`CanSeeAsync` list).
  - `src/Kumunita.Core/Projects/IProjectService.cs` — the five
    interface signatures updated to match.
  - `src/Kumunita.Core/Announcements/AnnouncementService.cs` +
    `IAnnouncementService.cs` — **add** (do not modify the existing
    `ListVisibleAsync` — it has call sites in the banner + the admin
    surface that don't want paging): a new
    `Task<AnnouncementPage> ListVisiblePagedAsync(string? actorId,
    IReadOnlySet<string> roles, int page, CancellationToken ct =
    default)` + a new `AnnouncementPage` record
    (`(IReadOnlyList<Announcement> Items, bool HasMore)`) — the
    `PageSize = 30` constant (D3) + the same `Where` filter the
    existing `ListVisibleAsync` uses + `Skip/Take` + the
    `hasMore = candidates.Count == PageSize` shape. **No audit row**
    (announcements have no audit lane — the existing
    `ListVisibleAsync`'s doc-comment pin; C-M7·1 is vacuously satisfied).
  - `src/Kumunita.Core/Tags/TagService.cs` + `ITagService.cs` —
    **add** `Task<TagPage> ListPostsByTagPagedAsync(string slug,
    string actorId, int page, CancellationToken ct = default)` and
    `Task<TagPage> ListPagesByTagPagedAsync(string slug, string actorId,
    int page, CancellationToken ct = default)` + a new `TagPage` record
    (`(IReadOnlyList<object> Items, bool HasMore)`) — **no:** the two
    methods return **different element types** (`Post` vs `Page`); the
    record is **two** records: `TagPostPage (IReadOnlyList<Post> Items,
    bool HasMore)` and `TagPagePage (IReadOnlyList<Page> Items, bool
    HasMore)` (the design doc's § Seams pins the exact two records —
    U00's entry reads caught this; if the design doc pinned a single
    `TagPage`, U01 records a `## U01 — Drift pause` in the drift log
    and uses the two-record shape, which is the correct one). The
    existing `ListPostsByTagAsync` / `ListPagesByTagAsync` are
    **unmodified** (the non-paged call sites keep working; the paged
    overloads are the new lane).
  - `tests/Kumunita.Core.Tests/M7PaginationSeamTests.cs` (new file) —
    **12 tests**, the exact names from the design doc § Pinned seam
    tests:
    1. `F1_FullPage_HasMoreTrue` — a component with 31 visible posts:
       `ListFeedAsync(page: 1)` returns `HasMore: true`, `Total: 31`.
    2. `F2_PartialPage_HasMoreFalse` — a component with 5 posts:
       `ListFeedAsync(page: 1)` returns `HasMore: false`, `Total: 5`.
    3. `F5_OversizedPage_EmptyAndNoAuditRow` — `ListFeedAsync(page:
       99)` returns `Visible.Count: 0, HasMore: false, Total:
       candidateCount` and **zero** `AccessAudit` rows (C-M7·5 — the
       early-return ran before `CanSeeAsync`).
    4. `F8_TotalIsCandidateCount_NotPageCount` — 31 posts, 10 hidden:
       page 1 returns `Total: 30` (the candidate count — the page's
       30), `Visible.Count: 20`, `HasMore: true`.
    5. `C_M7_1_OneAggregateRowPerPageVisit` — 31 posts: page 1 **and**
       page 2 each emit exactly **one** `AccessAudit` row (targetKind
       `"post"`); page 2's row names the same component.
    6. `C_M7_5_ListUpcomingAsync_OversizedPage_NoAuditRow` — the
       `EventService.ListUpcomingAsync(page: 99)` shape: zero rows,
       `hasMore: false`, zero `AccessAudit` rows.
    7. `ListUpcomingAsync_FullPage_HasMoreTrue` — 31 upcoming events:
       page 1 → 30 rows, `hasMore: true`.
    8. `ListUpcomingAsync_PartialPage_HasMoreFalse` — 5 upcoming
       events: page 1 → 5 rows, `hasMore: false`.
    9. `ListTodosAsync_FullPage_HasMoreTrue` — 31 visible to-dos:
       page 1 → 30 rows, `hasMore: true`.
    10. `ListTodosAsync_OversizedPage_HasMoreFalseAndEmpty` — page 99
        → zero rows, `hasMore: false`.
    11. `ListVisiblePagedAsync_Announcements_Page1_Full_HasMoreTrue` —
        31 visible announcements: page 1 → 30 items, `HasMore: true`;
        page 2 → 1 item, `HasMore: false`.
    12. `ListPostsByTagPagedAsync_FullPage_HasMoreTrue` — a tag with
        31 visible posts: page 1 → 30 items, `HasMore: true`; page 2 →
        1 item, `HasMore: false`.
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The 12 tests
  exist and **pass** (run via the reliable path: `dotnet exec
  tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
  --filter-class Kumunita.Core.Tests.M7PaginationSeamTests` — or the
  full assembly run if the filter flag is finicky on this runner; the
  AGENTS.md quirk pin applies). **No Web changes, no views, no partial,
  no design-doc edits** (a drift finding → the drift log, not the body).
  Handoff note: 6–8 lines starting `## U01 — Core HasMore + Total + 3
  new seams + 12 seam tests` — (a) the 8 file paths touched; (b) the
  `FeedResult` shape (the new field + the corrected `Total` comment);
  (c) the two tag-page record names (the drift, if hit); (d) the 12
  test names (verbatim) + the pass count (12/12 expected); (e) any
  compile warnings on the touched seams.

### U02 — Web: `PagedViewModel` + `_Pager` partial + the `kw-l` keys

- **Goal:** the one shared pager UI — the view model, the partial, the
  localized strings — **not yet wired into any surface** (U03/U04 do
  the wiring). A new surface that needs a pager drops in one
  `<partial name="_Pager" model="…"/>` line.
- **Entry reads:** `docs/design/m7-pagination-filtering-design.md` §
  Seams (the `PagedViewModel` exact C#) + § FACES (F1, F2, F3) + §
  Per-surface filter inventory (the D7 reset rule the partial's hidden
  inputs implement), `docs/plans-milestones/in-progress/m7/
  m7-handoff-notes.md` (U01's section — the `HasMore` signal U02
  consumes), `src/Kumunita.Web/Models/EventEditorModel.cs` (the
  `EventIndexViewModel` with its `CurrentPage` field — the precedent
  for the `PagedViewModel` shape), `src/Kumunita.Web/Views/Event/
  Index.cshtml` (the filter-form markup — the `_Pager`'s hidden-input
  idiom to mirror), `src/Kumunita.Web/Views/Shared/_EventsTabs.cshtml`
  (the tab-strip partial — the `_Pager`'s sibling, the
  `@model`-less partial shape), `src/Kumunita.Core/Localization/
  KnownTranslationKeys.cs` (the `kw-l` registry — the two new keys'
  home), `src/Kumunita.Web/Views/Shared/_Layout.cshtml` (the
  Bootstrap-5 `pagination` CSS is already loaded — confirm before
  adding a CDN), `tests/Kumunita.Web.Tests/EventControllerTests.cs`
  (the NSubstitute harness U02's VM-factory test mirrors).
- **Deliverables (4 files, 2 new + 2 modify):**
  - `src/Kumunita.Web/Models/PagedViewModel.cs` (new) — the exact
    record from the design doc § Seams:
    `public sealed record PagedViewModel(int CurrentPage, int PageSize,
    bool HasPrevious, bool HasNext, string BaseUrl,
    IReadOnlyDictionary<string, string> FilterParams);` + the static
    factory `public static PagedViewModel ForRoute(string baseUrl,
    int page, int pageSize, bool hasMore,
    IReadOnlyDictionary<string, string>? filterParams = null)` —
    `HasPrevious = page > 1`, `HasNext = hasMore`, `FilterParams`
    defaults to an empty dict. The record's doc-comment pins D1 (no
    `TotalPages`), D5 (prev/next only), D7 (the `FilterParams` are the
    hidden inputs).
  - `src/Kumunita.Web/Views/Shared/_Pager.cshtml` (new, ~30 lines) —
    `@model Kumunita.Web.Models.PagedViewModel`. Renders **nothing**
    when `!HasPrevious && !HasNext` (a one-page surface shows no pager
    — the F2 pin). Otherwise a `<nav aria-label="pagination">` with a
    Bootstrap-5 `ul.pagination`: a *Prev* `<li>` (disabled iff
    `!HasPrevious`) linking `BaseUrl?page=CurrentPage-1` + the
    `FilterParams` as `?key=value` pairs; a *Next* `<li>` (disabled
    iff `!HasNext`) linking `BaseUrl?page=CurrentPage+1` + the same
    `FilterParams`. The labels are `<kw-l key="pagination.prev">` /
    `<kw-l key="pagination.next">`. The links are **plain `href`**
    (a GET navigation — the server-rendered idiom; no JS).
  - `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — add the
    two keys (`pagination.prev` = "← Newer" / "→ Older"? — **no:** the
    feeds are newest-first, so *Prev* = older posts, *Next* = … wait:
    the feed is `OrderByDescending(Created)` — page 1 is the
    *newest*; page 2 is *older*. So the *Next* link goes to **older**
    posts and the *Prev* link goes to **newer** ones. The keys are
    `pagination.prev` = "Newer" (the arrow is toward newer) and
    `pagination.next` = "Older" (the arrow is toward older). U00's
    design doc pins the exact en strings; U02 uses them verbatim) ×
    the seeded languages (`en`, `de`, `fr`, `da` — the ADR 0042 /
    ADR 0045 set; the exact language list is in the design doc's §
    Seams). The `KnownTranslationKeys_ParityTests` (the existing
    `tests/Kumunita.Core.Tests/KnownTranslationKeys_ParityTests.cs`)
    must still pass — the new keys are in the registry, so they're
    covered.
  - `tests/Kumunita.Web.Tests/PagedViewModelTests.cs` (new) — **4**
    tests:
    1. `ForRoute_Page1_Full_HasNextTrue_HasPrevFalse` — `ForRoute(
       "/community/safety", 1, 30, true)` → `HasNext: true,
       HasPrevious: false, CurrentPage: 1`.
    2. `ForRoute_Page2_Partial_HasNextFalse_HasPrevTrue` — `ForRoute(
       "/community/safety", 2, 30, false)` → `HasNext: false,
       HasPrevious: true`.
    3. `ForRoute_FilterParams_Preserved` — `ForRoute("/events", 2, 30,
       true, new() { ["componentId"] = "safety" })` →
       `FilterParams` contains `componentId = safety` (the D7 pin —
       the partial's hidden-input source).
    4. `ForRoute_Page1_NotFull_RendersNothing` — `ForRoute("/events",
       1, 30, false)` → `HasNext: false, HasPrevious: false` (the
       partial's no-render condition, F2).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green; the 4 VM
  tests pass (the reliable test path). **No surface wired yet** (U03/
  U04). **No design-doc edits.** Handoff note: 5–6 lines starting
  `## U02 — PagedViewModel + _Pager + kw-l` — (a) the 4 file paths;
  (b) the `PagedViewModel` shape (the field list); (c) the two `kw-l`
  key names + the en strings (so U03/U04 cite them); (d) the 4 test
  names + the pass count; (e) the "renders nothing when one page" pin
  (the partial's no-render condition, for U03's drop-in).

### U03 — Web: wire the pager into the 7 existing paged routes (8 pager instances) + the D2 view-text fixes

- **Goal:** the 7 routes that already page in Core (the community feed, the
  all-sections feed, the group detail's **two** sections, the events feed,
  the projects landing's **two** sections, the to-dos feed, the boards
  feed) get the `_Pager` drop-in + the `PagedViewModel` in their view model
  + the controller passes `hasMore` through. One route = one VM field + one
  controller line + one or two `<partial>` lines + one test assertion.
  The two views that render `Total` as a viewer-facing count
  (`Posts/Index.cshtml`, `Groups/Detail.cshtml` — D2) get their "N in this
  community" phrasing **removed** (the `HiddenCount` field already signals
  hidden existence; the *count* of hidden posts is the leak, and it goes).
  **No new seams, no Core change, no new filters.**
- **Entry reads:** `docs/design/m7-pagination-filtering-design.md` §
  Per-surface filter inventory (the D7 hidden-input values per surface)
  + § FACES (F1, F3), `docs/plans-milestones/in-progress/m7/
  m7-handoff-notes.md` (U01's `HasMore` signal + U02's
  `PagedViewModel` shape), `src/Kumunita.Web/Models/PagedViewModel.cs`
  (U02's record), `src/Kumunita.Web/Views/Shared/_Pager.cshtml`
  (U02's partial — the `@model` directive), and **per surface** (the
  entry reads are the 7 routes' controller + VM + view; the closed set
  is in Deliverables):
  - `src/Kumunita.Web/Controllers/PostsController.cs` + `Models/
    FeedViewModel.cs` (or the equivalent VM — the `page: 1` call site
    at line 143) + `Views/Posts/Index.cshtml` (the `Model.Total >
    Model.Items.Count` "N in this community" block at line 190 — the
    D2 view-text fix)
  - `src/Kumunita.Web/Controllers/GroupsController.cs` (the
    `ListGroupFeedAsync(group.Id, actor, page: 1)` call at line 282
    **and** the `ListGroupEventsAsync(group.Id, actor, page: 1)` call
    at line 311 — both on the same `Detail` route, so **two** pager
    instances) + `Views/Groups/Detail.cshtml` (the `GroupPostsTotal`
    block at line 346 **and** the `GroupEventsTotal` block at line 409
    — the D2 view-text fixes)
  - `src/Kumunita.Web/Controllers/EventController.cs` + `Models/
    EventEditorModel.cs` (`EventIndexViewModel.CurrentPage`) +
    `Views/Event/Index.cshtml`
  - `src/Kumunita.Web/Controllers/ProjectsController.cs` (the
    `ProjectsIndex` action — the `ListGoalsAsync` + `ListProjectsAsync`
    calls at lines 2469 — both on the same `/projects` route, so
    **two** pager instances) + `Models/ProjectBoardViewModels.cs` (or
    the equivalent `ProjectsIndexViewModel`) + `Views/Projects/
    ProjectsIndex.cshtml`
  - `src/Kumunita.Web/Controllers/ProjectsController.cs` (the to-dos
    feed) + `Models/ProjectTodoViewModels.cs`
    (`TodoIndexViewModel.CurrentPage`) + `Views/Projects/
    TodosIndex.cshtml`
  - `src/Kumunita.Web/Controllers/ProjectsController.cs` (the boards
    feed) + `Models/ProjectBoardViewModels.cs`
    (`BoardIndexViewModel.CurrentPage`) + `Views/Projects/
    BoardIndex.cshtml`
  - `src/Kumunita.Web/Controllers/HomeController.cs` (the
    `ListAllFeedAsync` call at line 176 — the all-sections feed; **no
    pager** on this surface — the home page is a *truncated* feed by
    design, not a paged one, D6's not-paged list; the `ListFeedAsync`
    + `ListAllFeedAsync` seam change in U01 still applies to the Core
    contract, but the home page's `FeedRowsPerColumn` truncation is a
    different shape and is **out of scope** for the pager — the
    design doc's § Per-surface filter inventory pins this)
- **Deliverables (the closed set — per surface: 1 VM modify + 1
  controller modify + 1 view modify; ≤ 24 files, one logical change
  per file):**
  - **Per route** (× 7 routes, **8 pager instances** — the group
    detail's `Detail` route and the projects landing's `ProjectsIndex`
    route each render **two** paged sections, so they get **two**
    `PagedViewModel?` fields and **two** `<partial>` drop-ins; the
    other 4 routes get **one** each):
    - The route's **view model** — add one `PagedViewModel?` field per
      paged section (nullable — a one-page section leaves it `null`,
      the partial's no-render condition, the F2 pin; the VM's
      doc-comment notes "null when the section is one page"). For the
      two-section routes, the two fields are named section-scoped (the
      group detail: `PagerPosts` / `PagerEvents`; the projects
      landing: `PagerGoals` / `PagerProjects`) — the section-scoped
      names make each partial's `BaseUrl` unambiguous (each section has
      its own `BaseUrl` + `FilterParams`).
    - The route's **controller action** — (a) accept `int page = 1`
      (most already do); (b) call each section's Core seam's `out bool
      hasMore` / `HasMore` field (U01's signal); (c) build a
      `PagedViewModel` per section via `PagedViewModel.ForRoute(<the
      section's route>, page, 30, hasMore, <the section's filter params
      as a dict — the D7 hidden-input source, from the design doc's
      inventory>)` and assign it to the VM's section-`Pager` field
      (**only** when `hasMore || page > 1` — otherwise leave it
      `null`, the one-page no-render pin); (d) pass `page` through to
      the seam (most already do — the `page: 1` hard-coded call sites
      become `page: page`).
    - The route's **view** — add one line per section, just before the
      closing `</div>` of the section's list container:
      `@if (Model.PagerPosts is not null) { <partial name="_Pager" model="Model.PagerPosts" /> }`
      (the section-scoped field name matches the section's `BaseUrl`;
      U03's per-route entry read pins the exact placement).
  - **The D2 view-text fixes** (the two views that render `Total` as a
    viewer-facing count — D2, C-M7·7):
    - `src/Kumunita.Web/Views/Posts/Index.cshtml` — remove the
      `@if (Model.Total > Model.Items.Count)` block at line 190 (the
      "N in this community; M shown to you" phrasing — the
      `HiddenCount` field already signals hidden existence; the *count*
      of hidden posts is the audit-lane leak, and it goes).
    - `src/Kumunita.Web/Views/Groups/Detail.cshtml` — remove **both**
      the `@if (Model.GroupPostsTotal > Model.GroupPosts.Count)` block
      at line 346 and the `@if (Model.GroupEventsTotal >
      Model.GroupEvents.Count)` block at line 409 (the same phrasing,
      for the group posts and group events sections).
  - `tests/Kumunita.Web.Tests/M7PagerWiringTests.cs` (new) — **3**
    tests (one per *class* of route — the 7 routes are the same
    shape; the 3 tests pin the shape, not all 7):
    1. `PostsFeed_Page2_HasMore_PagerPresent` — the
       `PostsController.Index` harness (the `EventControllerTests`
       NSubstitute shape): `PostService.ListFeedAsync` returns a
       `FeedResult` with `HasMore: true, Page: 2`; the action result
       is a `ViewResult` whose `Model` is a `FeedViewModel` (or the
       route's VM) with `Pager != null` (or `PagerPosts != null` on a
       two-section route), `Pager.HasNext: true`,
       `Pager.HasPrevious: true`.
    2. `PostsFeed_Page1_Partial_PagerAbsent` — `HasMore: false, Page:
       1` → `Pager == null` (the one-page no-render pin, F2).
    3. `EventsFeed_FilterParam_PreservedInPager` — the
       `EventController.Index` harness with `componentId: "safety"`,
       `page: 2`, `hasMore: true` → `Pager.FilterParams` contains
       `componentId = safety` (the D7 hidden-input pin, F3).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green; the 3 wiring
  tests pass (the reliable test path). **All 7 routes render the pager**
  (U03's exit check: a quick `grep` for `<partial name="_Pager"` in the
  7 views — **8 hits**, the two-section routes contributing two each).
  **The two D2 view-text blocks are removed** (grep `Model.Total >` in
  `Posts/Index.cshtml` and `Groups/Detail.cshtml` — 0 hits; grep
  `GroupPostsTotal` / `GroupEventsTotal` in `Groups/Detail.cshtml` — 0
  hits). **No Core change, no new seams, no design-doc edits.**
  Handoff note: 6–8 lines starting `## U03 — pager wired into 7 routes
  (8 instances) + D2 view-text` — (a) the 7 route paths + the 7 view
  paths (one line each); (b) the 3 test names + the pass count; (c) any
  route where the VM shape was different from the pattern (a deviation,
  if any); (d) the `Pager is not null` guard's presence in all 7 views
  (the grep count: 8 partial hits); (e) the two D2 view-text blocks
  removed (the grep count: 0 hits).

### U04 — Web: wire the pager into the 2 newly-paged routes (3 paged sections) + the D7 filter-reset pin

- **Goal:** the 2 D6 routes (the announcements list, the tag-by-tag
  `/tags/{slug}` route) get the `_Pager` drop-in (the U03 pattern over
  the U01 new seams) — the tag-by-tag route renders **two** paged
  sections (posts + pages) on one page, so it gets **two**
  `PagedViewModel?` fields (section-scoped, like U03's two-section
  routes) and **two** `<partial>` drop-ins; and **every** filter form
  on every paged surface gets the D7 reset pin (the form submits
  without `?page=`). **No new seams, no Core change.**
- **Entry reads:** `docs/design/m7-pagination-filtering-design.md` §
  Per-surface filter inventory (the D7 reset rule per surface — the
  *primary* source) + § FACES (F4, F6, F7),
  `docs/plans-milestones/in-progress/m7/m7-handoff-notes.md` (U01's 3
  new seams + U02's `PagedViewModel` + U03's drop-in pattern),
  `src/Kumunita.Core/Announcements/IAnnouncementService.cs` (the new
  `ListVisiblePagedAsync` + the `AnnouncementPage` record),
  `src/Kumunita.Core/Tags/ITagService.cs` (the two new paged overloads
  + the two page records), `src/Kumunita.Web/Controllers/
  AnnouncementController.cs` (the `ListVisibleAsync` call site → the
  new `ListVisiblePagedAsync`), `src/Kumunita.Web/Controllers/
  TagController.cs` (the two `List*ByTagAsync` call sites → the new
  paged overloads), `src/Kumunita.Web/Views/Announcement/Index.cshtml`
  + `src/Kumunita.Web/Views/Tag/ByTag.cshtml` (the tag-by-tag view —
  one route, two paged sections) +
  `src/Kumunita.Web/Views/Event/Index.cshtml` (the filter-form markup
  the D7 reset touches) + `src/Kumunita.Web/Views/Projects/
  TodosIndex.cshtml` (the to-do feed's filter form — the D7 reset
  target).
- **Deliverables (the closed set):**
  - `src/Kumunita.Web/Controllers/AnnouncementController.cs` — the
    announcements-list action: accept `int page = 1`, call
    `ListVisiblePagedAsync(actorId, roles, page)` (the U01 new seam),
    build the `PagedViewModel` (the U03 pattern — `BaseUrl: "/
    announcements"`, no filter params — the announcements list has no
    filter form, the D9 inventory confirms), assign to the VM's
    `Pager` field.
  - The announcements list's **view model** — add the `PagedViewModel?
    Pager` field (the U03 pattern).
  - `src/Kumunita.Web/Views/Announcement/Index.cshtml` — add the
    `<partial name="_Pager">` drop-in (the U03 pattern).
  - `src/Kumunita.Web/Controllers/TagController.cs` — the `ByTag`
    action (one route, `/tags/{slug}`): accept `int page = 1`, call
    the two U01 paged overloads (`ListPostsByTagPagedAsync` +
    `ListPagesByTagPagedAsync`), build a `PagedViewModel` per section
    (`BaseUrl: "/tags/{slug}"` for both — the route is one; the
    section is disambiguated by the section-scoped `PagerPosts` /
    `PagerPages` field names, like U03's two-section routes), assign
    to the VM's two section-`Pager` fields.
  - The tag-by-tag **view model** (`TagByTagViewModel`) — add two
    `PagedViewModel?` fields: `PagerPosts` and `PagerPages` (the
    U03 two-section pattern).
  - `src/Kumunita.Web/Views/Tag/ByTag.cshtml` — add **two** `<partial
    name="_Pager">` drop-ins (one before the posts list's closing
    `</div>`, one before the pages list's closing `</div>`), each
    guarded by `@if (Model.PagerPosts is not null)` / `@if (Model.
    PagerPages is not null)` (the U03 two-section pattern).
  - **The D7 reset pin** — on **every** paged surface's filter form
    (the D9 inventory's surfaces with a filter form: `/events`,
    `/projects/todos`, `/projects/boards`, `/projects/goals`,
    `/projects/projects` — the 5 surfaces with a community / assignee
    / unassigned / blocked / project / goal picker): the form's
    `<button type="submit">` is preceded by **no** `name="page"`
    hidden input (the form submits without `?page=` — the controller
    floors to 1, the existing `if (page < 1) page = 1;` shape). U04
    **verifies** the 5 forms have no `name="page"` hidden input (the
    D7 pin — a form that carries `?page=3` into a filter submission
    would strand the viewer on an empty page; the grep: `grep -r
    'name="page"' src/Kumunita.Web/Views/` should return **zero**
    hits in the 5 filter forms). **If** a form has one, U04 removes
    it (a one-line delete per form). **The `_Pager` partial's** links
    *do* carry `?page=N` — that is the pager, not a filter form; the
    grep is scoped to the 5 filter forms' `<form>` blocks, not the
    `<partial>` output (the partial is server-rendered — its
    `?page=` is in the *output*, not in a `<form name="page">`).
  - `tests/Kumunita.Web.Tests/M7NewlyPagedTests.cs` (new) — **3**
    tests:
    1. `F6_AnnouncementsList_Page2_HasMore_PagerPresent` — the
       `AnnouncementController` harness: `ListVisiblePagedAsync`
       returns `AnnouncementPage(30 items, HasMore: true)`, `page: 2`
       → the VM's `Pager != null`, `Pager.HasNext: true`.
    2. `F7_TagByTag_Page1_Full_BothSectionsPaged` — the
       `TagController` harness: `ListPostsByTagPagedAsync` returns
       `TagPostPage(30 items, HasMore: true)` and
       `ListPagesByTagPagedAsync` returns `TagPagePage(30 items,
       HasMore: true)`, `page: 1` → the VM's `PagerPosts != null` and
       `PagerPages != null`, both with `HasNext: true`,
       `HasPrevious: false` (the two-section route's both-pagers
       pin).
    3. `F4_FilterForm_SubmitsWithoutPage` — the `EventController`
       harness: the `/events` action with `componentId: "safety"` and
       **no** `page` param → the `ListUpcomingAsync` call receives
       `page: 1` (the floor) and the VM's `Pager` (if present) has
       `CurrentPage: 1` (the D7 reset pin — a filter submission never
       carries a page).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green; the 3 tests
  pass (the reliable test path); the 2 newly-paged views render the
  pager (the grep: `<partial name="_Pager"` in the 2 views — **3
  hits**: the announcements list view has 1, the tag-by-tag view has
  2); the 5 filter forms have no `name="page"` hidden input (the
  grep: 0 hits). **No Core change, no new seams, no design-doc
  edits.** Handoff note: 6–8 lines starting `## U04 — 2 newly-paged
  routes (3 sections) + D7 reset` — (a) the 2 route paths + view
  paths; (b) the 5 filter forms verified (the grep: 0 `name="page"`
  hits); (c) the 3 test names + the pass count; (d) any form that
  **had** a `name="page"` hidden input (a deviation, if any).

### U05 — Close: README / `Milestones.cs` flip + `ARCHITECTURE.md` note + the handoff `## Summary`

- **Goal:** flip the M7 milestone to **done** in the two pinned places
  (README Roadmap + `Milestones.cs` — the AGENTS.md doc↔code parity
  contract), note the M7 lane in `ARCHITECTURE.md` (the "shape of the
  code" section — the `PagedViewModel` + `_Pager` as the shared
  pagination idiom), and write the handoff-note `## Summary` so the
  next milestone (M8 search) starts from the locked text.
- **Entry reads:** `README.md` § Status + § Roadmap (the M7 line to
  flip — the "next is M7" sentence), `src/Kumunita.Web/Milestones.cs`
  (the `M7` entry — `StatusNext` → `StatusDone`; the **next** entry —
  `M8` — `StatusPlanned` → `StatusNext`), `tests/Kumunita.Web.Tests/
  MilestonesTests.cs` (the test that pins the milestone order + the
  single-in-progress pin — **U05 must keep it green**: the flip
  changes `M7` from `StatusNext` to `StatusDone` and `M8` from
  `StatusPlanned` to `StatusNext`; the test asserts the *order* and
  the *single in-progress* — the flip preserves both, so the test
  should pass unchanged; **if** the test asserts `M7` is the
  in-progress one *by id*, U05 updates the test's expected id to `M8`
  — a one-line test edit, in scope), `docs/ARCHITECTURE.md` § "The
  shape of the code" (the section to note), `docs/plans-milestones/
  in-progress/m7/m7-handoff-notes.md` (all of U00–U04's sections — the
  `## Summary`'s source).
- **Deliverables (≤ 4 files, all modify):**
  - `README.md` — the § Status paragraph: the "**next is M7** —
    pagination & filtering" sentence becomes "**M7 is done** —
    pagination & filtering (the `HasMore` signal on every paged seam,
    the `FeedResult.Total` correction, the `PagedViewModel` + `_Pager`
    partial, the pager wired into the 11 list surfaces, the D7
    filter-reset pin; ADR 0090)" and "**next is M8** — search". The
    § Roadmap table (if present): the M7 row → **Done**, the M8 row →
    **Next**.
  - `src/Kumunita.Web/Milestones.cs` — the `M7` entry: `StatusNext`
    → `StatusDone`; the `M8` entry: `StatusPlanned` → `StatusNext`.
    **No other entry touched** (the order is preserved — the
    `MilestonesTests` pin).
  - `tests/Kumunita.Web.Tests/MilestonesTests.cs` — **only if** the
    test asserts the in-progress milestone *by id* (U05's entry read
    confirms): the expected id `M7` → `M8`. A one-line edit. **If**
    the test asserts only the *order* + *count of in-progress*, no
    edit (the flip preserves both).
  - `docs/ARCHITECTURE.md` — the "shape of the code" section (or the
    closest equivalent — U05's entry read confirms the exact section):
    add one short paragraph: "M7 (ADR 0090) shipped the shared
    pagination idiom — the `PagedViewModel` record + the `_Pager`
    partial (`Views/Shared/_Pager.cshtml`) + the `HasMore` signal on
    every paged Core seam. A new list surface that needs paging adds
    the `HasMore` signal to its seam (the D1/D3 shape) and drops in
    the `_Pager` partial (the D5 shape); it does not re-invent a pager."
  - `docs/plans-milestones/in-progress/m7/m7-handoff-notes.md` —
    append `## Summary` — a table of the shipped units (U00–U04),
    with their one-liner goal + test count + any deviations (the
    `## U<m> — Drift pause` sections, if any) + the M8 handoff line:
    "M7 is closed; the paging contract is ADR 0090; the filter set is
    frozen (D9) — M8 search is the next filter surface (text-based),
    and it reuses the `HasMore` signal + the `_Pager` partial (no new
    pager idiom)."
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green (the
  `Milestones.cs` change is a constant flip — it compiles);
  `MilestonesTests` green (the reliable test path); the README +
  `Milestones.cs` agree (the M7 row is **Done** in both, the M8 row is
  **Next** in both — the AGENTS.md parity pin); the `ARCHITECTURE.md`
  note is present; the handoff note's `## Summary` section is present.
  **No code beyond the `Milestones.cs` flip + the possible one-line
  `MilestonesTests` edit.** Handoff note: the `## Summary` section is
  the **last** M7 artifact — the M8 agent reads it + ADR 0090 + the
  design doc's § Per-surface filter inventory (the D9 frozen set) and
  starts.

---

## Moving the lane (when the work is done)

When U05's exit is met, the whole lane moves:

```
docs/plans-milestones/in-progress/m7/  →  docs/plans-milestones/done/m7/
```

(the `plan-m7-pagination-filtering.md` + the `m7-handoff-notes.md`
together — the M3 precedent: `done/m3/plan-m3-posts-components.md` +
`done/m3/m3-handoff-notes.md`). The design doc
(`docs/design/m7-pagination-filtering-design.md`) and ADR 0090
(`docs/adr/0090-pagination-and-filtering.md`) **stay put** — they are
repo-root artifacts, not lane-scoped (the M3 precedent: the design doc
is in `docs/design/`, the ADR in `docs/adr/`).
