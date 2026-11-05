using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Query;
using Kumunita.Core.Tags;
using Kumunita.Core.Translations;

namespace Kumunita.Core.Projects;

/// <summary>
/// Todo aggregate: todo CRUD, claim/assign/status, subtasks, comments, todo
/// translations, and the event picker list (C08, ADR 0287). Split from
/// <c>IProjectService</c>; signatures unchanged.
/// </summary>
public interface ITodoService
{
    /// <summary>
    /// The standalone to-do list (the feed): candidates = <c>!IsDeleted</c>,
    /// filtered by the optional <paramref name="componentId"/> (a filter, never
    /// a gate — C-M3·2) and the optional <paramref name="assigneeId"/> (a
    /// filter, never a gate — C-M5·6); the survivors are
    /// <c>CanSeeAsync(Read)</c>-filtered (C6 / C3) over the
    /// <see cref="TodoItemToAuditableResource"/>; ordered by <c>Created</c>
    /// descending (the newest first — the post feed shape); paged. The
    /// **aggregate** <c>AccessAudit</c> row (<c>TargetKind = "todo"</c>,
    /// <c>visibleCount</c> / <c>hiddenCount</c>) is the C-M3·3 shape.
    /// <para>
    /// <paramref name="unassignedOnly"/> (ADR 0073) is the **unassigned pool
    /// filter** — a *filter, never a gate* (the same discipline as
    /// <paramref name="componentId"/> / <paramref name="assigneeId"/>). When
    /// <c>true</c>, only to-dos with <see cref="TodoItem.AssigneeId"/> null are
    /// returned (the pool a group / community member can pick up and
    /// <see cref="ClaimTodoAsync"/>); it narrows the candidate set, it does not
    /// change the audience decision (a to-do still only appears if the actor
    /// passes the <c>CanSeeAsync(Read)</c> pass).
    /// </para>
    /// <para>
    /// <paramref name="projectId"/> (ADR 0086, U04) is the **project
    /// association filter** — a *filter, never a gate* (the same discipline as
    /// <paramref name="componentId"/> / <paramref name="assigneeId"/> /
    /// <paramref name="unassignedOnly"/>). When non-null, only to-dos with
    /// <see cref="TodoItem.ProjectId"/> equal to it are returned (the
    /// <c>ProjectId == projectId</c> row set); when <c>null</c> (the default),
    /// no filter — it narrows the candidate set, it does not change the
    /// audience decision (a to-do still only appears if the actor passes the
    /// <c>CanSeeAsync(Read)</c> pass — C-M3·2 / C-PL·3).
    /// </para>
    /// </summary>
Task<TodoPage> ListTodosAsync(string? componentId, string? assigneeId, string actorId, int page, bool unassignedOnly = false, string? projectId = null, bool blockedOnly = false, CancellationToken ct = default, SortSpec? sort = null, int? pageSize = null); // ADR 0090 D1/D3 — HasMore = candidates.Count == PageSize (false on an empty page, C-M7·5); record shape, not an out param (CS1988).

    /// <summary>
    /// The **reverse read seam** of the M14 interlock (ADR 0115 D2) — the
    /// to-dos **linked to this event**: the <see cref="ListTodosAsync"/> shape
    /// filtered to one event (the <see cref="ListBoardsForTodoAsync"/>
    /// "list X for a Y" reverse-read precedent). The candidate set is
    /// <c>!IsDeleted</c> with <see cref="TodoItem.EventId"/> equal to
    /// <paramref name="eventId"/> — **a filter, never a gate** (C-M14·1: the
    /// <c>EventId</c> narrows the candidate set; it never changes the audience
    /// decision — a to-do is visible iff it passes its **own**
    /// <c>CanSeeAsync(Read)</c>). The survivors are
    /// <c>CanSeeAsync(Read)</c>-filtered (C6 / C3) over the **existing**
    /// <see cref="TodoItemToAuditableResource"/> (C-M14·4 — no new adapter, no
    /// new <c>AccessAction</c> / <c>AccessVia</c> / <c>Decide()</c> branch);
    /// ordered by <c>Created</c> descending; paged with
    /// <c>HasMore = candidates.Count == PageSize</c> (ADR 0090 D1/D3).
    /// **One aggregate** <c>AccessAudit</c> row (<c>TargetKind = "todo"</c>,
    /// <c>visibleCount</c> / <c>hiddenCount</c>) is the C-M3·3 shape; a 0-
    /// candidate page reports <c>HasMore: false</c> and emits **no** decision
    /// row (C-M7·5). The seam returns **only to-dos the actor may read** — an
    /// event with zero readable linked to-dos returns an **empty page**, never
    /// a 404/403 (the event's own detail page decides its own visibility — this
    /// lane is called only from a page that already passed the event's
    /// <c>Read</c> decision).
    /// </summary>
    Task<TodoPage> ListTodosForEventAsync(string eventId, string actorId, int page, CancellationToken ct = default, int? pageSize = null); // ADR 0090 D1/D3 — HasMore = candidates.Count == PageSize (false on an empty page, C-M7·5); record shape, not an out param (CS1988).

