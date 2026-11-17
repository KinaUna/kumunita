namespace Kumunita.Core.Usage;

/// <summary>
/// The **pure** M25 decision (C-UP·3). <see cref="Decide"/> is size-first
/// (C-UP·2 ordering). <c>0</c> for the effective max = **unlimited** file
/// size; <c>0</c> for the quota = **unlimited** quota (C-UP·5). No HTTP types,
/// no <c>IFormFile</c>, no session (C-UP·3 / ADR 0006-D) — the Web gate (U8)
/// is the only place a <c>413</c> is produced.
/// </summary>
public static class StorageLimits
{
    /// <summary>
    /// <c>settings.MaxFileBytes ?? envMaxBytes</c>; <c>0</c> = unlimited file
    /// size (C-UP·5). The admin override (F4) wins over the env fallback (F6).
    /// </summary>
    public static long EffectiveMaxFileBytes(CommunityStorageSettings settings,
        long envMaxBytes) => settings.MaxFileBytes ?? envMaxBytes;

    /// <summary>
    /// IMPROVE-UX A15 — the **effective** per-user quota for a resident:
    /// <c>override ?? settings.PerUserQuotaBytes</c>. A <c>null</c> (unset)
    /// override falls back to the global quota; an override of <c>0</c> =
    /// unlimited **for that resident** (the C-UP·5 sentinel preserved per
    /// resident, distinct from the unset <c>null</c>); an override <c>&gt; 0</c>
    /// = that resident's cap, winning over the global. Pure — no DB, no session
    /// (C-UP·3). The per-file cap (<see cref="EffectiveMaxFileBytes"/>) is
    /// independent and untouched.
    /// </summary>
    public static long EffectivePerUserQuota(long? perUserQuotaOverride,
        CommunityStorageSettings settings)
        => perUserQuotaOverride ?? settings.PerUserQuotaBytes;

    /// <summary>
    /// Size checked **first** (C-UP·2 ordering — test 10): over the effective
    /// max → <see cref="StorageDecision.Oversize"/> (F2); else over the
    /// per-user quota → <see cref="StorageDecision.OverQuota"/> (F3); else
    /// <see cref="StorageDecision.Allowed"/> (F1).
    /// </summary>
    public static StorageDecision Decide(long incomingBytes, long currentUsageBytes,
        CommunityStorageSettings settings, long envMaxBytes)
        => Decide(incomingBytes, currentUsageBytes, settings, envMaxBytes,
            perUserQuotaOverride: null);

    /// <summary>
    /// IMPROVE-UX A15 — the same pure decision with an explicit **per-resident
    /// quota override** (the <see cref="EffectivePerUserQuota"/> resolution):
    /// <paramref name="perUserQuotaOverride"/> <c>null</c> ⇒ the global
    /// <see cref="CommunityStorageSettings.PerUserQuotaBytes"/>; <c>0</c> ⇒
    /// unlimited for that resident; <c>&gt; 0</c> ⇒ that resident's cap. The
    /// per-file cap (<see cref="EffectiveMaxFileBytes"/>) is untouched. This is
    /// the single place the <c>override ?? global</c> effective quota is folded
    /// into the decision (the loop's heart, resolved once in the upload gate).
    /// </summary>
    public static StorageDecision Decide(long incomingBytes, long currentUsageBytes,
        CommunityStorageSettings settings, long envMaxBytes, long? perUserQuotaOverride)
    {
        var effectiveMax = EffectiveMaxFileBytes(settings, envMaxBytes);
        if (effectiveMax > 0 && incomingBytes > effectiveMax)
            return StorageDecision.Oversize;

        // IMPROVE-UX A15 — resolve the effective per-user quota (override ??
        // global) **before** the quota check; a 0 effective quota = unlimited
        // (the C-UP·5 sentinel), exactly as the global quota already reads.
        var effectiveQuota = EffectivePerUserQuota(perUserQuotaOverride, settings);
        if (effectiveQuota > 0
            && currentUsageBytes + incomingBytes > effectiveQuota)
            return StorageDecision.OverQuota;
        return StorageDecision.Allowed;
    }
}
