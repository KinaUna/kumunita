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
/// The resident messaging surface (M9, ADR 0105, U04 — the resident-side
/// controller over U03's <see cref="IMessagingService"/>).
/// <para>
/// <b>The toggle is the whole access story (C-M9·1, the F5 pin):</b> every
/// action reads <see cref="IMessagingService.IsMessagingEnabledAsync"/>
/// <i>first</i>; a disabled instance never lists conversations, never reads a
/// thread, never sends — the off-state renders the <c>message.disabled</c>
/// notice (Index) or 404s (Thread). There is deliberately <b>no</b>
/// <see cref="Kumunita.Core.Authorization.IAuthorizationService"/> call: the
/// service's non-leaky seams are the authorization — a conversation the actor
/// is not a participant of is a <see cref="KeyNotFoundException"/> 404 that
/// is indistinguishable from a missing id (D3/D4, the service contract), and
/// a disabled instance is a 403 at the service for the write seams
/// (C-M9·2).
/// </para>
/// <para>
/// Routes (design doc §web):
/// <list type="bullet">
/// <item>GET /messages — the conversation list (+ the new-conversation
/// resident picker).</item>
/// <item>POST /messages/open — open (or return) the 1:1 for a resident, then
/// redirect to the thread (the idempotent F1 pair-unique seam).</item>
/// <item>GET /messages/{id} — the thread page (read + composer + unread
/// markers); <see cref="IMessagingService.MarkReadAsync"/> runs on entry
/// (D8), best-effort — a mark-read failure never blanks the thread.</item>
/// <item>POST /messages/{id}/send — send one message; blank/over-cap bodies
/// render the error inline without a service call (the U06
/// <c>Messages_Send_BlankBody_RendersError_NoServiceCall</c> pin).</item>
/// </list>
/// The paging signal rides the shared <see cref="PagedViewModel"/> +
/// <c>_Pager</c> partial exactly as the M8 search surface does (ADR 0090):
/// a <c>null</c> Pager means one page and the partial renders nothing.
/// </para>
/// </summary>
[Authorize]
public sealed class MessagesController(
    ILogger<MessagesController> logger,
    IMessagingService messaging,
    IUserInfoService userInfo,
    // The per-request localization read seams (the LocaleController.FlashAsync
    // idiom): the <c>Send</c> success flash names the recipient ("Sent to
    // {name}"), and a UI string needs the resident's effective language to
    // resolve. **Optional** (default null) so the existing test-construction
    // sites that build this controller without the provider keep compiling —
    // the flash falls back to the KnownTranslationKeys.EnValues source text
    // when the seam is absent (the provider floor — a resident never sees a
    // raw key); DI always supplies the live ILocalizationService +
    // ITranslationProvider in the app.
    ILocalizationService? localization = null,
    ITranslationProvider? translationProvider = null) : Controller
{
    private readonly ILogger<MessagesController> _logger = logger;
    private readonly IMessagingService _messaging = messaging;
    private readonly IUserInfoService _userInfo = userInfo;
    private readonly ILocalizationService? _localization = localization;
    private readonly ITranslationProvider? _translationProvider = translationProvider;

    /// <summary>
    /// Resolve a UI-string template in the resident's effective language and
    /// apply its <c>{0}</c> placeholder (the LocaleController.FlashAsync idiom).
    /// Falls back to the <see cref="KnownTranslationKeys.EnValues"/> source text
    /// when the seam is absent (the provider floor — ADR 0015 D1), so a
    /// resident never sees a raw key.
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

    // ── GET /messages — the conversation list (+ new-conversation picker) ──

    /// <summary>
    /// The conversation list (newest activity first) plus the resident picker
    /// for a new conversation. The toggle gate runs <b>before any list
    /// call</b>: disabled → the <c>message.disabled</c> state, no
    /// <see cref="IMessagingService.ListConversationsAsync"/> read (the U06
    /// <c>Messages_Index_ToggleOff_RendersDisabled_NoListCall</c> pin).
    /// </summary>
    [HttpGet("/messages")]
    public async Task<IActionResult> Index(int? page)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null)
            return NotFound();

        // F5 — the toggle read is first; off → the disabled state and nothing
        // else is read (no list call, no picker, no thread).
        if (!await _messaging.IsMessagingEnabledAsync())
            return View(new MessagesIndexViewModel { Disabled = true });

        // M9 amendment — the per-actor gate: the actor's own opt-in must be
        // on AND the guardian's ceiling (if any) must not be forcing it off.
        // Disallowed → the same disabled render (no list call, no picker —
        // the resident is told "messaging is off" the same way an off
        // instance is; the reason is not surfaced, the shape is the shape).
        if (!await _messaging.IsMessagingAllowedForAsync(actorId))
            return View(new MessagesIndexViewModel { Disabled = true });

        var pageNum = page is > 0 ? page.Value : 1;
        // The resident's items-per-page preference (FeedPaging resolves
        // Profile.PageSize → PageSizer default/clamp); a no-actor read uses
        // the platform default.
        int pageSize = await FeedPaging.PageSizeAsync(_userInfo, actorId);
        var list = await _messaging.ListConversationsAsync(actorId, pageNum, pageSize: pageSize);

        // The new-conversation picker: every non-blocked resident except the
        // actor themself (the directory's catalog read — verifiedOnly: false,
        // the CommunityController picker pattern; self-open has no 1:1 use).
        var candidates = (await _userInfo.GetProfilesAsync(verifiedOnly: false))
            .Where(p => !p.Blocked && p.SubjectId != actorId)
            .Select(p => new PickerCandidate(p.SubjectId, p.DisplayName))
            .OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        PagedViewModel? pager = null;
        if (list.HasMore || pageNum > 1)
            pager = PagedViewModel.ForRoute("/messages", pageNum, pageSize, list.HasMore);

        return View(new MessagesIndexViewModel
        {
            Conversations = list.Items,
            Candidates = candidates,
            Page = pageNum,
            Pager = pager,
        });
    }

    // ── POST /messages/open — open/return the 1:1, redirect to the thread ──

    /// <summary>
    /// Open (or return the existing) 1:1 conversation for the picked resident
    /// and redirect to the thread (F1 — idempotent on the pair, either order).
    /// A blank id is a 400 by the service contract; a disabled instance is a
    /// 403 (C-M9·2) — both surface as the non-leaky disabled-state render so
    /// the resident is not told which of the two went wrong.
    /// </summary>
    [HttpPost("/messages/open")]
    public async Task<IActionResult> Open([FromForm] string? otherId)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null)
            return NotFound();

        if (!await _messaging.IsMessagingEnabledAsync())
            return View("Index", new MessagesIndexViewModel { Disabled = true });

        // M9 amendment — the per-actor gate: disallowed → the disabled render
        // (the reason is not surfaced, the shape is the shape).
        if (!await _messaging.IsMessagingAllowedForAsync(actorId))
            return View("Index", new MessagesIndexViewModel { Disabled = true });

        try
        {
            var conversation = await _messaging.OpenConversationAsync(actorId, otherId ?? string.Empty);
            return Redirect($"/messages/{conversation.Id}");
        }
        catch (ArgumentException)
        {
            // Blank id (or a service cap) — render the disabled/error state;
            // the picker is where the resident goes next.
            return View("Index", new MessagesIndexViewModel { Disabled = false, Error = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return View("Index", new MessagesIndexViewModel { Disabled = true });
        }
    }

    // ── GET /messages/{id} — the thread page (read + composer) ──────────────

    /// <summary>
    /// One page of the thread (newest-first, the service's ordering) with the
    /// composer + unread markers. <see cref="IMessagingService.MarkReadAsync"/>
    /// runs on entry (D8) — best-effort: a mark-read failure (a race with a
    /// concurrent send) never blanks an otherwise-valid thread read.
    /// A conversation the actor is not a participant of is a <b>404</b>
    /// (<see cref="KeyNotFoundException"/>, non-leaky — D3/D4, C-M9·1): the
    /// same shape as a missing id.
    /// </summary>
    [HttpGet("/messages/{id}")]
    public async Task<IActionResult> Thread(string id, int? page)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null)
            return NotFound();

        // F5 — the toggle read is first; off → 404 (no thread data read).
        if (!await _messaging.IsMessagingEnabledAsync())
            return NotFound();

        // M9 amendment — the per-actor gate: disallowed → 404 (no thread
        // data read; the non-leaky shape — the same as a missing id, the
        // C-M9·1 pin: the reason is not surfaced).
        if (!await _messaging.IsMessagingAllowedForAsync(actorId))
            return NotFound();

        var pageNum = page is > 0 ? page.Value : 1;
        // The resident's items-per-page preference (FeedPaging resolves
        // Profile.PageSize → PageSizer default/clamp); a no-actor read uses
        // the platform default.
        int pageSize = await FeedPaging.PageSizeAsync(_userInfo, actorId);
        ConversationDetail detail;
        try
        {
            detail = await _messaging.GetConversationAsync(id, actorId, pageNum, pageSize: pageSize);
        }
        catch (KeyNotFoundException)
        {
            // Missing id OR not a participant — deliberately indistinguishable
            // (the non-leaky 404 is the whole access story, C-M9·1).
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403);
        }

        // D8 — mark the thread read for the actor on entry. Best-effort: the
        // read above already succeeded, so a mark-read failure degrades to
        // "still shows unread" rather than a broken page.
        try
        {
            await _messaging.MarkReadAsync(id, actorId);
        }
        catch (KeyNotFoundException) { /* the thread vanished mid-request; the read above stands */ }
        catch (UnauthorizedAccessException) { /* same */ }

        // The actor's own display name — the view's "You" sender label
        // (best-effort; a missing profile degrades to the otherWord fallback).
        string? actorDisplayName = null;
        try { actorDisplayName = (await _userInfo.GetProfileAsync(actorId))?.DisplayName; }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Thread actor display-name read failed for {ActorId}; degrading to the sender fallback.", actorId);
            actorDisplayName = null;
        }

        PagedViewModel? pager = null;
        if (detail.HasMore || pageNum > 1)
            pager = PagedViewModel.ForRoute($"/messages/{detail.Conversation.Id}", pageNum,
                pageSize, detail.HasMore);

        // The service returns the window newest-first (its own contract);
        // the thread renders it chronologically — oldest first, the **latest
        // message last** (a chat reads bottom-up). Reverse for the view only;
        // the service contract is untouched.
        var chronological = detail.Messages;
        if (chronological.Count > 1)
        {
            chronological = chronological.Reverse().ToList();
        }

        return View(new MessagesThreadViewModel
        {
            Conversation = detail.Conversation,
            Messages = chronological,
            ActorId = actorId,
            ActorDisplayName = actorDisplayName,
            Page = pageNum,
            Pager = pager,
        });
    }

    // ── POST /messages/{id}/send — send one message ─────────────────────────

    /// <summary>
    /// Send one plain-text message to the thread. A blank/whitespace body
    /// renders the error state inline <b>without any service call</b> (the U06
    /// <c>Messages_Send_BlankBody_RendersError_NoServiceCall</c> pin); an
    /// over-cap body (<see cref="IMessagingService"/>'s
    /// <see cref="MessagingService.MaxBodyChars"/>) is the service's
    /// <see cref="ArgumentException"/> — the same error render, the service
    /// did the deciding. On success: a flash + redirect back to the thread.
    /// </summary>
    [HttpPost("/messages/{id}/send")]
    [EnableRateLimiting("message")]
    public async Task<IActionResult> Send(string id, [FromForm] string? body)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null)
            return NotFound();

        // F5 — the toggle read is first; off → error render, no send read.
        if (!await _messaging.IsMessagingEnabledAsync())
            return View("Thread", new MessagesThreadViewModel
            {
                Conversation = null,
                Messages = Array.Empty<Message>(),
                Error = true,
                Disabled = true,
            });

        // M9 amendment — the per-actor gate: disallowed → the disabled
        // error render (the reason is not surfaced, the shape is the shape).
        if (!await _messaging.IsMessagingAllowedForAsync(actorId))
            return View("Thread", new MessagesThreadViewModel
            {
                Conversation = null,
                Messages = Array.Empty<Message>(),
                Error = true,
                Disabled = true,
            });

        // Blank body — client-side validation is a convenience, the server
        // decides (and the pin is "no service call" — so the check is here,
        // not the service's).
        if (string.IsNullOrWhiteSpace(body))
            return View("Thread", new MessagesThreadViewModel
            {
                Conversation = null,
                Messages = Array.Empty<Message>(),
                Error = true,
                Disabled = false,
            });

        try
        {
            await _messaging.SendAsync(id, actorId, body!);
        }
        catch (ArgumentException)
        {
            // Over-cap (D7) — the service's own 400, rendered as the inline
            // composer error (the U06 shape-compatible path).
            return View("Thread", new MessagesThreadViewModel
            {
                Conversation = null,
                Messages = Array.Empty<Message>(),
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

        // The success flash names the recipient — the other participant. The
        // send above already committed, so a name-read failure (the thread
        // vanishing mid-request) never blanks the flash: it degrades to the
        // fallback word and the send stands (the D8 best-effort shape).
        string recipientName = "the other person";
        try
        {
            var detail = await _messaging.GetConversationAsync(id, actorId, 1);
            if (!string.IsNullOrWhiteSpace(detail?.Conversation?.OtherDisplayName))
                recipientName = detail.Conversation.OtherDisplayName;
        }
        catch (KeyNotFoundException) { /* the thread vanished mid-send — the send above stood */ }
        catch (UnauthorizedAccessException) { /* same */ }

        TempData["info"] = await FlashAsync("message.sent_to", recipientName);
        return Redirect($"/messages/{id}");
    }

    // ── View models (nested — the 5-deliverable cap keeps them here) ────────

    /// <summary>
    /// The GET /messages list page: the conversation rows, the new-conversation
    /// picker candidates, the paging signal, and the two error/disabled states
    /// the controller renders without a list call (toggle off, or a failed
    /// open).
    /// </summary>
    public sealed class MessagesIndexViewModel
    {
        /// <summary>true when the instance toggle is off (F5 — the disabled state).</summary>
        public bool Disabled { get; init; }

        /// <summary>true when the open failed (blank id / cap) — the error state.</summary>
        public bool Error { get; init; }

        /// <summary>The actor's conversations (newest activity first).</summary>
        public IReadOnlyList<ConversationRef> Conversations { get; init; } = Array.Empty<ConversationRef>();

        /// <summary>Picker candidates for a new conversation (every non-blocked resident except the actor).</summary>
        public IReadOnlyList<PickerCandidate> Candidates { get; init; } = Array.Empty<PickerCandidate>();

        /// <summary>The current 1-based page.</summary>
        public int Page { get; init; } = 1;

        /// <summary>The ADR 0090 paging signal — null means one page (the _Pager renders nothing).</summary>
        public PagedViewModel? Pager { get; init; }
    }

    /// <summary>
    /// One new-conversation picker row — the low-entropy shape (subject +
    /// display name), mirroring the <see cref="DirectoryViewModel"/> row
    /// convention without its directory-specific <c>Verified</c>/<c>Address</c>
    /// fields (the picker only needs "who to write").
    /// </summary>
    public sealed record PickerCandidate(string SubjectId, string DisplayName);

    /// <summary>
    /// The GET /messages/{id} thread page: the conversation row (the other
    /// participant's name), the message window (newest-first, the service's
    /// ordering), the paging signal, and the <see cref="ActorId"/> the view
    /// needs to mark the other participant's unread rows (D8).
    /// </summary>
    public sealed class MessagesThreadViewModel
    {
        /// <summary>The conversation row (its <c>OtherDisplayName</c> labels the thread header; null in the error states).</summary>
        public ConversationRef? Conversation { get; init; }

        /// <summary>The thread's message window (newest-first).</summary>
        public IReadOnlyList<Message> Messages { get; init; } = Array.Empty<Message>();

        /// <summary>The signed-in actor's subject id (the view's unread-marker comparison).</summary>
        public string ActorId { get; init; } = string.Empty;

        /// <summary>The actor's own display name (the view's "You" sender label; null degrades to the fallback word).</summary>
        public string? ActorDisplayName { get; init; }

        /// <summary>The current 1-based page.</summary>
        public int Page { get; init; } = 1;

        /// <summary>The ADR 0090 paging signal — null means one page (the _Pager renders nothing).</summary>
        public PagedViewModel? Pager { get; init; }

        /// <summary>true when a send failed (blank/over-cap body) — the composer error state.</summary>
        public bool Error { get; init; }

        /// <summary>true when the instance toggle is off — the disabled state.</summary>
        public bool Disabled { get; init; }
    }
}
