using Kumunita.Core.Authorization;

namespace Kumunita.Core.Projects;

/// <summary>
/// C3-03 (ADR 0293) — the **to-do comment** lane (ADR 0100 / C-M3·1),
/// extracted out of <see cref="TodoService"/> as a real part-vs-whole surface
/// (the <see cref="TodoCommentService"/> class + this interface — the C3-00
/// <c>EventRsvpGuardianService</c> pattern). The two comment methods are a
/// **named domain concept** with their own standing read (the to-do's single
/// <c>Read</c> decision is the access boundary, C-M3·1), their own
/// reply-parent validation (ADR 0100 / C-M5·7), their own mention-emitter
/// lane (M36 / ADR 0165, the <c>todo.mention</c> notification), and their own
/// author-only soft-delete (ADR 0016 / ADR 0024) — a C13 cold read of
/// <see cref="TodoCommentService"/> passes on its own.
/// <para>
/// The over-ceiling <c>CreateTodoCommentAsync</c> (68/cc18) lives here, so its
/// ratchet entry **re-points** to this named seam (the C3-00 <c>RsvpAsync</c>
/// re-point) rather than stay on the god-service. The two private helpers
/// (<c>BuildTodoAssignBodyAsync</c> for the assign lane,
/// <c>ComputeTodoEditChanged</c> for the edit lane) serve **other** lanes and
/// stay on <see cref="TodoService"/>.
/// </para>
/// </summary>
public interface ITodoCommentService
{
    /// <summary>Create a comment (or reply) on a to-do (the to-do's Read
    /// decision is the standing gate; the reply-parent validation is the
    /// ADR 0100 / C-M5·7 shape; the mention emitter runs on the write session,
    /// M36 / ADR 0165).</summary>
    Task<TodoComment> CreateTodoCommentAsync(
        string todoId, string actorId, IReadOnlySet<string> actorRoles,
        string body, string? languageCode, string? parentId = null,
        CancellationToken ct = default);

    /// <summary>Soft-delete a comment (author-only, ADR 0016 / ADR 0024 — the
    /// comment's own <c>DeletedAt</c> stamp; the record is kept, never
    /// hard-deleted).</summary>
    Task<TodoComment> DeleteTodoCommentAsync(
        string todoId, string commentId, string actorId, IReadOnlySet<string> actorRoles,
        CancellationToken ct = default);
}
