using Kumunita.Identity.Domain;
using Kumunita.Shared.Kernel;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;


namespace Kumunita.Identity.Infrastructure;

public class IdentityDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // All Identity tables go into the 'identity' schema
        builder.HasDefaultSchema("identity");

        builder.Entity<AppUser>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.CreatedAt)
                .HasDefaultValueSql("now()");
        });

        builder.Entity<AppRole>(b =>
        {
            b.ToTable("roles");
            b.OwnsOne(r => r.DisplayName, nav =>
            {
                // LocalizedContent serialized as JSON column
                nav.ToJson();
            });
        });

        // OpenIddict tables are configured separately
    }

    protected override void ConfigureConventions(
        ModelConfigurationBuilder configurationBuilder)
    {
        // Register EF Core converters for all strongly typed IDs
        configurationBuilder
            .Properties<UserId>()
            .HaveConversion<UserId.EfCoreValueConverter>();

        configurationBuilder
            .Properties<GroupId>()
            .HaveConversion<GroupId.EfCoreValueConverter>();

        configurationBuilder
            .Properties<RoleId>()
            .HaveConversion<RoleId.EfCoreValueConverter>();
    }
}