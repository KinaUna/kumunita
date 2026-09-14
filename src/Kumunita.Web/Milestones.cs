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
        new("M3", "Posts & announcements in components; moderation + reports", StatusDone),
        new("GP", "Group posts — the membership-scoped post channel inside a group (ADR 0013)", StatusDone),
        new("ML", "Multilingual — UI & platform texts (terms, about, help) translatable; admin manages languages (ADR 0005)", StatusDone),
        new("ML-UI", "Multilingual — live UI: every in-scope view resolves per request; seeded en floor; key-managed admin editor; public language picker (ADR 0015)", StatusDone),
        new("TZ", "Timezone — platform default (admin) + per-resident override; timestamps rendered in the effective zone (ADR 0019)", StatusDone),
        new("DF", "Date & time format — platform default (admin) + per-resident override + custom; timestamps rendered in the effective format (ADR 0020)", StatusDone),
        new("TR", "Translator role — delegate translation editing to non-admin residents; UI strings + static pages open to GlobalAdmin ∪ Translator (ADR 0021)", StatusDone),
        new("RC", "Rich content — Markdown bodies + in-content images on posts, replies, announcements & static pages (ADR 0025)", StatusDone),
        new("GU", "Guardian controls — a parent adds a child's account and supervises it at the account level (suspend, communities/groups, invitation approval); no standing to read the child's private content (ADR 0028)", StatusNext),
        new("M4", "Events, RSVPs & reminders", StatusPlanned),
        new("M5", "Projects — goals, tasks, contributors", StatusPlanned),
        new("M6", "Portability (export/import), iCal, notifications, search", StatusPlanned),
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
