using Kumunita.Core.Events;
using Kumunita.Core.Localization;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The resident-facing group-management surface (M2 plan U9) — the <b>list</b> +
/// <b>create</b> for <c>/groups</c>. <c>Index</c> (GET <c>/groups</c>) renders
/// <see cref="IUserInfoService.GetGroupsForUserAsync"/>'s owner-∪-member
/// projection (F14 — "my group list shows only groups I own plus groups I belong
/// to"); <c>Create</c> (GET/POST <c>/groups/create</c>) calls the M1 seam
/// <see cref="IUserInfoService.CreateGroupAsync"/> with
/// <c>ownerId = SubjectId(User)</c> (the actor — ADR 0003 SoD is enforced by the
/// seam's owner derivation <c>addedBy == group.OwnerId</c> ⇒ <c>Via: Owner</c>,
/// not by a Web-role gate).
/// <para>
/// <b>U9 scope pin:</b> GET <c>Index</c> + POST <c>Create</c> — *no* detail
/// route, *no* add/remove member (those are U10), *no* moderator / GlobalAdmin
/// lane at the Web layer. ADR 0006-D: the Web shapes HTTP, the Core decides; the
/// M1 seam's owner-branch derivation owns SoD.
/// </para>
/// <para>
/// <b>ADR 0003 SoD pin (F14):</b> an actor who is neither the owner nor a member
/// does not see the row in the list — the projection rule (owner ∪ member) is the
/// product definition of "my groups". A GlobalAdmin sees groups they own ∪ belong
/// to via the same rule; the admin's <i>management</i> surface (role/scope,
/// break-glass, audit) is M1's <c>/admin</c>, not here.
/// </para>
/// </summary>
[Authorize]
[Route("groups")]
public sealed class GroupsController(
    IUserInfoService userInfo,
    PostService posts,
    ILocalizationService localization,
    IDocumentStore store,
    // Group events (ADR 0089) — the M4 event surface's group lane. The
    // group-event actions' write + read seams (CreateGroupEventAsync /
    // UpdateGroupEventAsync / PublishAsync / RsvpAsync / GetMyRsvpAsync /
    // GetRsvpsAsync / ListGroupEventsAsync) resolve through this; the
    // create/edit/publish/RSVP gates are the authoritative denies (GE·3/GE·4)
    // and the group-lane membership read runs at Core. Appended **before** the
    // optional translationProvider so the test-construction site (which now
    // passes a null for it) keeps the required-params shape.
    IEventService events,
    // Back-link display name (the group name on the post-detail page's
    // "back to the group" link) — the per-request translation read seam,
    // used to resolve the group's stored name into the viewer's current
    // language when a user-added name translation exists (the ADR 0026
    // floor). **Optional** so the existing test-construction site keeps
    // compiling; DI always supplies the live ITranslationProvider in the app.
    ITranslationProvider? translationProvider = null) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// The group-post composer's + reply form's <b>authored-in language</b>
    /// picker options (ADR 0018, ADR 0005 B) — the instance's **enabled**
    /// <see cref="LanguageCatalog"/>, ordered by <c>SortOrder</c>. Read through
    /// <see cref="ILocalizationService.ListLanguagesAsync"/> (the HTTP-free
    /// seam, ADR 0005 D) — the exact catalog read the
    /// <see cref="LocaleController.Index"/> page uses. The create lane
    /// pre-selects the actor's current effective language (ADR 0049 —
    /// <see cref="ResolveComposeDefaultLanguageAsync"/>) so the picker
    /// highlights the language the resident is reading the platform in; the
    /// reply/comment forms mark the same code selected in their option list.
    /// </summary>
    private async Task<IReadOnlyList<(string Code, string NativeName)>> SeedLanguagePickerAsync()
    {
        var catalog = await localization.ListLanguagesAsync().ConfigureAwait(false);
        return catalog
            .Where(l => l.Enabled)
            .OrderBy(l => l.SortOrder)
            .Select(l => (l.Id, l.NativeName))
            .ToList();
    }

    /// <summary>
    /// The create-lane composer's <b>default authored-in language</b> (the
    /// picker's pre-selection): the actor's per-request **effective**
    /// language (ADR 0049 — the <c>kumunita.locale</c> cookie → first enabled
    /// <c>Accept-Language</c> match → instance default → <c>en</c> floor, the
    /// exact <see cref="EffectiveLanguageCode.ResolveAsync"/> chain the
    /// <c>&lt;kw-l&gt;</c> TagHelper resolves UI strings through) — "write in
    /// the language you're reading in", the ADR 0018 "instance default"
    /// pre-selection generalized. A null <see cref="ITranslationProvider"/>
    /// (test-construction site) falls back to the instance default — the
    /// legacy behavior, so the existing mock sites keep passing.
    /// </summary>
    private async Task<string> ResolveComposeDefaultLanguageAsync()
    {
        if (translationProvider is null)
            return await localization.GetDefaultLanguageCodeAsync().ConfigureAwait(false);
        return await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider).ConfigureAwait(false);
    }

    /// <summary>
    /// The group list (F14): the groups the actor owns or is a member of, projected
    /// to the <see cref="GroupViewModel"/> row shape (exactly four fields —
    /// <c>Id</c>, <c>Name</c>, <c>MemberCount</c>, <c>IsOwner</c>).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject))
            return View(new GroupListViewModel());

        var groups = await userInfo.GetGroupsForUserAsync(subject);

        // MemberCount is per-group: a second read per row (the U9 second-M2-ADD
        // GetGroupMembersAsync — one read lane also serves U10's Detail.Members).
        // The U9 pin: GroupViewModel is *exactly* { Id, Name, MemberCount } —
        // the three-field projection. The row's IsOwner state (the badge and
        // U10's gate) is derivable by the view from the membership row; the
        // list's own <see cref="GroupViewModel"/> shape stays the pinned
        // 3-tuple (drift-guard: no fields beyond the pin).
        var rows = new List<GroupViewModel>(groups.Count);
        foreach (var g in groups)
        {
            var members = await userInfo.GetGroupMembersAsync(g.Id);
            rows.Add(new GroupViewModel(g.Id, g.Name, members.Count));
        }

        // m2b — the actor's OWN pending invitations (the "Your invitations"
        // card). Read lane #2 (no audit, C-M2·2 carried); the group name per
        // row comes from the single-group read — the invitee CANNOT reach the
        // detail (owner ∪ member gate) until accepting, so the card must not
        // depend on it. An empty list renders no card at all.
        List<InvitationViewModel> invitations = [];
        var pending = await userInfo.GetPendingInvitationsForUserAsync(subject);
        foreach (var inv in pending)
        {
            var group = await userInfo.GetGroupAsync(inv.GroupId);
            var by = await userInfo.GetProfileAsync(inv.InvitedBy);
            invitations.Add(new InvitationViewModel(
                inv.GroupId,
                group?.Name ?? inv.GroupId,
                by?.DisplayName ?? inv.InvitedBy));
        }

        // ADR 0094 — the "Other public groups" directory: the public groups the
        // actor is NOT yet in (GetPublicGroupsAsync minus the actor's own
        // owner∪member set — a group the actor already belongs to or owns is on
        // the Groups list above, so requesting to join it would be a no-op).
        // Same GroupViewModel 3-tuple projection + the same per-row member-count
        // read; the only difference is the view's affordance (a request button).
        var myGroupIds = groups.Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
        var publicGroups = await userInfo.GetPublicGroupsAsync();
        var publicRows = new List<GroupViewModel>();
        foreach (var g in publicGroups)
        {
            if (myGroupIds.Contains(g.Id))
                continue;
            var members = await userInfo.GetGroupMembersAsync(g.Id);
            publicRows.Add(new GroupViewModel(g.Id, g.Name, members.Count));
        }

        // ADR 0094 — the actor's OWN pending join requests (the "Your join
        // requests" card; the withdraw self-lane's UI). Read lane (no audit,
        // C-M2·2 carried); the group name per row comes from the single-group
        // read — same shape as the invitations card above.
        List<JoinRequestViewModel> myJoinRequests = [];
        var pendingRequests = await userInfo.GetPendingJoinRequestsForUserAsync(subject);
        foreach (var req in pendingRequests)
        {
            var group = await userInfo.GetGroupAsync(req.GroupId);
            myJoinRequests.Add(new JoinRequestViewModel(
                req.GroupId,
                group?.Name ?? req.GroupId));
        }

        return View(new GroupListViewModel
        {
            Groups = rows,
            Invitations = invitations,
            PublicGroups = publicRows,
            MyJoinRequests = myJoinRequests
        });
    }

    /// <summary>
    /// The create-group form (GET <c>/groups/create</c>) — the target of the
    /// /groups "Create a group" / "Create one" links. Returns an empty
    /// <see cref="GroupCreateModel"/>; the paired POST below does the write.
    /// </summary>
    [HttpGet("create")]
    public IActionResult Create() => View(new GroupCreateModel());

    /// <summary>
    /// Create a group (POST <c>/groups/create</c>). The owner is the *actor*
    /// (<c>SubjectId(User)</c>) — never a form field — so ADR 0003 SoD is enforced
    /// structurally by the single identity source (the cookie principal), not by a
    /// re-gate. The M1 seam <c>CreateGroupAsync</c> commits the
    /// <see cref="Kumunita.Core.UserInfo.Group"/> + the owner's own
    /// <see cref="Kumunita.Core.UserInfo.GroupMembership"/> in one session, so the
    /// list on the next request already includes the new group (C4 strong
    /// consistency).
    /// </summary>
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GroupCreateModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject))
        {
            ModelState.AddModelError(nameof(GroupCreateModel.Name), "You must sign in to create a group.");
            return View(model);
        }

        // Name is required + trimmed: a whitespace-only name is a dead row. (The
        // M1 seam itself does not validate — ADR 0006-E "add a seam, named" —
        // validation stays a Web concern; see U8's "the guard is in the action" pin.)
        var name = (model.Name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
        {
            ModelState.AddModelError(nameof(GroupCreateModel.Name), "A group needs a name.");
            return View(model);
        }

        var description = string.IsNullOrWhiteSpace(model.Description)
            ? null
            : model.Description.Trim();

        var group = await userInfo.CreateGroupAsync(
            ownerId: subject,
            name: name,
            description: description,
            isPrivate: model.IsPrivate);

        TempData["info"] = $"Group “{group.Name}” created.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// The group detail + add/remove member surface (M2 plan U10):
    /// <c>GET /groups/{id}</c>. Renders the group's identity (name, owner),
    /// the owner's display name, the <see cref="Kumunita.Web.Models.GroupDetailViewModel.IsOwner"/>
    /// badge, and the member list (owner included — M1's
    /// <see cref="Kumunita.Core.UserInfo.IUserInfoService.CreateGroupAsync"/>
    /// commits the owner's own <see cref="Kumunita.Core.UserInfo.GroupMembership"
    /// "/> in one session, so the owner is a member *row* like any other).
    /// <para>
    /// **Web SoD pin (M2 plan U10 line 152):** the gate on *this* surface is the
    /// U9 <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetGroupsForUserAsync"/>
    /// projection (owner ∪ member) — a group the actor does not own and is not a
    /// member of is not visible, and 404s here (structural SoD; the audit lane is
    /// M1's per-<see cref="Kumunita.Core.UserInfo.GroupMembership"/>
    /// <c>Via: Owner</c>/<c>Via: Admin</c> derivation, the Web does not re-gate).
    /// <para>
    /// **ADR 0006-D:** the Web reads through the frozen
    /// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetGroupsForUserAsync"/>
    /// (the owner ∪ member set), <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetGroupMembersAsync"/>
    /// (the member rows), and <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetProfileAsync"/>
    /// (the per-row display names) — never a direct
    /// <see cref="Kumunita.Core.UserInfo.Group"/> or
    /// <see cref="Kumunita.Core.UserInfo.GroupMembership"/> query.
    /// <para>
    /// **U9's note (line 131):** reuse <c>GetGroupMembersAsync</c>, do not open
    /// a third member-read seam (design doc §2.7 freeze line).
    /// </para>
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(string id, int page = 1)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Unauthorized();

        // Web SoD gate: the actor must be in the owner ∪ member projection for this
        // group id (U9's GetGroupsForUserAsync pin, the single "groups for user"
        // read — ADR 0006-D). A non-visible group is a 404, not a redirect.
        var groups = await userInfo.GetGroupsForUserAsync(actor);
        var group = groups.FirstOrDefault(g => g.Id == id);
        if (group is null)
            return NotFound();

        // The member list (U9's second M2 read; the owner ∪ members set is already
        // the strong-consistency live rows — C4). One read lane serves U9's count
        // and U10's member list (design doc §2.7 — no third seam).
        var memberRows = await userInfo.GetGroupMembersAsync(group.Id);

        // The resident catalog read (the directory's visibility surface: every
        // non-blocked resident — the platform is invitation-only, so "who is
        // here" is not a gated read lane). One read also resolves the owner's,
        // each member's and each pending invitee's display name — the in-memory
        // lookups below replace the per-row GetProfileAsync pattern.
        var allProfiles = (await userInfo.GetProfilesAsync(verifiedOnly: false)).ToList();
        var bySubject = allProfiles
            .Where(p => !string.IsNullOrEmpty(p.SubjectId))
            .ToDictionary(p => p.SubjectId, p => p);

        // The owner's display name (falls back to the raw subject id if the
        // owner's profile is absent — fail-safe, not a silent "(owner)" stub
        // on the header).
        bySubject.TryGetValue(group.OwnerId, out var ownerProfile);

        // Each member's display name (an in-memory lookup off the catalog read
        // above; the fail-safe is the raw subject id, not a blank row).
        var members = new List<GroupMemberViewModel>(memberRows.Count);
        foreach (var row in memberRows)
        {
            Profile? p;
            bySubject.TryGetValue(row.UserId, out p);
            members.Add(new GroupMemberViewModel(row.UserId, p?.DisplayName ?? row.UserId));
        }

        // The IsOwner badge (a display-only pin; not a gate — M1's audit lane owns
        // the SoD derivation at the *write* path). A non-owner who is a member
        // sees "You are a member" (not "You own the group") but still sees the
        // Add form (the plan's U10 line 152 pin: "the controller passes
        // the actor's subjectId as addedBy and does not re-gate"); the Remove
        // lane is owner ∪ GlobalAdmin (C-M2·3) — the view hides its form for
        // plain members and the route 404s their POST.
        var isOwner = group.OwnerId == actor;
        // ── Group posts (ADR 0013) — the membership-scoped feed stays on the
        //    detail page; the composer is its own page (GET/POST under
        //    /groups/{id}/posts/new + /posts). Same lanes as the old feed
        //    action: ListGroupFeedAsync is the single access decision + the
        //    aggregate AccessAudit row (G·1/G·5), and the CanPost read is
        //    the live GetGroupIdsAsync membership read (G·3 — the POST gate
        //    stays the authoritative deny). The Detail page is member-scoped
        //    by its owner ∪ member gate, so every viewer here is a member
        //    and sees the feed + the "New post" button. ──
        var feed = await posts.ListGroupFeedAsync(group.Id, actor, page: page);

        var groupPosts = new List<PostListItem>(feed.Visible.Count);
        foreach (var post in feed.Visible)
        {
            var profile = await userInfo.GetProfileAsync(post.AuthorId);
            var preview = MarkdownRenderer.PlainTextPreview(post.Body, 200);
            groupPosts.Add(new PostListItem(
                post.Id,
                post.Title,
                preview,
                post.Created,
                profile?.DisplayName ?? post.AuthorId,
                post.AuthorId));
        }

        // CanPost: the SAME rule the CreateGroupPost gate enforces (G·3) —
        // the live membership read; a display convenience that drives the
        // composer's visibility (the POST gate is the authoritative deny).
        var canPost = (await userInfo.GetGroupIdsAsync(actor)).Contains(group.Id);

        // ── Group events (ADR 0089) — the membership-scoped events feed stays
        //    on the detail page (the group-posts lane's shape carried to the M4
        //    event surface). ListGroupEventsAsync is the single access decision
        //    + the aggregate AccessAudit row (GE·1/GE·5); the create gate is the
        //    authoritative deny (GE·3), so the "New event" button reuses the
        //    same live membership read as CanPost. The detail page is
        //    member-scoped by its owner ∪ member gate, so every viewer here is a
        //    member and sees the feed + the button. ──
        var eventFeed = await events.ListGroupEventsAsync(group.Id, actor, page: page);

        var groupEvents = new List<GroupEventListItem>(eventFeed.Visible.Count);
        foreach (var ev in eventFeed.Visible)
        {
            var authorProfile = await userInfo.GetProfileAsync(ev.AuthorId);
            var preview = MarkdownRenderer.PlainTextPreview(ev.Body, 200);
            groupEvents.Add(new GroupEventListItem(
                ev.Id,
                ev.Title,
                preview,
                ev.Start,
                authorProfile?.DisplayName ?? ev.AuthorId,
                ev.AuthorId));
        }

        // CanCreateEvent: the SAME rule the CreateGroupEvent gate enforces
        // (GE·3) — the live membership read; a display convenience that drives
        // the "New event" button's visibility (the POST gate is the
        // authoritative deny). Reuses the canPost membership read (one read
        // serves both affordances — the detail page is already member-scoped).
        var canCreateEvent = canPost;
        // canPost gates the "New post" button (the standalone compose page
        // at /groups/{id}/posts/new) — the POST gate is the authoritative deny.
        // m2b read lane #3 — the group's pending invitations (the owner's
        // invite surface: the pending list + cancel links). Read lane (no
        // audit, C-M2·2 carried); each row's display name via the same
        // catalog read as the member rows above.
        List<PendingInvitationViewModel> pendingInvitations = [];
        var pending = await userInfo.GetPendingInvitationsForGroupAsync(group.Id);
        foreach (var inv in pending)
        {
            Profile? p;
            bySubject.TryGetValue(inv.UserId, out p);
            pendingInvitations.Add(new PendingInvitationViewModel(inv.UserId, p?.DisplayName ?? inv.UserId));
        }

        // ADR 0094 — the group's pending join requests (the owner ∪ GlobalAdmin
        // review surface: the pending list + approve/decline links). Read lane
        // (no audit, C-M2·2 carried); each row's display name via the same
        // catalog read as the member rows above (the PendingInvitations shape
        // carried to the join-request axis).
        List<PendingJoinRequestViewModel> pendingJoinRequests = [];
        var pendingRequests = await userInfo.GetPendingJoinRequestsForGroupAsync(group.Id);
        foreach (var req in pendingRequests)
        {
            Profile? p;
            bySubject.TryGetValue(req.UserId, out p);
            pendingJoinRequests.Add(new PendingJoinRequestViewModel(req.UserId, p?.DisplayName ?? req.UserId));
        }

        // The "Add a member" dropdown rows: the catalog minus the group's
        // current members (adding someone already in is a no-op the form
        // should not offer), sorted by display name — the view filters
        // client-side (resident name contains the typed string).
        var memberSubjects = memberRows.Select(r => r.UserId).ToHashSet(StringComparer.Ordinal);
        var residentCandidates = allProfiles
            .Where(p => !p.Blocked)
            .Where(p => !memberSubjects.Contains(p.SubjectId))
            .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(p =>
            {
                string name = p.DisplayName;
                return new ResidentOption(
                    p.SubjectId,
                    string.IsNullOrWhiteSpace(name) ? p.SubjectId : name);
            })
            .ToList();

        // ── ADR 0026 — group name/description translations ────────────────
        // Rendered as a "a read, not a decision" surface (the same standing the
        // ADR 0022 post-detail surface uses for PostTranslation rows): the
        // group is already an authorized read for this actor (404 above if not
        // visible), so the translation rows inherit that reach. The enabled
        // catalog (ListLanguagesAsync, Enabled + SortOrder — the same read the
        // ADR 0022 post surface uses) seeds the chips / "add a translation"
        // candidate list. CanTranslate is the display convenience mirroring
        // UserInfoService's AddGroupTranslationAsync standing check — the POST
        // re-checks server-side, so this is not the gate.
        var groupTranslations = await userInfo.GetGroupTranslationsAsync(group.Id);
        var translationCodes = groupTranslations.Select(t => t.LanguageCode).ToHashSet();
        var catalog = await localization.ListLanguagesAsync();
        var groupLanguages = catalog
            .Where(l => l.Enabled)
            .OrderBy(l => l.SortOrder)
            .Select(l => new LanguageOption(l.Id, l.NativeName, translationCodes.Contains(l.Id)))
            .ToList();
        var canTranslate = userInfo.CanTranslateGroup(
            group.OwnerId, actor, KumunitaPrincipal.RoleSet(User));

        return View(new GroupDetailViewModel(
            group.Id,
            group.Name,
            group.Description,
            group.OwnerId,
            ownerProfile?.DisplayName ?? group.OwnerId,
            isOwner,
            members,
            pendingInvitations,
            residentCandidates,
            group.IsPrivate)
        {
            GroupPosts = groupPosts,
            GroupPostsTotal = feed.Total,
            CanPost = canPost,
            GroupEvents = groupEvents,
            GroupEventsTotal = eventFeed.Total,
            CanCreateEvent = canCreateEvent,
            GroupTranslations = groupTranslations,
            Languages = groupLanguages,
            CanTranslate = canTranslate,
            // ADR 0094 — the owner ∪ GlobalAdmin's pending join requests to
            // review (approve/decline); empty when the group holds none.
            PendingJoinRequests = pendingJoinRequests,
            // M7 (ADR 0090 D5) — the two paged sections' pagers (the F2
            // one-page no-render pin: null on a single page so the _Pager
            // partial renders nothing). The group is the route (D9) — no
            // filter form; the links carry ?page=N only.
            PagerPosts = (feed.HasMore || page > 1)
                ? PagedViewModel.ForRoute($"/groups/{group.Id}", page, 30, feed.HasMore)
                : null,
            PagerEvents = (eventFeed.HasMore || page > 1)
                ? PagedViewModel.ForRoute($"/groups/{group.Id}", page, 30, eventFeed.HasMore)
                : null,
        });
    }

    // ── Shared write-path helper (M2 plan U10, line 152) ────────────────
    // All three write lanes (AddMember, RemoveMember, invite/cancel) sit
    // on the <b>owner ∪ GlobalAdmin</b> standing (M2 design invariant
    // C-M2·3 — extended to the add lane by ADR 0007), on top of the
    // owner ∪ member reachability projection that the helper resolves
    // once (the plan's U10 line 152 pin: "the controller passes the
    // actor's subjectId as addedBy/removedBy and does not re-gate").
    // The helper returns a small value type (no `out` param on an async
    // method). A non-visible/denied group ⇒ (null, _) and the action 404s
    // (consistent failure shape across routes; no re-gate in any route).
    private sealed record ActorGroup(string Actor, Kumunita.Core.UserInfo.Group Group);

    private async Task<ActorGroup?> TryResolveWriteSurface(string id)
    {
        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(actor))
            return null;

        // Web SoD gate: the actor must be in the owner ∪ member projection
        // (U9's GetGroupsForUserAsync pin; the single "groups for user" read —
        // ADR 0006-D). A non-visible group is a 404, not a 200 + error text.
        var groups = await userInfo.GetGroupsForUserAsync(actor);
        var group = groups.FirstOrDefault(g => g.Id == id);
        return group is null ? null : new ActorGroup(actor, group);
    }

    /// <summary>
    /// Add a member (M2 plan U10, line 152; the add lane is on the C-M2·3
    /// owner ∪ GlobalAdmin standing per ADR 0007 — the same gate as
    /// RemoveMember and the m2b invite lane):
    /// <c>POST /groups/{id}/add-member</c>. The actor is the caller
    /// (<c>KumunitaPrincipal.SubjectId(User)</c>) — the form does not carry an
    /// owner id (a so-called "addedBy" field would be a Web-layer SoD hole;
    /// the plan's U10 line 152 pin: "the controller passes the actor's
    /// <c>subjectId</c> as <c>addedBy</c> and does not re-gate"). A plain
    /// member's POST 404s at the <see cref="TryResolveOwnerSurface"/> gate.
    /// The Core seam <see cref="Kumunita.Core.UserInfo.IUserInfoService.AddGroupMemberAsync"
    /// "/> loads the group's <c>OwnerId</c> in the same session and derives the
    /// <see cref="Kumunita.Core.Authorization.AccessVia"/> for the
    /// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row —
    /// <c>actor == OwnerId ⇒ Owner</c>, else <c>Admin</c>. The write is
    /// strong-consistency (C4): the new member is live on the very next
    /// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetGroupMembersAsync"/>
    /// call, and visible in the directory the next request (C4 + M2 plan U10's
    /// e2e c.).
    /// </summary>
    [HttpPost("{id}/add-member")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(string id, [FromForm] string? subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            return NotFound();

        var resolved = await TryResolveOwnerSurface(id);
        if (resolved is null)
            return NotFound();

        // The Core seam owns the SoD audit lane (Via: Owner / Via: Admin). The
        // Web passes the *actor* subject as `addedBy` and does not re-derive
        // the role — ADR 0006-D: Web shapes HTTP, Core decides; the M1 seam's
        // owner derivation is the single SoD source.
        await userInfo.AddGroupMemberAsync(
            groupId: resolved.Group.Id,
            userId: subjectId!,
            addedBy: resolved.Actor);

        TempData["info"] = $"Added a member to “{resolved.Group.Name}”.";
        return RedirectToAction(nameof(Detail), new { id = resolved.Group.Id });
    }

    /// <summary>
    /// Remove a member (M2 plan U10, line 152):
    /// <c>POST /groups/{id}/remove-member</c>. SoD gate: the
    /// <see cref="TryResolveOwnerSurface"/> owner ∪ GlobalAdmin lane (on top
    /// of <see cref="TryResolveWriteSurface"/>'s visibility projection) — M2
    /// design invariant C-M2·3 ("group SoD — owner ∪ GlobalAdmin only"); a
    /// plain member's remove POST 404s (a consistent failure shape with the
    /// m2b invite lane and with <see cref="AddMember"/>, which sits on the
    /// same owner ∪ GlobalAdmin lane per ADR 0007). ADR 0008: this lane never
    /// reaches the <b>owner's own</b> row — a member self-leaves through
    /// <see cref="LeaveGroup"/>, and the owner cannot leave at all (the view
    /// hides the button on the owner's own row, <em>and</em> the route
    /// itself 404s the owner-self target — see the check in the action body;
    /// a GlobalAdmin removing the owner still passes, actor ≠ target). The
    /// actor is the caller — the
    /// form does not carry an owner id (the <c>removedBy</c> field the M1
    /// seam takes is always the
    /// <c>KumunitaPrincipal.SubjectId(User)</c>; a form-bound owner id would
    /// defeat the seam's derivation). The Core seam's
    /// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row carries
    /// <c>Via: Owner</c>/<c>Via: Admin</c> per the M1 rule (actor == OwnerId
    /// ⇒ Owner, else Admin); the "who removed, when" fact is on that audit
    /// row (not on the <see cref="Kumunita.Core.UserInfo.GroupMembership"/>
    /// row, M1 design line 49).
    /// </summary>
    [HttpPost("{id}/remove-member")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(string id, [FromForm] string? subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            return NotFound();

        var resolved = await TryResolveOwnerSurface(id);
        if (resolved is null)
            return NotFound();

        // ADR 0008: the owner cannot self-remove through this lane either —
        // the detail view hides the button on their own row, and the route
        // enforces the same rule (a crafted POST with subjectId == the
        // owner's subject 404s, consistent failure shape). A GlobalAdmin
        // removing the *owner* still passes (the actor is not the target).
        if (StringComparer.Ordinal.Equals(subjectId, resolved.Group.OwnerId)
            && StringComparer.Ordinal.Equals(resolved.Actor, resolved.Group.OwnerId))
            return NotFound();

        // The shared Web-layer SoD pin: no re-derive. The M1 seam loads the
        // group's OwnerId in the same session and derives the audit row's Via.
        await userInfo.RemoveGroupMemberAsync(
            groupId: resolved.Group.Id,
            userId: subjectId!,
            removedBy: resolved.Actor);

        TempData["info"] = $"Removed {subjectId} from “{resolved.Group.Name}”.";
        return RedirectToAction(nameof(Detail), new { id = resolved.Group.Id });
    }

    /// <summary>
    /// A member leaving their own group (ADR 0008):
    /// <c>POST /groups/{id}/leave</c>. The target is the <b>actor</b> — minted
    /// from the signed-in principal, no form field (the m2b self-lane pattern:
    /// an actor-bound identity would be a Web-layer SoD hole, exactly the U10
    /// "no <c>removedBy</c> field" pin), and no one else's row is removable
    /// through this route. Gate: the U10 owner ∪ member reachability projection
    /// (<see cref="TryResolveWriteSurface"/>) — a non-visible group 404s, the
    /// consistent failure shape of the write lanes. On top of that the actor
    /// must <b>not</b> be the group's owner (ADR 0008's exception — an owner
    /// cannot leave their own group; the owner row is the group's anchor and
    /// owner transfer is not a M2-era surface): the owner's POST 404s, and the
    /// detail view hides the button on their own row so the form and the route
    /// agree (what the user sees is exactly what the route accepts). The write
    /// is the same strong-consistency seam as <see cref="RemoveMember"/>
    /// (<c>userId == removedBy == actor</c> — C4: the actor is out of the
    /// owner ∪ member projection on the very next read), so a non-member's
    /// re-POST simply hits the gate's 404 and the seam's no-membership branch
    /// — no state change, no 500. The redirect goes to
    /// <see cref="Index"/>, never <see cref="Detail"/>: the actor no longer
    /// passes the detail's visibility gate after leaving (the m2b
    /// <see cref="DeclineInvitation"/> redirect shape).
    /// </summary>
    [HttpPost("{id}/leave")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LeaveGroup(string id)
    {
        var resolved = await TryResolveWriteSurface(id);
        if (resolved is null)
            return NotFound();

        // ADR 0008's exception: the owner cannot self-leave. The owner row is
        // the group's anchor (M1's CreateGroupAsync commits it) — dropping it
        // would leave an ownerless group with no transfer lane; 404 keeps the
        // consistent failure shape (no 200 + error text).
        if (resolved.Group.OwnerId == resolved.Actor)
            return NotFound();

        await userInfo.RemoveGroupMemberAsync(
            groupId: resolved.Group.Id,
            userId: resolved.Actor,
            removedBy: resolved.Actor);

        TempData["info"] = $"You have left “{resolved.Group.Name}”.";
        return RedirectToAction(nameof(Index));
    }

    // ── ADR 0026: group name/description translations ──────────────────────

    /// <summary>
    /// Adds a **user-added translation** of the group's name and/or
    /// description into <paramref name="languageCode"/> (ADR 0026):
    /// <c>POST /groups/{id}/translations</c>. A thin Web lane (ADR 0006-D:
    /// routes + shape) that delegates the write + standing decision to
    /// <see cref="Kumunita.Core.UserInfo.IUserInfoService
    /// .AddGroupTranslationAsync"/> (the standing — owner / GlobalAdmin /
    /// Translator — is re-pinned server-side; the detail page's
    /// <see cref="Kumunita.Web.Models.GroupDetailViewModel.CanTranslate"/> is
    /// only the display affordance).
    /// <para>
    /// <b>Precondition:</b> the actor must be in the group's owner ∪ member
    /// reachability projection (the <see cref="TryResolveWriteSurface"/> gate,
    /// the same consistent 404 shape every other group write lane uses); the
    /// group is already an authorized read for the actor, so the translation
    /// write inherits that reach. A denied standing actor (a plain member)
    /// is a 403 (<see cref="UnauthorizedAccessException"/> →
    /// <see cref="Microsoft.AspNetCore.Mvc.Controller.Forbid"/>); a missing
    /// group is a 404. At least one of name/description must be non-blank
    /// (re-checked server-side by the seam).
    /// </para>
    /// <para>
    /// <b>Session shape (C3):</b> the controller owns the
    /// <see cref="Marten.IDocumentStore.LightweightSession()"/>; the service's
    /// <c>SaveChangesAsync</c> is the single write — the
    /// <see cref="Kumunita.Core.UserInfo.GroupTranslation"/> row and its
    /// <c>AccessAudit</c> row commit atomically.
    /// </para>
    /// </summary>
    [HttpPost("{id}/translations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode,
        [FromForm] string? name,
        [FromForm] string? description)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Forbid();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(description))
        {
            TempData["error"] = "A translation needs a name and/or description.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        // Reachability gate (owner ∪ member projection) — the consistent 404
        // shape every other group write lane uses.
        var resolved = await TryResolveWriteSurface(id);
        if (resolved is null)
            return NotFound();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await userInfo.AddGroupTranslationAsync(
                resolved.Group.Id,
                languageCode,
                string.IsNullOrWhiteSpace(name) ? null : name,
                string.IsNullOrWhiteSpace(description) ? null : description,
                actor,
                actorRoles,
                session);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var catalog = await localization.ListLanguagesAsync();
        var langName = catalog.FirstOrDefault(l => l.Id == languageCode)?.NativeName ?? languageCode;
        TempData["info"] = $"Translation added ({langName}).";
        return RedirectToAction(nameof(Detail), new { id = resolved.Group.Id });
    }

    // ── ADR 0048 — edit + delete lanes for group translations ─────────────
    // ADR 0026 was add-only; ADR 0048 lifts the "add-only" pin on the same
    // standing matrix (group owner / GlobalAdmin / Translator). Failure
    // shapes mirror the add lane (denied → 403; missing → 404).

    /// <summary>
    /// **Updates** the existing user-added translation of the group's name
    /// and/or description (ADR 0048):
    /// <c>POST /groups/{id}/translations/update</c>. Thin Web lane; delegates
    /// to <see cref="Kumunita.Core.UserInfo.IUserInfoService
    /// .UpdateGroupTranslationAsync"/>. Failure shapes mirror
    /// <see cref="AddTranslation"/>.
    /// </summary>
    [HttpPost("{id}/translations/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode,
        [FromForm] string? name,
        [FromForm] string? description)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Forbid();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(description))
        {
            TempData["error"] = "A translation needs a name and/or description.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var resolved = await TryResolveWriteSurface(id);
        if (resolved is null)
            return NotFound();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await userInfo.UpdateGroupTranslationAsync(
                resolved.Group.Id,
                languageCode,
                string.IsNullOrWhiteSpace(name) ? null : name,
                string.IsNullOrWhiteSpace(description) ? null : description,
                actor,
                actorRoles,
                session);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name2 = await SeedLanguageName(languageCode);
        TempData["info"] = $"Translation updated ({name2}).";
        return RedirectToAction(nameof(Detail), new { id = resolved.Group.Id });
    }

    /// <summary>
    /// **Removes** the existing user-added translation of the group's name
    /// and/or description (ADR 0048):
    /// <c>POST /groups/{id}/translations/remove</c>. Thin Web lane; delegates
    /// to <see cref="Kumunita.Core.UserInfo.IUserInfoService
    /// .RemoveGroupTranslationAsync"/>. Failure shapes mirror
    /// <see cref="AddTranslation"/>.
    /// </summary>
    [HttpPost("{id}/translations/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTranslation(
        [FromRoute] string id, [FromForm] string? languageCode)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Forbid();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var resolved = await TryResolveWriteSurface(id);
        if (resolved is null)
            return NotFound();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await userInfo.RemoveGroupTranslationAsync(resolved.Group.Id, languageCode, actor, actorRoles, session);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name2 = await SeedLanguageName(languageCode);
        TempData["info"] = $"Translation removed ({name2}).";
        return RedirectToAction(nameof(Detail), new { id = resolved.Group.Id });
    }

    // ── ADR 0009: the group's description (resident-facing display + the
    // owner ∪ GlobalAdmin write lane) ──────────────────────────────────

    /// <summary>
    /// Update the group's description (ADR 0009):
    /// <c>POST /groups/{id}/update-description</c>. The SoD lane is the
    /// <see cref="TryResolveOwnerSurface"/> owner ∪ GlobalAdmin standing
    /// (ADR 0007's new-lane rule — identical to the add/remove/invite
    /// lanes): a plain member's POST 404s, the same consistent failure shape
    /// as every other group write lane. The form carries the description
    /// value only — the actor is minted from the signed-in principal and
    /// passed as <c>updatedBy</c> (never a form field; the seam's
    /// <c>Via</c> derivation is the single SoD source, exactly the U10
    /// add-member pin). A blank value clears the description (the create
    /// lane's whitespace-is-null mapping, U9) so "clear" and "set" are the
    /// same route. The Core seam
    /// <see cref="IUserInfoService.UpdateGroupDescriptionAsync"/> loads the
    /// group's <c>OwnerId</c> in the same session and derives the audit
    /// row's <c>Via</c> (actor == OwnerId ⇒ Owner, else Admin; action
    /// <c>group.update</c>); the change is strong-consistency (C4) — the
    /// detail on the very next request renders the new value.
    /// </summary>
    [HttpPost("{id}/update-description")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDescription(string id, [FromForm] string? description)
    {
        var resolved = await TryResolveOwnerSurface(id);
        if (resolved is null)
            return NotFound();

        // Same whitespace mapping as the create lane (U9): a blank textarea
        // clears the description rather than storing whitespace.
        var value = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        await userInfo.UpdateGroupDescriptionAsync(
            groupId: resolved.Group.Id,
            description: value,
            updatedBy: resolved.Actor);

        TempData["info"] = value is null
            ? $"Cleared the description of “{resolved.Group.Name}”."
            : $"Saved the description of “{resolved.Group.Name}”.";
        return RedirectToAction(nameof(Detail), new { id = resolved.Group.Id });
    }

    // ── ADR 0010: the group's privacy (public ↔ private; the back-office
    // grant-list hide behind a single owner ∪ GlobalAdmin standing) ────

    /// <summary>
    /// Toggle the group's privacy (ADR 0010):
    /// <c>POST /groups/{id}/update-privacy</c>. The SoD lane is
    /// <see cref="TryResolveOwnerSurface"/> (owner ∪ GlobalAdmin — identical
    /// to the description edit lane's standing, ADR 0007's new-lane rule): a
    /// plain member's POST 404s, the same consistent failure shape as every
    /// other group write lane (a member can <i>see</i> the detail — owner ∪
    /// member — but not flip the privacy flag). The form carries only the
    /// boolean — an unchecked checkbox clears the field and binds
    /// <paramref name="isPrivate"/> to <c>false</c>, a checked one to
    /// <c>true</c>, so "make public" and "make private" are the same route and
    /// a single "Private group" checkbox is the whole surface. The actor is
    /// minted from the signed-in principal and passed as <c>updatedBy</c>
    /// (never a form field; the <c>Via</c> derivation is the single SoD
    /// source, exactly the description / U10 add-member pins). The Core seam
    /// <see cref="IUserInfoService.SetGroupPrivacyAsync"/> loads the group in
    /// the same session and derives the audit row's <c>Via</c> (actor ==
    /// OwnerId ⇒ Owner, else Admin; action <c>group.update</c>); strong
    /// consistency (C4) — the very next <c>/groups</c> grant picker read and
    /// the detail badge render the new value.
    /// </summary>
    [HttpPost("{id}/update-privacy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePrivacy(string id, [FromForm] bool isPrivate)
    {
        var resolved = await TryResolveOwnerSurface(id);
        if (resolved is null)
            return NotFound();

        await userInfo.SetGroupPrivacyAsync(
            groupId: resolved.Group.Id,
            isPrivate: isPrivate,
            updatedBy: resolved.Actor);

        TempData["info"] = isPrivate
            ? $"“{resolved.Group.Name}” is now private."
            : $"“{resolved.Group.Name}” is now public.";
        return RedirectToAction(nameof(Detail), new { id = resolved.Group.Id });
    }

    // ── ADR 0093: the group's delete (the owner ∪ GlobalAdmin lane, the
    // destructive counterpart of the privacy toggle above) ────────────────

    /// <summary>
    /// Delete a group (ADR 0093): <c>POST /groups/{id}/delete</c>. The SoD
    /// lane is <see cref="TryResolveOwnerSurface"/> (owner ∪ GlobalAdmin —
    /// ADR 0007's new-lane rule, identical to the add/remove/invite/
    /// description/privacy lanes): a plain member's POST 404s, the same
    /// consistent failure shape as every other group write lane. The form
    /// carries no actor id — the actor is minted from the signed-in
    /// principal and passed as <c>deletedBy</c> (never a form field; the
    /// seam's <c>Via</c> derivation is the single SoD source, exactly the U10
    /// add-member pin). The Core seam
    /// <see cref="IUserInfoService.DeleteGroupAsync"/> hard-deletes the group
    /// document, its membership rows, and its invitation rows in one session
    /// (invariant C3) and appends the <c>group.delete</c> audit row; group-
    /// scoped posts / events / translations are left in storage and become
    /// unreachable (the group lane is membership-only, ADR 0013, and the
    /// membership rows are gone) rather than cascaded.
    /// <para>
    /// The redirect goes to <see cref="Index"/>, never
    /// <see cref="Detail"/>: the actor is out of the owner ∪ member
    /// projection on the very next read (the ADR 0008
    /// <see cref="LeaveGroup"/> redirect shape), so a redirect to the detail
    /// page would 404.
    /// </para>
    /// </summary>
    [HttpPost("{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGroup(string id)
    {
        var resolved = await TryResolveOwnerSurface(id);
        if (resolved is null)
            return NotFound();

        await userInfo.DeleteGroupAsync(
            groupId: resolved.Group.Id,
            deletedBy: resolved.Actor);

        TempData["info"] = $"Group “{resolved.Group.Name}” deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── M2b: owner-invited membership (invite → accept/decline; the
    // immediate add/remove above is kept side-by-side — U10/F7 pin
    // untouched). docs/design/m2b-group-invitations.md ────────────────

    /// <summary>
    /// The owner ∪ GlobalAdmin SoD lane: reachability is U10's
    /// <see cref="TryResolveWriteSurface"/> (the actor must be in the owner ∪
    /// member projection), and on top of that the standing must be the
    /// <b>owner or a GlobalAdmin</b>. Three write routes sit on it: the m2b
    /// invite/cancel lane (C-M2b·1), the immediate
    /// <see cref="RemoveMember"/> lane (C-M2·3), and the immediate
    /// <see cref="AddMember"/> lane (C-M2·3 extended per ADR 0007). A plain
    /// member reaches the detail surface but their invite/cancel/remove/add
    /// POSTs 404. The audit <c>Via</c> derivation (owner ⇒ Owner, else
    /// Admin) stays exactly the M1 group-lane rule inside the Core seam.
    /// </summary>
    private async Task<ActorGroup?> TryResolveOwnerSurface(string id)
    {
        var resolved = await TryResolveWriteSurface(id);
        if (resolved is null)
            return null;

        if (resolved.Group.OwnerId != resolved.Actor && !KumunitaPrincipal.IsGlobalAdmin(User))
            return null;

        return resolved;
    }

    /// <summary>
    /// The m2b invite/cancel lane gate (C-M2b·1) — identical to
    /// <see cref="TryResolveOwnerSurface"/> (owner ∪ GlobalAdmin on top of
    /// the owner ∪ member projection); the lane-specific name keeps call
    /// sites self-documenting.
    /// </summary>
    private Task<ActorGroup?> TryResolveInviteSurface(string id)
        => TryResolveOwnerSurface(id);

    /// <summary>
    /// Invite a resident into the group (m2b lane C-M2b·1):
    /// <c>POST /groups/{id}/invite</c>. The form carries only the target
    /// subject — the actor's own subject is minted from the signed-in
    /// principal and passed as <c>invitedBy</c> (never a form field; the
    /// seam's <c>Via</c> derivation is the single SoD source, exactly the
    /// U10 add-member pin). The Core writes the
    /// <see cref="Kumunita.Core.UserInfo.GroupInvitation"/> row
    /// (<c>Pending</c>, re-invite resets a resolved row — C-M2b·3) but
    /// touches <b>no</b> membership: the membership lands only on
    /// <see cref="AcceptInvitation"/> (invariant C4 on that lane).
    /// </summary>
    [HttpPost("{id}/invite")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InviteMember(string id, [FromForm] string? subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            return NotFound();

        var resolved = await TryResolveInviteSurface(id);
        if (resolved is null)
            return NotFound();

        await userInfo.InviteGroupMemberAsync(
            groupId: resolved.Group.Id,
            userId: subjectId!,
            invitedBy: resolved.Actor);

        // The toast must show the resident's name, not their opaque subject id —
        // resolve the profile's display name (fall back to the subject id only
        // if no profile row exists, consistent with the invitations card's
        // <c>by?.DisplayName ?? inv.InvitedBy</c> shape).
        var invitee = await userInfo.GetProfileAsync(subjectId!);
        TempData["info"] = $"Invited {invitee?.DisplayName ?? subjectId} to “{resolved.Group.Name}”.";
        return RedirectToAction(nameof(Detail), new { id = resolved.Group.Id });
    }

    /// <summary>
    /// Accept a pending invitation (m2b self-lane C-M2b·2):
    /// <c>POST /groups/{id}/invitations/accept</c>. Only the invitee
    /// themselves can resolve their own row — the actor is always
    /// <c>SubjectId(User)</c>, and the gate is "in <em>my</em> pending list,
    /// else 404" (read lane #2; the Core re-verifies actor == row.UserId).
    /// On success the
    /// <see cref="Kumunita.Core.UserInfo.GroupMembership"/> row is live on
    /// the very next read (C4) — and the actor now passes the detail's
    /// owner ∪ member gate, hence the redirect <em>into</em> the group.
    /// A row already resolved by the time the click lands is mapped to an
    /// error message, never a 500 (C-M2b·3).
    /// </summary>
    [HttpPost("{id}/invitations/accept")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptInvitation(string id)
    {
        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Unauthorized();

        // Self-lane gate (C-M2b·2): the row must be in MY pending list.
        var pending = await userInfo.GetPendingInvitationsForUserAsync(actor);
        if (pending.All(inv => inv.GroupId != id))
            return NotFound();

        try
        {
            await userInfo.AcceptGroupInvitationAsync(id, actor);
        }
        catch (InvalidOperationException)
        {
            // Resolved (or re-invited) in the gap between the click and this
            // commit — the Core's C-M2b·3 invalid-transition wall.
            TempData["error"] = "That invitation is no longer pending.";
            return RedirectToAction(nameof(Index));
        }

        TempData["info"] = "Invitation accepted — you are now a member.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    /// <summary>
    /// Decline a pending invitation (m2b self-lane C-M2b·2):
    /// <c>POST /groups/{id}/invitations/decline</c>. Same gate and shape as
    /// <see cref="AcceptInvitation"/>, but <b>no</b> membership lane is
    /// written — the invitee simply never becomes a member; the row is
    /// re-invitable by the owner afterward (C-M2b·3).
    /// </summary>
    [HttpPost("{id}/invitations/decline")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclineInvitation(string id)
    {
        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Unauthorized();

        var pending = await userInfo.GetPendingInvitationsForUserAsync(actor);
        if (pending.All(inv => inv.GroupId != id))
            return NotFound();

        try
        {
            await userInfo.DeclineGroupInvitationAsync(id, actor);
        }
        catch (InvalidOperationException)
        {
            TempData["error"] = "That invitation is no longer pending.";
            return RedirectToAction(nameof(Index));
        }

        TempData["info"] = "Invitation declined.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Accept a pending invitation **from a link** (ADR 0095):
    /// <c>GET /groups/{id}/invitations/accept</c>. The link-clickable form of
    /// <see cref="AcceptInvitation"/> — the group-invite email's "Accept" link
    /// and the inbox's accept button both land here. Same self-lane gate (the
    /// row must be in MY pending list, C-M2b·2) and the same Core seam
    /// (<c>AcceptGroupInvitationAsync</c>) and the same invalid-transition
    /// wall (C-M2b·3 → <c>InvalidOperationException</c> → the error message)
    /// as the POST action; the only difference is that a GET must be
    /// link-clickable (it carries <b>no</b> anti-forgery token — the M1
    /// <c>/account/verify</c> one-time-link GET precedent, the link's
    /// integrity is the subject-bearing self-lane gate, not a CSRF token).
    /// An unauthenticated invitee (a fresh cookie after clicking the email
    /// link) is bounced to sign-in by the controller's
    /// <c>[Authorize]</c> (the cookie's
    /// <c>LoginPath</c>/<c>ReturnUrl</c> round-trip) and lands back here
    /// signed in.
    /// </summary>
    [HttpGet("{id}/invitations/accept")]
    public async Task<IActionResult> AcceptInvitationLink(string id)
    {
        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Unauthorized();

        var pending = await userInfo.GetPendingInvitationsForUserAsync(actor);
        if (pending.All(inv => inv.GroupId != id))
            return NotFound();

        try
        {
            await userInfo.AcceptGroupInvitationAsync(id, actor);
        }
        catch (InvalidOperationException)
        {
            TempData["error"] = "That invitation is no longer pending.";
            return RedirectToAction(nameof(Index));
        }

        TempData["info"] = "Invitation accepted — you are now a member.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    /// <summary>
    /// Decline a pending invitation **from a link** (ADR 0095):
    /// <c>GET /groups/{id}/invitations/decline</c>. The link-clickable form of
    /// <see cref="DeclineInvitation"/> — identical gate, seam, wall, and
    /// redirect shape; the only difference is that a GET carries <b>no</b>
    /// anti-forgery token (it must be link-clickable). An unauthenticated
    /// invitee is bounced to sign-in by <c>[Authorize]</c> and lands back
    /// here signed in.
    /// </summary>
    [HttpGet("{id}/invitations/decline")]
    public async Task<IActionResult> DeclineInvitationLink(string id)
    {
        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Unauthorized();

        var pending = await userInfo.GetPendingInvitationsForUserAsync(actor);
        if (pending.All(inv => inv.GroupId != id))
            return NotFound();

        try
        {
            await userInfo.DeclineGroupInvitationAsync(id, actor);
        }
        catch (InvalidOperationException)
        {
            TempData["error"] = "That invitation is no longer pending.";
            return RedirectToAction(nameof(Index));
        }

        TempData["info"] = "Invitation declined.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Cancel a pending invitation (m2b lane C-M2b·1 — owner ∪ GlobalAdmin
    /// only, the <see cref="TryResolveInviteSurface"/> gate):
    /// <c>POST /groups/{id}/invitations/{subjectId}/cancel</c>. The
    /// <c>subjectId</c> is the invitee's opaque subject, route-carried the
    /// way U10's <c>remove-member</c> carries its form field — never the
    /// actor's, never a re-gate. A row already resolved is an invalid
    /// transition (C-M2b·3) and maps to an error message, never a 500.
    /// </summary>
    [HttpPost("{id}/invitations/{subjectId}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelInvitation(string id, string subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            return NotFound();

        var resolved = await TryResolveInviteSurface(id);
        if (resolved is null)
            return NotFound();

        try
        {
            await userInfo.CancelGroupInvitationAsync(
                groupId: resolved.Group.Id,
                userId: subjectId!,
                cancelledBy: resolved.Actor);
        }
        catch (InvalidOperationException)
        {
            TempData["error"] = "That invitation is no longer pending.";
            return RedirectToAction(nameof(Detail), new { id = resolved.Group.Id });
        }

        TempData["info"] = $"Cancelled the invitation for {subjectId}.";
        return RedirectToAction(nameof(Detail), new { id = resolved.Group.Id });
    }

    // ── ADR 0094: resident self-initiated join requests (public groups) — the
    // reverse of the m2b invitation lane above: the resident starts it
    // (request/withdraw self-lane) and the owner ∪ GlobalAdmin resolves it
    // (approve → membership / decline). docs/adr/0094-group-join-request-lane.md ──

    /// <summary>
    /// Request to join a group (ADR 0094 self-lane):
    /// <c>POST /groups/{id}/join/request</c>. The actor is always
    /// <c>SubjectId(User)</c> (never a form field). The Core writes the
    /// <see cref="Kumunita.Core.UserInfo.GroupJoinRequest"/> row
    /// (<c>Pending</c>, re-request resets a resolved row — the ADR 0094 state
    /// machine) and appends the <c>group.join.request</c> audit row; it touches
    /// <b>no</b> membership — that lands only on
    /// <see cref="ApproveJoinRequest"/>. The route 404s when the group is
    /// missing (the Core's fail-safe) or when the actor already belongs to it
    /// (a request to join one's own group is a no-op — the Index view only
    /// offers the button for public non-member groups, this is the route's own
    /// wall).
    /// </summary>
    [HttpPost("{id}/join/request")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestToJoin(string id)
    {
        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Unauthorized();

        // Fail-safe: the group must exist and be one the actor can request to
        // join (public, and not already a member of). The Core does not
        // re-check membership (it is a write lane, not a projection), so the
        // Web asserts it here — the same "what is offered is what the route
        // accepts" rule the invite lane uses.
        var group = await userInfo.GetGroupAsync(id);
        if (group is null || group.IsPrivate)
            return NotFound();

        var myGroups = await userInfo.GetGroupsForUserAsync(actor);
        if (myGroups.Any(g => g.Id == id))
            return NotFound();

        try
        {
            await userInfo.RequestToJoinGroupAsync(id, actor);
        }
        catch (InvalidOperationException)
        {
            TempData["error"] = "Could not record that request.";
            return RedirectToAction(nameof(Index));
        }

        TempData["info"] = $"Requested to join “{group.Name}”.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Withdraw the actor's own pending join request (ADR 0094 self-lane):
    /// <c>POST /groups/{id}/join/withdraw</c>. Same gate and shape as
    /// <see cref="RequestToJoin"/>; the Core verifies actor == row.UserId (the
    /// self-lane wall) and moves the row to its terminal <c>Withdrawn</c> state
    /// (the m2b owner's <c>Cancelled</c> analogue — the row drops off both the
    /// requester's "Your join requests" card and the owner's review list) — the
    /// actor may re-request afterward (a re-request resets it to <c>Pending</c>).
    /// </summary>
    [HttpPost("{id}/join/withdraw")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WithdrawJoinRequest(string id)
    {
        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Unauthorized();

        // Self-lane gate: the row must be in MY pending list.
        var pending = await userInfo.GetPendingJoinRequestsForUserAsync(actor);
        if (pending.All(r => r.GroupId != id))
            return NotFound();

        try
        {
            await userInfo.WithdrawJoinRequestAsync(id, actor);
        }
        catch (InvalidOperationException)
        {
            // Resolved (approved/declined) in the gap between the click and this
            // commit — the Core's invalid-transition wall.
            TempData["error"] = "That request is no longer pending.";
            return RedirectToAction(nameof(Index));
        }

        TempData["info"] = "Join request withdrawn.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Approve a pending join request (ADR 0094 owner ∪ GlobalAdmin lane):
    /// <c>POST /groups/{id}/join-requests/{subjectId}/approve</c>. The
    /// <see cref="TryResolveOwnerSurface"/> gate (owner ∪ GlobalAdmin on top of
    /// the owner ∪ member projection — a plain member's POST 404s). The
    /// <c>subjectId</c> is the requester's opaque subject, route-carried the
    /// way <see cref="CancelInvitation"/> carries its form field. On success the
    /// <see cref="Kumunita.Core.UserInfo.GroupMembership"/> row is live on the
    /// very next read (C4). A row already resolved maps to an error message,
    /// never a 500.
    /// </summary>
    [HttpPost("{id}/join-requests/{subjectId}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveJoinRequest(string id, string subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            return NotFound();

        var resolved = await TryResolveOwnerSurface(id);
        if (resolved is null)
            return NotFound();

        try
        {
            await userInfo.ApproveJoinRequestAsync(
                groupId: resolved.Group.Id,
                userId: subjectId!,
                resolvedBy: resolved.Actor);
        }
        catch (InvalidOperationException)
        {
            TempData["error"] = "That request is no longer pending.";
            return RedirectToAction(nameof(Detail), new { id = resolved.Group.Id });
        }

        TempData["info"] = $"Approved {subjectId}'s request to join “{resolved.Group.Name}”.";
        return RedirectToAction(nameof(Detail), new { id = resolved.Group.Id });
    }

    /// <summary>
    /// Decline a pending join request (ADR 0094 owner ∪ GlobalAdmin lane):
    /// <c>POST /groups/{id}/join-requests/{subjectId}/decline</c>. The same
    /// <see cref="TryResolveOwnerSurface"/> gate and shape as
    /// <see cref="ApproveJoinRequest"/>, but <b>no</b> membership row is
    /// written — the requester simply never becomes a member; they may
    /// re-request afterward.
    /// </summary>
    [HttpPost("{id}/join-requests/{subjectId}/decline")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclineJoinRequest(string id, string subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            return NotFound();

        var resolved = await TryResolveOwnerSurface(id);
        if (resolved is null)
            return NotFound();

        try
        {
            await userInfo.DeclineJoinRequestAsync(
                groupId: resolved.Group.Id,
                userId: subjectId!,
                resolvedBy: resolved.Actor);
        }
        catch (InvalidOperationException)
        {
            TempData["error"] = "That request is no longer pending.";
            return RedirectToAction(nameof(Detail), new { id = resolved.Group.Id });
        }

        TempData["info"] = $"Declined {subjectId}'s request to join “{resolved.Group.Name}”.";
        return RedirectToAction(nameof(Detail), new { id = resolved.Group.Id });
    }

    // ── Group posts (ADR 0013, group-posts milestone U7) — the membership
    //    channel: feed / detail / create / reply under /groups/{id}/posts.
    //    Thin HTTP (ADR 0006-D: routes + shape; the M2 thin-controller
    //    precedent in this file): every access decision comes from
    //    <see cref="PostService"/>'s group surface (U6), the membership lane
    //    is the sole decision (G·1), the audience lane is never evaluated
    //    (G·8), there is no moderator / break-glass branch to reach (G·4 —
    //    *unavailable*, not deferred), and this controller never re-derives
    //    access — no <c>IAuthorizationService</c> call here at all. ──

    /// <summary>
    /// A group post's <b>detail</b> + its one-level replies (ADR 0013,
    /// G11 FACES): <c>GET /groups/{id}/posts/{postId}</c>. The service's
    /// <see cref="PostService.GetGroupPostAsync"/> is the single detail
    /// decision row (G·5, TargetId = the post id) and returns the replies
    /// <b>as-is</b> under the parent's single group-lane decision (G·7 —
    /// no second evaluation, no per-reply row). A missing post, a lane
    /// mismatch, or a membership Deny all return <c>Post = null</c> (Core
    /// doesn't distinguish — the audit row does); the controller maps that
    /// to a 404 (the group lane's fail-closed shape — G·3/G·4, this file's
    /// "a non-visible group 404s" precedent).
    /// </summary>
    [HttpGet("{id}/posts/{postId}")]
    public async Task<IActionResult> GroupPostDetail(string id, string postId)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(postId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        var group = await userInfo.GetGroupAsync(id);
        if (group is null)
            return NotFound();

        var result = await posts.GetGroupPostAsync(id, postId, actor);
        if (result.Post is null)
            return NotFound();

        var authorProfile = await userInfo.GetProfileAsync(result.Post.AuthorId);

        // ADR 0022 (group lane) — the post's user-added translations, the
        // enabled-catalog language set the chips / "add a translation"
        // candidate list render from, and the standing flag. On the group lane
        // the standing is author ∪ GlobalAdmin only (the component-moderator
        // branch is excluded by CanAddTranslation's isGroupLane flag — ADR 0007).
        var postTranslations = await posts.GetPostTranslationsAsync(result.Post.Id);
        var enabledLanguages = await SeedLanguagePickerAsync();
        var translationCodes = postTranslations.Select(t => t.LanguageCode).ToHashSet();
        var languages = enabledLanguages
            .Select(l => new LanguageOption(l.Code, l.NativeName, translationCodes.Contains(l.Code)))
            .ToList();
        var actorRoles = KumunitaPrincipal.RoleSet(User);
        var canTranslate = PostService.CanAddTranslation(
            isGroupLane: true, result.Post.ComponentId, result.Post.AuthorId, actor, actorRoles);

        var replyIds = result.Replies.Select(r => r.Id).ToList();
        var allReplyTranslations = await posts.GetReplyTranslationsAsync(replyIds);
        var translationsByReply = allReplyTranslations
            .GroupBy(t => t.ReplyId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Kumunita.Core.Posts.ReplyTranslation>)g.ToList());

        var replyItems = new List<ReplyItem>(result.Replies.Count);
        foreach (var reply in result.Replies)
        {
            var replyAuthorProfile = await userInfo.GetProfileAsync(reply.AuthorId);
            // ADR 0022 (group lane) — the reply's standing is its parent's:
            // author ∪ GlobalAdmin (no component-moderator branch, ADR 0007).
            var canTranslateReply = PostService.CanAddTranslation(
                isGroupLane: true, result.Post.ComponentId, reply.AuthorId, actor, actorRoles);
            replyItems.Add(new ReplyItem(
                reply.Id,
                replyAuthorProfile?.DisplayName ?? reply.AuthorId,
                reply.AuthorId,
                reply.Body,
                reply.Created,
                reply.Modified,
                reply.AuthorId == actor,
                translationsByReply.TryGetValue(reply.Id, out var trs) ? trs : [],
                canTranslateReply,
                reply.DeletedAt,
                reply.LanguageCode));
        }

        // ADR 0018 — the reply form's authored-in language picker options
        // (the enabled catalog), stored on ViewData (the same read-only
        // channel the post detail page uses — the detail VM is a projection,
        // not a form-bound model).
        ViewData["Reply_Languages"] = enabledLanguages;

        // Back-link display name — the group's name in the viewer's language
        // (the ADR 0026 floor, exactly the idiom the community-name surface
        // uses; a read, not a decision: the post's single group-lane
        // decision already ran in GetGroupPostAsync). A display gap, not an
        // error: when no translation exists for the viewer's language the
        // stored (authored) name stays.
        string groupName = group.Name;
        if (translationProvider is not null)
        {
            string gLang = await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider);
            var gTranslations = await userInfo.GetGroupTranslationsAsync(id);
            var gMatch = gTranslations.FirstOrDefault(t => String.Equals(t.LanguageCode, gLang, StringComparison.OrdinalIgnoreCase));
            if (gMatch is not null && !string.IsNullOrWhiteSpace(gMatch.Name))
                groupName = gMatch.Name;
        }

        return View("PostDetail", new GroupPostDetailViewModel
        {
            GroupId = id,
            Post = result.Post,
            AuthorDisplayName = authorProfile?.DisplayName ?? result.Post.AuthorId,
            AuthorSubjectId = result.Post.AuthorId,
            Replies = replyItems,
            IsAuthor = result.Post.AuthorId == actor,
            PostTranslations = postTranslations,
            Languages = languages,
            CanTranslate = canTranslate,
            OriginalLanguageCode = result.Post.LanguageCode, // TD·1/TD·4 (ADR 0027) — the authored-in code, read from the ADR 0018 field.
            GroupDisplayName = groupName ?? group.Name,
        });
    }

    /// <summary>
    /// The group-post <b>composer's page</b> (the standalone compose surface
    /// the detail page's "New post" button links to): <c>GET
    /// /groups/{id}/posts/new</c>. Returns an empty
    /// <see cref="GroupPostComposeViewModel"/> for <c>New.cshtml</c> (the U7
    /// failure re-render view, now a first-class page). The group's identity
    /// is the route's <c>{id}</c>; the membership lane that reaches this page
    /// is the same owner ∪ member projection as the group's <see
    /// cref="Detail"/> (the paired POST's gate is the authoritative deny,
    /// G·3) — no separate re-gate here.
    /// </summary>
    [HttpGet("{id}/posts/new")]
    public async Task<IActionResult> NewGroupPost(string id)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Unauthorized();

        var group = await userInfo.GetGroupAsync(id);
        if (group is null)
            return NotFound();

        return View("New", new GroupPostComposeViewModel
        {
            Languages = await SeedLanguagePickerAsync(), // ADR 0018 — the authored-in language picker.
            // ADR 0018 / ADR 0049 — pre-select the actor's current effective
            // language so the picker highlights the right option and a
            // no-change submit is a concrete BCP-47 code (never an empty row).
            LanguageCode = await ResolveComposeDefaultLanguageAsync(),
        });
    }

    /// <summary>
    /// The group-post <b>composer's POST</b> (ADR 0013, G5/G6 FACES):
    /// <c>POST /groups/{id}/posts</c>. The form carries <b>title + body
    /// only</b> (the <see cref="GroupPostComposeViewModel"/> shape — the
    /// M3 composer minus the component picker and the audience slot; the
    /// group's membership is the audience proxy, and the service writes the
    /// post's <c>Audience</c> non-null and <b>empty</b> — G·8 — regardless
    /// of anything on this form). The group's identity is the route's
    /// <c>{id}</c>, never a form field (a form-bound group id would be a
    /// lane-bypass hole). <para>
    /// **Create gate (G·3):** <see
    /// cref="PostService.CreateGroupPostAsync"/> <b>is</b> the group-lane
    /// membership decision (the <see cref="IDocumentStore.LightweightSession"/>
    /// lane — C3 same-transaction shape: the controller opens the session,
    /// the service's <c>SaveChangesAsync</c> is the single write; the gate
    /// row + the post commit atomically). A non-member (including a
    /// non-member moderator or GlobalAdmin — G·4, no skip on this lane)
    /// hits the <see cref="UnauthorizedAccessException"/> wall — mapped to
    /// a 404 (the master register's "Web renders 404" pin; this file's
    /// "a plain member's POST 404s" precedent).
    /// </para>
    /// </summary>
    [HttpPost("{id}/posts")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGroupPost(string id, [FromForm] GroupPostComposeViewModel model)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to post.");
            return View("New", model);
        }

        var group = await userInfo.GetGroupAsync(id);
        if (group is null)
            return NotFound();

        model.Languages = await SeedLanguagePickerAsync(); // ADR 0018 — re-seed on re-render

        if (!model.IsValid)
        {
            // Re-render the standalone compose page, prefilled with what
            // the actor typed (the GET /groups/{id}/posts/new shape).
            ModelState.AddModelError(nameof(model.Body), "A post needs some text.");
            return View("New", model);
        }

        var draft = new GroupPostDraft(
            GroupId: id,
            Title: string.IsNullOrWhiteSpace(model.Title) ? null : model.Title,
            Body: model.Body.Trim(),
            LanguageCode: string.IsNullOrWhiteSpace(model.LanguageCode) ? null : model.LanguageCode,
            ImageIds: ContentImageIds.ExtractContentImageIds(model.Body), // RC R·3 (U05) — server-side parse of the body's /content-image/{id} links; the client never sends the ids (drift pause b: the U04 field was inert, now wired).
            AttachmentIds: AttachmentIds.ExtractAttachmentIds(model.Body), // ATT U7 (C-ATT·4) — server-side parse of the body's /attachment/{id} links; the client never sends the ids (parity with the image lane's group-post wire, C-ATT·9).
            IsDraft: model.SaveAsDraft // ADR 0037 — draft mode: saved but invisible to all but the author until published.
        );

        // C3 same-transaction lane: the controller opens the
        // <c>IDocumentStore.LightweightSession()</c>, the service's
        // <c>SaveChangesAsync</c> is the single write (the M3
        // PostsController <c>New</c> precedent) — the gate's audit row and
        // the new <c>Post</c> commit atomically.
        await using var session = store.LightweightSession();

        Post post;
        try
        {
            post = await posts.CreateGroupPostAsync(draft, actor, session);
        }
        catch (UnauthorizedAccessException)
        {
            // G·3/G·4 — only group members may post to the channel; the
            // gate's Deny row was persisted before the throw (G6 FACES).
            // 404: the register's "Web renders 404" pin (a 403 on a POST
            // would advertise a gate the UI doesn't offer).
            return NotFound();
        }

        TempData["info"] = $"Post added to “{group.Name}”.";
        return Redirect($"/groups/{id}/posts/{post.Id}");
    }

    // ── Group-post edit (ADR 0016, author-only) ──────────────────────────────

    /// <summary>
    /// The group-post <b>editor's page</b> (ADR 0016):
    /// <c>GET /groups/{id}/posts/{postId}/edit</c>. The author-only edit
    /// lane's GET — a mirror of the M3 <see cref="PostsController.Edit"/>
    /// (ADR 0014) adapted to the group lane: the actor must be the post's
    /// own author, and the post must be a **group** post of this group
    /// (the group-lane identity check, G·2). A non-author, a non-group
    /// post, a missing group, or a missing post all return a 404 (the group
    /// lane's fail-closed shape — G·3/G·4, this file's "a non-visible group
    /// 404s" precedent; deliberately **not** a 403, which would advertise a
    /// gate the UI doesn't offer). <para>
    /// The form is title + body + the authored-in language tag (ADR 0018,
    /// amended 2026-09-13 — ADR 0016's editable surface extended) — the
    /// group lane has no audience slot (G·8: the audience is non-null empty,
    /// the membership is the audience proxy) and no component picker (G·2 lane
    /// exclusivity: the post's <c>ComponentId</c> is empty and stays empty).
    /// The edit reuses the <see cref="GroupPostComposeViewModel"/> shape
    /// (title + body + language picker, the same "mirror, minus the audience
    /// slot" as the composer).
    /// </para>
    /// </summary>
    [HttpGet("{id}/posts/{postId}/edit")]
    public async Task<IActionResult> EditGroupPost(string id, string postId)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(postId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        var group = await userInfo.GetGroupAsync(id);
        if (group is null)
            return NotFound();

        // Load the post in a lightweight read (the group-lane identity check
        // + the author gate are the pre-render decisions; the POST's gate is
        // the authoritative deny, mirroring the create lane's shape).
        await using var read = store.LightweightSession();
        var post = await read.LoadAsync<Post>(postId);
        if (post is null || string.IsNullOrEmpty(post.GroupId) || post.GroupId != id)
            return NotFound();

        if (post.AuthorId != actor)
            return NotFound();

        return View("Edit", new GroupPostComposeViewModel
        {
            Title = post.Title,
            Body = post.Body,
            // ADR 0018 (amended 2026-09-13) — the authored-in language tag is
            // editable on this lane (ADR 0016 surface extended): seed the
            // picker from the enabled catalog and pre-select the post's
            // stored tag (the ADR 0017 edit-lane precedent).
            Languages = await SeedLanguagePickerAsync(),
            LanguageCode = post.LanguageCode,
        });
    }

    /// <summary>
    /// The group-post <b>editor's POST</b> (ADR 0016, author-only):
    /// <c>POST /groups/{id}/posts/{postId}/edit</c>. Re-writes the post's
    /// title, body, and authored-in language tag (ADR 0018, amended
    /// 2026-09-13) via <see cref="PostService.UpdateGroupPostAsync"/> —
    /// the service is the decision: a non-author (even a GlobalAdmin, even a
    /// member who is the post's *replier*) is denied with
    /// <see cref="UnauthorizedAccessException"/>, and a non-group post or a
    /// missing id is a <see cref="KeyNotFoundException"/> — both mapped to
    /// the 404 fail-closed shape (G·3/G·4, the group lane's register pin).
    /// The post's <c>GroupId</c> / <c>ComponentId</c> / <c>Audience</c> /
    /// <c>AuthorId</c> / <c>Created</c> / <c>Status</c> are untouched (the
    /// group lane's identity is immutable); the edit stamps
    /// <c>Post.Modified</c> forward. One <c>SaveChangesAsync</c> (C3).
    /// </summary>
    [HttpPost("{id}/posts/{postId}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditGroupPost(
        string id, string postId, [FromForm] GroupPostComposeViewModel model)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(postId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to edit.");
            return Redirect($"/groups/{id}/posts/{postId}");
        }

        var group = await userInfo.GetGroupAsync(id);
        if (group is null)
            return NotFound();

        if (!model.IsValid)
        {
            // Re-render the editor, prefilled with what the actor typed (the
            // GET /groups/{id}/posts/{postId}/edit shape).
            model.Languages = await SeedLanguagePickerAsync(); // ADR 0018 — re-seed on re-render
            ModelState.AddModelError(nameof(model.Body), "A post needs some text.");
            return View("Edit", model);
        }

        // C3 same-transaction lane: the controller opens the
        // <c>IDocumentStore.LightweightSession()</c>, the service's
        // <c>SaveChangesAsync</c> is the single write (the M3
        // PostsController <c>Edit</c> POST precedent) — the author gate and
        // the write commit atomically.
        await using var session = store.LightweightSession();

        Post post;
        try
        {
            post = await posts.UpdateGroupPostAsync(
                postId, actor,
                string.IsNullOrWhiteSpace(model.Title) ? null : model.Title,
                model.Body.Trim(),
                string.IsNullOrWhiteSpace(model.LanguageCode) ? null : model.LanguageCode, // ADR 0018 (amended) — the authored-in tag
                session,
                AttachmentIds.ExtractAttachmentIds(model.Body.Trim())); // ATT U12 (C-ATT·4/8) — the group-post edit lane re-parses the re-submitted body (replace-style); the image edit lane stays byte-for-byte (C-ATT·9).
        }
        catch (KeyNotFoundException)
        {
            // Missing id or a non-group post (G·2 lane check failed): the
            // 404 fail-closed shape (non-leaky about which ids are real).
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            // Non-author: the 404 fail-closed shape (a 403 on a POST would
            // advertise a gate the UI doesn't offer — the group lane's
            // register pin, G·3/G·4).
            return NotFound();
        }

        TempData["info"] = "Post updated.";
        return Redirect($"/groups/{id}/posts/{post.Id}");
    }

    /// <summary>
    /// A group-post <b>reply</b> (ADR 0013, G11 FACES):
    /// <c>POST /groups/{id}/posts/{postId}/replies</c>. Exactly the M3
    /// <c>Replies(id, body)</c> one-field form shape — a plain
    /// <c>body</c> field, no per-reply audience (G·7: a reply inherits the
    /// parent's single group-lane decision; <see
    /// cref="PostService.CreateReplyAsync"/> is lane-neutral and is
    /// <b>reused as-is</b> — no new Core seam, no group field on the
    /// <c>PostReply</c>). Before opening a write session the parent's
    /// group-lane decision is re-run via
    /// <see cref="PostService.GetGroupPostAsync"/> (the same
    /// <c>Post = null</c> fail-closed shape as <see cref="GroupPostDetail"/>'s
    /// GET, mapped to a 404 here) — the reply lands only on a post the
    /// viewer can currently see (C4 strong consistency: a member removed
    /// in the gap between the detail render and the POST is denied).
    /// </summary>
    [HttpPost("{id}/posts/{postId}/replies")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GroupPostReply(string id, string postId, [FromForm] string? body, [FromForm] string? languageCode)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(postId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        if (string.IsNullOrWhiteSpace(body))
        {
            // A reply is a body-only write (G·7: no own audience, no
            // title). Fail-closed to the detail page — the form is
            // re-presented there.
            TempData["error"] = "A reply needs some text.";
            return Redirect($"/groups/{id}/posts/{postId}");
        }

        // Authz via the parent's single group-lane decision (G·7 — the
        // reply inherits it, so the decision is the pre-write gate).
        // GetGroupPostAsync returns Post = null for **both** "missing /
        // lane mismatch" and "membership denied" (Core doesn't
        // distinguish; the audit row does) — both map to the 404 fail-
        // closed shape (the register's non-member-404 pin).
        var parent = await posts.GetGroupPostAsync(id, postId, actor);
        if (parent.Post is null)
            return NotFound();

        // C3 same-transaction lane: the controller owns the session; the
        // service's <c>SaveChangesAsync</c> is the single write (the M3
        // PostsController <c>Replies</c> precedent).
        await using var session = store.LightweightSession();
        await posts.CreateReplyAsync(postId, actor, body, session, string.IsNullOrWhiteSpace(languageCode) ? null : languageCode); // ADR 0018 — the reply's own authored-in tag; null/empty ⇒ instance default.

        TempData["info"] = "Reply added.";
        return Redirect($"/groups/{id}/posts/{postId}");
    }

    // ── Group-reply edit (ADR 0016, author-only) ─────────────────────────────

    /// <summary>
    /// A group-post <b>reply's edit</b> (ADR 0016, author-only):
    /// <c>POST /groups/{id}/posts/{postId}/replies/{replyId}/edit</c>. A
    /// body-only re-write (a reply carries no <c>Audience</c>, C-M3·1, and no
    /// title) via <see cref="PostService.UpdateReplyAsync"/> — the service is
    /// the decision: only the reply's own author may edit it (no moderator or
    /// GlobalAdmin branch). Before writing, the parent's group-lane decision
    /// is re-run via <see cref="PostService.GetGroupPostAsync"/> (the same
    /// <c>Post = null</c> fail-closed shape as <see cref="GroupPostReply"/>,
    /// mapped to a 404) and the reply must be **under this post** — both are
    /// the group lane's non-leaky 404 posture (G·3/G·4; a non-member or a
    /// reply not on this post 404s). A non-author is the
    /// <see cref="UnauthorizedAccessException"/> wall, also mapped to the 404
    /// fail-closed shape. The edit stamps <c>PostReply.Modified</c> forward
    /// (null until first edited); <c>PostId</c> / <c>AuthorId</c> /
    /// <c>Created</c> are untouched. One <c>SaveChangesAsync</c> (C3).
    /// </summary>
    [HttpPost("{id}/posts/{postId}/replies/{replyId}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditGroupPostReply(
        string id, string postId, string replyId, [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(postId) || string.IsNullOrEmpty(replyId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = "A reply needs some text.";
            return Redirect($"/groups/{id}/posts/{postId}");
        }

        // The parent's single group-lane decision (G·7 — the reply inherits
        // it) is the pre-write gate: a non-member, a missing post, or a lane
        // mismatch all return Post = null → 404 (the group lane's non-leaky
        // fail-closed shape, the register's non-member-404 pin).
        var parent = await posts.GetGroupPostAsync(id, postId, actor);
        if (parent.Post is null)
            return NotFound();

        // The reply must be **under this post** (a replyId on a different
        // post is not reachable through this group lane — the 404 shape).
        if (parent.Replies.All(r => r.Id != replyId))
            return NotFound();

        // C3 same-transaction lane: the controller owns the session; the
        // service's <c>SaveChangesAsync</c> is the single write (the
        // <see cref="GroupPostReply"/> precedent).
        await using var session = store.LightweightSession();
        try
        {
            await posts.UpdateReplyAsync(replyId, actor, body, session);
        }
        catch (KeyNotFoundException)
        {
            // Reply id not found → the 404 fail-closed shape.
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            // Non-author → the 404 fail-closed shape (a 403 on a POST would
            // advertise a gate the UI doesn't offer — G·3/G·4).
            return NotFound();
        }

        TempData["info"] = "Reply updated.";
        return Redirect($"/groups/{id}/posts/{postId}");
    }

    // ── Author soft-delete (ADR 0024, group lane — 404 fail-closed shape) ──

    /// <summary>
    /// Author <b>soft-deletes</b> a group post (ADR 0024):
    /// <c>POST /groups/{id}/posts/{postId}/delete</c>. A thin Web lane
    /// (ADR 0006-D) delegating the write + author-stand decision to
    /// <see cref="PostService.DeletePostAsync"/> — the record is kept
    /// (<see cref="Post.DeletedAt"/> stamped), never hard-deleted. The group
    /// lane's non-leaky posture (G·3/G·4) maps both <see
    /// cref="KeyNotFoundException"/> and <see cref="UnauthorizedAccessException"/>
    /// to a 404 (a 403 on a POST would advertise a gate the UI doesn't
    /// offer). The parent's single group-lane decision is the pre-write gate
    /// (the <see cref="EditGroupPostReply"/> precedent): a non-member / missing
    /// post / lane mismatch 404s, and the reply/post must be on this group.
    /// </summary>
    [HttpPost("{id}/posts/{postId}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGroupPost(string id, string postId)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(postId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        // The parent's single group-lane decision (G·7) is the pre-write gate:
        // a non-member, a missing post, or a lane mismatch all return Post =
        // null → 404 (the group lane's non-leaky fail-closed shape).
        var parent = await posts.GetGroupPostAsync(id, postId, actor);
        if (parent.Post is null)
            return NotFound();

        // C3 same-transaction lane: the controller owns the session; the
        // service's <c>SaveChangesAsync</c> is the single write (the
        // <see cref="EditGroupPost"/> precedent).
        await using var session = store.LightweightSession();
        try
        {
            await posts.DeletePostAsync(postId, actor, session);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            // Non-author → the 404 fail-closed shape (a 403 on a POST would
            // advertise a gate the UI doesn't offer — G·3/G·4).
            return NotFound();
        }

        TempData["info"] = "Post deleted.";
        return Redirect($"/groups/{id}/posts/{postId}");
    }

    /// <summary>
    /// Publish a draft group post (ADR 0037): <c>POST
    /// /groups/{id}/posts/{postId}/publish</c>. <b>Author-only</b> — the sole
    /// lever that clears <see cref="Post.IsDraft"/> is the author's own choice
    /// (a non-member, non-author, moderator, or GlobalAdmin is denied: a
    /// group-lane draft is invisible to them, so they have no affordance to
    /// reach this; the service re-pins the author gate server-side). The
    /// group lane's non-leaky posture (G·3/G·4) maps both <see
    /// cref="KeyNotFoundException"/> and <see cref="UnauthorizedAccessException"/>
    /// to a 404 (the <see cref="DeleteGroupPost"/> precedent — a 403 on a POST
    /// would advertise a gate the UI doesn't offer). On success, redirect back
    /// to the detail page (now live).
    /// </summary>
    [HttpPost("{id}/posts/{postId}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishGroupPost(string id, string postId)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(postId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        // The parent's single group-lane decision (G·7) is the pre-write gate:
        // a non-member, a missing post, a lane mismatch, or a draft the actor
        // is not the author of all return Post = null → 404 (the group lane's
        // non-leaky fail-closed shape, the ADR 0037 author-only draft gate).
        var parent = await posts.GetGroupPostAsync(id, postId, actor);
        if (parent.Post is null)
            return NotFound();

        await using var session = store.LightweightSession();
        try
        {
            await posts.PublishPostAsync(postId, actor, session);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            // Non-author → the 404 fail-closed shape (G·3/G·4).
            return NotFound();
        }

        TempData["info"] = "Post published.";
        return Redirect($"/groups/{id}/posts/{postId}");
    }

    /// <summary>
    /// Author <b>soft-deletes</b> a group-post reply (ADR 0024):
    /// <c>POST /groups/{id}/posts/{postId}/replies/{replyId}/delete</c>. The
    /// record is kept (<see cref="PostReply.DeletedAt"/> stamped) — the detail
    /// view renders a placeholder in its place and the reply still counts
    /// toward the parent's count. Same group-lane 404 fail-closed shape as
    /// <see cref="EditGroupPostReply"/> (both <see
    /// cref="KeyNotFoundException"/> and <see cref="UnauthorizedAccessException"/>
    /// map to a 404; the parent's single decision is the pre-write gate).
    /// </summary>
    [HttpPost("{id}/posts/{postId}/replies/{replyId}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGroupPostReply(string id, string postId, string replyId)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(postId) || string.IsNullOrEmpty(replyId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        // The parent's single group-lane decision (G·7 — the reply inherits it)
        // is the pre-write gate (the <see cref="EditGroupPostReply"/> precedent).
        var parent = await posts.GetGroupPostAsync(id, postId, actor);
        if (parent.Post is null)
            return NotFound();

        // The reply must be **under this post** (a replyId on a different post
        // is not reachable through this group lane — the 404 shape).
        if (parent.Replies.All(r => r.Id != replyId))
            return NotFound();

        await using var session = store.LightweightSession();
        try
        {
            await posts.DeleteReplyAsync(replyId, actor, session);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }

        TempData["info"] = "Reply deleted.";
        return Redirect($"/groups/{id}/posts/{postId}");
    }

    // ── Group events (ADR 0089) — the ADR 0013 membership lane applied to the
    //    M4 event surface. The read/write lanes resolve through <see
    //    cref="IEventService"/>'s group seams (CreateGroupEventAsync /
    //    UpdateGroupEventAsync / PublishAsync / RsvpAsync / GetMyRsvpAsync /
    //    GetRsvpsAsync); the group-lane membership gate is the authoritative
    //    deny (GE·3), edit/publish are author-only (GE·4), and RSVP + the
    //    author-only RSVP list reuse the M4 lane keyed by EventId (GE·7 — no
    //    group branch). Every 404 is the non-leaky fail-closed shape (a 403 on
    //    a POST would advertise a gate the UI doesn't offer, the G·3/G·4 pin). ──

    /// <summary>
    /// A group event's <b>detail</b> page (ADR 0089, GE·1/GE·3):
    /// <c>GET /groups/{id}/events/{eventId}</c>. The single detail decision
    /// (GE·5, TargetId = the event id) runs at Core via
    /// <see cref="IEventService.GetGroupEventAsync"/>; a denied, missing, or
    /// lane-mismatched event (a draft the actor did not author included) is a
    /// 404 (the group lane's fail-closed shape — G·3/G·4, this file's "a
    /// non-visible group 404s" precedent). On success the RSVP surface is
    /// assembled from the M4 lane reused as-is (GE·7: the author sees the full
    /// <c>GetRsvpsAsync</c> list; every member sees their own
    /// <c>GetMyRsvpAsync</c> RSVP). The Edit / Publish affordances are the
    /// author's (GE·4, author-only — <b>no</b> GlobalAdmin override).
    /// </summary>
    [HttpGet("{id}/events/{eventId}")]
    public async Task<IActionResult> GroupEventDetail(string id, string eventId)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(eventId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        var group = await userInfo.GetGroupAsync(id);
        if (group is null)
            return NotFound();

        var @event = await events.GetGroupEventAsync(id, eventId, actor);
        if (@event is null)
            return NotFound(); // non-member / lane mismatch / non-author draft (GE·3/GE·4)

        var isAuthor = @event.AuthorId == actor;

        var authorProfile = await userInfo.GetProfileAsync(@event.AuthorId);

        // The RSVP surface (GE·7 — the M4 lane reused as-is, keyed by EventId):
        // the author's full list + the viewer's own RSVP.
        var myRsvp = default(EventRsvp);
        try
        {
            myRsvp = await events.GetMyRsvpAsync(eventId, actor, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException) { myRsvp = null; }
        catch (UnauthorizedAccessException) { myRsvp = null; }

        var rsvpRows = default(IReadOnlyList<EventRsvp>);
        if (isAuthor)
        {
            try
            {
                rsvpRows = await events.GetRsvpsAsync(eventId, HttpContext.RequestAborted);
            }
            catch (KeyNotFoundException) { rsvpRows = []; }
        }
        rsvpRows ??= [];

        var rsvps = new List<EventRsvpEntry>(rsvpRows.Count);
        foreach (var rsvp in rsvpRows)
        {
            var p = await userInfo.GetProfileAsync(rsvp.UserId);
            var name = p?.DisplayName is not null && p.DisplayName.Length > 0 ? p.DisplayName : rsvp.UserId;
            rsvps.Add(new EventRsvpEntry(rsvp, name));
        }

        // The group's display name (the ADR 0026 floor, the group-post detail's
        // back-link idiom): the stored name, resolved into the viewer's
        // language when a user-added name translation exists.
        var groupName = group.Name;
        if (translationProvider is not null)
        {
            string gLang = await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider);
            var gTranslations = await userInfo.GetGroupTranslationsAsync(id);
            var gMatch = gTranslations.FirstOrDefault(t => String.Equals(t.LanguageCode, gLang, StringComparison.OrdinalIgnoreCase));
            if (gMatch is not null && !string.IsNullOrWhiteSpace(gMatch.Name))
                groupName = gMatch.Name;
        }

        return View("EventDetail", new GroupEventDetailViewModel
        {
            GroupId = id,
            GroupDisplayName = groupName,
            Event = @event,
            AuthorDisplayName = authorProfile?.DisplayName ?? @event.AuthorId,
            AuthorSubjectId = @event.AuthorId,
            IsAuthor = isAuthor,
            MyRsvp = myRsvp,
            Rsvps = rsvps,
        });
    }

    /// <summary>
    /// The group-event <b>composer's page</b> (ADR 0089, GE·3):
    /// <c>GET /groups/{id}/events/new</c>. Returns an empty
    /// <see cref="GroupEventComposeViewModel"/> for the standalone compose page
    /// (the failure re-render view, the group-post <c>New.cshtml</c> analog).
    /// The group's identity is the route's <c>{id}</c>; the membership lane that
    /// reaches this page is the same owner ∪ member projection as the group's
    /// <see cref="Detail"/> (the paired POST's gate is the authoritative deny,
    /// GE·3) — no separate re-gate here.
    /// </summary>
    [HttpGet("{id}/events/new")]
    public async Task<IActionResult> NewGroupEvent(string id)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Unauthorized();

        var group = await userInfo.GetGroupAsync(id);
        if (group is null)
            return NotFound();

        return View("EventNew", new GroupEventComposeViewModel
        {
            Languages = await SeedLanguagePickerAsync(), // ADR 0018 — the authored-in language picker.
            // ADR 0018 / ADR 0049 — pre-select the actor's current effective
            // language so the picker highlights the right option and a
            // no-change submit is a concrete BCP-47 code (never an empty row).
            LanguageCode = await ResolveComposeDefaultLanguageAsync(),
            // Default the time range to the actor's *current* local date/time
            // (the M4 CreateGet idiom: now rounded to the minute, End one hour
            // after Start — the author adjusts both on the form).
            Start = DateTime.Now,
            End = DateTime.Now.AddHours(1),
        });
    }

    /// <summary>
    /// The group-event <b>composer's POST</b> (ADR 0089, GE·3):
    /// <c>POST /groups/{id}/events</c>. The form carries the event's editable
    /// surface (title + body + time + location + capacity + color + the
    /// authored-in language + the save-as-draft toggle); the group's identity
    /// is the route's <c>{id}</c>, never a form field (a form-bound group id
    /// would be a lane-bypass hole). The create gate <b>is</b> the group-lane
    /// membership decision (GE·3): <see
    /// cref="IEventService.CreateGroupEventAsync"/> runs in the controller's
    /// <c>LightweightSession</c> (C3 same-transaction shape — the gate row +
    /// the new event commit atomically). A non-member (including a non-member
    /// moderator or GlobalAdmin — GE·4, no skip on this lane) hits the
    /// <see cref="UnauthorizedAccessException"/> wall — mapped to a 404 (the
    /// register's "Web renders 404" pin). The write pins GE·2/GE·8
    /// (server-side: <c>GroupId</c> = the lane marker,
    /// <c>ComponentId = string.Empty</c>, non-null empty <c>Audience</c>,
    /// <c>IsDraft = true</c>) regardless of anything on this form.
    /// </summary>
    [HttpPost("{id}/events")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGroupEvent(string id, [FromForm] GroupEventComposeViewModel model)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to create an event.");
            return View("EventNew", model);
        }

        var group = await userInfo.GetGroupAsync(id);
        if (group is null)
            return NotFound();

        model.Languages = await SeedLanguagePickerAsync(); // ADR 0018 — re-seed on re-render

        if (!model.IsValid)
        {
            // Re-render the standalone compose page, prefilled with what the
            // actor typed (the GET /groups/{id}/events/new shape).
            if (string.IsNullOrWhiteSpace(model.Body))
                ModelState.AddModelError(nameof(model.Body), "An event needs some text.");
            if (model.Start == default || model.End == default)
                ModelState.AddModelError(string.Empty, "An event needs a start and an end time.");
            if (model.End <= model.Start)
                ModelState.AddModelError(nameof(model.End), "The end must be after the start.");
            return View("EventNew", model);
        }

        var draft = new GroupEventDraft(
            GroupId: id,
            Title: string.IsNullOrWhiteSpace(model.Title) ? string.Empty : model.Title,
            Body: model.Body.Trim(),
            StartUtc: model.Start,
            EndUtc: model.End,
            Location: string.IsNullOrWhiteSpace(model.Location) ? null : model.Location.Trim(),
            Capacity: model.Capacity,
            Color: string.IsNullOrWhiteSpace(model.Color) ? null : model.Color,
            LanguageCode: string.IsNullOrWhiteSpace(model.LanguageCode) ? null : model.LanguageCode,
            ReminderEnabled: true); // ADR 0037 §6.4 floor — the §6.4 job's default.

        // C3 same-transaction lane: the controller opens the
        // <c>IDocumentStore.LightweightSession()</c>, the service's
        // <c>SaveChangesAsync</c> is the single write (the group-post create
        // precedent) — the gate's audit row and the new event commit atomically.
        await using var session = store.LightweightSession();

        Kumunita.Core.Events.Event @event;
        try
        {
            @event = await events.CreateGroupEventAsync(draft, actor, session);
        }
        catch (UnauthorizedAccessException)
        {
            // GE·3/GE·4 — only group members may create events for the channel;
            // the gate's Deny row was persisted before the throw (GE6 FACES).
            // 404: the register's "Web renders 404" pin (a 403 on a POST would
            // advertise a gate the UI doesn't offer).
            return NotFound();
        }

        TempData["info"] = $"Event added to “{group.Name}”.";
        return Redirect($"/groups/{id}/events/{@event.Id}");
    }

    /// <summary>
    /// The group-event <b>editor's page</b> (ADR 0089, GE·4):
    /// <c>GET /groups/{id}/events/{eventId}/edit</c>. The author-only edit
    /// lane's GET (the ADR 0016 group-post edit idiom carried to the M4 event
    /// surface): the actor must be the event's own author (a non-author, even a
    /// GlobalAdmin, 404s — GE·4), and the event must be a **group** event of
    /// this group (the group-lane identity check, GE·2). The form is the event's
    /// editable surface — the group lane has no audience slot (GE·8) and no
    /// component picker (GE·2 lane exclusivity).
    /// </summary>
    [HttpGet("{id}/events/{eventId}/edit")]
    public async Task<IActionResult> EditGroupEvent(string id, string eventId)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(eventId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        var group = await userInfo.GetGroupAsync(id);
        if (group is null)
            return NotFound();

        // The group-lane identity check + the author gate (GE·4) before render:
        // a non-author or a non-group event is a 404 (the group-post edit GET's
        // fail-closed shape). The POST's gate is the authoritative deny.
        var @event = await events.GetGroupEventAsync(id, eventId, actor);
        if (@event is null || !string.Equals(@event.AuthorId, actor, StringComparison.Ordinal))
            return NotFound();

        return View("EventEdit", new GroupEventComposeViewModel
        {
            Title = @event.Title,
            Body = @event.Body,
            Start = @event.Start,
            End = @event.End,
            Location = @event.Location,
            Capacity = @event.Capacity,
            Color = @event.Color,
            Languages = await SeedLanguagePickerAsync(), // ADR 0018 — the authored-in language picker.
            LanguageCode = @event.LanguageCode,          // ADR 0018 (amended) — the authored-in tag.
        });
    }

    /// <summary>
    /// The group-event <b>editor's POST</b> (ADR 0089, GE·4, author-only):
    /// <c>POST /groups/{id}/events/{eventId}/edit</c>. Re-writes the event's
    /// editable surface via <see cref="IEventService.UpdateGroupEventAsync"/> —
    /// the service is the decision: a non-author (even a GlobalAdmin) is denied
    /// with <see cref="UnauthorizedAccessException"/>, and a non-group event or a
    /// missing id is a <see cref="KeyNotFoundException"/> — both mapped to the
    /// 404 fail-closed shape (GE·2/GE·4, the group lane's register pin). The
    /// lane markers (<c>GroupId</c> / <c>ComponentId</c> / <c>Audience</c> /
    /// <c>AuthorId</c> / <c>Created</c> / <c>IsDraft</c>) are untouched.
    /// </summary>
    [HttpPost("{id}/events/{eventId}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditGroupEvent(
        string id, string eventId, [FromForm] GroupEventComposeViewModel model)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(eventId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to edit.");
            return Redirect($"/groups/{id}/events/{eventId}");
        }

        var group = await userInfo.GetGroupAsync(id);
        if (group is null)
            return NotFound();

        if (!model.IsValid)
        {
            // Re-render the editor, prefilled with what the actor typed.
            model.Languages = await SeedLanguagePickerAsync(); // ADR 0018 — re-seed on re-render
            if (string.IsNullOrWhiteSpace(model.Body))
                ModelState.AddModelError(nameof(model.Body), "An event needs some text.");
            if (model.Start == default || model.End == default)
                ModelState.AddModelError(string.Empty, "An event needs a start and an end time.");
            if (model.End <= model.Start)
                ModelState.AddModelError(nameof(model.End), "The end must be after the start.");
            return View("EventEdit", model);
        }

        var update = new GroupEventUpdate(
            Title: string.IsNullOrWhiteSpace(model.Title) ? string.Empty : model.Title,
            Body: model.Body.Trim(),
            StartUtc: model.Start,
            EndUtc: model.End,
            Location: string.IsNullOrWhiteSpace(model.Location) ? null : model.Location.Trim(),
            Capacity: model.Capacity,
            Color: string.IsNullOrWhiteSpace(model.Color) ? null : model.Color,
            LanguageCode: string.IsNullOrWhiteSpace(model.LanguageCode) ? null : model.LanguageCode,
            ReminderEnabled: null); // null = leave the event's stored value (the ADR 0016 shape).

        // C3 same-transaction lane (the group-post edit POST precedent).
        await using var session = store.LightweightSession();

        Kumunita.Core.Events.Event @event;
        try
        {
            @event = await events.UpdateGroupEventAsync(
                eventId, actor, update, session, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            // Missing id or a non-group event (GE·2 lane check failed): the
            // 404 fail-closed shape (non-leaky about which ids are real).
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            // Non-author → the 404 fail-closed shape (GE·3/GE·4).
            return NotFound();
        }

        TempData["info"] = "Event updated.";
        return Redirect($"/groups/{id}/events/{@event.Id}");
    }

    /// <summary>
    /// Publishes a group event's <b>draft</b> (ADR 0089, GE·4, author-only;
    /// ADR 0037): <c>POST /groups/{id}/events/{eventId}/publish</c>. The event's
    /// single group-lane decision (via
    /// <see cref="IEventService.GetGroupEventAsync"/>) is the pre-write gate: a
    /// non-member, a missing event, a lane mismatch, or a draft the actor is not
    /// the author of all 404. On success the author-only
    /// <see cref="IEventService.PublishAsync"/> lane (the M4
    /// <c>EventService.PublishAsync</c> reused as-is, GE·7) flips
    /// <c>IsDraft = false</c> — a non-author is denied and 404s (the group
    /// lane's non-leaky shape, the <see cref="PublishGroupPost"/> precedent).
    /// </summary>
    [HttpPost("{id}/events/{eventId}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishGroupEvent(string id, string eventId)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(eventId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        // The event's single group-lane decision (GE·7) is the pre-write gate:
        // a non-member, a missing event, a lane mismatch, or a draft the actor
        // is not the author of all 404 (the group lane's non-leaky fail-closed
        // shape, the ADR 0037 author-only draft gate).
        var @event = await events.GetGroupEventAsync(id, eventId, actor);
        if (@event is null)
            return NotFound();

        try
        {
            await events.PublishAsync(eventId, actor, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            // Non-author → the 404 fail-closed shape (GE·3/GE·4).
            return NotFound();
        }

        TempData["info"] = "Event published.";
        return Redirect($"/groups/{id}/events/{eventId}");
    }

    /// <summary>
    /// A group event's <b>RSVP</b> (ADR 0089, GE·7 — the M4 RSVP lane reused
    /// as-is, keyed by <c>EventId</c>, no group branch):
    /// <c>POST /groups/{id}/events/{eventId}/rsvp</c>. The event's single
    /// group-lane decision (via <see
    /// cref="IEventService.GetGroupEventAsync"/>) is the pre-write gate: a
    /// non-member, a missing event, or a lane mismatch is a 404. A signed-out
    /// actor cannot RSVP (404 — the group lane's non-leaky shape). The
    /// <see cref="IEventService.RsvpAsync"/> write (last-write-wins, no audit
    /// row — the M4 §3.2 pin) upserts the actor's <c>(EventId, UserId)</c> row
    /// with the latest <see cref="RsvpStatus"/>.
    /// </summary>
    [HttpPost("{id}/events/{eventId}/rsvp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GroupEventRsvp(string id, string eventId, [FromForm] RsvpStatus status)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(eventId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        // The event's single group-lane decision (GE·7) is the pre-write gate:
        // a non-member, a missing event, or a lane mismatch is a 404.
        var @event = await events.GetGroupEventAsync(id, eventId, actor);
        if (@event is null)
            return NotFound();

        try
        {
            await events.RsvpAsync(eventId, actor, status, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }

        return Redirect($"/groups/{id}/events/{eventId}");
    }

    // ── Translations (ADR 0022, group lane) ────────────────────────────────

    /// <summary>
    /// Adds a **user-added translation** of a group post into
    /// <paramref name="languageCode"/> (ADR 0022, group lane):
    /// <c>POST /groups/{id}/posts/{postId}/translations</c>. A thin Web lane
    /// (ADR 0006-D) delegating the write + standing decision to
    /// <see cref="PostService.AddPostTranslationAsync"/>. On the group lane the
    /// standing is **author ∪ GlobalAdmin only** (the component-moderator
    /// branch is excluded by the service's <c>isGroupLane</c> flag — ADR 0007:
    /// a group has no component-moderator scope). A denied standing actor is a
    /// 404 (the group lane's non-leaky fail-closed shape — a 403 on a POST would
    /// advertise a gate; the register's non-member-404 pin).
    /// </summary>
    [HttpPost("{id}/posts/{postId}/translations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPostTranslation(
        string id, string postId, [FromForm] string? languageCode, [FromForm] string? title, [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(postId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/groups/{id}/posts/{postId}");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = "A translation needs some text.";
            return Redirect($"/groups/{id}/posts/{postId}");
        }

        // The parent's single group-lane decision (G·7) is the precondition:
        // non-member, missing post, or lane mismatch all return Post = null → 404.
        var parent = await posts.GetGroupPostAsync(id, postId, actor);
        if (parent.Post is null)
            return NotFound();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await posts.AddPostTranslationAsync(
                postId,
                languageCode,
                string.IsNullOrWhiteSpace(title) ? null : title,
                body,
                actor,
                actorRoles,
                session);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            // Denied standing → the group lane's 404 fail-closed shape (the
            // EditGroupPostReply precedent; a 403 would advertise a gate).
            return NotFound();
        }

        TempData["info"] = $"Translation added ({await SeedLanguageName(languageCode)}).";
        return Redirect($"/groups/{id}/posts/{postId}");
    }

    /// <summary>
    /// Adds a **user-added translation** of a group post's reply into
    /// <paramref name="languageCode"/> (ADR 0022, group lane):
    /// <c>POST /groups/{id}/posts/{postId}/replies/{replyId}/translations</c>.
    /// Standing (author ∪ GlobalAdmin, no component-moderator branch — ADR 0007)
    /// + fail-closed-404 shape mirror <see cref="AddPostTranslation"/>; the
    /// reply must be under this post (the <see cref="EditGroupPostReply"/>
    /// precedent).
    /// </summary>
    [HttpPost("{id}/posts/{postId}/replies/{replyId}/translations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReplyTranslation(
        string id, string postId, string replyId, [FromForm] string? languageCode, [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(postId) || string.IsNullOrEmpty(replyId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/groups/{id}/posts/{postId}");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = "A translation needs some text.";
            return Redirect($"/groups/{id}/posts/{postId}");
        }

        var parent = await posts.GetGroupPostAsync(id, postId, actor);
        if (parent.Post is null)
            return NotFound();
        if (parent.Replies.All(r => r.Id != replyId))
            return NotFound();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await posts.AddReplyTranslationAsync(
                replyId,
                languageCode,
                body,
                actor,
                actorRoles,
                session);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }

        TempData["info"] = $"Translation added ({await SeedLanguageName(languageCode)}).";
        return Redirect($"/groups/{id}/posts/{postId}");
    }

    // ── ADR 0048 — edit + delete lanes for group post / reply translations ─
    // ADR 0022 (group lane) was add-only; ADR 0048 lifts the "add-only" pin on
    // the same standing matrix (author ∪ GlobalAdmin, no community moderator).
    // Failure shapes mirror the add lane: the group lane's 404 fail-closed
    // posture (a 403 would advertise a gate the group lane does not expose).

    /// <summary>
    /// **Updates** the existing user-added translation of a group post in
    /// <paramref name="languageCode"/> (ADR 0048):
    /// <c>POST /groups/{id}/posts/{postId}/translations/update</c>. Failure
    /// shapes mirror <see cref="AddPostTranslation"/> (404 fail-closed).
    /// </summary>
    [HttpPost("{id}/posts/{postId}/translations/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePostTranslation(
        string id, string postId, [FromForm] string? languageCode, [FromForm] string? title, [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(postId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/groups/{id}/posts/{postId}");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = "A translation needs some text.";
            return Redirect($"/groups/{id}/posts/{postId}");
        }

        var parent = await posts.GetGroupPostAsync(id, postId, actor);
        if (parent.Post is null)
            return NotFound();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await posts.UpdatePostTranslationAsync(
                postId, languageCode,
                string.IsNullOrWhiteSpace(title) ? null : title,
                body, actor, actorRoles, session);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }

        TempData["info"] = $"Translation updated ({await SeedLanguageName(languageCode)}).";
        return Redirect($"/groups/{id}/posts/{postId}");
    }

    /// <summary>
    /// **Removes** the existing user-added translation of a group post in
    /// <paramref name="languageCode"/> (ADR 0048):
    /// <c>POST /groups/{id}/posts/{postId}/translations/remove</c>. Failure
    /// shapes mirror <see cref="AddPostTranslation"/> (404 fail-closed).
    /// </summary>
    [HttpPost("{id}/posts/{postId}/translations/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePostTranslation(
        string id, string postId, [FromForm] string? languageCode)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(postId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/groups/{id}/posts/{postId}");
        }

        var parent = await posts.GetGroupPostAsync(id, postId, actor);
        if (parent.Post is null)
            return NotFound();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await posts.RemovePostTranslationAsync(postId, languageCode, actor, actorRoles, session);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }

        TempData["info"] = $"Translation removed ({await SeedLanguageName(languageCode)}).";
        return Redirect($"/groups/{id}/posts/{postId}");
    }

    /// <summary>
    /// **Updates** the existing user-added translation of a group post's reply
    /// (ADR 0048):
    /// <c>POST /groups/{id}/posts/{postId}/replies/{replyId}/translations/update</c>.
    /// Failure shapes mirror <see cref="AddReplyTranslation"/> (404 fail-closed).
    /// </summary>
    [HttpPost("{id}/posts/{postId}/replies/{replyId}/translations/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateReplyTranslation(
        string id, string postId, string replyId, [FromForm] string? languageCode, [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(postId) || string.IsNullOrEmpty(replyId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/groups/{id}/posts/{postId}");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = "A translation needs some text.";
            return Redirect($"/groups/{id}/posts/{postId}");
        }

        var parent = await posts.GetGroupPostAsync(id, postId, actor);
        if (parent.Post is null)
            return NotFound();
        if (parent.Replies.All(r => r.Id != replyId))
            return NotFound();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await posts.UpdateReplyTranslationAsync(replyId, languageCode, body, actor, actorRoles, session);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }

        TempData["info"] = $"Translation updated ({await SeedLanguageName(languageCode)}).";
        return Redirect($"/groups/{id}/posts/{postId}");
    }

    /// <summary>
    /// **Removes** the existing user-added translation of a group post's reply
    /// (ADR 0048):
    /// <c>POST /groups/{id}/posts/{postId}/replies/{replyId}/translations/remove</c>.
    /// Failure shapes mirror <see cref="AddReplyTranslation"/> (404 fail-closed).
    /// </summary>
    [HttpPost("{id}/posts/{postId}/replies/{replyId}/translations/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveReplyTranslation(
        string id, string postId, string replyId, [FromForm] string? languageCode)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(postId) || string.IsNullOrEmpty(replyId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/groups/{id}/posts/{postId}");
        }

        var parent = await posts.GetGroupPostAsync(id, postId, actor);
        if (parent.Post is null)
            return NotFound();
        if (parent.Replies.All(r => r.Id != replyId))
            return NotFound();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await posts.RemoveReplyTranslationAsync(replyId, languageCode, actor, actorRoles, session);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }

        TempData["info"] = $"Translation removed ({await SeedLanguageName(languageCode)}).";
        return Redirect($"/groups/{id}/posts/{postId}");
    }

    /// <summary>
    /// Resolves a BCP-47 code to its catalog <c>NativeName</c> for a
    /// <c>TempData</c> confirmation message (a display convenience — a
    /// <see cref="ILocalizationService.ListLanguagesAsync"/> read, not a
    /// decision). Falls back to the raw code when the language is not in the
    /// catalog (a never-blank shape).
    /// </summary>
    private async Task<string> SeedLanguageName(string code)
    {
        var catalog = await localization.ListLanguagesAsync();
        return catalog.FirstOrDefault(l => l.Id == code)?.NativeName ?? code;
    }
}
