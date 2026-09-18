using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Identity;
using Kumunita.Core.Pages;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0039 / PG U02 — the <see cref="PageService"/> read-lane + standing-matrix
/// + hierarchy-guard seam tests (design doc <c>pages-design.md</c> §3.3 / §3.4 /
/// §3.7). The <c>PG_*</c> family mirrors the <see cref="AuthorizationServiceTests
/// .A0036_"/> branch shape (the frozen <c>Decide()</c> public / community / grants
/// branches exercised through the real <see cref="IAuthorizationService"/> with a
/// <see cref="PageToAuditableResource"/> target), and the <see cref="PostServiceTests" />
/// harness shape (fresh scratch Postgres per test, <c>Plant</c> seeding, the frozen
/// <c>IUserInfoService</c> + <c>IAuthorizationService</c> composition).
/// <para>
/// This unit **does not** exercise the Web layer (that is U04's
/// <c>PageControllerTests</c>) and **does not** exercise the write lanes (U03) —
/// the standing helpers and hierarchy guards are the *pure / DB-backed* helpers
/// the write lanes will call, tested now so the guard is pinned before the
/// destructive write lanes land.
/// </para>
/// <para>
/// **Key-type note (the U01 handoff):** the <see cref="Page"/> /
/// <see cref="PageTranslation"/> docs use <c>string Id</c> (Marten's
/// conventional identity), so all id params are <c>string</c> /
/// <c>string?</c> — the plan's <c>Guid</c> wording was a typo, corrected here.
/// </para>
/// </summary>
public class PageServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ─── 1 — The PageToAuditableResource adapter (pure, no store) ──────────
    //
    // The adapter is the only new authorization surface (ADR 0039 §3.4):
    // it plugs into the frozen IAuthorizationService (ADR 0006 §A) with no
    // signature change. These tests pin the projection shape (Id / Name /
    // OwnerId / Audience / ComponentId / TargetKind) verbatim against the
    // PostToAuditableResource mapping, including the null-allowed Audience
    // (a public page's world-readable shape — ADR 0039 §3.4).

    [Fact]
    public void PG_Adapter_ProjectsEveryField_Verbatim()
    {
        var page = new Page
        {
            Id = "pg-adapter-1",
            Title = "About",
            Body = "body",
            AuthorId = "u-author-1",
            ComponentId = "comp-1",
            Audience = new Audience(AudienceMode.Any,
                [new AudienceGrant(GrantKind.User, "u-granted-1")]),
        };
        var target = new PageToAuditableResource(page);

        Assert.Equal("pg-adapter-1", target.Id);
        Assert.Equal("About", target.Name);
        Assert.Equal("u-author-1", target.OwnerId);
        Assert.Same(page.Audience, target.Audience);   // projected, not copied
        Assert.Equal("comp-1", target.ComponentId);
        Assert.Equal("page", target.TargetKind);
    }

    [Fact]
    public void PG_Adapter_NullAudience_ProjectsNull_PublicPath()
    {
        // A public page (ADR 0039 §3.4): Audience is null-allowed, and the
        // frozen Decide() branch 5 treats null as public (world-readable).
        var page = new Page { Id = "pg-adapter-null", AuthorId = "u-author-2" };
        var target = new PageToAuditableResource(page);

        Assert.Null(target.Audience);
    }

    [Fact]
    public void PG_Adapter_EmptyTitle_FallsBackToBodyTruncated()
    {
        var body = new string('x', 80);
        var page = new Page { Id = "pg-adapter-name", Title = "", Body = body };
        var target = new PageToAuditableResource(page);

        // 57 chars + "..." = 60 (the PostToAuditableResource.Name shape).
        Assert.Equal(60, target.Name.Length);
        Assert.EndsWith("...", target.Name);
    }

    // ─── 2 — The frozen Decide() branches through a Page target (DB-backed) ──
    //
    // The A0036_* family shape, re-pointed at a PageToAuditableResource target:
    // branch 5 (Audience null ⇒ public), branch 4 (Community flag + member),
    // branch 6 (MatchGroups grants), branch 1 (owner). The adapter is additive
    // — these prove the page plugs into the **unchanged** Decide() with no new
    // branch (ADR 0006 §A, ADR 0039 §3.4).

    [Fact]
    public async Task PG_NullAudience_Stranger_Reads_Public()
    {
        var (_store, _userInfo, auth) = await BootAsync();

        // A public page (Audience null — the one place pages differ from
        // posts). A stranger who is neither the owner nor in any grant reads
        // it via branch 5 (public).
        var page = new Page
        {
            Id = "pg-public", Slug = "about", Title = "About",
            AuthorId = "u-owner-pub",    // someone else — the owner branch is inert
            Audience = null,             // public (Decide branch 5)
        };
        var target = new PageToAuditableResource(page);

        var decision = await auth.CanAsync("u-stranger-pub", AccessAction.Read, target);
        Assert.True(decision.Allowed);
        Assert.Equal(AccessVia.Audience, decision.Via);   // denyVia for a public resource
    }

    [Fact]
    public async Task PG_CommunityFlagAndMember_Allows_ViaCommunity()
    {
        var (store, userInfo, auth) = await BootAsync();
        const string actor = "u-member-pg";
        const string componentId = "comp-pg-community";
        await SeedCommunityWithMemberAsync(store, userInfo, componentId, actor);

        // Community flag on, empty grants (the "all community members" shape),
        // component set — the actor is a member → Allow via Community (branch 4).
        var audience = new Audience(AudienceMode.Any, []) { Community = true };
        var page = new Page
        {
            Id = "pg-community", Slug = "rules", AuthorId = "u-other-pg",
            ComponentId = componentId,
            Audience = audience,
        };
        var target = new PageToAuditableResource(page);

        var decision = await auth.CanAsync(actor, AccessAction.Read, target);
        Assert.True(decision.Allowed);
        Assert.Equal(AccessVia.Community, decision.Via);
    }

    [Fact]
    public async Task PG_CommunityFlagButNotMember_Denies()
    {
        var (store, userInfo, auth) = await BootAsync();
        const string nonMember = "u-nonmember-pg";
        const string member = "u-member-pg2";
        const string componentId = "comp-pg-nonmember";
        await SeedCommunityWithMemberAsync(store, userInfo, componentId, member);

        // The actor is NOT a member of the component — the Community branch
        // fails and the grants are empty so MatchGroups denies too.
        var audience = new Audience(AudienceMode.Any, []) { Community = true };
        var page = new Page
        {
            Id = "pg-nonmember", Slug = "rules2", AuthorId = "u-other-pg2",
            ComponentId = componentId,
            Audience = audience,
        };
        var target = new PageToAuditableResource(page);

        var decision = await auth.CanAsync(nonMember, AccessAction.Read, target);
        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task PG_GrantsBranch_UserGrant_Allows()
    {
        var (_store, _userInfo, auth) = await BootAsync();
        const string granted = "u-granted-pg";

        // An explicit user grant (branch 6, MatchGroups) — the granted user
        // reads the page via the audience lane; a stranger does not.
        var audience = new Audience(AudienceMode.Any,
            [new AudienceGrant(GrantKind.User, granted)]);
        var page = new Page
        {
            Id = "pg-grants", Slug = "private", AuthorId = "u-other-pg3",
            Audience = audience,
        };
        var target = new PageToAuditableResource(page);

        var grantedDecision = await auth.CanAsync(granted, AccessAction.Read, target);
        Assert.True(grantedDecision.Allowed);
        Assert.Equal(AccessVia.Audience, grantedDecision.Via);

        var strangerDecision = await auth.CanAsync("u-stranger-pg3", AccessAction.Read, target);
        Assert.False(strangerDecision.Allowed);
    }

    [Fact]
    public async Task PG_GrantsBranch_OwnerBranchStillAllows()
    {
        var (_store, _userInfo, auth) = await BootAsync();
        const string owner = "u-owner-pg-grants";

        // The owner branch (branch 1) fires before MatchGroups — the author
        // always reads their own page even outside the audience.
        var audience = new Audience(AudienceMode.Any,
            [new AudienceGrant(GrantKind.User, "u-someone-else")]);
        var page = new Page
        {
            Id = "pg-owner-grants", Slug = "mine", AuthorId = owner,
            Audience = audience,
        };
        var target = new PageToAuditableResource(page);

        var ownerDecision = await auth.CanAsync(owner, AccessAction.Read, target);
        Assert.True(ownerDecision.Allowed);
        Assert.Equal(AccessVia.Owner, ownerDecision.Via);
    }

    // ─── 3 — GetByPath resolution (the derived path, ADR 0039 §3.3) ────────

    [Fact]
    public async Task PG_GetByPath_ResolvedRootToLeaf()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await PlantChainAsync(store, depth: 3, rootId: "pgpath");

        // The chain is a (root) → b → c; the derived path a/b/c resolves to c.
        var resolved = await svc.GetByPathAsync("a/b/c");
        Assert.Equal("pgpath-c", resolved.Id);
        Assert.Equal("c", resolved.Slug);

        // The intermediate level a/b resolves to b.
        var mid = await svc.GetByPathAsync("a/b");
        Assert.Equal("pgpath-b", mid.Id);

        // The root a resolves to a.
        var root = await svc.GetByPathAsync("a");
        Assert.Equal("pgpath-a", root.Id);
    }

    [Fact]
    public async Task PG_GetByPath_AbsentSegment_Throws()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await PlantChainAsync(store, depth: 2, rootId: "pgpathd");

        // a/e (e was never planted) — KeyNotFoundException (the Web layer's 404).
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.GetByPathAsync("a/e"));
    }

    [Fact]
    public async Task PG_GetByPath_DeletedSegment_TreatedAsAbsent()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await PlantChainAsync(store, depth: 2, rootId: "pgdel");

        // Soft-delete the middle page (b) — the read lanes treat it as absent
        // (the ADR 0024 filter), so a/b no longer resolves.
        await UpdatePageAsync(store, "pgdel-b", p => p.IsDeleted = true);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.GetByPathAsync("a/b"));
        // The root still resolves (it is not deleted).
        Assert.Equal("pgdel-a", (await svc.GetByPathAsync("a")).Id);
    }

    // ─── 4 — GetBySlugUnderParentAsync (the business-key lane) ─────────────

    [Fact]
    public async Task PG_GetBySlugUnderParent_Resolved()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await PlantChainAsync(store, depth: 2, rootId: "pgslug");

        var child = await svc.GetBySlugUnderParentAsync("pgslug-a", "b");
        Assert.Equal("pgslug-b", child.Id);

        var root = await svc.GetBySlugUnderParentAsync(null, "a");
        Assert.Equal("pgslug-a", root.Id);
    }

    [Fact]
    public async Task PG_GetBySlugUnderParent_Absent_Throws()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pgslugonly", Slug = "a", Title = "A", Body = "b" });

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.GetBySlugUnderParentAsync("pgslugonly", "does-not-exist"));
    }

    // ─── 5 — GetTreeAsync (the forest, IsDeleted-filtered) ─────────────────

    [Fact]
    public async Task PG_GetTree_FiltersDeletedPages()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await PlantChainAsync(store, depth: 2, rootId: "pgtree");
        await UpdatePageAsync(store, "pgtree-b", p => p.IsDeleted = true);

        var tree = await svc.GetTreeAsync();
        // The root (a) is live; the child (b) is deleted → filtered out.
        Assert.Single(tree);
        Assert.Equal("pgtree-a", Assert.Single(tree).Id);
    }

    [Fact]
    public async Task PG_GetTree_ReturnsAllLivePages()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await PlantChainAsync(store, depth: 3, rootId: "pgtreeall");

        var tree = await svc.GetTreeAsync();
        Assert.Equal(3, tree.Count);
    }

    // ─── 6 — GetTranslationsAsync (the ADR 0022 read, ordered) ──────────────

    [Fact]
    public async Task PG_GetTranslations_OrderedByLanguageCode()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg-tr-page", Slug = "t", Title = "T", Body = "b" });
        // Plant in unsorted order; the read must sort by LanguageCode.
        await Plant(store, new PageTranslation
        { Id = "pg-tr-1", PageId = "pg-tr-page", LanguageCode = "fr", Body = "FR", AuthorId = "u1" });
        await Plant(store, new PageTranslation
        { Id = "pg-tr-2", PageId = "pg-tr-page", LanguageCode = "de", Body = "DE", AuthorId = "u1" });
        await Plant(store, new PageTranslation
        { Id = "pg-tr-3", PageId = "pg-tr-page", LanguageCode = "en", Body = "EN", AuthorId = "u1" });

        var translations = await svc.GetTranslationsAsync("pg-tr-page");
        Assert.Equal(["de", "en", "fr"], translations.Select(t => t.LanguageCode).ToArray());
    }

    [Fact]
    public async Task PG_GetTranslations_MissingPage_Throws()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.GetTranslationsAsync("no-such-page"));
    }

    [Fact]
    public async Task PG_GetTranslations_NoRows_ReturnsEmpty()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg-tr-empty", Slug = "e", Title = "E", Body = "b" });

        var translations = await svc.GetTranslationsAsync("pg-tr-empty");
        Assert.Empty(translations);
    }

    // ─── 7 — GetByMountPointAsync (the UI-slot resolver) ───────────────────

    [Fact]
    public async Task PG_GetByMountPoint_Resolved()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page
        { Id = "pg-mount-1", Slug = "about", Title = "About", Body = "b", MountPoint = "footer/community" });

        var mounted = await svc.GetByMountPointAsync("footer/community");
        Assert.NotNull(mounted);
        Assert.Equal("pg-mount-1", mounted!.Id);
    }

    [Fact]
    public async Task PG_GetByMountPoint_NotMounted_ReturnsNull()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg-mount-2", Slug = "x", Title = "X", Body = "b" });

        Assert.Null(await svc.GetByMountPointAsync("footer/community"));
        Assert.Null(await svc.GetByMountPointAsync(""));
    }

    [Fact]
    public async Task PG_GetByMountPoint_DeletedPage_NotResolved()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page
        { Id = "pg-mount-3", Slug = "about", Title = "About", Body = "b", MountPoint = "help/account" });
        await UpdatePageAsync(store, "pg-mount-3", p => p.IsDeleted = true);

        Assert.Null(await svc.GetByMountPointAsync("help/account"));
    }

    // ─── 8 — Standing matrix (ADR 0039 §3.7 — pure role-claim checks) ──────
    //
    // The Check*Standing helpers are pure (no store) and reuse the existing
    // role claims (Roles.GlobalAdmin / Roles.Translator /
    // Roles.ModeratorComponent) — no new AccessAction / AccessVia / branch.
    // A null page is a KeyNotFoundException (404); a denied actor is an
    // UnauthorizedAccessException (403), the AnnouncementService shape.

    [Fact]
    public void PG_CheckCreateStanding_GlobalAdmin_AllowsAnyPage()
    {
        var page = new Page { Id = "pg-stand-create", ComponentId = "comp-x" };
        PageService.CheckCreateStanding("u-admin", RolesSet(Roles.GlobalAdmin), page);
    }

    [Fact]
    public void PG_CheckCreateStanding_CommunityModerator_Denied_SystemPage()
    {
        // ADR 0040: a system page (the default Kind) is GlobalAdmin-only for
        // create — a community Moderator has no standing on it.
        var page = new Page { Id = "pg-stand-create-c", ComponentId = "comp-x" };
        Assert.Throws<UnauthorizedAccessException>(
            () => PageService.CheckCreateStanding("u-mod", RolesSet(Roles.ModeratorComponent("comp-x")), page));
    }

    [Fact]
    public void PG_CheckCreateStanding_PlainMember_Denies()
    {
        var page = new Page { Id = "pg-stand-create-m", ComponentId = "comp-x" };
        Assert.Throws<UnauthorizedAccessException>(
            () => PageService.CheckCreateStanding("u-member", RolesSet(Roles.Member), page));
    }

    [Fact]
    public void PG_CheckCreateStanding_FlatPage_ModeratorOfOtherComp_Denies()
    {
        // A flat/public page (ComponentId null) requires a GlobalAdmin — a
        // moderator claim for an unrelated community does not qualify.
        var page = new Page { Id = "pg-stand-create-flat", ComponentId = null };
        Assert.Throws<UnauthorizedAccessException>(
            () => PageService.CheckCreateStanding("u-mod", RolesSet(Roles.ModeratorComponent("comp-other")), page));
    }

    [Fact]
    public void PG_CheckEditStanding_Author_Allows_BlogPage()
    {
        // ADR 0040: the author lane applies to a blog page (Kind = User).
        var page = new Page { Id = "pg-stand-edit", AuthorId = "u-author", Kind = PageKind.User };
        PageService.CheckEditStanding("u-author", RolesSet(Roles.Member), page);
    }

    [Fact]
    public void PG_CheckEditStanding_GlobalAdmin_AllowsAnyPage()
    {
        var page = new Page { Id = "pg-stand-edit-g", AuthorId = "u-someone-else" };
        PageService.CheckEditStanding("u-admin", RolesSet(Roles.GlobalAdmin), page);
    }

    [Fact]
    public void PG_CheckEditStanding_CommunityModerator_Denied_SystemPage()
    {
        // ADR 0040: a system page is GlobalAdmin-only for edit — a community
        // Moderator has no standing on it.
        var page = new Page { Id = "pg-stand-edit-c", AuthorId = "u-someone-else", ComponentId = "comp-x" };
        Assert.Throws<UnauthorizedAccessException>(
            () => PageService.CheckEditStanding("u-mod", RolesSet(Roles.ModeratorComponent("comp-x")), page));
    }

    [Fact]
    public void PG_CheckEditStanding_PlainMember_Denies()
    {
        var page = new Page { Id = "pg-stand-edit-m", AuthorId = "u-someone-else", ComponentId = "comp-x" };
        Assert.Throws<UnauthorizedAccessException>(
            () => PageService.CheckEditStanding("u-member", RolesSet(Roles.Member), page));
    }

    [Fact]
    public void PG_CheckTranslateStanding_GlobalAdmin_AllowsAnyPage()
    {
        var page = new Page { Id = "pg-stand-trans-g" };
        PageService.CheckTranslateStanding("u-admin", RolesSet(Roles.GlobalAdmin), page);
    }

    [Fact]
    public void PG_CheckTranslateStanding_Translator_AllowsAnyPage()
    {
        var page = new Page { Id = "pg-stand-trans-t" };
        PageService.CheckTranslateStanding("u-translator", RolesSet(Roles.Translator), page);
    }

    [Fact]
    public void PG_CheckTranslateStanding_CommunityModerator_Denied_AllKinds()
    {
        // ADR 0040: a community Moderator has no translation standing on
        // either kind (system: no community to moderate; blog: personal
        // content). Only GlobalAdmin / Translator qualify.
        var systemPage = new Page { Id = "pg-stand-trans-c-s", ComponentId = "comp-x" };
        var blogPage = new Page { Id = "pg-stand-trans-c-b", ComponentId = "comp-x", Kind = PageKind.User };
        Assert.Throws<UnauthorizedAccessException>(
            () => PageService.CheckTranslateStanding("u-mod", RolesSet(Roles.ModeratorComponent("comp-x")), systemPage));
        Assert.Throws<UnauthorizedAccessException>(
            () => PageService.CheckTranslateStanding("u-mod", RolesSet(Roles.ModeratorComponent("comp-x")), blogPage));
    }

    [Fact]
    public void PG_CheckTranslateStanding_FlatPage_ModeratorOfOtherComp_Denies()
    {
        // A flat/public page has no community to moderate — the component-
        // moderator standing does not qualify; only GlobalAdmin / Translator.
        var page = new Page { Id = "pg-stand-trans-flat", ComponentId = null };
        Assert.Throws<UnauthorizedAccessException>(
            () => PageService.CheckTranslateStanding("u-mod", RolesSet(Roles.ModeratorComponent("comp-other")), page));
    }

    [Fact]
    public void PG_CheckTranslateStanding_PlainMember_Denies()
    {
        var page = new Page { Id = "pg-stand-trans-m", ComponentId = "comp-x" };
        Assert.Throws<UnauthorizedAccessException>(
            () => PageService.CheckTranslateStanding("u-member", RolesSet(Roles.Member), page));
    }

    // ─── 8b — the public display probe (CanTranslatePage) ──────────────────
    //
    // U06 (ADR 0029 carried to pages): the Web layer's "add a translation"
    // affordance flag (PageShowViewModel.CanTranslate) and the AddTranslationAsync
    // write-lane gate both reduce to this one decision. The repo pins the
    // bool probes directly (PostService.CanAddTranslation /
    // AnnouncementService.CanTranslateAnnouncement are tested as pure
    // allow/deny matrices, not only via their write-lane tests), so the
    // three standing branches are pinned here. A scoped page (ComponentId
    // "comp-x") vs a flat/public page (ComponentId null) is the ADR 0029
    // matrix: the community-Moderator branch only qualifies on the scoped
    // page.

    [Fact]
    public void PG6_CanTranslatePage_GlobalAdmin_AllowsAnyPage()
    {
        // GlobalAdmin qualifies on a scoped page AND a flat/public page.
        var scoped = new Page { Id = "pg6-t-ga-s", ComponentId = "comp-x" };
        var flat = new Page { Id = "pg6-t-ga-f", ComponentId = null };
        Assert.True(PageService.CanTranslatePage("u-admin", RolesSet(Roles.GlobalAdmin), scoped));
        Assert.True(PageService.CanTranslatePage("u-admin", RolesSet(Roles.GlobalAdmin), flat));
    }

    [Fact]
    public void PG6_CanTranslatePage_Translator_AllowsAnyPage()
    {
        // Translator qualifies on a scoped page AND a flat/public page.
        var scoped = new Page { Id = "pg6-t-tr-s", ComponentId = "comp-x" };
        var flat = new Page { Id = "pg6-t-tr-f", ComponentId = null };
        Assert.True(PageService.CanTranslatePage("u-translator", RolesSet(Roles.Translator), scoped));
        Assert.True(PageService.CanTranslatePage("u-translator", RolesSet(Roles.Translator), flat));
    }

    [Fact]
    public void PG6_CanTranslatePage_CommunityModerator_Denied_AllKinds()
    {
        // ADR 0040: a community Moderator has no translation standing on
        // either kind (the ADR 0039 §3.7 moderator lane is removed).
        var systemPage = new Page { Id = "pg6-t-mod-s", ComponentId = "comp-x" };
        var blogPage = new Page { Id = "pg6-t-mod-b", ComponentId = "comp-x", Kind = PageKind.User };
        var modRoles = RolesSet(Roles.Moderator, Roles.ModeratorComponent("comp-x"));

        Assert.False(PageService.CanTranslatePage("u-mod", modRoles, systemPage));
        Assert.False(PageService.CanTranslatePage("u-mod", modRoles, blogPage));
    }

    [Fact]
    public void PG6_CanTranslatePage_PlainMember_Denies()
    {
        // A plain resident (no standing claim) never qualifies — scoped or
        // flat, a Translator-elsewhere, nothing.
        var scoped = new Page { Id = "pg6-t-mem-s", ComponentId = "comp-x" };
        var flat = new Page { Id = "pg6-t-mem-f", ComponentId = null };
        Assert.False(PageService.CanTranslatePage("u-member", RolesSet(Roles.Member), scoped));
        Assert.False(PageService.CanTranslatePage("u-member", RolesSet(Roles.Member), flat));
    }

    [Fact]
    public void PG6_CanTranslatePage_NullPageOrBlankActor_Denies()
    {
        // The probe is pure (no exception) — a null page or a blank actor
        // is a flat denial (the display flag is simply unset; the write-lane
        // gate is the one that throws for those shapes).
        var scoped = new Page { Id = "pg6-t-n", ComponentId = "comp-x" };
        Assert.False(PageService.CanTranslatePage("u-admin", RolesSet(Roles.GlobalAdmin), null));
        Assert.False(PageService.CanTranslatePage("", RolesSet(Roles.GlobalAdmin), scoped));
    }

    [Fact]
    public void PG_CheckStanding_NullPage_KeyNotFound()
    {
        Assert.Throws<KeyNotFoundException>(
            () => PageService.CheckCreateStanding("u-a", RolesSet(Roles.Member), null));
        Assert.Throws<KeyNotFoundException>(
            () => PageService.CheckEditStanding("u-a", RolesSet(Roles.Member), null));
        Assert.Throws<KeyNotFoundException>(
            () => PageService.CheckTranslateStanding("u-a", RolesSet(Roles.Member), null));
    }

    // ─── 9 — Hierarchy guards (ADR 0039 §3.3 — cycle-guard + depth-cap) ────
    //
    // The write lanes (U03's CreateAsync / MoveAsync) call these before
    // committing. They are DB-backed (Marten has no FK, so the guard is the
    // service's) and are tested now so the guard is pinned before the write
    // lanes land.

    [Fact]
    public async Task PG_CycleGuard_MovingUnderSelf_Throws()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg-cycle-a", Slug = "a", Title = "A", Body = "b" });

        // Moving a under a is a self-cycle.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.EnsureNoCycleAsync("pg-cycle-a", "pg-cycle-a"));
    }

    [Fact]
    public async Task PG_CycleGuard_MovingUnderDescendant_Throws()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        // a (root) → b (child of a) → c (child of b).
        await PlantChainAsync(store, depth: 3, rootId: "pgcyc");

        // Moving a under c (a's own descendant) would make a a descendant of
        // itself — a cycle.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.EnsureNoCycleAsync("pgcyc-a", "pgcyc-c"));
    }

    [Fact]
    public async Task PG_CycleGuard_MovingUnderSibling_Allows()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        // a (root) with two children: b and d (siblings).
        await Plant(store, new Page { Id = "pg-sib-a", Slug = "a", Title = "A", Body = "b" });
        await Plant(store, new Page { Id = "pg-sib-b", Slug = "b", ParentId = "pg-sib-a", Title = "B", Body = "b" });
        await Plant(store, new Page { Id = "pg-sib-d", Slug = "d", ParentId = "pg-sib-a", Title = "D", Body = "b" });

        // Moving b under d (a sibling) is a valid re-parent — no cycle.
        await svc.EnsureNoCycleAsync("pg-sib-b", "pg-sib-d");   // no throw
    }

    [Fact]
    public async Task PG_DepthCap_WithinLimit_Allows()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        // A chain of 7 pages (depth 1..7). Placing a new node under the
        // depth-7 node (pgdwithin-g) would make it depth 8 (== MaxDepth) —
        // allowed.
        await PlantChainAsync(store, depth: 7, rootId: "pgdwithin");

        await svc.EnsureDepthWithinLimitAsync("pgdwithin-g");   // depth 7 → 8, within the cap
    }

    [Fact]
    public async Task PG_DepthCap_ExceedsLimit_Throws()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        // A chain of 8 pages (depth 1..8). Placing a new node under the
        // depth-8 node (pgdover-h) would make it depth 9 (> MaxDepth 8) —
        // denied.
        await PlantChainAsync(store, depth: 8, rootId: "pgdover");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.EnsureDepthWithinLimitAsync("pgdover-h"));
    }

    [Fact]
    public async Task PG_GetDepth_RootIsOne()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg-deep-a", Slug = "a", Title = "A", Body = "b" });

        Assert.Equal(1, await svc.GetDepthAsync("pg-deep-a"));
    }

    [Fact]
    public async Task PG_GetDepth_ChainCountsAncestors()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await PlantChainAsync(store, depth: 3, rootId: "pgdeep");

        Assert.Equal(1, await svc.GetDepthAsync("pgdeep-a"));
        Assert.Equal(2, await svc.GetDepthAsync("pgdeep-b"));
        Assert.Equal(3, await svc.GetDepthAsync("pgdeep-c"));
    }

    // ─── 10 — Write lanes (ADR 0039 §3.7 — U03) ───────────────────────────
    //
    // The standing-gated mutation surface. Each lane: (a) re-checks standing
    // server-side (the C3 single-source pin — a Web [Authorize] is not the
    // source of truth), (b) persists, and (c) stores an AccessAudit row in
    // the caller's in-flight session (C3, ADR 0006 — synchronous,
    // in-transaction, not a Wolverine side effect) with TargetKind = "page"
    // and the §3.7 action name. The standing matrix per lane (§3.7):
    //   create  → GlobalAdmin ∪ Moderator(ComponentId)          Via Admin/Moderator
    //   edit    → Author ∪ GlobalAdmin ∪ Moderator(ComponentId) Via Owner/Admin/Moderator
    //   publish → Author only (ADR 0037)                         Via Owner
    //   move    → GlobalAdmin ∪ Moderator(ComponentId) — NOT author    Via Admin/Moderator
    //   delete  → GlobalAdmin ∪ Moderator(ComponentId) — NOT author    Via Admin/Moderator
    //   translate → GlobalAdmin ∪ Translator ∪ Moderator(ComponentId) Via Admin/Moderator
    // The ImageIds/AttachmentIds are caller-parsed (the Web layer's
    // ContentImageIds/AttachmentIds idiom, ADR 0025/0034) — Core normalizes
    // the POCO's fields (?? []) and never parses the body.

    // ─── 10.1 — CreateAsync ────────────────────────────────────────────────

    [Fact]
    public async Task PG3_Create_ByGlobalAdmin_Persists_WithAdminAudit()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        var page = new Page { Slug = "c-ga", Title = "T", Body = "b", ComponentId = "comp-c" };

        await using var session = newSession(store);
        var saved = await svc.CreateAsync(page, "u-admin", RolesSet(Roles.GlobalAdmin), session);

        Assert.False(string.IsNullOrEmpty(saved.Id));
        Assert.Equal("u-admin", saved.AuthorId);
        var audits = await AuditRows(store);
        var row = Assert.Single(audits, a => a.TargetId == saved.Id && a.Action == "page.create");
        Assert.Equal("page", row.TargetKind);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal("u-admin", row.ActorId);
    }

    [Fact]
    public async Task PG3_Create_ByCommunityModerator_Denied_SystemPage()
    {
        // ADR 0040: a system page (default Kind) is GlobalAdmin-only for
        // create — a community Moderator is denied.
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        var page = new Page { Slug = "c-mod", Title = "T", Body = "b", ComponentId = "comp-c" };

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(page, "u-mod-c", RolesSet(Roles.Moderator, Roles.ModeratorComponent("comp-c")), session));

        // The deny precedes SaveChangesAsync — nothing is persisted, no audit row.
        await using var q = store.QuerySession();
        Assert.Equal(0, await q.Query<Page>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await AuditRows(store));
    }

    [Fact]
    public async Task PG3_Create_ByPlainMember_Denied_NoRow()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        var page = new Page { Slug = "c-mem", Title = "T", Body = "b", ComponentId = "comp-c" };

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(page, "u-member", RolesSet(Roles.Member), session));

        // The deny precedes SaveChangesAsync — nothing is persisted, no audit row.
        await using var q = store.QuerySession();
        Assert.Equal(0, await q.Query<Page>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await AuditRows(store));
    }

    [Fact]
    public async Task PG3_Create_RootSlugCollision_Throws()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        // The (ParentId, Slug) unique index does NOT prevent two roots sharing
        // a slug (Postgres treats NULLs as distinct) — CreateAsync is the
        // authoritative root-slug guard.
        await Plant(store, new Page { Id = "pg3-c-root-1", Slug = "dup", Title = "R1", Body = "b" });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(new Page { Slug = "dup", Title = "R2", Body = "b" },
                "u-admin", RolesSet(Roles.GlobalAdmin), session));
    }

    [Fact]
    public async Task PG3_Create_NormalizesNullImageAndAttachmentIds()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        // The caller (Web layer) parses the body into ImageIds/AttachmentIds
        // (ADR 0025/0034) and sets them on the POCO; Core persists them
        // verbatim (?? []). The POCO's non-null default [] round-trips through
        // CreateAsync — verify the fields are non-null and stable after save.
        var page = new Page { Slug = "c-img", Title = "T", Body = "b" };   // default ImageIds/AttachmentIds = []

        await using var session = newSession(store);
        var saved = await svc.CreateAsync(page, "u-admin", RolesSet(Roles.GlobalAdmin), session);

        Assert.NotNull(saved.ImageIds);
        Assert.NotNull(saved.AttachmentIds);
        Assert.Empty(saved.ImageIds);
        Assert.Empty(saved.AttachmentIds);
    }

    // ─── 10.2 — UpdateAsync ────────────────────────────────────────────────

    [Fact]
    public async Task PG3_Update_ByAuthor_Persists_WithOwnerAudit()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-u-auth", Slug = "u", Title = "Old", Body = "ob", AuthorId = "u-author", Kind = PageKind.User });

        await using var session = newSession(store);
        var saved = await svc.UpdateAsync(
            new Page { Id = "pg3-u-auth", Slug = "u", Title = "New", Body = "nb" },
            "u-author", RolesSet(Roles.Member), session);

        Assert.Equal("New", saved.Title);
        Assert.NotNull(saved.Modified);
        var audits = await AuditRows(store);
        var row = Assert.Single(audits, a => a.TargetId == "pg3-u-auth" && a.Action == "page.update");
        Assert.Equal("page", row.TargetKind);
        Assert.Equal(AccessVia.Owner, row.Via);
    }

    [Fact]
    public async Task PG3_Update_ByGlobalAdmin_Allows_WithAdminAudit()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-u-ga", Slug = "u", Title = "Old", Body = "ob", AuthorId = "u-someone" });

        await using var session = newSession(store);
        await svc.UpdateAsync(
            new Page { Id = "pg3-u-ga", Slug = "u", Title = "New", Body = "nb" },
            "u-admin", RolesSet(Roles.GlobalAdmin), session);

        var audits = await AuditRows(store);
        Assert.Equal(AccessVia.Admin,
            (await AuditsFor(store, "pg3-u-ga", "page.update")).Single().Via);
    }

    [Fact]
    public async Task PG3_Update_ByCommunityModerator_Denied_SystemPage()
    {
        // ADR 0040: a system page (default Kind) is GlobalAdmin-only for edit
        // — a community Moderator is denied.
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page
        { Id = "pg3-u-mod", Slug = "u", Title = "Old", Body = "ob", AuthorId = "u-someone", ComponentId = "comp-u" });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UpdateAsync(
                new Page { Id = "pg3-u-mod", Slug = "u", Title = "New", Body = "nb" },
                "u-mod-u", RolesSet(Roles.ModeratorComponent("comp-u")), session));
    }

    [Fact]
    public async Task PG3_Update_ByNonAuthorMember_Denied()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-u-den", Slug = "u", Title = "Old", Body = "ob", AuthorId = "u-someone" });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UpdateAsync(new Page { Id = "pg3-u-den", Slug = "u", Title = "X", Body = "nb" },
                "u-member", RolesSet(Roles.Member), session));
    }

    [Fact]
    public async Task PG3_Update_MissingPage_KeyNotFound()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await using var session = newSession(store);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.UpdateAsync(new Page { Id = "pg3-u-miss", Title = "X", Body = "nb" },
                "u-admin", RolesSet(Roles.GlobalAdmin), session));
    }

    // ─── 10.2b — MountPoint guard (ADR 0040 — MountPoint is a system-page
    // concept; a resident's blog page only surfaces in its own /blog feed, so
    // the write lane clears it server-side; a system page keeps its slot). ──

    [Fact]
    public async Task PG3_Create_BlogPage_ClearsMountPoint()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        // A client-supplied MountPoint on a blog page must be stripped.
        var page = new Page { Slug = "b-mount", Title = "T", Body = "b", Kind = PageKind.User, MountPoint = "footer/community" };

        await using var session = newSession(store);
        var saved = await svc.CreateAsync(page, "u-resident", RolesSet(Roles.Member), session);

        Assert.Null(saved.MountPoint);
    }

    [Fact]
    public async Task PG3_Create_SystemPage_KeepsMountPoint()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        // A system page may mount to a UI slot — the guard must not touch it.
        var page = new Page { Slug = "sys-mount", Title = "About", Body = "b", MountPoint = "footer/community" };

        await using var session = newSession(store);
        var saved = await svc.CreateAsync(page, "u-admin", RolesSet(Roles.GlobalAdmin), session);

        Assert.Equal("footer/community", saved.MountPoint);
    }

    [Fact]
    public async Task PG3_Update_BlogPage_ClearsMountPoint()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-mu-blog", Slug = "u", Title = "Old", Body = "ob", AuthorId = "u-author", Kind = PageKind.User });

        await using var session = newSession(store);
        var saved = await svc.UpdateAsync(
            new Page { Id = "pg3-mu-blog", Slug = "u", Title = "New", Body = "nb", MountPoint = "help/account" },
            "u-author", RolesSet(Roles.Member), session);

        Assert.Null(saved.MountPoint);
    }

    [Fact]
    public async Task PG3_Update_SystemPage_KeepsMountPoint()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-mu-sys", Slug = "u", Title = "Old", Body = "ob", AuthorId = "u-someone" });

        await using var session = newSession(store);
        var saved = await svc.UpdateAsync(
            new Page { Id = "pg3-mu-sys", Slug = "u", Title = "New", Body = "nb", MountPoint = "help/account" },
            "u-admin", RolesSet(Roles.GlobalAdmin), session);

        Assert.Equal("help/account", saved.MountPoint);
    }

    // ─── 10.3 — PublishAsync (ADR 0037 author-only pin) ────────────────────

    [Fact]
    public async Task PG3_Publish_ByAuthor_ClearsDraft_WithOwnerAudit()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-p-auth", Slug = "p", Title = "T", Body = "b", AuthorId = "u-author", IsDraft = true });

        await using var session = newSession(store);
        var saved = await svc.PublishAsync("pg3-p-auth", "u-author", session);

        Assert.False(saved.IsDraft);
        Assert.NotNull(saved.Modified);
        var audits = await AuditRows(store);
        var row = Assert.Single(audits, a => a.TargetId == "pg3-p-auth" && a.Action == "page.publish");
        Assert.Equal("page", row.TargetKind);
        Assert.Equal(AccessVia.Owner, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
    }

    [Fact]
    public async Task PG3_Publish_ByGlobalAdmin_Denied_Adr0037Pin()
    {
        // ADR 0037 author-only: a GlobalAdmin CANNOT publish someone else's
        // draft — publishing is the author's choice, not an admin's lever.
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-p-ga", Slug = "p", Title = "T", Body = "b", AuthorId = "u-author", IsDraft = true });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.PublishAsync("pg3-p-ga", "u-admin", session));

        Assert.Empty(await AuditRows(store));
    }

    [Fact]
    public async Task PG3_Publish_ByModerator_Denied()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page
        { Id = "pg3-p-mod", Slug = "p", Title = "T", Body = "b", AuthorId = "u-author", IsDraft = true, ComponentId = "comp-p" });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.PublishAsync("pg3-p-mod", "u-mod-p", session));
    }

    [Fact]
    public async Task PG3_Publish_AlreadyLive_Idempotent_NoModifiedStamp()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        var initial = DateTimeOffset.UtcNow.AddDays(-1);
        await Plant(store, new Page { Id = "pg3-p-idem", Slug = "p", Title = "T", Body = "b", AuthorId = "u-author", IsDraft = false, Modified = initial });

        await using var session = newSession(store);
        var saved = await svc.PublishAsync("pg3-p-idem", "u-author", session);

        // Idempotent — a second publish on an already-live page does not stamp Modified.
        Assert.Equal(initial, saved.Modified);
    }

    // ─── 10.4 — MoveAsync (admin/mod only; cycle-guard + depth-cap) ────────

    [Fact]
    public async Task PG3_Move_ByGlobalAdmin_ReparsesPath_WithAdminAudit()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-m-root", Slug = "root", Title = "R", Body = "b" });
        await Plant(store, new Page { Id = "pg3-m-child", Slug = "child", ParentId = "pg3-m-root", Title = "C", Body = "b" });

        // Move the child to be a root (newParentId null).
        await using var session = newSession(store);
        var saved = await svc.MoveAsync("pg3-m-child", null, null, "u-admin", RolesSet(Roles.GlobalAdmin), session);

        Assert.Null(saved.ParentId);
        var audits = await AuditRows(store);
        var row = Assert.Single(audits, a => a.TargetId == "pg3-m-child" && a.Action == "page.move");
        Assert.Equal("page", row.TargetKind);
        Assert.Equal(AccessVia.Admin, row.Via);
    }

    [Fact]
    public async Task PG3_Move_ByAuthor_Denied_NotAuthorLane()
    {
        // §3.7: move is admin/moderator-scoped, NOT a plain author (a page is
        // platform content, not a personal note). Even the author is denied.
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-m-auth", Slug = "m", Title = "T", Body = "b", AuthorId = "u-author" });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.MoveAsync("pg3-m-auth", null, "m2", "u-author", RolesSet(Roles.Member), session));
    }

    [Fact]
    public async Task PG3_Move_ByCommunityModerator_Denied_SystemPage()
    {
        // ADR 0040: a system page (default Kind) is GlobalAdmin-only for move
        // — a community Moderator is denied.
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page
        { Id = "pg3-m-mod", Slug = "m", Title = "T", Body = "b", AuthorId = "u-someone", ComponentId = "comp-m" });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.MoveAsync("pg3-m-mod", null, "m2", "u-mod-m", RolesSet(Roles.ModeratorComponent("comp-m")), session));
    }

    [Fact]
    public async Task PG3_Move_UnderDescendant_CycleGuardThrows()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await PlantChainAsync(store, depth: 3, rootId: "pg3-cyc");   // a→b→c

        // Moving a under c (a's own descendant) is a cycle.
        await using var session = newSession(store);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.MoveAsync("pg3-cyc-a", "pg3-cyc-c", null, "u-admin", RolesSet(Roles.GlobalAdmin), session));
    }

    [Fact]
    public async Task PG3_Move_UnderDepthEight_Throws_DepthCap()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await PlantChainAsync(store, depth: 8, rootId: "pg3-dep");   // a..h, h is depth 8
        await Plant(store, new Page { Id = "pg3-dep-x", Slug = "x", Title = "X", Body = "b" });   // a separate root

        // Moving x under h (depth 8) would make it depth 9 — exceeds MaxDepth (8).
        await using var session = newSession(store);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.MoveAsync("pg3-dep-x", "pg3-dep-h", null, "u-admin", RolesSet(Roles.GlobalAdmin), session));
    }

    // ─── 10.5 — DeleteAsync (soft-delete; admin/mod only) ─────────────────

    [Fact]
    public async Task PG3_Delete_ByGlobalAdmin_SoftDeletes_HiddenFromRead()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-d-ga", Slug = "d", Title = "T", Body = "b", AuthorId = "u-someone" });

        await using var session = newSession(store);
        await svc.DeleteAsync("pg3-d-ga", "u-admin", RolesSet(Roles.GlobalAdmin), session);

        // The row is NOT removed — it is soft-deleted (IsDeleted = true).
        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<Page>("pg3-d-ga", TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.True(stored!.IsDeleted);

        // And it is hidden from the read lanes (U02's IsDeleted filter).
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.GetByPathAsync("d"));
        Assert.DoesNotContain(await svc.GetTreeAsync(), p => p.Id == "pg3-d-ga");

        var audits = await AuditRows(store);
        var row = Assert.Single(audits, a => a.TargetId == "pg3-d-ga" && a.Action == "page.delete");
        Assert.Equal("page", row.TargetKind);
        Assert.Equal(AccessVia.Admin, row.Via);
    }

    [Fact]
    public async Task PG3_Delete_ByAuthor_Denied_NotAuthorLane()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-d-auth", Slug = "d", Title = "T", Body = "b", AuthorId = "u-author" });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.DeleteAsync("pg3-d-auth", "u-author", RolesSet(Roles.Member), session));

        // Nothing changed — the page is still live.
        await using var q = store.QuerySession();
        Assert.False((await q.LoadAsync<Page>("pg3-d-auth", TestContext.Current.CancellationToken))!.IsDeleted);
    }

    [Fact]
    public async Task PG3_Delete_ByCommunityModerator_Denied_SystemPage()
    {
        // ADR 0040: a system page (default Kind) is GlobalAdmin-only for
        // delete — a community Moderator is denied.
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page
        { Id = "pg3-d-mod", Slug = "d", Title = "T", Body = "b", AuthorId = "u-someone", ComponentId = "comp-d" });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.DeleteAsync("pg3-d-mod", "u-mod-d", RolesSet(Roles.ModeratorComponent("comp-d")), session));

        // Nothing changed — the page is still live.
        await using var q = store.QuerySession();
        Assert.False((await q.LoadAsync<Page>("pg3-d-mod", TestContext.Current.CancellationToken))!.IsDeleted);
    }

    [Fact]
    public async Task PG3_Delete_MissingPage_KeyNotFound()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await using var session = newSession(store);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.DeleteAsync("pg3-d-miss", "u-admin", RolesSet(Roles.GlobalAdmin), session));
    }

    // ─── 10.6 — AddTranslationAsync (standing + add-only + audit) ─────────

    [Fact]
    public async Task PG3_Translate_ByGlobalAdmin_Persists_WithAdminAudit()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-t-ga", Slug = "t", Title = "T", Body = "b", AuthorId = "u-someone" });

        await using var session = newSession(store);
        var saved = await svc.AddTranslationAsync("pg3-t-ga", "fr", "T", "Corps", "u-admin", RolesSet(Roles.GlobalAdmin), session);

        Assert.Equal("pg3-t-ga", saved.PageId);
        Assert.Equal("fr", saved.LanguageCode);
        var audits = await AuditRows(store);
        var row = Assert.Single(audits, a => a.TargetId == "pg3-t-ga" && a.Action == "page.translation.add");
        Assert.Equal("page", row.TargetKind);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
    }

    [Fact]
    public async Task PG3_Translate_ByTranslator_Persists_WithAdminAudit()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-t-tr", Slug = "t", Title = "T", Body = "b", AuthorId = "u-someone" });

        await using var session = newSession(store);
        await svc.AddTranslationAsync("pg3-t-tr", "de", "T", "Körper", "u-translator", RolesSet(Roles.Translator), session);

        var audits = await AuditRows(store);
        Assert.Equal(AccessVia.Admin,
            (await AuditsFor(store, "pg3-t-tr", "page.translation.add")).Single().Via);
    }

    [Fact]
    public async Task PG3_Translate_ByCommunityModerator_Denied_AllKinds()
    {
        // ADR 0040: a community Moderator has no translation standing on
        // either kind (the ADR 0039 §3.7 moderator lane is removed).
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page
        { Id = "pg3-t-mod-s", Slug = "ts", Title = "T", Body = "b", AuthorId = "u-someone", ComponentId = "comp-t" });
        await Plant(store, new Page
        { Id = "pg3-t-mod-b", Slug = "tb", Title = "T", Body = "b", AuthorId = "u-someone", ComponentId = "comp-t", Kind = PageKind.User });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.AddTranslationAsync("pg3-t-mod-s", "es", "T", "Cuerpo",
                "u-mod-t", RolesSet(Roles.ModeratorComponent("comp-t")), session));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.AddTranslationAsync("pg3-t-mod-b", "es", "T", "Cuerpo",
                "u-mod-t", RolesSet(Roles.ModeratorComponent("comp-t")), session));
    }

    [Fact]
    public async Task PG3_Translate_ByPlainMember_Denied()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-t-mem", Slug = "t", Title = "T", Body = "b", AuthorId = "u-someone" });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.AddTranslationAsync("pg3-t-mem", "fr", "T", "Corps", "u-member", RolesSet(Roles.Member), session));

        Assert.Empty(await AuditRows(store));
    }

    [Fact]
    public async Task PG3_Translate_FlatPage_ModeratorOfOtherComp_Denied()
    {
        // A flat/public page has no community to moderate — the component-
        // moderator standing does not qualify; only GlobalAdmin / Translator.
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-t-flat", Slug = "t", Title = "T", Body = "b", AuthorId = "u-someone", ComponentId = null });

        await using var session = newSession(store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.AddTranslationAsync("pg3-t-flat", "fr", "T", "Corps",
                "u-mod", RolesSet(Roles.ModeratorComponent("comp-other")), session));
    }

    [Fact]
    public async Task PG3_Translate_DuplicateLanguage_UseIndexToReject()
    {
        // The (PageId, LanguageCode) unique index is the add-only duplicate
        // guard: a second add of the same (page, language) is rejected by the
        // DB (Marten wraps it in a Postgres exception).
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await Plant(store, new Page { Id = "pg3-t-dup", Slug = "t", Title = "T", Body = "b", AuthorId = "u-someone" });

        await using (var session = newSession(store))
        {
            await svc.AddTranslationAsync("pg3-t-dup", "fr", "T1", "Corps 1", "u-admin", RolesSet(Roles.GlobalAdmin), session);
        }
        await using var session2 = newSession(store);
        await Assert.ThrowsAnyAsync<Exception>(() =>
            svc.AddTranslationAsync("pg3-t-dup", "fr", "T2", "Corps 2", "u-admin", RolesSet(Roles.GlobalAdmin), session2));
    }

    [Fact]
    public async Task PG3_Translate_MissingPage_KeyNotFound()
    {
        var store = await BootStoreAsync();
        var svc = new PageService(store);
        await using var session = newSession(store);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.AddTranslationAsync("pg3-t-miss", "fr", "T", "Corps", "u-admin", RolesSet(Roles.GlobalAdmin), session));
    }

    // ─── PG U05 (ADR 0039 §3.9, amended by ADR 0040) — the seeder's Page docs ─
    //
    // The seeder seeds the new Page docs for the canonical default pages (terms /
    // help / privacy / conduct — the four-page set, ADR 0043 D1) under a `system`
    // namespace root (ADR 0040), straight into the
    // caller's IDocumentSession, idempotently. The seeder's Page-upsert is a
    // public static (SeedDefaultPagesAsync) + a public page-data source
    // (EnDefaultPages), so these tests pin idempotency + the exact set across
    // two live sessions (boot twice, no duplicates) and the U05 drift pin
    // (no `about` page is seeded — the /about product-story view stays
    // authoritative on a fresh instance).

    [Fact]
    public async Task PG5_Seeder_TermsAndHelp_UnderSystemRoot_NoDuplicates_AcrossTwoBoots()
    {
        var store = await BootStoreAsync();
        var defaultPages = FirstBootSeeder.EnDefaultPages();
        var ct = TestContext.Current.CancellationToken;

        // Boot 1 — seed into one session, commit.
        await using (var s1 = newSession(store))
        {
            await FirstBootSeeder.SeedDefaultPagesAsync(s1, defaultPages, DateTimeOffset.UtcNow, ct);
            await s1.SaveChangesAsync(ct);
        }

        // Boot 2 — a second seed into a SECOND session must NOT create new rows
        // (idempotent refresh, not a duplicate).
        await using (var s2 = newSession(store))
        {
            await FirstBootSeeder.SeedDefaultPagesAsync(s2, defaultPages, DateTimeOffset.UtcNow, ct);
            await s2.SaveChangesAsync(ct);
        }

        // The `system` root exists (ADR 0040 namespace container).
        await using var q = store.QuerySession();
        var systemRoot = await q.Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(systemRoot);
        Assert.Equal(PageKind.System, systemRoot!.Kind);

        // The four seeded pages are nested under the `system` root (not roots
        // themselves). ADR 0043 D1 — the exact seeded set is terms / help /
        // privacy / conduct (SP U01 moved this pin from two to four pages).
        var children = await q.Query<Page>()
            .Where(p => p.ParentId == systemRoot.Id && p.IsDeleted == false)
            .ToListAsync(ct);
        Assert.Equal(new[] { "conduct", "help", "privacy", "terms" },
            children.Select(p => p.Slug).OrderBy(s => s, StringComparer.Ordinal).ToArray());

        // Exactly one child per slug (no duplicate from the second boot).
        Assert.Equal(4, children.Count);   // ADR 0043 D1: terms/help/privacy/conduct
        Assert.Equal(1, children.Count(p => p.Slug == "terms"));
        Assert.Equal(1, children.Count(p => p.Slug == "help"));
        Assert.Equal(1, children.Count(p => p.Slug == "privacy"));
        Assert.Equal(1, children.Count(p => p.Slug == "conduct"));

        // No other roots exist (the `system` root is the only root-level page).
        var allRoots = await q.Query<Page>()
            .Where(p => p.ParentId == null && p.IsDeleted == false)
            .ToListAsync(ct);
        var onlyRoot = Assert.Single(allRoots);
        Assert.Equal("system", onlyRoot.Slug);
    }

    [Fact]
    public async Task PG5_Seeder_About_IsNotSeeded_ProductStoryStaysAuthoritative()
    {
        var store = await BootStoreAsync();
        var defaultPages = FirstBootSeeder.EnDefaultPages();
        var ct = TestContext.Current.CancellationToken;

        await using (var s = newSession(store))
        {
            await FirstBootSeeder.SeedDefaultPagesAsync(s, defaultPages, DateTimeOffset.UtcNow, ct);
            await s.SaveChangesAsync(ct);
        }

        // The U05 drift pin: `about` is deliberately absent from the seeded set —
        // a fresh /about is the full-bleed product-story view (not a Markdown
        // page), so the seeder never writes an `about` page (root or nested).
        await using var q = store.QuerySession();
        var aboutPages = await q.Query<Page>()
            .Where(p => p.Slug == "about" && p.IsDeleted == false)
            .CountAsync(ct);

        Assert.Equal(0, aboutPages);
    }

    [Fact]
    public async Task PG5_Seeder_SeededPages_ArePublic_AudienceNull_LanguageEn_EmptyAuthor()
    {
        var store = await BootStoreAsync();
        var defaultPages = FirstBootSeeder.EnDefaultPages();
        var ct = TestContext.Current.CancellationToken;

        await using (var s = newSession(store))
        {
            await FirstBootSeeder.SeedDefaultPagesAsync(s, defaultPages, DateTimeOffset.UtcNow, ct);
            await s.SaveChangesAsync(ct);
        }

        await using var q = store.QuerySession();
        var systemRoot = await q.Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstAsync(ct);
        var terms = await q.Query<Page>()
            .Where(p => p.Slug == "terms" && p.ParentId == systemRoot.Id)
            .FirstAsync(ct);

        // The seeded page's shape: public (Audience null — the one place pages
        // differ from posts, ADR 0039 §3.4), authored-in `en` (the seeder's
        // source language), nested under the `system` root (ADR 0040),
        // platform content (no resident author — AuthorId empty), not a
        // draft / not deleted.
        Assert.Null(terms.Audience);
        Assert.Equal(FirstBootSeeder.SourceLanguage, terms.LanguageCode);
        Assert.Equal(systemRoot.Id, terms.ParentId);
        Assert.Equal(string.Empty, terms.AuthorId);
        Assert.Equal(PageKind.System, terms.Kind);
        Assert.False(terms.IsDraft);
        Assert.False(terms.IsDeleted);
        // Body + title carried verbatim (the byte-identical gate: the seeded
        // `Page` doc carries exactly the canonical `en` text, so /terms renders
        // the expected body).
        var expected = defaultPages.Single(p => p.Slug == "terms");
        Assert.Equal(expected.Body, terms.Body);
        Assert.Equal(expected.Title, terms.Title);
    }

    [Fact]
    public async Task PG5_Seeder_PrivacyAndConduct_UnderSystemRoot_DeFrBaselines_AtU02()
    {
        // ADR 0043 D1/D3 (SP U02) — the two new pages are part of the exact
        // seeded set under the `system` root (system/privacy + system/conduct
        // path shape), and each now carries exactly one `de` and one `fr`
        // PageTranslation row, attached to the page's **own** Id (the read
        // path GetTranslationsAsync(page.Id) finds them) — NOT on the
        // `system` root container (the ADR 0042 D6 parentage distinction).
        // The body parity with De/FrDefaultPages() is the structural pin:
        // the seeder flows the new rows from the baseline arrays (ADR 0043
        // D3 — generic loop, no seeder-branch change).
        var store = await BootStoreAsync();
        var defaultPages = FirstBootSeeder.EnDefaultPages();
        var ct = TestContext.Current.CancellationToken;

        await using (var s = newSession(store))
        {
            var pageIds = await FirstBootSeeder.SeedDefaultPagesAsync(s, defaultPages, DateTimeOffset.UtcNow, ct);
            await FirstBootSeeder.SeedPageTranslationsAsync(s, pageIds, DateTimeOffset.UtcNow, ct);
            await s.SaveChangesAsync(ct);
        }

        await using var q = store.QuerySession();
        var systemRoot = await q.Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstAsync(ct);

        foreach (var slug in new[] { "privacy", "conduct" })
        {
            var page = await q.Query<Page>()
                .Where(p => p.Slug == slug && p.ParentId == systemRoot.Id)
                .FirstAsync(ct);

            // The system/{slug} path shape: nested under the `system` root, the
            // same standing shape as terms/help (public, en, no resident author,
            // a system page — ADR 0040 §2 matrix inherited).
            Assert.Equal(systemRoot.Id, page.ParentId);
            Assert.Null(page.Audience);
            Assert.Equal(FirstBootSeeder.SourceLanguage, page.LanguageCode);
            Assert.Equal(string.Empty, page.AuthorId);
            Assert.Equal(PageKind.System, page.Kind);

            // The en body + title carried verbatim from EnDefaultPages() (the
            // byte-identical gate for the two new pages).
            var expected = defaultPages.Single(p => p.Slug == slug);
            Assert.Equal(expected.Body, page.Body);
            Assert.Equal(expected.Title, page.Title);

            // U02 final state: exactly one `de` + one `fr` PageTranslation row
            // on the page's own Id, body non-empty, parity with the canonical
            // De/FrDefaultPages() sources (ADR 0043 D3 — the loop is generic
            // over the baseline arrays, which U02 extends with these two slugs).
            var de = await q.Query<PageTranslation>()
                .Where(t => t.PageId == page.Id && t.LanguageCode == "de")
                .FirstOrDefaultAsync(ct);
            var fr = await q.Query<PageTranslation>()
                .Where(t => t.PageId == page.Id && t.LanguageCode == "fr")
                .FirstOrDefaultAsync(ct);
            Assert.NotNull(de);
            Assert.NotNull(fr);
            Assert.False(string.IsNullOrWhiteSpace(de!.Body));
            Assert.False(string.IsNullOrWhiteSpace(fr!.Body));
            Assert.False(string.IsNullOrWhiteSpace(de.Title));
            Assert.False(string.IsNullOrWhiteSpace(fr.Title));

            // Baseline parity with the canonical sources (structure preserved —
            // the ADR 0042 D2 bar holds for the two new pages too).
            var deBaseline = FirstBootSeeder.DeDefaultPages().Single(p => p.Slug == slug);
            var frBaseline = FirstBootSeeder.FrDefaultPages().Single(p => p.Slug == slug);
            Assert.Equal(deBaseline.Body, de.Body);
            Assert.Equal(deBaseline.Title, de.Title);
            Assert.Equal(frBaseline.Body, fr.Body);
            Assert.Equal(frBaseline.Title, fr.Title);

            // Exactly one row per (page, language) — no duplicates from the
            // create-if-missing idiom (ADR 0042 D1).
            var deCount = await q.Query<PageTranslation>()
                .Where(t => t.PageId == page.Id && t.LanguageCode == "de")
                .CountAsync(ct);
            var frCount = await q.Query<PageTranslation>()
                .Where(t => t.PageId == page.Id && t.LanguageCode == "fr")
                .CountAsync(ct);
            Assert.Equal(1, deCount);
            Assert.Equal(1, frCount);

            // The `system` root itself carries NO translations (ADR 0042 D6
            // parentage — the root is a container; the read path queries the
            // page's own id, never the root's).
            var onRoot = await q.Query<PageTranslation>()
                .Where(t => t.PageId == systemRoot.Id)
                .CountAsync(ct);
            Assert.Equal(0, onRoot);
        }
    }

    // ─── Shared helpers ─────────────────────────────────────────────────────

    /// <summary>Boot a fresh scratch store (M1 + M3 + Page doc types) and
    /// compose the two frozen seams (IUserInfoService + IAuthorizationService)
    /// — the shape the U02 authorization tests need (the PostServiceTests
    /// Services shape, re-pointed at Page).</summary>
    private async Task<(IDocumentStore store, UserInfoService userInfo, AuthorizationService auth)>
        BootAsync()
    {
        var connString = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(connString);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            PageDocTypes.Configure(opts);   // U02 — the two new Page/PageTranslation docs
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);

        var userInfo = new UserInfoService(store);
        var auth = new AuthorizationService(store, userInfo);
        return (store, userInfo, auth);
    }

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
            PageDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>Seed an enabled component + an explicit member row for the
    /// actor (the A0036 SeedCommunityWithMemberAsync shape, reused).</summary>
    private async Task SeedCommunityWithMemberAsync(
        IDocumentStore store, UserInfoService userInfo, string componentId, string member)
    {
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            session.Store(new Component
            {
                Id = componentId,
                Name = "PG Community",
                Enabled = true,
                Mandatory = false,
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await userInfo.SetCommunityMembershipAsync(componentId, member, "u-root-pg");
    }

    /// <summary>Plant a root-to-leaf chain of <paramref name="depth"/> pages,
    /// slugs a, b, c, … (up to h for depth 8). Node n (0-based) has id
    /// <c>{rootId}-{slug}</c> and (except the root) its parent is node n-1.</summary>
    private static async Task PlantChainAsync(IDocumentStore store, int depth, string rootId)
    {
        var ct = TestContext.Current.CancellationToken;
        const string alphabet = "abcdefgh";
        string? parentId = null;
        await using var session = store.OpenSession(new SessionOptions());
        for (int i = 0; i < depth; i++)
        {
            var slug = alphabet[i].ToString();
            var id = $"{rootId}-{slug}";
            session.Store(new Page
            {
                Id = id,
                Slug = slug,
                Title = slug.ToUpperInvariant(),
                Body = "body " + slug,
                ParentId = parentId,
            });
            parentId = id;
        }
        await session.SaveChangesAsync(ct);
    }

    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    private static async Task UpdatePageAsync(IDocumentStore store, string pageId, Action<Page> mutate)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.OpenSession(new SessionOptions());
        var page = await session.LoadAsync<Page>(pageId);
        if (page is null) throw new KeyNotFoundException($"Page '{pageId}' not found for update.");
        mutate(page);
        session.Store(page);
        await session.SaveChangesAsync(ct);
    }

    private static HashSet<string> RolesSet(params string[] roles) => roles.ToHashSet();

    /// <summary>Open the caller's in-flight write session (the C3 shape the
    /// write lanes commit into — the <c>AnnouncementServiceTests.newSession</c>
    /// shape, reused).</summary>
    private static IDocumentSession newSession(IDocumentStore store)
        => store.OpenSession(new Marten.Services.SessionOptions());

    /// <summary>All <see cref="AccessAudit"/> rows in the store (the
    /// <c>AnnouncementServiceTests.AuditRows</c> shape, reused).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> AuditRows(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>().ToListAsync(ct);
    }

    /// <summary>The <see cref="AccessAudit"/> rows for one (target, action)
    /// pair — the "assert the audit-row shape" helper (the audit-row
    /// Action/TargetKind/Via shape pin, asserted on every lane).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> AuditsFor(
        IDocumentStore store, string targetId, string action)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>()
            .Where(a => a.TargetId == targetId && a.Action == action)
            .ToListAsync(ct);
    }
}
