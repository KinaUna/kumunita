using Kumunita.Core.Identity;
using Kumunita.Core.Pages;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The C3-01 <see cref="PageStandingPolicy"/> truth table — the ADR 0040 /
/// ADR 0161 page standing matrix (create / edit / translate gates + the
/// <c>CanTranslatePage</c> display probe), lifted from the
/// <c>PageServiceTests</c> "PG_*" family (section 8) and asserted against the
/// policy directly. A **pure** unit — no <c>PostgresFixture</c>, no
/// <see cref="Marten.IDocumentStore"/>: just the <see cref="Page"/> POCO + the
/// <see cref="Roles"/> constants. Behaviour is byte-identical to the pre-C3-01
/// <c>PageService</c> statics; this file proves the relocation (the ADR 0290
/// named-seam shrink) kept every input/allow/deny/throw combination intact.
/// <para>
/// **Gate seam-test rule:** the extracted <c>*Policy</c> must be named by a
/// **plain** (non-<c>PostgresFixture</c>) unit test — this file is that test
/// (the predecessor's <c>EventStandingPolicyTests</c> shape, C04).
/// </para>
/// </summary>
public class PageStandingPolicyTests
{
    private static HashSet<string> RolesSet(params string[] roles) => roles.ToHashSet();

    private static void ThrowsUnauthorized(Action action)
    {
        Assert.Throws<UnauthorizedAccessException>(action);
    }

