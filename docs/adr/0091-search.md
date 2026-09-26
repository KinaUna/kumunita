# ADR 0091 — M8 — Search: one `/search` surface over the frozen seams

Status: Accepted
Date: 2026-09-26

## Context

M3 shipped content and M7 shipped honest paging, but the platform has **no
way to find content by text** (grep-verified: no search box, no `/search`
route, no `?q=` parameter anywhere). ADR 0090 (M7) deferred search
explicitly — its D9 freezes the filter inventory and its Consequences hand
M8 exactly this: "M8 reuses the `HasMore` signal + the `_Pager` partial
(no new pager idiom) and adds text-based filters on top of the same `page`
param discipline." ADR 0018 names "a future **language-scoped search**" as
the deferred lane the `LanguageCode` field was made safe for.

At one-neighborhood scale (ADR 0002: one instance, one community,
hundreds-to-thousands of documents, not millions), the honest ceiling for
search is a case-insensitive substring match over the stored text — no
ranking, no stemming, no `tsvector`. The load-bearing question is therefore
not the engine; it is that **search must compose the frozen authorization
seams exactly like the canonical reads do** (`PostService.ListFeedAsync` is
the reference shape), so that a search can never surface a document the
feed would hide and never leak a hidden count.

This ADR records the decision;
`docs/design/m8-search-design.md` (parts 1–2) freezes the invariants
**C-M8·1…7**, the FACES **F1–F6**, the **exact C#** of the seam, the
per-surface candidate predicates, and the 24 pinned test names — the design
doc is the authority on shape; this ADR is the authority on *why these
shapes, and why nothing else*.

## Decision

- **One dedicated `/search` surface — not per-feed `?q=` filters (D1).**
  A single nav search box (anonymous + signed-in); `surface=all` renders
  the top **5** hits per surface (a search-box answer, no pager);
  `surface=posts|events|pages|announcements` renders a **paged** list on
  the `?page` discipline with the `HasMore` signal + the `_Pager` partial
  (the ADR 0090 hand-off, verbatim). The rejected alternative is per-feed
  `?q=` filters on all five list surfaces: it touches 5 controllers + 5
  views, duplicates the matching/audit wiring five times, and no roadmap
  milestone asks for it — the README milestone shape is "Search" itself
  (a standalone capability).

- **The surface scope is the four resident content surfaces (D2).** In:
  posts (community **and** group), events (community feed + group events),
  pages, announcements. Out: projects, todo items, boards, goals, the
  directory, tags, groups as entities — the project family is M5's scope
  (roadmap); adding it later is a one-line predicate, not a redesign.

- **Two scopes: `community` (default) and `groups`; degradation is silent
  (D3).** `scope=groups` is decided by the **frozen** ADR 0013 group seams
  (`CanSeeGroupAsync` / `CanSeeGroupFeedAsync`) — a group the viewer cannot
  see contributes **zero** hits, not a hidden hit, not a count (the ADR
  0010 private-group pin extended). Anonymous with `scope=groups` degrades
  to community-only results — no 403, no "you lack access" text: group
  membership is the access boundary and the audience filter is *display*,
  never a refusal surface (the C-M7·2 discipline extended).

- **The engine is a case-insensitive substring match (Postgres `ILIKE`) —
  zero schema change (D4).** No `tsvector`, no GIN index, no migration:
  the ADR 0004 §B versioned-DDL lane is untouched because nothing is DDL.
  Body hits render a truncated, HTML-escaped context window (radius 120).
  Full-text search is a **future lane with its own ADR** — it is a
  versioned migration by construction and belongs to the scale at which
  substring matching stops being honest.

- **Matching is authored-in text only (D5).** The stored `Title`/`Body`;
  UGC translation rows are not searched. This is precisely the follow-up
  lane ADR 0018 names ("a future language-scoped search") — M8 does not
  pre-empt it and does not need to.

- **Authorization is the frozen seams, the canonical predicates (D6).**
  Each surface's candidate predicate is the **same expression** the
  canonical read uses (the design doc §2.2 table), so the search hit set is
  a **subset** of the corresponding feeds' visible sets for the same actor
  (C-M8·2 — no visibility widening). The match filter is a *feed
  organizer*, never an access decision (C-M3·2 extended). **No new
  `AccessAction`, no new `AccessVia`, no new adapter, no new `Decide()`
  branch** (C-M8·1; the ADR 0090 "zero new authorization surface"
  discipline).

- **Audit: one aggregate row per (surface, scope) decision (D7).**
  `TargetKind = "search:<surface>"`, `TargetId = null`,
  `VisibleCount`/`HiddenCount` set — strictly inside the two stored
  `AccessAudit` shapes (drift-guard entry 2). `Action = "read"`
  (`AccessAction.Read.Id`). Zero-candidate surfaces and anonymous visits
  emit **no** row (the C-M7·5 "a read, not a decision" pin extended).
  `HiddenCount` is stored, never rendered (C-M8·4; the C-M7·7 pin).

- **The seam is one bounded context, one interface (D8).**
  `Kumunita.Core.Search` with `ISearchService`
  (`SearchAsync` — the `all` read; `SearchSurfaceAsync` — the paged
  single-surface read, the ADR 0090 D1/D3 record-return shape) + a
  store-composing `SearchService` registered in the ADR 0039/0044
  `AddTransient<I…Service>` factory shape. The service composes **only**
  `IDocumentStore`, `IAuthorizationService`, `IUserInfoService` (frozen).
  The interface exists so the Web controller tests substitute without
  Postgres (the ADR 0090 D10 "split by seam" test home: 14 Core pins over
  `PostgresFixture`, 10 Web pins over NSubstitute).

## Consequences

- **A resident can find content** across the four surfaces — the first
  time — with the search box in the nav, anonymous or signed in (F1, F5).
- **Search can never leak a hidden document or a hidden count**
  (C-M8·2/4/7): the hit set is a subset of the feeds' visible sets,
  drafts/soft-deletes are invisible, and the render surface is hits +
  `HasMore` only.
- **Every decided visit is audited; empty and anonymous visits are not**
  (C-M8·3) — the audit log's "who saw what, by what right" query now
  includes search visits without new audit noise.
- **The two deferred lanes are named, not guessed:** language-scoped
  search (ADR 0018's own consequence) and full-text search (a future ADR
  that carries the `tsvector` migration) — both land without re-designing
  this one, because the seam is one interface (D8) and the engine is one
  predicate per surface (D6).
- **`q`/`page`/`scope`/`surface` are display-only** (C-M8·5) — the
  ADR 0064 `?view=` precedent and the ADR 0090 C-M7·2 pin, extended.
- **Close (U04):** README Roadmap + `Milestones.cs` flip (M8 → Done, M9 →
  In progress; `MilestonesTests.cs` kept green per the AGENTS.md parity
  contract); `ARCHITECTURE.md` "shape of the code" gains the `Search/`
  bounded-context note.
