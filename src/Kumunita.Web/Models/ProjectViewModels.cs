using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Kumunita.Web.Security;

namespace Kumunita.Web.Models;

/// <summary>
/// One <see cref="Kumunita.Core.Projects.ProjectGoal"/> as a <b>feed card</b>
/// (the <c>GET /projects</c> landing's goals section — the PL lane, ADR 0086;
/// the M5 <see cref="TodoRow"/> / <see cref="BoardRow"/> card shape).
/// <para>
/// **<see cref="DescriptionHtml"/>** is the goal's optional Markdown body
/// pre-rendered to an HTML fragment through the one
/// <see cref="MarkdownRenderer"/> (the ADR 0025 shape — the <b>rendered</b>
/// card body, not the WYSIWYG <c>rc-editor</c>); <c>null</c> / empty input
/// renders <c>null</c> (the view omits the block). It is **safe, sanitized
/// HTML** — the view renders it with <c>Html.Raw</c> (the
/// <c>Page/Show.cshtml</c> idiom), never with <c>@</c> interpolation.
/// </para>
/// <para>
/// **No** per-card project count: there is no count seam in the lane's frozen
/// <c>IProjectService</c> surface — the card's "View projects →" link to the
/// goal detail (U06, where the projects-in-this-goal list lives via
/// <c>ListProjectsForGoalAsync</c>) is the affordance (the design doc §5
/// card-shape pin).
/// </para>
/// <para>
/// The audience gate is the service's (the feed's single
/// <c>CanSeeAsync(Read)</c> — C-PL·1); <see cref="AuthorDisplayName"/> is a
/// **read** lookup via <c>IUserInfoService.GetProfileAsync</c> (never an
/// access decision), falling back to the raw id (null-safe).
/// </para>
/// </summary>
public sealed record GoalCard(
    string Id,
    string Title,
    string? DescriptionHtml,
    string AuthorId,
    string AuthorDisplayName,
    string? ComponentId,
    string? ComponentDisplayName,
    DateTimeOffset Created,
    DateTimeOffset? Modified);

/// <summary>
/// One standalone <see cref="Kumunita.Core.Projects.Project"/> as a
/// <b>feed card</b> (the <c>GET /projects</c> landing's standalone-projects
/// section — the <c>GoalId == null</c> feed, the design doc §5 second
/// section). The M5 <see cref="BoardRow"/> card shape + the ADR 0079
/// optional-date pair.
/// <para>
/// **<see cref="DescriptionHtml"/>** is the project's optional Markdown body
/// pre-rendered through the one <see cref="MarkdownRenderer"/> (the ADR 0025
/// shape); <c>null</c> when the project has no description (the view omits
/// the block). Safe HTML — rendered with <c>Html.Raw</c>, never
/// <c>@</c>.
/// </para>
/// <para>
/// **<see cref="Status"/>** is the project's nullable state label (C-PL·4 —
/// a string, **not** an enum; <c>null</c> = none — the C-M5·4 pin carried
/// over; rendered verbatim as a badge). **<see cref="StartAt"/> /
/// <see cref="DueAt"/>** are the ADR 0079 optional dates (C-PL·5;
/// <c>null</c> = no date) — display metadata only; the <kw-dt> TagHelper
/// renders them in the effective timezone and the view gates each label on
/// non-null.
/// </para>
/// </summary>
public sealed record ProjectCard(
    string Id,
    string Title,
    string? DescriptionHtml,
    string? Status,
    DateTimeOffset? StartAt,
    DateTimeOffset? DueAt,
    string AuthorId,
    string AuthorDisplayName,
    string? ComponentId,
    string? ComponentDisplayName,
    DateTimeOffset Created,
    DateTimeOffset? Modified);

/// <summary>
/// The <b>goals + projects landing</b> view model (the <c>GET /projects</c>
/// read surface — the <see cref="Kumunita.Core.Projects.ProjectGoal"/> feed
/// + the **standalone** <see cref="Kumunita.Core.Projects.Project"/> feed,
/// the <c>GoalId == null</c> rows — the design doc §5 two-section order,
/// goals first; the M5 <see cref="TodoIndexViewModel"/> /
/// <see cref="BoardIndexViewModel"/> shape).
/// <para>
/// **<see cref="ComponentPickerOptions"/>** (the filter picker's options) is
/// the enabled <c>Component</c> set (a *filter, never a gate* — C-M3·2);
/// <see cref="CurrentComponentId"/> is the selected filter (null ⇒
/// unfiltered). <see cref="CurrentPage"/> is the current page number
/// (1-based).
/// </para>
/// </summary>
public sealed record ProjectsIndexViewModel(
    IReadOnlyList<GoalCard> Goals,
    IReadOnlyList<ProjectCard> StandaloneProjects,
    IReadOnlyList<(string Id, string Name)> ComponentPickerOptions,
    string? CurrentComponentId,
    int CurrentPage);

