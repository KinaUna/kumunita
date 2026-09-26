# M8 handoff notes

One `## U#` section per unit, **appended, never rewritten** (the shared
scratch tier of the three-tier contract — see the plan header). Each entry:
files written/touched, decisions locked or refined (with the design-doc /
ADR section it maps to), anything that drifted from the plan's text, and the
next unit's entry reads.

## U00 — design doc + ADR 0091

- **Files written:** `docs/design/m8-search-design.md` (all sections:
  Context / §2 verified-reuse list / D1–D8 locked / C-M8·1–7 / F1–F6 /
  Parts affected / Rollout / Risks / the three acceptance tests + Part 2:
  the exact C# of `SearchModels.cs` / the per-surface candidate predicates /
  the audit-row shape / the 24 pinned test names / the 11 `kw-l` keys /
  drift-guard with 3 entries) + `docs/adr/0091-search.md` (Accepted,
  2026-09-26) + one index row in `docs/adr/README.md`.
- **ADR number:** **0091 confirmed free** (index ran 0001–0090; `0091`
  appeared only in the M8 plan text).
- **Veto window closed:** D1–D5 **confirmed by the user 2026-09-26** (the
  register's "Open veto" block is retired; D6–D8 were never open —
  they follow from D1–D5 + the frozen-surface discipline).
- **Drift log (3 entries, design doc §2.6):** (1) the plan's D5 ADR-0018
  quotation corrected to the exact text (decision unchanged); (2) the
  audit row locked to the aggregate shape with `TargetKind =
  "search:<surface>"`, `TargetId = null` — the plan's D7 sketch had also
  named `TargetId` as the surface carrier, which would have broken the two
  stored `AccessAudit` shapes (verified on the doc at
  `Authorization/AccessAudit.cs`); (3) `Via` locked to the dominant
  standing enum value (`Audience` / `Group`) — the plan's D7 sketch
  wrote `Via: "service"`, which is not an `AccessVia` value (the frozen
  enum verified at `Authorization/Decision.cs`).
