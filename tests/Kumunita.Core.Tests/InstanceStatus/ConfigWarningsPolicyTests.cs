using Kumunita.Core.InstanceStatus;

namespace Kumunita.Core.Tests.InstanceStatus;

/// <summary>
/// Unit tests for the <see cref="ConfigWarningsPolicy"/> pure decision
/// surface (IMPROVE-UX O01, the
/// <see cref="Kumunita.Core.Notifications.MailAlarmThresholdPolicy"/> /
/// <see cref="Kumunita.Core.Usage.UsagePurgePolicy"/> /
/// <see cref="Kumunita.Core.Usage.StorageLimits"/> precedent — pure, no
/// <c>PostgresFixture</c>, no <c>IDocumentStore</c>, no <c>IOptions</c>:
/// just the <see cref="ConfigWarningInputs"/> posture snapshot in, an
/// <c>IReadOnlyList&lt;ConfigWarning&gt;</c> out).
/// <para>
/// Pins:
/// <list type="bullet">
/// <item>each of the four rules fires when its posture is bad and stays quiet
/// when it is good (one test per rule);</item>
/// <item>a combined test: a fully misconfigured instance returns exactly the
/// four expected warnings, in the card's order;</item>
/// <item>a clean instance returns zero warnings;</item>
/// <item>the no-secret invariant: every warning's reason is a stable
/// secret-free constant, and a set credential/token input (a non-empty
/// <c>SeedAdmin__Email</c> / <c>SeedAdmin__Token</c> / <c>Health__Token</c>
/// value) never appears in the result (the "no secret values" DoD).</item>
/// </list>
/// </para>
/// </summary>
public class ConfigWarningsPolicyTests
{
    // ── Rule 1 — SeedAdmin__* still set AND a GlobalAdmin exists ──────────

    [Fact]
    public void Evaluate_SeedAdminSet_AdminExists_FiresWarning()
    {
        var inputs = new ConfigWarningInputs(
            SeedAdminCredentialsSet: true, AdminExists: true,
            SampleDataEnabled: false, IsProduction: true,
            HealthTokenSet: true, DataProtectionKeysPersisted: true);

        var result = ConfigWarningsPolicy.Evaluate(inputs);
        Assert.Single(result);
        Assert.Equal(ConfigWarning.Keys.SeedAdminStillSet, result[0].Key);
    }

    [Fact]
    public void Evaluate_SeedAdminSet_NoAdmin_StayQuiet()
    {
        // A fresh instance with no admin yet — the credentials are the
        // setup path, not a misconfiguration.
        var inputs = new ConfigWarningInputs(
            SeedAdminCredentialsSet: true, AdminExists: false,
            SampleDataEnabled: false, IsProduction: true,
            HealthTokenSet: true, DataProtectionKeysPersisted: true);

        var result = ConfigWarningsPolicy.Evaluate(inputs);
        Assert.False(result.Any(w => w.Key == ConfigWarning.Keys.SeedAdminStillSet));
    }

    [Fact]
    public void Evaluate_SeedAdminUnset_AdminExists_StayQuiet()
    {
        var inputs = new ConfigWarningInputs(
            SeedAdminCredentialsSet: false, AdminExists: true,
            SampleDataEnabled: false, IsProduction: true,
            HealthTokenSet: true, DataProtectionKeysPersisted: true);

        var result = ConfigWarningsPolicy.Evaluate(inputs);
        Assert.False(result.Any(w => w.Key == ConfigWarning.Keys.SeedAdminStillSet));
    }

    // ── Rule 2 — SampleData__Enabled on in Production ─────────────────────

    [Fact]
    public void Evaluate_SampleDataEnabled_Production_FiresWarning()
    {
        var inputs = new ConfigWarningInputs(
            SeedAdminCredentialsSet: false, AdminExists: false,
            SampleDataEnabled: true, IsProduction: true,
            HealthTokenSet: true, DataProtectionKeysPersisted: true);

        var result = ConfigWarningsPolicy.Evaluate(inputs);
        Assert.True(result.Any(w => w.Key == ConfigWarning.Keys.SampleDataInProduction));
    }

    [Fact]
    public void Evaluate_SampleDataEnabled_Development_StayQuiet()
    {
        // A Development instance opting in is the intended loop (ADR 0055/0056).
        var inputs = new ConfigWarningInputs(
            SeedAdminCredentialsSet: false, AdminExists: false,
            SampleDataEnabled: true, IsProduction: false,
            HealthTokenSet: true, DataProtectionKeysPersisted: true);

        var result = ConfigWarningsPolicy.Evaluate(inputs);
        Assert.False(result.Any(w => w.Key == ConfigWarning.Keys.SampleDataInProduction));
    }

    [Fact]
    public void Evaluate_SampleDataDisabled_Production_StayQuiet()
    {
        var inputs = new ConfigWarningInputs(
            SeedAdminCredentialsSet: false, AdminExists: false,
            SampleDataEnabled: false, IsProduction: true,
            HealthTokenSet: true, DataProtectionKeysPersisted: true);

        var result = ConfigWarningsPolicy.Evaluate(inputs);
        Assert.False(result.Any(w => w.Key == ConfigWarning.Keys.SampleDataInProduction));
    }

    // ── Rule 3 — Health__Token unset ──────────────────────────────────────

