namespace Kumunita.Core.Usage;

/// <summary>
/// The single community storage-settings doc (C-UP·1). Stable string
/// <c>Id = "community"</c> — one row per community (single-neighborhood
/// platform, ADR 0002). Sentinel semantics (C-UP·5): <see cref="MaxFileBytes"/>
/// <c>null</c> ⇒ env <c>Media__MaxBytes</c> fallback; <see
/// cref="PerUserQuotaBytes"/> <c>0</c> ⇒ unlimited.
/// </summary>
public sealed class CommunityStorageSettings
{
    public string Id { get; set; } = "community";

    /// <summary>Admin per-file override in bytes. <c>null</c>/absent ⇒ env
    /// fallback (C-UP·1/5).</summary>
    public long? MaxFileBytes { get; set; }

    /// <summary>Per-user total content quota in bytes. <c>0</c> = unlimited
    /// (C-UP·5).</summary>
    public long PerUserQuotaBytes { get; set; }

    public DateTimeOffset Modified { get; set; }
    public string? ModifiedById { get; set; }
}
