# ADR 0090 — Pagination & filtering: the one canonical paging contract (M7)

Status: Accepted
Date: 2026-09-26

## Context

M3 shipped paging **in Core** — `PostService`, `EventService`, and
`ProjectService` all do `Skip((page-1)*30).Take(30)` — but the paging
surface has been absent and dishonest ever since:

- **No surface shows a pager.** Every paged feed (community feed,
  all-sections feed, group posts, group events, events, projects landing,
  to-dos, boards) accepts `?page=` but no view renders prev/next. A
  resident with 31 posts in a component sees the first 30 and has **no
  way to the last one**.
- **The paging signal is wrong or missing.** `FeedResult.Total` is
  documented as "total" but assigned the *page's* visible count
  (`Total: visible.Count`) — a pager that trusted it would compute the
  wrong page count. The bare-list seams (`ListUpcomingAsync`,
  `ListTodosAsync`, the other four `IProjectService` feeds) return
  `IReadOnlyList<T>` — the caller cannot distinguish "30 rows, page 1 of 3"
  from "30 rows, last page".
- **Three growth lists load all rows** (the announcements list, the
  tag-by-tag posts and pages) — unbounded by construction.
- **A pre-existing audit-lane leak is rendered.** Two views
  (`Posts/Index.cshtml`, `Groups/Detail.cshtml`) render `Total` as
  "N posts in this community; M shown to you" — the *count* of hidden
  posts in a component, surfaced to the viewer (the C-M3·2 / C5 pin),
  and currently a *lie* as well, because the assignment is the page's
  visible count.

At one-neighborhood scale the lists are small by design — but
"small by design" is a growth assumption, not a correctness property, and
the contract defects (the wrong `Total`, the missing `HasMore`, the
unbounded lists, the rendered leak) are wrong regardless of scale.

This ADR records the decision; `docs/design/m7-pagination-filtering-design.md`
(parts 1–2) freezes the invariants **C-M7·1…7**, the FACES **F1–F8**, and
the **exact C#** of every seam this ADR points at — the design doc is the
authority on shape; this ADR is the authority on *why these shapes, and
why nothing else*.

## Decision

- **The contract is signal-based, not count-based (D1, C-M7·4).** Every
  paged seam reports a **`bool HasMore`** — `true` iff the page's
  *candidate* set filled the page (`candidates.Count == PageSize`). The
  Web computes "next page?" from `HasMore` alone and **never divides
  `Total` by `PageSize`**; there is no `TotalPages`. `HasMore` is the
  only honest signal: a visible total is unknowable without auditing the
  whole component (one `CanSeeAsync` per page-visit, C-M3·3), and
  auditing the whole set to compute a page count would leak *how many
  hidden posts exist* through the audit lane (C-M3·2 / C5).

- **`FeedResult.Total` becomes the pre-decision candidate count (D2,
  C-M7·7)** — the component's candidate count (post-filter, post
  paging-bounds excluded: 31, not the page's 30), computed with one
  `CountAsync` over the same filtered query **before** the `Skip/Take`.
  It is a diagnostic/operational number, **never a viewer-facing count** —
  the two views that render "N in this community" (a pre-existing
  audit-lane leak, and currently a lie) stop rendering it (U03); the
  `HiddenCount` field already signals hidden *existence*, and the
  *count* of hidden posts is the leak. A second field next to a wrong
  field is a trap; one correct field + the view fix is not.

- **The bare-list seams get an extra `HasMore` signal, not a generic
  `PagedResult<T>` wrapper (D3).** `ListUpcomingAsync`, `ListTodosAsync`,
  `ListPickerTodosAsync`, `ListBoardsAsync`, `ListGoalsAsync`,
  `ListProjectsAsync` each report the `HasMore` signal; the record-shaped
  seams (`FeedResult`, `GroupEventFeedResult`) carry it as a `HasMore`
  field. Seven new `PagedResult<T>` records for one `bool` (a generic
  wrapper type) is the rejected alternative. **Vehicle (U01 drift,
  2026-09-26):** the seam is `async`, and C# forbids `ref` / `in` / `out`
  parameters on `async` methods (**CS1988**), so the `out bool hasMore`
  parameter form is not compilable — the signal is carried by a **page
  record return** `(Items, HasMore)` per seam: `EventPage` / `TodoPage` /
  `BoardPage` / `GoalPage` / `ProjectPage`. This is the same record-return
  idiom the record-shaped seams already use, not the rejected generic
  `PagedResult<T>` wrapper — the decision's substance (D3: one extra
  signal, not a generic wrapper; D1: `HasMore` is the sole paging signal)
  is unchanged.

- **Page size stays 30, stays per-service (D4, C-M7·3).** The existing
  `PageSize = 30` constants in the three paged services are the contract
  (not unified into a shared constant — that is a refactor, not a lane);
  the two newly-paged services adopt the same per-service shape. Not
  configuration, not user-selectable, not a query parameter.

