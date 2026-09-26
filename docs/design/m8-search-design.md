# M8 — Search — design (Part 1 + Part 2)

> **Milestone M8.** The first text search: one nav search box, one `/search`
> surface over four content surfaces (posts, events, pages, announcements),
> community + group scopes, composed entirely on the **frozen**
> authorization seams and the M7 paging idiom. **ADR 0091** is this
> milestone's decision record — **authored in this unit (U00)**, **Accepted
> 2026-09-26**; the decisions D1–D8 are **locked** (the `[PROPOSED]` markers
> in the lane plan `plan-m8-search.md` are the pre-lock shape and are
> retired by this lock — the ADR 0090 precedent; **D1–D5 were confirmed by
> the user 2026-09-26**, closing the open-veto window). The sealed-unit
> register is
> `docs/plans-milestones/in-progress/m8/plan-m8-search.md`; the scratch log
> is `docs/plans-milestones/in-progress/m8/m8-handoff-notes.md` (one `## U#`
> section per unit, appended, never rewritten).
>
> **Status.** **LOCKED.** The decisions D1–D8 are locked in **ADR 0091
> (Accepted, 2026-09-26)**; **Part 2** (the exact C# shapes of the seam,
> the pinned test names, and the drift-guard) follows in the "Seams &
> contracts" section below.
>
> **Roadmap confirm (U00):** `M8` is the single `StatusNext` on
> `Milestones.cs` (M7 closed it that way; the ADR 0090 close record says so);
> M8 is a **milestone**, not a named lane — the close unit (U04) flips
> `Milestones.cs` / the README Roadmap / `MilestonesTests.cs` (the AGENTS.md
> doc↔code parity contract).
>
> **Scope of this file:** what this milestone is; the existing surface it
> reuses — *verified against the actual files*; the design decisions
> (D1–D8); the invariants (C-M8·1…7); the FACES (F1–F6); the parts affected;
> the risks. **Part 2:** the exact C# shape of the seam, the per-surface
> candidate predicates (D6), the pinned test names, and the drift-guard.

## 1. What this milestone is

The app has five content surfaces (posts, events, pages, announcements,
projects) and **no way to find content by text** (grep-verified: no search
box, no `/search` route, no `?q=` parameter anywhere). Every list surface is
already audience-filtered and paged — search must therefore **compose the
frozen authorization seams exactly like the canonical reads do**
(`PostService.ListFeedAsync` is the reference shape, verified at
`PostService.cs:87`): candidate pre-filter → one `CanSeeAsync` over the
page's candidates → set-filter the visible — never a new lane of its own.

M8 ships:

1. **One Core seam** (`Kumunita.Core.Search`, D8): `ISearchService` with two
   read methods — a `all`-scope read (top 5 per surface) and a
   single-surface paged read — both over a case-insensitive substring match
   (D4), both composing the frozen `IAuthorizationService` surface (D6),
   both emitting the aggregate `AccessAudit` shape (D7).
2. **One Web surface** (D1): a nav search box (anonymous + signed-in),
   `GET /search?q=…&surface=…&scope=…&page=…`.
3. **Nothing else.** No writes, no new document, no new index, no new
   `AccessAction` / `AccessVia` / adapter (C-M8·1 / C-M8·6).

