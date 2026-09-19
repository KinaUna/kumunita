# ADR 0048 — Edit + remove lanes for user-added translations

Status: Accepted
Date: 2026-09-28

## Context

Every user-added translation lane on the platform shipped **add-only**:

- **Posts + replies** — ADR 0022 ("no separate 'edit' or 'delete' seam,
  matching ADR 0022's add-only shape"), re-confirmed for the group lane by
  ADR 0027 (the display/swap surface reads the rows but adds no write lane).
- **Announcements** — ADR 0029 (add lane; no update/remove seam).
- **Group + community name/description** — ADR 0026 ("**Add-only lane,
  at-least-one field.** There is no edit or remove of a translation row").
- **Pages** — ADR 0039 / 0047 (the backfill is create-if-missing; no
  edit/remove of a user-added row).

The consequence is visible to residents and to the small set of actors who
can write translations (authors, owners, GlobalAdmin, Translator — the
standing matrix each ADR already fixed):

- A `fr` translation row with a typo **cannot be fixed**. The only lever is
  to add a *different* language's row, which does not touch the `fr` row.
- A translation that is wrong (a mistranslation, a stale description)
  **cannot be withdrawn** without a direct database edit.

The add lanes are settled and tested; the standing resolvers behind them
(`PostService.ResolveTranslationStanding`, `AnnouncementService.
ResolveTranslationStanding`, `UserInfoService.ResolveGroupTranslationStanding`
/ `ResolveCommunityTranslationStanding`, `PageService.
ResolveTranslationStandingVia`) already compute the *exact* set of actors who
may write a translation row for a given parent — the same set should be the
ones who may correct or withdraw a row they are entitled to write. There is
no standing question this ADR needs to settle; it only lifts the "add-only"
pin on the surface each ADR already scoped, on that ADR's own standing
matrix and failure shape.

**What this is, in one line:** every user-added translation row gains an
**update** and a **remove** lane — same parent, same language, same standing
as the add lane — on the six surfaces (post, reply, announcement, group
name/description, community name/description, page), each with its own audit
action and its own surface's existing failure shape.

## Decision

### D1 — Six surfaces, two new seams each

Each context's service gains exactly two methods, mirroring the add seam's
signature minus the "row must not exist" gate and with the row's business
key `(parentId, languageCode)` as the target:

| Surface | Service | Update seam | Remove seam |
| --- | --- | --- | --- |
| Post | `PostService` | `UpdatePostTranslationAsync` | `RemovePostTranslationAsync` |
| Reply | `PostService` | `UpdateReplyTranslationAsync` | `RemoveReplyTranslationAsync` |
| Announcement | `AnnouncementService` | `UpdateAnnouncementTranslationAsync` | `RemoveAnnouncementTranslationAsync` |
| Group | `UserInfoService` | `UpdateGroupTranslationAsync` | `RemoveGroupTranslationAsync` |
| Community | `UserInfoService` | `UpdateCommunityTranslationAsync` | `RemoveCommunityTranslationAsync` |
| Page | `PageService` | `UpdateTranslationAsync` | `RemoveTranslationAsync` |

All follow the platform's C3 invariant: writes go through the **caller's**
`IDocumentSession` (passed in as `session`); the domain write and the
`AccessAudit` row commit atomically in that one session.

### D2 — Standing is the add lane's standing, unchanged

Each new seam calls the **same** standing resolver the add lane already uses
for its parent. No new standing, no new role, no new `AccessVia` value:

- **Post / reply** — the ADR 0022 matrix (author ∪ GlobalAdmin ∪ Translator;
  the group-lane author lane per ADR 0027's parent-post standing).
- **Announcement** — the ADR 0029 matrix (author ∪ GlobalAdmin ∪ Translator).
- **Group** — the ADR 0026 matrix (owner ∪ GlobalAdmin ∪ Translator; a
  member is *not* a standing, unchanged).
- **Community** — the ADR 0026 matrix (GlobalAdmin ∪ Translator; a community
  has no owner, unchanged).
- **Page** — the ADR 0039 / 0042 matrix (the page's author standing ∪
  GlobalAdmin ∪ Translator, via `PageService.ResolveTranslationStandingVia`).

A denied standing throws `UnauthorizedAccessException`; a missing parent or
a missing translation row throws `KeyNotFoundException`; a blank submission
on a row that must carry content throws `ArgumentException` (same
at-least-one-field rule as the add lane on the name/description surfaces).

### D3 — The editable surface is the row's own fields

- **Post / page / announcement rows** — `Title` (nullable) and/or `Body`.
  The add lane's non-blank rule is preserved on update: a row must end with
  at least one non-blank field.
- **Reply rows** — `Body` only (replies have no title, ADR 0022).
- **Group / community rows** — `Name` and/or `Description`; at least one
  non-blank, else `ArgumentException` (ADR 0026's at-least-one rule, held).

Identity fields are immutable on the edit lane in every case:
`AuthorId` (a correction is not a re-attribution — the row keeps the author
who added it; `Modified`-style stamping is out of scope for translation
rows, which carry none), `Created`, the parent id, and `LanguageCode`
(the target is identified *by* language; re-targeting a row's language is
"remove this row, add a row in the other language", not an update).

### D4 — Audit: two new actions per surface, the add lane's target kind

Every update and remove writes exactly one `AccessAudit` row through the
caller's session, in the same shape as the add lane's row. The new
`AccessAction` values, keyed to each surface's *existing* `TargetKind`:

| Surface | Update action | Remove action | TargetKind |
| --- | --- | --- | --- |
| Post | `posttranslation.update` | `posttranslation.remove` | `post` |
| Reply | `replytranslation.update` | `replytranslation.remove` | `reply` |
| Announcement | `announcementtranslation.update` | `announcementtranslation.remove` | `announcement` |
| Group | `grouptranslation.update` | `grouptranslation.remove` | `group` |
| Community | `communitytranslation.update` | `communitytranslation.remove` | `component` |
| Page | `page.translation.update` | `page.translation.remove` | `page` |

The `Via` tag is the standing resolver's output for the actor on that
surface (Owner / Admin), exactly as the add lane derives it. The audit
target id is the **parent**'s id (the post/reply/announcement/group/component
id), not the translation row's surrogate id — the row's identity is the
`(parent, language)` pair, and the audit log's existing target vocabulary is
parent-keyed.

### D5 — Web: two new routes per surface, the surface's own failure shape

Each Web controller gains `Update…Translation` / `Remove…Translation` POST
routes alongside the existing add route, thin over the Core seam (the actor
minted from the signed-in principal, never a form field; the session from
`store.LightweightSession()`):

- **Posts** (`/posts/{id}/translations/update` + `/…/remove`, and the reply
  equivalents on the post detail) — the community-lane failure shape:
  denied standing → **403 `Forbid`**; missing post/reply/row → **404
  `NotFound`**.
- **Groups** (group name/description on `/groups/{id}/translations/…`; the
  group post + reply translation routes on the group-post detail) — the
  group-lane failure shape (ADR 0026's add-lane pin, held): **404
  fail-closed** for both a non-visible group *and* a denied standing; the
  surface advertises no 403.
- **Community** (`/community/translations/update` + `/…/remove`) —
  denied → **403 `Forbid`**; missing row → **404 `NotFound`**.
- **Announcement** (`/announcements/{id}/translations/update` + `/…/remove`)
  — denied → **403 `Forbid`**; missing announcement/row → **404
  `NotFound`**.
- **Page** (`/pages/{id}/translations/update` + `/…/remove`) —
  denied → **403 `Forbid`**; missing page/row → **404 `NotFound`**.

The view affordances (edit + remove forms in the translation
`<details>` blocks) live on the six surfaces' existing show views
(`Posts/Detail`, `Groups/PostDetail`, `Groups/Detail`, `Community/Manage`,
`Announcement/Detail`, `Page/Show`); the two UI labels per surface
(`…_translation_edit` / `…_translation_remove` key families) are registered
in `KnownTranslationKeys` for all four enabled languages.

### D6 — The add lanes are untouched

The add seams keep their "row must not exist" gate exactly as written; a
second `Add…Translation` call in a language that already has a row still
fails the way it always has (the update lane is now the re-save path for a
language that already has one, on the surfaces whose add lane refused it
before; where the add lane already overwrote, the update lane is the
semantically-named path for the same write). No add-lane behaviour,
standing, audit action, or failure shape changes.

## Consequences

- **Positive:** the typo and the wrong-translation cases both have a
  supported lane on every surface; the standing matrix, audit vocabulary
  (shape), and failure shapes are all *unchanged* from each surface's add
  lane, so there is no new surface for a resident to learn and no new
  decision for the audit log to disambiguate.
- **Neutral:** the translation documents are unchanged (no new field, no
  new document, no migration — ADR 0004 §B untouched); the `AccessAction`
  enum gains twelve values (two per surface); the six surfaces' show views
  each gain one pair of small forms.
- **Web test coverage note:** the post/reply translation edit/remove Web
  lanes are **not** isolated-tested at the Web layer because
  `PostService` is `public sealed` (NSubstitute cannot proxy it); their
  standing + exception contract is pinned end-to-end by the Core-level
  `PostTranslationTests` suite, which the Web layer's 403/404 mapping
  mirrors verbatim. The group name/description, community, announcement,
  and page Web lanes are covered by their respective Web test files
  (`GroupsControllerTranslationTests`, `CommunityControllerTranslationTests`,
  `AnnouncementControllerTests`, `PageControllerTranslationTests`).
- **Deferred:** `Modified`-style stamping on translation rows (a row's
  last-correction time) — the rows carry no `Modified` field today and no
  surface reads one; adding it is a small additive ADR if ever wanted.
- **Deferred:** a "who corrected this row" trail beyond the audit log — the
  audit rows (D4) are the platform's single correction trail; the row
  itself is deliberately not stamped.
