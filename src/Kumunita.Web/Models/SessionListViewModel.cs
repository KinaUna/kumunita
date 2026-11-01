namespace Kumunita.Web.Models;

/// <summary>
/// The sessions-lane (per-session list + revoke one device — the F5 "Sign out
/// everywhere" whole-account kill coexists and is unchanged; see the design
/// doc <c>docs/design/sessions-per-session-revocation.md</c>) view model for
/// <c>GET /settings/security/sessions</c> (U7). This is the **per-session**
/// surface (the F5 <see cref="SessionSettingsViewModel"/> kill-all block is
/// the whole-account kill — the two coexist).
/// <para>
/// The list is this resident's **active (non-revoked)** <c>SessionDoc</c>
/// rows, most-recent-first (the single composition point is
/// <see cref="Kumunita.Core.Sessions.SessionService"/> — the controller is
/// route + shape, never a re-derivation). <see cref="IsCurrent"/> marks the
/// row whose id is in this browser's <c>kumunita.sid</c> cookie item — the
/// session the per-row <c>POST …/{id}/revoke</c> action **refuses** to revoke
/// (C-S1·1, a 409). <see cref="KillAllEnabled"/> keeps the existing "Sign out
/// everywhere" button active (the F5 path is unchanged).
/// </para>
/// </summary>
public sealed class SessionListViewModel
{
    /// <summary>The current browser's session id (the <c>kumunita.sid</c> cookie
    /// item value; <c>""</c> when absent — a pre-lane session).</summary>
    public string CurrentSessionId { get; set; } = "";

    /// <summary>The active (non-revoked) sessions, most-recent-first.</summary>
    public IReadOnlyList<SessionItem> Sessions { get; set; } = [];

    /// <summary>Whether the "Sign out everywhere" (kill-all) button is shown
    /// (always <c>true</c> in this lane — the F5 whole-account kill is kept).</summary>
    public bool KillAllEnabled { get; set; } = true;
}

/// <summary>
/// One row in the <see cref="SessionListViewModel"/> sessions table: the
/// sign-in metadata (the resident-facing "which device is that?" display) +
/// the <see cref="IsCurrent"/> flag (C-S1·1 — the row the per-row revoke
/// refuses, the one this browser is using).
/// </summary>
public sealed class SessionItem
{
    /// <summary>The session id (the <c>SessionDoc.Id</c>, a GUID).</summary>
    public string Id { get; set; } = "";

    /// <summary>The sign-in <c>User-Agent</c> header (the raw string — a
    /// resident-set <c>DeviceLabel</c> is a named <c>SL·D</c> deferral).</summary>
    public string UserAgent { get; set; } = "";

    /// <summary>The sign-in client IP.</summary>
    public string Ip { get; set; } = "";

    /// <summary>The sign-in time (UTC) — set once at creation.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>The last-touch time (UTC) — updated per-request, rate-limited
    /// by the <c>SessionRevocationMiddleware</c> (C-S1·5).</summary>
    public DateTime LastActive { get; set; }

    /// <summary><c>true</c> when <see cref="Id"/> == the
    /// <see cref="SessionListViewModel.CurrentSessionId"/> (this browser's
    /// session — the row the per-row revoke refuses, C-S1·1).</summary>
    public bool IsCurrent { get; set; }
}
