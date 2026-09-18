using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
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
/// + the bundled initial pack's <c>de</c> (enabled, sort 1) / <c>fr</c> (enabled,
/// sort 2) rows (LS U04, ADR 0042 D4) + the instance default
/// (<see cref="LocaleSettings.DefaultLanguageCode"/>) set to
/// <c>en</c> (ADR 0005 B — the "source language ships with the code" clause;
/// the default stays <c>en</c> — ADR 0042 D4).</li>
/// <li><b>Canonical <c>en</c> UI strings + the <c>de</c> / <c>fr</c> baselines
/// + pages</b> (ML-UI U1, D2; LS U04, ADR 0042 D1/D2): the closed set
/// in <see cref="Localization.KnownTranslationKeys"/> materialized as <c>en</c>
/// <c>TranslationResource</c> rows (code-wins, <c>en</c>-only — never touches a
/// non-<c>en</c> row), plus <c>de</c> and <c>fr</c> rows per key from
/// <see cref="Localization.KnownTranslationKeys.DeValues"/> /
/// <see cref="Localization.KnownTranslationKeys.FrValues"/> (create-if-missing —
/// first-boot-only by construction; after first boot an admin's in-app edit is
/// the only write path and is never overwritten). The <c>en</c> <c>terms</c> /
/// <c>help</c> <see cref="Kumunita.Core.Pages.Page"/> docs (PG U05, ADR 0039
/// §3.9 — the absorb: the <c>Page</c> surface is the single source for
/// <c>/terms</c> / <c>/help</c>, so a fresh instance is byte-identical to today)
/// carry their <c>de</c> / <c>fr</c> bodies as
/// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows (ADR 0042 D2; the ADR
/// 0039/0040 PG U06 lane shape — attached to the terms / help pages' <b>own</b>
/// ids, never the <c>system</c> root container's; the read path
/// <c>IPageService.GetTranslationsAsync(page.Id)</c> queries by the page's own
/// id). <c>about</c> is intentionally not seeded as a page (a fresh
/// <c>/about</c> is the product-story view, not a Markdown body — it is the
/// registry-key surface: the <c>about.*</c> keys). Makes the M·9 <c>en</c>
/// floor and the M·12 completeness view real on first boot (100% for all three
/// languages).</li>
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

        // Ensure the GlobalAdmin + Moderator + Translator role rows exist. On a pristine
        // DB the Identity role catalog is empty; IdentityService.SetRoleAsync's
        // "host's seed (FirstBootSeeder)" comment expects the role rows to be here.
        // Idempotent: FindByNameAsync then CreateAsync only when absent.
        if (await roleManager.FindByNameAsync(Roles.GlobalAdmin) is null)
            await roleManager.CreateAsync(new IdentityRole(Roles.GlobalAdmin));
        if (await roleManager.FindByNameAsync(Roles.Moderator) is null)
            await roleManager.CreateAsync(new IdentityRole(Roles.Moderator));
        if (await roleManager.FindByNameAsync(Roles.Translator) is null)
            await roleManager.CreateAsync(new IdentityRole(Roles.Translator));

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
    /// Step 4 — the language-catalog rows + the instance default (ADR 0005 B:
    /// "The source language (en) ships with the code"; ADR 0042 D4: the bundled
    /// initial pack's <c>de</c> / <c>fr</c> rows ship too). Three
    /// <see cref="LanguageCatalog"/> rows (id = BCP-47 code: <c>en</c> sort 0,
    /// <c>de</c> sort 1, <c>fr</c> sort 2, all enabled) + one
    /// <see cref="LocaleSettings"/> (id = "singleton") whose
    /// <see cref="LocaleSettings.DefaultLanguageCode"/> stays <c>en</c> (the
    /// lane is about *available* languages, not about switching the default).
    /// Idempotent: a re-run is a no-op-or-refresh (the admin's
    /// additions/reordering of the other languages are untouched — only these
    /// rows and the singleton default are the keys the seeder owns).
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

        // LS U04 (ADR 0042 D4): the bundled initial pack — the `de` / `fr`
        // catalog rows (enabled, sort 1 / 2, the picker's second and third
        // entries). The singleton default below STAYS `en` — ADR 0042 D4:
        // this lane is about *available* languages at first boot, not about
        // switching the default. Same load-then-Store idempotent shape as
        // the `en` row.
        var existingDe = await session.LoadAsync<LanguageCatalog>("de", ct);
        session.Store(existingDe ?? new LanguageCatalog
        {
            Id = "de",
            NativeName = "Deutsch",
            Enabled = true,
            SortOrder = 1
        });

        var existingFr = await session.LoadAsync<LanguageCatalog>("fr", ct);
        session.Store(existingFr ?? new LanguageCatalog
        {
            Id = "fr",
            NativeName = "Français",
            Enabled = true,
            SortOrder = 2
        });

        var existingSettings = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct);
        session.Store(existingSettings ?? new LocaleSettings
        {
            Id = LocaleSettings.SingletonId,
            DefaultLanguageCode = SourceLanguage,
            // ADR 0019 — the platform default timezone (the fallback a resident's
            // timestamps render in when they have set no Profile.TimeZone override).
            // `UTC` is the neutral, unambiguous default for a fresh instance (the
            // POCO initializer also defaults to `UTC`; set here explicitly for parity
            // with DefaultLanguageCode and to make the seed's intent visible).
            DefaultTimezone = "UTC",
            // ADR 0020 — the platform default date-time format (the fallback a
            // resident's timestamps are formatted in when they have set no
            // Profile.DateFormat override). The floor (DateFormat.FloorFormat =
            // the "Long" preset) is the least-ambiguous of the presets (the month
            // spelled out, day/_year never confused); the POCO initializer also
            // defaults to it — set here explicitly for parity with the other two
            // seeded defaults and to make the seed's intent visible.
            DefaultDateFormat = Localization.DateFormat.FloorFormat
        });

        await session.SaveChangesAsync();

        logger.LogInformation(
            "First boot: language catalog seeded (source language '{Lang}' enabled, sort 0; bundled initial pack " +
            "'de' sort 1, 'fr' sort 2 — ADR 0042 D4); instance default stays '{Lang}'.",
            SourceLanguage, SourceLanguage);
    }

    /// <summary>
    /// Step 5 — the canonical <c>en</c> UI-string floor + the <c>de</c> / <c>fr</c>
    /// baselines (LS U04, ADR 0042 D1) + the <c>en</c> terms/help pages with their
    /// <c>de</c> / <c>fr</c> <see cref="Kumunita.Core.Pages.PageTranslation"/> rows
    /// (ML-UI U1, D2; ADR 0042 D2). Materializes, as <c>en</c> rows, every key in
    /// <see cref="KnownTranslationKeys"/> (one <see cref="TranslationResource"/>
    /// per key), plus the <c>de</c> and <c>fr</c> <see cref="TranslationResource"/>
    /// rows per key from <see cref="KnownTranslationKeys.DeValues"/> /
    /// <see cref="KnownTranslationKeys.FrValues"/>, plus the <c>en</c>
    /// <see cref="Kumunita.Core.Pages.Page"/> docs for <c>terms</c> and
    /// <c>help</c> carrying their <c>de</c> / <c>fr</c> bodies. This is what makes
    /// M·9's "<c>en</c> floor is always seeded", M·12's completeness view
    /// (missing = <c>en</c>-present minus <c>code</c>-present — 100% for all three
    /// languages on a fresh boot), and ADR 0042's "the platform demonstrates its
    /// headline feature from first bootup" real the moment a fresh instance boots.
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

        // LS U04 (ADR 0042 D1): the bundled `de` / `fr` baselines — one
        // TranslationResource row per key, **create-if-missing** (plain
        // query-then-Store idiom, the same shape as the `en` loop above, but
        // existing rows are skipped — never refreshed). First-boot-only BY
        // CONSTRUCTION: the outer IsPristineAsync gate means this method never
        // runs on a warm instance, so an admin's later in-app edit of a `de` /
        // `fr` row (the ADR 0021 editor) is never overwritten; the skip branch
        // makes that invariant hold even if the gate ever changed.
        foreach (var (code, baseline) in new[] { ("de", KnownTranslationKeys.DeValues), ("fr", KnownTranslationKeys.FrValues) })
        {
            foreach (var (key, text) in baseline)
            {
                var existing = await session
                    .Query<TranslationResource>()
                    .Where(t => t.Key == key && t.LanguageCode == code)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    session.Store(new TranslationResource
                    {
                        Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                        Key = key,
                        LanguageCode = code,
                        Text = text
                    });
                }
                // else: skip — create-if-missing (never overwrite; ADR 0042 D1).
            }
        }

        // Static pages: the `en` terms + help `Page` docs (PG U05/U07, ADR 0039
        // §3.9). `about` is deliberately NOT seeded — a fresh instance's /about
        // keeps its product-story view, and an admin can create an `about` page
        // at runtime (the seeder never writes it).
        var enPages = EnDefaultPages();

        // PG U05 (ADR 0039 §3.9): the `Page` docs for the seeded default pages —
        // the `en` terms/help bodies, so a fresh instance is byte-identical for
        // /terms and /help read from the tree (`StaticPagesController` reading
        // `IPageService.GetByPathAsync`). The legacy static-page store was
        // retired in U07 — this is now the single source.
        //
        // Extracted to a public static so the Core test can pin idempotency
        // across two sessions (boot twice, no duplicate root pages).
        //
        // LS U04 (ADR 0042 D2): the `de` / `fr` bodies of these same seeded
        // pages ride along as `PageTranslation` rows (the ADR 0039/0040 PG
        // U06 lane shape) — attached to the terms/help pages' **own** Ids
        // (the read path, `IPageService.GetTranslationsAsync(page.Id)`,
        // queries by the page's own id — never the `system` root's).
        var seededPageIds = await SeedDefaultPagesAsync(session, enPages, now, ct).ConfigureAwait(false);
        await SeedPageTranslationsAsync(session, seededPageIds, now, ct).ConfigureAwait(false);

        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "First boot: canonical `en` UI strings seeded ({0} keys) + `de`/`fr` baselines " +
            "({1} keys each — ADR 0042 D1) + `en` terms/help pages ({2} Page docs) with `de`/`fr` " +
            "bodies ({3} PageTranslation rows); `about` is not seeded (admin-created at runtime).",
            KnownTranslationKeys.EnValues.Count,
            KnownTranslationKeys.DeValues.Count,
            enPages.Length,
            enPages.Length * 2);   // terms + help × de + fr
    }

    /// <summary>
    /// PG U05 (ADR 0039 §3.9) — seed the **new** <see cref="Page"/> docs for
    /// the canonical default pages, into the **caller's** in-flight
    /// <see cref="IDocumentSession"/> (the C3 invariant: same session as the
    /// caller's other writes, so it commits in the caller's single
    /// <c>SaveChangesAsync</c>). Idempotent: a root page (a
    /// <see cref="Page.ParentId"/> of <c>null</c>) with the same slug is
    /// refreshed in place (<c>Title</c>/<c>Body</c>), never duplicated — so
    /// booting twice (or a warm re-run) yields exactly the same set of root
    /// <c>Page</c> docs.
    /// <para>
    /// Each seeded page is <see cref="Page.Audience"/> = <c>null</c> (public —
    /// the canonical <c>about</c>/<c>terms</c>/<c>help</c> pages are world-readable;
    /// the *composer* default is non-public/community-visible, ADR 0039 §3.4
    /// amended 2026-09-17 — the seed and the composer default are independent),
    /// <see cref="Page.LanguageCode"/> = <c>en</c> (the
    /// <see cref="SourceLanguage"/>), a root node (<see cref="Page.ParentId"/>
    /// = <c>null</c>), and <see cref="Page.AuthorId"/> empty (platform content
    /// — no resident author). It does NOT seed <c>about</c> (the U05 drift pin —
    /// a fresh <c>/about</c> is the product-story view, not a Markdown page).
    /// </para>
    /// <para>
    /// Public (not internal) so the Core test can pin idempotency + the
    /// exact set across two live sessions without an
    /// <c>InternalsVisibleTo</c> (the repo's Core test constraint — only
    /// <c>public</c> members are reachable).
    /// </para>
    /// <para>
    /// LS U04 (ADR 0042 D2): returns the <c>slug → page Id</c> map for the
    /// seeded pages (the terms/help pages under the <c>system</c> root) so
    /// the caller can attach the <c>de</c> / <c>fr</c>
    /// <see cref="PageTranslation"/> rows to the pages' **own** ids (the
    /// read path queries <c>PageTranslation.PageId == page.Id</c> — the
    /// <c>system</c> root container's id is not a valid parent).
    /// </para>
    /// </summary>
    public static async Task<Dictionary<string, string>> SeedDefaultPagesAsync(
        IDocumentSession session,
        IReadOnlyList<(string Slug, string Title, string Body)> defaultPages,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var pageIds = new Dictionary<string, string>(StringComparer.Ordinal);
        // 1. Ensure the `system` namespace root (ADR 0040: the standing
        //    discriminator — a kind=System container for the platform pages;
        //    admins may add sub-levels under it, e.g. system/about).
        var systemRoot = await session
            .Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (systemRoot is null)
        {
            systemRoot = new Page
            {
                Id = Guid.NewGuid().ToString("N"),
                Slug = "system",
                ParentId = null,
                Kind = PageKind.System,
                Title = string.Empty,    // a container — no content of its own
                Body = string.Empty,
                LanguageCode = SourceLanguage,
                Audience = null,         // public
                AuthorId = string.Empty, // platform content — no resident author
                Created = now,
                Modified = now,
            };
            session.Store(systemRoot);
        }
        else if (systemRoot.Kind != PageKind.System)
        {
            // An upgrade path: a pre-ADR-0040 `system` root may exist with
            // the default kind — normalize it (the ADR 0004 §B.1 additive
            // delta: a field refresh is idempotent).
            systemRoot.Kind = PageKind.System;
            session.Store(systemRoot);
        }

        // 2. Seed the platform pages under the `system` root (terms + help).
        foreach (var (slug, title, body) in defaultPages)
        {
            var existingPage = await session
                .Query<Page>()
                .Where(p => p.Slug == slug && p.ParentId == systemRoot.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (existingPage is null)
            {
                // A pre-ADR-0040 deployment may have a root-level page with
                // this slug (terms/help were roots in ADR 0039). Re-parent it
                // under the `system` root rather than duplicating it.
                var orphan = await session
                    .Query<Page>()
                    .Where(p => p.Slug == slug && p.ParentId == null && p.IsDeleted == false)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                if (orphan is not null)
                {
                    orphan.ParentId = systemRoot.Id;
                    orphan.Kind = PageKind.System;
                    orphan.Title = title;
                    orphan.Body = body;
                    orphan.Modified = now;
                    session.Store(orphan);
                    pageIds[slug] = orphan.Id;   // LS U04 — the de/fr translations attach here
                    continue;
                }

                var newPage = new Page
                {
                    Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                    Slug = slug,
                    ParentId = systemRoot.Id,   // under the `system` root (ADR 0040)
                    Kind = PageKind.System,     // a platform page
                    Title = title,
                    Body = body,
                    LanguageCode = SourceLanguage,
                    Audience = null,            // public — world-readable (ADR 0039 §3.4)
                    AuthorId = string.Empty,    // platform content — no resident author
                    Created = now,
                    Modified = now,
                };
                session.Store(newPage);
                pageIds[slug] = newPage.Id;    // LS U04 — the de/fr translations attach here
            }
            else
            {
                existingPage.Kind = PageKind.System;   // normalize on re-seed
                existingPage.Title = title;
                existingPage.Body = body;   // code wins: refresh the `en` page body
                existingPage.Modified = now;
                session.Store(existingPage);
                pageIds[slug] = existingPage.Id;   // LS U04 — the de/fr translations attach here
            }
        }

        return pageIds;
    }

    /// <summary>
    /// The canonical <c>en</c> default-page bodies (terms + help). The single
    /// source of the seed text: the <see cref="Kumunita.Core.Pages.Page"/> docs
    /// (PG U05, ADR 0039 §3.9) carry <em>exactly</em> this text, so a fresh
    /// instance is byte-identical to today for <c>/terms</c> and <c>/help</c>
    /// read from the tree.
    /// <para>
    /// <b><c>about</c> is deliberately absent</b> (the U05 drift pin): a fresh
    /// instance's <c>/about</c> is the <em>full-bleed product-story view</em>
    /// (<c>Views/StaticPages/About</c>, driven by <c>HomeViewModel</c>), not a
    /// Markdown body. Seeding an <c>about</c> <see cref="Kumunita.Core
    /// .Pages.Page"/> would change what <c>/about</c> renders and break the
    /// "byte-identical to today" exit gate — so the seeder never writes it and
    /// the <c>StaticPagesController</c> keeps its product-story fallback. This
    /// deviates from the plan's "seed all three" wording in favor of the
    /// byte-identical gate (recorded in the U05 handoff notes).
    /// </para>
    /// </summary>
    public static (string Slug, string Title, string Body)[] EnDefaultPages()
    {
        return
        [
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
        ];
    }

    /// <summary>
    /// LS U04 (ADR 0042 D2) — the curated <c>de</c> baseline for the seeded
    /// default pages (terms + help). A full translation of the
    /// <see cref="EnDefaultPages"/>() bodies — the same Markdown structure
    /// (heading, intro, the four bullets, the closing line), idiomatic
    /// German UI copy at the ADR 0042 D2 bar (the <c>du</c> register held,
    /// sentence case, no word-for-word calques). These ship as
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows on a pristine
    /// DB (ADR 0042 D1 — seeded once, then community-owned; the in-app
    /// editor supersedes them and no later deploy reverts an admin edit).
    /// <para>
    /// <b><c>about</c> is deliberately absent</b>, exactly as in
    /// <see cref="EnDefaultPages"/>() — it is the registry-key surface
    /// (the <c>about.*</c> keys in <see cref="KnownTranslationKeys"/>),
    /// not a Markdown body.
    /// </para>
    /// </summary>
    public static (string Slug, string Title, string Body)[] DeDefaultPages()
    {
        return
        [
            ("terms", "Nutzungsbedingungen",
             "## Nutzungsbedingungen\n\n" +
             "Kumunita ist eine selbst gehostete Plattform für genau ein Viertel. Als " +
             "Betreiber bist du dafür verantwortlich, wie deine Gemeinschaft sie nutzt: " +
             "wer dabei ist, was sie postet und wie du moderierst.\n\n" +
             "- **Nachbarschaft by design.** Die Plattform geht von genau einem, " +
             "abgegrenzten Viertel aus — nicht von einem öffentlichen Feed.\n" +
             "- **Die Zielgruppen wählt der Autor.** Jeder Beitrag trägt die Zielgruppe, " +
             "die sein Autor gewählt hat; die Plattform erzwingt sie.\n" +
             "- **Deine Daten gehören dir.** Datenbank und hochgeladene Dateien darfst " +
             "du sichern, migrieren und stilllegen, wie du willst.\n"),
            ("help", "Hilfe",
             "## Erste Schritte\n\n" +
             "Kumunita ist ein privater Ort für genau ein Viertel — der Feed, die " +
             "Gruppen und die angepinnten Notizen.\n\n" +
             "- **Beiträge** in einen Gemeinschafts-Feed posten und wählen, wer sie " +
             "sehen darf (eine Person, eine Gruppe oder das ganze Viertel).\n" +
             "- **Gruppen** helfen dir, die Nachbarn um ein Gebäude, ein Projekt oder " +
             "ein gemeinsames Interesse zu organisieren.\n" +
             "- **Das Verzeichnis** zeigt die Menschen auf der Plattform und die " +
             "Angaben, die jeder von ihnen teilen möchte.\n" +
             "- **Moderation** lässt einen Global-Admin (und, wo zugewiesen, einen " +
             "Moderator) den Feed einen sicheren Ort halten.\n\n" +
             "Probleme mit der Instanz selbst? Das ist eine Frage für den Betreiber — " +
             "siehe die Dokumentation zum Self-Hosting, verlinkt in der Fußzeile.\n"),
        ];
    }

    /// <summary>
    /// LS U04 (ADR 0042 D2) — the curated <c>fr</c> baseline for the seeded
    /// default pages (terms + help). A full translation of the
    /// <see cref="EnDefaultPages"/>() bodies — the same Markdown structure
    /// (heading, intro, the four bullets, the closing line), idiomatic
    /// French UI copy at the ADR 0042 D2 bar (the <c>tu</c> register held,
    /// accents and typography per French convention, no word-for-word
    /// calques). These ship as
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows on a pristine
    /// DB (ADR 0042 D1 — seeded once, then community-owned; the in-app
    /// editor supersedes them and no later deploy reverts an admin edit).
    /// <para>
    /// <b><c>about</c> is deliberately absent</b>, exactly as in
    /// <see cref="EnDefaultPages"/>() — it is the registry-key surface
    /// (the <c>about.*</c> keys in <see cref="KnownTranslationKeys"/>),
    /// not a Markdown body.
    /// </para>
    /// </summary>
    public static (string Slug, string Title, string Body)[] FrDefaultPages()
    {
        return
        [
            ("terms", "Conditions d'utilisation",
             "## Conditions d'utilisation\n\n" +
             "Kumunita est une plateforme auto-hébergée pour un seul quartier. En tant " +
             "que porteur, tu assumes la responsabilité de la façon dont ta communauté " +
             "l'utilise : qui en fait partie, ce qu'elle publie, et comment tu la " +
             "modères.\n\n" +
             "- **Le voisinage par conception.** La plateforme part du principe d'un " +
             "quartier unique et borné — pas d'un fil public.\n" +
             "- **Les audiences sont choisies par l'auteur.** Chaque message porte " +
             "l'audience choisie par son auteur ; la plateforme l'applique.\n" +
             "- **Tes données t'appartiennent.** La base de données et les fichiers " +
             "téléversés sont à toi de les sauvegarder, migrer et mettre à la " +
             "retraite.\n"),
            ("help", "Aide",
             "## Premiers pas\n\n" +
             "Kumunita est un foyer privé pour un seul quartier — le fil, les groupes " +
             "et les notes épinglées.\n\n" +
             "- **Publier** dans un fil communautaire et choisir qui peut voir ton " +
             "message (une personne, un groupe, ou tout le quartier).\n" +
             "- **Les groupes** permettent d'organiser les riverains autour d'un " +
             "bâtiment, d'un projet ou d'un intérêt partagé.\n" +
             "- **L'annuaire** montre les résidents de la plateforme et les détails " +
             "que chacun a choisi de partager.\n" +
             "- **La modération** laisse un administrateur global (et, si c'est " +
             "concédé, un modérateur) garder le fil un lieu sûr.\n\n" +
             "Un souci avec l'instance elle-même ? C'est une affaire de porteur — " +
             "consulte la documentation d'auto-hébergement, liée dans le pied de page.\n"),
        ];
    }

    /// <summary>
    /// LS U04 (ADR 0042 D2) — seed the <c>de</c> / <c>fr</c>
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows for the seeded
    /// default pages (terms + help), into the **caller's** in-flight
    /// <see cref="IDocumentSession"/> (the C3 invariant — same session, one
    /// commit). <paramref name="pageIds"/> is the slug → page-Id map
    /// <see cref="SeedDefaultPagesAsync"/> returns (the terms / help pages'
    /// **own** ids — the read path
    /// <c>IPageService.GetTranslationsAsync(page.Id)</c> queries
    /// <c>PageTranslation.PageId == page.Id</c>, so the <c>system</c> root
    /// container's id is *not* a valid parent; the ADR 0042 D6 text records
    /// the distinction).
    /// <para>
    /// Idempotent (query-then-Store, one row per (page, language) pair,
    /// <b>create-if-missing</b> — an existing row is skipped, never
    /// refreshed): a pristine DB has no rows yet (create); a warm re-run (the
    /// outer <c>IsPristineAsync</c> gate normally blocks this) leaves existing
    /// rows exactly as written — no duplicates, no overwrite. The rows are
    /// <b>first-boot-only by construction</b> (ADR 0042 D1): after first
    /// boot the in-app editor (GlobalAdmin ∪ Translator, ADR 0021) is the
    /// only write path, and an admin's edit is never overwritten by a later
    /// deploy.
    /// </para>
    /// <para>
    /// <see cref="PageTranslation.AuthorId"/> is empty (platform content —
    /// no resident author, the same convention as the
    /// <see cref="Kumunita.Core.Pages.Page"/> docs the rows attach to).
    /// </para>
    /// </summary>
    public static async Task SeedPageTranslationsAsync(
        IDocumentSession session,
        IReadOnlyDictionary<string, string> pageIds,
        DateTimeOffset now,
        CancellationToken ct)
    {
        foreach (var (code, baselines) in new[]
        {
            ("de", DeDefaultPages()),
            ("fr", FrDefaultPages()),
        })
        {
            foreach (var (slug, title, body) in baselines)
            {
                if (!pageIds.TryGetValue(slug, out var pageId))
                    continue;   // defensive — EnDefaultPages/DeDefaultPages/FrDefaultPages slugs agree; skip rather than throw
                var existing = await session
                    .Query<PageTranslation>()
                    .Where(t => t.PageId == pageId && t.LanguageCode == code)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    session.Store(new PageTranslation
                    {
                        Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                        PageId = pageId,
                        LanguageCode = code,
                        Title = title,
                        Body = body,
                        AuthorId = string.Empty,   // platform content — no resident author
                        Created = now,
                    });
                }
                // else: skip — create-if-missing (never overwrite; ADR 0042 D1).
            }
        }
    }

    private static string SeedAdminBody(string email, string userId, string token) =>
        $"Hi,\n\nThis first-boot setup email is the one-time handoff to bring your new Kumunita " +
        $"instance online. When you are ready, present the setup token below at the /admin/setup " +
        $"page to finish activating the GlobalAdmin account ({email}, userId {userId}).\n\n" +
        $"Setup token (one-time): {token}\n\n" +
        $"This token is a single-use credential — the account is sign-in-ready only after it " +
        $"is consumed (CompleteSeedAdminSetupAsync). Treat it like the account password.";
}
