# ADR 0123 — Extended user profiles (a resident biography + free author-set tags + a find-people read)

Status: **Accepted** (M23, in-progress — U00 sign-off; `**Done** (M23)` is
tagged by U06 at close)
Date: 2026-10-01

The README roadmap names M23 exactly: "**Extended user profiles** — a
biography + free author-set tags (skills, interests, knowledge, expertise) to
make it easier to find people with something in common." "a biography" is the
**new `Profile.Bio` field** (rich content, the ADR 0025/0031/0033 lane, D1/D5);
"free author-set tags (skills, interests, knowledge, expertise)" is the **new
`Profile.TagIds: string[]` field referencing the frozen ADR 0044 `Tag` by
`Tag.Id`** (the `Post.TagIds` / `Page.TagIds` idiom, D1, C-M23·6); "to make it
easier to find people with something in common" is the **new
`IProfileFindService`** find-people read (by-tag + bio-substring, D4).

M23 rides **frozen** seams, extending none of them:

- **The frozen `IAuthorizationService` `Read` path (ADR 0006)** — the single
  decision path. M23 **reuses the two existing** profile adapters
  (`ProfileToAuditableResource`, `TargetKind "directory"`, `Audience =>
  Profile.Visibility` — gates **bio + tags**; `ContactVisibilityResource` —
  gates the **contact block**) + `AccessAction.Read` + the **frozen ADR 0044
  `ITagService`** read lanes (tag display names). M23 adds **no** new
  `AccessAction`, **no** new `AccessVia`, **no** new `Decide()` branch, **no**
  new `IAuthorizationService` method, **no** new `TargetKind`, and **no** new
  claim type (C-M23·2, D7) — it rides the existing `directory` Read path
  through the **existing** adapters + one **new composition service**
  (`IProfileFindService`, the `DirectoryService` shape) that composes the
  frozen seams exactly like `DirectoryService`.
- **The frozen ADR 0044 `Tag` shared-id doc + `ITagService`** — `Profile.TagIds`
  is a **third referencer** of the **same** `Tag` the `Post.TagIds` /
  `Page.TagIds` already reference (the ADR 0011 shared-id-doc shape). The
  create-or-get write reuses the **frozen** `TagService.AttachToPostAsync`
  idiom (one `tag.create` row per newly created tag, C-TG·9; a present `Slug`
  is reused, no row). M23 adds **no** new `Tag` doc type, **no** new
  `*DocTypes` surface (D1).
- **The ADR 0025/0031/0033 rich-content lane** — the bio is **rich content**
  (Markdown + optional in-content images) rendered read-only by the **existing**
  `MarkdownRenderer.RenderHtml` helper (the "single Markdown engine"), authored
  in the existing `bindRichEditor` WYSIWYG (D5, C-M23·5).
- **The ADR 0004 §B.1 additive-surface discipline** — the two new fields are
  **additive POCO fields on the existing `Profile` doc** (delta-detected,
  idempotent, **no re-seed**, **no migration**, **no new DocTypes surface** —
  `Profile` already registers in `M1DocTypes`; D1).

## Context

The platform already ships a **directory** (M2): every non-blocked resident's
**basic** info (name + verified badge) is listed to every signed-in viewer, and
the **contact block** (email/phone/address) is gated by the **existing**
`Profile.ContactVisibility` audience (one `CanAsync` decision, one
`AccessAudit` row; `null` short-circuits with no row — the C-M2·1 two-gate
shape). The M2 design deliberately **kept** the `Profile.Visibility` audience on
the data model + editor *"for the audience that would gate the *detailed*
non-contact fields that will take it over once they exist."* **M23 is that
moment**: the **bio** + **tags** are those detailed fields, gated by the
**existing** `Visibility` audience, while the **contact block stays** on the
**existing** `ContactVisibility` audience.

The platform also already ships the **frozen ADR 0044 `Tag`** (free author-set
subject labels for posts and blog pages — the ADR 0011 shared-id-doc shape,
referenced by `Post.TagIds` / `Page.TagIds`; `Slug` the business key, `Name`
display, `CreatedBy` the translate-standing owner) and the **rich-content lane**
(ADR 0025/0031/0033 — Markdown + content images, one `MarkdownRenderer.RenderHtml`
engine on the read path, the `bindRichEditor` WYSIWYG on the authoring path).
What the platform does *not* have is a **resident's biography + free author-set
tags** on the **profile**, and a **find-people read** over them.

