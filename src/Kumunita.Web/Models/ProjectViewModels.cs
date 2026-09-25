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