    /// <summary>
    /// One to-do + its **subtasks** (the <see cref="TodoItem"/> rows with
    /// <c>ParentId == todoItemId</c>, ordered by <c>Created</c> ascending). One
    /// <c>CanAsync(Read)</c> over the to-do; <see cref="KeyNotFoundException"/>
    /// (404) on absent, <see cref="UnauthorizedAccessException"/> (403) on
    /// denied — the C3 404-vs-403 split. Each subtask is **itself**
    /// <c>CanAsync(Read)</c>-gated (a subtask is a full to-do with its own
    /// <c>Audience</c> — C-M5·7); a denied subtask is not returned. The to-do's
    /// single decision is the <c>AccessAudit</c> row (C3).
    /// </summary>
    Task<TodoDetailResult> GetTodoAsync(string todoItemId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// **Add a comment** (or a reply to another comment) on a to-do (ADR 0100).
    /// A new <see cref="TodoComment"/> row with the to-do's id as
    /// <see cref="TodoComment.TodoId"/> and, when <paramref name="parentId"/>
    /// is non-null, that comment's id as
    /// <see cref="TodoComment.ParentId"/> (the sole hierarchy mechanism —
    /// C-M5·7: a <c>null</c> parent is a top-level comment, a non-null parent
    /// is a reply). **Standing:** any actor who passes the to-do's
    /// <c>CanAsync(Read)</c> decision (the to-do's audience is the sole access
    /// boundary — a comment inherits it, C-M3·1; there is no separate
    /// comment-level standing matrix). A <paramref name="parentId"/> that is
    /// missing, already soft-deleted, or on a *different* to-do is a
    /// <see cref="KeyNotFoundException"/> (404). The <c>LanguageCode</c> is
    /// materialized from the instance default when <paramref
    /// name="languageCode"/> is null/empty (ADR 0018). One
    /// <see cref="AccessAudit"/> row (<c>todo.comment.create</c>,
    /// <c>TargetKind = "todo"</c>) commits atomically with the write (C3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The to-do id is not found, the
    /// to-do is soft-deleted, or a non-null <paramref name="parentId"/> does
    /// not resolve to a live comment on the same to-do.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor may not read
    /// the to-do.</exception>
    Task<TodoComment> CreateTodoCommentAsync(
        string todoId, string actorId, IReadOnlySet<string> actorRoles,
        string body, string? languageCode, string? parentId = null,
        CancellationToken ct = default);

    /// <summary>
    /// **Soft-delete a comment** the actor authored on a to-do (ADR 0100, the
    /// ADR 0024 author-soft-delete shape carried from
    /// <see cref="Kumunita.Core.Posts.PostReply.DeletedAt"/>): stamps
    /// <see cref="TodoComment.DeletedAt"/> forward (the record is kept, never
    /// hard-deleted), and the detail view renders a placeholder in place of
    /// the body. **Standing:** author-only (the comment's
    /// <see cref="TodoComment.AuthorId"/> == the actor — the ADR 0016
    /// reply-delete precedent: a non-author is refused, there is no
    /// moderator / GlobalAdmin override branch on a comment's own delete).
    /// The comment must exist and be under the given to-do (a comment on a
    /// different to-do is a <see cref="KeyNotFoundException"/> — 404,
    /// non-leaky). One <see cref="AccessAudit"/> row
    /// (<c>todo.comment.delete</c>, <c>TargetKind = "todo"</c>) commits
    /// atomically with the write (C3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The to-do id is not found, the
    /// to-do is soft-deleted, or the <paramref name="commentId"/> does not
    /// resolve to a comment under that to-do.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor is not the
    /// comment's author.</exception>
    Task<TodoComment> DeleteTodoCommentAsync(
        string todoId, string commentId, string actorId, IReadOnlySet<string> actorRoles,
        CancellationToken ct = default);

        /// <summary>
        /// The **blocker picker** read lane (ADR 0087 D7) — the actor's readable,
        /// non-deleted to-dos (the candidates are <c>!IsDeleted</c>; the
        /// survivors are <c>CanSeeAsync(Read)</c>-filtered (C6 / C3) over the
        /// <see cref="TodoItemToAuditableResource"/>; ordered by <c>Created</c>
        /// descending; paged). A **display** surface, never a gate (C-TBD·4) —
        /// it does not pre-check cycles (the write lane does — C-TBD·3).
        /// </summary>
        Task<TodoPage> ListPickerTodosAsync(string actorId, int page, CancellationToken ct = default, int? pageSize = null); // ADR 0090 D1/D3 — HasMore = candidates.Count == PageSize (false on an empty page, C-M7·5); record shape, not an out param (CS1988).

    /// <summary>
    /// Create a to-do — the author's choice is written verbatim (ADR 0001-B);
    /// the to-do is **published on creation** (no <c>IsDraft</c> — D8a). The
    /// author becomes the standing owner (the <c>AuthorId</c> branch). The
    /// <c>AccessAudit</c> row (<c>todo.create</c>, <c>TargetKind = "todo"</c>)
    /// is stored in the caller's session (C3).
    /// </summary>
    Task<TodoItem> CreateTodoAsync(string actorId, IReadOnlySet<string> actorRoles, CreateTodoRequest request, CancellationToken ct = default);

    /// <summary>
    /// Edit a to-do (title / body / status / component / language / tags /
    /// reparent) — **creator ∪ assignee ∪ GlobalAdmin** (the ADR 0014 / 0016 /
    /// 0017 precedent, enforced server-side per C-M5·6). <c>AuthorId</c> /
    /// <c>Created</c> preserved untouched; <c>Modified</c> stamped on a real
    /// change. <c>actorRoles</c> carries the principal's real role set (the Web
    /// layer passes <c>RoleSet(User)</c>). The **hierarchy cycle guard**
    /// (C-M5·7): a <c>request.ParentId</c> that would make the target a
    /// descendant of the to-do is refused (<c>InvalidOperationException</c>).
    /// The <c>AccessAudit</c> row (<c>todo.update</c>, <c>TargetKind =
    /// "todo"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task<TodoItem> UpdateTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, UpdateTodoRequest request, CancellationToken ct = default);

    /// <summary>
    /// Assign a to-do — sets <c>AssigneeId</c> to <paramref name="assigneeId"/>
    /// (<c>null</c> = unassign). **Creator ∪ assignee ∪ GlobalAdmin** (C-M5·6).
    /// The <c>AccessAudit</c> row (<c>todo.assign</c>, <c>TargetKind =
    /// "todo"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task<TodoItem> AssignTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? assigneeId, CancellationToken ct = default);

    /// <summary>
    /// **Claim** a to-do (ADR 0073 — the self-assign lane): sets <c>
    /// AssigneeId</c> to <paramref name="actorId"/> (the claimer takes the
    /// unassigned to-do onto themselves). Standing (server-side, C3):
    /// the to-do must be **unassigned** (<see cref="TodoItem.AssigneeId"/>
    /// null) **and** the actor must be a member of a **group** in the to-do's
    /// <see cref="TodoItem.Audience"/> grants **or** a member of the to-do's
    /// <see cref="TodoItem.ComponentId"/> community (the ADR 0013 group lane /
    /// ADR 0036 community lane, probed through the frozen <c>
    /// IUserInfoService</c> <c>GetGroupIdsAsync</c> / <c>GetCommunityIdsAsync</c>
    /// seams). A missing / soft-deleted id is <see cref="KeyNotFoundException"/>
    /// (404); a to-do that is already assigned, or the actor lacks the group /
    /// community standing, is <see cref="UnauthorizedAccessException"/> (403). One
    /// <c>AccessAudit</c> row (<c>todo.claim</c>, <c>TargetKind = "todo"</c>,
    /// <c>Via</c> = the branch the actor qualified under) is stored in the
    /// caller's session (C3). **No new <c>AccessAction</c>, no new
    /// <c>AccessVia</c>, no new branch in <c>Decide()</c>** — the lane reuses
    /// the existing <c>AccessVia.Group</c> / <c>AccessVia.Community</c> values
    /// (C-M5·11: M5 adds an *adapter*, not a *branch*).
    /// </summary>
    Task<TodoItem> ClaimTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// **Set the to-do's status** (the detail page's status change lane —
    /// the <see cref="UpdateTodoRequest"/> <c>Status</c> field surfaced as a
    /// standalone write). Sets <see cref="TodoItem.Status"/> to
    /// <paramref name="status"/> (<c>null</c> = clear — the same rule
    /// <see cref="AssignTodoAsync"/> applies to the assignee). Standing
    /// (server-side, C3): **creator ∪ assignee ∪ GlobalAdmin** (C-M5·6, the
    /// <see cref="ProjectService.CheckTodoStanding"/> shape). A missing /
    /// soft-deleted id is <see cref="KeyNotFoundException"/> (404); a denied
    /// actor is <see cref="UnauthorizedAccessException"/> (403).
    /// <c>AuthorId</c> / <c>Created</c> preserved untouched; <c>Modified</c>
    /// stamped. One <see cref="AccessAudit"/> row (<c>todo.set_status</c>,
    /// <c>TargetKind = "todo"</c>) commits atomically with the write (C3).
    /// </summary>
    Task<TodoItem> SetTodoStatusAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? status, CancellationToken ct = default);

