using Kumunita.Core.Identity;
using Kumunita.Core.Usage;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.InstanceStatus;

/// <summary>
/// The operator's read-only instance-status seam (IMPROVE-UX O01, the
/// <c>/admin/status</c> page's data in O02, the dashboard notice's input in
/// O03, the inbox alarm's payload in O04). One
/// <see cref="GetStatusAsync"/> composing the <see cref="InstanceStatus"/>
/// POCO from the **reused** seams (the
/// <see cref="Kumunita.Web.Controllers.HealthController"/> liveness shape, the
/// <see cref="IMailAlarmStateReader"/> mail-alarm T4 state — which itself reuses
/// the <see cref="Kumunita.Core.Identity.IEmailDeadLetterCounter"/> dead-letter
/// read + the
/// <see cref="Kumunita.Core.Notifications.MailAlarmThresholdPolicy"/> firing
/// decision, the <see cref="ISmtpHealthCheck"/> relay handshake, the
/// <see cref="IStorageMetricsService"/> storage snapshot, the
/// <see cref="UserManager{User}"/> GlobalAdmin count, and the host-bound
/// <see cref="InstanceStatusOptions"/> / <see cref="SeedAdminOptions"/> /
/// <see cref="SampleDataOptions"/> / <see cref="HealthOptions"/> postures) —
/// each called at most once, not re-derived.
/// <para>
/// <b>Resilient degradation:</b> a failing sub-read (no DB, no SMTP, an
/// un-registered seam in a minimal harness) degrades its section to
/// <c>Available = false</c> (the <see cref="MailStatus.Unavailable"/> /
/// <see cref="MailAlarmStatus.Unavailable"/> /
/// <see cref="StorageStatus.Unavailable"/> statics) and the read still
/// returns — the operator still gets the rows that <em>are</em> available
/// (the <c>HealthController</c> degraded-probe posture). The DB reachability
/// fact is read first (the <c>HealthController</c> liveness shape); when it
/// fails, the DB-backed sections (mail-alarm, GlobalAdmin count) degrade in
/// one branch instead of each attempting a connection and each throwing.
/// </para>
/// <para>
/// <b>No secret values:</b> the POCO carries only booleans, integers, longs,
/// a <see cref="DateTimeOffset"/> as-of, a <see cref="TimeSpan"/> uptime, a
/// version string, and the <see cref="ConfigWarning"/> list (a stable key +
/// a secret-free reason). The <c>SeedAdminOptions</c> / <c>HealthOptions</c>
/// values are read for their **presence** only (the
/// <c>string.IsNullOrWhiteSpace</c> check) — the credential / token values
/// themselves are never carried into the POCO (the "no secret values" DoD).
/// </para>
/// </summary>
public sealed class InstanceStatusService : IInstanceStatusService
{
    private readonly IDocumentStore _store;
    private readonly IMailAlarmStateReader _mailAlarmReader;
    private readonly ISmtpHealthCheck _smtpHealth;
    private readonly IStorageMetricsService _storageMetrics;
    private readonly UserManager<User> _userManager;
    private readonly SeedAdminOptions _seedAdmin;
    private readonly SampleDataOptions _sampleData;
    private readonly HealthOptions _health;
    private readonly InstanceStatusOptions _instanceStatus;
    private readonly Func<DateTimeOffset> _now;
    private readonly DateTimeOffset _processStart;

