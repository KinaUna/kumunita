using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.Messaging;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// Unit tests for <see cref="MessagesController"/> (the M9 resident
/// surface, ADR 0105 U04) and <see cref="AdminMessagingController"/> (the
/// M9 admin toggle surface, ADR 0105 U05). The pins this harness owns:
/// <list type="number">
/// <item><b>F5 — the toggle is the whole gate.</b> When
///       <see cref="IMessagingService.IsMessagingEnabledAsync"/> returns
///       <c>false</c>, the <c>Index</c> action renders the
///       <c>message.disabled</c> state and <b>never calls
///       <see cref="IMessagingService.ListConversationsAsync"/></b> — the
///       disabled state is decided by the toggle read alone, before any
///       list data is touched.</item>
/// <item><b>C-M9·1 — the non-leaky 404.</b> When
///       <see cref="IMessagingService.GetConversationAsync"/> throws
///       <see cref="KeyNotFoundException"/> (the actor is not a
///       participant, or the id is unknown — the service does not
///       distinguish), the <c>Thread</c> action returns a
///       <see cref="NotFoundResult"/> with no view data and never calls
///       <see cref="IMessagingService.MarkReadAsync"/> — no 403, no
///       existence leak (D3/D4).</item>
/// <item><b>Blank body — client-side validation is the convenience,
///       the server decides.</b> <c>Send</c> with a null/whitespace body
///       renders the <c>Thread</c> error view and never calls
///       <see cref="IMessagingService.SendAsync"/> — the pin is "no
///       service call," not just "no exception."</item>
/// <item><b>Admin toggle hand-off (F6).</b>
///       <c>AdminMessagingController.Save</c> maps the form's
///       <c>enabled</c> value + the actor's subject id and calls
///       <see cref="IMessagingService.SetMessagingEnabledAsync"/> exactly
///       once; the <c>messaging.toggle</c> audit row is the
///       <b>service's</b> (pinned in U02's Core tests) — this test pins
///       the controller's call shape only.</item>
/// </list>
/// <para>
/// The harness pattern mirrors <see cref="AnnouncementControllerTests"/>:
/// NSubstitute stands in for <see cref="IMessagingService"/> and
/// <see cref="IUserInfoService"/>; <see cref="KumunitaPrincipal
/// .SubjectId"/> reads the <c>Kumunita.Sub</c> claim from a
/// <see cref="ClaimsPrincipal"/> built with
/// <see cref="ClaimTypes.Subject"/>; <see cref="TempData"/> is backed by a
/// no-op <see cref="ITempDataProvider"/> (the
/// <see cref="AccountControllerSignupGateTests"/> pattern) so the
/// <c>Send</c> and <c>Save</c> actions can touch <c>TempData</c> without a
/// real session. No Postgres, no Marten store — the controller consumes
/// the <b>interface</b> and NSubstitute fills in.
/// </para>
/// <para>
/// The two nav tests (<c>Messages_Nav_ToggleOff_EntryHidden</c> /
/// <c>Messages_Nav_ToggleOn_EntryPresent</c>) cannot drive the
/// <c>_AccountNav.cshtml</c> Razor partial through a controller
/// substitute (the partial is rendered in the layout, not by a
/// <see cref="Controller"/> action) — they follow the
/// <see cref="SearchControllerTests.Search_NavBox_Rendered_ForAnonymous"/>
/// pattern: assert that the view source carries the
/// <c>IsMessagingEnabledAsync()</c> gate (the F5 read) and, when the
/// gate's body is present in the markup, that it wraps the
/// <c>/messages</c> link + the <c>message.nav</c> key (both verified
/// against <c>Views/Shared/_AccountNav.cshtml</c> — the U04 deliverable
/// markup).
/// </para>
/// </summary>
public class MessagesControllerTests
{
    private const string Actor   = "subj-resident-001";
    private const string OtherId = "subj-resident-002";
    private const string ConvoId = "conv-test-0001";

    // ── 1 — F5: toggle off → disabled render, no list call ─────────────