- **One `_Pager` partial, one `PagedViewModel` (D5).** A Bootstrap-5
  prev/next pager (no page-number window — a neighborhood doesn't need
  "page 7 of 40"), localized via the `kw-l` registry (two new keys
  `pagination.prev` / `pagination.next` × the four seeded languages —
  `en`: "Newer" / "Older", the feeds being newest-first /
  earliest-start-first), the pager's links carrying the current filter
  values as query pairs, and rendering **nothing** on a one-page surface
  (F2). A new paged surface drops in one `<partial name="_Pager">` line;
  it does not re-invent a pager.

- **The growth lists page; the bounded lists don't (D6, C-M7·4).**
  Newly paged: the announcements list, the tag-by-tag posts, the
  tag-by-tag pages. Explicitly **not** paged: drafts, the page tree,
  components, the directory, RSVP lists, membership lists, the
  notification inbox, the home page's truncated feed. Paging every list
  is a ceremony tax ("boring where it can be"); paging the growth lists
  is the lane.

- **A filter change resets to page 1; a pager step preserves the filter
  (D7, C-M7·6).** The existing filter forms submit without `page` (the
  controller floors to 1); the `_Pager` links carry the current filter
  values. "I changed the filter and landed on an empty page 3" is the
  classic pager bug; the two rules together are the whole fix.

- **An oversized page is empty, no audit row (D8, C-M7·5).** `?page=99`
  returns zero rows, `HasMore: false`, `Total: 0`, and **no**
  `CanSeeAsync` call — the existing early-return shape runs before any
  decision (and before the D2 `CountAsync`). A zero-information audit
  row naming a component the viewer can see is noise; the C3 "one row per
  decision" pin is cleaner when *no decision ran*.

- **No new filters in M7 (D9).** The per-surface filter inventory
  (design doc §8) is the frozen set; "pagination and filtering" means
  *making the existing filters paginate correctly*. New filters
  (by-author, by-tag on the community feed, date-range, text search) are
  M8's surface or dedicated lanes.

- **The test home is split by seam (D10).** Core-seam pins in
  `Kumunita.Core.Tests` over `PostgresFixture` (12 tests); Web-shape pins
  in `Kumunita.Web.Tests` over the NSubstitute controller harness (10
  tests across U02–U04). No e2e — a pager is server-rendered markup.

- **Paging is display-only, never an access input (C-M7·2).** A page
  number never appears in an `IAuthorizationService` call, an `Audience`
  evaluation, or an `AccessAudit` row's identity — the ADR 0064 `?view=`
  display-selector precedent. One page, one aggregate audit row
  (C-M7·1) extends C-M3·3 to paged visits; the paged visit emits exactly
  the same audit shape as an unpaged one.

- **Zero new authorization surface, zero schema change.** No new
  `AccessAction`, no new `AccessVia`, no new `Decide()` branch, no new
  adapter, no new bounded context, no new document, no new index — this
  is a read-lane and a UI lane on the frozen seams (the ADR 0006 /
  ADR 0004 §B discipline). The frozen-surface rule is honoured: the
  `HasMore` signal (record return / `HasMore` field) / the new paged read
  seams are compatible ADDs; the tag lane's non-paged
  `ListPostsByTagAsync` / `ListPagesByTagAsync` were the pre-M7 shape and
  were retired once the paged pair became the lane's only read — no
  non-paged call site ever survived.

## Consequences

- **Every list surface with a Next link is honest.** A 31st post /
  event / to-do / board / goal / project / announcement / tagged post /
  tagged page is reachable; the pager never claims a page count that
  can't be computed without an audit-lane leak (F1–F7).
- **The two rendered leaks go** (U03): the "N in this community; M shown
  to you" phrasing is removed from `Posts/Index.cshtml` and
  `Groups/Detail.cshtml` — `HiddenCount` still signals hidden existence;
  the *count* no longer reaches the viewer.
- **The `FeedResult.Total` field is finally what its name says** — the
  pre-decision candidate count — and is pinned (C-M7·7) as a diagnostic
  number, never a viewer-facing one. The (A) semantics (component count,
  31 — not the page-local 30) are locked over the plan's conflicting
  test body; the drift log records it.
- **A new paged surface is two lines of Core + one partial tag** — the
  `HasMore` signal (D1/D3 shape) + the `_Pager` drop-in (D5 shape); the
  `PagedViewModel.ForRoute` factory keeps the wiring mechanical.
- **M8 (search) starts from the locked text:** the filter set is frozen
  (D9, design doc §8 inventory); M8 reuses the `HasMore` signal + the
  `_Pager` partial (no new pager idiom) and adds text-based filters on
  top of the same `page` param discipline.
- **Close (U05):** README Roadmap + `Milestones.cs` flip (M7 → Done, M8
  → Next; `MilestonesTests.cs` kept green per the AGENTS.md parity
  contract); `ARCHITECTURE.md` "shape of the code" gains the shared
  pagination idiom note.