- **Verified against the actual files (U01's copy-from list):** the
  `PostService.ListFeedAsync` candidate predicate + early-return +
  `CanSeeAsync` shape (`Posts/PostService.cs:87`); the announcement
  predicate (`Announcements/AnnouncementService.cs:87`); the
  `AccessAction` record shape (`Authorization/AccessAction.cs` —
  `Action = AccessAction.Read.Id` in the audit row); the `AccessVia` /
  `AccessOutcome` frozen enums (`Authorization/Decision.cs`); the ADR 0018
  exact text (Context + Decision + Consequences).
- **No code touched:** nothing under `src/` or `tests/` was modified; no
  build was run. **U01 entry reads:** the design doc §2 (the verified
  reuse list) + §3 D6/D7 + **Part 2 §2.1–2.4 (the exact C# — the pinned
  records, the six candidate predicates, the audit-row shape, the 14
  pinned test names)** + §4 (C-M8·1/2/3/7) + this section.

## U01 — Core seam (`ISearchService` + `SearchService` + DI + 14 tests)

- **Files written (the 5 U01 deliverables):**
  - `src/Kumunita.Core/Search/SearchModels.cs` — `SearchHit`,
    `SearchResults`, `SearchSurfacePage`, `SearchScope` (verbatim §2.1).
  - `src/Kumunita.Core/Search/ISearchService.cs` — `SearchAsync` +
    `SearchSurfaceAsync` (verbatim §2.1 / D8).
  - `src/Kumunita.Core/Search/SearchService.cs` — the store-composing
    implementation.
  - `src/Kumunita.Core/DependencyInjection.cs` — one `AddTransient<ISearchService>`
    factory line (composes `IDocumentStore` + `IAuthorizationService` +
    `IUserInfoService`).
  - `tests/Kumunita.Core.Tests/SearchServiceTests.cs` — the 14 pinned tests
    (§2.4) + helpers.
- **Build + tests:** `dotnet build Kumunita.slnx -c Debug` clean (Core
  compiles; Web has only pre-existing warnings). The 14 pinned tests run
  green via the AGENTS.md path —
  `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
  -class "Kumunita.Core.Tests.SearchServiceTests"` → **Total: 14, Errors: 0,
  Failed: 0** (xunit.v3 runner; `dotnet test` / Test Explorer discovery is the
  known-broken path, not a real failure).
- **Candidate predicates (copied from the named canonical methods, D6/§2.2):**
  - community posts — `PostService.ListFeedAsync`: `GroupId == "" &&
    DeletedAt == null && !IsDraft`, `OrderByDescending Created`.
  - group posts — `PostService` group-feed: `GroupId == <group> && DeletedAt ==
    null && !IsDraft`, membership-gated via `GetGroupIdsAsync` +
    `CanSeeGroupFeedAsync`.
  - community events — `EventService.ListUpcomingAsync`: `GroupId == "" &&
    !IsDeleted && !IsDraft`.
  - group events — same shape: `GroupId == <group> && !IsDeleted && !IsDraft`,
    membership-gated.
  - pages — `IPageService` canonical: `!IsDraft && !IsDeleted`.
  - announcements — `AnnouncementService.ListVisibleAsync` flat branch:
    `!IsDraft` (in query) + `Scope == Public || (authed && Scope == Community)`
    (flat scope check only — search has no roles/CommunityId/admin branch).
- **Frozen seams used (no new seams):** caller-session overloads
  `IAuthorizationService.CanSeeAsync(actor, Read, candidates, session)` and
  `CanSeeGroupFeedAsync(actor, groupId, count, session)` (the *caller
  commits* form) + `GetGroupIdsAsync(actor)`. Anonymous + zero-candidate
  visits call **none** of these (D7 "no row").
- **Audit-mechanism decision (D7 / C3, the crux):** `CanSeeAsync`/
  `CanSeeGroupFeedAsync` emit their **own** rows with the *adapter's*
  `TargetKind` ("post"/"event"/"page") into Search's session. Search then
  stores its **own** aggregate row — `TargetKind = "search:<surface>"`,
  `TargetId = null`, `VisibleCount`/`HiddenCount` set, `Via` = dominant
  standing (Audience / Group), `Outcome = Allow iff visible.Count > 0` — in
  the **same session**; ONE `SaveChangesAsync` commits the read + all rows
  (C3 "same transaction as the read"). The tests read with
  `Where(a => a.TargetKind == "search:<surface>")`, so only the search rows
  count; the frozen-seam per-item rows are present but correctly ignored.
- **Drift (1 new entry → design doc §2.6 entry 4):** the design's `ILIKE`
  match (D4/§2.2) is **not** in Marten 9.31.2's LINQ surface (verified by
  scanning the installed `Marten.dll` + `docs/marten/querying.md`). Locked
  resolution: the canonical (non-match) predicate stays in the Marten query
  (C-M8·2 holds at the query layer); the case-insensitive substring match is
  applied in C# over Title + Body (`OrdinalIgnoreCase`) after load, and the
  visible set is paginated in C#. Behavior is test-pinned (tests 1, 13) and
  mechanism-independent.
- **Constants (D8):** `PageSize = 20`, `MaxPerSurface = 5`,
  `TruncationRadius = 120`; `surface=all` → top 5 per surface (no pager),
  `surface=<one>` → paged, `HasMore` the sole signal, page floors to 1.
- **U02 entry reads:** the Web surface is next — `SearchController` + the
  search view + the nav box + the 11 `kw-l` keys (design doc §2.5 / ADR
  0091 FACES). This Core seam is the only surface it may call; `q`/`page`/
  `scope`/`surface` are display-only (C-M8·5) and never inputs to
  `IAuthorizationService` or the audit identity. **Do not start U02 from
  this unit** — U01 stops here.

## U02 — Web surface (`SearchController` + view + nav box + 11 keys)

