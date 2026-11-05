using System.Security.Claims;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Localization;
using Kumunita.Core.Projects;
using Kumunita.Core.Query;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Localization;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;


/// <summary>
/// The /projects/goals actions (C08, ADR 0287 — the Goals route family of the
/// C08 Projects cluster): goal CRUD (create / read / edit / soft-delete).
/// Inherited from <see cref="ProjectsBaseController"/> (shared fields +
/// helpers) — the same C08 twin shape as <see cref="BoardsController"/> and
/// <see cref="TodosController"/>; the sibling <see cref="ProjectsController"/>
/// keeps the goals + projects landing (PL lane) and the standalone-project
/// CRUD. ADR 0290 (the C2-05 D5 seam — the Goals route family split off the
/// /projects/{projects,goals} controller).
/// </summary>
[Authorize]
public sealed class GoalsController : ProjectsBaseController
{

    // C08 (ADR 0287) — the child forwards the full seam set to the
    // <see cref="ProjectsBaseController"/> base (the boards / todos sibling
    // seams are supplied by DI; a test site may pass <c>null</c> for a seam it
    // does not exercise).
    public GoalsController(
        IProjectService projects,
        IUserInfoService userInfo, IGroupService groupService, IProfileSettingsService profileSettings,
        ILocalizationService localization,
        IDocumentStore store,
        IBoardService boards,
        ITodoService todos,
        ITranslationProvider? translationProvider = null,
        EffectiveTimezoneResolver? timezone = null,
        IEventService? events = null,
        ITagService? tags = null) : base(projects, userInfo, groupService, profileSettings, localization, store, boards, todos, translationProvider, timezone, events, tags)
    {
    }



