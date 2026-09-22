using Kumunita.Core;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ATT U6 — the <b>10</b> Core seam tests pinned by the design doc
/// (<c>file-attachments-design.md</c> §2.9, the ATT lane's pinned Core half).
/// This is the Core half of the attachment lane; the Web half (the serve /
/// upload / link-splice FACES) is <see cref="Kumunita.Web.Tests"/> (U11).
/// <para>
/// This file <b>mirrors</b> <see cref="ContentImageOwnershipTests"/>
/// line-for-line — same harness (<see cref="PostgresFixture"/> /
/// <c>BootStoreAsync</c> / <c>Services</c> / <c>Plant</c> /
/// <c>RunInSession</c> / <c>GlobalAdminRoles</c>), same composition —
/// swapping <c>ImageIds</c> → <c>AttachmentIds</c>,
/// <c>/content-image/</c> → <c>/attachment/</c>, and
/// <c>Find*ByImageIdAsync</c> → <c>Find*ByAttachmentIdAsync</c>. The
/// GlobalAdmin-bypass idiom is reused as-is (the cleanest way to drive the
/// write lanes without planting membership rows) — the tests pin the
/// <b>verbatim <c>AttachmentIds</c> write</b>, not the gate.
/// </para>
/// <para>
/// <b>U6 drift pause — RESOLVED (U12, option 1):</b> test
/// <c>#6 — PostEdit_ReparsesAttachmentIds</c> was left commented out by U6
/// because U4 — deliberately, following the image lane's own "create only,
/// not edit" precedent for these two lanes — had not added an
/// <c>attachmentIds</c> parameter to
/// <see cref="PostService.UpdatePostAsync"/> /
/// <see cref="PostService.UpdateGroupPostAsync"/>. U12 (the close unit)
/// resolved the pause as <b>option 1</b> (restore the §2.3 frozen pin by
/// fixing the code, per the decision framing in the U6 handoff): the two
/// edit lanes now take the trailing <c>attachmentIds</c> param and write
/// <c>post.AttachmentIds = attachmentIds ?? []</c> (replace-style), and the
/// Web edit call-sites pass the re-parsed
/// <c>AttachmentIds.ExtractAttachmentIds(body)</c>. Test #6 below is live
/// again and pins that. Recorded in the handoff <c>## U12</c> /
/// <c>## Summary</c> (see <c>## U6 — DRIFT PAUSE</c> for the original
/// conflict).
/// </para>
/// <para>
/// <b>Parse behavior is not re-asserted here</b> (the same drift-pause note
/// <see cref="ContentImageOwnershipTests"/> carries): the
/// dedupe / first-occurrence-order / not-over-match parse of a body's
/// <c>/attachment/{id}</c> links is the <b>Web</b> helper's job
/// (<c>Kumunita.Web.Security.AttachmentIds.ExtractAttachmentIds</c>, U7),
/// and <see cref="Kumunita.Core.Tests"/> references <b>only</b>
/// <c>Kumunita.Core</c> (no <c>Kumunita.Core.Tests</c> →
/// <c>Kumunita.Web</c>). Core stays body-parse-free (C-ATT·4). What <b>is</b>
/// pinned here is the Core half — the service writes the draft's / POCO's
/// <c>AttachmentIds</c> <b>verbatim</b> (and null-coalesces to a non-null
/// empty list).
/// </para>
/// </summary>
public class AttachmentOwnershipTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string ComponentId = "c-att-u06-comp";
    private const string Actor = "u-att-u06-actor";

    /// <summary>GlobalAdmin bypasses <see cref="PostService.CreatePostAsync"/>
    /// 's posting-right gate and the announcement create/edit gates (the
    /// admin path skips the membership read / the role-narrowing entirely) —
    /// the cleanest way to drive the write lanes without planting membership
    /// rows. These tests pin the <b>verbatim <c>AttachmentIds</c> write</b>,
    /// not the gate.</summary>
    private static readonly IReadOnlySet<string> GlobalAdminRoles =
        new HashSet<string> { Roles.GlobalAdmin };

    // ── 1 — FindPostByAttachmentId_ReturnsOwningPost ─────────────────────
    //
    // A post whose `AttachmentIds` contains the id is found by the
    // un-audited reverse-lookup read seam (C-ATT·4 — the serve route's owner
    // resolution, the `Find*ByImageIdAsync` mirror with AttachmentIds
    // swapped in). Mirrors the image lane's R5_ReverseLookup_FindsOwningPost.

    [Fact]
    public async Task FindPostByAttachmentId_ReturnsOwningPost()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);

        // The owner post carries an attachment reference
        // (`AttachmentIds = ["aaaa"]`).
        await Plant(store, new Post
        {
            Id = "u06-owner",
            ComponentId = ComponentId,
            AuthorId = Actor,
            Body = "[Report](/attachment/aaaa)",
            Audience = Audience(GrantKind.User, Actor),
            Created = DateTimeOffset.UtcNow,
            AttachmentIds = ["aaaa"],
        });

        // A second post with <b>no</b> attachment references — its empty
        // `AttachmentIds` must not be confused with `"aaaa"`.
        await Plant(store, new Post
        {
            Id = "u06-noref",
            ComponentId = ComponentId,
            AuthorId = Actor,
            Body = "no attachments here",
            Audience = Audience(GrantKind.User, Actor),
            Created = DateTimeOffset.UtcNow,
        });

        var found = await svc.FindPostByAttachmentIdAsync("aaaa");
        Assert.NotNull(found);
        Assert.Equal("u06-owner", found!.Id);
        Assert.Equal(["aaaa"], found.AttachmentIds);
    }

    // ── 2 — FindPostByAttachmentId_ReturnsNullWhenAbsent ─────────────────
    //
    // An id no post owns → null (the serve route's 404 branch, §2.7 step 2).
    // Mirrors the image lane's unknown-id → null assertion.

    [Fact]
    public async Task FindPostByAttachmentId_ReturnsNullWhenAbsent()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);

        // A post with an empty `AttachmentIds` is present but owns nothing.
        await Plant(store, new Post
        {
            Id = "u06-noref",
            ComponentId = ComponentId,
            AuthorId = Actor,
            Body = "no attachments here",
            Audience = Audience(GrantKind.User, Actor),
            Created = DateTimeOffset.UtcNow,
        });

        // The unknown id → null (the serve route's 404 branch).
        Assert.Null(await svc.FindPostByAttachmentIdAsync("zzz"));
    }

    // ── 3 — FindReplyByAttachmentId_ReturnsOwningReply ───────────────────
    //
    // A reply whose `AttachmentIds` contains the id is returned; an unknown
    // id → null. Mirrors the image lane's R5_ReverseLookup_FindsOwningReply.

    [Fact]
    public async Task FindReplyByAttachmentId_ReturnsOwningReply()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);

        await Plant(store, new Post
        {
            Id = "u06-parent",
            ComponentId = ComponentId,
            AuthorId = Actor,
            Body = "parent body, no attachments",
            Audience = Audience(GrantKind.User, Actor),
            Created = DateTimeOffset.UtcNow,
        });

        await Plant(store, new PostReply
        {
            Id = "u06-reply",
            PostId = "u06-parent",
            AuthorId = Actor,
            Body = "[File](/attachment/cccc)",
            Created = DateTimeOffset.UtcNow,
            AttachmentIds = ["cccc"],
        });

        var found = await svc.FindReplyByAttachmentIdAsync("cccc");
        Assert.NotNull(found);
        Assert.Equal("u06-reply", found!.Id);
        Assert.Equal(["cccc"], found.AttachmentIds);

        // The unknown id → null (the serve route's 404 branch).
        Assert.Null(await svc.FindReplyByAttachmentIdAsync("zzz"));
    }

    // ── 4 — FindAnnouncementByAttachmentId_ReturnsOwningAnnouncement ─────
    //
    // An announcement whose `AttachmentIds` contains the id is found by the
    // `IAnnouncementService.FindByAttachmentIdAsync` seam (the
    // `FindByImageIdAsync` mirror); an unknown id → null.

    [Fact]
    public async Task FindAnnouncementByAttachmentId_ReturnsOwningAnnouncement()
    {
        var store = await BootStoreAsync();
        var annSvc = AnnouncementsSvc(store);

        await Plant(store, new Announcement
        {
            Id = "u06-ann-owner",
            Scope = AnnouncementScope.Public,
            Title = "Scheduled maintenance",
            Body = "[Runbook](/attachment/dddd)",
            AuthorId = Actor,
            Created = DateTimeOffset.UtcNow,
            AttachmentIds = ["dddd"],
        });

        // A second announcement with <b>no</b> attachment references.
        await Plant(store, new Announcement
        {
            Id = "u06-ann-noref",
            Scope = AnnouncementScope.Public,
            Title = "Another notice",
            Body = "no attachments here",
            AuthorId = Actor,
            Created = DateTimeOffset.UtcNow,
        });

        var found = await annSvc.FindByAttachmentIdAsync("dddd");
        Assert.NotNull(found);
        Assert.Equal("u06-ann-owner", found!.Id);
        Assert.Equal(["dddd"], found.AttachmentIds);

        // The unknown id → null (the serve route's 404 branch).
        Assert.Null(await annSvc.FindByAttachmentIdAsync("zzz"));
    }

    // ── 5 — PostCreate_PersistsAttachmentIds ──────────────────────────────
    //
    // The create write lane writes the draft's `AttachmentIds` **verbatim**
    // (non-null, order-preserving) and round-trips from Postgres. Mirrors the
    // image lane's R3_ImageIdsPopulatedFromBodyLinks (the verbatim-write
    // half — the parse is the Web helper's, C-ATT·4).

    [Fact]
    public async Task PostCreate_PersistsAttachmentIds()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);

        // The draft's `AttachmentIds` is what the (Web) parse of the body
        // would hand the service — deduped, first-occurrence order (U7). The
        // Core obligation this pins: the service writes it verbatim.
        const string body = "See this: [Report](/attachment/aaaa) and this: " +
                            "[Sheet](/attachment/bbbb) (again) [Report](/attachment/aaaa).";
        var draft = new PostDraft(
            ComponentId, "T", body, Audience(GrantKind.User, Actor),
            AttachmentIds: ["aaaa", "bbbb"]);

        var post = await RunInSession(store, s =>
            svc.CreatePostAsync(draft, Actor, GlobalAdminRoles, s));

        // The in-memory doc the service returned:
        Assert.Equal(["aaaa", "bbbb"], post.AttachmentIds);
        Assert.Equal(body, post.Body);

        // And the <b>stored</b> doc (reloaded from Postgres) round-trips the
        // same non-null, order-preserving list — the verbatim-write pin.
        var stored = await RunInSession(store, s => s.LoadAsync<Post>(post.Id));
        Assert.NotNull(stored);
        Assert.Equal(["aaaa", "bbbb"], stored!.AttachmentIds);
    }

    // ── 6 — PostEdit_ReparsesAttachmentIds ────────────────────────────────
    //
    // The post edit lane persists `AttachmentIds` (replaced wholesale — the
    // re-parse of the re-submitted body is authoritative, the same shape as
    // the reply edit lane, #8). LIVE since U12 (option 1): the
    // `UpdatePostAsync` edit lane gained the trailing `attachmentIds`
    // parameter + the `post.AttachmentIds = attachmentIds ?? []` write (U12
    // resolved the `## U6 — DRIFT PAUSE` by restoring the §2.3 frozen pin
    // rather than weakening it). The Web edit call-site passes the re-parsed
    // `AttachmentIds.ExtractAttachmentIds(body)` (the U12 wiring).

    [Fact]
    public async Task PostEdit_ReparsesAttachmentIds()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);

        // Create a post that already carries an attachment reference …
        var created = await RunInSession(store, s => svc.CreatePostAsync(
            new PostDraft(ComponentId, "T", "old body",
                Audience(GrantKind.User, Actor), AttachmentIds: ["old"]),
            Actor, GlobalAdminRoles, s));

        // … and the edit lane re-copies the (re-parsed) list — a body edit
        // can add or remove /attachment/{id} links; the re-parse is
        // authoritative, so the stored list is replaced wholesale (the
        // §2.3 `existing.AttachmentIds = draft.AttachmentIds ?? []` shape).
        var edited = await RunInSession(store, s => svc.UpdatePostAsync(
            created.Id, Actor, "new title", "new body with [x](/attachment/new1) " +
                                          "and [y](/attachment/new2)",
            Audience(GrantKind.User, Actor), null, s,
            attachmentIds: ["new1", "new2"]));

        Assert.Equal(["new1", "new2"], edited.AttachmentIds);
        var stored = await RunInSession(store, s => s.LoadAsync<Post>(edited.Id));
        Assert.NotNull(stored);
        Assert.Equal(["new1", "new2"], stored!.AttachmentIds);
    }

    // ── 7 — ReplyCreate_PersistsAttachmentIds ─────────────────────────────
    //
    // The reply create lane persists `AttachmentIds` (C-ATT·8 — the write the
    // image lane deliberately lacks: the image reply create does not set
    // `ImageIds`, the reply-404 drift pause). Written fresh from the
    // `CreateReplyAsync` signature (U4) — there is no image twin to mirror.

    [Fact]
    public async Task ReplyCreate_PersistsAttachmentIds()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);

        await Plant(store, new Post
        {
            Id = "u06-rparent",
            ComponentId = ComponentId,
            AuthorId = Actor,
            Body = "parent body, no attachments",
            Audience = Audience(GrantKind.User, Actor),
            Created = DateTimeOffset.UtcNow,
        });

        const string body = "[File](/attachment/eeee)";
        var reply = await RunInSession(store, s =>
            svc.CreateReplyAsync("u06-rparent", Actor, body, s,
                languageCode: null, attachmentIds: ["eeee"]));

        Assert.Equal(["eeee"], reply.AttachmentIds);
        Assert.Equal(body, reply.Body);
        Assert.Equal("u06-rparent", reply.PostId);

        // The stored doc round-trips the same non-null list.
        var stored = await RunInSession(store, s => s.LoadAsync<PostReply>(reply.Id));
        Assert.NotNull(stored);
        Assert.Equal(["eeee"], stored!.AttachmentIds);
    }

    // ── 8 — ReplyEdit_ReparsesAttachmentIds ───────────────────────────────
    //
    // The reply edit lane persists `AttachmentIds` (C-ATT·8 — the write the
    // image lane's reply edit deliberately lacks). The re-parse of the
    // (re-submitted) body is authoritative, so the stored list is replaced
    // wholesale (mirrors `reply.Body = body ?? string.Empty`). Written fresh
    // from the `UpdateReplyAsync` signature (U4) — there is no image twin to
    // mirror.

    [Fact]
    public async Task ReplyEdit_ReparsesAttachmentIds()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);

        await Plant(store, new Post
        {
            Id = "u06-rparent",
            ComponentId = ComponentId,
            AuthorId = Actor,
            Body = "parent body, no attachments",
            Audience = Audience(GrantKind.User, Actor),
            Created = DateTimeOffset.UtcNow,
        });

        // Create a reply that already carries an attachment reference …
        var created = await RunInSession(store, s =>
            svc.CreateReplyAsync("u06-rparent", Actor, "old body", s,
                languageCode: null, attachmentIds: ["old"]));
        Assert.Equal(["old"], created.AttachmentIds);

        // … and the edit lane re-copies the (re-parsed) list (replace-style).
        var edited = await RunInSession(store, s =>
            svc.UpdateReplyAsync(created.Id, Actor,
                "new body with [x](/attachment/new1) and [y](/attachment/new2)",
                s, attachmentIds: ["new1", "new2"]));

        Assert.Equal(["new1", "new2"], edited.AttachmentIds);

        // The stored doc round-trips the same non-null list.
        var stored = await RunInSession(store, s => s.LoadAsync<PostReply>(edited.Id));
        Assert.NotNull(stored);
        Assert.Equal(["new1", "new2"], stored!.AttachmentIds);
    }

    // ── 9 — AnnouncementCreate_PersistsAttachmentIds ──────────────────────
    //
    // The announcement create lane is POCO-direct: `CreateAsync` stores the
    // POCO the Web layer hands it as-is (after minting Id / AuthorId /
    // Created / LanguageCode), so the `AttachmentIds` the POCO carries is
    // preserved verbatim. This mirrors the U7 controller-initializer shape —
    // Core never parses a body (C-ATT·4).

    [Fact]
    public async Task AnnouncementCreate_PersistsAttachmentIds()
    {
        var store = await BootStoreAsync();
        var annSvc = AnnouncementsSvc(store);

        var announcement = new Announcement
        {
            Scope = AnnouncementScope.Public,
            Title = "Scheduled maintenance",
            Body = "[Runbook](/attachment/aaaa) and [Sheet](/attachment/bbbb)",
            AttachmentIds = ["aaaa", "bbbb"],
        };

        var stored = await RunInSession(store, s =>
            annSvc.CreateAsync(announcement, Actor, GlobalAdminRoles, s));

        // The in-memory doc the service returned:
        Assert.Equal(["aaaa", "bbbb"], stored.AttachmentIds);

        // And the stored doc (reloaded from Postgres) round-trips the same
        // non-null, order-preserving list — the POCO-direct write pin.
        var reloaded = await RunInSession(store, s => s.LoadAsync<Announcement>(stored.Id));
        Assert.NotNull(reloaded);
        Assert.Equal(["aaaa", "bbbb"], reloaded!.AttachmentIds);
    }

    // ── 10 — AnnouncementEdit_ReparsesAttachmentIds ───────────────────────
    //
    // The announcement edit lane re-copies `AttachmentIds` (the U5 line:
    // `existing.AttachmentIds = updated.AttachmentIds ?? []`). A null /
    // absent list coalesces to a non-null empty list. `Modified`-stamp
    // behavior is deliberately NOT pinned (the `changed` block excludes both
    // `ImageIds` and `AttachmentIds` — by design, matching the image lane).

    [Fact]
    public async Task AnnouncementEdit_ReparsesAttachmentIds()
    {
        var store = await BootStoreAsync();
        var annSvc = AnnouncementsSvc(store);

        // Plant a stored announcement that already carries an attachment.
        var seeded = await RunInSession(store, s =>
            annSvc.CreateAsync(new Announcement
            {
                Scope = AnnouncementScope.Public,
                Title = "Original",
                Body = "old body",
                AttachmentIds = ["old"],
            }, Actor, GlobalAdminRoles, s));
        Assert.Equal(["old"], seeded.AttachmentIds);

        // The edit lane re-copies the (re-parsed) list — a body edit can add
        // or remove /attachment/{id} links; the re-parse is authoritative, so
        // the stored list is replaced wholesale.
        var edited = await RunInSession(store, s =>
            annSvc.UpdateAsync(new Announcement
            {
                Id = seeded.Id,
                Scope = AnnouncementScope.Public,
                Title = "Updated",
                Body = "new body with [x](/attachment/new1) and [y](/attachment/new2)",
                AttachmentIds = ["new1", "new2"],
            }, Actor, GlobalAdminRoles, s));

        Assert.Equal(["new1", "new2"], edited.AttachmentIds);

        // The stored doc round-trips the same non-null list.
        var reloaded = await RunInSession(store, s => s.LoadAsync<Announcement>(edited.Id));
        Assert.NotNull(reloaded);
        Assert.Equal(["new1", "new2"], reloaded!.AttachmentIds);

        // A null / absent list on re-save coalesces to a non-null empty list
        // (the `?? []` pin).
        var cleared = await RunInSession(store, s =>
            annSvc.UpdateAsync(new Announcement
            {
                Id = seeded.Id,
                Scope = AnnouncementScope.Public,
                Title = "Cleared",
                Body = "body with no attachment links",
                // AttachmentIds left at the POCO default (null-coalesce
                // exercised by the U5 `?? []`).
            }, Actor, GlobalAdminRoles, s));
        Assert.NotNull(cleared.AttachmentIds);
        Assert.Empty(cleared.AttachmentIds);
    }

    // ── harness (mirrors ContentImageOwnershipTests) ──────────────────────

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

    private static (UserInfoService User, AuthorizationService Authz, PostService Posts)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var posts = new PostService(userInfo, authz, store);
        return (userInfo, authz, posts);
    }

    private static AnnouncementService AnnouncementsSvc(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        return new AnnouncementService(store, userInfo);
    }

    private static Audience Audience(GrantKind kind, string id)
        => new(AudienceMode.Any, [new AudienceGrant(kind, id)]);

    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    private static async Task<T> RunInSession<T>(IDocumentStore store, Func<IDocumentSession, Task<T>> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        return await action(session);
    }
}
