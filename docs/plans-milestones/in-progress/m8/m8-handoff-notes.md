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

