using Kumunita.Core.Identity;
using Marten;

namespace Kumunita.Core.Documents;

/// <summary>
/// The "documents organization" lane's write service (bounded context
/// <c>Kumunita.Core.Documents</c>, the ADR 0006-D lane pin): the folder CRUD
/// + document-move seams. Composes **only** the host-registered
/// <see cref="IDocumentStore"/> (the write lanes' C3 audit rows + the read
/// lane's <c>ListByParentAsync</c>) — never opens a new seam on a frozen
/// interface, never re-derives access for its own decisions (the ADR 0006-D
/// boundary — the access is the frozen
/// <see cref="Kumunita.Core.Authorization.IAuthorizationService"/> Read
/// decision, which the Web layer has already applied before calling here).
/// <para>
/// <b>Standing (the ADR 0125 owner-reading carried to folders, ∪ the ADR 0044
/// elevated standing):</b> the <c>OwnerId</c> (the creator) ∪ <c>GlobalAdmin</c>
/// may rename, move, or delete a folder. A <c>Moderator</c> (or other role)
/// holds no folder standing — the same elevated-standing pattern as the
/// <see cref="Tags.TagService"/> translate lane. The standing probe
/// (<see cref="CanManageFolder"/>) is a display pin (the Web renders the
/// affordance); the real deny is the write-lane re-check.
/// </para>
/// <para>
/// <b>Delete semantics:</b> a folder with any documents or child folders
/// cannot be deleted (an <see cref="InvalidOperationException"/> — the Web
/// layer maps it to a form error, the M3 "a form is a shape" precedent); a
/// leaf empty folder is deleted outright (the ADR 0024 soft-delete shape is
/// not used — a folder is an organizational aid, not a content artifact; the
/// ADR 0125 owner-only edit lane precedent: the owner's intent is absolute).
/// </para>
/// </summary>
public sealed class DocumentFolderService
{
    private readonly IDocumentStore _store;
    public DocumentFolderService(IDocumentStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// The standing probe (a display pin, not a gate — the Web renders the
    /// rename / move / delete affordance; the real deny is the write-lane
    /// re-check below). True iff the actor is the folder's
    /// <see cref="DocumentFolder.OwnerId"/> (the creator) or a
    /// <see cref="Roles.GlobalAdmin"/>.
    /// </summary>
    public bool CanManageFolder(DocumentFolder folder, string actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(roles);
        return string.Equals(folder.OwnerId, actorId, StringComparison.Ordinal)
               || roles.Contains(Roles.GlobalAdmin);
    }

    /// <summary>
    /// Create a new folder (a root if <paramref name="parentId"/> is
    /// <c>null</c>, a child otherwise). Standing: the Web layer has already
    /// gated the actor to <c>GlobalAdmin ∪ Moderator</c> (the same lane as
    /// the <see cref="DocumentService.UploadAsync"/> upload right, the
    /// ADR 0122 D5 precedent). Returns the new folder. One
    /// <see cref="AccessAudit"/> row (<c>documentfolder.create</c>) is
    /// written in the caller's session (C3 — the write and the audit commit
    /// or roll back atomically, the ADR 0006-D C3 idiom).
    /// <para>
    /// Shape guards: <paramref name="name"/> must be non-blank (the Web
    /// layer maps a blank to a form error); <paramref name="parentId"/>
    /// (if present) must reference an existing folder (a missing parent is a
    /// <see cref="KeyNotFoundException"/> — the Web layer maps it to a 404,
    /// the ADR 0122 D7 posture).
    /// </para>
    /// </summary>
    public async Task<DocumentFolder> CreateAsync(
        string name, string? parentId, string actorId, IDocumentSession session)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A folder name is required.", nameof(name));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

        if (parentId is not null)
        {
            var parent = await session.LoadAsync<DocumentFolder>(parentId).ConfigureAwait(false);
            if (parent is null)
                throw new KeyNotFoundException($"Parent folder '{parentId}' was not found.");
        }

        var folder = new DocumentFolder
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name.Trim(),
            ParentId = parentId,
            OwnerId = actorId,
            Created = DateTimeOffset.UtcNow,
            Modified = null,
        };

