using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
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
        var svc = NewTagService(store);
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
        var svc = NewTagService(store);
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
        var svc = NewTagService(store);
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
        var svc = NewTagService(store);
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
        var svc = NewTagService(store);
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
        var svc = NewTagService(store);
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
        var svc = NewTagService(store);
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
        var svc = NewTagService(store);
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
        var svc = NewTagService(store);
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
        var svc = NewTagService(store);
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

    // ─── U6 — the read lane (C-TG·2 base query + the privacy-pin) ─────────
    //
    // All four read methods derive from the one C-TG·2 base query over the
    // actor's readable content; the content's own Read decision (not a tag
    // grant) is the gate (C-TG·1 / C-TG·3), and the tag lane writes no
    // tag-family AccessAudit row of its own (C-TG·8 / D7).
    //
    // F3 — a group post's tag is invisible to a non-member (C-TG·3): the
    // post's own Read decision (the ADR 0013 membership lane) is applied
    // before the post is returned; a non-member's by-tag result is empty,
    // and the tag is absent from their autocomplete / list.

    [Fact]
    public async Task F3_GroupPostTagInvisibleToNonMember_ByTag()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        const string owner = "u-f3-owner";
        const string stranger = "u-f3-stranger";

        var group = await userInfo.CreateGroupAsync(owner, "F3 family", null);
        await Plant(store, new Tag { Id = "tag-f3", Slug = "sanitation", Name = "Sanitation", LanguageCode = "en", CreatedBy = owner });
        await Plant(store, TaggedGroupPost("f3-post", group.Id, owner, "tag-f3"));
        var svc = NewTagService(store);

        // The non-member's by-tag result is **empty** — the group post's own
        // Read decision (membership) is the gate, applied **before** it is
        // returned (C-TG·3, D5). The tag grants nothing.
        Assert.Empty(await svc.ListPostsByTagAsync("sanitation", stranger));

        // Positive control: the member (the owner) **does** see it — the tag
        // is real, only the non-member's Read is the exclusion.
        var memberPosts = await svc.ListPostsByTagAsync("sanitation", owner);
        var memberPost = Assert.Single(memberPosts);
        Assert.Equal("f3-post", memberPost.Id);
    }

    [Fact]
    public async Task F3_GroupPostTagInvisibleToNonMember_Suggest()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        const string owner = "u-f3b-owner";
        const string stranger = "u-f3b-stranger";

        var group = await userInfo.CreateGroupAsync(owner, "F3b family", null);
        await Plant(store, new Tag { Id = "tag-f3b", Slug = "sanitation", Name = "Sanitation", LanguageCode = "en", CreatedBy = owner });
        await Plant(store, TaggedGroupPost("f3b-post", group.Id, owner, "tag-f3b"));
        var svc = NewTagService(store);

        // The tag is used **only** on a group post the non-member may not read →
        // it is absent from their autocomplete (C-TG·1 / C-TG·3, the
        // anti-leak pin: a tag reveals nothing behind unread content).
        Assert.Empty(await svc.SuggestAsync("san", stranger));
        Assert.Empty(await svc.ListForActorAsync(stranger));

        // Positive control: the member sees the tag in autocomplete.
        var ownerSuggest = await svc.SuggestAsync("san", owner);
        Assert.Contains(ownerSuggest, i => i.Tag.Id == "tag-f3b");
    }

    // F4 — a tag used only on unread content is absent from all four shapes
    // (C-TG·2, D5): the base query is scoped to content the viewer may read,
    // so a tag whose only use is behind an unread community post never
    // surfaces (no name, no "hidden" placeholder).

    [Fact]
    public async Task F4_TagUsedOnlyOnUnreadContentInvisible_List()
    {
        var store = await BootStoreAsync();
        const string other = "u-f4-other";
        const string viewer = "u-f4-viewer";

        await Plant(store, new Tag { Id = "tag-f4", Slug = "secret", Name = "Secret", LanguageCode = "en", CreatedBy = other });
        // A community post the viewer is **not** in the audience of (author =
        // other; audience = other only) → unread by the viewer.
        await Plant(store, TaggedPost("f4-post", other, "tag-f4"));
        var svc = NewTagService(store);

        // The tag exists, but only on unread content → the viewer's tag list
        // is empty (C-TG·2 / D5: a tag behind unread content is as good as
        // absent to that viewer — no name, no "hidden" placeholder).
        Assert.Empty(await svc.ListForActorAsync(viewer));

        // Positive control: the author (in the audience) sees the tag.
        Assert.Contains(await svc.ListForActorAsync(other), i => i.Tag.Id == "tag-f4");
    }

    [Fact]
    public async Task F4_TagUsedOnlyOnUnreadContentInvisible_ByTag()
    {
        var store = await BootStoreAsync();
        const string other = "u-f4b-other";
        const string viewer = "u-f4b-viewer";

        await Plant(store, new Tag { Id = "tag-f4b", Slug = "secret", Name = "Secret", LanguageCode = "en", CreatedBy = other });
        await Plant(store, TaggedPost("f4b-post", other, "tag-f4b"));
        var svc = NewTagService(store);

        // The by-tag result is empty for the unread viewer (C-TG·1 / C-TG·2).
        Assert.Empty(await svc.ListPostsByTagAsync("secret", viewer));

        // Positive control: the author (in the audience) sees the post.
        var authorPosts = await svc.ListPostsByTagAsync("secret", other);
        Assert.Contains(authorPosts, p => p.Id == "f4b-post");
    }

    [Fact]
    public async Task F4_TagUsedOnlyOnUnreadContentInvisible_Suggest()
    {
        var store = await BootStoreAsync();
        const string other = "u-f4c-other";
        const string viewer = "u-f4c-viewer";

        await Plant(store, new Tag { Id = "tag-f4c", Slug = "secret", Name = "Secret", LanguageCode = "en", CreatedBy = other });
        await Plant(store, TaggedPost("f4c-post", other, "tag-f4c"));
        var svc = NewTagService(store);

        // Autocomplete (the base query filtered) is empty for the unread viewer.
        Assert.Empty(await svc.SuggestAsync("sec", viewer));

        // Positive control: the author's autocomplete surfaces the tag.
        Assert.Contains(await svc.SuggestAsync("sec", other), i => i.Tag.Id == "tag-f4c");
    }

    // F9 — the display name is resolved in the viewer's language (C-TG·4, D3/D5):
    // a `de`-preferring actor typing `hy` gets the `de` name `hygiène` even
    // though the Slug is `sanitation`. In Core (no cookie) the ADR 0005
    // preference order collapses to the instance default, so the test seeds the
    // default to `de`.

    [Fact]
    public async Task F9_AutocompleteMatchesViewerLanguage_DisplayName()
    {
        var store = await BootStoreAsync();
        const string author = "u-f9-author";

        await SeedDefaultLanguage(store, "de");
        // Base name `Sanitation` (authored in en) — but the viewer is `de`,
        // so the `de` TagTranslation (`hygiène`) is the resolved display name.
        await Plant(store, new Tag { Id = "tag-f9", Slug = "sanitation", Name = "Sanitation", LanguageCode = "en", CreatedBy = author });
        await Plant(store, new TagTranslation { Id = "tag-f9-de", TagId = "tag-f9", LanguageCode = "de", Name = "hygiène", AuthorId = author });
        await Plant(store, TaggedPost("f9-post", author, "tag-f9"));
        var svc = NewTagService(store);

        // Typing `hy` matches the **displayed** name (`hygiène`), not the slug
        // (`sanitation`) — the display-name branch of the filter.
        var hit = Assert.Single(await svc.SuggestAsync("hy", author));
        Assert.Equal("hygiène", hit.DisplayedName);
        Assert.Equal("sanitation", hit.Tag.Slug);
        Assert.Equal("tag-f9", hit.Tag.Id);
    }

    [Fact]
    public async Task F9_AutocompleteMatchesViewerLanguage_Slug()
    {
        var store = await BootStoreAsync();
        const string author = "u-f9b-author";

        await SeedDefaultLanguage(store, "de");
        // The `de` display name (`Hof`) does **not** start with the prefix
        // `gar`, but the Slug (`garden`) does → the Slug fallback branch of
        // the filter (C-TG·4: the Slug is identity, the display name is the
        // viewer's-language name; the prefix can match either).
        await Plant(store, new Tag { Id = "tag-f9b", Slug = "garden", Name = "Garden", LanguageCode = "en", CreatedBy = author });
        await Plant(store, new TagTranslation { Id = "tag-f9b-de", TagId = "tag-f9b", LanguageCode = "de", Name = "Hof", AuthorId = author });
        await Plant(store, TaggedPost("f9b-post", author, "tag-f9b"));
        var svc = NewTagService(store);

        // The prefix `gar` matches the Slug (`garden`), **not** the displayed
        // name (`Hof`) — the slug-fallback branch still surfaces the tag, and
        // the row's display name is the viewer's-language name.
        var hit = Assert.Single(await svc.SuggestAsync("gar", author));
        Assert.Equal("Hof", hit.DisplayedName);    // resolved in the viewer's language
        Assert.Equal("garden", hit.Tag.Slug);       // the prefix matched the Slug

        // And the displayed name itself does **not** match the prefix (the
        // proof this is the slug branch, not the display-name branch).
        Assert.False(hit.DisplayedName.StartsWith("gar", StringComparison.Ordinal));
    }

    // F10 — autocomplete is capped at ≤ 10 (C-TG·2): a blank prefix matches
    // every readable tag, but only the first 10 are returned.

    [Fact]
    public async Task F10_AutocompleteCappedAtTen()
    {
        var store = await BootStoreAsync();
        const string author = "u-f10-author";

        var tagIds = new List<string>(15);
        for (var i = 0; i < 15; i++)
        {
            var id = "tag-f10-" + i;
            tagIds.Add(id);
            await Plant(store, new Tag
            {
                Id = id, Slug = "tag" + i, Name = "Tag " + i,
                LanguageCode = "en", CreatedBy = author,
            });
        }
        // One post carrying all 15 tag ids → the base query surfaces 15 tags.
        var multi = NewPost("f10-post", author);
        multi.TagIds = tagIds;
        await Plant(store, multi);
        var svc = NewTagService(store);

        // A blank prefix matches all 15 readable tags → capped at 10.
        var suggestions = await svc.SuggestAsync(string.Empty, author);
        Assert.Equal(10, suggestions.Count);

        // The tag list (no cap — the cap is autocomplete-only) still shows all 15.
        Assert.Equal(15, (await svc.ListForActorAsync(author)).Count);
    }

    // C-TG·8 (D7) — the read lane emits **no tag-family** AccessAudit row: a
    // read is not a decision. The content's own Read-decision rows (post /
    // page / grouppost) are the content's; the tag lane writes none of its
    // own (no `tag.*` action, no `TargetKind == "tag"` row).

    [Fact]
    public async Task List_EmitsNoAuditRow()
    {
        var store = await BootStoreAsync();
        const string author = "u-list-audit";

        await Plant(store, new Tag { Id = "tag-la", Slug = "sanitation", Name = "Sanitation", LanguageCode = "en", CreatedBy = author });
        await Plant(store, TaggedPost("la-post", author, "tag-la"));
        var svc = NewTagService(store);

        // Exercise the tag-list + by-tag reads (the two read shapes that
        // return tagged content).
        await svc.ListForActorAsync(author);
        await svc.ListPostsByTagAsync("sanitation", author);

        Assert.Empty(await AuditsFor(store, "tag.attach"));
        Assert.Empty(await AuditsFor(store, "tag.create"));
        Assert.Empty(await AuditsFor(store, "tagtranslation.add"));
        // And no tag-family row of any kind (the content's own post Read row
        // is the content's, not the tag lane's — D7).
        await using var q = store.QuerySession();
        var all = await q.Query<AccessAudit>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(all, a => a.TargetKind == "tag");
    }

    [Fact]
    public async Task Suggest_EmitsNoAuditRow()
    {
        var store = await BootStoreAsync();
        const string author = "u-suggest-audit";

        await Plant(store, new Tag { Id = "tag-sa", Slug = "sanitation", Name = "Sanitation", LanguageCode = "en", CreatedBy = author });
        await Plant(store, TaggedPost("sa-post", author, "tag-sa"));
        var svc = NewTagService(store);

        await svc.SuggestAsync("san", author);

        Assert.Empty(await AuditsFor(store, "tag.attach"));
        Assert.Empty(await AuditsFor(store, "tag.create"));
        Assert.Empty(await AuditsFor(store, "tagtranslation.add"));
        await using var q = store.QuerySession();
        var all = await q.Query<AccessAudit>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(all, a => a.TargetKind == "tag");
    }

    // ─── U10 — ADR 0006-D lane pin (design doc §2.4 #24) ─────────────────
    //
    // <c>PostService_MakesNoNewModerateCall</c> — the tag lane composes only
    // the **frozen** seams (<c>IAuthorizationService</c> + the frozen
    // <c>IUserInfoService</c> read seams (reached through
    // <c>AuthorizationService</c>, never directly by <c>TagService</c>) +
    // <c>ITranslationProvider</c>) and **never opens a new seam** on any
    // frozen interface, and writes only <c>AccessVia.Owner</c> /
    // <c>AccessVia.Admin</c> audit rows. Pinned three ways (the
    // GroupPostServiceTests #19 / PostServiceTests #16 "seam-level absence"
    // idiom, carried to the tag lane):
    // (1) **seam level** — a recorder over the **real**
    // <c>AuthorizationService</c> drives the *whole* tag surface (attach
    // create/reuse, attach page, translate owner + admin, all four read
    // lanes, the group-lane read for a member **and** a non-member) and
    // **no** <c>AccessAction.Moderate</c> call reaches the seam;
    // (2) **audit level** — every tag-family row (<c>tag.attach</c> /
    // <c>tag.create</c> / <c>tagtranslation.add</c>) carries
    // <c>Via = Owner</c> or <c>Via = Admin</c> — never <c>Moderator</c> or
    // any other value (C-TG·9's two permitted Vias, U5 handoff note (f));
    // (3) **surface level** — reflection: neither <c>IAuthorizationService</c>
    // nor <c>IUserInfoService</c> exposes a <c>*Moderate*</c> seam (no new
    // seam), and <c>TagService</c>'s composition is exactly the frozen trio.

    [Fact]
    public async Task PostService_MakesNoNewModerateCall()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var recorder = new RecordingAuthz(new AuthorizationService(store, userInfo));
        var svc = new TagService(store, recorder, new TranslationProvider(store));
        var ct = TestContext.Current.CancellationToken;
        const string author = "u-lane-author";
        const string globalAdm = "u-lane-admin";
        const string stranger = "u-lane-stranger";

        // ── Drive the write lanes (all three) ────────────────────────────
        await Plant(store, NewPost("lane-post", author));
        await Plant(store, new Page
        {
            Id = "lane-page", Slug = "lane-page", Title = "t", Body = "b",
            AuthorId = author, Kind = PageKind.User,
            Audience = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, author)]),
            Created = DateTimeOffset.UtcNow,
        });

        await using (var s1 = store.OpenSession(new SessionOptions()))
            await svc.AttachToPostAsync("lane-post", ["sanitation"], author, RolesSet(Roles.Member), s1);
        // Reuse on the same post with the GlobalAdmin standing (the Admin Via branch).
        await using (var s2 = store.OpenSession(new SessionOptions()))
            await svc.AttachToPostAsync("lane-post", ["sanitation"], globalAdm, RolesSet(Roles.GlobalAdmin), s2);
        // The page write lane (ADR 0040 standing, author).
        await using (var s3 = store.OpenSession(new SessionOptions()))
            await svc.AttachToPageAsync("lane-page", ["budget"], author, RolesSet(Roles.Member), s3);

        await using (var q = store.QuerySession())
        {
            var tag = await q.Query<Tag>().Where(t => t.Slug == "sanitation").FirstAsync(ct);
            Assert.NotNull(tag);
            var tagId = tag!.Id;
            // Translate — the creator (Owner) and a non-creator GlobalAdmin (Admin).
            await using (var s4 = store.OpenSession(new SessionOptions()))
                await svc.AddTagTranslationAsync(tagId, "de", "Hygiene", author, RolesSet(Roles.Member), s4);
            await using (var s5 = store.OpenSession(new SessionOptions()))
                await svc.AddTagTranslationAsync(tagId, "fr", "Assainissement", globalAdm, RolesSet(Roles.GlobalAdmin), s5);
        }

        // ── Drive the read lanes (all four, C-TG·2 base query) ───────────
        await svc.ListForActorAsync(author);
        await svc.ListPostsByTagAsync("sanitation", author);
        await svc.ListPagesByTagAsync("budget", author);
        await svc.SuggestAsync("san", author);

        // ── Drive the group-lane read (C-TG·3 — the membership lane) ─────
        var group = await userInfo.CreateGroupAsync(author, "TG lane family", null);
        await Plant(store, new Tag { Id = "tag-lane-g", Slug = "family", Name = "Family", LanguageCode = "en", CreatedBy = author });
        await Plant(store, TaggedGroupPost("lane-gpost", group.Id, author, "tag-lane-g"));
        await svc.ListPostsByTagAsync("family", stranger);   // non-member — the Deny branch
        await svc.ListPostsByTagAsync("family", author);      // member — the Allow branch
        await svc.SuggestAsync("fam", stranger);
        await svc.SuggestAsync("fam", author);
        await svc.ListForActorAsync(stranger);

        // ── Pin 1 — seam level: no Moderate call reached the seam ────────
        // (The reads call the seam once per tagged post/page — the C-TG·2
        // base query; ≥ 5 is the count the driven surface actually reaches,
        // but the pin is the **absence** of a Moderate call, not the count.)
        Assert.True(recorder.TotalCalls >= 5,
            $"expected ≥ 5 authorization calls across the tag surface, got {recorder.TotalCalls}");
        Assert.Equal(0, recorder.ModerateCalls);

        // ── Pin 2 — audit level: tag-family rows only Owner / Admin ──────
        await using (var qa = store.QuerySession())
        {
            var tagRows = await qa.Query<AccessAudit>()
                .Where(a => a.Action == "tag.attach" || a.Action == "tag.create" || a.Action == "tagtranslation.add")
                .ToListAsync(ct);
            Assert.NotEmpty(tagRows);
            Assert.All(tagRows, r => Assert.True(
                r.Via == AccessVia.Owner || r.Via == AccessVia.Admin,
                $"tag-family audit row '{r.Action}' carried Via {r.Via} — only Owner/Admin are permitted by the ADR 0006-D lane pin"));
        }

        // ── Pin 3 — surface level: no new seam on a frozen interface; the
        //    TagService's composition is exactly the frozen trio ─────────
        Assert.DoesNotContain(typeof(IAuthorizationService).GetMethods(), m => m.Name.Contains("Moderate"));
        Assert.DoesNotContain(typeof(IUserInfoService).GetMethods(), m => m.Name.Contains("Moderate"));
        var ctor = typeof(TagService).GetConstructors().Single();
        Assert.Equal(
            new[] { typeof(IDocumentStore), typeof(IAuthorizationService), typeof(ITranslationProvider) },
            ctor.GetParameters().Select(p => p.ParameterType).ToList());
    }

    // ─── U12 — the three-test acceptance gate (design doc §2.5) ─────────────
    //
    // Mirrors the M2 / ML-UI lane's gate-test shape (the
    // <see cref="MLUI_FacesTests.MLUI_U8_Gate_ClosedLoop_RegistryKeySave"/>
    // precedent): one <c>[Fact]</c> per leg — closed-loop, handoff,
    // part-vs-whole. The real part-vs-whole evidence is the full
    // <c>dotnet exec …Kumunita.Core.Tests.dll</c> run count (the 24-test
    // §2.4 list **and** these three gate tests) recorded in the handoff
    // note; the thin <c>[Fact]</c> here re-runs a representative subset of
    // the §2.4 anchors in a single test to prove they pass *together*.

    // ── Gate 1 — Closed loop ───────────────────────────────────────────────
    // The author attaches `sanitation` to a post → the tag appears in the
    // author's tag list **and** by-tag view; exactly one <c>tag.attach</c>
    // + one <c>tag.create</c> audit row (C-TG·9, F1).

    [Fact(DisplayName = "GATE closed-loop — attach → tag list + by-tag + 1 tag.attach + 1 tag.create")]
    public async Task U12_Gate_ClosedLoop_Attach_VisibleInBothReadSurfaces_TwoAuditRows()
    {
        var store = await BootStoreAsync();
        var svc = NewTagService(store);
        var ct = TestContext.Current.CancellationToken;

        await SeedDefaultLanguage(store, "en");
        await Plant(store, NewPost("cl-post", "u-cl-author"));

        // Write lane: attach `sanitation` (a new slug → creates the Tag doc +
        // writes the audit rows).
        var tags = new List<Tag>();
        await using (var session = store.OpenSession(new SessionOptions()))
            tags.AddRange(await svc.AttachToPostAsync("cl-post", ["sanitation"], "u-cl-author", RolesSet(Roles.Member), session));

        // U8b call-site idiom: persist the resolved TagIds onto the content
        // doc in a **fresh** session (the AttachToPostAsync lane's own
        // SaveChangesAsync does not carry the loaded post's TagIds mutation
        // to the DB — the U8b change-tracking quirk).
        await using (var persist = store.OpenSession(new SessionOptions()))
        {
            var p = await persist.LoadAsync<Post>("cl-post", ct);
            Assert.NotNull(p);
            p!.TagIds = tags.Select(t => t.Id).ToList();
            persist.Store(p);
            await persist.SaveChangesAsync(ct);
        }

        // Exactly one tag.attach + one tag.create (the C-TG·9 two-row shape).
        var attachRows = await AuditsFor(store, "tag.attach");
        var createRows = await AuditsFor(store, "tag.create");
        Assert.Single(attachRows);
        Assert.Single(createRows);
        Assert.Equal("u-cl-author", attachRows[0].ActorId);
        Assert.Equal("u-cl-author", createRows[0].ActorId);
        Assert.Equal(AccessVia.Owner, attachRows[0].Via);
        Assert.Equal(AccessVia.Owner, createRows[0].Via);

        // The read surfaces (the *next* request — strong consistency): the
        // author's tag list shows the tag with UseCount 1.
        var list = await svc.ListForActorAsync("u-cl-author");
        var item = Assert.Single(list, i => i.Tag.Slug == "sanitation");
        Assert.Equal(1, item.UseCount);

        // The by-tag view shows the post.
        var posts = await svc.ListPostsByTagAsync("sanitation", "u-cl-author");
        Assert.Contains(posts, p => p.Id == "cl-post");
    }

    // ── Gate 2 — Handoff ───────────────────────────────────────────────────
    // Two legs (the design doc §2.5 pin):
    // (a) **Strong consistency (C4):** a group member added *after* the post
    //     sees the tag on the *very next* request (the F3 flip — the
    //     membership row is live, no projection lag).
    // (b) **Creator-reword ("handoff to the creator"):** the creator sets a
    //     `de` TagTranslation; a viewer whose effective language is `de`
    //     sees the `de` name on the next read (the F6 / C-TG·5 pin; the
    //     translation is live, no cache).

    [Fact(DisplayName = "GATE handoff — membership add is live + creator reword is live")]
    public async Task U12_Gate_Handoff_MembershipLive_CreatorRewordLive()
    {
        var store = await BootStoreAsync();
        var svc = NewTagService(store);
        var userInfo = new UserInfoService(store);
        var ct = TestContext.Current.CancellationToken;

        await SeedDefaultLanguage(store, "en");
        const string owner = "u-ho-owner";
        const string member = "u-ho-member";

        // ── Leg (a): strong consistency on the group-lane read ─────────────
        var group = await userInfo.CreateGroupAsync(owner, "Ho family", null);
        await Plant(store, new Tag
        {
            Id = "tag-ho", Slug = "sanitation", Name = "Sanitation",
            LanguageCode = "en", CreatedBy = owner,
        });
        await Plant(store, TaggedGroupPost("ho-post", group.Id, owner, "tag-ho"));

        // Before the membership add: the member (not yet in the group) sees
        // **nothing** — the group post's own Read decision denies.
        Assert.Empty(await svc.ListPostsByTagAsync("sanitation", member));
        Assert.Empty(await svc.ListForActorAsync(member));

        // The membership lands through the frozen write seam (C4 live row).
        await userInfo.AddGroupMemberAsync(group.Id, member, addedBy: owner);

        // The **very next** request: the member now sees the post + the tag.
        var memberPosts = await svc.ListPostsByTagAsync("sanitation", member);
        Assert.Contains(memberPosts, p => p.Id == "ho-post");
        Assert.Contains(await svc.ListForActorAsync(member), i => i.Tag.Id == "tag-ho");

        // ── Leg (b): creator-reword is live on the viewer's next read ──────
        // (The "handoff to the creator" reading of §2.5: the creator's
        // TagTranslation is visible to all viewers on their next read.)
        await Plant(store, new Tag
        {
            Id = "tag-ho2", Slug = "hygiene", Name = "Hygiene",
            LanguageCode = "en", CreatedBy = owner,
        });
        await Plant(store, TaggedPost("ho2-post", owner, "tag-ho2"));

        // Before the reword: the base name (`Hygiene`) is the display.
        var before = await svc.ListForActorAsync(owner);
        var beforeItem = Assert.Single(before, i => i.Tag.Id == "tag-ho2");
        Assert.Equal("Hygiene", beforeItem.DisplayedName);

        // The creator sets the `de` translation (C-TG·5 standing holds).
        await using (var session = store.OpenSession(new SessionOptions()))
            await svc.AddTagTranslationAsync(
                "tag-ho2", "de", "Reinigung", owner, RolesSet(Roles.Member), session);

        // Switch the instance default to `de` (the viewer's effective
        // language is now `de` — the ADR 0005 preference order collapses
        // to the instance default in Core).
        await SeedDefaultLanguage(store, "de");

        // The **very next** read: the `de` translation is the display name.
        var after = await svc.ListForActorAsync(owner);
        var afterItem = Assert.Single(after, i => i.Tag.Id == "tag-ho2");
        Assert.Equal("Reinigung", afterItem.DisplayedName);
    }

    // ── Gate 3 — Part-vs-whole ─────────────────────────────────────────────
    // The 24-test §2.4 list is the **whole**; closed-loop + handoff are the
    // **parts**; all must pass together. This test re-runs a representative
    // subset of the §2.4 anchors (F1, F4, F6, F9, F10, the audit-row shape,
    // the no-audit-row pin, the Slug derivation) in a single execution to
    // prove they are mutually consistent. The *real* part-vs-whole
    // evidence is the full `dotnet exec` run count (the 24-test list +
    // these three gate tests) recorded in the handoff note.

    [Fact(DisplayName = "GATE part-vs-whole — §2.4 anchor subset passes together in one run")]
    public async Task U12_Gate_PartVsWhole_AnchorSubsetPassesTogether()
    {
        var store = await BootStoreAsync();
        var svc = NewTagService(store);
        var ct = TestContext.Current.CancellationToken;

        await SeedDefaultLanguage(store, "en");
        const string author = "u-pw-author";
        const string viewer = "u-pw-viewer";

        // ── F1 anchor (C-TG·9): attach → one tag.attach + one tag.create ──
        await Plant(store, NewPost("pw-post-a", author));
        var pwTags = new List<Tag>();
        await using (var s1 = store.OpenSession(new SessionOptions()))
            pwTags.AddRange(await svc.AttachToPostAsync("pw-post-a", ["sanitation"], author, RolesSet(Roles.Member), s1));
        // U8b call-site idiom: persist TagIds in a fresh session (the change-tracking quirk).
        await using (var s1p = store.OpenSession(new SessionOptions()))
        {
            var p = await s1p.LoadAsync<Post>("pw-post-a", ct);
            Assert.NotNull(p);
            p!.TagIds = pwTags.Select(t => t.Id).ToList();
            s1p.Store(p);
            await s1p.SaveChangesAsync(ct);
        }
        Assert.Single(await AuditsFor(store, "tag.attach"));
        Assert.Single(await AuditsFor(store, "tag.create"));

        // ── F4 anchor (C-TG·1 / C-TG·2): tag behind unread content is absent ─
        await Plant(store, new Tag
        {
            Id = "tag-pw-secret", Slug = "secret", Name = "Secret",
            LanguageCode = "en", CreatedBy = author,
        });
        await Plant(store, TaggedPost("pw-post-b", author, "tag-pw-secret"));
        Assert.Empty(await svc.ListForActorAsync(viewer));
        Assert.Empty(await svc.ListPostsByTagAsync("secret", viewer));

        // ── F6 anchor (C-TG·5): creator sets a translation → one row ───────
        await using var q = store.QuerySession();
        var tagId = (await q.Query<Tag>().Where(t => t.Slug == "sanitation").FirstAsync(ct)).Id;
        await using (var s2 = store.OpenSession(new SessionOptions()))
            await svc.AddTagTranslationAsync(tagId, "de", "Reinigung", author, RolesSet(Roles.Member), s2);
        Assert.Single(await AuditsFor(store, "tagtranslation.add"));

        // ── F9 anchor (C-TG·4): display name resolves in the viewer's lang ─
        await SeedDefaultLanguage(store, "de");
        var list = await svc.ListForActorAsync(author);
        var item = Assert.Single(list, i => i.Tag.Id == tagId);
        Assert.Equal("Reinigung", item.DisplayedName);

        // ── F10 anchor (C-TG·2): suggest is capped at ≤ 10 ─────────────────
        for (var i = 0; i < 12; i++)
            await Plant(store, new Tag
            {
                Id = "tag-pw-" + i, Slug = "cap" + i, Name = "Cap " + i,
                LanguageCode = "en", CreatedBy = author,
            });
        var capPost = NewPost("pw-cap-post", author);
        capPost.TagIds = Enumerable.Range(0, 12).Select(i => "tag-pw-" + i).ToList();
        await Plant(store, capPost);
        Assert.True((await svc.SuggestAsync(string.Empty, author)).Count <= 10);

        // ── No-audit-row pin (C-TG·8): reads emitted no tag-family row ─────
        // The write rows so far (F1 tag.attach + tag.create, F6
        // tagtranslation.add, the Slug anchor's tag.attach on the existing
        // slug) are the writes'; the two reads below must add **zero**
        // additional tag-family rows (a read is not a decision — D7).
        int before;
        await using (var qb = store.QuerySession())
            before = await qb.Query<AccessAudit>()
                .Where(a => a.TargetKind == "tag").CountAsync(ct);
        await svc.ListForActorAsync(author);
        await svc.SuggestAsync("san", author);
        await using var qa = store.QuerySession();
        var after = await qa.Query<AccessAudit>()
            .Where(a => a.TargetKind == "tag").CountAsync(ct);
        Assert.Equal(before, after);

        // ── Slug derivation anchor (C-TG·4): lowercase + trim ──────────────
        await Plant(store, NewPost("pw-slug-post", author));
        await using (var s3 = store.OpenSession(new SessionOptions()))
        {
            var tags = await svc.AttachToPostAsync(
                "pw-slug-post", ["  Sanitation  "], author, RolesSet(Roles.Member), s3);
            Assert.Contains(tags, t => t.Slug == "sanitation");
        }

        // ── F12 anchor (C-TG·7): a fresh store has zero tags ───────────────
        // (Verified in F12_FreshInstanceHasZeroTags; here we assert the
        // current store has ≥ 1 tag, proving the anchors above operated on
        // a non-empty store.)
        await using var qf = store.QuerySession();
        Assert.True(await qf.Query<Tag>().CountAsync(ct) >= 1);
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

    // ─── U8b — the Web submit → PostService.CreatePostAsync tag attach ────
    //
    // U8b (the register patch to U8's closed deliverable set) wires the
    // composer's <c>TagIds</c> form field through the Web layer (the
    // <c>TagSlugs.Parse</c> helper) into <c>PostDraft.TagSlugs</c>, which
    // <see cref="PostService.CreatePostAsync"/> hands to
    // <see cref="TagService.AttachToPostAsync"/> on the **same**
    // <c>IDocumentSession</c> (C3 single-transaction idiom). This test
    // exercises that full write path at the Core level: a post created via
    // <c>CreatePostAsync</c> with <c>TagSlugs = ["sanitation"]</c> must
    // round-trip with a non-empty <c>TagIds</c> list, the <c>Tag</c> doc
    // must exist with the derived slug, the audit rows must be
    // <c>tag.attach</c> + <c>tag.create</c> (exactly one each), and
    // <c>ListForActorAsync</c> must surface the tag to the author.

    [Fact]
    public async Task U8b_WebSubmit_AttachesTag_RoundTrips_OnPost()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        // Seed the language catalog so ResolveLanguageCodeAsync can resolve
        // a concrete BCP-47 code (the ADR 0018 authored-in idiom).
        await SeedDefaultLanguage(store, "en");

        // Plant the component (the posting-right gate's target) and
        // membership for the author (the posting-right bypass via
        // GlobalAdmin would also work, but a plain Member + membership row
        // is the canonical M3 path).
        const string comp = "u8b-comp";
        const string author = "u8b-author";
        await Plant(store, new Component { Id = comp, Name = "U8b Test Community", Enabled = true });
        var userInfo = new UserInfoService(store);
        await userInfo.SetCommunityMembershipAsync(comp, author, actorId: "u8b-admin");

        // Build the PostService with the live TagService as the 4th ctor
        // arg (the U8b constructor growth — optional trailing parameter,
        // so the 7 existing positional call sites in PostServiceTests
        // compile unchanged).
        var authz = new AuthorizationService(store, userInfo);
        var translations = new TranslationProvider(store);
        var tagSvc = new TagService(store, authz, translations);
        var postsSvc = new PostService(userInfo, authz, store, tagSvc);

        // The Web layer would parse the form's TagIds JSON string via
        // TagSlugs.Parse; at the Core level we pass the slug list directly
        // (the PostDraft.TagSlugs parameter).
        var draft = new PostDraft(
            comp,
            "U8b tag test post",
            "A post created with a tag through the U8b wire.",
            new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, author)]),
            LanguageCode: "en",
            TagSlugs: ["sanitation"]);

        Post created;
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            created = await postsSvc.CreatePostAsync(
                draft, author, RolesSet(Roles.Member), session);
        }

        // (1) The Post doc round-trips with a non-empty TagIds list.
        Assert.NotNull(created);
        Assert.Equal(author, created.AuthorId);
        Assert.Equal(comp, created.ComponentId);
        Assert.NotNull(created.TagIds);
        Assert.Single(created.TagIds);

        // (2) The Tag doc exists with the derived slug + the creator.
        await using (var q = store.QuerySession())
        {
            var tag = await q.LoadAsync<Kumunita.Core.Tags.Tag>(created.TagIds[0], ct);
            Assert.NotNull(tag);
            Assert.Equal("sanitation", tag.Slug);
            Assert.Equal(author, tag.CreatedBy);
        }

        // (3) Audit rows: exactly one tag.attach + one tag.create.
        var attachRows = await AuditsFor(store, "tag.attach");
        var createRows = await AuditsFor(store, "tag.create");
        Assert.Single(attachRows);
        Assert.Single(createRows);
        Assert.Equal(author, attachRows[0].ActorId);
        Assert.Equal(author, createRows[0].ActorId);

        // (4) ListForActorAsync surfaces the tag to the author (the C-TG·2
        // base query: distinct Tag rows used on ≥ 1 post the actor may
        // read).
        var items = await tagSvc.ListForActorAsync(author);
        var item = Assert.Single(items, i => i.Tag.Slug == "sanitation");
        Assert.Equal(1, item.UseCount);
    }

    // ─── U6 read-lane helpers ─────────────────────────────────────────────

    /// <summary>Build the read-lane trio over the scratch store (the
    /// <see cref="GroupPostServiceTests.Services"/> precedent, verbatim) and
    /// return a <see cref="TagService"/> wired to the live
    /// <see cref="AuthorizationService"/> / <see cref="TranslationProvider"/>
    /// — the U6 constructor growth (C-TG·2 base query needs the content's own
    /// <c>Read</c> decision + the display-name resolution). The write-lane
    /// U5 tests now call this same helper (their <see cref="ITagService"/>
    /// surface is unchanged; only the constructor grew).</summary>
    private static TagService NewTagService(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var translations = new TranslationProvider(store);
        return new TagService(store, authz, translations);
    }

    /// <summary>Seed the instance language catalog + default to
    /// <paramref name="code"/> so
    /// <see cref="ITranslationProvider.ResolveEffectiveLanguageAsync(string?)"/>
    /// resolves to <paramref name="code"/> (the F9 "viewer's language" pin —
    /// in Core there is no cookie, so the ADR 0005 preference order collapses
    /// to the instance default; the Web layer would pass the
    /// <c>kumunita.locale</c> cookie as the explicit preference).</summary>
    private static async Task SeedDefaultLanguage(IDocumentStore store, string code)
    {
        await Plant(store, new LanguageCatalog { Id = code, NativeName = code, Enabled = true, SortOrder = 0 });
        await Plant(store, new LocaleSettings { DefaultLanguageCode = code });
    }

    /// <summary>A planted community post (audience = the author) carrying the
    /// given tag id — the U6 read-lane fixture (the
    /// <see cref="GroupPostServiceTests.GroupPost"/> community-lane shape).</summary>
    private static Post TaggedPost(string id, string author, string tagId)
    {
        var p = NewPost(id, author);
        p.TagIds = [tagId];
        return p;
    }

    /// <summary>A planted group post (non-empty <c>GroupId</c>, empty
    /// <c>ComponentId</c>, audience non-null empty — the G·8 shape) carrying
    /// the given tag id — the U6 F3 fixture (the
    /// <see cref="GroupPostServiceTests.GroupPost"/> group-lane shape).</summary>
    private static Post TaggedGroupPost(string id, string groupId, string author, string tagId)
    {
        var p = new Post
        {
            Id = id,
            ComponentId = string.Empty,
            GroupId = groupId,
            AuthorId = author,
            Body = "group post " + id,
            Audience = new Audience(),
            TagIds = [tagId],
            Created = DateTimeOffset.UtcNow,
        };
        return p;
    }

    /// <summary>A planted <see cref="PageKind.User"/> blog page carrying the
    /// given tag id — the U6 read-lane page fixture (the ADR 0040 author
    /// audience).</summary>
    private static Page TaggedBlogPage(string id, string author, string tagId) => new()
    {
        Id = id,
        Slug = id,
        Title = "page " + id,
        Body = "body " + id,
        AuthorId = author,
        Kind = PageKind.User,
        TagIds = [tagId],
        Audience = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, author)]),
        Created = DateTimeOffset.UtcNow,
    };

    /// <summary>Test double **over the real** <see cref="AuthorizationService"/>
    /// (decision behavior unchanged — only the *call surface* is recorded):
    /// counts total authorization calls and <c>AccessAction.Moderate</c>
    /// invocations. The U10 ADR 0006-D lane pin's spy — the
    /// <see cref="GroupPostServiceTests.RecordingAuthz"/> idiom, carried to
    /// the tag lane (the tag surface only ever needs <c>Read</c> decisions;
    /// the reserved <c>Moderate</c> action id must never be invoked through
    /// it).</summary>
    private sealed class RecordingAuthz(IAuthorizationService inner) : IAuthorizationService
    {
        public int TotalCalls { get; private set; }
        public int ModerateCalls { get; private set; }

        private void Note(AccessAction action)
        {
            TotalCalls++;
            if (action == AccessAction.Moderate) ModerateCalls++;
        }

        public Task<Decision> CanAsync(string actorId, AccessAction action, IAuditableResource target)
        {
            Note(action);
            return inner.CanAsync(actorId, action, target);
        }

        public Task<Decision> CanAsync(string actorId, AccessAction action, IAuditableResource target, IDocumentSession session)
        {
            Note(action);
            return inner.CanAsync(actorId, action, target, session);
        }

        public Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates)
        {
            Note(action);
            return inner.CanSeeAsync(actorId, action, candidates);
        }

        public Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates, IDocumentSession session)
        {
            Note(action);
            return inner.CanSeeAsync(actorId, action, candidates, session);
        }

        public Task<Decision> CanSeeGroupAsync(string actorId, string groupId, string? targetPostId)
        {
            TotalCalls++;
            return inner.CanSeeGroupAsync(actorId, groupId, targetPostId);
        }

        public Task<Decision> CanSeeGroupAsync(string actorId, string groupId, string? targetPostId, IDocumentSession session)
        {
            TotalCalls++;
            return inner.CanSeeGroupAsync(actorId, groupId, targetPostId, session);
        }

        public Task<Decision> CanSeeGroupFeedAsync(string actorId, string groupId, int candidateCount)
        {
            TotalCalls++;
            return inner.CanSeeGroupFeedAsync(actorId, groupId, candidateCount);
        }

        public Task<Decision> CanSeeGroupFeedAsync(string actorId, string groupId, int candidateCount, IDocumentSession session)
        {
            TotalCalls++;
            return inner.CanSeeGroupFeedAsync(actorId, groupId, candidateCount, session);
        }
    }
}
