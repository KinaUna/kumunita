// ── /community/manage/{id} — the community-membership management lane (ADR 0012)
//
// Mirrors the PostsController surface conventions: [Authorize] primary ctor,
// full-route templates, explicit CTS, and fail-closed standing (never the
// bare "Moderator" role): member add/remove is the community-scoped
// moderator ∪ GlobalAdmin, while toggling the mandatory flag is
// **GlobalAdmin-only** (whether a community is mandatory is an admin call —
// the moderator governs its members, not its standing). Standing the actor
// does NOT have renders/behaves as **404** (ADR 0008 "U10" shape): the
// community itself stays visible in the feed to reachable viewers; only
// membership *management* is scoped.
namespace Kumunita.Web.Controllers;

using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
public sealed class CommunityController : Controller
{
    private readonly IUserInfoService userInfo;
    private readonly ILocalizationService localization;
    private readonly IDocumentStore store;

    public CommunityController(IUserInfoService userInfo, ILocalizationService localization, IDocumentStore store)
    {
        this.userInfo = userInfo;
        this.localization = localization;
        this.store = store;
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
    // The mandatory toggle is offered in the view only to GlobalAdmins; a
    // community moderator sees member management without it.
    [HttpGet("/community/manage/{componentId}")]
    public async Task<IActionResult> Manage(string componentId, CancellationToken ct = default)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        var component = (await userInfo.GetComponentsAsync(enabledOnly: false))
            .FirstOrDefault(c => c.Id.Equals(componentId, StringComparison.OrdinalIgnoreCase));
        if (component is null || !Standing(actorId, componentId))
            return NotFound();
        if (actorId is null)
            return NotFound(); // Standing above implies non-null; pin it for the CanTranslateCommunity call.

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

        // ── ADR 0026 — community name/description translations ─────────────
        // Rendered as a "a read, not a decision" surface (the same standing the
        // ADR 0022 post-detail surface uses for PostTranslation rows): the
        // manage page already required the community's enabled visibility +
        // the management standing (the gate above), so the translation rows
        // inherit that reach. The enabled catalog (ListLanguagesAsync, Enabled
        // + SortOrder — the same read the ADR 0022 post surface uses) seeds the
        // chips / "add a translation" candidate list. CanTranslate is the
        // display convenience mirroring UserInfoService's
        // AddCommunityTranslationAsync standing check (GlobalAdmin ∪ Translator
        // — no owner branch, a component-moderator is denied) — the POST
        // re-checks server-side, so this is not the gate.
        var communityTranslations = await userInfo.GetCommunityTranslationsAsync(component.Id);
        var translationCodes = communityTranslations.Select(t => t.LanguageCode).ToHashSet();
        var catalog = await localization.ListLanguagesAsync();
        var communityLanguages = catalog
            .Where(l => l.Enabled)
            .OrderBy(l => l.SortOrder)
            .Select(l => new LanguageOption(l.Id, l.NativeName, translationCodes.Contains(l.Id)))
            .ToList();
        var canTranslate = userInfo.CanTranslateCommunity(actorId, KumunitaPrincipal.RoleSet(User));

        return View(new ManageCommunityViewModel
        {
            ComponentId = component.Id,
            Name = component.Name,
            Description = component.Description,
            Mandatory = component.Mandatory,
            Enabled = component.Enabled,
            CanSetMandatory = KumunitaPrincipal.IsGlobalAdmin(User),
            ActorHasScopeClaim = KumunitaPrincipal.HasRole(User, Roles.ModeratorComponent(componentId)),
            Members = members,
            Candidates = candidates,
            CommunityTranslations = communityTranslations,
            Languages = communityLanguages,
            CanTranslate = canTranslate,
        });
    }

    // ── Forms (all redirect back to Manage with a TempData message) ────────

