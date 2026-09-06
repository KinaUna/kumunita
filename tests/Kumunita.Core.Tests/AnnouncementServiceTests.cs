using Kumunita.Core;
using Kumunita.Core.Announcements;
using Kumunita.Core.Identity;
using Kumunita.Core.Authorization;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <see cref="AnnouncementService"/> seam tests (M4, the "platform
/// announcements" lane). The shape follows <see cref="PostServiceTests"/> —
/// same <see cref="PostgresFixture"/>, same <c>BootStoreAsync</c>, same
/// <c>Plant</c> helper, fresh scratch Postgres per test method.
/// <para>
/// The pins this lane owns (see <see cref="AnnouncementService"/> for the
/// full ADR rationale):
/// </para>
/// <list type="number">
/// <item><b>The scope split</b> (see <see cref="AnnouncementScope"/>): a
///       <see cref="AnnouncementScope.Public"/> announcement is visible to
///       every visitor (authenticated <em>or</em> not) and authorable only
///       by a <see cref="Roles.GlobalAdmin"/>; a
///       <see cref="AnnouncementScope.Community"/> announcement is visible
///       to every signed-in user and authorable by a GlobalAdmin or a
///       <see cref="Roles.Moderator"/>. <see cref="AnnouncementService.CreateAsync"/>
///       enforces this split at the Core layer (defense-in-depth — the
///       ASP.NET gate narrows the author, the service pins the scope).</item>
/// <item><b>Not an <see cref="AccessAudit"/> subject.</b> Announcements are
///       not audience-restricted content, so there is no per-user decision
///       to log — the coarse role gate IS the whole decision. The list and
///       delete lanes emit no <c>AccessAudit</c> row (pinned below).</item>
/// <item><b>Read = flat public/audience split, never a <c>CanSeeAsync</c>
///       call.</b> <see cref="AnnouncementService.ListVisibleAsync"/> is a
///       single query filter on <c>Scope</c>; the <c>true</c> / <c>false</c>
///       argument is the caller's authenticated state from the Web layer,
///       not a principal's subject id.</item>
/// <item><b>Hard delete.</b> <see cref="AnnouncementService.DeleteAsync"/>
///       removes the document (there is no <see
///       cref="Posts.PostStatus"/>-shaped surface on this lane — a flat
///       public surface has no "re-appear" semantics to model).</item>
/// <item><b>C3 same-transaction write lane.</b> Every write goes through
///       the caller's in-flight <see cref="IDocumentSession"/> — the
///       <c>session.Store(...) → session.SaveChangesAsync()</c> shape is
///       the single commit; the <c>await using</c> in the caller (in the
///       Web layer: <c>IDocumentStore.LightweightSession()</c>) owns the
///       lifetime.</item>
/// </list>
/// </summary>
public class AnnouncementServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── List (read lane — the flat public/community split) ────────────────

    /// <summary>
    /// A visitor sees only the <see cref="AnnouncementScope.Public"/>
    /// announcements. A Community-scope announcement is never returned to
    /// an unauthenticated caller — regardless of how many of each are
    /// planted (the split pin on the read side).
    /// </summary>
    [Fact]
    public async Task ListVisible_Anonymous_OnlySeesPublic()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store);

        const string author = "u-author-public";
        await Plant(store, new Announcement
        {
            Id = "pub-1", Scope = AnnouncementScope.Public,
            Title = "Scheduled maintenance", Body = "Saturday 02:00–04:00 UTC",
            AuthorId = author, Created = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new Announcement
        {
            Id = "comm-1", Scope = AnnouncementScope.Community,
            Title = "Help us with…", Body = "Community event this weekend",
            AuthorId = author, Created = new DateTimeOffset(2026, 1, 14, 12, 0, 0, TimeSpan.Zero),
        });

        var visible = await svc.ListVisibleAsync(isAuthenticated: false);

        var ids = visible.Select(a => a.Id).ToHashSet();
        Assert.Contains("pub-1", ids);
        Assert.DoesNotContain("comm-1", ids);

        // The community-scope body is never *rendered* either (F2-pin
        // analog, the "never return a hidden post's fields" rule from M3):
        // assert on the returned collection, not just the id set.
        Assert.All(visible, a =>
        {
            Assert.NotEqual("Community event this weekend", a.Body);
        });
    }

    /// <summary>
    /// A resident sees the union of <see cref="AnnouncementScope.Public"/>
    /// + <see cref="AnnouncementScope.Community"/> (the two-way split on the
    /// read side: <c>Community</c> is only hidden from anonymous visitors,
    /// not from residents).
    /// </summary>
    [Fact]
    public async Task ListVisible_Authenticated_SeesBothScopes()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store);

        const string author = "u-author-both";
        await Plant(store, new Announcement
        {
            Id = "pub-2", Scope = AnnouncementScope.Public,
            Title = "Maintenance", Body = "body public",
            AuthorId = author, Created = new DateTimeOffset(2026, 1, 16, 12, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new Announcement
        {
            Id = "comm-2", Scope = AnnouncementScope.Community,
            Title = "Help us", Body = "body community",
            AuthorId = author, Created = new DateTimeOffset(2026, 1, 17, 12, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new Announcement
        {
            Id = "comm-3", Scope = AnnouncementScope.Community,
            Title = "Event", Body = "body community 2",
            AuthorId = author, Created = new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero),
        });

        var visible = await svc.ListVisibleAsync(isAuthenticated: true);

        var ids = visible.Select(a => a.Id).ToHashSet();
        Assert.Equal(new[] { "comm-2", "comm-3", "pub-2" }, ids.OrderBy(x => x).ToArray());
    }

    /// <summary>
    /// The read lane sorts by <c>Created</c> descending (latest first) —
    /// the <see cref="AnnouncementService.ListVisibleAsync"/> doc pin.
    /// Order stability across the two scopes is what the view model
    /// surfaces (the <c>Index</c> view renders the list verbatim).
    /// </summary>
    [Fact]
    public async Task ListVisible_SortedCreated_Descending()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store);

        const string author = "u-author-sort";
        // Plant in an order deliberately different from the expected sort
        // order, so the test would fail if the query returned insertion
        // order instead of <c>Created</c> desc.
        var c = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
        await Plant(store, new Announcement { Id = "mid",   Scope = AnnouncementScope.Public,    AuthorId = author, Body = "2026-01-11", Created = c.AddDays(1) });
        await Plant(store, new Announcement { Id = "old",   Scope = AnnouncementScope.Community, AuthorId = author, Body = "2026-01-09", Created = c.AddDays(-1) });
        await Plant(store, new Announcement { Id = "new",   Scope = AnnouncementScope.Public,    AuthorId = author, Body = "2026-01-12", Created = c.AddDays(2) });
        await Plant(store, new Announcement { Id = "mid2",  Scope = AnnouncementScope.Community, AuthorId = author, Body = "2026-01-10", Created = c });

        var visible = await svc.ListVisibleAsync(isAuthenticated: true);

        var createdOrder = visible.Select(a => a.Created).ToArray();
        Assert.Equal(createdOrder.OrderByDescending(x => x), createdOrder);
        // And pin the id order (deterministic, given the distinct Created
        // timestamps we planted): new → mid → mid2 → old.
        Assert.Equal(new[] { "new", "mid", "mid2", "old" }, visible.Select(a => a.Id).ToArray());
    }

    /// <summary>
    /// The no-announcement state returns an <see cref="Empty{T}"/> list
    /// (not null) — the view renders "No announcements yet." cleanly.
    /// </summary>
    [Fact]
    public async Task ListVisible_NoDocuments_ReturnsEmptyList_NotNull()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store);

        var visible = await svc.ListVisibleAsync(isAuthenticated: true);

        Assert.NotNull(visible);
        Assert.Empty(visible);
    }

    /// <summary>
    /// The read lane is a *view-model filter* (a single query over
    /// <c>Scope</c>), not a call through <see cref="IAuthorizationService"/>.
    /// An unauthenticated visitor listing their visible announcements emits
    /// no <see cref="AccessAudit"/> row at all — the split gate is the whole
    /// decision, and the flat public/community split has no per-user
    /// "who / via / outcome" to log the <c>AccessAudit</c> lane was built
    /// for (C1's empty-audience-denies is an audience-restricted invariant;
    /// announcements are *not* audience-restricted content, so C1 does not
    /// apply and there is nothing for the audit lane to record).
    /// </summary>
    [Fact]
    public async Task ListVisible_NoAuditRow_Emitted()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store);

        const string author = "u-author-noaudit";
        await Plant(store, new Announcement
        {
            Id = "pub-noaudit", Scope = AnnouncementScope.Public,
            Title = "No-audit-pin", Body = "body", AuthorId = author,
            Created = DateTimeOffset.UtcNow,
        });

        // Two read calls — anonymous and resident — to pin that the *read*
        // lane emits nothing for either.
        await svc.ListVisibleAsync(isAuthenticated: false);
        await svc.ListVisibleAsync(isAuthenticated: true);

        var rows = await AuditRows(store);
        Assert.Empty(rows);
    }

    // ── Create (the scope-vs-role split, the single write surface) ─────────

    /// <summary>
    /// GlobalAdmin creates a <see cref="AnnouncementScope.Public"/>
    /// announcement: the split allows it. The row is persisted with the
    /// author's <c>AuthorId</c>, the body/title verbatim, and the
    /// <see cref="AnnouncementScope"/> as chosen by the caller (the
    /// "author's choice verbatim" shape mirrors <see cref="Posts.Post"/> —
    /// the service never mutates <c>Scope</c> based on the caller's role;
    /// it only <em>refuses</em> a caller whose role does not allow the
    /// scope).
    /// </summary>
    [Fact]
    public async Task Create_Public_AsGlobalAdmin_Persists()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store);

        const string actor = "u-admin-public";
        await using var session = newSession(store);
        var created = await svc.CreateAsync(
            new Announcement { Scope = AnnouncementScope.Public, Title = "Maintenance", Body = "Sat 02:00 UTC" },
            actorId: actor,
            authorRoles: new HashSet<string> { Roles.GlobalAdmin },
            session);

        Assert.NotEmpty(created.Id);
        Assert.Equal(actor, created.AuthorId);
        Assert.Equal(AnnouncementScope.Public, created.Scope);
        Assert.Equal("Maintenance", created.Title);

        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<Announcement>(created.Id);
        Assert.NotNull(stored);
        Assert.Equal("Sat 02:00 UTC", stored!.Body);
        Assert.Equal(AnnouncementScope.Public, stored.Scope);
    }

    /// <summary>
    /// The split pin, moderator case: a Moderator cannot create a
    /// <see cref="AnnouncementScope.Public"/> announcement even if they
    /// somehow submit one. The call throws <see
    /// cref="UnauthorizedAccessException"/>, and the document is NOT
    /// persisted (the service throws *before* the <c>Store</c> call, so
    /// the row lands nowhere).
    /// </summary>
    [Fact]
    public async Task Create_Public_AsModerator_Denied_NotPersisted()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store);

        const string actor = "u-moderator-public";
        await using var session = newSession(store);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(
                new Announcement { Scope = AnnouncementScope.Public, Title = "Should not exist", Body = "x" },
                actorId: actor,
                authorRoles: new HashSet<string> { Roles.Moderator },
                session));

        // The "not persisted" assertion — the split pin's most important
        // observable: the write is *not executed*, not just refused at
        // the UI.
        await using var q = store.QuerySession();
        var count = await q.Query<Announcement>().CountAsync();
        Assert.Equal(0, count);
    }

    /// <summary>
    /// The split pin, a third actor: a verified resident (<c>Member</c>)
    /// cannot create a <see cref="AnnouncementScope.Public"/> announcement
    /// either. Symmetric to the Moderator case — the split allows
    /// <c>Public</c> only for the <see cref="Roles.GlobalAdmin"/> role
    /// string, not for the broader authenticated set (a <c>Member</c> is
    /// *verified*, which is a different claim axis than the
    /// <c>GlobalAdmin</c> role).
    /// </summary>
    [Fact]
    public async Task Create_Public_AsMember_Denied()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store);

        const string actor = "u-member-public";
        await using var session = newSession(store);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(
                new Announcement { Scope = AnnouncementScope.Public, Body = "x" },
                actorId: actor,
                authorRoles: new HashSet<string> { Roles.Member },
                session));

        await using var q = store.QuerySession();
        var count = await q.Query<Announcement>().CountAsync();
        Assert.Equal(0, count);
    }

    /// <summary>
    /// GlobalAdmin creating a <see cref="AnnouncementScope.Community"/>
    /// announcement: the split allows it — for a GlobalAdmin both scope
    /// values are valid authors, and the service never mutates the scope
    /// (a Moderator's scope would only be allowed if the scope were
    /// Community; a GlobalAdmin's can be either, and the service honors
    /// the caller's choice).
    /// </summary>
    [Fact]
    public async Task Create_Community_AsGlobalAdmin_Persists()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store);

        const string actor = "u-admin-community";
        await using var session = newSession(store);
        var created = await svc.CreateAsync(
            new Announcement { Scope = AnnouncementScope.Community, Title = "Help us", Body = "x" },
            actorId: actor,
            authorRoles: new HashSet<string> { Roles.GlobalAdmin },
            session);

        Assert.Equal(AnnouncementScope.Community, created.Scope);

        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<Announcement>(created.Id);
        Assert.Equal(AnnouncementScope.Community, stored!.Scope);
    }

    /// <summary>
    /// The split pin, moderator case, Community scope: a Moderator CAN
    /// create a <see cref="AnnouncementScope.Community"/> announcement —
    /// Community is the *only* scope a Moderator may author, and this is
    /// exactly where that split is exercisable at the Core layer (the
    /// moderator's write lane for "help us with X" calls).
    /// </summary>
    [Fact]
    public async Task Create_Community_AsModerator_Persists()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store);

        const string actor = "u-moderator-community";
        await using var session = newSession(store);
        var created = await svc.CreateAsync(
            new Announcement { Scope = AnnouncementScope.Community, Title = "Community event", Body = "x" },
            actorId: actor,
            authorRoles: new HashSet<string> { Roles.Moderator },
            session);

        Assert.Equal(AnnouncementScope.Community, created.Scope);
        Assert.Equal(actor, created.AuthorId);

        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<Announcement>(created.Id);
        Assert.Equal(AnnouncementScope.Community, stored!.Scope);
    }

    /// <summary>
    /// The split pin, a third actor, Community scope: a <c>Member</c>
    /// cannot create a Community-scope announcement either (the "help us"
    /// lane is for moderators/admins, not for verified residents).
    /// Symmetric with the <see cref="Create_Public_AsMember_Denied"/>
    /// pin.
    /// </summary>
    [Fact]
    public async Task Create_Community_AsMember_Denied()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store);

        const string actor = "u-member-community";
        await using var session = newSession(store);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(
                new Announcement { Scope = AnnouncementScope.Community, Body = "x" },
                actorId: actor,
                authorRoles: new HashSet<string> { Roles.Member },
                session));

        await using var q = store.QuerySession();
        var count = await q.Query<Announcement>().CountAsync();
        Assert.Equal(0, count);
    }

    /// <summary>
    /// The split pin, empty role set: a caller with <em>no roles at all</em>
    /// (a blocked account's claim shape, or a mis-shaped principal) cannot
    /// create <em>either</em> scope. This is the "defense-in-depth" pin —
    /// even though the [Authorize] gate should have stopped the unauthed
    /// request, the service still refuses.
    /// </summary>
    [Fact]
    public async Task Create_EmptyRoleSet_Denied_EitherScope()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store);
        const string actor = "u-no-roles";

        await using var session = newSession(store);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(
                new Announcement { Scope = AnnouncementScope.Public, Body = "x" },
                actorId: actor,
                authorRoles: new HashSet<string>(),
                session));

        await using var session2 = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(
                new Announcement { Scope = AnnouncementScope.Community, Body = "x" },
                actorId: actor,
                authorRoles: new HashSet<string>(),
                session2));

        await using var q = store.QuerySession();
        var count = await q.Query<Announcement>().CountAsync();
        Assert.Equal(0, count);
    }

    // ── Delete (the hard-delete lane) ──────────────────────────────────────

    /// <summary>
    /// Deleting an existing <see cref="Announcement"/> removes it — the
    /// next <see cref="AnnouncementService.ListVisibleAsync"/> is
    /// empty, and <c>LoadAsync</c> returns null. The hard-delete pin
    /// (no soft-hidden state on this lane).
    /// </summary>
    [Fact]
    public async Task Delete_Existing_RemovesFromStore()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store);

        const string author = "u-author-delete";
        await Plant(store, new Announcement
        {
            Id = "delete-me", Scope = AnnouncementScope.Public,
            Title = "Doomed", Body = "body", AuthorId = author,
            Created = DateTimeOffset.UtcNow,
        });

        await using var session = newSession(store);
        await svc.DeleteAsync("delete-me", session);

        await using var q = store.QuerySession();
        Assert.Null(await q.LoadAsync<Announcement>("delete-me"));
        var count = await q.Query<Announcement>().CountAsync();
        Assert.Equal(0, count);
    }

    /// <summary>
    /// Deleting a missing id is a <see cref="KeyNotFoundException"/> (the
    /// Web layer maps that to a 404). The missing-id case is
    /// "not a partial state": the caller's in-flight session is not
    /// committed, but the caller is expected to dispose/rollback on the
    /// exception anyway. The service's contract is the exception type.
    /// </summary>
    [Fact]
    public async Task Delete_MissingId_ThrowsKeyNotFound()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store);

        await using var session = newSession(store);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.DeleteAsync("no-such-id", session));
    }

    // ── Shared helpers ─────────────────────────────────────────────────────

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            // M1DocTypes registers AccessAudit (the read lane's "no audit
            // row" pin needs it in the schema to query).
            M1DocTypes.Configure(opts);
            // M4DocTypes registers the Announcements bounded context's
            // document (the whole point of these tests).
            M4DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>Plant a document row directly (test fixture seeding — the
    /// same "read lane tests seed data directly" shape as
    /// <see cref="PostServiceTests.Plant"/>).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    /// <summary>Open a fresh session (the caller's in-flight session for
    /// the write lane). The test's <c>await using</c> owns the lifetime
    /// — the service never disposes the caller's session (C3: the
    /// service commits via <c>session.SaveChangesAsync()</c> inside the
    /// caller's transaction); the caller of <c>ListVisibleAsync</c> is
    /// <c>await using</c>ed too (the service's internal
    /// <c>IDocumentStore.QuerySession()</c>). Marten's
    /// <c>IDocumentSession</c> implements <c>IAsyncDisposable</c>
    /// directly, so <c>await using var s = newSession(store);</c> is
    /// valid.</summary>
    private static IDocumentSession newSession(IDocumentStore store)
        => store.OpenSession(new Marten.Services.SessionOptions());

    /// <summary>Query the AccessAudit lane for the "no audit row" pin.
    /// The read lane and delete lane on <see cref="Announcement"/> are
    /// expected to emit nothing here — so this helper's job is to prove
    /// it's empty. <see cref="Marten.IDocumentQuerySession"/> is
    /// <c>IAsyncDisposable</c>, so <c>await using</c> closes the session.</summary>
    private static async Task<IReadOnlyList<AccessAudit>> AuditRows(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>().ToListAsync(ct);
    }
}
