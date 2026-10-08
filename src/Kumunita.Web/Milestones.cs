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
        new("GP", "Group posts — the membership-scoped post channel inside a group", StatusDone),
        new("ML", "Multilingual — UI & platform texts (terms, about, help) translatable; admin manages languages", StatusDone),
        new("ML-UI", "Multilingual — live UI: every in-scope view resolves per request; seeded English floor; key-managed admin editor; public language picker", StatusDone),
        new("LS", "Languages seeded — German and French ship enabled on first boot with complete UI plus about/terms/help baselines", StatusDone),
        new("SP", "System pages shipped — About, Terms, Help, Privacy and Code of conduct ship complete in English, German and French, reachable from the footer", StatusDone),
        new("TZ", "Timezone — platform default (admin) + per-resident override; timestamps rendered in the effective zone", StatusDone),
        new("DF", "Date & time format — platform default (admin) + per-resident override + custom; timestamps rendered in the effective format", StatusDone),
        new("TR", "Translator role — delegate translation editing to non-admin residents; UI strings + static pages open to GlobalAdmin and Translator", StatusDone),
        new("RC", "Rich content — Markdown bodies + in-content images on posts, replies, announcements and static pages", StatusDone),
        new("GU", "Guardian controls — a parent adds a child's account and supervises it at the account level (suspend, communities/groups, invitation approval); no standing to read the child's private content", StatusDone),
        new("GA", "Guardian assignment — an existing guardian invites a second guardian to a child's account (email-driven); the invited guardian accepts (with the same consent to the child-account terms the creating guardian accepts) or declines, and holds no standing over the child until they accept", StatusDone),
        new("RE", "Rich editor — a WYSIWYG split-view + Markdown-splice toolbar, with no external editor dependency", StatusDone),
        new("TG", "Tags — free author-set subject labels on posts (community + group) + blog pages; creator-owned per-language display names; by-tag browse + autocomplete (a tag is a label, never a gate)", StatusDone),
        new("PG", "Pages — a hierarchical, audience-restricted, translatable knowledge tree (absorbs and retires the legacy static pages)", StatusDone),
        new("UG", "User guides — resident-facing how-to guides in the `help/` subtree of the page tree (English floor; a where / when / how-often structure)", StatusDone),
        new("M4", "Events, RSVPs & reminders — a day-before reminder email; RSVPs with audience controls", StatusDone),
        new("EV-CAL", "Events calendar — a month-anchored overview of the caller's visible events over a rolling 30-day window (overlap pairs highlighted; prev/next/today navigation to go back in time); display-only", StatusDone),
        new("EV-DWM", "Events calendar day/week/month views — Day, Week (Monday-start, time-ruler), and Month (true calendar month) views over the same authorized events; a `?view=` selector", StatusDone),
        new("EV-NW", "Events calendar quick-create + week default — the calendar defaults to the week view (was month); a \"New event\" button + clicking an empty slot opens a quick-create modal (title + start + end + location)", StatusDone),
        new("M5", "Projects — goals, tasks, contributors", StatusDone),
        new("M6", "Notifications", StatusDone),
        new("M7", "Pagination and filtering", StatusDone),
        new("M8", "Search — one /search surface (nav search box, anonymous + signed-in) over the ten resident content surfaces: posts, events, pages, announcements, projects, boards, todos, inventory, documents, people; tag-name match; single-surface paged, group scope", StatusDone),
        new("M9", "Messaging — 1:1 resident messaging: a signed-in resident opens a conversation with another resident, exchanges messages, and sees read state; an admin can enable or disable the feature instance-wide", StatusDone),
        new("M10", "PWA and responsive design", StatusDone),
        new("M11", "Portability (import/export)", StatusDone),
        new("M12", "iCal", StatusDone),
        new("M13", "Logging and analytics", StatusDone),
        new("M14", "Integration of Events and Projects", StatusDone),
        new("M15", "Translation bulk — import/export, review & extend the platform's translations as a batch rather than one at a time", StatusDone),
        new("M16", "Inventory — check-out / check-in shared, community-owned, or private items (equipment, sports-team clothes, books, …); track where items are and, optionally, who uses them how much", StatusDone),
        new("M17", "Bookmarks — save posts, events, todos, etc. for quick personal access", StatusDone),
        new("M18", "Recurring events — repeating events over the M4 events surface", StatusDone),
        new("M19", "Guest accounts — limited-privilege accounts for consultants, coaches, teachers, speakers, entertainers, etc.; admins set what a guest may access and when", StatusDone),
        new("M20", "Notification quiet times — per-resident quiet schedules (allowed/blocked hours of day and days of week) on the notifications feature; admin-set check cadence for pending notifications", StatusDone),
        new("M21", "Document management — a shared repository for official documents, contracts, etc., with per-document access controls", StatusDone),
        // M23 shipped before M22: M23 (extended profiles) was pulled forward and completed while M22 (onboarding) remained planned; M22 (onboarding) then shipped, and M24 (storage metrics) shipped after it. The order M20, M21, M23, M22, M24, M25 is pinned by MilestonesTests.cs — do not reorder (the "named lane, not a renumber" precedent).
        new("M23", "Extended user profiles — a biography + free author-set tags (skills, interests, knowledge, expertise) to make it easier to find people with something in common", StatusDone),
        new("M22", "Onboarding — a guided walk-through that walks a new user through account setup", StatusDone),
        new("M24", "Storage metrics — an admin view of storage: total used space, available space, user-content used space, and space used per user", StatusDone),
        new("M25", "Upload limits — admin-set per-file size limit and per-user total content quota; residents see their own usage and how much of their quota remains", StatusDone),
        new("M26", "Sorting — feeds, lists & search results are sortable by various properties in increasing or decreasing order", StatusDone),
        new("M27", "User-scoped portability — a resident exports / backs up their own data; on import they manually resolve conflicts, choosing per entity to add it elsewhere or discard it (imports may not fit the community's structure and authorization)", StatusDone),
        new("SITE", "Site content customization — the landing surfaces' hero text is admin-editable + the sections are show/hide (ADR 0150)", StatusDone),
        new("M28", "Guardian time limits — for a child's account, a parent/guardian sets when the child may use the platform: allow or block certain hours of each day and days of the week", StatusDone),
    };

    public static string BadgeClass(string status) => status switch
    {
        StatusDone => "text-bg-success",
        StatusNext => "text-bg-primary",
        StatusPlanned => "text-bg-secondary",
        _ => "text-bg-light text-dark",
    };
}
