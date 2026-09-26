# ADR 0100 — Comments + replies on a to-do

Status: Accepted
Date: 2026-09-26
Extends the **M5 to-do surface** (ADR 0067 the standing matrix + the frozen
`IProjectService` convention + the service-owns-the-session / `CanAsync(Read)`
gate / C3 audit row convention; ADR 0087 the "waiting on" blocker lane;
ADR 0099 the board-added-card audience inheritance) and the **comment
precedent set**: the **C-M3·1** "comment-inherits the parent's single Read
decision" rule (a comment/reply carries **no** `Audience` of its own — carried
from the `Posts.PostReply` precedent), the **ADR 0018** authored-in language
tag, the **ADR 0024** author-soft-delete shape, and the **ADR 0016**
reply-delete author-only rule. This ADR adds a new read+write capability to the
to-do detail surface: residents can **comment** on a to-do and **reply** to
another comment.

## Context

A to-do is a work item with a single, already-authorized audience decision
(`CanAsync(Read)` over the to-do's `Audience`). The platform already carries a
well-trodden shape for "attach lightweight prose to an authorized record": the
`Posts.PostReply` / `Posts.Post` comment-and-reply surface (ADR 0016 the
author-only reply delete, ADR 0018 the authored-in language tag, ADR 0024 the
author soft-delete, and the C-M3·1 invariant that a reply carries **no**
`Audience` of its own — its visibility inherits the parent post's single Read
decision, so there is no second authorization evaluation and no read-time audit
row).

To-dos were the one authorized work item that had **no** comment surface. The
natural request ("let residents discuss a to-do; let them reply to a
discussion") maps one-to-one onto that precedent, with the to-do standing in for
the post:

- **Standing for commenting = the to-do's Read decision.** Anyone who can
  *see* the to-do may comment on it. There is no separate comment-level standing
  matrix (no author ∪ assignee ∪ GlobalAdmin over the comment) — the to-do's
  single audience is the sole access boundary (C-M3·1).
- **One level of nesting.** A comment is top-level (`ParentId == null`) or a
  reply to a top-level comment (`ParentId == <comment id>`) — the
  `TodoItem.ParentId` / C-M5·7 sole-hierarchy shape carried to the comment lane.
  A reply to a reply is not a distinct shape.
- **Author-only soft-delete.** The comment's author may delete their own comment
  (ADR 0024 stamp `DeletedAt`, ADR 0016 author-only); there is no moderator /
  GlobalAdmin override branch on a comment's own delete.

This is a **named lane on the already-shipped M5 surface** (like ADR 0086–0099)
— not a milestone, so `Milestones.cs` / the README Roadmap /
`MilestonesTests.cs` are deliberately untouched.

## Decision

A new document type `Projects.TodoComment` (registered on the `M5DocTypes`
surface — the additive-schema shape, ADR 0004 §B.1) carries:

```csharp
public sealed class TodoComment
{
    public string Id { get; set; }
    public string TodoId { get; set; }          // the parent surface (visibility inherits its Read decision, C-M3·1)
    public string? ParentId { get; set; }        // null = top-level comment; non-null = a reply (C-M5·7)
    public string AuthorId { get; set; }
    public string Body { get; set; }
    public DateTimeOffset Created { get; set; }
    public string LanguageCode { get; set; }     // ADR 0018 authored-in tag
    public DateTimeOffset? DeletedAt { get; set; } // ADR 0024 author soft-delete
}
```

**No `Audience` field** (C-M3·1), **no `Modified`** (a comment is immutable
until its author soft-deletes it), and **no attachments** (the ask was
add-comment + reply-only; the to-do itself already carries the rich content +
attachment surface).

Two new lanes on `IProjectService` (the frozen seam, ADR 0006), each following
the ADR 0067 service convention (the service opens its own session, runs
`CanAsync(Read)` over the to-do, writes the C3 audit row in the same session,
and `SaveChangesAsync`s once):

1. **`CreateTodoCommentAsync(todoId, actorId, actorRoles, body, languageCode,
   parentId = null)`** —
   - **404** (`KeyNotFoundException`): the to-do id is missing, the to-do is
     soft-deleted, the body is blank → `ArgumentException`, or a non-null
     `parentId` does not resolve to a live comment **on the same to-do**
     (missing / soft-deleted / on a different to-do are all 404, non-leaky).
   - **403** (`UnauthorizedAccessException`): the actor fails the to-do's
     `CanAsync(Read)` decision (the standing gate — C-M3·1).
   - On success: stores the row, materializes `LanguageCode` from the instance
     default when unchosen (ADR 0018), commits a
     `todo.comment.create` / `TargetKind = "todo"` / `Via = Owner`
     `AccessAudit` row atomically (C3), and returns the comment.
2. **`DeleteTodoCommentAsync(todoId, commentId, actorId, actorRoles)`** —
   - **404**: the to-do is missing / soft-deleted, or `commentId` does not
     resolve to a comment **under that to-do**.
   - **403**: the actor is not the comment's author (author-only — ADR 0016
     precedent; no moderator / GlobalAdmin override branch).
   - On success: stamps `DeletedAt` forward (the record is **kept**, never
     hard-deleted), commits a `todo.comment.delete` / `TargetKind = "todo"` /
     `Via = Owner` audit row (C3), and returns the comment.

**Read lane extended.** `GetTodoAsync` now returns the to-do's comments (all of
them — top-level and replies, ordered by `Created` ascending, including
soft-deleted rows so the view can render a placeholder in place of the body).
`TodoDetailResult` gains `IReadOnlyList<TodoComment> Comments`. There is **no**
per-comment read gate and **no** read-time audit row for comments (C-M3·1: the
to-do's single Read decision is the only authorization event, and that is the
one audit row).

