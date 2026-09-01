using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Kumunita.Core.Identity;

/// <summary>
/// EF Core context for ASP.NET Core Identity — the only EF usage in the tree (ADR 0004
/// Decisions A/C). Owns the `identity` schema; Marten owns `mt`. No domain model ever
/// touches this context.
/// </summary>
public sealed class AppDbContext : IdentityDbContext<IdentityUser, IdentityRole, string>
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
