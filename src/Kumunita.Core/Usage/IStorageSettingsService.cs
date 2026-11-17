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
    /// The committed settings-write lane (IMPROVE-CODE C09): the
    /// <see cref="SetAsync(long?, long, string, Marten.IDocumentSession)"/>
    /// semantics (the C-UP·1 single in-caller-session write) on a fresh
    /// <c>LightweightSession</c>. The session-based form commits on its own, so
    /// this wrapper opens the session and delegates. Lets the
    /// <c>AdminStorageController</c> drop the <c>IDocumentStore</c> dependency.
    /// </summary>
    Task SetAsync(long? maxFileBytes, long perUserQuotaBytes, string actorId);

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

    // ── IMPROVE-UX A15 — the per-resident quota override (admin half) ────────
    // A14's "request more space" nudge points at an override the admin sets;
    // this pair is that seam. The override lives on the **single** settings
    // row (<see cref="CommunityStorageSettings.ResidentQuotaOverrides"/>, keyed
    // by subjectId) — a map, not a new doc type (a new registered type would
    // break the IS20h registered-doc-type count pin). Sentinel (C-UP·5) is
    // preserved per-resident: <b>null</b> (unset) is distinct from <b>0</b>
    // (unlimited for that resident) — the <c>override ?? global</c> fallback
    // signal is the unset null.

    /// <summary>
    /// A15 read: the resident's per-user-quota override in bytes, or <c>null</c>
    /// when no override is set (the <c>override ?? global</c> fallback signal).
    /// <c>0</c> = unlimited for that resident (the C-UP·5 sentinel); <c>&gt;0</c>
    /// = that resident's cap. Read-only; a resident may read their own effective
    /// quota but never writes here (the admin write lane is
    /// <see cref="SetResidentQuotaOverrideAsync"/>).
    /// </summary>
    Task<long?> GetResidentQuotaOverrideAsync(string subjectId, CancellationToken ct);

    /// <summary>
    /// A15 **admin** write lane: set (or clear) the resident's per-user-quota
    /// override, stamped with the actor, in the **caller's** session (the C-UP·1
    /// single in-caller-session write — the <see cref="SetAsync(long?, long,
    /// string, Marten.IDocumentSession)"/> precedent). <paramref
    /// name="quotaBytes"/> <c>null</c> = **clear** the override (back to the
    /// global); <c>0</c> = unlimited for that resident; <c>&gt; 0</c> = the cap.
    /// Standing is gated at the Web boundary (ADR 0050 split — Core does not
    /// re-check it); <paramref name="actorId"/> is the signed-in admin.
    /// </summary>
    Task SetResidentQuotaOverrideAsync(string subjectId, long? quotaBytes,
        string actorId, Marten.IDocumentSession session);

    /// <summary>
    /// A15 committed write lane (the <see cref="SetAsync(long?, long, string)"/>
    /// C09 precedent): the <see
    /// cref="SetResidentQuotaOverrideAsync(string, long?, string,
    /// Marten.IDocumentSession)"/> semantics on a fresh <c>LightweightSession</c>
    /// (the session-based form commits on its own, so this wrapper opens the
    /// session and delegates). Lets the admin controller drop the
    /// <c>IDocumentStore</c> dependency.
    /// </summary>
    Task SetResidentQuotaOverrideAsync(string subjectId, long? quotaBytes,
        string actorId);
}