- **Files written (the 4 U02 deliverables):**
  - `src/Kumunita.Web/Controllers/SearchController.cs` — one action
    `Index(string? q, string? surface, string? scope, int? page)` on
    `[HttpGet("/search")]`; no `[Authorize]` (F1 — anonymous can search the
    community scope). Consumes the frozen `ISearchService` seam only
    (`SearchAsync` on `surface=all`, `SearchSurfaceAsync` on a single
    surface). `surface` normalizes: unknown → `all`. `scope` + signed-in
    gate: `wantsGroups && User.Identity.IsAuthenticated ? Groups : Community`
    (F6 — anonymous silently degrades to the community scope, no 403).
    `page` floors to 1 (`page is > 0 ? page : 1`). Blank `q` short-circuits
    to an empty `SearchResults`/`SearchSurfacePage` **without** a service
    call (test 1's contract: the empty state is a render decision, not a
    query). The pager is built only when `sp.HasMore || pageNum > 1`, via
    `PagedViewModel.ForRoute("/search", sp.Page, SearchService.PageSize,
    sp.HasMore, {q, surface, scope})` — the ADR 0090 D7 filter-preservation
    shape (the pager links carry `q` + `surface` + `scope`).
  - `src/Kumunita.Web/Models/SearchIndexViewModel.cs` — the render model
    (record: `Q`, `Scope`, `Surface`, `Page`, `Sections`, `PageHrefs`,
    `Pager`). `IsAll` = `surface=all`. `HrefFor(SearchHit)` resolves the
    detail href: `pages` → `PageHrefs` map (path-derived); group-scope hit
    → `/groups/{GroupId}/{surface}/{Id}`; else `/{surface}/{Id}`.
  - `src/Kumunita.Web/Views/Search/Index.cshtml` — the one search page.
    Fixed surface order posts→events→pages→announcements; a surface absent
    from `Sections` (zero visible hits) renders nothing (C-M8·2/4). Each
    section headed by `search.section.<surface>`; on `all`, the heading is a
    "see all" link to `/search?q=…&surface=<s>&scope=…`. Hits render title
    (linked) + the raw `BodyExcerpt` (Razor auto-escapes — risk #4). The
    scope `<select>` is resident-only (anonymous = community, no mislead).
    Blank `q` → `search.empty.hint`; non-blank + no hits → `search.no-results`
    + the echoed `q` (C-M8·4 — no count, only the query string).
  - `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — the nav search box
    (D1 — one entry point): a GET `<form action="/search">` with a
    `<input name="q">`, placed between the nav `<ul>` and
    `<partial name="_AccountNav" />` (the nav block; the existing links do
    not reflow — the box is a sibling `<form>`, not a nav item). The
    placeholder is resolved server-side via
    `EffectiveLanguageCode.ResolveAsync` +
    `ITranslationProvider.GetAsync("search.placeholder", _kwL)` (the
    ADR 0072 pattern — `<kw-l>` can't sit inside an attribute).
- **The 11 `kw-l` keys × 4 languages (design §2.5):** added to
  `KnownTranslationKeys` `EnValues`/`DeValues`/`FrValues`/`DaValues`
  (`search.nav`, `search.title`, `search.placeholder`, `search.no-results`,
  `search.section.posts/events/pages/announcements`,
  `search.scope.community/groups`, `search.empty.hint`). The
  `KnownTranslationKeys_ParityTests` enforces the exact en==de==fr==da key
  set with no empty values — all 11 land non-empty in all four dicts.
- **Drift (2 notes — NOT design-doc rewrites):**
  1. **Page-hit hrefs are path-derived, not id-derived** (the design's
     §9 handoff named the route as `/pages/{**path}`). The controller
     injects `IPageService` and calls `GetTreeAsync()` **once** (no args)
     per `surface=all` request that has page hits, building an `id →
     PagePaths.Href(byId, page)` map; the view model carries it as
     `PageHrefs` and `HrefFor` resolves it. This is a *display projection*
     (the visibility decision already ran in the service) — the `SearchHit`
     record shape from Part 2 §2.1 is unchanged (U01's frozen seam is
     untouched). The first draft wrongly rewrote the `SearchHit` records
     client-side; removed in favor of the side-map.
  2. **Placeholder localization** uses the ADR 0072 server-side `GetAsync`
     pattern in **both** the layout nav box and the view's in-page search
     form (`kw-l` can't emit into an attribute). The layout gained
     `@inject ITranslationProvider` + `@inject ILocalizationService` +
     `@using Kumunita.Web.Security` (for `EffectiveLanguageCode`) — none of
     these were previously present in `_Layout.cshtml`.
- **Build + tests (all green):**
  - `dotnet build Kumunita.slnx -c Debug` → **0 errors** (2 pre-existing
    warnings in `ProjectsController`/`BoardDetail`, unrelated to U02).
  - `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
    → **Total: 476, Errors: 0, Failed: 0** — includes
    `KwLRegistryConsistencyTests` (every literal `key="…"` in a `.cshtml`
    under `Views/` resolves in `EnValues` — the check that pins the 11 new
    `search.*` keys as registered, since the new view + layout now reference
    them) and `MilestonesTests`. **No `SearchControllerTests` exist yet** —
    the mocked search Web suite is U03's deliverable; U02 is the surface
    under it.
  - `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
    -class "Kumunita.Core.Tests.KnownTranslationKeys_ParityTests"` →
    **Total: 7, Errors: 0, Failed: 0** — the four-dict parity the U02 key
    insertion must satisfy.
- **App smoke (live, on the dev Postgres 5433 container):**
  - `/search?q=community` (anonymous) → `Pages` section renders; each hit
    links to its **path-derived** route (`/pages/system/help/notifications`,
    `/pages/system/help/moderators`, …) — the `PageHrefs` projection works
    end-to-end. The section heading "see all" link carries `q=community` +
    `surface=pages` + `scope=community` (ADR 0090 D7). Nav box visible.
  - `/search?q=zzzqqqxyyxwvut` (anonymous) → the empty-results state:
    `No results for zzzqqqxyyxwvut` (the localized `search.no-results` +
    the echoed `q` — no count shown, C-M8·4).
  - `/search?q=the&surface=events&scope=community` (anonymous) → the
    single-surface shape; the seeded community event set is ≤ 4 (no `>20`
    visible-surface in the seed data), so the `_Pager` partial is **not**
    exercised against the live app — the ADR 0090 D7 filter-preservation on
    the pager links is instead pinned by the mocked Web test in U03
    (`Search_Index_SingleSurface_RendersPager_WithPageAndQInLinks`). This
    is the correct place for that assertion (a live-app exercise would
    require a data mutation the plan does not ask for).
- **Scope / F6 / F1 confirmed on the live page:** the nav box and the
  in-page form are visible to anonymous (F1); the in-page scope `<select>`
  is **not** rendered to anonymous (F6 — only the signed-in branch renders
  the community/groups picker; anonymous is community-scope on the server
  side via the controller's `effectiveScope` gate).
- **U03 entry reads:** the mocked Web test suite (design §2.4, tests 1–10 —
  `SearchControllerTests` over an NSubstitute `ISearchService` +
  `IPageService`, the `AnnouncementControllerTests` precedent) is the next
  unit. U02's live-app smoke covered the anonymous community-scope render
  (hits, no-results, nav box, section "see all" link filters); U03 covers
  the **signed-in** group-scope render, the **pager link filter-preservation**
  (the one U02 couldn't exercise live), the **page-floor** (`page=0`/
  `page=-1` → 1), and the **escaped-rendering** pin (a hit with `<script>`
  in its excerpt must render escaped, not execute). **Do not start U03
  from this unit** — U02 stops here.

## U03 — Web tests (`SearchControllerTests`, the 10 pinned mocks)

- **Files written (1):** `tests/Kumunita.Web.Tests/SearchControllerTests.cs`
  — the 10 pinned tests from design doc §2.4 (Web pins 1–10), over an
  NSubstitute `ISearchService` + `IPageService` harness (the
  `AnnouncementControllerTests` precedent — `DefaultHttpContext`, no host,
  the `ViewResult`'s model asserted on). **No `src/` change, no Core
  interface change, no `kw-l` key change** (the U03 plan rule — tests-only).
- **Build + tests (all green):**
  - `dotnet build Kumunita.slnx -c Debug` → **0 errors** (the same 2
    pre-existing warnings in `ProjectsController`/`BoardDetail` that U02
    recorded — unrelated to U03).
  - `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
    -class "Kumunita.Web.Tests.SearchControllerTests"` → **Total: 10, Errors: 0,
    Failed: 0** (the 10 pinned tests, in order).
  - `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
    (full assembly) → **Total: 486, Errors: 0, Failed: 0** — the 476 pre-existing
    (U02's exit count) + the 10 new `SearchControllerTests`. The
    `KwLRegistryConsistencyTests` and `MilestonesTests` in that set stay green.
- **The 10 pinned tests (in the order of design doc §2.4):**
  1. `Search_Index_NoQuery_RendersEmptyState_NoServiceCall` — the blank-`q`
     short-circuit (a render decision, not a query): the view model carries
     the empty `Sections` + null `Pager` + the trimmed-blank `Q`, and the
     `ISearchService` substitute records **zero** calls (both `SearchAsync`
     and `SearchSurfaceAsync` — the `DidNotReceiveWithAnyArgs` pin — the
     legitimate use of that idiom, "the method was never called").
  2. `Search_Index_AllScope_RendersSections_NoPager` — `surface=all` renders
     per-surface sections (the mocked `SearchResults`) with `Pager` **null**
     (D1 — the search-box answer, F2 no-render); the service is driven with
     `SearchScope.Community` + the null anonymous actor.
  3. `Search_Index_SingleSurface_RendersPager_WithPageAndQInLinks` — the
     single-surface shape: with `HasMore` true the `PagedViewModel` is
     present, `BaseUrl == "/search"`, `PageSize == SearchService.PageSize`
     (20), `HasNext` mirroring the seam's `HasMore` (D1) and `HasPrevious`
     false (page 1, D5); **`FilterParams` carries `q` + `surface` + `scope`**
     (the ADR 0090 D7 filter-preservation rule the `_Pager` partial renders
     into its links — the one U02's live-app smoke could not exercise; this
     is the pin that owns it). The service is driven with the signed-in
     subject id (the `subj-resident-001` claim) — the subject-claim pin is
     the Web-half of the D3 signed-in read.
  4. `Search_Index_ScopeGroups_SignedIn_RendersGroupHits` — a signed-in
     `scope=groups` request drives the service with `SearchScope.Groups`
     + the subject id; the group-scope hits (a group post + a group event,
     each carrying its `GroupId`) render, the model's `Scope` reflects the
     scope actually served, and `HrefFor` resolves the group-scope hit to
     its `/groups/{gid}/{surface}/{id}` route (the `SearchIndexViewModel`
     projection — a pure Web-side read).
  5. `Search_Index_ScopeGroups_Anonymous_CommunityOnly` — the silent
     degradation (D3/F6): an anonymous `scope=groups` request returns a 200
     view (not a 403, not a redirect), the service is driven with
     `SearchScope.Community` + the null actor, and the model's `Scope`
     reflects `Community` (the view never shows a groups-scope affordance to
     a guest). *Harness note:* see the drift entry below for the first
     draft's `DidNotReceiveWithAnyArgs` trap.
  6. `Search_Hit_BodyTruncated_RenderedEscaped` — the `<script>`-in-Title/
     BodyExcerpt XSS pin. The view emits the model through Razor's `@`
     (`@h.Title` / `@h.BodyExcerpt` in `Views/Search/Index.cshtml`), which
     applies the `HtmlEncoder`; this pin drives the same encoder over the
     model value and asserts the escaped entity form is present and the raw
     tag is not, *plus* reads the view's source to confirm the `@h.Title`
     / `@h.BodyExcerpt` shape is the rendering path (the
     `PublicLocaleAndAboutTests` / `StaticPagesSP_U04Tests` source-read
     pattern, walked up to the repo root via `Kumunita.slnx`). The Core
     seam's own truncation pin is U01's `Search_BodyTruncated_AroundFirstMatch`
     shape; this is the Web-half (the render is escaped, not the store).
  7. `Search_Index_PageFloor_FloorsToOne` — `page=0` and `page=-1` both
     floor to 1 at the controller (the D5 discipline — the Web side does
     not pass a sub-1 page through). The service receives the floored page
     (1) in both iterations of the loop; `Received(1)` with the exact
     floored arg + `ClearReceivedCalls()` between iterations is the precise
     pin.
  8. `Search_NavBox_Rendered_ForAnonymous` — F1 (anonymous can search): the
     `SearchController` renders a 200 view for a signed-out visitor; the
     `_Layout.cshtml` nav carries the unconditional search box (a
     `<form action="/search" method="get">` + `<input type="search" name="q">`
     — the U02 deliverable's markup, verified by a source read); the
     `search.nav` + `search.placeholder` keys the box's placeholder resolves
     through are registered non-empty in `KnownTranslationKeys.EnValues`
     (the `KnownTranslationKeys_ParityTests` in Core enforces the four-
     language parity; this pin is the Web-side registration shape).
  9. `Search_NavBox_Rendered_ForSignedIn` — F1 parity: the same nav box
     renders for a signed-in resident (the box is unconditional layout
     markup, not a conditional `@if` on `User.Identity` — the pin is its
     presence alongside a successful signed-in search render, so a future
     reflow that gates the box on sign-in breaks this).
  10. `Search_EmptyResults_RendersLocalizedNoResults` — C-M8·4 (no hidden-
     count leak): a non-blank `q` with zero visible hits renders the
     localized `search.no-results` text + the echoed `q` (the view's
     `<code>@Model.Q</code>` shape — verified by a source read), and the
     `SearchIndexViewModel` record carries **no** `Total`/`HiddenCount`/
     `CandidateCount` field at all (the hidden count lives on the stored
     aggregate `AccessAudit` row, never the render surface — the pin is the
     type shape, asserted via `typeof(SearchIndexViewModel).GetProperties()`).
- **Drift (1 entry, harness note — not a design-doc rewrite):** the
  `DidNotReceiveWithAnyArgs` trap in test 5. `DidNotReceiveWithAnyArgs`
  matches **any** call to the method regardless of argument matchers — the
  NSubstitute idiom is `DidNotReceive()` (with `Arg.Any` for the args you
  want to ignore, `Arg.Is`/`Arg.Equal` for the args you want to constrain).
  The first draft's
  `DidNotReceiveWithAnyArgs().SearchAsync(Arg.Any<string>(), Arg.Is<SearchScope>(s => s == SearchScope.Groups), …)`
  was therefore semantically "no calls at all" and failed against the
  positive `Received(1)` call. Resolution: the positive `Received(1)` with
  the exact `Community` scope already constrains every arg the test cares
  about; the redundant negative was dropped and a comment explains the
  shape. No other test in the suite uses the negative pin (tests 1, 2 do —
  but their argument matchers are pure `Arg.Any` on every arg, which is the
  form `DidNotReceiveWithAnyArgs` is actually for — "the method was never
  called"). **This is a harness idiom, not a U02 surface defect** — the
  controller/view are unchanged.
- **U04 entry reads:** the lane close (the M7 U05 shape) is next — the
  README Roadmap + "What works" flip (M8 → `**Done.**` citing ADR 0091;
  M9 → `**In progress.**`), `src/Kumunita.Web/Milestones.cs` (M8 →
  `StatusDone`, M9 → `StatusNext` — the single-in-progress invariant),
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the M8/M9 pins), and
  `docs/ARCHITECTURE.md` (the "shape of the code" — the `Search/` bounded
  context + the seam + the audit `TargetKind`s). **Do not start U04 from
  this unit** — U03 stops here.

## U04 — Close the lane (README ↔ `Milestones.cs` ↔ `MilestonesTests.cs` parity + ARCHITECTURE.md)

- **Files written (the 4 U04 deliverables):**
  - `README.md` — the status line (M8 → **done** citing ADR 0091, M9 →
    **next**, M10–M13 tail); a new **Features** bullet for the search box +
    the `/search` surface (all/surface/scope/pager, group-scope gated on the
    frozen ADR 0013 seams, localized, zero schema change, the two deferred
    lanes named); the **Roadmap** M8 entry flips to `**Done** (ADR 0091)`
    with the full decision text + the *deliberately not in M8* follow-on
    lanes, and M9 flips to `**In progress.**` (the single-in-progress
    invariant).
  - `src/Kumunita.Web/Milestones.cs` — M8 → `StatusDone` (title gains the
    ADR 0091 summary), M9 → `StatusNext`. The single-in-progress invariant
    the `MilestonesTests` pin now points at M9.
  - `tests/Kumunita.Web.Tests/MilestonesTests.cs` — M8 added to the
    shipped-Done list; the sole-in-progress test renamed
    `M9_Is_The_Single_InProgress_Milestone_And_M10_Through_M13_Are_Planned`
    (M9 → `StatusNext`, M10–M13 → `StatusPlanned`). The M0–M13 + lanes
    order pin is unchanged (M8 already sat in order).
  - `docs/ARCHITECTURE.md` — the tree gains the `Search/` bounded context
    entry (M8 ✓ ADR 0091: `ISearchService` + `SearchService`, zero docs /
    zero schema change, the frozen-seam composition, the
    `TargetKind = "search:<surface>"` audit rows, the two deferred lanes);
    the §3 feature-modules list gains `Search (M8 ✓ — ADR 0091)`; the
    "Two projects" prose gains the M8 live-context line alongside the M6 /
    M7 entries.
- **Rules held:** no `Kumunita.Core` change, no new `kw-l` keys, no tests
  beyond the `MilestonesTests` pins (the pins were updated, not loosened —
  M9 is now the sole in-progress, M10–M13 the planned tail).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` 0 errors; full Web
  assembly green via `dotnet exec … Kumunita.Web.Tests.dll` (486+);
  `MilestonesTests` green (M8 done, M9 sole in-progress). Handoff note ends
  with the `## Summary` below; the lane then moves
  `docs/plans-milestones/in-progress/m8/` → `docs/plans-milestones/done/m8/`.

## Summary

**M8 — Search is done (ADR 0091).** The platform's first text-find
capability: a single nav search box (anonymous + signed-in, F1/F5) over the
four resident content surfaces — community **and** group posts, community
**and** group events, pages, announcements (D2). `surface=all` renders the
top 5 hits per surface (a search-box answer, no pager); a single surface
renders a paged list on the M7 `HasMore` signal + the shared `_Pager`
partial (the ADR 0090 hand-off — no new pager idiom), pager links carrying
`q` + `surface` + `scope` (ADR 0090 D7 filter-preservation). `scope=groups`
is signed-in-only and rides the **frozen** ADR 0013 group seams
(`CanSeeGroupAsync` / `CanSeeGroupFeedAsync`) — a group the viewer cannot
see contributes **no hit and no count** (F2, C-M8·2); anonymous with
`scope=groups` degrades silently to community (F6, D3).

**The seams.** One bounded context, one interface (D8): `Kumunita.Core.Search`
with `ISearchService` (`SearchAsync` — the `all` read; `SearchSurfaceAsync` —
the paged single-surface read, the ADR 0090 D1/D3 record-return shape) + a
store-composing `SearchService` that composes **only** the frozen
`IDocumentStore` / `IAuthorizationService` / `IUserInfoService` (C-M8·1). The
candidate predicates are the **same expressions** the canonical reads use
(D6/§2.2), so the hit set is a **subset** of the feeds' visible sets —
search can never surface a document the feed would hide (C-M8·2/7; drafts and
soft-deletes invisible, F3). `q`/`page`/`scope`/`surface` are display-only,
never an input to `IAuthorizationService` or an `Audience` evaluation (C-M8·5).

**The audit.** One aggregate row per (query, surface, scope) decision (D7,
C-M8·3): `TargetKind = "search:<surface>"` (`search:posts` / `search:events`
/ `search:pages` / `search:announcements`), `TargetId = null`,
`VisibleCount`/`HiddenCount` set, `Via` = the dominant standing
(Audience / Group), `Outcome = Allow` iff the visible set is non-empty —
inside the two stored `AccessAudit` shapes (drift-guard entry 2).
Zero-candidate surfaces and anonymous visits emit **no** row (the C-M7·5
"read, not a decision" pin extended). `HiddenCount` is stored, never rendered
(C-M8·4; the C-M7·7 pin).

**The engine (and the one drift).** Case-insensitive substring match over
authored-in `Title` + `Body` (D5 — translations excluded, ADR 0018's
lane), a truncated HTML-escaped context window (radius 120, D4), **zero
schema change** — no index, no migration, ADR 0004 §B untouched (C-M8·6).
One recorded drift (design doc §2.6 entry 4): the `ILIKE` match is not in
Marten 9.31.2's LINQ surface — the canonical (non-match) predicate stays in
the Marten query and the case-insensitive substring match is applied in C#
(`OrdinalIgnoreCase`) after load. Behavior is test-pinned and
mechanism-independent.

**The pins.** 14 Core pins (`SearchServiceTests`, `PostgresFixture`) +
10 Web pins (`SearchControllerTests`, NSubstitute — no Postgres) — all
green. The Web suite also gained the `KwLRegistryConsistencyTests` coverage
of the 11 new `search.*` keys (all four languages, `KnownTranslationKeys_ParityTests`
enforcing).

**The two deferred lanes (named, not guessed).** (1) **language-scoped
search** — the ADR 0018 consequence ("a future language-scoped search"),
matching the `PostTranslation` / `EventTranslation` / … rows; M8 does not
pre-empt it. (2) **`tsvector` full-text** — a versioned migration by
construction, its own ADR, landing at the scale where substring matching
stops being honest. Both land without re-designing this one, because the
seam is one interface (D8) and the engine is one predicate per surface (D6).

**Gate (U04 exit).** `dotnet build Kumunita.slnx -c Debug` 0 errors;
`Kumunita.Web.Tests` full assembly green (486+ via the in-process xunit.v3
runner — `dotnet test` discovery is the known-broken path, not a real
failure); `MilestonesTests` green (M8 done, M9 sole in-progress, M10–M13
planned); the README Roadmap + Features + `Milestones.cs` +
`MilestonesTests.cs` + `ARCHITECTURE.md` all in parity. Lane moved
`in-progress/m8/` → `done/m8/`.