/// <summary>
/// One <see cref="Kumunita.Core.Projects.Project"/> in the **goal detail's
/// "projects in this goal" section** (the <c>GET /projects/goals/{id}</c>
/// surface — the <see cref="Kumunita.Core.Projects.IProjectService
/// .ListProjectsForGoalAsync"/> per-parent list, the U06 / ADR 0086 lane).
/// The same card fields as the landing's <see cref="ProjectCard"/> (the
/// frozen surface — the M5 <see cref="BoardRow"/> card shape), minus the
/// goal association (implicit: they are all under this goal).
/// <para>
/// **<see cref="DescriptionHtml"/>** is the project's optional Markdown body
/// pre-rendered through the one <see cref="MarkdownRenderer"/> (the ADR
/// 0025 shape); <c>null</c> when the project has no description (the view
/// omits the block). Safe HTML — rendered with <c>Html.Raw</c>, never
/// <c>@</c>.
/// </para>
/// </summary>
public sealed record GoalProjectCard(
    string Id,
    string Title,
    string? DescriptionHtml,
    string? Status,
    DateTimeOffset? StartAt,
    DateTimeOffset? DueAt,
    string AuthorId,
    string AuthorDisplayName,
    string? ComponentId,
    string? ComponentDisplayName,
    DateTimeOffset Created,
    DateTimeOffset? Modified);

/// <summary>
/// The **goal detail** view model (the <c>GET /projects/goals/{id}</c> read
/// surface — the <see cref="Kumunita.Core.Projects.ProjectGoal"/> doc fields
/// + the **projects in this goal** list, the U06 / ADR 0086 lane; the M5
/// <see cref="BoardDetailViewModel"/> header shape — title + status-line,
/// the description block, the <see cref="CanEdit"/> standing preview, the
/// <see cref="Kumunita.Web.Security.MarkdownRenderer"/>-rendered body).
/// <para>
/// **<see cref="DescriptionHtml"/>** is the goal's optional Markdown body
/// pre-rendered to an HTML fragment through the one <see
/// cref="MarkdownRenderer"/> (the ADR 0025 shape — the **rendered** detail
/// body, not the WYSIWYG <c>rc-editor</c>); <c>null</c> / empty input
/// renders <c>null</c> (the view omits the block and shows the
/// <c>pl.goal.empty_description</c> hint). Safe, sanitized HTML — rendered
/// with <c>Html.Raw</c>, never with <c>@</c>.
/// </para>
/// <para>
/// **<see cref="Projects"/>** is the goal's non-deleted projects the actor
/// may read (the <see cref="Kumunita.Core.Projects.IProjectService
/// .ListProjectsForGoalAsync"/> per-parent list — a denied project is
/// dropped, not the whole set; **unpaged** — the small per-parent list
/// precedent). Each card links to <c>/projects/projects/{id}</c> (the U07
/// route — the register's deliberate adjacent-unit relaxation: the href
/// ships now, the target with U07).
/// </para>
/// <para>
/// **<see cref="CanEdit"/>** is the **standing preview** (creator ∪
/// GlobalAdmin — C-PL·2, the ADR 0070 board-edit precedent) — a
/// **display-only** mirror of the service's server-side standing re-check
/// in <see cref="Kumunita.Core.Projects.IProjectService.UpdateGoalAsync"/>
/// (the frozen ADR 0006 split: the service is the enforcement, the
/// controller's flag is the affordance).
/// </para>
/// </summary>
public sealed record GoalDetailViewModel(
    string Id,
    string Title,
    string? DescriptionHtml,
    string AuthorId,
    string AuthorDisplayName,
    string? ComponentId,
    string? ComponentDisplayName,
    string LanguageCode,
    /// <summary>
    /// true when the goal's <see
    /// cref="Kumunita.Core.Authorization.Audience"/> is <c>null</c> (public
    /// — visible to everyone; the ADR 0001-B / 0036 shape). The view renders
    /// the <c>pl.goal.audience_public</c> / <c>pl.goal.audience_restricted</c>
    /// line from this flag (a display surface, never a gate — the audience
    /// decision already ran in the service's <c>GetGoalAsync</c> entry gate).
    /// </summary>
    bool IsPublicAudience,
    IReadOnlyList<GoalProjectCard> Projects,
    bool CanEdit,
    DateTimeOffset Created,
    DateTimeOffset? Modified);

