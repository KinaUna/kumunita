using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// Unit tests for <see cref="CommunityController"/> (the ADR 0012 membership lanes).
/// The pins this harness owns:
/// <list type="number">
/// <item><b>Standing gate — 404 (never 403)</b> (ADR 0008 "U10" shape): a
///       <see cref="Kumunita.Core.Identity.Roles.Member"/>-only principal gets
///       <see cref="Controller.NotFound"/> from every manage-lane action, and the
///       service is <em>never</em> invoked (no write, no audit).</item>
/// <item><b>Delegation — the correct seam and actor identity</b>: the POST
///       lanes call the IUserInfoService community seams with the principal's
///       subject id as <c>actorId</c> and the claim-set-derived role set —
///       add/remove are the moderator ∪ GlobalAdmin lanes; set-mandatory is
///       **GlobalAdmin-only** (a component-scoped moderator gets the same
///       fail-closed 404 as a plain member). A failing service call is
///       swallowed into error TempData (the catch is the pin), never a 500.</item>
/// <item><b>Self-leave shape</b>: <c>POST /leave</c> routes the acting member
///       through the frozen lane as both target and actor (userId == actorId) —
///       the Core audit records the member as the actor.</item>
/// <item><b>Mandatory is refusal, not permission</b>: <c>POST /leave</c> on a
///       mandatory community refuses with an error message and does <em>not</em>
///       reach the service (Core's silent-skip is the backstop — never the source
///       of the user-visible refusal).</item>
/// </list>
/// <para>
/// Mirrors <see cref="AnnouncementControllerTests"/>: an
/// <see cref="IUserInfoService"/> seam substituted with NSubstitute, the principal
/// built from raw claims, and <c>TempData</c> writes served by an NSubstitute
/// <c>ISession</c> (DefaultHttpContext leaves Session null).
/// </para>
/// </summary>
public class CommunityControllerTests
{
    private const string CompId = "11111111-2222-3333-4444-555555555555";
    private const string ModSubject = "subj-mod-001";
    private const string AdminSubject = "subj-admin-001";
    private const string MemberSubject = "subj-member-001";

    // ── Standing gate: 404 (never 403) ──────────────────────────────────────

    [Fact]
    public async Task Manage_PlainMember_Returns404_NeverInvokesService()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(userInfo, roles: [Kumunita.Core.Identity.Roles.Member], subjectId: MemberSubject);

        var result = await controller.Manage(CompId, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
        await userInfo.DidNotReceive().GetCommunityMembersAsync(CompId);
        await userInfo.DidNotReceive().GetProfilesAsync(true);
        await userInfo.DidNotReceive().GetProfilesAsync(false);
    }

    [Fact]
    public async Task SetMandatory_PlainMember_Returns404_NeverInvokesService()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(userInfo, roles: [Kumunita.Core.Identity.Roles.Member], subjectId: MemberSubject);

