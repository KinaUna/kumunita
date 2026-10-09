using System.Security.Claims;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Models;

/// <summary>
/// The composer trio shared by the post/announcement/event/project/group
/// composer GETs (U05, IMPROVE lane) — new composer GETs call this, do not
/// copy it.
/// <para>
/// Before U05, each of the four composer controllers
/// (<see cref="Controllers.PostsController"/>,
/// <see cref="Controllers.EventController"/>,
/// <see cref="Controllers.ProjectsController"/>,
/// <see cref="Controllers.AnnouncementController"/>) carried its own
/// private copy of the three seed methods below (a copy-paste "silent
/// coupling" — the <c>anti-patterns.md</c> seam the IMPROVE lane names).
/// The claim in <see cref="Controllers.ProjectsController"/>'s class-level
/// doc-comment that the trio was "the M2/M3/M4 shared pattern, reused not
/// reinvented" was **false**: the methods were defined once per controller.
/// U05 closes that gap: the logic now lives here, exactly once; the
/// per-controller private methods are thin delegates to these.
/// </para>
/// <list type="bullet">
/// <item><see cref="SeedGrantPickerOptionsAsync"/> — the "Who to grant to"
/// option lists for the shared <c>Views/Shared/_GrantPickers</c> partial:
/// <b>Users</b> (every visible, non-blocked, verified <c>Profile</c> except
/// the actor themself) + <b>Groups</b> (the platform public group list; a
/// private group is an organizing unit, never granted as an audience,
/// ADR 0010). Stored on <see cref="Controller.ViewData"/> (read-only view
/// data — never model properties on the composer's view-model; the only
/// form-bound grants field remains the partial's hidden
/// <c>Audience.Grants</c> textarea).</item>
/// <item><see cref="SeedLanguagePickerAsync"/> — the authored-in language
/// picker (ADR 0018, ADR 0005 B): the instance's **enabled**
/// <see cref="Kumunita.Core.Localization.LanguageCatalog"/>, ordered by
/// <c>SortOrder</c>, read through
/// <see cref="ILocalizationService.ListLanguagesAsync"/> (the HTTP-free
/// seam, ADR 0005 D — the exact catalog read the
/// <c>LocaleController.Index</c> page uses).</item>
/// <item><see cref="SeedComponentPickerAsync"/> — the component
/// *feed-organizer* picker (C-M3·2): the instance's **enabled**
/// <see cref="Kumunita.Core.UserInfo.Component"/> set (a *filter, never a
/// gate*; the audience is the sole access boundary on the form).</item>
/// </list>
/// </summary>
public static class ComposerSeedOptions
{
    /// <summary>
    /// Seeds the composer's "Who to grant to" option lists for the shared
    /// <c>Views/Shared/_GrantPickers</c> partial. <b>Users</b> — every
    /// visible, non-blocked, verified <c>Profile</c> except the actor
    /// themself; <b>Groups</b> — the platform public group list (a private
    /// group is an organizing unit, never granted as an audience, ADR 0010).
    /// Stored on <see cref="Controller.ViewData"/> (read-only view data —
    /// never model properties on the composer's view-model; the only
    /// form-bound grants field remains the partial's hidden
    /// <c>Audience.Grants</c> textarea).
    /// </summary>
    /// <param name="includeAssignUsers">
    /// When <c>true</c>, also seeds <c>Assign_Users</c> — the same
    /// verified/non-blocked set but **including the actor** (ADR 0106 —
    /// the self-assign lane). Only <see cref="Controllers.ProjectsController"/>
    /// passes <c>true</c>; all other composers leave the default (<c>false</c>)
    /// and do not set the key.
    /// </param>
    public static async Task SeedGrantPickerOptionsAsync(
        Controller controller,
        IUserInfoService userInfo,
        bool includeAssignUsers = false)
    {
        var profiles = await userInfo.GetProfilesAsync(verifiedOnly: true);
        var selfId = KumunitaPrincipal.SubjectId(controller.User);
        var userOptions = profiles
            .Where(p => !p.Blocked)
            .Where(p => !string.Equals(p.SubjectId, selfId, StringComparison.Ordinal))
            .Select(p => new GrantOption
            {
                Id    = p.SubjectId,
                Label = string.IsNullOrWhiteSpace(p.DisplayName) ? p.SubjectId : p.DisplayName,
                Kind  = "User",
            })
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groups = await userInfo.GetPublicGroupsAsync();
        var groupOptions = groups
            .Select(g => new GrantOption
            {
                Id    = g.Id,
                Label = string.IsNullOrWhiteSpace(g.Name) ? g.Id : g.Name,
                Kind  = "Group",
            })
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        controller.ViewData["Audience_Users"] = userOptions;
        controller.ViewData["Audience_Groups"] = groupOptions;

        // ADR 0106 — the **assign** people list includes the actor themself:
        // "Assign to…" should let a resident take a to-do onto themselves
        // (the self-assign lane). This is a *separate* list from the
        // grant-picker `Audience_Users` (which deliberately excludes self —
        // a grant of "addressed to me" is meaningless). Same verified/non-blocked
        // set, same order, self included.
        if (includeAssignUsers)
        {
            var assignUserOptions = profiles
                .Where(p => !p.Blocked)
                .Select(p => new GrantOption
                {
                    Id    = p.SubjectId,
                    Label = string.IsNullOrWhiteSpace(p.DisplayName) ? p.SubjectId : p.DisplayName,
                    Kind  = "User",
                })
                .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
            controller.ViewData["Assign_Users"] = assignUserOptions;
        }
    }

    /// <summary>
    /// Seeds the composer's <b>authored-in language</b> picker (ADR 0018,
    /// ADR 0005 B) — the instance's **enabled**
    /// <see cref="Kumunita.Core.Localization.LanguageCatalog"/>, ordered by
    /// <c>SortOrder</c>, read through
    /// <see cref="ILocalizationService.ListLanguagesAsync"/> (the HTTP-free
    /// seam, ADR 0005 D — the exact catalog read the
    /// <c>LocaleController.Index</c> page uses).
    /// </summary>
    public static async Task<IReadOnlyList<(string Code, string NativeName)>> SeedLanguagePickerAsync(
        ILocalizationService localization)
    {
        var catalog = await localization.ListLanguagesAsync().ConfigureAwait(false);
        return catalog
            .Where(l => l.Enabled)
            .OrderBy(l => l.SortOrder)
            .Select(l => (l.Id, l.NativeName))
            .ToList();
    }

    /// <summary>
    /// Seeds the composer's component *feed-organizer* picker (C-M3·2) —
    /// the instance's **enabled**
    /// <see cref="Kumunita.Core.UserInfo.Component"/> set (a *filter, never
    /// a gate*; the audience is the sole access boundary on the form).
    /// </summary>
    public static async Task<IReadOnlyList<(string Id, string Name)>> SeedComponentPickerAsync(
        IUserInfoService userInfo)
    {
        var components = await userInfo.GetComponentsAsync(enabledOnly: true);
        return components
            .Select(c => (c.Id, Name: string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
