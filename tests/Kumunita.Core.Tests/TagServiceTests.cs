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

    // ─── U5 — the TagService write lane (attach / create / translate) ──────
    //
    // U5 lands the six write-lane members + the Slug derivation (design doc
    // §2.1, §2.3 rows 1/2/3/5, §2.4 tests 1/2/3/10/11/12/18/19/20/23). The
    // standing split: attach = the object's existing edit standing
    // (author ∪ GlobalAdmin — ADR 0014/0016 posts, ADR 0040 pages; the ADR
    // 0013 group lane has no component-moderator standing, ADR 0007);
    // translate = the tag's CreatedBy ∪ GlobalAdmin only (C-TG·5, D4). The
    // ADR 0006-D lane pin: the tag lane composes only the frozen seams and
    // writes only AccessVia.Owner / AccessVia.Admin audit rows.

    // ─── F1 — free attach on own post (C-TG·9, C-TG·4) ────────────────────

    [Fact]
    public async Task F1_AttachFreeOnOwnPost()
    {
        var store = await BootStoreAsync();
        var svc = new TagService(store);
        var ct = TestContext.Current.CancellationToken;

        await Plant(store, NewPost("f1-post", "u-author"));

        await using var session = store.OpenSession(new SessionOptions());
        var tags = await svc.AttachToPostAsync(
            "f1-post", ["sanitation"], "u-author", RolesSet(Roles.Member), session);

        Assert.Single(tags);
        Assert.Equal("sanitation", tags[0].Slug);
        Assert.Equal("sanitation", tags[0].Name);
        Assert.Equal("u-author", tags[0].CreatedBy);

        // F1: one tag.attach + one tag.create audit row (the C-TG·9 two-row
        // shape — a new Slug creates the tag, then attaches it).
        var attach = await AuditsFor(store, "tag.attach");
        Assert.Single(attach);
        var create = await AuditsFor(store, "tag.create");
        Assert.Single(create);
    }

    // ─── F2 — same-Slug reuse (C-TG·4, C-TG·5) ─────────────────────────────

    [Fact]
    public async Task F2_SameSlugSecondAuthorReusesTag()
    {
        var store = await BootStoreAsync();
        var svc = new TagService(store);
        var ct = TestContext.Current.CancellationToken;

        await Plant(store, NewPost("f2-post-a", "u-a"));
        await Plant(store, NewPost("f2-post-b", "u-b"));

        // Author A creates the tag.
        await using (var s1 = store.OpenSession(new SessionOptions()))
            await svc.AttachToPostAsync("f2-post-a", ["sanitation"], "u-a", RolesSet(Roles.Member), s1);

        // Author B attaches the **same** Slug to their own post — the Tag
        // doc is reused, not duplicated (F2, C-TG·4).
        await using (var s2 = store.OpenSession(new SessionOptions()))
            await svc.AttachToPostAsync("f2-post-b", ["sanitation"], "u-b", RolesSet(Roles.Member), s2);

        // Exactly one Tag row (reused, not two).
        await using var q = store.QuerySession();
        var all = await q.Query<Tag>().ToListAsync(ct);
        Assert.Single(all);
        Assert.Equal("sanitation", all[0].Slug);
    }

    [Fact]
    public async Task F2_SameSlugSecondAuthorNotCreatedBy()
    {
        var store = await BootStoreAsync();
        var svc = new TagService(store);
        var ct = TestContext.Current.CancellationToken;

        await Plant(store, NewPost("f2b-post-a", "u-a"));
        await Plant(store, NewPost("f2b-post-b", "u-b"));

        await using (var s1 = store.OpenSession(new SessionOptions()))
            await svc.AttachToPostAsync("f2b-post-a", ["sanitation"], "u-a", RolesSet(Roles.Member), s1);
        await using (var s2 = store.OpenSession(new SessionOptions()))
            await svc.AttachToPostAsync("f2b-post-b", ["sanitation"], "u-b", RolesSet(Roles.Member), s2);

        // The **first** author is CreatedBy; the **second** (mere attacher)
        // is not (F2, C-TG·5 — the name is the creator's artifact).
        await using var q = store.QuerySession();
        var tag = await q.Query<Tag>().Where(t => t.Slug == "sanitation").FirstAsync(ct);
        Assert.Equal("u-a", tag.CreatedBy);
        Assert.NotEqual("u-b", tag.CreatedBy);
    }

    // ─── F6 — the creator sets a translation (C-TG·5, C-TG·9) ─────────────

    [Fact]
    public async Task F6_CreatorSetsTranslation()
    {
        var store = await BootStoreAsync();
        var svc = new TagService(store);
        var ct = TestContext.Current.CancellationToken;

        await Plant(store, NewPost("f6-post", "u-creator"));
        await using (var s1 = store.OpenSession(new SessionOptions()))
            await svc.AttachToPostAsync("f6-post", ["hygiene"], "u-creator", RolesSet(Roles.Member), s1);

        await using var q = store.QuerySession();
        var tagId = (await q.Query<Tag>().Where(t => t.Slug == "hygiene").FirstAsync(ct)).Id;

        // The creator sets the de translation — one tagtranslation.add row,
        // Via = Owner (F6, C-TG·5, C-TG·9).
        await using (var s2 = store.OpenSession(new SessionOptions()))
        {
            var tr = await svc.AddTagTranslationAsync(
                tagId, "de", "Hygiene", "u-creator", RolesSet(Roles.Member), s2);
            Assert.Equal("Hygiene", tr.Name);
            Assert.Equal("u-creator", tr.AuthorId);
        }

        var audit = await AuditsFor(store, "tagtranslation.add");
        Assert.Single(audit);
        Assert.Equal(AccessVia.Owner, audit[0].Via);
    }

    // ─── F7 — a non-creator attacher cannot reword (C-TG·5) ────────────────

    [Fact]
    public async Task F7_NonCreatorAttacherCannotReword()
    {
        var store = await BootStoreAsync();
        var svc = new TagService(store);
        var ct = TestContext.Current.CancellationToken;

        await Plant(store, NewPost("f7-post-a", "u-creator"));
        await Plant(store, NewPost("f7-post-b", "u-attacher"));

        // Creator creates the tag.
        await using (var s1 = store.OpenSession(new SessionOptions()))
            await svc.AttachToPostAsync("f7-post-a", ["sanitation"], "u-creator", RolesSet(Roles.Member), s1);
        // Attacher (not creator) reuses it.
        await using (var s2 = store.OpenSession(new SessionOptions()))
            await svc.AttachToPostAsync("f7-post-b", ["sanitation"], "u-attacher", RolesSet(Roles.Member), s2);

        await using var q = store.QuerySession();
        var tagId = (await q.Query<Tag>().Where(t => t.Slug == "sanitation").FirstAsync(ct)).Id;

        // The attacher cannot reword the tag's translation (F7, C-TG·5 — the
        // name is the creator's artifact; a mere attacher gains no standing).
        await using (var s3 = store.OpenSession(new SessionOptions()))
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                svc.AddTagTranslationAsync(
                    tagId, "de", "Sauberkeit", "u-attacher", RolesSet(Roles.Member), s3));
        }
    }

    // ─── F8 — a GlobalAdmin rewords any tag (C-TG·5 break-glass, C-TG·9) ──

    [Fact]
    public async Task F8_GlobalAdminRewordsAnyTag()
    {
        var store = await BootStoreAsync();
        var svc = new TagService(store);
        var ct = TestContext.Current.CancellationToken;

        await Plant(store, NewPost("f8-post", "u-creator"));
        await using (var s1 = store.OpenSession(new SessionOptions()))
            await svc.AttachToPostAsync("f8-post", ["sanitation"], "u-creator", RolesSet(Roles.Member), s1);

        await using var q = store.QuerySession();
        var tagId = (await q.Query<Tag>().Where(t => t.Slug == "sanitation").FirstAsync(ct)).Id;

        // A GlobalAdmin (not the creator) rewords the tag (F8, C-TG·5
        // break-glass) — one tagtranslation.add row, Via = Admin.
        await using (var s2 = store.OpenSession(new SessionOptions()))
        {
            var tr = await svc.AddTagTranslationAsync(
                tagId, "fr", "Assainissement", "u-admin", RolesSet(Roles.GlobalAdmin), s2);
            Assert.Equal("Assainissement", tr.Name);
            Assert.Equal("u-admin", tr.AuthorId);
        }

        var audit = await AuditsFor(store, "tagtranslation.add");
        Assert.Single(audit);
        Assert.Equal(AccessVia.Admin, audit[0].Via);
    }

    // ─── Audit-row shape tests (C-TG·9, D7) ────────────────────────────────

    [Fact]
    public async Task Attach_WritesOneAuditRow_tag_attach()
    {
        var store = await BootStoreAsync();
        var svc = new TagService(store);
        var ct = TestContext.Current.CancellationToken;

        // Plant an existing tag so **only** tag.attach (no tag.create) is
        // written — isolates the attach row shape.
        await Plant(store, new Tag
        {
            Id = "tag-attach-seed", Slug = "budget", Name = "budget",
            LanguageCode = "en", CreatedBy = "u-seed", Created = DateTimeOffset.UtcNow,
        });
        await Plant(store, NewPost("attach-post", "u-author"));

        await using var session = store.OpenSession(new SessionOptions());
        await svc.AttachToPostAsync(
            "attach-post", ["budget"], "u-author", RolesSet(Roles.Member), session);

        var attach = await AuditsFor(store, "tag.attach");
        Assert.Single(attach);
        Assert.Equal("tag.attach", attach[0].Action);
        Assert.Equal("post", attach[0].TargetKind);
        Assert.Equal("attach-post", attach[0].TargetId);
        Assert.Equal(AccessVia.Owner, attach[0].Via);
        Assert.Equal(AccessOutcome.Allow, attach[0].Outcome);
        Assert.Equal("u-author", attach[0].ActorId);
        // No tag.create row (the tag already existed).
        Assert.Empty(await AuditsFor(store, "tag.create"));
    }

    [Fact]
    public async Task Create_WritesOneAuditRow_tag_create()
    {
        var store = await BootStoreAsync();
        var svc = new TagService(store);
        var ct = TestContext.Current.CancellationToken;

        await Plant(store, NewPost("create-post", "u-author"));

        // A new Slug creates the tag — one tag.create row.
        await using var session = store.OpenSession(new SessionOptions());
        await svc.AttachToPostAsync(
            "create-post", ["newtag"], "u-author", RolesSet(Roles.Member), session);

        var create = await AuditsFor(store, "tag.create");
        Assert.Single(create);
        Assert.Equal("tag.create", create[0].Action);
        Assert.Equal("tag", create[0].TargetKind);
        Assert.NotNull(create[0].TargetId);
        Assert.Equal(AccessVia.Owner, create[0].Via);
        Assert.Equal(AccessOutcome.Allow, create[0].Outcome);
        Assert.Equal("u-author", create[0].ActorId);
    }

    [Fact]
    public async Task Translate_WritesOneAuditRow_tagtranslation_add()
    {
        var store = await BootStoreAsync();
        var svc = new TagService(store);
        var ct = TestContext.Current.CancellationToken;

        await Plant(store, NewPost("translate-post", "u-creator"));
        await using (var s1 = store.OpenSession(new SessionOptions()))
            await svc.AttachToPostAsync("translate-post", ["hygiene"], "u-creator", RolesSet(Roles.Member), s1);

        await using var q = store.QuerySession();
        var tagId = (await q.Query<Tag>().Where(t => t.Slug == "hygiene").FirstAsync(ct)).Id;

        await using (var s2 = store.OpenSession(new SessionOptions()))
            await svc.AddTagTranslationAsync(
                tagId, "de", "Hygiene", "u-creator", RolesSet(Roles.Member), s2);

        var audit = await AuditsFor(store, "tagtranslation.add");
        Assert.Single(audit);
        Assert.Equal("tagtranslation.add", audit[0].Action);
        Assert.Equal("tag", audit[0].TargetKind);
        Assert.Equal(tagId, audit[0].TargetId);
        Assert.Equal(AccessVia.Owner, audit[0].Via);
        Assert.Equal(AccessOutcome.Allow, audit[0].Outcome);
        Assert.Equal("u-creator", audit[0].ActorId);
    }

    // ─── Slug derivation (C-TG·4, D3) ──────────────────────────────────────

    [Fact]
    public async Task Slug_Derivation_Lowercase_Trim_Charset()
    {
        var store = await BootStoreAsync();
        var svc = new TagService(store);
        var ct = TestContext.Current.CancellationToken;

        await Plant(store, NewPost("slug-post", "u-author"));

        // "  Sanitation  " → "sanitation" (lowercase + trim).
        await using (var s1 = store.OpenSession(new SessionOptions()))
        {
            var tags = await svc.AttachToPostAsync(
                "slug-post", ["  Sanitation  "], "u-author", RolesSet(Roles.Member), s1);
            Assert.Single(tags);
            Assert.Equal("sanitation", tags[0].Slug);
        }

        // "  Hygiène  " → "hygiène" (a **different** slug, D3 — no
        // accent-folding, no cross-language merge).
        await using (var s2 = store.OpenSession(new SessionOptions()))
        {
            var tags = await svc.AttachToPostAsync(
                "slug-post", ["sanitation", "  Hygiène  "], "u-author", RolesSet(Roles.Member), s2);
            var hy = tags.First(t => t.Slug == "hygiène");
            Assert.Equal("hygiène", hy.Slug);
        }

        // Two distinct tags: "sanitation" and "hygiène".
        await using var q = store.QuerySession();
        var slugs = await q.Query<Tag>().Select(t => t.Slug).ToListAsync(ct);
        Assert.Contains("sanitation", slugs);
        Assert.Contains("hygiène", slugs);
        Assert.NotEqual("sanitation", "hygiène");

        // Invalid charset (a space + a non-letter "!") → ArgumentException.
        await using (var s3 = store.OpenSession(new SessionOptions()))
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.AttachToPostAsync(
                    "slug-post", ["bad slug!"], "u-author", RolesSet(Roles.Member), s3));
        }
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

    /// <summary>The U5 write-lane test's <see cref="Post"/> fixture (a live
    /// community post, audience = the author — the minimum shape the
    /// <see cref="TagService.AttachToPostAsync"/> write lane reads:
    /// <c>AuthorId</c> / <c>LanguageCode</c> / <c>TagIds</c>).</summary>
    private static Post NewPost(string id, string author) => new()
    {
        Id = id,
        ComponentId = "tg-comp",
        AuthorId = author,
        Body = "a post planted for the TG write-lane tests",
        Audience = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, author)]),
        LanguageCode = "en",
        Created = DateTimeOffset.UtcNow,
    };

    /// <summary>All <see cref="AccessAudit"/> rows with the given
    /// <c>Action</c> (the <see cref="PostTranslationTests.AuditsFor"/> shape,
    /// reused) — the U5 audit-row tests assert their shape against this.</summary>
    private static async Task<IReadOnlyList<AccessAudit>> AuditsFor(IDocumentStore store, string action)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>().Where(a => a.Action == action).ToListAsync(ct);
    }

    private static HashSet<string> RolesSet(params string[] roles) => roles.ToHashSet();
}
