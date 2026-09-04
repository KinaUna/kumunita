using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Kumunita.Core.Identity;

/// <summary>
/// EF Core context for ASP.NET Core Identity — the only EF usage in the tree (ADR 0004
/// Decisions A/C). Owns the `identity` schema; Marten owns `mt`. No domain model ever
/// touches this context.
/// <para>
/// Derives from <see cref="User"/> (Kumunita's subclass of <c>IdentityUser</c>): the
/// Identity layer exposes its own typed user entity through the stock
/// <c>UserManager&lt;TUser&gt;</c> / <c>RoleManager&lt;TRole&gt;</c> generics, and
/// <see cref="User.ExternalId"/> (ADR 0001: reserved for federation) lands in the
/// same `identity` schema the context already owns.
/// </para>
/// </summary>
public sealed class AppDbContext : IdentityDbContext<User, IdentityRole, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("identity");
    }
}
