using Kumunita.Core.GroupMessaging;
using Kumunita.Core.Localization;
using Kumunita.Core.Messaging;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The resident group-messaging surface (the N:1 generalization of the M9
/// 1:1 surface, ADR 0105 — multi-participant chat threads).
/// <para>
/// <b>The toggle is the whole access story (the C-M9·1 / F5 pin carried to
/// the N:1 surface):</b> every action reads
/// <see cref="IGroupMessagingService.IsMessagingEnabledAsync"/>
/// <i>first</i> (the same instance-level <c>MessagingEnabled</c> master gate
/// as the 1:1 surface — group messaging rides the same toggle); a disabled
/// instance never lists groups, never reads a thread, never sends. The
/// per-actor gate (opt-in + guardian ceiling) is the same
/// <c>IsMessagingAllowedForAsync</c> read on the 1:1 service. There is
/// deliberately <b>no</b> <see cref="Kumunita.Core.Authorization
/// .IAuthorizationService"/> call: the service's non-leaky seams are the
/// authorization — a group the actor is not a participant of is a
/// <see cref="KeyNotFoundException"/> 404 that is indistinguishable from a
/// missing id (the D3/D4 shape from ADR 0105, generalized to the N:1
/// participant set).
/// </para>
/// <para>
/// Routes:
/// <list type="bullet">
/// <item>GET /group-messages — the group list (+ the new-group resident
/// picker, the multi-recipient selector).</item>
/// <item>POST /group-messages/open — open a new group; the creator is the
/// only initial participant and the picked residents each get a pending
/// invitation (the invitation flow, ADR 0278 amendment), then redirect to
/// the thread.</item>
/// <item>GET /group-messages/{id} — the thread page (read + composer +
/// unread markers + the member roster + the add-people invitation picker);
/// <see cref="IGroupMessagingService.MarkReadAsync"/> runs on entry (the
/// per-participant read marker, best-effort).</item>
/// <item>POST /group-messages/{id}/send — send one message to every
/// participant except the sender.</item>
/// <item>POST /group-messages/{id}/members — invite eligible residents to
/// the group (each gets a pending invitation + a group.message.invite
/// nudge; the any-participant-can-invite rule).</item>
/// <item>GET /group-messages/{id}/invitations/accept — accept the actor's
/// pending invitation (self-lane; the inbox accept-button deep-link).</item>
/// <item>POST /group-messages/{id}/invitations/accept — accept (the form
/// variant; anti-forgery token).</item>
/// <item>GET /group-messages/{id}/invitations/decline — decline (the inbox
/// decline-button deep-link).</item>
/// <item>POST /group-messages/{id}/invitations/decline — decline (the form
/// variant; anti-forgery token).</item>
/// </list>
/// The paging signal rides the shared <see cref="PagedViewModel"/> +
/// <c>_Pager</c> partial exactly as the M9 surface does (ADR 0090).
/// </para>
/// </summary>
[Authorize]
public sealed class GroupMessagesController(
    ILogger<GroupMessagesController> logger,
    IGroupMessagingService groupMessaging,
    IMessagingService messaging,
    IUserInfoService userInfo,
    ILocalizationService? localization = null,
    ITranslationProvider? translationProvider = null) : Controller
{
    private readonly ILogger<GroupMessagesController> _logger = logger;
    private readonly IGroupMessagingService _groupMessaging = groupMessaging;
    private readonly IMessagingService _messaging = messaging;
    private readonly IUserInfoService _userInfo = userInfo;
    private readonly ILocalizationService? _localization = localization;
    private readonly ITranslationProvider? _translationProvider = translationProvider;

    /// <summary>
    /// Resolve a UI-string template in the resident's effective language and
    /// apply its <c>{0}</c> placeholder (the MessagesController.FlashAsync
    /// idiom).
    /// </summary>
    private async Task<string> FlashAsync(string key, params object?[] args)
    {
        var template = _translationProvider is null
            ? KnownTranslationKeys.EnValues.GetValueOrDefault(key) ?? key
            : await _translationProvider.GetAsync(key,
                await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, _localization!, _translationProvider));
        return args.Length > 0
            ? System.String.Format(System.Globalization.CultureInfo.InvariantCulture, template, args)
            : template;
    }

    // ── GET /group-messages — the group list (+ new-group picker) ──────────

    /// <summary>
    /// The group list (newest activity first) plus the resident picker for a
    /// new group (the multi-recipient selector). The instance toggle gate +
    /// the per-actor gate run <b>before any list call</b> (the F5 pin carried
    /// to the N:1 surface).
    /// </summary>
    [HttpGet("/group-messages")]
    public async Task<IActionResult> Index(int? page)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null)
            return NotFound();

        // F5 — the instance toggle read is first (the same master gate as the
        // 1:1 surface); off → the disabled state and nothing else is read.
        if (!await _groupMessaging.IsMessagingEnabledAsync())
            return View(new GroupMessagesIndexViewModel { Disabled = true });

        // The per-actor gate (the same IsMessagingAllowedForAsync read as the
        // M9 surface — group messaging rides the resident's messaging
        // standing). Disallowed → the same disabled render.
        if (!await _messaging.IsMessagingAllowedForAsync(actorId))
            return View(new GroupMessagesIndexViewModel { Disabled = true });

        var pageNum = page is > 0 ? page.Value : 1;
        int pageSize = await FeedPaging.PageSizeAsync(_userInfo, actorId);
        var list = await _groupMessaging.ListGroupsAsync(actorId, pageNum, pageSize: pageSize);

        // The new-group picker: every non-blocked, messaging-eligible resident
        // except the actor themself (the same candidate set as the M9
        // 1:1 picker — the recipient's own standing is the gate, the
        // DirectoryController.D1 two-sided shape).
        var candidates = (await _userInfo.GetProfilesAsync(verifiedOnly: false))
            .Where(p => !p.Blocked && p.SubjectId != actorId
                        && p.MessagingOptIn && !p.MessagingRestricted)
            .Select(p => new GroupPickerCandidate(p.SubjectId, p.DisplayName))
            .OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        PagedViewModel? pager = null;
        if (list.HasMore || pageNum > 1)
            pager = PagedViewModel.ForRoute("/group-messages", pageNum, pageSize, list.HasMore);

        return View(new GroupMessagesIndexViewModel
        {
            Groups = list.Items,
            Candidates = candidates,
            Page = pageNum,
            Pager = pager,
        });
    }

    // ── POST /group-messages/open — open a new group, redirect to thread ──

    /// <summary>
    /// Open a new group conversation with the actor + the picked residents
    /// (two or more total participants) and redirect to the thread. A
    /// single/blank recipient is a 400 by the service contract (a one-person
    /// "group" is a 1:1 — the M9 surface covers that); a disabled instance
    /// is a 403 (C-M9·2) — both surface as the non-leaky error render.
    /// </summary>
    [HttpPost("/group-messages/open")]
    [EnableRateLimiting("message")]
    public async Task<IActionResult> Open([FromForm] string[]? recipientIds, [FromForm] string? title)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null)
            return NotFound();

        if (!await _groupMessaging.IsMessagingEnabledAsync())
            return View("Index", new GroupMessagesIndexViewModel { Disabled = true });

        if (!await _messaging.IsMessagingAllowedForAsync(actorId))
            return View("Index", new GroupMessagesIndexViewModel { Disabled = true });

        try
        {
            var conversation = await _groupMessaging.OpenGroupAsync(actorId, recipientIds ?? Array.Empty<string>(), title);
            // The picked residents got a pending invitation (the invitation
            // flow, ADR 0278 amendment) — the toast tells the creator their
            // invite went out (the resident's name is not resolved here — the
            // count is the low-entropy shape).
            var inviteCount = (recipientIds ?? Array.Empty<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r) && r != actorId)
                .Distinct(StringComparer.Ordinal).Count();
            if (inviteCount > 0)
                TempData["info"] = System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                    await FlashAsync("group_message.invite_sent"), inviteCount);
            return Redirect($"/group-messages/{conversation.Id}");
        }
        catch (ArgumentException)
        {
            // Fewer than two distinct participants — render the error state;
            // the picker is where the resident goes next.
            return View("Index", new GroupMessagesIndexViewModel { Disabled = false, Error = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return View("Index", new GroupMessagesIndexViewModel { Disabled = true });
        }
    }

    // ── GET /group-messages/{id} — the thread page (read + composer) ───────

    /// <summary>
    /// One page of the thread (newest-first, the service's ordering) with the
    /// composer + unread markers + the add-member picker.
    /// <see cref="IGroupMessagingService.MarkReadAsync"/> runs on entry (the
    /// per-participant read marker) — best-effort. A group the actor is not a
    /// participant of is a <b>404</b> (non-leaky, the D3/D4 shape).
    /// </summary>
    [HttpGet("/group-messages/{id}")]
    public async Task<IActionResult> Thread(string id, int? page)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null)
            return NotFound();

        if (!await _groupMessaging.IsMessagingEnabledAsync())
            return NotFound();

        if (!await _messaging.IsMessagingAllowedForAsync(actorId))
            return NotFound();

        var pageNum = page is > 0 ? page.Value : 1;
        int pageSize = await FeedPaging.PageSizeAsync(_userInfo, actorId);
        GroupConversationDetail detail;
        try
        {
            detail = await _groupMessaging.GetConversationAsync(id, actorId, pageNum, pageSize: pageSize);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403);
        }

        // The per-participant read marker — best-effort (a mark-read failure
        // degrades to "still shows unread" rather than a broken page).
        try
        {
            await _groupMessaging.MarkReadAsync(id, actorId);
        }
        catch (KeyNotFoundException) { /* the group vanished mid-request */ }
        catch (UnauthorizedAccessException) { /* same */ }

        // The actor's own display name (the view's "You" sender label).
        string? actorDisplayName = null;
        try { actorDisplayName = (await _userInfo.GetProfileAsync(actorId))?.DisplayName; }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Group-thread actor display-name read failed for {ActorId}; degrading.", actorId);
            actorDisplayName = null;
        }

        // The member roster (accepted participants + pending invitees, each
        // with their display name) — the thread's "Members" section (the
        // invitation flow, ADR 0278 amendment).
        GroupMessageMembers? roster = null;
        try
        {
            roster = await _groupMessaging.GetMembersAsync(id, actorId);
        }
        catch (KeyNotFoundException) { /* the group vanished mid-request */ }
        catch (UnauthorizedAccessException) { /* same */ }

        // The add-people picker candidates: eligible residents not already a
        // participant AND not already pending an invite (the
        // any-participant-can-invite rule — the picker is available to every
        // participant of the group).
        var existing = detail.Conversation.ParticipantIds.ToHashSet(StringComparer.Ordinal);
        var pendingIds = (roster?.Members.Where(m => m.Pending) ?? Array.Empty<GroupMessageMember>())
            .Select(m => m.SubjectId)
            .ToHashSet(StringComparer.Ordinal);
        var addCandidates = (await _userInfo.GetProfilesAsync(verifiedOnly: false))
            .Where(p => !p.Blocked
                        && p.MessagingOptIn && !p.MessagingRestricted
                        && !existing.Contains(p.SubjectId)
                        && !pendingIds.Contains(p.SubjectId))
            .Select(p => new GroupPickerCandidate(p.SubjectId, p.DisplayName))
            .OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        PagedViewModel? pager = null;
        if (detail.HasMore || pageNum > 1)
            pager = PagedViewModel.ForRoute($"/group-messages/{detail.Conversation.Id}", pageNum,
                pageSize, detail.HasMore);

        // The service returns the window newest-first; the thread renders it
        // chronologically (the M9 thread's rendering convention).
        var chronological = detail.Messages;
        if (chronological.Count > 1)
            chronological = chronological.Reverse().ToList();

        return View(new GroupMessagesThreadViewModel
        {
            Conversation = detail.Conversation,
            Messages = chronological,
            ActorId = actorId,
            ActorDisplayName = actorDisplayName,
            Members = roster?.Members ?? Array.Empty<GroupMessageMember>(),
            AddCandidates = addCandidates,
            Page = pageNum,
            Pager = pager,
        });
    }

    // ── POST /group-messages/{id}/send — send one message ──────────────────

    /// <summary>
    /// Send one plain-text message to the group thread (every participant
    /// except the sender is nudged). A blank/whitespace body renders the
    /// error state inline <b>without any service call</b>; an over-cap body
    /// is the service's <see cref="ArgumentException"/> — the same error
    /// render.
    /// </summary>
    [HttpPost("/group-messages/{id}/send")]
    [EnableRateLimiting("message")]
    public async Task<IActionResult> Send(string id, [FromForm] string? body)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null)
            return NotFound();

        if (!await _groupMessaging.IsMessagingEnabledAsync())
            return View("Thread", new GroupMessagesThreadViewModel
            {
                Conversation = null,
                Messages = Array.Empty<GroupMessage>(),
                Error = true,
                Disabled = true,
            });

        if (!await _messaging.IsMessagingAllowedForAsync(actorId))
            return View("Thread", new GroupMessagesThreadViewModel
            {
                Conversation = null,
                Messages = Array.Empty<GroupMessage>(),
                Error = true,
                Disabled = true,
            });

        if (string.IsNullOrWhiteSpace(body))
            return View("Thread", new GroupMessagesThreadViewModel
            {
                Conversation = null,
                Messages = Array.Empty<GroupMessage>(),
                Error = true,
                Disabled = false,
            });

        try
        {
            await _groupMessaging.SendAsync(id, actorId, body!);
        }
        catch (ArgumentException)
        {
            return View("Thread", new GroupMessagesThreadViewModel
            {
                Conversation = null,
                Messages = Array.Empty<GroupMessage>(),
                Error = true,
                Disabled = false,
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403);
        }

        TempData["info"] = await FlashAsync("group_message.sent_to");
        return Redirect($"/group-messages/{id}");
    }

    // ── POST /group-messages/{id}/members — invite residents to the group ─

    /// <summary>
    /// Invite eligible residents to the group (the any-participant-can-invite
    /// rule — the actor must be a participant, which the service enforces
    /// with the non-leaky 404). Each invitee gets a <b>pending
    /// invitation</b> + a <c>group.message.invite</c> nudge (the invitation
    /// flow, ADR 0278 amendment); they join the thread only when they
    /// accept. A blank selection is a 400 by the service contract.
    /// </summary>
    [HttpPost("/group-messages/{id}/members")]
    [EnableRateLimiting("message")]
    public async Task<IActionResult> InviteMembers(string id, [FromForm] string[]? memberIds)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null)
            return NotFound();

        if (!await _groupMessaging.IsMessagingEnabledAsync())
            return View("Thread", new GroupMessagesThreadViewModel
            {
                Conversation = null,
                Messages = Array.Empty<GroupMessage>(),
                Error = true,
                Disabled = true,
            });

        if (!await _messaging.IsMessagingAllowedForAsync(actorId))
            return View("Thread", new GroupMessagesThreadViewModel
            {
                Conversation = null,
                Messages = Array.Empty<GroupMessage>(),
                Error = true,
                Disabled = true,
            });

        try
        {
            await _groupMessaging.InviteMembersAsync(id, actorId, memberIds ?? Array.Empty<string>());
        }
        catch (ArgumentException)
        {
            // Empty selection (or all already members) — the thread
            // re-renders with the picker + roster.
            return Redirect($"/group-messages/{id}");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403);
        }

        TempData["info"] = await FlashAsync("group_message.invite_sent", 1);
        return Redirect($"/group-messages/{id}");
    }

    // ── Accept / decline a pending invitation (self-lane) ──────────────────

    /// <summary>
    /// Accept the actor's pending invitation to this thread (self-lane — the
    /// community-group <c>AcceptInvitation</c> precedent). The actor must be
    /// the invitee (the Web gate: the conversation id must be in <em>my</em>
    /// pending list, else a non-leaky 404); the Core re-verifies
    /// <c>row.InvitedId == actorId</c> + <c>Pending</c>. On success the
    /// actor is appended to the participant set (live on the very next read)
    /// — and the actor now passes the thread's participant gate, hence the
    /// redirect <em>into</em> the thread. A row already resolved is mapped to
    /// an error message, never a 500 (C-M2b·3).
    /// </summary>
    [HttpGet("/group-messages/{id}/invitations/accept")]
    [EnableRateLimiting("message")]
    public async Task<IActionResult> AcceptInviteLink(string id)
        => await AcceptInviteAsync(id);

    /// <inheritdoc cref="AcceptInviteLink"/>
    [HttpPost("/group-messages/{id}/invitations/accept")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("message")]
    public async Task<IActionResult> AcceptInvite(string id)
        => await AcceptInviteAsync(id);

    /// <summary>The shared accept implementation (GET + POST).</summary>
    private async Task<IActionResult> AcceptInviteAsync(string id)
    {
        var actor = KumunitaPrincipal.SubjectId(User);
        if (actor is null)
            return NotFound();

        if (!await _groupMessaging.IsMessagingEnabledAsync())
            return NotFound();

        // Self-lane gate (C-M2b·2): the conversation must be in MY pending
        // list — else a non-leaky 404 (indistinguishable from a missing id).
        var pending = await _groupMessaging.GetPendingInvitationsForUserAsync(actor);
        if (pending.All(inv => inv.ConversationId != id))
            return NotFound();

        try
        {
            await _groupMessaging.AcceptInviteAsync(id, actor);
        }
        catch (InvalidOperationException)
        {
            // Resolved (or re-invited) in the gap between the click and this
            // commit — the Core's C-M2b·3 invalid-transition wall.
            TempData["error"] = await FlashAsync("group_message.invite_not_pending");
            return Redirect("/group-messages");
        }

        TempData["info"] = await FlashAsync("group_message.invite_accepted");
        return Redirect($"/group-messages/{id}");
    }

    /// <summary>
    /// Decline the actor's pending invitation to this thread (self-lane — the
    /// community-group <c>DeclineInvitation</c> precedent). Same gate and
    /// shape as <see cref="AcceptInviteAsync"/>, but <b>no</b> participant
    /// set is touched — the invitee simply never becomes a member.
    /// </summary>
    [HttpGet("/group-messages/{id}/invitations/decline")]
    [EnableRateLimiting("message")]
    public async Task<IActionResult> DeclineInviteLink(string id)
        => await DeclineInviteAsync(id);

    /// <inheritdoc cref="DeclineInviteLink"/>
    [HttpPost("/group-messages/{id}/invitations/decline")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("message")]
    public async Task<IActionResult> DeclineInvite(string id)
        => await DeclineInviteAsync(id);

    /// <summary>The shared decline implementation (GET + POST).</summary>
    private async Task<IActionResult> DeclineInviteAsync(string id)
    {
        var actor = KumunitaPrincipal.SubjectId(User);
        if (actor is null)
            return NotFound();

        if (!await _groupMessaging.IsMessagingEnabledAsync())
            return NotFound();

        var pending = await _groupMessaging.GetPendingInvitationsForUserAsync(actor);
        if (pending.All(inv => inv.ConversationId != id))
            return NotFound();

        try
        {
            await _groupMessaging.DeclineInviteAsync(id, actor);
        }
        catch (InvalidOperationException)
        {
            TempData["error"] = await FlashAsync("group_message.invite_not_pending");
            return Redirect("/group-messages");
        }

        TempData["info"] = await FlashAsync("group_message.invite_declined");
        return Redirect("/group-messages");
    }

    // ── View models (nested — the M9 convention keeps them here) ───────────

    /// <summary>
    /// The GET /group-messages list page: the group rows, the new-group
    /// picker candidates, the paging signal, and the two error/disabled
    /// states.
    /// </summary>
    public sealed class GroupMessagesIndexViewModel
    {
        /// <summary>true when the instance toggle (or the per-actor gate) is off — the disabled state.</summary>
        public bool Disabled { get; init; }

        /// <summary>true when the open failed (fewer than two participants) — the error state.</summary>
        public bool Error { get; init; }

        /// <summary>The actor's groups (newest activity first).</summary>
        public IReadOnlyList<GroupConversationRef> Groups { get; init; } = Array.Empty<GroupConversationRef>();

        /// <summary>Picker candidates for a new group (every eligible resident except the actor).</summary>
        public IReadOnlyList<GroupPickerCandidate> Candidates { get; init; } = Array.Empty<GroupPickerCandidate>();

        /// <summary>The current 1-based page.</summary>
        public int Page { get; init; } = 1;

        /// <summary>The ADR 0090 paging signal — null means one page.</summary>
        public PagedViewModel? Pager { get; init; }
    }

    /// <summary>
    /// One picker row — the low-entropy shape (subject + display name),
    /// mirroring the M9 <see cref="MessagesController.PickerCandidate"/>.
    /// </summary>
    public sealed record GroupPickerCandidate(string SubjectId, string DisplayName);

    /// <summary>
    /// The GET /group-messages/{id} thread page: the conversation row, the
    /// message window, the add-member candidates, the paging signal, and the
    /// actor identity the view's unread markers need.
    /// </summary>
    public sealed class GroupMessagesThreadViewModel
    {
        /// <summary>The conversation row (null in the error states).</summary>
        public GroupConversationRef? Conversation { get; init; }

        /// <summary>The thread's message window (newest-first, the service's ordering).</summary>
        public IReadOnlyList<GroupMessage> Messages { get; init; } = Array.Empty<GroupMessage>();

        /// <summary>The signed-in actor's subject id (the view's "You" marker).</summary>
        public string ActorId { get; init; } = string.Empty;

        /// <summary>The actor's own display name (the view's "You" sender label).</summary>
        public string? ActorDisplayName { get; init; }

        /// <summary>
        /// The thread's member roster (accepted participants + pending
        /// invitees, each with their display name) — the view's "Members"
        /// section (the invitation flow, ADR 0278 amendment).
        /// </summary>
        public IReadOnlyList<GroupMessageMember> Members { get; init; } = Array.Empty<GroupMessageMember>();

        /// <summary>The add-people picker candidates (eligible, not yet a member or pending).</summary>
        public IReadOnlyList<GroupPickerCandidate> AddCandidates { get; init; } = Array.Empty<GroupPickerCandidate>();

        /// <summary>The current 1-based page.</summary>
        public int Page { get; init; } = 1;

        /// <summary>The ADR 0090 paging signal — null means one page.</summary>
        public PagedViewModel? Pager { get; init; }

        /// <summary>true when a send failed (blank/over-cap body) — the composer error state.</summary>
        public bool Error { get; init; }

        /// <summary>true when the instance toggle (or the per-actor gate) is off — the disabled state.</summary>
        public bool Disabled { get; init; }
    }
}
