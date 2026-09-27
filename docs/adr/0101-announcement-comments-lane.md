# ADR 0101 — Signed-in resident comments on announcements

Status: Accepted
Date: 2026-09-26
Extends the **shipped M3b announcements surface** (the
`Announcements/` feature module registered on the `M3DocTypes` surface) and the
**comment precedent set**: the **C-M3·1** "comment-inherits the parent's
visibility" rule (a comment carries **no** `Audience` of its own — its
visibility inherits the parent's, so there is no second authorization evaluation
and no read-time audit row), the **ADR 0018** authored-in language tag, the
**ADR 0024** author-soft-delete shape, and the **ADR 0016**
reply-delete author-only rule. This ADR adds a new read+write capability to the
announcement detail surface: **signed-in** residents can **comment** on an
announcement, and an **admin toggle** can close the surface to everyone.

## Context

An announcement is a platform notice with a **flat two-way visibility split**,
not an audience-restricted record. `AnnouncementService` resolves who may read
an announcement through `ResolveReadVisibilityAsync` (Public = everyone;
Community = any signed-in resident; a community-targeted announcement = that
community's residents/moderators, plus GlobalAdmin). It does **not** go through
`IAuthorizationService.CanAsync` (unlike the ADR 0100 to-do lane) — the
announcement's own scope split is the sole read decision, and that flat split is
what the comment lane inherits.

The request:

- **Let admins enable or disable commenting** on announcements.
- **Visitors who are not signed in can never comment** on any announcement.
- **When enabled, comments are visible to signed-in users only** — even on an
  otherwise **public** announcement — and otherwise carry the same visibility
  as the announcement they belong to (a community-targeted one is visible to
  that community's residents).

Three deliberate differences from the ADR 0100 to-do lane, each a narrowing
rather than a new mechanism:

- **Signed-in-only, not "can-see" — and *stronger* for public announcements.**
  The to-do lane lets anyone who can *see* the to-do comment. Here the write
  gate additionally requires the actor to be **signed in**, so even a *public*
  announcement's comments stay out of reach of visitors. For a
  community-targeted announcement the two rules coincide (only its residents
  can see it, and they are signed in); for a public one the signed-in rule is
  strictly tighter than the visibility rule.
- **Top-level only — no replies.** ADR 0100 carried the C-M5·7 sole-hierarchy
  shape (comment + reply). Here a comment is always top-level
  (`ParentId == null`) — there is no `ParentId` field at all. A single flat
  list under the announcement is the shipped scope; the request was
  "let residents comment," not "let them reply."
- **A new admin toggle** (the to-do lane has none). An admin can close the
  whole surface: signed-in residents see only the announcement body, and no
  one — visitor or resident — can add a comment. Existing comments are kept.

This is a **named lane on the already-shipped M3b surface** (like ADR
0086–0100) — not a milestone, so `Milestones.cs` / the README Roadmap /
`MilestonesTests.cs` are deliberately untouched.

## Decision

A new document type `Announcements.AnnouncementComment` (registered on the
`M3DocTypes` surface — the additive-schema shape, ADR 0004 §B.1) carries:

```csharp
public sealed class AnnouncementComment
{
    public string Id { get; set; }
    public string AnnouncementId { get; set; }  // the parent surface (visibility inherits its split)
    public string AuthorId { get; set; }
    public string Body { get; set; }
    public DateTimeOffset Created { get; set; }
    public string LanguageCode { get; set; }     // ADR 0018 authored-in tag
    public DateTimeOffset? DeletedAt { get; set; } // ADR 0024 author soft-delete
}
```

**No `ParentId`** (top-level only — the deliberate scope cut vs ADR 0100),
**no `Audience`** field (C-M3·1), **no `Modified`** (a comment is immutable
until its author soft-deletes it), and **no attachments** (the ask was
add-comment-only; the announcement itself already carries the rich content
surface).

**An admin toggle** lives on `LocaleSettings`:
`public bool AnnouncementCommentsEnabled { get; set; } = true;` (the codebase
`true`-floor convention, ADR 0004 §B.1 additive — mirrors `IsSignupOpen` /
`NotifyAdminsOnSignup`). A `null` settings row reads as **on** (the floor).

Five new seams on `IAnnouncementService` (the frozen seam, ADR 0006), each
following the announcement service's own convention (the service opens its own
session, applies the flat visibility split, writes the C3 audit row in the same
session, and `SaveChangesAsync`s once):

1. **`AreAnnouncementCommentsEnabledAsync()`** — reads the toggle with the
   `true` floor (`settings is null || settings.AnnouncementCommentsEnabled`).
2. **`SetAnnouncementCommentsEnabledAsync(enabled, actorId)`** — the admin
   write. Loads-or-mints the `LocaleSettings` row, stores the flag, and commits
   an `announcementcomments.set-enabled` / `TargetKind = "announcementcomments"`
   / `Via = Admin` / `Outcome = Allow` `AccessAudit` row (C3). An empty
   `actorId` is 403. (Called from the dedicated GlobalAdmin controller — the
   `AdminController` constructor is test-pinned.)
3. **`GetAnnouncementCommentsAsync(announcementId, actorId, actorRoles)`** —
   the read lane:
   - **403** (`UnauthorizedAccessException`): the actor is **not signed in**
     (anonymous). Comments are signed-in-only, even on a public announcement.
   - **404** (`KeyNotFoundException`): the announcement is missing, is a
     **draft**, or the actor is **not visible for** it under the flat split
     (a non-leaky 404 — the same non-existence shape the announcement read
     uses).
   - On success: returns the announcement's comments ordered by `Created`
     **ascending**, including soft-deleted rows so the view can render a
     placeholder in place of the body.
4. **`CreateAnnouncementCommentAsync(announcementId, actorId, actorRoles,
   body, languageCode, session)`** —
   - **400** (`ArgumentException`): the body is blank; an empty `actorId` is
     403.
   - **403** (`UnauthorizedAccessException`): the admin toggle is **off**, or
     the actor is **not signed in**.
   - **404** (`KeyNotFoundException`): the announcement is missing, a **draft**,
     or **not visible** to the actor under the flat split.
   - On success: materializes `LanguageCode` from the instance default when
     unchosen (ADR 0018), stores the row, commits an
     `announcementcomment.create` / `TargetKind = "announcement"` /
     `Via = Owner` `AccessAudit` row atomically (C3), and returns the comment.
5. **`DeleteAnnouncementCommentAsync(announcementId, commentId, actorId,
   actorRoles, session)`** —
   - **404**: the comment is missing or is **not under that announcement**.
   - **403**: the actor is not the comment's **author** (author-only — ADR
     0016 precedent; no moderator / GlobalAdmin override branch).
   - On success: stamps `DeletedAt` forward (the record is **kept**, never
     hard-deleted), commits an `announcementcomment.delete` /
     `TargetKind = "announcement"` / `Via = Owner` audit row (C3), and returns
     the comment.

