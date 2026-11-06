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
/// <see cref="IProfileSettingsService.SeedComponentsAsync"/> is already idempotent and
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

    // ── Seeded-content data loaders (C02, ADR 0289) ─────────────────────────────────
    // The seeded guide / default-page text and the nested-guide parent map are
    // embedded resources, not C# literals: Bootstrap/GuidePages/{lang}/{slug}.md
    // (one Markdown file per guide, read by slug) + order.json (the seed order) +
    // parent-map.json (ADR 0160 nesting); Bootstrap/DefaultPages/{lang}/{slug}.md
    // + index.json (slug → display title, the four-surface set). The loaders below
    // read them; the seeding logic in this file (the IsPristineAsync gate, the
    // upserts, the en-only first-boot rule) is unchanged.
    /// <summary>
    /// Runs the five seeder steps. See the class doc for order and idempotency.
    /// Called once by <see cref="SchemaBootstrap"/> on a pristine DB.
    /// </summary>
    public static async Task SeedAsync(
        AppDbContext identity,
        IDocumentStore mt,
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager,
        IProfileSettingsService profileSettings,
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
        // C07 (ADR 0286) — SeedComponentsAsync moved to IProfileSettingsService.
        await profileSettings.SeedComponentsAsync();

        // 4. Language catalog (ADR 0005: source-language row + instance default).
        await SeedLanguageCatalogAsync(mt, logger, ct);

        // 4b. Site content (SITE lane, ADR 0150): the landing-surface
        // `SiteContent` singleton (the two heroes' editable eyebrow + lead and
        // the 9 section toggles) — the same "one row per instance" shape as the
        // `LocaleSettings` singleton seeded in step 4. Create-if-missing,
        // idempotent, never-overwrites (an admin's later edit is honored on a
        // warm re-run); the 4 text-field defaults are the exact `en` source
        // text from `KnownTranslationKeys` (SITE·3).
        await SeedSiteContentAsync(mt, logger, ct);

        // 4c. Surface labels (M29, ADR 0152): the `SurfaceLabels` singleton —
        // the 13 top-navigation surfaces' admin-editable display-name
        // overrides. Create-if-missing, idempotent, never-overwrites (the
        // exact `SiteContent` shape): a fresh boot stores exactly one row with
        // all 13 label fields `null` (the "use the `kw-l` fallback" default,
        // M29·3 / M29·4 — byte-identical to a fresh instance that never
        // touches the surface); a second boot leaves an existing row
        // untouched, so an admin's later rename is honored.
        await SeedSurfaceLabelsAsync(mt, logger, ct);

        // 4d. Admin onboarding (M30, ADR 0153): the `AdminOnboarding` singleton
        // — the GlobalAdmin's guided-walk-through completion record (the
        // `CompletedAt` flag, `null` = not-yet-guided). The same "one row per
        // instance" shape as the `SiteContent` / `SurfaceLabels` singletons
        // seeded in steps 4b/4c. Create-if-missing, idempotent, never-overwrites
        // (a GlobalAdmin's later completion stamp is honored on a warm re-run);
        // a fresh boot stores exactly one row with `CompletedAt = null` (the
        // "not-yet-guided" floor, M30·2).
        await SeedAdminOnboardingAsync(mt, logger, ct);

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
            // F2 — AUTH-02 (pin §8.2): store the SHA-256 hex digest, never the raw env
            // token at rest. Shares IdentityService.Sha256Hex (pin §2) — the operator
            // holds the raw in env/email; only its hash is persisted.
            Token = IdentityService.Sha256Hex(token),
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
    /// SITE lane (ADR 0150) — seed the <see cref="SiteContent.SiteContent"/>
    /// singleton: the two landing heroes' editable eyebrow + lead text and the
    /// show/hide toggle for every other section of <c>/home</c> and
    /// <c>/about</c>. The <c>LocaleSettings</c> "one row per instance" shape
    /// (ADR 0005 B): a single row, <c>Id = "singleton"</c>, so re-resolving it
    /// is a plain identity-keyed load.
    /// <para>
    /// <b>Create-if-missing, idempotent, never-overwrites</b> (the
    /// <see cref="SeedLanguageCatalogAsync"/> / <see cref="SeedDefaultPagesAsync"/>
    /// shape): a fresh boot stores exactly one row with the 13 fields at their
    /// defaults; a second boot (a warm re-run) leaves an existing row untouched,
    /// so an admin's later edit is honored (SITE·3 / SITE·6).
    /// </para>
    /// <para>
    /// The 4 text-field defaults are the **exact** <c>en</c> source text from
    /// <see cref="KnownTranslationKeys.EnValues"/> (the
    /// <c>home.intro_eyebrow</c> / <c>home.intro_lead</c> / <c>about.eyebrow</c>
    /// / <c>about.lead</c> keys), read from the registry — not retyped (SITE·3,
    /// byte-identical). The 9 toggle-field defaults are <c>true</c> (every
    /// section shown).
    /// </para>
    /// <para>
    /// <b>No <c>AccessAudit</c> row.</b> A first-boot seed is not an actor's
    /// auditable action (the seeder is not a principal) — the single
    /// <c>SaveChangesAsync</c> commits the singleton row only.
    /// </para>
    /// <para>
    /// <b>Public (not private)</b> so the Core test can pin the fresh-boot
    /// state + idempotency across two live boots without an
    /// <c>InternalsVisibleTo</c> (the repo's Core test constraint — only
    /// <c>public</c> members are reachable; the <see cref="SeedDefaultPagesAsync"/>
    /// precedent).
    /// </para>
    /// </summary>
    public static async Task SeedSiteContentAsync(
        IDocumentStore mt, ILogger logger, CancellationToken ct)
    {
        await using var session = mt.OpenSession(new SessionOptions());

        var existing = await session
            .LoadAsync<SiteContent.SiteContent>(SiteContent.SiteContent.SingletonId, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            session.Store(new SiteContent.SiteContent
            {
                Id = SiteContent.SiteContent.SingletonId,
                // 4 text fields — the exact `en` source text (SITE·3, byte-identical).
                HomeHeroEyebrow = KnownTranslationKeys.EnValues["home.intro_eyebrow"],
                HomeHeroLead = KnownTranslationKeys.EnValues["home.intro_lead"],
                AboutHeroEyebrow = KnownTranslationKeys.EnValues["about.eyebrow"],
                AboutHeroLead = KnownTranslationKeys.EnValues["about.lead"],
                // 9 toggles — every section shown (the POCO initializers already
                // default to `true`; set explicitly to make the seed's intent
                // visible, mirroring SeedLanguageCatalogAsync's explicit defaults).
                HomeShowAboutButton = true,
                HomeShowFeatures = true,
                HomeShowRoadmap = true,
                AboutShowFeatures = true,
                AboutShowScope = true,
                AboutShowPhilosophy = true,
                AboutShowProject = true,
                AboutShowWhatsNew = true,
                AboutShowContactCta = true,
            });
            await session.SaveChangesAsync(ct).ConfigureAwait(false);

            logger.LogInformation(
                "First boot: SiteContent singleton seeded (id 'singleton'; hero text = the shipped `kw-l` `en` text, all 9 section toggles shown — ADR 0150).");
        }
        else
        {
            logger.LogInformation(
                "First boot: SiteContent singleton already present; skipping (idempotent, never-overwrites — ADR 0150).");
        }
    }
    /// <summary>
    /// M29 (ADR 0152) — seed the <see cref="SurfaceLabels.SurfaceLabels"/>
    /// singleton: the 13 top-navigation surfaces' admin-editable display-name
    /// overrides (the nav item + the surface's page header, resolved by
    /// <see cref="SurfaceLabels.ISurfaceLabelsService"/>). The
    /// <c>SiteContent</c> / <c>LocaleSettings</c> "one row per instance" shape
    /// (ADR 0005 B / ADR 0150): a single row, <c>Id = "singleton"</c>, so
    /// re-resolving it is a plain identity-keyed load.
    /// <para>
    /// <b>Create-if-missing, idempotent, never-overwrites</b> (the exact
    /// <see cref="SeedSiteContentAsync"/> shape): a fresh boot stores exactly
    /// one row with <b>all 13</b> label fields <c>null</c> — the "use the
    /// <c>kw-l</c> fallback" default (M29·3 / M29·4), so a fresh instance that
    /// never touches the surface is byte-identical to today (every label falls
    /// back to its <c>kw-l</c> key in the viewer's language). A second boot
    /// (a warm re-run) leaves an existing row untouched, so an admin's later
    /// rename is honored (the never-overwrites pin).
    /// </para>
    /// <para>
    /// <b>All 13 fields are <c>null</c> by construction</b> — there is no
    /// "default text" to seed here (unlike <see cref="SeedSiteContentAsync"/>,
    /// whose 4 text fields carry the shipped <c>en</c> source text). The
    /// floor is the <c>kw-l</c> registry entry each field resolves to, read at
    /// resolution time by <see cref="SurfaceLabels.ISurfaceLabelsService"/> —
    /// the registry entries stay (ADR 0152 D3), so the seeder only creates the
    /// row and leaves every override unset.
    /// </para>
    /// <para>
    /// <b>No <c>AccessAudit</c> row.</b> A first-boot seed is not an actor's
    /// auditable action (the seeder is not a principal) — the single
    /// <c>SaveChangesAsync</c> commits the singleton row only.
    /// </para>
    /// <para>
    /// <b>Public (not private)</b> so the Core test can pin the fresh-boot
    /// state + idempotency across two live boots without an
    /// <c>InternalsVisibleTo</c> (the <see cref="SeedSiteContentAsync"/>
    /// precedent — the repo's Core test constraint).
    /// </para>
    /// </summary>
    public static async Task SeedSurfaceLabelsAsync(
        IDocumentStore mt, ILogger logger, CancellationToken ct)
    {
        await using var session = mt.OpenSession(new SessionOptions());

        var existing = await session
            .LoadAsync<SurfaceLabels.SurfaceLabels>(SurfaceLabels.SurfaceLabels.SingletonId, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            // All 13 label fields default to `null` (the "use the `kw-l`
            // fallback" default, M29·3 / M29·4). None are set — the floor is
            // the `kw-l` registry entry each resolves to (ADR 0152 D3).
            session.Store(new SurfaceLabels.SurfaceLabels
            {
                Id = SurfaceLabels.SurfaceLabels.SingletonId,
                // Home / Announcements / Community / Groups / Events / Projects /
                // Inventory / Bookmarks / Documents / Pages / Tags / Directory /
                // People — all left `null` (the POCO initializers already
                // default each to `null`; the all-null row IS the shipped
                // default, so no field is set explicitly here).
            });
            await session.SaveChangesAsync(ct).ConfigureAwait(false);

            logger.LogInformation(
                "First boot: SurfaceLabels singleton seeded (id 'singleton'; all 13 label overrides null — the `kw-l` fallback default, byte-identical to the shipped text — ADR 0152).");
        }
        else
        {
            logger.LogInformation(
                "First boot: SurfaceLabels singleton already present; skipping (idempotent, never-overwrites — ADR 0152).");
        }
    }
    /// <summary>
    /// M30 (ADR 0153) — seed the <see
    /// cref="AdminOnboarding.AdminOnboarding"/> singleton: the
    /// GlobalAdmin's guided-walk-through completion record (the
    /// <c>CompletedAt</c> flag, <c>null</c> = not-yet-guided). The
    /// <c>SiteContent</c> / <c>SurfaceLabels</c> / <c>LocaleSettings</c>
    /// "one row per instance" shape (ADR 0005 B / ADR 0150 / ADR 0152): a
    /// single row, <c>Id = "singleton"</c>, so re-resolving it is a plain
    /// identity-keyed load.
    /// <para>
    /// <b>Create-if-missing, idempotent, never-overwrites</b> (the exact
    /// <see cref="SeedSiteContentAsync"/> / <see cref="SeedSurfaceLabelsAsync"/>
    /// shape): a fresh boot stores exactly one row with <c>CompletedAt =
    /// null</c> — the "not-yet-guided" floor (M30·2), so a fresh instance
    /// that never touches the surface looks exactly the same as it does
    /// today (the banner is the affordance, not a wall). A second boot (a
    /// warm re-run) leaves an existing row untouched, so a GlobalAdmin's
    /// later completion stamp is honored (the never-overwrites pin).
    /// </para>
    /// <para>
    /// <b><c>CompletedAt</c> is <c>null</c> by construction</b> — there is no
    /// "default value" to seed here (unlike <see cref="SeedSiteContentAsync"/>,
    /// whose 4 text fields carry the shipped <c>en</c> source text). The floor
    /// is <c>null</c> = not-yet-guided (M30·2), so the seeder only creates the
    /// row and leaves <c>CompletedAt</c> unset.
    /// </para>
    /// <para>
    /// <b>No <c>AccessAudit</c> row.</b> A first-boot seed is not an actor's
    /// auditable action (the seeder is not a principal) — the single
    /// <c>SaveChangesAsync</c> commits the singleton row only.
    /// </para>
    /// <para>
    /// <b>Public (not private)</b> so the Core test can pin the fresh-boot
    /// state + idempotency across two live boots without an
    /// <c>InternalsVisibleTo</c> (the <see cref="SeedSiteContentAsync"/> /
    /// <see cref="SeedSurfaceLabelsAsync"/> precedent — the repo's Core test
    /// constraint).
    /// </para>
    /// </summary>
    public static async Task SeedAdminOnboardingAsync(
        IDocumentStore mt, ILogger logger, CancellationToken ct)
    {
        await using var session = mt.OpenSession(new SessionOptions());

        var existing = await session
            .LoadAsync<AdminOnboarding.AdminOnboarding>(AdminOnboarding.AdminOnboarding.SingletonId, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            // CompletedAt defaults to `null` (the "not-yet-guided" floor,
            // M30·2). None is set — the floor is `null` (M30·2).
            session.Store(new AdminOnboarding.AdminOnboarding
            {
                Id = AdminOnboarding.AdminOnboarding.SingletonId,
                // CompletedAt — left `null` (the POCO initializer already
                // defaults it to `null`; the not-yet-guided row IS the
                // shipped floor, so no field is set explicitly here).
            });
            await session.SaveChangesAsync(ct).ConfigureAwait(false);

            logger.LogInformation(
                "First boot: AdminOnboarding singleton seeded (id 'singleton'; CompletedAt null — the not-yet-guided floor, byte-identical to a fresh instance that never touches the surface — ADR 0153).");
        }
        else
        {
            logger.LogInformation(
                "First boot: AdminOnboarding singleton already present; skipping (idempotent, never-overwrites — ADR 0153).");
        }
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
        var enPages = SeededContentCatalog.EnDefaultPages();

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

        // ADR 0160 — nested guide parents: some guides (settings/*, group/*,
        // admin/*, projects/*) hang from another guide's page rather than
        // from `help` directly. The parent map tells us which.
        var parentMap = SeededContentCatalog.GuideParentMap();

        foreach (var (slug, title, body) in SeededContentCatalog.GuidePages())
        {
            // Resolve the parent: the guide's own parentSlug (a sibling guide)
            // if present, else the `help` page (top-level guides).
            string parentId = helpPageId;
            if (parentMap.TryGetValue(slug, out var parentSlug))
            {
                // The parent guide was seeded in this same pass (top-level
                // guides like `projects`, `settings`, `group`, `admin` come
                // before their nested children in GuidePages() order).
                if (!guidePageIds.TryGetValue(parentSlug, out var parentGuideId))
                {
                    // Defensive: the parent slug is in GuidePages() but was
                    // not yet seeded (shouldn't happen — top-level entries
                    // precede their children). Fall back to `help`.
                    parentId = helpPageId;
                }
                else
                {
                    parentId = parentGuideId;
                }
            }

            var existing = await session
                .Query<Page>()
                .Where(p => p.Slug == slug && p.ParentId == parentId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                var newGuide = new Page
                {
                    Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                    Slug = slug,
                    ParentId = parentId,   // under `help` or a top-level guide (ADR 0057 D1 / ADR 0160)
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
            ("de", SeededContentCatalog.DeGuidePages()),
            ("fr", SeededContentCatalog.FrGuidePages()),
            ("da", SeededContentCatalog.DaGuidePages()),
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
            ("de", SeededContentCatalog.DeDefaultPages()),
            ("fr", SeededContentCatalog.FrDefaultPages()),
            ("da", SeededContentCatalog.DaDefaultPages()),
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
    private static string SeedAdminBody(string email, string userId, string token) =>
        $"Hi,\n\nThis first-boot setup email is the one-time handoff to bring your new Kumunita " +
        $"instance online. When you are ready, present the setup token below at the /admin/setup " +
        $"page to finish activating the GlobalAdmin account ({email}, userId {userId}).\n\n" +
        $"Setup token (one-time): {token}\n\n" +
        $"This token is a single-use credential — the account is sign-in-ready only after it " +
        $"is consumed (CompleteSeedAdminSetupAsync). Treat it like the account password.";
}
