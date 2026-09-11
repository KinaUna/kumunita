using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
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
public sealed class GroupsController(IUserInfoService userInfo) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

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

        return View(new GroupListViewModel { Groups = rows, Invitations = invitations });
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
    public async Task<IActionResult> Detail(string id)
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
            group.IsPrivate));
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

        TempData["info"] = $"Invited {subjectId} to “{resolved.Group.Name}”.";
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
}
