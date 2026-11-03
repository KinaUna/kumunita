using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Documents;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Inventory;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Targets;

/// <summary>
/// The single content-target resolution seam (IMPROVE-CODE C05 — ADR 0118
/// D3/D5 + ADR 0167 D1/D7). Replaces the four private clones:
/// <c>BookmarkService.ResolveTargetAsync</c> / <c>ResolveForListAsync</c>
/// and <c>PersonalNoteService.ResolveTargetAsync</c> / <c>ResolveForListAsync</c>.
/// <para>
/// **Write lane** (<see cref="ResolveAsync"/>): re-runs the target's own
/// frozen <c>CanAsync(Read)</c> (or the owning surface's own <c>GetAsync</c>
/// read, which commits the target's own audit row). The same call the
/// detail page uses (the ADR 0118 D3 / ADR 0167 D3 pin).
/// </para>
/// <para>
/// **List lane** (<see cref="ResolveForListAsync"/>): a pure, non-auditing
/// doc load (the ADR 0118 D5 / C-M17·2 zero-audit shape). Commits no
/// <c>AccessAudit</c> row, makes no <c>CanAsync(Read)</c> call.
/// </para>
/// <para>
/// The pure kind table (<see cref="ContentTargetKinds"/>) is unit-testable
/// with no Postgres. The loader calls (this class) are integration-tested
/// at the seam (one test per kind family).
/// </para>
/// <para>
/// **D6 boundary:** this class is in the <c>Targets</c> module. The
/// <c>Bookmarks</c> and <c>PersonalNotes</c> modules reference only
/// <see cref="IContentTargetResolver"/> (an interface — legal) and
/// <see cref="ContentTargetKinds"/> (a static <c>*Kinds</c> class — legal).
/// This class's outbound references to document types in other modules are
/// baselined boundary violations (the same category as the clones it
/// replaces).
/// </para>
/// </summary>
public sealed class ContentTargetResolver : IContentTargetResolver
{
    private readonly IUserInfoService _userInfo;
    private readonly IAuthorizationService _authz;
    private readonly IDocumentStore _store;
    private readonly IAnnouncementService _announcements;
    private readonly IEventService _events;
    private readonly IProjectService _projects;
    private readonly IInventoryService _inventory;
    private readonly IIdentityService _identity;

    public ContentTargetResolver(
        IUserInfoService userInfo,
        IAuthorizationService authz,
        IDocumentStore store,
        IAnnouncementService announcements,
        IEventService events,
        IProjectService projects,
        IInventoryService inventory,
        IIdentityService identity)
    {
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _authz = authz ?? throw new ArgumentNullException(nameof(authz));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _announcements = announcements ?? throw new ArgumentNullException(nameof(announcements));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
    }

    // ── Write lane (D3 create-gate check) ────────────────────────────────────

    /// <inheritdoc />
    public async Task<(bool Visible, string? Title, string? Link)> ResolveAsync(
        string targetKind, string targetId, string actorId, string surface)
    {
        ArgumentNullException.ThrowIfNull(targetKind);
        ArgumentNullException.ThrowIfNull(targetId);
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(surface);

        if (!ContentTargetKinds.IsValid(targetKind, surface))
            return (false, null, null);

        // Static page-* rows (ADR 0167 amendment) — always visible, fixed title + link.
        if (ContentTargetKinds.TryStaticRow(targetKind, out var staticRow))
            return (true, staticRow.Title, staticRow.Link);

        switch (targetKind)
        {
            case "post":
                return await ResolvePostTargetAsync(targetId, actorId);
            case "grouppost":
                return await ResolveGrouppostTargetAsync(targetId, actorId);
            case "event":
                return await ResolveEventTargetAsync(targetId, actorId);
            case "todo":
                return await ResolveTodoTargetAsync(targetId, actorId);
            case "project":
                return await ResolveProjectTargetAsync(targetId, actorId, surface);
            case "board":
                return await ResolveBoardTargetAsync(targetId, actorId);
            case "announcement":
                return await ResolveAnnouncementTargetAsync(targetId, actorId);
            case "page":
                return await ResolvePageTargetAsync(targetId, actorId);
            case "inventory":
                return await ResolveInventoryTargetAsync(targetId, actorId);
            case "directory":
                return await ResolveDirectoryTargetAsync(targetId, actorId);
            case "document":
                return await ResolveDocumentTargetAsync(targetId, actorId);
            case "page-group":
                return await ResolvePageGroupTargetAsync(targetId, actorId);
            case "page-project":
                return await ResolvePageProjectTargetAsync(targetId, actorId);
            default:
                return (false, null, null);
        }
    }

