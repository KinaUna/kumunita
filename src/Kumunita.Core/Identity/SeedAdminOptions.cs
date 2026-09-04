namespace Kumunita.Core.Identity;

/// <summary>
/// Seed-admin bootstrap (OPS §2, FirstBootSeeder). Per-instance: bound from
/// <c>SeedAdmin__Email</c> / <c>SeedAdmin__Token</c> env (both are *one-time* credentials;
/// a long-lived admin password is **not** in env — the app invalidates the token on first
/// use, which removes the change-then-delete race, OPS §2).
/// <para>
/// Honor absence: when <see cref="Email"/> is null/blank the seeder (and
/// <see cref="IIdentityService.CompleteSeedAdminSetupAsync"/>'s token lookup) skip the
/// seed-admin lane entirely — an instance without first-run env is still usable.
/// </para>
/// </summary>
public sealed class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";

    public string? Email { get; set; }

    /// <summary>High-entropy one-time token; consumed on first use. Never in a URL.</summary>
    public string? Token { get; set; }
}