    // ─── 8 — Standing matrix (ADR 0039 §3.7 / ADR 0040 — pure role-claim checks) ──
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
        PageStandingPolicy.CheckCreateStanding("u-admin", RolesSet(Roles.GlobalAdmin), page);
    }

    [Fact]
    public void PG_CheckCreateStanding_CommunityModerator_Denied_SystemPage()
    {
        // ADR 0040: a system page (the default Kind) is GlobalAdmin-only for
        // create — a community Moderator has no standing on it.
        var page = new Page { Id = "pg-stand-create-c", ComponentId = "comp-x" };
        Assert.Throws<UnauthorizedAccessException>(
            () => PageStandingPolicy.CheckCreateStanding("u-mod", RolesSet(Roles.ModeratorComponent("comp-x")), page));
    }

    [Fact]
    public void PG_CheckCreateStanding_PlainMember_Denies()
    {
        var page = new Page { Id = "pg-stand-create-m", ComponentId = "comp-x" };
        Assert.Throws<UnauthorizedAccessException>(
            () => PageStandingPolicy.CheckCreateStanding("u-member", RolesSet(Roles.Member), page));
    }

    [Fact]
    public void PG_CheckCreateStanding_FlatPage_ModeratorOfOtherComp_Denies()
    {
        // A flat/public page (ComponentId null) requires a GlobalAdmin — a
        // moderator claim for an unrelated community does not qualify.
        var page = new Page { Id = "pg-stand-create-flat", ComponentId = null };
        Assert.Throws<UnauthorizedAccessException>(
            () => PageStandingPolicy.CheckCreateStanding("u-mod", RolesSet(Roles.ModeratorComponent("comp-other")), page));
    }

    [Fact]
    public void PG_CheckEditStanding_Author_Allows_BlogPage()
    {
        // ADR 0040: the author lane applies to a blog page (Kind = User).
        var page = new Page { Id = "pg-stand-edit", AuthorId = "u-author", Kind = PageKind.User };
        PageStandingPolicy.CheckEditStanding("u-author", RolesSet(Roles.Member), page);
    }

    [Fact]
    public void PG_CheckEditStanding_GlobalAdmin_AllowsAnyPage()
    {
        var page = new Page { Id = "pg-stand-edit-g", AuthorId = "u-someone-else" };
        PageStandingPolicy.CheckEditStanding("u-admin", RolesSet(Roles.GlobalAdmin), page);
    }

    [Fact]
    public void PG_CheckEditStanding_CommunityModerator_Denied_SystemPage()
    {
        // ADR 0040: a system page is GlobalAdmin-only for edit — a community
        // Moderator has no standing on it.
        var page = new Page { Id = "pg-stand-edit-c", AuthorId = "u-someone-else", ComponentId = "comp-x" };
        Assert.Throws<UnauthorizedAccessException>(
            () => PageStandingPolicy.CheckEditStanding("u-mod", RolesSet(Roles.ModeratorComponent("comp-x")), page));
    }

    [Fact]
    public void PG_CheckEditStanding_PlainMember_Denies()
    {
        var page = new Page { Id = "pg-stand-edit-m", AuthorId = "u-someone-else", ComponentId = "comp-x" };
        Assert.Throws<UnauthorizedAccessException>(
            () => PageStandingPolicy.CheckEditStanding("u-member", RolesSet(Roles.Member), page));
    }

    [Fact]
    public void PG_CheckTranslateStanding_GlobalAdmin_AllowsAnyPage()
    {
        var page = new Page { Id = "pg-stand-trans-g" };
        PageStandingPolicy.CheckTranslateStanding("u-admin", RolesSet(Roles.GlobalAdmin), page);
    }

    [Fact]
    public void PG_CheckTranslateStanding_Translator_AllowsAnyPage()
    {
        var page = new Page { Id = "pg-stand-trans-t" };
        PageStandingPolicy.CheckTranslateStanding("u-translator", RolesSet(Roles.Translator), page);
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
            () => PageStandingPolicy.CheckTranslateStanding("u-mod", RolesSet(Roles.ModeratorComponent("comp-x")), systemPage));
        Assert.Throws<UnauthorizedAccessException>(
            () => PageStandingPolicy.CheckTranslateStanding("u-mod", RolesSet(Roles.ModeratorComponent("comp-x")), blogPage));
    }

    [Fact]
    public void PG_CheckTranslateStanding_FlatPage_ModeratorOfOtherComp_Denies()
    {
        // A flat/public page has no community to moderate — the component-
        // moderator standing does not qualify; only GlobalAdmin / Translator.
        var page = new Page { Id = "pg-stand-trans-flat", ComponentId = null };
        Assert.Throws<UnauthorizedAccessException>(
            () => PageStandingPolicy.CheckTranslateStanding("u-mod", RolesSet(Roles.ModeratorComponent("comp-other")), page));
    }

    [Fact]
    public void PG_CheckTranslateStanding_PlainMember_Denies()
    {
        var page = new Page { Id = "pg-stand-trans-m", ComponentId = "comp-x" };
        Assert.Throws<UnauthorizedAccessException>(
            () => PageStandingPolicy.CheckTranslateStanding("u-member", RolesSet(Roles.Member), page));
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
        Assert.True(PageStandingPolicy.CanTranslatePage("u-admin", RolesSet(Roles.GlobalAdmin), scoped));
        Assert.True(PageStandingPolicy.CanTranslatePage("u-admin", RolesSet(Roles.GlobalAdmin), flat));
    }

    [Fact]
    public void PG6_CanTranslatePage_Translator_AllowsAnyPage()
    {
        // Translator qualifies on a scoped page AND a flat/public page.
        var scoped = new Page { Id = "pg6-t-tr-s", ComponentId = "comp-x" };
        var flat = new Page { Id = "pg6-t-tr-f", ComponentId = null };
        Assert.True(PageStandingPolicy.CanTranslatePage("u-translator", RolesSet(Roles.Translator), scoped));
        Assert.True(PageStandingPolicy.CanTranslatePage("u-translator", RolesSet(Roles.Translator), flat));
    }

    [Fact]
    public void PG6_CanTranslatePage_CommunityModerator_Denied_AllKinds()
    {
        // ADR 0040: a community Moderator has no translation standing on
        // either kind (the ADR 0039 §3.7 moderator lane is removed).
        var systemPage = new Page { Id = "pg6-t-mod-s", ComponentId = "comp-x" };
        var blogPage = new Page { Id = "pg6-t-mod-b", ComponentId = "comp-x", Kind = PageKind.User };
        var modRoles = RolesSet(Roles.Moderator, Roles.ModeratorComponent("comp-x"));

        Assert.False(PageStandingPolicy.CanTranslatePage("u-mod", modRoles, systemPage));
        Assert.False(PageStandingPolicy.CanTranslatePage("u-mod", modRoles, blogPage));
    }

    [Fact]
    public void PG6_CanTranslatePage_PlainMember_Denies()
    {
        // A plain resident (no standing claim) never qualifies — scoped or
        // flat, a Translator-elsewhere, nothing.
        var scoped = new Page { Id = "pg6-t-mem-s", ComponentId = "comp-x" };
        var flat = new Page { Id = "pg6-t-mem-f", ComponentId = null };
        Assert.False(PageStandingPolicy.CanTranslatePage("u-member", RolesSet(Roles.Member), scoped));
        Assert.False(PageStandingPolicy.CanTranslatePage("u-member", RolesSet(Roles.Member), flat));
    }

    // ─── ADR 0161 — the author-of-a-blog-page branch (an editor can translate) ─
    //
    // ADR 0161 closes the "a user who can edit a page should also be able to
    // add/edit/remove translations" gap: the author of a PageKind.User (blog)
    // page now qualifies for the translation standing — the same branch the
    // edit standing uses (a blog page's edit standing is author ∪ GlobalAdmin,
    // ADR 0040). A system page's author is an audit identity, not a standing
    // branch (a system page is GlobalAdmin-only), so the branch is scoped to
    // the User kind.

    [Fact]
    public void PG6_CanTranslatePage_AuthorOfBlogPage_Allows()
    {
        // A plain resident (no elevated role) who is the author of a blog
        // page may translate it — the ADR 0161 author branch (the same branch
        // as the edit standing).
        var blogPage = new Page
        {
            Id = "pg6-t-auth-b", Kind = PageKind.User,
            AuthorId = "u-author", ComponentId = "comp-x",
        };
        Assert.True(PageStandingPolicy.CanTranslatePage("u-author", RolesSet(Roles.Member), blogPage));
    }

    [Fact]
    public void PG6_CanTranslatePage_AuthorOfSystemPage_Denies()
    {
        // A system page's author is an audit identity, not a standing branch
        // (a system page is GlobalAdmin-only, ADR 0040) — the ADR 0161 author
        // branch is scoped to the User kind, so a plain resident who happens
        // to be the stored author of a system page still cannot translate it.
        var systemPage = new Page
        {
            Id = "pg6-t-auth-s", Kind = PageKind.System,
            AuthorId = "u-author", ComponentId = "comp-x",
        };
        Assert.False(PageStandingPolicy.CanTranslatePage("u-author", RolesSet(Roles.Member), systemPage));
    }

    [Fact]
    public void PG6_CanTranslatePage_NonAuthorOfBlogPage_Denies()
    {
        // The author branch is author-specific: a plain resident who is NOT
        // the blog page's author has no translation standing on it.
        var blogPage = new Page
        {
            Id = "pg6-t-notauth", Kind = PageKind.User,
            AuthorId = "u-someone-else", ComponentId = "comp-x",
        };
        Assert.False(PageStandingPolicy.CanTranslatePage("u-resident", RolesSet(Roles.Member), blogPage));
    }

    [Fact]
    public void PG6_CheckTranslateStanding_AuthorOfBlogPage_Allows()
    {
        // The write-lane gate accepts the author of a blog page (ADR 0161) —
        // no exception thrown.
        var blogPage = new Page
        {
            Id = "pg6-t-gate-auth", Kind = PageKind.User,
            AuthorId = "u-author", ComponentId = "comp-x",
        };
        PageStandingPolicy.CheckTranslateStanding("u-author", RolesSet(Roles.Member), blogPage);
    }

    [Fact]
    public void PG6_CanTranslatePage_NullPageOrBlankActor_Denies()
    {
        // The probe is pure (no exception) — a null page or a blank actor
        // is a flat denial (the display flag is simply unset; the write-lane
        // gate is the one that throws for those shapes).
        var scoped = new Page { Id = "pg6-t-n", ComponentId = "comp-x" };
        Assert.False(PageStandingPolicy.CanTranslatePage("u-admin", RolesSet(Roles.GlobalAdmin), null));
        Assert.False(PageStandingPolicy.CanTranslatePage("", RolesSet(Roles.GlobalAdmin), scoped));
    }

    [Fact]
    public void PG_CheckStanding_NullPage_KeyNotFound()
    {
        Assert.Throws<KeyNotFoundException>(
            () => PageStandingPolicy.CheckCreateStanding("u-a", RolesSet(Roles.Member), null));
        Assert.Throws<KeyNotFoundException>(
            () => PageStandingPolicy.CheckEditStanding("u-a", RolesSet(Roles.Member), null));
        Assert.Throws<KeyNotFoundException>(
            () => PageStandingPolicy.CheckTranslateStanding("u-a", RolesSet(Roles.Member), null));
    }
}
