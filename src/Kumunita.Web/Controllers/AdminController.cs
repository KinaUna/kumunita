using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Pages;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Marten;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The /admin surface (M1 step 8; the section split is ADR 0062) — all
/// <see cref="Roles.GlobalAdmin"/>-gated. The one long scroll is split into five
/// linkable routes, all on this controller (the constructor is pinned by the
/// Web-layer test harnesses, so the section actions live here rather than on
/// separate controllers):
/// <list type="bullet">
/// <item><c>/admin</c> — the <see cref="Index"/> overview dashboard (counts + the
///       unverified-queue shortcut).</item>
/// <item><c>/admin/accounts</c> — the <see cref="Accounts"/> account list + verify
///       queue + Block / Unblock.</item>
/// <item><c>/admin/communities</c> — the <see cref="Communities"/> community list +
///       add / edit / mandatory / enable-disable.</item>
/// <item><c>/admin/platform</c> — the <see cref="Platform"/> platform links + the
///       platform pages table.</item>
/// <item><c>/admin/security</c> — the <see cref="Security"/> landing hub for the
///       <c>/admin/audit</c> (<see cref="Audit"/>) and <c>/admin/break-glass</c>
///       (<see cref="BreakGlass"/>) surfaces, which keep their own routes and pages.</item>
/// </list>
/// The per-account detail is <see cref="Manage"/> at <c>/admin/accounts/{subjectId}</c>.
/// The write lanes (Block / Unblock / SetRole / SetCommunityMembership / the community
/// mutations / the manual-verify) are unchanged; only the surfaces that render them,
/// and the redirect targets after a save, changed.
/// </summary>
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminController(
    AppDbContext identities,
    IDocumentStore store,
    IIdentityService identity,
    IUserInfoService userInfo,
    IPageService pages) : Controller
{
    // SP U03 (ADR 0043 D4) — the five shipped platform surfaces, in the
    // footer-column order (about view first, then the four Page docs). The
    // slugs are the ADR 0040 canonical `system/{slug}` paths; the bare-slug
    // fallback mirrors StaticPagesController.Page (ADR 0040's pre-migration
    // re-parent seam). `about` normally resolves to no id (ADR 0043 D1 — it
    // is a view, not a seeded page): that is correct, not a defect.
    private static readonly (string Slug, string Route)[] PlatformSurfaceRows =
    {
        ("about",   "/about"),
        ("terms",   "/terms"),
        ("help",    "/help"),
        ("privacy", "/privacy"),
        ("conduct", "/conduct"),
    };

    public static async Task<string?> ResolvePageIdAsync(IPageService pages, string slug)
    {
        Page? page;
        try
        {
            page = await pages.GetByPathAsync($"system/{slug}");
        }
        catch (KeyNotFoundException)
        {
            try
            {
                page = await pages.GetByPathAsync(slug);
            }
            catch (KeyNotFoundException)
            {
                page = null;
            }
        }
        return page?.Id;
    }

    /// <summary>
    /// SP U03 (ADR 0043 D4) — composes the five <see cref="AdminIndexViewModel
    /// .PlatformPageRow"/> rows in footer order, resolving each slug to a page id
    /// (the edit target) via <see cref="ResolvePageIdAsync"/>. Pure: no EF Core
    /// round-trip, no <see cref="AdminController"/> instance state — callable from
    /// <see cref="Index"/> and from the test harness without a database.
    /// <c>about</c> is a view, not a seeded page (ADR 0043 D1), so it normally
    /// yields <c>PageId == null</c>; the other four resolve to their seeded
    /// <c>system/{slug}</c> ids (or <c>null</c> if absent — preview-only).
    /// </summary>
    public static async Task<IReadOnlyList<AdminIndexViewModel.PlatformPageRow>>
        BuildPlatformPagesAsync(IPageService pages)
    {
        var ids = await Task.WhenAll(
            PlatformSurfaceRows.Select(r => ResolvePageIdAsync(pages, r.Slug)));
        return ids
            .Select((pageId, i) => new AdminIndexViewModel.PlatformPageRow
            {
                Slug   = PlatformSurfaceRows[i].Slug,
                Route  = PlatformSurfaceRows[i].Route,
                PageId = pageId
            })
            .ToList();
    }

    private static string? AdminSubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?.Value;

    // ── /admin — the overview dashboard (ADR 0062) ───────────────────────
    // The one-page status at a glance: account / unverified / blocked counts,
    // the community count, and the unverified-queue shortcut (the safety
    // valve, still reachable in one click from the dashboard). The per-section
    // detail moved off the single /admin scroll onto its own page —
    // /admin/accounts, /admin/communities, /admin/platform, /admin/security
    // (see ADR 0062). The read assembly (BuildAccountDataAsync) and the write
    // lanes are untouched; only the surfaces that render them changed.

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var (accounts, componentOptions, communityRows) = await BuildAccountDataAsync();

        return View(new AdminDashboardViewModel
        {
            AccountsCount          = accounts.Count,
            UnverifiedCount        = accounts.Count(a => !a.Verified),
            BlockedCount           = accounts.Count(a => a.Blocked),
            CommunitiesCount       = communityRows.Count,
            DisabledCommunityCount = communityRows.Count(c => !c.Enabled),
            Unverified             = accounts
                .Where(a => !a.Verified)
                .Select(a => new AdminDashboardViewModel.UnverifiedAccountRow
                {
                    SubjectId   = a.SubjectId,
                    Email       = a.Email,
                    DisplayName = a.DisplayName
                })
                .ToList()
        });
    }

    // ── /admin/accounts — the account list + verify queue + block/unblock ─
    // The accounts table (the read-only overview + the quick Block / Unblock
    // actions) plus the unverified-account verify queue (the safety valve).
    // The per-account writes live on /admin/accounts/{id} (the Manage detail
    // page). ADR 0062 — the section split off the old single /admin scroll.
    [Route("admin/accounts")]
    [HttpGet]
    public async Task<IActionResult> Accounts()
    {
        var (accounts, componentOptions, communityRows) = await BuildAccountDataAsync();
        return View(new AdminIndexViewModel
        {
            Accounts    = accounts,
            Components  = componentOptions,
            Communities = communityRows
        });
    }

    // ── /admin/communities — the community list + add / edit / toggle ─────
    // The community (per-instance Component) list with the add / edit /
    // mandatory / enable-disable actions. ADR 0062 — the section split off
    // the old single /admin scroll.
    [Route("admin/communities")]
    [HttpGet]
    public async Task<IActionResult> Communities()
    {
        var (accounts, _, communityRows) = await BuildAccountDataAsync();

        // The add-picker candidates for every community panel (the
        // CommunityController.Manage shape): non-blocked profiles, not the
        // actor themself — per community, minus its explicit members.
        var adminSubject = AdminSubjectId(User);
        var profiles = await userInfo.GetProfilesAsync(verifiedOnly: false);
        var candidatePool = profiles
            .Where(p => !p.Blocked && p.SubjectId != adminSubject)
            .ToList();

        var communities = communityRows
            .Select(c =>
            {
                // The per-community member list (the "manage users" panel,
                // ADR 0012's admin-side read): the explicit ComponentMembership
                // rows are exactly the accounts whose CommunityIds contain
                // this community's id — the inverse of the per-account "set of
                // communities" surface. Each member is annotated with whether
                // that account holds the Moderator role with a scope
                // assignment on this community. Both data sets are already
                // loaded by BuildAccountDataAsync (AccountRow.Roles +
                // AccountRow.ComponentIds).
                var memberRows = accounts
                    .Where(a => a.CommunityIds.Contains(c.Id))
                    .Select(a => new AdminCommunitiesViewModel.MemberRow
                    {
                        UserId      = a.SubjectId,
                        DisplayName = a.DisplayName,
                        IsModerator = a.Roles.Contains(Roles.Moderator)
                                     && a.ComponentIds.Contains(c.Id),
                        Roles           = a.Roles,
                        ModeratorScopes = a.Roles.Contains(Roles.Moderator)
                                         ? a.ComponentIds : []
                    })
                    .OrderBy(r => r.DisplayName ?? "", StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var memberIds = memberRows.Select(r => r.UserId).ToHashSet(StringComparer.Ordinal);
                var candidateRows = candidatePool
                    .Where(p => !memberIds.Contains(p.SubjectId))
                    .Select(p => new AdminCommunitiesViewModel.CandidateRow
                    {
                        UserId      = p.SubjectId,
                        DisplayName = p.DisplayName
                    })
                    .OrderBy(r => r.DisplayName ?? "", StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return new AdminCommunitiesViewModel.CommunityRowWithMembers
                {
                    Id              = c.Id,
                    Name            = c.Name,
                    Description     = c.Description,
                    SortOrder       = c.SortOrder,
                    Enabled         = c.Enabled,
                    ModeratorAccess = c.ModeratorAccess,
                    Mandatory       = c.Mandatory,
                    Members         = memberRows,
                    Candidates      = candidateRows
                };
            })
            .ToList();

        return View(new AdminCommunitiesViewModel { Communities = communities });
    }

    // ── /admin/platform — the platform links + the platform pages table ───
    // The platform surfaces (languages, timezone, date format, sign-up,
    // audit, break-glass) and the five shipped platform pages (preview /
    // edit). ADR 0062 — the section split off the old single /admin scroll.
    [Route("admin/platform")]
    [HttpGet]
    public async Task<IActionResult> Platform()
    {
        var platformPages = await BuildPlatformPagesAsync(pages);
        // ADR 0138 — surface the sample-data link (the /admin/sample
        // change-password lock) only on a SampleData__Enabled instance (the
        // "unreachable by construction" shape, ADR 0056 — a real deployment
        // never carries the flag, so the link is absent there). The identity
        // dependency is already in this controller's ctor (the block /
        // role / verify lanes use it), so no test-pinned ctor change is needed.
        bool sampleDataEnabled = await identity.IsSampleDataEnabledAsync();
        return View(new AdminPlatformViewModel
        {
            PlatformPages  = platformPages,
            ShowSampleData = sampleDataEnabled
        });
    }

    // ── /admin/security — the audit + break-glass hub ─────────────────────
    // A landing page linking to the two live security surfaces (/admin/audit
    // and /admin/break-glass — their own pages, unchanged). ADR 0062 — the
    // section split off the old single /admin scroll.
    [Route("admin/security")]
    [HttpGet]
    public IActionResult Security()
    {
        return View();
    }

    // ── /admin/accounts/{subjectId} — the per-account management detail page ──
    // Extracted out of the shell's accounts table (which inlined the role /
    // scope / posting-membership checkbox forms per row — the clutter the
    // admin page needed). The shell keeps the read-only overview + a Manage
    // link; this page owns the writes and renders the three fieldset forms.
    // SetRole and SetCommunityMembership redirect here after a save so the
    // admin lands on the same page with the TempData message. The Block /
    // Unblock buttons stay on the shell (they're the row-level quick actions)
    // and on this page (the full standing surface).

    // The app uses conventional routing (Program.cs: `MapControllerRoute
    // "{controller=Home}/{action=Index}/{id?}"`); the `accounts` segment in
    // `/admin/accounts/{subjectId}` is a URL segment, not an action name, so
    // an explicit route is required (the AdminTimezoneController /
    // AdminSignupController precedent: `[Route("admin/{surface}")]` on
    // sub-controllers; here a single action-level attribute keeps the
    // action in the AdminController where its write lanes already live).
    [Route("admin/accounts/{subjectId}")]
    [HttpGet]
    public async Task<IActionResult> Manage(string subjectId)
    {
        var (accounts, componentOptions, communityRows) = await BuildAccountDataAsync();
        var account = accounts.FirstOrDefault(a => a.SubjectId == subjectId);
        if (account is null)
            return NotFound();

        var mySubject = AdminSubjectId(User);
        return View(new AdminAccountManageViewModel
        {
            Account     = account,
            Components  = componentOptions,
            Communities = communityRows,
            IsSelf      = mySubject is not null
                          && string.Equals(mySubject, subjectId, StringComparison.Ordinal),
            DisabledCommunityIds = communityRows.Where(c => !c.Enabled).Select(c => c.Id).ToList()
        });
    }

    // ── Shared assembly for Index and Manage ──────────────────────────────
    // The shell (Index) and the account detail (Manage) both need the account
    // list + component options + community rows. The read paths are the same
    // (identity EF Core + IUserInfoService + IPageService); the write lanes
    // are untouched by this split (thin-token / audited lanes are unchanged —
    // only the surfaces that render them change).

    private async Task<(IReadOnlyList<AdminIndexViewModel.AccountRow> accounts,
                        IReadOnlyList<AdminIndexViewModel.ComponentOption> componentOptions,
                        IReadOnlyList<AdminIndexViewModel.CommunityRow> communityRows)> BuildAccountDataAsync()
    {
        var users = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            identities.Users
                .OrderBy(u => u.Email ?? u.UserName ?? string.Empty)
                .AsNoTracking());

        var accountsWithRoles = new List<AdminIndexViewModel.AccountRow>();
        foreach (var user in users)
        {
            var subject = user.Id ?? string.Empty;
            var roles = identities.UserRoles
                .Where(ur => ur.UserId == subject)
                .Select(ur => ur.RoleId)
                .ToHashSet();
            var roleNames = identities.Roles
                .Where(r => roles.Contains(r.Id))
                .Select(r => r.Name!)
                .ToList();
            var componentIds = roleNames.Contains(Kumunita.Core.Identity.Roles.Moderator)
                ? (await userInfo.GetAssignmentsAsync(subject)).Select(a => a.ComponentId).ToList()
                : new List<string>();
            var communityIds = (await userInfo.GetCommunityIdsAsync(subject)).ToList();
            var profile = await userInfo.GetProfileAsync(subject);
            accountsWithRoles.Add(new AdminIndexViewModel.AccountRow
            {
                SubjectId    = subject,
                Email        = profile?.Email ?? user.Email ?? user.UserName,
                DisplayName  = profile?.DisplayName ?? user.UserName,
                Verified     = profile?.Verified ?? false,
                Blocked      = profile?.Blocked ?? false,
                Roles        = roleNames,
                ComponentIds = componentIds,
                CommunityIds = communityIds
            });
        }

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

        return (accountsWithRoles, componentOptions, communityRows);
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
            return RedirectToAction(nameof(Communities));

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
            return RedirectToAction(nameof(Communities));
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return RedirectToAction(nameof(Communities));
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Communities));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCommunity(UpdateCommunityViewModel model)
    {
        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Communities));

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
            return RedirectToAction(nameof(Communities));
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Communities));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCommunityEnabled([FromForm] string componentId, [FromForm] bool enabled)
    {
        if (string.IsNullOrEmpty(componentId))
            return RedirectToAction(nameof(Communities));

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
            return RedirectToAction(nameof(Communities));
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Communities));
    }

    // ── Mandatory toggle (the ADR 0012 GlobalAdmin decision, now reachable
    // from the /admin list too — the same lane the community's manage page
    // uses, so the standing rule is identical; only the surface changes) ────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCommunityMandatory([FromForm] string componentId, [FromForm] bool mandatory)
    {
        if (string.IsNullOrEmpty(componentId))
            return RedirectToAction(nameof(Communities));

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
            return RedirectToAction(nameof(Communities));
        }
        catch (ArgumentException)
        {
            return RedirectToAction(nameof(Communities));
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Communities));
    }

    // ── /admin/communities — per-community user management ("manage users") ──
    // The inverse of the per-account "set of communities" surface (the
    // Manage page): from the community's side, grant/revoke its member
    // (the posting right, the ComponentMembership row) and its moderator
    // (the standing-moderator scope, ADR 0003). ADR 0012's add/remove-
    // member lanes are reused verbatim (the same audited Core surface the
    // /community/manage page uses; the GlobalAdmin standing carries —
    // this page is [Authorize(Roles=GlobalAdmin)]).
    //
    // The moderator lanes follow the Manage page's **set-lane shape**
    // (ADR 0030): the form renders the target's *complete* desired role
    // set + scope set at GET time (already loaded by
    // BuildAccountDataAsync — no database read in the POST path) and the
    // action passes the full set straight through to
    // IIdentityService.SetRoleAsync.

    // POST /admin/AddCommunityUser — grant the member (posting right).
    // ADR 0012's add lane; the GlobalAdmin standing carries.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCommunityUser([FromForm] string componentId, [FromForm] string userId)
    {
        if (string.IsNullOrEmpty(componentId) || string.IsNullOrEmpty(userId))
            return RedirectToAction(nameof(Communities));

        var admin = AdminSubjectId(User) ?? string.Empty;
        try
        {
            await userInfo.AddCommunityMemberAsync(componentId, userId, admin, KumunitaPrincipal.RoleSet(User));
            TempData["info"] = "User added to the community.";
        }
        catch (UnauthorizedAccessException)
        {
            TempData["error"] = "You are not permitted to add a community user.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        catch (ArgumentException)
        {
            TempData["error"] = "Unknown community or user.";
        }
        return RedirectToAction(nameof(Communities));
    }

    // POST /admin/RemoveCommunityUser — revoke the member (posting right).
    // ADR 0012's remove lane (Core refuses mandatory communities with a
    // surfaced message; the view hides the button there anyway).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCommunityUser([FromForm] string componentId, [FromForm] string userId)
    {
        if (string.IsNullOrEmpty(componentId) || string.IsNullOrEmpty(userId))
            return RedirectToAction(nameof(Communities));

        var admin = AdminSubjectId(User) ?? string.Empty;
        try
        {
            await userInfo.RemoveCommunityMemberAsync(componentId, userId, admin, KumunitaPrincipal.RoleSet(User));
            TempData["info"] = "User removed from the community.";
        }
        catch (UnauthorizedAccessException)
        {
            TempData["error"] = "You are not permitted to remove a community user.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        catch (ArgumentException)
        {
            TempData["error"] = "Unknown community or user.";
        }
        return RedirectToAction(nameof(Communities));
    }

    // POST /admin/SetCommunityModerator / UnsetCommunityModerator — the
    // moderator scope (the standing-moderator lane, ADR 0003). The form
    // submits the target's complete desired role set + scope set (the
    // Manage-page set-lane shape — ADR 0030): the Unset variant carries
    // the same set minus Moderator and minus this community's scope.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCommunityModerator([FromForm] CommunityModeratorFormViewModel model)
    {
        if (string.IsNullOrEmpty(model.ComponentId) || string.IsNullOrEmpty(model.UserId))
            return RedirectToAction(nameof(Communities));

        var admin = AdminSubjectId(User) ?? string.Empty;
        try
        {
            await identity.SetRoleAsync(
                targetSubjectId: model.UserId,
                adminSubjectId: admin,
                roles: model.RoleNames,
                componentIds: model.ComponentIds);
            TempData["info"] = "User is now a moderator of the community.";
        }
        catch (UnauthorizedAccessException)
        {
            TempData["error"] = "You are not permitted to set a community moderator.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Communities));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnsetCommunityModerator([FromForm] CommunityModeratorFormViewModel model)
    {
        if (string.IsNullOrEmpty(model.ComponentId) || string.IsNullOrEmpty(model.UserId))
            return RedirectToAction(nameof(Communities));

        var admin = AdminSubjectId(User) ?? string.Empty;
        try
        {
            await identity.SetRoleAsync(
                targetSubjectId: model.UserId,
                adminSubjectId: admin,
                roles: model.RoleNames,
                componentIds: model.ComponentIds);
            TempData["info"] = "User no longer moderates the community.";
        }
        catch (UnauthorizedAccessException)
        {
            TempData["error"] = "You are not permitted to unset a community moderator.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Communities));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(SetRoleViewModel model)
    {
        if (string.IsNullOrEmpty(model.TargetSubjectId))
            return RedirectToAction(nameof(Index));

        // Land back on the detail page (not the shell) after a save — the
        // admin stays on the surface they were working on (the shell's own
        // inline form was replaced by the Manage link; this redirect targets
        // the detail page the form lives on). An explicit URL (not
        // RedirectToAction) — the app uses conventional routing, and the
        // `accounts` segment in the path is a URL segment, not an action
        // name, so action-name-based route generation would not resolve.
        var back = $"/admin/accounts/{model.TargetSubjectId}";

        var admin = AdminSubjectId(User) ?? string.Empty;

        // ADR 0030 — roles are independent: the form submits the *set* of elevated roles
        // to grant. `Member` is the "nothing selected" state (the implicit
        // verified-resident standing) and is never a real role, so it is filtered out
        // (Core also guards this — only the three elevated roles are AddTo/RemoveFrom'd).
        // An empty set means "no elevated role" (a plain Member).
        var roles = model.RoleNames
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Where(r => r is Roles.Moderator or Roles.Translator or Roles.GlobalAdmin)
            .Distinct()
            .ToList();

        var componentIds = model.ComponentIds
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();

        try
        {
            await identity.SetRoleAsync(
                targetSubjectId: model.TargetSubjectId,
                adminSubjectId: admin,
                roles: roles,
                componentIds: componentIds);
        }
        catch (UnauthorizedAccessException)
        {
            // Shouldn't be reachable (this page is already [Authorize(Roles=GlobalAdmin)]),
            // but Core's lane enforces it too as a second gate — map to a clean 403.
            TempData["error"] = "You are not permitted to perform this action.";
            return Redirect(back);
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
            return Redirect(back);
        }

        TempData["info"] = "Roles updated.";
        return Redirect(back);
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

        // Same redirect-target note as SetRole: land back on the detail page
        // (the surface the form lives on). Explicit URL — see SetRole.
        var back = $"/admin/accounts/{model.TargetSubjectId}";

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
            return Redirect(back);
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
        return Redirect(back);
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
        return RedirectToAction(nameof(Accounts));
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
            return RedirectToAction(nameof(Accounts));
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
        return RedirectToAction(nameof(Accounts));
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
        return RedirectToAction(nameof(Accounts));
    }

    // ── Delete account — the admin removal lane (ADR 0142) ────────────────
    // A GlobalAdmin removes another resident's account: their audit rows are
    // pseudonymized (the actor id replaced by a tombstone), their group /
    // community memberships are removed, their Profile row is deleted, and
    // the Identity account is removed. The resident's self-serve lane
    // (POST /account/delete, ADR 0142) is the self-deletion branch of the
    // same Core seam (IIdentityService.DeleteAccountAsync); this action is
    // the admin-initiated branch (actor ≠ target, the GlobalAdmin gate
    // applies). Self-deletion through this surface is refused up-front
    // (defense in depth over the Core branch logic): a GlobalAdmin who wants
    // to leave should use their own /account/delete lane (the self-serve
    // shape, Via: Owner) — not the admin lane.

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount([FromForm] string subjectId)
    {
        var admin = AdminSubjectId(User) ?? string.Empty;

        // Self-deletion guard: the admin lane is for removing *other*
        // residents. A GlobalAdmin deleting their own account goes through
        // /account/delete (the self-serve lane, the Via: Owner branch).
        // Refuse before touching the Core lane (the Block self-guard shape).
        if (!string.IsNullOrEmpty(subjectId) && string.Equals(subjectId, admin, StringComparison.Ordinal))
        {
            TempData["error"] = "You cannot delete your own account through this surface. Use your own Delete account settings page instead.";
            return RedirectToAction(nameof(Accounts));
        }

        try
        {
            // The admin-initiated branch (actor ≠ target — the GlobalAdmin
            // gate is enforced in the Core seam, the fail-closed pin).
            await identity.DeleteAccountAsync(targetSubjectId: subjectId, adminSubjectId: admin);
            TempData["info"] = "Account deleted. Their audit trail is preserved (pseudonymized); their account, profile, and memberships are removed.";
        }
        catch (UnauthorizedAccessException)
        {
            TempData["error"] = "You are not permitted to delete this account.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Accounts));
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
