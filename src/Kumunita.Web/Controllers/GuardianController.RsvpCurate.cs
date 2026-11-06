using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/guardian</c> RSVP + group-curation lane — the five actions that
/// delegate to <see cref="IEventRsvpGuardian"/> (ADR 0028 account-scope
/// supervision) and <see cref="IGuardianService"/> group-invitation /
/// community-membership requests. A partial class of
/// <see cref="GuardianController"/> extracted so the main file stays under
/// the 600 <c>cs_file</c> ceiling (C3-04). The shared helpers
/// (<c>SubjectId</c>, <c>RoleSet</c>, <c>guardian</c>, <c>rsvpGuardian</c>)
/// are resolved through the same partial-class scope.
/// </summary>
public partial class GuardianController
{
    /// <summary>
    /// <b>Approve</b> a child's event-attendance request (POST
    /// <c>me/children/{childId}/eventrsvp/{eventId}/approve</c>) — the
    /// <see cref="Kumunita.Core.UserInfo.EventRsvpMode.GuardianApproves"/>
    /// posture's resolve lane (the <see cref="ApproveInvitation"/> /
    /// <see cref="RejectInvitation"/> idiom verbatim, over events): writes the
    /// child's <c>EventRsvp</c> with the request's desired status through the
    /// frozen <see cref="Kumunita.Core.Events.IEventService
    /// .ApproveEventRsvpAsync"/> seam. <c>UnauthorizedAccessException</c>
    /// (no active link) → 404; <c>KeyNotFoundException</c> (no request) → 404;
    /// <c>InvalidOperationException</c> (already resolved) → the page's error
    /// surface.
    /// </summary>
    [HttpPost("{childId}/eventrsvp/{eventId}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveEventRsvp(string childId, string eventId)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(eventId) || _rsvpGuardian is null)
            return NotFound();

        // The standing gate lives in the Core seam (the GU G·2/G·3 deny-by-
        // default — the <see cref="ApproveInvitation"/> precedent: the
        // controller does not re-check standing; <see cref
        // "UnauthorizedAccessException"/> from the seam is the 404).
        try
        {
            await _rsvpGuardian!.ApproveEventRsvpAsync(eventId, childId, subject);
            TempData["info"] = await this.FlashAsync("guardian.flash_rsvp_approved", "Attendance approved — the child’s RSVP has been recorded.");
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
        catch (KeyNotFoundException)
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
    /// <b>Deny</b> a child's event-attendance request (POST
    /// <c>me/children/{childId}/eventrsvp/{eventId}/deny</c>) — the
    /// <see cref="Kumunita.Core.UserInfo.EventRsvpMode.GuardianApproves"/>
    /// posture's resolve lane: the request row moves Pending → Denied, <b>no</b>
    /// <c>EventRsvp</c> row is written. The same standing / failure shape as
    /// <see cref="ApproveEventRsvp"/>.
    /// </summary>
    [HttpPost("{childId}/eventrsvp/{eventId}/deny")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DenyEventRsvp(string childId, string eventId)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(eventId) || _rsvpGuardian is null)
            return NotFound();

        try
        {
            await _rsvpGuardian!.DenyEventRsvpAsync(eventId, childId, subject);
            TempData["info"] = await this.FlashAsync("guardian.flash_rsvp_denied", "Attendance denied — the child’s request was not approved.");
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
        catch (KeyNotFoundException)
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
    /// <b>Veto</b> (remove) a child's existing event attendance (POST
    /// <c>me/children/{childId}/eventrsvp/{eventId}/veto</c>) — the
    /// <see cref="Kumunita.Core.UserInfo.EventRsvpMode.GuardianNotifies"/>
    /// posture's window to undo an auto-approved attendance: deletes the
    /// child's <c>EventRsvp</c> row through the frozen <see
    /// cref="Kumunita.Core.Events.IEventService.VetoEventRsvpAsync"/> seam.
    /// The same standing / failure shape as <see cref="ApproveEventRsvp"/>.
    /// </summary>
    [HttpPost("{childId}/eventrsvp/{eventId}/veto")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VetoEventRsvp(string childId, string eventId)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(eventId) || _rsvpGuardian is null)
            return NotFound();

        try
        {
            await _rsvpGuardian!.VetoEventRsvpAsync(eventId, childId, subject);
            TempData["info"] = await this.FlashAsync("guardian.flash_rsvp_vetoed", "Attendance vetoed — the child’s RSVP has been removed.");
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
        catch (KeyNotFoundException)
        {
            TempData["error"] = await this.FlashAsync("guardian.flash_rsvp_none_to_veto", "This child has no attendance to veto on that event.");
            return RedirectToAction(nameof(Detail), new { childId });
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
            await guardian.SuspendChildAsync(childId, subject);
            TempData["info"] = await this.FlashAsync("guardian.flash_suspended", "Child account suspended.");
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
            await guardian.UnsuspendChildAsync(childId, subject);
            TempData["info"] = await this.FlashAsync("guardian.flash_unsuspended", "Child account un-suspended.");
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
    /// M28 (ADR 0151 D6) — <b>Save / clear the child's time-limit schedule</b>
    /// (POST <c>me/children/{childId}/timelimits</c>). The
    /// <see cref="LocaleController.SaveQuiet"/> shape (the <c>enabled</c> /
    /// <c>mode</c> / <c>hours</c> / <c>daysOfWeek</c> / <c>clear</c> form
    /// fields), the GU inverse: writes the
    /// <see cref="GuardianTimeLimitSchedule"/> through the
    /// <see cref="IUserInfoService.SetChildTimeLimitAsync"/> seam (one audit
    /// row, <c>guardian.time-limit.set</c>). <c>clear=1</c> → null → row
    /// delete (the M20 "clear" idiom, C-M28·3 floor). Flash:
    /// <c>guardian.timelimit.flash_saved</c> / <c>_cleared</c> (the
    /// <see cref="Kumunita.Core.Localization.KnownTranslationKeys.EnValues"/>
    /// floor — the same pattern as the other GU actions' hardcoded English
    /// strings).
    /// <para>
    /// <b>Standing gate:</b> the Core seam's own
    /// <see cref="IUserInfoService.SetChildTimeLimitAsync"/> gate (active
    /// <see cref="GuardianLink"/> ∪ GlobalAdmin, C-M28·4) — the Web layer
    /// does not re-gate (the <see cref="Suspend"/> / <see cref="Unsuspend"/>
    /// precedent: the catch of <see cref="UnauthorizedAccessException"/> is
    /// the 404).
    /// </para>
    /// </summary>
    [HttpPost("{childId}/timelimits")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTimeLimits(
        string childId,
        bool enabled,
        string? mode,
        int[]? hours,
        int[]? daysOfWeek,
        string? clear)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId))
            return NotFound();

        try
        {
            if (clear == "1")
            {
                // Clear = never restricted (the floor, C-M28·3): delete the
                // row (the M20 SetQuietScheduleAsync "clear" idiom — the
                // null-schedule = delete shape in the Core seam).
                await guardian.SetChildTimeLimitAsync(subject, childId, null);
                TempData["info"] = await this.FlashAsync("guardian.timelimit.flash_cleared", "Time limits cleared");
            }
            else
            {
                var schedule = new GuardianTimeLimitSchedule
                {
                    ChildId   = childId,
                    Enabled   = enabled,
                    Mode      = mode == "allowed" ? TimeLimitMode.Allowed : TimeLimitMode.Blocked,
                    Hours     = hours ?? [],
                    DaysOfWeek = daysOfWeek ?? [],
                };
                await guardian.SetChildTimeLimitAsync(subject, childId, schedule);
                TempData["info"] = await this.FlashAsync("guardian.timelimit.flash_saved", "Time limits saved");
            }
        }
        catch (UnauthorizedAccessException)
        {
            // The Core seam's own gate (C-M28·4, deny-by-default): non-guardian
            // or dissolved link → 404 (the Suspend / Unsuspend precedent).
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
                ? await this.FlashAsync("guardian.flash_group_added", "Added to group {0}.", groupId)
                : await this.FlashAsync("guardian.flash_group_removed", "Removed from group {0}.", groupId);
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
            await guardian.SetChildCommunityBlockAsync(childId, communityId, blocked, subject);
            TempData["info"] = blocked
                ? await this.FlashAsync("guardian.flash_community_blocked", "Community {0} is now blocked and hidden for this child.", communityId)
                : await this.FlashAsync("guardian.flash_community_restored", "Access to community {0} is restored for this child.", communityId);
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
            await guardian.ApproveGroupInvitationAsync(groupId, childId, subject);
            TempData["info"] = await this.FlashAsync("guardian.flash_invite_approved", "Approved the invitation to group {0}.", groupId);
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
    /// Reject (decline) a <b>child's</b> pending group invitation (the GU
    /// extension of U06's <see cref="IUserInfoService
    /// .ApproveGroupInvitationAsync"/>): resolves the child's <c>Pending</c>
    /// row as <c>Declined</c> (no <c>GroupMembership</c> row lands) with
    /// <c>ResolvedBy = guardianId</c>, audit <c>group.invite.reject</c>
    /// <c>Via: Guardian</c>. A non-guardian → 404 (the ADR 0028 G·3
    /// deny-by-default shape).
    /// </summary>
    [HttpPost("{childId}/invitations/{groupId}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectInvitation(string childId, string groupId)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(groupId))
            return NotFound();

        try
        {
            await guardian.RejectGroupInvitationAsync(groupId, childId, subject);
            TempData["info"] = await this.FlashAsync("guardian.flash_invite_rejected", "Rejected the invitation to group {0}.", groupId);
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
    /// Approve a <b>child's</b> pending <b>community</b> membership request
    /// (the GU community-approval lane — the sibling of
    /// <see cref="ApproveInvitation"/>'s group lane, but the
    /// <c>ComponentMembership</c> row lands on approval): resolves the
    /// child's <c>Pending</c> <c>CommunityMembershipRequest</c> row as
    /// <c>Approved</c> (the membership write + <c>ResolvedBy = guardianId</c>),
    /// audit <c>community.membership.approve</c> <c>Via: Guardian</c>. A
    /// non-guardian → 404.
    /// </summary>
    [HttpPost("{childId}/communities/{communityId}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCommunityMembership(string childId, string communityId)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(communityId))
            return NotFound();

        try
        {
            await guardian.ApproveCommunityMembershipRequestAsync(communityId, childId, subject);
            TempData["info"] = await this.FlashAsync("guardian.flash_community_approved", "Approved community {0} for this child.", communityId);
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
    /// Reject a <b>child's</b> pending <b>community</b> membership request
    /// (the GU community-approval lane's decline lane — no
    /// <c>ComponentMembership</c> row lands): resolves the child's
    /// <c>Pending</c> <c>CommunityMembershipRequest</c> row as
    /// <c>Declined</c> (<c>ResolvedBy = guardianId</c>), audit
    /// <c>community.membership.decline</c> <c>Via: Guardian</c>. A
    /// non-guardian → 404.
    /// </summary>
    [HttpPost("{childId}/communities/{communityId}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectCommunityMembership(string childId, string communityId)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(communityId))
            return NotFound();

        try
        {
            await guardian.DeclineCommunityMembershipRequestAsync(communityId, childId, subject);
            TempData["info"] = await this.FlashAsync("guardian.flash_community_rejected", "Rejected community {0} for this child.", communityId);
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
}
