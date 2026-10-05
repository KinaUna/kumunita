namespace Kumunita.Web.Models;

/// <summary>
/// The resident's **own** storage-usage read (M25 U7 — the self-only resident
/// surface, C-UP·4/F7). A signed-in resident sees *their own* usage bytes, the
/// community per-user quota (or "unlimited"), and how much of their quota
/// remains. The subject is always the signed-in principal minted server-side
/// (never a path param), so a resident can never read another resident's
/// numbers. Read-only: the surface emits **no** <c>AccessAudit</c> row
/// (C-UP·7 — a resident's own usage is not an audience-restricted read).
/// <para>
/// Sentinel semantics (C-UP·5): the per-user quota <c>0</c> ⇒ <b>unlimited</b>
/// — shown as "Unlimited", <b>not</b> <c>0</c> remaining and <b>not</b> an
/// error. In that case <see cref="RemainingBytes"/> is <c>null</c> (the
/// "unlimited" marker) and <see cref="RemainingHuman"/> is "Unlimited".
/// </para>
/// Web-only shape (C-UP·3 / ADR 0006-D keeps Core HTTP-free): no
/// <c>IFormFile</c> / <c>ActionResult</c> / <c>HttpContext</c>.
/// </summary>
public class ResidentStorageViewModel
{
    /// <summary>The resident's own used bytes (<c>Σ SizeBytes WHERE
    /// CreatedById == subject</c> — the C-UP·4 read reusing the M24 C-SM·7
    /// seam). The subject is the signed-in principal.</summary>
    public long MyUsageBytes { get; init; }

    /// <summary>The community per-user quota in bytes. <c>0</c> = unlimited
    /// (C-UP·5).</summary>
    public long PerUserQuotaBytes { get; init; }

    /// <summary>The resident's remaining bytes = <c>quota − usage</c>
    /// (clamped to ≥ 0). <c>null</c> ⇒ unlimited (the quota is <c>0</c>,
    /// C-UP·5) — not <c>0</c> and not an error.</summary>
    public long? RemainingBytes { get; init; }

    /// <summary>True when the per-user quota is <c>0</c> (unlimited, C-UP·5) —
    /// drives the view's "Unlimited" label vs. a concrete quota figure.</summary>
    public bool QuotaUnlimited { get; init; }

    /// <summary>Human-readable usage (IEC units) of <see cref="MyUsageBytes"/>.</summary>
    public string MyUsageHuman { get; init; } = string.Empty;

    /// <summary>Human-readable quota of <see cref="PerUserQuotaBytes"/> —
    /// "Unlimited" when <see cref="QuotaUnlimited"/>.</summary>
    public string QuotaHuman { get; init; } = string.Empty;

    /// <summary>Human-readable remaining of <see cref="RemainingBytes"/> —
    /// "Unlimited" when <see cref="RemainingBytes"/> is <c>null</c>.</summary>
    public string RemainingHuman { get; init; } = string.Empty;

    /// <summary>A compact human byte formatter (IEC units, matching the admin
    /// settings view's <c>Bytes</c> helper). Pure — no kw-l dependency.</summary>
    public static string FormatBytes(long bytes)
    {
        string[] u = { "B", "KiB", "MiB", "GiB", "TiB", "PiB" };
        double n = bytes;
        int i = 0;
        while (n >= 1024 && i < u.Length - 1) { n /= 1024; i++; }
        return i == 0 ? $"{n:0} {u[i]}" : $"{n:0.##} {u[i]}";
    }
}
