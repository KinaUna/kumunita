using Marten;

namespace Kumunita.Core.Authorization;

/// <summary>
/// Tiered retention for <see cref="AccessAudit"/> rows (ARCHITECTURE.md §5
/// "Audition retention &amp; scope"; the job's expiry decision is <em>configured,
/// not improvised</em> per §6.4 / the design doc's "purge decision ... is set in
/// the job's config").
/// <para>
/// The purge job is the <b>only writer that deletes audit rows</b>, and deletion
/// is itself logged: each run appends one <see cref="AuditPurgeSummary"/>
/// (count, cutoff, at) in the <em>same</em> transaction as the deletion — audit-of-
/// the-audit, so "how many rows were expired when" is always traceable forward.
/// </para>
/// <para>
/// <b>Tiers (on <see cref="AccessVia"/>):</b>
/// <list type="bullet">
/// <item><b>Routine — expires at <c>now − RoutineDays</c> (default ~90 d):</b>
/// <see cref="AccessVia.Owner"/>, <see cref="AccessVia.Audience"/>,
/// <see cref="AccessVia.Delegation"/> — the everyday Allow/Deny of restricted
/// content the log exists to answer "what did this person see."</item>
/// <item><b>Standing — kept indefinitely:</b>
/// <see cref="AccessVia.Moderator"/>, <see cref="AccessVia.Admin"/>,
/// <see cref="AccessVia.BreakGlass"/>, <see cref="AccessVia.Report"/> — the
/// rows the design doc keeps "until the report resolves (+90 days)"; in M1 there
/// are no <see cref="AccessVia.Report"/> rows yet (reports land in M3), so the
/// <see cref="AuditPurgeOptions.UnresolvedReportDays"/> window is a no-op then,
/// but the split is on the <see cref="AccessVia"/> value so the standing set is
/// preserved the day reports arrive.</item>
/// </list>
/// </para>
/// </summary>
public sealed class AuditPurgeOptions
{
    /// <summary>Configuration section name (bound by the host, e.g. <c>AuditPurge__RoutineDays</c>).</summary>
    public const string SectionName = "AuditPurge";

    /// <summary>Routine rows expire once older than this many days (default ~90, per §5 "after ~90 days").</summary>
    public int RoutineDays { get; set; } = 90;

    /// <summary>
    /// Report-attached + standing rows are kept until the report resolves, then a
    /// further <see cref="UnresolvedReportDays"/> (default is a conservative 1 year).
    /// In M1 there are no report rows yet, so this is a documentation knob, not a
    /// live expiry — the tier split is on <see cref="AccessVia"/> and Report rows
    /// ride the standing tier.
    /// </summary>
    public int UnresolvedReportDays { get; set; } = 365;
}

/// <summary>
/// The single delete/summary writer for <see cref="AccessAudit"/> rows
/// (ARCHITECTURE.md §5: "the purge job is the *only* writer that deletes audit
/// rows"). Wolverine-free: the host's scheduled job (a self-rescheduling
/// <c>TimeoutMessage</c>, §6.4) hands a live <see cref="IDocumentSession"/> and
/// this does the query + delete + summary-append in <em>one</em> session so the
/// deletion and its own summary row commit atomically. Extracted from the host
/// so the tiering and the summary shape are testable without a message host.
/// </summary>
public static class AuditPurgeService
{
    /// <summary>Routine-tier values; rows with an older <see cref="AccessAudit.At"/> than the cutoff are deleted.</summary>
    private static readonly AccessVia[] Routine =
    {
        AccessVia.Owner,
        AccessVia.Audience,
        AccessVia.Delegation
    };

    /// <summary>Standing-tier values; never deleted by <see cref="PurgeAsync"/>.</summary>
    private static readonly AccessVia[] Standing =
    {
        AccessVia.Moderator,
        AccessVia.Admin,
        AccessVia.BreakGlass,
        AccessVia.Report
    };

    /// <summary>
    /// Expire routine <see cref="AccessAudit"/> rows older than the configured
    /// cutoff; standing rows are untouched. Appends one
    /// <see cref="AuditPurgeSummary"/> for the run and commits both in the same
    /// session (deletion + its own audit commit together — §5's only-writer).
    /// </summary>
    /// <param name="store">The shared document store (the host injects the same <c>IDocumentStore</c> the domain services use; the two stores share one Postgres so there is no cross-store window).</param>
    /// <param name="options">The retention tiers (per-instance config, §6.4 — not improvised).</param>
    /// <param name="now">Injection point for tests (pin the "now" so the ~90-day tier boundary is deterministic).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The appended <see cref="AuditPurgeSummary"/> (count deleted, cutoff, at).</returns>
    public static async Task<AuditPurgeSummary> PurgeAsync(
        IDocumentStore store,
        AuditPurgeOptions options,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        if (store is null) throw new ArgumentNullException(nameof(store));
        if (options is null) throw new ArgumentNullException(nameof(options));

        var cutoff = now.AddDays(-Math.Max(0, options.RoutineDays));

        AuditPurgeSummary summary;
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        {
            // Routine tier only: the standing set (Moderator/Admin/BreakGlass/Report)
            // is deliberately NOT in this clause — §5 "all moderator/admin-access
            // rows are kept indefinitely until the report resolves (+90 days)".
            var idsToPurge = (await session.Query<AccessAudit>()
                                           .Where(a =>
                                               a.At < cutoff &&
                                               (a.Via == AccessVia.Owner ||
                                                a.Via == AccessVia.Audience ||
                                                a.Via == AccessVia.Delegation))
                                           .Select(a => a.Id)
                                           .ToListAsync())
                                .Distinct()
                                .ToList();

            // Stage each deletion on the same session so the summary Store below
            // commits in the *same Postgres transaction* as its own deletion — the
            // "audit-of-the-audit" guarantee (§5: the purge job is the only writer
            // that deletes, and deletion is itself logged). Per-row Delete<T>(id)
            // matches the repo's existing idiom (UserInfoService.RemoveGroupMember,
            // IdentityService.SetRole demotion), no bulk-delete API needed.
            foreach (var id in idsToPurge)
                session.Delete<AccessAudit>(id);
            var deleted = (long)idsToPurge.Count;

            summary = new AuditPurgeSummary
            {
                Id = Guid.NewGuid().ToString("N"),
                At = now,
                Count = deleted,
                Cutoff = cutoff
            };
            session.Store(summary);
            await session.SaveChangesAsync();
        }

        return summary;
    }
}
