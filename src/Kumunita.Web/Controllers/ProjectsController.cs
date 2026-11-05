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
/// The /projects/{projects,goals} actions (C08, ADR 0287): goal/project
/// CRUD, the goal project picker, and project translations. Inherited
/// from <see cref="ProjectsBaseController"/> (shared fields + helpers).
/// </summary>
[Authorize]
public sealed class ProjectsController : ProjectsBaseController
{

    // C08 (ADR 0287) — the child forwards the full seam set to the
    // <see cref="ProjectsBaseController"/> base. The boards / todos sibling
    // seams are supplied by DI (all three are registered in
    // <c>DependencyInjection</c>); the board / todo actions in this controller
    // resolve against them. A test site may pass <c>null</c> for a seam it does
    // not exercise.
    public ProjectsController(
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
    /// <c>GET /projects</c> — the **goals + projects landing** (the <c>PL</c>
    /// lane, ADR 0086 D8 / design doc §5 F9): a two-section feed — the
    /// <see cref="ProjectGoal"/>s (the <see
    /// cref="IProjectService.ListGoalsAsync"/> <c>CanSeeAsync(Read)</c> gate
    /// is the sole reader) **then** the <b>standalone</b>
    /// <see cref="Project"/>s (the <see cref="IProjectService.ListProjectsAsync"/>
    /// <c>goalId == null</c> feed — the projects with <c>GoalId == null</c>;
    /// the FACES are the service's, the controller's <c>ForbidResult</c> /
    /// <c>NotFound</c> split is the C3 pin only). The
    /// <paramref name="componentId"/> query is a *filter, never a gate*
    /// (C-M3·2) — passed to both feeds verbatim. Author + component display
    /// names are *read* lookups (never access decisions — the M5 feed
    /// actions' idiom).
    /// <para>
    /// The card's detail links (<c>/projects/goals/{id}</c> /
    /// <c>/projects/projects/{id}</c>) and the <c>new</c> composer links are
    /// the **U06 / U07 routes** — inert until those units ship them (the
    /// register's deliberate adjacent-unit relaxation, the lane plan's
    /// sequencing note). The <c>/projects/todos</c> + <c>/projects/boards</c>
    /// M5 routes are untouched (C-PL·7).
    /// </para>
    /// </summary>
    [HttpGet("/projects")]
    public async Task<IActionResult> ProjectsIndex(string? componentId, int page = 1, string? sort = null, string? dir = null)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        // M26 U12 (C-SORT·3) — the landing's **two** sections (goals +
        // standalone projects) each parse the same ?sort=/?dir= request
        // params against **their own** allowlist (rows 9–10 — the identical
        // created/modified/title set), mirroring the U11 group-detail
        // dual-section rule: a surface-unknown key falls back to that
        // section's default order.
        var goalsSort = ParseSort(sort, dir, ProjectFeedAllowedKeys);
        var projectsSort = ParseSort(sort, dir, ProjectFeedAllowedKeys);

        IReadOnlyList<ProjectGoal> goals;
        IReadOnlyList<Project> standaloneProjects;
        GoalPage goalsPage;
        ProjectPage projectsPage;
        // The resident's items-per-page preference (FeedPaging resolves
        // Profile.PageSize → PageSizer default/clamp); a no-actor read
        // uses the platform default. Passed to the seam (the Skip/Take
        // window) and the pager's PageSize.
        int pageSize = await FeedPaging.PageSizeAsync(userInfo, actorId);
        try
        {
            goalsPage = await projects.ListGoalsAsync(componentId, actorId, page, ct: HttpContext.RequestAborted, sort: goalsSort, pageSize: pageSize); // ADR 0090 D1/D3 — the paging signal is the page's .HasMore.
            goals = goalsPage.Items;
            // The landing's projects section is the **standalone** feed
            // (the `goalId == null` filter — the D8 / design doc §5 pin;
            // a goal's projects are the goal detail's (U06) surface, not
            // this page's).
            projectsPage = await projects.ListProjectsAsync(componentId, null, actorId, page, ct: HttpContext.RequestAborted, sort: projectsSort, pageSize: pageSize); // ADR 0090 D1/D3 — the paging signal is the page's .HasMore.
            standaloneProjects = projectsPage.Items;
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // ADR 0088 / ADR 0051 — the feed translation swap: the viewer's
        // current-language translation of each **project** (title +
        // description) swaps in over the authored-in value (the Event feed
        // idiom). ProjectGoal is out of scope (the ADR 0088 follow-on note),
        // so only the standalone projects are swapped. A read convenience
        // (the audience decision already ran in ListProjectsAsync); a no-op
        // when no provider is wired.
        if (translationProvider is not null)
        {
            string effLang = await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider);
            foreach (var p in standaloneProjects)
                await ApplyProjectTranslationAsync(p, effLang);
        }

        var authorIds = goals
            .Select(g => g.AuthorId)
            .Concat(standaloneProjects.Select(p => p.AuthorId))
            .Where(a => a is not null && a.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var subjectId in authorIds)
            names[subjectId] = await ResolveDisplayNameAsync(subjectId);

        var componentNames = await ResolveComponentNamesAsync();

        string? ComponentDisplay(string? componentId) =>
            componentId is not null && componentNames.TryGetValue(componentId, out var n) ? n : null;
        static string? DescriptionHtml(string? markdown) =>
            string.IsNullOrWhiteSpace(markdown) ? null : MarkdownRenderer.RenderHtml(markdown);

        var goalCards = goals
            .Select(g => new GoalCard(
                Id: g.Id,
                Title: g.Title,
                DescriptionHtml: DescriptionHtml(g.Description),
                AuthorId: g.AuthorId,
                AuthorDisplayName: names.GetValueOrDefault(g.AuthorId, g.AuthorId),
                ComponentId: g.ComponentId,
                ComponentDisplayName: ComponentDisplay(g.ComponentId),
                Created: g.Created,
                Modified: g.Modified))
            .ToList();

        var projectCards = standaloneProjects
            .Select(p => new ProjectCard(
                Id: p.Id,
                Title: p.Title,
                DescriptionHtml: DescriptionHtml(p.Description),
                Status: p.Status,
                StartAt: p.StartAt,
                DueAt: p.DueAt,
                AuthorId: p.AuthorId,
                AuthorDisplayName: names.GetValueOrDefault(p.AuthorId, p.AuthorId),
                ComponentId: p.ComponentId,
                ComponentDisplayName: ComponentDisplay(p.ComponentId),
                Created: p.Created,
                Modified: p.Modified))
            .ToList();

        var vm = new ProjectsIndexViewModel(
            Goals: goalCards,
            StandaloneProjects: projectCards,
            ComponentPickerOptions: await ComposerSeedOptions.SeedComponentPickerAsync(profileSettings),
            CurrentComponentId: componentId,
            CurrentPage: page,
            // M7 (ADR 0090 D5) — the two paged sections' pagers (F2 one-page
            // no-render pin): each null on a single page so the _Pager partial
            // renders nothing. The community filter is carried across prev/next
            // (D7) as a FilterParams pair on each. The M26 U12 (C-SORT·8)
            // sort/dir pairs join **each section's own** FilterParams only when
            // the request carried them (C-SORT·3); an unsorted read stays
            // byte-identical to pre-M26 (C-SORT·2). **Dual-pager key split** —
            // the two sections' `BaseUrl` is the *same* `/projects` (confirmed
            // against the pre-M26 code: both `PagedViewModel.ForRoute("/projects", …)`),
            // so the same `sort`/`dir` query keys ride each section's own
            // pager without a collision (the brief's "if the split is by
            // BaseUrl" branch does not apply — both sections read the request's
            // single `?sort=`/`?dir=` pair, the U11 dual-section idiom).
            PagerGoals: (goalsPage.HasMore || page > 1)
                ? PagedViewModel.ForRoute("/projects", page, pageSize, goalsPage.HasMore,
                    BuildLandingFilterParams(componentId, sort, dir))
                : null,
            PagerProjects: (projectsPage.HasMore || page > 1)
                ? PagedViewModel.ForRoute("/projects", page, pageSize, projectsPage.HasMore,
                    BuildLandingFilterParams(componentId, sort, dir))
                : null,
            // M26 U12 (D-SORT·5) — the one shared sort control (the U10
            // _Sort reference, reused verbatim — C-SORT·1), **per section**:
            // the goals section (row 9) and the projects section (row 10)
            // each pass their own closed allowlist (the identical
            // created/modified/title set). Renders nothing when a field
            // is null (the no-sort pin).
            SortGoals: SortViewModel.ForRoute(
                "/projects",
                currentKey: goalsSort?.Key,
                currentDir: goalsSort is { } g ? (g.Descending ? "desc" : "asc") : null,
                options: [
                    ("created", "desc"),
                    ("modified", "desc"),
                    ("title", "asc"),
                ]),
            SortProjects: SortViewModel.ForRoute(
                "/projects",
                currentKey: projectsSort?.Key,
                currentDir: projectsSort is { } ps ? (ps.Descending ? "desc" : "asc") : null,
                options: [
                    ("created", "desc"),
                    ("modified", "desc"),
                    ("title", "asc"),
                ]));

        return View("ProjectsIndex", vm);
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
    /// <c>GET /projects/projects/{id}</c> — the **project detail** (the
    /// <c>PL</c> lane, ADR 0086 / design doc F5 / D10): the project's
    /// <c>Title</c> + rendered-<c>Description</c> (the one
    /// <see cref="MarkdownRenderer"/> — ADR 0025), the optional
    /// <c>Status</c> badge (C-PL·4 — a string, not an enum), the ADR 0079
    /// optional <c>StartAt</c> / <c>DueAt</c> dates (C-PL·5), the audience
    /// line (the project's <c>Audience</c> is <c>null</c> = public — ADR
    /// 0001-B / 0036; a display surface, never a gate — the audience decision
    /// already ran in the service's <see
    /// cref="IProjectService.GetProjectAsync"/> entry <c>CanAsync(Read)</c>),
    /// the **goal link** (D10 — present only when the project's <c>GoalId</c>
    /// is non-null **and** the target goal is non-deleted + readable by the
    /// actor, via <see cref="IProjectService.GetGoalAsync"/> — a denied /
    /// missing goal leaves the link out), the **associated to-dos / boards**
    /// (the <see cref="IProjectService.ListTodosAsync"/> / <see
    /// cref="IProjectService.ListBoardsAsync"/> feeds narrowed by the U04
    /// <c>projectId</c> filter — C-PL·3, a feed filter, never a gate), the
    /// <c>Created</c> date, the author, and the <see cref="CanEdit"/> standing
    /// preview (creator ∪ GlobalAdmin — C-PL·2, the ADR 0070 board-edit
    /// precedent — **display-only**; the service's <see
    /// cref="IProjectService.UpdateProjectAsync"/> standing re-check is the
    /// enforcement, the frozen ADR 0006 split). A missing project is 404, a
    /// denied actor 403 (the C3 split).
    /// </summary>
    [HttpGet("/projects/projects/{id}")]
    public async Task<IActionResult> ProjectDetail(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        Project project;
        try
        {
            project = await projects.GetProjectAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // The goal link (D10): only when the project has a GoalId and the
        // target goal is non-deleted + readable by the actor (a denied or
        // missing goal leaves both null — the view omits the link entirely).
        string? goalId = null;
        string? goalTitle = null;
        if (project.GoalId is not null && project.GoalId.Length > 0)
        {
            try
            {
                var goal = await projects.GetGoalAsync(project.GoalId, actorId, HttpContext.RequestAborted);
                goalId = goal.Id;
                goalTitle = goal.Title;
            }
            catch (KeyNotFoundException)
            {
                // The goal was soft-deleted (the dangling-association rule —
                // D6 / C-PL·6) — the project's GoalId is kept but the link is
                // not shown. Not a 404 for the project itself (C3: the goal
                // read is a display surface, the project was just loaded).
            }
            catch (UnauthorizedAccessException)
            {
                // The actor cannot read the goal — the link is hidden, not a
                // 403 for the project (the project's own audience gate already
                // ran; the goal's is a display-surface read).
            }
        }

        // The associated to-dos + boards (the U04 projectId feed filter —
        // C-PL·3, a feed filter, never a gate; a denied item is dropped, not
        // the whole set). Both are **unpaged** (page 1) — the small
        // per-parent list precedent (the goal detail's projects list).
        IReadOnlyList<TodoItem> todos;
        try
        {
            todos = (await this.todos.ListTodosAsync(null, null, actorId, 1, unassignedOnly: false, projectId: id, ct: HttpContext.RequestAborted)).Items; // ADR 0090 D1/D3
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        IReadOnlyList<KanbanBoard> boards;
        try
        {
            boards = (await this.boards.ListBoardsAsync(null, actorId, 1, projectId: id, ct: HttpContext.RequestAborted)).Items; // ADR 0090 D1/D3
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // Collect the subject ids (project author + each associated to-do
        // author) for display-name resolution (a read, never a decision).
        var subjectIds = new[] { project.AuthorId }
            .Concat(todos.Select(t => t.AuthorId))
            .Concat(boards.Select(b => b.AuthorId))
            .Where(a => a is not null && a.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var subjectId in subjectIds)
            names[subjectId] = await ResolveDisplayNameAsync(subjectId);

        var componentNames = await ResolveComponentNamesAsync();
        string? componentDisplayName =
            project.ComponentId is not null && componentNames.TryGetValue(project.ComponentId, out var cn) ? cn : null;

        string? DescriptionHtml(string? markdown) =>
            string.IsNullOrWhiteSpace(markdown) ? null : MarkdownRenderer.RenderHtml(markdown);

        var todoCards = todos
            .Select(t => new ProjectAssociatedTodoCard(
                Id: t.Id,
                Title: t.Title,
                Status: t.Status,
                AuthorId: t.AuthorId,
                AuthorDisplayName: names.GetValueOrDefault(t.AuthorId, t.AuthorId),
                StartAt: t.StartAt,
                DueAt: t.DueAt,
                Created: t.Created))
            .ToList();

        var boardCards = boards
            .Select(b => new ProjectAssociatedBoardCard(
                Id: b.Id,
                Title: b.Title,
                DescriptionHtml: DescriptionHtml(b.Description),
                AuthorId: b.AuthorId,
                AuthorDisplayName: names.GetValueOrDefault(b.AuthorId, b.AuthorId),
                Created: b.Created))
            .ToList();

        // ADR 0088 — the project's user-added translations (the ADR 0027
        // chip-swap + ADR 0049 default-visible-variant + ADR 0022 add-form
        // shape). A "a read, not a decision" surface; the parent project's
        // single Read decision already ran in GetProjectAsync (C-M3·1).
        var projectTranslations = await projects.GetProjectTranslationsAsync(id);

        var vm = new ProjectDetailViewModel(
            Id: project.Id,
            Title: project.Title,
            DescriptionHtml: DescriptionHtml(project.Description),
            Status: project.Status,
            StartAt: project.StartAt,
            DueAt: project.DueAt,
            AuthorId: project.AuthorId,
            AuthorDisplayName: names.GetValueOrDefault(project.AuthorId, project.AuthorId),
            ComponentId: project.ComponentId,
            ComponentDisplayName: componentDisplayName,
            LanguageCode: project.LanguageCode,
            IsPublicAudience: project.Audience is null,
            GoalId: goalId,
            GoalTitle: goalTitle,
            Todos: todoCards,
            Boards: boardCards,
            // C-PL·2 — the standing preview (creator ∪ GlobalAdmin — the
            // ADR 0070 board-edit precedent): display-only, the service's
            // UpdateProjectAsync server-side re-check is the enforcement.
            CanEdit: !string.IsNullOrEmpty(actorId)
                     && (string.Equals(project.AuthorId, actorId, StringComparison.Ordinal)
                         || RoleSet(User).Contains(Kumunita.Core.Identity.Roles.GlobalAdmin)),
            Created: project.Created,
            Modified: project.Modified,
            // ADR 0088 — the project's user-added translations (the ADR 0027
            // chip-swap + ADR 0049 default-visible-variant + ADR 0022 add-form
            // shape). A "a read, not a decision" surface; the parent project's
            // single Read decision already ran in GetProjectAsync (C-M3·1).
            // CanTranslate is the ADR 0088 display pin (creator ∪ Translator ∪
            // GlobalAdmin) — the real gate is the server-side re-check in the
            // Add/Update/Remove lanes at POST.
            Translations: projectTranslations,
            Languages: LanguageOptionsFrom(projectTranslations, await ComposerSeedOptions.SeedLanguagePickerAsync(localization), t => t.LanguageCode),
            CanTranslate: !string.IsNullOrEmpty(actorId)
                          && ProjectTranslationPolicy.CanAddProjectTranslation(project.AuthorId, actorId, RoleSet(User)),
            OriginalLanguageCode: project.LanguageCode);

        return View("ProjectDetail", vm);
    }


    /// <summary>
    /// <c>GET /projects/projects/new</c> — the **project composer** (any
    /// signed-in resident becomes the author — the <c>Owner</c> branch).
    /// Seeds the audience editor (the M2 single-source pin, ADR 0001-B — the
    /// sole access boundary), the authored-in language picker (ADR 0018), the
    /// component feed-organizer picker (C-M3·2), the **goal picker** (D10 —
    /// the actor's readable, non-deleted goals, a display surface never a
    /// gate), and the grant-picker option lists (the M2/M3/M4/M5 shared
    /// <c>_GrantPickers</c> partial).
    /// </summary>
    [HttpGet("/projects/projects/new")]
    public async Task<IActionResult> ProjectNew()
    {
        var model = new ProjectComposerViewModel
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
            Goals = await SeedGoalPickerAsync(),
        };
        await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);
        return View("ProjectNew", model);
    }


    /// <summary>
    /// <c>POST /projects/projects</c> — the **project create** write lane (any
    /// signed-in resident becomes the author; a refused write is a 403 — the
    /// C3 split). Validates the shape (<see
    /// cref="ProjectComposerViewModel.IsValid"/>), writes through
    /// <see cref="IProjectService.CreateProjectAsync"/> (the service opens its
    /// own write session + audit row, C3; the <c>GoalId</c> guard refuses a
    /// soft-deleted / unreadable goal, the C3 split), and redirects to the
    /// new project's <c>/projects/projects/{id}</c>. The audience's
    /// <see cref="AudienceEditorModel.BuildAudience()"/> is the single
    /// deserialization site (the M2 single-source pin, ADR 0001-B).
    /// </summary>
    [HttpPost("/projects/projects")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProjectCreate([FromForm] ProjectComposerViewModel model)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to create a project.");
            return View("ProjectNew", model);
        }

        // Re-seed the pickers so a failed-shape re-render below still shows
        // the language + component + goal + grant options (the M5
        // board-create shape).
        model.Languages = await ComposerSeedOptions.SeedLanguagePickerAsync(localization);
        model.Components = await ComposerSeedOptions.SeedComponentPickerAsync(profileSettings);
        model.Goals = await SeedGoalPickerAsync();
        await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);

        if (!model.IsValid)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "A title is required.");
            if (model.Audience is null || !model.Audience.IsValid)
                ModelState.AddModelError("Audience.Mode", "Audience mode is required (Any or All).");
            return View("ProjectNew", model);
        }

        // ADR 0079 / ADR 0019 — convert wall-clock to UTC before the write.
        var zone = await ResolveZoneAsync();
        var startUtc = model.StartAt is not null ? new DateTimeOffset(model.StartAt.Value, zone.GetUtcOffset(model.StartAt.Value)).UtcDateTime : (DateTime?)null;
        var dueUtc   = model.DueAt   is not null ? new DateTimeOffset(model.DueAt.Value,   zone.GetUtcOffset(model.DueAt.Value)).UtcDateTime   : (DateTime?)null;
        var request = new CreateProjectRequest
        {
            Title = model.Title!,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
            GoalId = string.IsNullOrWhiteSpace(model.GoalId) ? null : model.GoalId,
            Status = string.IsNullOrWhiteSpace(model.Status) ? null : model.Status,
            StartAt = startUtc is not null ? new DateTimeOffset(startUtc.Value, TimeSpan.Zero) : null,
            DueAt   = dueUtc   is not null ? new DateTimeOffset(dueUtc.Value,   TimeSpan.Zero) : null,
            ComponentId = string.IsNullOrWhiteSpace(model.ComponentId) ? null : model.ComponentId,
            Audience = model.Audience.BuildAudience(), // ADR 0001-B — the single deserialization site.
            LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? null : model.LanguageCode,
        };

        Project created;
        try
        {
            created = await projects.CreateProjectAsync(actorId, RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            ModelState.AddModelError(string.Empty, "You do not have permission to create a project.");
            return View("ProjectNew", model);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        TempData["info"] = await this.FlashAsync("projects.flash_project_created", "Project created.");
        return Redirect($"/projects/projects/{created.Id}");
    }


    /// <summary>
    /// <c>GET /projects/projects/{id}/edit</c> — the **project edit** page
    /// (ADR 0070 shape): the project's <c>Title</c> + <c>Description</c>
    /// (the ADR 0070 full-update surface), plus the D10 lane's editable
    /// <c>GoalId</c> / <c>Status</c> / <c>StartAt</c> / <c>DueAt</c> (the
    /// <see cref="IProjectService.UpdateProjectAsync"/> partial-update seam —
    /// a blank date posts <c>null</c>, ADR 0079; a blank status clears it,
    /// C-PL·4; the goal picker lets the project be re-associated or un-goal'd
    /// (the <c>ClearGoal</c> flag when no goal is chosen)). The project is
    /// loaded through the frozen seam's <see cref="IProjectService
    /// .GetProjectAsync"/> (the audience gate is the service's single entry
    /// <c>CanAsync(Read)</c>) — a missing project is 404, a denied actor 403
    /// (the C3 split). The standing decision (creator ∪ GlobalAdmin) is the
    /// service's on write (the frozen ADR 0006 split); the form renders
    /// regardless of standing (the detail's <see cref="ProjectDetailViewModel
    /// .CanEdit"/> gate is the display-only affordance).
    /// </summary>
    [HttpGet("/projects/projects/{id}/edit")]
    public async Task<IActionResult> ProjectEdit(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        Project project;
        try
        {
            project = await projects.GetProjectAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // The edit posts Title + Description (the ADR 0070 full-update shape)
        // + the D10 editable GoalId / Status / StartAt / DueAt (the partial
        // update shape — a blank date → null, a blank status → clear, and the
        // goal picker's empty choice → ClearGoal). The model round-trips
        // those; the creation-time choices (audience / community / language)
        // are not shown.
        // ADR 0079 / ADR 0019 — the stored StartAt / DueAt are UTC instants;
        // convert to wall-clock in the actor's effective zone so the form
        // shows the local time the author chose.
        var zone = await ResolveZoneAsync();
        var model = new ProjectComposerViewModel
        {
            Title = project.Title,
            Description = project.Description,
            GoalId = project.GoalId,
            Status = project.Status,
            StartAt = project.StartAt is not null ? TimeZoneInfo.ConvertTime(project.StartAt.Value, zone).DateTime : null,
            DueAt   = project.DueAt   is not null ? TimeZoneInfo.ConvertTime(project.DueAt.Value,   zone).DateTime : null,
            Goals = await SeedGoalPickerAsync(),
        };
        ViewData["projectId"] = id; // the edit form's POST action (POST /projects/projects/{id}).
        return View("ProjectEdit", model);
    }


    /// <summary>
    /// <c>POST /projects/projects/{id}</c> — the **project update** write
    /// lane (the ADR 0070 full-update shape for <c>Title</c> /
    /// <c>Description</c> + the D10 partial update for <c>GoalId</c> /
    /// <c>Status</c> / <c>StartAt</c> / <c>DueAt</c>): a blank title is a
    /// form error (re-render the edit view with the error); a blank
    /// description clears it to <c>null</c>; a blank status clears it
    /// (C-PL·4); a blank date posts <c>null</c> (ADR 0079); the goal picker's
    /// empty choice is the <c>ClearGoal</c> explicit un-goal (D10). **Creator
    /// ∪ GlobalAdmin** over the project (C-PL·2) — the service's server-side
    /// standing gate; a missing id is 404, a denied actor 403 (the C3 split).
    /// Redirect-after-POST back to the project.
    /// </summary>
    [HttpPost("/projects/projects/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProjectUpdate(string id, [FromForm] ProjectComposerViewModel model)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        // The edit lane posts Title + Description (the required fields) + the
        // D10 optional GoalId / Status / dates. Title is the required field
        // (the BoardUpdateModel.IsValid shape).
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Title), await this.FlashAsync("validation.title_required", "A title is required."));
            return View("ProjectEdit", model);
        }

        var goalChosen = !string.IsNullOrWhiteSpace(model.GoalId);
        // ADR 0079 / ADR 0019 — convert wall-clock to UTC before the write.
        var zone = await ResolveZoneAsync();
        var startUtc = model.StartAt is not null ? new DateTimeOffset(model.StartAt.Value, zone.GetUtcOffset(model.StartAt.Value)).UtcDateTime : (DateTime?)null;
        var dueUtc   = model.DueAt   is not null ? new DateTimeOffset(model.DueAt.Value,   zone.GetUtcOffset(model.DueAt.Value)).UtcDateTime   : (DateTime?)null;
        var request = new UpdateProjectRequest
        {
            Title = model.Title!,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
            GoalId = goalChosen ? model.GoalId : null,
            ClearGoal = !goalChosen, // the goal picker's empty choice is an explicit un-goal (D10).
            Status = string.IsNullOrWhiteSpace(model.Status) ? null : model.Status,
            StartAt = startUtc is not null ? new DateTimeOffset(startUtc.Value, TimeSpan.Zero) : null,
            DueAt   = dueUtc   is not null ? new DateTimeOffset(dueUtc.Value,   TimeSpan.Zero) : null,
        };

        try
        {
            await projects.UpdateProjectAsync(id, actorId, RoleSet(User), request, HttpContext.RequestAborted);
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
            return View("ProjectEdit", model);
        }

        TempData["info"] = await this.FlashAsync("projects.flash_project_updated", "Project updated.");
        return Redirect($"/projects/projects/{id}");
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


    /// <summary>
    /// <c>POST /projects/projects/{id}/delete</c> — the **project
    /// soft-delete** write lane (the ADR 0024 author-lane shape): sets the
    /// project's <see cref="Kumunita.Core.Projects.Project.IsDeleted"/> to
    /// <c>true</c>. **The D6 dangling-association rule (C-PL·6):** the
    /// project's to-dos / boards are **kept** — their <c>ProjectId</c> is
    /// **not** cleared (the association simply dangles; the read lane's
    /// 404-on-soft-deleted behavior is the filter). **Creator ∪
    /// GlobalAdmin** over the project (C-PL·2) — the service's server-side
    /// standing gate; a missing id is 404, a denied actor 403 (the C3
    /// split). Redirect-after-POST back to the U05 landing
    /// (<c>/projects</c>).
    /// </summary>
    [HttpPost("/projects/projects/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProjectDelete(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await projects.DeleteProjectAsync(id, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = await this.FlashAsync("projects.flash_project_deleted", "Project deleted.");
        return Redirect("/projects");
    }


    /// <summary>
    /// Adds a **user-added translation** of the project into
    /// <paramref name="languageCode"/> (ADR 0088):
    /// <c>POST /projects/projects/{id}/translations</c>. A thin Web lane (ADR
    /// 0006-D) delegating to
    /// <see cref="IProjectService.ProjectTranslationLane"/> (ADR 0288).
    /// <para>
    /// <b>Precondition (C-M3·1):</b> the viewer must be able to see the project
    /// (re-run the parent's single <c>Read</c> decision via
    /// <see cref="IProjectService.GetProjectAsync"/> — a
    /// <c>KeyNotFoundException</c> is a 404, a
    /// <c>UnauthorizedAccessException</c> a 403 — the C3 non-leaky posture).
    /// </para>
    /// </summary>
    [HttpPost("/projects/projects/{id}/translations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProjectTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode,
        [FromForm] string? title,
        [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return new ForbidResult();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_choose_language", "Choose a language for the translation.");
            return Redirect($"/projects/projects/{id}");
        }
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_needs_title_or_text", "A translation needs a title or some text.");
            return Redirect($"/projects/projects/{id}");
        }

        try
        {
            await projects.GetProjectAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        try
        {
            // C2-02 (ADR 0288) — the shared write lane (M3: controller owns the
            // session, C3 one SaveChangesAsync); the descriptor normalizes title/body.
            await using var session = store.LightweightSession();
            await this.projects.ProjectTranslationLane.AddAsync(session, id, languageCode, title, body, actorId, RoleSet(User));
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = await this.FlashAsync("flash.translation_added", "Translation added ({0}).", name);
        return Redirect($"/projects/projects/{id}");
    }


    /// <summary>
    /// **Updates** the existing user-added translation of the project in
    /// <paramref name="languageCode"/> (ADR 0088, the ADR 0048 edit-lane
    /// shape): <c>POST /projects/projects/{id}/translations/update</c>. Thin
    /// Web lane; delegates to
    /// <see cref="IProjectService.ProjectTranslationLane"/> (ADR 0288).
    /// Precondition + failure shapes mirror <see cref="AddProjectTranslation"/>.
    /// </summary>
    [HttpPost("/projects/projects/{id}/translations/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProjectTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode,
        [FromForm] string? title,
        [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return new ForbidResult();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_choose_language", "Choose a language for the translation.");
            return Redirect($"/projects/projects/{id}");
        }
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_needs_title_or_text", "A translation needs a title or some text.");
            return Redirect($"/projects/projects/{id}");
        }

        try
        {
            await projects.GetProjectAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        try
        {
            // C2-02 (ADR 0288) — the shared write lane (M3: controller owns the
            // session, C3 one SaveChangesAsync); the descriptor normalizes title/body.
            await using var session = store.LightweightSession();
            await this.projects.ProjectTranslationLane.UpdateAsync(session, id, languageCode, title, body, actorId, RoleSet(User));
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = await this.FlashAsync("flash.translation_updated", "Translation updated ({0}).", name);
        return Redirect($"/projects/projects/{id}");
    }


    /// <summary>
    /// **Removes** the existing user-added translation of the project in
    /// <paramref name="languageCode"/> (ADR 0088, the ADR 0048 remove-lane
    /// shape): <c>POST /projects/projects/{id}/translations/remove</c>. Thin
    /// Web lane; delegates to
    /// <see cref="IProjectService.ProjectTranslationLane"/> (ADR 0288). Failure
    /// shapes mirror <see cref="AddProjectTranslation"/>.
    /// </summary>
    [HttpPost("/projects/projects/{id}/translations/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveProjectTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return new ForbidResult();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_choose_language", "Choose a language for the translation.");
            return Redirect($"/projects/projects/{id}");
        }

        try
        {
            await projects.GetProjectAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        try
        {
            // C2-02 (ADR 0288) — the shared write lane (M3: controller owns the
            // session, C3 one SaveChangesAsync).
            await using var session = store.LightweightSession();
            await this.projects.ProjectTranslationLane.RemoveAsync(session, id, languageCode, actorId, RoleSet(User));
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = await this.FlashAsync("flash.translation_removed", "Translation removed ({0}).", name);
        return Redirect($"/projects/projects/{id}");
    }
}
