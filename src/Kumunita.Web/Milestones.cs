namespace Kumunita.Web;

/// <summary>
/// The project's milestone roadmap, as shown on the public home page while the
/// site is still under active development. This is source-controlled static
/// data — not configuration — because the roadmap is decided in the repo
/// (README.md, docs/ARCHITECTURE.md), not per-deployment.
///
/// Keep this list in sync with the "Roadmap" section of README.md: bump
/// <c>Status</c> to "done" for a shipped milestone and move the next one to
/// "next" when that milestone's work begins.
/// </summary>
public static class Milestones
{
    public sealed record Entry(string Id, string Title, string Status);

    public const string StatusDone = "done";
    public const string StatusNext = "status-next";
    public const string StatusPlanned = "planned";

    public static IReadOnlyList<Entry> All { get; } = new List<Entry>
    {
        new("M0", "Deployable scaffold — solution, Docker, Coolify, live DB", StatusDone),
        new("M1", "Identity, groups, delegation & the authorization model", StatusDone),
        new("M2", "Directory of residents, profile visibility & group management", StatusDone),
        new("M3", "Posts & announcements in components; moderation + reports", StatusNext),
        new("M4", "Events, RSVPs & reminders", StatusPlanned),
        new("M5", "Projects — goals, tasks, contributors", StatusPlanned),
        new("M6", "Portability (export/import), iCal, notifications, search, multilingual support", StatusPlanned),
    };

    public static string LabelFor(string status) => status switch
    {
        StatusDone => "Done",
        StatusNext => "In progress",
        StatusPlanned => "Planned",
        _ => status,
    };

    /// <summary>
    /// Bootstrap badge css-class for a milestone's status.
    /// </summary>
    public static string BadgeClass(string status) => status switch
    {
        StatusDone => "text-bg-success",
        StatusNext => "text-bg-primary",
        StatusPlanned => "text-bg-secondary",
        _ => "text-bg-light text-dark",
    };
}
