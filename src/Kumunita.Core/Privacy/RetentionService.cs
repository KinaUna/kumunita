using Kumunita.Core.ErrorReports;
using Kumunita.Core.Identity;
using Kumunita.Core.Sessions;
using Marten;

namespace Kumunita.Core.Privacy;

/// <summary>
/// The IMPROVE-SEC D6 / E5 retention sweep, **part 1** (the I/O leg): deletes or
/// nulls the part-1 document types once they cross their retention cutoff. The
/// **decision** of *when* a row crosses a cutoff is the pure
/// <see cref="RetentionPolicy"/> (IS33) — this class only supplies the I/O and
/// must call <see cref="RetentionPolicy.CutoffFor"/> for every cutoff and never
/// re-derive it (the
/// <see cref="Kumunita.Core.Usage.UsagePurgeService"/> /
/// <see cref="Kumunita.Core.Usage.UsagePurgePolicy"/> split, applied here).
/// <para>
/// Part-1 types (E5 / D6):
/// <list type="bullet">
/// <item><see cref="Kumunita.Core.Sessions.SessionDoc"/> — a revoked session's
/// terminal field is <c>RevokedAt</c> (the only nullable field on the doc; a
/// session has no separate expiry), so a row is purged iff
/// <c>RevokedAt &lt; Sessions</c> cutoff. Active sessions (<c>RevokedAt</c>
/// null) are always kept.</item>
/// <item><see cref="Kumunita.Core.Identity.IdentityToken"/> — consumed or
/// expired beyond the <c>Tokens</c> cutoff.</item>
/// <item><see cref="Kumunita.Core.ErrorReports.EscalationToken"/> — revoked
/// beyond the <c>Tokens</c> cutoff.</item>
/// <item><see cref="Kumunita.Core.Identity.EmailDeadLetter"/> — beyond the
/// <c>DeadLetters</c> cutoff.</item>
/// <item><see cref="Kumunita.Core.Identity.OutboxEmail"/> — the <c>Body</c> is
/// nulled after the <c>OutboxBodyNullDays</c> cutoff (the row itself kept
/// until the <c>OutboxDays</c> cutoff), and the row deleted after the
/// <c>OutboxDays</c> cutoff.</item>
/// </list>
/// </para>
/// A key set to <c>0</c> (or absent) makes <see cref="RetentionPolicy.CutoffFor"/>
/// return <c>null</c> — that type is **skipped** (kept), the fail-safe.
/// <para>
/// <b>No PII</b>: the return is the affected counts only; no email, body or
/// subject id leaves this class (the IS08 <c>LogTemplatePiiGateTests</c> apply
/// to anything logged here — log counts, never rows).
/// </para>
/// <para>
/// The Wolverine handler and the <c>Program.cs</c> seed are IS34b; part 2
/// (notifications, error reports, log files) is IS35. This class is
/// Wolverine-free and HTTP-free (the <see cref="Kumunita.Core.Usage.UsagePurgeService"/>
/// "Core does the work, Web hosts the handler" split).
/// </para>
/// </summary>
public static class RetentionService
{
    /// <summary>
    /// Runs the part-1 retention sweep over <paramref name="now"/>: deletes or
    /// nulls each part-1 type's rows that have crossed their cutoff (see the
    /// type summary). Every cutoff is <see cref="RetentionPolicy.CutoffFor"/>
    /// over <paramref name="options"/>; a <c>null</c> cutoff (key <c>0</c> /
    /// absent) skips that type. One id-collection + delete per type in a single
    /// session (the
    /// <see cref="Kumunita.Core.Usage.UsagePurgeService.PurgeAsync"/> house
    /// shape — batched, one <c>SaveChangesAsync</c>, never a per-row round-trip).
    /// </summary>
    /// <param name="store">The shared document store (the host injects the same <c>IDocumentStore</c> the domain services use).</param>
    /// <param name="options">The config-bound day-counts (the <c>Retention__*</c> surface, IS33).</param>
    /// <param name="now">Injection point for tests (pin "now" so each boundary is deterministic).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The affected counts per type (no PII). A type skipped by a <c>null</c> cutoff reports <c>0</c>.</returns>
    public static async Task<RetentionSweepResult> RunAsync(
        IDocumentStore store,
        RetentionOptions options,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);

        int sessions = 0, identityTokens = 0, escalationTokens = 0,
            deadLetters = 0, outboxBodyNulled = 0, outboxDeleted = 0;