    /// <summary>
    /// <c>GET /projects/goals/{id}</c> — the **goal detail** (the <c>PL</c>
    /// lane, ADR 0086 / design doc F2 / F5): the goal's <c>Title</c> +
    /// rendered-<c>Description</c> (the one <see cref="MarkdownRenderer"/> —
    /// ADR 0025), the audience line (the goal's <c>Audience</c> is
    /// <c>null</c> = public — ADR 0001-B / 0036; a display surface, never a
    /// gate — the audience decision already ran in the service's
    /// <see cref="IProjectService.GetGoalAsync"/> entry
    /// <c>CanAsync(Read)</c>), the **projects in this goal**
    /// (<see cref="IProjectService.ListProjectsForGoalAsync"/> — the
    /// per-parent list; a denied project is dropped, not the whole set),
    /// the <c>Created</c> date, the author, and the <see cref="CanEdit"/>
    /// standing preview (creator ∪ GlobalAdmin — C-PL·2, the ADR 0070
    /// board-edit precedent — **display-only**; the service's
    /// <see cref="IProjectService.UpdateGoalAsync"/> standing re-check is
    /// the enforcement, the frozen ADR 0006 split). A missing goal is 404,
    /// a denied actor 403 (the C3 split). The project cards link to
    /// <c>/projects/projects/{id}</c> (the **U07** route — the register's
    /// deliberate adjacent-unit relaxation: the href ships now, the target
    /// with U07).
    /// </summary>
    [HttpGet("/projects/goals/{id}")]
    public async Task<IActionResult> GoalDetail(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        ProjectGoal goal;
        try
        {
            goal = await projects.GetGoalAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // The projects in this goal (the U03 per-parent seam — a denied
        // project is dropped, not the whole set; **unpaged** — the small
        // per-parent list precedent).
        IReadOnlyList<Project> goalProjects;
        try
        {
            goalProjects = await projects.ListProjectsForGoalAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            // Unreachable in practice (the goal was just loaded readable) —
            // the C3 split, kept for the seam's contract.
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // Collect the subject ids (goal author + each project author) for
        // display-name resolution (a read, never a decision — the M5
        // BoardDetail idiom).
        var subjectIds = new[] { goal.AuthorId }
            .Concat(goalProjects.Select(p => p.AuthorId))
            .Where(a => a is not null && a.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var subjectId in subjectIds)
            names[subjectId] = await ResolveDisplayNameAsync(subjectId);

        var componentNames = await ResolveComponentNamesAsync();
        string? componentDisplayName =
            goal.ComponentId is not null && componentNames.TryGetValue(goal.ComponentId, out var cn) ? cn : null;

        string? DescriptionHtml(string? markdown) =>
            string.IsNullOrWhiteSpace(markdown) ? null : MarkdownRenderer.RenderHtml(markdown);

        var projectCards = goalProjects
            .Select(p => new GoalProjectCard(
                Id: p.Id,
                Title: p.Title,
                DescriptionHtml: DescriptionHtml(p.Description),
                Status: p.Status,
                StartAt: p.StartAt,
                DueAt: p.DueAt,
                AuthorId: p.AuthorId,
                AuthorDisplayName: names.GetValueOrDefault(p.AuthorId, p.AuthorId),
                ComponentId: p.ComponentId,
                ComponentDisplayName: p.ComponentId is not null && componentNames.TryGetValue(p.ComponentId, out var pn) ? pn : null,
                Created: p.Created,
                Modified: p.Modified))
            .ToList();

        var vm = new GoalDetailViewModel(
            Id: goal.Id,
            Title: goal.Title,
            DescriptionHtml: DescriptionHtml(goal.Description),
            AuthorId: goal.AuthorId,
            AuthorDisplayName: names.GetValueOrDefault(goal.AuthorId, goal.AuthorId),
            ComponentId: goal.ComponentId,
            ComponentDisplayName: componentDisplayName,
            LanguageCode: goal.LanguageCode,
            IsPublicAudience: goal.Audience is null,
            Projects: projectCards,
            // C-PL·2 — the standing preview (creator ∪ GlobalAdmin — the
            // ADR 0070 board-edit precedent): display-only, the service's
            // UpdateGoalAsync server-side re-check is the enforcement.
            CanEdit: !string.IsNullOrEmpty(actorId)
                     && (string.Equals(goal.AuthorId, actorId, StringComparison.Ordinal)
                         || RoleSet(User).Contains(Kumunita.Core.Identity.Roles.GlobalAdmin)),
            Created: goal.Created,
            Modified: goal.Modified);

        return View("GoalDetail", vm);
    }

    /// <summary>
    /// <c>GET /projects/goals/new</c> — the **goal composer** (any signed-in
    /// resident becomes the author — the <c>Owner</c> branch). Seeds the
    /// audience editor (the M2 single-source pin, ADR 0001-B — the sole
    /// access boundary), the authored-in language picker (ADR 0018), the
    /// component feed-organizer picker (C-M3·2), and the grant-picker option
    /// lists (the M2/M3/M4/M5 shared <c>_GrantPickers</c> partial — the
    /// <see cref="SeedGrantPickerOptionsAsync"/> / <see
    /// cref="SeedLanguagePickerAsync"/> / <see cref="SeedComponentPickerAsync"/>
    /// composer trio, reused not reinvented).
    /// </summary>
    [HttpGet("/projects/goals/new")]
    public async Task<IActionResult> GoalNew()
    {
        var model = new GoalComposerViewModel
        {
            Audience = new AudienceEditorModel
            {
                Mode = "Any",
                Grants = "[]",
                // Seeded like the board composer: the view's "Everyone" switch
                // renders from `Audience?.CommunityVisible ?? true`, which a
                // non-null `false` would defeat (the form opened author-only).
                CommunityVisible = true,
            },
            Languages = await ComposerSeedOptions.SeedLanguagePickerAsync(localization),
            // ADR 0018 / ADR 0049 — pre-select the actor's current effective
            // language so the picker highlights the right option and a
            // no-change submit is a concrete BCP-47 code (never an empty row).
            LanguageCode = await ResolveComposeDefaultLanguageAsync(),
            Components = await ComposerSeedOptions.SeedComponentPickerAsync(profileSettings),
        };
        await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);
        return View("GoalNew", model);
    }

    /// <summary>
    /// <c>POST /projects/goals</c> — the **goal create** write lane (any
    /// signed-in resident becomes the author; a refused write is a 403 — the
    /// C3 split). Validates the shape (<see
    /// cref="GoalComposerViewModel.IsValid"/>), writes through <see
    /// cref="IProjectService.CreateGoalAsync"/> (the service opens its own
    /// write session + audit row, C3), redirects to the new goal's
    /// <c>/projects/goals/{id}</c> (the M5 board-create redirect shape). The
    /// audience's <see cref="AudienceEditorModel.BuildAudience()"/> is the
    /// single deserialization site (the M2 single-source pin, ADR 0001-B).
    /// </summary>
    [HttpPost("/projects/goals")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GoalCreate([FromForm] GoalComposerViewModel model)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to create a goal.");
            return View("GoalNew", model);
        }

        // Re-seed the pickers so a failed-shape re-render below still shows
        // the language + component + grant options (the M5 board-create
        // shape).
        model.Languages = await ComposerSeedOptions.SeedLanguagePickerAsync(localization);
        model.Components = await ComposerSeedOptions.SeedComponentPickerAsync(profileSettings);
        await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);

