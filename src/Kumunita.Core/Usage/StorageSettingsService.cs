using Marten;

namespace Kumunita.Core.Usage;

/// <summary>
/// The <see cref="IStorageSettingsService"/> impl (U4). HTTP-free (C-UP·3 /
/// ADR 0006-D). <see cref="GetOrCreateAsync"/> is the read + create-if-missing
/// (sentinel defaults, C-UP·5); <see cref="SetAsync"/> is the **single admin**
/// write lane — one in-caller-session write (C-UP·1, the C3 same-transaction
/// lane); <see cref="GetPerUserUsageBytesAsync"/> **reuses** the M24 C-SM·7
/// seam (<see cref="IStorageMetricsService.GetPerUserUsageBytesAsync"/>) rather
/// than re-implementing the query (design §2.6 drift-guard); <see cref="Decide"/>
/// is the **pure** decision (C-UP·3), delegating to
/// <see cref="StorageLimits.Decide"/> (size-first, C-UP·2 ordering).
/// </summary>
public sealed class StorageSettingsService : IStorageSettingsService
{
    private readonly IDocumentStore _store;
    private readonly IStorageMetricsService _metrics;

    public StorageSettingsService(IDocumentStore store, IStorageMetricsService metrics)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    /// <inheritdoc/>
    public async Task<CommunityStorageSettings> GetOrCreateAsync(CancellationToken ct)
    {
        const string id = "community";

        // Fast path: read (a QuerySession read).
        await using (var q = _store.QuerySession())
        {
            var existing = await q.LoadAsync<CommunityStorageSettings>(id, ct);
            if (existing is not null)
                return existing;
        }

        // Create-if-missing with sentinel defaults (C-UP·5): MaxFileBytes null
        // ⇒ env fallback; PerUserQuotaBytes 0 ⇒ unlimited.
        var settings = new CommunityStorageSettings
        {
            Id = id,
            MaxFileBytes = null,
            PerUserQuotaBytes = 0,
            Modified = DateTimeOffset.UtcNow,
            ModifiedById = null
        };

        await using var w = _store.LightweightSession();
        w.Store(settings);
        await w.SaveChangesAsync(ct);
        return settings;
    }

    /// <inheritdoc/>
    public async Task SetAsync(long? maxFileBytes, long perUserQuotaBytes, string actorId,
        IDocumentSession session)
    {
        // One in-caller-session write (C-UP·1 / C3 same-transaction lane): load-or-create
        // the single community doc, stamp the admin values + actor, commit in the
        // caller's session.
        const string id = "community";
        var settings = await session.LoadAsync<CommunityStorageSettings>(id)
            ?? new CommunityStorageSettings { Id = id };
        settings.MaxFileBytes = maxFileBytes;
        settings.PerUserQuotaBytes = perUserQuotaBytes;
        settings.Modified = DateTimeOffset.UtcNow;
        settings.ModifiedById = actorId;
        session.Store(settings);
        await session.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public Task<long> GetPerUserUsageBytesAsync(string subjectId, CancellationToken ct)
    {
        // C-SM·7 seam reuse (design §2.6 drift-guard): delegate to the M24
        // IStorageMetricsService read (Σ SizeBytes WHERE CreatedById == subjectId)
        // — not a second copy of the query.
        return _metrics.GetPerUserUsageBytesAsync(subjectId, ct);
    }

    // ── IMPROVE-UX A15 — the per-resident quota override (admin half) ─────────
    // Stored on the single settings row (ResidentQuotaOverrides map, keyed by
    // subjectId) — a map, not a new doc type (a new registered type would break
    // the IS20h registered-doc-type count pin). Sentinel (C-UP·5) per resident:
    // absent ⇒ fall back to the global; 0 ⇒ unlimited for that resident; >0 ⇒
    // that resident's cap. The write lanes keep the C-UP·1 single in-caller-
    // session write (the SetAsync precedent); standing is gated at the Web
    // boundary (ADR 0050 split — Core does not re-check it).

    /// <inheritdoc/>
    public async Task<long?> GetResidentQuotaOverrideAsync(string subjectId, CancellationToken ct)
    {
        // Fast-path read (a QuerySession read, the GetOrCreateAsync shape):
        // the override lives on the single settings row, keyed by subjectId.
        // An absent row or an absent key is both read as "unset" (null) — the
        // override ?? global fallback signal.
        await using var q = _store.QuerySession();
        var settings = await q.LoadAsync<CommunityStorageSettings>("community", ct);
        if (settings is null
            || settings.ResidentQuotaOverrides is null
            || !settings.ResidentQuotaOverrides.TryGetValue(subjectId, out var quotaBytes))
            return null;
        return quotaBytes;
    }

    /// <inheritdoc/>
    public async Task SetResidentQuotaOverrideAsync(string subjectId, long? quotaBytes,
        string actorId, IDocumentSession session)
    {
        // One in-caller-session write (C-UP·1 / C3 same-transaction lane, the
        // SetAsync precedent): load-or-create the single community row, set or
        // clear the resident's override on its map, stamp the admin actor,
        // commit in the caller's session. Null = clear (remove the key → back
        // to the global); 0 = unlimited for that resident; >0 = the cap.
        const string id = "community";
        var settings = await session.LoadAsync<CommunityStorageSettings>(id)
            ?? new CommunityStorageSettings { Id = id };
        if (settings.ResidentQuotaOverrides is null)
            settings.ResidentQuotaOverrides = new Dictionary<string, long>();
        if (quotaBytes is null)
            settings.ResidentQuotaOverrides.Remove(subjectId);   // clear → global
        else
            settings.ResidentQuotaOverrides[subjectId] = quotaBytes.Value;
        settings.Modified = DateTimeOffset.UtcNow;
        settings.ModifiedById = actorId;
        session.Store(settings);
        await session.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task SetResidentQuotaOverrideAsync(string subjectId, long? quotaBytes,
        string actorId)
    {
        // The session-based SetResidentQuotaOverrideAsync commits on its own
        // (C-UP·1), so this wrapper opens the session and delegates — no second
        // SaveChangesAsync (the committed SetAsync C09 precedent).
        await using var session = _store.LightweightSession();
        await SetResidentQuotaOverrideAsync(subjectId, quotaBytes, actorId, session).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public StorageDecision Decide(long incomingBytes, long currentUsageBytes,
        CommunityStorageSettings settings, long envMaxBytes)
        => StorageLimits.Decide(incomingBytes, currentUsageBytes, settings, envMaxBytes);

    /// <inheritdoc cref="IStorageSettingsService.SetAsync(long?, long, string)"/>
    /// <remarks>
    /// The session-based <see cref="SetAsync"/> commits on its own (C-UP·1), so
    /// this wrapper opens the session and delegates — no second
    /// <c>SaveChangesAsync</c>.
    /// </remarks>
    public async Task SetAsync(long? maxFileBytes, long perUserQuotaBytes, string actorId)
    {
        await using var session = _store.LightweightSession();
        await SetAsync(maxFileBytes, perUserQuotaBytes, actorId, session).ConfigureAwait(false);
    }
}
