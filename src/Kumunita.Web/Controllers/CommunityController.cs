// ── /community/manage/{id} — the moderator's community-membership lane (ADR 0012)
//
// Mirrors the PostsController surface conventions: [Authorize] primary ctor,
// full-route templates, explicit CTS, and the same fail-closed standing rule as
// the posting gate (GlobalAdmin, or the community-scoped moderator claim —
// never the bare "Moderator" role — or no standing at all). Standing the actor
// does NOT have renders/behaves as **404** (ADR 0008 "U10" shape): the community
// itself stays visible in the feed to reachable viewers; only membership
// *management* is scoped.
namespace Kumunita.Web.Controllers;

using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
public sealed class CommunityController : Controller
{
    private readonly IUserInfoService userInfo;

    public CommunityController(IUserInfoService userInfo)
    {
        this.userInfo = userInfo;
    }

    // ── Standing (fail-closed, mirrors the posting gate) ──────────────────

    private bool Standing(string? actorId, string componentId)
        => actorId is not null
            && (KumunitaPrincipal.IsGlobalAdmin(User)
                || KumunitaPrincipal.HasRole(User, Roles.ModeratorComponent(componentId)));

    // ── Views ───────────────────────────────────────────────────────────────

    // GET /community/manage/{componentId} — member list + add-picker + mandatory
    // state. A component the actor can't manage is indistinguishable from a
    // missing one (404, no body) — same shape as the group leave-lane (ADR 0008).
    // componentId is an unconstrained route value — component ids are
    // slugs (e.g. "social"), not GUIDs (same rule as /community/{componentId}).
    [HttpGet("/community/manage/{componentId}")]
    public async Task<IActionResult> Manage(string componentId, CancellationToken ct = default)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        var component = (await userInfo.GetComponentsAsync(enabledOnly: false))
            .FirstOrDefault(c => c.Id.Equals(componentId, StringComparison.OrdinalIgnoreCase));
        if (component is null || !Standing(actorId, componentId))
            return NotFound();

        var memberships = await userInfo.GetCommunityMembersAsync(componentId);
        var profiles = await userInfo.GetProfilesAsync(verifiedOnly: false);
        var nameBySubject = profiles
            .ToDictionary(p => p.SubjectId, p => p.DisplayName, StringComparer.Ordinal);
        bool TryName(string subjectId, out string? name)
            => nameBySubject.TryGetValue(subjectId, out name);

        var memberIds = memberships.Select(m => m.UserId).ToHashSet(StringComparer.Ordinal);
        var members = memberships
            .Select(m => new ManageCommunityViewModel.MemberRow(m.UserId, TryName(m.UserId, out var n) ? n : null))
            .OrderBy(r => r.DisplayName ?? "", StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Add-picker candidates: every non-blocked profile that is not already a
        // member, minus the actor themself (self-add is a no-op membership-wise
        // but has no management use; keep the picker about *other* people).
        var candidates = profiles
            .Where(p => !p.Blocked && p.SubjectId != actorId && !memberIds.Contains(p.SubjectId))
            .Select(p => new ManageCommunityViewModel.MemberRow(p.SubjectId, p.DisplayName))
            .OrderBy(r => r.DisplayName ?? "", StringComparer.OrdinalIgnoreCase)
            .ToList();

        return View(new ManageCommunityViewModel
        {
            ComponentId = component.Id,
            Name = component.Name,
            Description = component.Description,
            Mandatory = component.Mandatory,
            Enabled = component.Enabled,
            ActorHasScopeClaim = KumunitaPrincipal.HasRole(User, Roles.ModeratorComponent(componentId)),
            Members = members,
            Candidates = candidates,
        });
    }

    // ── Forms (all redirect back to Manage with a TempData message) ────────