The constraints the choice must honor (all pre-existing, not new):

- **Audit-by-default** (SECURITY.md §3, ADR 0006-C): every audience-restricted
  read is resolved per request and logged; the floor is **deny** (an empty
  audience denies everyone — ADR 0006-C1). A find-people read over
  audience-restricted bios must therefore be computed **over profiles the
  viewer may already see** (the D6 privacy-pin; the match is a feed organizer,
  never a gate).
- **The `Audience` is the whole access story** (ADR 0006-D): "feature modules
  never re-derive access" — the profile's **two existing** audiences are the
  *only* access carriers (D2, C-M23·1); a profile tag is a **label, never a
  gate** (C-TG·1 carried to profiles, C-M23·6).
- **`Core` stays HTTP-free** (ADR 0006-D): the find-people service composes the
  frozen `IUserInfoService` + `IAuthorizationService` + `ITagService` seams; it
  never reads `GroupMembership`/`DelegationGrant` for its own access decisions.
- **Marten owns the domain documents** (ADR 0004 §B): the two new fields are
  additive fields on the **existing** `Profile` doc (D1) — **no** new document,
  **no** new DocTypes surface, **no** migration.
- **Lean + Boring, one database** (README principles, ADR 0002): no new doc, no
  new context, no new `AccessAction`, no new `ITagService` method, no new tag
  type, no new storage lane (D1/D3/D7).

The shape that keeps M23 safe (C-M23·2): **ride the frozen `Read` path through
the two existing adapters, gate bio + tags by the existing `Visibility`
audience, tag through the frozen ADR 0044 `Tag`, find people over visible
profiles, audit every read** — and **add zero new authorization surface** (D7).
The profile write is a **single `UpsertProfileAsync` lane** with **no
profile-write audit row** (a field write, not an access decision — the existing
`UpsertProfileAsync` / `SetProfileTimezoneAsync` / `SetProfileAvatarAsync`
convention; only the **frozen TG-lane** `tag.create` rows are emitted, one per
newly created tag).

## Decision

**D1 — `Profile` gains **two additive fields**; no new document, no new DocTypes
surface (ADR 0004 §B.1).** The M1-registered `Profile` doc
(`Kumunita.Core.UserInfo`) gains exactly two **additive POCO fields** (the
`Post.Status` / `Post.TagIds` / `Profile.AvatarId` / `Profile.TimeZone` history
— delta-detected, idempotent, **no re-seed**, **no EF migration**, **no new
`*DocTypes` surface** — `Profile` already registers in `M1DocTypes`):
`public string? Bio { get; set; }` (null/empty ⇒ no bio block) and
`public string[] TagIds { get; set; } = []` (a `string[]` of **`Tag.Id`** values
referencing the **frozen ADR 0044 `Tag`** — the `Post.TagIds` / `Page.TagIds`
idiom verbatim; M23 adds `Profile` as a **third referencer**, creating **no**
new tag type). *Forbids:* a new `ProfileBio` / `ProfileTag` document, a new
`M23DocTypes` surface, a new relational table, a second `Tag` doc type for
profiles, or storing the bio as a separate doc.

**D2 — Bio + tags are gated by the **existing** `Profile.Visibility` audience;
the contact block **stays** on `Profile.ContactVisibility` (the M2 two-gate
shape).** Bio + tags render on the directory detail **only** when the viewer's
`Visibility` decision allows (one `CanAsync` via the **existing**
`ProfileToAuditableResource`; the **owner branch** lets the author always see
their own). The contact block (email/phone/address) **stays** on the
**existing** `ContactVisibility` audience + `ContactVisibilityResource`
decision — **two independent gates, two `CanAsync` calls, two `AccessAudit`
rows**; a **self-view** short-circuits both (owner branch); a `null`/absent
`ContactVisibility` short-circuits the contact gate with no row (the existing
C-M2·1 shape). *Forbids:* a **new** `AccessAction` / `AccessVia` / `Decide()`
branch, a new `IAuditableResource` adapter (the two existing ones are reused),
a new `TargetKind` (`"directory"` is reused), a **per-field** audience (bio and
tags share the *one* `Visibility` gate), or folding the contact gate into the
bio/tags gate.

