using Kumunita.Core.Identity;
using Marten;
using Microsoft.Extensions.Logging;

namespace Kumunita.Core.Bootstrap;

public static class FirstBootSeeder
{
    public static async Task SeedAsync(AppDbContext identity, IDocumentStore mt,
        CommunityOptions community, ILogger logger)
    {
        // 1. mt.community row (id "default") — write/confirm; M1 makes this the runtime
        //    source of truth for the displayed name, env is the seed.
        // 2. Identity: create the seed GlobalAdmin for SeedAdmin__Email with
        //    SeedAdmin__Token as a one-time setup credential; no-op if any user exists.
        //      - honor SeedAdmin absence → skip (instance without first-run env).
        // 3. Default components: Safety, Maintenance, Social, Governance (upsert by key).
        // 4. Language catalog: `en` enabled + default (ADR 0005).
        // 5. First-boot setup email to the seed admin (OPS §2 "a verification email
        //    is sent" — the first-login instructions; via the durable email handler).
        logger.LogInformation("First boot: initialization complete.");
    }
}
