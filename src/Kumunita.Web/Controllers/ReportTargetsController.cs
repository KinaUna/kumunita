using Kumunita.Core.Events;
using Kumunita.Core.Moderation;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Kumunita.Web.Controllers;

/// <summary>
/// The <b>report-target intake</b> endpoints (ADR 0173, the
/// report-targets lane). Extends the existing M3b report lane
/// (<see cref="PostsController.Report"/>,
/// <see cref="PostsController.ReportReply"/>) to five more surfaces:
/// <b>events</b>, <b>projects</b>, <b>Kanban boards</b>, <b>to-dos</b>,
/// and <b>group posts</b>.
/// <para>
/// Each action is a Web-layer <b>thin</b> lane (ADR 0006-D: routes +
/// shape) that delegates the write to
/// <see cref="ModerationService.FileReportTargetAsync"/>. The Core
/// lane makes **no** <c>IAuthorizationService</c> call (it is an
/// *intake* action, not an access decision); the Web-layer gate is the
/// surface's own single <c>Read</c> decision, already run before the
/// caller may file.
/// <para>
/// **Authz shape** (per surface): the resident must be able to *see*
/// the target (the same fail-closed gate as the post-report lane —
/// the read seam returns <c>null</c> for both "missing" and "denied",
/// mapping to <c>Forbid()</c> 403).
/// <para>
/// **Session shape (C3 / ADR 0006-C):** the controller opens its own
/// <see cref="IDocumentStore.LightweightSession"/> and passes it to
/// <see cref="ModerationService.FileReportTargetAsync"/>; that service
/// performs the single <c>SaveChangesAsync</c>, so the <see
/// cref="Report"/> row and the filing <c>AccessAudit</c> row commit or
/// roll back atomically.
/// <para>
/// **Rate limiting:** the same <c>"report"</c> policy (10/hour/IP)
/// applies to every endpoint in this controller (the SECURITY.md
/// authorization-escalation surface).
/// </para>
/// </summary>
[Authorize]
public sealed class ReportTargetsController(
    IDocumentStore store,
    ModerationService moderation,
    Kumunita.Core.Events.IEventService events,
    Kumunita.Core.Projects.IProjectService projects,
    Kumunita.Core.Posts.PostService posts) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    // ── POST /events/{id}/report ───────────────────────────────────────

    /// <summary>
    /// A resident-facing **report intake** against an <b>event</b>
    /// (ADR 0173). The Web-layer gate is the event's own single
    /// <c>Read</c> decision — <see cref="IEventService.GetAsync"/>
    /// returns a non-null event only when the actor may see it
    /// (audience gate, C-M3·1 / ADR 0054 §3.4).
    /// </summary>
    [HttpPost("/events/{id}/report")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("report")]
    public async Task<IActionResult> ReportEvent(
        [FromRoute] string id,
        [FromForm] string? reason)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Forbid();

        // The event's single Read decision — the resident must be able
        // to *see* the event. GetAsync throws KeyNotFoundException
        // (missing) or UnauthorizedAccessException (denied).
        Event ev;
        try
        {
            ev = await events.GetAsync(id, actor, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return Forbid();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        await using var session = store.LightweightSession();
        await moderation.FileReportTargetAsync(
            targetKind:  "event",
            targetId:    ev.Id,
            componentId: ev.ComponentId,
            authorId:    ev.AuthorId,
            actorId:     actor,
            reason:      reason,
            session:     session);

        TempData["info"] = await this.FlashAsync("flash.report_submitted", "Report submitted. A moderator may review it.");
        return Redirect($"/events/{id}");
    }

    // ── POST /projects/projects/{id}/report ────────────────────────────

    /// <summary>
    /// A resident-facing **report intake** against a <b>project</b>
    /// (ADR 0173). The Web-layer gate is the project's own single
    /// <c>Read</c> decision — <see cref="IProjectService.GetProjectAsync"/>
    /// returns a non-null project only when the actor may see it.
    /// </summary>
    [HttpPost("/projects/projects/{id}/report")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("report")]
    public async Task<IActionResult> ReportProject(
        [FromRoute] string id,
        [FromForm] string? reason)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Forbid();

        Project project;
        try
        {
            project = await projects.GetProjectAsync(id, actor, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return Forbid();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        await using var session = store.LightweightSession();
        await moderation.FileReportTargetAsync(
            targetKind:  "project",
            targetId:    project.Id,
            componentId: project.ComponentId,
            authorId:    project.AuthorId,
            actorId:     actor,
            reason:      reason,
            session:     session);

        TempData["info"] = await this.FlashAsync("flash.report_submitted", "Report submitted. A moderator may review it.");
        return Redirect($"/projects/projects/{id}");
    }

    // ── POST /projects/boards/{id}/report ──────────────────────────────

    /// <summary>
    /// A resident-facing **report intake** against a <b>Kanban
    /// board</b> (ADR 0173). The Web-layer gate is the board's own
    /// two-level audience decision — <see cref="IProjectService.GetBoardAsync"/>
    /// returns a non-null board only when the actor may see it
    /// (C-M5·3).
    /// </summary>
    [HttpPost("/projects/boards/{id}/report")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("report")]
    public async Task<IActionResult> ReportBoard(
        [FromRoute] string id,
        [FromForm] string? reason)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Forbid();

        KanbanBoard board;
        try
        {
            var result = await projects.GetBoardAsync(id, actor, HttpContext.RequestAborted);
            board = result.Board;
        }
        catch (KeyNotFoundException)
        {
            return Forbid();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        await using var session = store.LightweightSession();
        await moderation.FileReportTargetAsync(
            targetKind:  "board",
            targetId:    board.Id,
            componentId: board.ComponentId,
            authorId:    board.AuthorId,
            actorId:     actor,
            reason:      reason,
            session:     session);

        TempData["info"] = await this.FlashAsync("flash.report_submitted", "Report submitted. A moderator may review it.");
        return Redirect($"/projects/boards/{id}");
    }

    // ── POST /projects/todos/{id}/report ───────────────────────────────

    /// <summary>
    /// A resident-facing **report intake** against a <b>to-do</b>
    /// (ADR 0173). The Web-layer gate is the to-do's own single
    /// <c>Read</c> decision — <see cref="IProjectService.GetTodoAsync"/>
    /// returns a non-null to-do only when the actor may see it.
    /// </summary>
    [HttpPost("/projects/todos/{id}/report")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("report")]
    public async Task<IActionResult> ReportTodo(
        [FromRoute] string id,
        [FromForm] string? reason)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Forbid();

        TodoItem todo;
        try
        {
            var result = await projects.GetTodoAsync(id, actor, HttpContext.RequestAborted);
            todo = result.Todo;
        }
        catch (KeyNotFoundException)
        {
            return Forbid();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        await using var session = store.LightweightSession();
        await moderation.FileReportTargetAsync(
            targetKind:  "todo",
            targetId:    todo.Id,
            componentId: todo.ComponentId,
            authorId:    todo.AuthorId,
            actorId:     actor,
            reason:      reason,
            session:     session);

        TempData["info"] = await this.FlashAsync("flash.report_submitted", "Report submitted. A moderator may review it.");
        return Redirect($"/projects/todos/{id}");
    }

    // ── POST /groups/{groupId}/posts/{postId}/report ───────────────────

    /// <summary>
    /// A resident-facing **report intake** against a <b>group post</b>
    /// (ADR 0173). The Web-layer gate is the group post's single
    /// <c>Read</c> decision — <see cref="PostService.GetGroupPostAsync"/>
    /// returns <c>Post = null</c> for both "missing" and "membership
    /// denied" (the ADR 0013 group-lane decision, G·5), and this action
    /// maps that to the <c>Forbid()</c> 403 shape (the same fail-closed
    /// gate as the post-report lane). The target kind is
    /// <c>"grouppost"</c> (distinct from the community <c>"post"</c>
    /// discriminator, so the queue can render the group context).
    /// </summary>
    [HttpPost("/groups/{groupId}/posts/{postId}/report")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("report")]
    public async Task<IActionResult> ReportGroupPost(
        [FromRoute] string groupId,
        [FromRoute] string postId,
        [FromForm] string? reason)
    {
        if (string.IsNullOrEmpty(groupId) || string.IsNullOrEmpty(postId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Forbid();

        // The group lane's single decision: GetGroupPostAsync runs the
        // ADR 0013 membership read + the post's audience check; a
        // missing post, a lane mismatch, or a membership Deny all
        // return Post = null (the group lane's fail-closed shape).
        var result = await posts.GetGroupPostAsync(groupId, postId, actor);
        if (result.Post is null)
            return Forbid();

        await using var session = store.LightweightSession();
        await moderation.FileReportTargetAsync(
            targetKind:  "grouppost",
            targetId:    result.Post.Id,
            componentId: result.Post.ComponentId,
            authorId:    result.Post.AuthorId,
            actorId:     actor,
            reason:      reason,
            session:     session);

        TempData["info"] = await this.FlashAsync("flash.report_submitted", "Report submitted. A moderator may review it.");
        return Redirect($"/groups/{groupId}/posts/{postId}");
    }
}
