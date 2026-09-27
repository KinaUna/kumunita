using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace Kumunita.Core.Portability;

/// <summary>
/// The U02 no-secret principals extractor (D3 / C-M11·2) — the
/// <c>identity/principals.json</c> payload. Reads every Identity
/// <see cref="User"/> via <see cref="AppDbContext.Users"/> (the EF Core
/// <c>identity</c>-schema context — the principal source) + the
/// <see cref="Profile"/> doc (for <c>DisplayName</c> / <c>Verified</c> /
/// <c>Blocked</c>) + the Identity roles via
/// <see cref="UserManager{TUser}.GetRolesAsync(TUser)"/>, and projects
/// <b>only</b> the eight allowed §principals fields into
/// <see cref="PortabilityPrincipal"/>.
/// <para>
/// <b>The C-M11·2 boundary at the source:</b> the projection
/// <em>never</em> reads <c>PasswordHash</c> / <c>SecurityStamp</c> /
/// <c>AccessToken</c> / <c>RefreshToken</c> / <c>RecoveryCode</c> / the
/// Identity sign-in record. The <see cref="PortabilityPrincipal"/> POCO
/// has no such field (the U07 no-secret field-shape pin), and this
/// extractor never touches those columns (the U07 no-secret byte-scan
/// witness). The eight allowed fields are the §principals locked set —
/// U06's re-creator mirrors them verbatim.
/// </para>
/// </summary>
public static class PrincipalsExport
{
    /// <summary>
    /// Exports the no-secret principals (the eight allowed §principals
    /// fields per user; the credential columns are never read — the
    /// C-M11·2 source boundary).
    /// </summary>
    /// <param name="appDbContext">The frozen EF Core <c>identity</c>-schema seam (the user source — <see cref="AppDbContext.Users"/>).</param>
    /// <param name="userManager">The frozen Identity seam (the roles source — <see cref="UserManager{TUser}.GetRolesAsync(TUser)"/>).</param>
    /// <param name="documentStore">The frozen Marten seam (the <see cref="Profile"/> source for <c>DisplayName</c> / <c>Verified</c> / <c>Blocked</c>).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The <c>identity/principals.json</c> payload bytes (a JSON array of <see cref="PortabilityPrincipal"/>).</returns>
    public static async Task<byte[]> ExportAsync(
        AppDbContext appDbContext,
        UserManager<User> userManager,
        IDocumentStore documentStore,
        CancellationToken ct = default)
    {
        // The principal source — AppDbContext.Users (the identity schema's
        // user table; the EF Core context is the principal store, not a
        // domain-doc read). Fully-qualified EF Core ToListAsync to avoid
        // the ambiguity with Marten.QueryableExtensions.ToListAsync<T>.
        var users = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(appDbContext.Users, ct);

        // Load all Profile docs in one query (the DisplayName / Verified /
        // Blocked source — keyed by SubjectId = User.Id).
        await using var session = documentStore.QuerySession();
        var profiles = (await session.Query<Profile>().ToListAsync(ct))
            .ToDictionary(p => p.SubjectId, StringComparer.Ordinal);

        var principals = new List<PortabilityPrincipal>(users.Count);

        foreach (var user in users)
        {
            ct.ThrowIfCancellationRequested();

            profiles.TryGetValue(user.Id, out var profile);

            // The eight allowed §principals fields — the C-M11·2 boundary:
            // no PasswordHash / SecurityStamp / AccessToken / RefreshToken /
            // RecoveryCode / Identity sign-in record is ever read.
            var principal = new PortabilityPrincipal
            {
                SubjectId = user.Id,
                Username = user.UserName,
                Email = user.Email,
                NormalizedEmail = user.NormalizedEmail,
                DisplayName = profile?.DisplayName,
                Verified = profile?.Verified ?? false,
                Blocked = profile?.Blocked ?? false,
                Roles = (await userManager.GetRolesAsync(user)).ToList(),
            };

            principals.Add(principal);
        }

        return KumunitaArchive.ToJson(principals);
    }
}