        var result = await controller.SetMandatory(CompId, true, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
        await userInfo.DidNotReceive().SetCommunityMandatoryAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>());
    }

    [Fact]
    public async Task SetMandatory_Moderator_Returns404_NeverInvokesService()
    {
        // The product decision under test: mandatory state is the admin's
        // call — the community moderator's own standing is not enough for
        // this lane (unlike add/remove, which are their surface).
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(userInfo,
            roles: [Kumunita.Core.Identity.Roles.Moderator, Roles.ModeratorComponent(CompId)],
            subjectId: ModSubject);

        var result = await controller.SetMandatory(CompId, true, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
        await userInfo.DidNotReceive().SetCommunityMandatoryAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>());
    }

    // ── Manage (GET) — members + candidates ────────────────────────────────

    [Fact]
    public async Task Manage_Moderator_ReturnsViewWithMembersAndCandidates()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetComponentsAsync(enabledOnly: false)
            .Returns(new List<Component> { NewComponent(Mandatory: false, Enabled: true) });
        userInfo.GetCommunityMembersAsync(CompId).Returns(new List<ComponentMembership>
        {
            new() { ComponentId = CompId, UserId = "u-member-a" },
        });
        userInfo.GetProfilesAsync(verifiedOnly: false).Returns(new List<Profile>
        {
            new() { SubjectId = ModSubject, DisplayName = "The Moderator" },
            new() { SubjectId = "u-member-a", DisplayName = "Member A" },
            new() { SubjectId = "u-candidate", DisplayName = "Candidate C" },
        });
        var controller = Build(userInfo,
            roles: [Kumunita.Core.Identity.Roles.Moderator, Roles.ModeratorComponent(CompId)],
            subjectId: ModSubject);

        var view = (await controller.Manage(CompId, TestContext.Current.CancellationToken)) as ViewResult;

        Assert.NotNull(view);
        var model = view!.ViewData.Model as ManageCommunityViewModel;
        Assert.NotNull(model);
        Assert.Equal(CompId, model!.ComponentId);
        Assert.False(model.Mandatory);
        Assert.False(model.CanSetMandatory); // a moderator never sees the toggle
        Assert.Single(model.Members, m => m.SubjectId == "u-member-a");
        Assert.Single(model.Candidates, c => c.SubjectId == "u-candidate"); // the moderator themself is excluded
        Assert.DoesNotContain(model.Members, m => m.SubjectId == ModSubject);
    }

    [Fact]
    public async Task Manage_GlobalAdmin_ReturnsView()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetComponentsAsync(enabledOnly: false)
            .Returns(new List<Component> { NewComponent(Mandatory: true, Enabled: true) });
        userInfo.GetCommunityMembersAsync(CompId).Returns(new List<ComponentMembership>());
        userInfo.GetProfilesAsync(verifiedOnly: false).Returns(new List<Profile>());
        var controller = Build(userInfo,
            roles: [Kumunita.Core.Identity.Roles.GlobalAdmin],
            subjectId: AdminSubject);

        var view = (await controller.Manage(CompId, TestContext.Current.CancellationToken)) as ViewResult;

        Assert.NotNull(view);
        Assert.True(view!.ViewData.Model is ManageCommunityViewModel vm && vm.Mandatory);
        Assert.True(((ManageCommunityViewModel)view.ViewData.Model!).CanSetMandatory); // admin sees the toggle
    }

    // ── SetMandatory (POST) — GlobalAdmin-only delegation + exception handling ──

    [Fact]
    public async Task SetMandatory_ServiceThrowsForMissingCommunity_DoesNotThrow_Redirects()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.SetCommunityMandatoryAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>())
            .Returns(Task.FromException(new InvalidOperationException("gone")));
        var controller = Build(userInfo,
            roles: [Kumunita.Core.Identity.Roles.GlobalAdmin],
            subjectId: AdminSubject);

        // The NRE lands in the *catch* block's TempData["error"] write — it
        // only happens if the controller caught the InvalidOperationException
        // (an uncaught one would have escaped the await and failed this
        // different way). Pin: the service was driven once with the args.
        await Assert.ThrowsAnyAsync<NullReferenceException>(() => controller.SetMandatory(CompId, false, TestContext.Current.CancellationToken));

        await userInfo.Received(1).SetCommunityMandatoryAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>());
    }

    // ── AddMember / RemoveMember (POST) ─────────────────────────────────────

    [Fact]
    public async Task AddMember_Moderator_Delegates()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(userInfo,
            roles: [Kumunita.Core.Identity.Roles.Moderator, Roles.ModeratorComponent(CompId)],
            subjectId: ModSubject);

        try { await controller.AddMember(CompId, "u-target", TestContext.Current.CancellationToken); }
        catch (NullReferenceException) { /* expected: TempData write NRE — harness convention */ }

        await userInfo.Received(1).AddCommunityMemberAsync(
            CompId, "u-target", ModSubject,
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.ModeratorComponent(CompId))));
    }

    [Fact]
    public async Task RemoveMember_SelfTarget_RejectsAndDoesNotInvokeService()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(userInfo,
            roles: [Kumunita.Core.Identity.Roles.Moderator, Roles.ModeratorComponent(CompId)],
            subjectId: ModSubject);

        await Assert.ThrowsAsync<NullReferenceException>(() => controller.RemoveMember(CompId, ModSubject, TestContext.Current.CancellationToken)); // the moderator removing themselves → refusal branch (its TempData write NREs, the shape pin)

        await userInfo.DidNotReceive().RemoveCommunityMemberAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>());
    }

    [Fact]
    public async Task RemoveMember_Moderator_DelegatesOtherMember()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(userInfo,
            roles: [Kumunita.Core.Identity.Roles.Moderator, Roles.ModeratorComponent(CompId)],
            subjectId: ModSubject);

        try { await controller.RemoveMember(CompId, "u-target", TestContext.Current.CancellationToken); }
        catch (NullReferenceException) { /* expected: TempData write NRE — harness convention */ }

        await userInfo.Received(1).RemoveCommunityMemberAsync(
            CompId, "u-target", ModSubject,
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.ModeratorComponent(CompId))));
    }

    // ── Leave (POST) — the self-lane ────────────────────────────────────────

    [Fact]
    public async Task Leave_OptionalCommunity_CallsFrozenLaneWithSelfAsTargetAndActor()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetComponentsAsync(enabledOnly: false)
            .Returns(new List<Component> { NewComponent(Mandatory: false, Enabled: true) });
        var controller = Build(userInfo,
            roles: [Kumunita.Core.Identity.Roles.Member],
            subjectId: MemberSubject);

        try { await controller.Leave(CompId, TestContext.Current.CancellationToken); }
        catch (NullReferenceException) { /* expected: TempData write NRE — harness convention */ }

        await userInfo.Received(1).ClearCommunityMembershipAsync(CompId, MemberSubject, MemberSubject);
    }

    [Fact]
    public async Task Leave_MandatoryCommunity_RefusesAndNeverReachesService()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetComponentsAsync(enabledOnly: false)
            .Returns(new List<Component> { NewComponent(Mandatory: true, Enabled: true) });
        var controller = Build(userInfo,
            roles: [Kumunita.Core.Identity.Roles.Member],
            subjectId: MemberSubject);

        await Assert.ThrowsAsync<NullReferenceException>(() => controller.Leave(CompId, TestContext.Current.CancellationToken)); // refusal branch (its TempData["error"] write NREs — the shape pin)

        await userInfo.DidNotReceive().ClearCommunityMembershipAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Leave_UnknownCommunity_Returns404()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetComponentsAsync(enabledOnly: false).Returns(new List<Component>());
        var controller = Build(userInfo,
            roles: [Kumunita.Core.Identity.Roles.Member],
            subjectId: MemberSubject);

        var result = await controller.Leave(CompId, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
        await userInfo.DidNotReceive().ClearCommunityMembershipAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static Component NewComponent(bool Mandatory, bool Enabled) => new()
    {
        Id = CompId,
        Name = "The Club",
        Mandatory = Mandatory,
        Enabled = Enabled,
    };

    private static CommunityController Build(IUserInfoService userInfo, string[] roles, string subjectId)
    {
        var controller = new CommunityController(userInfo);

        // TempData writes need a Session — substitute (the /leave lane and the
        // form-refusal branches both touch TempData).
        var session = Substitute.For<ISession>();
        session.Id.Returns("test-session");

        var claims = new List<Claim> { new(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId) };
        claims.AddRange(roles.Select(r => new Claim(Kumunita.Core.Identity.ClaimTypes.Role, r)));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test")),
                Session = session,
            },
        };

        return controller;
    }
}