    // POST /community/manage/{componentId}/mandatory — the on/off switch the
    // requirement puts in the **admin's** hands (product decision: mandatory
    // state is an admin call; the community's moderator manages *members*,
    // not the flag). Moderators hitting this route get the same fail-closed
    // 404 as a community they can't manage.
    [HttpPost("/community/manage/{componentId}/mandatory")]
    public async Task<IActionResult> SetMandatory(string componentId, [FromForm] bool Mandatory, CancellationToken ct = default)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null || !KumunitaPrincipal.IsGlobalAdmin(User))
            return NotFound();

        try
        {
            await userInfo.SetCommunityMandatoryAsync(componentId, Mandatory, actorId, KumunitaPrincipal.RoleSet(User));
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

    // ── ADR 0026: community name/description translations ──────────────────

    // POST /community/manage/{componentId}/translations — a thin Web lane
    // (ADR 0006-D: routes + shape) delegating the write + standing decision to
    // UserInfoService.AddCommunityTranslationAsync (standing — GlobalAdmin ∪
    // Translator, no owner branch, component-moderator denied — is re-pinned
    // server-side; the manage page's CanTranslate is only the display
    // affordance). A denied standing actor is a 403 (UnauthorizedAccessException
    // → Forbid()); a missing community is a 404. At least one of
    // name/description must be non-blank (re-checked server-side by the seam).
    // Session shape (C3): the controller owns the LightweightSession; the
    // service's SaveChangesAsync is the single write — the CommunityTranslation
    // row and its AccessAudit row commit atomically.
    [HttpPost("/community/manage/{componentId}/translations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTranslation(string componentId, [FromForm] string? languageCode, [FromForm] string? name, [FromForm] string? description)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null)
            return Forbid();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return RedirectToAction(nameof(Manage), new { componentId });
        }
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(description))
        {
            TempData["error"] = "A translation needs a name and/or description.";
            return RedirectToAction(nameof(Manage), new { componentId });
        }

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await userInfo.AddCommunityTranslationAsync(
                componentId,
                languageCode,
                string.IsNullOrWhiteSpace(name) ? null : name,
                string.IsNullOrWhiteSpace(description) ? null : description,
                actorId,
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
        return RedirectToAction(nameof(Manage), new { componentId });
    }

    // ── ADR 0048 — edit + delete lanes for community translations ────────
    // ADR 0026 was add-only; ADR 0048 lifts the "add-only" pin on the same
    // standing matrix (GlobalAdmin ∪ Translator). Failure shapes mirror the
    // add lane (denied → 403; missing → 404).

    /// <summary>
    /// **Updates** the existing user-added translation of the community's
    /// name and/or description (ADR 0048):
    /// <c>POST /community/manage/{componentId}/translations/update</c>.
    /// Thin Web lane; delegates to
    /// <see cref="IUserInfoService.UpdateCommunityTranslationAsync"/>.
    /// Failure shapes mirror <see cref="AddTranslation"/>.
    /// </summary>
    [HttpPost("/community/manage/{componentId}/translations/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTranslation(
        string componentId, [FromForm] string? languageCode, [FromForm] string? name, [FromForm] string? description)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null)
            return Forbid();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return RedirectToAction(nameof(Manage), new { componentId });
        }
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(description))
        {
            TempData["error"] = "A translation needs a name and/or description.";
            return RedirectToAction(nameof(Manage), new { componentId });
        }

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await userInfo.UpdateCommunityTranslationAsync(
                componentId,
                languageCode,
                string.IsNullOrWhiteSpace(name) ? null : name,
                string.IsNullOrWhiteSpace(description) ? null : description,
                actorId,
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
        TempData["info"] = $"Translation updated ({langName}).";
        return RedirectToAction(nameof(Manage), new { componentId });
    }

    /// <summary>
    /// **Removes** the existing user-added translation of the community's
    /// name and/or description (ADR 0048):
    /// <c>POST /community/manage/{componentId}/translations/remove</c>.
    /// Thin Web lane; delegates to
    /// <see cref="IUserInfoService.RemoveCommunityTranslationAsync"/>.
    /// Failure shapes mirror <see cref="AddTranslation"/>.
    /// </summary>
    [HttpPost("/community/manage/{componentId}/translations/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTranslation(
        string componentId, [FromForm] string? languageCode)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null)
            return Forbid();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return RedirectToAction(nameof(Manage), new { componentId });
        }

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await userInfo.RemoveCommunityTranslationAsync(componentId, languageCode, actorId, actorRoles, session);
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
        TempData["info"] = $"Translation removed ({langName}).";
        return RedirectToAction(nameof(Manage), new { componentId });
    }
}
