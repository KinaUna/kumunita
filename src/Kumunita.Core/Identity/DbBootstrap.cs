using Microsoft.EntityFrameworkCore;

namespace Kumunita.Core.Identity;

public static class DbBootstrap
{
    public static async Task<bool> IsPristineAsync(AppDbContext db, CancellationToken ct = default)
    {
        const string sql =
            "SELECT NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'mt') " +
            "AND NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'identity')";

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
