namespace Kumunita.Core.Identity;

/// <summary>
/// A resident's single-use token (verification link or seed-admin setup credential).
/// <see cref="Id"/> is the stable row id; <see cref="Token"/> is the secret value the
/// endpoint compares against (never in a URL — the URL carries <c>Id</c>).
/// <para>
/// Two kinds, one table:
/// <list type="bullet">
/// <item><see cref="KindVerify"/> — the signup verification link token (single-send, the
/// world-seam handoff, M1 design "World seams"). One row per attempt
/// (idempotency key <c>verify:{userId}:{attempt}</c>, §6.2).</item>
/// <item><see cref="KindSetup"/> — the one-time seed-admin setup credential
/// (<c>SeedAdmin__Token</c>, OPS §2). The env holds it because a *one-time* credential in
/// env is acceptable; a long-lived admin password is not — the app invalidates it on
/// first use, which removes the change-then-delete race (OPS §2).</item>
/// </list>
/// </para>
/// </summary>
public sealed class IdentityToken
{
    public const string KindVerify = "verify";
    public const string KindSetup = "setup";

    public string Id { get; set; } = string.Empty;

    public string Kind { get; set; } = KindVerify;

    public string UserId { get; set; } = string.Empty;

    /// <summary>The high-entropy secret value.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>1 for a first verification; incremented for each resend (a new attempt,
    /// a new token; per §6.2 idempotency).</summary>
    public int Attempt { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set when the token is consumed in-app. A null value = still valid (subject
    /// to <see cref="ExpiresAt"/>).</summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    public bool IsUsableAt(DateTimeOffset now) => ConsumedAt is null && now < ExpiresAt;
}
