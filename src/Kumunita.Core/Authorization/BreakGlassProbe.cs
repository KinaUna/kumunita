using Marten;

namespace Kumunita.Core.Authorization;

/// <summary>
/// Break-glass elevation probe (ADR 0003 §4.5, ADR 0299).
/// <para>
/// Answers "does this user hold an active break-glass override right now?"
/// as a standalone static seam, so callers that already own a query session
/// (e.g. the full instance export in IS28) can gate on it directly without
/// widening the frozen <see cref="IAuthorizationService"/> surface (ADR 0006).
/// </para>
/// <para>
/// <see cref="AdminOverride"/> is hand-rolled (<c>AuthorizationFeature</c> /
/// ADR 0004 §B.1) — operator-written via psql; the app only reads it. It is
/// NOT a Marten document, so the read is a parameterised SQL query on the
/// <em>same query session</em>: this keeps the read inside the caller's
/// transaction when one is in flight, and the ADO.NET command executes
/// against the same physical connection the session holds (Marten
/// <see cref="IQuerySession"/> surfaces a <c>Connection</c> property for
/// exactly this raw-SQL seam — see <c>M1Feature</c>'s raw-SQL reads in DDL
/// tests for the same pattern).
/// </para>
/// <remarks>
/// The semantics are the single source of truth for "an active override":
/// a row exists for the user that has been <em>consumed</em>
/// (<c>consumedAt</c> set) and is not yet expired (<c>expiresAt</c> in the
/// future). <see cref="AuthorizationService"/> delegates its private
/// break-glass check to this probe, so the two can never drift.
/// </remarks>
public static class BreakGlassProbe
{
    /// <summary>
    /// True when <paramref name="userId"/> holds a consumed, non-expired
    /// <c>AdminOverride</c> row; false otherwise.
    /// </summary>
    /// <param name="session">
    /// Any Marten query/document session (the raw SQL runs on its
    /// underlying <c>NpgsqlConnection</c>).
    /// </param>
    /// <param name="userId">The account being checked.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<bool> HasActiveAsync(
        IQuerySession session, string userId, CancellationToken ct = default)
    {
        var conn = ((Npgsql.NpgsqlConnection)session.Connection!);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT EXISTS (" +
            "  SELECT 1 FROM \"mt\".\"AdminOverride\" " +
            "  WHERE \"userId\" = @userId " +
            "    AND \"consumedAt\" IS NOT NULL " +
            "    AND \"expiresAt\" > @now" +
            ")";

        var pUser = cmd.CreateParameter();
        pUser.ParameterName = "@userId";
        pUser.Value = userId;
        cmd.Parameters.Add(pUser);

        var pNow = cmd.CreateParameter();
        pNow.ParameterName = "@now";
        pNow.Value = DateTimeOffset.UtcNow;
        cmd.Parameters.Add(pNow);

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is bool b && b;
    }
}