    // ── List lane (D5 zero-audit personal read) ──────────────────────────────

    /// <inheritdoc />
    public async Task<(string? Title, string? Link)> ResolveForListAsync(
        string targetKind, string targetId, string actorId, string surface)
    {
        if (!ContentTargetKinds.IsValid(targetKind, surface))
            return (null, null);

        if (ContentTargetKinds.TryStaticRow(targetKind, out var staticRow))
            return (staticRow.Title, staticRow.Link);

        switch (targetKind)
        {
            case "post":
                return await ResolvePostListAsync(targetId, actorId);
            case "grouppost":
                return await ResolveGrouppostListAsync(targetId, actorId);
            case "event":
                return await ResolveEventListAsync(targetId, actorId);
            case "todo":
                return await ResolveTodoListAsync(targetId);
            case "project":
                return await ResolveProjectListAsync(targetId, surface);
            case "board":
                return await ResolveBoardListAsync(targetId);
            case "announcement":
                return await ResolveAnnouncementListAsync(targetId, actorId, surface);
            case "page":
                return await ResolvePageListAsync(targetId, actorId);
            case "inventory":
                return await ResolveInventoryListAsync(targetId);
            case "directory":
                return await ResolveDirectoryListAsync(targetId);
            case "document":
                return await ResolveDocumentListAsync(targetId);
            case "page-group":
                return await ResolvePageGroupListAsync(targetId, actorId);
            case "page-project":
                return await ResolvePageProjectListAsync(targetId);
            default:
                return (null, null);
        }
    }

    // ── Write-lane loaders (D3 — the target's own frozen read) ──────────────

    private async Task<(bool, string?, string?)> ResolvePostTargetAsync(
        string targetId, string actorId)
    {
        await using var s = _store.QuerySession();
        var post = await s.LoadAsync<Post>(targetId).ConfigureAwait(false);
        if (post is null || post.DeletedAt is not null)
            return (false, null, null);
        if (post.IsDraft && !string.Equals(post.AuthorId, actorId, StringComparison.Ordinal))
            return (false, null, null);
        var decision = await _authz.CanAsync(actorId, AccessAction.Read, new PostToAuditableResource(post))
            .ConfigureAwait(false);
        if (!decision.Allowed)
            return (false, null, null);
        return (true, post.Title, ContentTargetKinds.PostLink(targetId));
    }

    private async Task<(bool, string?, string?)> ResolveGrouppostTargetAsync(
        string targetId, string actorId)
    {
        await using var s = _store.QuerySession();
        var post = await s.LoadAsync<Post>(targetId).ConfigureAwait(false);
        if (post is null || post.DeletedAt is not null)
            return (false, null, null);
        if (string.IsNullOrEmpty(post.GroupId))
            return (false, null, null);
        if (post.IsDraft && !string.Equals(post.AuthorId, actorId, StringComparison.Ordinal))
            return (false, null, null);
        var decision = await _authz.CanSeeGroupAsync(actorId, post.GroupId, targetId)
            .ConfigureAwait(false);
        if (!decision.Allowed)
            return (false, null, null);
        return (true, post.Title, ContentTargetKinds.GrouppostLink(post.GroupId, targetId));
    }

    private async Task<(bool, string?, string?)> ResolveEventTargetAsync(
        string targetId, string actorId)
    {
        try
        {
            var ev = await _events.GetAsync(targetId, actorId).ConfigureAwait(false);
            return (true, ev.Title, ContentTargetKinds.EventLink(targetId));
        }
        catch (KeyNotFoundException) { return (false, null, null); }
        catch (UnauthorizedAccessException) { return (false, null, null); }
    }

