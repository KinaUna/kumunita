using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Pages;
using Kumunita.Core.Usage;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Marten;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/communities</c> management lane — the
/// community CRUD + membership + moderator lanes (ADR 0012
/// community-membership-mandatory, ADR 0003 moderator-scoping).
/// A partial class of <see cref="AdminController"/> extracted so the
/// main file stays under the 600 <c>cs_file</c> ceiling (C3-04).
/// </summary>
public partial class AdminController
{
    [HttpGet]
    public async Task<IActionResult> Communities()
    {
        var (accounts, _, communityRows) = await BuildAccountDataAsync();

        // The add-picker candidates for every community panel (the
        // CommunityController.Manage shape): non-blocked profiles, not the
        // actor themself — per community, minus its explicit members.
        var adminSubject = AdminSubjectId(User);
        var profiles = await profileSettings.GetProfilesAsync(verifiedOnly: false);
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
        // ADR 0161 — the per-page translation surface: for each of the four
        // Page-backed platform pages (about is preview-only, no Page doc), the
        // stored translations + the enabled-language candidate list (the
        // shared _PageTranslationModals partial's data). Standing is the
        // GlobalAdmin gate on this controller (the controller's
        // [Authorize(Roles = GlobalAdmin)]); the PageController write lanes
        // re-check standing server-side (C3). Degrades to an empty list when
        // the localization seam is absent (a test harness — the candidate
        // list is simply empty, the stored-translation rows still render).
        var platformPageTranslations = await BuildPlatformPageTranslationsAsync(pages, localization);
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
            PlatformPageTranslations = platformPageTranslations,
            ShowSampleData = sampleDataEnabled
        });
    }

    /// <summary>
    /// ADR 0161 — composes the per-page translation surface for the
    /// <c>/admin/platform</c> platform pages table: for each of the four
    /// <c>Page</c>-backed platform surfaces (the <c>about</c> view is
    /// preview-only, no <c>Page</c> doc, and is skipped), one
    /// <see cref="AdminPlatformViewModel.PlatformPage"/> row carrying the
    /// page's authored-in title, its stored
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows, the enabled-
    /// language candidate list (the enabled catalog languages with no stored
    /// translation row — the ADR 0027 "candidate list excludes the item's
    /// already-translated languages" rule), and the code → native-name map.
    /// The <c>Slug</c> is the id-prefix the shared
    /// <c>Views/Shared/_PageTranslationModals</c> partial uses to disambiguate
    /// element ids across the multiple rows on one document. Each platform
    /// page is resolved once (the same <c>system/{slug}</c> → bare-slug
    /// fallback the edit-link resolution uses) — the <c>PageId</c> and the
    /// title come from that single read. Pure: no EF Core round-trip,
    /// callable from the test harness.
    /// </summary>
    public static async Task<IReadOnlyList<AdminPlatformViewModel.PlatformPage>>
        BuildPlatformPageTranslationsAsync(
            IPageService pages,
            Kumunita.Core.Localization.ILocalizationService? localization = null)
    {
        // The enabled catalog (the candidate list + the code → name map).
        // When the localization seam is absent (a test harness), the enabled
        // set is empty — the candidate list is simply empty (no "add" modals),
        // and the stored-translation rows still render (graceful degradation).
        IReadOnlyList<Kumunita.Core.Localization.LanguageCatalog> enabledCatalog = [];
        if (localization is not null)
        {
            var catalog = await localization.ListLanguagesAsync();
            enabledCatalog = catalog.Where(l => l.Enabled).OrderBy(l => l.SortOrder).ToList();
        }

        var result = new List<AdminPlatformViewModel.PlatformPage>();
        foreach (var (slug, _route) in PlatformSurfaceRows)
        {
            // Resolve the page once (system/{slug} → bare-slug → absent) —
            // the same fallback the edit-link resolution (ResolvePageIdAsync)
            // uses. about is a view, not a Page doc (ADR 0043 D1), so it is
            // absent and skipped (preview-only).
            Kumunita.Core.Pages.Page? page = null;
            try { page = await pages.GetByPathAsync($"system/{slug}"); }
            catch (KeyNotFoundException) { /* try the bare-slug legacy fallback below */ }
            if (page is null)
            {
                try { page = await pages.GetByPathAsync(slug); }
                catch (KeyNotFoundException) { continue; /* absent — preview-only */ }
            }

            var translations = await pages.GetTranslationsAsync(page.Id);
            var translatedCodes = translations
                .Select(t => t.LanguageCode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            // The candidate list mirrors the page detail's rule (Views/Page/
            // Show.cshtml missingLanguages): enabled catalog languages with no
            // stored PageTranslation row AND not the page's authored-in
            // language (the ADR 0027 "candidate list excludes the item's own
            // language" rule — the authored-in body is the default, not a
            // translation to add).
            var missingLanguages = enabledCatalog
                .Where(l => !translatedCodes.Contains(l.Id)
                            && !string.Equals(l.Id, page.LanguageCode, StringComparison.OrdinalIgnoreCase))
                .Select(l => new LanguageOption(l.Id, l.NativeName, false))
                .ToList();
            var langNames = enabledCatalog
                .ToDictionary(l => l.Id, l => l.NativeName, StringComparer.OrdinalIgnoreCase);

            result.Add(new AdminPlatformViewModel.PlatformPage
            {
                Slug = slug,
                PageId = page.Id,
                Title = page.Title,
                Translations = translations,
                MissingLanguages = missingLanguages,
                LangNames = langNames
            });
        }

        return result;
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
        // IMPROVE-UX A15 — the per-resident quota override card (the admin half
        // of the storage loop): the resident's current override, the effective
        // quota (override ?? global), the community-wide global, and their
        // usage. Null-tolerant (a test harness with no IStorageSettingsService
        // renders the card with its zero/empty defaults; the app always has the
        // seam — DI supplies it).
        var storageCard = await BuildStorageOverrideCardAsync(subjectId);
        return View(new AdminAccountManageViewModel
        {
            Account     = account,
            Components  = componentOptions,
            Communities = communityRows,
            IsSelf      = mySubject is not null
                          && string.Equals(mySubject, subjectId, StringComparison.Ordinal),
            DisabledCommunityIds = communityRows.Where(c => !c.Enabled).Select(c => c.Id).ToList(),
            // ── A15 override card ───────────────────────────────────────────
            QuotaOverrideBytes    = storageCard.OverrideBytes,
            EffectiveQuotaBytes   = storageCard.EffectiveQuotaBytes,
            EffectiveQuotaUnlimited = storageCard.EffectiveQuotaUnlimited,
            UsedBytes             = storageCard.UsedBytes,
            GlobalQuotaBytes      = storageCard.GlobalQuotaBytes,
            QuotaOverrideInput    = storageCard.OverrideInput,
            UsedHuman             = ResidentStorageViewModel.FormatBytes(storageCard.UsedBytes),
            EffectiveQuotaHuman   = storageCard.EffectiveQuotaUnlimited ? "Unlimited"
                                : ResidentStorageViewModel.FormatBytes(storageCard.EffectiveQuotaBytes),
            GlobalQuotaHuman      = storageCard.GlobalQuotaBytes == 0 ? "Unlimited"
                                : ResidentStorageViewModel.FormatBytes(storageCard.GlobalQuotaBytes)
        });
    }

    // ── A15 — the storage override card read (the Manage detail page) ────────
    // Composes the resident's current override (GetResidentQuotaOverrideAsync),
    // the community settings (GetOrCreateAsync — the global per-user quota) and
    // their usage (GetPerUserUsageBytesAsync, the C-SM·7 seam) into the
    // effective-quota card. The effective quota is override ?? global (the
    // C-UP·5 sentinel preserved per-resident: unset ⇒ global, 0 ⇒ unlimited for
    // the resident). Null-tolerant: a harness with no IStorageSettingsService
    // yields the zero/empty defaults (the card renders empty, no NRE).
    private async Task<(
        long? OverrideBytes, long EffectiveQuotaBytes, bool EffectiveQuotaUnlimited,
        long UsedBytes, long GlobalQuotaBytes, string OverrideInput)>
        BuildStorageOverrideCardAsync(string subjectId)
    {
        var defaults = (
            OverrideBytes: (long?)null, EffectiveQuotaBytes: 0L,
            EffectiveQuotaUnlimited: true, UsedBytes: 0L,
            GlobalQuotaBytes: 0L, OverrideInput: string.Empty);
        if (storageSettings is null)
            return defaults;

        // Three independent reads (override + settings + usage) — start both
        // reads that don't depend on each other, then await.
        var overrideTask = storageSettings.GetResidentQuotaOverrideAsync(subjectId, CancellationToken.None);
        var settingsTask = storageSettings.GetOrCreateAsync(CancellationToken.None);
        var usageTask    = storageSettings.GetPerUserUsageBytesAsync(subjectId, CancellationToken.None);
        long? overrideBytes = await overrideTask;
        var settings = await settingsTask;
        long usage = await usageTask;

        var globalQuota = settings.PerUserQuotaBytes;
        // A15 effective quota: override ?? global (the loop's heart).
        var effectiveQuota = StorageLimits.EffectivePerUserQuota(overrideBytes, settings);
        // The form pre-fill (MiB): blank = no override (clear → global); "0" =
        // the per-resident unlimited sentinel; else the cap in whole MiB.
        var overrideInput = overrideBytes is null
            ? string.Empty
            : overrideBytes == 0
                ? "0"
                : (overrideBytes.Value / (1024L * 1024L)).ToString();
        return (overrideBytes, effectiveQuota, effectiveQuota == 0,
            usage, globalQuota, overrideInput);
    }

    // ── A15 — set / clear the per-resident quota override ────────────────────
    // The admin write half of the storage loop (A14's "request more space"
    // nudge points at this action). POST /admin/accounts/{subjectId}/quota.
    // Value semantics (the AdminStorageController.Save C-UP·5 shape): a **blank**
    // input ⇒ **clear** the override (back to the community default); <c>0</c> ⇒
    // unlimited for this resident (the per-resident sentinel); a whole MiB
    // number ⇒ the cap. The actor is the signed-in admin (SubjectId(User)); the
    // target is the route subjectId. GlobalAdmin-gated at this boundary (the
    // class-level [Authorize(Roles=GlobalAdmin)] — the ADR 0050 split: the Core
    // lane does not re-check standing). Flash-redirect back to the detail page
    // (the AdminStorageController.Save / SetRole shape).
    [Route("admin/accounts/{subjectId}/quota")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStorageQuotaOverride(string subjectId, string? quotaMiBInput)
    {
        // In the app the IStorageSettingsService is always DI-supplied (the seam
        // is registered by AddUsage); the null-tolerance below is the
        // harness-only guard (a test constructing the controller without the
        // seam). The write delegates to the committed lane (one in-caller-
        // session write, the ADR 0050 split — standing is gated at this
        // boundary, the Core lane does not re-check it).
        var back = $"/admin/accounts/{subjectId}";
        var actor = AdminSubjectId(User) ?? string.Empty;

        // C-UP·5 value semantics (MiB input, converted to bytes): blank ⇒ clear
        // (null); "0" ⇒ the per-resident unlimited sentinel; else the cap in
        // whole MiB. A negative or non-numeric value is rejected (flash back).
        long? quotaBytes;
        if (string.IsNullOrWhiteSpace(quotaMiBInput))
        {
            quotaBytes = null;   // clear → back to the community default
        }
        else if (quotaMiBInput == "0")
        {
            quotaBytes = 0;      // unlimited for this resident
        }
        else if (long.TryParse(quotaMiBInput, out var miB) && miB > 0)
        {
            quotaBytes = miB * 1024L * 1024L;
        }
        else
        {
            TempData["error"] = await this.FlashAsync(
                "admin.flash_storage_override_invalid",
                "The quota override must be a whole number of MiB, 0 for unlimited, or left blank to use the community default.");
            return Redirect(back);
        }

        // In the app storageSettings is always DI-supplied (registered by
        // AddUsage); the `!` is the harness-only guard (a test without the seam).
        await storageSettings!.SetResidentQuotaOverrideAsync(subjectId, quotaBytes, actor);

        var flashKey = quotaBytes is null
            ? "admin.flash_storage_override_cleared"
            : "admin.flash_storage_override_saved";
        var flashText = quotaBytes is null
            ? "Quota override cleared — this account uses the community default."
            : quotaBytes == 0
                ? "This account's quota is now unlimited."
                : "Quota override saved.";
        TempData["info"] = await this.FlashAsync(flashKey, flashText);
        return Redirect(back);
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
                ? (await profileSettings.GetAssignmentsAsync(subject)).Select(a => a.ComponentId).ToList()
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

        var allComponents = await profileSettings.GetComponentsAsync(enabledOnly: false);
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
            var created = await community.CreateCommunityAsync(model.Name, model.Description, admin);
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
            TempData["error"] = await this.FlashAsync("admin.flash_mandatory_forbidden", "You are not permitted to set a community as mandatory.");
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
            await community.UpdateCommunityAsync(
                componentId: model.ComponentId,
                name: model.Name,
                description: model.Description,
                sortOrder: model.SortOrder,
                moderatorAccess: null,
                enabled: null,
                actorId: admin);
            TempData["info"] = await this.FlashAsync("admin.flash_community_updated", "Community “{0}” updated.", model.Name);
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
            await community.SetCommunityEnabledAsync(componentId, enabled, admin);
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
            TempData["error"] = await this.FlashAsync("admin.flash_mandatory_standing_forbidden", "You are not permitted to set a community's mandatory standing.");
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
            TempData["info"] = await this.FlashAsync("admin.flash_community_user_added", "User added to the community.");
        }
        catch (UnauthorizedAccessException)
        {
            TempData["error"] = await this.FlashAsync("admin.flash_community_user_add_forbidden", "You are not permitted to add a community user.");
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        catch (ArgumentException)
        {
            TempData["error"] = await this.FlashAsync("admin.flash_unknown_community_or_user", "Unknown community or user.");
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
            TempData["info"] = await this.FlashAsync("admin.flash_community_user_removed", "User removed from the community.");
        }
        catch (UnauthorizedAccessException)
        {
            TempData["error"] = await this.FlashAsync("admin.flash_community_user_remove_forbidden", "You are not permitted to remove a community user.");
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        catch (ArgumentException)
        {
            TempData["error"] = await this.FlashAsync("admin.flash_unknown_community_or_user", "Unknown community or user.");
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
            TempData["info"] = await this.FlashAsync("admin.flash_moderator_set", "User is now a moderator of the community.");
        }
        catch (UnauthorizedAccessException)
        {
            TempData["error"] = await this.FlashAsync("admin.flash_moderator_set_forbidden", "You are not permitted to set a community moderator.");
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Communities));
    }
}
