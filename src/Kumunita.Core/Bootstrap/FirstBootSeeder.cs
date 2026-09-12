using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Kumunita.Core.Bootstrap;

/// <summary>
/// First-boot seeder (OPS §2 — "a verification email is sent"; ADR 0005 — language
/// catalog; plan M1 step 6). Runs ONLY when <see cref="DbBootstrap.IsPristineAsync"/>
/// says the database is fresh (neither the <c>mt</c> nor the <c>identity</c> schema
/// exists) — every step here is idempotent AND that outer gate keeps the seeder from
/// touching data on a warm boot (the design doc's "first boot is also the first seeder
/// run", SchemaBootstrap.cs).
/// <para>
/// Six steps, in the order the plan pins them (step 6 of the M1 plan):
/// </para>
/// <ol>
/// <li><b>Community row</b> in <c>mt.community</c> (id "default"): M1 makes this the
/// runtime source of truth for the displayed name; the <c>Community__Name</c> env is
/// the seed. <c>INSERT … ON CONFLICT DO UPDATE</c> — an existing row's name is honored
/// on a re-seed, so a warm re-run (rare; the outer gate prevents it) never resets
/// an admin edit.</li>
/// <li><b>Seed GlobalAdmin</b>: the one-time setup token (<c>SeedAdmin__Token</c>) is
/// the credential (no initial password — the setup mail hands off first-login
/// instructions); a duplicate is a no-op. Honors <see cref="SeedAdminOptions"/>
/// absence (an instance without first-run env is still usable — the plan pins this
/// explicitly).</li>
/// <li><b>Default components</b> (safety/maintenance/social/governance):
/// <see cref="IUserInfoService.SeedComponentsAsync"/> is already idempotent and
/// idempotency-guarded (only <c>SetComponentModeratorAccessAsync</c> may flip the
/// <c>ModeratorAccess</c> flag — invariant C5).</li>
/// <li><b>Language catalog</b>: the source-language <c>en</c> row (enabled, sort 0)
/// + the instance default (<see cref="LocaleSettings.DefaultLanguageCode"/>) set to
/// <c>en</c> (ADR 0005 B — the "source language ships with the code" clause).</li>
/// <li><b>Canonical <c>en</c> UI strings + pages</b> (ML-UI U1, D2): the closed set
/// in <see cref="Localization.KnownTranslationKeys"/> materialized as <c>en</c>
/// <c>TranslationResource</c> rows, plus the <c>en</c> <c>terms</c> / <c>help</c>
/// <c>LocalizedPage</c> rows. Code-wins upsert for <c>en</c> only (never touches a
/// non-<c>en</c> row); <c>about</c> is intentionally not seeded. Makes the M·9
/// <c>en</c> floor and the M·12 completeness view real on first boot.</li>
/// <li><b>First-boot setup email</b> to the seed admin (OPS §2 handoff — staged on
/// the session, dispatched by the durable handler in M1 step 7). Honors absence:
/// no seed admin ⇒ no email (the lane is skipped end-to-end).</li>
/// </ol>
/// </summary>
public static class FirstBootSeeder
{
    /// <summary>The community row's stable identity (the table's PK — one row, id "default").</summary>
    public const string CommunityId = "default";

    /// <summary>The language-catalog source-language code (ADR 0005).</summary>
    public const string SourceLanguage = "en";

    /// <summary>
    /// Runs the five seeder steps. See the class doc for order and idempotency.
    /// Called once by <see cref="SchemaBootstrap"/> on a pristine DB.
    /// </summary>
    public static async Task SeedAsync(
        AppDbContext identity,
        IDocumentStore mt,
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager,
        IUserInfoService userInfo,
        IMailerStage mailer,
        CommunityOptions community,
        SeedAdminOptions? seedAdmin,
        int setupTokenTtlDays,
        ILogger logger,
        CancellationToken ct = default)
    {
        // Step order follows the plan's pinning: community → seed-admin → components →
        // language → (first-boot email lives in the seed-admin lane by design — it's
        // the OPS §2 "a verification email is sent" handoff, keyed to the seed admin).

        // 1. Community row.
        await SeedCommunityRowAsync(mt, community.Name, logger, ct);

        // 2+5. Seed GlobalAdmin + the first-boot email (honors SeedAdminOptions absence).
        if (seedAdmin is { Email: { Length: > 0 } email, Token: { Length: > 0 } token })
        {
            await SeedAdminAsync(
                userManager, roleManager, mt, mailer, email, token, setupTokenTtlDays, logger, ct);
        }
        else
        {
            logger.LogInformation(
                "First boot: SeedAdmin__Email/Token not configured; skipping the seed-admin lane (the instance is still usable without first-run env).");
        }

        // 3. Default components (idempotent upsert — existing rows' flags are honored).
        await userInfo.SeedComponentsAsync();

        // 4. Language catalog (ADR 0005: source-language row + instance default).
        await SeedLanguageCatalogAsync(mt, logger, ct);

        // 5. Canonical `en` UI strings + the `en` terms/help pages (ML-UI U1 — D2):
        // materializes the M·9 `en` floor + the M·12 completeness universe. Runs
        // AFTER the catalog/default (step 4) so the `en` language row already exists;
        // `about` is intentionally NOT seeded (a fresh instance's /about keeps its
        // product-story view — an admin creates an `about` page at runtime).
        await SeedTranslationResourcesAsync(mt, logger, ct);

        logger.LogInformation("First boot: initialization complete.");
    }