    // POST /community/manage/{componentId}/mandatory — the on/off switch the
    // requirement puts in the moderator's hands ("set by the moderator").
    [HttpPost("/community/manage/{componentId}/mandatory")]
    public async Task<IActionResult> SetMandatory(string componentId, [FromForm] bool Mandatory, CancellationToken ct = default)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (!Standing(actorId, componentId))
            return NotFound();

        try
        {
            // Standing() above implies actorId is non-null (fail-closed null check).
            await userInfo.SetCommunityMandatoryAsync(componentId, Mandatory, actorId!, KumunitaPrincipal.RoleSet(User));
            TempData["info"] = Mandatory
                ? "Marked mandatory — everyone in the neighborhood is a member."
                : "Marked optional.";
        }
        catch (InvalidOperationException)
        {
            TempData["error"] = "That community no longer exists.";
        }
        return RedirectToAction(nameof(Manage), new { componentId });
    }

    // POST /community/manage/{componentId}/add — moderator adds a member.
    [HttpPost("/community/manage/{componentId}/add")]
    public async Task<IActionResult> AddMember(string componentId, [FromForm] string UserId, CancellationToken ct = default)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (!Standing(actorId, componentId) || string.IsNullOrWhiteSpace(UserId))
            return NotFound();

        try
        {
            // Standing() above implies actorId is non-null (fail-closed null check).
            await userInfo.AddCommunityMemberAsync(componentId, UserId, actorId!, KumunitaPrincipal.RoleSet(User));
            TempData["info"] = "Member added.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Manage), new { componentId });
    }

    // POST /community/manage/{componentId}/remove — moderator removes a member.
    // layer when the community is mandatory (Core still re-checks and throws).
    [HttpPost("/community/manage/{componentId}/remove")]
    public async Task<IActionResult> RemoveMember(string componentId, [FromForm] string UserId, CancellationToken ct = default)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (!Standing(actorId, componentId) || string.IsNullOrWhiteSpace(UserId))
            return NotFound();

        if (UserId == actorId)
        {
            // Leaving as a member is the /leave route (audited as the member
            // acting, not a moderator removing themself).
            TempData["info"] = "To leave as a member, use the Leave button on the community feed.";
            return RedirectToAction(nameof(Manage), new { componentId });
        }

        try
        {
            // Standing() above implies actorId is non-null (fail-closed null check).
            await userInfo.RemoveCommunityMemberAsync(componentId, UserId, actorId!, KumunitaPrincipal.RoleSet(User));
            TempData["info"] = "Member removed.";
        }
        catch (InvalidOperationException ex)
        {
            // Mandatory community (Core) or the membership is gone.
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Manage), new { componentId });
    }

    // ── Leave (the self-lane — ADR 0008 shape, no new Core seam) ───────────

    // POST /community/{componentId}/leave — a member removes themself. The
    // service call is the frozen admin lane with the acting member as the
    // target user (userId == actorId) — Core refuses the write when the
    // community is mandatory (silent skip + no audit), so "members cannot be
    // removed from a mandatory community" holds for this lane as well.
    // /community/{componentId}/leave — the self-lane. Note this one *is*
    // path-nested under the component (like the group self-leave route), so
    // "leave" binds the component and the member is the authenticated actor
    // (no target anywhere) — unlike /community/manage/{componentId}.
    [HttpPost("/community/{componentId}/leave")]
    public async Task<IActionResult> Leave(string componentId, CancellationToken ct = default)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null)
            return Forbid();

        var component = (await userInfo.GetComponentsAsync(enabledOnly: false))
            .FirstOrDefault(c => c.Id.Equals(componentId, StringComparison.OrdinalIgnoreCase));
        if (component is null)
            return NotFound();
        if (component.Mandatory)
        {
            TempData["error"] = $"You're a member of {component.Name} because it's mandatory for everyone — you can't leave it.";
            return Redirect("/community");
        }

        await userInfo.ClearCommunityMembershipAsync(componentId, actorId, actorId);
        TempData["info"] = $"You've left {component.Name}.";
        return Redirect("/community");
    }
}
