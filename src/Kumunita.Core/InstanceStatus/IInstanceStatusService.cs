namespace Kumunita.Core.InstanceStatus;

/// <summary>
/// The operator's read-only instance-status seam (IMPROVE-UX O01, the
/// <c>/admin/status</c> page's data in O02, the dashboard notice's input in
/// O03, the inbox alarm's payload in O04). One method:
/// <see cref="GetStatusAsync"/> returning a pure <see cref="InstanceStatus"/>
/// POCO (counts, booleans, version, uptime, a warning list — no
/// <c>IActionResult</c>, no <c>IHttpContext</c>, ADR 0006-D — and
/// <b>never</b> a secret value: no credential, no token, no key path, no
/// connection string; the "no secret values" DoD).
/// <para>
/// <b>Resilient:</b> a failing sub-read (no DB, no SMTP, an un-registered
/// seam in a minimal harness) degrades that row to a "degraded /
/// unavailable" fact rather than throwing the whole read — the operator
/// still gets the rows that <em>are</em> available (the
/// <c>HealthController</c> degraded-probe posture).
/// </para>
/// </summary>
public interface IInstanceStatusService
{
    /// <summary>
    /// The one read: compose the <see cref="InstanceStatus"/> POCO from the
    /// reused seams (DB reachability, dead-letter count, mail-alarm T4
    /// state, SMTP relay, storage snapshot, GlobalAdmin count, config
    /// warnings). Each reused seam is called at most once; a failing
    /// sub-read degrades its section to <c>Available = false</c> and the
    /// read still returns.
    /// </summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>
    /// The composed <see cref="InstanceStatus"/> POCO — always a non-null
    /// result (a sub-read failure degrades the section, not the whole
    /// read).
    /// </returns>
    Task<InstanceStatus> GetStatusAsync(CancellationToken ct);
}