    /// <summary>
    /// Constructor. The <paramref name="now"/> injection point (the
    /// <see cref="Kumunita.Core.Notifications.MailAlarmsService"/>
    /// <c>now</c>-injection precedent) lets a test drive
    /// <see cref="InstanceStatus.Uptime"/> deterministically; production
    /// passes <c>null</c> (the default <see cref="DateTimeOffset.UtcNow"/>).
    /// </summary>
    public InstanceStatusService(
        IDocumentStore store,
        IMailAlarmStateReader mailAlarmReader,
        ISmtpHealthCheck smtpHealth,
        IStorageMetricsService storageMetrics,
        UserManager<User> userManager,
        IOptions<SeedAdminOptions> seedAdmin,
        IOptions<SampleDataOptions> sampleData,
        IOptions<HealthOptions> health,
        IOptions<InstanceStatusOptions> instanceStatus,
        Func<DateTimeOffset>? now = null)
    {
        _store = store;
        _mailAlarmReader = mailAlarmReader;
        _smtpHealth = smtpHealth;
        _storageMetrics = storageMetrics;
        _userManager = userManager;
        _seedAdmin = seedAdmin.Value;
        _sampleData = sampleData.Value;
        _health = health.Value;
        _instanceStatus = instanceStatus.Value;
        _now = now ?? (Func<DateTimeOffset>)(() => DateTimeOffset.UtcNow);
        _processStart = _now();
    }

    /// <inheritdoc />
    public async Task<InstanceStatus> GetStatusAsync(CancellationToken ct)
    {
        var asOf = _now();

        // ── 1. DB reachability (the /health liveness shape) ───────────────
        // The HealthController's first read: AssertConnectivityAsync opens a
        // connection + runs a trivial query; it throws if the DB is down.
        bool dbReachable = true;
        try
        {
            await _store.Storage.Database.AssertConnectivityAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            dbReachable = false;   // the DB-backed sections degrade below
        }

        // ── 2. SMTP relay (best-effort; independent of the DB) ────────────
        var mail = await ReadMailAsync(ct).ConfigureAwait(false);

        // ── 3. Storage snapshot (independent of the DB) ───────────────────
        var storage = await ReadStorageAsync(ct).ConfigureAwait(false);

        // ── 4. Mail-alarm T4 state (DB-backed; via the IMailAlarmStateReader seam) ──
        var mailAlarm = dbReachable
            ? await ReadMailAlarmAsync(ct).ConfigureAwait(false)
            : MailAlarmStatus.Unavailable;

        // ── 5. Config warnings (pure, no DB) ──────────────────────────────
        bool adminExists = dbReachable
            ? await ReadAdminExistsAsync(ct).ConfigureAwait(false)
            : false;

        var inputs = new ConfigWarningInputs(
            SeedAdminCredentialsSet:
                !string.IsNullOrWhiteSpace(_seedAdmin.Email) ||
                !string.IsNullOrWhiteSpace(_seedAdmin.Token),
            AdminExists: adminExists,
            SampleDataEnabled: _sampleData.Enabled,
            IsProduction: _instanceStatus.IsProduction,
            HealthTokenSet: !string.IsNullOrWhiteSpace(_health.Token),
            DataProtectionKeysPersisted: _instanceStatus.DataProtectionKeysPersisted);
        var warnings = ConfigWarningsPolicy.Evaluate(inputs);

        // ── 6. Version + uptime (no DB) ───────────────────────────────────
        var version = ReadVersion();
        var uptime = asOf - _processStart;

        return new InstanceStatus
        {
            AsOf = asOf,
            Version = version,
            Uptime = uptime,
            DatabaseReachable = dbReachable,
            Mail = mail,
            MailAlarm = mailAlarm,
            Storage = storage,
            Warnings = warnings
        };
    }

    // ── section readers (each degrades to its Unavailable static) ─────────

    /// <summary>
    /// The SMTP relay probe (the <see cref="ISmtpHealthCheck"/> handshake — a
    /// failure is a degraded fact, not a throw). A null
    /// <see cref="Kumunita.Core.Identity.SmtpHealthResult"/> or a thrown
    /// exception degrades to <see cref="MailStatus.Unavailable"/>.
    /// </summary>
    private async Task<MailStatus> ReadMailAsync(CancellationToken ct)
    {
        try
        {
            var result = await _smtpHealth.CheckAsync(ct).ConfigureAwait(false);
            return new MailStatus(Available: true, Reachable: result.Reachable, Reason: result.Reason);
        }
        catch
        {
            return MailStatus.Unavailable;
        }
    }

