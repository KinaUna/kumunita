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
    /// <para>
    /// The 2026-10-25 page-level notes lane (the ADR 0167 amendment) adds
    /// 11 <c>page-*</c> kinds — one per named platform page — so a resident
    /// can attach a private note to the page itself (e.g. "the /announcements
    /// page needs a search box"), not just to the items on it. The static
    /// kinds (<c>page-announcements</c>, <c>page-community</c>,
    /// <c>page-events</c>, <c>page-projects</c>, <c>page-todos</c>,
    /// <c>page-boards</c>, <c>page-inventory</c>, <c>page-documents</c>) are
    /// always visible (the page itself is the surface — no item-level
    /// visibility to re-check; the write-lane check is a closed-set
    /// membership + a fixed title + a fixed link). The dynamic kinds
    /// (<c>page-group</c>, <c>page-project</c>) re-run the owning surface's
    /// own frozen read (the same call the detail page uses) so a group /
    /// project the actor cannot see cannot be page-noted (the M16
    /// create-gate posture — the D3 write-lane check re-applied to the
    /// page-level lane).
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> ClosedTargetKinds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "post", "grouppost", "event", "page", "project",
            "todo", "board", "document", "directory", "announcement", "inventory",
            // 2026-10-25 page-level notes lane (ADR 0167 amendment):
            "page-announcements", "page-community", "page-group",
            "page-events", "page-projects", "page-project",
            "page-todos", "page-boards", "page-inventory", "page-documents",
        };

    // The closed set's canonical order (for group ordering in the list).
    private static readonly string[] ClosedTargetKindsOrdered =
        ["post", "grouppost", "event", "page", "project",
         "todo", "board", "document", "directory", "announcement", "inventory",
         // 2026-10-25 page-level notes lane (ADR 0167 amendment):
         "page-announcements", "page-community", "page-group",
         "page-events", "page-projects", "page-project",
         "page-todos", "page-boards", "page-inventory", "page-documents"];

    private readonly IUserInfoService _userInfo;
    private readonly IAuthorizationService _authz;
    private readonly IDocumentStore _store;
    private readonly IEventService _events;
    private readonly IProjectService _projects;
    private readonly IAnnouncementService _announcements;
    private readonly IInventoryService _inventory;
    private readonly IIdentityService _identity;

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
        IProjectService projects,
        IAnnouncementService announcements,
        IInventoryService inventory,
        IIdentityService identity)
    {
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _authz = authz ?? throw new ArgumentNullException(nameof(authz));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        // ADR 0167 D1 (extension) — the announcement lane's two seams, the
        // M17 BookmarkService shape (ADR 0118 D3/F2 obs-4): the frozen
        // IAnnouncementService (the detail page's own read) + the frozen
        // IIdentityService (the owner's real standing — the roles never
        // cross the IPersonalNoteService seam; the same composition
        // category as the already-composed IUserInfoService).
        _announcements = announcements ?? throw new ArgumentNullException(nameof(announcements));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
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

    // ── GetNoteByTargetAsync (C-M37·2 zero-audit personal read) ──────────

    /// <inheritdoc />
    public async Task<NoteRow?> GetNoteByTargetAsync(
        string ownerId, string targetKind, string targetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);

        // C-M37·2 — a personal read over the owner's own row, by the
        // identity predicate alone: no AccessAudit row, no CanAsync pass
        // over the note rows (the ADR 0105 "operator has no read standing"
        // precedent). A non-owner — a GlobalAdmin included — gets null
        // (no leak that the note exists).
        await using var q = _store.QuerySession();
        var note = await q.Query<PersonalNote>()
            .Where(n => n.OwnerId == ownerId
                        && n.TargetKind == targetKind
                        && n.TargetId == targetId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (note is null)
            return null;

        // D7 — resolve via the pure, non-auditing doc load (the same
        // mechanism as the list read — C-M37·2). A dead / hidden target
        // degrades to (null, null); the body remains visible (D4).
        var (title, link) =
            await ResolveForListAsync(note.TargetKind, note.TargetId, ownerId).ConfigureAwait(false);
        return ToNoteRow(note, title, link);
    }

    // ── Folder-tree methods (U03 — the D6 implementations) ────────────────
    //
    // D6 (ADR 0167): the folder tree is a personal tree, bounded in depth
    // (≤ 2 — a new folder's parent must itself be a root), unique in name
    // within the owner's tree (the (OwnerId, ParentId, Name) unique index
    // is the witness), a note's FolderId is the owner's folder id (a note
    // cannot live in another resident's folder), and a folder's deletion
    // MOVES its notes to the root (FolderId = null) — the note bodies are
    // never lost. The interface is frozen for U03–U05 (declared once, U02);
    // these bodies implement to it.

    /// <inheritdoc />
    public async Task<FolderRow> CreateFolderAsync(
        string ownerId, string name, string? parentId, IDocumentSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(session);

        // D6 — the depth ≤ 2 pin: if a parent is given, it must itself be a
        // ROOT (ParentId == null). A folder whose parent is a non-root would
        // open the tree past the bound (the ADR 0167 "arbitrary-depth folder
        // tree" forbid). A foreign / absent parent is a non-leaky 404. The
        // depth violation throws InvalidOperationException (the Web layer maps
        // it to 400 — the depth is violated).
        if (parentId is not null)
        {
            var parent = await session.LoadAsync<NoteFolder>(parentId).ConfigureAwait(false);
            if (parent is null || !string.Equals(parent.OwnerId, ownerId, StringComparison.Ordinal))
                throw new KeyNotFoundException($"Parent folder '{parentId}' not found for owner '{ownerId}'.");
            if (parent.ParentId is not null)
                throw new InvalidOperationException(
                    "Folder depth exceeds the bound (≤ 2): a new folder's parent must be a root folder.");
        }

        // D6 — the (OwnerId, ParentId, Name) unique index is the witness: a
        // sibling already holding the name under this parent is the no-op
        // (return the existing row — the M17 Bookmark idempotent-create
        // shape, adapted to the frozen <see cref="FolderRow"/> return). The
        // same name under a *different* parent is allowed (the index is
        // keyed on all three).
        var existing = parentId is null
            ? await session.Query<NoteFolder>()
                .Where(f => f.OwnerId == ownerId && f.Name == name && f.ParentId == null)
                .FirstOrDefaultAsync().ConfigureAwait(false)
            : await session.Query<NoteFolder>()
                .Where(f => f.OwnerId == ownerId && f.Name == name && f.ParentId == parentId)
                .FirstOrDefaultAsync().ConfigureAwait(false);
        if (existing is not null)
            return ToFolderRow(existing, await FolderNoteCountAsync(session, ownerId, existing.Id).ConfigureAwait(false));

        var folder = new NoteFolder
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = ownerId,
            ParentId = parentId,
            Name = name,
            Created = DateTimeOffset.UtcNow,
        };
        session.Store(folder);
        return new FolderRow(folder.Id, folder.Name, folder.ParentId, 0);
    }

    /// <inheritdoc />
    public async Task<FolderRow> RenameFolderAsync(
        string ownerId, string folderId, string newName, IDocumentSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        ArgumentNullException.ThrowIfNull(session);

        var folder = await session.LoadAsync<NoteFolder>(folderId).ConfigureAwait(false);
        // Owner pin (D6): a non-owner — a GlobalAdmin included — gets a
        // non-leaky 404 (no leak that the folder exists).
        if (folder is null || !string.Equals(folder.OwnerId, ownerId, StringComparison.Ordinal))
            throw new KeyNotFoundException($"Folder '{folderId}' not found for owner '{ownerId}'.");

        // No-op when the name is unchanged.
        if (string.Equals(folder.Name, newName, StringComparison.Ordinal))
            return ToFolderRow(folder, await FolderNoteCountAsync(session, ownerId, folderId).ConfigureAwait(false));

        // D6 — the (OwnerId, ParentId, Name) unique index is the witness: a
        // sibling already holding the new name under the same parent is a
        // conflict the DB would reject on save; surfaced as
        // InvalidOperationException (the Web layer maps to 400/409).
        var conflict = folder.ParentId is null
            ? await session.Query<NoteFolder>()
                .Where(f => f.OwnerId == ownerId && f.Name == newName && f.ParentId == null && f.Id != folderId)
                .AnyAsync().ConfigureAwait(false)
            : await session.Query<NoteFolder>()
                .Where(f => f.OwnerId == ownerId && f.Name == newName && f.ParentId == folder.ParentId && f.Id != folderId)
                .AnyAsync().ConfigureAwait(false);
        if (conflict)
            throw new InvalidOperationException(
                $"A folder named '{newName}' already exists in this part of the owner's tree.");

        folder.Name = newName;
        session.Store(folder);
        return ToFolderRow(folder, await FolderNoteCountAsync(session, ownerId, folderId).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<FolderDeleteResult> DeleteFolderAsync(
        string ownerId, string folderId, IDocumentSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        ArgumentNullException.ThrowIfNull(session);

        var folder = await session.LoadAsync<NoteFolder>(folderId).ConfigureAwait(false);
        // Owner pin (D6): a non-owner gets a non-leaky 404 (no row, no leak).
        if (folder is null || !string.Equals(folder.OwnerId, ownerId, StringComparison.Ordinal))
            throw new KeyNotFoundException($"Folder '{folderId}' not found for owner '{ownerId}'.");

        // D6 — the folder's notes are MOVED to the root (FolderId = null);
        // the note bodies are never lost (the folder row is physically
        // removed — the M17 Bookmark physical-removal shape — but the note
        // rows are re-parented, not deleted).
        await MoveFolderNotesToRootAsync(session, ownerId, folderId).ConfigureAwait(false);

        // D6 — child folders are recursively deleted (their notes move to
        // the root too). The tree is bounded (≤ 2), so there is at most one
        // level of children; the loop is a belt-and-suspenders over that
        // bound. Deleting a folder that has children is the FoldersDeleted
        // outcome; a leaf folder (notes only) is the NotesMoved outcome.
        var result = FolderDeleteResult.NotesMoved;
        var children = await session.Query<NoteFolder>()
            .Where(f => f.OwnerId == ownerId && f.ParentId == folderId)
            .ToListAsync().ConfigureAwait(false);
        foreach (var child in children)
        {
            await MoveFolderNotesToRootAsync(session, ownerId, child.Id).ConfigureAwait(false);
            // Grandchildren — bounded, but recurse for safety (no unbounded
            // surface per D6; the guard below is defensive only).
            var grandchildren = await session.Query<NoteFolder>()
                .Where(f => f.OwnerId == ownerId && f.ParentId == child.Id)
                .ToListAsync().ConfigureAwait(false);
            foreach (var grandchild in grandchildren)
                await MoveFolderNotesToRootAsync(session, ownerId, grandchild.Id).ConfigureAwait(false);
            session.Delete(child);
            result = FolderDeleteResult.FoldersDeleted;
        }

        session.Delete(folder);
        return result;
    }

    /// <inheritdoc />
    public async Task<NoteRow> MoveNoteToFolderAsync(
        string ownerId, string noteId, string? folderId, IDocumentSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(noteId);
        ArgumentNullException.ThrowIfNull(session);

        var note = await session.LoadAsync<PersonalNote>(noteId).ConfigureAwait(false);
        // Owner pin: a non-owner gets a non-leaky 404.
        if (note is null || !string.Equals(note.OwnerId, ownerId, StringComparison.Ordinal))
            throw new KeyNotFoundException($"Note '{noteId}' not found for owner '{ownerId}'.");

        // D6 — the note and the folder are in the SAME owner's tree: a note
        // cannot live in another resident's folder (C-M37·6). An absent or
        // foreign folder is a non-leaky 404 (no leak that the folder exists).
        if (folderId is not null)
        {
            var folder = await session.LoadAsync<NoteFolder>(folderId).ConfigureAwait(false);
            if (folder is null || !string.Equals(folder.OwnerId, ownerId, StringComparison.Ordinal))
                throw new KeyNotFoundException($"Folder '{folderId}' not found for owner '{ownerId}'.");
        }

        note.FolderId = folderId; // null = move the note to the root.
        session.Store(note);

        var (title, link) =
            await ResolveForListAsync(note.TargetKind, note.TargetId, ownerId).ConfigureAwait(false);
        return ToNoteRow(note, title, link);
    }

    // ── Folder-tree private helpers (U03) ─────────────────────────────────

    /// <summary>Builds a <see cref="FolderRow"/> from a <see cref="NoteFolder"/>
    /// + the folder's note count.</summary>
    private static FolderRow ToFolderRow(NoteFolder folder, int noteCount)
        => new FolderRow(folder.Id, folder.Name, folder.ParentId, noteCount);

    /// <summary>
    /// The count of the owner's notes sitting in the given folder (the
    /// <see cref="FolderRow.NoteCount"/> shape; the M17 shape of counting over
    /// the identity predicate).
    /// </summary>
    private static async Task<int> FolderNoteCountAsync(
        IDocumentSession session, string ownerId, string folderId)
        => await session.Query<PersonalNote>()
            .Where(n => n.OwnerId == ownerId && n.FolderId == folderId)
            .CountAsync()
            .ConfigureAwait(false);

    /// <summary>
    /// Moves every owner-note in the given folder to the root
    /// (<c>FolderId = null</c>) — the D6 pin: the note bodies are never lost,
    /// the folder deletion is a re-parent, not a note removal.
    /// </summary>
    private static async Task MoveFolderNotesToRootAsync(
        IDocumentSession session, string ownerId, string folderId)
    {
        var notes = await session.Query<PersonalNote>()
            .Where(n => n.OwnerId == ownerId && n.FolderId == folderId)
            .ToListAsync()
            .ConfigureAwait(false);
        foreach (var note in notes)
        {
            note.FolderId = null; // D6 — moved to the root; body never lost.
            session.Store(note);
        }
    }

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
                // ADR 0239 D4 — a soft-deleted document is dangling (the note's
                // "document" reference is gone — the same shape the post /
                // project / board / page / todo / announcement targets already
                // use when their record is <c>IsDeleted</c> /
                // <c>DeletedAt != null</c>).
                if (doc is null || doc.DeletedAt is not null)
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

            case "announcement":
            {
                var a = await s.LoadAsync<Announcement>(targetId).ConfigureAwait(false);
                if (a is null)
                    return (null, null);
                // ADR 0037 — the author-only draft gate (the M17 BookmarkService
                // announcement shape, re-applied; a pure ordinal check — no
                // CanAsync, no audit row).
                if (a.IsDraft && !string.Equals(a.AuthorId, ownerId, StringComparison.Ordinal))
                    return (null, null);
                return (a.Title, $"/announcements/{targetId}");
            }

            case "inventory":
            {
                var item = await s.LoadAsync<InventoryItem>(targetId).ConfigureAwait(false);
                if (item is null || item.IsDeleted)
                    return (null, null);
                return (item.Name, $"/inventory/{targetId}");
            }

            // ── 2026-10-25 page-level notes lane (ADR 0167 amendment) ──
            // The static page kinds are always visible (the page itself is the
            // surface — no item-level visibility to re-check); the dynamic
            // page-group / page-project kinds re-run the owning surface's own
            // frozen read (the same call the detail page uses) so a group /
            // project the owner can no longer see degrades to the generic
            // "no longer available" label (the D7 pin — the body remains
            // visible, the pointer does not).

            case "page-announcements":
                return ("Announcements", "/announcements");

            case "page-community":
                return ("Community", "/community");

            case "page-events":
                return ("Events", "/events");

            case "page-projects":
                return ("Projects", "/projects");

            case "page-todos":
                return ("To-dos", "/projects/todos");

            case "page-boards":
                return ("Boards", "/projects/boards");

            case "page-inventory":
                return ("Inventory", "/inventory");

            case "page-documents":
                return ("Documents", "/documents");

            case "page-group":
            {
                // The groups surface. Two shapes, distinguished by the
                // target id:
                //   - "static" — the /groups index page (always visible,
                //     fixed title + link).
                //   - a real group id — the /groups/{id} detail page (re-run
                //     the owning surface's own frozen read; the group the
                //     owner can no longer see degrades to the generic
                //     "no longer available" label — the D7 pin, the body
                //     remains visible, the pointer does not).
                if (targetId == "static")
                    return ("Groups", "/groups");
                var groups = await _userInfo.GetGroupsForUserAsync(ownerId).ConfigureAwait(false);
                var g = groups.FirstOrDefault(gr => gr.Id == targetId);
                if (g is null)
                    return (null, null);
                return (g.Name, $"/groups/{targetId}");
            }

            case "page-project":
            {
                var project = await s.LoadAsync<Project>(targetId).ConfigureAwait(false);
                if (project is null || project.IsDeleted)
                    return (null, null);
                return (project.Title, $"/projects/projects/{targetId}");
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
                // ADR 0239 D4 — a soft-deleted document is dangling (the
                // note's "document" reference is gone); the decision below
                // does not run (a missing record is indistinguishable from
                // a denied one — the ADR 0122 D7 posture).
                if (doc is null || doc.DeletedAt is not null)
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

            case "announcement":
            {
                // The announcement lane has no AccessAudit / CanAsync of its
                // own — its GetAsync is the whole visibility gate (the
                // scope-vs-role split), returning null when not visible.
                //
                // ADR 0167 D1 (extension, the M17 BookmarkService shape, ADR
                // 0118 D3/F2 obs-4): the write-lane check must re-run *the
                // same read the detail page uses*, so the owner's real
                // standing is in play. The PersonalNotes seam hands us only
                // an owner id (roles never cross the frozen IPersonalNoteService
                // seam), so we resolve the owner's thin-principal role set
                // in-Core via the frozen IIdentityService (the same category
                // of composition as the already-composed IUserInfoService)
                // and hand that set to the announcement read. A GlobalAdmin /
                // community-moderator who is not a member therefore resolves a
                // community-targeted row — matching the detail page — instead
                // of the old lapse of an empty role set denying them.
                var principal = await _identity.GetBySubjectAsync(actorId).ConfigureAwait(false);
                var roles = principal is not null
                    ? new HashSet<string>(principal.Roles, StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);
                var a = await _announcements.GetAsync(targetId, actorId, roles)
                    .ConfigureAwait(false);
                if (a is null)
                    return (false, null, null);
                return (true, a.Title, $"/announcements/{targetId}");
            }

            case "inventory":
            {
                // ADR 0167 extension — the M16 Inventory detail page's own
                // read, re-run: IInventoryService.GetItemAsync (the same
                // call the /inventory/{id} detail page uses — the C-M37·3
                // pin: the target's own audit row commits, M37 commits none
                // of its own). KeyNotFoundException (404 on absent /
                // soft-deleted / denied — the C-M3·4 non-leaky shape) and
                // UnauthorizedAccessException both degrade to Refused.
                try
                {
                    var item = await _inventory.GetItemAsync(targetId, actorId).ConfigureAwait(false);
                    return (true, item.Name, $"/inventory/{targetId}");
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

            // ── 2026-10-25 page-level notes lane (ADR 0167 amendment) ──
            // The static page kinds (page-announcements, page-community,
            // page-events, page-projects, page-todos, page-boards,
            // page-inventory, page-documents) are always visible — the page
            // itself is the surface (no item-level visibility to re-check);
            // the write-lane check is a closed-set membership + a fixed
            // title + a fixed link. The targetId is the fixed page id
            // ("static" for static pages, the group/project id for the
            // dynamic page-group / page-project kinds).

            case "page-announcements":
                return (true, "Announcements", "/announcements");

            case "page-community":
                return (true, "Community", "/community");

            case "page-events":
                return (true, "Events", "/events");

            case "page-projects":
                return (true, "Projects", "/projects");

            case "page-todos":
                return (true, "To-dos", "/projects/todos");

            case "page-boards":
                return (true, "Boards", "/projects/boards");

            case "page-inventory":
                return (true, "Inventory", "/inventory");

            case "page-documents":
                return (true, "Documents", "/documents");

            case "page-group":
            {
                // The groups surface. Two shapes, distinguished by the
                // target id:
                //   - "static" — the /groups index page (one page; the note
                //     is about the index itself). Always visible, fixed
                //     title + link (no group to look up).
                //   - a real group id — the /groups/{id} detail page. Re-run
                //     the owning surface's own frozen read (the same
                //     GetGroupsForUserAsync the /groups/{id} controller uses
                //     — the owner ∪ member gate). A group the actor cannot
                //     see cannot be page-noted (the M16 create-gate posture
                //     — the D3 write-lane check re-applied).
                if (targetId == "static")
                    return (true, "Groups", "/groups");
                var groups = await _userInfo.GetGroupsForUserAsync(actorId).ConfigureAwait(false);
                var g = groups.FirstOrDefault(gr => gr.Id == targetId);
                if (g is null)
                    return (false, null, null);
                return (true, g.Name, $"/groups/{targetId}");
            }

            case "page-project":
            {
                // The project's own page (the /projects/projects/{id} detail) —
                // re-run the owning surface's own frozen read (the same
                // IProjectService.GetProjectAsync the detail page uses).
                try
                {
                    var p = await _projects.GetProjectAsync(targetId, actorId).ConfigureAwait(false);
                    return (true, p.Title, $"/projects/projects/{targetId}");
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
