using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0026 — the user-added group/community name/description translation
/// lane. Exercises the <see cref="UserInfoService"/> read + write seams the
/// ADR 0022 <see cref="PostTranslationTests"/> don't cover:
/// <see cref="UserInfoService.GetGroupTranslationsAsync"/> /
/// <see cref="UserInfoService.GetCommunityTranslationsAsync"/> (the "a read,
/// not a decision" surface — no audit row) and
/// <see cref="UserInfoService.AddGroupTranslationAsync"/> /
/// <see cref="UserInfoService.AddCommunityTranslationAsync"/> (the standing
/// gate — group: owner ∪ GlobalAdmin ∪ Translator; community: GlobalAdmin ∪
/// Translator — + its hand-written <c>AccessAudit</c> row) and the public
/// <see cref="UserInfoService.CanTranslateGroup"/> /
/// <see cref="UserInfoService.CanTranslateCommunity"/> display probes. Same
/// <see cref="PostgresFixture"/> / <c>BootStoreAsync</c> /
/// <c>UserInfoService</c> harness as the M3 tests.
/// </summary>
public class GroupCommunityTranslationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string GroupId = "a026-grp";
    private const string ComponentId = "a026-comp";

    private static IReadOnlySet<string> RoleSet(params string[] roles) =>
        new HashSet<string>(roles);

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

    private static UserInfoService Services(IDocumentStore store) => new(store);

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

    private static async Task RunInSession(IDocumentStore store, Func<IDocumentSession, Task> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        await action(session);
    }

    private static async Task<IReadOnlyList<AccessAudit>> AuditsFor(IDocumentStore store, string action)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>().Where(a => a.Action == action).ToListAsync(ct);
    }

    private static Group NewGroup(string ownerId) => new()
    {
        Id = GroupId,
        Name = "Bike owners",
        OwnerId = ownerId,
        Created = DateTimeOffset.UtcNow,
    };

    private static Component NewComponent() => new()
    {
        Id = ComponentId,
        Name = "The neighborhood",
        Enabled = true,
    };

    // ── Read seams: a "read, not a decision" surface ────────────────────────

    [Fact]
    public async Task GetGroupTranslations_ReturnsRowsForGroup_Only()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string author = "u-a026-owner";

        await Plant(store, NewGroup(author));
        await Plant(store, new GroupTranslation
        {
            Id = "a026-t1", GroupId = GroupId, LanguageCode = "pl",
            Name = "Wolontariusze rowerów", AuthorId = author, Created = DateTimeOffset.UtcNow
        });
        await Plant(store, new GroupTranslation
        {
            Id = "a026-t2", GroupId = GroupId, LanguageCode = "fr",
            Description = "cours", AuthorId = author, Created = DateTimeOffset.UtcNow
        });
        // A translation under a *different* group must not leak in.
        await Plant(store, new GroupTranslation
        {
            Id = "a026-other", GroupId = "a026-other-grp", LanguageCode = "pl",
            Name = "elsewhere", AuthorId = author, Created = DateTimeOffset.UtcNow
        });

        var rows = await svc.GetGroupTranslationsAsync(GroupId);
        var codes = rows.Select(t => t.LanguageCode).OrderBy(c => c).ToList();
        Assert.Equal(new[] { "fr", "pl" }, codes);

        // The read seam writes **no** audit row (the ADR 0022 "a read, not a
        // decision" pin carried over).
        Assert.Empty(await AuditsFor(store, "grouptranslation.add"));
    }

    [Fact]
    public async Task GetCommunityTranslations_ReturnsRowsForComponent_Only()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string author = "u-a026-admin";

        await Plant(store, NewComponent());
        await Plant(store, new CommunityTranslation
        {
            Id = "a026-c1", ComponentId = ComponentId, LanguageCode = "pl",
            Name = "Osiedle", AuthorId = author, Created = DateTimeOffset.UtcNow
        });
        await Plant(store, new CommunityTranslation
        {
            Id = "a026-c2", ComponentId = "a026-other-comp", LanguageCode = "pl",
            Name = "elsewhere", AuthorId = author, Created = DateTimeOffset.UtcNow
        });

        var rows = await svc.GetCommunityTranslationsAsync(ComponentId);
        var codes = rows.Select(t => t.LanguageCode).OrderBy(c => c).ToList();
        Assert.Equal(new[] { "pl" }, codes);

        Assert.Empty(await AuditsFor(store, "communitytranslation.add"));
    }

    // ── Group write seam: standing + audit row ──────────────────────────────

    [Fact]
    public async Task AddGroupTranslation_Owner_Allows_ViaOwner()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string owner = "u-a026-owner";

        await Plant(store, NewGroup(owner));

        var created = await RunInSession(store, s =>
            svc.AddGroupTranslationAsync(GroupId, "pl", "Wolontariusze rowerów", null, owner, RoleSet(), s));

        Assert.Equal("pl", created.LanguageCode);
        Assert.Equal("Wolontariusze rowerów", created.Name);
        Assert.Null(created.Description); // the at-least-one rule: name alone is enough

        var rows = await svc.GetGroupTranslationsAsync(GroupId);
        Assert.Single(rows);

        var audit = Assert.Single(await AuditsFor(store, "grouptranslation.add"));
        Assert.Equal(AccessVia.Owner, audit.Via);
        Assert.Equal("group", audit.TargetKind);
        Assert.Equal(GroupId, audit.TargetId);
        Assert.Equal(owner, audit.ActorId);
    }

    [Fact]
    public async Task AddGroupTranslation_Translator_Allows_ViaAdmin()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string owner = "u-a026-owner";
        const string translator = "u-a026-translator";

        await Plant(store, NewGroup(owner));

        await RunInSession(store, s =>
            svc.AddGroupTranslationAsync(GroupId, "pl", null, "opis", translator, RoleSet(Roles.Translator), s));

        var audit = Assert.Single(await AuditsFor(store, "grouptranslation.add"));
        Assert.Equal(AccessVia.Admin, audit.Via);
        Assert.Equal(translator, audit.ActorId);
    }

    [Fact]
    public async Task AddGroupTranslation_GlobalAdmin_Allows_ViaAdmin()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string owner = "u-a026-owner";
        const string admin = "u-a026-admin";

        await Plant(store, NewGroup(owner));

        await RunInSession(store, s =>
            svc.AddGroupTranslationAsync(GroupId, "pl", "Wolontariusze rowerów", "opis", admin, RoleSet(Roles.GlobalAdmin), s));

        var audit = Assert.Single(await AuditsFor(store, "grouptranslation.add"));
        Assert.Equal(AccessVia.Admin, audit.Via);
    }

    [Fact]
    public async Task AddGroupTranslation_PlainMember_Denied_NoRow()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string owner = "u-a026-owner";
        const string member = "u-a026-member";

        await Plant(store, NewGroup(owner));

        await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
            RunInSession(store, s =>
                svc.AddGroupTranslationAsync(GroupId, "pl", "X", null, member, RoleSet(Roles.Member), s)));

        Assert.Empty(await svc.GetGroupTranslationsAsync(GroupId));
        Assert.Empty(await AuditsFor(store, "grouptranslation.add"));
    }

    [Fact]
    public async Task AddGroupTranslation_ComponentModerator_Denied()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string owner = "u-a026-owner";
        const string mod = "u-a026-mod";

        await Plant(store, NewGroup(owner));

        // A component-moderator claim does not qualify on the group lane (the
        // group lane has no component-moderator standing, ADR 0007/0012).
        await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
            RunInSession(store, s =>
                svc.AddGroupTranslationAsync(
                    GroupId, "pl", "X", null, mod,
                    RoleSet(Roles.Moderator, Roles.ModeratorComponent(ComponentId)), s)));

        Assert.Empty(await svc.GetGroupTranslationsAsync(GroupId));
        Assert.Empty(await AuditsFor(store, "grouptranslation.add"));
    }

    [Fact]
    public async Task AddGroupTranslation_BothFieldsBlank_ArgumentException()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string owner = "u-a026-owner";

        await Plant(store, NewGroup(owner));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            RunInSession(store, s =>
                svc.AddGroupTranslationAsync(GroupId, "pl", "   ", null, owner, RoleSet(), s)));

        Assert.Empty(await svc.GetGroupTranslationsAsync(GroupId));
        Assert.Empty(await AuditsFor(store, "grouptranslation.add"));
    }

    [Fact]
    public async Task AddGroupTranslation_MissingGroup_KeyNotFound()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            RunInSession(store, s =>
                svc.AddGroupTranslationAsync("a026-missing-grp", "pl", "X", null, "u-a026", RoleSet(Roles.GlobalAdmin), s)));

        Assert.Empty(await AuditsFor(store, "grouptranslation.add"));
    }

    // ── Community write seam: standing + audit row ──────────────────────────

    [Fact]
    public async Task AddCommunityTranslation_GlobalAdmin_Allows_ViaAdmin()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string admin = "u-a026-admin";

        await Plant(store, NewComponent());

        var created = await RunInSession(store, s =>
            svc.AddCommunityTranslationAsync(ComponentId, "pl", "Osiedle", null, admin, RoleSet(Roles.GlobalAdmin), s));

        Assert.Equal("pl", created.LanguageCode);
        Assert.Equal("Osiedle", created.Name);

        var audit = Assert.Single(await AuditsFor(store, "communitytranslation.add"));
        Assert.Equal(AccessVia.Admin, audit.Via);
        Assert.Equal("component", audit.TargetKind);
        Assert.Equal(ComponentId, audit.TargetId);
    }

    [Fact]
    public async Task AddCommunityTranslation_Translator_Allows_ViaAdmin()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string translator = "u-a026-translator";

        await Plant(store, NewComponent());

        await RunInSession(store, s =>
            svc.AddCommunityTranslationAsync(ComponentId, "pl", null, "opis", translator, RoleSet(Roles.Translator), s));

        var audit = Assert.Single(await AuditsFor(store, "communitytranslation.add"));
        Assert.Equal(AccessVia.Admin, audit.Via);
        Assert.Equal(translator, audit.ActorId);
    }

    [Fact]
    public async Task AddCommunityTranslation_ComponentModerator_Denied_NoRow()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string mod = "u-a026-mod";

        await Plant(store, NewComponent());

        // A component-moderator governs a community's *members* (ADR 0012), not
        // its name — the community lane has no owner branch and the moderator
        // does not qualify.
        await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
            RunInSession(store, s =>
                svc.AddCommunityTranslationAsync(
                    ComponentId, "pl", "X", null, mod,
                    RoleSet(Roles.Moderator, Roles.ModeratorComponent(ComponentId)), s)));

        Assert.Empty(await svc.GetCommunityTranslationsAsync(ComponentId));
        Assert.Empty(await AuditsFor(store, "communitytranslation.add"));
    }

    [Fact]
    public async Task AddCommunityTranslation_PlainMember_Denied_NoRow()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string member = "u-a026-member";

        await Plant(store, NewComponent());

        await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
            RunInSession(store, s =>
                svc.AddCommunityTranslationAsync(ComponentId, "pl", "X", null, member, RoleSet(Roles.Member), s)));

        Assert.Empty(await svc.GetCommunityTranslationsAsync(ComponentId));
        Assert.Empty(await AuditsFor(store, "communitytranslation.add"));
    }

    [Fact]
    public async Task AddCommunityTranslation_MissingComponent_KeyNotFound()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            RunInSession(store, s =>
                svc.AddCommunityTranslationAsync("a026-missing-comp", "pl", "X", null, "u-a026", RoleSet(Roles.GlobalAdmin), s)));

        Assert.Empty(await AuditsFor(store, "communitytranslation.add"));
    }

    // ── ADR 0048 — edit + remove lane (same standing matrix, in-place row) ──

    [Fact]
    public async Task UpdateGroupTranslation_Owner_Allows_UpdatesRow_ViaOwner()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string owner = "u-a026-owner";

        await Plant(store, NewGroup(owner));
        await Plant(store, new GroupTranslation
        {
            Id = "a026-t1", GroupId = GroupId, LanguageCode = "pl",
            Name = "Wolontariusze rowerów", AuthorId = owner, Created = DateTimeOffset.UtcNow
        });

        var updated = await RunInSession(store, s =>
            svc.UpdateGroupTranslationAsync(GroupId, "pl", "Nowa nazwa", null, owner, RoleSet(), s));

        Assert.Equal("a026-t1", updated.Id);
        Assert.Equal("Nowa nazwa", updated.Name);
        Assert.Null(updated.Description);

        // A single row (in-place update — the unique index on (GroupId,
        // LanguageCode) would have blocked a second row).
        var rows = await svc.GetGroupTranslationsAsync(GroupId);
        Assert.Single(rows);

        var audit = Assert.Single(await AuditsFor(store, "grouptranslation.update"));
        Assert.Equal(AccessVia.Owner, audit.Via);
        Assert.Equal("group", audit.TargetKind);
        Assert.Equal(GroupId, audit.TargetId);
        Assert.Equal(AccessOutcome.Allow, audit.Outcome);
    }

    [Fact]
    public async Task UpdateGroupTranslation_Translator_Allows_ViaAdmin()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string owner = "u-a026-owner";
        const string translator = "u-a026-translator";

        await Plant(store, NewGroup(owner));
        await Plant(store, new GroupTranslation
        {
            Id = "a026-t2", GroupId = GroupId, LanguageCode = "pl",
            Name = "Wolontariusze rowerów", AuthorId = owner, Created = DateTimeOffset.UtcNow
        });

        await RunInSession(store, s =>
            svc.UpdateGroupTranslationAsync(GroupId, "pl", null, "nowy opis", translator, RoleSet(Roles.Translator), s));

        var audit = Assert.Single(await AuditsFor(store, "grouptranslation.update"));
        Assert.Equal(AccessVia.Admin, audit.Via);
        Assert.Equal(translator, audit.ActorId);
    }

    [Fact]
    public async Task UpdateGroupTranslation_PlainMember_Denied_RowKept()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string owner = "u-a026-owner";
        const string member = "u-a026-member";

        await Plant(store, NewGroup(owner));
        await Plant(store, new GroupTranslation
        {
            Id = "a026-t3", GroupId = GroupId, LanguageCode = "pl",
            Name = "Wolontariusze rowerów", AuthorId = owner, Created = DateTimeOffset.UtcNow
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            RunInSession(store, s =>
                svc.UpdateGroupTranslationAsync(GroupId, "pl", "X", null, member, RoleSet(Roles.Member), s)));

        Assert.Single(await svc.GetGroupTranslationsAsync(GroupId)); // untouched
        Assert.Empty(await AuditsFor(store, "grouptranslation.update"));
    }

    [Fact]
    public async Task UpdateGroupTranslation_MissingRow_KeyNotFound()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string owner = "u-a026-owner";

        await Plant(store, NewGroup(owner));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            RunInSession(store, s =>
                svc.UpdateGroupTranslationAsync(GroupId, "pl", "X", null, owner, RoleSet(), s)));

        Assert.Empty(await AuditsFor(store, "grouptranslation.update"));
    }

    [Fact]
    public async Task RemoveGroupTranslation_Owner_Allows_DeletesRow_ViaOwner()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string owner = "u-a026-owner";

        await Plant(store, NewGroup(owner));
        await Plant(store, new GroupTranslation
        {
            Id = "a026-t4", GroupId = GroupId, LanguageCode = "pl",
            Name = "Wolontariusze rowerów", AuthorId = owner, Created = DateTimeOffset.UtcNow
        });

        await RunInSession(store, s =>
            svc.RemoveGroupTranslationAsync(GroupId, "pl", owner, RoleSet(), s));

        Assert.Empty(await svc.GetGroupTranslationsAsync(GroupId));

        // The trail survives the hard delete — the audit row is the record.
        var audit = Assert.Single(await AuditsFor(store, "grouptranslation.remove"));
        Assert.Equal(AccessVia.Owner, audit.Via);
        Assert.Equal("group", audit.TargetKind);
        Assert.Equal(GroupId, audit.TargetId);
    }

    [Fact]
    public async Task RemoveGroupTranslation_PlainMember_Denied_RowKept()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string owner = "u-a026-owner";
        const string member = "u-a026-member";

        await Plant(store, NewGroup(owner));
        await Plant(store, new GroupTranslation
        {
            Id = "a026-t5", GroupId = GroupId, LanguageCode = "pl",
            Name = "Wolontariusze rowerów", AuthorId = owner, Created = DateTimeOffset.UtcNow
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            RunInSession(store, s =>
                svc.RemoveGroupTranslationAsync(GroupId, "pl", member, RoleSet(Roles.Member), s)));

        Assert.Single(await svc.GetGroupTranslationsAsync(GroupId)); // untouched
        Assert.Empty(await AuditsFor(store, "grouptranslation.remove"));
    }

    [Fact]
    public async Task UpdateCommunityTranslation_GlobalAdmin_Allows_UpdatesRow_ViaAdmin()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string admin = "u-a026-admin";

        await Plant(store, NewComponent());
        await Plant(store, new CommunityTranslation
        {
            Id = "a026-c1", ComponentId = ComponentId, LanguageCode = "pl",
            Name = "Osiedle", AuthorId = admin, Created = DateTimeOffset.UtcNow
        });

        var updated = await RunInSession(store, s =>
            svc.UpdateCommunityTranslationAsync(ComponentId, "pl", "Nowa nazwa", null, admin, RoleSet(Roles.GlobalAdmin), s));

        Assert.Equal("a026-c1", updated.Id);
        Assert.Equal("Nowa nazwa", updated.Name);

        var rows = await svc.GetCommunityTranslationsAsync(ComponentId);
        Assert.Single(rows);

        var audit = Assert.Single(await AuditsFor(store, "communitytranslation.update"));
        Assert.Equal(AccessVia.Admin, audit.Via);
        Assert.Equal("component", audit.TargetKind);
        Assert.Equal(ComponentId, audit.TargetId);
        Assert.Equal(AccessOutcome.Allow, audit.Outcome);
    }

    [Fact]
    public async Task UpdateCommunityTranslation_Translator_Allows_ViaAdmin()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string translator = "u-a026-translator";

        await Plant(store, NewComponent());
        await Plant(store, new CommunityTranslation
        {
            Id = "a026-c2", ComponentId = ComponentId, LanguageCode = "pl",
            Description = "opis", AuthorId = translator, Created = DateTimeOffset.UtcNow
        });

        await RunInSession(store, s =>
            svc.UpdateCommunityTranslationAsync(ComponentId, "pl", null, "nowy opis", translator, RoleSet(Roles.Translator), s));

        var audit = Assert.Single(await AuditsFor(store, "communitytranslation.update"));
        Assert.Equal(AccessVia.Admin, audit.Via);
        Assert.Equal(translator, audit.ActorId);
    }

    [Fact]
    public async Task UpdateCommunityTranslation_PlainMember_Denied_RowKept()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string member = "u-a026-member";

        await Plant(store, NewComponent());
        await Plant(store, new CommunityTranslation
        {
            Id = "a026-c3", ComponentId = ComponentId, LanguageCode = "pl",
            Name = "Osiedle", AuthorId = "u-a026-admin", Created = DateTimeOffset.UtcNow
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            RunInSession(store, s =>
                svc.UpdateCommunityTranslationAsync(ComponentId, "pl", "X", null, member, RoleSet(Roles.Member), s)));

        Assert.Single(await svc.GetCommunityTranslationsAsync(ComponentId)); // untouched
        Assert.Empty(await AuditsFor(store, "communitytranslation.update"));
    }

    [Fact]
    public async Task RemoveCommunityTranslation_GlobalAdmin_Allows_DeletesRow_ViaAdmin()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string admin = "u-a026-admin";

        await Plant(store, NewComponent());
        await Plant(store, new CommunityTranslation
        {
            Id = "a026-c4", ComponentId = ComponentId, LanguageCode = "pl",
            Name = "Osiedle", AuthorId = admin, Created = DateTimeOffset.UtcNow
        });

        await RunInSession(store, s =>
            svc.RemoveCommunityTranslationAsync(ComponentId, "pl", admin, RoleSet(Roles.GlobalAdmin), s));

        Assert.Empty(await svc.GetCommunityTranslationsAsync(ComponentId));

        var audit = Assert.Single(await AuditsFor(store, "communitytranslation.remove"));
        Assert.Equal(AccessVia.Admin, audit.Via);
        Assert.Equal("component", audit.TargetKind);
        Assert.Equal(ComponentId, audit.TargetId);
    }

    [Fact]
    public async Task RemoveCommunityTranslation_Translator_Allows_DeletesRow_ViaAdmin()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string translator = "u-a026-translator";

        await Plant(store, NewComponent());
        await Plant(store, new CommunityTranslation
        {
            Id = "a026-c5", ComponentId = ComponentId, LanguageCode = "pl",
            Description = "opis", AuthorId = translator, Created = DateTimeOffset.UtcNow
        });

        await RunInSession(store, s =>
            svc.RemoveCommunityTranslationAsync(ComponentId, "pl", translator, RoleSet(Roles.Translator), s));

        Assert.Empty(await svc.GetCommunityTranslationsAsync(ComponentId));

        var audit = Assert.Single(await AuditsFor(store, "communitytranslation.remove"));
        Assert.Equal(AccessVia.Admin, audit.Via);
        Assert.Equal(translator, audit.ActorId);
    }

    // ── Display probes: the Can* rules mirror the write gates ───────────────

    [Theory]
    [InlineData(true, false)]    // owner, no roles
    [InlineData(false, true)]   // non-owner + GlobalAdmin
    [InlineData(false, false)]  // non-owner + Member only (denied)
    public void CanTranslateGroup_MirrorsStanding(bool isOwner, bool hasQualifyingRole)
    {
        // The probe is pure (it delegates to the static standing resolver,
        // which never touches the store) — a null store is fine.
        var svc = new UserInfoService(store: null!);
        const string owner = "u-a026-owner";
        var actor = isOwner ? owner : "u-a026-actor";

        IReadOnlySet<string> roles = hasQualifyingRole
            ? RoleSet(Roles.GlobalAdmin)
            : RoleSet(Roles.Member);

        var expected = isOwner || hasQualifyingRole;
        Assert.Equal(expected, svc.CanTranslateGroup(owner, actor, roles));
    }

    [Fact]
    public void CanTranslateCommunity_GlobalAdmin_Allows()
    {
        var svc = new UserInfoService(store: null!); // the probe is pure
        Assert.True(svc.CanTranslateCommunity("u-a026", RoleSet(Roles.GlobalAdmin)));
    }

    [Fact]
    public void CanTranslateCommunity_Translator_Allows()
    {
        var svc = new UserInfoService(store: null!); // the probe is pure
        Assert.True(svc.CanTranslateCommunity("u-a026", RoleSet(Roles.Translator)));
    }

    [Fact]
    public void CanTranslateCommunity_ComponentModerator_Denied()
    {
        var svc = new UserInfoService(store: null!); // the probe is pure
        Assert.False(svc.CanTranslateCommunity(
            "u-a026", RoleSet(Roles.Moderator, Roles.ModeratorComponent(ComponentId))));
    }

    [Fact]
    public void CanTranslateCommunity_PlainMember_Denied()
    {
        var svc = new UserInfoService(store: null!); // the probe is pure
        Assert.False(svc.CanTranslateCommunity("u-a026", RoleSet(Roles.Member)));
    }
}