    /// <summary>
    /// Step 1 — the community row in <c>mt.community</c>. The table is hand-rolled by
    /// <see cref="KumunitaFeature"/> (Weasel object, not a Marten document), so we write
    /// through the session's connection with raw SQL — the <c>ConsumeBreakGlassAsync</c>
    /// pattern. <c>ON CONFLICT DO UPDATE</c> makes a re-seed a no-op-or-refresh, never
    /// a reset (an admin's earlier edit to the name is honored on a warm re-run).
    /// </summary>
    private static async Task SeedCommunityRowAsync(
        IDocumentStore mt, string name, ILogger logger, CancellationToken ct)
    {
        await using var session = mt.OpenSession(new SessionOptions());
        var conn = (Npgsql.NpgsqlConnection)session.Connection!;
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO \"mt\".\"community\" (\"id\", \"name\") VALUES (@id, @name) " +
            "ON CONFLICT (\"id\") DO UPDATE SET \"name\" = EXCLUDED.\"name\"";
        var id = cmd.CreateParameter();
        id.ParameterName = "@id";
        id.Value = CommunityId;
        cmd.Parameters.Add(id);
        var nameP = cmd.CreateParameter();
        nameP.ParameterName = "@name";
        nameP.Value = name;
        cmd.Parameters.Add(nameP);
        await cmd.ExecuteNonQueryAsync(ct);

        logger.LogInformation("First boot: community row (id '{Id}') upserted with name '{Name}'.",
            CommunityId, name);
    }

    /// <summary>
    /// Step 2+5 — the seed GlobalAdmin account + its one-time setup token + the staged
    /// first-boot email. Skipped (with a log) if <paramref name="email"/> resolves to
    /// an existing account — the "no-op if any user exists" invariant.
    /// <para>
    /// Ordering mirrors <c>IdentityService.CompleteSeedAdminSetupAsync</c>: the EF write
    /// (account + role) lands first (the primary domain op), then the <c>mt</c>-side
    /// rows (Profile, IdentityToken, OutboxEmail) land in one Marten transaction
    /// (invariant C3 — the domain write + the outbox row commit atomically).
    /// </para>
    /// </summary>
    private static async Task SeedAdminAsync(
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager,
        IDocumentStore mt,
        IMailerStage mailer,
        string email,
        string token,
        int setupTokenTtlDays,
        ILogger logger,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Idempotent guard: a fresh DB has no user yet; a warm/re-run DB might (rare —
        // the outer IsPristineAsync gate normally blocks this). Either way, skip.
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            logger.LogInformation("First boot: seed admin '{Email}' already exists; skipping the seed lane (idempotent).", email);
            return;
        }

        // Ensure the GlobalAdmin + Moderator role rows exist. On a pristine DB the
        // Identity role catalog is empty; IdentityService.SetRoleAsync's
        // "host's seed (FirstBootSeeder)" comment expects the role rows to be here.
        // Idempotent: FindByNameAsync then CreateAsync only when absent.
        if (await roleManager.FindByNameAsync(Roles.GlobalAdmin) is null)
            await roleManager.CreateAsync(new IdentityRole(Roles.GlobalAdmin));
        if (await roleManager.FindByNameAsync(Roles.Moderator) is null)
            await roleManager.CreateAsync(new IdentityRole(Roles.Moderator));

        // The account — no initial password. The setup token IS the credential: the
        // admin's first sign-in is CompleteSeedAdminSetupAsync, where the token is
        // consumed and replaced with a real password (the OPS §2 change-then-delete
        // race fix). Unverified-resident's "cannot sign in" gate is enforced by the
        // Web ClaimsPrincipalFactory (step 8); the mt-side Profile.Verified=true
        // here signals "this account is ready to complete setup", not "already set up".
        var user = new User
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = email,
            UserName = email
        };
        await userManager.CreateAsync(user);
        await userManager.AddToRoleAsync(user, Roles.GlobalAdmin);

