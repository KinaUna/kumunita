# M23 — Extended user profiles (rolling handoff notes)

> **The cross-unit memory.** Every unit reads the `## U##` sections before it
> and appends its own after. A unit does not re-derive what an earlier unit
> already settled (a D# amendment, the key set, a seam shape, the `Tag.Id`
> reference convention, the two-gate posture, the aggregate-row shape). Created
> by U00 (the docs + ADR sign-off gate).

## U00

- **Delivered:** the design doc
  `docs/design/m23-extended-profiles-design.md` (all 11 sections, 0–10, incl.
  §1.a) + the ADR `docs/adr/0123-extended-profiles.md` (Status: **Accepted** —
  `**Done** (M23)` is tagged by U06 at close) + the one ADR 0123 index row in
  `docs/adr/README.md` (after the 0122 row). The register's [PROPOSED] set
  (D1–D8, C-M23·1…6, FACES F1–F6, the closed 12-key `kw-l` set, GATE-1…5, the
  §drift-guard) is **locked** in the design doc + ADR.
- **D#s locked / amended:** **D1–D8 locked verbatim** (no D# text rewritten).
  **One clarifying amendment — §1.a C1:** the **D4 gate mechanism** is locked at
  the **register's** **single `CanSeeAsync` pass** over
  `candidates.Select(p => new ProfileToAuditableResource(p))`. The unit plan
  `m23-u00.md` §2.4 restated it as "gate each survivor (one `CanAsync` per
  survivor)"; the **register wins** (tier-1 authority). Locked at the
  `CanSeeAsync` bulk pass because (1) it is the frozen M3/M21 feed idiom and
  returns `VisibleSet` (`Visible` ids + `HiddenCount`) → maps directly to the
  C-M23·4 aggregate-row shape (`targetKind "directory"`,
  `visibleCount`/`hiddenCount` set, `targetId = null`); (2) a per-survivor
  `CanAsync` loop would emit **one decision row per profile** (the "multiple
  rows" drift the §drift-guard forbids); (3) it resolves the **owner branch
  internally** (F3) without a special case. **U03 implements the single
  `CanSeeAsync` pass; the unit plan's per-survivor wording is superseded.**
- **FACES:** F1 (author sets bio+tags), F2 (detail bio+tags behind
  `Visibility`, contact block behind the unchanged `ContactVisibility` — two
  independent gates), F3 (owner always sees own), F4 (fresh profile =
  owner-only, the lean default), F5 (find-people over visible profiles, one
  aggregate row), F6 (a profile tag is a label, never a gate).
- **§gate:** GATE-1 (bio+tags gated by `Visibility`, owner sees own, contact
  block independent), GATE-2 (find-people visible-only + one aggregate row + no
  leak), GATE-3 (single write lane, verbatim bio + tag create-or-get + no
  profile-write audit row), GATE-4 (zero new authorization surface), GATE-5
  (bio is rich content via `MarkdownRenderer.RenderHtml`, null-safe).
- **Key mechanical pins locked (codebase verified):** the two `Profile` fields
  (`Bio` `string?`, `TagIds` `string[] = []`) are **additive POCO fields** on
  the M1-registered `Profile` doc (ADR 0004 §B.1 — **no new DocTypes surface, no
  migration**); `ProfileUpdate` gains `Bio = null` + `TagIds = null` **after**
  the frozen `Address` default (existing codebase = five fields + `Address`);
  `ProfileToAuditableResource` is `TargetKind "directory"`, `Audience =>
  Profile.Visibility`, `OwnerId => SubjectId`; `DirectoryService` composes
  `IUserInfoService` + `IAuthorizationService` and filters
  `GetProfilesAsync(false)` by `!p.Blocked`; `CanSeeAsync(actorId, AccessAction,
  IEnumerable<IAuditableResource>)` → `VisibleSet(Visible, HiddenCount)`; the
  `Tag` POCO is `Slug`/`Name`/`LanguageCode`/`CreatedBy` (the `TagService.
  AttachToPostAsync` create-or-get = one `tag.create` row per **newly created**
  tag, a present `Slug` reused with no row); the bio renders via the existing
  `MarkdownRenderer.RenderHtml(string?)`.
- **`TargetKind` pin:** `"directory"` (reused — **no new `TargetKind`**; the
  aggregate audit discriminator for bio+tags + find-people is the same existing
  value the directory already uses).
- **`kw-l` authorship split (locked §7):** **U04** authors
  `profile.edit.bio` / `profile.edit.tags` / `profile.edit.tags.placeholder` /
  `profile.detail.bio` / `profile.detail.tags` / `profile.detail.tags.empty` /
  `profile.flash.saved`; **U05** authors `nav.people` / `profile.find.title` /
  `profile.find.by_tag` / `profile.find.by_bio` / `profile.find.results` /
  `profile.find.empty`. All four languages (en/de/fr/da); no unit consumes a key
  it has not authored.
- **U01 next** — read the design doc §4 (the two `Profile` fields + the
  `ProfileUpdate` extension, exact C#) + §2 (C-M23·3/6) + the ADR 0123 §Decision
  (D1/D3). U01 owns the **two additive `Profile` fields** + the **`ProfileUpdate`
  extension** (the data model only — no write-lane logic, no find-people); it
  exits on `Kumunita.Core.Tests`.

## U01

- **Delivered:** the two additive `Profile` fields
  (`Bio` `string?` + `TagIds` `IReadOnlyList<string> = []`) in
  `src/Kumunita.Core/UserInfo/Profile.cs`, **after `EmailLanguage`** (the
  additive-fields-at-the-end shape, ADR 0004 §B.1), each with the §B.1
  doc-comment shape; + the `ProfileUpdate` extension — **appended** two optional
  trailing fields `string? Bio = null` + `IReadOnlyCollection<string>? TagIds =
  null` after the frozen `Address` default (the `Address` appended-with-default
  precedent; the frozen five positional fields' order/types are **unchanged**).
- **Idiom note (codebase wins for mechanics):** the design doc / register spell
  the POCO field `string[] TagIds = []`, but the **real** codebase idiom —
  `Post.TagIds` / `Page.TagIds` / `TodoItem.TagIds` / `Event.TagIds` — is
  `public IReadOnlyList<string> TagIds { get; set; } = [];`. Per the unit
  plan's "copy the field shape verbatim / the codebase wins for mechanics,"
  the **POCO** field is `IReadOnlyList<string>` (consistent with its four
  siblings); the **`ProfileUpdate` patch** field stays the locked surface's
  `IReadOnlyCollection<string>?` (the patch's nullability is what "null ⇒
  don't touch" needs, and it's interface-compatible with the POCO). No
  round-trip or equality code is affected.
- **Tests:** `tests/Kumunita.Core.Tests/UserInfo/ProfileBioTagsShapeTests.cs`
  (4 tests, `PostgresFixture` harness, mirrors `ProfileTimezoneLaneTests`) —
  (1) the two `Profile` fields **round-trip** (store the `Profile` doc directly
  through the document store, reload verbatim via `GetProfileAsync`); (2) a
  fresh profile defaults `Bio = null` / `TagIds` empty (the §B.1 no-reseed
  pin); (3) `ProfileUpdate` **carries** the two fields — the frozen six-field
  positional shape still compiles (new fields `null`), a set shape reflects
  them; (4) `null` ⇒ don't-touch is **distinguishable** from set (and
  null-vs-empty list). These are **shape** tests only — the write-lane
  behavior (bio verbatim, tag create-or-get, the `tag.create` row, no
  profile-write audit row) is **U02's**.
- **Boundary confirmed (not a bug):** the current
  `UserInfoService.UpsertProfileAsync` (line 1641) does **not** persist
  `Bio`/`TagIds` (its create-branch field list + patch-application block lack
  them) — that extension is **U02's** (the F13 single-write-surface pin, D3).
  U01 is data-model shape only; `UpsertProfileAsync` is untouched here.
- **Exit:** `dotnet build Kumunita.slnx -c Debug` **clean**; `dotnet exec
  tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` **green**
  (Total: 1072, Errors: 0, Failed: 0, Skipped: 0). No drift. **U02 next** —

## U02

- **Delivered:** the F13 single-write-lane extension (D3, C-M23·3, C-M23·6,
  GATE-3) — **one optional trailing param** appended to
  `IUserInfoService.UpsertProfileAsync(profile, patch, string? actorBy = null)`
  (the `SetProfileTimezoneAsync` `actorBy` idiom — the ADR 0006-E
  compatible-addition lane; the existing two-param call site
  `ProfileController.Edit` line 261 compiles unchanged). In
  `UserInfoService.UpsertProfileAsync`, after the "patch wins" block:
  `if (patch.Bio is not null) doc.Bio = patch.Bio;` (**verbatim**, ADR 0001-B,
  `null` ⇒ don't touch) + the **inlined** ADR 0044 tag create-or-get
  (`TagService.AttachToPostAsync` shape — **not** a new `ITagService` method):
  a missing `Slug` **creates** a `Tag` (+ exactly **one** `tag.create`
  `AccessAudit` row, `CreatedBy = actorBy ?? subjectId`, `Via = Owner`,
  C-TG·9); a present `Slug` is **reused** (no row, C-TG·4); blank slugs
  skipped. The profile write itself emits **no** audit row (a Profile field
  write, not an access decision — the existing shape); the create-branch field
  list also carries `Bio`/`TagIds`; still **one** `SaveChangesAsync` (C3).
- **Open question:** none — the register's D3 [PROPOSED] set was locked
  verbatim by U00; no D# amended here. `LanguageCode` is set to `"en"` on new
  profile tags (the profile doc has no language field to derive from — the
  `TagService` post/page lane uses the content's `LanguageCode`; the profile
  has none, so the `en` floor matches the existing `Tag` default).
- **Tests:** `tests/Kumunita.Core.Tests/UserInfo/ProfileBioTagsWriteLaneTests.cs`
  (7 tests, `PostgresFixture` + `TagDocTypes.Configure` in the boot) —
  (1) bio stored **byte-for-byte verbatim** (no Core Markdown processing);
  (2) `null` `Bio`/`TagIds` ⇒ don't-touch (both preserved); (3) new slug
  creates a `Tag` + exactly one `tag.create` row (`CreatedBy = actor`,
  `Via = Owner`, `Outcome = Allow`) and **no other** audit row; (4) reuse of a
  present slug creates no new tag/row; (5) a bio-only write emits **zero**
  audit rows; (6) `actorBy = null` ⇒ `CreatedBy` falls back to the owner;
  (7) the `ProfileToAuditableResource` six-member shape + `"directory"`
  `TargetKind` is unchanged (zero-new-authorization-surface pin, C-M23·2).
- **Boundary confirmed:** the `ProfileController.Edit` call site
  (`UpsertProfileAsync(profile, patch)`) keeps compiling via the optional
  `actorBy` default; U04 will pass `SubjectId(User)` explicitly for a
  non-fallback `CreatedBy` (its own deliverable).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` **clean**; `dotnet exec
  tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` **green**
  (Total: 1079, Errors: 0, Failed: 0, Skipped: 0). No drift. **U03 next** —
  adds `IProfileFindService` (the find-people read) in `Kumunita.Core.UserInfo`;
  it composes the existing `GetProfilesAsync` candidate set + the **single**
  `CanSeeAsync` pass over `ProfileToAuditableResource` (U00's §1.a C1 pin),
  reuses the frozen ADR 0044 `ITagService` for tag display names, and emits the
  aggregate `directory` `AccessAudit` row via `CanSeeAsync` itself.
  extend `UpsertProfileAsync` to write `Profile.Bio` verbatim + resolve
  `Profile.TagIds` to `Tag.Id`s (the `TagService.AttachToPostAsync` create-or-
  get, one `tag.create` row per **newly created** tag), **no** new
  `IUserInfoService` method, **no** profile-write audit row; exits on
  `Kumunita.Core.Tests`.

## U03

- **Delivered:** the **one new composition service** (ADR 0006-D "composes only
  the frozen seams") in `Kumunita.Core.UserInfo` — `IProfileFindService`
  (interface + the two page records `ProfileTagPage(IReadOnlyList<Profile>,
  Tag?, bool HasMore)` and `ProfileBioPage(IReadOnlyList<Profile>, bool
  HasMore)` in `IProfileFindService.cs`) + `ProfileFindService`
  (implementation in `ProfileFindService.cs`) + the DI registration (one
  `AddTransient<IProfileFindService>` immediately after `DirectoryService`).
  Two **paged** finds (`PageSize = 30`, ADR 0090 D6 `HasMore` idiom) over
  **profiles the viewer may already see**: **by profile-tag** (slug → the
  frozen ADR 0044 `Tag` doc → `TagIds` containment) + **bio-substring**
  (case-insensitive `Contains`). Each gates the candidate set with the
  **single** `CanSeeAsync(actorId, Read, candidates.Select(p => new
  ProfileToAuditableResource(p)))` pass (U00's §1.a C1 pin — the aggregate
  `directory` row is emitted **by the frozen seam itself**, `TargetKind
  "directory"`, counts set, `TargetId = null`), then returns only the visible
  survivors. **Blank query / missing tag / 0 candidates ⇒ empty page, no
  decision, no row** (the early return happens **before** `CanSeeAsync`, so
  zero audit rows of any kind). **Zero new authorization surface** (GATE-4).
- **Idiom note (codebase wins for mechanics):** `Profile.TagIds` is
  `IReadOnlyList<string>` (U01's shape, its four siblings' idiom) not the
  register's `string[]`; `Profile` has **no `.Id`** — identity is
  `.SubjectId` (so `GateAsync` filters survivors on `p.SubjectId` while reading
  the `Visible` ids via `v.Id`); the candidate filter also drops
  `!p.Blocked` (mirroring `DirectoryService.ListAsync`). The **`HasMore`
  idiom** is the M3 paged-slice shape — `pageSlice.Count == PageSize` (the
  sliced page), **not** `candidates.Count == PageSize` (the full filtered
  list); a 31-candidate set → page 1 full (30) → `HasMore = true`, the
  `TagService.ListPostsByTagPagedAsync` / `PostService.ListFeedAsync`
  precedent.
- **Tests:** `tests/Kumunita.Core.Tests/UserInfo/ProfileFindServiceTests.cs`
  (7 tests, `PostgresFixture` + `TagDocTypes.Configure` in the boot) —
  (1) by-tag **visible-only** (a denied survivor never surfaces, GATE-2);
  (2) by-bio **visible-only** (same pin); (3) a non-empty read emits **exactly
  one** aggregate audit row (`TargetKind "directory" && TargetId == null`) —
  isolated from the per-item `directory` rows the frozen seam also writes for
  non-null `Audience` profiles (that per-item behavior is the frozen seam's
  own, not drift); (4) **blank/missing** input ⇒ empty page + **zero** audit
  rows (total count, the early return precedes `CanSeeAsync`); (5) the **owner
  always sees own** profile even when `Visibility` denies others (F3);
  (6) **paging** — 31 candidates → page 1 full (`HasMore` true) + page 2 the
  remainder (1, `HasMore` false), ADR 0090 D6; (7) the `IAuthorizationService`
  **surface is unchanged** (a reflection pin — still exactly 8 methods,
  zero new, GATE-4).
- **Open question:** none — no D# amended; the register's [PROPOSED] set was
  locked verbatim by U00.
- **Exit:** `dotnet build Kumunita.slnx -c Debug` **clean**; `dotnet exec
  tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  **green** (Total: 1086, Errors: 0, Failed: 0, Skipped: 0). No drift.
  **U04 next** — the Web editor (bio textarea + tag picker on the profile-edit
  page, authoring its own `kw-l` keys `profile.edit.*` / `profile.flash.saved`
  in all four languages) + the directory-detail bio/tags gate (F2's two
  independent gates: bio+tags behind `Visibility`, contact block behind the
  unchanged `ContactVisibility`); it passes `SubjectId(User)` as `actorBy`
  into `UpsertProfileAsync` (U02's optional param) and renders the bio via
  `MarkdownRenderer.RenderHtml` (GATE-5, null-safe).

## U04

- **Delivered:** the **Web profile-edit surface** (the U04 exit gate's
  Web.Tests-only half). (1) **`ProfileEditViewModel`** gains the two new editor
  fields — `Bio` (`string?`) + `Tags` (`string?`, the ADR 0044 tag-input
  string) — and `ToProfileUpdate` maps them onto the U02
  `ProfileUpdate` extension: `Bio` **verbatim** (D3, `null` when blank) and
  `TagIds` the parsed slugs (split on comma/space, trimmed, lowercased,
  de-duplicated, C-TG·4 `>64`-char + blank tokens dropped, order-preserving;
  `null` when the input is whitespace-only — the "null ⇒ don't touch" shape).
  (2) **`ProfileController.Edit`** (POST) writes through the **single** U02 lane
  `UpsertProfileAsync(profile, patch, subject)` — passing `SubjectId(User)`
  explicitly as `actorBy` (C-TG·9 provenance) — and flashes
  `TempData["info"]` to the `profile.flash.saved` key (the
  `FlashAsync` `KnownTranslationKeys.EnValues` floor when the translation seam
  is absent). (3) **`DirectoryController.Detail`** projects the bio + tag names
  **only** when the profile's `Visibility` admits the viewer — a **self-view
  short-circuit** (`viewer == p.SubjectId` → `true`, no `CanAsync`, no row)
  else **one** `CanAsync(viewer, Read, new ProfileToAuditableResource(p))`
  (frozen seam, zero new authorization surface, C-M23·2); a **missing** authz
  seam is fail-closed (`false`). The contact block (Email/Phone/Address)
  stays on the **independent** `ContactVisibility` gate owned by
  `DirectoryService.DetailAsync` (F2 "two independent gates, zero new
  audiences" — admitting one does not admit the other). (4)
  **`DirectoryViewModel.Detail`** gains `Bio` + `TagNames` (the eight-field
  shape). (5) **`Views/Directory/Detail.cshtml`** renders the bio via
  `MarkdownRenderer.RenderHtml` (GATE-5, null-safe) + the tag badge chips
  (`profile.detail.bio` / `profile.detail.tags` /
  `profile.detail.tags.empty`). (6) **`Views/Profile/Edit.cshtml`** adds the
  bio rich-content card (`rc-editor`, `~/js/lib/rich-editor.js`) + the tags
  free-form input (`profile.edit.bio` / `profile.edit.tags` /
  `profile.edit.tags.placeholder`, the already-registered `tag.input.hint`).
- **`kw-l` authorship (locked §7, U04's set):** the **seven** U04 keys
  `profile.edit.bio` / `profile.edit.tags` / `profile.edit.tags.placeholder` /
  `profile.detail.bio` / `profile.detail.tags` /
  `profile.detail.tags.empty` / `profile.flash.saved` — each non-empty in all
  four languages (en/de/fr/da) in `KnownTranslationKeys`. **No unit consumes a
  key it has not authored**; no key invented outside the locked set (the
  `KwLRegistryConsistencyTests` / `KnownTranslationKeys_ParityTests` gate).
- **Idiom note (codebase wins for mechanics):** `ProfileUpdate.TagIds` is
  `IReadOnlyCollection<string>?` (U01's patch shape); the editor's `ToProfileUpdate`
  emits a `string[]` via `.ToArray()` (interface-compatible). The
  **`null` vs empty-list** distinction on `TagIds` is deliberate and pinned:
  a whitespace-only input → `null` ("don't touch"); a **separators-only**
  input ("  ,  ") → **`[]`** ("clear all"), NOT `null` (a non-blank input
  parses to the empty slug list). `Decision` is a **class** (sealed record) —
  NSubstitute's default `Task<Decision>` return is `null` (→ NRE on
  `.Allowed`), so any test driving `DirectoryService.EvaluateContactGateAsync`
  with a non-self viewer must set a default `Decision` on the stub.
- **Tests:** `tests/Kumunita.Web.Tests/M23ProfileExtendedTests.cs` (10 tests)
  — (1) editor binds bio+tags to the patch (verbatim bio + parsed slugs);
  (2) blank bio/tags → `null`/`null`; (3) the `null`-vs-`[]` distinction
  (separators-only → `[]`); (4) the C-TG·4 `>64`-char + blank-token floor;
  (5) POST Edit passes `SubjectId(User)` as `actorBy` (single
  `UpsertProfileAsync` lane, the `profile.flash.saved` en-floor flash);
  (6) bio gate admits A / denies B (contact block independent, on
  `ContactVisibility`); (7) **self-view** — owner sees own bio, **zero**
  `CanAsync` calls; (8) **two independent gates** — contact-ADMITS/bio-DENIES
  and contact-DENIES/bio-ADMITS; (9) **fail-closed** missing authz seam;
  (10) **zero-new-authorization-surface** reflection pin (8 methods, 4
  claims) + the **seven** U04 `kw-l` keys non-empty in en/de/fr/da.
- **Open question:** none — no D# amended; the register's [PROPOSED] set was
  locked verbatim by U00.
- **Exit:** `dotnet build Kumunita.slnx -c Debug` **clean**; `dotnet exec
  tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  **green** (Total: 710, Errors: 0, Failed: 0, Skipped: 0); `dotnet exec
  tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  **green** (Total: 1086, Errors: 0, Failed: 0, Skipped: 0). No drift.
  **U05 next** — the find-people surface (U03's `IProfileFindService` exposed
  on the Web, authoring its own `profile.find.*` / `nav.people` `kw-l` keys
  in all four languages) + the visible-only + one-aggregate-row + no-leak
  gate (GATE-2).

## U05

- **Delivered:** the **`/people` find-people surface** over the frozen U03
  `IProfileFindService`. (1) **`FindPeopleController`** (`[Authorize]`,
  `[Route("people")]`) — **zero new authorization surface** (C-M23·2, D7): it
  supplies only the signed-in subject (the `KumunitaPrincipal.SubjectId` idiom
  over `User`) + the page, then hands the caller state to the frozen service; it
  never re-derives access and never projects a field the U03 service did not
  surface. **`Index(bio, tag, page)`**: a non-blank `tag` **302-redirects** to
  the canonical `/people/tag/{slug}` (the by-tag form is a GET — a form GET
  cannot place a field into a path segment without inline script, so the index
  form posts the slug as a `tag` query field and this action redirects; the
  `ByTag` route stays the results URL + the paging link target); a non-blank
  `bio` calls `FindPeopleByBioAsync(bio.Trim(), viewer, page)` and views
  `Bio`; a blank bio renders the `Index` forms (the U03 "no decision, no row"
  shape — the service is never consulted for a blank query, no 404/no error).
  **`ByTag(slug, page)`** → `FindPeopleByTagAsync` → views `Tag`.
  (2) **`FindPeopleViewModels`** (`FindPeopleIndexViewModel` /
  `FindPeopleTagViewModel(Slug, Result, Page)` + `TagDisplayName` /
  `FindPeopleBioViewModel(Query, Result, Page)`) project the U03 `ProfileTagPage`
  / `ProfileBioPage` verbatim (the ADR 0090 D6 `HasMore` + `PageSize = 30`
  idiom carried straight through). (3) **Three views** — `Views/People/Index.cshtml`
  (the by-tag + by-bio forms; both `aria-label`s are **plain text** to avoid the
  `<kw-l>`-in-attribute Razor trap), `Tag.cshtml` (empty-state `profile.find.empty`
  when `Profiles.Count == 0`, tag display name, profile rows →
  `Directory/Detail?subjectId=…`, a `pagination.next` link when `HasMore`),
  `Bio.cshtml` (same shape). (4) **Nav entry** in **both** `_Layout` variants —
  the variant-B "More ▾" dropdown (next to Directory) and the variant-C icon
  rail — each `asp-controller="FindPeople" asp-action="Index"` with the
  `nav.people` `kw-l` key. (5) **U05 `kw-l` authorship (locked §7):** the **six**
  U05 keys `nav.people` / `profile.find.title` / `profile.find.by_tag` /
  `profile.find.by_bio` / `profile.find.results` (the `{0}` format-string key,
  consumed via `ITranslationProvider.GetAsync` + `string.Format` — the
  `_GrantPickers` `grant.count_selected` idiom, since `kw-l` does not support
  format args) / `profile.find.empty` — each non-empty in all four languages
  (en/de/fr/da) in `KnownTranslationKeys`.
- **Idiom note (codebase wins for mechanics):** `FindPeopleController` returns
  `Task<IActionResult>` — tests must `await` the action before `IsType<ViewResult>`.
  The by-bio query is **trimmed before the service call** (the D4 substring
  engine + the Face-2 "trimmed query" pin). The `profile.find.results` key is a
  `{0}` template (not a static `kw-l` value) because the count is dynamic —
  `kw-l` emits its string verbatim, so a format-string key must go through
  `Translation.GetAsync` + `string.Format`.
- **Tests:** `tests/Kumunita.Web.Tests/M23FindPeopleTests.cs` (12 tests) —
  (1) by-tag passes slug+subject+page + projects the U03 page verbatim (tag
  display name + `HasMore`); (2) by-bio passes the **trimmed** query + subject +
  page + projects the U03 page; (3) blank bio → the index forms, **zero** find
  calls (the U03 service never consulted); (4) missing tag → the empty-state
  view (**not** a 404); (5) the by-tag form 302-redirects to the canonical
  `/people/tag/{slug}` route; (6) paging carries the `HasMore` flag + page;
  (7) the nav entry is present in **both** `_Layout` variants; (8) the people
  views carry the empty state + the paging link + the directory-link target;
  (9) the **six** U05 `kw-l` keys are non-empty in en/de/fr/da; (10) the
  controller carries `[Authorize]` (no anonymous find); (11) **zero-new-
  authorization-surface** reflection pin (8 `IAuthorizationService` methods, 4
  claims).
- **Open question:** none — no D# amended; the register's [PROPOSED] set was
  locked verbatim by U00.
- **Exit:** `dotnet build Kumunita.slnx -c Debug` **clean** (pre-existing
  CS8603/CS8602 in `DirectoryController.cs` + xUnit1051 warnings in pre-existing
  test files only — none in U05 code); `dotnet exec
  tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` **green**
  (Total: 721, Errors: 0, Failed: 0, Skipped: 0). No drift. **U06 next** — the
  close flip (Milestones.cs + README Roadmap + the ADR 0123 `**Done** (M23)`
  tag).

## U06

- **Delivered:** the close flip (the `Milestones.cs` / `MilestonesTests.cs` /
  README / STATUS / ADR-README parity quintet) + the flat move. `Milestones.cs`:
  M23 `StatusNext` → `StatusDone`, M22 `StatusPlanned` → `StatusNext` (order
  unchanged: `…M20, M21, M23, M22`). `MilestonesTests.cs`:
  `Shipped_Milestones_Are_Marked_Done` gains `"M23"`;
  `M23_Is_The_Single_InProgress_Milestone` replaced by
  `M22_Is_The_Single_InProgress_Milestone`; the
  `Roadmap_Covers_M0_Through_M23_Plus_Named_Lanes_In_Order` order pin
  unchanged. README: top-of-file status + Roadmap bullets M23 → done
  (ADR 0123), M22 → in progress. STATUS.md: same flip. ADR 0123: the status
  row was already `Accepted` (U00); the `docs/adr/README.md` index row
  flipped `**In progress** (M23)` → `**Done** (M23)`. `ARCHITECTURE.md`:
  no M23/M22 line to flip (verified — grep returned empty). Flat move:
  register, design doc, handoff notes, this U06 plan → `done/` (flat, not
  a `done/m23/` subfolder).
- **Idiom note (codebase wins for mechanics):** the ADR 0123 status row was
  already `Accepted` at U00 sign-off (U00's deliverable), so U06's ADR
  work is the `docs/adr/README.md` index row only — the `**In progress**
  (M23)` tag (the U00 "tagged by U06 at close" pin) becomes `**Done**
  (M23)` (the ADR 0117/0122 close precedent).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` **clean** (only the
  pre-existing CS8603/CS8602 in `DirectoryController.cs` + the xUnit1051
  warnings in pre-existing test files — none in U06 code); `dotnet exec
  tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  **green** (Total: 721, Errors: 0, Failed: 0, Skipped: 0). No drift.
  **M23 is closed.**