    [Fact]
    public void Evaluate_HealthTokenUnset_FiresWarning()
    {
        var inputs = new ConfigWarningInputs(
            SeedAdminCredentialsSet: false, AdminExists: false,
            SampleDataEnabled: false, IsProduction: true,
            HealthTokenSet: false, DataProtectionKeysPersisted: true);

        var result = ConfigWarningsPolicy.Evaluate(inputs);
        Assert.True(result.Any(w => w.Key == ConfigWarning.Keys.HealthTokenUnset));
    }

    [Fact]
    public void Evaluate_HealthTokenSet_StayQuiet()
    {
        var inputs = new ConfigWarningInputs(
            SeedAdminCredentialsSet: false, AdminExists: false,
            SampleDataEnabled: false, IsProduction: true,
            HealthTokenSet: true, DataProtectionKeysPersisted: true);

        var result = ConfigWarningsPolicy.Evaluate(inputs);
        Assert.False(result.Any(w => w.Key == ConfigWarning.Keys.HealthTokenUnset));
    }

    // ── Rule 4 — DataProtection__KeysDirectory not persisted ─────────────

    [Fact]
    public void Evaluate_DataProtectionNotPersisted_FiresWarning()
    {
        var inputs = new ConfigWarningInputs(
            SeedAdminCredentialsSet: false, AdminExists: false,
            SampleDataEnabled: false, IsProduction: true,
            HealthTokenSet: true, DataProtectionKeysPersisted: false);

        var result = ConfigWarningsPolicy.Evaluate(inputs);
        Assert.True(result.Any(w => w.Key == ConfigWarning.Keys.DataProtectionNotPersisted));
    }

    [Fact]
    public void Evaluate_DataProtectionPersisted_StayQuiet()
    {
        var inputs = new ConfigWarningInputs(
            SeedAdminCredentialsSet: false, AdminExists: false,
            SampleDataEnabled: false, IsProduction: true,
            HealthTokenSet: true, DataProtectionKeysPersisted: true);

        var result = ConfigWarningsPolicy.Evaluate(inputs);
        Assert.False(result.Any(w => w.Key == ConfigWarning.Keys.DataProtectionNotPersisted));
    }

    // ── Combined: fully misconfigured instance ────────────────────────────

    [Fact]
    public void Evaluate_FullyMisconfigured_ReturnsExactlyFourWarnings_InCardOrder()
    {
        var inputs = new ConfigWarningInputs(
            SeedAdminCredentialsSet: true, AdminExists: true,
            SampleDataEnabled: true, IsProduction: true,
            HealthTokenSet: false, DataProtectionKeysPersisted: false);

        var result = ConfigWarningsPolicy.Evaluate(inputs);

        Assert.Equal(4, result.Count);
        // Card order: seedadmin → sampledata → health-token → dataprotection
        Assert.Equal(ConfigWarning.Keys.SeedAdminStillSet, result[0].Key);
        Assert.Equal(ConfigWarning.Keys.SampleDataInProduction, result[1].Key);
        Assert.Equal(ConfigWarning.Keys.HealthTokenUnset, result[2].Key);
        Assert.Equal(ConfigWarning.Keys.DataProtectionNotPersisted, result[3].Key);
    }

    // ── Combined: clean instance ──────────────────────────────────────────

    [Fact]
    public void Evaluate_CleanInstance_ReturnsNoWarnings()
    {
        var result = ConfigWarningsPolicy.Evaluate(ConfigWarningInputs.Clean);
        Assert.Empty(result);
    }

    // ── No-secret invariant ───────────────────────────────────────────────

    [Fact]
    public void Evaluate_Warnings_CarryNoSecretValue()
    {
        // Even when the input "value" is set (the SeedAdmin credentials are
        // non-empty, the Health token is non-empty), the warning reason is
        // a stable, secret-free constant. The policy sees only the boolean
        // posture, never the value.
        var inputs = new ConfigWarningInputs(
            SeedAdminCredentialsSet: true, AdminExists: true,
            SampleDataEnabled: true, IsProduction: true,
            HealthTokenSet: true, DataProtectionKeysPersisted: false);

        var result = ConfigWarningsPolicy.Evaluate(inputs);

        // Every reason is one of the four known constants.
        var knownReasons = new[]
        {
            ConfigWarning.Reasons.SeedAdminStillSet,
            ConfigWarning.Reasons.SampleDataInProduction,
            ConfigWarning.Reasons.HealthTokenUnset,
            ConfigWarning.Reasons.DataProtectionNotPersisted,
        };

        Assert.Equal(3, result.Count);
        foreach (var w in result)
        {
            Assert.Contains(w.Reason, knownReasons);
            // No credential-shaped material (an @, a colon, a long hex string).
            Assert.DoesNotContain("@", w.Reason);
            Assert.DoesNotContain(":", w.Reason);
        }
    }

    [Fact]
    public void Evaluate_WarningKeys_AreStableAndClosedSet()
    {
        var inputs = new ConfigWarningInputs(
            SeedAdminCredentialsSet: true, AdminExists: true,
            SampleDataEnabled: true, IsProduction: true,
            HealthTokenSet: false, DataProtectionKeysPersisted: false);

        var result = ConfigWarningsPolicy.Evaluate(inputs);

        var knownKeys = new[]
        {
            ConfigWarning.Keys.SeedAdminStillSet,
            ConfigWarning.Keys.SampleDataInProduction,
            ConfigWarning.Keys.HealthTokenUnset,
            ConfigWarning.Keys.DataProtectionNotPersisted,
        };
        Assert.All(result, w => Assert.Contains(w.Key, knownKeys));
    }
}