        // The mt-side rows: Profile (the seed admin is the instance owner), the
        // single-use setup IdentityToken (KindSetup, the env's token value), and the
        // staged first-boot OutboxEmail (idempotency key setup:{userId} per §6.2).
        // One session, one SaveChangesAsync — the domain write and the outbox row
        // commit atomically (invariant C3).
        await using var session = mt.OpenSession(new SessionOptions());
        session.Store(new Profile
        {
            SubjectId = user.Id,
            DisplayName = email,
            Email = email,
            Verified = true,   // the seed admin is the instance owner (no verify lane)
            Visibility = new Audience()   // self-only (owner branch; C1 invariant)
        });
        session.Store(new IdentityToken
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = IdentityToken.KindSetup,
            UserId = user.Id,
            Token = token,
            Attempt = 1,
            CreatedAt = now,
            ExpiresAt = now.AddDays(setupTokenTtlDays)
        });
        await mailer.StageAsync(session,
            idempotencyKey: $"setup:{user.Id}",
            recipient: email,
            subject: "Kumunita: first-boot setup instructions",
            body: SeedAdminBody(email, user.Id, token),
            ct: ct);
        await session.SaveChangesAsync();

        logger.LogInformation("First boot: seeded GlobalAdmin '{Email}' (userId {UserId}); setup token staged to outbox.",
            email, user.Id);
    }

    /// <summary>
    /// Step 4 — the language-catalog source-language row + the instance default
    /// (ADR 0005 B: "The source language (en) ships with the code"). Two documents:
    /// <see cref="LanguageCatalog"/> (id = BCP-47 code) and
    /// <see cref="LocaleSettings"/> (id = "singleton"). Idempotent: a re-run is a
    /// no-op-or-refresh (the admin's additions/reordering of the other languages are
    /// untouched — only the source row and the singleton default are the two keys).
    /// </summary>
    private static async Task SeedLanguageCatalogAsync(
        IDocumentStore mt, ILogger logger, CancellationToken ct)
    {
        await using var session = mt.OpenSession(new SessionOptions());

        // Upsert: load-then-Store is the idempotent shape (Marten's default identity
        // convention picks up Id on both POCOs — no Identity(mapping) pin needed).
        var existingEn = await session.LoadAsync<LanguageCatalog>(SourceLanguage, ct);
        session.Store(existingEn ?? new LanguageCatalog
        {
            Id = SourceLanguage,
            NativeName = "English",
            Enabled = true,
            SortOrder = 0   // first in the selector
        });

        var existingSettings = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct);
        session.Store(existingSettings ?? new LocaleSettings
        {
            Id = LocaleSettings.SingletonId,
            DefaultLanguageCode = SourceLanguage
        });

        await session.SaveChangesAsync();

        logger.LogInformation("First boot: language catalog seeded (source language '{Lang}' enabled, default '{Lang}').",
            SourceLanguage, SourceLanguage);
    }

    /// <summary>
    /// Step 5 — the canonical <c>en</c> UI-string floor + the <c>en</c> terms/help
    /// pages (ML-UI U1, D2). Materializes, as <c>en</c> rows, every key in
    /// <see cref="KnownTranslationKeys"/> (one <see cref="TranslationResource"/>
    /// per key) plus the <c>en</c> <see cref="LocalizedPage"/> rows for
    /// <c>terms</c> and <c>help</c>. This is what makes M·9's "<c>en</c> floor is
    /// always seeded" and M·12's completeness view (missing = <c>en</c>-present
    /// minus <c>code</c>-present) real the moment a fresh instance boots.
    /// <para>
    /// <b>Upsert semantics (code-wins, <c>en</c>-only).</b> Each key is upserted
    /// by its business key <c>(Key, "en")</c> / <c>(Slug, "en")</c> — the same
    /// pair idiom as <c>LocalizationService.Upsert*</c> (query-then-<c>Store</c>,
    /// create-with-a-fresh-<c>Id</c> or overwrite-in-place), but with two deliberate
    /// differences that make it idempotent AND upgrade-safe:
    /// </para>
    /// <list type="bullet">
    /// <li><b>Code wins for <c>en</c>.</b> The registry's text is always written,
    /// so a new key added to <see cref="KnownTranslationKeys"/> appears on the next
    /// start (upgrade) and an edited <c>en</c> value refreshes the row. ADR 0005 B:
    /// the source language's UI strings are <i>embedded and materialized</i> by the
    /// seeder — they are code, not admin data.</li>
    /// <li><b>Never touches a non-<c>en</c> row.</b> The query and the <c>Store</c>
    /// are both scoped to <c>LanguageCode == "en"</c>; admin translations for other
    /// languages (and an admin-created <c>about</c> page) are data, not config, and
    /// are left exactly as the admin wrote them (M·4).</li>
    /// </list>
    /// <para>
    /// <b>No <c>AccessAudit</c> row.</b> Unlike the admin <c>Upsert*</c> path, a
    /// first-boot seed is not an actor's auditable action (the seeder is not a
    /// principal), so the single <c>SaveChangesAsync</c> commits the content rows
    /// only. One session, one save — the whole step is atomic.
    /// </para>
    /// </summary>
    private static async Task SeedTranslationResourcesAsync(
        IDocumentStore mt, ILogger logger, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        await using var session = mt.OpenSession(new SessionOptions());

        // UI strings: one `en` TranslationResource row per key in the canonical
        // registry (code-wins upsert by (Key, "en"); never a non-`en` row).
        foreach (var (key, enText) in KnownTranslationKeys.EnValues)
        {
            var existing = await session
                .Query<TranslationResource>()
                .Where(t => t.Key == key && t.LanguageCode == SourceLanguage)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                session.Store(new TranslationResource
                {
                    Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                    Key = key,
                    LanguageCode = SourceLanguage,
                    Text = enText
                });
            }
            else
            {
                existing.Text = enText;   // code wins: refresh the `en` value
                session.Store(existing);
            }
        }

        // Static pages: the `en` terms + help rows (code-wins upsert by
        // (Slug, "en")). `about` is deliberately NOT seeded — a fresh instance's
        // /about keeps its product-story view, and an admin can create an
        // `about` page at runtime (the seeder never writes it).
        var enPages = new (string Slug, string Title, string Body)[]
        {
            ("terms", "Terms",
             "## Terms of use\n\n" +
             "Kumunita is a self-hosted platform for one neighborhood. As its operator, " +
             "you are responsible for how your community uses it: who joins, what they " +
             "post, and how you moderate it.\n\n" +
             "- **Residency is by design.** The platform assumes a single, bounded " +
             "neighborhood — not a public feed.\n" +
             "- **Audiences are chosen by the author.** Every post carries the audience " +
             "its author picked; the platform enforces it.\n" +
             "- **You own your data.** The database and the uploaded files are yours to " +
             "back up, migrate, and retire.\n"),
            ("help", "Help",
             "## Getting started\n\n" +
             "Kumunita is a private home for one neighborhood — the feed, the groups, " +
             "and the pinned notes.\n\n" +
             "- **Post** to a community feed and choose who can see it (an individual, a " +
             "group, or everyone in the neighborhood).\n" +
             "- **Groups** let you organize residents around a building, a project, or a " +
             "shared interest.\n" +
             "- **Directory** shows the residents on the platform and the details each " +
             "has chosen to share.\n" +
             "- **Moderation** lets a global admin (and, where granted, a moderator) " +
             "keep the feed a safe place.\n\n" +
             "Need help with the instance itself? That's an operator concern — see the " +
             "self-hosted documentation linked in the footer.\n"),
        };
        foreach (var (slug, title, body) in enPages)
        {
            var existing = await session
                .Query<LocalizedPage>()
                .Where(p => p.Slug == slug && p.LanguageCode == SourceLanguage)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                session.Store(new LocalizedPage
                {
                    Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                    Slug = slug,
                    LanguageCode = SourceLanguage,
                    Title = title,
                    Body = body,
                    Updated = now
                });
            }
            else
            {
                existing.Title = title;
                existing.Body = body;
                existing.Updated = now;   // code wins: refresh the `en` page
                session.Store(existing);
            }
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "First boot: canonical `en` UI strings seeded ({Keys} keys) + `en` terms/help pages " +
            "({Pages} pages); `about` is not seeded (admin-created at runtime).",
            KnownTranslationKeys.EnValues.Count, enPages.Length);
    }

    private static string SeedAdminBody(string email, string userId, string token) =>
        $"Hi,\n\nThis first-boot setup email is the one-time handoff to bring your new Kumunita " +
        $"instance online. When you are ready, present the setup token below at the /admin/setup " +
        $"page to finish activating the GlobalAdmin account ({email}, userId {userId}).\n\n" +
        $"Setup token (one-time): {token}\n\n" +
        $"This token is a single-use credential — the account is sign-in-ready only after it " +
        $"is consumed (CompleteSeedAdminSetupAsync). Treat it like the account password.";
}
