using Kumunita.Core;
using Kumunita.Core.Authorization;
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
    // that is the one place pages differ from posts.

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
        // The one place pages differ from posts (ADR 0039 §3.4): Audience is
        // null-allowed, and the frozen Decide() branch 5 treats null as public.
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
    public void PG_CheckCreateStanding_CommunityModerator_AllowsScopedPage()
    {
        var page = new Page { Id = "pg-stand-create-c", ComponentId = "comp-x" };
        PageService.CheckCreateStanding("u-mod", RolesSet(Roles.ModeratorComponent("comp-x")), page);
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
    public void PG_CheckEditStanding_Author_Allows()
    {
        var page = new Page { Id = "pg-stand-edit", AuthorId = "u-author" };
        PageService.CheckEditStanding("u-author", RolesSet(Roles.Member), page);
    }

    [Fact]
    public void PG_CheckEditStanding_GlobalAdmin_AllowsAnyPage()
    {
        var page = new Page { Id = "pg-stand-edit-g", AuthorId = "u-someone-else" };
        PageService.CheckEditStanding("u-admin", RolesSet(Roles.GlobalAdmin), page);
    }

    [Fact]
    public void PG_CheckEditStanding_CommunityModerator_AllowsScopedPage()
    {
        var page = new Page { Id = "pg-stand-edit-c", AuthorId = "u-someone-else", ComponentId = "comp-x" };
        PageService.CheckEditStanding("u-mod", RolesSet(Roles.ModeratorComponent("comp-x")), page);
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
    public void PG_CheckTranslateStanding_CommunityModerator_AllowsScopedPage()
    {
        var page = new Page { Id = "pg-stand-trans-c", ComponentId = "comp-x" };
        PageService.CheckTranslateStanding("u-mod", RolesSet(Roles.ModeratorComponent("comp-x")), page);
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
}
