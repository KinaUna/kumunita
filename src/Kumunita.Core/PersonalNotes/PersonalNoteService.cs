using Kumunita.Core.Authorization;
using Kumunita.Core.Documents;
using Kumunita.Core.Events;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.PersonalNotes;

/// <summary>
/// The <c>M37</c> (Personal notes) composition service (ADR 0167 / design doc
/// <c>m37-personal-notes-design.md</c> §2.2–§2.5). A concrete class pairing the
/// frozen seams with the host-registered Marten <see cref="IDocumentStore"/>
/// (the M17 <c>BookmarkService</c> / M3 <c>PostService</c> shape) — it
/// **composes** seams, it is not itself one.
/// <para>
/// **M37 composes only frozen seams** (C-M37·1): the frozen
/// <see cref="IAuthorizationService"/> (the D3 write-lane check only) + the
/// owning surfaces' existing read seams (the D7 list resolution) over the
/// <see cref="IDocumentStore"/>. **No** <c>INotificationService</c>,
/// **no** new <c>AccessAction</c> / <c>AccessVia</c> / <c>Decide()</c>
/// branch, **no** adapter of its own.
/// </para>
/// <para>
/// **U02 implements the 8 note methods; the 4 folder-tree methods are
/// U03** (stubbed with <see cref="NotImplementedException"/>).
/// </para>
/// </summary>
public sealed class PersonalNoteService : IPersonalNoteService
{
    /// <summary>
    /// The closed <c>TargetKind</c> set (D1 — the M5 <c>KanbanStatuses</c>
    /// string-not-enum shape), the exact vocabulary the existing
    /// <c>*ToAuditableResource</c> adapters and <c>AccessAudit</c> rows
    /// already emit. A kind outside this set is a drift event (§2.7).
    /// </summary>
    public static readonly IReadOnlySet<string> ClosedTargetKinds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "post", "grouppost", "event", "page", "project",
            "todo", "board", "document", "directory",
        };

    // The closed set's canonical order (for group ordering in the list).
    private static readonly string[] ClosedTargetKindsOrdered =
        ["post", "grouppost", "event", "page", "project",
         "todo", "board", "document", "directory"];

    private readonly IUserInfoService _userInfo;
    private readonly IAuthorizationService _authz;
    private readonly IDocumentStore _store;
    private readonly IEventService _events;
    private readonly IProjectService _projects;

    /// <summary>
    /// Composes the frozen seams over the host-registered Marten
    /// <see cref="IDocumentStore"/> (design doc §2.2 composition root —
    /// **no** <c>INotificationService</c>). The extra seams versus the
    /// M17 <c>BookmarkService</c> are the frozen <see cref="IEventService"/>
    /// and <see cref="IProjectService"/> (the D3 write-lane check re-runs
    /// the *same call the detail page uses*). They never cross the frozen
    /// <see cref="IPersonalNoteService"/> seam — composition detail, not a
    /// new surface (C-M37·1).
    /// </summary>
    public PersonalNoteService(
        IUserInfoService userInfo,
        IAuthorizationService authz,
        IDocumentStore store,
        IEventService events,
        IProjectService projects)
    {
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _authz = authz ?? throw new ArgumentNullException(nameof(authz));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
    }

    // ── CreateNoteAsync (D3 write-lane check; F1 idempotent) ──────────────

    /// <inheritdoc />
    public async Task<NoteRow> CreateNoteAsync(
        string ownerId, string targetKind, string targetId, string body,
        IDocumentSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentNullException.ThrowIfNull(session);

        // The closed set is the boundary of the note world (D1): a kind
        // outside it has no owning surface to re-check, so there is no
        // target to note (fail-closed — the Web layer 404s it).
        if (!ClosedTargetKinds.Contains(targetKind))
            throw new KeyNotFoundException($"Unknown target kind '{targetKind}'.");

        // D3 (design doc §2.3) — re-run the target's own frozen Read decision
        // FIRST, the *same call the target's detail page uses*: Deny / absent
        // / soft-deleted ⇒ throw (404; no row; the target's own audit row is
        // the decision's, not M37's).
        var (visible, title, link) =
            await ResolveTargetAsync(targetKind, targetId, ownerId).ConfigureAwait(false);
        if (!visible)
            throw new KeyNotFoundException($"Target '{targetId}' is not visible to the actor.");

        // F1 — the unique (OwnerId, TargetKind, TargetId) row is at-most-one:
        // an existing row is the no-op (AlreadyNoted), one row, one Created.
        // The PersonalNotesDocTypes unique index is the DB witness.
        var existing = await session.Query<PersonalNote>()
            .Where(n => n.OwnerId == ownerId && n.TargetKind == targetKind && n.TargetId == targetId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (existing is not null)
            return ToNoteRow(existing, title, link);

        var now = DateTimeOffset.UtcNow;
        var note = new PersonalNote
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = ownerId,
            TargetKind = targetKind,
            TargetId = targetId,
            Body = body,
            Created = now,
        };
        session.Store(note);

        return new NoteRow(
            Id: note.Id,
            TargetKind: targetKind,
            TargetId: targetId,
            Body: body,
            FolderId: null,
            Tags: [],
            Created: now,
            Modified: null,
            Degraded: false,
            Title: title,
            Link: link);
    }

    // ── GetNoteAsync (C-M37·2 zero-audit personal read) ───────────────────

    /// <inheritdoc />
    public async Task<NoteRow> GetNoteAsync(string ownerId, string noteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(noteId);

        await using var q = _store.QuerySession();
        var note = await q.LoadAsync<PersonalNote>(noteId).ConfigureAwait(false);
        // Non-owner (a GlobalAdmin included) gets a non-leaky 404 — the
        // Web layer maps to 404; the Core seam throws KeyNotFoundException.
        // No AccessAudit row (C-M37·2 — the ADR 0105 "operator has no read
        // standing" precedent).
        if (note is null || !string.Equals(note.OwnerId, ownerId, StringComparison.Ordinal))
            throw new KeyNotFoundException($"Note '{noteId}' not found for owner '{ownerId}'.");

        var (title, link) =
            await ResolveForListAsync(note.TargetKind, note.TargetId, ownerId).ConfigureAwait(false);
        return ToNoteRow(note, title, link);
    }

    // ── ListNotesAsync (C-M37·2 zero-audit personal read) ─────────────────

    /// <inheritdoc />
    public async Task<NoteListResult> ListNotesAsync(string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        // C-M37·2 — the owner's own rows, loaded by the identity predicate
        // alone (no AccessAudit row, no CanSeeAsync pass over the note rows
        // — the ADR 0105 / M6-inbox personal-read shape).
        await using var q = _store.QuerySession();
        var notes = await q.Query<PersonalNote>()
            .Where(n => n.OwnerId == ownerId)
            .OrderByDescending(n => n.Created)
            .ToListAsync()
            .ConfigureAwait(false);

        // Resolve each note (D7) via a **pure, non-auditing doc load** — the
        // C-M37·2 / ADR 0105 "reads never audit" shape. Resolved rows carry
        // Title + Link; a dead / hidden target degrades to (null, null) so
        // the Web layer renders the closed pn.list.degraded label (C-M37·5).
        // Deliberately NOT the D3 write-lane check (which re-runs the
        // target's frozen CanAsync(Read) and would commit the target's own
        // audit row — forbidden on the personal list read by C-M37·2).
        var resolved = new List<(PersonalNote Note, string? Title, string? Link)>();
        foreach (var note in notes)
        {
            var (title, link) =
                await ResolveForListAsync(note.TargetKind, note.TargetId, ownerId).ConfigureAwait(false);
            resolved.Add((note, title, link));
        }

        // Group by (TargetKind, FolderId) in the closed set's canonical order
        // (D1), only including kinds the owner actually noted.
        var groups = new List<NoteGroup>();
        foreach (var kind in ClosedTargetKindsOrdered)
        {
            var kindNotes = resolved
                .Where(r => string.Equals(r.Note.TargetKind, kind, StringComparison.Ordinal))
                .ToList();
            if (kindNotes.Count == 0) continue;

            // Root group (FolderId = null).
            var rootItems = kindNotes
                .Where(r => r.Note.FolderId is null)
                .Select(r => ToNoteRow(r.Note, r.Title, r.Link))
                .ToList();
            if (rootItems.Count > 0)
                groups.Add(new NoteGroup(kind, null, rootItems));

            // Folder sub-groups.
            var folderIds = kindNotes
                .Select(r => r.Note.FolderId)
                .Where(f => f is not null)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            foreach (var folderId in folderIds)
            {
                var items = kindNotes
                    .Where(r => r.Note.FolderId == folderId)
                    .Select(r => ToNoteRow(r.Note, r.Title, r.Link))
                    .ToList();
                groups.Add(new NoteGroup(kind, folderId, items));
            }
        }

        // Load the owner's folders (D6 — the personal tree).
        var folders = await q.Query<NoteFolder>()
            .Where(f => f.OwnerId == ownerId)
            .OrderBy(f => f.Created)
            .ToListAsync()
            .ConfigureAwait(false);

        var folderRows = folders
            .Select(f => new FolderRow(f.Id, f.Name, f.ParentId,
                notes.Count(n => n.FolderId == f.Id)))
            .ToList();

        return new NoteListResult(groups, folderRows);
    }

    // ── UpdateNoteBodyAsync (D4 body-is-content, Modified witness) ────────

    /// <inheritdoc />
    public async Task<NoteRow> UpdateNoteBodyAsync(
        string ownerId, string noteId, string body, IDocumentSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(noteId);
        ArgumentNullException.ThrowIfNull(session);

        var note = await session.LoadAsync<PersonalNote>(noteId).ConfigureAwait(false);
        if (note is null || !string.Equals(note.OwnerId, ownerId, StringComparison.Ordinal))
            throw new KeyNotFoundException($"Note '{noteId}' not found for owner '{ownerId}'.");

        // D4 — the body is never mutated in place; edit is a new revision
        // and the Modified timestamp is the witness (the M14 / M16
        // author-only edit-lane shape).
        note.Body = body;
        note.Modified = DateTimeOffset.UtcNow;
        session.Store(note);

        var (title, link) =
            await ResolveForListAsync(note.TargetKind, note.TargetId, ownerId).ConfigureAwait(false);
        return ToNoteRow(note, title, link);
    }

    // ── DeleteNoteAsync (D7 / F4 — no target read) ────────────────────────

    /// <inheritdoc />
    public async Task<NoteDeleteStatus> DeleteNoteAsync(
        string ownerId, string noteId, IDocumentSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(noteId);
        ArgumentNullException.ThrowIfNull(session);

        // D7 / F4 — removing a note is keyed on the row's own id; it needs
        // no target read at all (a dangling row is removable).
        var note = await session.LoadAsync<PersonalNote>(noteId).ConfigureAwait(false);
        if (note is null || !string.Equals(note.OwnerId, ownerId, StringComparison.Ordinal))
            return NoteDeleteStatus.NotNoted;

        // D4 — physical removal (no IsDeleted to flip).
        session.Delete(note);
        return NoteDeleteStatus.Deleted;
    }

    // ── AddTagAsync (D5 tag normalization) ────────────────────────────────

    /// <inheritdoc />
    public async Task<NoteRow> AddTagAsync(
        string ownerId, string noteId, string tag, IDocumentSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(noteId);
        ArgumentNullException.ThrowIfNull(session);

        var note = await session.LoadAsync<PersonalNote>(noteId).ConfigureAwait(false);
        if (note is null || !string.Equals(note.OwnerId, ownerId, StringComparison.Ordinal))
            throw new KeyNotFoundException($"Note '{noteId}' not found for owner '{ownerId}'.");

        // D5 / C-M37·5 — normalize the tag (lowercase, trimmed, deduped,
        // sorted — the M26 Tag doc's Name normalization shape, the
        // idempotency witness).
        var normalized = NormalizeTag(tag);
        if (string.IsNullOrEmpty(normalized))
            throw new ArgumentException("A non-empty tag is required.", nameof(tag));

        var tags = (note.Tags ?? []).ToList();
        if (!tags.Contains(normalized, StringComparer.Ordinal))
        {
            tags.Add(normalized);
            tags.Sort(StringComparer.Ordinal);
            note.Tags = tags;
            session.Store(note);
        }

        var (title, link) =
            await ResolveForListAsync(note.TargetKind, note.TargetId, ownerId).ConfigureAwait(false);
        return ToNoteRow(note, title, link);
    }

    // ── RemoveTagAsync (D5 tag normalization) ─────────────────────────────

    /// <inheritdoc />
    public async Task<NoteRow> RemoveTagAsync(
        string ownerId, string noteId, string tag, IDocumentSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(noteId);
        ArgumentNullException.ThrowIfNull(session);

        var note = await session.LoadAsync<PersonalNote>(noteId).ConfigureAwait(false);
        if (note is null || !string.Equals(note.OwnerId, ownerId, StringComparison.Ordinal))
            throw new KeyNotFoundException($"Note '{noteId}' not found for owner '{ownerId}'.");

        // D5 / C-M37·5 — normalize before removal (the idempotency witness).
        var normalized = NormalizeTag(tag);
        var tags = (note.Tags ?? []).ToList();
        if (tags.Remove(normalized))
        {
            note.Tags = tags;
            session.Store(note);
        }

        var (title, link) =
            await ResolveForListAsync(note.TargetKind, note.TargetId, ownerId).ConfigureAwait(false);
        return ToNoteRow(note, title, link);
    }

    // ── HasNoteAsync (F1 witness, C-M37·2 zero-audit) ─────────────────────

    /// <inheritdoc />
    public async Task<bool> HasNoteAsync(string ownerId, string targetKind, string targetId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return false;
        if (!ClosedTargetKinds.Contains(targetKind))
            return false;

        // C-M37·2 — a pure existence check over the owner's own row, by the
        // identity predicate alone: no AccessAudit row, no CanAsync pass,
        // no target read (a dangling row still answers true — the button's
        // remove affordance must work even when the target is gone, D7 / F4).
        await using var q = _store.QuerySession();
        return await q.Query<PersonalNote>()
            .Where(n => n.OwnerId == ownerId && n.TargetKind == targetKind && n.TargetId == targetId)
            .AnyAsync()
            .ConfigureAwait(false);
    }

    // ── Folder-tree methods (U03 — stubbed) ───────────────────────────────
    //
    // U03 replaces these 4 stub bodies with the D6 implementations (the
    // depth ≤ 2 pin, the (OwnerId, ParentId, Name) unique index witness,
    // the folder-deletion-moves-notes-to-root pin). The interface is frozen
    // for U03–U05 (the M36/U03/U04 precedent of the interface declared once
    // and extended additively).

    /// <summary>U03 implements this (the D6 folder-tree seam).</summary>
    public Task<FolderRow> CreateFolderAsync(
        string ownerId, string name, string? parentId, IDocumentSession session)
        => throw new NotImplementedException("U03 implements this (the D6 folder-tree seam).");

    /// <summary>U03 implements this (the D6 folder-tree seam).</summary>
    public Task<FolderRow> RenameFolderAsync(
        string ownerId, string folderId, string newName, IDocumentSession session)
        => throw new NotImplementedException("U03 implements this (the D6 folder-tree seam).");

    /// <summary>U03 implements this (the D6 folder-tree seam).</summary>
    public Task<FolderDeleteResult> DeleteFolderAsync(
        string ownerId, string folderId, IDocumentSession session)
        => throw new NotImplementedException("U03 implements this (the D6 folder-tree seam).");

    /// <summary>U03 implements this (the D6 folder-tree seam).</summary>
    public Task<NoteRow> MoveNoteToFolderAsync(
        string ownerId, string noteId, string? folderId, IDocumentSession session)
        => throw new NotImplementedException("U03 implements this (the D6 folder-tree seam).");

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Constructs a <see cref="NoteRow"/> from a <see cref="PersonalNote"/>
    /// document + the resolved target (<c>title</c>, <c>link</c>).
    /// <see cref="NoteRow.Degraded"/> is <c>true</c> when both are
    /// <c>null</c> (the D7 degrade — the closed <c>pn.list.degraded</c>
    /// label; the body remains visible to the owner by D4).
    /// </summary>
    private static NoteRow ToNoteRow(PersonalNote note, string? title, string? link)
    {
        bool degraded = title is null && link is null;
        return new NoteRow(
            Id: note.Id,
            TargetKind: note.TargetKind,
            TargetId: note.TargetId,
            Body: note.Body,
            FolderId: note.FolderId,
            Tags: note.Tags ?? [],
            Created: note.Created,
            Modified: note.Modified,
            Degraded: degraded,
            Title: title,
            Link: link);
    }

    /// <summary>
    /// D5 / C-M37·5 — normalizes a tag (lowercase, trimmed — the M26
    /// <c>Tag</c> doc's <c>Name</c> normalization shape, the idempotency
    /// witness). Dedup and sort are applied on the collection in
    /// <see cref="AddTagAsync"/> / <see cref="RemoveTagAsync"/>.
    /// </summary>
    private static string NormalizeTag(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// The D7 degraded-resolution mechanism (design doc §2.4) — a **pure,
    /// non-auditing doc load** that commits <b>no</b>
    /// <see cref="Authorization.AccessAudit"/> row and makes <b>no</b>
    /// <see cref="IAuthorizationService.CanAsync"/> call (C-M37·2 /
    /// <c>F3_OwnerList_ZeroAuditRows_ZeroCanSeeAsyncPasses</c>, the ADR 0105
    /// "reads never audit" + M6-inbox personal-read shape). Both return
    /// <c>null</c> when the target is absent, soft-deleted, or a non-author
    /// draft (the D7 degrade — C-M37·5).
    /// <para>
    /// This is deliberately distinct from the D3 write-lane check
    /// (<see cref="ResolveTargetAsync"/>): the write lane re-runs the target's
    /// own frozen <c>CanAsync(Read)</c> (the detail page's exact decision),
    /// which commits the target's own audit row. That is correct and expected
    /// at note *creation* (C-M37·3), but would violate the list's zero-audit
    /// pin if reused here — so the list resolves by document shape alone
    /// (existence + not-deleted + draft-to-own), which is exactly the
    /// information a note row needs to render a <c>Title</c> and a <c>Link</c>.
    /// </para>
    /// </summary>
    private async Task<(string? Title, string? Link)> ResolveForListAsync(
        string targetKind, string targetId, string ownerId)
    {
        await using var s = _store.QuerySession();
        switch (targetKind)
        {
            case "post":
            {
                var post = await s.LoadAsync<Post>(targetId).ConfigureAwait(false);
                if (post is null || post.DeletedAt is not null)
                    return (null, null);
                // ADR 0037 — the author-only draft gate, as a pure ordinal
                // check (no CanAsync, no audit row).
                if (post.IsDraft && !string.Equals(post.AuthorId, ownerId, StringComparison.Ordinal))
                    return (null, null);
                return (post.Title, $"/posts/{targetId}");
            }

            case "grouppost":
            {
                var post = await s.LoadAsync<Post>(targetId).ConfigureAwait(false);
                if (post is null || post.DeletedAt is not null)
                    return (null, null);
                if (post.IsDraft && !string.Equals(post.AuthorId, ownerId, StringComparison.Ordinal))
                    return (null, null);
                return (post.Title, $"/groups/{post.GroupId}/posts/{targetId}");
            }

            case "event":
            {
                var ev = await s.LoadAsync<Event>(targetId).ConfigureAwait(false);
                if (ev is null || ev.IsDeleted)
                    return (null, null);
                if (ev.IsDraft && !string.Equals(ev.AuthorId, ownerId, StringComparison.Ordinal))
                    return (null, null);
                return (ev.Title, $"/events/{targetId}");
            }

            case "page":
            {
                var page = await s.LoadAsync<Page>(targetId).ConfigureAwait(false);
                if (page is null || page.IsDeleted)
                    return (null, null);
                if (page.IsDraft && !string.Equals(page.AuthorId, ownerId, StringComparison.Ordinal))
                    return (null, null);
                var path = await BuildPagePathAsync(s, page.Id).ConfigureAwait(false);
                return path is null ? (null, null) : (page.Title, path);
            }

            case "project":
            {
                var project = await s.LoadAsync<Project>(targetId).ConfigureAwait(false);
                if (project is null || project.IsDeleted)
                    return (null, null);
                return (project.Title, $"/projects/{targetId}");
            }

            case "todo":
            {
                var todo = await s.LoadAsync<TodoItem>(targetId).ConfigureAwait(false);
                if (todo is null || todo.IsDeleted)
                    return (null, null);
                return (todo.Title, $"/projects/todos/{targetId}");
            }

            case "board":
            {
                var board = await s.LoadAsync<KanbanBoard>(targetId).ConfigureAwait(false);
                if (board is null || board.IsDeleted)
                    return (null, null);
                return (board.Title, $"/projects/boards/{targetId}");
            }

            case "document":
            {
                var doc = await s.LoadAsync<Document>(targetId).ConfigureAwait(false);
                if (doc is null)
                    return (null, null);
                return (doc.Title, $"/documents/{targetId}");
            }

            case "directory":
            {
                var profile = await _userInfo.GetProfileAsync(targetId).ConfigureAwait(false);
                if (profile is null || profile.Blocked)
                    return (null, null);
                return (profile.DisplayName, $"/directory/{targetId}");
            }

            default:
                return (null, null);
        }
    }

    /// <summary>
    /// The D3 write-lane resolution (design doc §2.3) — the owning surface's
    /// own frozen read, re-run **the same way the target's own detail page
    /// reads it**. <c>title</c> / <c>link</c> are both <c>null</c> when the
    /// target is absent, soft-deleted, a non-author draft, or denied to the
    /// actor. Re-runs the target's own frozen <c>CanAsync(Read)</c> (committing
    /// the target's own audit row — C-M37·3); NOT used by the personal list
    /// read (C-M37·2).
    /// </summary>
    private async Task<(bool Visible, string? Title, string? Link)> ResolveTargetAsync(
        string targetKind, string targetId, string actorId)
    {
        switch (targetKind)
        {
            case "post":
            {
                await using var s = _store.QuerySession();
                var post = await s.LoadAsync<Post>(targetId).ConfigureAwait(false);
                if (post is null || post.DeletedAt is not null)
                    return (false, null, null);
                if (post.IsDraft && !string.Equals(post.AuthorId, actorId, StringComparison.Ordinal))
                    return (false, null, null);
                var decision = await _authz.CanAsync(
                    actorId, AccessAction.Read, new PostToAuditableResource(post))
                    .ConfigureAwait(false);
                if (!decision.Allowed)
                    return (false, null, null);
                return (true, post.Title, $"/posts/{targetId}");
            }

            case "grouppost":
            {
                await using var s = _store.QuerySession();
                var post = await s.LoadAsync<Post>(targetId).ConfigureAwait(false);
                if (post is null || post.DeletedAt is not null)
                    return (false, null, null);
                if (string.IsNullOrEmpty(post.GroupId))
                    return (false, null, null); // not a group post
                if (post.IsDraft && !string.Equals(post.AuthorId, actorId, StringComparison.Ordinal))
                    return (false, null, null);
                var decision = await _authz.CanSeeGroupAsync(actorId, post.GroupId, targetId)
                    .ConfigureAwait(false);
                if (!decision.Allowed)
                    return (false, null, null);
                return (true, post.Title, $"/groups/{post.GroupId}/posts/{targetId}");
            }

            case "event":
            {
                try
                {
                    var ev = await _events.GetAsync(targetId, actorId).ConfigureAwait(false);
                    return (true, ev.Title, $"/events/{targetId}");
                }
                catch (KeyNotFoundException)
                {
                    return (false, null, null);
                }
                catch (UnauthorizedAccessException)
                {
                    return (false, null, null);
                }
            }

            case "page":
            {
                await using var s = _store.QuerySession();
                var page = await s.LoadAsync<Page>(targetId).ConfigureAwait(false);
                if (page is null || page.IsDeleted)
                    return (false, null, null);
                if (page.IsDraft && !string.Equals(page.AuthorId, actorId, StringComparison.Ordinal))
                    return (false, null, null);
                var decision = await _authz.CanAsync(
                    actorId, AccessAction.Read, new PageToAuditableResource(page))
                    .ConfigureAwait(false);
                if (!decision.Allowed)
                    return (false, null, null);
                var path = await BuildPagePathAsync(s, page.Id).ConfigureAwait(false);
                return path is null ? (false, null, null) : (true, page.Title, path);
            }

            case "project":
            {
                try
                {
                    var project = await _projects.GetProjectAsync(targetId, actorId).ConfigureAwait(false);
                    return (true, project.Title, $"/projects/{targetId}");
                }
                catch (KeyNotFoundException)
                {
                    return (false, null, null);
                }
                catch (UnauthorizedAccessException)
                {
                    return (false, null, null);
                }
            }

            case "todo":
            {
                try
                {
                    var detail = await _projects.GetTodoAsync(targetId, actorId).ConfigureAwait(false);
                    return (true, detail.Todo.Title, $"/projects/todos/{targetId}");
                }
                catch (KeyNotFoundException)
                {
                    return (false, null, null);
                }
                catch (UnauthorizedAccessException)
                {
                    return (false, null, null);
                }
            }

            case "board":
            {
                try
                {
                    var detail = await _projects.GetBoardAsync(targetId, actorId).ConfigureAwait(false);
                    return (true, detail.Board.Title, $"/projects/boards/{targetId}");
                }
                catch (KeyNotFoundException)
                {
                    return (false, null, null);
                }
                catch (UnauthorizedAccessException)
                {
                    return (false, null, null);
                }
            }

            case "document":
            {
                await using var s = _store.QuerySession();
                var doc = await s.LoadAsync<Document>(targetId).ConfigureAwait(false);
                if (doc is null)
                    return (false, null, null);
                var decision = await _authz.CanAsync(
                    actorId, AccessAction.Read, new DocumentToAuditableResource(doc))
                    .ConfigureAwait(false);
                if (!decision.Allowed)
                    return (false, null, null);
                return (true, doc.Title, $"/documents/{targetId}");
            }

            case "directory":
            {
                var profile = await _userInfo.GetProfileAsync(targetId).ConfigureAwait(false);
                if (profile is null || profile.Blocked)
                    return (false, null, null);
                var decision = await _authz.CanAsync(
                    actorId, AccessAction.Read, new ProfileToAuditableResource(profile))
                    .ConfigureAwait(false);
                if (!decision.Allowed)
                    return (false, null, null);
                return (true, profile.DisplayName, $"/directory/{targetId}");
            }

            default:
                return (false, null, null);
        }
    }

    /// <summary>
    /// The <c>/pages/</c>-prefixed ancestor-slug chain of a page (the
    /// <see cref="PageService.GetByPathAsync"/> shape, walked over the store
    /// — <c>IPageService</c> exposes no by-id path lane, so the note surface
    /// derives the same derived path from the <see cref="Page.ParentId"/>
    /// chain directly). <c>null</c> on a broken chain.
    /// </summary>
    private async Task<string?> BuildPagePathAsync(Marten.IQuerySession s, string pageId)
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
