using Kumunita.Core.Authorization;
using Kumunita.Core.Query;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;

namespace Kumunita.Core.Documents;

/// <summary>
/// The documents-side composition service (M21, ADR 0122; bounded context
/// <c>Kumunita.Core.Documents</c>, ADR 0006-D lane). The M21 analog of M3's
/// <see cref="Posts.PostService"/>. A pure caller of the two frozen modules —
/// <see cref="IUserInfoService"/> read seams and
/// <see cref="IAuthorizationService"/> (the single decision path) — plus its own
/// <see cref="IDocumentStore"/> for the read/write lanes; it never re-derives
/// access for its own decisions (the ADR 0006-D boundary).
/// <para>
/// Session shape (C-M21·4): reads open their own <c>QuerySession</c> (the
/// standalone <see cref="IAuthorizationService"/> overloads commit their own
/// aggregate / decision audit row); **writes go through the caller's
/// <c>IDocumentSession</c>** (<see cref="UploadAsync"/> — one
/// <c>SaveChangesAsync</c>, so the domain write commits atomically).
/// **Zero new authorization surface** (C-M21·2): the only
/// <see cref="IAuthorizationService"/> call sites are the
/// <c>Read</c>-path <c>CanSeeAsync</c> (feed) and <c>CanAsync</c> (detail) —
/// no new <c>AccessAction</c>, no new overload, no new seam on a frozen
/// interface.
/// </para>
/// <para>
/// **Organization lane (the "documents organization" feature):** the
/// <c>UploadAsync</c> / <c>UpdateAsync</c> write lanes accept a
/// <c>FolderId</c> (written verbatim onto <see cref="Document.FolderId"/>,
/// the ADR 0039 Pages <c>ParentId</c> forest carried to Documents) and
/// <c>TagSlugs</c> (the <c>TG</c>-lane slugs, resolved to <c>TagIds</c>
/// through the <see cref="ITagService.AttachToDocumentAsync"/> seam — the
/// <see cref="PostService.CreatePostAsync"/> tag-attach precedent). The
/// <see cref="ITagService"/> seam is **optional** (nullable default, the
/// CS1736 shape — the existing <see cref="DocumentServiceTests"/> call sites
/// that construct <see cref="DocumentService"/> positionally keep compiling
/// unchanged; they omit it ⇒ <c>null</c> ⇒ **no tag attach** on the lane).
/// The DI registration passes the live <see cref="ITagService"/>.
/// </para>
/// </summary>
public sealed class DocumentService
{
    private static readonly int PageSize = 30;

    private readonly IUserInfoService _userInfo;
    private readonly IAuthorizationService _authz;
    private readonly IDocumentStore _store;
    private readonly ITagService? _tags;

    public DocumentService(IUserInfoService userInfo, IAuthorizationService authz, IDocumentStore store,
        ITagService? tags = null)
    {
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _authz = authz ?? throw new ArgumentNullException(nameof(authz));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _tags = tags;
    }

