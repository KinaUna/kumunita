# Plan: M8 — Search

> **In progress.** Unit register (secondary tier). Living handoff note:
> `docs/plans-milestones/in-progress/m8/m8-handoff-notes.md` (scratch tier — one
> `## U#` section per unit, appended, never rewritten). The authoritative design
> (primary tier) — `docs/design/m8-search-design.md` (the `m7-…-design.md`
> naming) — is authored by **U00** and
> locked before any code unit runs; the decision record is **ADR 0091**.
> **U00 is the sign-off gate** for the design decisions and the invariant/FACES
> contract; the decisions below are the [PROPOSED] set U00 locks (or the user
> vetoes before U00 runs — this register is the last cheap place to change
> them).
>
> **Atomicity contract.** This register is sized for **~32K-context agents**:
> every unit is one coherent step — ≤ 5 files, ≤ ~400 LOC of change, a short
> entry-reads list (3–6 files), and an Exit check that fits in one build +
> test run. A unit's full context (this file + its entry reads + its
> deliverables) fits in one 32K window with headroom. **Unit-series rule:
> never touch files outside your own Deliverables; never rewrite the design
> doc outside the drift-guard note; no tests beyond the pinned list; no new
> seams on frozen interfaces.**
>
> M8 is **greenfield**: there is no search box, no `/search` route, no `?q=`
> parameter anywhere yet (grep-confirmed). M7 (pagination/filtering) shipped
> the shared `HasMore` + `_Pager` idiom and a frozen filter inventory
> (ADR 0090 D9); ADR 0090's Consequences hand M8 exactly this: "M8 reuses the
> `HasMore` signal + the `_Pager` partial (no new pager idiom) and adds
> text-based filters on top of the same `page` param discipline." M8 starts
> from that locked text.

---

## Understanding

Kumunita has five content surfaces (posts, events, pages, announcements,
projects) and no way to find content by text. Every list surface is already
audience-filtered and paged; search must **compose the frozen authorization
seams exactly like the canonical reads do** — never widen a visibility set,
never leak a hidden count, audit every decision. Scope decisions (which
surfaces, which engine, whether group content is in scope) are [PROPOSED]
below and lockable by ADR 0091 in U00.

The platform is single-neighborhood (hundreds-to-thousands of documents, not
millions): a case-insensitive substring search over the existing columns is
honest, needs **zero schema change** (the ADR 0090 "zero schema change"
discipline; ADR 0004 §B would force any index into a versioned migration),
and is the right ceiling for M8. Full-text (`tsvector`) is a follow-up lane,
not M8.

## Assumptions / decisions — [PROPOSED, lockable by ADR 0091 in U00]

> **Open veto (M7 pattern).** These are the decisions the user can still
> change cheaply — **before U00 runs**. After U00 they are locked by
> `docs/design/m8-search-design.md` + ADR 0091 and changeable only via the drift guard.

