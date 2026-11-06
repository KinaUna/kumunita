using Microsoft.EntityFrameworkCore;

namespace Kumunita.Core.Identity;

/// <summary>
/// C3-07 (ADR 0297, D5 contract extraction) — the read + wipe contract over the
/// EF Core <c>identity</c>-schema principal store (the ASP.NET Core Identity
/// tables, ADR 0004 §B — the <em>only</em> EF Core surface in the tree). A
/// consumer module (<c>Portability</c> — the M11/U02 no-secret principals
/// export reads the user list; the U06 clean-slate import wipes the Identity
/// principals) codes against this *contract* instead of the concrete
/// <c>AppDbContext</c> context type. This retires the
/// <c>Portability→Identity:AppDbContext</c> boundary edge (the gate's
/// allowed-target rule: an interface is a named contract, not a concrete type
/// crossing) — the EF Core context stays a private detail of the Identity
/// module, exactly as ADR 0004 §B intended.
/// <para>
/// The surface is exactly what the two consumer lanes need:
/// <see cref="GetUsersAsync"/> (the full principal list — the no-secret
/// <c>principals.json</c> export source; the caller projects only the eight
/// allowed §principals fields, never the credential columns) and
/// <see cref="WipeIdentityPrincipalsAsync"/> (the clean-slate import's
/// dependent-order delete of the stock Identity principal + dependent rows).
/// </para>
/// <para>
/// The concrete <see cref="IdentityPrincipalStore"/> adapter wraps
/// <c>AppDbContext</c> (the source module owns both the context and the
/// contract, so the binding stays in the Identity module — D5 "update the DI
/// registration in the source module").
/// </para>
/// </summary>
public interface IIdentityPrincipalStore
{
    /// <summary>
    /// The full list of Identity principals (the <c>identity</c>-schema
    /// <c>AspNetUsers</c> rows) — the no-secret <c>principals.json</c> export
    /// source (the caller reads only the eight allowed fields; the credential
    /// columns are never projected).
    /// </summary>
    Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct);

    /// <summary>
    /// Wipes the Identity principals in the clean-slate import's
    /// dependent-order (the four user-dependent stock Identity tables —
    /// <c>AspNetUserRoles</c> / <c>UserClaims</c> / <c>UserLogins</c> /
    /// <c>UserTokens</c> — then <c>AspNetUsers</c> itself; no cascade
    /// reliance), and clears the change tracker (a fresh-slate semantic so a
    /// same-<c>subjectId</c> re-create does not collide with a previously
    /// tracked principal). A no-op when there are no principals.
    /// </summary>
    Task WipeIdentityPrincipalsAsync(CancellationToken ct);
}

/// <summary>
/// The <see cref="IIdentityPrincipalStore"/> adapter — wraps
/// <c>AppDbContext</c> (the EF Core <c>identity</c>-schema context, ADR 0004
/// §B). The Identity module owns both the context and the contract; this is
/// where the EF Core detail stays private to the source module. Registered in
/// <see cref="IdentityModule"/>.
/// </summary>
public sealed class IdentityPrincipalStore(
    AppDbContext appDbContext) : IIdentityPrincipalStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct)
    {
        // Fully-qualified EF Core ToListAsync (disambiguate from
        // Marten.QueryableExtensions.ToListAsync&lt;T&gt;).
        var users = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(appDbContext.Users, ct).ConfigureAwait(false);
        return users;
    }

    /// <inheritdoc />
    public async Task WipeIdentityPrincipalsAsync(CancellationToken ct)
    {
        // The user-dependent tables are wiped <em>first, in the dependent-
        // order</em> (AspNetUserRoles / Claims / Logins / Tokens, then
        // AspNetUsers) — deliberate and not left to cascade:
        // <see cref="EntityFrameworkQueryableExtensions.ExecuteDeleteAsync"/>
        // does not reliably cascade the dependent rows, and a surviving
        // AspNetUserRoles row for a re-created subjectId would collide with
        // the apply's <c>AddToRoleAsync</c> (a duplicate (userId, roleId)).
        // The AspNetRoles / AspNetRoleClaims rows are the standing definitions
        // (Member / Moderator / GlobalAdmin / Translator) — left in place;
        // the re-creator's roleManager.FindByNameAsync / CreateAsync ensures
        // existence and the archive's roles re-attach onto the fresh
        // principals.
        var users = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(appDbContext.Users, ct).ConfigureAwait(false);
        if (users.Count == 0)
        {
            return;
        }

        var userIds = users.Select(u => u.Id).ToList();
        // The four user-dependent tables (the stock Identity schema, all keyed
        // by the principal id). Each is a flat delete of the rows for the
        // to-be-deleted principals — one ExecuteDelete per table, no cascade
        // reliance.
        await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ExecuteDeleteAsync(appDbContext.UserRoles
                .Where(r => userIds.Contains(r.UserId)), ct).ConfigureAwait(false);
        await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ExecuteDeleteAsync(appDbContext.UserClaims
                .Where(c => userIds.Contains(c.UserId)), ct).ConfigureAwait(false);
        await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ExecuteDeleteAsync(appDbContext.UserLogins
                .Where(l => userIds.Contains(l.UserId)), ct).ConfigureAwait(false);
        await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ExecuteDeleteAsync(appDbContext.UserTokens
                .Where(t => userIds.Contains(t.UserId)), ct).ConfigureAwait(false);
        // The principals themselves.
        await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ExecuteDeleteAsync(appDbContext.Users
                .Where(u => userIds.Contains(u.Id)), ct).ConfigureAwait(false);
        // ExecuteDeleteAsync bypasses the change tracker — any User /
        // dependent entities already attached to this context's tracker
        // (e.g. a principal read earlier in the same scoped request) stay
        // tracked, and the apply's re-CreateAsync of the same subjectId would
        // then throw an EF identity conflict. A clean-slate import wipes every
        // principal, so dropping the whole tracker is the correct fresh-slate
        // semantic and keeps the re-create clean regardless of what was
        // previously tracked in this context.
        appDbContext.ChangeTracker.Clear();
    }
}
