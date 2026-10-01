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