    private async Task<(bool, string?, string?)> ResolveTodoTargetAsync(
        string targetId, string actorId)
    {
        try
        {
            var detail = await _projects.GetTodoAsync(targetId, actorId).ConfigureAwait(false);
            return (true, detail.Todo.Title, ContentTargetKinds.TodoLink(targetId));
        }
        catch (KeyNotFoundException) { return (false, null, null); }
        catch (UnauthorizedAccessException) { return (false, null, null); }
    }

    private async Task<(bool, string?, string?)> ResolveProjectTargetAsync(
        string targetId, string actorId, string surface)
    {
        try
        {
            var project = await _projects.GetProjectAsync(targetId, actorId).ConfigureAwait(false);
            return (true, project.Title, ContentTargetKinds.ProjectLink(targetId, surface));
        }
        catch (KeyNotFoundException) { return (false, null, null); }
        catch (UnauthorizedAccessException) { return (false, null, null); }
    }

    private async Task<(bool, string?, string?)> ResolveBoardTargetAsync(
        string targetId, string actorId)
    {
        try
        {
            var detail = await _projects.GetBoardAsync(targetId, actorId).ConfigureAwait(false);
            return (true, detail.Board.Title, ContentTargetKinds.BoardLink(targetId));
        }
        catch (KeyNotFoundException) { return (false, null, null); }
        catch (UnauthorizedAccessException) { return (false, null, null); }
    }

