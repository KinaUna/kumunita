using Kumunita.Core.Privacy;

namespace Kumunita.Core.Sessions;

/// <summary>
/// The sessions lane's (the per-session list + revoke one device; the F5 "Sign out
/// everywhere" whole-account kill is unchanged and coexists with this — see the
/// design doc, `docs/design/sessions-per-session-revocation.md`) per-session
/// document: one row per sign-in, created on the <c>OnSigningIn</c> hook (the only
/// creation path — C-S1·4), and read back per-request by the
/// <c>SessionRevocationMiddleware</c> (the per-session analog of F5's
/// <c>RevocationStampMiddleware</c>).
/// <para>
/// Marten-native (ADR 0004 §B.1): a POCO with the conventional <c>string</c>
/// <c>Id</c> identity (a GUID, the session id — the <c>kumunita.sid</c> cookie
/// item, set alongside F5's <c>kumunita.stamp</c> in <c>Program.cs</c>'s
/// <c>OnSigningIn</c> handler), registered in the parallel
/// <see cref="Kumunita.Core.SessionDocTypes"/> surface (the
/// <see cref="Kumunita.Core.PostDocTypes"/> pattern, not additive on
/// <see cref="Kumunita.Core.CoreDocTypes"/>). No business-key index in this lane
/// — the <c>(UserId, RevokedAt)</c> index is a named <c>SL·D</c> deferral, not a
/// U3 deliverable. No seeding: sessions are created on sign-in, never by a seeder
/// (ADR 0004 §B.1, the M3/Media/Page precedent).
/// </para>
/// </summary>
[DataClass(DataClassKind.Pii, Erasure.Delete, ExportScope.Never,
    reason: "F1 §2C decision (now decided): a session credential, not content; D6: 30 days after revoke or expiry",
    retentionKey: "SessionDays")]
public sealed class SessionDoc
{
    /// <summary>The session id — a GUID string, the Marten document identity (conventional,
    /// no <c>Identity(...)</c> override needed) and the value stamped into the
    /// <c>kumunita.sid</c> cookie item on sign-in.</summary>
    public string Id { get; set; } = "";

    /// <summary>The resident's <c>User.Id</c> (the <c>Kumunita.Sub</c> claim) this session
    /// belongs to.</summary>
    public string UserId { get; set; } = "";

    /// <summary>The sign-in request's <c>User-Agent</c> header, for the resident-facing
    /// "which device is that?" display (the <c>SL·D</c> <c>DeviceLabel</c> lane, a
    /// resident-set label, is a named deferral — this is the raw string only).</summary>
    public string UserAgent { get; set; } = "";

    /// <summary>The sign-in request's client IP, for the same resident-facing display.</summary>
    public string Ip { get; set; } = "";

    /// <summary>The sign-in time (UTC) — set once at creation, never updated.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>The last-touch time (UTC) — updated by <c>SessionService.TouchSessionAsync</c>,
    /// rate-limited (≤ once per 60 s per session) by the calling
    /// <c>SessionRevocationMiddleware</c>, not by the service itself (C-S1·5).</summary>
    public DateTime LastActive { get; set; }

    /// <summary>The active/revoked discriminator: <c>null</c> = active; set (to the revoke
    /// time, UTC) = revoked. This is the only nullable field on the document.</summary>
    public DateTime? RevokedAt { get; set; }
}
