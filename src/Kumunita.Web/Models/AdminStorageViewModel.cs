namespace Kumunita.Web.Models;

/// <summary>
/// The view model for <c>/admin/storage/settings</c> (M25, the storage
/// **settings** surface — the read-only display U5 ships; the form is U6).
/// A plain projection of the <see cref="Kumunita.Core.Usage.CommunityStorageSettings"/>
/// settings doc + the community total usage from M24's
/// <see cref="Kumunita.Core.Usage.StorageMetricsSnapshot"/> — Web-only shape
/// (no <c>IFormFile</c> / <c>ActionResult</c> / <c>HttpContext</c>; C-UP·3 /
/// ADR 0006-D keeps Core HTTP-free). Read-only: the surface emits zero
/// <c>AccessAudit</c> rows (C-UP·7).
/// <para>
/// The "effective" per-file limit is the admin override (<see
/// cref="MaxFileBytes"/>, <c>null</c> = env fallback) resolved against the
/// env default (<see cref="EnvMaxFileBytes"/>) — the exact
/// <c>settings.MaxFileBytes ?? envMaxBytes</c> expression the U8 gate and
/// <see cref="Kumunita.Core.Usage.StorageLimits.EffectiveMaxFileBytes"/>
/// compute, so the admin sees what the gate actually enforces.
/// </para>
/// </summary>
public class AdminStorageViewModel
{
    /// <summary>The admin per-file override in bytes. <c>null</c> = no override
    /// (the env fallback, <see cref="EnvMaxFileBytes"/>, is in force; C-UP·5).</summary>
    public long? MaxFileBytes { get; init; }

    /// <summary>The per-user total content quota in bytes. <c>0</c> = unlimited
    /// (C-UP·5).</summary>
    public long PerUserQuotaBytes { get; init; }

    /// <summary>The community total used (the <c>Σ SizeBytes</c>
    /// <see cref="Kumunita.Core.Usage.StorageMetricsSnapshot.TotalUsedBytes"/>
    /// from M24's metrics seam — C-UP·7).</summary>
    public long TotalUsedBytes { get; init; }

    /// <summary>The point-in-time of the total-used read (the M24 snapshot's
    /// <c>AsOf</c>).</summary>
    public DateTimeOffset AsOf { get; init; }

    /// <summary>The env per-file default (<see cref="Kumunita.Core.Media.MediaOptions.MaxBytes"/>
    /// — the same cap the four upload-lane guards use; the fallback when
    /// <see cref="MaxFileBytes"/> is <c>null</c>).</summary>
    public long EnvMaxFileBytes { get; init; }

    /// <summary>The **effective** per-file limit = <see cref="MaxFileBytes"/> ??
    /// <see cref="EnvMaxFileBytes"/> (C-UP·5; <see
    /// cref="Kumunita.Core.Usage.StorageLimits.EffectiveMaxFileBytes"/>).
    /// <c>0</c> = unlimited file size.</summary>
    public long EffectiveMaxFileBytes { get; init; }

    /// <summary>True when the per-user quota is <c>0</c> (unlimited, C-UP·5) —
    /// drives the view's "unlimited" label vs. a concrete quota figure.</summary>
    public bool QuotaUnlimited { get; init; }

    /// <summary>Form input for the per-file limit (raw string, bound from the
    /// <c>POST /admin/storage/settings</c> set-lane — U6). Value is in **MiB**,
    /// converted to bytes server-side before persisting. Blank = no override
    /// (the env fallback is in force). Pre-seeded from <see cref="MaxFileBytes"/>
    /// on the <c>GET</c> (converted to MiB).</summary>
    public string? MaxFileBytesInput { get; set; }

    /// <summary>Form input for the per-user quota (raw string, bound from the
    /// <c>POST /admin/storage/settings</c> set-lane — U6). Value is in **MiB**,
    /// converted to bytes server-side before persisting. Blank or <c>0</c> =
    /// unlimited (C-UP·5). Pre-seeded from <see cref="PerUserQuotaBytes"/>
    /// on the <c>GET</c> (blank when unlimited, otherwise converted to MiB).</summary>
    public string? PerUserQuotaBytesInput { get; set; }
}
