using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Marten;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The /admin shell surface (M1 step 8): roles + component-scope assignment, the
/// unverified-account safety valve (the admin manual-verify lane), — all
/// <see cref="Roles.GlobalAdmin"/>-gated. <c>/admin/audit</c> and
/// <c>/admin/break-glass</c> live under the same controller but on distinct routes:
/// <c>Index</c> (shell), <c>Audit</c>, <c>BreakGlass</c>.
/// </summary>
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminController(
    AppDbContext identities,
    IDocumentStore store,
    IIdentityService identity,
    IUserInfoService userInfo) : Controller
{
    private static string? AdminSubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?.Value;

    // ── /admin — the shell: account list + role/scope assignment + safety valve ─────────

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // The identity schema is the only source of account + role facts — the `mt`
        // documents (Profile, ModeratorAssignment) are joined via IUserInfoService
        // reads, per ADR 0004 A/C (no domain model touches the EF context, and vice versa).
        // Both EF Core and Marten define ToListAsync/FirstOrDefaultAsync extensions, so
        // qualify the EF one explicitly wherever this controller mixes the two.
        var users = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            identities.Users
                .OrderBy(u => u.Email ?? u.UserName ?? string.Empty)
                .AsNoTracking());

        var accountsWithRoles = new List<AdminIndexViewModel.AccountRow>();

        foreach (var user in users)
        {
            var subject = user.Id ?? string.Empty;
            // A single join covers every account's role rows (no N UserManager round trips).
            var roles = identities.UserRoles
                .Where(ur => ur.UserId == subject)
                .Select(ur => ur.RoleId)
                .ToHashSet();
            var roleNames = identities.Roles
                .Where(r => roles.Contains(r.Id))
                .Select(r => r.Name!)
                .ToList();

            // Only a Moderator's scope matters per ADR 0003 — a non-Moderator's
            // ModeratorAssignment rows (if any — shouldn't exist post-demotion but
            // defensively) are never read by the authorization path.
            var componentIds = roleNames.Contains(Kumunita.Core.Identity.Roles.Moderator)
                ? (await userInfo.GetAssignmentsAsync(subject)).Select(a => a.ComponentId).ToList()
                : new List<string>();

            // Community membership (the **posting right** — a distinct data row
            // from the moderator scope above; see ComponentMembership doc-comment
            // in Kumunita.Core.UserInfo for the "posting right vs moderator scope"
            // distinction).
            var communityIds = (await userInfo.GetCommunityIdsAsync(subject)).ToList();

            var profile = await userInfo.GetProfileAsync(subject);
            var verified = profile?.Verified ?? false;
            var blocked  = profile?.Blocked ?? false;

            accountsWithRoles.Add(new AdminIndexViewModel.AccountRow
            {
                SubjectId    = subject,
                Email        = profile?.Email ?? user.Email ?? user.UserName,
                DisplayName  = profile?.DisplayName ?? user.UserName,
                Verified     = verified,
                Blocked      = blocked,
                Roles        = roleNames,
                ComponentIds = componentIds,
                CommunityIds = communityIds
            });
        }

        // Components come in two projections:
        //   - `Components` — the enabled-only set for the role-assignment "scope"
        //     checkbox list (the M1 shape; a disabled component is not a valid
        //     moderator scope).
        //   - `Communities` — the full set (enabled + disabled) for the new
        //     "Communities" section (add / edit / enable-disable). The admin
        //     needs to *see* the disabled rows to re-enable them.
        var allComponents = await userInfo.GetComponentsAsync(enabledOnly: false);
        var componentOptions = allComponents
            .Where(c => c.Enabled)
            .Select(c => new AdminIndexViewModel.ComponentOption
            {
                Id              = c.Id,
                Name            = c.Name,
                ModeratorAccess = c.ModeratorAccess
            })
            .ToList();
        var communityRows = allComponents
            .OrderBy(c => c.SortOrder)
            .Select(c => new AdminIndexViewModel.CommunityRow
            {
                Id              = c.Id,
                Name            = c.Name,
                Description     = c.Description,
                SortOrder       = c.SortOrder,
                Enabled         = c.Enabled,
                ModeratorAccess = c.ModeratorAccess,
                Mandatory       = c.Mandatory
            })
            .ToList();

        return View(new AdminIndexViewModel
        {
            Accounts    = accountsWithRoles,
            Components  = componentOptions,
            Communities = communityRows
        });
    }

    // ── /admin — community management (add / edit / enable-disable) ──────
    // The "communities" are the per-instance <see cref="Component"/> rows
    // (Safety, Maintenance, Social, Governance by default — ADR 0002's
    // "a single Kumunita row per instance" + the four seeded feeds). A
    // GlobalAdmin can add a new one, edit an existing one, or hide one
    // (Enabled=false — the user-chosen "remove"; the row + posts + any
    // moderator assignments remain intact for recovery). Every write
    // delegates to <see cref="IUserInfoService"/> (the single Core write
    // lane, ADR 0006-D) and appends an AccessAudit row (via:Admin) in the
    // same transaction — the C3/C4 invariants live in the service, the
    // controller is a thin wrapper.

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCommunity(AddCommunityViewModel model)
    {
        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Index));

        var admin = AdminSubjectId(User) ?? string.Empty;
        try
        {
            var created = await userInfo.CreateCommunityAsync(model.Name, model.Description, admin);
            // ADR 0012 — the form's "mandatory" checkbox: the Core CreateAsync
            // defaults Mandatory=false, so flip the flag on through the same
            // GlobalAdmin-only audited lane (the admin's standing, never the
            // moderator's). A second audit row (community.set-mandatory) is
            // correct — it *is* a distinct, audited action.
            if (model.Mandatory)
            {
                await userInfo.SetCommunityMandatoryAsync(created.Id, true, admin, KumunitaPrincipal.RoleSet(User));
            }
            TempData["info"] = model.Mandatory
                ? $"Community “{model.Name}” added (mandatory — everyone in the neighborhood is a member)."
                : $"Community “{model.Name}” added.";
        }
        catch (UnauthorizedAccessException)
        {
            // Unreachable in practice (this page is [Authorize(Roles=GlobalAdmin)]
            // so RoleSet(User) carries GlobalAdmin), but the mandatory lane
            // re-checks standing in Core (thin token) — fail closed.
            TempData["error"] = "You are not permitted to set a community as mandatory.";
            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCommunity(UpdateCommunityViewModel model)
    {
        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Index));

        var admin = AdminSubjectId(User) ?? string.Empty;
        try
        {
            // The "edit" form only exposes name / description / sort — description
            // and sort arrive from the hidden inputs, both optional at the Core
            // patch (null = keep-as-is). The Core also accepts icon / flag toggles,
            // but they're not exposed here (the moderator-access flag has its own
            // surface; the icon picker is a separate future piece of UI).
            await userInfo.UpdateCommunityAsync(
                componentId: model.ComponentId,
                name: model.Name,
                description: model.Description,
                sortOrder: model.SortOrder,
                moderatorAccess: null,
                enabled: null,
                actorId: admin);
            TempData["info"] = $"Community “{model.Name}” updated.";
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCommunityEnabled([FromForm] string componentId, [FromForm] bool enabled)
    {
        if (string.IsNullOrEmpty(componentId))
            return RedirectToAction(nameof(Index));

        var admin = AdminSubjectId(User) ?? string.Empty;
        try
        {
            await userInfo.SetCommunityEnabledAsync(componentId, enabled, admin);
            TempData["info"] = enabled
                ? "Community re-enabled. It is visible on the feed and in the moderator scope list."
                : "Community disabled. It is hidden from the /community feed and the moderator scope list; its posts and assignments remain intact.";
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    // ── Mandatory toggle (the ADR 0012 GlobalAdmin decision, now reachable
    // from the /admin list too — the same lane the community's manage page
    // uses, so the standing rule is identical; only the surface changes) ────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCommunityMandatory([FromForm] string componentId, [FromForm] bool mandatory)
    {
        if (string.IsNullOrEmpty(componentId))
            return RedirectToAction(nameof(Index));

        var admin = AdminSubjectId(User) ?? string.Empty;
        try
        {
            // RoleSet(User) carries GlobalAdmin (this page is
            // [Authorize(Roles=GlobalAdmin)]) — the Core standing gate holds.
            await userInfo.SetCommunityMandatoryAsync(componentId, mandatory, admin, KumunitaPrincipal.RoleSet(User));
            TempData["info"] = mandatory
                ? $"Community marked mandatory — everyone in the neighborhood is a member."
                : $"Community marked optional — membership is now explicit again.";
        }
        catch (UnauthorizedAccessException)
        {
            // Unreachable (page is GlobalAdmin-gated) but the Core lane
            // re-checks standing (thin token) — fail closed.
            TempData["error"] = "You are not permitted to set a community's mandatory standing.";
            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException)
        {
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(SetRoleViewModel model)
    {
        if (string.IsNullOrEmpty(model.TargetSubjectId))
            return RedirectToAction(nameof(Index));

        var admin = AdminSubjectId(User) ?? string.Empty;
        var componentIds = model.ComponentIds
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();

        try
        {
            // "Member" is the "no elevated role" value (Core's SetRoleAsync removes any
            // GlobalAdmin/Moderator when the role string is neither of those).
            var role = string.IsNullOrWhiteSpace(model.Role) || model.Role == Kumunita.Core.Identity.Roles.Member
                ? Kumunita.Core.Identity.Roles.Member
                : model.Role;
            await identity.SetRoleAsync(
                targetSubjectId: model.TargetSubjectId,
                adminSubjectId: admin,
                role: role,
                componentIds: componentIds);
        }
        catch (UnauthorizedAccessException)
        {
            // Shouldn't be reachable (this page is already [Authorize(Roles=GlobalAdmin)]),
            // but Core's lane enforces it too as a second gate — map to a clean 403.
            TempData["error"] = "You are not permitted to perform this action.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }

        TempData["info"] = "Role updated.";
        return RedirectToAction(nameof(Index));
    }

    // ── /admin — Community membership (the **posting right**; the distinct data
    // from SetRole's moderator scope). A GlobalAdmin grants/removes which
    // communities a given account may post to. The Core write lanes
    // (SetCommunityMembershipAsync / ClearCommunityMembershipAsync) are
    // audited (via:Admin); this controller is the thin web wrapper.
    // The diff of the new set against the current row decides add-vs-remove
    // per community so an admin can "uncheck one" without unchecking every
    // other.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCommunityMembership([FromForm] CommunityMembershipSetViewModel model)
    {
        if (string.IsNullOrEmpty(model.TargetSubjectId))
            return RedirectToAction(nameof(Index));

        var admin = AdminSubjectId(User) ?? string.Empty;

        // All component ids (enabled + disabled — the current rows are keyed
        // on them regardless of Enabled state).
        var allComponents = (await userInfo.GetComponentsAsync(enabledOnly: false)).Select(c => c.Id).ToHashSet();

        // ADR 0012 — the enabled mandatory ids (the union read in
        // GetCommunityIdsAsync always carries them — see the scope note
        // below the diff).
        var mandatoryComponentIds = (await userInfo.GetComponentsAsync(enabledOnly: true))
            .Where(c => c.Mandatory)
            .Select(c => c.Id)
            .ToHashSet();

        var newSet = model.CommunityIds
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToHashSet();

        // Only target ids that exist on this instance (defensive: an admin
        // shouldn't be able to create orphans via an old form state, but it
        // is a data-hygiene guard worth keeping — the Core write lane
        // throws on a missing component, so this filter keeps the error
        // surface at the controller level and keeps the per-community
        // loop tight).
        newSet.IntersectWith(allComponents);

        // ADR 0012 — mandatory components are out of this form's scope: every
        // verified resident is an implicit member (the union read in
        // GetCommunityIdsAsync always includes an enabled mandatory id), so
        // "unchecking" one would be a silent no-op (the Core lane skips it)
        // and "checking" it a redundant row. Drop them from both sides of the
        // diff so the added/removed counts stay honest.
        newSet.ExceptWith(mandatoryComponentIds);
        var currentSet = new HashSet<string>(await userInfo.GetCommunityIdsAsync(model.TargetSubjectId));
        currentSet.ExceptWith(mandatoryComponentIds);

        var toAdd    = newSet.Except(currentSet).ToList();
        var toRemove = currentSet.Except(newSet).ToList();

        var errors = new List<string>();

        foreach (var componentId in toAdd)
        {
            try
            {
                await userInfo.SetCommunityMembershipAsync(
                    componentId: componentId,
                    userId: model.TargetSubjectId,
                    actorId: admin);
            }
            catch (InvalidOperationException ex) { errors.Add(ex.Message); }
        }

        foreach (var componentId in toRemove)
        {
            try
            {
                await userInfo.ClearCommunityMembershipAsync(
                    componentId: componentId,
                    userId: model.TargetSubjectId,
                    actorId: admin);
            }
            catch (InvalidOperationException ex) { errors.Add(ex.Message); }
        }

        if (errors.Count > 0)
        {
            TempData["error"] = string.Join(" ", errors);
            return RedirectToAction(nameof(Index));
        }

        var addedCount    = toAdd.Count;
        var removedCount  = toRemove.Count;
        var summary = (addedCount, removedCount) switch
        {
            (0, 0) => "No membership change.",
            (_, 0) => $"Granted membership: {addedCount} community(ies).",
            (0, _) => $"Revoked membership: {removedCount} community(ies).",
            _      => $"Membership updated: {addedCount} added, {removedCount} removed.",
        };

        TempData["info"] = summary;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManuallyVerify([FromForm] string subjectId)
    {
        var admin = AdminSubjectId(User) ?? string.Empty;
        try
        {
            await identity.ManuallyVerifyAsync(targetSubjectId: subjectId, adminSubjectId: admin);
            TempData["info"] = "Account verified. It can now sign in.";
        }
        catch (UnauthorizedAccessException)
        {
            TempData["error"] = "You are not permitted to verify this account.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    // ── Block / Unblock — the admin account suspension lane (GlobalAdmin) ─────────────
    // Reversible suspension: Block strips the account's standing (no Member/Moderator/
    // GlobalAdmin) at the Identity↔cookie seam; Unblock restores it. Both delegate to the
    // Core admin lane (audited via:Admin + security-stamp rotation) — the controller is a
    // thin wrapper, matching the other /admin account actions.
    //
    // Self-block is refused up-front in the controller (defense in depth): the row
    // renders an Unblock button (if currently blocked) but *no* Block button on the
    // admin's own row, so the UI never offers the action it would refuse. A GlobalAdmin
    // who blocks themselves becomes a blocked, standing-less admin (Member/Moderator/
    // GlobalAdmin all stripped) — if they were the only GlobalAdmin, every /admin surface
    // (including the unblock lane) requires GlobalAdmin, so they could not self-restore.

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Block([FromForm] string subjectId)
    {
        var admin = AdminSubjectId(User) ?? string.Empty;

        // Guard: an admin cannot block themselves. Refuse before touching the Core lane,
        // so no Profile.Blocked flip / audit row / security-stamp rotation happens at all.
        if (!string.IsNullOrEmpty(subjectId) && string.Equals(subjectId, admin, StringComparison.Ordinal))
        {
            TempData["error"] = "You cannot block your own account. Have another GlobalAdmin perform this, or use a different admin account.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await identity.BlockAsync(targetSubjectId: subjectId, adminSubjectId: admin);
            TempData["info"] = "Account blocked. It has no standing until unblocked.";
        }
        catch (UnauthorizedAccessException)
        {
            // Shouldn't be reachable (this page is already [Authorize(Roles=GlobalAdmin)]),
            // but the Core lane enforces it too — map to a clean 403-style message.
            TempData["error"] = "You are not permitted to block this account.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unblock([FromForm] string subjectId)
    {
        var admin = AdminSubjectId(User) ?? string.Empty;
        try
        {
            await identity.UnblockAsync(targetSubjectId: subjectId, adminSubjectId: admin);
            TempData["info"] = "Account unblocked. Its standing is available again.";
        }
        catch (UnauthorizedAccessException)
        {
            TempData["error"] = "You are not permitted to unblock this account.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    // ── /admin/audit — the always-on access-decision log (GlobalAdmin) ────────────────

    [HttpGet]
    public async Task<IActionResult> Audit([FromQuery] int? last = null, [FromQuery] string? via = null, [FromQuery] string? outcome = null)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        // Type the running query as the (non-ordered) IQueryable so .Where() filters below
        // don't need to re-assign into an IOrderedQueryable; the ordering is already applied.
        System.Linq.IQueryable<AccessAudit> query = session.Query<AccessAudit>()
            .OrderByDescending(a => a.At);

        // Filter on the enum directly (Marten translates an enum comparison to a SQL
        // comparison); a `.ToString().Equals(...)` would not translate. Parse to the enum
        // value, ignoring invalid strings (treated as "no filter").
        if (Enum.TryParse<AccessVia>(via, ignoreCase: true, out var viaEnum))
            query = query.Where(a => a.Via == viaEnum);
        if (Enum.TryParse<AccessOutcome>(outcome, ignoreCase: true, out var outcomeEnum))
            query = query.Where(a => a.Outcome == outcomeEnum);

        const int page = 50;
        // Fully-qualify the Marten async extension (same EF/Marten ambiguity as elsewhere).
        var rows = await Marten.QueryableExtensions.ToListAsync(query.Take(page));

        return View(new AdminAuditPageViewModel
        {
            Rows = rows.Select(r => new AdminAuditPageViewModel.Row
            {
                Id                  = r.Id,
                At                  = r.At,
                ActorId             = r.ActorId,
                EffectivePrincipal = r.EffectivePrincipalId,
                Action              = r.Action,
                TargetKind          = r.TargetKind,
                TargetId            = r.TargetId,
                VisibleCount        = r.VisibleCount,
                HiddenCount         = r.HiddenCount,
                Via                 = r.Via.ToString(),
                Outcome             = r.Outcome.ToString()
            }).ToList(),
            Via = via,
            Outcome = outcome,
            Page    = last ?? 0
        });
    }

    // ── /admin/break-glass — consume the operator-written AdminOverride (once) ─────────

    [HttpGet]
    public async Task<IActionResult> BreakGlass()
    {
        var subject = AdminSubjectId(User) ?? string.Empty;

        // This page is [Authorize(Roles=GlobalAdmin)] already, but break-glass is the
        // elevation path itself — the row targets the specific account that will consume
        // the token. Show the row's state read-only (never list/created here — the
        // operator writes it in psql, OPS §9).
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        // The break-glass rows for one account are rare (one-shot, operator-written) so a
        // small in-memory ordering is fine and avoids the EF/Marten FirstOrDefaultAsync ambiguity.
        var candidates = await Marten.QueryableExtensions.ToListAsync(
            session.Query<AdminOverride>().Where(o => o.UserId == subject));
        var row = candidates.OrderByDescending(o => o.GrantedAt).FirstOrDefault();

        return View(new BreakGlassViewModel
        {
            HasOverride = row is not null,
            Consumed    = row?.ConsumedAt is not null,
            ExpiresAt   = row?.ExpiresAt,
            GrantedAt   = row?.GrantedAt
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BreakGlass(BreakGlassConsumeViewModel model)
    {
        var subject = AdminSubjectId(User) ?? string.Empty;
        try
        {
            await identity.ConsumeBreakGlassAsync(subject, model.Token);
            TempData["info"] = "Break-glass elevation activated. It lasts until its expiry; every privileged decision under it is audited with via:BreakGlass.";
        }
        catch (InvalidOperationException ex)
        {
            // "Not recognized, already consumed, or expired" — the single-use guarantee.
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(BreakGlass));
    }
}
