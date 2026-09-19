using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Tags;
using Marten;
using Marten.Services;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// <c>TG</c> (tags) seam tests (design doc <c>tags-design.md</c> §2.4, the
/// 24-test pin list) — U4 lands the first three: **F5** (the
/// <see cref="PageKind.System"/> write-lane refusal, C-TG·6), **F11** (the
/// ADR 0004 §B.1 additive no-reseed pin — a pre-existing post's
/// <c>TagIds</c> reads back **empty** after a warm re-boot), and **F12**
/// (a fresh instance has **zero** tags, C-TG·7).
/// <para>
/// The harness shape mirrors <see cref="PageServiceTests"/> (fresh scratch
/// Postgres per test, the <c>*DocTypes.Configure</c> cluster, the
/// <see cref="PostgresFixture"/> shared <c>postgres:18</c> container). The
/// only delta vs. <see cref="PageServiceTests"/> is the added
/// <c>TagDocTypes.Configure(opts)</c> line (the U3 handoff note: the test
/// boot path is the one place the <c>Tags</c> doc types must be registered
/// here, since <c>TagService</c> is not yet landed — U5/U6).
/// </para>
/// <para>
/// These tests exercise the <see cref="Page.TagIds"/> / <see cref
/// "Post.TagIds"/> additive fields and the <see cref="PageService"/>
/// System-page refusal only. The <c>TagService</c> attach / read lanes (U5
/// / U6) are **not** exercised yet — no <c>TagService</c> reference appears
/// in this file (U4's scope pin).
/// </para>
/// </summary>
public class TagServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ─── F5 — the System-page write-lane refusal (C-TG·6, D6) ───────────
    //
    // A PageKind.System page's TagIds is **always empty**: the shape permits
    // the field on both kinds (the ADR 0004 §B.1 additive field), the
    // **write lane is the guard**. The refusal throws (ArgumentException —
    // a shape violation, not a standing denial) **before** anything is
    // stored, on **both** CreateAsync and UpdateAsync. A PageKind.User (blog)
    // page with the **same** non-empty set is accepted and round-trips.

    [Fact]
    public async Task F5_SystemPageRefusesNonEmptyTagIds()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        var ct = TestContext.Current.CancellationToken;
        var tagIds = new[] { "tag-sanitation", "tag-budget" };

        // (1) Create: a System page (the default Kind) with a non-empty
        // TagIds set is refused — even by a GlobalAdmin (the refusal is a
        // shape violation, independent of standing). Throws **before**
        // anything is stored.
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.CreateAsync(
                    new Page { Slug = "f5-sys", Title = "T", Body = "b", TagIds = tagIds },
                    "u-admin", RolesSet(Roles.GlobalAdmin), session));
        }
        await using (var q = store.QuerySession())
        Assert.Equal(0, await q.Query<Page>().CountAsync(ct));

        // (2) Update: a planted System page — an incoming non-empty TagIds
        // set is refused, **before** the field-copy / save.
        await Plant(store, new Page
        {
            Id = "f5-sys-upd", Slug = "f5-sys-upd", Title = "Old", Body = "ob",
            AuthorId = "u-someone", Kind = PageKind.System,
        });
        await using (var session2 = store.OpenSession(new SessionOptions()))
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.UpdateAsync(
                    new Page { Id = "f5-sys-upd", Slug = "f5-sys-upd", Title = "New", Body = "nb", TagIds = tagIds },
                    "u-admin", RolesSet(Roles.GlobalAdmin), session2));
        }
        // The stored System page's TagIds is still empty (the refusal threw
        // before the copy).
        await using (var q2 = store.QuerySession())
        {
            var stored = await q2.LoadAsync<Page>("f5-sys-upd", ct);
            Assert.NotNull(stored);
            Assert.NotNull(stored!.TagIds);
            Assert.Empty(stored.TagIds);
        }

        // (3) The **same** non-empty set is accepted on a PageKind.User
        // (blog) page and round-trips (the field is the label surface; the
        // User lane permits it).
        var userPage = new Page
        {
            Slug = "f5-user", Title = "T", Body = "b", Kind = PageKind.User, TagIds = tagIds,
        };
        await using (var session3 = store.OpenSession(new SessionOptions()))
        {
            var saved = await svc.CreateAsync(
                userPage, "u-author", RolesSet(Roles.Member), session3);
            Assert.Equal(PageKind.User, saved.Kind);
            Assert.NotNull(saved.TagIds);
            Assert.Equal(tagIds, saved.TagIds);
        }
        await using (var q3 = store.QuerySession())
        {
            var reloaded = await q3.Query<Page>()
                .Where(p => p.Slug == "f5-user")
                .FirstAsync(ct);
            Assert.NotNull(reloaded);
            Assert.Equal(tagIds, reloaded!.TagIds);
        }
    }

    // ─── F11 — the ADR 0004 §B.1 additive no-reseed pin ─────────────────
    //
    // Plant a post with the **default-empty** TagIds, close the store, and
    // re-open a **fresh** DocumentStore against the same scratch DB (the
    // warm-reboot simulation). The post's TagIds reads back **empty** —
    // the additive field is delta-detected, idempotent, and no-reseed:
    // a warm re-boot does not backfill or re-seed a value onto a
    // pre-existing document.

    [Fact]
    public async Task F11_PreExistingPostTagIdsReadBackEmptyAfterReboot()
    {
        var connString = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = BootStore(connString);
        var ct = TestContext.Current.CancellationToken;

        // Plant a post with the default-empty TagIds (the ADR 0004 §B.1
        // additive field's default) on a first boot.
        var post = new Post
        {
            Id = "f11-post",
            ComponentId = "comp-f11",
            AuthorId = "u-f11",
            Body = "a pre-existing post planted before the TG field existed",
            Audience = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, "u-f11")]),
            Created = DateTimeOffset.UtcNow,
        };
        Assert.Empty(post.TagIds);
        await Plant(store, post);

        // Warm re-boot: dispose the first store and open a **fresh**
        // DocumentStore against the **same** scratch DB (the apply-changes
        // is idempotent — no re-seed, no backfill).
        await store.DisposeAsync();
        var store2 = BootStore(connString);
        await using var q = store2.QuerySession();
        var reloaded = await q.LoadAsync<Post>("f11-post", ct);
        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded!.TagIds);
        Assert.Empty(reloaded.TagIds);
    }

    // ─── F12 — no seeding (C-TG·7) ───────────────────────────────────────
    //
    // A freshly booted store has **zero** Tag rows and zero
    // TagTranslation rows — there is no seeder, no "suggested tags" pack,
    // no seed row (C-TG·7, D8).

    [Fact]
    public async Task F12_FreshInstanceHasZeroTags()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        await using var s = store.QuerySession();
        Assert.Equal(0, await s.Query<Tag>().CountAsync(ct));
        Assert.Equal(0, await s.Query<TagTranslation>().CountAsync(ct));
    }

    // ─── Shared helpers ─────────────────────────────────────────────────────

    /// <summary>Boot a fresh scratch store (M1 + M3 + Page + **Tag** doc
    /// types) — the <see cref="PageServiceTests.BootStoreAsync"/> shape with
    /// the U3 <c>TagDocTypes.Configure(opts)</c> line added (the test boot
    /// path is the one place the Tags doc types must be registered here
    /// before <c>TagService</c> lands, per the U3 handoff note).</summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = BootStore(conn);
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>Build (but do **not** apply changes to) a scratch store —
    /// the shape the F11 warm-reboot test re-opens against the same DB.</summary>
    private static IDocumentStore BootStore(string connString)
    {
        return DocumentStore.For(opts =>
        {
            opts.Connection(connString);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            PageDocTypes.Configure(opts);
            TagDocTypes.Configure(opts);   // U3 — the two new Tag/TagTranslation docs
        });
    }

    /// <summary>Plant a document row directly (test-fixture seeding, not a
    /// service write seam — the <see cref="PageServiceTests.Plant"/> shape,
    /// reused).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    private static HashSet<string> RolesSet(params string[] roles) => roles.ToHashSet();
}
