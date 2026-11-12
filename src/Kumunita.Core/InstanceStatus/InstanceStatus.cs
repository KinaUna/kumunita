namespace Kumunita.Core.InstanceStatus;

/// <summary>
/// The operator's status read (the
/// <see cref="IInstanceStatusService.GetStatusAsync"/> result — the
/// <c>/admin/status</c> page's data in O02, the dashboard notice's input in
/// O03, the inbox alarm's payload in O04). A **pure POCO** — booleans,
/// integers, longs, a <see cref="DateTimeOffset"/> as-of, a
/// <see cref="TimeSpan"/> uptime, a version/build string, and a list of
/// <see cref="ConfigWarning"/> (stable key + secret-free reason). **No
/// <c>IActionResult</c>, no <c>IHttpContext</c>, no <c>IFormFile</c>, no
/// connection string, no SMTP password, no key path, no credential value**
/// (ADR 0006-D — Core stays HTTP-free; the "no secret values" DoD).
/// <para>
/// The POCO is composed from the **reused** seams (the
/// <see cref="Kumunita.Web.Controllers.HealthController"/> /health liveness
/// shape, the <see cref="Kumunita.Core.Identity.IEmailDeadLetterCounter"/>
/// dead-letter read, the <see cref="Kumunita.Core.Identity.ISmtpHealthCheck"/>
/// relay handshake, the
/// <see cref="Kumunita.Core.Notifications.MailAlarmThresholdPolicy"/> T4
/// decision, the <see cref="Kumunita.Core.Usage.IStorageMetricsService"/>
/// storage snapshot, the
/// <see cref="Microsoft.AspNetCore.Identity.UserManager{User}"/> GlobalAdmin
/// count, and the host-bound <see cref="InstanceStatusOptions"/> /
/// <see cref="Kumunita.Core.Identity.SeedAdminOptions"/> /
/// <see cref="Kumunita.Core.Identity.SampleDataOptions"/> /
/// <see cref="Kumunita.Core.Identity.HealthOptions"/> postures) — each called
/// once, not re-derived.
/// </para>
/// <para>
/// <b>Resilient degradation:</b> a failing sub-read degrades its section to
/// <c>Available = false</c> (the
/// <see cref="Kumunita.Core.AdminOnboarding.AdminOnboardingService"/>
/// missing-store idiom) and the read still returns — the operator gets the
/// rows that <em>are</em> available, not a thrown exception on the whole
/// read (the /health degraded-probe posture).
/// </para>
/// </summary>
public sealed record InstanceStatus
{
    /// <summary>When this read was taken (UTC).</summary>
    public required DateTimeOffset AsOf { get; init; }

    /// <summary>The running assembly's version + build SHA (the
    /// <c>Environment.GetEnvironmentVariable("SOURCE_COMMIT")</c> /
    /// <c>Assembly.GetExecutingAssembly().GetName().Version</c> read — the
    /// <c>HealthController</c> <c>build</c> field precedent). The build SHA is
    /// a git commit hash, not a secret (already GlobalAdmin-visible in the
    /// full /health payload).</summary>
    public required string Version { get; init; }

    /// <summary>Process uptime (a <see cref="TimeSpan"/> — the
    /// <c>now - _processStart</c> read, the
    /// <see cref="Kumunita.Core.Notifications.MailAlarmsService"/>
    /// <c>now</c>-injection precedent for test determinism).</summary>
    public required TimeSpan Uptime { get; init; }

    /// <summary>The database reachability fact (the
    /// <c>IDocumentStore.Storage.Database.AssertConnectivityAsync</c>
    /// liveness shape — <c>true</c> when the connection succeeds;
    /// <c>false</c> when it throws; <see cref="DatabaseReachable"/> itself
    /// is the <c>Available</c> flag for this row).</summary>
    public required bool DatabaseReachable { get; init; }

    /// <summary>The mail (SMTP relay) section — the
    /// <see cref="Kumunita.Core.Identity.ISmtpHealthCheck"/> handshake result
    /// (a failure is a degraded fact, not a throw — the /health posture).</summary>
    public required MailStatus Mail { get; init; }

