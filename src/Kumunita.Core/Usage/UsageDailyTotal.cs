namespace Kumunita.Core.Usage;

/// <summary>
/// One per-day usage total (M34, ADR 0162, M34·1 — the 2-member ceiling).
/// Carries <b>exactly</b> <see cref="Date"/> + <see cref="Total"/> — no
/// field outside the set may appear (drift-guard §2.6).
/// <para>
/// <see cref="Date"/> is the <see cref="UsageEvent.At"/>'s UTC date (the
/// M13 <see cref="UsageCsvRow.Date"/> precedent). <see cref="Total"/> is the
/// count of <see cref="UsageEvent"/> rows whose <c>At</c> falls on that UTC
/// day (the M13 <see cref="UsageCsvRow.Total"/> /
/// <see cref="SurfaceRow.Total"/> count precedent). An aggregate — never a
/// per-account row (C-M13·3).
/// </para>
/// <para>
/// This POCO is **not** an <c>IAuditableResource</c> (the M13 C-M13·6 "the
/// <c>UsageEvent</c> row is not an auditable resource" precedent; the read
/// emits **zero** <c>AccessAudit</c> rows, M34·4). It is **not** a Marten
/// document (there is **no** doc-type surface to register — M34·1 derived,
/// not captured).
/// </para>
/// </summary>
public sealed class UsageDailyTotal
{
    /// <summary>The UTC day — the <see cref="UsageEvent.At"/>'s UTC date (the M13 <see cref="UsageCsvRow.Date"/> precedent).</summary>
    public DateTimeOffset Date  { get; init; }

    /// <summary>The count of <see cref="UsageEvent"/> rows whose <c>At</c> falls on that UTC day (the M13 <see cref="UsageCsvRow.Total"/> / <see cref="SurfaceRow.Total"/> count precedent). An aggregate — never a per-account row (C-M13·3).</summary>
    public int            Total { get; init; }
}