/// <summary>
/// The **goal composer** form model — shared by <b>both</b> the
/// <c>GET /projects/goals/new</c> + <c>POST /projects/goals</c> lanes
/// (the <see cref="GoalComposerViewModel"/> is the new/edit
/// single-source shape; the M5 <see cref="BoardEditorModel"/> composer /
/// <see cref="BoardUpdateModel"/> edit split is the board's
/// <c>Title</c> + <c>Description</c> full-update surface — the goal edit
/// (ADR 0070) posts exactly those two fields and the composer's
/// creation-time choices (audience / community / language) are **not**
/// editable, so the goal lane shares one model across new + edit, the M5
/// precedent the design doc pins).
/// <para>
/// <list type="bullet">
/// <item><see cref="Title"/> — the goal's label (non-empty — the
/// <see cref="Kumunita.Core.Projects.CreateGoalRequest.Title"/> /
/// <see cref="Kumunita.Core.Projects.UpdateGoalRequest.Title"/> row).</item>
/// <item><see cref="Description"/> — optional Markdown (a goal is usable
/// title-only; the one <c>MarkdownRenderer</c> / <c>bindRichEditor</c>,
/// ADR 0025 / 0031); a blank value clears it to <c>null</c> on both lanes
/// (the create-path normalization + the ADR 0070 full-update shape).</item>
/// <item><see cref="ComponentId"/> — a feed organizer (C-M3·2: a filter,
/// never a gate) — **creation-time only** (not editable, ADR 0070).</item>
/// <item><see cref="Audience"/> — the M2 reusable
/// <see cref="AudienceEditorModel"/> (the single-source pin;
/// <see cref="AudienceEditorModel.BuildAudience()"/> is the one
/// deserialization site; ADR 0001-B) — the **sole** access boundary on the
/// form, **creation-time only**.</item>
/// <item><see cref="LanguageCode"/> — the authored-in tag (ADR 0018) —
/// **creation-time only**.</item>
/// </list>
/// </para>
/// </summary>
public sealed class GoalComposerViewModel
{
    /// <summary>The goal's display label (the feed's title — the
    /// <see cref="Kumunita.Core.Projects.ProjectGoal.Title"/> row).
    /// Required.</summary>
    [Required(ErrorMessage = "A title is required.")]
    public string? Title { get; set; }

    /// <summary>The goal's optional rich description (Markdown — the one
    /// <see cref="Kumunita.Web.Security.MarkdownRenderer"/>, edited by the
    /// one <c>bindRichEditor</c>; a goal is usable title-only; a blank
    /// value clears it).</summary>
    public string? Description { get; set; }

    /// <summary>The feed organizer (a <c>Component</c> id) — a
    /// <b>filter, never a gate</b> (C-M3·2) — creation-time only (ADR 0070:
    /// not editable on the edit lane).</summary>
    public string? ComponentId { get; set; }

    /// <summary>The goal's **audience** editor — the M2 reusable
    /// <see cref="AudienceEditorModel"/> (the single-source pin; the
    /// **sole** access boundary on the form — the <see
    /// cref="ComponentId"/> is a filter, never a gate) — creation-time
    /// only (ADR 0070: not editable on the edit lane).</summary>
    public AudienceEditorModel Audience { get; set; } = new();

    /// <summary>The authored-in language (ADR 0018) — creation-time only;
    /// empty/unset is materialized from the instance default server-side at
    /// write time.</summary>
    public string? LanguageCode { get; set; }

    /// <summary>The composer's language *picker* options — the instance's
    /// **enabled** catalog, ordered by <c>SortOrder</c> (the
    /// <c>SeedLanguagePickerAsync</c> seed). <b>[BindNever]</b> — the form
    /// POSTs a <see cref="LanguageCode"/>, not a catalog-list shape.</summary>
    [BindNever]
    public IReadOnlyList<(string Code, string NativeName)> Languages { get; set; } = [];

    /// <summary>The composer's component *picker* options — the enabled
    /// <c>Component</c> set (the <c>SeedComponentPickerAsync</c> seed).
    /// <b>[BindNever]</b> — the form POSTs a <see cref="ComponentId"/>.</summary>
    [BindNever]
    public IReadOnlyList<(string Id, string Name)> Components { get; set; } = [];

    /// <summary>
    /// true when the model is well-formed for a round-trip. <see
    /// cref="Title"/> is required (a goal with no title is a malformed
    /// shape, not a silent blank row); on the **new** lane <see
    /// cref="Audience"/> is well-formed (the editor's <see
    /// cref="AudienceEditorModel.IsValid"/> — a missing mode is a malformed
    /// post, not a silent default — C1). The edit lane (ADR 0070) posts
    /// only <see cref="Title"/> + <see cref="Description"/> and the
    /// controller validates <see cref="Title"/> alone (the <see
    /// cref="BoardUpdateModel.IsValid"/> shape).
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Title))
                return false;
            if (Audience is null || !Audience.IsValid)
                return false;
            return true;
        }
    }
}
