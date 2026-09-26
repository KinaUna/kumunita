# M7 handoff notes

One `## U#` section per unit, **appended, never rewritten** (the shared
scratch tier of the three-tier contract — see the plan header).

## U00 — design doc + ADR 0090

- **Files written:** `docs/design/m7-pagination-filtering-design.md`
  (all sections: Context / Scope / D1–D10 locked / C-M7·1–7 / F1–F8 /
  exact C# seams / 22 pinned test names / D9 filter inventory / drift
  log) + `docs/adr/0090-pagination-and-filtering.md` (Accepted,
  2026-09-26) + one index row in `docs/adr/README.md`.
- **ADR number:** **0090 confirmed free** (index ran 0001–0089; `0090`
  appeared only in the M7 plan text).
- **Refinements beyond the plan's text (locked, drift log §10):**
  (1) the plan's test #4 body (`Total: 30, "the page's 30"`) locked to
  the **(A)** reading — `Total: 31`, the component's candidate count
  (D2 + F8 + C-M7·7 + the test's own name all agree; user-approved);
  (2) the plan's test #3 body locked to `Total: 0` for the empty page
  (D8's early return runs before the `CountAsync` — the C-M7·5 pin);
  (3) the two tag records locked as `TagPostPage` / `TagPagePage`
  (the plan's first-named single `TagPage(object)` was already
  self-corrected in the plan; the doc pins the two-record shape);
  (4) the locked `en` strings: `pagination.prev` = "Newer",
  `pagination.next` = "Older" (feeds are newest-first /
  earliest-start-first — *Prev* steps toward the head);
  (5) the **U01 call-site compile pass** (new §7.2a) — the D3 `out bool`
  change is a source-level break at the ~5 Web-controller call sites +
  ~20 existing-test call sites, so U01's whole-solution-build exit + the
  "no other Web/test change" clause only both hold with the one-token
  `out` fix at each; the compiler is the authoritative break set.
- **No code touched:** nothing under `src/` or `tests/` was modified;
  no build was run. U01 entry reads: the design doc § Seams (the exact
  C#) + **§7.2a (the call-site compile pass — the only Web/test touch
  in U01)** + § Pinned seam tests (the 12 names) + § Invariants
  (C-M7·1/4/5/7) + this section.

## U01 — paged-seam `HasMore` + `Total` correction + D6 surfaces + 12 pinned tests

- **The six seams report `HasMore`.** `FeedResult` / `GroupEventFeedResult`
  carry `HasMore` as a field (already the convention); the four remaining
  bare-list seams pivot to a **page-record return** `(Items, HasMore)`:
  `EventService.ListUpcomingAsync` → `EventPage`; `ProjectService.
  ListTodosAsync` / `ListPickerTodosAsync` / `ListBoardsAsync` /
  `ListGoalsAsync` / `ListProjectsAsync` → `TodoPage` / `TodoPage` /
  `BoardPage` / `GoalPage` / `ProjectPage`. **Drift (design log §U01):**
  the design doc §7.5 / ADR 0090 D3 pin of `out bool hasMore` is
  **CS1988-illegal** — C# forbids `ref` / `in` / `out` on `async` methods —
  so the record return is the only C#-legal + idiomatic vehicle. Semantics
  unchanged: D1 (`HasMore` sole signal), D2 (`Total` candidate count),
  D8 (0-candidate early return = no-decision), C3 (one aggregate audit row).
- **`FeedResult.Total` corrected to the candidate count** (D2 / C-M7·7):
  pre-decision, pre-paging `CountAsync` over the filtered query — `31`,
  never the page's `30`.
- **The three D6 surfaces gained paged seams:**
  `AnnouncementService.ListVisiblePagedAsync` → `AnnouncementPage`;
  `TagService.ListPostsByTagPagedAsync` → `TagPostPage`;
  `TagService.ListPagesByTagPagedAsync` → `TagPagePage`.
- **Call-site compile pass (design doc §7.2a):** one `.Items` extraction at
  the ~5 `EventController` + ~11 `ProjectsController` call sites; ~43 Core
  test sites (`, out _` → `.Items`); 19 Web NSubstitute sites (`.Returns(
  new <PageRecord>(…))` + `Task.FromException<<PageRecord>>`).
- **12 pinned seam tests (all passing):** `F1_FullPage_HasMoreTrue`,
  `F2_PartialPage_HasMoreFalse`, `F5_OversizedPage_EmptyAndNoAuditRow`,
  `F8_TotalIsCandidateCount_NotPageCount`, `C_M7_1_OneAggregateRowPerPageVisit`,
  `C_M7_5_ListUpcomingAsync_OversizedPage_NoAuditRow`,
  `ListUpcomingAsync_FullPage_HasMoreTrue`,
  `ListUpcomingAsync_PartialPage_HasMoreFalse`,
  `ListTodosAsync_FullPage_HasMoreTrue`,
  `ListTodosAsync_OversizedPage_HasMoreFalseAndEmpty`,
  `ListVisiblePagedAsync_Announcements_Page1_Full_HasMoreTrue`,
  `ListPostsByTagPagedAsync_FullPage_HasMoreTrue`.
- **Validation:** `dotnet build Kumunita.slnx -c Debug` → **Build succeeded,
  0 errors / 0 warnings**. The 12 tests pass:
  `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
  -class Kumunita.Core.Tests.M7PaginationSeamTests` → `Total: 12, Errors: 0,
  Failed: 0` (Testcontainers `postgres:18`; ~15 s). **Note:** the xunit.v3
  runner's class filter is `-class "<fqname>"` (positional), *not*
  `--filter-class` (that is the v1/v2 / VSTest flag the summary had pinned).
- **Files touched:** 6 seam files under `src/Kumunita.Core/` (`Events/
  IEventService.cs` + `EventService.cs`; `Projects/IProjectService.cs` +
  `ProjectService.cs`; `Posts/PostService.cs`; `Announcements/
  AnnouncementService.cs`; `Tags/TagService.cs`), 2 controller files under
  `src/Kumunita.Web/Controllers/` (`EventController.cs`,
  `ProjectsController.cs`), 5 test files under `tests/` (`M7PaginationSeamTests.cs`
  new; `PostServiceTests.cs`, `EventServiceTests.cs`,
  `GroupEventServiceTests.cs`, `ProjectServiceTests.cs`,
  `EventControllerTests.cs`, `ProjectsControllerTests.cs`), and the 2 ADR /
  design docs above. **No schema, auth-surface, or document changes.**

## U02 — PagedViewModel + _Pager + kw-l keys

- **Files touched (4: 2 new + 1 modify + 1 new test):** `src/Kumunita.Web/
  Models/PagedViewModel.cs` (new), `src/Kumunita.Web/Views/Shared/_Pager.
  cshtml` (new), `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
  (modify — the two keys × en/de/fr/da, appended at each dictionary's tail),
  `tests/Kumunita.Web.Tests/PagedViewModelTests.cs` (new, 4 tests).
- **Record shape (D5 — the exact C# from design doc §7.7):** `public
  sealed record PagedViewModel(int CurrentPage, int PageSize, bool
  HasPrevious, bool HasNext, string BaseUrl, IReadOnlyDictionary<string,
  string> FilterParams)` + the static `ForRoute(string baseUrl, int page,
  int pageSize, bool hasMore, IReadOnlyDictionary<string, string>?
  filterParams = null)` factory — `HasPrevious = page > 1`, `HasNext =
  hasMore`, `FilterParams` defaults to an empty `Dictionary`.
- **The two kw-l keys (locked en strings, design §7.8):**
  `pagination.prev` = "Newer" (toward the head — the feeds are
  newest-first / earliest-start-first), `pagination.next` = "Older"
  (toward the tail). Non-en values (initial baselines, community-owned via
  the in-app editor — the ADR 0042 D1 / D2 idiom): `de` "Neuere"/"Ältere";
  `fr` "Plus récents"/"Plus anciens"; `da` "Nyere"/"Ældre".
- **The `_Pager` partial's no-render pin (F2):** the partial's top guard is
  `@if (Model.HasPrevious || Model.HasNext) { … }` — a one-page surface
  (`page = 1`, `hasMore = false`) renders **nothing**. U03's drop-in
  pattern is `@if (Model.PagerPosts is not null) { <partial name="_Pager"
  model="Model.PagerPosts" /> }` (the VM field is `null` on a one-page
  section, the F2 pin's consumer).
- **The 4 VM tests (all passing):** `ForRoute_Page1_Full_HasNextTrue_
  HasPrevFalse`, `ForRoute_Page2_Partial_HasNextFalse_HasPrevTrue`,
  `ForRoute_FilterParams_Preserved`, `ForRoute_Page1_NotFull_RendersNothing`.
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
  -class Kumunita.Web.Tests.PagedViewModelTests` → `Total: 4, Errors: 0,
  Failed: 0`.
- **Parity tests (still passing):** `dotnet exec tests\Kumunita.Core.Tests\
  bin\Debug\net10.0\Kumunita.Core.Tests.dll -class Kumunita.Core.Tests.
  KnownTranslationKeys_ParityTests` → `Total: 7, Errors: 0, Failed: 0`
  (the two new keys are present in all four dictionaries; the en/non-en
  parity invariants hold).
- **Validation:** `dotnet build Kumunita.slnx -c Debug` → **Build succeeded,
  0 errors** (10 warnings, all pre-existing — none in the 3 new files or
  the modified registry). **No surface wired** (U03/U04 do that), **no
  design-doc edits** (the design doc's § Seams / § FACES / §7.8 are the
  pins consumed, not modified).