        if (!model.IsValid)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "A title is required.");
            if (model.Audience is null || !model.Audience.IsValid)
                ModelState.AddModelError("Audience.Mode", "Audience mode is required (Any or All).");
            return View("GoalNew", model);
        }

        var request = new CreateGoalRequest
        {
            Title = model.Title!,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
            ComponentId = string.IsNullOrWhiteSpace(model.ComponentId) ? null : model.ComponentId,
            Audience = model.Audience.BuildAudience(), // ADR 0001-B — the single deserialization site.
            LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? null : model.LanguageCode,
        };

        ProjectGoal goal;
        try
        {
            goal = await projects.CreateGoalAsync(actorId, RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            ModelState.AddModelError(string.Empty, "You do not have permission to create a goal.");
            return View("GoalNew", model);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        TempData["info"] = await this.FlashAsync("projects.flash_goal_created", "Goal created.");
        return Redirect($"/projects/goals/{goal.Id}");
    }

    /// <summary>
    /// <c>GET /projects/goals/{id}/edit</c> — the **goal edit** page (ADR
    /// 0070 shape): the goal's own <c>Title</c> + <c>Description</c> (the
    /// <see cref="GoalComposerViewModel"/> shape — the same model the
    /// composer uses; the audience / component / language are creation-time
    /// choices, **not** editable here, ADR 0070). The goal is loaded through
    /// the frozen seam's <see cref="IProjectService.GetGoalAsync"/> (the
    /// audience gate is the service's single entry <c>CanAsync(Read)</c>) —
    /// a missing goal is 404, a denied actor 403 (the C3 split). The
    /// standing decision (creator ∪ GlobalAdmin) is the service's on write
    /// (the frozen ADR 0006 split); the form renders regardless of standing
    /// (the detail's <see cref="GoalDetailViewModel.CanEdit"/> gate is the
    /// display-only affordance — the M5 <c>BoardEditGet</c> shape).
    /// </summary>
    [HttpGet("/projects/goals/{id}/edit")]
    public async Task<IActionResult> GoalEdit(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        ProjectGoal goal;
        try
        {
            goal = await projects.GetGoalAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // The edit posts only Title + Description (ADR 0070) — the model
        // round-trips those two; the creation-time choices are not shown.
        var model = new GoalComposerViewModel
        {
            Title = goal.Title,
            Description = goal.Description,
        };
        ViewData["goalId"] = id; // the edit form's POST action (POST /projects/goals/{id}).
        return View("GoalEdit", model);
    }

    /// <summary>
    /// <c>POST /projects/goals/{id}</c> — the **goal update** write lane
    /// (ADR 0070 shape): a **full update** of the goal's <c>Title</c> +
    /// <c>Description</c> (a blank description clearing it to
    /// <c>null</c>) through <see cref="IProjectService.UpdateGoalAsync"/>.
    /// **Creator ∪ GlobalAdmin** over the goal (C-PL·2) — the service's
    /// server-side standing gate; a missing id is 404, a denied actor 403
    /// (the C3 split); a blank title is a form error (the M4 "a form is a
    /// shape" precedent — re-render the edit view with the error).
    /// Redirect-after-POST back to the goal.
    /// </summary>
    [HttpPost("/projects/goals/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GoalUpdate(string id, [FromForm] GoalComposerViewModel model)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        // The edit lane posts Title + Description only (ADR 0070) — Title is
        // the required field (the BoardUpdateModel.IsValid shape).
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Title), "A title is required.");
            return View("GoalEdit", model);
        }

        var request = new UpdateGoalRequest
        {
            Title = model.Title!,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
        };

        try
        {
            await projects.UpdateGoalAsync(id, actorId, RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (ArgumentException ex)
        {
            // A blank title (the service's 400) — a form error, not a 500.
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("GoalEdit", model);
        }

        TempData["info"] = await this.FlashAsync("projects.flash_goal_updated", "Goal updated.");
        return Redirect($"/projects/goals/{id}");
    }

    /// <summary>
    /// <c>POST /projects/goals/{id}/delete</c> — the **goal soft-delete**
    /// write lane (the ADR 0024 author-lane shape): sets the goal's
    /// <see cref="Kumunita.Core.Projects.ProjectGoal.IsDeleted"/> to
    /// <c>true</c>. **The D6 dangling-association rule (C-PL·6):** the goal's
    /// projects are **kept** — their <c>GoalId</c> is **not** cleared (the
    /// association simply dangles; the read lane's 404-on-soft-deleted
    /// behavior is the filter). **Creator ∪ GlobalAdmin** over the goal
    /// (C-PL·2) — the service's server-side standing gate; a missing id is
    /// 404, a denied actor 403 (the C3 split). Redirect-after-POST back to
    /// the U05 landing (<c>/projects</c>).
    /// </summary>
    [HttpPost("/projects/goals/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GoalDelete(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await projects.DeleteGoalAsync(id, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = await this.FlashAsync("projects.flash_goal_deleted", "Goal deleted.");
        return Redirect("/projects");
    }

}
