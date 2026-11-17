namespace Kumunita.Core.InstanceStatus;

/// <summary>
/// One config warning on the operator's status read (the
/// <see cref="ConfigWarningsPolicy.Evaluate"/> result, the
/// <see cref="InstanceStatus"/> POCO's <c>Warnings</c> list, the
/// <c>/admin/status</c> page's warning rows in O02).
/// <para>
/// <see cref="Key"/> is a <b>stable, closed-set</b> machine identifier (not a
/// human string) — O02 maps it to a localized UI key (en/de/fr/da) and uses it
/// to style the row (red / amber). <see cref="Reason"/> is a <b>secret-free</b>,
/// plain-English Core constant naming the misconfiguration in one sentence (the
/// "no secret values" invariant — never the credential value, the token value,
/// a key path's contents, or a connection string).
/// </para>
/// <para>
/// The closed set is exactly the four keys the O01 card names:
/// <see cref="Keys.SeedAdminStillSet"/>, <see cref="Keys.SampleDataInProduction"/>,
/// <see cref="Keys.HealthTokenUnset"/>, <see cref="Keys.DataProtectionNotPersisted"/>
/// (no other key is ever minted — a new warning needs a plan + an ADR).
/// </para>
/// </summary>
public sealed record ConfigWarning(string Key, string Reason)
{
    /// <summary>The stable machine keys (closed set, O02 maps to UI keys).</summary>
    public static class Keys
    {
        /// <summary><c>SeedAdmin__Email</c> / <c>SeedAdmin__Token</c> still set in env AND a GlobalAdmin already exists — the one-time setup credentials should be removed (the <c>first-boot.md</c> step-4 precedent; a fresh instance with no admin yet is not warned).</summary>
        public const string SeedAdminStillSet = "seedadmin-still-set";

        /// <summary><c>SampleData__Enabled</c> is on in <c>Production</c> — the mock-neighborhood seeder is on in a real deployment (ADR 0055/0056).</summary>
        public const string SampleDataInProduction = "sampledata-in-production";

        /// <summary><c>Health__Token</c> is unset — the full <c>/health</c> diagnostic payload is reachable by any GlobalAdmin session without a shared secret (recommended for production; the Coolify / edge-proxy liveness probe shape is unaffected).</summary>
        public const string HealthTokenUnset = "health-token-unset";

        /// <summary><c>DataProtection__KeysDirectory</c> is not persisted — sign-in + antiforgery state is in-memory and dies on every redeploy (the "I keep getting bounced back to login" failure mode, the <c>Program.cs</c> DataProtection comment).</summary>
        public const string DataProtectionNotPersisted = "dataprotection-not-persisted";
    }

    /// <summary>
    /// The four secret-free, plain-English reason strings (stable — the
    /// <c>ConfigWarningsPolicyTests</c> pin the exact text; the O02 page maps
    /// the <see cref="Keys"/> to the localized UI text, so these Core constants
    /// are the operator-facing fallback when a UI key is missing).
    /// </summary>
    public static class Reasons
    {
        public const string SeedAdminStillSet =
            "The one-time setup credentials (SeedAdmin__Email / SeedAdmin__Token) are still set in env even though a GlobalAdmin already exists. Remove them (first-boot.md step 4).";

        public const string SampleDataInProduction =
            "The mock-neighborhood seeder (SampleData__Enabled) is on in a real (Production) deployment. Real deployments omit it — a deployed demo site (ADR 0056) is the one intended case.";

        public const string HealthTokenUnset =
            "Health__Token is unset — the full /health diagnostic payload is reachable by any GlobalAdmin session without a shared secret. Set a shared secret for production (the anonymous liveness probe shape is unaffected).";

        public const string DataProtectionNotPersisted =
            "DataProtection__KeysDirectory is not persisted — sign-in + antiforgery state is in-memory and dies on every redeploy (the \"bounced back to login\" failure mode). Point it at a persistent directory (deploy/coolify/step-5/52-data-protection.md).";
    }
}

/// <summary>
/// The pure-input snapshot the <see cref="ConfigWarningsPolicy.Evaluate"/> reads
/// over (the <see cref="Kumunita.Core.Usage.UsagePurgePolicy"/> /
/// <see cref="Kumunita.Core.Usage.StorageLimits"/> precedent — a small input
/// POCO, no DB, no session, no HTTP).
/// <para>
/// Six posture booleans — the four the card names plus the two
/// <c>IWebHostEnvironment</c>-shaped facts Core cannot read itself (carried by
/// the host-bound <see cref="InstanceStatusOptions"/>):
/// <list type="bullet">
/// <item><see cref="SeedAdminCredentialsSet"/> — <c>SeedAdmin__Email</c> or <c>SeedAdmin__Token</c> is non-empty (the <see cref="Kumunita.Core.Identity.SeedAdminOptions"/> read; presence only, never the value).</item>
/// <item><see cref="AdminExists"/> — at least one <c>GlobalAdmin</c> user exists (the <c>UserManager&lt;User&gt;.GetUsersInRoleAsync</c> count &gt; 0; the identity is never carried, only the bool).</item>
/// <item><see cref="SampleDataEnabled"/> — the <c>SampleData__Enabled</c> flag is on (the <see cref="Kumunita.Core.Identity.SampleDataOptions"/> read).</item>
/// <item><see cref="IsProduction"/> — the host is running in <c>Production</c> (the <see cref="InstanceStatusOptions.IsProduction"/> read).</item>
/// <item><see cref="HealthTokenSet"/> — the <c>Health__Token</c> is non-empty (the <see cref="Kumunita.Core.Identity.HealthOptions"/> read; presence only, never the value).</item>
/// <item><see cref="DataProtectionKeysPersisted"/> — the host set <c>DataProtection__KeysDirectory</c> (the <see cref="InstanceStatusOptions.DataProtectionKeysPersisted"/> read).</item>
/// </list>
/// </para>
/// </summary>
public sealed record ConfigWarningInputs(
    bool SeedAdminCredentialsSet,
    bool AdminExists,
    bool SampleDataEnabled,
    bool IsProduction,
    bool HealthTokenSet,
    bool DataProtectionKeysPersisted)
{
    /// <summary>A fully-clean instance (no warning fires for any rule).</summary>
    public static ConfigWarningInputs Clean => new(
        SeedAdminCredentialsSet:   false,
        AdminExists:               false,
        SampleDataEnabled:         false,
        IsProduction:              true,
        HealthTokenSet:            true,
        DataProtectionKeysPersisted: true);
}
