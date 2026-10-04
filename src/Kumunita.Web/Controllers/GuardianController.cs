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

        var guardianItems = await ActiveGuardiansAsync(childId);

        // M9 amendment — the guardian's messaging-restriction ceiling
        // (the child's Profile.MessagingRestricted flag, read through the
        // existing GetProfileAsync; null-safe — a missing profile degrades
        // to "not restricted", the floor). Exposed on ViewData (the
        // MembershipEditorModel is a pinned 5-field record — the U07
        // GuardianViewModelsTests.MembershipEditorModel_Is_Exact_Five_Field_Projection
        // pin forbids adding a field; the repo's _AudienceEditor /
        // SeedGrantPickerOptionsAsync precedent for non-model view data).
        var childProfile = await userInfo.GetProfileAsync(childId);
        ViewData["MessagingRestricted"] = childProfile?.MessagingRestricted ?? false;
        ViewData["ChildMessagingOptIn"] = childProfile?.MessagingOptIn ?? false;

        // The child's per-community block set (the guardian's block-and-hide
        // ceiling, Profile.BlockedCommunityIds) — exposed on ViewData, the
        // MessagingRestricted precedent (the MembershipEditorModel is a pinned
        // 5-field record; the U07 pin forbids adding a field). The Detail view
        // renders a block/unblock toggle per community against this set. The
        // model's CommunityIds stays the RAW membership read (so a mandatory
        // community still appears here for the guardian to block) — only the
        // child's OWN access surfaces read through GetEffectiveCommunityIdsAsync.
        ViewData["BlockedCommunityIds"] = childProfile?.BlockedCommunityIds ?? [];

        // The Detail header's child identity (name + email) — the same
        // profile read; the MembershipEditorModel is a pinned 5-field
        // record (the U07 GuardianViewModelsTests pin forbids adding a
        // field), so the view data goes on ViewData (the M9
        // MessagingRestricted / _AudienceEditor precedent). Null-safe —
        // a missing profile degrades to the child id (the view's
        // fallback).
        ViewData["ChildDisplayName"] = string.IsNullOrWhiteSpace(childProfile?.DisplayName) ? null : childProfile!.DisplayName;
        ViewData["ChildEmail"] = string.IsNullOrWhiteSpace(childProfile?.Email) ? null : childProfile!.Email;

        return View(new MembershipEditorModel(
            childId,
            groupIds.OrderBy(g => g, StringComparer.OrdinalIgnoreCase).ToList(),
            communityIds.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList(),
            invitations,
            guardianItems));
    }

    /// <summary>
    /// <b>Restrict / allow messaging</b> over a supervised child (POST
    /// <c>me/children/{childId}/messaging</c>) — the M9 amendment
    /// guardian-side control (the ADR 0028 G·2/G·3 guardian-standing shape,
    /// the <see cref="Suspend"/> / <see cref="Unsuspend"/> idiom
    /// verbatim): writes <c>Profile.MessagingRestricted</c> through the
    /// frozen <see cref="IUserInfoService.SetChildMessagingRestrictionAsync"/>
    /// seam. <c>true</c> = force messaging OFF for the child (a hard
    /// ceiling that wins over the child's own opt-in); <c>false</c> = allow
    /// messaging, deferring to the child's own opt-in.
    /// <para>
    /// <b>Standing gate:</b> the actor must hold an <b>active</b>
    /// <see cref="GuardianLink"/> over this child (the same
    /// <see cref="ActiveLinkAsync"/> standing the <see cref="Detail"/>
    /// GET / <see cref="Suspend"/> POST already use). A non-guardian (or a
    /// dissolved link) is a <c>404</c> — the ADR 0028 deny-by-default
    /// shape. A missing profile is a user-presentable
    /// <c>TempData["error"]</c>, never a 500.
    /// </para>
    /// </summary>
    [HttpPost("{childId}/messaging")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetChildMessaging(string childId, bool restricted)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId))
            return NotFound();

        // Standing gate first (the Detail GET's ActiveLinkAsync check — a
        // non-guardian / dissolved link is a 404, the ADR 0028 shape).
        var link = await ActiveLinkAsync(subject, childId);
        if (link is null)
            return NotFound();

        try
        {
            await userInfo.SetChildMessagingRestrictionAsync(childId, restricted, subject);
            TempData["info"] = restricted
                ? "Messaging restricted for this child account."
                : "Messaging allowed for this child account.";
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

    /// <summary>
    /// <b>Block / unblock a community</b> for a supervised child (POST
    /// <c>me/children/{childId}/communities/{communityId}</c>) — the guardian's
    /// block-and-hide control, replacing the old "remove the child from a
    /// community" lane (which was a silent no-op for a <b>mandatory</b>
    /// community — ADR 0012's membership is implicit and the removal lanes
    /// refuse / skip it). The <see cref="IUserInfoService
    /// .SetChildCommunityBlockAsync"/> seam flips the child's
    /// <c>Profile.BlockedCommunityIds</c> entry, which the child's own access
    /// surfaces (directory, feed, posting gate, audience visibility) read
    /// through <c>GetEffectiveCommunityIdsAsync</c> and exclude — a ceiling that
    /// works even for a mandatory community, whose membership cannot be removed.
    /// <para>
    /// <b>Standing gate:</b> the actor must hold an <b>active</b>
    /// <see cref="GuardianLink"/> over this child (the same
    /// <see cref="ActiveLinkAsync"/> standing the Detail GET uses — Core re-gates
    /// it too). A non-guardian (or a dissolved link) is a <c>404</c> — the
    /// ADR 0028 deny-by-default shape. A missing community or profile is a
    /// user-presentable <c>TempData["error"]</c>, never a 500.
    /// </para>
    /// </summary>
    [HttpPost("{childId}/communities/{communityId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetChildCommunityBlock(string childId, string communityId, bool blocked)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(communityId))
            return NotFound();

        // Standing gate first (the Detail GET's ActiveLinkAsync check — a
        // non-guardian / dissolved link is a 404, the ADR 0028 shape).
        var link = await ActiveLinkAsync(subject, childId);
        if (link is null)
            return NotFound();

        try
        {
            await userInfo.SetChildCommunityBlockAsync(childId, communityId, blocked, subject);
            TempData["info"] = blocked
                ? $"Community {communityId} is now blocked and hidden for this child."
                : $"Access to community {communityId} is restored for this child.";
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

        // The guardian consent checkbox: [Required] can't reject a non-nullable
        // bool of false, so the guard is explicit — creation is refused until
        // the guardian confirms the child-account terms.
        if (!form.GuardianConsent)
        {
            ModelState.AddModelError(nameof(AddChildForm.GuardianConsent),
                "You must consent to the child-account terms before adding the account.");
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

    /// <summary>
    /// GA (ADR 0038): assign a second guardian to this child. The
    /// standing gate (G-A·1 — the <c>ActiveLinkAsync</c> helper the
    /// <c>Dissolve</c> route already uses) runs first; a non-guardian
    /// → 404. The resolution (G-A·2 — the
    /// <c>FindSubjectByEmailAsync</c> seam) runs second; null → the
    /// form's error surface ("No account with that email.").
    /// Self-assignment (G-A·5) + duplicate-assignment (G-A·4) are the
    /// third step (both <see cref="InvalidOperationException"/> →
    /// the form's error surface). The
    /// <see cref="IUserInfoService.AssignGuardianLinkAsync"/> call (GA-AR,
    /// ADR 0038 §Amendment (2026-09-17, second)) is the fourth — one
    /// commit, <b>two</b> audit rows (C3): <c>guardian.create</c>
    /// (<c>ActorId</c> = the assigned guardian, the GU seam's shape) +
    /// <c>guardian.assign</c> (<c>ActorId</c> = the assigning guardian /
    /// conferrer).
    /// </summary>
    [HttpPost("{childId}/assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(string childId, [FromForm] AssignGuardianForm form)
    {
        if (!ModelState.IsValid)
            return View("Detail", new AssignGuardianForm { Email = form.Email });

        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId))
            return NotFound();

        // G-A·1 — the standing gate: the actor must hold an active link
        // over this child (the ActiveLinkAsync helper the Dissolve route
        // already uses). A non-guardian → 404 (the ADR 0012/0013 "a
        // non-guardian learns nothing" shape).
        var link = await ActiveLinkAsync(subject, childId);
        if (link is null)
            return NotFound();

        var email = (form.Email ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError(nameof(AssignGuardianForm.Email), "An email is required.");
            return View("Detail", new AssignGuardianForm { Email = email });
        }

        // G-A·2 — the resolution: the email → subject id (the
        // FindSubjectByEmailAsync seam). Null → the form's error
        // surface (a user-presentable error, never a 500, never an
        // auto-create — the GU lane's G·4 "formation is creation-based"
        // precedent).
        var assignedId = await identity.FindSubjectByEmailAsync(email);
        if (assignedId is null)
        {
            ModelState.AddModelError(string.Empty, "No account with that email.");
            return View("Detail", new AssignGuardianForm { Email = email });
        }

        // G-A·5 — self-assignment is refused (the (actorId, childId)
        // pair is the same as the (assignedId, childId) pair — the GU
        // formation lane's territory, and CreateGuardianLinkAsync is
        // already idempotent for it — a no-op, not a useful act).
        if (assignedId == subject)
        {
            ModelState.AddModelError(string.Empty, "You are already this child's guardian.");
            return View("Detail", new AssignGuardianForm { Email = email });
        }

        try
        {
            // G-A·4 — the AssignGuardianLinkAsync seam is idempotent for
            // the (guardianId, childId) pair (the GU lane's G·4
            // precedent, inherited): a duplicate active row is a no-op —
            // the row is left as-is, no second pair of audit rows. The
            // happy path is one commit, two audit rows (C3 — the
            // guardian.create row [ActorId = the assigned guardian, S·5]
            // + the guardian.assign row [ActorId = the assigning
            // guardian / conferrer, S·5]). GA-AR (ADR 0038 amendment).
            await userInfo.AssignGuardianLinkAsync(childId, assignedId, subject);
            TempData["info"] = $"Guardian assigned.";
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Detail", new AssignGuardianForm { Email = email });
        }

        return RedirectToAction(nameof(Detail), new { childId });
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
    /// GA (ADR 0038) — the child's <b>active</b>
    /// <see cref="GuardianLink"/> rows (a read, not a decision), joined
    /// to each guardian's display name (ids/names only — G-A·3). The
    /// <c>ActiveChildrenAsync</c> helper inverted: the child's active
    /// guardian rows, not the guardian's child rows.
    /// </summary>
    private async Task<IReadOnlyList<GuardianItem>> ActiveGuardiansAsync(string childId)
    {
        await using var session = store.QuerySession();
        var links = await session
            .Query<GuardianLink>()
            .Where(l => l.ChildId == childId && l.Status == GuardianLinkStatus.Active)
            .ToListAsync(System.Threading.CancellationToken.None);

        var rows = new List<GuardianItem>(links.Count);
        foreach (var link in links)
        {
            var profile = await userInfo.GetProfileAsync(link.GuardianId);
            rows.Add(new GuardianItem(
                link.GuardianId,
                profile?.DisplayName ?? link.GuardianId));
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
