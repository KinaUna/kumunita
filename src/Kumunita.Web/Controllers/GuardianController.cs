using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <b>guardian controls</b> surface (GU, ADR 0028) — a <b>parent</b>'s
/// account-level supervision of a <b>child</b> they created: list the children,
/// suspend / un-suspend, curate group + community memberships, approve a group
/// invitation sent to a child, dissolve the link (independence), and add a child
/// account (the usual confirm-email lane + the guardian link, one commit).
/// <para>
/// <b>Thin controller (ADR 0006-D / the M2 ADR 0012 / ADR 0013 precedent):</b>
/// every write + standing decision is delegated to the Core seams (U04–U06).
/// This layer resolves the actor's <see cref="KumunitaPrincipal.SubjectId"/>(the
/// cookie principal, the single identity source — never a form field) and passes
/// it as the <c>guardianId</c>/<c>actorId</c> argument; it does <b>no</b> standing
/// logic itself. The <b>failure shape</b> is the existing lane's:
/// <see cref="UnauthorizedAccessException"/> from a Core seam → <c>404</c> (a
/// non-guardian learns nothing — the ADR 0008/0012 precedent);
/// <see cref="InvalidOperationException"/> → the page's error surface
/// (<c>TempData["error"]</c>), never a 500.
/// </para>
/// <para>
/// <b>G·1 in the Web (load-bearing):</b> no route reads or links to a child's
/// <em>content</em> — the reads are the <c>GuardianLink</c> row + the child's
/// membership rows + the child's pending group invitations (curation data,
/// ids/names only), never the child's posts or profile body.
/// </para>
/// <para>
/// <b>G·4 (one commit):</b> the add-a-child POST does
/// <see cref="IIdentityService.RegisterAsync"/> then
/// <see cref="IUserInfoService.CreateGuardianLinkAsync"/> in the <b>same
/// request</b> — a created-but-unlinked account can never exist. The
/// verification email is M1's usual lane (<c>RegisterAsync</c> stages the
/// <c>OutboxEmail</c>); this surface does not verify.
/// </para>
/// </summary>
[Authorize]
[Route("me/children")]
public sealed class GuardianController(IUserInfoService userInfo, IIdentityService identity, IDocumentStore store) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// The guardian's <b>own</b> active child list (G·2 — the active
    /// <see cref="GuardianLink"/> rows where <c>GuardianId == SubjectId</c>, joined
    /// to each child's <see cref="Kumunita.Core.UserInfo.Profile.DisplayName"/> +
    /// <c>Blocked</c> flag). A read, not a decision: the list's shape is the
    /// standing itself (a non-guardian sees an empty list — the same as the
    /// ADR 0013 owner ∪ member projection, a non-member sees nothing).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject))
            return Unauthorized();

        // The pinned <see cref="ChildAccountItem"/> list (the 4-VM pin — no
        // container record; U08's Index view binds this projection directly).
        var rows = await ActiveChildrenAsync(subject);

        return View(rows);
    }

    /// <summary>
    /// One <b>child</b>'s curation view: the child's <b>group</b> memberships,
    /// <b>community</b> memberships, and <b>pending</b> group invitations — ids/names
    /// only (G·1: no posts, no profile body). The actor must have an <b>active</b>
    /// link over this child (a non-guardian → 404; the ADR 0012/0013 "a non-guardian
    /// learns nothing" shape).
    /// </summary>
    [HttpGet("{childId}")]
    public async Task<IActionResult> Detail(string childId)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId))
            return NotFound();

        // Standing gate: the actor must hold an active link over this child. A
        // non-guardian (or a dissolved link) sees a 404 — they learn nothing.
        var link = await ActiveLinkAsync(subject, childId);
        if (link is null)
            return NotFound();

        var groupIds = (await userInfo.GetGroupIdsAsync(childId)).ToHashSet(StringComparer.Ordinal);
        var communityIds = (await userInfo.GetCommunityIdsAsync(childId)).ToList();
        var pending = await userInfo.GetPendingInvitationsForUserAsync(childId);

        var invitations = new List<PendingInvitationItem>(pending.Count);
        foreach (var inv in pending)
        {
            var group = await userInfo.GetGroupAsync(inv.GroupId);
            invitations.Add(new PendingInvitationItem(
                inv.GroupId,
                group?.Name ?? inv.GroupId,
                inv.InvitedAt.ToString("O")));
        }

        return View(new MembershipEditorModel(
            childId,
            groupIds.OrderBy(g => g, StringComparer.OrdinalIgnoreCase).ToList(),
            communityIds.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList(),
            invitations));
    }

    /// <summary>Suspend a child (sets <c>Profile.Blocked</c>; the existing
    /// <c>BlockedAccountMiddleware</c> + directory already read the flag —
    /// enforcement parity). Core throws
    /// <see cref="UnauthorizedAccessException"/> (no active link → 404) or
    /// <see cref="InvalidOperationException"/> (missing profile → error).</summary>
    [HttpPost("{childId}/suspend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(string childId)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId))
            return NotFound();

        try
        {
            await userInfo.SuspendChildAsync(childId, subject);
            TempData["info"] = "Child account suspended.";
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { childId });
    }

    /// <summary>Un-suspend a child (restores <c>Profile.Blocked</c> to false).
    /// Same standing + failure shape as <see cref="Suspend"/>.</summary>
    [HttpPost("{childId}/unsuspend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unsuspend(string childId)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId))
            return NotFound();

        try
        {
            await userInfo.UnsuspendChildAsync(childId, subject);
            TempData["info"] = "Child account un-suspended.";
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { childId });
    }

    /// <summary>
    /// Curate a child's <b>group</b> membership (add or remove). The U05
    /// guardian branch records <c>Via: Guardian</c> in Core — the controller
    /// just passes the actor (never re-gates). A non-guardian → 404.
    /// </summary>
    [HttpPost("{childId}/memberships/group")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CurateGroup(string childId, [FromForm] string groupId, [FromForm] bool add)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(groupId))
            return NotFound();

        try
        {
            if (add)
                await userInfo.AddGroupMemberAsync(groupId, childId, subject);
            else
                await userInfo.RemoveGroupMemberAsync(groupId, childId, subject);
            TempData["info"] = add
                ? $"Added to group {groupId}."
                : $"Removed from group {groupId}.";
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { childId });
    }

    /// <summary>Curate a child's <b>community</b> membership (add or remove). The
    /// U05 guardian branch bypasses the GlobalAdmin/moderator gate (a guardian holds
    /// neither) and records <c>Via: Guardian</c> — the controller passes the actor
    /// + their role set (the community lanes' signature carries
    /// <c>actorRoles</c>). A non-guardian → 404.</summary>
    [HttpPost("{childId}/memberships/community")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CurateCommunity(string childId, [FromForm] string communityId, [FromForm] bool add)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(communityId))
            return NotFound();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        try
        {
            if (add)
                await userInfo.AddCommunityMemberAsync(communityId, childId, subject, actorRoles);
            else
                await userInfo.RemoveCommunityMemberAsync(communityId, childId, subject, actorRoles);
            TempData["info"] = add
                ? $"Added to community {communityId}."
                : $"Removed from community {communityId}.";
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { childId });
    }

    /// <summary>
    /// Approve a <b>child's</b> pending group invitation (U06's
    /// <see cref="IUserInfoService.ApproveGroupInvitationAsync"/>): resolves the
    /// child's <c>Pending</c> row as <c>Accepted</c> (the membership write +
    /// <c>ResolvedBy = guardianId</c>), audit <c>group.invite.approve</c>
    /// <c>Via: Guardian</c>. A non-guardian → 404.
    /// </summary>
    [HttpPost("{childId}/invitations/{groupId}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveInvitation(string childId, string groupId)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(groupId))
            return NotFound();

        try
        {
            await userInfo.ApproveGroupInvitationAsync(groupId, childId, subject);
            TempData["info"] = $"Approved the invitation to group {groupId}.";
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { childId });
    }

    /// <summary>
    /// <b>Dissolve</b> the (guardian, child) link — the independence lane (the
    /// child's self-lanes restore on the next read, G·2/C4). This is the
    /// guardian's <b>own</b> path (<c>viaAdmin: false</c>); the GlobalAdmin
    /// <c>viaAdmin: true</c> shell is a different surface, out of scope here. The
    /// <see cref="GuardianLink.Id"/> is resolved from the active (guardian, child)
    /// row first (the <c>GuardianLink</c> business key is the pair).
    /// </summary>
    [HttpPost("{childId}/dissolve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dissolve(string childId)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId))
            return NotFound();

        var link = await ActiveLinkAsync(subject, childId);
        if (link is null)
            return NotFound();

        try
        {
            await userInfo.DissolveGuardianLinkAsync(link.Id, subject, viaAdmin: false);
            TempData["info"] = "Guardianship dissolved — the account is now the child's own.";
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <b>Add a child account</b> (POST <c>me/children</c>) — the GU formation lane
    /// (G·4, one commit): <see cref="IIdentityService.RegisterAsync"/> (the usual M1
    /// signup — unverified account + profile + the verification email staged on the
    /// durable outbox) then
    /// <see cref="IUserInfoService.CreateGuardianLinkAsync"/> in the <b>same
    /// request</b>. A created-but-unlinked account can never exist. The child's
    /// <see cref="Kumunita.Core.Identity.ThinPrincipal.SubjectId"/> is the
    /// <c>childId</c>; the actor's <c>SubjectId</c> is the <c>guardianId</c>. The
    /// add-a-child form is rendered inline on the <see cref="Index"/> page (U08);
    /// this POST is its one-commit write.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddChild(AddChildForm form)
    {
        if (!ModelState.IsValid)
            return View(form);

        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to add a child account.");
            return View(form);
        }

        var displayName = (form.DisplayName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            ModelState.AddModelError(nameof(AddChildForm.DisplayName), "A display name is required.");
            return View(form);
        }

        try
        {
            // (1) the usual M1 signup — unverified account + profile + the
            //     verification email staged on the durable outbox (M1's lane; this
            //     surface does not verify). Returns the thin principal.
            var child = await identity.RegisterAsync(displayName, form.Email!, form.Password!);

            // (2) the GU formation seam — the (guardian, child) Active row, in the
            //     same request (G·4, one commit; C3).
            await userInfo.CreateGuardianLinkAsync(child.SubjectId, subject);
        }
        catch (InvalidOperationException ex)
        {
            // A signup-lane failure (e.g. the email is already taken) or a formation
            // seam failure — both are user-presentable, never a 500.
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(form);
        }
        catch (UnauthorizedAccessException)
        {
            // Should not be reachable here (no standing gate on formation) — fail
            // closed anyway.
            return NotFound();
        }

        TempData["info"] = "Child account created. They'll need to verify their email to sign in.";
        return RedirectToAction(nameof(Index));
    }

    // ── Read helpers (the GuardianLink standing read + the per-child
    //    display-name join) — reads only, no standing logic, no writes. ────────

    /// <summary>The actor's <b>active</b> <see cref="GuardianLink"/> rows (a read,
    /// not a decision), joined to each child's display name + <c>Blocked</c> flag
    /// (ids/names only — G·1).</summary>
    private async Task<IReadOnlyList<ChildAccountItem>> ActiveChildrenAsync(string guardianId)
    {
        await using var session = store.QuerySession();
        var links = await session
            .Query<GuardianLink>()
            .Where(l => l.GuardianId == guardianId && l.Status == GuardianLinkStatus.Active)
            .ToListAsync(System.Threading.CancellationToken.None);

        var rows = new List<ChildAccountItem>(links.Count);
        foreach (var link in links)
        {
            var profile = await userInfo.GetProfileAsync(link.ChildId);
            rows.Add(new ChildAccountItem(
                link.ChildId,
                profile?.DisplayName ?? link.ChildId,
                profile?.Blocked ?? false));
        }

        return rows;
    }

    /// <summary>
    /// The active <see cref="GuardianLink"/> for a (guardian, child) pair, or
    /// null — the standing read the <see cref="Detail"/> / <see cref="Dissolve"/>
    /// routes gate on (a non-guardian / dissolved link → 404).
    /// </summary>
    private async Task<GuardianLink?> ActiveLinkAsync(string guardianId, string childId)
    {
        await using var session = store.QuerySession();
        return await session
            .Query<GuardianLink>()
            .Where(l => l.GuardianId == guardianId && l.ChildId == childId && l.Status == GuardianLinkStatus.Active)
            .FirstOrDefaultAsync(System.Threading.CancellationToken.None);
    }
}
