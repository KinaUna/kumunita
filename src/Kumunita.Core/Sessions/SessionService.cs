using Kumunita.Core.Authorization;
using Marten;

namespace Kumunita.Core.Sessions;

/// <summary>
/// The sessions lane's (per-session list + revoke one device; the F5 "Sign out
/// everywhere" whole-account kill is unchanged and coexists — see
/// <c>docs/design/sessions-per-session-revocation.md</c>) single composition
/// service. A pure caller of its own <see cref="IDocumentStore"/> for the
/// read/write lanes (the M3 <see cref="Posts.PostService"/> pattern — the
/// controller is route + authz + shape, never a re-derivation).
/// <para>
/// Session shape (invariant C3 — same transaction): writes open their own
/// <c>LightweightSession()</c> and commit via a single <c>SaveChangesAsync</c>;
/// the <see cref="AccessAudit"/> row commits in the **same session** as the
/// domain write. Reads use <c>Query</c> (no write session opened).
/// </para>
/// <para>
/// C-S1·1: <see cref="RevokeSessionAsync"/> does nothing when
/// <c>doc.UserId != userId</c> or <c>doc.RevokedAt</c> is already set —
/// the controller enforces the current-session rule (409) before calling;
/// the service enforces the owner-scoped + idempotency rules (C-S1·3).
/// </para>
/// <para>
/// C-S1·5: <see cref="TouchSessionAsync"/> is the dumb write — it updates
/// <c>LastActive</c> when called; the calling middleware
/// (<c>SessionRevocationMiddleware</c>, U6) is the smart rate-limit gate.
/// </para>
/// </summary>
public sealed class SessionService : ISessionService
{
    private readonly IDocumentStore _store;

    public SessionService(IDocumentStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Create a new session (F1, called from the <c>OnSigningIn</c> handler —
    /// C-S1·4: the sign-in flow is unchanged; this is the only creation path).
    /// Constructs a <see cref="SessionDoc"/>, stores it, and returns the stored doc.
    /// </summary>
    public async Task<SessionDoc> CreateSessionAsync(string userId, string userAgent, string ip)
    {
        var now = DateTime.UtcNow;
        var doc = new SessionDoc
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            UserAgent = userAgent,
            Ip = ip,
            CreatedAt = now,
            LastActive = now,
            RevokedAt = null,
        };
        await using var session = _store.LightweightSession();
        session.Store(doc);
        await session.SaveChangesAsync();
        return doc;
    }

    /// <summary>
    /// List the user's active (non-revoked) sessions, most-recent-first
    /// (F2/F3). A read — no write session opened.
    /// </summary>
    public async Task<IReadOnlyList<SessionDoc>> GetSessionsForUserAsync(string userId)
    {
        await using var session = _store.QuerySession();
        return (await session.Query<SessionDoc>()
            .Where(d => d.UserId == userId && d.RevokedAt == null)
            .OrderByDescending(d => d.LastActive)
            .ToListAsync());
    }

    /// <summary>
    /// Revoke one session (F4, C-S1·1 + C-S1·3 + idempotent). Loads the doc by
    /// <c>Id</c>; if <c>doc.UserId != userId</c> **or** <c>doc.RevokedAt</c> is
    /// already set, does nothing (the C-S1·3 owner-scoped rule + the C-S1·1
    /// current-session rule + idempotency). Otherwise sets
    /// <c>RevokedAt = UtcNow</c> and appends a <see cref="AccessAudit"/> row
    /// (<c>targetKind "session"</c>, <c>Action "session.revoke"</c>) in the
    /// **same session** (C3 same-transaction).
    /// </summary>
    public async Task RevokeSessionAsync(string userId, string sessionId)
    {
        await using var session = _store.LightweightSession();
        var doc = await session.LoadAsync<SessionDoc>(sessionId);
        if (doc is null || doc.UserId != userId || doc.RevokedAt is not null)
            return; // C-S1·3 (cross-user) / C-S1·1 (already revoked / not found) — no-op

        doc.RevokedAt = DateTime.UtcNow;
        session.Store(doc);
        session.Store(new AccessAudit
        {
            Id = Guid.NewGuid().ToString(),
            At = DateTimeOffset.UtcNow,
            ActorId = userId,
            Action = "session.revoke",
            TargetKind = "session",
            TargetId = sessionId,
            Via = AccessVia.Owner,
            Outcome = AccessOutcome.Allow,
        });
        await session.SaveChangesAsync();
    }

    /// <summary>
    /// Revoke all-but-current (F5, the kill-all extension; C3 same-transaction).
    /// Loads the user's active sessions; for each doc where
    /// <c>doc.Id != currentSessionId</c>, sets <c>RevokedAt = UtcNow</c>;
    /// appends **one** <see cref="AccessAudit"/> row
    /// (<c>targetKind "session"</c>, <c>Action "session.revoke_all_but_current"</c>)
    /// in the **same session**. The current session's <c>RevokedAt</c> stays
    /// <c>null</c> (the resident stays signed in).
    /// </summary>
    public async Task RevokeAllButCurrentAsync(string userId, string currentSessionId)
    {
        await using var session = _store.LightweightSession();
        var sessions = (await session.Query<SessionDoc>()
            .Where(d => d.UserId == userId && d.RevokedAt == null)
            .ToListAsync());

        var revokedCount = 0;
        foreach (var doc in sessions)
        {
            if (doc.Id == currentSessionId)
                continue; // the current session stays active
            doc.RevokedAt = DateTime.UtcNow;
            session.Store(doc);
            revokedCount++;
        }

        // C3: the audit row commits in the same session (same transaction).
        session.Store(new AccessAudit
        {
            Id = Guid.NewGuid().ToString(),
            At = DateTimeOffset.UtcNow,
            ActorId = userId,
            Action = "session.revoke_all_but_current",
            TargetKind = "session",
            TargetId = currentSessionId,
            Via = AccessVia.Owner,
            Outcome = AccessOutcome.Allow,
        });
        await session.SaveChangesAsync();
    }

    /// <summary>
    /// Touch the session's <c>LastActive</c> (F6, C-S1·5). The service is the
    /// dumb write — it updates <c>LastActive</c> when called; the calling
    /// middleware is the smart rate-limit gate (≤ once per 60 s per session).
    /// Does nothing if the session is already revoked (C-S1·2: a revoked
    /// session is not touched).
    /// </summary>
    public async Task TouchSessionAsync(string sessionId)
    {
        await using var session = _store.LightweightSession();
        var doc = await session.LoadAsync<SessionDoc>(sessionId);
        if (doc is null || doc.RevokedAt is not null)
            return; // not found or already revoked — no-op

        doc.LastActive = DateTime.UtcNow;
        session.Store(doc);
        await session.SaveChangesAsync();
    }
}
