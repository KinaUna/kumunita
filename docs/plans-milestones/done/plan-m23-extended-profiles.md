# M23 — Extended user profiles (biography + author-set tags + find-people)

> **This file is the register** — the milestone map, the locked [PROPOSED]
> decision set, the invariants, the FACES, the §gate, the unit map, the
> workflow, the atomicity contract, and the §drift-guard. It is read first by
> **every** unit agent (U00–U06) before anything else. It is **not** a
> per-unit plan: each unit has its own self-contained file (`m23-uNN.md`) that
> a ~32K-context agent can execute without re-deriving this file.
>
> **M23 is the single `StatusNext` milestone already.** `Milestones.All` reads
> `new("M23", "Extended user profiles — …", StatusNext)` and the live pin is
> `MilestonesTests.M23_Is_The_Single_InProgress_Milestone` (M22 is
> `StatusPlanned`, deferred behind M23). So M23 has **no open-flip unit** — the
> roadmap already reads "M23 in progress." Its **one** flip is **U06 (the
> close)**: `StatusNext` → `StatusDone` (M23), **M22 promoted**
> `StatusPlanned` → `StatusNext`, the pin re-pinned to
> `M22_Is_The_Single_InProgress_Milestone`. **No unit before U06 touches
> `Milestones.cs` or `MilestonesTests`** (the roadmap already reads "M23 in
> progress" — it stays that way until M23 ships).

## Tiering (three documents, like M18–M21)

| Tier | File | Who owns it | Lifetime |
| --- | --- | --- | --- |
| 1 — Register (this file) | `in-progress/plan-m23-extended-profiles.md` | U00 (author) + all units (read) | moves to `done/` at U06 |
| 2 — Per-unit plans | `in-progress/m23-uNN.md` | the `U##` agent | moves to `done/` when the unit is done |
| 3 — Rolling handoff notes | `in-progress/m23-extended-profiles-handoff-notes.md` | every agent appends its `## U##` section | moves to `done/` at U06 |

The handoff-notes file is created by **U00** (at runtime, not by this authoring
pass). Every unit appends a short `## U##` section before it moves its own plan
to `done/`.

## Atomicity contract (sized for ~32K context)

Each unit is one self-contained step a fresh agent can complete and hand off:

- **Entry reads:** 4–8 files, named in the unit plan (with a one-line "why"
  each).
- **Deliverables:** ≤ 7 small files, named exactly (paths +, where locked, the
  exact C# / keys the design doc pins).
- **Exit gate:** **one** `dotnet build Kumunita.slnx -c Debug` **plus one**
  `dotnet exec` test assembly — either
  `tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` **or**
  `tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`. Both
  must be green.
- **A unit that needs *both* test assemblies is too big — split it.**
  (Core units exit on Core.Tests; Web units exit on Web.Tests. The close-flip
  unit — U06 — exits on Web.Tests, because it flips `Milestones.cs`, which is
  pinned by `MilestonesTests` in `Kumunita.Web.Tests`.)
- **No build/test exit for U00** — it is docs-only (design doc + ADR). Its exit
  is "the files exist and the [PROPOSED] set is locked or amended in-unit."

> **Test-runner quirk (do not retry the broken paths):** `dotnet test` and VS
> Test Explorer discovery go wrong on this machine (xunit.v3 bridge). The
> reliable exit is the `dotnet build` + `dotnet exec … .dll` pair above.
> `Kumunita.Core.Tests` starts `postgres:18` via Testcontainers (~20 s) and
> leaves Docker containers behind if killed — `docker container prune` to clean
> up.

## Unit map

| Unit | Title | Track | Exit test assembly |
| --- | --- | --- | --- |
| U00 | Design doc + ADR 0123 (the sign-off gate) | docs-only | none (no build/test) |
| U01 | `Profile.Bio` + `Profile.TagIds` additive fields + `ProfileUpdate` extension | Core | `Kumunita.Core.Tests` |
| U02 | `UpsertProfileAsync` write-lane extension (bio verbatim + tag create-or-get; new tags each emit a `tag.create` row, the profile write itself emits no audit row) | Core | `Kumunita.Core.Tests` |
| U03 | `IProfileFindService` (by-tag + bio-substring, gated, one aggregate row) + DTOs + DI | Core | `Kumunita.Core.Tests` |
| U04 | Web: `ProfileController.Edit` extension (bio + tags) + directory detail gate (render bio+tags behind `Visibility`) + **the editor/detail/flash `kw-l` keys** × en/de/fr/da (author) | Web | `Kumunita.Web.Tests` |
| U05 | Web: `FindPeopleController` (`/people` by-tag + by-bio) + view models + views + nav + **the find-people `kw-l` keys** × en/de/fr/da (author) | Web | `Kumunita.Web.Tests` |
| U06 | **Close M23** — flip `StatusDone`, promote M22 to `StatusNext`, re-pin `MilestonesTests`, README/STATUS/ARCHITECTURE parity, `done/` move | Web + docs | `Kumunita.Web.Tests` |

## Understanding (what M23 is)

**M23 = extended user profiles.** The roadmap line is the scope:
> "Extended user profiles — a biography + free author-set tags (skills,
> interests, knowledge, expertise) to make it easier to find people with
> something in common."

M23 adds **two author-owned fields to the existing `Profile` document** and a
**find-people read** over them. Two things follow from the roadmap sentence:

1. **"a biography"** = a new `Profile.Bio` field (rich content — Markdown +
   optional in-content images, the ADR 0025/0031/0033 lane), **and**
   **"free author-set tags (skills, interests, knowledge, expertise)"** = a new
   `Profile.TagIds: string[]` field that **references the frozen ADR 0044
   shared `Tag` doc** by `Tag.Id` (the `Post.TagIds` / `Page.TagIds` idiom).
   Both are **additive POCO fields** on the M1-registered `Profile` doc
   (ADR 0004 §B.1 — delta-detected, idempotent, **no re-seed**, **no new
   DocTypes surface**, **no new document**, **no new context**).
2. **"to make it easier to find people with something in common"** = a
   **find-people read** (a new `IProfileFindService`, the `DirectoryService`
   composition shape) with two paged reads — **by profile-tag** and
   **bio-substring** — both computed **over profiles the viewer may already
   see** (the ADR 0044 D5 privacy-pin; the match is a *feed organizer*, never a
   gate; the gate is the profile's own `Visibility` decision), each emitting
   **one aggregate `AccessAudit` row** (the M3-feed / M21-feed idiom).

Everything else about M23 is *what it is not* — see "What M23 is NOT" below
and the §deferred lanes (D8).

## What M23 is NOT

- **Not a new document, not a new context, not a new storage lane.** M23 adds
  **two fields to the existing `Profile` doc** + **one composition service**
  (`IProfileFindService`). The `Tag` doc is the **frozen ADR 0044** `Tag`
  (already registered in `TagDocTypes`, already referenced by `Post`/`Page`);
  M23 adds `Profile.TagIds` as a *third* referencer — it does **not** create a
  second `Tag` type (D1, D3).
- **Not a new authorization surface.** M23 adds **no** `AccessAction`, **no**
  `Decide()` branch, **no** `AccessVia` value, **no** `IAuthorizationService`
  method, **no** `TargetKind`. It **reuses** the **existing**
  `ProfileToAuditableResource` (`TargetKind "directory"`,
  `Audience => Profile.Visibility`) + the **existing**
  `ContactVisibilityResource` + `AccessAction.Read` (C-M23·2, D7).
- **Not a bio full-text / semantic search.** The find-people bio read is a
  **case-insensitive substring** over the stored `Bio` (the M8 `ISearchService`
  D4 engine, D6); a richer search is a deferred lane (D8·2).
- **Not a people-discovery *surface*.** M23 ships the *find-people read*
  (by-tag + bio-substring, paged). A facet browse, "people in common," or a
  `/people/skills/{tag}` taxonomy is a deferred lane (D8·3).
- **Not inline WYSIWYG editing of the bio on the detail view.** M23's bio is
  author-edited in `/profile/edit` (the ADR 0031/0033 `bindRichEditor` editor)
  and rendered read-only via the existing `MarkdownRenderer.RenderHtml` helper
  (`src/Kumunita.Web/Security/`); the ADR 0032/0033 inline-edit lane on the
  detail view is a deferred lane (D8·1).
- **Not a per-field audience.** Bio and tags are gated by the **one** existing
  `Profile.Visibility` audience (the M2 "kept for detailed fields" audience
  takes over, as designed); the contact block **stays** on the **one** existing
  `Profile.ContactVisibility` audience. Two audiences, two gates, **zero** new
  ones (C-M23·1, D2).
- **Not a new notification lane.** Setting a bio / tags stages **no** email
  (M6 is untouched); M23 is author-edit-and-find, not notify.

## Decisions (D1–D8) — [PROPOSED], locked or amended by U00

> Each D# carries a `*Forbids:*` tail — the anti-pattern it rules out. U00
> locks these verbatim in the design doc, or amends them **in U00** (recorded
> in the design doc's §1.a + the handoff notes). A later unit may not amend a
> D#; if a D# is wrong once the code starts, that is a §drift-guard stop.

### D1 — `Profile` gains **two additive fields**; no new document, no new DocTypes surface (ADR 0004 §B.1)

`Profile` (the M1-registered doc, `Kumunita.Core.UserInfo`) gains exactly two
fields, both **additive POCO fields** (the `Post.Status` / `Post.TagIds` /
`Profile.AvatarId` / `Profile.TimeZone` history on a POCO — delta-detected,
idempotent, **no re-seed**, **no EF migration**, **no new `*DocTypes`
surface** — `Profile` already registers in `M1DocTypes`; adding two fields is
the §B.1 additive lane):

```csharp
// in src/Kumunita.Core/UserInfo/Profile.cs — two additive fields (D1)
public string? Bio { get; set; }                    // null/empty ⇒ no bio block (C-M23·5)
public string[] TagIds { get; set; } = [];          // references the shared ADR 0044 Tag by Tag.Id (C-M23·6)
```

`Profile.TagIds` is the **`Post.TagIds` / `Page.TagIds` idiom verbatim** — a
`string[]` of `Tag.Id` values, default empty, referencing the **frozen ADR 0044
`Tag`** doc (the `MediaObject` shared-id-doc shape the `Tag` already is). M23
adds `Profile` as a **third referencer** of the shared `Tag`; it creates no new
tag type.

*Forbids:* a new `ProfileBio` / `ProfileTag` document, a new `M23DocTypes`
surface, a new relational table, a second `Tag` doc type for profiles, or
storing the bio as a separate doc (the bio is a field on the `Profile`).

### D2 — Bio + tags are gated by the **existing** `Profile.Visibility` audience; the contact block **stays** on `Profile.ContactVisibility` (the M2 two-gate shape, C-M2·1, carried to detailed fields)

The `Profile.Visibility` audience (non-null, default `new()` = empty =
deny-all) was *kept* in M2 "for the audience that would gate the *detailed*
non-contact fields that will take it over once they exist" — **M23 is that
moment.** Bio + tags render on the directory detail **only** when the viewer's
`Visibility` decision allows; the **owner branch** (the existing
`ProfileToAuditableResource` maps `OwnerId => Profile.SubjectId`,
`Audience => Profile.Visibility`) lets the author always see their own. The
contact block (email/phone/address) **stays** on the **existing**
`ContactVisibility` audience + `ContactVisibilityResource` decision — **two
independent gates, two `CanAsync` calls, two `AccessAudit` rows**, the exact
M2 C-M2·1 two-decision shape now applied to the new detailed fields.

Concretely: the detail surface evaluates (a) `CanAsync(viewer, Read,
new ProfileToAuditableResource(profile))` → gates **bio + tags**; (b)
`CanAsync(viewer, Read, new ContactVisibilityResource(profile))` → gates the
**contact block** (existing, unchanged). A **self-view** short-circuits both
(owner branch) — no decision, no row. A `null`/absent `ContactVisibility`
short-circuits (b) with no row (the existing C-M2·1 shape); bio/tags use the
non-null `Visibility` (an empty `Visibility` **denies** non-owners — the lean
default, C-M23·1 / ADR 0006-C1).

*Forbids:* a **new** `AccessAction` / `AccessVia` / `Decide()` branch, a new
`IAuditableResource` adapter (the two existing ones are reused), a new
`TargetKind` (`"directory"` is reused), a **per-field** audience (bio and tags
share the *one* `Visibility` gate — not two audiences), or folding the contact
gate into the bio/tags gate (the two gates stay independent).

### D3 — Bio + tags are written through the **existing** single `IUserInfoService.UpsertProfileAsync` lane (the F13 single-write-surface pin, extended — **no** new method)

`ProfileUpdate` (the `UpsertProfileAsync` patch record) gains **two additive
optional trailing fields** (the `Address` precedent — "appended with a default
after the frozen shape so existing positional call sites compile unchanged";
`null` ⇒ leave the current value — the "null ⇒ don't touch" patch rule every
other field follows):

```csharp
// in src/Kumunita.Core/UserInfo/Profile.cs — ProfileUpdate extension (D3)
public sealed record ProfileUpdate(
    string? DisplayName,
    string? Email,
    string? Phone,
    Audience? Visibility,
    Audience? ContactVisibility,
    string? Address = null,
    string? Bio = null,                       // null ⇒ don't touch (D3)
    IReadOnlyCollection<string>? TagIds = null); // null ⇒ don't touch (D3)
```

`UpsertProfileAsync` writes `Profile.Bio` **verbatim** (the author's choice is
absolute — ADR 0001-B; a `null` patch leaves it) and resolves
`Profile.TagIds` to `Tag.Id` values using the **frozen ADR 0044 create-or-get
idiom** (C-TG·4: `slug` = the typed string lowercased + trimmed; a missing
`Slug` **creates** a `Tag` with `CreatedBy = actorId`, C-TG·5; a present
`Slug` **reuses** the existing `Tag`, no row). The create-or-get is **inlined**
in the `UserInfoService` session — the `TagService.AttachToPostAsync` shape
(same `Tag` POCO, same `Slug` business key, same `tag.create` audit row) — **not**
a new `ITagService` method (D3's pin). The profile write itself emits **no**
`AccessAudit` row — the existing `UpsertProfileAsync` shape, "a Profile field
write, not an access decision" (the `SetProfileTimezoneAsync` /
`SetProfileAvatarAsync` precedent); the **only** audit rows are the **frozen
TG-lane** `tag.create` rows, **one per newly created tag** (C-TG·9). **All in
the caller's session, one `SaveChangesAsync`** (C3). The *standing* is the
**author's own profile** (the Web boundary owns the self-scope check, the
`SetProfileTimezoneAsync` idiom); the Core lane records the `OwnerId` and does
not re-gate.

*Forbids:* a **new** `IUserInfoService` write method for profile tags/bio (it
rides `UpsertProfileAsync`), a **new** `ITagService` profile method, a separate
"profile tag editor" controller (the tag is a field on the profile, edited
where the profile is edited — the ADR 0044 "no separate tag editor controller"
pin carried to profiles), or an unaudited direct `session.Store` from a
controller.

### D4 — A **new composition service** `IProfileFindService` (the `DirectoryService` shape) in `Kumunita.Core.UserInfo` — **no** new context, **no** new DocTypes, **no** new document

`IProfileFindService` (interface) + `ProfileFindService` (implementation) live
in `Kumunita.Core.UserInfo` (alongside `DirectoryService`), registered in
`DependencyInjection.cs` the same way `DirectoryService` is. It composes
**only** the frozen seams — `IUserInfoService` (the candidate set via the
existing `GetProfilesAsync`), `IAuthorizationService` (the per-profile
`Visibility` decision), the **frozen ADR 0044 `ITagService`** (tag display-name
resolution, ADR 0005 preference order) + its own `IDocumentStore` — exactly the
`DirectoryService` "pure caller of the frozen modules" shape (ADR 0006-D).

Two **paged** read methods (the M7 `HasMore` idiom — ADR 0090 D6 — the
`TagService.ListPostsByTagPagedAsync` shape, `PageSize = 30`):

```csharp
// in src/Kumunita.Core/UserInfo/ — new composition service (D4)
public interface IProfileFindService
{
    // by profile-tag (the C-TG·2 base query, carried to profiles): the
    // actor-visible profiles whose TagIds contain the tag resolved from `slug`,
    // paged. One aggregate AccessAudit row. (C-M23·4.)
    Task<ProfileTagPage> FindPeopleByTagAsync(string slug, string actorId, int page);

    // bio-substring (the M8 D4 engine, carried to profiles): the
    // actor-visible profiles whose Bio contains `q` (case-insensitive
    // substring), paged. One aggregate AccessAudit row. (C-M23·4.)
    Task<ProfileBioPage> FindPeopleByBioAsync(string q, string actorId, int page);
}

public sealed record ProfileTagPage(
    IReadOnlyList<Profile> Profiles,   // already Visibility-gated; the Tag row resolved
    Tag? Tag,                          // the tag (for display-name resolution); null on miss
    bool HasMore);

public sealed record ProfileBioPage(
    IReadOnlyList<Profile> Profiles,   // already Visibility-gated
    bool HasMore);
```

Both methods: (1) candidate set = the existing `GetProfilesAsync(false)`
(non-blocked profiles); (2) filter by the tag (`TagIds` contains the resolved
`Tag.Id`) or the bio substring; (3) **gate the candidate set in ONE
`CanSeeAsync` pass** over `candidates.Select(p => new ProfileToAuditableResource(p))`
(the **frozen bulk decision**, the M3 `PostService.ListFeedAsync` shape —
**not** a per-profile `CanAsync` loop; it resolves the owner branch internally,
so the author always sees their own); (4) return the survivors whose id the
`VisibleSet` surfaced + `HasMore` (`candidates.Count == PageSize`); (5) the
**one aggregate `AccessAudit` row** (`targetKind "directory"`,
`visibleCount`/`hiddenCount` set, `targetId = null`) is **emitted by
`CanSeeAsync` itself** (its own commit) — the M3-feed / M21-feed aggregate
idiom, **not hand-written**. A blank `q` / a missing tag returns an empty page
with **no** decision and **no** row (the M3 0-candidate / M8 "no decision, no
audit row" shape).

*Forbids:* a new bounded context, a new DocTypes surface, a new document, a
new `AccessAction`/`AccessVia`, a **per-profile `CanAsync` loop** (the gate is
the **one** `CanSeeAsync` pass — the M3 `ListFeedAsync` shape), a
**hand-written aggregate `AccessAudit` row** (the frozen `CanSeeAsync` writes
it — a second row is a drift violation), a find-people read that surfaces a
profile the viewer cannot see, or a find-people read that emits **no**
aggregate audit row (D6, C-M23·4).

### D5 — The bio is **rich content** (ADR 0025/0031/0033) + the profile editor **reuses** the existing `bindRichEditor` WYSIWYG and the frozen ADR 0044 tag input; the detail **reuses** the existing `MarkdownRenderer.RenderHtml` helper

The profile editor (`/profile/edit`) adds two surfaces, both **reusing frozen
land**: (a) a **bio** field — the ADR 0031/0033 WYSIWYG editor (or a Markdown
textarea) over a `string` field, exactly the surface posts/announcements/pages
use for their bodies; (b) a **tags** input — the **frozen ADR 0044 tag input**
(the composer's tag picker + `ITagService.SuggestAsync` autocomplete). The
existing two audience editors (`Visibility` + `ContactVisibility`) are
**unchanged**. The directory detail renders the bio via the **existing**
`MarkdownRenderer.RenderHtml` helper (the ADR 0025 rendered lane, the
"single Markdown engine") — `null`/empty bio ⇒
no bio block (no placeholder). Profile tags render as chips (the ADR 0044 tag
display-name idiom, resolved per-viewer).

*Forbids:* a **new** editor subsystem (reuse the ADR 0031/0033 `bindRichEditor`
+ the ADR 0044 tag input), a **new** tag-management surface for profiles, inline
WYSIWYG editing on the detail view (the ADR 0032/0033 lane — deferred, D8·1),
or a bio rendered as plain text (it is rich content, D5 — rendered by
`MarkdownRenderer.RenderHtml`).

### D6 — **Privacy-pin:** find-people results are computed over profiles the viewer may **already see**; a match is a *feed organizer*, never a gate; a profile the viewer cannot see never surfaces and its bio/tag never leaks (the ADR 0044 D5 global-enumeration exclusion carried to profiles)

The by-tag and bio-substring reads (D4) both: filter the candidate set by the
match, **then** gate each survivor by its `Visibility`; a profile whose
`Visibility` denies the viewer **never** appears in the results, and its
`Bio`/`TagIds` are never a match artifact. The tag's *display name* may
surface (it is a label, ADR 0044 D5) but only as part of a profile the viewer
may already see. A **global tag enumeration** over profiles is excluded by
design (it would leak a subject's interests behind content they cannot see).
Each read emits **one** aggregate `AccessAudit` row (C3).

*Forbids:* a bio/tag match that returns a profile the viewer cannot see, a
global profile-tag enumeration that leaks a subject, or an unaudited
find-people read (C-M23·4, D6).

### D7 — **Zero new authorization surface** (the C-M23·2 pin, the M21 D8 idiom)

M23 adds **no** `AccessAction`, **no** `Decide()` branch, **no** `AccessVia`
value, **no** `IAuthorizationService` method, **no** `TargetKind`. It reuses
exactly: the **existing** `ProfileToAuditableResource` (`TargetKind "directory"`,
`Audience => Profile.Visibility`) + the **existing** `ContactVisibilityResource`
+ `AccessAction.Read` + the **frozen ADR 0044 `ITagService`** read lanes (tag
display names). It adds **one** composition service
(`IProfileFindService`, the `DirectoryService` shape — a *caller* of the frozen
seams, not an extension of them).

*Forbids:* any extension of the frozen authorization seams (a §drift-guard stop
— the whole point of M23 is that it *rides* the existing `directory` Read path,
it does **not** extend it).

### D8 — **Deferred lanes** (each a future ADR, not part of M23)

1. **Inline WYSIWYG editing of the bio on the detail view** (the ADR 0032/0033
   inline-edit lane) — M23's bio is author-edited in `/profile/edit`;
   inline-edit on the detail is a future lane.
2. **Bio full-text / semantic search** (beyond substring) — the M8 substring
   engine is M23's floor; a richer search is a future lane.
3. **A people-discovery *surface*** (facet browse, "people in common," a
   `/people/skills/{tag}` taxonomy, shared-interests) — M23 ships the
   *find-people read*; a richer surface is a future lane.
4. **A "new bio/tags set" notification** — rides M6 (ADR 0076); M23 stages
   **no** email.
5. **Profile-tag curation by an admin** (a moderator/global-admin manages
   another resident's tags) — M23 is author-self-edit only (the ADR 0003 SoD
   lane is untouched); an admin curation lane is future.
6. **Bio image alt-text / captioning** — rides the ADR 0025/0011 image lane;
   M23 reuses it as-is; a dedicated bio-image lane is future.

## Invariants (C-M23·1 … C-M23·6)

- **C-M23·1 — Bio + tags are gated by the single `Profile.Visibility` audience; the contact block stays on `Profile.ContactVisibility` (D2, the M2 C-M2·1 two-gate shape carried to detailed fields).** One `CanAsync` (via the **existing** `ProfileToAuditableResource`) gates bio+tags; the **existing** `ContactVisibility` decision is untouched. A **self-view** short-circuits both (owner branch); a non-audience member sees neither. A test pins bio+tags hidden from a non-member + visible to the owner + the contact block independent.
- **C-M23·2 — Zero new authorization surface (D7).** No new `AccessAction` / `Decide()` branch / `AccessVia` value / `IAuthorizationService` method / `TargetKind`; the two **existing** adapters + `AccessAction.Read` carry everything; the `ITagService` is reused, not extended. A pin test asserts the seam + claim set are unchanged.
- **C-M23·3 — Bio + tags are written through the single `UpsertProfileAsync` lane (D3, the F13 pin extended); `ProfileUpdate` gains `Bio` + `TagIds`; the bio is written **verbatim** (ADR 0001-B); a missing tag slug is created (C-TG·4, one `tag.create` row each, C-TG·9) and a present slug is reused (no row); the profile write emits **no** audit row (the existing `UpsertProfileAsync` shape — a Profile field write, not an access decision); `null` ⇒ don't-touch; all in the caller's session, one `SaveChangesAsync` (C3).** A test pins the verbatim bio + the tag-creation + the reuse + `null` ⇒ don't-touch + the no-audit-row profile write.
- **C-M23·4 — Find-people results are computed over profiles the viewer may already see (D6, the ADR 0044 D5 privacy-pin); a match is a feed organizer, never a gate; a profile the viewer cannot see never surfaces and its bio/tag never leaks; one aggregate `AccessAudit` row per read (`targetKind "directory"`, `visibleCount`/`hiddenCount`, `targetId = null`) — **emitted by the frozen `CanSeeAsync` itself** (the M3 `ListFeedAsync` idiom), not hand-written.** A test pins the deny-exclusion + the aggregate row + the blank-query no-row shape.
- **C-M23·5 — The bio is rich content (D5): Markdown + optional in-content images, rendered by the existing `MarkdownRenderer.RenderHtml` helper (the "single Markdown engine"); `null`/empty ⇒ no bio block.** A test pins the rich render + the null case.
- **C-M23·6 — `Profile.TagIds` references the shared ADR 0044 `Tag` doc by `Tag.Id` (D1, the `Post.TagIds` idiom); a profile tag is a label, never a gate (C-TG·1 carried to profiles); the tag's display name is resolved per-viewer (ADR 0005 preference order, D5).** A test pins the `Tag.Id` reference shape + the no-gate pin.

## FACES (F1–F6) + the named trade

- **F1 — A resident may set a bio + tags on their own profile, gated by the profile's `Visibility` (the author's choice, ADR 0001-B).** *(C-M23·1, C-M23·3, C-M23·5.)*
- **F2 — The profile's bio + tags render on the directory detail *only* when the viewer's `Visibility` decision allows; the contact block renders under the *unchanged* `ContactVisibility` gate (two independent gates).** *(C-M23·1, C-M2·1.)*
- **F3 — The author always sees their own bio + tags (the owner branch), whatever their saved `Visibility` says.** *(C-M23·1.)*
- **F4 — A fresh profile's bio + tags are visible to no one but the author (the lean default: `Visibility = new()` empty denies non-owners).** *(C-M23·1, ADR 0006-C1.)*
- **F5 — A resident finds others "with something in common" via by-tag + bio-substring find-people, over profiles they may already see; one aggregate audit row per read; a profile they cannot see never surfaces.** *(C-M23·4, C-M23·6, D6.)*
- **F6 — A profile tag is a label, never a gate; its display name resolves per-viewer (ADR 0005); tagging a profile rides the frozen ADR 0044 `Tag` (no new tag type, no new gate).** *(C-M23·6, C-TG·1.)*

**The named trade.** M23 buys *a resident's biography + free author-set tags
(skills/interests/knowledge/expertise) and a find-people read over them — the
"find people with something in common" promise* with **zero new authorization
surface + two additive fields on the existing `Profile` doc + one composition
service + one extended write lane + a closed `kw-l` set**, in exchange for **no
new document, no new context, no new DocTypes surface, no inline-edit, no
full-text search, no people-discovery surface, no admin curation** (D8): M23 is
*the profile's bio + its tags + a find-people read*, **not** a profile
management system or a people network. The deferred lanes (D8·1…D8·6) are
deliberately *not* in M23: they are larger designs that need their own ADRs,
and M23's value (a neighborhood where "who knows about solar panels?" is a
question the platform can answer over content each resident already chose to
share, each read audited) is delivered by the two fields + the one service +
the reused `directory` Read path.

## §gate (acceptance tests, named — U00 locks them in the design doc)

- **GATE-1 — Bio+tags gated by `Visibility`, owner always sees own.** A
  non-audience member sees **no** bio/tags on the detail; the author does
  (owner branch); the contact block is an **independent** gate. *(C-M23·1.)*
- **GATE-2 — Find-people is over visible profiles, one aggregate row.**
  `FindPeopleByTagAsync` / `FindPeopleByBioAsync` return **only** visible
  profiles, emit **exactly one** aggregate `AccessAudit` row
  (`targetKind "directory"`, counts set, `targetId = null`), and **never**
  leak a denied profile's bio/tag. *(C-M23·4, D6.)*
- **GATE-3 — Single write lane, tag creation + verbatim bio.**
  `UpsertProfileAsync` writes `Bio` **verbatim**, resolves `TagIds` to
  `Tag.Id`s (creating missing tags + one `tag.create` row each; reusing
  present tags, no row), **no** profile-write audit row (a Profile field
  write), in-session, one `SaveChangesAsync`; `null` ⇒ don't-touch.
  *(C-M23·3, D3.)*
- **GATE-4 — Zero new authorization surface.** The seam/claim pin passes: no
  new `AccessAction` / `Decide()` branch / `AccessVia` / `IAuthorizationService`
  method / `TargetKind`; `ClaimTypes.All` unchanged. *(C-M23·2, D7.)*
- **GATE-5 — Bio is rich content, null-safe.** The bio renders via the
  existing `MarkdownRenderer.RenderHtml` helper (the "single Markdown
  engine"); `null`/empty ⇒ no bio block. *(C-M23·5, D5.)*

## Workflow (every unit, 7 steps)

1. **Read this register** (the Understanding, the [PROPOSED] D# set, the
   invariants, the FACES, the §gate, the §drift-guard, the closed `kw-l` key
   set).
2. **Read the unit plan** (`m23-uNN.md`) — the Goal, the Entry reads, the
   Deliverables, the Exit.
3. **Read the Entry reads** named in the unit plan (4–8 files, the design-doc
   sections the unit implements, the ADR, the code seams it touches).
4. **Execute** the Deliverables (≤ 7 files; implement the locked C# / keys
   exactly; do not amend a D#).
5. **Run the Exit gate** — one `dotnet build Kumunita.slnx -c Debug` + one
   `dotnet exec` test assembly, both green (U00: no build/test).
6. **Append a `## U##` section** to the handoff notes (5 lines: what was
   delivered, any open question, the next unit's entry point, no new drift).
7. **Move the unit plan** `in-progress/m23-uNN.md` → `done/m23-uNN.md` (flat,
   directly under `done/` — the M13–M21 convention, **not** a `done/m23/`
   subfolder).

## Unit-series rules

- **Order is U00 → U06.** U00 (docs + ADR) must land before any code unit,
  because the design doc's "Seams & contracts (Part 2)" section pins the exact
  C# U01–U05 implement. M23 is **already** `StatusNext` (M21's close promoted
  it ahead of M22), so **there is no open-flip unit** — U01 goes straight to the
  first code unit. A later unit may read an earlier unit's code, but never
  re-derive a D#.
- **One concern per unit.** U01 owns the two `Profile` fields + the
  `ProfileUpdate` extension (the data model). U02 owns the `UpsertProfileAsync`
  write-lane extension (bio + tag create-or-get). U03 owns
  `IProfileFindService` (the find-people read). U04 owns the editor + the
  directory-detail bio/tags gate (Web) + the editor/detail/flash `kw-l` keys.
  U05 owns the `/people` find-people surface + the nav entry (Web) + the
  find-people `kw-l` keys (incl. `nav.people`). U06 owns the close flip. No
  unit both *reads* and *writes* a seam another unit owns.
- **One test-assembly exit per unit.** Core units (U01, U02, U03) exit on
  Core.Tests; Web units (U04, U05) exit on Web.Tests. The close-flip unit (U06)
  exits on Web.Tests (it flips `Milestones.cs`, pinned by `MilestonesTests`).
  If a unit needs both, it is too big — split it (see the Atomicity contract).
- **The close flip is U06 ONLY, and ONLY it touches `Milestones.cs` /
  `MilestonesTests`.** U06: M23 `StatusNext` → `StatusDone` + **promote M22**
  `StatusPlanned` → `StatusNext` + append `"M23"` to the
  `Shipped_Milestones_Are_Marked_Done` done-list + replace
  `M23_Is_The_Single_InProgress_Milestone` with
  `M22_Is_The_Single_InProgress_Milestone` + README/STATUS/ARCHITECTURE parity
  + tag the ADR 0123 index row `**Done** (M23)` + move all M23 artifacts flat to
  `done/`. **The order in `Milestones.All` is unchanged** (M23 stays before
  M22; only the statuses move). **No other unit touches the roadmap.**
- **`kw-l` parity is authored by the unit that renders the key.** Every new
  user-visible string M23 introduces is a `KnownTranslationKeys` entry present,
  non-empty, in **all four** languages (en/de/fr/da); the
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` pin the
  closure. A unit **authors exactly the keys its surface renders** (so its own
  parity test is green at its own exit) — **U04** authors the editor + detail
  + flash keys (the `profile.edit.*` / `profile.detail.*` / `profile.flash_saved`
  set) and **U05** authors the find-people keys (the `profile.find.*` set). No
  unit consumes a key it has not authored. Both author **only** the keys in
  the locked set below — no key is invented outside it, none is duplicated.

### The closed `kw-l` key set (D5/D6) — locked by U00; U04 authors the editor/detail/flash rows, U05 authors the find-people rows

| Key | Purpose (surface) | Authored by |
| --- | --- | --- |
| `profile.edit.bio` | The editor bio field label (`/profile/edit`) | U04 |
| `profile.edit.tags` | The editor tags field label (`/profile/edit`) | U04 |
| `profile.edit.tags.placeholder` | The editor tag-input placeholder | U04 |
| `profile.detail.bio` | The biography block label (directory detail) | U04 |
| `profile.detail.tags` | The profile-tags block label (directory detail) | U04 |
| `profile.detail.tags.empty` | "No tags yet" (detail, when the bio block shows but tags are empty) | U04 |
| `profile.flash.saved` | "Profile saved" flash (`/profile/edit` POST) | U04 |
| `nav.people` | The "People" nav entry (next to Directory) | U05 |
| `profile.find.title` | The find-people page title (`/people`) | U05 |
| `profile.find.by_tag` | The "Find by tag" label + tag picker (`/people`) | U05 |
| `profile.find.by_bio` | The "Search bios" label + input (`/people`) | U05 |
| `profile.find.results` | The "X residents" result-count label (`/people`) | U05 |
| `profile.find.empty` | The empty-state ("No residents found") | U05 |

> **Reuse, don't invent:** the audience-editor labels, the "Save" submit, the
> profile-link target (`/directory/[subjectId]`), the verified-badge shape, and
> any tag-picker labels the ADR 0044 lane already ships are **not** re-keyed —
> M23 only adds the keys above (the bio/tags labels + the find-people surface +
> the nav entry).
>
> **Authorship split:** **U04** authors the editor/detail/flash keys (`profile.edit.*` / `profile.detail.*` / `profile.flash.saved`); **U05** authors the find-people keys (`nav.people` / `profile.find.*`). Each unit authors exactly the keys its surface renders, so its own parity test is green at its own exit; no unit consumes a key it has not authored.

## §drift-guard

**A unit stops (does not improvise) when it hits any of:**

- A D# it needs is not in the [PROPOSED] set, or two D#s contradict — **stop,
  report to the user**; do not pick one silently. (A D# is locked by U00; a
  post-U00 D# change is a design-doc §1.a amendment + a handoff-note line, and
  only U00 makes it.)
- A unit needs *both* test assemblies green to exit — **it is too big**; split
  it per the Atomicity contract rather than running both.
- A deliverable would require a **new `IAuthorizationService` signature, a new
  `Decide()` branch, a new `AccessVia`/`AccessAction`, or a new `TargetKind`** —
  that violates C-M23·2 / D7; stop and report (the whole point of M23 is that
  it *rides* the existing `directory` Read path via the two existing adapters,
  it does **not** extend the seams).
- A unit is about to **create a second `Tag` doc type for profiles, a new
  `ProfileBio`/`ProfileTag` document, or a new `*DocTypes` surface** — that
  violates D1 / C-M23·6; stop and report (the `Tag` is the frozen ADR 0044
  shared doc; the bio is a field on the `Profile`).
- A unit is about to **add a new `IUserInfoService` write method for
  profile tags/bio** (instead of extending `UpsertProfileAsync`) or **add a
  separate "profile tag editor" controller** — that violates D3 / C-M23·3; stop
  and report (the bio + tags ride the single F13 write lane).
- A unit is about to **introduce a per-field audience** (a distinct audience for
  the bio vs. the tags) or **fold the contact gate into the bio/tags gate** —
  that violates D2 / C-M23·1; stop and report (two independent gates, the two
  existing audiences).
- A find-people read is about to **surface a profile the viewer cannot see, leak
  a denied profile's bio/tag, or emit no aggregate audit row** — that violates
  D6 / C-M23·4; stop and report (the match is a feed organizer, never a gate;
  one aggregate row per read).
- A unit is about to **render the bio as plain text** (instead of via
  `MarkdownRenderer.RenderHtml`) or **invent a new editor / tag-input
  subsystem** — that violates D5 / C-M23·5; stop and report (reuse the
  ADR 0031/0033 `bindRichEditor` + the ADR 0044 tag input + the
  `MarkdownRenderer.RenderHtml` helper).
- A user-visible string that is **not** already in `KnownTranslationKeys` for
  all four languages is about to be rendered — add it to U05's closed set
  first; do not inline a string (the parity pin will fail, and that is the
  point).
- A unit other than **U06** is about to touch `Milestones.cs` or
  `MilestonesTests.cs` — stop; only the close-flip owns the roadmap (M23 has
  **one** flip — the close).
- A unit is about to **stage an email / notification** on a profile save or a
  find-people read — that is a D8·4 future lane, not M23; stop and report.

**The handoff-notes file is the cross-unit memory.** Every unit reads the
`## U##` sections before it and appends its own after; a unit does not re-derive
what an earlier unit already settled (a D# amendment, the key set, a seam shape,
the `Tag.Id` reference convention, the two-gate posture, the aggregate-row
shape).

**The register is the map, not the code.** If a unit is tempted to "just add a
`Decide()` branch for profiles," "create a new `ProfileTag` doc," "add a
separate tag-editor controller," or "let the find-people search leak a profile
the viewer can't see," that is the §drift-guard firing — M23's value is
precisely that it does *not*: it adds **two fields to the existing `Profile`
doc**, **rides the existing `directory` Read path** through the **two existing
adapters**, **writes through the single F13 write lane**, **finds people over
visible profiles**, and **audits every read** — with **zero new authorization
surface**.
