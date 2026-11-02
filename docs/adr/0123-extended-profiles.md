---
id: adr-0123
title: "Extended user profiles (a resident biography + free author-set tags + a find-people read)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m23-extended-profiles-design.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0123](0123-extended-profiles.md) — Extended user profiles (a resident biography + free author-set tags + a find-people read)

Status: Accepted
Date: 2026-09-26

> **Abstract:** M23 adds the **detailed non-contact profile fields** the M2
> directory deliberately deferred — the M2 design kept the
> `Profile.Visibility` audience on the data model + editor *"for the audience
> that would gate the *detailed* non-contact fields that will take it over
> once they exist."* **M23 is that moment:** a resident's **bio** (rich
> content, ADR [0025](0025-rich-content-markdown-and-content-images.md)/0031/0033) + **free author-set tags** (the **frozen ADR
> 0044 `Tag`**, `Profile.TagIds` as a **third referencer** — no new tag
> type) gated by the **existing** `Profile.Visibility` audience (the contact
> block **stays** on the existing `ContactVisibility` audience — two
> independent gates), written through the **existing single
> `UpsertProfileAsync` lane** (the profile write emits **no** audit row — a
> field write, not an access decision; only the frozen TG-lane `tag.create`
> rows are emitted), and a **find-people read** (a new `IProfileFindService`
> composition service — `FindPeopleByTagAsync` + `FindPeopleByBioAsync`, both
> computed **over profiles the viewer may already see**, gated in **one
> `CanSeeAsync` pass**, one aggregate `AccessAudit` row per read).
> **Zero new authorization surface** (no `AccessAction` / `AccessVia` /
> `Decide()` branch / `IAuthorizationService` method / `TargetKind`),
> **zero new document, zero new context, zero new DocTypes surface, zero
> migration** (two additive `Profile` fields, the ADR [0004](0004-data-persistence-and-schema-evolution.md) §B.1 shape).
> **For** a developer touching the `Kumunita.Core.UserInfo` `Profile` /
> `ProfileFindService`, the `IUserInfoService.UpsertProfileAsync` lane, the
> `/profile/edit` + directory detail surface, or the `tests/*` M23 families.
> The full field-set detail, the two-gate shape, the write-lane mechanics,
> the `IProfileFindService` composition, the rich-content editor, the
> privacy-pin, the deferred-lane catalog, and the named trade are in the
> [handoff
> note](../design/adr-handoffs.md#adr-0123--extended-user-profiles-moved-detail).

## Context

The platform already ships a **directory** (M2): every non-blocked resident's
**basic** info (name + verified badge) is listed to every signed-in viewer,
and the **contact block** (email/phone/address) is gated by the **existing**
`Profile.ContactVisibility` audience (one `CanAsync` decision, one
`AccessAudit` row; `null` short-circuits with no row — the C-M2·1 two-gate
shape). The M2 design deliberately **kept** the `Profile.Visibility` audience
on the data model + editor *"for the audience that would gate the *detailed*
non-contact fields that will take it over once they exist."* **M23 is that
moment**: the **bio** + **tags** are those detailed fields, gated by the
**existing** `Visibility` audience, while the **contact block stays** on the
**existing** `ContactVisibility` audience.

The platform also already ships the **frozen ADR [0044](0044-tags.md) `Tag`** (free author-set
subject labels for posts and blog pages — the ADR [0011](0011-media-and-file-storage.md) shared-id-doc shape,
referenced by `Post.TagIds` / `Page.TagIds`) and the **rich-content lane**
(ADR [0025](0025-rich-content-markdown-and-content-images.md)/0031/0033 — Markdown + content images, one
`MarkdownRenderer.RenderHtml` engine on the read path, the `bindRichEditor`
WYSIWYG on the authoring path). What the platform does *not* have is a
**resident's biography + free author-set tags** on the **profile**, and a
**find-people read** over them.

The constraints the choice must honor (all pre-existing, not new):
**audit-by-default** (SECURITY.md §3, ADR [0006](0006-module-boundary-contracts.md)-C) — a find-people read over
audience-restricted bios is computed **over profiles the viewer may already
see** (the D6 privacy-pin; the match is a feed organizer, never a gate);
**the `Audience` is the whole access story** (ADR [0006](0006-module-boundary-contracts.md)-D) — the profile's
**two existing** audiences are the *only* access carriers (D2, C-M23·1); a
profile tag is a **label, never a gate** (C-TG·1 carried to profiles).
**`Core` stays HTTP-free** (ADR [0006](0006-module-boundary-contracts.md)-D); **Marten owns the domain documents**
(ADR [0004](0004-data-persistence-and-schema-evolution.md) §B) — the two new fields are additive on the **existing** `Profile`
doc (D1); **Lean + Boring, one database** (README principles, ADR [0002](0002-deployment-topology.md)) — no
new doc, no new context, no new `AccessAction`, no new `ITagService` method,
no new tag type, no new storage lane.

The shape that keeps M23 safe (C-M23·2): **ride the frozen `Read` path
through the two existing adapters, gate bio + tags by the existing
`Visibility` audience, tag through the frozen ADR [0044](0044-tags.md) `Tag`, find people
over visible profiles, audit every read** — and **add zero new
authorization surface** (D7). The profile write is a **single
`UpsertProfileAsync` lane** with **no profile-write audit row** (a field
write, not an access decision); only the **frozen TG-lane** `tag.create`
rows are emitted, one per newly created tag.

## Decision

- **D1 — `Profile` gains two additive fields; no new document, no new
  DocTypes surface (ADR [0004](0004-data-persistence-and-schema-evolution.md) §B.1).** The M1-registered `Profile` doc gains
  exactly two **additive POCO fields** (delta-detected, idempotent, **no
  re-seed**, **no EF migration**, **no new `*DocTypes` surface** — `Profile`
  already registers in `CoreDocTypes`): `public string? Bio` (null/empty ⇒
  no bio block) and `public string[] TagIds = []` (a `string[]` of **`Tag.
  Id`** values referencing the **frozen ADR 0044 `Tag`** — the
  `Post.TagIds` / `Page.TagIds` idiom verbatim; M23 adds `Profile` as a
  **third referencer**, creating **no** new tag type). *Forbids:* a new
  `ProfileBio` / `ProfileTag` document, a new `M23DocTypes` surface, a second
  `Tag` doc type for profiles, or storing the bio as a separate doc. See the
  handoff note for the full detail.
- **D2 — Bio + tags are gated by the existing `Profile.Visibility` audience;
  the contact block stays on `Profile.ContactVisibility` (the M2 two-gate
  shape).** Bio + tags render on the directory detail **only** when the
  viewer's `Visibility` decision allows (one `CanAsync` via the **existing**
  `ProfileToAuditableResource`; the **owner branch** lets the author always
  see their own). The contact block **stays** on the **existing**
  `ContactVisibility` audience — **two independent gates, two `CanAsync`
  calls, two `AccessAudit` rows**; a **self-view** short-circuits both; a
  `null`/absent `ContactVisibility` short-circuits the contact gate with no
  row (the C-M2·1 shape). *Forbids:* a **new** `AccessAction` / `AccessVia` /
  `Decide()` branch, a new `IAuditableResource` adapter, a new `TargetKind`,
  a **per-field** audience, or folding the contact gate into the bio/tags
  gate. See the handoff note for the full detail.
