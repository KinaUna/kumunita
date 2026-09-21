namespace Kumunita.Core.Identity;

/// <summary>
/// Sample-data opt-in (ADR 0056, amending ADR 0055's <c>Development</c>-wall gate).
/// The mock neighborhood seeder runs only when <c>SampleData__Enabled=true</c>
/// AND the database is pristine — an explicit per-instance decision, never a side
/// effect of the environment.
/// <para>
/// Two sanctioned deployments carry the flag:
/// <list type="bullet">
/// <item>the local dev loop (<c>docker-compose.yml</c> / <c>appsettings.Development.json</c>,
/// <c>Development</c> environment) — the seeder keeps the documented weak
/// credentials, printed to the log and the README (the ADR 0055 posture);</item>
/// <item>a <b>deployed demo site</b> (<c>Production</c> environment, the
/// <c>examplium.com</c> names) — the seeder takes its <b>deploy posture</b>:
/// the seed-admin account stays on its <c>SeedAdmin__*</c> token lane (never a
/// weak password), the other demo accounts get random high-entropy passwords,
/// and a single credentials summary is staged to the seed admin's e-mail through
/// the durable outbox — no weak credential is ever stored on a public instance.
/// </list>
/// Absence (null/blank/omitted) is the default: real deployments never carry the
/// flag, so the seeder is unreachable by construction — the same posture as
/// <see cref="SeedAdminOptions"/>.
/// </para>
/// </summary>
public sealed class SampleDataOptions
{
    public const string SectionName = "SampleData";

    /// <summary>Whether the first-boot sample neighborhood is seeded on this instance.</summary>
    public bool Enabled { get; set; }
}
