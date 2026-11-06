namespace Kumunita.Core.Sessions;

/// <summary>
/// The session lifecycle seam (IMPROVE-CODE-2 C2-03 — the
/// <see cref="SessionService"/>'s cross-module surface, extracted onto a
/// narrow interface so the crossing modules (the <c>SecuritySettings</c>
/// controller, the <c>Program</c> sign-in handler, the
/// <c>SessionRevocation</c> middleware) code against a stable seam rather
/// than the <c>SessionService</c> concrete class — the C03
/// <see cref="Kumunita.Core.Notifications.INotificationEmitter"/> pattern,
/// the ADR 0006-D "route + authz + shape" rule).
/// <para>
/// The five methods are the cross-module surface the callers actually use
/// (the part-vs-whole rule — the service is small and single-purpose
/// [session create / list / revoke / touch], so its whole surface is the
/// crossing surface; there is no fat/tin split to avoid). The
/// <see cref="SessionDoc"/> read shape, the owner-scoped <c>RevokeSession</c>
/// rule, the <c>RevokeAllButCurrent</c> lane, and the <see
/// cref="Kumunita.Core.Authorization.AccessAudit"/> rows all stay in the
/// concrete — the interface adds no behaviour.
/// </para>
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Create a new session (F1, the <c>OnSigningIn</c> handler — the only
    /// creation path). The <see cref="SessionService"/> implementation is
    /// unchanged (the <see cref="SessionDoc"/> construction + the store
    /// commit stay in the concrete).
    /// </summary>
    Task<SessionDoc> CreateSessionAsync(string userId, string userAgent, string ip);

    /// <summary>
    /// List the user's active (non-revoked) sessions, most-recent-first
    /// (the <c>SecuritySettings</c> controller's session list). The
    /// <see cref="SessionService"/> implementation is unchanged.
    /// </summary>
    Task<System.Collections.Generic.IReadOnlyList<SessionDoc>> GetSessionsForUserAsync(string userId);

    /// <summary>
    /// Revoke one session (F4, the owner-scoped <c>RevokeSession</c> lane —
    /// the C-S1·1 current-session rule + the C-S1·3 cross-user no-op + the
    /// C3 same-transaction <see cref="Kumunita.Core.Authorization.AccessAudit"/>
    /// row all stay in the <see cref="SessionService"/> concrete).
    /// </summary>
    Task RevokeSessionAsync(string userId, string sessionId);

    /// <summary>
    /// Revoke all-but-current (F5, the kill-all extension). The
    /// <see cref="SessionService"/> implementation is unchanged (the current
    /// session stays active, the single aggregate audit row commits in the
    /// same session).
    /// </summary>
    Task RevokeAllButCurrentAsync(string userId, string currentSessionId);

    /// <summary>
    /// Touch the session's <c>LastActive</c> (F6, the
    /// <c>SessionRevocation</c> middleware's rate-limited write). The
    /// <see cref="SessionService"/> implementation is unchanged (the
    /// "dumb write — the middleware is the smart gate" rule, C-S1·5).
    /// </summary>
    Task TouchSessionAsync(string sessionId);
}