- **D3 — Bio + tags are written through the existing single
  `IUserInfoService.UpsertProfileAsync` lane (the F13 single-write-surface
  pin, extended — **no** new method).** `ProfileUpdate` gains two additive
  optional trailing fields (`string? Bio = null`,
  `IReadOnlyCollection<string>? TagIds = null` — the `Address` precedent, so
  existing positional call sites compile unchanged). `UpsertProfileAsync`
  writes `Profile.Bio` **verbatim** and resolves `Profile.TagIds` to `Tag.Id`
  values via the **frozen ADR [0044](0044-tags.md) create-or-get idiom** (a missing `Slug`
  **creates** a `Tag` + one `tag.create` row; a present `Slug` **reuses** the
  existing `Tag`, no row). The profile write emits **no** `AccessAudit` row;
  the **only** audit rows are the **frozen TG-lane** `tag.create` rows, one
  per newly created tag (C-TG·9). **All in the caller's session, one
  `SaveChangesAsync`** (C3). *Forbids:* a new `IUserInfoService` write method
  for profile tags/bio, a new `ITagService` profile method, a separate
  "profile tag editor" controller, or an unaudited direct `session.Store`.
  See the handoff note for the full mechanics.
- **D4 — A new composition service `IProfileFindService` (the
  `DirectoryService` shape) in `Kumunita.Core.UserInfo` — **no** new context,
  **no** new DocTypes, **no** new document.** It composes **only** the frozen
  seams (`IUserInfoService` for the candidate set, `IAuthorizationService`
  for the per-profile `Visibility` decision, the **frozen ADR [0044](0044-tags.md)
  `ITagService`** for tag display-name resolution + its own
  `IDocumentStore`). Two **paged** read methods (`PageSize = 30`, the M7/ADR
  0090 D6 idiom): `FindPeopleByTagAsync(slug, actorId, page)` and
  `FindPeopleByBioAsync(q, actorId, page)` (a case-insensitive bio substring,
  the M8 D4 engine). Both: candidate set = `GetProfilesAsync(false)`
  (non-blocked); filter by the match; **gate the candidate set in ONE
  `CanSeeAsync` pass** (the frozen bulk decision — **not** a per-profile
  `CanAsync` loop); return survivors + `HasMore`; the **one aggregate
  `AccessAudit` row** (`targetKind "directory"`) is **emitted by
  `CanSeeAsync` itself**, not hand-written. A blank `q` / a missing tag
  returns an empty page with **no** decision and **no** row. See the handoff
  note for the full detail + the *forbids*.
