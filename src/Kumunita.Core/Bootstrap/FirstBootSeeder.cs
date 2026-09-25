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
/// only write path and is never overwritten). The <c>en</c>
/// <c>terms</c> / <c>help</c> / <c>privacy</c> / <c>conduct</c>
/// <see cref="Kumunita.Core.Pages.Page"/> docs (PG U05, ADR 0039
/// §3.9 — the absorb; the two added in SP U01 per ADR 0043 D2/D5 — the
/// <c>Page</c> surface is the single source for
/// <c>/terms</c> / <c>/help</c> / <c>/privacy</c> / <c>/conduct</c>, so a
/// fresh instance is byte-identical to today) carry their <c>de</c> /
/// <c>fr</c> bodies as
/// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows (ADR 0042 D2; the ADR
/// 0039/0040 PG U06 lane shape — attached to the pages' <b>own</b>
/// ids, never the <c>system</c> root container's; the read path
/// <c>IPageService.GetTranslationsAsync(page.Id)</c> queries by the page's own
/// id; the de/fr baselines now cover all four pages per ADR 0043 D1).
/// <c>about</c> is intentionally not seeded
/// as a page (a fresh
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

        // 5. Canonical `en` UI strings + the `en` terms/help/privacy/conduct pages
        // (ML-UI U1 — D2; the four-page set per ADR 0043 D1/D5): materializes the
        // M·9 `en` floor + the M·12 completeness universe. Runs
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

        // The Danish (da) pre-seeded lane: seeded DISABLED — available in the
        // supported-language catalog with a full baseline (see
        // SeedTranslationResourcesAsync + DaDefaultPages), awaiting an admin's
        // enable. Sort 3, last in the selector. Same load-then-Store idempotent
        // shape; ADR 0042 D4 — the default stays `en` either way.
        var existingDa = await session.LoadAsync<LanguageCatalog>("da", ct);
        session.Store(existingDa ?? new LanguageCatalog
        {
            Id = "da",
            NativeName = "Dansk",
            Enabled = false,
            SortOrder = 3
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
            "'de' sort 1, 'fr' sort 2 — ADR 0042 D4; pre-seeded 'da' DISABLED, sort 3); instance default stays '{Lang}'.",
            SourceLanguage, SourceLanguage);
    }

    /// <summary>
    /// Step 5 — the canonical <c>en</c> UI-string floor + the <c>de</c> / <c>fr</c>
    /// baselines (LS U04, ADR 0042 D1) + the <c>en</c> terms/help/privacy/conduct
    /// pages with their
    /// <c>de</c> / <c>fr</c> <see cref="Kumunita.Core.Pages.PageTranslation"/> rows
    /// (ML-UI U1, D2; ADR 0042 D2; the four-page set per ADR 0043 D1). Materializes, as <c>en</c> rows, every key in
    /// <see cref="KnownTranslationKeys"/> (one <see cref="TranslationResource"/>
    /// per key), plus the <c>de</c> and <c>fr</c> <see cref="TranslationResource"/>
    /// rows per key from <see cref="KnownTranslationKeys.DeValues"/> /
    /// <see cref="KnownTranslationKeys.FrValues"/>, plus the <c>en</c>
    /// <see cref="Kumunita.Core.Pages.Page"/> docs for <c>terms</c>, <c>help</c>,
    /// <c>privacy</c> and <c>conduct</c> (the four-page set, ADR 0043 D1) carrying
    /// their <c>de</c> / <c>fr</c> bodies. This is what makes
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
        // makes that invariant hold even if the gate ever changed. The `da`
        // baseline (the pre-seeded lane) rides the same loop, seeded with the
        // catalog row disabled — the strings exist regardless of enable state.
        foreach (var (code, baseline) in new[]
        {
            ("de", KnownTranslationKeys.DeValues),
            ("fr", KnownTranslationKeys.FrValues),
            ("da", KnownTranslationKeys.DaValues)
        })
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

        // Static pages: the `en` terms/help/privacy/conduct `Page` docs (PG U05/U07, ADR 0039
        // §3.9). `about` is deliberately NOT seeded — a fresh instance's /about
        // keeps its product-story view, and an admin can create an `about` page
        // at runtime (the seeder never writes it).
        var enPages = EnDefaultPages();

        // PG U05 (ADR 0039 §3.9; SP U01, ADR 0043 D2/D5): the `Page` docs for
        // the seeded default pages — the `en` terms/help/privacy/conduct
        // bodies, so a fresh instance is byte-identical for
        // /terms /help /privacy /conduct read from the tree
        // (`StaticPagesController` reading
        // `IPageService.GetByPathAsync`). The legacy static-page store was
        // retired in U07 — this is now the single source.
        //
        // Extracted to a public static so the Core test can pin idempotency
        // across two sessions (boot twice, no duplicate root pages).
        //
        // LS U04 (ADR 0042 D2): the `de` / `fr` bodies of these same seeded
        // pages ride along as `PageTranslation` rows (the ADR 0039/0040 PG
        // U06 lane shape) — attached to the pages' **own** Ids
        // (the read path, `IPageService.GetTranslationsAsync(page.Id)`,
        // queries by the page's own id — never the `system` root's). The de/fr
        // baselines now cover all four pages (terms/help/privacy/conduct) per
        // ADR 0043 D1.
        var seededPageIds = await SeedDefaultPagesAsync(session, enPages, now, ct).ConfigureAwait(false);
        await SeedPageTranslationsAsync(session, seededPageIds, now, ct).ConfigureAwait(false);

        // UG (ADR 0057, amended 2026-09-21 to seed de/fr/da baselines): the
        // resident-facing guides — `Page` docs nested under the canonical
        // `help` page (the ADR 0043 D1 `system/help` surface). The `en` body
        // is the code-owned floor (ADR 0042 D1 "code wins for `en`"); the
        // `de` / `fr` / `da` baselines now ship as `PageTranslation` rows too
        // (the same shape as the four-surface set's baselines above) so a
        // fresh instance shows the guides in the viewer's language, and a
        // non-`en` body, once a human rewords one, is community-owned and
        // never clobbered by a later deploy (the create-if-missing invariant).
        // Same session / same commit as the four-surface set above (C3).
        // Pass the `help` page's Id straight through from `seededPageIds` —
        // it is the in-flight (not yet committed) `help` page, so re-querying
        // for it here would miss and silently drop every guide on first boot.
        if (seededPageIds.TryGetValue("help", out var helpId))
        {
            var guidePageIds = await SeedUserGuidesAsync(session, helpId, now, ct).ConfigureAwait(false);
            await SeedGuideTranslationsAsync(session, guidePageIds, now, ct).ConfigureAwait(false);
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "First boot: canonical `en` UI strings seeded ({0} keys) + `de`/`fr`/`da` baselines " +
            "({1} keys each — ADR 0042 D1) + `en` terms/help/privacy/conduct pages ({2} Page docs) with `de`/`fr`/`da` " +
            "bodies ({3} PageTranslation rows); `about` is not seeded (admin-created at runtime).",
            KnownTranslationKeys.EnValues.Count,
            KnownTranslationKeys.DeValues.Count,
            enPages.Length,
            enPages.Length * 3);   // four pages (terms/help/privacy/conduct) × de + fr + da (ADR 0043 D1)
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
    /// seeded pages (the terms/help/privacy/conduct pages under the <c>system</c> root) so
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

        // 2. Seed the platform pages under the `system` root (terms/help/privacy/conduct).
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
    /// UG (ADR 0057) — seed the **resident-facing guides** as <see cref="Page"/>
    /// docs nested under the canonical <c>help</c> page (the ADR 0043 D1
    /// <c>system/help</c> surface), into the **caller's** in-flight
    /// <see cref="IDocumentSession"/> (the C3 invariant — same session, one
    /// commit, so it commits in the caller's single <c>SaveChangesAsync</c>).
    /// <para>
    /// Each guide is <see cref="PageKind.System"/> (the ADR 0040 standing
    /// matrix: edit / move / delete = GlobalAdmin only; add a translation =
    /// GlobalAdmin ∪ Translator), <see cref="Page.Audience"/> = <c>null</c>
    /// (public — a resident's first question is "how do I …", and that answer
    /// is not audience-gated), <see cref="Page.LanguageCode"/> = <c>en</c>
    /// (authored-in the source language), <see cref="Page.AuthorId"/> empty
    /// (platform content — no resident author), and neither a draft nor
    /// deleted. A guide is a <b>direct child of the <c>help</c> page</b> —
    /// i.e. a <i>grandchild</i> of the <c>system</c> root, <b>not</b> a new
    /// root and <b>not</b> a direct child of <c>system</c> (the ADR 0043 D1
    /// exact-set pin — <c>system</c>'s direct children stay
    /// <c>{terms, help, privacy, conduct}</c> and <c>system</c> stays the
    /// only root — and the ADR 0040 namespace guard: a <c>System</c> guide
    /// under the <c>System</c> <c>help</c> page, a resident's <c>User</c>
    /// page never under <c>help</c>).
    /// </para>
    /// <para>
    /// <b><c>en</c>-only by construction.</b> The guides ship the
    /// <see cref="GuidePages"/>() <c>en</c> floor; a non-<c>en</c> guide body
    /// is <b>not</b> seeded here (no <see cref="PageTranslation"/> row is
    /// attached to a guide) — it is added later by a human Translator (the
    /// ADR 0021 lane) or a GlobalAdmin in the in-app editor, and is never
    /// clobbered by a later deploy (the ADR 0042 D1 "code wins for <c>en</c>"
    /// floor + the "a human Translator is the only writer of a non-<c>en</c>
    /// body" invariant, ADR 0005 C — the "never machine-translated" clause).
    /// This keeps the ADR 0044 page-baseline parity green (the
    /// <see cref="PageTranslation"/> count is unchanged by the guides).
    /// </para>
    /// <para>
    /// Idempotent (query-then-Store, code-wins): an existing guide for a
    /// slug is refreshed in place (<c>Title</c>/<c>Body</c>), never
    /// duplicated — so booting twice yields exactly the same guide set (the
    /// ADR 0042 D1 "code wins for <c>en</c>" shape, the same idempotent
    /// upsert as <see cref="SeedDefaultPagesAsync"/>).
    /// </para>
    /// <para>
    /// Public (not internal) so the Core test can pin the exact seeded set +
    /// idempotency across two live sessions without an
    /// <c>InternalsVisibleTo</c> (the repo's Core test constraint — only
    /// <c>public</c> members are reachable).
    /// </para>
    /// </summary>
    public static async Task<Dictionary<string, string>> SeedUserGuidesAsync(
        IDocumentSession session,
        string helpPageId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var guidePageIds = new Dictionary<string, string>(StringComparer.Ordinal);

        // The guides hang from the canonical `help` page (under the `system`
        // root, ADR 0043 D1). The caller hands us the `help` page's Id
        // directly (it is the very page `SeedDefaultPagesAsync` just
        // `Store`d in this same session and has not yet committed) — so we do
        // NOT re-query for it: a pre-commit query for the in-flight `help`
        // page returns nothing, which would silently skip every guide on a
        // pristine first boot. A null Id is the impossible state (no `help`
        // page), guarded as a no-op — never create the guides as orphan roots.
        if (string.IsNullOrEmpty(helpPageId))
        {
            return guidePageIds;   // no canonical `help` page — nothing to hang the guides from.
        }

        foreach (var (slug, title, body) in GuidePages())
        {
            var existing = await session
                .Query<Page>()
                .Where(p => p.Slug == slug && p.ParentId == helpPageId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                var newGuide = new Page
                {
                    Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                    Slug = slug,
                    ParentId = helpPageId,   // under the canonical `help` page (ADR 0057 D1)
                    Kind = PageKind.System,   // a platform guide (ADR 0040 standing matrix)
                    Title = title,
                    Body = body,
                    LanguageCode = SourceLanguage,   // authored-in `en` (the floor)
                    Audience = null,            // public — world-readable (the how-to is not gated)
                    AuthorId = string.Empty,    // platform content — no resident author
                    Created = now,
                    Modified = now,
                };
                session.Store(newGuide);
                guidePageIds[slug] = newGuide.Id;   // for SeedGuideTranslationsAsync
            }
            else
            {
                existing.Kind = PageKind.System;   // normalize on re-seed
                existing.Title = title;
                existing.Body = body;   // code wins: refresh the `en` guide body
                existing.Modified = now;
                session.Store(existing);
                guidePageIds[slug] = existing.Id;   // for SeedGuideTranslationsAsync
            }
        }

        return guidePageIds;
    }

    /// <summary>
    /// UG (ADR 0057, amended 2026-09-21) — seed the <c>de</c> / <c>fr</c> /
    /// <c>da</c> <see cref="PageTranslation"/> rows for the seeded resident
    /// guides, attached to the guides' **own** ids (the read path
    /// <c>IPageService.GetTranslationsAsync(page.Id)</c> queries
    /// <c>PageTranslation.PageId == page.Id</c> — the <c>system</c> root or
    /// the <c>help</c> page's id is not a valid parent). Mirrors
    /// <see cref="SeedPageTranslationsAsync"/> exactly (the four-surface set's
    /// baseline lane) — same create-if-missing idiom, same session (C3),
    /// same one <c>SaveChangesAsync</c> in the caller.
    /// <para>
    /// <paramref name="guidePageIds"/> is the slug → guide-Id map
    /// <see cref="SeedUserGuidesAsync"/> returns (the guides' own in-flight
    /// ids, not yet committed — the same "pass straight through" invariant as
    /// the four-surface set).
    /// </para>
    /// <para>
    /// Idempotent (query-then-Store, one row per (guide, language) pair,
    /// <b>create-if-missing</b> — an existing row is skipped, never
    /// refreshed): a pristine DB has no rows yet (create); a warm re-run (the
    /// outer <c>IsPristineAsync</c> gate normally blocks this) leaves existing
    /// rows exactly as written — no duplicates, no overwrite. A non-<c>en</c>
    /// guide body, once a human rewords one in the in-app editor, is
    /// community-owned and never clobbered by a later deploy (the ADR 0042 D1
    /// invariant, unchanged).
    /// </para>
    /// <para>
    /// <see cref="PageTranslation.AuthorId"/> is empty (platform content — no
    /// resident author, the same convention as the four-surface set's rows).
    /// </para>
    /// </summary>
    public static async Task SeedGuideTranslationsAsync(
        IDocumentSession session,
        IReadOnlyDictionary<string, string> guidePageIds,
        DateTimeOffset now,
        CancellationToken ct)
    {
        foreach (var (code, baselines) in new[]
        {
            ("de", DeGuidePages()),
            ("fr", FrGuidePages()),
            ("da", DaGuidePages()),
        })
        {
            foreach (var (slug, title, body) in baselines)
            {
                if (!guidePageIds.TryGetValue(slug, out var pageId))
                    continue;   // defensive — the GuidePages() slugs agree with the baseline slugs; skip rather than throw
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

    /// <summary>
    /// UG (ADR 0057) — **warm-boot backfill** of the guide <see cref="Page"/>
    /// docs for a deployment whose first boot predates the guides: it has the
    /// canonical <c>help</c> page but not its guide children. Mirrors the
    /// <see cref="BackfillPageTranslationsAsync"/> /
    /// <see cref="BackfillUiStringBaselinesAsync"/> shape (ADR 0047 D2 /
    /// ADR 0052) — **create-if-missing only** (the ADR 0042 D1 invariant): a
    /// guide that already exists under <c>help</c> (a community-edited guide,
    /// or a resident-created page that happens to use the same slug) is
    /// skipped, never refreshed — a code-wins refresh would clobber that edit.
    /// Only the absent guides are created, from the same
    /// <see cref="GuidePages"/>() registry the first-boot seeder writes, so a
    /// fresh and a backfilled instance carry the same <c>en</c> floor.
    /// <para>
    /// Idempotent: a second run finds every guide it created on the first run
    /// and skips (the create-if-missing path). No tombstones, no deletes.
    /// </para>
    /// </summary>
    public static async Task BackfillUserGuidesAsync(
        IDocumentSession session,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Resolve the canonical `help` page (the guides hang from it, ADR
        // 0057 D1). On a warm boot `help` is committed, so a query finds it —
        // unlike the first-boot path, where it is still in-flight. Primary
        // resolution is `system/help` (ADR 0040); the bare-slug fallback
        // covers a pre-ADR-0040 deployment whose `help` is still an orphan
        // root (the same fallback shape as BackfillPageTranslationsAsync).
        var systemRoot = await session
            .Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        Page? help = null;
        if (systemRoot is not null)
        {
            help = await session
                .Query<Page>()
                .Where(p => p.Slug == "help" && p.ParentId == systemRoot.Id && p.IsDeleted == false)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }
        if (help is null)
        {
            help = await session
                .Query<Page>()
                .Where(p => p.Slug == "help" && p.ParentId == null && p.IsDeleted == false)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        if (help is null)
        {
            return;   // no canonical `help` page to hang the guides from — nothing to backfill.
        }

        foreach (var (slug, title, body) in GuidePages())
        {
            var existing = await session
                .Query<Page>()
                .Where(p => p.Slug == slug && p.ParentId == help.Id && p.IsDeleted == false)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                session.Store(new Page
                {
                    Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                    Slug = slug,
                    ParentId = help.Id,   // under the canonical `help` page (ADR 0057 D1)
                    Kind = PageKind.System,   // a platform guide (ADR 0040 standing matrix)
                    Title = title,
                    Body = body,
                    LanguageCode = SourceLanguage,   // authored-in `en` (the floor)
                    Audience = null,            // public — world-readable (the how-to is not gated)
                    AuthorId = string.Empty,    // platform content — no resident author
                    Created = now,
                    Modified = now,
                });
            }
            // else: skip — create-if-missing (never overwrite a community edit; ADR 0042 D1).
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// UG (ADR 0057, amended 2026-09-21) — **warm-boot backfill** of the
    /// <c>de</c> / <c>fr</c> / <c>da</c> <see cref="PageTranslation"/> rows
    /// for the resident guides: the gap a deployment whose first boot predates
    /// this baseline lane has — the guides' <c>en</c> body (the
    /// <see cref="GuidePages"/>() floor, created by
    /// <see cref="BackfillUserGuidesAsync"/> or the original first-boot seed)
    /// but no translation rows, so a German / French / Danish-speaking
    /// resident reads the <c>en</c> body for every guide.
    /// <para>
    /// Mirrors <see cref="BackfillPageTranslationsAsync"/> (the four-surface
    /// set's baseline backfill) — **create-if-missing only** (the ADR 0042 D1
    /// invariant): a row for a (guide, language) pair that already exists — a
    /// human Translator's edit in the in-app editor — is skipped, never
    /// refreshed. Only the absent rows are created, from the same
    /// <see cref="DeGuidePages"/> / <see cref="FrGuidePages"/> /
    /// <see cref="DaGuidePages"/> baselines the first-boot seeder uses, so a
    /// fresh and a backfilled instance carry the same non-<c>en</c> baseline.
    /// </para>
    /// <para>
    /// Idempotent: a second run finds every row it created on the first run
    /// and skips (the create-if-missing path). No tombstones, no deletes.
    /// </para>
    /// </summary>
    public static async Task BackfillGuideTranslationsAsync(
        IDocumentSession session,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Resolve the canonical `help` page (the guides hang from it, ADR
        // 0057 D1) — the same `system/help` primary + bare-`help` fallback as
        // BackfillUserGuidesAsync, so the two backfills agree on the parent.
        var systemRoot = await session
            .Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        Page? help = null;
        if (systemRoot is not null)
        {
            help = await session
                .Query<Page>()
                .Where(p => p.Slug == "help" && p.ParentId == systemRoot.Id && p.IsDeleted == false)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }
        if (help is null)
        {
            help = await session
                .Query<Page>()
                .Where(p => p.Slug == "help" && p.ParentId == null && p.IsDeleted == false)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        if (help is null)
        {
            return;   // no canonical `help` page — nothing to backfill translations for.
        }

        foreach (var (slug, _, _) in GuidePages())
        {
            var guide = await session
                .Query<Page>()
                .Where(p => p.Slug == slug && p.ParentId == help.Id && p.IsDeleted == false)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (guide is null)
                continue;   // the guide itself is absent; BackfillUserGuidesAsync owns that gap.

            // The de/fr/da baselines for this guide, create-if-missing (ADR 0042 D1).
            foreach (var (code, baselines) in new[]
            {
                ("de", DeGuidePages()),
                ("fr", FrGuidePages()),
                ("da", DaGuidePages()),
            })
            {
                var baseline = baselines.FirstOrDefault(b => b.Slug == slug);
                if (baseline == default)
                    continue;   // defensive — the slug is not in the baseline set; skip rather than throw.

                var existing = await session
                    .Query<PageTranslation>()
                    .Where(t => t.PageId == guide.Id && t.LanguageCode == code)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    session.Store(new PageTranslation
                    {
                        Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                        PageId = guide.Id,
                        LanguageCode = code,
                        Title = baseline.Title,
                        Body = baseline.Body,
                        AuthorId = string.Empty,   // platform content — no resident author
                        Created = now,
                    });
                }
                // else: skip — create-if-missing (never overwrite a community edit; ADR 0042 D1).
            }
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// UG (ADR 0057) — the canonical <c>en</c> guide bodies: the single source
    /// of the resident-facing guide text (the seeder writes exactly this, and
    /// the <c>UG</c> drift-pin test reads exactly this — the ADR 0042 D1
    /// "the registry is the single source both the seeder and these tests
    /// read, so mirroring is exact" shape, applied to the guides). The closed
    /// set (the ADR 0040 <c>PageKind</c> closed-set rule applied to the guide
    /// set) — a new guide is a new lane, not a silent addition. Each entry is
    /// a <see cref="Page"/> nested under the canonical <c>help</c> page
    /// (ADR 0057 D1), written at the ADR 0042 D2 bar (plain language, the
    /// resident's steps, no code jargon, sentence case).
    /// </summary>
    public static (string Slug, string Title, string Body)[] GuidePages()
    {
        return
        [
            ("getting-started", "Getting started",
             "## Getting started\n\n" +
             "This is the first place to look. Kumunita is a private home for one " +
             "neighborhood — this page and the ones under it walk you through it, " +
             "step by step.\n\n" +
             "**Where things are.** The **feed** is where posts live. **Groups** " +
             "gather residents around a building, a project, or a shared interest. " +
             "The **directory** shows the residents on the platform and the details " +
             "each has chosen to share. The **pages** tree (the **Pages** link) is " +
             "where you find these guides, the terms, the privacy note, and the code " +
             "of conduct.\n\n" +
             "**Your first steps.**\n" +
             "- **Write a post.** Pick **New post**, write it, choose who can see it, " +
             "and click **Post**. See [posts](posts) for the full walk-through.\n" +
             "- **Join a group.** Groups are where a shared interest lives. See " +
             "[groups](groups).\n" +
             "- **Set your language and time zone.** They are saved on your account. " +
             "See [language](language).\n\n" +
             "Everything else has a guide under this page — follow the link for " +
             "that thing.\n"),
            ("posts", "Posts",
             "## Posts\n\n" +
             "A post is a note you share with the people you choose.\n\n" +
             "**To write one.** Pick **New post** at the top of the feed. Type what " +
             "you want to say. Under **Who can see it**, choose the audience. Click " +
             "**Post**.\n\n" +
             "**Who can see it.** By default a post is seen by everyone in the " +
             "neighborhood. You can narrow that — to a specific person, or to a " +
             "group — with the audience picker. See [audience](audience) for what " +
             "each choice means.\n\n" +
             "**Replies.** Anyone who can see your post can reply to it. A reply is " +
             "visible under your post's single audience choice — it doesn't get its " +
             "own audience. You can **Edit** or **Delete** your own replies; the " +
             "platform remembers when one was edited.\n\n" +
             "**Editing and deleting your post.** The author can edit their own " +
             "post, or soft-delete it — it stays where it was, marked as deleted, " +
             "so the thread still makes sense. A global admin (and, where granted, a " +
             "moderator) can remove a post that breaks the code of conduct.\n\n" +
             "**Files.** You can attach images or other files to a post. They are " +
             "stored on the instance and shared under the post's audience.\n"),
            ("groups", "Groups",
             "## Groups\n\n" +
             "A group gathers residents around a shared thing — a building, a " +
             "project, a hobby.\n\n" +
             "**Public and private.** A **public** group is anyone's to join; a " +
             "**private** group is by invitation, and only its members can see it, " +
             "post to it, or be added to a post's audience through it. Both kinds " +
             "let you organize the neighborhood.\n\n" +
             "**To create one.** Pick **Create a group**, give it a name and a " +
             "short description, and choose whether it's public or private. You can " +
             "edit the name and description any time.\n\n" +
             "**Group posts.** A group has its own feed. A post you make in a group " +
             "is seen by the group's members (and anyone you add to its audience). " +
             "You can post to a group the same way you post to the community feed " +
             "— the audience picker just includes the group.\n\n" +
             "**Leaving.** A member can leave a group; the group keeps its posts. A " +
             "group owner or a global admin can remove a member or close a group.\n"),
            ("drafts", "Drafts",
             "## Drafts\n\n" +
             "A draft is a post you've written but not yet shared.\n\n" +
             "**To save one.** In the composer, tick **Save as draft** and save. " +
             "The draft is saved but visible to no one — not even the admins — " +
             "until you publish it.\n\n" +
             "**To find your drafts.** Open **My drafts** in the menu. Every draft " +
             "you've saved is there, with a **Draft** badge.\n\n" +
             "**To publish.** Open the draft and click **Publish**. It becomes " +
             "visible under the audience you set. Until then, only you can see it.\n\n" +
             "Drafts are yours — only the author and a global admin can see or " +
             "change them, and a global admin can remove a draft that shouldn't " +
             "be there.\n"),
            ("audience", "Audience",
             "## Audience\n\n" +
             "Audience is the choice, made when you post, of who can see the post. " +
             "The platform enforces it on every read.\n\n" +
             "**The choices.**\n" +
             "- **Everyone in this neighborhood** — the default. Every member of the " +
             "community can see it.\n" +
             "- **A specific person** — only that resident (and you) can see it.\n" +
             "- **A group** — the group's members (and you) can see it.\n\n" +
             "You can combine the picks — for example, a group *and* a specific " +
             "person. Whatever you pick becomes the post's audience, and nothing " +
             "else.\n\n" +
             "**A reply doesn't have its own audience.** It is visible under the " +
             "post's single audience choice — that's the \"reply-inherits\" rule, " +
             "and it keeps a thread readable for the people already in the room.\n\n" +
             "**You can't change the audience of a post after you've published it.** " +
             "To share it with more people, start a new post with the wider " +
             "audience — the original stays with the people you first chose.\n"),
            ("language", "Language",
             "## Language\n\n" +
             "Kumunita can show itself in more than one language, and you choose " +
             "which one.\n\n" +
             "**To set yours.** Open **Settings**, pick **Choose your language**, " +
             "and select the language you want. Your choice is saved on your " +
             "account.\n\n" +
             "**What it changes.** The interface — buttons, headings, and the " +
             "built-in pages — appears in the language you picked. A post or group " +
             "description that someone has written *and* translated into your " +
             "language shows that translation first, with the original one click " +
             "away.\n\n" +
             "**What it doesn't change.** The words a resident types are their " +
             "words. A post is always read as its author wrote it unless someone " +
             "has added a translation — the platform never machine-translates a " +
             "resident's writing.\n\n" +
             "**Time zone and dates.** You can also set the time zone and the date " +
             "format you see, in the same **Settings** page. Both are saved on " +
             "your account.\n"),
            ("events", "Events",
             "## Events\n\n" +
             "An event is something the neighborhood is doing at a time and a " +
             "place — a meeting, a repair day, a social.\n\n" +
             "**To see them.** Events appear in the feed and on the events list, " +
             "with their date, time, and place.\n\n" +
             "**To join.** Open the event and pick **RSVP**. Your response is " +
             "recorded against the event and your account, and you can change it " +
             "any time before the event.\n\n" +
             "**The reminder.** You can opt in to a reminder the day before the " +
             "event. It's sent once, to your account, and only if you asked for " +
             "it — there is no reminder you didn't sign up for.\n\n" +
             "**Creating one.** A global admin (or, where granted, a moderator) " +
             "can create an event for the neighborhood, set its audience, and " +
             "choose whether it carries a reminder. You can post about an event in " +
             "the feed the same way you post about anything else.\n"),
            ("translator", "Translators",
             "## Translators\n\n" +
             "A **Translator** is a resident granted the standing to add and edit " +
             "a language version of a thing the platform already carries — a page, " +
             "a group description, a community name, or a post or reply.\n\n" +
             "**What a Translator may do.**\n" +
             "- **Add a translation** of a page, a group, a community name, a post, " +
             "or a reply — in a language the instance has enabled.\n" +
             "- **Edit a translation** they added, any time.\n\n" +
             "**What a Translator may not do.**\n" +
             "- **Change the original.** The author's words are the author's; a " +
             "Translator adds a language version beside them, never over them.\n" +
             "- **Translate for the machine.** A translation is a human act — the " +
             "platform never machine-translates a resident's writing, and a " +
             "Translator doesn't paste a machine output in as if it were their " +
             "own.\n" +
             "- **Stand in for a GlobalAdmin.** A Translator has no standing on the " +
             "platform's own pages, on moderation, or on a group's membership — " +
             "those are a GlobalAdmin's (or, where granted, a moderator's) " +
             "lanes.\n\n" +
             "A Translator's work is visible: the interface shows, next to a " +
             "translated thing, the one who added that translation, and the " +
             "original is always one click away.\n"),
            ("child-accounts", "Child accounts",
             "## Child accounts\n\n" +
             "A child account is an account you set up for a child, and you keep " +
             "the controls over it. You set it up because you created the account " +
             "— that standing is what gives you the controls, and it does not let " +
             "you read what the child writes.\n\n" +
             "**What you can do.** You can suspend and un-suspend the account, " +
             "decide which groups and communities the child belongs to, approve a " +
             "group invitation the child received, and assign another guardian to " +
             "share the controls. You can also hand the account over when the child " +
             "is ready to run it on their own.\n\n" +
             "**What you can't do.** You can't read the child's posts, replies, or " +
             "profile. Those are the child's, and they stay the child's.\n\n" +
             "**To add one.** Open **Account → Children**, fill in the child's " +
             "display name, email, and a password, and click **Add a child " +
             "account**. The child verifies their own email to sign in — the usual " +
             "sign-up flow.\n\n" +
             "**To suspend or un-suspend.** On the **Children** page, find the " +
             "child and click **Suspend** or **Un-suspend**. A suspended child " +
             "can't sign in until you un-suspend them.\n\n" +
             "**To manage memberships.** On the child's page, add or remove group " +
             "and community memberships under **Group memberships** and " +
             "**Community memberships**.\n\n" +
             "**To approve a group invitation.** On the child's page, find the " +
             "invitation under **Pending group invitations** and click " +
             "**Approve**.\n\n" +
             "**To assign another guardian.** On the child's page, open " +
             "**Assign a guardian**, enter their email, and click **Assign**. " +
             "They then hold the same controls you do.\n\n" +
             "**To hand over the account.** When the child is ready, open their " +
             "page and click **Dissolve guardianship**. Their memberships are " +
             "kept, and their own controls come back on the next read.\n"),
            ("being-a-child", "Being a child on Kumunita",
             "## Being a child on Kumunita\n\n" +
             "A child account is one your parent set up for you. It works like a " +
             "normal account — you read the feed, write posts, and reply — with a " +
             "few things your parent handles for you.\n\n" +
             "**What stays yours.** Your posts, replies, and profile are yours. " +
             "Your parent can't read them, even though they set up the account.\n\n" +
             "**What your parent handles.** Your parent decides which groups and " +
             "communities you're in, and can suspend or un-suspend your account.\n\n" +
             "**Group invitations.** You'll see an invitation under **Invitations**. " +
             "You can always **Decline**. To **Accept**, your parent has to approve " +
             "it first — ask them to approve it for you.\n\n" +
             "**When you're ready to take over.** When your parent hands the " +
             "account over, your own controls come back: you can accept group " +
             "invitations yourself and manage your own groups and communities.\n"),
            ("admins", "Admins",
             "## Admins\n\n" +
             "An **admin** (the platform's global admin) is the resident who keeps " +
             "the instance running for the whole neighborhood. Most days you'll " +
             "never meet one — you can do almost everything without them — but " +
             "here is what the role covers.\n\n" +
             "**What an admin does.**\n" +
             "- **Accounts.** Verify a new account, block or unblock a resident, " +
             "and decide who holds which elevated role.\n" +
             "- **Communities.** Add and edit the communities, and decide which " +
             "of them every resident belongs to.\n" +
             "- **The platform pages.** Edit the terms, the help guides, the " +
             "privacy note, and the code of conduct — and reset a page back to " +
             "its shipped text when you want the latest wording.\n" +
             "- **Sign-up.** Choose whether new residents can sign up on their " +
             "own, or only when you invite them.\n" +
             "- **The platform defaults.** Set the default language, time zone, " +
             "and date format for the instance. Every resident can still " +
             "override any of these on their own account.\n" +
             "- **Moderation.** See every report, and act on one — the standing " +
             "to assign, unlock, and resolve a report stays with an admin, even " +
             "when a moderator can see it.\n\n" +
             "**What keeps it in check.** Every admin action is written to an " +
             "audit trail you can review, and the standing is narrow by design: " +
             "an admin keeps the instance running, but they do not read your " +
             "posts, replies, or profile — audience still decides what is " +
             "visible, and a report is never a back-door into someone's " +
             "content.\n"),
            ("moderators", "Moderators",
             "## Moderators\n\n" +
             "A **moderator** is a resident you've granted the standing to look " +
             "after one part of the neighborhood — a community or a group — and " +
             "to keep it a safe place.\n\n" +
             "**What a moderator may do.**\n" +
             "- **See the reports** that concern the part of the neighborhood " +
             "they moderate. The report queue shows only those.\n" +
             "- **Read a report** in detail, including who filed it and what " +
             "they're asking for.\n\n" +
             "**What a moderator may not do.**\n" +
             "- **Act on a report.** Assigning, unlocking, and resolving a " +
             "report are an admin's standing — a moderator can see a report, " +
             "but the platform won't let them take an action on it.\n" +
             "- **See outside their part.** A moderator only sees the reports " +
             "that concern the part they were granted. Reports elsewhere are " +
             "not in their queue.\n" +
             "- **Read anyone's content.** Seeing a report is not a back-door " +
             "into a resident's posts, replies, or profile — audience still " +
             "decides what is visible, and a report doesn't change that.\n\n" +
             "A moderator is a neighbor helping keep their part of the " +
             "neighborhood in good order — not an admin, and not a reader of " +
             "other people's content.\n"),
            ("notifications", "Notifications",
             "## Notifications\n\n" +
             "Kumunita lets you know when something happens to the things you're " +
             "part of — a reply on your post, an RSVP on your event, a new post " +
             "in one of your groups, a to-do that lands with you.\n\n" +
             "**Where to find them.** Open the **bell** in the top bar. It shows " +
             "how many notifications are new, and a short list of the latest. " +
             "Open the **inbox** for the full list, newest first. The inbox " +
             "always records every notification — it's the reliable record.\n\n" +
             "**Email.** For the kinds you care about, you also get an email. " +
             "The email is in the language you chose for emails on your " +
             "profile. Turning a kind off in your preferences stops the email " +
             "for that kind — it never stops the inbox.\n\n" +
             "**Your choices.** Two places:\n" +
             "- **Notification preferences** — the on/off switch for each kind " +
             "of email.\n" +
             "- **Subscriptions** — the finer switches: follow a community or " +
             "group to get a post when it's made there, or follow the platform " +
             "for announcements.\n\n" +
             "You can mark the whole inbox as read in one click. What you " +
             "follow and what you get by email is always up to you.\n"),
        ];
    }

    /// <summary>
    /// UG (ADR 0057, amended 2026-09-21) — the curated <c>de</c> baseline for
    /// the resident guides: the single source of the seeded German guide
    /// bodies (the same shape as <see cref="GuidePages"/>() — a public static
    /// array of <c>(Slug, Title, Body)</c> tuples). A full translation of the
    /// <see cref="GuidePages"/>() bodies — the same Markdown structure
    /// (heading, the step blocks, the cross-links), idiomatic German UI copy at
    /// the ADR 0042 D2 bar (the <c>du</c> register held, sentence case, no
    /// word-for-word calques). These ship as
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows on a pristine DB
    /// (ADR 0042 D1 — seeded once, then community-owned; the in-app editor
    /// supersedes them and no later deploy reverts a human edit).
    /// <para>
    /// <b>Cross-links are absolute.</b> A guide's canonical URL is
    /// <c>/pages/system/help/{slug}</c> (the <c>system</c> root is part of the
    /// derived path — the guides are grandchildren of <c>system</c>), so a
    /// sibling link like the <c>en</c> floor's <c>[posts](posts)</c> does not
    /// resolve on the tree-browse page. These translations therefore link with
    /// the absolute form <c>[posts](/pages/system/help/posts)</c>, which is a
    /// relative <c>/</c>-rooted path the <see cref="Kumunita.Web.Security.MarkdownRenderer"/>
    /// <c>IsSafeUrl</c> allowlist accepts. The <c>en</c> floor
    /// (<see cref="GuidePages"/>) is untouched — only the seeded baselines use
    /// the absolute form, and both render identically for the resident.
    /// </para>
    /// </summary>
    public static (string Slug, string Title, string Body)[] DeGuidePages()
    {
        return
        [
            ("getting-started", "Erste Schritte",
             "## Erste Schritte\n\n" +
             "Das ist der erste Ort, an den du dich wendest. Kumunita ist ein " +
             "privater Ort für genau ein Viertel — diese Seite und die Seiten " +
             "darunter führen dich Schritt für Schritt durch alles.\n\n" +
             "**Wo was ist.** Der **Feed** ist der Ort der Beiträge. **Gruppen** " +
             "sammeln Bewohner um ein Gebäude, ein Projekt oder ein gemeinsames " +
             "Interesse. Das **Verzeichnis** zeigt die Bewohner auf der Plattform " +
             "und die Angaben, die jeder von ihnen teilen möchte. Der **Seiten**-Baum " +
             "(der Link **Seiten**) ist der Ort dieser Anleitungen, der Nutzungs-" +
             "bedingungen, des Datenschutzes und des Verhaltenskodex.\n\n" +
             "**Deine ersten Schritte.**\n" +
             "- **Einen Beitrag schreiben.** Wähle **Neuer Beitrag**, schreibe ihn, " +
             "wähle, wer ihn sehen darf, und klicke auf **Posten**. Siehe " +
             "[Beiträge](/pages/system/help/posts) für die vollständige Anleitung.\n" +
             "- **Einer Gruppe beitreten.** Gruppen sind der Ort eines gemeinsamen " +
             "Interesses. Siehe [Gruppen](/pages/system/help/groups).\n" +
             "- **Sprache und Zeitzone einstellen.** Sie werden auf deinem Konto " +
             "gespeichert. Siehe [Sprache](/pages/system/help/language).\n\n" +
             "Für alles andere gibt es unter dieser Seite eine Anleitung — folge " +
             "dem Link zu dem, was du tun willst.\n"),
            ("posts", "Beiträge",
             "## Beiträge\n\n" +
             "Ein Beitrag ist eine Notiz, die du mit den Menschen teilst, die du " +
             "wählst.\n\n" +
             "**So schreibst du einen.** Wähle oben im Feed **Neuer Beitrag**. Gib " +
             "ein, was du sagen möchtest. Unter **Wer darf ihn sehen** wählst du die " +
             "Zielgruppe. Klicke auf **Posten**.\n\n" +
             "**Wer darf ihn sehen.** Standardmäßig sieht ihn jeder im Viertel. Du " +
             "kannst das eingrenzen — auf eine bestimmte Person oder auf eine Gruppe " +
             "— über den Zielgruppen-Auswahlbereich. Siehe [Zielgruppe](/pages/system/help/audience), " +
             "was jede Wahl bedeutet.\n\n" +
             "**Antworten.** Jeder, der deinen Beitrag sehen kann, kann darauf " +
             "antworten. Eine Antwort ist unter der einzelnen Zielgruppenwahl deines " +
             "Beitrags sichtbar — sie hat keine eigene Zielgruppe. Du kannst deine " +
             "eigenen Antworten **Bearbeiten** oder **Löschen**; die Plattform merkt " +
             "sich, wann eine bearbeitet wurde.\n\n" +
             "**Einen Beitrag bearbeiten oder löschen.** Der Autor kann seinen " +
             "Beitrag bearbeiten oder weich löschen — er bleibt dort, wo er war, " +
             "gekennzeichnet als gelöscht, damit der Faden noch Sinn ergibt. Ein " +
             "Global-Admin (und, wo zugewiesen, ein Moderator) kann einen Beitrag " +
             "entfernen, der den Verhaltenskodex verletzt.\n\n" +
             "**Dateien.** Du kannst einem Beitrag Bilder oder andere Dateien " +
             "anhängen. Sie werden auf der Instanz gespeichert und unter der " +
             "Zielgruppe des Beitrags geteilt.\n"),
            ("groups", "Gruppen",
             "## Gruppen\n\n" +
             "Eine Gruppe sammelt Bewohner um eine gemeinsame Sache — ein Gebäude, " +
             "ein Projekt, ein Hobby.\n\n" +
             "**Öffentlich und privat.** Eine **öffentliche** Gruppe kann jeder " +
             "beitreten; eine **private** Gruppe nur auf Einladung, und nur ihre " +
             "Mitglieder können sie sehen, darauf posten oder sie über einen " +
             "Beitrag in die Zielgruppe nehmen. Beide Arten helfen dir, das " +
             "Viertel zu organisieren.\n\n" +
             "**So erstellst du eine.** Wähle **Gruppe erstellen**, gib einen Namen " +
             "und eine kurze Beschreibung und wähle, ob sie öffentlich oder privat " +
             "ist. Namen und Beschreibung kannst du jederzeit bearbeiten.\n\n" +
             "**Gruppenbeiträge.** Eine Gruppe hat einen eigenen Feed. Ein Beitrag, " +
             "den du in einer Gruppe machst, wird von den Mitgliedern der Gruppe " +
             "gesehen (und von allen, die du in ihre Zielgruppe nimmst). Du kannst " +
             "in eine Gruppe posten wie in den Gemeinschafts-Feed — der " +
             "Zielgruppen-Auswahlbereich enthält dann einfach die Gruppe.\n\n" +
             "**Verlassen.** Ein Mitglied kann eine Gruppe verlassen; die Gruppe " +
             "behält ihre Beiträge. Ein Gruppenbesitzer oder ein Global-Admin kann " +
             "ein Mitglied entfernen oder eine Gruppe schließen.\n"),
            ("drafts", "Entwürfe",
             "## Entwürfe\n\n" +
             "Ein Entwurf ist ein Beitrag, den du geschrieben hast, aber noch " +
             "nicht geteilt hast.\n\n" +
             "**So speicherst du einen.** Im Editor hake **Als Entwurf speichern** " +
             "an und speichere. Der Entwurf ist gespeichert, aber niemand kann ihn " +
             "sehen — auch nicht die Admins — bis du ihn veröffentlicht hast.\n\n" +
             "**Deine Entwürfe finden.** Öffne **Meine Entwürfe** im Menü. Jeder " +
             "Entwurf, den du gespeichert hast, ist dort, mit einem **Entwurf**-" +
             "Kennzeichen.\n\n" +
             "**Veröffentlichen.** Öffne den Entwurf und klicke auf " +
             "**Veröffentlichen**. Er wird unter der Zielgruppe sichtbar, die du " +
             "gesetzt hast. Bis dahin sieht ihn nur du.\n\n" +
             "Entwürfe gehören dir — nur der Autor und ein Global-Admin können sie " +
             "sehen oder ändern, und ein Global-Admin kann einen Entwurf entfernen, " +
             "der dort nicht hingehört.\n"),
            ("audience", "Zielgruppe",
             "## Zielgruppe\n\n" +
             "Zielgruppe ist die Wahl, die du beim Posten triffst, wer den Beitrag " +
             "sehen darf. Die Plattform erzwingt sie bei jedem Lesen.\n\n" +
             "**Die Wahlen.**\n" +
             "- **Alle in diesem Viertel** — der Standard. Jedes Mitglied der " +
             "Gemeinschaft kann ihn sehen.\n" +
             "- **Eine bestimmte Person** — nur dieser Bewohner (und du) kann ihn " +
             "sehen.\n" +
             "- **Eine Gruppe** — die Mitglieder der Gruppe (und du) können ihn " +
             "sehen.\n\n" +
             "Du kannst die Wahlen kombinieren — zum Beispiel eine Gruppe *und* " +
             "eine bestimmte Person. Was auch immer du wählst, wird die Zielgruppe " +
             "des Beitrags — und nichts anderes.\n\n" +
             "**Eine Antwort hat keine eigene Zielgruppe.** Sie ist unter der " +
             "einzigen Zielgruppenwahl des Beitrags sichtbar — das ist die " +
             "„Antwort-erbt\u201C-Regel, und sie hält den Faden für die Leute lesbar, " +
             "die bereits im Raum sind.\n\n" +
             "**Die Zielgruppe eines Beitrags lässt sich nach der Veröffentlichung " +
             "nicht ändern.** Um ihn mit mehr Leuten zu teilen, starte einen neuen " +
             "Beitrag mit der breiteren Zielgruppe — der ursprüngliche bleibt bei " +
             "denen, die du zuerst gewählt hast.\n"),
            ("language", "Sprache",
             "## Sprache\n\n" +
             "Kumunita kann sich in mehr als einer Sprache zeigen, und du wählst, " +
             "in welcher.\n\n" +
             "**So stellst du deine ein.** Öffne **Einstellungen**, wähle **Wähle " +
             "deine Sprache** und wähle die Sprache, die du möchtest. Deine Wahl " +
             "wird auf deinem Konto gespeichert.\n\n" +
             "**Was sich ändert.** Die Oberfläche — Knöpfe, Überschriften und die " +
             "integrierten Seiten — erscheint in der Sprache, die du gewählt hast. " +
             "Ein Beitrag oder eine Gruppensbeschreibung, die jemand geschrieben " +
             "*und* in deine Sprache übersetzt hat, zeigt diese Übersetzung zuerst, " +
             "das Original ist einen Klick entfernt.\n\n" +
             "**Was sich nicht ändert.** Die Worte, die ein Bewohner tippt, sind " +
             "seine. Ein Beitrag wird immer so gelesen, wie sein Autor ihn " +
             "geschrieben hat, es sei denn, jemand hat eine Übersetzung " +
             "hinzugefügt — die Plattform maschinensetzt nie das Schreiben eines " +
             "Bewohners.\n\n" +
             "**Zeitzone und Datum.** Du kannst auch die Zeitzone und das " +
             "Datumsformat, das du siehst, in derselben **Einstellung**-Seite " +
             "einstellen. Beides wird auf deinem Konto gespeichert.\n"),
            ("events", "Termine",
             "## Termine\n\n" +
             "Ein Termin ist etwas, das das Viertel zu einer Zeit an einem Ort " +
             "macht — ein Treffen, ein Reparaturtag, ein Beisammensein.\n\n" +
             "**So siehst du sie.** Termine erscheinen im Feed und auf der " +
             "Terminliste mit ihrem Datum, ihrer Uhrzeit und ihrem Ort.\n\n" +
             "**So nimmst du teil.** Öffne den Termin und wähle **Teilnehmen**. " +
             "Deine Antwort wird dem Termin und deinem Konto zugeordnet, und du " +
             "kannst sie jederzeit vor dem Termin ändern.\n\n" +
             "**Die Erinnerung.** Du kannst dich für eine Erinnerung am Vortag " +
             "anmelden. Sie wird einmalig an dein Konto gesendet, und nur wenn du " +
             "dich dafür angemeldet hast — es gibt keine Erinnerung, für die du " +
             "dich nicht angemeldet hast.\n\n" +
             "**Einen anlegen.** Ein Global-Admin (und, wo zugewiesen, ein " +
             "Moderator) kann einen Termin für das Viertel anlegen, seine " +
             "Zielgruppe setzen und wählen, ob er eine Erinnerung trägt. Du " +
             "kannst über einen Termin im Feed posten wie über alles andere.\n"),
            ("translator", "Übersetzer",
             "## Übersetzer\n\n" +
             "Ein **Übersetzer** ist ein Bewohner, dem das Recht zugewiesen ist, " +
             "eine Sprachversion eines Dings hinzuzufügen und zu bearbeiten, das " +
             "die Plattform bereits trägt — eine Seite, eine Gruppensbeschreibung, " +
             "einen Gemeindenamen oder einen Beitrag oder eine Antwort.\n\n" +
             "**Was ein Übersetzer darf.**\n" +
             "- **Eine Übersetzung hinzufügen** einer Seite, einer Gruppe, eines " +
             "Gemeindenamens, eines Beitrags oder einer Antwort — in einer Sprache, " +
             "die die Instanz aktiviert hat.\n" +
             "- **Eine Übersetzung bearbeiten**, die er hinzugefügt hat, jederzeit.\n\n" +
             "**Was ein Übersetzer nicht darf.**\n" +
             "- **Das Original ändern.** Die Worte des Autors sind die des " +
             "Autors; ein Übersetzer fügt eine Sprachversion daneben hinzu, nie " +
             "darüber.\n" +
             "- **Für die Maschine übersetzen.** Eine Übersetzung ist eine " +
             "menschliche Handlung — die Plattform maschinensetzt nie das Schreiben " +
             "eines Bewohners, und ein Übersetzer fügt kein Maschinen-Output ein, " +
             "als wäre es sein eigenes.\n" +
             "- **Einen Global-Admin vertreten.** Ein Übersetzer hat kein Recht auf " +
             "den eigenen Seiten der Plattform, auf Moderation oder auf die " +
             "Mitgliedschaft einer Gruppe — das sind die Lanes eines " +
             "Global-Admins (oder, wo zugewiesen, eines Moderators).\n\n" +
             "Die Arbeit eines Übersetzers ist sichtbar: die Oberfläche zeigt neben " +
             "einem übersetzten Ding die Person, die diese Übersetzung " +
             "hinzugefügt hat, und das Original ist immer einen Klick entfernt.\n"),
            ("child-accounts", "Kinderkonten",
             "## Kinderkonten\n\n" +
             "Ein Kinderkonto ist ein Konto, das du für ein Kind einrichtest, und " +
             "die Kontrollen darüber behältst du. Du hast das Konto eingerichtet — " +
             "genau das gibt dir die Kontrollen, aber es lässt dich nicht lesen, " +
             "was das Kind schreibt.\n\n" +
             "**Was du kannst.** Du kannst das Konto sperren und wieder aktivieren, " +
             "entscheiden, in welchen Gruppen und Gemeinschaften das Kind ist, " +
             "eine Gruppeneinladung genehmigen, die das Kind erhalten hat, und " +
             "einen weiteren Vormund zuweisen, mit dem du die Kontrollen teilst. " +
             "Und du kannst das Konto übergeben, wenn das Kind bereit ist, es " +
             "selbst zu führen.\n\n" +
             "**Was du nicht kannst.** Du kannst die Beiträge, Antworten und das " +
             "Profil des Kindes nicht lesen. Das sind des Kindes, und es bleibt " +
             "des Kindes.\n\n" +
             "**Zum Hinzufügen.** Öffne **Konto → Kinder**, gib den Anzeigenamen, " +
             "die E-Mail-Adresse und ein Passwort des Kindes ein und klicke auf " +
             "**Kinderkonto hinzufügen**. Das Kind bestätigt seine eigene E-Mail " +
             "zur Anmeldung — der gewöhnliche Anmeldevorgang.\n\n" +
             "**Zum Sperren oder Wieder aktivieren.** Auf der Seite **Kinder** " +
             "findest du das Kind und klickst auf **Sperren** oder " +
             "**Wieder aktivieren**. Ein gesperrtes Kind kann sich nicht " +
             "anmelden, bis du es wieder aktivierst.\n\n" +
             "**Mitgliedschaften verwalten.** Auf der Seite des Kindes fügst du " +
             "Gruppen- und Gemeinschaftsmitgliedschaften unter " +
             "**Gruppenmitgliedschaften** und **Gemeinschaftsmitgliedschaften** " +
             "hinzu oder entfernst sie.\n\n" +
             "**Eine Gruppeneinladung genehmigen.** Auf der Seite des Kindes " +
             "findest du die Einladung unter **Ausstehende Gruppeneinladungen** " +
             "und klickst auf **Genehmigen**.\n\n" +
             "**Einen weiteren Vormund zuweisen.** Auf der Seite des Kindes " +
             "öffnest du **Vormund zuweisen**, gibst dessen E-Mail ein und klickst " +
             "auf **Zuweisen**. Dann hat er dieselben Kontrollen wie du.\n\n" +
             "**Das Konto übergeben.** Wenn das Kind bereit ist, öffne seine Seite " +
             "und klicke auf **Vormundschaft auflösen**. Die Mitgliedschaften " +
             "bleiben erhalten, und die eigenen Kontrollen kommen beim nächsten " +
             "Lesen zurück.\n"),
            ("being-a-child", "Ein Kinderkonto nutzen",
             "## Ein Kinderkonto nutzen\n\n" +
             "Ein Kinderkonto ist eines, das deine Eltern für dich eingerichtet " +
             "haben. Es funktioniert wie ein normales Konto — du liest den Feed, " +
             "schreibst Beiträge und antwortest — nur ein paar Dinge übernehmen " +
             "deine Eltern für dich.\n\n" +
             "**Was dir gehört.** Deine Beiträge, Antworten und dein Profil gehören " +
             "dir. Deine Eltern können sie nicht lesen, auch wenn sie das Konto " +
             "eingerichtet haben.\n\n" +
             "**Was deine Eltern übernehmen.** Deine Eltern entscheiden, in welchen " +
             "Gruppen und Gemeinschaften du bist, und können dein Konto sperren " +
             "oder wieder aktivieren.\n\n" +
             "**Gruppeneinladungen.** Eine Einladung findest du unter " +
             "**Einladungen**. Du kannst sie immer **Ablehnen**. Um sie " +
             "**Anzunehmen**, müssen deine Eltern sie erst genehmigen — bitte sie, " +
             "sie für dich zu genehmigen.\n\n" +
             "**Wenn du bereit bist, es selbst zu übernehmen.** Wenn deine Eltern " +
             "dir das Konto übergeben, kommen deine eigenen Kontrollen zurück: Du " +
             "kannst Gruppeneinladungen selbst annehmen und deine eigenen Gruppen " +
             "und Gemeinschaften verwalten.\n"),
            ("admins", "Administratoren",
             "## Administratoren\n\n" +
             "Ein **Administrator** (der globale Admin der Plattform) ist die " +
             "Person, die die Instanz für das ganze Viertel in Betrieb hält. Den " +
             "Alltag über wirst du sie kaum bemerken — die meisten Dinge kannst " +
             "du ganz ohne sie erledigen. Hier ist, wofür die Rolle zuständig " +
             "ist.\n\n" +
             "**Was ein Administrator macht.**\n" +
             "- **Konten.** Ein neues Konto bestätigen, eine Person blockieren " +
             "oder wieder freigeben und entscheiden, wer welche erhöhte Rolle " +
             "trägt.\n" +
             "- **Gemeinschaften.** Gemeinschaften anlegen und bearbeiten und " +
             "festlegen, welchen davon jede Person angehört.\n" +
             "- **Die Plattformseiten.** Die Nutzungsbedingungen, die Hilfeseiten, " +
             "den Datenschutzhinweis und die Nutzungsordnung bearbeiten — und " +
             "eine Seite auf ihre ausgelieferte Fassung zurücksetzen, wenn du " +
             "den neuesten Text möchtest.\n" +
             "- **Registrierung.** Festlegen, ob sich neue Personen von allein " +
             "registrieren dürfen oder nur, wenn du sie einlädst.\n" +
             "- **Die Plattform-Voreinstellungen.** Die Standardsprache, die " +
             "Zeitzone und das Datumsformat der Instanz setzen. Jede Person " +
             "kann all das auf ihrem eigenen Konto individuell überschreiben.\n" +
             "- **Moderation.** Jeden Bericht sehen und einen abschließen — die " +
             "Befugnis, über einen Bericht zu entscheiden, bleibt beim " +
             "Administrator, auch wenn ein Moderator ihn einsehen kann.\n\n" +
             "**Was die Rolle bremst.** Jede Administratoraktion wird in einem " +
             "Prüfprotokoll festgehalten, das du einsehen kannst, und die Rolle " +
             "ist bewusst schmal gehalten: Ein Administrator hält die Instanz in " +
             "Gang, liest aber weder deine Beiträge noch deine Antworten noch " +
             "dein Profil — wer etwas sehen darf, bestimmt weiterhin die " +
             "Zielgruppe, und ein Bericht ist nie eine Hintertür zu den " +
             "Inhalten einer Person.\n"),
            ("moderators", "Moderatoren",
             "## Moderatoren\n\n" +
             "Ein **Moderator** ist eine Person, der du die Befugnis gegeben " +
             "hast, auf einen Teil deines Viertels zu achten — eine Gemeinschaft " +
             "oder eine Gruppe — und diesen Ort sicher zu halten.\n\n" +
             "**Was ein Moderator darf.**\n" +
             "- **Die Berichte sehen**, die seinen Bereich betreffen. Die " +
             "Berichtsanzeige zeigt genau diese Berichte.\n" +
             "- **Einen Bericht** im Detail lesen, einschließlich wer ihn " +
             "eingereicht hat und um was es geht.\n\n" +
             "**Was ein Moderator nicht darf.**\n" +
             "- **Über einen Bericht entscheiden.** Zuweisen, Freischalten und " +
             "Abschließen sind die Befugnis eines Administrators — ein " +
             "Moderator kann einen Bericht sehen, aber die Plattform lässt ihn " +
             "keine Handlung darüber ausführen.\n" +
             "- **Hinausgucken.** Ein Moderator sieht nur die Berichte, die den " +
             "Bereich betreffen, für den er zuständig ist. Berichte von woanders " +
             "stehen nicht in seiner Anzeige.\n" +
             "- **Die Inhalte beliebiger Person lesen.** Einen Bericht zu sehen " +
             "ist keine Hintertür zu den Beiträgen, Antworten oder dem Profil " +
             "einer Person — wer etwas sehen darf, bestimmt weiterhin die " +
             "Zielgruppe, und ein Bericht ändert das nicht.\n\n" +
             "Ein Moderator ist eine Nachbarin, die ihren Teil des Viertels " +
             "ordentlich hält — kein Administrator und kein Leser fremder " +
             "Inhalte.\n"),
            ("notifications", "Benachrichtigungen",
             "## Benachrichtigungen\n\n" +
             "Kumunita gibt dir Bescheid, wenn etwas mit den Dingen passiert, " +
             "die dich betreffen — eine Antwort auf deinen Beitrag, eine " +
             "Anmeldung für dein Ereignis, ein neuer Beitrag in einer deiner " +
             "Gruppen, eine Aufgabe, die bei dir landet.\n\n" +
             "**Wo du sie findest.** Öffne die **Glocke** in der Leiste oben. " +
             "Sie zeigt, wie viele Benachrichtigungen neu sind, und eine " +
             "kurze Liste der jüngsten. Öffne das **Postfach** für die " +
             "vollständige Liste, neueste zuerst. Das Postfach erfasst jede " +
             "Benachrichtigung zuverlässig — es ist die verlässliche " +
             "Aufzeichnung.\n\n" +
             "**E-Mail.** Für die Arten, die dich interessieren, bekommst du " +
             "auch eine E-Mail. Die E-Mail ist in der Sprache, die du für " +
             "E-Mails in deinem Profil gewählt hast. Schaltest du eine Art " +
             "in deinen Einstellungen aus, unterlässt das die E-Mail für " +
             "diese Art — nie das Postfach.\n\n" +
             "**Deine Wahl.** Zwei Orte:\n" +
             "- **Benachrichtigungseinstellungen** — der Ein-/Ausschalter " +
             "für die E-Mails jeder Art.\n" +
             "- **Abonnements** — die feineren Schalter: folge einer " +
             "Gemeinschaft oder Gruppe, um deren Beiträge zu erhalten, oder " +
             "der Plattform für Ankündigungen.\n\n" +
             "Du kannst das ganze Postfach mit einem Klick als gelesen " +
             "markieren. Was du folgst und was dir per E-Mail kommt, " +
             "entscheidest du allein.\n"),
        ];
    }

    /// <summary>
    /// UG (ADR 0057, amended 2026-09-21) — the curated <c>fr</c> baseline for
    /// the resident guides: the single source of the seeded French guide
    /// bodies (the same shape as <see cref="GuidePages"/>() — a public static
    /// array of <c>(Slug, Title, Body)</c> tuples). A full translation of the
    /// <see cref="GuidePages"/>() bodies — the same Markdown structure
    /// (heading, the step blocks, the cross-links), idiomatic French UI copy at
    /// the ADR 0042 D2 bar (the familiar <c>tu</c> register held, sentence
    /// case, no word-for-word calques). These ship as
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows on a pristine DB
    /// (ADR 0042 D1 — seeded once, then community-owned; the in-app editor
    /// supersedes them and no later deploy reverts a human edit).
    /// <para>
    /// <b>Cross-links are absolute</b> — see <see cref="DeGuidePages"/>() for
    /// the rationale (a guide's canonical URL is
    /// <c>/pages/system/help/{slug}</c>; the <c>en</c> floor's relative links
    /// do not resolve on the tree-browse page, so the baselines link with the
    /// absolute <c>/pages/system/help/…</c> form, which the
    /// <see cref="Kumunita.Web.Security.MarkdownRenderer"/> allowlist accepts).
    /// The <c>en</c> floor is untouched.
    /// </para>
    /// </summary>
    public static (string Slug, string Title, string Body)[] FrGuidePages()
    {
        return
        [
            ("getting-started", "Premiers pas",
             "## Premiers pas\n\n" +
             "C'est le premier endroit à consulter. Kumunita est un foyer privé " +
             "pour un seul quartier — cette page et les pages en dessous te " +
             "guident pas à pas.\n\n" +
             "**Où sont les choses.** Le **fil** est le lieu des messages. Les " +
             "**groupes** rassemblent les riverains autour d'un bâtiment, d'un " +
             "projet ou d'un intérêt partagé. L'**annuaire** montre les résidents " +
             "de la plateforme et les détails que chacun a choisi de partager. " +
             "L'**arborescence des pages** (le lien **Pages**) est le lieu de ces " +
             "guides, des conditions d'utilisation, de la confidentialité et du " +
             "code de conduite.\n\n" +
             "**Tes premiers pas.**\n" +
             "- **Écrire un message.** Choisis **Nouveau message**, rédige-le, " +
             "choisis qui peut le voir, et clique sur **Publier**. Voir " +
             "[Messages](/pages/system/help/posts) pour le guide complet.\n" +
             "- **Rejoindre un groupe.** Les groupes sont le lieu d'un intérêt " +
             "partagé. Voir [Groupes](/pages/system/help/groups).\n" +
             "- **Réglage la langue et le fuseau horaire.** Ils sont enregistrés " +
             "sur ton compte. Voir [Langue](/pages/system/help/language).\n\n" +
             "Tout le reste a un guide sous cette page — suis le lien vers ce " +
             "que tu veux faire.\n"),
            ("posts", "Messages",
             "## Messages\n\n" +
             "Un message est une note que tu partages avec les personnes que tu " +
             "choisis.\n\n" +
             "**Pour en écrire un.** Choisis **Nouveau message** en haut du fil. " +
             "Écris ce que tu veux dire. Sous **Qui peut le voir**, choisis " +
             "l'audience. Clique sur **Publier**.\n\n" +
             "**Qui peut le voir.** Par défaut, il est vu par tout le quartier. Tu " +
             "peux restreindre — à une personne précise, ou à un groupe — avec le " +
             "sélecteur d'audience. Voir [Audience](/pages/system/help/audience) " +
             "pour ce que chaque choix signifie.\n\n" +
             "**Réponses.** N'importe qui qui peut voir ton message peut y " +
             "répondre. Une réponse est visible sous l'audience unique de ton " +
             "message — elle n'a pas sa propre audience. Tu peux **Modifier** ou " +
             "**Supprimer** tes propres réponses ; la plateforme se souvient " +
             "quand une réponse a été modifiée.\n\n" +
             "**Modifier et supprimer ton message.** L'auteur peut modifier son " +
             "message ou le supprimer en douceur — il reste là où il était, " +
             "marqué comme supprimé, pour que le fil garde du sens. Un " +
             "administrateur global (et, si c'est concédé, un modérateur) peut " +
             "retirer un message qui viole le code de conduite.\n\n" +
             "**Fichiers.** Tu peux joindre des images ou d'autres fichiers à un " +
             "message. Ils sont stockés sur l'instance et partagés sous " +
             "l'audience du message.\n"),
            ("groups", "Groupes",
             "## Groupes\n\n" +
             "Un groupe rassemble les riverains autour d'une chose commune — un " +
             "bâtiment, un projet, un loisir.\n\n" +
             "**Public et privé.** Un groupe **public** est ouvert à tous ; un " +
             "groupe **privé** est sur invitation, et seul ses membres peuvent le " +
             "voir, y poster ou le prendre dans l'audience d'un message. Les deux " +
             "types t'aident à organiser le quartier.\n\n" +
             "**Pour en créer un.** Choisis **Créer un groupe**, donne-lui un nom " +
             "et une courte description, et choisis s'il est public ou privé. Tu " +
             "peux modifier le nom et la description à tout moment.\n\n" +
             "**Messages de groupe.** Un groupe a son propre fil. Un message que " +
             "tu fais dans un groupe est vu par les membres du groupe (et par " +
             "tous ceux que tu ajoutes à son audience). Tu peux poster dans un " +
             "groupe comme dans le fil communautaire — le sélecteur d'audience " +
             "contient simplement le groupe.\n\n" +
             "**Quitter.** Un membre peut quitter un groupe ; le groupe garde ses " +
             "messages. Un propriétaire de groupe ou un administrateur global " +
             "peut retirer un membre ou fermer un groupe.\n"),
            ("drafts", "Brouillons",
             "## Brouillons\n\n" +
             "Un brouillon est un message que tu as écrit mais pas encore partagé.\n\n" +
             "**Pour en sauvegarder un.** Dans le composeur, coche **Enregistrer " +
             "comme brouillon** et enregistre. Le brouillon est enregistré mais " +
             "visible par personne — pas même les administrateurs — jusqu'à ce " +
             "que tu le publies.\n\n" +
             "**Pour trouver tes brouillons.** Ouvre **Mes brouillons** dans le " +
             "menu. Chaque brouillon que tu as sauvegardé y est, avec une " +
             "badge **Brouillon**.\n\n" +
             "**Pour publier.** Ouvre le brouillon et clique sur **Publier**. Il " +
             "devient visible sous l'audience que tu as définie. En attendant, " +
             "seul tu peux le voir.\n\n" +
             "Les brouillons sont à toi — seul l'auteur et un administrateur " +
             "global peuvent les voir ou les modifier, et un administrateur " +
             "global peut retirer un brouillon qui n'y a pas sa place.\n"),
            ("audience", "Audience",
             "## Audience\n\n" +
             "L'audience est le choix, fait quand tu publies, de qui peut voir le " +
             "message. La plateforme l'applique à chaque lecture.\n\n" +
             "**Les choix.**\n" +
             "- **Tout le monde dans ce quartier** — par défaut. Chaque membre de " +
             "la communauté peut le voir.\n" +
             "- **Une personne précise** — seul ce résident (et toi) peut le voir.\n" +
             "- **Un groupe** — les membres du groupe (et toi) peuvent le voir.\n\n" +
             "Tu peux combiner les choix — par exemple, un groupe *et* une " +
             "personne précise. Ce que tu choisis devient l'audience du message, " +
             "et rien d'autre.\n\n" +
             "**Une réponse n'a pas sa propre audience.** Elle est visible sous " +
             "l'audience unique du message — c'est la règle « la réponse " +
             "hérite », et elle garde le fil lisible pour les gens déjà dans la " +
             "pièce.\n\n" +
             "**Tu ne peux pas changer l'audience d'un message après l'avoir " +
             "publié.** Pour le partager avec plus de gens, démarre un nouveau " +
             "message avec l'audience plus large — l'original reste avec ceux " +
             "que tu as d'abord choisis.\n"),
            ("language", "Langue",
             "## Langue\n\n" +
             "Kumunita peut se montrer dans plus d'une langue, et tu choisis " +
             "laquelle.\n\n" +
             "**Pour régler la tienne.** Ouvre **Paramètres**, choisis **Choisis " +
             "ta langue** et sélectionne la langue que tu veux. Ton choix est " +
             "enregistré sur ton compte.\n\n" +
             "**Ce qui change.** L'interface — boutons, titres et les pages " +
             "intégrées — apparaît dans la langue que tu as choisie. Un message ou " +
             "une description de groupe que quelqu'un a écrite *et* traduite dans " +
             "ta langue montre cette traduction d'abord, l'original est à un " +
             "clic.\n\n" +
             "**Ce qui ne change pas.** Les mots qu'un résident tape sont les " +
             "siens. Un message est toujours lu comme son auteur l'a écrit, à " +
             "moins que quelqu'un n'ait ajouté une traduction — la plateforme ne " +
             "traduit jamais par machine l'écriture d'un résident.\n\n" +
             "**Fuseau horaire et dates.** Tu peux aussi régler le fuseau " +
             "horaire et le format de date que tu vois, dans la même page " +
             "**Paramètres**. Les deux sont enregistrés sur ton compte.\n"),
            ("events", "Événements",
             "## Événements\n\n" +
             "Un événement est quelque chose que le quartier fait à un moment et " +
             "à un endroit — une réunion, une journée de réparation, une " +
             "rencontre.\n\n" +
             "**Pour les voir.** Les événements apparaissent dans le fil et sur " +
             "la liste des événements, avec leur date, leur heure et leur lieu.\n\n" +
             "**Pour y participer.** Ouvre l'événement et choisis **Participer**. " +
             "Ta réponse est enregistrée sur l'événement et ton compte, et tu " +
             "peux la changer à tout moment avant l'événement.\n\n" +
             "**Le rappel.** Tu peux t'inscrire à un rappel la veille de " +
             "l'événement. Il est envoyé une fois, à ton compte, et seulement si " +
             "tu t'es inscrit — il n'y a pas de rappel auquel tu ne t'es pas " +
             "inscrit.\n\n" +
             "**En créer un.** Un administrateur global (et, si c'est concédé, un " +
             "modérateur) peut créer un événement pour le quartier, définir son " +
             "audience, et choisir s'il porte un rappel. Tu peux parler d'un " +
             "événement dans le fil comme de n'importe quoi d'autre.\n"),
            ("translator", "Traducteurs",
             "## Traducteurs\n\n" +
             "Un **traducteur** est un résident à qui est accordé le droit " +
             "d'ajouter et de modifier une version d'une langue d'une chose que " +
             "la plateforme porte déjà — une page, une description de groupe, un " +
             "nom de communauté, ou un message ou une réponse.\n\n" +
             "**Ce qu'un traducteur peut faire.**\n" +
             "- **Ajouter une traduction** d'une page, d'un groupe, d'un nom de " +
             "communauté, d'un message ou d'une réponse — dans une langue que " +
             "l'instance a activée.\n" +
             "- **Modifier une traduction** qu'il a ajoutée, à tout moment.\n\n" +
             "**Ce qu'un traducteur ne peut pas faire.**\n" +
             "- **Changer l'original.** Les mots de l'auteur sont ceux de " +
             "l'auteur ; un traducteur ajoute une version d'une langue à côté, " +
             "jamais par-dessus.\n" +
             "- **Traduire pour la machine.** Une traduction est un acte humain — " +
             "la plateforme ne traduit jamais par machine l'écriture d'un " +
             "résident, et un traducteur n'insère pas une sortie machine comme si " +
             "c'était la sienne.\n" +
             "- **Remplacer un administrateur global.** Un traducteur n'a aucun " +
             "droit sur les propres pages de la plateforme, sur la modération, ou " +
             "sur l'adhésion d'un groupe — ce sont les lanes d'un administrateur " +
             "global (ou, si c'est concédé, d'un modérateur).\n\n" +
             "Le travail d'un traducteur est visible : l'interface montre, à côté " +
             "d'une chose traduite, la personne qui a ajouté cette traduction, et " +
             "l'original est toujours à un clic.\n"),
            ("child-accounts", "Comptes enfants",
             "## Comptes enfants\n\n" +
             "Un compte enfant est un compte que tu crées pour un enfant, et tu en " +
             "gardes les contrôles. Tu l'as créé — c'est cela qui te donne les " +
             "contrôles, mais cela ne te laisse pas lire ce que l'enfant " +
             "écrit.\n\n" +
             "**Ce que tu peux faire.** Tu peux suspendre et réactiver le compte, " +
             "décider de quels groupes et communautés l'enfant fait partie, " +
             "approuver une invitation de groupe que l'enfant a reçue, et assigner " +
             "un autre tuteur pour partager les contrôles. Et tu peux transférer " +
             "le compte quand l'enfant est prêt à le conduire seul.\n\n" +
             "**Ce que tu ne peux pas faire.** Tu ne peux pas lire les " +
             "publications, les réponses ou le profil de l'enfant. Ce sont ceux de " +
             "l'enfant, et ils restent ceux de l'enfant.\n\n" +
             "**Pour en ajouter un.** Ouvre **Compte → Enfants**, entre le nom " +
             "affiché, l'adresse e-mail et un mot de passe de l'enfant, et clique " +
             "sur **Ajouter un compte enfant**. L'enfant vérifie son propre e-mail " +
             "pour se connecter — le flux d'inscription habituel.\n\n" +
             "**Pour suspendre ou réactiver.** Sur la page **Enfants**, trouve " +
             "l'enfant et clique sur **Suspendre** ou **Réactiver**. Un enfant " +
             "suspendu ne peut pas se connecter jusqu'à ce que tu le " +
             "réactives.\n\n" +
             "**Gérer les adhésions.** Sur la page de l'enfant, ajoute ou retire " +
             "des adhésions aux groupes et aux communautés sous **Adhésions aux " +
             "groupes** et **Adhésions aux communautés**.\n\n" +
             "**Approuver une invitation de groupe.** Sur la page de l'enfant, " +
             "trouve l'invitation sous **Invitations de groupe en attente** et " +
             "clique sur **Approuver**.\n\n" +
             "**Assigner un autre tuteur.** Sur la page de l'enfant, ouvre " +
             "**Assigner un tuteur**, entre son e-mail et clique sur **Assigner**. " +
             "Il a alors les mêmes contrôles que toi.\n\n" +
             "**Transférer le compte.** Quand l'enfant est prêt, ouvre sa page et " +
             "clique sur **Dissoudre la tutelle**. Ses adhésions sont " +
             "conservées, et ses propres contrôles reviennent à la prochaine " +
             "lecture.\n"),
            ("being-a-child", "Utiliser un compte enfant",
             "## Utiliser un compte enfant\n\n" +
             "Un compte enfant est celui que ton parent a créé pour toi. Il " +
             "fonctionne comme un compte normal — tu lis le fil, tu écris des " +
             "publications et tu réponds — à quelques près que ton parent gère " +
             "pour toi.\n\n" +
             "**Ce qui te reste.** Tes publications, tes réponses et ton profil te " +
             "restent. Ton parent ne peut pas les lire, même s'il a créé le compte.\n\n" +
             "**Ce que ton parent gère.** Ton parent décide de quels groupes et " +
             "communautés tu fais partie, et peut suspendre ou réactiver ton compte.\n\n" +
             "**Invitations de groupe.** Tu verras une invitation sous " +
             "**Invitations**. Tu peux toujours **Refuser**. Pour l'**Accepter**, " +
             "ton parent doit d'abord l'approuver — demande-lui de l'approuver " +
             "pour toi.\n\n" +
             "**Quand tu es prêt à prendre la main.** Quand ton parent te transfère " +
             "le compte, tes propres contrôles reviennent : tu peux accepter les " +
             "invitations de groupe toi-même et gérer tes propres groupes et " +
             "communautés.\n"),
            ("admins", "Administrateurs",
             "## Administrateurs\n\n" +
             "Un **administrateur** (l'administrateur global de la plateforme) " +
             "est la personne qui fait tourner l'instance pour tout le quartier. " +
             "Tu ne la rencontreras presque jamais au quotidien — la plupart des " +
             "choses se font sans elle. Voici ce que couvre ce rôle.\n\n" +
             "**Ce que fait un administrateur.**\n" +
             "- **Les comptes.** Valider un nouveau compte, bloquer ou débloquer " +
             "un habitant, et décider qui détient quel rôle élevé.\n" +
             "- **Les communautés.** Ajouter et modifier les communautés, et " +
             "décider desquelles chaque habitant fait partie.\n" +
             "- **Les pages de la plateforme.** Modifier les conditions " +
             "d'utilisation, les guides d'aide, la note de confidentialité et " +
             "la charte — et réinitialiser une page à son texte livré quand tu " +
             "veux la dernière formulation.\n" +
             "- **L'inscription.** Choisir si de nouveaux habitants peuvent " +
             "s'inscrire tout seuls, ou seulement si tu les invites.\n" +
             "- **Les réglages de la plateforme.** Fixer la langue par défaut, " +
             "le fuseau horaire et le format de date de l'instance. Chaque " +
             "habitant peut encore tous les outrepasser sur son propre " +
             "compte.\n" +
             "- **La modération.** Examiner chaque signalement et en résoudre " +
             "un — la qualité d'agir sur un signalement reste celle d'un " +
             "administrateur, même quand un modérateur peut le voir.\n\n" +
             "**Ce qui freine le rôle.** Chaque action d'administrateur est " +
             "inscrite dans un journal d'audit que tu peux consulter, et la " +
             "qualité est volontairement étroite : un administrateur fait " +
             "tourner l'instance, mais il ne lit ni tes messages ni tes " +
             "réponses ni ton profil — l'audience continue de décider ce qui " +
             "est visible, et un signalement n'est jamais une porte dérobée " +
             "vers le contenu d'une personne.\n"),
            ("moderators", "Modérateurs",
             "## Modérateurs\n\n" +
             "Un **modérateur** est une personne à qui tu as donné la qualité de " +
             "veiller sur une partie du quartier — une communauté ou un groupe — " +
             "et d'en faire un lieu sûr.\n\n" +
             "**Ce qu'un modérateur peut faire.**\n" +
             "- **Voir les signalements** qui concernent la partie du quartier " +
             "dont il s'occupe. La file de signalement n'affiche que ceux-là.\n" +
             "- **Lire un signalement** en détail, y compris qui l'a déposé et " +
             "ce qu'il demande.\n\n" +
             "**Ce qu'un modérateur ne peut pas faire.**\n" +
             "- **Agir sur un signalement.** Affecter, débloquer et résoudre " +
             "sont la qualité d'un administrateur — un modérateur peut voir un " +
             "signalement, mais la plateforme ne lui permet pas d'agir dessus.\n" +
             "- **Voir au-delà de sa partie.** Un modérateur ne voit que les " +
             "signalements qui concernent la partie qu'on lui a donnée. Les " +
             "signalements d'ailleurs ne figurent pas dans sa file.\n" +
             "- **Lire le contenu de quiconque.** Voir un signalement n'est pas " +
             "une porte dérobée vers les messages, les réponses ou le profil " +
             "d'une personne — l'audience continue de décider ce qui est " +
             "visible, et un signalement ne change pas cela.\n\n" +
             "Un modérateur est un voisin qui veille sur sa part du quartier — " +
             "ni administrateur, ni lecteur du contenu des autres.\n"),
            ("notifications", "Notifications",
             "## Notifications\n\n" +
             "Kumunita te prévient quand quelque chose arrive sur les choses " +
             "qui te concernent — une réponse à ton message, une réponse à " +
             "une invitation pour ton événement, un nouveau message dans l'un " +
             "de tes groupes, une tâche qui arrive chez toi.\n\n" +
             "**Où les trouver.** Ouvre la **cloche** en haut de l'écran. " +
             "Elle indique combien de notifications sont neuves et affiche " +
             "une brève liste des plus récentes. Ouvre la **boîte de " +
             "réception** pour la liste complète, de la plus récente à la " +
             "plus ancienne. La boîte de réception enregistre toujours " +
             "chaque notification — c'est l'histoire fiable.\n\n" +
             "**E-mail.** Pour les types qui t'intéressent, tu reçois aussi " +
             "un e-mail. L'e-mail est dans la langue que tu as choisie pour " +
             "les e-mails dans ton profil. Désactiver un type dans tes " +
             "préférences arrête l'e-mail de ce type — jamais la boîte de " +
             "réception.\n\n" +
             "**Tes choix.** Deux endroits :\n" +
             "- **Préférences de notification** — l'interrupteur de chaque " +
             "type d'e-mail.\n" +
             "- **Abonnements** — les réglages plus fins : suis une " +
             "communauté ou un groupe pour recevoir ses messages, ou la " +
             "plateforme pour les annonces.\n\n" +
             "Tu peux marquer toute la boîte de réception comme lue d'un " +
             "seul clic. Ce que tu suis et ce qui t'arrive par e-mail " +
             "reste ton choix.\n"),
        ];
    }

    /// <summary>
    /// UG (ADR 0057, amended 2026-09-21) — the curated <c>da</c> baseline for
    /// the resident guides: the single source of the seeded Danish guide
    /// bodies (the same shape as <see cref="GuidePages"/>() — a public static
    /// array of <c>(Slug, Title, Body)</c> tuples). A full translation of the
    /// <see cref="GuidePages"/>() bodies — the same Markdown structure
    /// (heading, the step blocks, the cross-links), idiomatic Danish UI copy
    /// (the familiar <c>dig</c> register held, sentence case, no word-for-word
    /// calques). These ship as
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows on a pristine DB
    /// (ADR 0042 D1 — seeded once, then community-owned; the in-app editor
    /// supersedes them and no later deploy reverts a human edit).
    /// <para>
    /// <b>Cross-links are absolute</b> — see <see cref="DeGuidePages"/>() for
    /// the rationale. The <c>en</c> floor is untouched.
    /// </para>
    /// </summary>
    public static (string Slug, string Title, string Body)[] DaGuidePages()
    {
        return
        [
            ("getting-started", "Første skridt",
             "## Første skridt\n\n" +
             "Det er det sted, du skal starte på. Kumunita er et privat hjem til " +
             "præcis ét nabolag — denne side og siderne under den fører dig " +
             "trin for trin gennem alt.\n\n" +
             "**Hvad findes hvor.** **Feeden** er stedet for indlæggene. " +
             "**Grupper** samler beboere om en bygning, et projekt eller en " +
             "fælles interesse. **Kontaktlisten** viser beboerne på platformen og " +
             "de oplysninger, hver af dem har valgt at dele. **Sider**-træet " +
             "(linket **Sider**) er stedet for disse guides, vilkårene, " +
             "privatlivspolitikken og adfærdskodeksen.\n\n" +
             "**Dine første skridt.**\n" +
             "- **Skriv et indlæg.** Vælg **Nyt indlæg**, skriv det, vælg, hvem " +
             "der kan se det, og klik på **Opret**. Se [indlæg](/pages/system/help/posts) " +
             "for den fulde vejledning.\n" +
             "- **Bund dig til en gruppe.** Grupper er stedet for en fælles " +
             "interesse. Se [grupper](/pages/system/help/groups).\n" +
             "- **Indstil sprog og tidzone.** De gemmes på din konto. Se " +
             "[sprog](/pages/system/help/language).\n\n" +
             "Alt andet har en guide under denne side — følg linket til det, du " +
             "vil gøre.\n"),
            ("posts", "Indlæg",
             "## Indlæg\n\n" +
             "Et indlæg er en note, du deler med de mennesker, du vælger.\n\n" +
             "**Sådan skriver du ét.** Vælg **Nyt indlæg** øverst i feeden. Skriv " +
             "det, du vil sige. Under **Hvem kan se det** vælger du modtagerkredsen. " +
             "Klik på **Opret**.\n\n" +
             "**Hvem kan se det.** Som udgangspunkt ses det af alle i nabolaget. " +
             "Du kan indsnævre det — til en bestemt person eller til en gruppe — " +
             "med modtagerkreds-vælgeren. Se [modtagerkreds](/pages/system/help/audience), " +
             "hvad hvert valg betyder.\n\n" +
             "**Svar.** Enhver, der kan se dit indlæg, kan svare på det. Et svar " +
             "ses under dit indlægs ene modtagerkredsvalg — det har ikke sin egen " +
             "modtagerkreds. Du kan **redigere** eller **slette** dine egne svar; " +
             "platformen husker, hvornår et svar er blevet redigeret.\n\n" +
             "**Redigering og sletning af dit indlæg.** Forfatteren kan redigere " +
             "sit eget indlæg eller blødt slette det — det forbliver, hvor det var, " +
             "mærket som slettet, så tråden stadig giver mening. En global admin " +
             "(og, hvor tildelt, en moderator) kan fjerne et indlæg, der bryder " +
             "adfærdskodeksen.\n\n" +
             "**Filer.** Du kan vedhæfte billeder eller andre filer til et indlæg. " +
             "De gemmes på instansen og deles under indlægget modtagerkreds.\n"),
            ("groups", "Grupper",
             "## Grupper\n\n" +
             "En gruppe samler beboere om en fælles ting — en bygning, et projekt, " +
             "en hobby.\n\n" +
             "**Offentlige og private.** En **offentlig** gruppe kan enhver " +
             "blive medlem af; en **privat** gruppe er på invitation, og kun dens " +
             "medlemmer kan se den, poste til den eller tage den med i et " +
             "indlægs modtagerkreds. Begge typer hjælper dig med at organisere " +
             "nabolaget.\n\n" +
             "**Sådan opretter du én.** Vælg **Opret gruppe**, giv den et navn og " +
             "en kort beskrivelse, og vælg, om den er offentlig eller privat. Du " +
             "kan redigere navnet og beskrivelsen til enhver tid.\n\n" +
             "**Gruppeindlæg.** En gruppe har sin egen feed. Et indlæg, du " +
             "opretter i en gruppe, ses af gruppens medlemmer (og alle, du " +
             "tilføjer til dens modtagerkreds). Du kan poste til en gruppe, ligesom " +
             "du poster til fællesfeeden — modtagerkreds-vælgeren inkluderer bare " +
             "gruppen.\n\n" +
             "**Forladelse.** Et medlem kan forlade en gruppe; gruppen beholder " +
             "sit indlæg. En gruppeejere eller en global admin kan fjerne et " +
             "medlem eller lukke en gruppe.\n"),
            ("drafts", "Udkast",
             "## Udkast\n\n" +
             "Et udkast er et indlæg, du har skrevet, men endnu ikke delt.\n\n" +
             "**Sådan gemmer du ét.** I redaktøren markerer du **Gem som udkast** " +
             "og gemmer. Udkastet er gemt, men synligt for ingen — ikke engang " +
             "admin'erne — før du publicerer det.\n\n" +
             "**Sådan finder du dine udkast.** Åbn **Mine udkast** i menuen. " +
             "Hvert udkast, du har gemt, er dér, med et **Udkast**-mærke.\n\n" +
             "**Sådan publicerer du.** Åbn udkastet og klik på **Publicer**. Det " +
             "bliver synligt under den modtagerkreds, du har valgt. Indtil da kan " +
             "kun du se det.\n\n" +
             "Udkast er dine — kun forfatteren og en global admin kan se eller " +
             "ændre dem, og en global admin kan fjerne et udkast, der hører dér " +
             "ikke til.\n"),
            ("audience", "Modtagerkreds",
             "## Modtagerkreds\n\n" +
             "Modtagerkreds er valget, du træffer, når du poster, om hvem der kan " +
             "se indlægget. Platformen håndhæver det ved hvert læs.\n\n" +
             "**Valgene.**\n" +
             "- **Alle i dette nabolag** — udgangspunktet. ethvert medlem af " +
             "fællesskabet kan se det.\n" +
             "- **En bestemt person** — kun denne beboer (og du) kan se det.\n" +
             "- **En gruppe** — gruppens medlemmer (og du) kan se det.\n\n" +
             "Du kan kombinere valg — for eksempel en gruppe *og* en bestemt " +
             "person. Hvad end du vælger, bliver det indlægget modtagerkreds, og " +
             "intet andet.\n\n" +
             "**Et svar har ikke sin egen modtagerkreds.** Det ses under " +
             "indlægget ene modtagerkredsvalg — det er „svaret arver\u201C-reglen, og " +
             "den holder tråden læsbar for de mennesker, der allerede er i " +
             "lokalet.\n\n" +
             "**Du kan ikke ændre et indlægs modtagerkreds, efter du har " +
             "publiceret det.** For at dele det med flere mennesker, start et nyt " +
             "indlæg med den bredere modtagerkreds — det originale forbliver ved " +
             "dem, du først valgte.\n"),
            ("language", "Sprog",
             "## Sprog\n\n" +
             "Kumunita kan vise sig på mere end ét sprog, og du vælger, hvilket.\n\n" +
             "**Sådan indstiller du dit.** Åbn **Indstillinger**, vælg **Vælg dit " +
             "sprog** og vælg det sprog, du vil. Dit valg gemmes på din konto.\n\n" +
             "**Hvad der ændres.** Grænsefladen — knapper, overskrifter og de " +
             "indbyggede sider — vises på det sprog, du har valgt. Et indlæg eller " +
             "en gruppebeskrivelse, som nogen har skrevet *og* oversat til dit " +
             "sprog, viser denne oversættelse først, originalen er ét klik væk.\n\n" +
             "**Hvad der ikke ændres.** De ord, en beboer skriver, er dennes. Et " +
             "indlæg læses altid som forfatteren har skrevet det, medmindre nogen " +
             "har tilføjet en oversættelse — platformen maskinoversætter aldrig " +
             "en beboers skrivning.\n\n" +
             "**Tidzone og datoer.** Du kan også indstille den tidzone og det " +
             "datoformat, du ser, på den samme **Indstillinger**-side. Begge " +
             "gemmes på din konto.\n"),
            ("events", "Begivenheder",
             "## Begivenheder\n\n" +
             "En begivenhed er noget, nabolaget gør på et tidspunkt og et sted — " +
             "et møde, en reparationsdag, et samvær.\n\n" +
             "**Sådan ser du dem.** Begivenheder vises i feeden og på " +
             "begivenhedslisten med deres dato, deres tid og deres sted.\n\n" +
             "**Sådan deltager du.** Åbn begivenheden og vælg **Deltag**. Dit " +
             "svar registreres på begivenheden og din konto, og du kan ændre det " +
             "til enhver tid før begivenheden.\n\n" +
             "**Påmindelsen.** Du kan tilmelde dig en påmindelse dagen før " +
             "begivenheden. Den sendes én gang, til din konto, og kun hvis du " +
             "har bedt om den — der er ingen påmindelse, du ikke har tilmeldt " +
             "dig.\n\n" +
             "**Sådan opretter du én.** En global admin (og, hvor tildelt, en " +
             "moderator) kan oprette en begivenhed for nabolaget, sætte dens " +
             "modtagerkreds og vælge, om den bærer en påmindelse. Du kan poste " +
             "om en begivenhed i feeden, ligesom du poster om alt andet.\n"),
            ("translator", "Oversættere",
             "## Oversættere\n\n" +
             "En **oversætter** er en beboer, der er givet ret til at tilføje og " +
             "redigere en sproglig version af noget, platformen allerede bærer — " +
             "en side, en gruppebeskrivelse, et fællesskabsnavn eller et indlæg " +
             "eller et svar.\n\n" +
             "**Hvad en oversætter må gøre.**\n" +
             "- **Tilføje en oversættelse** af en side, en gruppe, et " +
             "fællesskabsnavn, et indlæg eller et svar — på et sprog, instansen " +
             "har aktiveret.\n" +
             "- **Redigere en oversættelse**, de har tilføjet, til enhver tid.\n\n" +
             "**Hvad en oversætter ikke må gøre.**\n" +
             "- **Ændre originalen.** Forfatterens ord er forfatterens; en " +
             "oversætter tilføjer en sproglig version ved siden af, aldrig " +
             "ovenpå.\n" +
             "- **Oversætte for maskinen.** En oversættelse er et menneskeligt " +
             "arbejde — platformen maskinoversætter aldrig en beboers skrivning, " +
             "og en oversætter indsætter ikke et maskineresultat, som var det " +
             "deres eget.\n" +
             "- **Stå i stedet for en global admin.** En oversætter har ingen " +
             "ret på platformens egne sider, på moderation eller på en gruppe " +
             "medlemskab — det er en global admin (eller, hvor tildelt, en " +
             "moderator) lane.\n\n" +
             "En oversætters arbejde er synligt: grænsefladen viser, ved siden " +
             "af en oversat ting, den person, der har tilføjet denne oversættelse, " +
             "og originalen er altid ét klik væk.\n"),
            ("child-accounts", "Barnkonti",
             "## Barnkonti\n\n" +
             "En barnkonto er en konto, du opretter til et barn, og kontrollerne " +
             "over den holder du. Du har oprettet kontoen — det er det, der giver " +
             "dig kontrollerne, men det lader dig ikke læse, hvad barnet " +
             "skriver.\n\n" +
             "**Hvad du kan.** Du kan suspendere og genoprette kontoen, bestemme, " +
             "hvilke grupper og fællesskaber barnet tilhører, godkende en " +
             "gruppeinvitation, barnet har modtaget, og tildele en anden " +
             "værgemand, så I deler kontrollerne. Og du kan give kontoen videre, " +
             "når barnet er klar til at drive den selv.\n\n" +
             "**Hvad du ikke kan.** Du kan ikke læse barnets indlæg, svar eller " +
             "profil. De er barnets, og de forbliver barnets.\n\n" +
             "**For at tilføje en.** Åbn **Konto → Børn**, udfyld barnets " +
             "vistnavn, e-mailadresse og en adgangskode, og klik på **Tilføj en " +
             "barnkonto**. Barnet bekræfter sin egen e-mail for at logge ind — " +
             "den sædvanlige tilmeldingsproces.\n\n" +
             "**For at suspendere eller genoprette.** På siden **Børn** finder du " +
             "barnet og klikker på **Suspendér** eller **Genopret**. Et suspendet " +
             "barn kan ikke logge ind, indtil du genopretter det.\n\n" +
             "**Styr på medlemskaber.** På barnets side tilføjer eller fjerner du " +
             "gruppemedlemskaber og fællesskabsmedlemskaber under " +
             "**Gruppemedlemskaber** og **Fællesskabsmedlemskaber**.\n\n" +
             "**Godkend en gruppeinvitation.** På barnets side finder du " +
             "invitationen under **Afventende gruppeinvitationer** og klikker " +
             "på **Godkend**.\n\n" +
             "**Tildel en anden værgemand.** På barnets side åbner du **Tildel " +
             "en værgemand**, indtaster e-mailen og klikker på **Tildel**. Så har " +
             "personen de samme kontroller som dig.\n\n" +
             "**Giv kontoen videre.** Når barnet er klar, åbner du dets side og " +
             "klikker på **Afløs værgemodet**. Medlemskaberne bevares, og " +
             "barnets egne kontroller kommer tilbage ved næste læsning.\n"),
            ("being-a-child", "At bruge en barnkonto",
             "## At bruge en barnkonto\n\n" +
             "En barnkonto er en, dine forældre har oprettet til dig. Den virker " +
             "som en normal konto — du læser feedet, skriver indlæg og svarer — " +
             "bortset fra, at dine forældre klarer et par ting for dig.\n\n" +
             "**Hvad der er dit.** Dine indlæg, svar og profil er dine. Dine " +
             "forældre kan ikke læse dem, selvom de har oprettet kontoen.\n\n" +
             "**Hvad dine forældre klarer.** Dine forældre beslutter, hvilke " +
             "grupper og fællesskaber, du tilhører, og de kan suspendere eller " +
             "genoprette din konto.\n\n" +
             "**Gruppeinvitationer.** Du finder invitationen under " +
             "**Indladelser**. Du kan altid klikke på **Afvis**. For at " +
             "klikke på **Acceptér**, skal dine forældre først godkende den — " +
             "bed dem godkende den for dig.\n\n" +
             "**Når du er klar til at overtage.** Når dine forældre giver kontoen " +
             "videre til dig, kommer dine egne kontroller tilbage: Du kan selv " +
             "acceptere gruppeinvitationer og styre dine egne grupper og " +
             "fællesskaber.\n"),
            ("admins", "Administrerende",
             "## Administrerende\n\n" +
             "En **administrerende** (platformens globale admin) er den person, " +
             "der holder instansen i gang for hele nabolaget. I dagligdagen " +
             "møder du dem sjældent — de fleste ting kan du gøre helt uden dem. " +
             "Her er, hvad rollen dækker.\n\n" +
             "**Det en administrerende gør.**\n" +
             "- **Konti.** Bekræfte en ny konto, blokere eller blokfratage en " +
             "person, og bestemme, hvem der bærer hvilken hævet rolle.\n" +
             "- **Samfund.** Oprette og redigere samfund og beslutte, hvilke af " +
             "dem hver person tilhører.\n" +
             "- **Platformsiderne.** Redigere vilkårne, hjælpeguiderne, " +
             "personvernshinvisningen og opførselsreglerne — og nulstille en " +
             "side til dens leverede tekst, når du vil have den nyeste ordlyd.\n" +
             "- **Tilmelding.** Bestemme, om nye personer må tilmelde sig selv, " +
             "eller kun når du inviterer dem.\n" +
             "- **Platformindstillingerne.** Sætte standardsproget, tidszonen " +
             "og datoformatet for instansen. Hver person kan stadig overskrive " +
             "dem på den egne konto.\n" +
             "- **Moderation.** Se alle anmeldelser og afslutte én — den " +
             "fuldmagt til at handle på en anmeldelse tilhører den " +
             "administrerende, selvom en moderator kan se den.\n\n" +
             "**Det, der holder rollen i skak.** Hver admintiltag bliver " +
             "skrevet i et auditlog, du kan gennemse, og rollen er med vilje " +
             "snæver: En administrerende holder instansen i gang, men læser " +
             "hverken dine indlæg eller dine svar eller din profil — " +
             "målgruppen bestemmer stadig, hvad der er synligt, og en " +
             "anmeldelse er aldrig en bagdør til en persons indhold.\n"),
            ("moderators", "Moderatorer",
             "## Moderatorer\n\n" +
             "En **moderator** er en person, du har givet fuldmagt til at passe " +
             "på en del af nabolaget — et samfund eller en gruppe — og holde det " +
             "til en sikker plads.\n\n" +
             "**Det en moderator må.**\n" +
             "- **Se de anmeldelser**, der vedrører den del af nabolaget, " +
             "personen modererer. Anmeldelseslisten viser kun dem.\n" +
             "- **Læse en anmeldelse** i detalje, inklusive hvem der har " +
             "afgivet den, og hvad det drejer sig om.\n\n" +
             "**Det en moderator ikke må.**\n" +
             "- **Handle på en anmeldelse.** At tildele, låse op og afslutte " +
             "er den administrerendes fuldmagt — en moderator kan se en " +
             "anmeldelse, men platformen lader personen ikke gribe ind over " +
             "den.\n" +
             "- **Se ud over sin del.** En moderator ser kun de anmeldelser, " +
             "der vedrører den del, personen er givet. Anmeldelser fra andre " +
             "steder står ikke i personens liste.\n" +
             "- **Læse andres indhold.** At se en anmeldelse er ikke en bagdør " +
             "til en persons indlæg, svar eller profil — målgruppen bestemmer " +
             "stadig, hvad der er synligt, og en anmeldelse ændrer ikke på " +
             "det.\n\n" +
             "En moderator er en nabo, der holder sin del af nabolaget i orden " +
             "— hverken en administrerende eller en læser af andres indhold.\n"),
            ("notifications", "Notifikationer",
             "## Notifikationer\n\n" +
             "Kumunita giver dig besked, når der sker noget med de ting, " +
             "der vedrører dig — et svar på dit indlæg, et svar på " +
             "indbydelsen til din begivenhed, et nyt indlæg i en af dine " +
             "grupper, en opgave, der lander hos dig.\n\n" +
             "**Hvor du finder dem.** Åbn **klokken** i bjælken øverst. " +
             "Den viser, hvor mange notifikationer der er nye, og en kort " +
             "liste over de nyeste. Åbn **indbakken** for den fulde liste, " +
             "nyeste først. Indbakken registrerer altid hver notifikation — " +
             "det er den pålidelige optegnelse.\n\n" +
             "**E-mail.** For de typer, der interesserer dig, får du også en " +
             "e-mail. E-mailen er på det sprog, du har valgt til e-mail i " +
             "din profil. Slår du en type fra i dine indstillinger, " +
             "undlader den e-mailen for den type — aldrig indbakken.\n\n" +
             "**Dine valg.** To steder:\n" +
             "- **Notifikationsindstillinger** — til/fra-knappen for hver " +
             "e-mailtype.\n" +
             "- **Abonnementer** — de finere skifter: følg et fællesskab " +
             "eller en gruppe for at modtage deres indlæg, eller platformen " +
             "for meddelelser.\n\n" +
             "Du kan markere hele indbakken som læst med ét klik. Hvad du " +
             "følger, og hvad du får på e-mail, bestemmer du selv.\n"),
        ];
    }

    /// <summary>
    /// The canonical <c>en</c> default-page bodies (terms + help + privacy +
    /// conduct — the four <c>Page</c>-backed surfaces of the ADR 0043 D1
    /// five-surface set). The single source of the seed text: the
    /// <see cref="Kumunita.Core.Pages.Page"/> docs (PG U05, ADR 0039 §3.9; the
    /// two added in SP U01 per ADR 0043 D2/D5) carry <em>exactly</em> this
    /// text, so a fresh instance is byte-identical for <c>/terms</c> /
    /// <c>/help</c> / <c>/privacy</c> / <c>/conduct</c> read from the tree.
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
             "## Guides\n\n" +
             "A step-by-step walkthrough of the platform, in plain language:\n\n" +
             "- [Getting started](/pages/system/help/getting-started) — your first ten minutes\n" +
             "- [Posts](/pages/system/help/posts) — writing, replying, editing, deleting, attaching files\n" +
             "- [Groups](/pages/system/help/groups) — public and private groups, group posts\n" +
             "- [Drafts](/pages/system/help/drafts) — save without publishing\n" +
             "- [Audience](/pages/system/help/audience) — choosing who can see a post\n" +
             "- [Language](/pages/system/help/language) — your language, time zone, and date format\n" +
             "- [Events](/pages/system/help/events) — the feed, RSVP, reminders\n" +
             "- [Translators](/pages/system/help/translator) — what a Translator may and may not do\n" +
             "- [Child accounts](/pages/system/help/child-accounts) — what a guardian may and may not do\n" +
             "- [Being a child](/pages/system/help/being-a-child) — what a child's account is like, and what stays yours\n" +
             "- [Admins](/pages/system/help/admins) — what a global admin does, and what keeps it in check\n" +
             "- [Moderators](/pages/system/help/moderators) — what a moderator may and may not do\n\n" +
             "Need help with the instance itself? That's an operator concern — see the " +
             "self-hosted documentation linked in the footer.\n"),
            ("privacy", "Privacy",
             "## Privacy\n\n" +
             "Kumunita is a self-hosted platform for one neighborhood. The operator " +
             "runs the instance and is the data controller for everything on it.\n\n" +
             "- **What the platform stores:** the neighborhood's accounts, posts, " +
             "groups, pages, and media — the database plus the media volume. What we " +
             "don't store, we can't leak.\n" +
             "- **Audience enforcement is the privacy mechanism:** content is " +
             "deny-by-default; the author chooses each post's audience, and the " +
             "platform enforces it on every request.\n" +
             "- **Audit-by-default:** access to audience-restricted content, and " +
             "moderation and admin actions, are always logged.\n" +
             "- **Backups, migration, and retirement are the operator's job:** the " +
             "database and the uploaded files are yours to back up, migrate, and " +
             "retire.\n" +
             "- **The one browser cookie:** the locale preference — that is the only " +
             "cookie the platform sets. No third-party cookies exist or are planned.\n\n" +
             "This page deliberately withholds operator-specific details — retention " +
             "periods, DPO contact, sub-processors. Those are the operator's to add " +
             "here, in-app, after first boot; that is the design, not a gap.\n"),
            ("conduct", "Code of conduct",
             "## Code of conduct\n\n" +
             "This is a bounded neighborhood. Treat your neighbors the way you'd want " +
             "to be treated on your street.\n\n" +
             "- No harassment.\n" +
             "- No doxxing (revealing private details).\n" +
             "- No spam.\n\n" +
             "Enforcement is the operator's and the moderators' judgment — the " +
             "moderation lane is report-gated and audited. This page states the " +
             "expectation, not the procedure.\n"),
        ];
    }

    /// <summary>
    /// LS U04 (ADR 0042 D2); SP U02 (ADR 0043 D1/D3) — the curated <c>de</c>
    /// baseline for the seeded default pages (terms + help + privacy +
    /// conduct — the full four-page set of the ADR 0043 D1 five-surface
    /// set). A full translation of the
    /// <see cref="EnDefaultPages"/>() bodies — the same Markdown structure
    /// (heading, intro, the bullets, the closing line), idiomatic
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
             "## Anleitungen\n\n" +
             "Eine Schritt-für-Schritt-Anleitung der Plattform, in verständlicher " +
             "Sprache:\n\n" +
             "- [Erste Schritte](/pages/system/help/getting-started) — deine ersten zehn Minuten\n" +
             "- [Beiträge](/pages/system/help/posts) — Schreiben, Antworten, Bearbeiten, Löschen, Dateien anhängen\n" +
             "- [Gruppen](/pages/system/help/groups) — Öffentliche und private Gruppen, Gruppenbeiträge\n" +
             "- [Entwürfe](/pages/system/help/drafts) — Speichern ohne zu veröffentlichen\n" +
             "- [Zielgruppe](/pages/system/help/audience) — Wer darf einen Beitrag sehen\n" +
             "- [Sprache](/pages/system/help/language) — Deine Sprache, Zeitzone und das Datumsformat\n" +
             "- [Termine](/pages/system/help/events) — Der Feed, RSVP, Erinnerungen\n" +
             "- [Übersetzer](/pages/system/help/translator) — Was ein Übersetzer darf und nicht darf\n" +
             "- [Kinderkonten](/pages/system/help/child-accounts) — Was ein Vormund darf und nicht darf\n" +
             "- [Ein Kinderkonto nutzen](/pages/system/help/being-a-child) — Wie sich ein Kinderkonto anfühlt und was dir gehört\n" +
             "- [Administratoren](/pages/system/help/admins) — Was ein globaler Admin macht, und was die Rolle bremst\n" +
             "- [Moderatoren](/pages/system/help/moderators) — Was ein Moderator darf und nicht darf\n\n" +
             "Probleme mit der Instanz selbst? Das ist eine Frage für den Betreiber — " +
             "siehe die Dokumentation zum Self-Hosting, verlinkt in der Fußzeile.\n"),
            ("privacy", "Datenschutz",
             "## Datenschutz\n\n" +
             "Kumunita ist eine selbst gehostete Plattform für genau ein Viertel. Der " +
             "Betreiber führt die Instanz und ist der Verantwortliche für alles, was " +
             "darauf liegt.\n\n" +
             "- **Was die Plattform speichert:** die Konten, Beiträge, Gruppen, Seiten " +
             "und Medien des Viertels — die Datenbank plus die hochgeladenen Dateien. " +
             "Was nicht gespeichert wird, kann nicht verloren gehen.\n" +
             "- **Die Zielgruppenprüfung ist der Schutzmechanismus:** Inhalte sind " +
             "standardmäßig nicht öffentlich; der Autor wählt für jeden Beitrag die " +
             "Zielgruppe, und die Plattform erzwingt sie bei jeder Anfrage.\n" +
             "- **Standardmäßig protokolliert:** Zugriffe auf Inhalte mit eingeschränkter " +
             "Zielgruppe sowie Moderations- und Admin-Aktionen werden immer protokolliert.\n" +
             "- **Sicherung, Migration und Stilllegung sind Sache des Betreibers:** " +
             "Datenbank und hochgeladene Dateien kannst du sichern, migrieren und " +
             "stilllegen.\n" +
             "- **Der eine Browser-Cookie:** die bevorzugte Sprache — das ist der einzige " +
             "Cookie, den die Plattform setzt. Cookies von Dritten gibt es nicht, und " +
             "es sind auch keine geplant.\n\n" +
             "Diese Seite verzichtet bewusst auf betreiberspezifische Angaben — " +
             "Aufbewahrungsfristen, Kontakt zur Datenschutzbeauftragten, " +
             "Sub-Verarbeiter. Das fügst du hier in der App nach dem ersten Start " +
             "hinzu; das ist so gedacht, kein Mangel.\n"),
            ("conduct", "Verhaltenskodex",
             "## Verhaltenskodex\n\n" +
             "Das ist ein abgegrenztes Viertel. Behandle deine Nachbarn so, wie du auf " +
             "deiner Straße behandelt werden möchtest.\n\n" +
             "- Keine Belästigung.\n" +
             "- Kein Doxxing (private Angaben preisgeben).\n" +
             "- Kein Spam.\n\n" +
             "Ob und wie durchgegriffen wird, liegt im Ermessen von Betreiber und " +
             "Moderatoren — die Moderation läuft über Berichte und wird protokolliert. " +
             "Diese Seite benennt die Erwartung, nicht das Verfahren.\n"),
        ];
    }

    /// <summary>
    /// LS U04 (ADR 0042 D2); SP U02 (ADR 0043 D1/D3) — the curated <c>fr</c>
    /// baseline for the seeded default pages (terms + help + privacy +
    /// conduct — the full four-page set of the ADR 0043 D1 five-surface
    /// set). A full translation of the
    /// <see cref="EnDefaultPages"/>() bodies — the same Markdown structure
    /// (heading, intro, the bullets, the closing line), idiomatic
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
             "## Guides\n\n" +
             "Un guide pas à pas de la plateforme, en langage simple :\n\n" +
             "- [Premiers pas](/pages/system/help/getting-started) — tes dix premières minutes\n" +
             "- [Messages](/pages/system/help/posts) — Écrire, répondre, modifier, supprimer, joindre des fichiers\n" +
             "- [Groupes](/pages/system/help/groups) — Groupes publics et privés, messages de groupe\n" +
             "- [Brouillons](/pages/system/help/drafts) — Sauvegarder sans publier\n" +
             "- [Audience](/pages/system/help/audience) — Qui peut voir un message\n" +
             "- [Langue](/pages/system/help/language) — Ta langue, ton fuseau horaire et ton format de date\n" +
             "- [Événements](/pages/system/help/events) — Le fil, la participation, les rappels\n" +
             "- [Traducteurs](/pages/system/help/translator) — Ce qu'un traducteur peut et ne peut pas faire\n" +
             "- [Comptes enfants](/pages/system/help/child-accounts) — Ce qu'un tuteur peut et ne peut pas faire\n" +
             "- [Utiliser un compte enfant](/pages/system/help/being-a-child) — À quoi ressemble un compte enfant et ce qui te reste\n" +
             "- [Administrateurs](/pages/system/help/admins) — Ce qu'un administrateur global fait, et ce qui freine le rôle\n" +
             "- [Modérateurs](/pages/system/help/moderators) — Ce qu'un modérateur peut et ne peut pas faire\n\n" +
             "Un souci avec l'instance elle-même ? C'est une affaire de porteur — " +
             "consulte la documentation d'auto-hébergement, liée dans le pied de page.\n"),
            ("privacy", "Vie privée",
             "## Vie privée\n\n" +
             "Kumunita est une plateforme auto-hébergée pour un seul quartier. Le " +
             "porteur exploite l'instance et est le responsable des données de " +
             "tout ce qu'elle contient.\n\n" +
             "- **Ce que la plateforme stocke :** les comptes, messages, groupes, " +
             "pages et médias du quartier — la base de données, plus le volume de " +
             "médias. Ce qu'elle ne stocke pas, elle ne peut pas le fuir.\n" +
             "- **L'application des audiences est le mécanisme de protection :** " +
             "le contenu est privé par défaut ; l'auteur choisit l'audience de " +
             "chaque message, et la plateforme l'applique à chaque requête.\n" +
             "- **Le journal par défaut :** les accès aux contenus réservés et les " +
             "actions de modération et d'administration sont toujours journalisés.\n" +
             "- **La sauvegarde, la migration et la fermeture sont l'affaire du " +
             "porteur :** la base de données et les fichiers que tu as téléversés " +
             "sont à toi de les sauvegarder, migrer, ou mettre à la retraite.\n" +
             "- **Le seul cookie du navigateur :** la langue préférée — c'est le " +
             "seul cookie que la plateforme pose. Il n'existe aucun cookie " +
             "tiers, et il n'en est prévu aucun.\n\n" +
             "Cette page omet volontairement les détails propres au porteur — " +
             "délais de conservation, contact DPO, sous-traitants. C'est à toi de " +
             "les ajouter ici, dans l'application, après le premier démarrage ; " +
             "c'est le design, pas un manque.\n"),
            ("conduct", "Code de conduite",
             "## Code de conduite\n\n" +
             "C'est un quartier borné. Traite tes voisins comme tu voudrais être " +
             "traité·e sur ta rue.\n\n" +
             "- Pas de harcèlement.\n" +
             "- Pas de doxxing (révéler des informations privées).\n" +
             "- Pas de spam.\n\n" +
             "L'application relève du jugement du porteur et des modérateurs — " +
             "la modération passe par les signalements et est journalisée. Cette " +
             "page énonce l'attente, pas la procédure.\n"),
        ];
    }

    /// <summary>
    /// The Danish (<c>da</c>) baseline — the curated <c>da</c> translation of
    /// the seeded default pages (terms + help + privacy + conduct). A full
    /// translation of the <see cref="EnDefaultPages"/>() bodies — the same
    /// Markdown structure (heading, intro, the bullets, the closing line),
    /// idiomatic Danish UI copy (the familiar <c>dig</c> register held,
    /// sentence case, no word-for-word calques). These ship as
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows on a pristine
    /// DB (seeded once, then community-owned; the in-app editor supersedes
    /// them and no later deploy reverts an admin edit). The <c>da</c> catalog
    /// row is seeded disabled (the pre-seeded lane), but its page baseline is
    /// present so the moment an admin enables Danish, the pages are ready.
    /// <para>
    /// <b><c>about</c> is deliberately absent</b>, exactly as in
    /// <see cref="EnDefaultPages"/>() — it is the registry-key surface
    /// (the <c>about.*</c> keys in <see cref="KnownTranslationKeys"/>),
    /// not a Markdown body.
    /// </para>
    /// </summary>
    public static (string Slug, string Title, string Body)[] DaDefaultPages()
    {
        return
        [
            ("terms", "Brugsbetingelser",
             "## Brugsbetingelser\n\n" +
             "Kumunita er en selv-hostet platform til præcis ét nabolag. Som " +
             "operatør er du ansvarlig for, hvordan dit fællesskab bruger den: " +
             "hvem der deltager, hvad de skriver, og hvordan du modererer.\n\n" +
             "- **Nabolag i udformningen.** Platformen udgår fra præcis ét " +
             "afgrænset nabolag — ikke fra en offentlig feed.\n" +
             "- **Modtagerkredsen vælges af forfatteren.** Hvert indlæg bærer den " +
             "modtagerkreds, som dens forfatter har valgt; platformen håndhæver den.\n" +
             "- **Dine data tilhører dig.** Database og uploadede filer kan du " +
             "sikkerhedskopiere, migrere og lukke ned, som du vil.\n"),
            ("help", "Hjælp",
             "## Første skridt\n\n" +
             "Kumunita er et privat hjem til præcis ét nabolag — feeden, grupperne " +
             "og de fastgjorte noter.\n\n" +
             "- **Skriv** i en fælles feed og vælg, hvem der kan se dine indlæg " +
             "(en person, en gruppe eller hele nabolaget).\n" +
             "- **Grupper** hjælper dig med at organisere naboer om en bygning, " +
             "et projekt eller et fælles interesseområde.\n" +
             "- **Kontaktlisten** viser de beboere på platformen og de oplysninger, " +
             "hver af dem har valgt at dele.\n" +
             "- **Moderation** lader en global admin (og, hvor tildelt, en " +
             "moderator) holde feeden et sikkert sted.\n\n" +
             "## Guides\n\n" +
             "En trin-for-trin vejledning til platformen, i sprog alle forstår:\n\n" +
             "- [Første skridt](/pages/system/help/getting-started) — dine første ti minutter\n" +
             "- [Indlæg](/pages/system/help/posts) — Skrive, svare, redigere, slette, vedhæfte filer\n" +
             "- [Grupper](/pages/system/help/groups) — Offentlige og private grupper, gruppeindlæg\n" +
             "- [Udkast](/pages/system/help/drafts) — Gemme uden at publicere\n" +
             "- [Modtagerkreds](/pages/system/help/audience) — Hvem der kan se et indlæg\n" +
             "- [Sprog](/pages/system/help/language) — Dit sprog, din tidzone og dit datoformat\n" +
             "- [Begivenheder](/pages/system/help/events) — Feeden, deltagelse, påmindelser\n" +
             "- [Oversættere](/pages/system/help/translator) — Hvad en oversætter må og ikke må\n" +
             "- [Barnkonti](/pages/system/help/child-accounts) — Hvad en værgemand må og ikke må\n" +
             "- [At bruge en barnkonto](/pages/system/help/being-a-child) — Hvordan en barnkonto er, og hvad der er dit\n" +
             "- [Administrerende](/pages/system/help/admins) — Hvad en global admin gør, og hvad der holder rollen i skak\n" +
             "- [Moderatorer](/pages/system/help/moderators) — Hvad en moderator må og ikke må\n\n" +
             "Problemer med selve instansen? Det er en sag for operatøren — " +
             "se dokumentationen om selv-hosting, linket i footeren.\n"),
            ("privacy", "Privatliv",
             "## Privatliv\n\n" +
             "Kumunita er en selv-hostet platform til præcis ét nabolag. " +
             "Operatøren driver instansen og er ansvarlig for alt, hvad den indeholder.\n\n" +
             "- **Hvad platformen gemmer:** nabolagets konti, indlæg, grupper, " +
             "sider og medier — databasen plus de uploadede filer. Hvad der ikke " +
             "gemes, kan ikke gå tabt.\n" +
             "- **Modtagerkreds-tjekket er beskyttelsesmekanismen:** indhold er " +
             "privat som udgangspunkt; forfatteren vælger modtagerkredsen for hvert " +
             "indlæg, og platformen håndhæver det ved hver anmodning.\n" +
             "- **Audit som udgangspunkt:** adgang til indhold med begrænset " +
             "modtagerkreds samt moderations- og admin-handlinger logges altid.\n" +
             "- **Sikkerhedskopiering, migration og lukning er operatørens sag:** " +
             "databasen og de filer, du har uploadet, kan du selv gemme, migrere " +
             "og lukke ned.\n" +
             "- **Den ene browsercookie:** det foretrukne sprog — det er den eneste " +
             "cookie, platformen sætter. Der er ingen tredjepartscookies, og det " +
             "planlægges heller ikke.\n\n" +
             "Denne side udelader bevidst operatørspecifikke oplysninger — " +
             "bevaringsfrister, kontakt til databeskyttelsesansvarlig, underbehandler. " +
             "Det tilføjer du her i appen efter første start; det er designet, " +
             "ikke en mangel.\n"),
            ("conduct", "Adfærdskodeks",
             "## Adfærdskodeks\n\n" +
             "Det her er et afgrænset nabolag. Behandle dine naboer, som du vil " +
             "blive behandlet på din gade.\n\n" +
             "- Ingen mobning.\n" +
             "- Intet doxxing (at afsløre private oplysninger).\n" +
             "- Ingen spam.\n\n" +
             "Hvordan det håndhæves er op til operatøren og moderatorerne — " +
             "moderationen kører via rapporter og logges. Denne side opstiller " +
             "forventningen, ikke proceduren.\n"),
        ];
    }

    /// <summary>
    /// LS U04 (ADR 0042 D2) — seed the <c>de</c> / <c>fr</c>
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows for the seeded default
    /// pages (terms / help / privacy / conduct — the four-page set per ADR 0043
    /// D1), into the **caller's** in-flight
    /// <see cref="IDocumentSession"/> (the C3 invariant — same session, one
    /// commit). <paramref name="pageIds"/> is the slug → page-Id map
    /// <see cref="SeedDefaultPagesAsync"/> returns (the seeded pages'
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
            ("da", DaDefaultPages()),
        })
        {
            foreach (var (slug, title, body) in baselines)
            {
                if (!pageIds.TryGetValue(slug, out var pageId))
                    continue;   // defensive — EnDefaultPages/DeDefaultPages/FrDefaultPages/DaDefaultPages slugs agree; skip rather than throw
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

    // ─── ADR 0058 — the reset-to-seeded lane (a destructive override on
    //     request) ────────────────────────────────────────────────────────────
    //
    // The four-surface set (terms / help / privacy / conduct) and the UG
    // guides ship code-owned baselines here (the <see cref="EnDefaultPages"/>
    // / <see cref="DeDefaultPages"/> / <see cref="FrDefaultPages"/> /
    // <see cref="DaDefaultPages"/> + <see cref="GuidePages"/> /
    // <see cref="DeGuidePages"/> / <see cref="FrGuidePages"/> /
    // <see cref="DaGuidePages"/> registries). After first boot those are
    // community-owned (the ADR 0042 D1 "a human editor is the only writer of
    // a non-<c>en</c> body" invariant) — the warm-boot backfills only
    // create-if-missing and never clobber an admin edit. These two methods
    // are the **explicit, operator-requested** override that closes the gap:
    // they let an admin pull the seeded text back over a page they have
    // customized (or over a stale version from before a feature changed the
    // seeded copy). The <see cref="Kumunita.Core.Pages.PageService
    // .ResetToSeededAsync"/> write lane owns standing + the audit row; the
    // two methods below own the **content** (resolving the page to its
    // seeded baseline, and applying the <c>en</c> + non-<c>en</c> bodies) so
    // the seeder stays the single source of the seeded text and the lane and
    // the seeder can never drift.

    /// <summary>
    /// ADR 0058 — the **display probe**: does the <see cref="Page"/> at
    /// (<paramref name="slug"/>, <paramref name="parentId"/>) have a
    /// <b>seeded</b> <c>en</c> baseline and at least one non-<c>en</c>
    /// (de / fr / da) baseline to reset to? The Web edit view calls this to
    /// decide whether to render the "Reset to seeded" affordance (a
    /// <b>display</b> pin, not a gate — the real deny is the
    /// <see cref="Kumunita.Core.Pages.PageService.ResetToSeededAsync"/>
    /// standing re-check). <c>false</c> for a page the seed never wrote (a
    /// resident-authored system page, a blog page, a page with a custom slug)
    /// — there is no seeded text to reset to, so there is nothing to offer.
    /// <para>
    /// The parent resolution mirrors the seeder / backfill: the four-surface
    /// set resolves under the <c>system</c> root (ADR 0040), the guides
    /// resolve under the canonical <c>help</c> page (ADR 0057 D1), with the
    /// bare-root fallback for a pre-ADR-0040 deployment.
    /// </para>
    /// </summary>
    public static bool HasSeededText(string slug)
    {
        // The probe is purely over the code-owned seed registries (no DB): a
        // slug has seeded text to reset to exactly when it has a seeded `en`
        // body (the four-surface set or a UG guide). The non-`en` baselines
        // are a superset check — if the `en` body is seeded, at least the `en`
        // reset is meaningful, so the `en` presence is the single gate.
        if (string.IsNullOrEmpty(slug)) return false;
        return FindSeededPageText(slug) is not null;
    }

    /// <summary>
    /// ADR 0058 — the **reset applier**: overwrites the <see cref="Page"/>
    /// <c>en</c> <see cref="Kumunita.Core.Pages.Page.Title"/> /
    /// <see cref="Kumunita.Core.Pages.Page.Body"/> and the <c>de</c> /
    /// <c>fr</c> / <c>da</c> <see cref="Kumunita.Core.Pages
    /// .PageTranslation"/> rows of <paramref name="page"/> with the seeded
    /// baseline text (matched on <c>page.Slug</c>), in the **caller's**
    /// in-flight <see cref="IDocumentSession"/> (the C3 invariant — the
    /// <see cref="Kumunita.Core.Pages.PageService
    /// .ResetToSeededAsync"/> lane commits it in its single
    /// <c>SaveChangesAsync</c> and owns the standing re-check + the
    /// <c>page.reset</c> <see cref="Kumunita.Core.Authorization.AccessAudit"/>
    /// row). <para>
    /// Mirrors the <see cref="SeedDefaultPagesAsync"/> /
    /// <see cref="SeedPageTranslationsAsync"/> content exactly, but is
    /// **destructive by design** (a code-wins overwrite of the
    /// <c>(PageId, LanguageCode)</c> rows rather than the seeder's
    /// create-if-missing): the reset is the operator's explicit choice to
    /// accept the seeded text, so a customized body is replaced (the lane's
    /// confirmation prompt is the guard). A page with no seeded
    /// <c>en</c> baseline (<see cref="FindSeededPageText"/> returns
    /// <c>null</c>) is a <see cref="InvalidOperationException"/> — the Web
    /// layer maps that to a re-rendered form error (there is nothing to
    /// reset to).
    /// </para>
    /// </summary>
    public static async Task ResetSeededTextAsync(
        IDocumentSession session, Page page, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(page);
        var slug = page.Slug;

        // The `en` body + title (the page itself).
        var seeded = FindSeededPageText(slug);
        if (seeded is null)
            throw new InvalidOperationException(
                $"The page '{slug}' has no seeded text to reset to " +
                "(it is not one of the seeded platform pages or guides).");

        page.Title = seeded.Value.Title;
        page.Body = seeded.Value.Body;
        page.LanguageCode = SourceLanguage;
        page.Modified = now;
        session.Store(page);

        // The non-`en` translations (de / fr / da) — a code-wins overwrite of
        // each (PageId, LanguageCode) row that has a seeded baseline. A
        // language the seeded set does not carry (e.g. a community-added
        // language the baseline does not translate into) is left untouched —
        // the reset only replaces what the seed knows.
        foreach (var (code, baselines) in new[]
        {
            ("de", DeDefaultPages()),
            ("fr", FrDefaultPages()),
            ("da", DaDefaultPages()),
            ("de", DeGuidePages()),
            ("fr", FrGuidePages()),
            ("da", DaGuidePages()),
        })
        {
            var baseline = baselines.FirstOrDefault(b => b.Slug == slug);
            if (baseline == default)
                continue;   // this language carries no baseline for this slug.

            var row = await session
                .Query<PageTranslation>()
                .Where(t => t.PageId == page.Id && t.LanguageCode == code)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (row is null)
            {
                session.Store(new PageTranslation
                {
                    Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                    PageId = page.Id,
                    LanguageCode = code,
                    Title = baseline.Title,
                    Body = baseline.Body,
                    AuthorId = string.Empty,   // platform content — no resident author
                    Created = now,
                });
            }
            else
            {
                // Destructive by design (ADR 0058): the operator asked to
                // reset, so the customized row is replaced with the seeded
                // baseline. The original text is preserved by the
                // page.translation.* / page.reset audit trail + the page's
                // own Modified stamp (the seeder's create-if-missing never
                // reaches here — this lane is the explicit override).
                row.Title = baseline.Title;
                row.Body = baseline.Body;
                session.Store(row);
            }
        }
    }

    /// <summary>
    /// ADR 0058 — the seeded <c>en</c> (title, body) registry for a single
    /// page slug: from <see cref="EnDefaultPages"/> (the four-surface set) or
    /// <see cref="GuidePages"/> (the UG guides). <c>null</c> when the slug is
    /// not one of the seeded pages (the reset lane maps that to a form error
    /// — there is nothing to reset to).
    /// </summary>
    private static (string Title, string Body)? FindSeededPageText(string slug)
    {
        var en = EnDefaultPages().FirstOrDefault(p => p.Slug == slug);
        if (en != default)
            return (en.Title, en.Body);

        var guide = GuidePages().FirstOrDefault(p => p.Slug == slug);
        if (guide != default)
            return (guide.Title, guide.Body);

        return null;
    }

    /// <summary>
    /// Warm-boot backfill of the <c>de</c> / <c>fr</c> / <c>da</c>
    /// <see cref="PageTranslation"/> rows for the four canonical seeded pages
    /// (terms / help / privacy / conduct) — the lane that closes the ADR 0043
    /// D7 / ADR 0044 D5 gap: a deployment seeded before the <c>de</c> /
    /// <c>fr</c> / <c>da</c> baselines shipped (LS U04, SP U02) has the four
    /// pages but no translation rows, so <c>/terms</c> / <c>/help</c> /
    /// <c>/privacy</c> / <c>/conduct</c> render the authored-in <c>en</c>
    /// body even for a German / French / Danish-speaking resident.
    /// <para>
    /// **Create-if-missing only** (the ADR 0042 D1 invariant, the same rule
    /// as <see cref="SeedPageTranslationsAsync"/>): an existing row for a
    /// (page, language) pair is skipped, never refreshed — an admin's in-app
    /// edit of a baseline (the GlobalAdmin ∪ Translator lane, ADR 0021) is
    /// never clobbered by a later deploy. The <c>en</c> page bodies are
    /// **never** touched here: unlike the first-boot path, this runs on a warm
    /// DB where an admin may have edited the canonical <c>en</c> body, so a
    /// code-wins refresh would clobber that edit. A deployment that has the
    /// pages but no translation rows is exactly the one this lane serves —
    /// the rows are created from the same <see cref="DeDefaultPages"/> /
    /// <see cref="FrDefaultPages"/> / <see cref="DaDefaultPages"/> baselines
    /// the first-boot seeder uses, so a fresh and a backfilled instance are
    /// byte-identical for these surfaces.
    /// </para>
    /// <para>
    /// Idempotent: a second run finds every row it created on the first run
    /// and skips (the ADR 0042 D1 skip path). The <c>system/{slug}</c>
    /// primary resolution mirrors the <c>StaticPagesController</c> read path
    /// (ADR 0040 — the seeder nests the canonical pages under the
    /// <c>system</c> root); the bare-<c>{slug}</c> fallback covers a
    /// pre-ADR-0040 deployment whose pages are still orphan roots (the
    /// seeder's ADR 0040 lane re-parents them on the next first-boot
    /// re-seed, but a warm boot does not re-run that lane — the backfill
    /// finds them where they are).
    /// </para>
    /// </summary>
    public static async Task BackfillPageTranslationsAsync(
        IDocumentSession session,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // The slugs that have a de/fr/da baseline in the seed data — the
        // same set EnDefaultPages() covers (terms / help / privacy /
        // conduct; `about` is deliberately absent — it is the
        // registry-key surface, not a Markdown body).
        var slugs = EnDefaultPages().Select(p => p.Slug).ToList();

        // The system root (ADR 0040) — null when the instance predates ADR
        // 0040 and the pages are still orphan roots (the fallback below).
        var systemRoot = await session
            .Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        foreach (var slug in slugs)
        {
            // Primary resolution: system/{slug} (ADR 0040 — the seeder
            // nests the canonical pages under the system root). The fallback
            // below is the pre-ADR-0040 orphan: a deployment whose pages are
            // still root-level, which the ADR 0040 re-parent lane has not
            // touched on this warm boot.
            Page? page = null;
            if (systemRoot is not null)
            {
                page = await session
                    .Query<Page>()
                    .Where(p => p.Slug == slug
                                && p.ParentId == systemRoot.Id
                                && p.IsDeleted == false)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);
            }
            if (page is null)
            {
                page = await session
                    .Query<Page>()
                    .Where(p => p.Slug == slug && p.ParentId == null && p.IsDeleted == false)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);
            }

            if (page is null)
                continue;   // the page itself is absent; the first-boot path (or the ADR 0040 re-parent lane) owns that gap.

            // The de/fr/da baselines for this page, create-if-missing (ADR 0042 D1).
            foreach (var (code, baselines) in new[]
            {
                ("de", DeDefaultPages()),
                ("fr", FrDefaultPages()),
                ("da", DaDefaultPages()),
            })
            {
                var baseline = baselines.FirstOrDefault(b => b.Slug == slug);
                if (baseline == default)
                    continue;   // defensive — the slug is not in the baseline set (about, or a future slug).

                var existing = await session
                    .Query<PageTranslation>()
                    .Where(t => t.PageId == page.Id && t.LanguageCode == code)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    session.Store(new PageTranslation
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        PageId = page.Id,
                        LanguageCode = code,
                        Title = baseline.Title,
                        Body = baseline.Body,
                        AuthorId = string.Empty,   // platform content — no resident author
                        Created = now,
                    });
                }
                // else: skip — create-if-missing (never overwrite; ADR 0042 D1).
            }
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Warm-boot backfill of the <c>de</c> / <c>fr</c> / <c>da</c>
    /// <see cref="TranslationResource"/> baseline rows for the closed
    /// <see cref="Localization.KnownTranslationKeys"/> registry — the lane that
    /// closes the ADR 0042 D1 / ADR 0047 D2 "new-key asymmetry" for the
    /// UI-string surface (ADR 0052). A deployment whose first boot predates a
    /// baseline added to the registry in a later code release has the
    /// <c>en</c> row (and, for new keys, the provider floor renders its
    /// English) but no <c>de</c> / <c>fr</c> / <c>da</c> row, so a German /
    /// French / Danish-speaking resident sees the English floor for that string
    /// on every boot — the same visible seam ADR 0047 D2 closed for the four
    /// canonical pages' <see cref="Pages.PageTranslation"/> rows.
    /// <para>
    /// **Create-if-missing only** (the ADR 0042 D1 invariant, the same rule as
    /// <see cref="BackfillPageTranslationsAsync"/>): an existing row for a
    /// (key, language) pair is skipped, never refreshed — an admin's in-app
    /// edit of a baseline (the GlobalAdmin ∪ Translator lane, ADR 0021; the
    /// localization editor updates the row in place, so its <c>Id</c> persists
    /// and the skip path finds it) is never clobbered by a later deploy.
    /// </para>
    /// <para>
    /// **The <c>en</c> row is never read or written here.** It is the floor,
    /// owned by code (the ADR 0042 D1 code-wins upsert lives on the
    /// pristine-boot path only), and a new key's English already renders on a
    /// warm instance via the provider floor (ADR 0015) — the only gap this
    /// lane serves is the missing non-<c>en</c> baseline rows.
    /// </para>
    /// <para>
    /// Idempotent: a second run finds every row it created on the first run
    /// and skips (the ADR 0042 D1 skip path). No tombstones, no deletes —
    /// create-if-missing is what makes the re-run a no-op.
    /// </para>
    /// </summary>
    public static async Task BackfillUiStringBaselinesAsync(
        IDocumentSession session,
        CancellationToken ct)
    {
        foreach (var (code, baseline) in new[]
        {
            ("de", KnownTranslationKeys.DeValues),
            ("fr", KnownTranslationKeys.FrValues),
            ("da", KnownTranslationKeys.DaValues),
        })
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

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static string SeedAdminBody(string email, string userId, string token) =>
        $"Hi,\n\nThis first-boot setup email is the one-time handoff to bring your new Kumunita " +
        $"instance online. When you are ready, present the setup token below at the /admin/setup " +
        $"page to finish activating the GlobalAdmin account ({email}, userId {userId}).\n\n" +
        $"Setup token (one-time): {token}\n\n" +
        $"This token is a single-use credential — the account is sign-in-ready only after it " +
        $"is consumed (CompleteSeedAdminSetupAsync). Treat it like the account password.";
}
