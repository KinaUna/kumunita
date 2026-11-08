using Kumunita.Core.ErrorReports;
using Kumunita.Core.Identity;
using Kumunita.Core.Logging;
using Kumunita.Core.Notifications;
using Kumunita.Core.Sessions;
using Marten;

namespace Kumunita.Core.Privacy;

/// <summary>
/// The IMPROVE-SEC D6 / E5 retention sweep, **parts 1+2** (the I/O leg): deletes
/// or nulls the D6 document types once they cross their retention cutoff, and
/// runs the daily file-log <see cref="RollingFileSink.Retain"/> leg (D6: the
/// file-log <c>Retain</c> runs daily, not only at boot). The **decision** of
/// *when* a row crosses a cutoff is the pure
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
/// <para>
/// Part-2 types (E5 / D6):
/// <list type="bullet">
/// <item><see cref="Kumunita.Core.Notifications.Notification"/> — a **read**
/// row (non-null <c>ReadAt</c>) is purged past the <c>NotificationsRead</c>
/// cutoff (90 d); an **unread** row (null <c>ReadAt</c>) is purged past the
/// <c>NotificationsUnread</c> cutoff (365 d) by its <c>Created</c> age. Both
/// cohorts are deleted in one pass (a read row is past the read cutoff only;
/// an unread row is past the unread cutoff only).</item>
/// <item><see cref="Kumunita.Core.ErrorReports.ErrorReport"/> — a **resolved**
/// report (non-null <c>ResolvedAt</c>) is purged past the <c>ErrorReports</c>
/// cutoff (180 d). **Unresolved** reports (null <c>ResolvedAt</c>) are always
/// kept — never delete an unresolved report.</item>
/// </list>
/// </para>
/// <para>
/// The log-file leg: <see cref="RollingFileSink.Retain"/> deletes the
/// <c>app-*.log</c> files under the configured directory whose mtime is older
/// than <c>now − RetentionDays</c> (the D6 daily-rotation pass, re-used here
/// so it runs every tick, not only at boot). It is the one leg with no
/// D6 <see cref="RetentionOptions"/> key — it is driven by the two optional
/// <c>logDir</c> / <c>logRetentionDays</c> parameters (<c>null</c> / <c>0</c>
/// skips the leg, the same fail-safe), and <see cref="RollingFileSink.Retain"/>
/// stays pure (BCL-only, no store).
/// </para>
/// A key set to <c>0</c> (or absent) makes <see cref="RetentionPolicy.CutoffFor"/>
/// return <c>null</c> — that type is **skipped** (kept), the fail-safe.
/// <para>
/// <b>No PII</b>: the return is the affected counts only; no email, body or
/// subject id leaves this class (the IS08 <c>LogTemplatePiiGateTests</c> apply
/// to anything logged here — log counts, never rows).
/// </para>
/// <para>
/// The Wolverine handler and the <c>Program.cs</c> seed are IS34b (frozen).
/// This class is Wolverine-free and HTTP-free (the
/// <see cref="Kumunita.Core.Usage.UsagePurgeService"/> "Core does the work,
/// Web hosts the handler" split).
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
    /// <param name="logDir">The directory the dated <c>app-*.log</c> files land in (the <see cref="Kumunita.Core.Logging.FileSinkOptions"/> <c>Directory</c>). <c>null</c> skips the log-file leg (the fail-safe; the caller then runs no file retention this pass).</param>
    /// <param name="logRetentionDays">The file-log retention day-count (the <see cref="Kumunita.Core.Logging.FileSinkOptions"/> <c>RetentionDays</c>). A non-positive value skips the log-file leg (the fail-safe, matching <see cref="RollingFileSink.Retain"/>'s "no files older than <c>now − 0</c>" degenerate).</param>
    /// <returns>The affected counts per type (no PII). A type skipped by a <c>null</c> cutoff reports <c>0</c>; a leg skipped by a <c>null</c>/<c>0</c> log parameter reports <c>0</c> for <see cref="RetentionSweepResult.LogFilesDeleted"/>.</returns>
    public static async Task<RetentionSweepResult> RunAsync(
        IDocumentStore store,
        RetentionOptions options,
        DateTimeOffset now,
        CancellationToken ct = default,
        string? logDir = null,
        int logRetentionDays = 0)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);

        int sessions = 0, identityTokens = 0, escalationTokens = 0,
            deadLetters = 0, outboxBodyNulled = 0, outboxDeleted = 0,
            notifications = 0, errorReports = 0, logFilesDeleted = 0;

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

        // --- Notification: part 2 (D6). Two cohorts, one pass. A READ row
        //     (ReadAt != null) is past the NotificationsRead cutoff (90 d);
        //     an UNREAD row (ReadAt null) is past the NotificationsUnread
        //     cutoff (365 d) by its Created age. The two predicates are
        //     mutually exclusive (a row is read or unread, never both), so the
        //     single WHERE clause is the clean two-cohort disjunction —
        //     each cutoff is its own CutoffFor, and a 0-valued/absent key
        //     nulls that term out (the fail-safe).
        //     The unread cutoff is pre-computed to a nullable constant so the
        //     lambda is a clean two-term null-safe comparison (the part-1
        //     single-cutoff shape, no ternary / no case-split — Linq2DB
        //     translates a null-safe comparison over a constant, not a
        //     conditional expression).
        DateTimeOffset? notifReadCutoff = RetentionPolicy.CutoffFor(options, "NotificationsRead", now);
        DateTimeOffset? notifUnreadCutoff = RetentionPolicy.CutoffFor(options, "NotificationsUnread", now);
        if (notifReadCutoff is not null || notifUnreadCutoff is not null)
        {
            await using var s = store.OpenSession(new Marten.Services.SessionOptions());
            var notifIds = (await s.Query<Notification>()
                    .Where(x =>
                        (x.ReadAt != null && x.ReadAt < notifReadCutoff) ||
                        (x.ReadAt == null && x.Created < notifUnreadCutoff))
                    .Select(x => x.Id)
                    .ToListAsync(ct)).Distinct().ToList();
            foreach (var id in notifIds) s.Delete<Notification>(id);
            await s.SaveChangesAsync(ct);
            notifications = notifIds.Count;
        }

        // --- ErrorReport: part 2 (D6). A RESOLVED report (ResolvedAt != null)
        //     is purged past the ErrorReports cutoff (180 d). An UNRESOLVED
        //     report (ResolvedAt null) is always kept — never delete an
        //     unresolved report (the part-1 "active SessionDoc always kept"
        //     shape, on the resolved cohort).
        DateTimeOffset? errorCutoff = RetentionPolicy.CutoffFor(options, "ErrorReports", now);
        if (errorCutoff is { } ec)
        {
            await using var s = store.OpenSession(new Marten.Services.SessionOptions());
            var errorIds = (await s.Query<ErrorReport>()
                    .Where(x => x.ResolvedAt != null && x.ResolvedAt < ec)
                    .Select(x => x.Id)
                    .ToListAsync(ct)).Distinct().ToList();
            foreach (var id in errorIds) s.Delete<ErrorReport>(id);
            await s.SaveChangesAsync(ct);
            errorReports = errorIds.Count;
        }

        // --- Log files: part 2 (D6 — "the file-log Retain runs daily, not
        //     only at boot"). The one leg with no D6 RetentionOptions key:
        //     driven by the two optional log parameters (null/0 skips it, the
        //     fail-safe). RollingFileSink.Retain stays pure (BCL-only); this
        //     sweep just calls it — deletes the app-*.log files under
        //     logDir whose mtime is older than now − logRetentionDays, per-file
        //     try/catch (a file being written degrades to a skipped file).
        if (logDir is not null && logRetentionDays > 0)
        {
            logFilesDeleted = RollingFileSink.Retain(logDir, logRetentionDays, now);
        }

        return new RetentionSweepResult(
            sessions, identityTokens, escalationTokens, deadLetters,
            outboxBodyNulled, outboxDeleted,
            notifications, errorReports, logFilesDeleted);
    }

}

/// <summary>
/// The affected counts from a <see cref="RetentionService.RunAsync"/> sweep
/// (no PII — counts only). A type skipped by a <c>null</c> cutoff (key <c>0</c>
/// / absent) reports <c>0</c>; the log-file leg skipped by a <c>null</c>/<c>0</c>
/// log parameter reports <c>0</c> for <see cref="LogFilesDeleted"/>.
/// </summary>
public sealed record RetentionSweepResult(
    int SessionsDeleted,
    int IdentityTokensDeleted,
    int EscalationTokensDeleted,
    int DeadLettersDeleted,
    int OutboxBodyNulled,
    int OutboxDeleted,
    int NotificationsDeleted,
    int ErrorReportsDeleted,
    int LogFilesDeleted);
