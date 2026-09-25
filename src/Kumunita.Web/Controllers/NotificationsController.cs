using Kumunita.Core.Notifications;
using Kumunita.Core.Projects;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/notifications</c> surface (M6 — the **shared awareness** arrow,
/// ADR 0076): the resident's **inbox** (the durable record — D5), the
/// **unread-count** poller endpoint (the layout bell's 30-second target,
/// D10), the **mark-all-read** state lane (D8), and the **preferences**
/// editor (D9). A *thin* HTTP layer (ADR 0006-D): routes + the auth-gate +
/// shape; the frozen <see cref="NotificationService"/> (U03 — the
/// **frozen** six-method surface, unit-series rule 4) is the single
/// decision path.
/// <list type="bullet">
/// <item><c>GET /notifications</c> — the inbox: the caller's most recent
/// <see cref="NotificationService.InboxCap"/> (50) rows, newest-first
/// (C-M6·8); a **personal read** (C-M6·3) — no
/// <c>IAuthorizationService</c> call, no audit row (F11).</item>
/// <item><c>GET /notifications/unread-count</c> — the poller endpoint:
/// <c>200</c> + JSON <c>{ "count": N }</c> (the 30-second bell poller's
/// call target, C-M6·10).</item>
/// <item><c>POST /notifications/mark-all-read</c> — the state lane: sets
/// <c>ReadAt = now</c> on **all** the caller's unread rows (one commit,
/// C-M6·8), then back to the inbox.</item>
/// <item><c>GET /notifications/preferences</c> — the preference read:
/// the eleven <see cref="NotificationKinds.Known"/> toggles (ADR 0077
/// adds the two admin-lane kinds), the lean-default (C-M6·9 — no
/// preference yet = all enabled).</item>
/// <item><c>POST /notifications/preferences</c> — the preference write
/// (the resident's own choice is the authority at emit time, C-M6·7),
/// then back to the inbox.</item>
/// </list>
/// <para>
/// **The auth-gate is the <c>[Authorize]</c> attribute** (the M4 / M5
/// controller precedent — the *identity* check); the actor id is minted
/// from the signed-in cookie via <see cref="KumunitaPrincipal.SubjectId"/>
/// (the thin-token rule). There is **no audience to evaluate** on any of
/// these lanes — the <c>RecipientId</c> *is* the whole access story
/// (C-M6·3), so no <c>IAuthorizationService</c> seam appears here at all
/// (C-M6·11 — no new seam, no re-derivation).
/// </para>
/// </summary>
[Authorize]
public sealed class NotificationsController(
    NotificationService notifications,
    IProjectService? projects = null) : Controller
{
    /// <summary>
    /// <c>GET /notifications</c> — the inbox (design doc §6.4 route 1,
    /// F11): the caller's most recent 50 rows, newest-first (C-M6·8). A
    /// personal read — the <c>[Authorize]</c> gate + the actor being the
    /// recipient is the whole decision (C-M6·3); no audit row.
    /// </summary>
    [HttpGet("/notifications")]
    public async Task<IActionResult> Index()
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return NotFound();

        var rows = await notifications.ListInboxAsync(actorId);

        // todo.assign rows (the kind whose SourceId is a TodoItem.Id) are
        // enriched at **read time** with a structured card: the to-do's title
        // / body / status / start / due, the subtasks the actor may read
        // (via the existing access-filtered GetTodoAsync), and the boards the
        // actor may read on which the to-do sits (the new
        // ListBoardsForTodoAsync read lane — the "link(s) to the boards it
        // is associated with, *if the user has access to them*" surface,
        // ADR 0006-D: the access decision happens in Core, not here). A row
        // that cannot be enriched (the to-do was later deleted, or the actor
        // may not read it) degrades to the plain subject + body — the inbox
        // never 404s on an old notification (C-M6·3, personal read).
        var todoCards = new Dictionary<string, TodoCard>(StringComparer.Ordinal);
        if (projects is not null)
        {
            foreach (var row in rows)
            {
                if (row.Kind != NotificationKinds.TodoAssign || row.SourceId is null)
                    continue;

                try
                {
                    var detail = await projects.GetTodoAsync(row.SourceId, actorId);
                    var boards = await projects.ListBoardsForTodoAsync(row.SourceId, actorId);
                    todoCards[row.SourceId] = new TodoCard(detail.Todo, boards, detail.Subtasks);
                }
                catch (Exception ex) when (
                    ex is KeyNotFoundException or UnauthorizedAccessException)
                {
                    // The to-do no longer exists or the actor may no longer
                    // read it — leave the row to render as its plain subject
                    // + body rather than failing the whole inbox.
                }
            }
        }

        return View(new NotificationsInboxViewModel(rows, rows.Count, todoCards));
    }

    /// <summary>
    /// <c>GET /notifications/unread-count</c> — the layout bell's poller
    /// endpoint (design doc §6.4 route 2, C-M6·10): <c>200</c> +
    /// <c>application/json</c> <c>{ "count": N }</c> at the 30-second
    /// poll interval. A personal read — no audit row (C-M6·3, F11).
    /// </summary>
    [HttpGet("/notifications/unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return NotFound();

        var count = await notifications.CountUnreadAsync(actorId);
        return Json(new { count });
    }

    /// <summary>
    /// <c>POST /notifications/mark-all-read</c> — the state lane
    /// (design doc §6.4 route 3, C-M6·8): sets <c>ReadAt = now</c> on
    /// **all** the caller's unread rows in one commit (the frozen service
    /// owns the write — C-M6·11), then back to the inbox. A state lane,
    /// not a read — no audit row (C-M6·3).
    /// </summary>
    [HttpPost("/notifications/mark-all-read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return NotFound();

        await notifications.MarkAllReadAsync(actorId);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <c>GET /notifications/preferences</c> — the preference read
    /// (design doc §6.4 route 4, C-M6·9): the eleven
    /// <see cref="NotificationKinds.Known"/> toggles (ADR 0077 adds the
    /// two admin-lane kinds); the lean-default — no stored preference yet
    /// = <c>KindsEnabled</c>
    /// <c>null</c> = all enabled (the service never throws for a missing
    /// preference row). A personal read — no audit row (C-M6·3).
    /// </summary>
    [HttpGet("/notifications/preferences")]
    public async Task<IActionResult> Preferences()
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return NotFound();

        var preference = await notifications.GetPreferencesAsync(actorId);
        return View(new NotificationPreferencesViewModel(
            preference.KindsEnabled,
            NotificationKinds.Known));
    }

    /// <summary>
    /// <c>POST /notifications/preferences</c> — the preference write
    /// (design doc §6.4 route 5, C-M6·9): upserts the caller's
    /// <see cref="NotificationPreference"/> with the bound toggle set
    /// (the resident's own choice is the authority at emit time, C-M6·7),
    /// then back to the inbox. A state lane — no audit row (C-M6·3).
    /// </summary>
    [HttpPost("/notifications/preferences")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePreferences(NotificationPreferencesViewModel model)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return NotFound();

        // C-M6·2 (the closed kind vocabulary): a bound list containing a
        // kind outside <see cref="NotificationKinds.Known"/> is not a
        // legitimate toggle set — store the intersection (a client cannot
        // mint a kind string the code's emitters don't use). An empty or
        // absent list passes through as-is (empty = all disabled; the
        // lean-default <c>null</c> is what the *reader* synthesizes, not a
        // writable value).
        var submitted = model.KindsEnabled;
        var enabled = submitted is null
            ? null
            : submitted.Where(k => NotificationKinds.Known.Contains(k, StringComparer.Ordinal)).ToList();

        await notifications.SetPreferencesAsync(actorId, enabled);
        return RedirectToAction(nameof(Index));
    }
}
