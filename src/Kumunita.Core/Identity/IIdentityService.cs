using Kumunita.Core.UserInfo;

namespace Kumunita.Core.Identity;

/// <summary>
/// The IdentityModule's public surface — ADR 0006 §A (frozen; changes are breaking).
/// <para>
/// Only the IdentityModule knows the identity source (ASP.NET Core Identity,
/// <c>identity</c> schema; cookie claims now, the later OIDC <c>sub</c> swap is mechanical
/// and confined to this module) and only it issues <see cref="ThinPrincipal"/>.
/// </para>
/// <para>
/// **M1 lifecycle methods** (the ADR 0006-E *compatible* lane — added to the owning
/// module's public surface, named here): the M1 design's identity lifecycle
/// (signup, the verification token + admin manual-verify valve, the seed-admin setup
/// token) reaches the module through these. Each such action appends its own
/// <see cref="Authorization.AccessAudit"/> admin-action row (<c>via: Admin</c> /
/// <c>via: BreakGlass</c>) in the same commit as its own writes.
/// </para>
/// </summary>
public interface IIdentityService
{
    /// <summary>The thin principal for the current request, or null when unauthenticated
    /// (built from <see cref="IClaimsSource.Current"/>'s claim set — the claim shape is
    /// the whole principal).</summary>
    Task<ThinPrincipal?> GetCurrentAsync();

    /// <summary>The principal for a given subject, or null (identity lookup for decisions
    /// made on someone else's behalf — e.g. resolving a moderator's scope).</summary>
    Task<ThinPrincipal?> GetBySubjectAsync(string subjectId);

    // ── M1 lifecycle (ADR 0006-E compatible lane) ──────────────────────────────────────

    /// <summary>
    /// Signup (OPS §2/§7, the design's "the first resident touch"): create an
    /// *unverified* account, bootstrap its <see cref="Profile"/>, create a fresh
    /// <see cref="IdentityToken"/> (kind
    /// <see cref="IdentityToken.KindVerify"/>, idempotency <c>verify:{userId}:1</c>), and
    /// stage the one verification email (<see cref="OutboxEmail"/>) — the only world seam,
    /// the single-send designed handoff. The account cannot sign in until verified.
    /// </summary>
    Task<ThinPrincipal> RegisterAsync(string displayName, string email, string password);

    /// <summary>
    /// Consume a verification link (the resident clicked the link — the handoff ends
    /// on-platform): set <see cref="Profile.Verified"/>, mark the token consumed, audit
    /// (<c>via: Owner</c> — the resident verifying their own account).
    /// </summary>
    Task<Profile> VerifyWithTokenAsync(string tokenValue);

    /// <summary>
    /// The admin manual-verify valve (OPS §7 safety valve — the unverified-signup pile-up
    /// signal when verification emails dead-letter): a GlobalAdmin verifies an unverified
    /// account in-app; the account becomes usable immediately. Audited
    /// (<c>via: Admin</c>, target the account).
    /// </summary>
    Task<Profile> ManuallyVerifyAsync(string targetSubjectId, string adminSubjectId);

    /// <summary>
    /// Block a resident (the admin suspension lane): a GlobalAdmin marks the target
    /// <see cref="Profile.Blocked"/>; the account immediately loses all role standing
    /// (no <c>Member</c>/<c>Moderator</c>/<c>GlobalAdmin</c>, so it cannot act or be
    /// granted standing) until unblocked. The account and its documents are preserved
    /// (a reversible suspension, not a delete). Rotates the security stamp (existing
    /// sessions invalidate on their next re-mint) and appends an audit row
    /// (<c>via: Admin</c>, action <c>"block"</c>, target the account). Only a GlobalAdmin
    /// may call this.
    /// </summary>
    Task BlockAsync(string targetSubjectId, string adminSubjectId);

    /// <summary>
    /// Unblock a resident (the inverse of <see cref="BlockAsync"/>): restores the target's
    /// <see cref="Profile.Blocked"/> to false, so its standing (roles) is available again at
    /// next sign-in / re-mint. Rotates the security stamp and appends an audit row
    /// (<c>via: Admin</c>, action <c>"unblock"</c>, target the account). Only a GlobalAdmin
    /// may call this.
    /// </summary>
    Task UnblockAsync(string targetSubjectId, string adminSubjectId);

    /// <summary>
    /// Seed-admin bootstrap (OPS §2, FirstBootSeeder): the one-time setup token is
    /// consumed, invalidating it; the account is sign-in-ready (verified), a password set;
    /// a duplicate token use is rejected (single-use). Audited (<c>via: Admin</c>).
    /// </summary>
    Task<ThinPrincipal> CompleteSeedAdminSetupAsync(string email, string setupTokenValue, string newPassword);

    /// <summary>
    /// Consume a break-glass <see cref="Authorization.AdminOverride"/> token (§4.5, OPS §9):
    /// the target account, in-app at <c>/admin/break-glass</c>, presents the token exactly
    /// once; the row's <see cref="Authorization.AdminOverride.ConsumedAt"/> is set (single-use);
    /// the elevation lasts until <see cref="Authorization.AdminOverride.ExpiresAt"/> — every
    /// subsequent privileged decision under it records <c>via: BreakGlass</c> (the
    /// AuthorizationModule's inline read, not an identity state).
    /// </summary>
    Task ConsumeBreakGlassAsync(string subjectId, string token);

    /// <summary>
    /// Role promote/demote + component-scope assignment (ADR 0003): a GlobalAdmin promotes
    /// to / demotes from <c>GlobalAdmin</c> or <c>Moderator</c>; for a <c>Moderator</c>,
    /// <paramref name="componentIds"/> is the complete scope (null/empty clears it — the
    /// only standing-moderator path is <c>moderatorAccess</c>, invariant C5). Rotates the
    /// security stamp (invalidates existing sessions — a demoted account loses the
    /// elevated access immediately, not at cookie expiry, OPS §10). Appends an audit row
    /// <c>(via: Admin, action: "role")</c>. Only a GlobalAdmin may call this.
    /// </summary>
    Task SetRoleAsync(string targetSubjectId, string adminSubjectId, string role,
        IReadOnlyList<string>? componentIds);

    /// <summary>
    /// Change password (self-serve, or a GlobalAdmin reset): rotates the security stamp
    /// so the account's existing sessions invalidate. Appends an audit row
    /// <c>(via: Owner | Admin)</c>.
    /// </summary>
    Task ChangePasswordAsync(string subjectId, string newPassword, bool byAdmin);
}