**There is no read-time audit row** (C-M3·1: the announcement's single
visibility decision is the only read authorization event, and comments read
only under that announcement — there is no comment feed).

**Web layer.** `AnnouncementController`:
- `Detail` sets `CanComment` (signed in **and** toggle on), loads the comments
  (each row resolves the author's display name via `UserInfoService`, null-safe
  to the raw subject id, flags `IsAuthor`) and seeds the composer's language
  picker from the enabled catalog (ADR 0018). When `CanComment` is false the
  detail page renders neither the list nor the composer.
- `AddComment` (`POST /announcements/{id}/comments`) posts `body`, optional
  `languageCode` — the C3 split (404 / 403) with the `ForbidResult` /
  `NotFound()` shapes; on success it `TempData`s a confirmation and redirects
  to the detail page.
- `DeleteComment` (`POST /announcements/{id}/comments/{commentId}/delete`) —
  the same split; author-only enforced in the service.

Neither route carries a coarse `[Authorize(Roles)]` gate (any signed-in
resident may comment; the service is the authority — the same posture as the
to-do comment routes).

`Detail.cshtml` gains a **Comments** section: a list of comments with a
per-row author-display-name + avatar + `<kw-dt>` timestamp, a soft-deleted
placeholder for deleted rows, an author-only delete button, and a composer (the
M3 reply-form shape: `.rc-editor` + language picker) that posts to `AddComment`.
The rich editor is loaded via the page's existing `@section Scripts` include
(`~/js/lib/rich-editor.js`). A muted note states the audience rule (signed-in
only, inherits the announcement's split).

**Admin surface.** A dedicated `AdminAnnouncementCommentsController`
(`GET/POST /admin/announcements/comments`, `[Authorize(Roles = GlobalAdmin)]`)
toggles `AnnouncementCommentsEnabled` — mirroring the `AdminSignupController`
pattern (the `AdminController` constructor is test-pinned). A dashboard card on
`Views/Admin/Index.cshtml` and the nav list point at it.

**i18n.** Thirteen new keys (`announcements.comments`, `.comment_empty`,
`.comment_deleted`, `.comment_delete`, `.comment_reply`, `.comment_submit`,
`.comment_audience_note`, and `admin.anncomments_title` / `_lede` / `_on` /
`_off` / `_save`) added to **all four** locale dictionaries (en/de/fr/da) —
the `KnownTranslationKeys_ParityTests` invariant holds.

**Tests.** New `AnnouncementServiceTests` ADR 0101 pins:
- `AreAnnouncementCommentsEnabled_DefaultOn` — a `null` settings row reads as
  **on** (the `true` floor).
- `SetAnnouncementCommentsEnabled_StoresFlagAndAuditRow` — setting the flag
  persists it and commits the `announcementcomments.set-enabled` / `Via = Admin`
  audit row.
- `CreateAnnouncementComment_ToggleOff_Refused` — with the toggle **off**,
  creating is 403 and nothing is written.
- `CreateAnnouncementComment_AnonymousRefused` — an empty `actorId` is 403.
- `CreateAnnouncementComment_MissingDraftInvisibleRefused` — a missing, a
  draft, and a not-visible announcement are all 404.
- `CreateAnnouncementComment_StoresRowAndAuditRow` — a comment stores the
  body, `LanguageCode = en` (the floor), a `announcementcomment.create` /
  `Via = Owner` audit row, and is returned by `GetAnnouncementCommentsAsync`.
- `CreateAnnouncementComment_BlankBodyRefused` — a blank body is
  `ArgumentException` and nothing is written.
- `GetAnnouncementComments_AnonymousRefused` — an anonymous read is 403.
- `GetAnnouncementComments_InCreatedOrder` — comments come back `Created`
  ascending.
- `DeleteAnnouncementComment_NonAuthorRefused` — a non-author is 403, the
  record is untouched.
- `DeleteAnnouncementComment_Author_SoftDeletesAndKeepsRow` — the author
  stamps `DeletedAt`, the `announcementcomment.delete` audit row commits, and
  the read lane still returns the (now-deleted) row.

## Consequences

- Signed-in residents can now comment on an announcement, and an admin can
  close the surface instance-wide — reusing the C-M3·1 visibility inheritance,
  the ADR 0018 authored-in tag, and the ADR 0024 / ADR 0016 soft-delete shapes
  rather than inventing a new mechanism.
- The visibility model is deliberately **tighter** than the ADR 0100 to-do
  lane: comments are **signed-in-only** (even on a public announcement) and,
  for a community-targeted announcement, visible to exactly that community's
  residents. A comment can never be visible where the announcement is not, and
  never to a visitor — no feed-style leak path exists because there is no
  comment feed (comments are read only under the announcement they belong to).
- Author-soft-delete is reversible-by-presence: the record is kept and rendered
  as a placeholder, so the discussion history is preserved.
- The admin toggle is a **new** capability the to-do lane lacks: it governs
  *new* comments only (closing it hides the list + composer and denies
  creation, but does not remove existing comments). It is stored on
  `LocaleSettings` (additive) and written by a dedicated GlobalAdmin
  controller (the test-pinned `AdminController` is left untouched).
- The schema is purely additive (a new `AnnouncementComment` table + one
  index on the `M3DocTypes` surface; a new `LocaleSettings` field); no existing
  table, index, or seam changes. The `Milestones.cs` / README Roadmap /
  `MilestonesTests.cs` triple is untouched (a named lane on the shipped M3b
  surface, not a milestone — the ADR 0086–0100 precedent).
