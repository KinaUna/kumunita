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
/// to"); <c>Create</c> (POST <c>/groups</c>) calls the M1 seam
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

        return View(new GroupListViewModel { Groups = rows });
    }

    /// <summary>
    /// Create a group (POST <c>/groups</c>). The owner is the *actor*
    /// (<c>SubjectId(User)</c>) — never a form field — so ADR 0003 SoD is enforced
    /// structurally by the single identity source (the cookie principal), not by a
    /// re-gate. The M1 seam <c>CreateGroupAsync</c> commits the
    /// <see cref="Kumunita.Core.UserInfo.Group"/> + the owner's own
    /// <see cref="Kumunita.Core.UserInfo.GroupMembership"/> in one session, so the
    /// list on the next request already includes the new group (C4 strong
    /// consistency).
    /// </summary>
    [HttpPost]
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
            description: description);

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

        // The owner's display name (a single GetProfileAsync read; falls back to
        // the raw subject id if the owner's profile is absent — fail-safe, not a
        // silent "(owner)" stub on the header).
        var ownerProfile = await userInfo.GetProfileAsync(group.OwnerId);

        // Each member's display name (a per-row GetProfileAsync read — the same
        // single-document read as U9's MemberCount pattern; N+1 is acceptable
        // per the U9 precedent and the "single identity source" ADR 0003 SoD
        // pin).
        var members = new List<GroupMemberViewModel>(memberRows.Count);
        foreach (var row in memberRows)
        {
            var p = await userInfo.GetProfileAsync(row.UserId);
            members.Add(new GroupMemberViewModel(row.UserId, p?.DisplayName ?? row.UserId));
        }

        // The IsOwner badge (a display-only pin; not a gate — M1's audit lane owns
        // the SoD derivation at the *write* path). A non-owner who is a member
        // sees "You are a member" (not "You own the group") but still sees the
        // Add/Remove forms (the plan's U10 line 152 pin: "the controller passes
        // the actor's subjectId as addedBy/removedBy and does not re-gate").
        var isOwner = group.OwnerId == actor;

        return View(new GroupDetailViewModel(
            group.Id,
            group.Name,
            group.OwnerId,
            ownerProfile?.DisplayName ?? group.OwnerId,
            isOwner,
            members));
    }

    // ── Shared write-path helper (M2 plan U10, line 152) ────────────────
    // Both AddMember and RemoveMember share the same SoD gate (the actor
    // must be in the owner ∪ member projection for this group id; the
    // plan's U10 line 152 pin: "GlobalAdmin reach (ADR 0003) is enforced by
    // M1's AddGroupMemberAsync/RemoveGroupMemberAsync — the controller
    // passes the actor's subjectId as addedBy/removedBy and does not
    // re-gate"). The helper resolves the (group, actor) pair once; it
    // returns a small value type (no `out` param on an async method).
    // A non-visible group ⇒ (null, _) and the action 404s (consistent
    // failure shape on both routes; no re-gate in either route).
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
    /// Add a member (M2 plan U10, line 152):
    /// <c>POST /groups/{id}/add-member</c>. The actor is the caller
    /// (<c>KumunitaPrincipal.SubjectId(User)</c>) — the form does not carry an
    /// owner id (a so-called "addedBy" field would be a Web-layer SoD hole;
    /// the plan's U10 line 152 pin: "the controller passes the actor's
    /// <c>subjectId</c> as <c>addedBy</c> and does not re-gate"). The Core
    /// seam <see cref="Kumunita.Core.UserInfo.IUserInfoService.AddGroupMemberAsync"
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

        var resolved = await TryResolveWriteSurface(id);
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
    /// <c>POST /groups/{id}/remove-member</c>. Same SoD gate as
    /// <see cref="AddMember"/> (the shared <see cref="TryResolveWriteSurface"/>
    /// helper). The actor is the caller — the form does not carry an owner id
    /// (the <c>removedBy</c> field the M1 seam takes is always the
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

        var resolved = await TryResolveWriteSurface(id);
        if (resolved is null)
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
}