    private async Task<(bool, string?, string?)> ResolveAnnouncementTargetAsync(
        string targetId, string actorId)
    {
        var principal = await _identity.GetBySubjectAsync(actorId).ConfigureAwait(false);
        var roles = principal is not null
            ? new HashSet<string>(principal.Roles, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var a = await _announcements.GetAsync(targetId, actorId, roles).ConfigureAwait(false);
        if (a is null)
            return (false, null, null);
        return (true, a.Title, ContentTargetKinds.AnnouncementLink(targetId));
    }

    private async Task<(bool, string?, string?)> ResolvePageTargetAsync(
        string targetId, string actorId)
    {
        await using var s = _store.QuerySession();
        var page = await s.LoadAsync<Page>(targetId).ConfigureAwait(false);
        if (page is null || page.IsDeleted)
            return (false, null, null);
        if (page.IsDraft && !string.Equals(page.AuthorId, actorId, StringComparison.Ordinal))
            return (false, null, null);
        var decision = await _authz.CanAsync(actorId, AccessAction.Read, new PageToAuditableResource(page))
            .ConfigureAwait(false);
        if (!decision.Allowed)
            return (false, null, null);
        var path = await BuildPagePathAsync(s, page.Id).ConfigureAwait(false);
        return path is null ? (false, null, null) : (true, page.Title, path);
    }

    private async Task<(bool, string?, string?)> ResolveInventoryTargetAsync(
        string targetId, string actorId)
    {
        try
        {
            var item = await _inventory.GetItemAsync(targetId, actorId).ConfigureAwait(false);
            return (true, item.Name, ContentTargetKinds.InventoryLink(targetId));
        }
        catch (KeyNotFoundException) { return (false, null, null); }
        catch (UnauthorizedAccessException) { return (false, null, null); }
    }

    private async Task<(bool, string?, string?)> ResolveDirectoryTargetAsync(
        string targetId, string actorId)
    {
        var profile = await _userInfo.GetProfileAsync(targetId).ConfigureAwait(false);
        if (profile is null || profile.Blocked)
            return (false, null, null);
        var decision = await _authz.CanAsync(
            actorId, AccessAction.Read, new ProfileToAuditableResource(profile))
            .ConfigureAwait(false);
        if (!decision.Allowed)
            return (false, null, null);
        return (true, profile.DisplayName, ContentTargetKinds.DirectoryLink(targetId));
    }

    private async Task<(bool, string?, string?)> ResolveDocumentTargetAsync(
        string targetId, string actorId)
    {
        await using var s = _store.QuerySession();
        var doc = await s.LoadAsync<Document>(targetId).ConfigureAwait(false);
        if (doc is null || doc.DeletedAt is not null)
            return (false, null, null);
        var decision = await _authz.CanAsync(
            actorId, AccessAction.Read, new DocumentToAuditableResource(doc))
            .ConfigureAwait(false);
        if (!decision.Allowed)
            return (false, null, null);
        return (true, doc.Title, ContentTargetKinds.DocumentLink(targetId));
    }

    private async Task<(bool, string?, string?)> ResolvePageGroupTargetAsync(
        string targetId, string actorId)
    {
        if (targetId == ContentTargetKinds.PageGroupStaticId)
            return (true, "Groups", "/groups");
        var groups = await _userInfo.GetGroupsForUserAsync(actorId).ConfigureAwait(false);
        var g = groups.FirstOrDefault(gr => gr.Id == targetId);
        if (g is null)
            return (false, null, null);
        return (true, g.Name, ContentTargetKinds.GroupLink(targetId));
    }

    private async Task<(bool, string?, string?)> ResolvePageProjectTargetAsync(
        string targetId, string actorId)
    {
        try
        {
            var p = await _projects.GetProjectAsync(targetId, actorId).ConfigureAwait(false);
            return (true, p.Title, ContentTargetKinds.PageProjectLink(targetId));
        }
        catch (KeyNotFoundException) { return (false, null, null); }
        catch (UnauthorizedAccessException) { return (false, null, null); }
    }

    // ── List-lane loaders (D5 — pure, non-auditing doc load) ─────────────────

    private async Task<(string?, string?)> ResolvePostListAsync(string targetId, string actorId)
    {
        await using var s = _store.QuerySession();
        var post = await s.LoadAsync<Post>(targetId).ConfigureAwait(false);
        if (post is null || post.DeletedAt is not null)
            return (null, null);
        if (post.IsDraft && !string.Equals(post.AuthorId, actorId, StringComparison.Ordinal))
            return (null, null);
        return (post.Title, ContentTargetKinds.PostLink(targetId));
    }

    private async Task<(string?, string?)> ResolveGrouppostListAsync(string targetId, string actorId)
    {
        await using var s = _store.QuerySession();
        var post = await s.LoadAsync<Post>(targetId).ConfigureAwait(false);
        if (post is null || post.DeletedAt is not null)
            return (null, null);
        if (post.IsDraft && !string.Equals(post.AuthorId, actorId, StringComparison.Ordinal))
            return (null, null);
        return (post.Title, ContentTargetKinds.GrouppostLink(post.GroupId ?? string.Empty, targetId));
    }

    private async Task<(string?, string?)> ResolveEventListAsync(string targetId, string actorId)
    {
        await using var s = _store.QuerySession();
        var ev = await s.LoadAsync<Event>(targetId).ConfigureAwait(false);
        if (ev is null || ev.IsDeleted)
            return (null, null);
        if (ev.IsDraft && !string.Equals(ev.AuthorId, actorId, StringComparison.Ordinal))
            return (null, null);
        return (ev.Title, ContentTargetKinds.EventLink(targetId));
    }

    private async Task<(string?, string?)> ResolveTodoListAsync(string targetId)
    {
        await using var s = _store.QuerySession();
        var todo = await s.LoadAsync<TodoItem>(targetId).ConfigureAwait(false);
        if (todo is null || todo.IsDeleted)
            return (null, null);
        return (todo.Title, ContentTargetKinds.TodoLink(targetId));
    }

    private async Task<(string?, string?)> ResolveProjectListAsync(string targetId, string surface)
    {
        await using var s = _store.QuerySession();
        var project = await s.LoadAsync<Project>(targetId).ConfigureAwait(false);
        if (project is null || project.IsDeleted)
            return (null, null);
        return (project.Title, ContentTargetKinds.ProjectLink(targetId, surface));
    }

    private async Task<(string?, string?)> ResolveBoardListAsync(string targetId)
    {
        await using var s = _store.QuerySession();
        var board = await s.LoadAsync<KanbanBoard>(targetId).ConfigureAwait(false);
        if (board is null || board.IsDeleted)
            return (null, null);
        return (board.Title, ContentTargetKinds.BoardLink(targetId));
    }

    private async Task<(string?, string?)> ResolveAnnouncementListAsync(
        string targetId, string actorId, string surface)
    {
        await using var s = _store.QuerySession();
        var a = await s.LoadAsync<Announcement>(targetId).ConfigureAwait(false);
        if (a is null)
            return (null, null);
        // ADR 0167 — the M37 surface applies the author-only draft gate
        // (the M17 BookmarkService shape re-applied; a pure ordinal check —
        // no CanAsync, no audit row). The M17 surface does NOT apply the
        // draft gate on the list lane (the ADR 0118 D5 shape).
        if (surface == ContentTargetKinds.PersonalNotes
            && a.IsDraft && !string.Equals(a.AuthorId, actorId, StringComparison.Ordinal))
            return (null, null);
        return (a.Title, ContentTargetKinds.AnnouncementLink(targetId));
    }

    private async Task<(string?, string?)> ResolvePageListAsync(string targetId, string actorId)
    {
        await using var s = _store.QuerySession();
        var page = await s.LoadAsync<Page>(targetId).ConfigureAwait(false);
        if (page is null || page.IsDeleted)
            return (null, null);
        if (page.IsDraft && !string.Equals(page.AuthorId, actorId, StringComparison.Ordinal))
            return (null, null);
        var path = await BuildPagePathAsync(s, page.Id).ConfigureAwait(false);
        return path is null ? (null, null) : (page.Title, path);
    }

    private async Task<(string?, string?)> ResolveInventoryListAsync(string targetId)
    {
        await using var s = _store.QuerySession();
        var item = await s.LoadAsync<InventoryItem>(targetId).ConfigureAwait(false);
        if (item is null || item.IsDeleted)
            return (null, null);
        return (item.Name, ContentTargetKinds.InventoryLink(targetId));
    }

    private async Task<(string?, string?)> ResolveDirectoryListAsync(string targetId)
    {
        var profile = await _userInfo.GetProfileAsync(targetId).ConfigureAwait(false);
        if (profile is null || profile.Blocked)
            return (null, null);
        return (profile.DisplayName, ContentTargetKinds.DirectoryLink(targetId));
    }

    private async Task<(string?, string?)> ResolveDocumentListAsync(string targetId)
    {
        await using var s = _store.QuerySession();
        var doc = await s.LoadAsync<Document>(targetId).ConfigureAwait(false);
        if (doc is null || doc.DeletedAt is not null)
            return (null, null);
        return (doc.Title, ContentTargetKinds.DocumentLink(targetId));
    }

    private async Task<(string?, string?)> ResolvePageGroupListAsync(string targetId, string actorId)
    {
        if (targetId == ContentTargetKinds.PageGroupStaticId)
            return ("Groups", "/groups");
        var groups = await _userInfo.GetGroupsForUserAsync(actorId).ConfigureAwait(false);
        var g = groups.FirstOrDefault(gr => gr.Id == targetId);
        if (g is null)
            return (null, null);
        return (g.Name, ContentTargetKinds.GroupLink(targetId));
    }

    private async Task<(string?, string?)> ResolvePageProjectListAsync(string targetId)
    {
        await using var s = _store.QuerySession();
        var project = await s.LoadAsync<Project>(targetId).ConfigureAwait(false);
        if (project is null || project.IsDeleted)
            return (null, null);
        return (project.Title, ContentTargetKinds.PageProjectLink(targetId));
    }

    // ── Shared helper ────────────────────────────────────────────────────────

    /// <summary>
    /// The <c>/pages/</c>-prefixed ancestor-slug chain of a page (the
    /// <see cref="PageService.GetPathAsync"/> shape, walked over the store).
    /// <c>null</c> on a broken chain.
    /// </summary>
    private async Task<string?> BuildPagePathAsync(IQuerySession s, string pageId)
    {
        var segments = new List<string>();
        string? currentId = pageId;
        int guard = 0;
        while (currentId is not null && ++guard <= 64)
        {
            var page = await s.LoadAsync<Page>(currentId).ConfigureAwait(false);
            if (page is null)
                return null;
            if (!string.IsNullOrWhiteSpace(page.Slug))
                segments.Add(page.Slug);
            currentId = page.ParentId;
        }
        if (segments.Count == 0)
            return null;
        segments.Reverse();
        return "/pages/" + string.Join("/", segments);
    }
}
