using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kumunita.Core.Identity;

/// <summary>
/// Design-time factory for <see cref="AppDbContext"/> so `dotnet ef migrations add ...`
/// can build the context without a live connection string (this is the "DesignTimeDbContextFactory
/// seam" the csproj comment below the Microsoft.EntityFrameworkCore.Design reference calls out).
/// The dummy `Host=localhost;Database=design-time` is never opened — the design-time model build
/// only requires the provider to enumerate.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=design-time");
        return new AppDbContext(optionsBuilder.Options);
    }
}
