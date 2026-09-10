using Kumunita.Core;
using Kumunita.Core.Announcements;
using Kumunita.Core.Identity;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
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
///       <see cref="Roles.Moderator"/>. A Community-scoped announcement may
///       additionally <em>target</em> one community (<see cref="Announcement.CommunityId"/>):
///       it is then visible only to that community's members, its
///       moderators, or a GlobalAdmin, and authorable by that community's
///       <c>moderator:{id}</c> standing (or a GlobalAdmin). <see
///       cref="AnnouncementService.CreateAsync"/> enforces this split at the
///       Core layer (defense-in-depth — the ASP.NET gate narrows the
///       author, the service pins the scope <em>and</em> the target).</item>
/// <item><b>Not an <see cref="AccessAudit"/> subject.</b> Announcements are
///       not audience-restricted content, so there is no per-user decision
///       to log — the coarse role gate IS the whole decision. The list and
///       delete lanes emit no <c>AccessAudit</c> row (pinned below).</item>
/// <item><b>Read = a coarse role/standing filter, never a <c>CanSeeAsync</c>
///       call.</b> <see cref="AnnouncementService.ListVisibleAsync"/> is a
///       single query filter on <c>Scope</c> / <see cref="Announcement.CommunityId"/>;
///       the <c>actorId</c> + <c>roles</c> arguments are the caller's subject
///       id and role set from the Web layer principal — the membership set
///       is resolved through the frozen <see cref="IUserInfoService"/> seam
///       (no <see cref="IAuthorizationService"/> decision).</item>
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
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

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

        var visible = await svc.ListVisibleAsync(null, new HashSet<string>());

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
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

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

        var visible = await svc.ListVisibleAsync("u-resident", new HashSet<string> { Roles.Member });

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
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        const string author = "u-author-sort";
        // Plant in an order deliberately different from the expected sort
        // order, so the test would fail if the query returned insertion
        // order instead of <c>Created</c> desc.
        var c = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
        await Plant(store, new Announcement { Id = "mid",   Scope = AnnouncementScope.Public,    AuthorId = author, Body = "2026-01-11", Created = c.AddDays(1) });
        await Plant(store, new Announcement { Id = "old",   Scope = AnnouncementScope.Community, AuthorId = author, Body = "2026-01-09", Created = c.AddDays(-1) });
        await Plant(store, new Announcement { Id = "new",   Scope = AnnouncementScope.Public,    AuthorId = author, Body = "2026-01-12", Created = c.AddDays(2) });
        await Plant(store, new Announcement { Id = "mid2",  Scope = AnnouncementScope.Community, AuthorId = author, Body = "2026-01-10", Created = c });

        var visible = await svc.ListVisibleAsync("u-resident", new HashSet<string> { Roles.Member });

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
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        var visible = await svc.ListVisibleAsync("u-resident", new HashSet<string> { Roles.Member });

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
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        const string author = "u-author-noaudit";
        await Plant(store, new Announcement
        {
            Id = "pub-noaudit", Scope = AnnouncementScope.Public,
            Title = "No-audit-pin", Body = "body", AuthorId = author,
            Created = DateTimeOffset.UtcNow,
        });

        // Two read calls — anonymous and resident — to pin that the *read*
        // lane emits nothing for either.
        await svc.ListVisibleAsync(null, new HashSet<string>());
        await svc.ListVisibleAsync("u-resident", new HashSet<string> { Roles.Member });

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
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

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
        var stored = await q.LoadAsync<Announcement>(created.Id, TestContext.Current.CancellationToken);
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
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

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
        var count = await q.Query<Announcement>().CountAsync(TestContext.Current.CancellationToken);
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
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        const string actor = "u-member-public";
        await using var session = newSession(store);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(
                new Announcement { Scope = AnnouncementScope.Public, Body = "x" },
                actorId: actor,
                authorRoles: new HashSet<string> { Roles.Member },
                session));

        await using var q = store.QuerySession();
        var count = await q.Query<Announcement>().CountAsync(TestContext.Current.CancellationToken);
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
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        const string actor = "u-admin-community";
        await using var session = newSession(store);
        var created = await svc.CreateAsync(
            new Announcement { Scope = AnnouncementScope.Community, Title = "Help us", Body = "x" },
            actorId: actor,
            authorRoles: new HashSet<string> { Roles.GlobalAdmin },
            session);

        Assert.Equal(AnnouncementScope.Community, created.Scope);

        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<Announcement>(created.Id, TestContext.Current.CancellationToken);
        Assert.Equal(AnnouncementScope.Community, stored!.Scope);
    }

    /// <summary>
    /// The split pin, moderator case, Community scope
    /// create a <see cref="AnnouncementScope.Community"/> announcement —
    /// Community is the *only* scope a Moderator may author, and this is
    /// exactly where that split is exercisable at the Core layer (the
    /// moderator's write lane for "help us with X" calls).
    /// </summary>
    [Fact]
    public async Task Create_Community_AsModerator_Persists()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

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
        var stored = await q.LoadAsync<Announcement>(created.Id, TestContext.Current.CancellationToken);
        Assert.Equal(AnnouncementScope.Community, stored!.Scope);
    }

    /// <summary>
    /// The split pin, a third actor, Community scope
    /// cannot create a Community-scope announcement either (the "help us"
    /// lane is for moderators/admins, not for verified residents).
    /// Symmetric with the <see cref="Create_Public_AsMember_Denied"/>
    /// pin.
    /// </summary>
    [Fact]
    public async Task Create_Community_AsMember_Denied()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        const string actor = "u-member-community";
        await using var session = newSession(store);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(
                new Announcement { Scope = AnnouncementScope.Community, Body = "x" },
                actorId: actor,
                authorRoles: new HashSet<string> { Roles.Member },
                session));

        await using var q = store.QuerySession();
        var count = await q.Query<Announcement>().CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }

    /// <summary>
    /// The split pin, empty role set
    /// (a blocked account's claim shape, or a mis-shaped principal) cannot
    /// create <em>either</em> scope. This is the "defense-in-depth" pin —
    /// even though the [Authorize] gate should have stopped the unauthed
    /// request, the service still refuses.
    /// </summary>
    [Fact]
    public async Task Create_EmptyRoleSet_Denied_EitherScope()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);
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
        var count = await q.Query<Announcement>().CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }

    // ── Delete (the hard-delete lane)

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
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

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
        Assert.Null(await q.LoadAsync<Announcement>("delete-me", TestContext.Current.CancellationToken));
        var count = await q.Query<Announcement>().CountAsync(TestContext.Current.CancellationToken);
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
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        await using var session = newSession(store);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.DeleteAsync("no-such-id", session));
    }

    // ── Update (edit lane — the same scope-vs-role split, applied to the edited scope) ──

    /// <summary>
    /// The happy pin: a GlobalAdmin editing a public-scope announcement
    /// persists the new Title/Body/Scope. <see cref="Announcement.AuthorId"/>
    /// and <see cref="Announcement.Created"/> are preserved untouched (the
    /// author of record is whoever created it, not whoever edited it), and
    /// <see cref="Announcement.Modified"/> is stamped (the observable "this
    /// was edited after creation" state).
    /// </summary>
    [Fact]
    public async Task Update_Public_AsGlobalAdmin_Persists_KeepsAuthorAndCreated_StampedModified()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        const string originalAuthor = "u-author-original";
        var created = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        await Plant(store, new Announcement
        {
            Id = "edit-pub", Scope = AnnouncementScope.Public,
            Title = "Old title", Body = "Old body", AuthorId = originalAuthor,
            Created = created,
        });

        await using var session = newSession(store);
        var updated = await svc.UpdateAsync(
            new Announcement { Id = "edit-pub", Scope = AnnouncementScope.Public, Title = "New title", Body = "New body" },
            actorId: "u-admin-editor",
            actorRoles: new HashSet<string> { Roles.GlobalAdmin },
            session);

        Assert.Equal(originalAuthor, updated.AuthorId);
        Assert.Equal(created, updated.Created);
        Assert.Equal("New title", updated.Title);
        Assert.Equal("New body", updated.Body);
        Assert.NotNull(updated.Modified);

        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<Announcement>("edit-pub", TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal("New title", stored!.Title);
        Assert.Equal(originalAuthor, stored.AuthorId);
        Assert.Equal(created, stored.Created);
        Assert.NotNull(stored.Modified);
    }

    /// <summary>
    /// The split pin, moderator case: a Moderator editing a
    /// <see cref="AnnouncementScope.Public"/> announcement throws
    /// <see cref="UnauthorizedAccessException"/> and NOTHING is written: the
    /// split check runs before the write, so the store's version still reads
    /// back untouched (the "not persisted" observable, same shape as the
    /// <see cref="CreateAsync"/> moderator-denied pin).
    /// </summary>
    [Fact]
    public async Task Update_ToPublic_AsModerator_Denied_NotPersisted()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        const string author = "u-author-public-denied";
        await Plant(store, new Announcement
        {
            Id = "edit-denied", Scope = AnnouncementScope.Community,
            Title = "Still community", Body = "body", AuthorId = author,
            Created = DateTimeOffset.UtcNow,
        });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UpdateAsync(
                new Announcement { Id = "edit-denied", Scope = AnnouncementScope.Public, Title = "Escalated?", Body = "x" },
                actorId: "u-moderator-editor",
                actorRoles: new HashSet<string> { Roles.Moderator },
                session));

        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<Announcement>("edit-denied", TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal(AnnouncementScope.Community, stored!.Scope);
        Assert.Equal("Still community", stored.Title);
    }

    /// <summary>
    /// The happy pin, community scope: a Moderator may edit a
    /// <see cref="AnnouncementScope.Community"/> announcement (the split's
    /// community-side lane — the same split <see cref="CreateAsync"/>
    /// enforces for a community-scope create).
    /// </summary>
    [Fact]
    public async Task Update_Community_AsModerator_Persists()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        const string author = "u-author-community";
        await Plant(store, new Announcement
        {
            Id = "edit-comm", Scope = AnnouncementScope.Community,
            Title = "Old", Body = "Old body", AuthorId = author,
            Created = DateTimeOffset.UtcNow,
        });

        await using var session = newSession(store);
        var updated = await svc.UpdateAsync(
            new Announcement { Id = "edit-comm", Scope = AnnouncementScope.Community, Title = "Updated", Body = "New body" },
            actorId: "u-moderator-editor-community",
            actorRoles: new HashSet<string> { Roles.Moderator },
            session);

        Assert.Equal("Updated", updated.Title);
        Assert.Equal("New body", updated.Body);
        Assert.Equal(AnnouncementScope.Community, updated.Scope);

        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<Announcement>("edit-comm", TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal("Updated", stored!.Title);
        Assert.Equal(AnnouncementScope.Community, stored.Scope);
    }

    /// <summary>
    /// Editing a missing id is a <see cref="KeyNotFoundException"/> (the
    /// Web layer maps that to a 404) — the same contract
    /// <see cref="DeleteAsync"/> pins for a missing id, on the write lane.
    /// </summary>
    [Fact]
    public async Task Update_MissingId_ThrowsKeyNotFound()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        await using var session = newSession(store);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.UpdateAsync(
                new Announcement { Id = "no-such-id", Scope = AnnouncementScope.Public, Title = "x", Body = "x" },
                actorId: "u-admin-missing",
                actorRoles: new HashSet<string> { Roles.GlobalAdmin },
                session));
    }

    /// <summary>
    /// No-op re-save (Title/Body/Scope all unchanged) must NOT stamp
    /// <see cref="Announcement.Modified"/> — the "edited after creation"
    /// state is meaningful only when a value actually changed, so a
    /// resubmit without change leaves the row's stamp exactly where the
    /// last real edit put it (null, if none yet).
    /// </summary>
    [Fact]
    public async Task Update_NoOp_DoesNotStampModified()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        const string author = "u-author-noop";
        await Plant(store, new Announcement
        {
            Id = "edit-noop", Scope = AnnouncementScope.Public,
            Title = "Same", Body = "same body", AuthorId = author,
            Created = DateTimeOffset.UtcNow,
        });

        await using var session = newSession(store);
        var updated = await svc.UpdateAsync(
            new Announcement { Id = "edit-noop", Scope = AnnouncementScope.Public, Title = "Same", Body = "same body" },
            actorId: "u-admin-noop",
            actorRoles: new HashSet<string> { Roles.GlobalAdmin },
            session);

        Assert.Null(updated.Modified);

        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<Announcement>("edit-noop", TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Null(stored!.Modified);
    }

    // ── Pinned (the site-wide banner lane — the visibility pin) ────────────

    /// <summary>
    /// The pinned lane is the same two-way split as the normal list, but
    /// narrowed to <see cref="Announcement.Pinned"/> = true. An anonymous
    /// visitor sees at most one pinned <see cref="AnnouncementScope.Public"/>
    /// announcement — pinned <see cref="AnnouncementScope.Community"/> rows
    /// are invisible to anonymous callers (the split pin, the same reason
    /// <see cref="ListVisible_Anonymous_OnlySeesPublic"/> above applies).
    /// </summary>
    [Fact]
    public async Task Pinned_Anonymous_ReturnsOnlyPinnedPublic()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        const string author = "u-author-pinned-visit";
        var c = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        await Plant(store, new Announcement
        {
            Id = "pin-pub", Scope = AnnouncementScope.Public, Pinned = true,
            Title = "Maintenance", Body = "Sat 02:00 UTC", AuthorId = author,
            Created = c,
        });
        await Plant(store, new Announcement
        {
            Id = "pin-comm", Scope = AnnouncementScope.Community, Pinned = true,
            Title = "Resident event", Body = "this weekend", AuthorId = author,
            Created = c.AddDays(1),
        });
        await Plant(store, new Announcement
        {
            // Pinned row in the Community scope, but a different scope than an
            // anonymous viewer is allowed to see: must be invisible.
            Id = "pin-comm-2", Scope = AnnouncementScope.Community, Pinned = true,
            Title = "Volunteers", Body = "needed", AuthorId = author,
            Created = c.AddDays(2),
        });

        var pinned = await svc.PinnedAsync(null, new HashSet<string>());

        Assert.NotNull(pinned);
        Assert.Equal("pin-pub", pinned!.Id);
        Assert.Equal(AnnouncementScope.Public, pinned.Scope);
    }

    /// <summary>
    /// A signed-in caller sees the most-recently-created pinned announcement
    /// across both scopes (the "most-recently-created wins" rule, applied to
    /// the union). A newer Community pin trumps an older Public pin even
    /// though the Public one is visible more broadly by virtue of its scope.
    /// </summary>
    [Fact]
    public async Task Pinned_Authenticated_MostRecentlyCreatedWinsAcrossScopes()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        const string author = "u-author-pinned-auth";
        var c = new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero);
        await Plant(store, new Announcement
        {
            Id = "older-pub", Scope = AnnouncementScope.Public, Pinned = true,
            Title = "Scheduled maintenance", Body = "Sat 02:00 UTC",
            AuthorId = author, Created = c,
        });
        await Plant(store, new Announcement
        {
            Id = "older-comm", Scope = AnnouncementScope.Community, Pinned = true,
            Title = "Help us with X", Body = "this weekend",
            AuthorId = author, Created = c.AddDays(1),
        });
        await Plant(store, new Announcement
        {
            Id = "newer-comm", Scope = AnnouncementScope.Community, Pinned = true,
            Title = "New event", Body = "this weekend",
            AuthorId = author, Created = c.AddDays(2),
        });

        var pinned = await svc.PinnedAsync("u-resident", new HashSet<string> { Roles.Member });

        Assert.NotNull(pinned);
        Assert.Equal("newer-comm", pinned!.Id);

        // Spot-check: had the query returned only the anonymous-visible set,
        // the anonymous call would have returned "older-pub" (newest Public)
        // — the Community pin "newer-comm" is the correct answer for a
        // signed-in caller, proving both scopes are on the table.
        var anonymousPinned = await svc.PinnedAsync(null, new HashSet<string>());
        Assert.NotNull(anonymousPinned);
        Assert.Equal("older-pub", anonymousPinned!.Id);
    }

    /// <summary>
    /// The empty state: no announcement is pinned. Returns null (the Web
    /// layer uses null to skip the banner). This is not the same shape as
    /// <see cref="ListVisible_NoDocuments_ReturnsEmptyList_NotNull"/> — the
    /// pinned lane is single-result, so its empty sentinel is null.
    /// </summary>
    [Fact]
    public async Task Pinned_NoPinnedAnnouncements_ReturnsNull()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        // Plant two non-pinned (Pinned = false) announcements: the pinned
        // lane must return null even though other announcements exist.
        const string author = "u-author-no-pin";
        var c = new DateTimeOffset(2026, 2, 3, 0, 0, 0, TimeSpan.Zero);
        await Plant(store, new Announcement
        {
            Id = "unpin-pub", Scope = AnnouncementScope.Public, Pinned = false,
            Title = "Maintenance", Body = "Sat 02:00 UTC", AuthorId = author,
            Created = c,
        });
        await Plant(store, new Announcement
        {
            Id = "unpin-comm", Scope = AnnouncementScope.Community, Pinned = false,
            Title = "Resident call", Body = "help us", AuthorId = author,
            Created = c.AddDays(1),
        });

        var pinned = await svc.PinnedAsync("u-resident", new HashSet<string> { Roles.Member });

        Assert.Null(pinned);
    }

    /// <summary>
    /// The split pin on the read gate: an announcement that was pinned but
    /// later unpinned (Pinned=false) is not returned, regardless of scope or
    /// auth state. The pinned lane does not remember that it was *ever*
    /// pinned; only the current <see cref="Announcement.Pinned"/> state
    /// counts (there's no "re-appear" lane on this bounded context —
    /// mirroring the "hard delete" pin, no soft-hidden state surface).
    /// </summary>
    [Fact]
    public async Task Pinned_UnpinnedRows_AreNotReturned()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        const string author = "u-author-unpin";
        var c = new DateTimeOffset(2026, 2, 4, 0, 0, 0, TimeSpan.Zero);
        await Plant(store, new Announcement
        {
            Id = "unpin-pub-1", Scope = AnnouncementScope.Public, Pinned = false,
            Title = "Maintenance", Body = "Sat 02:00 UTC", AuthorId = author,
            Created = c,
        });
        await Plant(store, new Announcement
        {
            Id = "unpin-comm-1", Scope = AnnouncementScope.Community, Pinned = false,
            Title = "Resident call", Body = "help us", AuthorId = author,
            Created = c.AddDays(1),
        });

        Assert.Null(await svc.PinnedAsync(null, new HashSet<string>()));
        Assert.Null(await svc.PinnedAsync("u-resident", new HashSet<string> { Roles.Member }));
    }

    /// <summary>
    /// The scope split is applied to the pinned result itself (not just
    /// which rows are returned, but which scope the caller can see). An
    /// unauthenticated caller sees a pinned Public announcement but the
    /// pinned Community one is never surfaced — and vice versa, a
    /// signed-in caller sees the most-recently-created pinned announcement
    /// in either scope. This test pins the "scope of the returned doc
    /// matches the caller's auth state" shape.
    /// </summary>
    [Fact]
    public async Task Pinned_Anonymous_ScopeOfReturnedDoc_RespectsAuthGate()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        const string author = "u-author-scope";
        var c = new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero);
        await Plant(store, new Announcement
        {
            Id = "pin-pub-a", Scope = AnnouncementScope.Public, Pinned = true,
            Title = "Maintenance", Body = "Sat 02:00 UTC", AuthorId = author,
            Created = c,
        });
        await Plant(store, new Announcement
        {
            Id = "pin-comm-b", Scope = AnnouncementScope.Community, Pinned = true,
            Title = "Resident call", Body = "help us", AuthorId = author,
            Created = c.AddDays(1),
        });

        // Anonymous: only Public is visible. The most-recently-created
        // pinned announcement that passes the gate is "pin-pub-a" even
        // though "pin-comm-b" is newer (it's a Community pin).
        var anon = await svc.PinnedAsync(null, new HashSet<string>());
        Assert.NotNull(anon);
        Assert.Equal("pin-pub-a", anon!.Id);
        Assert.Equal(AnnouncementScope.Public, anon.Scope);

        // Signed-in: union of pinned rows, most-recently-created wins —
        // "pin-comm-b" even though it's Community scope.
        var auth = await svc.PinnedAsync("u-resident", new HashSet<string> { Roles.Member });
        Assert.NotNull(auth);
        Assert.Equal("pin-comm-b", auth!.Id);
        Assert.Equal(AnnouncementScope.Community, auth.Scope);
    }

    // ── Targeted announcements (CommunityId — one community instead of every resident) ──

    /// <summary>
    /// A <see cref="Announcement"/> with a <c>CommunityId</c> targets that
    /// community: visible to the community's <em>members</em>, its
    /// <em>moderators</em>, and a <see cref="Roles.GlobalAdmin"/> — and to no
    /// one else (a resident who is not in the target community, or an
    /// anonymous visitor, never sees it). The membership set is resolved
    /// through the frozen <see cref="IUserInfoService"/> seam
    /// (<c>GetCommunityIdsAsync</c>), the moderation standing through the
    /// caller's <c>moderator:{id}</c> role set — the <see
    /// cref="AnnouncementService.ListVisibleAsync"/> doc contract.
    /// </summary>
    [Fact]
    public async Task ListVisible_Targeted_VisibleToMembersAndAdminsOfTargetOnly()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        await Plant(store, new Component { Id = "community-A", Name = "Community A", Enabled = true });
        await Plant(store, new ComponentMembership
        {
            Id = "mm-a", ComponentId = "community-A", UserId = "u-member-A",
            AddedBy = "u-admin", At = DateTimeOffset.UtcNow,
        });
        await Plant(store, new Announcement
        {
            Id = "targeted-A", Scope = AnnouncementScope.Community, CommunityId = "community-A",
            Title = "Community A event", Body = "x", AuthorId = "u-moderator-A",
            Created = DateTimeOffset.UtcNow,
        });

        var ids = (IReadOnlyList<Announcement> visible) => visible.Select(a => a.Id).ToHashSet();

        // A member of the target community sees it.
        Assert.Contains("targeted-A", ids(await svc.ListVisibleAsync("u-member-A", new HashSet<string> { Roles.Member })));

        // A resident of another community does not — membership (not
        // authentication) is the read gate on a targeted row.
        Assert.DoesNotContain("targeted-A", ids(await svc.ListVisibleAsync("u-member-B", new HashSet<string> { Roles.Member })));

        // An anonymous visitor never sees it.
        Assert.DoesNotContain("targeted-A", ids(await svc.ListVisibleAsync(null, new HashSet<string>())));

        // A GlobalAdmin sees it regardless of membership.
        Assert.Contains("targeted-A", ids(await svc.ListVisibleAsync("u-admin-A", new HashSet<string> { Roles.GlobalAdmin })));
    }

    /// <summary>
    /// The moderator standing is target-specific: a
    /// <c>moderator:community-A</c> standing (no membership needed) makes a
    /// target-<c>community-A</c> row visible, while a
    /// <c>moderator:community-B</c> standing does not (the claim set carries
    /// <em>which</em> components the moderator governs — ADR 0003).
    /// </summary>
    [Fact]
    public async Task ListVisible_Targeted_ModeratorOfTargetSeesIt_OtherModeratorDoesNot()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        await Plant(store, new Component { Id = "community-A", Name = "Community A", Enabled = true });
        await Plant(store, new Component { Id = "community-B", Name = "Community B", Enabled = true });
        await Plant(store, new Announcement
        {
            Id = "targeted-A", Scope = AnnouncementScope.Community, CommunityId = "community-A",
            Title = "Community A event", Body = "x", AuthorId = "u-moderator-A",
            Created = DateTimeOffset.UtcNow,
        });

        var ids = (IReadOnlyList<Announcement> visible) => visible.Select(a => a.Id).ToHashSet();

        Assert.Contains("targeted-A",
            ids(await svc.ListVisibleAsync("u-moderator-A", new HashSet<string> { Roles.Moderator, Roles.ModeratorComponent("community-A") })));

        Assert.DoesNotContain("targeted-A",
            ids(await svc.ListVisibleAsync("u-moderator-B", new HashSet<string> { Roles.Moderator, Roles.ModeratorComponent("community-B") })));
    }

    /// <summary>
    /// The pinned lane honors the same target split: a pinned target-<c>community-A</c>
    /// announcement is the banner for that community's members (and a
    /// GlobalAdmin) and null for everyone else — the two-way split on the
    /// read side, applied to the targeted shape.
    /// </summary>
    [Fact]
    public async Task Pinned_Targeted_MemberOfTarget_SeesIt_OthersSeeNull()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        await Plant(store, new Component { Id = "community-A", Name = "Community A", Enabled = true });
        await Plant(store, new ComponentMembership
        {
            Id = "mm-a-pin", ComponentId = "community-A", UserId = "u-member-A",
            AddedBy = "u-admin", At = DateTimeOffset.UtcNow,
        });
        await Plant(store, new Announcement
        {
            Id = "pin-targeted-A", Scope = AnnouncementScope.Community, CommunityId = "community-A", Pinned = true,
            Title = "Community A pinned", Body = "x", AuthorId = "u-moderator-A",
            Created = DateTimeOffset.UtcNow,
        });

        var mine = await svc.PinnedAsync("u-member-A", new HashSet<string> { Roles.Member });
        Assert.NotNull(mine);
        Assert.Equal("pin-targeted-A", mine!.Id);

        var admin = await svc.PinnedAsync("u-admin-A", new HashSet<string> { Roles.GlobalAdmin });
        Assert.NotNull(admin);
        Assert.Equal("pin-targeted-A", admin!.Id);

        Assert.Null(await svc.PinnedAsync("u-member-B", new HashSet<string> { Roles.Member }));
        Assert.Null(await svc.PinnedAsync(null, new HashSet<string>()));
    }

    /// <summary>
    /// Write-lane pin: a community's own moderator (<see cref="Roles.Moderator"/>
    /// + the <c>moderator:community-A</c> standing) may author a
    /// target-<c>community-A</c> announcement. The stored document carries the
    /// <c>CommunityId</c> verbatim (the service never mutates authority —
    /// the same "author's choice verbatim" shape as the scope split).
    /// </summary>
    [Fact]
    public async Task Create_Targeted_ByModeratorOfTarget_Persists()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        await Plant(store, new Component { Id = "community-A", Name = "Community A", Enabled = true });

        await using var session = newSession(store);
        var created = await svc.CreateAsync(
            new Announcement { Scope = AnnouncementScope.Community, CommunityId = "community-A", Title = "Event", Body = "x" },
            actorId: "u-moderator-A",
            authorRoles: new HashSet<string> { Roles.Moderator, Roles.ModeratorComponent("community-A") },
            session);

        Assert.Equal("community-A", created.CommunityId);

        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<Announcement>(created.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal(AnnouncementScope.Community, stored!.Scope);
        Assert.Equal("community-A", stored.CommunityId);
    }

    /// <summary>
    /// A GlobalAdmin may target <em>any</em> community (the admin-lane pin on
    /// the targeted shape — symmetric with the scope split's admin-lane pin).
    /// </summary>
    [Fact]
    public async Task Create_Targeted_ByGlobalAdmin_Persists()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        await Plant(store, new Component { Id = "community-B", Name = "Community B", Enabled = true });

        await using var session = newSession(store);
        var created = await svc.CreateAsync(
            new Announcement { Scope = AnnouncementScope.Community, CommunityId = "community-B", Title = "Event", Body = "x" },
            actorId: "u-admin-A",
            authorRoles: new HashSet<string> { Roles.GlobalAdmin },
            session);

        Assert.Equal("community-B", created.CommunityId);
    }

    /// <summary>
    /// A plain <see cref="Roles.Moderator"/> — no <c>moderator:{id}</c>
    /// standing — cannot target a community even though they may use the
    /// flat "all residents" Community lane (<see
    /// cref="Create_Community_AsModerator_Persists"/>): the target pin is
    /// the standing claim, denied <see cref="UnauthorizedAccessException"/>,
    /// and the document is NOT persisted.
    /// </summary>
    [Fact]
    public async Task Create_Targeted_ByModeratorWithoutStandingClaim_Denied_NotPersisted()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        await Plant(store, new Component { Id = "community-A", Name = "Community A", Enabled = true });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(
                new Announcement { Scope = AnnouncementScope.Community, CommunityId = "community-A", Title = "x", Body = "x" },
                actorId: "u-moderator-unknown",
                authorRoles: new HashSet<string> { Roles.Moderator },
                session));

        await using var q = store.QuerySession();
        var count = await q.Query<Announcement>().CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }

    /// <summary>
    /// A moderator of a <em>different</em> community cannot target this one
    /// — the standing must name the exact <c>CommunityId</c> (a moderator
    /// claim is a scoped standing, not a global key).
    /// </summary>
    [Fact]
    public async Task Create_Targeted_ByModeratorOfOtherCommunity_Denied_NotPersisted()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        await Plant(store, new Component { Id = "community-A", Name = "Community A", Enabled = true });
        await Plant(store, new Component { Id = "community-B", Name = "Community B", Enabled = true });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(
                new Announcement { Scope = AnnouncementScope.Community, CommunityId = "community-A", Title = "x", Body = "x" },
                actorId: "u-moderator-B",
                authorRoles: new HashSet<string> { Roles.Moderator, Roles.ModeratorComponent("community-B") },
                session));

        await using var q = store.QuerySession();
        var count = await q.Query<Announcement>().CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }

    /// <summary>
    /// A target that names a <em>unknown</em> community (no
    /// <c>Component</c> row) is a <see cref="ArgumentException"/> (the Web
    /// layer maps that to a 400) — not a silent success or a 403. This
    /// guards against a <c>moderator:{bogus-id}</c> shape minted by a
    /// mis-shaped principal: the target must name a real, enabled
    /// functional component.
    /// </summary>
    [Fact]
    public async Task Create_Targeted_UnknownCommunity_ArgumentException()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        await using var session = newSession(store);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(
                new Announcement { Scope = AnnouncementScope.Community, CommunityId = "community-ghost", Title = "x", Body = "x" },
                actorId: "u-moderator-ghost",
                authorRoles: new HashSet<string> { Roles.Moderator, Roles.ModeratorComponent("community-ghost") },
                session));

        await using var q = store.QuerySession();
        var count = await q.Query<Announcement>().CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }

    /// <summary>
    /// A <see cref="AnnouncementScope.Public"/> announcement may not target
    /// a community (a public notice is, by definition, platform-wide — the
    /// <see cref="Announcement.CommunityId"/> doc pin): even a GlobalAdmin
    /// is refused a <c>Public</c> + <c>CommunityId</c> shape with an
    /// <see cref="ArgumentException"/> (a shape error, mapped to a 400 by
    /// the Web layer — not a 403 authorization denial).
    /// </summary>
    [Fact]
    public async Task Create_PublicScope_WithCommunityId_ArgumentException()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        await Plant(store, new Component { Id = "community-A", Name = "Community A", Enabled = true });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(
                new Announcement { Scope = AnnouncementScope.Public, CommunityId = "community-A", Title = "x", Body = "x" },
                actorId: "u-admin-A",
                authorRoles: new HashSet<string> { Roles.GlobalAdmin },
                session));

        await using var q = store.QuerySession();
        var count = await q.Query<Announcement>().CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }

    /// <summary>
    /// The edit lane re-validates the <em>edited</em> target: a
    /// target-<c>community-A</c> announcement stays editable by that
    /// community's moderator (the same standing pin as create, applied to
    /// the target shape) …
    /// </summary>
    [Fact]
    public async Task Update_Targeted_ByModeratorOfTarget_Persists()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        await Plant(store, new Component { Id = "community-A", Name = "Community A", Enabled = true });
        await Plant(store, new Announcement
        {
            Id = "edit-targeted-A", Scope = AnnouncementScope.Community, CommunityId = "community-A",
            Title = "Old", Body = "Old body", AuthorId = "u-moderator-A",
            Created = DateTimeOffset.UtcNow,
        });

        await using var session = newSession(store);
        var updated = await svc.UpdateAsync(
            new Announcement { Id = "edit-targeted-A", Scope = AnnouncementScope.Community, CommunityId = "community-A", Title = "New", Body = "New body" },
            actorId: "u-moderator-A",
            actorRoles: new HashSet<string> { Roles.Moderator, Roles.ModeratorComponent("community-A") },
            session);

        Assert.Equal("community-A", updated.CommunityId);

        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<Announcement>("edit-targeted-A", TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal("New", stored!.Title);
        Assert.Equal("community-A", stored.CommunityId);
    }

    /// <summary>
    /// … and a moderator whose standing names a <em>different</em> community
    /// cannot edit a target-<c>community-A</c> row: the edit split is
    /// re-checked against the row's current target, denied <see
    /// cref="UnauthorizedAccessException"/>, and nothing is written.
    /// </summary>
    [Fact]
    public async Task Update_Targeted_ByModeratorOfOtherCommunity_Denied_NotPersisted()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);

        await Plant(store, new Component { Id = "community-A", Name = "Community A", Enabled = true });
        await Plant(store, new Component { Id = "community-B", Name = "Community B", Enabled = true });
        await Plant(store, new Announcement
        {
            Id = "edit-denied-A", Scope = AnnouncementScope.Community, CommunityId = "community-A",
            Title = "Still A's", Body = "body", AuthorId = "u-moderator-A",
            Created = DateTimeOffset.UtcNow,
        });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UpdateAsync(
                new Announcement { Id = "edit-denied-A", Scope = AnnouncementScope.Community, CommunityId = "community-A", Title = "Hijacked?", Body = "x" },
                actorId: "u-moderator-B",
                actorRoles: new HashSet<string> { Roles.Moderator, Roles.ModeratorComponent("community-B") },
                session));

        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<Announcement>("edit-denied-A", TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal("Still A's", stored!.Title);
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
            // M3DocTypes registers Announcement (the M3b "platform announcements"
            // lane's bounded context — the whole point of these tests) alongside
            // Post / PostReply / Report.
            M3DocTypes.Configure(opts);
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
