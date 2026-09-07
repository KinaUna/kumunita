using Microsoft.EntityFrameworkCore;

namespace Kumunita.Core.Identity;

public static class DbBootstrap
{
    /// <summary>
    /// A pristine database is one that has never completed a first boot. The reliable
    /// first-boot signal is the absence of the <c>identity</c> schema: it is created
    /// only by the app's own EF Core migrations (<see cref="SchemaBootstrap"/> →
    /// <see cref="DatabaseExtensions.MigrateAsync"/>) and never by the Wolverine/Marten
    /// host bootstrap. The <c>mt</c> schema is deliberately NOT part of this test —
    /// Wolverine/Marten create it (the <c>wolverine_*</c> tables, the Marten catalog)
    /// during <c>app.StartAsync()</c>, which the web host runs *before*
    /// <see cref="SchemaBootstrap.ApplyAsync"/> executes the first-boot gate. Keying on
    /// <c>mt</c> would report a genuinely fresh database as "not pristine" and silently
    /// skip the first-boot seeder (no seed-admin account, no setup email — OPS §2).
    /// The seeder is idempotent (see <see cref="FirstBootSeeder"/>'s existing-user
    /// guard), so a DB that happens to already carry <c>mt</c> but lacks <c>identity</c>
    /// still seeds exactly once.
    /// </summary>
    public static async Task<bool> IsPristineAsync(AppDbContext db, CancellationToken ct = default)
    {
        const string sql =
            "SELECT NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'identity')";

        // The context owns this connection (EF reuses it for MigrateAsync) — open/close,
        // never dispose here: a disposed instance breaks the next EF operation on this context.
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            return (bool)(await cmd.ExecuteScalarAsync(ct)! ?? throw new InvalidOperationException());
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