    /// <summary>
    /// The storage snapshot (the
    /// <see cref="IStorageMetricsService.GetSnapshotAsync"/> total used / free
    /// / as-of). A null snapshot or a thrown exception degrades to
    /// <see cref="StorageStatus.Unavailable"/>.
    /// </summary>
    private async Task<StorageStatus> ReadStorageAsync(CancellationToken ct)
    {
        try
        {
            var snap = await _storageMetrics.GetSnapshotAsync(ct).ConfigureAwait(false);
            if (snap is null) return StorageStatus.Unavailable;
            return new StorageStatus(
                Available: true,
                TotalUsedBytes: snap.TotalUsedBytes,
                FreeVolumeBytes: snap.FreeVolumeBytes,
                AsOf: snap.AsOf);
        }
        catch
        {
            return StorageStatus.Unavailable;
        }
    }

    /// <summary>
    /// The mail-alarm T4 state (the F18 gate): delegates to the
    /// <see cref="IMailAlarmStateReader"/> seam (which reuses the
    /// <see cref="Kumunita.Core.Identity.IEmailDeadLetterCounter"/> dead-letter
    /// read + the
    /// <see cref="Kumunita.Core.Notifications.MailAlarmThresholdPolicy"/>
    /// firing decision + the window's
    /// <see cref="Kumunita.Core.Notifications.SuppressedAddress"/> bounce /
    /// complaint count — the
    /// <see cref="Kumunita.Core.Notifications.MailAlarmsService"/> T4 query
    /// shape). A thrown exception degrades to
    /// <see cref="MailAlarmStatus.Unavailable"/>.
    /// </summary>
    private async Task<MailAlarmStatus> ReadMailAlarmAsync(CancellationToken ct)
    {
        try
        {
            var counts = await _mailAlarmReader.ReadAsync(_now(), ct).ConfigureAwait(false);
            return new MailAlarmStatus(
                Available: true,
                Bounces: counts.Bounces,
                Complaints: counts.Complaints,
                DeadLetters: counts.DeadLetters,
                Firing: counts.Firing);
        }
        catch
        {
            return MailAlarmStatus.Unavailable;
        }
    }

    /// <summary>
    /// The "GlobalAdmin exists" fact (the
    /// <see cref="Kumunita.Core.Identity.DeletionScheduler"/> /
    /// <see cref="Kumunita.Core.Bootstrap.FirstBootSeeder"/>
    /// <c>GetUsersInRoleAsync(Roles.GlobalAdmin)</c> precedent). A thrown
    /// exception degrades to <c>false</c> (a fresh / no-admin instance — the
    /// <see cref="ConfigWarning.Keys.SeedAdminStillSet"/> rule stays quiet,
    /// the conservative choice for a degraded read).
    /// </summary>
    private async Task<bool> ReadAdminExistsAsync(CancellationToken ct)
    {
        try
        {
            return (await _userManager.GetUsersInRoleAsync(Roles.GlobalAdmin).ConfigureAwait(false)).Any();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// The running assembly's version + build SHA (the
    /// <see cref="Kumunita.Web.Controllers.HealthController"/>
    /// <c>build</c> field precedent — <c>Environment.GetEnvironmentVariable("SOURCE_COMMIT")</c>
    /// ?? <c>"local"</c>, joined to the assembly version). The build SHA is a
    /// git commit hash, not a secret (already GlobalAdmin-visible in the full
    /// /health payload).
    /// </summary>
    private static string ReadVersion()
    {
        var build = Environment.GetEnvironmentVariable("SOURCE_COMMIT") ?? "local";
        var assemblyVersion = typeof(InstanceStatusService).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
        return $"{assemblyVersion} ({build})";
    }
}
