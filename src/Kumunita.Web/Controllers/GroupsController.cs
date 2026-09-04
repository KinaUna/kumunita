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
}
