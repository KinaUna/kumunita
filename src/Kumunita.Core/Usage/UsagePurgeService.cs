using Marten;

namespace Kumunita.Core.Usage;

/// <summary>
/// The M13 usage-row retention (ADR 0114 D5). A <b>platform constant</b>,
/// not a config knob (the <see cref="Kumunita.Core.Authorization.AuditPurgeService"/>
/// "the cutoff is per-instance config, not improvised" principle
/// <b>inverted</b> for M13: the usage retention has no per-instance reason to
/// vary — the operator who wants a different value edits the constant and
/// redeploys; the per-instance knob is a named deferral, §deferred lanes).
/// The shape mirrors <see cref="Kumunita.Core.Authorization.AuditPurgeService
/// .PurgeAsync"/>: a batched id-collection + delete in <em>one</em> session,
/// no per-row <c>SaveChangesAsync</c>. There is <b>no</b> tier (the usage rows
/// have a single retention, unlike the <see cref="Kumunita.Core.Authorization
/// .AccessAudit"/> tiered retention); there is <b>no</b>
/// <see cref="Kumunita.Core.Authorization.AuditPurgeSummary"/> row (the
/// deletion is the sink's own housekeeping, not a domain write — the
/// <see cref="Kumunita.Core.Logging.RollingFileSink.Retain"/> "no summary"
/// shape).
/// </summary>
public static class UsagePurgeService
{
    /// <summary>The M13 usage-row retention: 365 days (the ADR 0114 D5 constant).</summary>
    public const int RetentionDays = 365;

    /// <summary>
    /// Deletes <see cref="UsageEvent"/> rows older than
    /// <see cref="RetentionDays"/> days from <paramref name="now"/>, in one
    /// session (batched id-collection + delete, the
    /// <see cref="Kumunita.Core.Authorization.AuditPurgeService"/> shape).
    /// No summary row (the D5 "no tier, no summary" inversion).
    /// </summary>
    /// <param name="store">The shared document store (the host injects the same <c>IDocumentStore</c> the domain services use).</param>
    /// <param name="now">Injection point for tests (pin "now" so the 365-day boundary is deterministic).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The number of rows deleted.</returns>
    public static async Task<int> PurgeAsync(
        IDocumentStore store,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var cutoff = now.AddDays(-RetentionDays);

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        {
            // Batched id-collection + delete in ONE session (the
            // AuditPurgeService house shape): one SaveChangesAsync for the
            // whole batch, never a per-row round-trip.
            var ids = (await session.Query<UsageEvent>()
                                      .Where(e => e.At < cutoff)
                                      .Select(e => e.Id)
                                      .ToListAsync(ct))
                        .Distinct().ToList();
            foreach (var id in ids) session.Delete<UsageEvent>(id);
            await session.SaveChangesAsync(ct);
            return ids.Count;
        }
    }
}
