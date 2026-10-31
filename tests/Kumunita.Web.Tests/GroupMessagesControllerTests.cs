using System.Security.Claims;
using Kumunita.Core.GroupMessaging;
using Kumunita.Core.Messaging;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// Unit tests for <see cref="GroupMessagesController"/> (the resident
/// group-messaging surface — the N:1 generalization of the M9 1:1 surface
/// in ADR 0105). The pins this harness owns:
/// <list type="number">
/// <item><b>F5 — the toggle is the whole gate (carried to the N:1
///       surface).</b> When <see cref="IGroupMessagingService
///       .IsMessagingEnabledAsync"/> returns <c>false</c>, the
///       <c>Index</c> action renders the <c>group_message.disabled</c>
///       state and <b>never calls
///       <see cref="IGroupMessagingService.ListGroupsAsync"/></b> — the
///       disabled state is decided by the toggle read alone, before any
///       list data is touched (the same F5 pin as the M9 1:1 surface).</item>
/// <item><b>Non-leaky 404 — a non-participant's read.</b> When
///       <see cref="IGroupMessagingService.GetConversationAsync"/> throws
///       <see cref="KeyNotFoundException"/> (the actor is not a
///       participant, or the id is unknown — the service does not
///       distinguish), the <c>Thread</c> action returns a
///       <see cref="NotFoundResult"/> with no view data and never calls
///       <see cref="IGroupMessagingService.MarkReadAsync"/> — no 403, no
///       existence leak (the D3/D4 shape from ADR 0105, generalized to the
///       N:1 participant set).</item>
/// <item><b>Blank body — client-side validation is the convenience,
///       the server decides.</b> <c>Send</c> with a null/whitespace body
///       renders the <c>Thread</c> error view and never calls
///       <see cref="IGroupMessagingService.SendAsync"/> — the pin is "no
///       service call," not just "no exception."</item>
/// <item><b>The nav entry.</b> <c>_AccountNav.cshtml</c> renders the
///       <c>/group-messages</c> link behind the same
///       <c>IsMessagingEnabledAsync</c> +
///       <c>IsMessagingAllowedForAsync</c> gate as the M9 1:1 entry (the
///       group lane rides the resident's messaging standing, the C-M9·2
///       "enforced in the service, not only the view" pin carried to the
///       N:1 surface), and the entry carries the
///       <c>group_message.nav</c> key.</item>
/// </list>
/// <para>
/// The harness pattern mirrors <see cref="MessagesControllerTests"/>:
/// NSubstitute stands in for <see cref="IGroupMessagingService"/> /
/// <see cref="IMessagingService"/> / <see cref="IUserInfoService"/>;
/// <see cref="Kumunita.Web.Security.KumunitaPrincipal
/// .SubjectId"/> reads the <c>Kumunita.Sub</c> claim from a
/// <see cref="ClaimsPrincipal"/> built with
/// <see cref="Kumunita.Core.Identity.ClaimTypes.Subject"/>;
/// <see cref="Microsoft.AspNetCore.Mvc.Controller.TempData"/> is backed by
/// a no-op <see cref="ITempDataProvider"/> (the
/// <see cref="MessagesControllerTests"/> pattern) so the
/// <c>Send</c> action can touch <c>TempData</c> without a real session.
/// No Postgres, no Marten store — the controller consumes the
/// <b>interface</b> and NSubstitute fills in.
/// </para>
/// </summary>
public class GroupMessagesControllerTests
{
    private const string Actor  = "subj-resident-001";
    private const string RecIp1 = "subj-resident-002";
    private const string RecIp2 = "subj-resident-003";
    private const string GrpId  = "grp-test-0001";

    // ── 1 — F5: toggle off → disabled render, no list call ───────────────

