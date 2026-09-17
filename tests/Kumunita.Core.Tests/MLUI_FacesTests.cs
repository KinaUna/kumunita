using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ML-UI U8 — the FACES gate (the plan's FACES table L1–L9 + the acceptance
/// gate), the Core-seam half. Written against the **shipped** code (U1–U7),
/// re-anchoring the inherited 19 <c>ML</c> anchors (<see cref="LocalizationServiceTests"/>)
/// on the **registry** (<see cref="KnownTranslationKeys"/>) — the whole point
/// of the <c>ML-UI</c> lane: the gate now tests the *whole* (a seeded <c>en</c>
/// floor, a registered key, a closed universe), not just the seam in isolation.
///
/// <para>
/// The "view path" (L1/L3) is exercised at the **cookie→provider seam**, in
/// the style of the inherited <c>ML</c> M7 anchors (D8-2): the <c>&lt;kw-l&gt;</c>
/// TagHelper is a documented pass-through, so L1 here is
/// <c>provider.GetAsync("nav.home", "pl")</c> over a seeded registry key. The
/// Web cookie-write assertion (L3's M·11 half, U7's recorded deferral) lives in
/// <c>Kumunita.Web.Tests/MLUI_FacesTests.cs</c>.
/// </para>
///
/// <para>
/// <b>Seeding note (D8-5 / the blocker protocol):</b>
/// <see cref="Kumunita.Core.Bootstrap.FirstBootSeeder.SeedTranslationResourcesAsync"/>
/// is <c>private</c> and <see cref="Kumunita.Core.Bootstrap.FirstBootSeeder.SeedAsync"/>
/// needs the whole bootstrap dependency set, so the tests **mirror** the seeder
/// step's exact shape (an <c>en</c> <see cref="TranslationResource"/> row per key
/// in <see cref="KnownTranslationKeys.EnValues"/>) — the same
/// <c>SeedM1RowAsync</c>-mirrors-<c>SeedLanguageCatalogAsync</c> precedent the
/// inherited <c>ML</c> anchors use. The registry is the single source of truth
/// both the seeder and these tests read, so mirroring is exact.
/// </para>
///
/// <para>
/// No new Core or Web code; no new seams. The private helper set at the bottom
/// is a copy of the inherited idiom (same template as
/// <see cref="LocalizationServiceTests"/> + <c>M3DocTypes.Configure</c> for
/// L8's <see cref="Post"/> plant).
/// </para>
/// </summary>
public class MLUI_FacesTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── L1 — a signed-in resident with a `pl` preference sees the nav in Polish ──
    // (M·1, M·10.) D8-2: the view path at the cookie→provider seam. Seed the
    // `en` floor (the registry) + a `pl` row for the nav key; a `pl` preference
    // resolves the `pl` text. (The Web cookie-write half is L3's, Web.Tests.)

    [Fact(DisplayName = "L1 a pl-preference resident sees the nav bar in Polish (cookie→provider seam)")]
    public async Task MLUI_U8_L1_PreferenceResolvesNavKeyInPolish()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await SeedEnFloor(store);
        await AddLanguage(store, "pl", "Polski");

        // A real registry key (the nav) with a pl row.
        const string key = "nav.home";
        await UpsertTranslation(store, key, "pl", "Strona główna");

        var provider = new TranslationProvider(store);

        // pl preference → the pl row (M·1: preferred-if-enabled wins).
        Assert.Equal("Strona główna", await provider.GetAsync(key, "pl"));

        // The key is in the registry (assert by reference, never a hardcoded list).
        Assert.Contains(key, KnownTranslationKeys.AllKeys);
        Assert.True(KnownTranslationKeys.EnValues.ContainsKey(key));
    }

    // ── L2 — a pl-preferring resident sees a label with no pl row fall back per string ──
    // (M·2, M·10.) Seed the `en` floor (registry) + `pl` for **all but two**
    // registry keys. The two missing `pl` keys resolve to their `en` value
    // (per-string, M·2); every sibling that **has** a `pl` row resolves `pl`.

    [Fact(DisplayName = "L2 a key with no pl row falls back per string to en, the rest stays pl")]
    public async Task MLUI_U8_L2_MissingKeyFallsBackPerStringRestStaysPolish()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        var allKeys = KnownTranslationKeys.AllKeys.ToList();
        // Omit two registry keys from the pl seed (deterministic: the first two).
        var omit = allKeys.Take(2).ToList();
        var present = allKeys.Where(k => !omit.Contains(k)).ToList();

        // The `en` floor: every registry key (mirrors the seeder, D8-5).
        await SeedEnFloor(store);
        // `pl` for every key except the two omitted — value pinned as "pl-<key>".
        foreach (var key in present)
            await UpsertTranslation(store, key, "pl", $"pl-{key}");

        var provider = new TranslationProvider(store);

        // The two missing pl keys resolve to their `en` reference (M·2).
        Assert.Equal(KnownTranslationKeys.EnValues[omit[0]], await provider.GetAsync(omit[0], "pl"));
        Assert.Equal(KnownTranslationKeys.EnValues[omit[1]], await provider.GetAsync(omit[1], "pl"));

        // Every present sibling resolves its pl row (the "rest of the page stays pl").
        foreach (var key in present)
            Assert.Equal($"pl-{key}", await provider.GetAsync(key, "pl"));
    }

    // ── L4 — a fresh instance (default en, no cookie) renders entirely in English ──
    // (M·1, M·9.) D8-5: the `en` floor is **seeded** — mirror the seeder step,
    // plant an `en` row for every key in `KnownTranslationKeys.EnValues`.

    [Fact(DisplayName = "L4 a fresh instance (default en) renders every in-scope key in English (the en floor is seeded)")]
    public async Task MLUI_U8_L4_FreshInstance_RendersEnglishFloor()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        // Mirror FirstBootSeeder.SeedTranslationResourcesAsync (D8-5): an `en`
        // row per registry key. No other language is added.
        await SeedEnFloor(store);

        var provider = new TranslationProvider(store);

        // No preference → the instance default is "en".
        Assert.Equal("en", await provider.ResolveEffectiveLanguageAsync(null));

        // Every registry key resolves to its `en` reference (the seeded floor).
        foreach (var (key, enText) in KnownTranslationKeys.EnValues)
            Assert.Equal(enText, await provider.GetAsync(key, null));
    }

    // ── L5 (Core half) — the closed list of canonical keys is the registry ──
    // (M·4, M·6.) D8-6: re-anchor the inherited U6 batch read against the
    // registry — plant `pl` rows for exactly 10 registry keys, assert the
    // map's key set equals that 10-key subset (no fallback, no extras).

    [Fact(DisplayName = "L5 the batch read returns exactly the stored pl rows (re-anchored on the registry)")]
    public async Task MLUI_U8_L5_BatchRead_ReanchoredOnRegistry()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        // Plant pl rows for exactly 10 registry keys (the closed subset).
        var ten = KnownTranslationKeys.AllKeys.Take(10).ToList();
        foreach (var key in ten)
            await UpsertTranslation(store, key, "pl", $"pl-{key}");

        var svc = new LocalizationService(store);
        var pl = await svc.GetTranslationsForAsync("pl");

        Assert.NotNull(pl);
        Assert.Equal(ten.Count, pl.Count);
        Assert.Equal(
            ten.ToHashSet(),
            pl.Keys.ToHashSet());
        // Each value is the stored pl text (raw rows — no fallback into en).
        foreach (var key in ten)
            Assert.Equal($"pl-{key}", pl[key]);
    }

    // ── L6 — the closed loop at the seam (save a registry key → visible + 1 audit row) ──
    // (M·4, M·6.) D8-7: `UpsertTranslationAsync` on a **registry** key →
    // `provider.GetAsync` live → exactly one `AccessAudit` row with the pinned
    // shape (the `ML` M9/M17 shape, re-anchored on a registry key).

    [Fact(DisplayName = "L6 saving a pl value is visible next request + exactly one AccessAudit row (Via = Admin)")]
    public async Task MLUI_U8_L6_SaveRegistryKey_VisibleNextRequest_OneAuditRow()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        const string actor = "admin-L6";
        const string key = "nav.groups";   // a real registry key
        Assert.True(KnownTranslationKeys.EnValues.ContainsKey(key));
        const string text = "Grupy";

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

    // ── L7 — the pl completeness view lists real missing/present keys; 100% for en ──
    // (M·2, M·9.) D8-5: because the `en` floor is seeded, completeness is real.
    // `en` half: `GetCompletenessAsync("en")` has an **empty** MissingKeys.
    // `pl` half: seed all but two registry keys → exactly those two are Missing.

    [Fact(DisplayName = "L7 the pl completeness view lists real missing/present keys and is 100% for en")]
    public async Task MLUI_U8_L7_CompletenessRealForPl_FullForEn()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        var allKeys = KnownTranslationKeys.AllKeys.ToList();
        var omit = allKeys.Take(2).ToList();

        // The `en` floor (registry) + `pl` for all but the two omitted keys.
        await SeedEnFloor(store);
        foreach (var key in allKeys.Where(k => !omit.Contains(k)))
            await UpsertTranslation(store, key, "pl", $"pl-{key}");

        var svc = new LocalizationService(store);

        // `en` half: 100% present (the floor IS the en universe).
        var en = await svc.GetCompletenessAsync("en");
        Assert.Empty(en.MissingKeys);
        Assert.Equal(allKeys.Count, en.PresentKeys.Count);

        // `pl` half: exactly the two omitted registry keys are missing; the rest present.
        var pl = await svc.GetCompletenessAsync("pl");
        Assert.Equal(omit.OrderBy(k => k).ToArray(), pl.MissingKeys.ToArray());
        Assert.DoesNotContain(omit[0], pl.PresentKeys);
        Assert.DoesNotContain(omit[1], pl.PresentKeys);
        Assert.Equal(allKeys.Count - omit.Count, pl.PresentKeys.Count);
    }

    // ── L8 — a pl-preferring resident reads an en post → the body is as authored ──
    // (M·3.) D8-8: the negative test. Plant an `en` Post (the `ML` M6 idiom),
    // seed a `pl` registry row, and assert: the post's Body is unchanged (load
    // it back), the provider resolves the `pl` UI string, and the provider API
    // surface has **no** method that takes a Post/PostReply/Group.

    [Fact(DisplayName = "L8 an en post body is never translated; the provider surface never consults UGC")]
    public async Task MLUI_U8_L8_UgcBodyAsAuthored_ProviderNeverConsultsUgc()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        // UGC: an en-authored Post (the ML M6 idiom).
        const string postBody = "This is a post authored in English, verbatim.";
        await Plant(store, new Post
        {
            Id = "l8-post",
            ComponentId = "c-l8",
            AuthorId = "u-l8",
            Body = postBody,
            Created = DateTimeOffset.UtcNow,
            Audience = new Authorization.Audience(),
        });

        // Platform text: a `pl` row for a **registry** key.
        await UpsertTranslation(store, "nav.home", "pl", "Strona główna");

        var provider = new TranslationProvider(store);

        // The provider resolves platform text in pl (a registry key).
        Assert.Equal("Strona główna", await provider.GetAsync("nav.home", "pl"));

        // The Post's Body is **unchanged** — never translated (M·3).
        await using var session = store.QuerySession();
        var post = await session.LoadAsync<Post>("l8-post", TestContext.Current.CancellationToken);
        Assert.Equal(postBody, post!.Body);

        // The provider never consults UGC: no method on the read seam takes a
        // Post / PostReply / Group (assert the API surface, not the body).
        AssertUgcFreeSurface();
    }

    // ── Gate: Closed loop (L6's shape, a registry key) ───────────────────────────
    // The admin edits a **real, canonical** key → a resident with a matching
    // preference sees the new text on the next request, and the
    // `translation.save` AccessAudit row exists.

    [Fact(DisplayName = "GATE closed-loop — a registry key save is visible to the preference + audited")]
    public async Task MLUI_U8_Gate_ClosedLoop_RegistryKeySave()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        const string actor = "admin-gate-cl";
        const string key = "nav.announcements";
        Assert.True(KnownTranslationKeys.EnValues.ContainsKey(key));
        const string text = "Ogłoszenia";

        var svc = new LocalizationService(store);
        await svc.UpsertTranslationAsync(key, "pl", text, actor);

        // The resident's next request (a pl preference) sees the new text.
        var provider = new TranslationProvider(store);
        Assert.Equal(text, await provider.GetAsync(key, "pl"));

        // The audit row exists with the pinned shape.
        var audits = await AuditRows(store, action: "translation.save");
        Assert.Single(audits);
        Assert.Equal("translation", audits[0].TargetKind);
        Assert.Equal(key, audits[0].TargetId);
        Assert.Equal(AccessVia.Admin, audits[0].Via);
        Assert.Equal(AccessOutcome.Allow, audits[0].Outcome);
    }

    // ── Gate: Handoff (default → pl; per-string fallback on a registry key) ─────
    // The GlobalAdmin sets the default to `pl` → a resident with **no**
    // preference and a key that has no `pl` row sees that one label fall back
    // to `en` while a sibling key resolves `pl` (the `ML` M10/M2 composition,
    // re-anchored on registry keys).

    [Fact(DisplayName = "GATE handoff — default pl: a no-pl registry key falls back to en, a sibling resolves pl")]
    public async Task MLUI_U8_Gate_Handoff_DefaultPl_PerStringFallbackOnRegistry()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");

        var allKeys = KnownTranslationKeys.AllKeys.ToList();
        var noPlKey = allKeys[0];              // the sibling with NO pl row
        var siblingKey = allKeys[1];           // the sibling WITH a pl row

        // `en` floor for both; a `pl` row for the sibling only.
        await UpsertTranslation(store, noPlKey, "en", KnownTranslationKeys.EnValues[noPlKey]);
        await UpsertTranslation(store, siblingKey, "en", KnownTranslationKeys.EnValues[siblingKey]);
        await UpsertTranslation(store, siblingKey, "pl", "pl-sibling");

        // The admin sets the instance default to `pl` (the LocaleSettings handoff).
        var svc = new LocalizationService(store);
        await svc.SetDefaultLanguageAsync("pl", "admin-gate-ho");

        var provider = new TranslationProvider(store);

        // No preference → default is now pl. The no-pl key falls back per string to en.
        Assert.Equal(KnownTranslationKeys.EnValues[noPlKey], await provider.GetAsync(noPlKey, null));
        // The sibling has a pl row → it resolves pl.
        Assert.Equal("pl-sibling", await provider.GetAsync(siblingKey, null));
    }

    // ── Gate: Part-vs-whole (the inherited anchors still pass in the same run) ───
    // D8-10: a thin [Fact] that re-runs the M1 + M9 + M12 assertions in sequence
    // on registry keys. The **real** part-vs-whole evidence is the full
    // `dotnet exec` run count recorded in the handoff note (the 19 `ML` anchors
    // AND the new L/gate tests in the same `Kumunita.Core.Tests` process).

    [Fact(DisplayName = "GATE part-vs-whole — M1 + M9 + M12 anchor shapes hold on registry keys in one run")]
    public async Task MLUI_U8_Gate_PartVsWhole_InheritedAnchorsHoldOnRegistry()
    {
        var store = await BootStoreAsync();
        await SeedM1RowAsync(store);
        await AddLanguage(store, "pl", "Polski");
        await SeedEnFloor(store);

        var keys = KnownTranslationKeys.AllKeys.ToList();
        var k1 = keys[0];
        var k2 = keys[1];
        var k3 = keys[2];

        // M1 (registry key): a pl preference resolves the pl row.
        await UpsertTranslation(store, k1, "pl", "pl-one");
        var provider = new TranslationProvider(store);
        Assert.Equal("pl-one", await provider.GetAsync(k1, "pl"));
        Assert.Equal(KnownTranslationKeys.EnValues[k1], await provider.GetAsync(k1, null));

        // M9 (registry key): an admin save is visible next request + one audit row.
        var svc = new LocalizationService(store);
        await svc.UpsertTranslationAsync(k2, "pl", "pl-two", "admin-gate-pw");
        Assert.Equal("pl-two", await provider.GetAsync(k2, "pl"));
        var audits = await AuditRows(store, action: "translation.save");
        Assert.Single(audits);
        Assert.Equal(k2, audits[0].TargetId);
        Assert.Equal(AccessVia.Admin, audits[0].Via);

        // M12 (registry key): completeness lists a real missing key (k3 has no pl).
        await UpsertTranslation(store, k3, "en", KnownTranslationKeys.EnValues[k3]);
        var completeness = await svc.GetCompletenessAsync("pl");
        Assert.Contains(k3, completeness.MissingKeys);
        Assert.Contains(k2, completeness.PresentKeys);
    }

    // ── L8's UGC-free-surface assertion (D8-8) ───────────────────────────────────
    // The provider read seam (ITranslationProvider) has **no** method whose
    // parameter list contains a Post / PostReply / Group — the surface never
    // consults UGC (M·3). Asserted against the actual type surface, not a body.

    private static void AssertUgcFreeSurface()
    {
        var ugcTypes = new[]
        {
            typeof(Post),          // Kumunita.Core.Posts
            typeof(PostReply),     // Kumunita.Core.Posts
            typeof(Group),         // Kumunita.Core.UserInfo
        };
        var methods = typeof(ITranslationProvider).GetMethods(
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Instance);

        foreach (var method in methods)
        {
            foreach (var p in method.GetParameters())
            {
                Assert.DoesNotContain(p.ParameterType, ugcTypes);
            }
        }
    }

    // ── Shared helpers (the inherited LocalizationServiceTests idiom, copied) ────

    /// <summary>
    /// Fresh scratch DB for the calling test, bootstrapped Marten store with
    /// M0 + M1 features + the M1 + M3 domain documents (L8's Post plant needs
    /// the M3 surface). The caller owns the returned store's lifetime.
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
    /// Idempotent: load-then-store.
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

    /// <summary>
    /// The <c>en</c> floor (D8-5): mirror
    /// <c>FirstBootSeeder.SeedTranslationResourcesAsync</c> — an <c>en</c>
    /// <see cref="TranslationResource"/> row per key in
    /// <see cref="KnownTranslationKeys.EnValues"/> (code-wins upsert by
    /// (Key, "en"), never a non-<c>en</c> row). This is what L4/L7's
    /// "the <c>en</c> floor is seeded" asserts against.
    /// </summary>
    private static async Task SeedEnFloor(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.OpenSession(new SessionOptions());

        foreach (var (key, enText) in KnownTranslationKeys.EnValues)
        {
            var existing = await session
                .Query<TranslationResource>()
                .Where(t => t.Key == key && t.LanguageCode == "en")
                .FirstOrDefaultAsync(ct);

            if (existing is null)
            {
                session.Store(new TranslationResource
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Key = key,
                    LanguageCode = "en",
                    Text = enText
                });
            }
            else
            {
                existing.Text = enText;
                session.Store(existing);
            }
        }

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
