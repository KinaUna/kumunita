namespace Kumunita.Core.Usage;

/// <summary>
/// The M34 history read result (ADR 0162, M34·1 — the 2-member ceiling).
/// Mirrors the M33 <see cref="StorageHistoryResult"/> wrapper shape
/// verbatim (the M13 <see cref="UsageAnalyticsResult.WindowDays"/>
/// precedent for <see cref="WindowDays"/>).
/// <para>
/// <see cref="WindowDays"/> echoes the pinned window the caller passed (so
/// the view can label the section without re-deriving it).
/// <see cref="Points"/> is the <see cref="UsageDailyTotal"/> rows
/// **ascending** by <see cref="UsageDailyTotal.Date"/> — **only the days
/// present** (no fabricated zero rows, the M34·7 / M33-9 FACE pin).
/// </para>
/// <para>
/// This is **not** a Marten document (M34·1 derived, not captured — the M13
/// <see cref="UsageEvent"/> doc is **reused** by the read, not re-added).
/// The read emits **zero** <c>AccessAudit</c> rows (M34·4).
/// </para>
/// </summary>
public sealed record UsageHistoryResult(int WindowDays,
    IReadOnlyList<UsageDailyTotal> Points);