        var now = DateTimeOffset.UtcNow;
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "documentfolder.create",
            TargetKind = "documentfolder",
            TargetId = folder.Id,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow,
        };

        session.Store(folder);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return folder;
    }

    /// <summary>
    /// List the folders under a parent (the <c>null</c> parent = the roots —
    /// the ADR 0039 Pages <c>ParentId</c> forest shape). Ordered by name
    /// (case-insensitive ordinal, the <c>DocumentService.ListAsync</c>
    /// <c>OrderBy</c> idiom — a folder's display name is the sort key, not
    /// the creation time). No <see cref="Authorization.AccessAudit"/> row
    /// (the folder is a label, never a gate — the Web layer's Read decision
    /// already ran for the document feed; a plain folder listing is a plain
    /// read, the <see cref="Tags.ITagService.ListForActorAsync"/>
    /// "no audit row on a plain read" shape).
    /// </summary>
    public async Task<IReadOnlyList<DocumentFolder>> ListByParentAsync(string? parentId)
    {
        await using var session = _store.QuerySession();
        if (parentId is null)
        {
            return await session
                .Query<DocumentFolder>()
                .Where(f => f.ParentId == null)
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        return await session
            .Query<DocumentFolder>()
            .Where(f => f.ParentId == parentId)
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Load a folder by id. <c>null</c> = the id does not exist (the Web
    /// layer maps it to a 404, the ADR 0122 D7 posture).
    /// </summary>
    public async Task<DocumentFolder?> GetAsync(string folderId)
    {
        if (string.IsNullOrEmpty(folderId))
            throw new ArgumentException("A folder id is required.", nameof(folderId));
        await using var session = _store.QuerySession();
        return await session.LoadAsync<DocumentFolder>(folderId).ConfigureAwait(false);
    }

    /// <summary>
    /// Move a document to a different folder (the <c>null</c>
    /// <paramref name="newFolderId"/> = "Unfiled" — the root). Standing:
    /// the Web layer has already gated the actor to the document's owner
    /// (the ADR 0125 owner-reading — the same standing that governs the
    /// edit lane; the folder's standing is **not** re-checked here because
    /// a document's owner is the actor who can re-file it, not the folder's
    /// owner). The Core lane is the **sole real gate**: it loads the stored
    /// row first (the missing-id → <see cref="KeyNotFoundException"/>
    /// contract is preserved) and writes <c>FolderId</c> verbatim. One
    /// <see cref="AccessAudit"/> row (<c>document.move</c>).
    /// <para>
    /// A <paramref name="newFolderId"/> that does not reference an existing
    /// folder is a <see cref="KeyNotFoundException"/> (the Web layer maps it
    /// to a 404 — the ADR 0122 D7 posture).
    /// </para>
    /// </summary>
    public async Task<Document> MoveDocumentAsync(
        string documentId, string? newFolderId, string actorId, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(documentId))
            throw new ArgumentException("A document id is required.", nameof(documentId));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting owner is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

        var doc = await session.LoadAsync<Document>(documentId).ConfigureAwait(false);
        if (doc is null)
            throw new KeyNotFoundException($"Document '{documentId}' was not found.");

        // The owner-only gate (the ADR 0125 owner-reading — the same
        // standing that governs the edit lane): only the owner may re-file.
        if (!string.Equals(doc.OwnerId, actorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Only the owner of a document may move it.");

        if (newFolderId is not null)
        {
            var folder = await session.LoadAsync<DocumentFolder>(newFolderId).ConfigureAwait(false);
            if (folder is null)
                throw new KeyNotFoundException($"Folder '{newFolderId}' was not found.");
        }

        doc.FolderId = newFolderId;
        doc.Modified = DateTimeOffset.UtcNow;

        var now = DateTimeOffset.UtcNow;
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "document.move",
            TargetKind = "document",
            TargetId = documentId,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow,
        };

        session.Store(doc);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return doc;
    }

    /// <summary>
    /// Rename a folder (the <see cref="DocumentFolder.Name"/> display label).
    /// Standing: the folder's <see cref="DocumentFolder.OwnerId"/> ∪
    /// <see cref="Roles.GlobalAdmin"/> (the ADR 0125 owner-reading ∪ the
    /// ADR 0044 elevated standing — the same pattern as the
    /// <see cref="Tags.TagService"/> translate lane). The name must be
    /// non-blank (the Web layer maps a blank to a form error). One
    /// <see cref="AccessAudit"/> row (<c>documentfolder.rename</c>).
    /// </summary>
    public async Task<DocumentFolder> RenameAsync(
        string folderId, string name, string actorId, IReadOnlySet<string> roles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(folderId))
            throw new ArgumentException("A folder id is required.", nameof(folderId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A folder name is required.", nameof(name));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(session);

        var folder = await session.LoadAsync<DocumentFolder>(folderId).ConfigureAwait(false);
        if (folder is null)
            throw new KeyNotFoundException($"Folder '{folderId}' was not found.");

        if (!CanManageFolder(folder, actorId, roles))
            throw new UnauthorizedAccessException(
                "Only the folder's owner or a GlobalAdmin may rename it.");

        folder.Name = name.Trim();
        folder.Modified = DateTimeOffset.UtcNow;

        var now = DateTimeOffset.UtcNow;
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "documentfolder.rename",
            TargetKind = "documentfolder",
            TargetId = folderId,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow,
        };

        session.Store(folder);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return folder;
    }

    /// <summary>
    /// Move a folder to a different parent (the <c>null</c>
    /// <paramref name="newParentId"/> = promote to a root). Standing: the
    /// folder's <see cref="DocumentFolder.OwnerId"/> ∪
    /// <see cref="Roles.GlobalAdmin"/>. A <paramref name="newParentId"/>
    /// that does not reference an existing folder is a
    /// <see cref="KeyNotFoundException"/>. A move that would create a cycle
    /// (the new parent is the folder itself, or a descendant of the folder)
    /// is an <see cref="ArgumentException"/> (the write-lane guard — the
    /// ADR 0044 tag <c>DeriveSlug</c> shape). One
    /// <see cref="AccessAudit"/> row (<c>documentfolder.move</c>).
    /// </summary>
    public async Task<DocumentFolder> MoveAsync(
        string folderId, string? newParentId, string actorId, IReadOnlySet<string> roles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(folderId))
            throw new ArgumentException("A folder id is required.", nameof(folderId));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(session);

        var folder = await session.LoadAsync<DocumentFolder>(folderId).ConfigureAwait(false);
        if (folder is null)
            throw new KeyNotFoundException($"Folder '{folderId}' was not found.");

        if (!CanManageFolder(folder, actorId, roles))
            throw new UnauthorizedAccessException(
                "Only the folder's owner or a GlobalAdmin may move it.");

        if (newParentId is not null)
        {
            if (string.Equals(newParentId, folderId, StringComparison.Ordinal))
                throw new ArgumentException(
                    "A folder cannot be its own parent.", nameof(newParentId));

            var newParent = await session.LoadAsync<DocumentFolder>(newParentId).ConfigureAwait(false);
            if (newParent is null)
                throw new KeyNotFoundException($"Parent folder '{newParentId}' was not found.");

            // Cycle guard: walk up from the new parent; if we reach the
            // folder itself, the move would create a cycle.
            var cursor = newParent;
            var hops = 0;
            const int MaxHops = 64; // a forest this deep is a shape violation; bail out
            while (cursor.ParentId is not null && hops < MaxHops)
            {
                if (string.Equals(cursor.ParentId, folderId, StringComparison.Ordinal))
                    throw new ArgumentException(
                        "A folder cannot be moved under one of its own descendants.", nameof(newParentId));
                cursor = await session.LoadAsync<DocumentFolder>(cursor.ParentId).ConfigureAwait(false);
                if (cursor is null) break;
                hops++;
            }
        }

        folder.ParentId = newParentId;
        folder.Modified = DateTimeOffset.UtcNow;

        var now = DateTimeOffset.UtcNow;
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "documentfolder.move",
            TargetKind = "documentfolder",
            TargetId = folderId,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow,
        };

        session.Store(folder);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return folder;
    }

    /// <summary>
    /// Delete a folder (the ADR 0125 owner-only standing ∪ the ADR 0044
    /// elevated standing). A folder with any documents or child folders
    /// cannot be deleted (an <see cref="InvalidOperationException"/> — the
    /// Web layer maps it to a form error, the M3 "a form is a shape"
    /// precedent; the <c>DocumentService</c> has no delete lane — documents
    /// are content artifacts, folders are organizational aids, and the
    /// ADR 0024 soft-delete shape is not used here). A leaf empty folder is
    /// deleted outright (the <c>session.Delete</c> call — a hard delete, the
    /// ADR 0125 "the owner's intent is absolute" shape). One
    /// <see cref="AccessAudit"/> row (<c>documentfolder.delete</c>).
    /// </summary>
    public async Task DeleteAsync(
        string folderId, string actorId, IReadOnlySet<string> roles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(folderId))
            throw new ArgumentException("A folder id is required.", nameof(folderId));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(session);

        var folder = await session.LoadAsync<DocumentFolder>(folderId).ConfigureAwait(false);
        if (folder is null)
            throw new KeyNotFoundException($"Folder '{folderId}' was not found.");

        if (!CanManageFolder(folder, actorId, roles))
            throw new UnauthorizedAccessException(
                "Only the folder's owner or a GlobalAdmin may delete it.");

        // Shape guard: a folder with documents or child folders cannot be
        // deleted (the Web layer's form error, the M3 "a form is a shape"
        // precedent — the ADR 0024 soft-delete shape is not used here).
        var childCount = await session
            .Query<DocumentFolder>()
            .CountAsync(f => f.ParentId == folderId).ConfigureAwait(false);
        if (childCount > 0)
            throw new InvalidOperationException(
                "A folder with subfolders cannot be deleted; move or delete the subfolders first.");

        var docCount = await session
            .Query<Document>()
            .CountAsync(d => d.FolderId == folderId).ConfigureAwait(false);
        if (docCount > 0)
            throw new InvalidOperationException(
                "A folder with documents cannot be deleted; move the documents to another folder first.");

        var now = DateTimeOffset.UtcNow;
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "documentfolder.delete",
            TargetKind = "documentfolder",
            TargetId = folderId,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow,
        };

        session.Delete(folder);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }
}
