using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// RC U07 — the <b>5</b> Core seam tests pinned by the design doc
/// (<c>rich-content-design.md</c> §Pinned seam tests, items 8–12). This is the
/// Core half of the U07 13-test deliverable; the Web half is
/// <see cref="Kumunita.Web.Tests"/> (<c>ContentImageServingTests</c> +
/// <c>ContentImageUploadTests</c>).
/// <para>
/// The authoritative shape pins these tests encode:
/// <list type="number">
/// <item><b>R·3</b> — <c>ImageIds</c> is populated <b>server-side</b>, and
///       <b>Core stays body-parse-free</b>: "the draft records carry
///       <c>ImageIds</c>, the service writes them verbatim" (the
///       <c>ContentImageIds.ExtractContentImageIds</c> parse — dedupe,
///       first-occurrence order, not-over-matching — is the <b>Web</b>
///       layer's job, U04). The Core half is that
///       <see cref="PostService.CreatePostAsync"/> writes the draft's
///       <c>ImageIds</c> <b>verbatim</b> (and null-coalesces to a non-null
///       empty list).</item>
/// <item><b>R·5</b> — the reverse lookup is an <b>un-audited read seam</b> on
///       <see cref="PostService"/> (<see cref="PostService.FindPostByImageIdAsync"/>
///       / <see cref="PostService.FindReplyByImageIdAsync"/>); null when no
///       doc references the id (the route's 404 branch, R·4 step 3).</item>
/// <item><b>R·7</b> — <c>ImageIds</c> is the <b>5th additive</b> field on the
///       <c>Post</c> POCO; the pre-RC field set is otherwise unchanged (the
///       zero-migration pin made executable — the compile-time regression
///       alarm).</item>
/// </list>
/// </para>
/// <para>
/// <b>Drift pause (recorded here + in the handoff note):</b> the two
/// <c>R3_*</c> names are pinned by the design doc as the <b>parse</b> outcome
/// (dedupe / first-occurrence order / not-over-matching <c>[label](url)</c>
/// text / rejecting a remote <c>src</c>). That parse is the Web helper
/// <c>Kumunita.Web.Security.ContentImageIds.ExtractContentImageIds</c>, and
/// <see cref="Kumunita.Core.Tests"/> references <b>only</b>
/// <c>Kumunita.Core</c> (no <c>Kumunita.Web</c>) — so the <b>parse behavior</b>
/// itself is unreachable from this assembly, and R·5 forbids it living in Core.
/// What <b>is</b> writable here is the Core half of R·3 — the service's
/// <b>verbatim write</b> of the draft's <c>ImageIds</c> — which is what these
/// two tests exercise: the draft carries the deduped id list exactly as the
/// (Web) parse would emit it (a body linking two images + a duplicate →
/// <c>[aaaa, bbbb]</c>), and the service writes it to the doc verbatim; and a
/// draft whose body has no <c>/content-image/</c> links carries an empty (or
/// null) list, which the service writes as a non-null empty list. The
/// <b>dedupe / not-over-match</b> rules remain the Web parse's (U04) and are
/// not re-asserted here.
/// </para>
/// <para>
/// Harness: the <see cref="PostgresFixture"/> / Testcontainers shape from
/// <see cref="PostServiceTests"/> (same <c>BootStoreAsync</c>, same
/// <c>Services</c> composition, fresh scratch Postgres per test method — the
/// Core suite takes ~20 s and leaves Docker containers behind if killed).
/// </para>
/// </summary>
public class ContentImageOwnershipTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string ComponentId = "c-rc-u07-comp";
    private const string Actor = "u-rc-u07-actor";

    /// <summary>GlobalAdmin bypasses <see cref="PostService.CreatePostAsync"/>
    /// 's posting-right gate (the C4 gate reads live rows; the admin path
    /// skips the <c>ComponentMembership</c> read entirely) — the cleanest
    /// way to drive the write lane without planting membership rows. These
    /// tests pin the <b>verbatim <c>ImageIds</c> write</b>, not the gate.</summary>
    private static readonly IReadOnlySet<string> GlobalAdminRoles =
        new HashSet<string> { Roles.GlobalAdmin };

    // ── 1 — R3_ImageIdsPopulatedFromBodyLinks (R·3 verbatim-write half) ───
    //
    // A draft whose body references two images (`/content-image/aaaa` and
    // `/content-image/bbbb`) plus a duplicate — the (Web) parse of that body
    // emits the deduped, first-occurrence-order list `[aaaa, bbbb]` (U04's
    // rule; the draft carries it). The Core obligation this pins: the
    // service writes the draft's `ImageIds` **verbatim** onto the stored doc
    // — exactly `[aaaa, bbbb]`, non-null, order preserved (R·3).

    [Fact]
    public async Task R3_ImageIdsPopulatedFromBodyLinks()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);

        // The body is the rendered source (what the resident wrote); the
        // draft's `ImageIds` is what the (Web) parse of that body would hand
        // the service — deduped, first-occurrence order (U04). The Core test
        // pins that the service writes it verbatim, not that it parses.
        const string body = "See this: ![a](/content-image/aaaa)\nand this: " +
                            "![b](/content-image/bbbb) (again) ![a](/content-image/aaaa).";
        var draft = new PostDraft(
            ComponentId, "T", body, Audience(GrantKind.User, Actor),
            ImageIds: ["aaaa", "bbbb"]);

        var post = await RunInSession(store, s =>
            svc.CreatePostAsync(draft, Actor, GlobalAdminRoles, s));

        // The in-memory doc the service returned:
        Assert.Equal(["aaaa", "bbbb"], post.ImageIds);
        Assert.Equal(body, post.Body);

        // And the <b>stored</b> doc (reloaded from Postgres) round-trips the
        // same non-null, order-preserving list — the verbatim-write pin.
        var stored = await RunInSession(store, s => s.LoadAsync<Post>(post.Id));
        Assert.NotNull(stored);
        Assert.Equal(["aaaa", "bbbb"], stored!.ImageIds);
    }

    // ── 2 — R3_ImageIdsEmpty_WhenNoLinks (R·3 verbatim-write half) ────────
    //
    // A draft whose body has a plain <b>link</b> and a <b>remote-src</b> image
    // but <b>no</b> <c>/content-image/</c> link — the (Web) parse emits an
    // empty list (the parse uses the route-shape predicate, not a blanket
    // `![…](…)` match; a remote src is not a platform route). The Core
    // obligation this pins: the service writes that empty list to the doc
    // **verbatim** — the stored `ImageIds` is a non-null, empty list (the
    // POCO field's `= []` invariant), never null (R·3).

    [Fact]
    public async Task R3_ImageIdsEmpty_WhenNoLinks()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);

        // A plain link and a remote-src image: neither is a
        // `/content-image/{id}` reference, so the (Web) parse finds none.
        const string body = "Read more at [x](https://example.com) or " +
                            "see ![x](https://evil.example/i.png).";

        // Two write shapes must both yield a non-null empty list: the draft
        // carries an <b>empty</b> list (the parse found none) …
        var emptyDraft = new PostDraft(
            ComponentId, "T", body, Audience(GrantKind.User, Actor),
            ImageIds: []);
        var emptyPost = await RunInSession(store, s =>
            svc.CreatePostAsync(emptyDraft, Actor, GlobalAdminRoles, s));
        Assert.NotNull(emptyPost.ImageIds);
        Assert.Empty(emptyPost.ImageIds);

        // … and a <b>null</b> draft list (the record's trailing default) is
        // null-coalesced to the same non-null empty list.
        var nullDraft = new PostDraft(
            ComponentId, "T", body, Audience(GrantKind.User, Actor));
        var nullPost = await RunInSession(store, s =>
            svc.CreatePostAsync(nullDraft, Actor, GlobalAdminRoles, s));
        Assert.NotNull(nullPost.ImageIds);
        Assert.Empty(nullPost.ImageIds);

        // The stored doc round-trips a non-null empty list too.
        var stored = await RunInSession(store, s => s.LoadAsync<Post>(nullPost.Id));
        Assert.NotNull(stored!.ImageIds);
        Assert.Empty(stored.ImageIds);
    }

    // ── 3 — R5_ReverseLookup_FindsOwningPost (R·5, R·4 step 3) ────────────
    //
    // Two posts — one with `ImageIds = ["abc"]`, one without → the
    // un-audited reverse-lookup read seam (R·5) returns the owning post for
    // `"abc"` and null for an unknown id (`"zzz"` — the route's 404 branch,
    // R·4 step 3).

    [Fact]
    public async Task R5_ReverseLookup_FindsOwningPost()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);

        // The owner post carries a content-image reference (`ImageIds = ["abc"]`).
        await Plant(store, new Post
        {
            Id = "u07-owner",
            ComponentId = ComponentId,
            AuthorId = Actor,
            Body = "![a](/content-image/abc)",
            Audience = Audience(GrantKind.User, Actor),
            Created = DateTimeOffset.UtcNow,
            ImageIds = ["abc"],
        });

        // A second post with <b>no</b> image references — its empty `ImageIds`
        // must not be confused with `"abc"` by the reverse lookup.
        await Plant(store, new Post
        {
            Id = "u07-noref",
            ComponentId = ComponentId,
            AuthorId = Actor,
            Body = "no images here",
            Audience = Audience(GrantKind.User, Actor),
            Created = DateTimeOffset.UtcNow,
        });

        var found = await svc.FindPostByImageIdAsync("abc");
        Assert.NotNull(found);
        Assert.Equal("u07-owner", found!.Id);
        Assert.Equal(["abc"], found.ImageIds);

        // The unknown id → null (the route's 404 branch, R·4 step 3).
        Assert.Null(await svc.FindPostByImageIdAsync("zzz"));
    }

    // ── 4 — R5_ReverseLookup_FindsOwningReply (R·5, R·4 step 3) ───────────
    //
    // Same shape for the reply seam: a reply whose `ImageIds` contains the
    // id is returned; an unknown id → null.

    [Fact]
    public async Task R5_ReverseLookup_FindsOwningReply()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);

        await Plant(store, new Post
        {
            Id = "u07-parent",
            ComponentId = ComponentId,
            AuthorId = Actor,
            Body = "parent body, no images",
            Audience = Audience(GrantKind.User, Actor),
            Created = DateTimeOffset.UtcNow,
        });

        await Plant(store, new PostReply
        {
            Id = "u07-reply",
            PostId = "u07-parent",
            AuthorId = Actor,
            Body = "![b](/content-image/def)",
            Created = DateTimeOffset.UtcNow,
            ImageIds = ["def"],
        });

        var found = await svc.FindReplyByImageIdAsync("def");
        Assert.NotNull(found);
        Assert.Equal("u07-reply", found!.Id);
        Assert.Equal(["def"], found.ImageIds);

        // The unknown id → null (the route's 404 branch, R·4 step 3).
        Assert.Null(await svc.FindReplyByImageIdAsync("zzz"));
    }

    // ── 5 — R7_PostPoco_FieldSetUnmodifiedExceptImageIds (R·7) ────────────
    //
    // The R·7 zero-migration pin made executable: a <c>Post</c> is
    // constructed with <b>every</b> pre-RC field assigned at its pre-RC
    // type, and <c>ImageIds</c> assigned as the 5th additive field. The pin
    // is that <b>this compiles</b> — if any pre-RC field's name or type
    // changed, this file stops compiling, which <i>is</i> the R·7 alarm.
    //
    // R·7 field list (asserted, in the POCO's declaration order):
    //   Id, ComponentId, AuthorId, Title, Body, Audience, Created, Modified,
    //   Status (M3b), GroupId (ADR 0013), LanguageCode (ADR 0018),
    //   DeletedAt (ADR 0024), ImageIds (RC U03 — the 5th additive field).

    [Fact]
    public void R7_PostPoco_FieldSetUnmodifiedExceptImageIds()
    {
        // A fully-assigned Post. The compile-time surface is the pin: every
        // field below is named and type-checked against the current POCO.
        var post = new Post
        {
            // pre-RC (M3):
            Id = "r7-post",
            ComponentId = ComponentId,
            AuthorId = Actor,
            Title = "Title",
            Body = "![a](/content-image/aaaa)",
            Audience = Audience(GrantKind.User, Actor),
            Created = DateTimeOffset.UtcNow,
            Modified = null,
            // M3b ADD:
            Status = PostStatus.Active,
            // ADR 0013 ADD:
            GroupId = string.Empty,
            // ADR 0018 ADD:
            LanguageCode = "en",
            // ADR 0024 ADD:
            DeletedAt = null,
            // RC U03 ADD — the 5th additive field (R·7):
            ImageIds = ["aaaa", "bbbb"],
        };

        // Runtime assertions double the pin (the values round-trip through
        // the exact pre-RC types — string / Audience / DateTimeOffset /
        // PostStatus / IReadOnlyList<string>).
        Assert.Equal("r7-post", post.Id);
        Assert.Equal(ComponentId, post.ComponentId);
        Assert.Equal(Actor, post.AuthorId);
        Assert.Equal("Title", post.Title);
        Assert.NotNull(post.Body);
        Assert.IsType<Audience>(post.Audience);
        Assert.IsType<DateTimeOffset>(post.Created);
        Assert.Null(post.Modified);
        Assert.Equal(PostStatus.Active, post.Status);
        Assert.Equal(string.Empty, post.GroupId);
        Assert.Equal("en", post.LanguageCode);
        Assert.Null(post.DeletedAt);
        Assert.Equal(["aaaa", "bbbb"], post.ImageIds);
    }

    // ── harness (mirrors PostServiceTests) ────────────────────────────────

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
