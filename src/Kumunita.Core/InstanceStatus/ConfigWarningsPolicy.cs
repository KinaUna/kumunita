namespace Kumunita.Core.InstanceStatus;

/// <summary>
/// The pure decision surface for the four config warnings on the operator's
/// status read (the <see cref="InstanceStatus"/> POCO's <c>Warnings</c> list, the
/// <c>/admin/status</c> page's warning rows in O02). The exact
/// <see cref="Kumunita.Core.Usage.UsagePurgePolicy"/> /
/// <see cref="Kumunita.Core.Usage.StorageLimits"/> /
/// <see cref="Kumunita.Core.Notifications.MailAlarmThresholdPolicy"/> precedent:
/// a **pure** static decision — no <c>IDocumentStore</c>, no <c>IOptions</c>,
/// no session, no HTTP, no I/O — over the
/// <see cref="ConfigWarningInputs"/> posture snapshot. Unit-tested without a
/// <c>PostgresFixture</c> (the <see cref="MailAlarmThresholdPolicyTests"/> shape).
/// <para>
/// <b>The four rules (the O01 card, one pure branch each):</b>
/// <list type="number">
/// <item><see cref="ConfigWarning.Keys.SeedAdminStillSet"/> — fires iff
/// <c>SeedAdminCredentialsSet &amp;&amp; AdminExists</c>. The one-time setup
/// credentials are only a warning once a GlobalAdmin actually exists (a fresh
/// instance with no admin yet needs them — the <c>first-boot.md</c> step-4
/// "remove after first login" rule); the identity is never carried, only the bool.</item>
/// <item><see cref="ConfigWarning.Keys.SampleDataInProduction"/> — fires iff
/// <c>SampleDataEnabled &amp;&amp; IsProduction</c>. A <c>Development</c>
/// instance opting in to sample data is the intended loop (ADR 0055/0056) —
/// no warning there; the warning is the deployed-demo / misconfigured-real
/// deployment case.</item>
/// <item><see cref="ConfigWarning.Keys.HealthTokenUnset"/> — fires iff
/// <c>!HealthTokenSet</c>. The full <c>/health</c> payload is open to any
/// GlobalAdmin session without a shared secret; recommended for production,
/// and the warning is not environment-gated because a <c>Development</c>
/// instance with an accidental token also deserves the nudge.</item>
/// <item><see cref="ConfigWarning.Keys.DataProtectionNotPersisted"/> — fires iff
/// <c>!DataProtectionKeysPersisted</c>. Sign-in + antiforgery state is
/// in-memory and dies on every redeploy; same environment-agnostic posture as
/// rule 3 (a <c>Development</c> instance usually runs one-shot containers and
/// the warning is informational there).</item>
/// </list>
/// </para>
/// <para>
/// <b>No-secret invariant:</b> the result is a <see cref="ConfigWarning"/>
/// record with a <b>stable <see cref="ConfigWarning.Key"/></b> and a
/// <b>secret-free <see cref="ConfigWarning.Reason"/></b> (the
/// <see cref="ConfigWarning.Reasons"/> constants). The input snapshot carries
/// only booleans — even if the host's <c>SeedAdmin__Token</c> /
/// <c>Health__Token</c> values are set, the decision sees only
/// "is this set?" and never the value. The
/// <c>ConfigWarningsPolicyTests</c> pin this (a test that a set credential /
/// token input never appears in the result).
/// </para>
/// </summary>
public static class ConfigWarningsPolicy
{
    /// <summary>
    /// The pure decision (the four rules above, one branch each, in a fixed
    /// order — the result list is the operator's expected warning set, the
    /// order being the card's order: seedadmin → sampledata → health-token →
    /// dataprotection).
    /// </summary>
    /// <param name="inputs">
    /// The six-posture snapshot (the
    /// <see cref="Kumunita.Core.InstanceStatus"/> POCO's warning-input section,
    /// composed by the <see cref="IInstanceStatusService"/> from the host-bound
    /// <see cref="InstanceStatusOptions"/> + the <c>SeedAdminOptions</c> /
    /// <c>SampleDataOptions</c> / <c>HealthOptions</c> + the
    /// <c>UserManager&lt;User&gt;</c> GlobalAdmin-count read).
    /// </param>
    /// <returns>
    /// An empty list when all four postures are clean; otherwise the
    /// <see cref="ConfigWarning"/> records for each breached rule, in the
    /// card's order (seedadmin, sampledata, health-token, dataprotection).
    /// </returns>
    public static IReadOnlyList<ConfigWarning> Evaluate(ConfigWarningInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var warnings = new List<ConfigWarning>();

        // Rule 1 — SeedAdmin__* still set AND a GlobalAdmin already exists.
        // A fresh instance (no admin yet) is NOT warned (the first-boot step-4
        // "remove after first login" rule — the credentials are the setup path).
        if (inputs.SeedAdminCredentialsSet && inputs.AdminExists)
        {
            warnings.Add(new ConfigWarning(
                ConfigWarning.Keys.SeedAdminStillSet,
                ConfigWarning.Reasons.SeedAdminStillSet));
        }

        // Rule 2 — SampleData__Enabled on in Production. A Development instance
        // opting in is the intended loop (ADR 0055/0056) — no warning there.
        if (inputs.SampleDataEnabled && inputs.IsProduction)
        {
            warnings.Add(new ConfigWarning(
                ConfigWarning.Keys.SampleDataInProduction,
                ConfigWarning.Reasons.SampleDataInProduction));
        }

        // Rule 3 — Health__Token unset (the full /health payload is open to any
        // GlobalAdmin session). Not environment-gated: a Development instance
        // with an accidental token also deserves the nudge.
        if (!inputs.HealthTokenSet)
        {
            warnings.Add(new ConfigWarning(
                ConfigWarning.Keys.HealthTokenUnset,
                ConfigWarning.Reasons.HealthTokenUnset));
        }

        // Rule 4 — DataProtection__KeysDirectory not persisted (in-memory
        // keyring — sign-in + antiforgery state dies on redeploy).
        // Not environment-gated (a Development instance running one-shot
        // containers sees this as informational).
        if (!inputs.DataProtectionKeysPersisted)
        {
            warnings.Add(new ConfigWarning(
                ConfigWarning.Keys.DataProtectionNotPersisted,
                ConfigWarning.Reasons.DataProtectionNotPersisted));
        }

        return warnings;
    }
}