        // --- SessionDoc: revoked beyond the Sessions cutoff (active kept). ---
        DateTimeOffset? sessionsCutoff = RetentionPolicy.CutoffFor(options, "Sessions", now);
        if (sessionsCutoff is { } sc)
        {
            await using var s = store.OpenSession(new Marten.Services.SessionOptions());
            var sessionIds = (await s.Query<SessionDoc>()
                    .Where(x => x.RevokedAt != null && x.RevokedAt < sc)
                    .Select(x => x.Id)
                    .ToListAsync(ct)).Distinct().ToList();
            foreach (var id in sessionIds) s.Delete<SessionDoc>(id);
            await s.SaveChangesAsync(ct);
            sessions = sessionIds.Count;
        }

        // --- IdentityToken: consumed or expired beyond the Tokens cutoff. ---
        // --- EscalationToken: revoked beyond the Tokens cutoff. ---
        DateTimeOffset? tokensCutoff = RetentionPolicy.CutoffFor(options, "Tokens", now);
        if (tokensCutoff is { } tc)
        {
            await using var s = store.OpenSession(new Marten.Services.SessionOptions());
            var identityIds = (await s.Query<IdentityToken>()
                    .Where(x => (x.ConsumedAt != null && x.ConsumedAt < tc) || x.ExpiresAt < tc)
                    .Select(x => x.Id)
                    .ToListAsync(ct)).Distinct().ToList();
            foreach (var id in identityIds) s.Delete<IdentityToken>(id);

            var escalationIds = (await s.Query<EscalationToken>()
                    .Where(x => x.RevokedAt != null && x.RevokedAt < tc)
                    .Select(x => x.Id)
                    .ToListAsync(ct)).Distinct().ToList();
            foreach (var id in escalationIds) s.Delete<EscalationToken>(id);

            await s.SaveChangesAsync(ct);
            identityTokens = identityIds.Count;
            escalationTokens = escalationIds.Count;
        }

        // --- EmailDeadLetter: dead beyond the DeadLetters cutoff. ---
        DateTimeOffset? deadCutoff = RetentionPolicy.CutoffFor(options, "DeadLetters", now);
        if (deadCutoff is { } dc)
        {
            await using var s = store.OpenSession(new Marten.Services.SessionOptions());
            var deadIds = (await s.Query<EmailDeadLetter>()
                    .Where(x => x.DeadAt < dc)
                    .Select(x => x.Id)
                    .ToListAsync(ct)).Distinct().ToList();
            foreach (var id in deadIds) s.Delete<EmailDeadLetter>(id);
            await s.SaveChangesAsync(ct);
            deadLetters = deadIds.Count;
        }

        // --- OutboxEmail: null the Body past OutboxBodyNullDays (row kept),
        //     delete the row past OutboxDays (the two cutoffs are independent
        //     — a row is nulled first, kept, then deleted). ---
        DateTimeOffset? bodyCutoff = RetentionPolicy.CutoffFor(options, "OutboxBodyNullDays", now);
        if (bodyCutoff is { } bc)
        {
            await using var s = store.OpenSession(new Marten.Services.SessionOptions());
            var toNull = await s.Query<OutboxEmail>()
                .Where(x => x.Body != null && x.Body != "" && x.QueuedAt < bc)
                .ToListAsync(ct);
            // Explicitly re-Store each mutated row (the
            // EventService.Group / Pages update idiom, made explicit): a query-
            // loaded row is not guaranteed to be dirty-tracked for a field
            // assignment, so persisting the mutated object is the safe commit.
            foreach (var row in toNull)
            {
                row.Body = string.Empty;
                s.Store(row);
            }
            await s.SaveChangesAsync(ct);
            outboxBodyNulled = toNull.Count;
        }

        DateTimeOffset? outboxCutoff = RetentionPolicy.CutoffFor(options, "OutboxDays", now);
        if (outboxCutoff is { } oc)
        {
            await using var s = store.OpenSession(new Marten.Services.SessionOptions());
            var outboxIds = (await s.Query<OutboxEmail>()
                    .Where(x => x.QueuedAt < oc)
                    .Select(x => x.Id)
                    .ToListAsync(ct)).Distinct().ToList();
            foreach (var id in outboxIds) s.Delete<OutboxEmail>(id);
            await s.SaveChangesAsync(ct);
            outboxDeleted = outboxIds.Count;
        }

        return new RetentionSweepResult(
            sessions, identityTokens, escalationTokens, deadLetters,
            outboxBodyNulled, outboxDeleted);
    }

}

/// <summary>
/// The affected counts from a <see cref="RetentionService.RunAsync"/> part-1
/// sweep (no PII — counts only). A type skipped by a <c>null</c> cutoff
/// (key <c>0</c> / absent) reports <c>0</c>.
/// </summary>
public sealed record RetentionSweepResult(
    int SessionsDeleted,
    int IdentityTokensDeleted,
    int EscalationTokensDeleted,
    int DeadLettersDeleted,
    int OutboxBodyNulled,
    int OutboxDeleted);
