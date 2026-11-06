namespace Kumunita.Core.Usage;

/// <summary>
/// The M13 usage-row retention (ADR 0114 D5) **decision** surface, extracted
/// from the <see cref="UsagePurgeService"/> I/O method as a pure, store-free
/// <c>*Policy</c> static (the C04 / C2-06 decision-extraction shape — the
/// <see cref="Kumunita.Core.Logging.RollingFileSink.Retain"/> precedent: a
/// pure static over the <c>now</c> + retention constant that a plain unit
/// test names without a store).
/// <para>
/// The <b>decision</b>: given <c>now</c> + the
/// <see cref="UsagePurgeService.RetentionDays"/> platform constant, the
/// retention <b>cutoff</b> instant (<c>now − RetentionDays</c>), and which
/// side of it a <see cref="UsageEvent"/> row falls on — a row is purged iff
/// <c>At &lt; cutoff</c> (strictly older than the cutoff; a row exactly at the
/// cutoff is kept). The I/O method
/// (<see cref="UsagePurgeService.PurgeAsync"/>) keeps the
/// <c>Query</c>/<c>Delete</c>/one-<c>SaveChangesAsync</c> commit (the
/// <see cref="Kumunita.Core.Authorization.AuditPurgeService"/> house shape) and
/// the batched id-collection; the behaviour is unchanged — only the
/// cutoff/comparison's location moves here, where a plain unit test
/// (<c>UsagePurgePolicyTests</c>) can name it without Postgres.
/// </para>
/// <para>
/// <b>Why the I/O query inlines <c>e.At &lt; cutoff</c> instead of calling
/// <see cref="ShouldPurge"/></b>: the predicate must translate to SQL (Marten
/// server-side <c>Where</c>), so the queryable form stays the literal
/// <c>e.At &lt; cutoff</c>; <see cref="ShouldPurge"/> is the same comparison
/// made pure over plain values (single source of the rule), named for the
/// unit test and the reader — they must never drift.
/// </para>
/// </summary>
public static class UsagePurgePolicy
{
    /// <summary>
    /// The M13 retention cutoff (ADR 0114 D5): <paramref name="now"/> minus
    /// <paramref name="retentionDays"/> (the
    /// <see cref="UsagePurgeService.RetentionDays"/> platform constant — the
    /// single source). Pure — no store, no I/O.
    /// </summary>
    /// <param name="now">The "now" instant (the I/O method's injection point for a deterministic boundary).</param>
    /// <param name="retentionDays">The retention in days — the <see cref="UsagePurgeService.RetentionDays"/> constant by default.</param>
    /// <returns>The cutoff instant; rows with <c>At &lt; cutoff</c> are purged.</returns>
    public static DateTimeOffset ComputeCutoff(
        DateTimeOffset now,
        int retentionDays = UsagePurgeService.RetentionDays)
        => now.AddDays(-retentionDays);

    /// <summary>
    /// The row-qualifies predicate (the M13 D5 "older than the cutoff" rule):
    /// a <see cref="UsageEvent"/> row is purged iff
    /// <c>rowAt &lt; cutoff</c> — strictly older than the cutoff. A row exactly
    /// at the cutoff is **kept**; one newer is **kept**. Pure over plain
    /// values — no store, no I/O. Mirrors the I/O query's inline
    /// <c>e.At &lt; cutoff</c> (the SQL-translatable form) — they must never
    /// drift.
    /// </summary>
    /// <param name="rowAt">The row's <c>At</c> (the capture instant, UTC).</param>
    /// <param name="cutoff">The retention cutoff instant (see <see cref="ComputeCutoff"/>).</param>
    /// <returns><c>true</c> when the row is purged (<c>rowAt &lt; cutoff</c>); otherwise <c>false</c> (kept).</returns>
    public static bool ShouldPurge(DateTimeOffset rowAt, DateTimeOffset cutoff)
        => rowAt < cutoff;
}
