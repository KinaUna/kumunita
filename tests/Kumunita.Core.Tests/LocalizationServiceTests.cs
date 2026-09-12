using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Posts;
using Marten;
using Marten.Services;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ML lane U7 — the 19 pinned <see cref="LocalizationServiceTests"/> [Fact]s
/// (design doc <c>multilingual-design.md</c> §Pinned seam tests: 13 FACES
/// M1–M13 + 6 audit-row-shape, verbatim). Each test drives the shipped Core
/// seams (<see cref="TranslationProvider"/> / <see cref="LocalizationService"/>)
/// over a fresh scratch Postgres database (the <see cref="PostgresFixture"/>
/// harness — the same template <see cref="PostServiceTests"/> uses, extended
/// with <c>M3DocTypes.Configure</c> for M6's <see cref="Post"/> plant).
/// <para>
/// No new Core or Web code; no new seams. The 19 names are the **whole**;
/// the three-test acceptance gate (U8) runs them together with the inherited
/// M1/M2/M3/M3b/media/group-posts anchors (no per-name isolation).
/// </para>
/// </summary>
public class LocalizationServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── M1 — a resident with a `pl` preference sees a UI string in Polish ──
    // Seed `en` + `pl` (both enabled); `en` + `pl` rows for key "nav.home".
    // `pl` preference → the `pl` text; no preference → the `en` text.
    // (M·1, M·2.)

    [Fact]
    public async Task M1_PreferenceCookie_ResolvesPolishUIString()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        const string key = "nav.home";
        await UpsertTranslation(store, key, "en", "Home");
        await UpsertTranslation(store, key, "pl", "Strona główna");

        var provider = new TranslationProvider(store);

        // pl preference → the pl row (M·1: preferred-if-enabled wins).
        Assert.Equal("Strona główna", await provider.GetAsync(key, "pl"));

        // No preference → the instance default is "en" (M1 seed) → the en row.
        Assert.Equal("Home", await provider.GetAsync(key, null));
    }

    // ── M2 — per-string fallback (not a view-flip) ──────────────────────────
    // `pl` has row for key "a" but not for key "b"; `en` has both.
    // "a" resolves to the `pl` text; "b" falls back to the `en` text.
    // (M·2.)

    [Fact]
    public async Task M2_MissingStringInPreferredFallsBackPerString()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        // pl has "a" but not "b"; en has both.
        await UpsertTranslation(store, "a", "en", "A-en");
        await UpsertTranslation(store, "a", "pl", "A-pl");
        await UpsertTranslation(store, "b", "en", "B-en");
        // No pl row for "b".

        var provider = new TranslationProvider(store);

        // "a" in pl → the pl row (preferred wins).
        Assert.Equal("A-pl", await provider.GetAsync("a", "pl"));

        // "b" in pl → no pl row → falls back to en (per-string, M·2).
        Assert.Equal("B-en", await provider.GetAsync("b", "pl"));
    }

    // ── M3 — no preference → instance default ───────────────────────────────
    // Default is `pl`; no preference cookie → resolves to `pl`.
    // (M·1.)

    [Fact]
    public async Task M3_NoPreference_UsesInstanceDefault()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");
        await SetDefault(store, "pl");

        await UpsertTranslation(store, "nav.home", "pl", "Strona główna");
        await UpsertTranslation(store, "nav.home", "en", "Home");

        var provider = new TranslationProvider(store);

        // No preference → the instance default is "pl" (M·1).
        Assert.Equal("Strona główna", await provider.GetAsync("nav.home", null));
    }

    // ── M4 — fresh instance, default en, en floor ───────────────────────────
    // Only the M1 seed (en row + en default); no other language added.
    // `en` string resolves; effective language is "en".
    // (M·1, M·9.)

    [Fact]
    public async Task M4_FreshInstance_DefaultIsEnglish()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        // No other language added — only `en` in the catalog.

        await UpsertTranslation(store, "nav.home", "en", "Home");

        var provider = new TranslationProvider(store);

        Assert.Equal("Home", await provider.GetAsync("nav.home", null));
        Assert.Equal("en", await provider.ResolveEffectiveLanguageAsync(null));
    }

    // ── M5 — static page localizes per page ──────────────────────────────────
    // `en` has a "terms" page; `pl` has NO "terms" page (fallback to en).
    // `pl` has a "help" page (preferred wins).
    // (M·2, M·7.)

    [Fact]
    public async Task M5_StaticPageLocalizesPerPage()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        // en terms page; no pl terms page (per-page fallback).
        await UpsertPage(store, "terms", "en", "Terms", "en-terms-body");
        // pl help page (preferred wins).
        await UpsertPage(store, "help", "pl", "Pomoc", "pl-help-body");

        var provider = new TranslationProvider(store);

        // "terms" with pl preference → no pl row → falls back to en (M·2).
        var terms = await provider.GetPageAsync("terms", "pl");
        Assert.NotNull(terms);
        Assert.Equal("Terms", terms!.Title);
        Assert.Equal("en-terms-body", terms.Body);

        // "help" with pl preference → the pl row (preferred wins).
        var help = await provider.GetPageAsync("help", "pl");
        Assert.NotNull(help);
        Assert.Equal("Pomoc", help!.Title);
    }

    // ── M6 — UGC renders as authored, never translated ──────────────────────
    // Plant an `en` Post; plant a `pl` TranslationResource and a `pl`
    // LocalizedPage. The Post's Body is **unchanged** (as authored). The
    // provider resolves the `pl` UI string and page independently — it
    // **never** reads a Post body (M·3).
    // (M·3.)

    [Fact]
    public async Task M6_UgcRendersAsAuthored_NotTranslated()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        // UGC: an en-authored Post.
        const string postBody = "Hello, this is a post in English.";
        await Plant(store, new Post
        {
            Id = "m6-post",
            ComponentId = "c-m6",
            AuthorId = "u-m6",
            Body = postBody,
            Created = DateTimeOffset.UtcNow,
            Audience = new Authorization.Audience(),
        });

        // Platform text: a pl TranslationResource and a pl LocalizedPage.
        await UpsertTranslation(store, "nav.home", "pl", "Strona główna");
        await UpsertPage(store, "help", "pl", "Pomoc", "pl-help");

        var provider = new TranslationProvider(store);

        // The provider resolves platform text in pl (independent of the Post).
        Assert.Equal("Strona główna", await provider.GetAsync("nav.home", "pl"));
        var page = await provider.GetPageAsync("help", "pl");
        Assert.NotNull(page);
        Assert.Equal("Pomoc", page!.Title);

        // The Post's Body is **unchanged** — never translated (M·3).
        await using var session = store.QuerySession();
        var post = await session.LoadAsync<Post>("m6-post", TestContext.Current.CancellationToken);
        Assert.Equal(postBody, post!.Body);
    }

    // ── M7 — preference change takes effect on the next request ─────────────
    // Seed `en` + `pl`; `en` has "nav.home", `pl` has "feed.reply" but NOT
    // "nav.home". The provider resolves per-preference: "en" → en text;
    // "pl" → pl text for "feed.reply", fallback to en for "nav.home".
    // (M·1, M·5 — the cookie → provider handoff is the Web layer's job.)

    [Fact]
    public async Task M7_PreferenceChange_TakesEffectNextRequest()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        await UpsertTranslation(store, "nav.home", "en", "Home");
        await UpsertTranslation(store, "feed.reply", "pl", "Odpowiedz");
        // No pl row for "nav.home"; no en row for "feed.reply".

        var provider = new TranslationProvider(store);

        // en preference → en text for "nav.home".
        Assert.Equal("Home", await provider.GetAsync("nav.home", "en"));

        // pl preference → no pl row for "nav.home" → falls back to en (M·2).
        Assert.Equal("Home", await provider.GetAsync("nav.home", "pl"));

        // pl preference → the pl row for "feed.reply" (preferred wins).
        Assert.Equal("Odpowiedz", await provider.GetAsync("feed.reply", "pl"));
    }

    // ── M8 — preference at a removed language falls back to default ─────────
    // Seed `en` + `pl` (both enabled); default is `en`. Remove `pl`.
    // `pl` preference → no enabled `pl` row → falls back to `en`.
    // (M·1, M·7.)

    [Fact]
    public async Task M8_PreferenceAtRemovedLanguage_FallsBackToDefault()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        await UpsertTranslation(store, "nav.home", "en", "Home");
        await UpsertTranslation(store, "nav.home", "pl", "Strona główna");

        // Remove pl from the catalog (not the default, so it's allowed).
        var svc = new LocalizationService(store);
        await svc.RemoveLanguageAsync("pl", "admin-m8");

        var provider = new TranslationProvider(store);

        // pl preference → pl is no longer in the catalog → falls back to en.
        Assert.Equal("Home", await provider.GetAsync("nav.home", "pl"));
    }

    // ── M9 — admin saves a translation, visible on next request ─────────────
    // Seed M1 row; admin upserts a pl string; provider resolves it live;
    // one audit row with the pinned shape.
    // (M·4, M·6.)

    [Fact]
    public async Task M9_AdminSavesTranslation_VisibleNextRequest()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        const string actor = "admin-m9";
        const string key = "nav.home";
        const string text = "Strona główna";

        var svc = new LocalizationService(store);
        await svc.UpsertTranslationAsync(key, "pl", text, actor);

        // M·4: live on the next request.
        var provider = new TranslationProvider(store);
        Assert.Equal(text, await provider.GetAsync(key, "pl"));

        // M·6: exactly one audit row with the pinned shape.
        var audits = await AuditRows(store, action: "translation.save");
        Assert.Single(audits);
        var row = audits[0];
        Assert.Equal("translation.save", row.Action);
        Assert.Equal("translation", row.TargetKind);
        Assert.Equal(key, row.TargetId);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(actor, row.ActorId);
    }

    // ── M10 — admin sets default, resident without preference sees it ───────
    // Seed `en` + `pl`; admin sets default to `pl`; provider with no
    // preference resolves to `pl`; one audit row.
    // (M·1, M·6.)

    [Fact]
    public async Task M10_AdminSetsDefault_ResidentSeesNewDefault()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        await UpsertTranslation(store, "nav.home", "pl", "Strona główna");
        await UpsertTranslation(store, "nav.home", "en", "Home");

        const string actor = "admin-m10";
        var svc = new LocalizationService(store);
        await svc.SetDefaultLanguageAsync("pl", actor);

        // M·1: the default is picked up live — no preference → pl.
        var provider = new TranslationProvider(store);
        Assert.Equal("Strona główna", await provider.GetAsync("nav.home", null));

        // M·6: exactly one audit row with the pinned shape.
        var audits = await AuditRows(store, action: "language.set-default");
        Assert.Single(audits);
        Assert.Equal("language.set-default", audits[0].Action);
        Assert.Equal("language", audits[0].TargetKind);
        Assert.Equal("pl", audits[0].TargetId);
        Assert.Equal(AccessVia.Admin, audits[0].Via);
        Assert.Equal(AccessOutcome.Allow, audits[0].Outcome);
    }

    // ── M11 — removing the default language is blocked ──────────────────────
    // Default is `en`; attempt to remove `en` → throws; catalog unchanged;
    // no audit row committed.
    // (M·7, M·6.)

    [Fact]
    public async Task M11_RemoveDefaultLanguage_Blocked()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        const string actor = "admin-m11";
        var svc = new LocalizationService(store);

        // Default is "en" — removing it must throw (M·7 fail-closed).
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await svc.RemoveLanguageAsync("en", actor));

        // The en catalog row is unchanged.
        await using var session = store.QuerySession();
        var en = await session.LoadAsync<LanguageCatalog>("en", TestContext.Current.CancellationToken);
        Assert.NotNull(en);

        // No `language.remove` audit row was committed for the blocked attempt
        // (M·7 fail-closed — the throw happens before the commit).
        var removeAudits = await AuditRows(store, action: "language.remove");
        Assert.Empty(removeAudits);
    }

    // ── M12 — completeness view shows missing keys ──────────────────────────
    // en has keys a, b, c; pl has a, b (no c). en has "terms" page; pl has
    // "terms" page. Completeness for pl: MissingKeys = [c].
    // (M·2, M·6.)

    [Fact]
    public async Task M12_CompletenessView_ShowsMissingKeys()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        // en universe: keys a, b, c; page "terms".
        await UpsertTranslation(store, "a", "en", "A");
        await UpsertTranslation(store, "b", "en", "B");
        await UpsertTranslation(store, "c", "en", "C");
        await UpsertPage(store, "terms", "en", "Terms", "en");

        // pl present: keys a, b (no c); page "terms".
        await UpsertTranslation(store, "a", "pl", "A-pl");
        await UpsertTranslation(store, "b", "pl", "B-pl");
        await UpsertPage(store, "terms", "pl", "Warunki", "pl");

        var svc = new LocalizationService(store);
        var completeness = await svc.GetCompletenessAsync("pl");

        Assert.Equal("pl", completeness.LanguageCode);
        Assert.Contains("a", completeness.PresentKeys);
        Assert.Contains("b", completeness.PresentKeys);
        Assert.DoesNotContain("c", completeness.PresentKeys);
        Assert.Contains("c", completeness.MissingKeys);
        Assert.DoesNotContain("a", completeness.MissingKeys);
        Assert.Contains("terms", completeness.PresentPageSlugs);
        Assert.Empty(completeness.MissingPageSlugs);
    }

    // ── M13 — removed-language page rows are retained ────────────────────────
    // Seed `en` + `pl`; a `pl` "terms" page. Remove `pl`. Re-add `pl`.
    // The `pl` "terms" page row is **restored** (retained, not deleted).
    // (M·7.)

    [Fact]
    public async Task M13_RemovedLanguagePageRows_Retained()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        const string actor = "admin-m13";
        var svc = new LocalizationService(store);

        await svc.UpsertPageAsync("terms", "pl", "Warunki", "pl-terms-body", actor);

        // Remove pl (not the default — allowed).
        await svc.RemoveLanguageAsync("pl", actor);

        // Re-add pl.
        await svc.AddLanguageAsync("pl", "Polski", actor);

        // The pl "terms" page row is **restored** (M·7 retention).
        var page = await svc.GetPageAsync("terms", "pl");
        Assert.NotNull(page);
        Assert.Equal("Warunki", page!.Title);
        Assert.Equal("pl-terms-body", page.Body);
    }

    // ── ML-UI U6 — the batch read (D6-1: raw rows, no fallback, empty-not-null) ──
    // Seed two rows for "pl", one for "en". GetTranslationsForAsync("pl") →
    // exactly the two pl key → text pairs; no fallback into en; a code with no
    // rows → an empty map, never null. One query (no N round-trips).
    // (M·4 read path — the editor shows raw rows; no audit row: it is a read.)

    [Fact]
    public async Task MLUI_U6_GetTranslationsFor_ReturnsRawRows_NoFallback()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        // pl has two rows; en has a third key pl lacks (the fallback trap).
        await UpsertTranslation(store, "nav.home", "pl", "Strona główna");
        await UpsertTranslation(store, "nav.groups", "pl", "Grupy");
        await UpsertTranslation(store, "nav.directory", "en", "Directory");

        var svc = new LocalizationService(store);

        // Exactly the stored pl rows — no fallback into the en universe (D6-1).
        var pl = await svc.GetTranslationsForAsync("pl");
        Assert.NotNull(pl);
        Assert.Equal(2, pl.Count);
        Assert.Equal("Strona główna", pl["nav.home"]);
        Assert.Equal("Grupy", pl["nav.groups"]);
        Assert.False(pl.ContainsKey("nav.directory"));

        // A code with no rows → empty map, never null.
        var zz = await svc.GetTranslationsForAsync("zz");
        Assert.NotNull(zz);
        Assert.Empty(zz);
    }

    // ── Audit-row-shape tests (6) ────────────────────────────────────────────

    // 14 — Admin_AddLanguage_AuditRowShape_ViaAdmin
    [Fact]
    public async Task Admin_AddLanguage_AuditRowShape_ViaAdmin()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);

        const string actor = "admin-a14";
        var svc = new LocalizationService(store);
        await svc.AddLanguageAsync("pl", "Polski", actor);

        var audits = await AuditRows(store, action: "language.add");
        Assert.Single(audits);
        var row = audits[0];
        Assert.Equal("language.add", row.Action);
        Assert.Equal("language", row.TargetKind);
        Assert.Equal("pl", row.TargetId);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(actor, row.ActorId);
    }

    // 15 — Admin_RemoveLanguage_AuditRowShape_ViaAdmin
    [Fact]
    public async Task Admin_RemoveLanguage_AuditRowShape_ViaAdmin()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        const string actor = "admin-a15";
        var svc = new LocalizationService(store);
        // Remove pl (not the default — allowed).
        await svc.RemoveLanguageAsync("pl", actor);

        var audits = await AuditRows(store, action: "language.remove");
        Assert.Single(audits);
        var row = audits[0];
        Assert.Equal("language.remove", row.Action);
        Assert.Equal("language", row.TargetKind);
        Assert.Equal("pl", row.TargetId);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(actor, row.ActorId);
    }

    // 16 — Admin_SetDefaultLanguage_AuditRowShape_ViaAdmin
    [Fact]
    public async Task Admin_SetDefaultLanguage_AuditRowShape_ViaAdmin()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        const string actor = "admin-a16";
        var svc = new LocalizationService(store);
        await svc.SetDefaultLanguageAsync("pl", actor);

        var audits = await AuditRows(store, action: "language.set-default");
        Assert.Single(audits);
        var row = audits[0];
        Assert.Equal("language.set-default", row.Action);
        Assert.Equal("language", row.TargetKind);
        Assert.Equal("pl", row.TargetId);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(actor, row.ActorId);
    }

    // 17 — Admin_SaveTranslation_AuditRowShape_ViaAdmin
    [Fact]
    public async Task Admin_SaveTranslation_AuditRowShape_ViaAdmin()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        const string actor = "admin-a17";
        const string key = "nav.home";
        var svc = new LocalizationService(store);
        await svc.UpsertTranslationAsync(key, "pl", "Strona główna", actor);

        var audits = await AuditRows(store, action: "translation.save");
        Assert.Single(audits);
        var row = audits[0];
        Assert.Equal("translation.save", row.Action);
        Assert.Equal("translation", row.TargetKind);
        Assert.Equal(key, row.TargetId);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(actor, row.ActorId);
    }

    // 18 — Admin_SavePage_AuditRowShape_ViaAdmin
    [Fact]
    public async Task Admin_SavePage_AuditRowShape_ViaAdmin()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        const string actor = "admin-a18";
        const string slug = "terms";
        var svc = new LocalizationService(store);
        await svc.UpsertPageAsync(slug, "pl", "Warunki", "body", actor);

        var audits = await AuditRows(store, action: "page.save");
        Assert.Single(audits);
        var row = audits[0];
        Assert.Equal("page.save", row.Action);
        Assert.Equal("localized_page", row.TargetKind);
        Assert.Equal(slug, row.TargetId);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(actor, row.ActorId);
    }

    // 19 — Admin_RemoveDefaultLanguage_Blocked_NoAuditRow
    [Fact]
    public async Task Admin_RemoveDefaultLanguage_Blocked_NoAuditRow()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);

        const string actor = "admin-a19";
        var svc = new LocalizationService(store);

        // Default is "en" — removing it must throw (M·7 fail-closed).
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await svc.RemoveLanguageAsync("en", actor));

        // The fail-closed **absence**: no audit row at all (not just no
        // language.remove row — the throw happens before any write).
        var audits = await AuditRows(store);
        Assert.Empty(audits);
    }

    // ── Shared helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Fresh scratch DB for the calling test, bootstrapped Marten store with
    /// M0 + M1 features + the M1 + M3 domain documents (M6's Post plant needs
    /// the M3 surface). Applied against the fresh catalog. The caller owns the
    /// returned store's lifetime — the scratch DB itself is container-lifetime.
    /// </summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>
    /// Seed the M1 language-catalog row + the LocaleSettings singleton —
    /// mirrors <c>FirstBootSeeder.SeedLanguageCatalogAsync</c> exactly.
    /// Idempotent: load-then-store (a re-run is a no-op-or-refresh).
    /// </summary>
    private static async Task SeedM1RowAsync(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.OpenSession(new SessionOptions());

        var existingEn = await session.LoadAsync<LanguageCatalog>("en", ct);
        session.Store(existingEn ?? new LanguageCatalog
        {
            Id = "en",
            NativeName = "English",
            Enabled = true,
            SortOrder = 0
        });

        var existingSettings = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct);
        session.Store(existingSettings ?? new LocaleSettings
        {
            Id = LocaleSettings.SingletonId,
            DefaultLanguageCode = "en"
        });

        await session.SaveChangesAsync(ct);
    }

    /// <summary>Plant a document row directly (test fixture seeding, not a
    /// service write seam).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    /// <summary>Add a language row via the service (a service write seam —
    /// drives the M·6 audit-row path).</summary>
    private static async Task AddLanguage(IDocumentStore store, string code, string nativeName)
    {
        var svc = new LocalizationService(store);
        await svc.AddLanguageAsync(code, nativeName, "admin-seed");
    }

    /// <summary>Set the instance default via a direct write (test fixture
    /// seeding — not a service write seam; the M10 test drives the service's
    /// own SetDefaultLanguageAsync for the audit-row assertion).</summary>
    private static async Task SetDefault(IDocumentStore store, string code)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.OpenSession(new SessionOptions());
        var settings = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct);
        settings!.DefaultLanguageCode = code;
        session.Store(settings);
        await session.SaveChangesAsync(ct);
    }

    /// <summary>Upsert a TranslationResource row via a direct write (test
    /// fixture seeding — not a service write seam).</summary>
    private static async Task UpsertTranslation(
        IDocumentStore store, string key, string languageCode, string text)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.OpenSession(new SessionOptions());
        var existing = await session
            .Query<TranslationResource>()
            .Where(t => t.Key == key && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct);
        if (existing is null)
            session.Store(new TranslationResource
            {
                Id = Guid.NewGuid().ToString("N"),
                Key = key,
                LanguageCode = languageCode,
                Text = text
            });
        else
        {
            existing.Text = text;
            session.Store(existing);
        }
        await session.SaveChangesAsync(ct);
    }

    /// <summary>Upsert a LocalizedPage row via a direct write (test fixture
    /// seeding — not a service write seam).</summary>
    private static async Task UpsertPage(
        IDocumentStore store, string slug, string languageCode, string title, string body)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.OpenSession(new SessionOptions());
        var existing = await session
            .Query<LocalizedPage>()
            .Where(p => p.Slug == slug && p.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct);
        if (existing is null)
            session.Store(new LocalizedPage
            {
                Id = Guid.NewGuid().ToString("N"),
                Slug = slug,
                LanguageCode = languageCode,
                Title = title,
                Body = body,
                Updated = DateTimeOffset.UtcNow
            });
        else
        {
            existing.Title = title;
            existing.Body = body;
            existing.Updated = DateTimeOffset.UtcNow;
            session.Store(existing);
        }
        await session.SaveChangesAsync(ct);
    }

    /// <summary>Query the AccessAudit rows, optionally filtered by Action.
    /// Returns all matching rows (ordered by At) for the test's assertions.</summary>
    private static async Task<IReadOnlyList<AccessAudit>> AuditRows(
        IDocumentStore store, string? action = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        System.Linq.IQueryable<AccessAudit> query = session.Query<AccessAudit>();
        if (action is not null)
            query = query.Where(a => a.Action == action);
        return await query
            .OrderBy(a => a.At)
            .ToListAsync(ct);
    }
}
