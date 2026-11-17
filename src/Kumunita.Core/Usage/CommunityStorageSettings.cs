using Kumunita.Core.Privacy;

namespace Kumunita.Core.Usage;

/// <summary>
/// The single community storage-settings doc (C-UP·1). Stable string
/// <c>Id = "community"</c> — one row per community (single-neighborhood
/// platform, ADR 0002). Sentinel semantics (C-UP·5): <see cref="MaxFileBytes"/>
/// <c>null</c> ⇒ env <c>Media__MaxBytes</c> fallback; <see
/// cref="PerUserQuotaBytes"/> <c>0</c> ⇒ unlimited.
/// <para>
/// IMPROVE-UX A15 adds <see cref="ResidentQuotaOverrides"/> — a per-resident
/// override on top of the single <see cref="PerUserQuotaBytes"/> global. The
/// override is stored **on this row** (a map, not a new doc type — a new
/// registered type would break the IS20h registered-doc-type count pin) and
/// carries the **same** C-UP·5 sentinel per resident: absent/removed ⇒ fall
/// back to the global; <c>0</c> ⇒ unlimited for that resident; <c>&gt; 0</c> ⇒
/// that resident's cap. Only an admin writes it (the ADR 0050 split — Web
/// gates the standing); a resident only reads their effective quota.
/// </para>
/// </summary>
[DataClass(DataClassKind.Operational, Erasure.Keep, ExportScope.Never,
    reason: "the community's storage cap; no resident principal")]
public sealed class CommunityStorageSettings
{
    public string Id { get; set; } = "community";

    /// <summary>Admin per-file override in bytes. <c>null</c>/absent ⇒ env
    /// fallback (C-UP·1/5).</summary>
    public long? MaxFileBytes { get; set; }

    /// <summary>Per-user total content quota in bytes. <c>0</c> = unlimited
    /// (C-UP·5).</summary>
    public long PerUserQuotaBytes { get; set; }

    /// <summary>
    /// IMPROVE-UX A15 — per-resident per-user-quota overrides. Key = the
    /// resident's <c>subjectId</c>, value = the override in bytes. <b>Absent</b>
    /// (no key) ⇒ the resident uses the global <see
    /// cref="PerUserQuotaBytes"/>; <c>0</c> ⇒ unlimited for that resident;
    /// <c>&gt; 0</c> ⇒ that resident's cap (the C-UP·5 sentinel preserved
    /// per-resident). The upload gate resolves <c>override ?? global</c>
    /// (<see cref="StorageLimits.EffectivePerUserQuota"/>). Admin-written only
    /// (ADR 0050); never a resident principal's own row (the doc is an
    /// operational singleton, <see cref="Erasure.Keep"/>).
    /// </summary>
    public Dictionary<string, long> ResidentQuotaOverrides { get; set; } = new();

    public DateTimeOffset Modified { get; set; }
    public string? ModifiedById { get; set; }
}