- **D5 — The bio is rich content (ADR [0025](0025-rich-content-markdown-and-content-images.md)/0031/0033); the editor reuses the
  existing `bindRichEditor` WYSIWYG + the frozen ADR [0044](0044-tags.md) tag input; the
  detail reuses the existing `MarkdownRenderer.RenderHtml` helper.** The
  profile editor (`/profile/edit`) adds a **bio** field (the WYSIWYG editor
  over a `string` field — exactly the surface posts/announcements/pages use)
  + a **tags** input (the composer's tag picker +
  `ITagService.SuggestAsync` autocomplete). The existing two audience editors
  are **unchanged**. The directory detail renders the bio via the **existing**
  `MarkdownRenderer.RenderHtml` helper — `null`/empty bio ⇒ no bio block.
  Profile tags render as chips. See the handoff note for the full detail.
- **D6 — Privacy-pin.** Find-people results are computed over profiles the
  viewer may **already see**; a match is a *feed organizer*, never a gate; a
  profile the viewer cannot see never surfaces and its bio/tag never leaks
  (the ADR [0044](0044-tags.md) D5 global-enumeration exclusion carried to profiles). Both
  reads filter the candidate set by the match, **then** gate each survivor by
  its `Visibility`; a denied profile **never** appears and its `Bio`/`TagIds`
  are never a match artifact. A **global tag enumeration** over profiles is
  excluded by design. Each read emits **one** aggregate `AccessAudit` row.
  See the handoff note for the full detail + the *forbids*.
- **D7 — Zero new authorization surface (the C-M23·2 pin).** M23 adds **no**
  `AccessAction`, **no** `Decide()` branch, **no** `AccessVia` value, **no**
  `IAuthorizationService` method, **no** `TargetKind`; the claim set
  (`ClaimTypes.All`) is **unchanged**. It reuses exactly the **two existing**
  profile adapters (`ProfileToAuditableResource` `TargetKind "directory"` +
  `ContactVisibilityResource`) + `AccessAction.Read` + the **frozen ADR [0044](0044-tags.md)
  `ITagService`** read lanes, and adds **one** composition service
  (`IProfileFindService`). *Forbids:* any extension of the frozen
  authorization seams.
- **D8 — Deferred lanes (each a future ADR, not part of M23).** (1) Inline
  WYSIWYG editing of the bio on the detail view; (2) bio full-text / semantic
  search; (3) a people-discovery *surface*; (4) a "new bio/tags set"
  notification (rides M6); (5) profile-tag curation by an admin; (6) bio
  image alt-text / captioning. See the handoff note for the full catalog.

## Consequences

- **A resident's biography + free author-set tags + a find-people read, end-
  to-end** (the README M23 line): a resident sets a bio + tags on their own
  profile (D3, `Visibility`-gated); the bio + tags render on the directory
  detail **only** when the viewer's `Visibility` allows, the contact block
  under the **unchanged** `ContactVisibility` gate (D2, two independent
  gates); the author always sees their own (owner branch); a fresh profile's
  bio + tags are visible to no one but the author (the lean default, ADR
  0006-C1); and a resident finds others "with something in common" via by-tag
  + bio-substring, over profiles they may already see, one aggregate audit row
  per read (D4/D6). No new authorization surface (C-M23·2).
- **Zero new authorization surface — the strongest form** (C-M23·2, D7): the
  read verdict is the **frozen** `IAuthorizationService` `Read` decision path
  (D4/D6) through the **two existing** profile adapters — the only new seam
  is **one** composition service (`IProfileFindService`). The claim set
  (`ClaimTypes.All`) is **byte-identical** after M23.
- **The bio is a field on the `Profile`; the tags reference the frozen ADR
  0044 `Tag`** (D1, C-M23·6): **no** new document, **no** new context, **no**
  new DocTypes surface, **no** migration (the ADR [0004](0004-data-persistence-and-schema-evolution.md) §B.1 additive field
  lane).
- **The bio is rich content, null-safe** (D5, C-M23·5): rendered by the
  **existing** `MarkdownRenderer.RenderHtml` helper; `null`/empty ⇒ no bio
  block.
- **The write is a single `UpsertProfileAsync` lane** (D3, C-M23·3): bio
  verbatim + tag create-or-get (one `tag.create` row per new tag; reuse, no
  row), **no** profile-write audit row, in-session, one `SaveChangesAsync`.
- **Neutral / cost + the named trade + the deferred-lane catalog** — the
  FACES trade (a resident biography + tags + a find-people read for zero new
  authorization surface, in exchange for no inline-edit / no full-text search
  / no people-discovery surface / no admin curation), the two-independent-
  gates shape, and the find-people-over-visible-profiles pin are in the
  handoff note.