- **D1 · Surface — one `/search` page (global search box), not per-feed `?q=`.**
  A single nav search box; results render as per-surface sections. `all`
  (default) shows the top **5** hits per surface (a search-box answer, no
  pager); a single-surface view (`surface=posts|events|pages|announcements`)
  renders a **paged** list — `page`/`?page` discipline, `HasMore` + `_Pager`
  (ADR 0090 consequences: "no new pager idiom"). Alternative considered and
  rejected for M8: per-feed `?q=` filters on every existing list (bigger blast
  radius — touches 5 controllers + 5 views; the dedicated surface is the
  README milestone's shape, "Search").
- **D2 · In scope: posts (community **and** group), events (community), pages,
  announcements. Out of scope: projects, todo items, boards, goals, directory,
  tags, groups (as entities).** The project-family surfaces are M5's scope
  (roadmap) — adding them to search is a one-line predicate when that
  milestone wants it, not M8's. Group events are in via the group scope (D3).
- **D3 · Scopes.** `scope=community` (default; anonymous allowed) and
  `scope=groups` (signed-in only; group posts + group events of the viewer's
  **visible** groups, via the frozen `CanSeeGroupAsync` /
  `CanSeeGroupFeedAsync` — never a new authorization surface). Anonymous with
  `scope=groups` → community-only results, no 403 (the audience filter is
  display, ADR 0090 D9/C-M7·2 discipline). Group membership is the access
  boundary (ADR 0010/0013).
- **D4 · Engine: case-insensitive substring match (Postgres `ILIKE`
  `%(q)%`) over `Title` + `Body`.** No `tsvector`, no new index, zero schema
  change (ADR 0004 §B stays untouched). Body hits render a **truncated
  context window** (~120 chars around the first match, HTML-escaped).
  `tsvector`-based FTS is a future lane (it would be a versioned migration).
- **D5 · Matching text: authored-in text only.** UGC translations
  (`PostTranslation`, `EventTranslation`, …) are **not** searched in M8 —
  cross-language matching is a follow-up lane under the ADR 0018 language
  scope (the ADR notes `LanguageCode` is "safe to use for search" — that is
  the lane's entry, not M8's work).
- **D6 · Authorization = the frozen seams, the canonical predicates.**
  Candidates are pre-filtered with the **same** predicates the canonical
  reads use (drafts/soft-deletes/group scope — e.g. posts:
  `p.DeletedAt == null && !p.IsDraft`; events: `!e.IsDeleted && !e.IsDraft &&
  e.GroupId == string.Empty`; announcements: `!IsDraft` + scope rule), then
  the per-page candidate set goes through `IAuthorizationService.CanSeeAsync`
  (community) or `CanSeeGroupAsync`/`CanSeeGroupFeedAsync` (groups) exactly
  like `PostService.ListFeedAsync` does. **No new `AccessAction`, no new
  `AccessVia`, no new adapter** (ADR 0090 consequence: "zero new
  authorization surface").
- **D7 · Audit: one aggregate row per (query, surface, scope) decision.**
  Same shape as the M3/M7 aggregate pins: `Via: "service"`,
  `Action: AccessAction.Read`, `TargetKind: "search:posts" | "search:events" |
  "search:pages" | "search:announcements"`, `VisibleCount`/`HiddenCount`,
  `Outcome` (C-M3·3, C-M7·1). A surface section with **zero candidates** emits
  **no** row (C-M7·5). `HiddenCount` never renders (C-M7·7).
- **D8 · Seam shape: one new bounded context `Kumunita.Core.Search` with one
  seam** — `ISearchService` (interface, so the Web controller tests substitute
  without Postgres — the `IPageService`/`IAnnouncementService` convention) +
  a store-composing `SearchService` registered in `DependencyInjection.cs`.
  Records: `SearchHit`, `SearchResults` (per-surface, carrying `HasMore` —
  the ADR 0090 D1/D3 record-return signal). The service composes only
  `IDocumentStore`, `IAuthorizationService`, `IUserInfoService` (frozen).

## Invariants — [PROPOSED, U00 locks into the design doc]

- **C-M8·1 · Read-only, frozen seams.** Zero writes. No new `AccessAction`,
  `AccessVia`, adapter, or document. Only the frozen `IAuthorizationService`
  surface (ADR 0006 / ADR 0090 "zero new authorization surface").
- **C-M8·2 · No visibility widening.** Search candidates are pre-filtered by
  the canonical predicates (D2/D6); the post-decision visible set is a subset
  of what the corresponding feed would show. A search can never surface a
  document the feed would hide.
- **C-M8·3 · Audit is always-on, in-transaction, aggregate-shaped (D7).**
  One row per decision; zero-info rows suppressed (C-M7·5).
- **C-M8·4 · No hidden-count leak.** Search results render only hits +
  `HasMore` — never `Total`/`HiddenCount` (C-M7·7).
- **C-M8·5 · `q`/`page`/`scope`/`surface` are display-only** — never an input
  to an `IAuthorizationService` call or an `Audience` evaluation (the
  ADR 0090 C-M7·2 discipline extended).
- **C-M8·6 · Zero schema change.** No new index, no new document, no
  migration (ADR 0004 §B untouched).
- **C-M8·7 · Drafts and soft-deletes are invisible.** No draft, deleted, or
  `IsDeleted` document is ever a search hit (C1: author control is absolute).

## FACES — [PROPOSED, U00 locks]

- **F1 · Anonymous can search** community posts/events/pages/announcements —
  the search box renders in the nav for guests, parity with the public feed
  surface.
- **F2 · Group content is membership-gated.** Group posts/events appear only
  to viewers who pass `CanSeeGroupAsync`/`CanSeeGroupFeedAsync`; a non-member
  never sees a hit **or a count** of one (C1).
- **F3 · Drafts never leak.** A search for a draft's exact title still
  returns nothing (C7).
- **F4 · Every decided visit audits; empty searches don't.** (C-M8·3)
- **F5 · The search box is keyboard-operable and localized** (`kw-l` keys,
  all four languages — `KnownTranslationKeysTests` enforces this).
- **F6 · Scope degradation is silent.** Anonymous `scope=groups` yields
  community-only results, no 403, no "you lack access" text (C-M8·2/D3).

## Approach

- **Track A — Core seam (U01):** `Kumunita.Core.Search` (D8) + the pinned
  `SearchServiceTests` (Postgres, `PostgresFixture`).
- **Track B — Web surface (U02):** `SearchController` + `Views/Search/` + nav
  box + localization keys.
- **Track C — Web tests (U03):** NSubstitute over `ISearchService` (no
  Postgres).
- **Close (U04):** README + `Milestones.cs` + `MilestonesTests.cs` flip,
  `ARCHITECTURE.md`, handoff `## Summary`.

## Workflow (three-tier, per-unit)

Same contract as the M7 register (see
`docs/plans-milestones/done/m7/plan-m7-pagination-filtering.md` §Workflow):
primary tier = `docs/design/m8-search-design.md` (authored by U00; the only authority
after it lands); secondary = this register; scratch = the handoff note (one
`## U#` section per unit: entry state / what ran / drift / open items).
Per unit: Goal → Entry reads (3–6 files) → Deliverables (≤ 5 files) → Exit
(build green + handoff entry). **Unit-series rule: never touch files outside
your own Deliverables; never rewrite the design doc outside the drift-guard
note; no tests beyond the pinned list; no new seams on frozen interfaces.**
Tests run per AGENTS.md: `dotnet build Kumunita.slnx -c Debug`, then
`dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
-class "Kumunita.Core.Tests.SearchServiceTests"` and the Web equivalent.

---

## U00 — Lock the design: `m8-search-design.md` + ADR 0091

**Goal.** Author the primary tier and the decision record. Lock D1–D8 (or
record vetoes), the C-M8 invariants, F1–F6, the exact seam signature (D8),
the audit `TargetKind` values (D7), the truncation/cap numbers (D4: top-5
per section, page size 20, 120-char window), the pinned test names below, and
the drift log. ADR 0091 records: decisions + alternatives considered
(per-feed `?q=`; `tsvector`; projects-in-scope; group-events-in-scope) + the
Consequences hand-off (search-over-translations lane; tsvector lane).

**Entry reads (6).** `docs/philosophy/templates/design-doc.md`;
`docs/design/m7-pagination-filtering-design.md` (§§0, 1, 2, 4 — the house
style); `docs/adr/0090-pagination-and-filtering.md` (D9 + Consequences — the
M8 hand-off text); `docs/ARCHITECTURE.md` (§8); `docs/adr/0001-core-stack-and-identity-model.md`
(C1 — audience is absolute); `src/Kumunita.Core/Posts/PostService.cs` (the
canonical read + aggregate audit shape to mirror).

**Deliverables (2).** `docs/design/m8-search-design.md`; `docs/adr/0091-search.md`.

**Exit.** `dotnet build` still green (docs only). Handoff entry: decisions
locked/vetoed, any D-item text changed, the exact seam signature as written in
the design doc (U01 copies it verbatim).

---

## U01 — Core seam: `Kumunita.Core.Search` + pinned tests

**Goal.** The D8 seam and its behavior pins. This unit owns the search
semantics: candidate predicates (C-M8·2/7), the authorization composition
(D6), the aggregate audit (D7), the truncation (D4).

**Entry reads (6).** `docs/design/m8-search-design.md` (§seams, §invariants —
the locked text); `src/Kumunita.Core/Posts/FeedResult.cs` (the `HasMore`
record shape); `src/Kumunita.Core/Posts/PostService.cs` (candidate-filter →
`CanSeeAsync` → set-filter pattern; the audit row shape);
`src/Kumunita.Core/Authorization/AccessAudit.cs`;
`src/Kumunita.Core/Pages/IPageService.cs` (interface-first service convention);
`tests/Kumunita.Core.Tests/M7PaginationSeamTests.cs` (test harness shape,
`PostgresFixture`, audit spy).

**Deliverables (5).** `src/Kumunita.Core/Search/SearchModels.cs` (the
`SearchHit` + `SearchResults` records — the `ProjectPages.cs`
multi-record-file precedent); `src/Kumunita.Core/Search/ISearchService.cs`;
`src/Kumunita.Core/Search/SearchService.cs` (composes
`IDocumentStore`/`IAuthorizationService`/`IUserInfoService` — frozen seams
only); `src/Kumunita.Core/DependencyInjection.cs` (one registration, the
`AddTransient<ISearchService>` factory shape);
`tests/Kumunita.Core.Tests/SearchServiceTests.cs`.

**Pinned tests (14 — the design doc may rename, not rescope):**
`Search_Posts_HitsTitleAndBody_CaseInsensitive` ·
`Search_Posts_NeverReturnsDraftsOrSoftDeleted` ·
`Search_Posts_AudienceRestricted_HiddenFromUnprivilegedViewer` ·
`Search_Posts_EmptyQuery_NoCandidates_NoAuditRow` ·
`Search_Posts_EmitsOneAggregateAuditRow_PerVisit` ·
`Search_GroupScope_ReturnsVisibleGroupPostsOnly` ·
`Search_GroupScope_ExcludesGroupsActorCannotSee` ·
`Search_GroupScope_Anonymous_Denied` ·
`Search_Events_CommunityFeed_Only_ExcludesGroupEvents` ·
`Search_Events_NeverReturnsDraftsOrDeleted` ·
`Search_Pages_PublishedOnly_AudienceRespected` ·
`Search_Announcements_PublicAlways_CommunityScopeRequiresAuth` ·
`Search_BodyTruncated_AroundFirstMatch` ·
`Search_PagedSurface_HasMore_HonorsPageDiscipline`

**Exit.** Build green; the 14 pins pass
(`dotnet exec … Kumunita.Core.Tests.dll -class
"Kumunita.Core.Tests.SearchServiceTests"`). Handoff entry: seam signature as
implemented, any predicate copied from a canonical read (name it), drift
(e.g. a surface whose canonical predicate differed from the design doc's
quotation — record it, don't silently fix it).

---

## U02 — Web surface: `SearchController` + view + nav + keys

**Goal.** The D1 surface rendered. Anonymous + signed-in nav search box;
`/search` with `q`/`surface`/`scope`/`page`; `all` scope renders ≤ 5 hits per
surface; single-surface renders a paged list with `_Pager` (links preserve
`q` + `surface` + `scope`, the ADR 0090 D7 filter-preservation rule).

**Entry reads (5).** `docs/design/m8-search-design.md` (§seams, §web);
`src/Kumunita.Web/Controllers/StaticPagesController.cs` (controller +
`[Authorize]` optional pattern + `PagedViewModel` use);
`src/Kumunita.Web/Views/Shared/_Pager.cshtml`;
`src/Kumunita.Web/Views/Shared/_Layout.cshtml` (the nav block — where the
search box goes); `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
(the closed-key registry — new keys need all four languages; parity is
pinned by `tests/Kumunita.Core.Tests/KnownTranslationKeys_ParityTests.cs`;
the `kw-l` tag helper is used throughout the existing views).

**Deliverables (4).** `src/Kumunita.Web/Controllers/SearchController.cs`;
`src/Kumunita.Web/Views/Search/Index.cshtml`;
`src/Kumunita.Web/Views/Shared/_Layout.cshtml` (nav box only — no reflow of
the existing nav); `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
(new keys — **all four languages**, `KnownTranslationKeys_ParityTests`
enforces).

**New keys (provisional — the design doc is authoritative):**
`search.nav` · `search.title` · `search.placeholder` · `search.no-results` ·
`search.section.posts` · `search.section.events` · `search.section.pages` ·
`search.section.announcements` · `search.scope.community` ·
`search.scope.groups` · `search.empty.hint`.

**Exit.** Build green; app smoke (`dotnet run` + browser): `/search?q=<seed>`
renders sections for an anonymous user; `_Pager` links carry the filters.
Handoff entry: keys added, nav placement, any view-model shape the controller
needed that the design doc didn't name (drift).

---

## U03 — Web tests: `SearchControllerTests` (NSubstitute, no Postgres)

**Goal.** Pin the Web shape over the substituted `ISearchService`.

**Entry reads (4).** `docs/design/m8-search-design.md` (§web, §FACES);
`tests/Kumunita.Web.Tests/AnnouncementControllerTests.cs` (the
NSubstitute-over-`IAnnouncementService` controller harness — copy its setup);
`src/Kumunita.Web/Controllers/SearchController.cs` (U02's code);
`src/Kumunita.Web/Models/PagedViewModel.cs` (the `ForRoute` factory).

**Deliverables (1).** `tests/Kumunita.Web.Tests/SearchControllerTests.cs`.

**Pinned tests (10):** `Search_Index_NoQuery_RendersEmptyState_NoServiceCall` ·
`Search_Index_AllScope_RendersSections_NoPager` ·
`Search_Index_SingleSurface_RendersPager_WithPageAndQInLinks` ·
`Search_Index_ScopeGroups_SignedIn_RendersGroupHits` ·
`Search_Index_ScopeGroups_Anononymous_CommunityOnly` ·
`Search_Hit_BodyTruncated_RenderedEscaped` ·
`Search_Index_PageFloor_FloorsToOne` · `Search_NavBox_Rendered_ForAnonymous` ·
`Search_NavBox_Rendered_ForSignedIn` ·
`Search_EmptyResults_RendersLocalizedNoResults`

**Exit.** Build green; Web tests green (`dotnet exec …
Kumunita.Web.Tests.dll -class
"Kumunita.Web.Tests.SearchControllerTests"`). Handoff entry.

---

## U04 — Close the milestone

**Goal.** Flip the roadmap state and land the doc parity (the AGENTS.md
contract — README ↔ `Milestones.cs` ↔ `MilestonesTests.cs` move together).

**Entry reads (4).** `docs/plans-milestones/in-progress/m8/m8-handoff-notes.md`
(the full unit log); `README.md` (Roadmap + "What works" — the M8 entry);
`src/Kumunita.Web/Milestones.cs` (+ `tests/Kumunita.Web.Tests/MilestonesTests.cs`
— the pins); `docs/adr/0091-search.md` (the citation for the README flip).

**Deliverables (4).** `README.md` (Roadmap: M8 → `**Done.**` citing ADR 0091;
M9 → `**In progress.**`; "What works" gains the search box + `/search`
surface); `src/Kumunita.Web/Milestones.cs` (M8 → `StatusDone`, M9 →
`StatusNext` — the single-in-progress invariant);
`tests/Kumunita.Web.Tests/MilestonesTests.cs` (the M8/M9 pins);
`docs/ARCHITECTURE.md` ("shape of the code": the `Search/` bounded context +
the seam + the audit `TargetKind`s).

**Exit.** **Everything green** (`dotnet build Kumunita.slnx -c Debug`; both
test assemblies pass in full). Handoff `## Summary` (the M7 U05 shape: the
capability, the seams, the invariants, the two deferred lanes —
translations-search, tsvector). Then move
`docs/plans-milestones/in-progress/m8/` → `docs/plans-milestones/done/m8/`.

---

## Exit (all green)

- `dotnet build Kumunita.slnx -c Debug` — 0 errors.
- `Kumunita.Core.Tests.SearchServiceTests` — 14/14.
- `Kumunita.Web.Tests.SearchControllerTests` — 10/10.
- `MilestonesTests` — green (M8 done, M9 sole in-progress).
- The handoff note ends with `## Summary`.