**D3 — Bio + tags are written through the **existing** single
`IUserInfoService.UpsertProfileAsync` lane (the F13 single-write-surface pin,
extended — **no** new method).** `ProfileUpdate` gains **two additive optional
trailing fields** (`string? Bio = null`, `IReadOnlyCollection<string>? TagIds =
null` — the `Address` precedent, "appended with a default after the frozen shape
so existing positional call sites compile unchanged"; `null` ⇒ don't touch).
`UpsertProfileAsync` writes `Profile.Bio` **verbatim** (ADR 0001-B; a `null`
patch leaves it) and resolves `Profile.TagIds` to `Tag.Id` values via the
**frozen ADR 0044 create-or-get idiom** (inlined in the `UserInfoService`
session — the `TagService.AttachToPostAsync` shape; a missing `Slug` **creates**
a `Tag` with `CreatedBy = actorId` + one `tag.create` row; a present `Slug`
**reuses** the existing `Tag`, no row). The profile write itself emits **no**
`AccessAudit` row (a Profile field write, not an access decision — the existing
`UpsertProfileAsync` shape); the **only** audit rows are the **frozen TG-lane**
`tag.create` rows, one per newly created tag (C-TG·9). **All in the caller's
session, one `SaveChangesAsync`** (C3). *Forbids:* a **new** `IUserInfoService`
write method for profile tags/bio, a **new** `ITagService` profile method, a
separate "profile tag editor" controller, or an unaudited direct `session.Store`
from a controller.

**D4 — A **new composition service** `IProfileFindService` (the
`DirectoryService` shape) in `Kumunita.Core.UserInfo` — **no** new context,
**no** new DocTypes, **no** new document.** `IProfileFindService` +
`ProfileFindService` live in `Kumunita.Core.UserInfo` (alongside
`DirectoryService`), registered in `DependencyInjection.cs` the same way
`DirectoryService` is. It composes **only** the frozen seams —
`IUserInfoService` (the candidate set via the existing `GetProfilesAsync`),
`IAuthorizationService` (the per-profile `Visibility` decision), the **frozen
ADR 0044 `ITagService`** (tag display-name resolution) + its own
`IDocumentStore`. Two **paged** read methods (`PageSize = 30`, the M7/ADR 0090
D6 idiom): `FindPeopleByTagAsync(slug, actorId, page)` (the C-TG·2 base query,
carried to profiles) and `FindPeopleByBioAsync(q, actorId, page)` (a
case-insensitive bio substring, the M8 D4 engine). Both: candidate set =
`GetProfilesAsync(false)` (non-blocked); filter by the match; **gate the
candidate set in ONE `CanSeeAsync` pass** over
`candidates.Select(p => new ProfileToAuditableResource(p))` (the frozen bulk
decision — **not** a per-profile `CanAsync` loop; the owner branch resolves
internally); return survivors + `HasMore`; the **one aggregate `AccessAudit`
row** (`targetKind "directory"`, `visibleCount`/`hiddenCount` set,
`targetId = null`) is **emitted by `CanSeeAsync` itself** (its own commit),
**not hand-written**. A blank `q` / a missing tag returns an empty page with
**no** decision and **no** row. *Forbids:* a new bounded context, a new DocTypes
surface, a new document, a new `AccessAction`/`AccessVia`, a **per-profile
`CanAsync` loop**, a **hand-written aggregate `AccessAudit` row**, a
find-people read that surfaces a profile the viewer cannot see, or a
find-people read that emits **no** aggregate audit row.

**D5 — The bio is **rich content** (ADR 0025/0031/0033); the editor **reuses**
the existing `bindRichEditor` WYSIWYG + the frozen ADR 0044 tag input; the
detail **reuses** the existing `MarkdownRenderer.RenderHtml` helper.** The
profile editor (`/profile/edit`) adds a **bio** field (the ADR 0031/0033
WYSIWYG editor, or a Markdown textarea, over a `string` field — exactly the
surface posts/announcements/pages use) + a **tags** input (the **frozen ADR 0044
tag input** — the composer's tag picker + `ITagService.SuggestAsync`
autocomplete). The existing two audience editors (`Visibility` +
`ContactVisibility`) are **unchanged**. The directory detail renders the bio via
the **existing** `MarkdownRenderer.RenderHtml` helper (the "single Markdown
engine") — `null`/empty bio ⇒ no bio block (no placeholder). Profile tags render
as chips (the ADR 0044 tag display-name idiom, resolved per-viewer). *Forbids:*
a **new** editor subsystem, a **new** tag-management surface for profiles,
inline WYSIWYG editing on the detail view (the ADR 0032/0033 lane — deferred,
D8·1), or a bio rendered as plain text.

**D6 — **Privacy-pin:** find-people results are computed over profiles the
viewer may **already see**; a match is a *feed organizer*, never a gate; a
profile the viewer cannot see never surfaces and its bio/tag never leaks (the
ADR 0044 D5 global-enumeration exclusion carried to profiles).** Both reads
filter the candidate set by the match, **then** gate each survivor by its
`Visibility`; a denied profile **never** appears and its `Bio`/`TagIds` are
never a match artifact. The tag's *display name* may surface (it is a label,
ADR 0044 D5) but only as part of a profile the viewer may already see. A
**global tag enumeration** over profiles is excluded by design. Each read emits
**one** aggregate `AccessAudit` row. *Forbids:* a bio/tag match that returns a
profile the viewer cannot see, a global profile-tag enumeration that leaks a
subject, or an unaudited find-people read.

**D7 — **Zero new authorization surface** (the C-M23·2 pin).** M23 adds **no**
`AccessAction`, **no** `Decide()` branch, **no** `AccessVia` value, **no**
`IAuthorizationService` method, **no** `TargetKind`; the claim set
(`ClaimTypes.All`) is **unchanged**. It reuses exactly the **two existing**
profile adapters (`ProfileToAuditableResource` `TargetKind "directory"` +
`ContactVisibilityResource`) + `AccessAction.Read` + the **frozen ADR 0044
`ITagService`** read lanes, and adds **one** composition service
(`IProfileFindService`). *Forbids:* any extension of the frozen authorization
seams (a §drift-guard stop — the whole point of M23 is that it *rides* the
existing `directory` Read path, it does not extend it).

**D8 — **Deferred lanes** (each a future ADR, not part of M23).** (1) **Inline
WYSIWYG editing of the bio on the detail view** (the ADR 0032/0033 inline-edit
lane). (2) **Bio full-text / semantic search** (beyond substring). (3) **A
people-discovery *surface*** (facet browse, "people in common," a
`/people/skills/{tag}` taxonomy, shared-interests). (4) **A "new bio/tags set"
notification** — rides M6 (ADR 0076); M23 stages **no** email. (5) **Profile-tag
curation by an admin** (a moderator/global-admin manages another resident's
tags) — M23 is author-self-edit only (the ADR 0003 SoD lane is untouched). (6)
**Bio image alt-text / captioning** — rides the ADR 0025/0011 image lane; M23
reuses it as-is.

## Consequences

**Positive:**

- **A resident's biography + free author-set tags + a find-people read, end-to-end**
  (the README M23 line): a resident sets a bio + tags on their own profile
  (D3, F1, `Visibility`-gated — the author's choice, ADR 0001-B); the bio + tags
  render on the directory detail **only** when the viewer's `Visibility` allows,
  the contact block under the **unchanged** `ContactVisibility` gate (D2, F2,
  two independent gates — GATE-1); the author always sees their own (owner
  branch, F3); a fresh profile's bio + tags are visible to no one but the author
  (the lean default, F4, ADR 0006-C1); and a resident finds others "with
  something in common" via by-tag + bio-substring, over profiles they may
  already see, one aggregate audit row per read (D4/D6, F5 — GATE-2). No new
  authorization surface (C-M23·2, GATE-4).
- **Zero new authorization surface — the strongest form** (C-M23·2, D7): M23
  adds **no** new `AccessAction`, **no** new `AccessVia`, **no** branch in
  `Decide()`, **no** new `IAuthorizationService` method, **no** new
  `TargetKind`, **no** new claim *type*. The read verdict is the **frozen**
  `IAuthorizationService` `Read` decision path (D4/D6) through the **two
  existing** profile adapters — the only new seam is **one** composition
  service (`IProfileFindService`, the `DirectoryService` shape). The claim set
  (`ClaimTypes.All`) is **byte-identical** after M23 (the GATE-4 pin).
- **The bio is a field on the `Profile`; the tags reference the frozen ADR 0044
  `Tag`** (D1, C-M23·6): **no** new document, **no** new context, **no** new
  DocTypes surface, **no** migration (the ADR 0004 §B.1 additive field lane) —
  `Profile` already registers in `M1DocTypes`; adding two fields is delta-
  detected and idempotent, every existing row unchanged.
- **The bio is rich content, null-safe** (D5, C-M23·5): rendered by the
  **existing** `MarkdownRenderer.RenderHtml` helper (the "single Markdown
  engine"); `null`/empty ⇒ no bio block (GATE-5).
- **The write is a single `UpsertProfileAsync` lane** (D3, C-M23·3): bio verbatim
  + tag create-or-get (one `tag.create` row per new tag; reuse, no row), **no**
  profile-write audit row (a field write, not an access decision), in-session,
  one `SaveChangesAsync` (GATE-3).

**Neutral / cost:**

- **The named trade — a *resident biography + free author-set tags + a
  find-people read* for zero new authorization surface:** M23 buys *a
  resident's biography + free author-set tags (skills/interests/knowledge/
  expertise) and a find-people read over them — the "find people with something
  in common" promise* with **zero new authorization surface + two additive
  fields on the existing `Profile` doc + one composition service + one extended
  write lane + a closed `kw-l` set** (12 keys × en/de/fr/da), in exchange for
  **no new document, no new context, no new DocTypes surface, no inline-edit,
  no full-text search, no people-discovery surface, no admin curation** (D8):
  M23 is *the profile's bio + its tags + a find-people read*, **not** a profile
  management system or a people network. The deferred lanes (D8·1…D8·6) are
  deliberately *not* in M23: they are larger designs that need their own ADRs.
- **Two independent gates** (D2, C-M23·1): bio + tags are gated by the **one**
  existing `Visibility` audience; the contact block **stays** on the **one**
  existing `ContactVisibility` audience — **two audiences, two gates, zero new
  ones** (no per-field audience, the contact gate not folded into the
  bio/tags gate).
- **The find-people read is over visible profiles only** (D6, C-M23·4): a match
  is a feed organizer, never a gate; a denied profile's bio/tag never leaks; one
  aggregate audit row per read (emitted by the frozen `CanSeeAsync`, not
  hand-written).

**Follow-on lanes (each its own ADR — the design doc's §10):** inline WYSIWYG
editing of the bio on the detail view (D8·1); bio full-text / semantic search
(D8·2); a people-discovery *surface* (D8·3); a "new bio/tags set" notification
(D8·4); profile-tag curation by an admin (D8·5); bio image alt-text /
captioning (D8·6).

## Amendments

- **2026-10-01 — One clarifying amendment, locked in U00 (design doc §1.a C1;
  the register's [PROPOSED] set locked as-is, the unit plan's §2.4 gate wording
  superseded in-unit):**
  - **C1 — The D4 gate mechanism: locked at the **register's** single
    `CanSeeAsync` pass.** The unit plan `m23-u00.md` §2.4 restated the D4 gate
    as "**gate each survivor** by its `Profile.Visibility` (one `CanAsync` per
    survivor via the existing `ProfileToAuditableResource`)"; the register's D4
    (the tier-1 authority) locks it as "**gate the candidate set in ONE
    `CanSeeAsync` pass** over `candidates.Select(p => new ProfileToAuditableResource(p))`
    — **not** a per-profile `CanAsync` loop." **Locked at the register's
    wording.** The `CanSeeAsync` bulk decision (the M3 `PostService.ListFeedAsync`
    / M21 `DocumentService.ListAsync` idiom) returns a `VisibleSet`
    (`Visible` ids + `HiddenCount`) and emits **exactly one aggregate**
    `AccessAudit` row (`targetKind "directory"`, `visibleCount`/`hiddenCount`
    set, `targetId = null`) — exactly the C-M23·4 shape. A per-survivor
    `CanAsync` loop would emit **one decision row per profile** (the
    "multiple rows" drift the §drift-guard forbids), and it would special-case
    the owner branch (F3) that `CanSeeAsync` resolves internally. U03 implements
    the **single `CanSeeAsync` pass**; the unit plan's per-survivor wording is
    **superseded**. No D# text is rewritten — this is the single
    D#-relevant clarification in the unit.

## Supersedes

- **None.** M23 **extends the existing `Profile` doc + adds one composition
  service**; it does not supersede an earlier ADR. It *rides* ADR 0006 (the
  frozen `Read` path + the two existing profile adapters + the deny-by-default
  floor), ADR 0001-B (the `Visibility` audience + bio written verbatim), ADR
  0004 §B.1 (the additive-field discipline — no new DocTypes surface, no
  migration), ADR 0044 (the frozen `Tag` shared-id doc + the `ITagService`
  create-or-get + the tag display-name resolution + the D5 global-enumeration
  exclusion), ADR 0025/0031/0033 (the rich-content lane + the `bindRichEditor`
  WYSIWYG + the single `MarkdownRenderer.RenderHtml` engine), and ADR 0090 D6 /
  M8 D4 (the `PageSize = 30` idiom + the bio-substring engine). The two existing
  profile adapters, the frozen ADR 0044 `Tag`/`ITagService` seams, and the
  `M1DocTypes` registration are **unchanged** (C-M23·2/6). This is stated
  explicitly so a later reader knows M23 is an **additive profile + find-people
  surface, not a correction** — no earlier ADR's decisions are revised or
  re-scoped by this one.

## Affected files

- `src/Kumunita.Core/UserInfo/Profile.cs` — the two additive fields (`Bio`,
  `TagIds`) (D1) + the `ProfileUpdate` extension (`Bio`, `TagIds`) (D3) (U01).
- `src/Kumunita.Core/UserInfo/UserInfoService.cs` (`UpsertProfileAsync`) — the
  bio verbatim write + the inlined ADR 0044 tag create-or-get, no profile-write
  audit row, one `SaveChangesAsync` (D3) (U02).
- `src/Kumunita.Core/UserInfo/IProfileFindService.cs` + `ProfileFindService.cs`
  — new (the find-people read: `FindPeopleByTagAsync` / `FindPeopleByBioAsync`
  + the `ProfileTagPage` / `ProfileBioPage` DTOs) (D4); `src/Kumunita.Web/
  Program.cs` or `Kumunita.Core/DependencyInjection.cs` — the one
  `AddScoped<IProfileFindService, ProfileFindService>` registration (the
  `DirectoryService` shape) (D4) (U03).
- `src/Kumunita.Web/Controllers/ProfileController.cs` — the `Edit` extension
  (bio + tags surfaces) + the directory detail gate (bio + tags behind
  `Visibility`) (D2/D3/D5) (U04).
- `src/Kumunita.Web/Controllers/FindPeopleController.cs` — new (the `/people`
  by-tag + by-bio find-people surface + the nav entry) (D4/D6) (U05).
- `src/Kumunita.Web/Models/` + `src/Kumunita.Web/Views/` — the profile
  editor/detail + find-people view models + views (U04/U05).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` + the four locale
  files — the 12 `profile.*` / `nav.people` `kw-l` keys in en/de/fr/da (U04 the
  editor/detail/flash rows; U05 the find-people rows; the closed key set §7 of
  the design doc).
- `src/Kumunita.Web/Milestones.cs` + `README.md` + `docs/STATUS.md` +
  `docs/ARCHITECTURE.md` + `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the
  **one** milestone flip (U06 close: M23 → `StatusDone` + M22 promoted to
  `StatusNext` + the pin re-pinned to `M22_Is_The_Single_InProgress_Milestone`).
- **New tests** (the GATE-1…5 pins): `tests/Kumunita.Core.Tests/` — the
  bio+tags-`Visibility`-gated/owner-sees-own + contact-block-independent pin
  (GATE-1), the find-people visible-only + one-aggregate-row + no-leak pin
  (GATE-2), the single-write-lane verbatim-bio + tag-creation + no-audit-row
  pin (GATE-3), the zero-new-authorization-surface pin (GATE-4);
  `tests/Kumunita.Web.Tests/` — the rich-render + null-safe bio pin (GATE-5) +
  the `kw-l` closure pin (the `KwLRegistryConsistencyTests` /
  `KnownTranslationKeys_ParityTests`).