    /// <summary>
    /// Add a subtask — a new <see cref="TodoItem"/> with <c>ParentId =
    /// parentTodoItemId</c> (the sole hierarchy mechanism — C-M5·7); the
    /// subtask is a full to-do (its own status / assignee / placements) and its
    /// author is the actor. **Creator ∪ assignee ∪ GlobalAdmin** over the
    /// **parent** (C-M5·6). The parent's <c>ParentId</c> is unchanged. The
    /// <c>AccessAudit</c> row (<c>todo.add_subtask</c>, <c>TargetKind =
    /// "todo"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task<TodoItem> AddSubtaskAsync(string parentTodoItemId, string actorId, IReadOnlySet<string> actorRoles, CreateTodoRequest request, CancellationToken ct = default);

    /// <summary>
    /// **Soft-delete** a to-do — sets <c>IsDeleted = true</c> (the ADR 0024
    /// author-lane shape) + **cascades** to the descendant subtree (the
    /// <see cref="TodoItem"/> rows transitively reachable via <c>ParentId</c> —
    /// C-M5·7); the to-do's <see cref="BoardItemPlacement"/> rows are **kept**
    /// (a placement's target becoming a soft-deleted to-do is the read lane's
    /// filter — a board card for a deleted to-do is not returned). **Creator ∪
    /// assignee ∪ GlobalAdmin** (C-M5·6). The <c>AccessAudit</c> row
    /// (<c>todo.delete</c>, <c>TargetKind = "todo"</c>) is stored in the
    /// caller's session (C3).
    /// </summary>
    Task DeleteTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// Sets <c>TodoItem.ProjectId</c> to <paramref name="projectId"/>
    /// (<c>null</c> = unassociate). **Creator ∪ assignee ∪ GlobalAdmin** over
    /// the **to-do** (the C-M5·6 standing matrix, re-checked server-side). A
    /// non-null <c>projectId</c> pointing at a soft-deleted or unreadable
    /// project is **refused** (the C3 split). <c>AuthorId</c> / <c>Created</c>
    /// preserved untouched; <c>Modified</c> stamped; the <c>AccessAudit</c> row
    /// (<c>todo.set_project</c>, <c>TargetKind = "todo"</c>) is stored in the
    /// caller's session (C3).
    /// </summary>
    Task<TodoItem> SetTodoProjectAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? projectId, CancellationToken ct = default);

    /// <summary>
    /// The **M14 interlock** (ADR 0115 D3): sets <c>TodoItem.EventId</c> to
    /// <paramref name="eventId"/> (<c>null</c> = **clear the link** — the
    /// <see cref="SetTodoProjectAsync"/> null-clears rule). **Creator ∪
    /// assignee ∪ GlobalAdmin** over the **to-do** (the C-M5·6 standing
    /// matrix, re-checked **server-side** — C-M14·3; the Web <c>[Authorize]</c>
    /// is a convenience pre-gate only, never the source of truth). The to-do
    /// must exist and be <c>!IsDeleted</c> (<see cref="KeyNotFoundException"/>
    /// — 404). A non-null <c>eventId</c> that is **non-existent, soft-deleted,
    /// or not readable by the actor is refused** (<see
    /// cref="KeyNotFoundException"/> — 404, the non-leaky split, C-M14·3; the
    /// standing decision is the frozen <see cref="IAuthorizationService"/>
    /// <c>CanAsync(Read)</c> path over the event — **you can only link to an
    /// event you can see**; C-M14·4 — no new authorization surface).
    /// <c>AuthorId</c> / <c>Created</c> preserved untouched; <c>Modified</c>
    /// stamped; one <c>AccessAudit</c> row (<c>todo.set_event</c>,
    /// <c>TargetKind = "todo"</c>) commits **atomically** with the write (C3).
    /// </summary>
    Task<TodoItem> SetTodoEventAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? eventId, CancellationToken ct = default);

    /// <summary>
    /// The **read** seam for a to-do's user-added translations (ADR 0088):
    /// every <see cref="TodoTranslation"/> whose <see cref="TodoTranslation.TodoItemId"/>
    /// is <paramref name="todoItemId"/>, ordered by
    /// <see cref="TodoTranslation.LanguageCode"/>. Owns its own
    /// <c>QuerySession</c> (C3 read lane); **not** an authorization surface and
    /// writes **no** audit row (C-M3·1 — inherits the parent to-do's single
    /// <c>Read</c> decision; the Web reads this only after
    /// <see cref="GetTodoAsync"/> returned the to-do).
    /// </summary>
    Task<IReadOnlyList<TodoTranslation>> GetTodoTranslationsAsync(string todoItemId);

    /// <summary>
    /// The **shared translation-variant write lane** (ADR 0288) for a to-do —
    /// <see cref="TranslationLane{TParent,TVar}.AddAsync"/> /
    /// <see cref="TranslationLane{TParent,TVar}.UpdateAsync"/> /
    /// <see cref="TranslationLane{TParent,TVar}.RemoveAsync"/> over the
    /// <c>(todoItemId, languageCode)</c> pair the
    /// <see cref="ProjectDocTypes.Configure"/>'s <c>(TodoItemId, LanguageCode)</c>
    /// unique index enforces. C2-02 (IMPROVE-CODE-2 DoD #5) retired the three
    /// per-surface <c>Add/Update/Remove*TodoTranslationAsync</c> forwarders in
    /// favour of this lane + the to-do's
    /// <see cref="TranslationDescriptor{TParent,TVar}"/> (the C06/C2-01
    /// <c>PostService.PostTranslationLane</c> shape). The standing (the to-do's
    /// <b>creator</b> <see cref="AccessVia.Owner"/>, its
    /// <b>assignee</b> (ADR 0067) or <b>Translator</b> / <b>GlobalAdmin</b>
    /// (ADR 0021/0030), ADR 0088 D3), the hand-written <c>AccessAudit</c> row,
    /// and the single atomic <c>SaveChangesAsync</c> (C3) all live on the lane;
    /// the caller supplies the <c>IDocumentSession</c>. A denied actor throws
    /// <see cref="UnauthorizedAccessException"/>; a missing to-do or row throws
    /// <see cref="KeyNotFoundException"/>.
    /// </summary>
    TranslationLane<TodoItem, TodoTranslation> TodoTranslationLane { get; }

}