    /// <summary>The mail-alarm section — the F18 T4 gate state (the
    /// <see cref="Kumunita.Core.Notifications.MailAlarmsService"/>
    /// window counts + the <see cref="Kumunita.Core.Notifications.MailAlarmThresholdPolicy"/>
    /// firing decision, the
    /// <see cref="Kumunita.Core.Identity.IEmailDeadLetterCounter"/> dead-letter
    /// count). The alarm *firing* is unchanged (O04 adds the inbox leg, not
    /// O01).</summary>
    public required MailAlarmStatus MailAlarm { get; init; }

    /// <summary>The storage section — the
    /// <see cref="Kumunita.Core.Usage.IStorageMetricsService.GetSnapshotAsync"/>
    /// total used / free / as-of (a failure is a degraded fact, not a throw).</summary>
    public required StorageStatus Storage { get; init; }

    /// <summary>The four config warnings (the pure
    /// <see cref="ConfigWarningsPolicy.Evaluate"/> result over the
    /// <see cref="ConfigWarningInputs"/> snapshot). An empty list means no
    /// warning; each entry carries a stable <see cref="ConfigWarning.Key"/>
    /// + a secret-free <see cref="ConfigWarning.Reason"/> (the "no secret
    /// values" DoD).</summary>
    public required IReadOnlyList<ConfigWarning> Warnings { get; init; }
}

/// <summary>
/// The mail (SMTP relay) section (the
/// <see cref="Kumunita.Core.Identity.ISmtpHealthCheck"/> handshake result —
/// <see cref="Reachable"/> + the failure <see cref="Reason"/> when not
/// reachable, the <see cref="Kumunita.Core.Identity.SmtpHealthResult"/>
/// shape). <see cref="Available"/> is <c>false</c> when the probe itself
/// failed (the seam was un-registered or threw) — a degraded row, not a
/// throw.
/// </summary>
public sealed record MailStatus(bool Available, bool Reachable, string? Reason)
{
    /// <summary>A degraded / unavailable mail section (the probe itself failed — not a throw).</summary>
    public static MailStatus Unavailable { get; } = new(Available: false, Reachable: false, Reason: null);
}

/// <summary>
/// The mail-alarm section (the F18 T4 gate state): the window's
/// bounce/complaint counts, the dead-letter count, and whether the alarm is
/// currently firing (the <see cref="Kumunita.Core.Notifications.MailAlarmThresholdPolicy"/>
/// decision — the counts + the threshold, never the suppressed e-mail
/// addresses). <see cref="Available"/> is <c>false</c> when the reads failed
/// (no DB, an un-registered seam) — a degraded row, not a throw.
/// </summary>
public sealed record MailAlarmStatus(
    bool Available,
    int Bounces,
    int Complaints,
    int DeadLetters,
    bool Firing)
{
    /// <summary>A degraded / unavailable mail-alarm section (the reads failed — not a throw).</summary>
    public static MailAlarmStatus Unavailable { get; } = new(Available: false, Bounces: 0, Complaints: 0, DeadLetters: 0, Firing: false);
}

/// <summary>
/// The storage section (the
/// <see cref="Kumunita.Core.Usage.IStorageMetricsService.GetSnapshotAsync"/>
/// total used / free / as-of). <see cref="Available"/> is <c>false</c> when
/// the snapshot failed (no store, an un-registered seam) — a degraded row,
/// not a throw.
/// </summary>
public sealed record StorageStatus(
    bool Available,
    long TotalUsedBytes,
    long FreeVolumeBytes,
    DateTimeOffset AsOf)
{
    /// <summary>A degraded / unavailable storage section (the snapshot failed — not a throw).</summary>
    public static StorageStatus Unavailable { get; } = new(Available: false, TotalUsedBytes: 0, FreeVolumeBytes: 0, AsOf: DateTimeOffset.MinValue);
}