    /// <summary>
    /// <c>GET /group-messages</c> when <see cref="IGroupMessagingService
    /// .IsMessagingEnabledAsync"/> returns <c>false</c>: the action
    /// returns a <see cref="ViewResult"/> whose model has
    /// <c>Disabled = true</c>, <c>Groups</c> empty, <c>Candidates</c>
    /// empty, and <b>neither <see cref="IGroupMessagingService
    /// .ListGroupsAsync"/> nor <see cref="IUserInfoService
    /// .GetProfilesAsync"/> is called</b> — the F5 pin carried to the
    /// N:1 surface: the disabled state is decided by the toggle read
    /// alone, and no list data is fetched at all.
    /// </summary>
    [Fact]
    public async Task GroupMessages_Index_ToggleOff_RendersDisabled_NoListCall()
    {
        var (controller, group, userInfo) = Build(false);

        var action = await controller.Index(null);
        var view   = Assert.IsType<ViewResult>(action);
        var model  = Assert.IsType<GroupMessagesController.GroupMessagesIndexViewModel>(view.ViewData.Model);

        Assert.True(model.Disabled);
        Assert.Empty(model.Groups);
        Assert.Empty(model.Candidates);
        Assert.Null(model.Pager);

        await group.DidNotReceiveWithAnyArgs()
            .ListGroupsAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int?>());
        await userInfo.DidNotReceiveWithAnyArgs()
            .GetProfilesAsync(Arg.Any<bool>());
    }

    // ── 2 — toggle on → list + picker render ─────────────────────────────

    /// <summary>
    /// <c>GET /group-messages</c> when the toggle is <c>true</c> and the
    /// per-actor gate allows: the action calls
    /// <see cref="IGroupMessagingService.ListGroupsAsync"/> with the
    /// actor's subject id + page 1, and
    /// <see cref="IUserInfoService.GetProfilesAsync"/> for the new-group
    /// picker (every eligible resident except the actor). The model carries
    /// the group list, the candidates, and a <c>null</c> <c>Pager</c>
    /// (one page, <c>HasMore = false</c> — the ADR 0090 null-pager means
    /// "one page").
    /// </summary>
    [Fact]
    public async Task GroupMessages_Index_ToggleOn_RendersGroupList()
    {
        var (controller, group, userInfo) = Build(true);

        var grp = new GroupConversationRef(
            Id: GrpId, CreatorId: Actor,
            ParticipantIds: new[] { Actor, RecIp1, RecIp2 },
            ParticipantDisplayNames: new[] { "Ben", "Anna", "Cara" },
            LastMessageBody: "Hi!", LastMessageAt: new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero),
            UnreadCount: 1,
            Title: null);
        group.ListGroupsAsync(Actor, 1, Arg.Any<int?>())
            .Returns(new GroupConversationList(new[] { grp }, HasMore: false));
        var rec = new Profile { SubjectId = RecIp1, DisplayName = "Anna",
                                MessagingOptIn = true, MessagingRestricted = false, Blocked = false };
        userInfo.GetProfilesAsync(Arg.Any<bool>())
            .Returns(Task.FromResult<IReadOnlyList<Profile>>(new[] { rec }));

        var action = await controller.Index(null);
        var view   = Assert.IsType<ViewResult>(action);
        var model  = Assert.IsType<GroupMessagesController.GroupMessagesIndexViewModel>(view.ViewData.Model);

        Assert.False(model.Disabled);
        var onlyGroup = Assert.Single(model.Groups);
        Assert.Equal(GrpId, onlyGroup.Id);
        Assert.Null(model.Pager);   // one page — the ADR 0090 null-pager
    }

    // ── 3 — non-leaky 404: a non-participant's thread read ───────────────

    /// <summary>
    /// <c>GET /group-messages/{id}</c> when
    /// <see cref="IGroupMessagingService.GetConversationAsync"/> throws
    /// <see cref="KeyNotFoundException"/> (the actor is not a participant,
    /// or the id is unknown — the service does not distinguish): the
    /// action returns a <see cref="NotFoundResult"/> with no view data and
    /// never calls <see cref="IGroupMessagingService
    /// .MarkReadAsync"/> — no 403, no existence leak (the D3/D4 shape
    /// from ADR 0105, generalized to the N:1 participant set).
    /// </summary>
    [Fact]
    public async Task GroupMessages_Thread_NonParticipant_Returns404_NoMarkRead()
    {
        var (controller, group, _) = Build(true);
        group.GetConversationAsync(GrpId, Actor, Arg.Any<int>(), Arg.Any<int?>())
            .Returns(Task.FromException<GroupConversationDetail>(
                new KeyNotFoundException("The conversation was not found.")));

        var action = await controller.Thread(GrpId, null);

        Assert.IsType<NotFoundResult>(action);
        await group.DidNotReceiveWithAnyArgs()
            .MarkReadAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    // ── 4 — blank body → error render, no service call ───────────────────

    /// <summary>
    /// <c>POST /group-messages/{id}/send</c> with a null/whitespace body:
    /// the action renders the <c>Thread</c> error view and never calls
    /// <see cref="IGroupMessagingService.SendAsync"/> — the pin is "no
    /// service call," not just "no exception" (the M9 1:1 U06 shape,
    /// carried to the N:1 surface).
    /// </summary>
    [Fact]
    public async Task GroupMessages_Send_BlankBody_RendersError_NoServiceCall()
    {
        var (controller, group, _) = Build(true);

        var action = await controller.Send(GrpId, "   ");
        var view   = Assert.IsType<ViewResult>(action);
        var model  = Assert.IsType<GroupMessagesController.GroupMessagesThreadViewModel>(view.ViewData.Model);

        Assert.True(model.Error);
        Assert.Null(model.Conversation);
        Assert.Empty(model.Messages);

        await group.DidNotReceiveWithAnyArgs()
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // ── 5 — per-actor gate disallowed → disabled render, no send ─────────

    /// <summary>
    /// <c>POST /group-messages/{id}/send</c> when
    /// <see cref="IMessagingService.IsMessagingAllowedForAsync"/> returns
    /// <c>false</c> (the per-actor gate — the M9 amendment carried to the
    /// N:1 surface: group messaging rides the resident's messaging
    /// standing): the action renders the disabled error view and never
    /// calls <see cref="IGroupMessagingService.SendAsync"/>.
    /// </summary>
    [Fact]
    public async Task GroupMessages_Send_PerActorDisallowed_Disabled_NoSendCall()
    {
        var (controller, group, _) = Build(true, perActorAllowed: false);

        var action = await controller.Send(GrpId, "Hello");
        var view   = Assert.IsType<ViewResult>(action);
        var model  = Assert.IsType<GroupMessagesController.GroupMessagesThreadViewModel>(view.ViewData.Model);

        Assert.True(model.Disabled);
        Assert.Null(model.Conversation);
        Assert.Empty(model.Messages);

        await group.DidNotReceiveWithAnyArgs()
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // ── 6 — the group link lives in the messages tab strip, not the account menu ──

    /// <summary>
    /// The group-messaging link moved out of the account menu into the
    /// <c>_MessagesTabs.cshtml</c> tab strip (the user's ask: "move the link
    /// to group chats from the account menu to tabs on the messages page to
    /// keep them differentiated"). Two pins, both view-source:
    /// <list type="bullet">
    /// <item><c>Views/Shared/_AccountNav.cshtml</c> keeps the M9
    /// <c>/messages</c> entry under the toggle + per-actor gate, and no longer
    /// carries a separate <c>/group-messages</c> link (the group lane is
    /// reached via the tab, not the menu).</item>
    /// <item><c>Views/Shared/_MessagesTabs.cshtml</c> (the tab strip's
    /// deliverable) carries the <c>/group-messages</c> href + the
    /// <c>group_message.nav</c> key, and self-gates on the
    /// <c>IsMessagingEnabledAsync</c> F5 read + the
    /// <c>IsMessagingAllowedForAsync</c> per-actor read (the same composite
    /// gate the account menu used — a disallowed resident sees no tabs).</item>
    /// </list>
    /// </summary>
    [Fact]
    public void GroupMessages_Link_In_Messages_Tabs_Not_AccountMenu()
    {
        var nav = ReadViewSource("Shared", "_AccountNav.cshtml");
        var tabs = ReadViewSource("Shared", "_MessagesTabs.cshtml");

        // The account menu keeps the M9 /messages entry under the composite gate…
        Assert.Contains("Messaging.IsMessagingEnabledAsync()", nav, StringComparison.Ordinal);
        Assert.Contains("IsMessagingAllowedForAsync", nav, StringComparison.Ordinal);
        Assert.Contains("href=\"/messages\"", nav, StringComparison.Ordinal);
        // …and no longer carries a separate /group-messages link (moved to the tab).
        Assert.DoesNotContain("href=\"/group-messages\"", nav, StringComparison.Ordinal);

        // The tab strip is the group lane's new home: it carries the
        // /group-messages href + the group_message.nav key…
        Assert.Contains("href=\"/group-messages\"", tabs, StringComparison.Ordinal);
        Assert.Contains("group_message.nav", tabs, StringComparison.Ordinal);
        Assert.Contains(
            Kumunita.Core.Localization.KnownTranslationKeys.EnValues["group_message.nav"],
            tabs, StringComparison.Ordinal);
        // …and self-gates on the same composite gate (F5 + per-actor).
        Assert.Contains("IsMessagingEnabledAsync()", tabs, StringComparison.Ordinal);
        Assert.Contains("IsMessagingAllowedForAsync", tabs, StringComparison.Ordinal);
    }

    // ── Harness ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="GroupMessagesController"/> with NSubstitute
    /// stand-ins for <see cref="IGroupMessagingService"/> /
    /// <see cref="IMessagingService"/> / <see cref="IUserInfoService"/>
    /// (the <see cref="MessagesControllerTests"/> pattern). The
    /// <c>IsMessagingEnabledAsync</c> stub is set by the
    /// <paramref name="enabled"/> parameter; the per-actor gate defaults
    /// to <c>true</c> (the harness actor is messaging-allowed — a test
    /// that wants to exercise the disallowed path passes
    /// <paramref name="perActorAllowed"/> = false).
    /// <see cref="Microsoft.AspNetCore.Mvc.Controller.TempData"/> is backed
    /// by a no-op <see cref="ITempDataProvider"/> (the
    /// <see cref="MessagesControllerTests"/> pattern) so <c>Send</c> can
    /// set <c>TempData["info"]</c> without a real session.
    /// </summary>
    private static (GroupMessagesController controller, IGroupMessagingService group,
                    IUserInfoService userInfo)
        Build(bool enabled, bool perActorAllowed = true)
    {
        var group = Substitute.For<IGroupMessagingService>();
        group.IsMessagingEnabledAsync().Returns(enabled);

        var messaging = Substitute.For<IMessagingService>();
        // The per-actor gate (the M9 amendment, carried to the N:1 surface)
        // — the controller reads this on the 1:1 service (the group lane
        // rides the resident's messaging standing).
        messaging.IsMessagingAllowedForAsync(Arg.Any<string>()).Returns(perActorAllowed);

        var userInfo = Substitute.For<IUserInfoService>();
        // Default: the actor's profile (for the "You" sender label on the
        // thread). Tests that need specific profile data override this.
        userInfo.GetProfileAsync(Actor)
            .Returns(new Profile { SubjectId = Actor, DisplayName = "Ben" });

        var controller = new GroupMessagesController(
            NullLogger<GroupMessagesController>.Instance, group, messaging, userInfo);
        WireControllerContext(controller, Actor);

        return (controller, group, userInfo);
    }

    /// <summary>
    /// Wires the <see cref="Microsoft.AspNetCore.Mvc.Controller
    /// .ControllerContext"/> with a signed-in
    /// <see cref="ClaimsPrincipal"/> carrying the
    /// <see cref="Kumunita.Core.Identity.ClaimTypes.Subject"/> claim
    /// (value = <paramref name="subjectId"/>), and a no-op
    /// <see cref="ITempDataProvider"/> (the
    /// <see cref="MessagesControllerTests"/> pattern — the
    /// <c>Send</c> action touches <c>TempData["info"]</c> on the success
    /// path).
    /// </summary>
    private static void WireControllerContext(Microsoft.AspNetCore.Mvc.Controller controller, string subjectId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new List<Claim> { new(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId) },
                authenticationType: "test"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };

        controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(), new NoOpTempDataProvider());
    }

    /// <summary>
    /// Reads a view's source by walking up from the test's working
    /// directory to the repo root (the <c>Kumunita.slnx</c> file) — the
    /// same pattern <see cref="MessagesControllerTests
    /// .ReadViewSource"/> uses for its <c>_AccountNav.cshtml</c> pin.
    /// </summary>
    private static string ReadViewSource(string dir, string file)
    {
        var dirInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dirInfo is not null && !File.Exists(Path.Combine(dirInfo.FullName, "Kumunita.slnx")))
            dirInfo = dirInfo.Parent;
        Assert.NotNull(dirInfo);
        var path = Path.Combine(dirInfo!.FullName, "src", "Kumunita.Web", "Views", dir, file);
        Assert.True(File.Exists(path), $"view {path} not found");
        return File.ReadAllText(path);
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }
}