**The one thing every unit must respect:** this milestone is a **read lane
and a UI lane on frozen seams** — the exact ADR 0090 discipline ("zero new
authorization surface, zero schema change"), plus the M8-specific
**no-visibility-widening** pin (C-M8·2): search candidates are pre-filtered
with the *same* predicates the canonical feeds use, so a search can never
surface a document the feed would hide.

## 2. The existing surface this milestone reuses (verified)

Read directly from the actual files (not assumed), each a **frozen seam** M8
composes without amending:

1. **`PostService.ListFeedAsync`** (`Kumunita.Core/Posts/PostService.cs:87`)
   — the canonical read M8 mirrors: `Where(p => p.ComponentId == componentId
   && p.DeletedAt == null && !p.IsDraft)` → 0-candidate early return (no
   decision, C-M7·5) → `_authz.CanSeeAsync(actorId, AccessAction.Read,
   candidates.Select(p => new PostToAuditableResource(p)))` → set-filter.
   The `TargetKind` on the aggregate audit row is `"post"`.
2. **`AnnouncementService`** (`Kumunita.Core/Announcements/
   AnnouncementService.cs:87`) — the announcement candidate predicate,
   verified: `.Where(a => !a.IsDraft && (a.Scope == AnnouncementScope.Public
   || (authed && a.Scope == AnnouncementScope.Community)))`. Note: the
   announcement lane is **flat** (no `CanSeeAsync` — `Scope` is the
   boundary; ADR 0017 family), so search replicates that predicate and
   audits per D7 rather than per-announcement.
3. **`EventService`** (`Kumunita.Core/Events/EventService.cs`) — the
   community-feed predicate, verified: `!e.IsDeleted && !e.IsDraft &&
   e.GroupId == string.Empty` (the ADR 0089 group-event exclusion is
   explicit); group events carry `GroupId` non-empty and are the group
   scope's (D3) event lane.
4. **`IPageService`** (`Kumunita.Core/Pages/PageService.cs`) — the pages
   read; a page's candidate predicate is `!p.IsDraft && !p.IsDeleted`
   (ADR 0037/0024 idioms, verified on the `Page` doc), then the **frozen**
   `PageToAuditableResource` adapter + `CanSeeAsync` (ADR 0039's "PG adds
   an adapter, not a branch" — search reuses the adapter, never a branch).
5. **The group seams** (frozen, ADR 0013) — `CanSeeGroupAsync(actor,
   groupId, targetPostId?)` and `CanSeeGroupFeedAsync(actor, groupId,
   candidateCount)` on `IAuthorizationService`; the group-post candidate
   predicate is `p.GroupId == groupId && p.DeletedAt == null && !p.IsDraft`
   (verified in `PostService`'s group-feed method).
6. **`AccessAudit`** (`Kumunita.Core/Authorization/AccessAudit.cs`) — the
   two shapes: single-target (`TargetId`) and aggregate
   (`VisibleCount`/`HiddenCount`, `TargetId` null). M8 uses the aggregate
   shape only (D7). `AccessVia` / `AccessOutcome` are frozen enums
   (`Decision.cs`) — M8 appends **nothing**.
7. **The M7 paging idiom** (ADR 0090) — the `HasMore` record-return signal
   (the `EventPage` / `AnnouncementPage` record shapes, verified), the
   `PagedViewModel.ForRoute` factory, the `_Pager` partial, and the `page`
   param discipline (floor to 1; the links carry the current filter values).
8. **`PostgresFixture`** + the `M7PaginationSeamTests` harness
   (`tests/Kumunita.Core.Tests/`) — the Core test home (D8/D10 discipline,
   the ADR 0090 D10 "split by seam" precedent).

## 3. Decisions (locked in ADR 0091)

### D1 · Surface — one `/search` page, not per-feed `?q=` (user-confirmed)

A single nav search box; `/search` renders per-surface sections.
`surface=all` (the default) shows the top **5** hits per surface — a
search-box answer, no pager. `surface=posts|events|pages|announcements`
renders a **paged** list: `?page` discipline, the `HasMore` record signal,
the `_Pager` partial — **no new pager idiom** (the ADR 0090 Consequences
hand-off, verbatim). The rejected alternative (per-feed `?q=` filters on all
five list surfaces) is recorded in ADR 0091: it touches 5 controllers + 5
views and no dedicated milestone; the README milestone shape is "Search"
itself.

### D2 · Scope of surfaces (user-confirmed)

**In:** posts (community **and** group), events (community feed + group
events via D3), pages, announcements. **Out:** projects, todo items,
boards, goals, the directory, tags, groups as entities. The project-family
surfaces are M5's scope (roadmap); adding them is a one-line predicate when
that milestone wants it.

### D3 · Scopes — `community` (default) and `groups` (user-confirmed)

`scope=groups` requires a signed-in actor and returns group posts + group
events of the viewer's **visible** groups, decided by the frozen
`CanSeeGroupAsync` / `CanSeeGroupFeedAsync` (D6). **Anonymous with
`scope=groups` degrades silently to community-only** results — no 403, no
"you lack access" text: group membership is the access boundary
(ADR 0010/0013) and the audience filter is *display*, never a refusal
surface (the C-M7·2 discipline extended). A group the viewer cannot see
contributes **zero** hits — not a hidden hit, not a count.

### D4 · Engine — case-insensitive substring, zero schema change
(user-confirmed)

Postgres `ILIKE '%' || @q || '%'` over `Title` (where the surface has one)
+ `Body`. No `tsvector`, no GIN index, no migration — the ADR 0004 §B
versioned-DDL lane is untouched because nothing is DDL. The match is
**authoritative over the stored (Markdown/HTML) text** — a search for
`bold` matches a body containing `**bold**`; that is honest for
single-neighborhood scale and the FTS lane (future, its own ADR) may
replace it. Body hits render a **truncated context window**: up to
`TruncationRadius = 120` characters before and after the first match,
HTML-escaped at render, `…` markers where truncated. A title match renders
the title (no window). Empty / whitespace `q` → the Web layer renders the
empty state and **never calls the service** (no decision, no audit row —
the C-M7·5 shape extended to `q`).

### D5 · Matching text — authored-in only (user-confirmed)

The stored `Title`/`Body` in the row's authored language. UGC translation
rows (`PostTranslation`, `EventTranslation`, `PageTranslation`,
`AnnouncementTranslation`) are **not** searched. ADR 0018 already records
`LanguageCode` as "safe to use for search" and names a "future
language-scoped search" — that is the follow-up lane's entry, not M8's
work. (Drift log entry 1 records the wording.)

### D6 · Authorization — the frozen seams, the canonical predicates

Per surface, the candidate predicate is the **same expression** the
canonical read uses (verified list in §2):

| surface | candidate predicate (pre-filter) | decision |
|---|---|---|
| posts (community) | `p.DeletedAt == null && !p.IsDraft && p.GroupId == string.Empty` | `CanSeeAsync` + `PostToAuditableResource` (as `ListFeedAsync`, but over the search predicate — the component filter is replaced by the match; the *access* path is unchanged) |
| posts (group) | `p.GroupId == groupId && p.DeletedAt == null && !p.IsDraft` | `CanSeeGroupAsync` per (group, post), as the group-feed read does |
| events (community) | `!e.IsDeleted && !e.IsDraft && e.GroupId == string.Empty` | `CanSeeAsync` + `EventToAuditableResource` |
| events (group) | `e.GroupId == groupId && !e.IsDeleted && !e.IsDraft` | `CanSeeGroupFeedAsync` / `CanSeeGroupAsync`, as `ListGroupEventsAsync` does |
| pages | `!p.IsDraft && !p.IsDeleted` | `CanSeeAsync` + `PageToAuditableResource` (the ADR 0039 adapter, verbatim) |
| announcements | `!a.IsDraft && (Public \|\| (authed && Community))` | none — the flat scope predicate **is** the boundary (ADR 0017 family); the audit row is search's own (D7) |

**No new `AccessAction`, no new `AccessVia`, no new adapter, no new
`Decide()` branch** (C-M8·1). The match filter (`ILIKE`) is a *feed
organizer*, never an access decision (C-M3·2 extended — the candidate
filter never audits as an access event).

### D7 · Audit — one aggregate row per (surface, scope) decision

Every search visit over a surface that produced **≥ 1 candidate** emits
exactly **one** aggregate `AccessAudit` row (the `ListFeedAsync` shape):

- `ActorId` = the actor (anonymous searches emit no row at all — see the
  next bullet), `Action` = `AccessAction.Read`, `TargetKind` =
  **`"search"`** with `TargetId` = the surface name
  (`"posts"` / `"events"` / `"pages"` / `"announcements"`) — the
  aggregate shape's one sanctioned single-field carrier is `TargetId`,
  which the doc-comment assigns to single-target rows; to stay strictly
  inside the two stored shapes, M8 uses **`TargetKind = "search:<surface>"`**
  with `TargetId = null` and `VisibleCount`/`HiddenCount` set (drift log
  entry 2 records this choice over the `TargetId`-as-surface alternative).
- `Via` = the `AccessVia` the decision's dominant lane produced (the
  `CanSeeAsync` aggregate already carries per-candidate `Via`s — the row
  records `Audience` for the community surfaces; group-surface rows record
  `Group`; announcement rows record `Audience` — the flat-scope standing
  the row is *equivalent* to; drift log entry 3).
- `Outcome` = `Allow` (a search visit that produced any visible hit) or
  `Deny` (all candidates hidden).
- **Zero-candidate surfaces emit no row** (C-M7·5 extended; the early
  return runs before any decision).
- `HiddenCount` is **stored, never rendered** (C-M8·4, the C-M7·7 pin).

Anonymous searches: the community predicate (D6) is pure (a flat scope
check, no `CanSeeAsync` call — the announcement lane's shape, verified), so
an anonymous visit emits **no** `AccessAudit` row (the "a read, not a
decision" pin — `PostService.cs:1411` precedent, verified).

### D8 · Seam shape — one bounded context, one interface

`Kumunita.Core.Search` (new bounded context, the ADR 0039 `Pages`
precedent) with **one seam**:

```csharp
public interface ISearchService
{
    // surface=all: the top MaxPerSurface (5) hits per in-scope surface,
    // community scope (or community+groups when actorId is non-empty and
    // scope=groups). One row per surface that had ≥1 candidate (D7).
    Task<SearchResults> SearchAsync(string q, SearchScope scope,
        string? actorId, CancellationToken ct = default);

    // surface=<one>: the paged single-surface read (D1). page floors to 1
    // (the C-M7·2 / ADR 0090 discipline); HasMore is the sole paging
    // signal (the EventPage/AnnouncementPage record shape, ADR 0090 D1/D3).
    Task<SearchSurfacePage> SearchSurfaceAsync(string surface, string q,
        SearchScope scope, string actorId, int page, CancellationToken ct = default);
}
```

`SearchService` composes **only** `IDocumentStore`, `IAuthorizationService`,
`IUserInfoService` (frozen seams; the ADR 0006-D "frozen composition"
discipline) and is registered in `Kumunita.Core/DependencyInjection.cs` in
the `AddTransient<ISearchService>` factory shape (the `IAnnouncementService`
/ `IPageService` precedent, verified). The interface exists so
`SearchControllerTests` substitutes without Postgres (the ADR 0090 D10
"split by seam" test home).

## 4. Invariants (C-M8)

- **C-M8·1 · Read-only, frozen seams.** Zero writes. No new
  `AccessAction`, `AccessVia`, adapter, document, or `Decide()` branch.
- **C-M8·2 · No visibility widening.** A search hit set is a subset of the
  union of the corresponding feeds' visible sets for the same actor.
  (Enforced by D6's same-predicate pin — the test
  `Search_GroupScope_ExcludesGroupsActorCannotSee` is the pin.)
- **C-M8·3 · Audit is always-on, aggregate-shaped** for every decision that
  ran (D7); zero-candidate / anonymous visits emit no row.
- **C-M8·4 · No hidden-count leak.** Search results render only hits +
  `HasMore`; `HiddenCount` is stored, never rendered (C-M7·7).
- **C-M8·5 · `q`/`page`/`scope`/`surface` are display-only** — never an
  input to an `IAuthorizationService` call, an `Audience` evaluation, or
  an `AccessAudit` row's identity (C-M7·2 extended).
- **C-M8·6 · Zero schema change.** No index, no document, no migration.
- **C-M8·7 · Drafts and soft-deletes are invisible** — no draft, deleted,
  or `IsDeleted` document is ever a hit (C1 / ADR 0037 / ADR 0024).

## 5. FACES

- **F1 · Anonymous can search** community surfaces (the nav box renders for
  guests — parity with the public feed surface).
- **F2 · Group content is membership-gated; a non-member sees no hit and
  no count** (C1; the ADR 0010 private-group pin extended).
- **F3 · Drafts never leak** — a search for a draft's exact title returns
  nothing (C-M8·7).
- **F4 · Every decided visit audits; empty / anonymous visits don't**
  (C-M8·3).
- **F5 · The search box is keyboard-operable and localized** — the 11 new
  `kw-l` keys × 4 languages, `KnownTranslationKeys_ParityTests` enforces
  (ADR 0015).
- **F6 · Scope degradation is silent** — anonymous `scope=groups` yields
  community-only results, no 403, no "you lack access" text (D3).

*The trade the design prices:* **coherence for exhaustiveness** — the
top-5-per-section `all` view is a search-*box* answer, not a search
*engine*; a resident looking for a 6th match clicks through to the
single-surface paged view. At one-neighborhood scale (D4) the substring
engine is the honest ceiling; `tsvector` FTS is the follow-up lane and
costs a versioned migration (ADR 0004 §B) when the neighborhood outgrows
it. **Stable for flexible**: M8 adds one bounded context; it retires
nothing.

## 6. Parts affected

- **New:** `Kumunita.Core/Search/` (3 files), one DI registration line,
  `Kumunita.Web/Controllers/SearchController.cs`, `Kumunita.Web/Views/Search/Index.cshtml`,
  a nav-box addition to `Views/Shared/_Layout.cshtml`, 11 `kw-l` keys, two
  test classes (14 + 10 pinned tests).
- **Touched:** `DependencyInjection.cs` (one registration), `_Layout.cshtml`
  (nav box only), `KnownTranslationKeys.cs` (11 keys × 4 languages).
- **Untouched (pinned):** every frozen interface; `Milestones.cs` / README /
  `MilestonesTests.cs` until the U04 close; the schema (C-M8·6).

## 7. Rollout & rollback

No migration, no seed, no data change (C-M8·6) — rollout is a normal
deploy. Rollback is a normal revert: the surface is purely additive;
removing the `Search/` context, the controller, the view, and the nav box
returns the pre-M8 state with zero residue (no rows written, no columns
added). See `docs/OPS.md`.

## 8. Risks

1. **A drift in the candidate predicate** — the single most likely
   integration break: a search predicate that *diverges* from the canonical
   feed's predicate either widens (a C-M8·2 breach — a hidden post leaks
   into results) or narrows (a false "not found"). Mitigation: D6 pins the
   exact expressions; U01's exit names the canonical method each predicate
   was copied from; the 14 pinned tests include
   `Search_Posts_NeverReturnsDraftsOrSoftDeleted` and
   `Search_GroupScope_ExcludesGroupsActorCannotSee`.
2. **A hidden-count leak** — the rendered `Total`/`HiddenCount` (the M7
   U03 leak shape). Mitigation: C-M8·4 + the view renders only hits +
   `HasMore`; `Search_Hit_BodyTruncated_RenderedEscaped` + the no-results
   test pin the render shape.
3. **An audit-row shape that breaks the two stored shapes** — D7's
   `TargetKind = "search:<surface>"` choice is the drift-guard's first
   watch item (entry 2); `Search_Posts_EmitsOneAggregateAuditRow_PerVisit`
   pins the shape.
4. **Markdown/HTML in the truncated window** — the window is escaped at
   render; `Search_Hit_BodyTruncated_RenderedEscaped` pins it.

## 9. The three acceptance tests (template)

- **Closed-loop?** Yes: type → results → click a hit → the detail page
  (which its own frozen access check gates as always).
- **Handoff?** Yes: each hit links to the surface's canonical detail route
  (`/posts/{id}`, `/events/{id}`, `/pages/{path}`, `/announcements/{id}`) —
  the same routes the feeds use.
- **Part vs whole?** Yes: the search box is a part (findability) that does
  not optimize a part at the whole's cost — it is read-only, audited, and
  visibility-preserving (C-M8·2/3/4).

---

# Part 2 — the exact C# shape, the pinned tests, the drift-guard

## 2.1 The records (new file `Kumunita.Core/Search/SearchModels.cs`)

```csharp
namespace Kumunita.Core.Search;

/// One search hit. `BodyExcerpt` is null for a title-only match.
public sealed record SearchHit(
    string Id,
    string Surface,        // "posts" | "events" | "pages" | "announcements"
    string? GroupId,       // non-null iff the hit is a group-scope row
    string? Title,         // null when the surface's doc has no Title
    string? BodyExcerpt,   // ≤ 2×TruncationRadius chars around the first match
    DateTimeOffset Created);

/// surface=all: the per-surface sections, each ≤ MaxPerSurface hits.
public sealed record SearchResults(
    IReadOnlyDictionary<string, IReadOnlyList<SearchHit>> Sections,
    string Q);

/// surface=<one>: the paged single-surface read (the EventPage/AnnouncementPage
/// record-return shape, ADR 0090 D1/D3 — HasMore is the sole paging signal).
public sealed record SearchSurfacePage(
    string Surface,
    IReadOnlyList<SearchHit> Hits,
    int Page,
    bool HasMore);

public enum SearchScope { Community, Groups }
```

Constants (on `SearchService`): `PageSize = 20`, `MaxPerSurface = 5`,
`TruncationRadius = 120`.

## 2.2 The per-surface candidate predicates (D6, the frozen text)

```csharp
// posts — community (mirrors PostService.ListFeedAsync minus the component
// filter; the match filter replaces it — C-M3·2: the candidate filter is a
// feed organizer, never an access decision):
p => p.GroupId == string.Empty && p.DeletedAt == null && !p.IsDraft
     && (p.Title.ILIKE(pattern) || p.Body.ILIKE(pattern))

// posts — group (mirrors PostService's group-feed predicate):
p => p.GroupId == group && p.DeletedAt == null && !p.IsDraft
     && (p.Title.ILIKE(pattern) || p.Body.ILIKE(pattern))

// events — community (mirrors EventService.ListUpcomingAsync, verified
// "!e.IsDeleted && !e.IsDraft && e.GroupId == string.Empty"):
e => e.GroupId == string.Empty && !e.IsDeleted && !e.IsDraft
     && (e.Title.ILIKE(pattern) || e.Body.ILIKE(pattern))

// events — group (mirrors EventService.ListGroupEventsAsync):
e => e.GroupId == group && !e.IsDeleted && !e.IsDraft
     && (e.Title.ILIKE(pattern) || e.Body.ILIKE(pattern))

// pages (mirrors IPageService's read filter; ADR 0037/0024 idioms):
p => !p.IsDraft && !p.IsDeleted
     && (p.Title.ILIKE(pattern) || (p.Body != null && p.Body.ILIKE(pattern)))

// announcements (mirrors AnnouncementService, verified
// "!a.IsDraft && (Public || (authed && Community))"):
a => !a.IsDraft
     && (a.Scope == AnnouncementScope.Public
         || (authed && a.Scope == AnnouncementScope.Community))
     && (a.Title.ILIKE(pattern) || a.Body.ILIKE(pattern))
```

`ILIKE` = Marten's Postgres-translated case-insensitive `LIKE` (M9 supports
it natively; `pattern = $"%{q}%"`, `q` the raw query — the parameter is
bound, never interpolated into SQL).

## 2.3 The audit row (D7, the frozen text)

```csharp
// per surface with ≥1 candidate, in the same transaction/session as the
// read (C3, the standalone-commit lane — PostService.ListFeedAsync's
// "plain read … own commit is the correct C3 lane", verified):
new AccessAudit
{
    At = DateTimeOffset.UtcNow,
    ActorId = actorId,
    Action = AccessAction.Read.Id,   // "read" (AccessAction is a record; the
                                      // audit row stores the Id string — the
                                      // AuthorizationService.cs:192 convention)
    TargetKind = "search:" + surface,     // drift log entry 2
    TargetId = null,
    VisibleCount = visible.Count,
    HiddenCount = hiddenCount,
    Via = dominantVia,                    // drift log entry 3
    Outcome = visible.Count > 0 ? AccessOutcome.Allow : AccessOutcome.Deny,
};
```

## 2.4 Pinned tests

**Core — `tests/Kumunita.Core.Tests/SearchServiceTests.cs`** (14, over
`PostgresFixture` — the ADR 0090 D10 test home):

1. `Search_Posts_HitsTitleAndBody_CaseInsensitive`
2. `Search_Posts_NeverReturnsDraftsOrSoftDeleted`
3. `Search_Posts_AudienceRestricted_HiddenFromUnprivilegedViewer`
4. `Search_Posts_EmptyQuery_NoCandidates_NoAuditRow`
5. `Search_Posts_EmitsOneAggregateAuditRow_PerVisit`
6. `Search_GroupScope_ReturnsVisibleGroupPostsOnly`
7. `Search_GroupScope_ExcludesGroupsActorCannotSee`
8. `Search_GroupScope_Anonymous_Denied`
9. `Search_Events_CommunityFeed_Only_ExcludesGroupEvents`
10. `Search_Events_NeverReturnsDraftsOrDeleted`
11. `Search_Pages_PublishedOnly_AudienceRespected`
12. `Search_Announcements_PublicAlways_CommunityScopeRequiresAuth`
13. `Search_BodyTruncated_AroundFirstMatch`
14. `Search_PagedSurface_HasMore_HonorsPageDiscipline`

**Web — `tests/Kumunita.Web.Tests/SearchControllerTests.cs`** (10, over the
NSubstitute-over-`ISearchService` harness, the `AnnouncementControllerTests`
precedent):

1. `Search_Index_NoQuery_RendersEmptyState_NoServiceCall`
2. `Search_Index_AllScope_RendersSections_NoPager`
3. `Search_Index_SingleSurface_RendersPager_WithPageAndQInLinks`
4. `Search_Index_ScopeGroups_SignedIn_RendersGroupHits`
5. `Search_Index_ScopeGroups_Anonymous_CommunityOnly`
6. `Search_Hit_BodyTruncated_RenderedEscaped`
7. `Search_Index_PageFloor_FloorsToOne`
8. `Search_NavBox_Rendered_ForAnonymous`
9. `Search_NavBox_Rendered_ForSignedIn`
10. `Search_EmptyResults_RendersLocalizedNoResults`

## 2.5 The new `kw-l` keys (U02; all four languages —
`KnownTranslationKeys_ParityTests` enforces)

`search.nav` · `search.title` · `search.placeholder` · `search.no-results` ·
`search.section.posts` · `search.section.events` · `search.section.pages` ·
`search.section.announcements` · `search.scope.community` ·
`search.scope.groups` · `search.empty.hint`

## 2.6 Drift-guard (the drift log)

Append-only; each entry: date, unit, what diverged, the locked resolution.

1. **2026-09-26, U00** — the lane plan's D5 quoted ADR 0018 loosely as
   "`LanguageCode` is 'safe to use for search'". ADR 0018's actual text is
   "this is what makes the field safe to use for search **and for a future
   'add a translation' lane**" (Decision), and its Consequences name "(b) a
   future **language-scoped search** — neither of which is in scope here".
   The decision (authored-in-only matching; cross-language search is the
   follow-up lane ADR 0018 points at) is unchanged; the quotation is
   corrected to the exact text.
2. **2026-09-26, U00** — the plan's D7 sketched `TargetKind: "search:posts"`
   *and* named `TargetId` as the surface carrier; the two stored
   `AccessAudit` shapes (verified on the doc) assign `TargetId` to
   single-target rows and counts to aggregate rows. Locked: the aggregate
   shape with `TargetKind = "search:<surface>"`, `TargetId = null`,
   `VisibleCount`/`HiddenCount` set — strictly inside the two stored
   shapes.
3. **2026-09-26, U00** — the plan's D7 sketch named `Via: "service"`
   (a string that is not an `AccessVia` enum value — the enum is frozen,
   `Decision.cs` verified: Owner/Audience/Delegation/Moderator/Report/
   BreakGlass/Admin/Group/Guardian/Community/Resident). Locked: `Via`
   records the dominant standing — `Audience` for the community surfaces,
   `Group` for the group-scope surfaces, `Audience` for the announcement
   surface (its flat-scope equivalence).
4. **2026-09-27, U01** — D4/§2.2 implied the case-insensitive substring
   match runs *inside* the Marten query (an `ILIKE`-style predicate over
   Title + Body). `ILIKE`/`IContains`/`IStartsWith`/`IEndsWith` are **not**
   in Marten 9.31.2's LINQ surface (verified by scanning the installed
   `Marten.dll` for those members and by `docs/marten/querying.md`, which
   tracks 9.30–9.31 and lists no case-insensitive operator). Locked
   resolution: the **canonical (non-match) predicate stays in the Marten
   query** — `GroupId == ""` / non-empty `GroupId`, `!IsDraft`,
   `!IsDeleted` / `DeletedAt == null`, announcement `!IsDraft` — so the
   visibility-narrowing invariant (C-M8·2) holds at the query layer; the
   case-insensitive substring match is applied in C# over Title + Body
   (`OrdinalIgnoreCase`) *after* the candidates load, and the visible set
   is paginated in C#. Behavior is identical to a query-side match and is
   test-pinned (tests 1, 13); the mechanism (C# post-filter, not `ILIKE`)
   is what it is.

*End of design doc. Part 1 is the authority on why; Part 2 is the
authority on shape. The ADR 0091 is the authority on the decision record.*
