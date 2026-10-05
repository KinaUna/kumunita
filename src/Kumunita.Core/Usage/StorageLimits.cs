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
    /// Size checked **first** (C-UP·2 ordering — test 10): over the effective
    /// max → <see cref="StorageDecision.Oversize"/> (F2); else over the
    /// per-user quota → <see cref="StorageDecision.OverQuota"/> (F3); else
    /// <see cref="StorageDecision.Allowed"/> (F1).
    /// </summary>
    public static StorageDecision Decide(long incomingBytes, long currentUsageBytes,
        CommunityStorageSettings settings, long envMaxBytes)
    {
        var effectiveMax = EffectiveMaxFileBytes(settings, envMaxBytes);
        if (effectiveMax > 0 && incomingBytes > effectiveMax)
            return StorageDecision.Oversize;
        if (settings.PerUserQuotaBytes > 0
            && currentUsageBytes + incomingBytes > settings.PerUserQuotaBytes)
            return StorageDecision.OverQuota;
        return StorageDecision.Allowed;
    }
}
