namespace Kumunita.Core.Usage;

/// <summary>
/// The outcome of a pure upload decision (C-UP·2/3).
/// <see cref="Allowed"/> → F1 (within both limits); <see cref="Oversize"/> →
/// F2 (over the effective per-file max); <see cref="OverQuota"/> → F3 (over
/// the per-user quota). The Web gate (<c>IUploadGate</c>, U8) is the only
/// place any of these becomes a <c>413</c> — Core never sees an
/// <c>IActionResult</c> (C-UP·3 / ADR 0006-D).
/// </summary>
public enum StorageDecision { Allowed, Oversize, OverQuota }

/// <summary>
/// The M25 settings seam (C-UP·1/3/4). <see cref="SetAsync"/> is the **single
/// admin** write lane — one write in the caller's session (C-UP·1),
/// GlobalAdmin-gated in Web. <see cref="GetPerUserUsageBytesAsync"/> is the
/// C-UP·4 read and **reuses the M24 C-SM·7 handoff seam** (design §2.6) — it
/// is not re-implemented. <see cref="Decide"/> is the **pure** decision
/// (C-UP·3). HTTP-free (C-UP·3 / ADR 0006-D): no <c>IFormFile</c>, no
/// <c>Stream</c>, no <c>ActionResult</c>.
/// </summary>
public interface IStorageSettingsService
{
    /// <summary>
    /// Read the community settings; **create-if-missing** with sentinel
    /// defaults (<c>MaxFileBytes = null</c>, <c>PerUserQuotaBytes = 0</c>).
    /// </summary>
    Task<CommunityStorageSettings> GetOrCreateAsync(CancellationToken ct);

    /// <summary>
    /// The single **admin** write lane: set the per-file override + the
    /// per-user quota, stamped with the actor, in the **caller's** session
    /// (C-UP·1: one in-caller-session write — the C3 same-transaction lane).
    /// </summary>
    Task SetAsync(long? maxFileBytes, long perUserQuotaBytes, string actorId,
        Marten.IDocumentSession session);

    /// <summary>
    /// The C-UP·4 read: <c>Σ MediaObject.SizeBytes WHERE CreatedById ==
    /// subjectId</c>. **Reuses** the M24 <see cref="IStorageMetricsService"/>
    /// C-SM·7 handoff seam of the identical signature (design §2.6); do **not**
    /// duplicate the query.
    /// </summary>
    Task<long> GetPerUserUsageBytesAsync(string subjectId, CancellationToken ct);

    /// <summary>
    /// The **pure** decision (C-UP·3: no HTTP, no session). Delegates to
    /// <see cref="StorageLimits.Decide"/> (size-first, C-UP·2 ordering).
    /// </summary>
    StorageDecision Decide(long incomingBytes, long currentUsageBytes,
        CommunityStorageSettings settings, long envMaxBytes);
}
