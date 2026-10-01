using Kumunita.Core.Authorization;
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
/// </summary>
public sealed class DocumentService
{
    private static readonly int PageSize = 30;

    private readonly IUserInfoService _userInfo;
    private readonly IAuthorizationService _authz;
    private readonly IDocumentStore _store;

    public DocumentService(IUserInfoService userInfo, IAuthorizationService authz, IDocumentStore store)
    {
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _authz = authz ?? throw new ArgumentNullException(nameof(authz));
        _store = store ?? throw new ArgumentNullException(nameof(store));
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
    public async Task<DocumentListResult> ListAsync(string actorId, int page)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        var candidates = await session
            .Query<Document>()
            .OrderByDescending(d => d.Created)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
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
            Page: page, Total: candidateCount, HasMore: candidates.Count == PageSize);
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
            Created = DateTimeOffset.UtcNow,
            Modified = null
        };

        session.Store(doc);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return doc;
    }
}
