using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Marten;
using Marten.Services;
using NSubstitute;
using Xunit;

// The doc type and the service share the parent namespace `Kumunita.Core.SurfaceLabels`.
// A using-alias (not a bare using) avoids the type/namespace name collision in the
// Kumunita.Web.Tests scope (where the Kumunita.Core.Tests enclosing-namespace walk
// does not apply) — SL.SurfaceLabels = the doc type, SL.SurfaceLabelsService = the service.
using SL = Kumunita.Core.SurfaceLabels;

namespace Kumunita.Web.Tests;

/// <summary>
/// M29 U09 (ADR 0152) — the nav + header <b>resolution</b> pins (M29·1 /
/// M29·3 / M29·4): the nav and the surface's <c>&lt;h1&gt;</c> header call the
/// <b>same</b> resolver (<see cref="ISurfaceLabelsService.GetLabelAsync"/>)
/// so they always resolve to the <b>same</b> value; a saved label shows in the
/// nav <b>and</b> the header; a fresh instance shows the shipped <c>kw-l</c>
/// text; and a blank / whitespace label falls back to the <c>kw-l</c> key.
/// </summary>
/// <para>
/// <b>Test model:</b> the nav and the header are two call sites of the <b>one</b>
/// resolver — <see cref="ISurfaceLabelsService.GetLabelAsync(surfaceKey,
/// fallbackKey, effectiveLanguage)"/> (the U05 nav + the U06/U07 headers, M29·1).
/// The pins drive the <b>real</b> <see cref="SurfaceLabelsService"/> (the live
/// store + a stub <see cref="ITranslationProvider"/> standing in for the
/// <c>kw-l</c> floor, M29·3 / M29·4) — so "nav" = the resolver call the nav
/// makes, "header" = the resolver call the header makes, and M29·1 (they agree)
/// falls out of them calling the same seam. Integration tests (real Marten SQL),
/// the <see cref="GuardianCommunityBlockTests"/> shape — the
/// <c>SurfaceLabels</c> row is only visible via a live store.
/// </para>
/// <para>
/// <b>Collision idiom:</b> the doc type is fully qualified as
/// <c>SurfaceLabels.SurfaceLabels</c> (the type and its parent namespace share
/// the name <c>SurfaceLabels</c>; the unqualified name in this namespace
/// resolves to the *namespace*) — the exact <c>SiteContent.SiteContent</c>
/// idiom.
/// </para>
public sealed class SurfaceLabelResolutionTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — a fresh instance's nav shows the shipped kw-l text (M29·1/M29·2/M29·4) ──

    [Fact(DisplayName = "U09 a fresh instance's nav shows the shipped kw-l text (no override)")]
    public async Task FreshInstance_NavShowsKwLText()
    {
        var harness = await BuildAsync();
        var ct = TestContext.Current.CancellationToken;

        // A fresh instance (no SurfaceLabels row) — the nav resolves the label
        // via the resolver; with no override it falls back to the kw-l floor.
        var navLabel = await harness.GetLabel("announcements", "nav.announcements", "en", ct);
        Assert.Equal("Announcements", navLabel);
    }

    // ── 2 — a saved label shows in the nav (M29·1/M29·3) ───────────────────

    [Fact(DisplayName = "U09 a saved label shows in the nav (the override wins)")]
    public async Task SavedLabel_NavShowsOverride()
    {
        var harness = await BuildAsync();
        var ct = TestContext.Current.CancellationToken;

        // Seed a saved override for the Announcements surface (the seeder row,
        // with the admin's one label applied).
        await harness.SaveLabelAsync("announcements", "News", ct);

        // The nav (the resolver call) shows the saved override.
        var navLabel = await harness.GetLabel("announcements", "nav.announcements", "en", ct);
        Assert.Equal("News", navLabel);
    }

    // ── 3 — a saved label shows in the surface's <h1> header (M29·1) ───────

    [Fact(DisplayName = "U09 a saved label shows in the surface's page header (the override wins)")]
    public async Task SavedLabel_HeaderShowsOverride()
    {
        var harness = await BuildAsync();
        var ct = TestContext.Current.CancellationToken;

        await harness.SaveLabelAsync("projects", "Board", ct);

        // The header (the resolver call the <h1> makes) shows the saved override.
        var headerLabel = await harness.GetLabel("projects", "nav.projects", "en", ct);
        Assert.Equal("Board", headerLabel);
    }

    // ── 4 — the nav and the header resolve to the SAME value (M29·1) ───────

    [Fact(DisplayName = "U09 the nav and the header resolve to the same value (one resolver, one value)")]
    public async Task SavedLabel_SameLabelNavAndHeader()
    {
        var harness = await BuildAsync();
        var ct = TestContext.Current.CancellationToken;

        await harness.SaveLabelAsync("groups", "Boards", ct);

        // The nav and the header both call the SAME resolver with the SAME
        // (surfaceKey, fallbackKey) the U05 nav + U06/U07 headers use — so they
        // resolve to the same value (M29·1: a rename never splits nav from header).
        var navLabel    = await harness.GetLabel("groups", "nav.groups", "en", ct);
        var headerLabel = await harness.GetLabel("groups", "nav.groups", "en", ct);
        Assert.Equal("Boards", navLabel);
        Assert.Equal(navLabel, headerLabel);
    }

    // ── 5 — a blank / whitespace label falls back to the kw-l key (M29·3/M29·4) ──

    [Fact(DisplayName = "U09 a blank label falls back to the kw-l key (empty = use default)")]
    public async Task BlankLabel_FallsBackToKwL()
    {
        var harness = await BuildAsync();
        var ct = TestContext.Current.CancellationToken;

        // Seed a whitespace-only label (a "blank" override) for People.
        await harness.SaveLabelAsync("people", "   ", ct);

        // The stored override is blank → the resolver falls back to the kw-l key
        // (M29·3 / M29·4 — the "empty = use default" shape).
        var label = await harness.GetLabel("people", "nav.people", "en", ct);
        Assert.Equal("People", label);
    }

    // ─── Harness ────────────────────────────────────────────────────────────

    /// <summary>
    /// A scratch store (the M29 doc type registered — without it the
    /// <c>SurfaceLabels</c> doc is invisible to Marten) + the real
    /// <see cref="SurfaceLabelsService"/> over it (a stub
    /// <see cref="ITranslationProvider"/> stands in for the <c>kw-l</c> floor —
    /// the en source text for the keys the pins drive).
    /// </summary>
    private sealed class Harness
    {
        public Harness(IDocumentStore store, SL.SurfaceLabelsService svc)
        {
            Store = store;
            Svc = svc;
        }

        public IDocumentStore Store { get; }
        public SL.SurfaceLabelsService Svc { get; }

        /// <summary>The one resolver the nav + header call (M29·1).</summary>
        public Task<string> GetLabel(string surfaceKey, string fallbackKey, string lang, CancellationToken ct) =>
            Svc.GetLabelAsync(surfaceKey, fallbackKey, lang, ct);

        /// <summary>
        /// Seed the singleton (the seeder row) with one label override applied.
        /// The seeder is the only row-creator in the app (M29·6); this stores the
        /// all-null row first (a stand-in for the seeder) then applies the label.
        /// </summary>
        public async Task SaveLabelAsync(string surfaceKey, string label, CancellationToken ct)
        {
            await using var session = Store.OpenSession(new SessionOptions());
            var row = await session.LoadAsync<SL.SurfaceLabels>(SL.SurfaceLabels.SingletonId, ct);
            if (row is null)
            {
                row = new SL.SurfaceLabels { Id = SL.SurfaceLabels.SingletonId };
                session.Store(row);
            }
            switch (surfaceKey.Trim().ToLowerInvariant())
            {
                case "home":        row.Home = label; break;
                case "announcements": row.Announcements = label; break;
                case "community":   row.Community = label; break;
                case "groups":      row.Groups = label; break;
                case "events":      row.Events = label; break;
                case "projects":    row.Projects = label; break;
                case "inventory":   row.Inventory = label; break;
                case "bookmarks":   row.Bookmarks = label; break;
                case "documents":   row.Documents = label; break;
                case "pages":       row.Pages = label; break;
                case "tags":        row.Tags = label; break;
                case "directory":   row.Directory = label; break;
                case "people":      row.People = label; break;
                default:            throw new ArgumentOutOfRangeException(nameof(surfaceKey));
            }
            session.Store(row);
            await session.SaveChangesAsync(ct);
        }
    }

    private async Task<Harness> BuildAsync()
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
            SurfaceLabelsDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);

        // The kw-l floor (M29·3 / M29·4) — the en source text for the keys the
        // pins drive (the canonical values in KnownTranslationKeys.EnValues).
        var translations = Substitute.For<ITranslationProvider>();
        translations.GetAsync("nav.announcements", Arg.Any<string?>()).Returns("Announcements");
        translations.GetAsync("nav.projects", Arg.Any<string?>()).Returns("Projects");
        translations.GetAsync("nav.groups", Arg.Any<string?>()).Returns("Groups");
        translations.GetAsync("nav.people", Arg.Any<string?>()).Returns("People");

        return new Harness(store, new SL.SurfaceLabelsService(store, translations));
    }
}
