# ADR 0277 — Directory: name search, verified-only filter, and name sort

Status: Accepted
Date: 2026-10-30
Extends the **M2 directory surface** (`DirectoryController` / `DirectoryService`,
the "show-everyone" catalog read) and reuses the **M26 sort-control lane**
(ADR 0133 — the shared `Views/Shared/_Sort.cshtml` partial + `SortViewModel`
+ `SortKeys.Parse` closed-allowlist parser) exactly as the FindPeople (M23) and
Posts (M26) surfaces do. The user's ask is explicit: *"The Directory page needs
sorting and filtering functions."*

## Context

The directory list (`/directory`) rendered the full non-blocked resident catalog
as a static grid of cards — no way to find a person by name, no way to narrow
to verified residents, and no way to order the rows. As the neighborhood grows,
a flat alphabetical-free wall of cards is hard to scan; a name search, a
verified-only toggle, and a name sort are the three narrowings that surface
already carries data for (`Profile.DisplayName`, `Profile.Verified`) without
needing a new gate, a new audit row, or a new document.

The two constraints the surface already pins must stay intact:

- **The catalog read is the only gate.** `DirectoryService.ListAsync` is a pure
  "list every non-blocked resident" read (no per-viewer `CanAsync`, no audit) —
  the "show-everyone" pin. Filtering and sorting are presentation concerns over
  that already-admitted set; they must not become a second gate, and they must
  not change the set the gate admits (a suspended account never appears,
  regardless of filter/sort).
- **The directory row stays low-entropy.** `VisibleProfile` carries only
  `SubjectId` / `DisplayName` / `Verified` / `Address` / `CanMessage` — no email,
  no phone, no contact/audience field. The new controls must not widen that row;
  the search/filter/sort state lives on the **list model** (`DirectoryViewModel`),
  not the row type.

The one thing the search and sort need that the row already carries is
`DisplayName`; the verified toggle needs `Verified` — both are on the row today.
`Profile` has **no** `Created` / `Modified`, so (like the FindPeople
`PeopleFindAllowedKeys` allowlist) the only honest sort key for this surface is
`name` → `DisplayName`. Offering `created` / `modified` would be a dead option
(F9, no dead options).

## Decision

**A closed, viewer-side narrowing over the catalog rows, in the Web layer only.**
The `DirectoryController.Index` action accepts four optional query parameters and
projects the result into the existing `DirectoryViewModel` (whose `Profiles`
property is unchanged — the rows keep the same shape, order permitting):

- **`?q=`** — a case-insensitive substring match on `DisplayName` (trimmed). A
  blank/absent `q` is the no-search read (every row).
- **`?verified=true`** — the verified-residents-only toggle (a `bool`, so a
  checkbox reads as `true`/`false` from a GET form). A `false`/absent value is
  the no-filter read.
- **`?sort=name&?dir=asc|desc`** — the one closed sort key. `SortKeys.Parse`
  maps the pair against the directory's closed allowlist `{ name }`, defaulting
  to `name` ascending (the FindPeople pinned-asc idiom). A blank/absent `sort`
  is a *no-sort* read: the rows keep the catalog order exactly (the pre-feature
  shape, C-SORT·2).
- The **search box** and the **verified toggle** live in a `GET /directory` form
  above the cards; the **sort control** is the shared `_Sort` partial (one
  pill, `name`), rendered when the model's `Sort` is non-null.
- **Two distinct empty shapes** the view disambiguates with a new
  `DirectoryViewModel.HasAnyResidents` flag (the full catalog, before the
  viewer's filters): no residents on the platform at all renders
  `directory.empty` ("No residents on this neighborhood yet."); residents exist
  but the viewer's search/verified filter narrowed them to zero renders
  `directory.no_results` ("No residents match your search."). A pre-feature
  unfiltered read on an empty platform still renders `directory.empty`, so the
  visible behavior is unchanged there.

The directory's `CarriedParams` on the `_Sort` control carries the **active
filters** (`q`, `verified`) in addition to the `sort`/`dir` pair (via
`SortViewModel.SortFilterParams`), so a sort flip preserves the viewer's search
and verified state — an improvement over the FindPeople `PeopleSortViewModel`,
which carries only `sort`/`dir` and drops the bio filter on a sort flip.

**Privacy unchanged.** The filter and sort run *after* `ListAsync`'s gate, over
rows the viewer is already shown. They add no `CanAsync`, no audit row, and no
new field on `VisibleProfile` — the "no hidden-resident concept" and
"low-entropy row" pins are preserved. The verified toggle narrows to a *subset*
the viewer could already see; it never admits a row that is not in the catalog.

**Localization.** Six new `kw-l` keys (en/de/fr/da, the four-language parity
closure): `directory.search`, `directory.verified_only`, `directory.clear`,
`directory.no_results`, plus the search form's value-free aria-label
`a11y.find_directory` (the `a11y.*` attribute precedent — resolved through
`ITranslationProvider`, not `kw-l`, since a TagHelper cannot wrap an attribute).
`sort.name` already exists in the registry (the closed M26 `sort.*` set), so no
new sort label is needed. The gate's `KnownTranslationKeys.cs` size pin is
re-baselined 14687 → 14707 in `improve-check.ps1` (the documented
re-baseline-and-annotate mechanism).

## Consequences

- **`DirectoryViewModel` gains four fields** — `Query`, `VerifiedOnly`,
  `HasAnyResidents`, `Sort` — alongside `Profiles`. The two shape-pin tests that
  asserted "the list model is exactly `Profiles`"
  (`DirectoryIndexViewModelTests.DirectoryViewModel_Only_Exposes_Profiles` and
  `DirectoryDetailViewModelTests.DirectoryViewModel_Still_Only_Profiles`) are
  re-baselined to the five-field set (the viewer's own presentation state is not
  a contact/audience field, so the low-entropy *row* pin —
  `VisibleProfile_Has_Exactly_Five_Projected_Fields` — is untouched).
- **`DirectoryController.Index` now takes optional query params.** Existing
  callers (and the `DirectoryMessageButtonTests` fixtures, which invoke
  `controller.Index()` with no args) keep compiling: every parameter is optional
  and a no-arg call is the unfiltered, unsorted read — the pre-feature shape.
- **The `_Sort` partial and `SortViewModel` are reused verbatim** — no change to
  the shared sort infrastructure, and no new entry in the closed M26 `sort.*`
  translation set (the `name` key is already there).
- **The search box is a `GET` form** (state in the URL), not inline JS — the
  SECURITY.md §6 no-inline-script rule holds; the existing
  `client/lib/directory-card.js` whole-card click handler is unaffected (the new
  controls sit outside the `.dir-card` grid).
