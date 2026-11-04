using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Documents;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Inventory;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.Targets;
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

    private readonly IDocumentStore _store;
    private readonly IContentTargetResolver _resolver;

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
        IDocumentStore store,
        IContentTargetResolver resolver)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
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
            await _resolver.ResolveAsync(targetKind, targetId, ownerId, ContentTargetKinds.PersonalNotes).ConfigureAwait(false);
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

    /// <summary>
    /// The committed form of <see cref="CreateNoteAsync(string, string, string,
    /// string, IDocumentSession)"/> (IMPROVE-CODE C09 — the Web seam no
    /// longer opens a Marten session): the row commits in this service's own
    /// fresh <c>IDocumentSession</c> before the return. The visibility table
    /// and the <see cref="KeyNotFoundException"/> contract are unchanged.
    /// </summary>
    /// <inheritdoc />
    public async Task<NoteRow> CreateNoteAsync(
        string ownerId, string targetKind, string targetId, string body)
    {
        await using var session = _store.LightweightSession();
        var row = await CreateNoteAsync(ownerId, targetKind, targetId, body, session).ConfigureAwait(false);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
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
            await _resolver.ResolveForListAsync(note.TargetKind, note.TargetId, ownerId, ContentTargetKinds.PersonalNotes).ConfigureAwait(false);
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
                await _resolver.ResolveForListAsync(note.TargetKind, note.TargetId, ownerId, ContentTargetKinds.PersonalNotes).ConfigureAwait(false);
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
            await _resolver.ResolveForListAsync(note.TargetKind, note.TargetId, ownerId, ContentTargetKinds.PersonalNotes).ConfigureAwait(false);
        return ToNoteRow(note, title, link);
    }

    /// <summary>
    /// The committed form of <see cref="UpdateNoteBodyAsync(string, string,
    /// string, IDocumentSession)"/> (IMPROVE-CODE C09 — same committed-write
    /// shape as <see cref="CreateNoteAsync(string, string, string, string)"/>).
    /// </summary>
    /// <inheritdoc />
    public async Task<NoteRow> UpdateNoteBodyAsync(
        string ownerId, string noteId, string body)
    {
        await using var session = _store.LightweightSession();
        var row = await UpdateNoteBodyAsync(ownerId, noteId, body, session).ConfigureAwait(false);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
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

    /// <summary>
    /// The committed form of <see cref="DeleteNoteAsync(string, string,
    /// IDocumentSession)"/> (IMPROVE-CODE C09 — same committed-write shape as
    /// <see cref="CreateNoteAsync(string, string, string, string)"/>).
    /// </summary>
    /// <inheritdoc />
    public async Task<NoteDeleteStatus> DeleteNoteAsync(string ownerId, string noteId)
    {
        await using var session = _store.LightweightSession();
        var status = await DeleteNoteAsync(ownerId, noteId, session).ConfigureAwait(false);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return status;
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
            await _resolver.ResolveForListAsync(note.TargetKind, note.TargetId, ownerId, ContentTargetKinds.PersonalNotes).ConfigureAwait(false);
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
            await _resolver.ResolveForListAsync(note.TargetKind, note.TargetId, ownerId, ContentTargetKinds.PersonalNotes).ConfigureAwait(false);
        return ToNoteRow(note, title, link);
    }

    /// <summary>
    /// The committed form of <see cref="AddTagAsync(string, string, string,
    /// IDocumentSession)"/> (IMPROVE-CODE C09 — same committed-write shape as
    /// <see cref="CreateNoteAsync(string, string, string, string)"/>).
    /// </summary>
    /// <inheritdoc />
    public async Task<NoteRow> AddTagAsync(string ownerId, string noteId, string tag)
    {
        await using var session = _store.LightweightSession();
        var row = await AddTagAsync(ownerId, noteId, tag, session).ConfigureAwait(false);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
    }

    /// <summary>
    /// The committed form of <see cref="RemoveTagAsync(string, string,
    /// string, IDocumentSession)"/> (IMPROVE-CODE C09 — same committed-write
    /// shape as <see cref="CreateNoteAsync(string, string, string, string)"/>).
    /// </summary>
    /// <inheritdoc />
    public async Task<NoteRow> RemoveTagAsync(string ownerId, string noteId, string tag)
    {
        await using var session = _store.LightweightSession();
        var row = await RemoveTagAsync(ownerId, noteId, tag, session).ConfigureAwait(false);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
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
            await _resolver.ResolveForListAsync(note.TargetKind, note.TargetId, ownerId, ContentTargetKinds.PersonalNotes).ConfigureAwait(false);
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
            await _resolver.ResolveForListAsync(note.TargetKind, note.TargetId, ownerId, ContentTargetKinds.PersonalNotes).ConfigureAwait(false);
        return ToNoteRow(note, title, link);
    }

    /// <summary>
    /// The committed form of <see cref="CreateFolderAsync(string, string,
    /// string, IDocumentSession)"/> (IMPROVE-CODE C09 — same committed-write
    /// shape as <see cref="CreateNoteAsync(string, string, string, string)"/>).
    /// </summary>
    /// <inheritdoc />
    public async Task<FolderRow> CreateFolderAsync(string ownerId, string name, string? parentId)
    {
        await using var session = _store.LightweightSession();
        var row = await CreateFolderAsync(ownerId, name, parentId, session).ConfigureAwait(false);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
    }

    /// <summary>
    /// The committed form of <see cref="RenameFolderAsync(string, string,
    /// string, IDocumentSession)"/> (IMPROVE-CODE C09 — same committed-write
    /// shape as <see cref="CreateNoteAsync(string, string, string, string)"/>).
    /// </summary>
    /// <inheritdoc />
    public async Task<FolderRow> RenameFolderAsync(string ownerId, string folderId, string newName)
    {
        await using var session = _store.LightweightSession();
        var row = await RenameFolderAsync(ownerId, folderId, newName, session).ConfigureAwait(false);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
    }

    /// <summary>
    /// The committed form of <see cref="DeleteFolderAsync(string, string,
    /// IDocumentSession)"/> (IMPROVE-CODE C09 — same committed-write shape as
    /// <see cref="CreateNoteAsync(string, string, string, string)"/>).
    /// </summary>
    /// <inheritdoc />
    public async Task<FolderDeleteResult> DeleteFolderAsync(string ownerId, string folderId)
    {
        await using var session = _store.LightweightSession();
        var result = await DeleteFolderAsync(ownerId, folderId, session).ConfigureAwait(false);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// The committed form of <see cref="MoveNoteToFolderAsync(string, string,
    /// string, IDocumentSession)"/> (IMPROVE-CODE C09 — same committed-write
    /// shape as <see cref="CreateNoteAsync(string, string, string, string)"/>).
    /// </summary>
    /// <inheritdoc />
    public async Task<NoteRow> MoveNoteToFolderAsync(string ownerId, string noteId, string? folderId)
    {
        await using var session = _store.LightweightSession();
        var row = await MoveNoteToFolderAsync(ownerId, noteId, folderId, session).ConfigureAwait(false);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
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


}