**Web layer.** `ProjectsController`:
- `TodoDetail` populates `TodoDetailViewModel.Comments` (each row resolves the
  author's display name via `UserInfoService`, flags `IsAuthor`) and seeds the
  language picker (`ViewData["TodoComment_Languages"]`).
- `AddCommentPost` (`POST /projects/todos/{id}/comments`) posts `body`,
  optional `languageCode`, optional `parentId` — the C3 split (404 / 403) with
  the `ForbidResult` / `NotFound()` shapes.
- `DeleteCommentPost` (`POST /projects/todos/{id}/comments/{commentId}/delete`)
  — the same split.

`TodoDetail.cshtml` gains a **Comments** section: a list of top-level comments
with replies nested under their parent (replies indented), a per-row
author-display-name + avatar + `<kw-dt>` timestamp, a soft-deleted placeholder
for deleted rows, an author-only delete button, and a composer (the M3
reply-form shape: `.rc-editor` + language picker) that posts to
`AddCommentPost`. The rich editor is loaded via a per-page `@section Scripts`
include (`~/js/lib/rich-editor.js`).

**i18n.** Seven new keys (`projects.todo.comments`, `.comment_empty`,
`.comment_reply`, `.comment_submit`, `.comment_deleted`, `.comment_delete`,
`.comment_audience_note`) added to **all four** locale dictionaries
(en/de/fr/da) — the `KnownTranslationKeys_ParityTests` invariant holds.

**Tests.** New `ProjectServiceTests` ADR 0100 pins:
- `CreateTodoComment_TopLevel_StoresRowAndAuditRow` — a top-level comment
  stores the body, `LanguageCode = en`, a `todo.comment.create` / `Via = Owner`
  audit row, and is returned by `GetTodoAsync`.
- `CreateTodoComment_Reply_SetsParentId` — a reply sets `ParentId` to the
  top-level comment's id.
- `CreateTodoComment_InvalidParentRefused` — a `parentId` on a different to-do,
  a missing parent, and a soft-deleted parent are all 404.
- `CreateTodoComment_MissingOrDeletedTodoRefused` — absent and soft-deleted
  to-dos are 404.
- `CreateTodoComment_UnreadableTodoRefused` — a stranger denied the to-do's
  `Read` is 403 and nothing is written.
- `DeleteTodoComment_NonAuthorRefused` — a non-author is 403, the record is
  untouched.
- `DeleteTodoComment_Author_SoftDeletesAndKeepsRow` — the author stamps
  `DeletedAt`, the `todo.comment.delete` audit row commits, and the read lane
  still returns the (now-deleted) row.
- `GetTodo_ReturnsCommentsInCreatedOrder` — comments come back `Created`
  ascending.
- `CreateTodoComment_BlankBodyRefused` — a blank body is `ArgumentException`
  and nothing is written.

## Consequences

- Residents can now discuss a to-do and reply to a discussion, with the exact
  same authorization / soft-delete / language-tag semantics as the existing
  posts+replies surface — a new capability that reuses four already-accepted
  precedents rather than inventing a new mechanism.
- The C-M3·1 invariant holds: a comment has **no** audience of its own and
  produces **no** read-time audit row; its visibility is exactly the to-do's,
  so a comment can never be visible where the to-do is not (no feed-style leak
  path exists, because there is no comment feed — comments are read only under
  the to-do they belong to).
- Author-soft-delete is reversible-by-presence: the record is kept and rendered
  as a placeholder, so the discussion history is preserved (a deleted comment
  still shows "this comment has been deleted by its author").
- The schema is purely additive (a new `TodoComment` table + two indexes on the
  `M5DocTypes` surface); no existing table, index, or seam changes. The
  `Milestones.cs` / README Roadmap / `MilestonesTests.cs` triple is untouched
  (a named lane on the shipped M5 surface, not a milestone — the ADR
  0086–0099 precedent).