    /// <summary>
    /// <c>GET /messages</c> when <see cref="IMessagingService
    /// .IsMessagingEnabledAsync"/> returns <c>false</c>: the action
    /// returns a <see cref="ViewResult"/> whose model has
    /// <c>Disabled = true</c>, <c>Conversations</c> empty, <c>Candidates</c>
    /// empty, and <b>neither <see cref="IMessagingService
    /// .ListConversationsAsync"/> nor <see cref="IUserInfoService
    /// .GetProfilesAsync"/> is called</b> — the F5 pin: the disabled
    /// state is decided by the toggle read alone, and no list data is
    /// fetched at all.
    /// </summary>
    [Fact]
    public async Task Messages_Index_ToggleOff_RendersDisabled_NoListCall()
    {
        var (controller, messaging, userInfo) = BuildMessaging(false);

        var action = await controller.Index(null);
        var view   = Assert.IsType<ViewResult>(action);
        var model  = Assert.IsType<MessagesController.MessagesIndexViewModel>(view.ViewData.Model);

        Assert.True(model.Disabled);
        Assert.Empty(model.Conversations);
        Assert.Empty(model.Candidates);
        Assert.Null(model.Pager);

        await messaging.DidNotReceiveWithAnyArgs()
            .ListConversationsAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int?>());
        await userInfo.DidNotReceiveWithAnyArgs()
            .GetProfilesAsync(Arg.Any<bool>());
    }

    // ── 2 — toggle on → list + picker render ────────────────────────────

    /// <summary>
    /// <c>GET /messages</c> when the toggle is <c>true</c>: the action
    /// calls <see cref="IMessagingService.ListConversationsAsync"/> with
    /// the actor's subject id + page 1, and
    /// <see cref="IUserInfoService.GetProfilesAsync"/> for the
    /// new-conversation picker (every non-blocked resident except the
    /// actor). The model carries the conversation list, exactly one
    /// candidate (the other resident — the actor's own profile is
    /// excluded), and a <c>null</c> <c>Pager</c> (one page,
    /// <c>HasMore = false</c> — the ADR 0090 null-pager means "one
    /// page").
    /// </summary>
    [Fact]
    public async Task Messages_Index_ToggleOn_RendersConversationList()
    {
        var (controller, messaging, userInfo) = BuildMessaging(true);

        var convo = new ConversationRef(
            Id: ConvoId, OtherParticipantId: OtherId, OtherDisplayName: "Anna",
            LastMessageBody: "Hi!", LastMessageAt: new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero),
            UnreadCount: 1);
        messaging.ListConversationsAsync(Actor, 1, Arg.Any<int?>())
            .Returns(new ConversationList(new[] { convo }, HasMore: false));

        userInfo.GetProfilesAsync(false).Returns(new[]
        {
            new Profile { SubjectId = Actor,   DisplayName = "Ben"  },
            new Profile { SubjectId = OtherId, DisplayName = "Anna" },
        });

        var action = await controller.Index(null);
        var view   = Assert.IsType<ViewResult>(action);
        var model  = Assert.IsType<MessagesController.MessagesIndexViewModel>(view.ViewData.Model);

        Assert.False(model.Disabled);
        Assert.Single(model.Conversations);
        Assert.Equal(ConvoId, model.Conversations[0].Id);
        Assert.Single(model.Candidates);
        Assert.Equal(OtherId, model.Candidates[0].SubjectId);
        Assert.Equal(1, model.Page);
        Assert.Null(model.Pager);   // HasMore = false, page = 1 → null pager

        await messaging.Received(1).ListConversationsAsync(Actor, 1, Arg.Any<int?>());
    }

    // ── 3 — C-M9·1: non-participant → 404, no MarkRead, no view ────────

    /// <summary>
    /// <c>GET /messages/{id}</c> when <see cref="IMessagingService
    /// .GetConversationAsync"/> throws <see cref="KeyNotFoundException"/>
    /// (the actor is not a participant, or the id is unknown — the service
    /// does not distinguish; C-M9·1): the action returns a
    /// <see cref="NotFoundResult"/> with no view data, and
    /// <see cref="IMessagingService.MarkReadAsync"/> is
    /// <b>never called</b> (no read-state write for a non-participant;
    /// the non-leaky 404 is the whole access story).
    /// </summary>
    [Fact]
    public async Task Messages_Thread_NonParticipant_404_NoView()
    {
        var (controller, messaging, _) = BuildMessaging(true);

        messaging.GetConversationAsync(ConvoId, Actor, 1, Arg.Any<int?>())
            .Returns(Task.FromException<ConversationDetail>(
                new KeyNotFoundException("not a participant")));

        var action = await controller.Thread(ConvoId, null);

        Assert.IsType<NotFoundResult>(action);
        await messaging.DidNotReceiveWithAnyArgs()
            .MarkReadAsync(Arg.Any<string>(), Arg.Any<string>());
        // The toggle gate was read (the action reached the service call),
        // so GetConversationAsync was invoked once and threw.
        await messaging.Received(1).GetConversationAsync(ConvoId, Actor, 1, Arg.Any<int?>());
    }

    // ── 4 — thread renders with unread markers (D8 / F3) ───────────────

    /// <summary>
    /// <c>GET /messages/{id}</c> happy path: the model carries the
    /// conversation, the message window (two messages — one the actor
    /// sent, one from the other participant with <c>ReadBy = null</c>
    /// so the view renders the unread badge), the actor's display name,
    /// and a <c>null</c> pager. <see cref="IMessagingService
    /// .MarkReadAsync"/> is called exactly once for the actor (D8 —
    /// caller-only read-state update, best-effort on entry). The
    /// unread-marking shape is verified against
    /// <c>Views/Messages/Thread.cshtml</c> (the <c>message.unread</c>
    /// badge + the <c>!mine &amp;&amp; m.ReadBy is null</c> condition —
    /// the U04 deliverable markup).
    /// </summary>
    [Fact]
    public async Task Messages_Thread_RendersMessages_WithUnreadMarkers()
    {
        var (controller, messaging, _) = BuildMessaging(true);

        var convo = new ConversationRef(
            Id: ConvoId, OtherParticipantId: OtherId, OtherDisplayName: "Anna",
            LastMessageBody: "Hello", LastMessageAt: new DateTimeOffset(2026, 9, 27, 9, 30, 0, TimeSpan.Zero),
            UnreadCount: 1);
        var actorMsg = new Message
        {
            Id = "msg-1", ConversationId = ConvoId, SenderId = Actor,
            Body = "Hello", Created = new DateTimeOffset(2026, 9, 27, 9, 0, 0, TimeSpan.Zero),
            ReadBy = Actor,   // the actor has read their own message
        };
        var otherMsg = new Message
        {
            Id = "msg-2", ConversationId = ConvoId, SenderId = OtherId,
            Body = "Hi Ben", Created = new DateTimeOffset(2026, 9, 27, 9, 30, 0, TimeSpan.Zero),
            ReadBy = null,    // unread by the actor — the view renders the badge
        };
        messaging.GetConversationAsync(ConvoId, Actor, 1, Arg.Any<int?>())
            .Returns(new ConversationDetail(convo, new[] { otherMsg, actorMsg }, HasMore: false));

        var action = await controller.Thread(ConvoId, null);
        var view   = Assert.IsType<ViewResult>(action);
        var model  = Assert.IsType<MessagesController.MessagesThreadViewModel>(view.ViewData.Model);

        Assert.False(model.Disabled);
        Assert.False(model.Error);
        Assert.NotNull(model.Conversation);
        Assert.Equal(ConvoId, model.Conversation!.Id);
        Assert.Equal(2, model.Messages.Count);
        Assert.Equal(Actor, model.ActorId);
        Assert.Equal("Ben", model.ActorDisplayName);
        Assert.Null(model.Pager);

        // D8 — MarkRead runs for the actor on entry (best-effort).
        await messaging.Received(1).MarkReadAsync(ConvoId, Actor);

        // The view renders the unread badge (the !mine && ReadBy is null
        // condition — verified in Views/Messages/Thread.cshtml).
        var viewSource = ReadViewSource("Messages", "Thread.cshtml");
        Assert.Contains("message.unread", viewSource);
        Assert.Contains("m.ReadBy is null", viewSource);
    }

    // ── 5 — send happy path → redirect + SendAsync called ───────────────

    /// <summary>
    /// <c>POST /messages/{id}/send</c> with a non-blank body: the action
    /// calls <see cref="IMessagingService.SendAsync"/> exactly once with
    /// the conversation id, the actor's subject id, and the body, then
    /// redirects back to the thread (<c>/messages/{id}</c>).
    /// </summary>
    [Fact]
    public async Task Messages_Send_PostsBody_ToService()
    {
        var (controller, messaging, _) = BuildMessaging(true);

        var action = await controller.Send(ConvoId, "Hello there");
        var redirect = Assert.IsType<RedirectResult>(action);

        Assert.Equal($"/messages/{ConvoId}", redirect.Url);
        await messaging.Received(1).SendAsync(ConvoId, Actor, "Hello there");
    }

    // ── 6 — blank body → error render, no SendAsync call ────────────────

    /// <summary>
    /// <c>POST /messages/{id}/send</c> with a blank body (null, empty, or
    /// whitespace): the action returns the <c>Thread</c> error view
    /// (<c>Error = true</c>, no conversation data) and
    /// <see cref="IMessagingService.SendAsync"/> is
    /// <b>never called</b> — the blank-body check is in the controller,
    /// before any service call (the U06 pin: "no service call," not just
    /// "no exception").
    /// </summary>
    [Fact]
    public async Task Messages_Send_BlankBody_RendersError_NoServiceCall()
    {
        var (controller, messaging, _) = BuildMessaging(true);

        var action = await controller.Send(ConvoId, "   ");
        var view   = Assert.IsType<ViewResult>(action);
        var model  = Assert.IsType<MessagesController.MessagesThreadViewModel>(view.ViewData.Model);

        Assert.True(model.Error);
        Assert.False(model.Disabled);
        Assert.Null(model.Conversation);
        Assert.Empty(model.Messages);

        await messaging.DidNotReceiveWithAnyArgs()
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // ── M9 amendment — per-actor gate (the resident's opt-in + the
    //      guardian's ceiling, the composite IsMessagingAllowedForAsync
    //      read) ─────────────────────────────────────────────────────────

    /// <summary>
    /// M9 amendment — <c>GET /messages</c> when the instance toggle is on
    /// but <see cref="IMessagingService.IsMessagingAllowedForAsync"/>
    /// returns <c>false</c> for the actor (the resident has opted out, or a
    /// guardian has restricted them): the action returns the same
    /// <c>Disabled = true</c> render as an off instance (the non-leaky
    /// shape — the reason is not surfaced), and
    /// <see cref="IMessagingService.ListConversationsAsync"/> is
    /// <b>never called</b>.
    /// </summary>
    [Fact]
    public async Task Messages_Index_PerActorDisallowed_RendersDisabled_NoListCall()
    {
        var (controller, messaging, userInfo) = BuildMessaging(true);
        messaging.IsMessagingAllowedForAsync(Arg.Any<string>()).Returns(false);

        var action = await controller.Index(null);
        var view   = Assert.IsType<ViewResult>(action);
        var model  = Assert.IsType<MessagesController.MessagesIndexViewModel>(view.ViewData.Model);

        Assert.True(model.Disabled);
        Assert.Empty(model.Conversations);
        Assert.Empty(model.Candidates);
        Assert.Null(model.Pager);

        // The per-actor gate was read (the action reached the read).
        await messaging.Received(1).IsMessagingAllowedForAsync(Actor);
        // And no list data was fetched at all (the disabled state is
        // decided by the gate read alone).
        await messaging.DidNotReceiveWithAnyArgs()
            .ListConversationsAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int?>());
        await userInfo.DidNotReceiveWithAnyArgs()
            .GetProfilesAsync(Arg.Any<bool>());
    }

    /// <summary>
    /// M9 amendment — <c>GET /messages/{id}</c> when the per-actor gate is
    /// disallowed for the actor: the action returns a <see
    /// cref="NotFoundResult"/> (the non-leaky 404 — indistinguishable from a
    /// missing id, the C-M9·1 pin), and
    /// <see cref="IMessagingService.GetConversationAsync"/> is
    /// <b>never called</b> (no thread data read).
    /// </summary>
    [Fact]
    public async Task Messages_Thread_PerActorDisallowed_404_NoThreadRead()
    {
        var (controller, messaging, _) = BuildMessaging(true);
        messaging.IsMessagingAllowedForAsync(Arg.Any<string>()).Returns(false);

        var action = await controller.Thread(ConvoId, null);

        Assert.IsType<NotFoundResult>(action);
        await messaging.Received(1).IsMessagingAllowedForAsync(Actor);
        await messaging.DidNotReceiveWithAnyArgs()
            .GetConversationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int?>());
    }

    /// <summary>
    /// M9 amendment — <c>POST /messages/{id}/send</c> when the per-actor
    /// gate is disallowed for the actor: the action returns the disabled
    /// error render (<c>Disabled = true</c>) and
    /// <see cref="IMessagingService.SendAsync"/> is <b>never called</b>.
    /// </summary>
    [Fact]
    public async Task Messages_Send_PerActorDisallowed_Disabled_NoSendCall()
    {
        var (controller, messaging, _) = BuildMessaging(true);
        messaging.IsMessagingAllowedForAsync(Arg.Any<string>()).Returns(false);

        var action = await controller.Send(ConvoId, "Hello");
        var view   = Assert.IsType<ViewResult>(action);
        var model  = Assert.IsType<MessagesController.MessagesThreadViewModel>(view.ViewData.Model);

        Assert.True(model.Disabled);
        Assert.Null(model.Conversation);
        Assert.Empty(model.Messages);

        await messaging.Received(1).IsMessagingAllowedForAsync(Actor);
        await messaging.DidNotReceiveWithAnyArgs()
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>
    /// M9 amendment — <c>_AccountNav.cshtml</c> (the U04 deliverable, the
    /// M9 amendment gate) renders the <c>/messages</c> link <b>only</b>
    /// when the instance toggle is on <b>AND</b> the signed-in resident is
    /// per-actor allowed — the view source carries the
    /// <c>IsMessagingAllowedForAsync</c> read alongside the
    /// <c>IsMessagingEnabledAsync</c> F5 gate, both wrapping the
    /// <c>/messages</c> href + the <c>message.nav</c> key.
    /// </summary>
    [Fact]
    public void Messages_Nav_PerActorGate_Wraps_Messages_Link()
    {
        var nav = ReadViewSource("Shared", "_AccountNav.cshtml");

        // The M9 amendment gate: the nav entry is behind the
        // IsMessagingAllowedForAsync per-actor read (in addition to the
        // F5 IsMessagingEnabledAsync instance-toggle read).
        Assert.Contains("Messaging.IsMessagingEnabledAsync()", nav, StringComparison.Ordinal);
        Assert.Contains("IsMessagingAllowedForAsync", nav, StringComparison.Ordinal);
        // The gate's body carries the /messages link + the message.nav key.
        Assert.Contains("href=\"/messages\"", nav, StringComparison.Ordinal);
        Assert.Contains("message.nav", nav, StringComparison.Ordinal);
    }

    // ── 7 — nav entry: toggle off → hidden (view-source pin) ───────────

    /// <summary>
    /// <c>_AccountNav.cshtml</c> (the U04 deliverable) renders the
    /// <c>/messages</c> link <b>only</b> when
    /// <see cref="IMessagingService.IsMessagingEnabledAsync"/> returns
    /// <c>true</c> — the F5 gate. This test cannot drive the Razor partial
    /// through a controller substitute (the partial renders in the layout,
    /// not in a <see cref="Controller"/> action), so it pins the shape:
    /// the view source carries the <c>IsMessagingEnabledAsync()</c>
    /// conditional, and that conditional wraps the <c>/messages</c> href
    /// + the <c>message.nav</c> key. The "hidden when off" behavior is
    /// the <c>@if</c> gate itself — the same pattern
    /// <see cref="SearchControllerTests.Search_NavBox_Rendered_ForAnonymous"/>
    /// uses for the <c>_Layout.cshtml</c> nav box.
    /// </summary>
    [Fact]
    public void Messages_Nav_ToggleOff_EntryHidden()
    {
        var nav = ReadViewSource("Shared", "_AccountNav.cshtml");

        // The F5 gate: the nav entry is behind the IsMessagingEnabledAsync
        // read — when the toggle is false the @if block is skipped and the
        // /messages link is not rendered.
        Assert.Contains("Messaging.IsMessagingEnabledAsync()", nav, StringComparison.Ordinal);
        // The gate's body carries the /messages link + the message.nav key.
        Assert.Contains("href=\"/messages\"", nav, StringComparison.Ordinal);
        Assert.Contains("message.nav", nav, StringComparison.Ordinal);

        // The key is registered (the F7 localization pin — the parity test
        // enforces all four languages; this is the Web-side registration
        // shape check).
        Assert.False(string.IsNullOrWhiteSpace(
            Kumunita.Core.Localization.KnownTranslationKeys.EnValues["message.nav"]));
    }

    // ── 8 — nav entry: toggle on → present (view-source pin) ────────────

    /// <summary>
    /// Same shape pin as test 7, from the "present" angle: when
    /// <see cref="IMessagingService.IsMessagingEnabledAsync"/> returns
    /// <c>true</c>, the <c>/messages</c> link + the <c>message.nav</c>
    /// <c>kw-l</c> key are in the account dropdown. The pin asserts both
    /// the gate and the link are in the same file — a future reflow that
    /// moves the link outside the <c>@if</c> block (making it always
    /// rendered) or drops the <c>message.nav</c> key breaks this.
    /// </summary>
    [Fact]
    public void Messages_Nav_ToggleOn_EntryPresent()
    {
        var nav = ReadViewSource("Shared", "_AccountNav.cshtml");

        // The /messages link is present in the account dropdown.
        Assert.Contains("href=\"/messages\"", nav, StringComparison.Ordinal);
        // The message.nav kw-l key is registered (the F7 pin).
        Assert.Contains("message.nav", nav, StringComparison.Ordinal);

        // The gate is the IsMessagingEnabledAsync read (F5 — the same
        // seam the controller's Index/Thread/Send actions read first).
        Assert.Contains("IsMessagingEnabledAsync()", nav, StringComparison.Ordinal);
    }

    // ── 9 — admin GET: renders current toggle state ─────────────────────

    /// <summary>
    /// <c>GET /admin/messaging</c>: the action reads
    /// <see cref="IMessagingService.IsMessagingEnabledAsync"/> into the
    /// nested <c>MessagingAdminViewModel.Enabled</c> field and returns a
    /// <see cref="ViewResult"/> with that model — the controller adds no
    /// audit row (C-M9·3 — the service owns the audit).
    /// </summary>
    [Fact]
    public async Task AdminMessaging_Get_RendersCurrentState()
    {
        var (controller, messaging) = BuildAdmin(true);

        var action = await controller.Index();
        var view   = Assert.IsType<ViewResult>(action);
        var model  = Assert.IsType<AdminMessagingController.MessagingAdminViewModel>(view.ViewData.Model);

        Assert.True(model.Enabled);
        await messaging.Received(1).IsMessagingEnabledAsync();
    }

    // ── 10 — admin POST: flips toggle, calls SetMessagingEnabledAsync ──

    /// <summary>
    /// <c>POST /admin/messaging</c> with <c>enabled = false</c>: the
    /// action calls <see cref="IMessagingService
    /// .SetMessagingEnabledAsync"/> exactly once with the form's
    /// <c>enabled</c> value (<c>false</c>) and the actor's subject id,
    /// then redirects back to <c>Index</c>. The <c>messaging.toggle</c>
    /// audit row is the <b>service's</b> (pinned in U02's Core tests) —
    /// this test pins the controller's call shape only (F6: "the admin
    /// write is the service's").
    /// </summary>
    [Fact]
    public async Task AdminMessaging_Post_FlipsToggle_AuditedByService()
    {
        var (controller, messaging) = BuildAdmin(true);

        var action = await controller.Save(enabled: false);
        var redirect = Assert.IsType<RedirectToActionResult>(action);

        Assert.Equal(nameof(AdminMessagingController.Index), redirect.ActionName);
        // The controller maps the form's enabled value + the actor's
        // subject id and hands off to the service — exactly one call,
        // with the form's value (false), not a negation or a default.
        await messaging.Received(1).SetMessagingEnabledAsync(false, Actor);
        await messaging.DidNotReceive().SetMessagingEnabledAsync(true, Arg.Any<string>());
    }

    // ── harness ─────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="MessagesController"/> with NSubstitute stand-ins
    /// for <see cref="IMessagingService"/> and <see cref="IUserInfoService"/>
    /// (the <see cref="AnnouncementControllerTests"/> pattern). The
    /// <c>IsMessagingEnabledAsync</c> stub is set by the
    /// <paramref name="enabled"/> parameter. <see cref="TempData"/> is
    /// backed by a no-op <see cref="ITempDataProvider"/> (the
    /// <see cref="AccountControllerSignupGateTests"/> pattern) so
    /// <c>Send</c> can set <c>TempData["info"]</c> without a real session.
    /// </summary>
    private static (MessagesController controller, IMessagingService messaging, IUserInfoService userInfo)
        BuildMessaging(bool enabled)
    {
        var messaging = Substitute.For<IMessagingService>();
        messaging.IsMessagingEnabledAsync().Returns(enabled);
        // M9 amendment — the per-actor gate: defaults to allowed for the
        // harness actor (the existing tests pin the instance-toggle gate,
        // not the per-actor gate — the per-actor behavior is pinned in its
        // own new tests below). A test that wants to exercise the
        // disallowed path can re-stub this to false.
        messaging.IsMessagingAllowedForAsync(Arg.Any<string>()).Returns(true);

        var userInfo = Substitute.For<IUserInfoService>();
        // Default: the actor's profile (for the "You" sender label on the
        // thread). Tests that need specific profile data override this.
        userInfo.GetProfileAsync(Actor).Returns(new Profile { SubjectId = Actor, DisplayName = "Ben" });

        var controller = new MessagesController(NullLogger<MessagesController>.Instance, messaging, userInfo);
        WireControllerContext(controller, Actor);

        return (controller, messaging, userInfo);
    }

    /// <summary>
    /// Builds an <see cref="AdminMessagingController"/> with an NSubstitute
    /// <see cref="IMessagingService"/> stand-in. The
    /// <c>IsMessagingEnabledAsync</c> stub is set by the
    /// <paramref name="enabled"/> parameter.
    /// </summary>
    private static (AdminMessagingController controller, IMessagingService messaging)
        BuildAdmin(bool enabled)
    {
        var messaging = Substitute.For<IMessagingService>();
        messaging.IsMessagingEnabledAsync().Returns(enabled);

        var controller = new AdminMessagingController(messaging);
        WireControllerContext(controller, Actor);

        return (controller, messaging);
    }

    /// <summary>
    /// Wires the <see cref="ControllerContext"/> with a signed-in
    /// <see cref="ClaimsPrincipal"/> carrying the
    /// <see cref="ClaimTypes.Subject"/> claim (value =
    /// <paramref name="subjectId"/>), and a no-op <see cref="TempData"/>
    /// provider (the <see cref="AccountControllerSignupGateTests"/>
    /// pattern — the <c>Send</c> / <c>Save</c> actions touch
    /// <c>TempData["info"]</c> on the success path).
    /// </summary>
    private static void WireControllerContext(Controller controller, string subjectId)
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

        // The Send and Save actions set TempData["info"] on success — a
        // no-op provider closes the gap without requiring a real session.
        controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(), new NoOpTempDataProvider());
    }

    /// <summary>
    /// Reads a view's source by walking up from the test's working directory
    /// to the repo root (the <c>Kumunita.slnx</c> file) — the same pattern
    /// <see cref="SearchControllerTests.ReadViewSource"/> /
    /// <see cref="StaticPagesSP_U04Tests.ResolveLayout"/> use for their
    /// <c>_Layout.cshtml</c> / <c>_AccountNav.cshtml</c> pins.
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