    /// <summary>
    /// The community's document feed (M21, ADR 0122; design doc §5, D4): the
    /// candidate set is **all** documents (documents are community-level — D2
    /// <c>ComponentId => null</c>, so there is no component filter). One
    /// <see cref="IAuthorizationService.CanSeeAsync(string, AccessAction, IEnumerable{IAuditableResource})"/>
    /// over the paged candidate set (C6's one shared matching pass) writes the
    /// visit's **single aggregate** <see cref="AccessAudit"/> row (C-M21·3;
    /// <c>TargetKind = "document"</c> via the <see cref="DocumentToAuditableResource"/>
    /// adapter). <see cref="DocumentListResult.HiddenCount"/> counts only the
    /// candidates that call evaluated.
    /// </summary>
    public async Task<DocumentListResult> ListAsync(string actorId, int page, SortSpec? sort = null, int? pageSize = null)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));
        if (page < 1) page = 1;
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        await using var session = _store.QuerySession();
        IQueryable<Document> q = session
            .Query<Document>();
        // M26 U7 (design doc §2.2 row 12, closed allowlist
        // created/modified/title/size; size → the non-null SizeBytes long):
        // null keeps the pinned OrderByDescending(Created) byte-for-byte
        // (C-SORT·2); non-null applies the allowlist + the ThenBy(Id)
        // tie-breaker (C-SORT·5) via the shared MiscSortSupport helper.
        if (sort is null)
            q = q.OrderByDescending(d => d.Created); // ← the pinned line, verbatim
        else
            q = MiscSortSupport.OrderByMiscSort<Document, DateTimeOffset, DateTimeOffset?, long>(q, sort,
                new HashSet<string> { "created", "modified", "title", "size" },
                d => d.Created, d => d.Modified, d => d.Title,
                sizeKey: "size", d => d.SizeBytes, nameKey: "", d => string.Empty, d => d.Id);
        var candidates = await q
            .Skip((page - 1) * ps)
            .Take(ps)
            .ToListAsync()
            .ConfigureAwait(false);

        // The 0-candidate early return runs **before** any decision and
        // **before** the CountAsync: an oversized page is a no-decision,
        // Total: 0, HasMore: false (no audit row names anything).
        if (candidates.Count == 0)
            return new DocumentListResult(
                Visible: Array.Empty<Document>(), HiddenCount: 0, Page: page, Total: 0, HasMore: false);

        // Candidate-set count (pre-decision), one CountAsync over the same query.
        int candidateCount = await session
            .Query<Document>()
            .CountAsync()
            .ConfigureAwait(false);

        // C-M21·3 — one shared matching pass over the whole candidate set;
        // one aggregate audit row (<c>TargetKind = "document"</c>) from that
        // single call. Standalone form (no IDocumentSession overload): a plain
        // read with no in-flight caller transaction, so the standalone method's
        // own commit is the correct C-M21·4 lane.
        var visibleSet = await _authz
            .CanSeeAsync(
                actorId, AccessAction.Read,
                candidates.Select(d => new DocumentToAuditableResource(d)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        var visible = candidates.Where(d => visibleIds.Contains(d.Id)).ToList();

        return new DocumentListResult(
            Visible: visible, HiddenCount: visibleSet.HiddenCount,
            Page: page, Total: candidateCount, HasMore: candidates.Count == ps);
    }

    /// <summary>
    /// A document's detail (M21, ADR 0122; design doc §5, D2/D7):
    /// <see cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource)"/>
    /// — the document's **single decision row** (C-M21·4, not an aggregate).
    /// A missing document is fail-closed (no decision ran, no audit row — the
    /// M2 detail shape); a Deny returns <c>Document = null</c> (the decision's
    /// row <i>was</i> written, C-M21·4) for the Web layer's **404** (D7 — not
    /// 403; feed and detail agree on the deny posture).
    /// </summary>
    public async Task<DocumentDetailResult> GetAsync(string documentId, string actorId)
    {
        if (string.IsNullOrEmpty(documentId)) throw new ArgumentException("A document id is required.", nameof(documentId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));

        await using var session = _store.QuerySession();
        var doc = await session.LoadAsync<Document>(documentId).ConfigureAwait(false);
        if (doc is null)
            return new DocumentDetailResult(Document: null);

        var decision = await _authz
            .CanAsync(actorId, AccessAction.Read, new DocumentToAuditableResource(doc))
            .ConfigureAwait(false);

        return new DocumentDetailResult(Document: decision.Allowed ? doc : null);
    }

    /// <summary>
    /// The upload write lane (M21, ADR 0122; design doc §5, D3/D5): writes a new
    /// <see cref="Document"/> in the **caller's** <see cref="session"/> (one
    /// <c>SaveChangesAsync</c> — the domain write commits atomically).
    /// **Standing-agnostic** (D5): the upload right is gated at the **Web
    /// boundary** (U03 — the <c>GlobalAdmin ∪ Moderator</c> lane), not here;
    /// the Core service trusts the caller has already resolved standing and only
    /// records the <paramref name="actorId"/> as the owner. The bytes are
    /// already on the frozen ADR 0011 <c>IMediaStore</c>
    /// (<see cref="DocumentUpload.MediaId"/>) — this lane never touches them
    /// (D3). **No <c>AccessAudit</c> row** (§1.a A2 — the
    /// <see cref="Posts.PostService"/>.CreatePostAsync /
    /// <c>AttachmentController.Upload</c> write-lane convention: writes are
    /// authenticated, not audience-restricted reads).
    /// </summary>
    public async Task<Document> UploadAsync(DocumentUpload draft, string actorId, IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An authoring actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

        var doc = new Document
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = draft.Title,
            Summary = draft.Summary,
            MediaId = draft.MediaId,
            Filename = draft.Filename,
            ContentType = draft.ContentType,
            SizeBytes = draft.SizeBytes,
            Audience = draft.Audience,   // C-M21·1 — written verbatim; never mutated here.
            OwnerId = actorId,
            FolderId = draft.FolderId,   // the organization lane — written verbatim (the Web layer validated it).
            Created = DateTimeOffset.UtcNow,
            Modified = null
        };

        session.Store(doc);
        await session.SaveChangesAsync().ConfigureAwait(false);

        // TG lane (the "documents organization" feature) — resolve the typed
        // slugs to Tag ids through the AttachToDocumentAsync seam (the
        // PostService.CreatePostAsync tag-attach precedent). Only if the form
        // gave slugs (empty ⇒ no tags, the M3/M7 default-empty idiom). The
        // lane re-checks the standing (owner ∪ GlobalAdmin — here the actor
        // is the owner by construction) and create-or-reuses each tag
        // (C-TG·4); it stores the resolved Tag ids onto Document.TagIds. A
        // bad Slug is an ArgumentException (the Web layer maps it to a form
        // error — the M3 "a form is a shape" precedent).
        if (draft.TagSlugs is { Count: > 0 } && _tags is not null)
        {
            var actorRoles = new HashSet<string>(0); // empty — the owner-match short-circuits the standing probe
            var resolved = await _tags.AttachToDocumentAsync(doc.Id, draft.TagSlugs, actorId, actorRoles, session)
                .ConfigureAwait(false);
            doc.TagIds = resolved.Select(t => t.Id).ToList();
            session.Store(doc);
            await session.SaveChangesAsync().ConfigureAwait(false);
        }

        return doc;
    }

    /// <summary>
    /// The owner-only edit write lane (ADR 0125, D1/D2/D3): the <b>owner</b>
    /// (the uploader — <see cref="Document.OwnerId"/>) of a document may re-choose
    /// <b>who can access it</b> (<see cref="DocumentEdit.Audience"/>, written
    /// verbatim — C-M21·1) and <b>replace the file</b> (<see
    /// cref="DocumentEdit.MediaId"/> + <see cref="DocumentEdit.FileReplaced"/> —
    /// ADR 0125 D3: an empty file is a no-op on the byte surface). The Core lane
    /// is the **sole real gate**: it loads the stored row first (the missing-id
    /// → <see cref="KeyNotFoundException"/> contract is preserved and the stored
    /// <c>OwnerId</c> is in hand), then re-checks <c>OwnerId == actorId</c>
    /// before any write — a non-owner is a hard
    /// <see cref="UnauthorizedAccessException"/> (the Web boundary 404s; the ADR
    /// 0122 D7 posture: the form's existence is not leaked to a non-owner).
    /// <para>
    /// **Ownership is immutable** (ADR 0014/0016/0017 precedent): the stored
    /// <c>OwnerId</c> is preserved and never re-assigned — editing does not
    /// transfer the document to another owner. <c>Created</c> is preserved;
    /// <c>Modified</c> is stamped (the ADR 0122 D1 "set on a future replace"
    /// field, now exercised). **No <c>AccessAudit</c> row** (ADR 0122 §1.a A2 —
    /// the write is authenticated, not an audience-restricted read).
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The document id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor is not the document's owner.</exception>
    public async Task<Document> UpdateAsync(string documentId, DocumentEdit edit, string actorId, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(documentId)) throw new ArgumentException("A document id is required.", nameof(documentId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting owner is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(session);

        var doc = await session.LoadAsync<Document>(documentId).ConfigureAwait(false);
        if (doc is null)
            throw new KeyNotFoundException($"Document '{documentId}' was not found in the session; nothing to edit.");

        // Owner-only gate (the sole decision on this lane — ADR 0125 D1): only
        // the owner may edit. A non-owner (including a non-owner GlobalAdmin —
        // the ADR 0125 D1 "owner" reading is the uploader, not an elevated
        // standing) throws before anything is written.
        if (!string.Equals(doc.OwnerId, actorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Only the owner of a document may edit it.");

        // The two named edit fields (ADR 0125): the audience (the access control
        // — written verbatim, C-M21·1) and the file (the replacement — D3). The
        // title/summary are the owner's labels, editable alongside them (D2).
        doc.Audience = edit.Audience;
        doc.Title = edit.Title;
        doc.Summary = edit.Summary;
        // Organization lane (the "documents organization" feature): the folder
        // is written verbatim (the Web layer resolved it to a validated id; a
        // null = "Unfiled" the root). The owner-only gate above already
        // passed, so the write is authorized.
        doc.FolderId = edit.FolderId;
        if (edit.FileReplaced)
        {
            doc.MediaId = edit.MediaId;
            doc.Filename = edit.Filename;
            doc.ContentType = edit.ContentType;
            doc.SizeBytes = edit.SizeBytes;
        }
        doc.Modified = DateTimeOffset.UtcNow;

        session.Store(doc);
        await session.SaveChangesAsync().ConfigureAwait(false);

        // TG lane (the "documents organization" feature) — resolve the typed
        // slugs to Tag ids through the AttachToDocumentAsync seam (the
        // PostService.UpdatePostAsync tag-attach precedent). The tri-state
        // (the U8b register patch's detach semantics): <c>null</c> ⇒ leave
        // the document's existing tags (the <c>PostService.UpdatePostAsync</c>
        // "leave existing" shape — the optional trailing param); a **present**
        // field (even an empty <c>[]</c> when the owner removed every chip) is
        // authoritative ⇒ empty detaches all, non-empty attaches (the
        // AttachToDocumentAsync lane's standing probe is the authoritative
        // gate — it throws UnauthorizedAccessException before anything is
        // written if the actor lacks standing; here the owner-only gate
        // already passed, so it short-circuits on the owner match).
        if (edit.TagSlugs is not null && _tags is not null)
        {
            var actorRoles = new HashSet<string>(0); // empty — the owner-match short-circuits the standing probe
            var resolved = await _tags.AttachToDocumentAsync(
                    documentId, edit.TagSlugs, actorId, actorRoles, session)
                .ConfigureAwait(false);
            doc.TagIds = resolved.Select(t => t.Id).ToList();
            session.Store(doc);
            await session.SaveChangesAsync().ConfigureAwait(false);
        }

        return doc;
    }
}
