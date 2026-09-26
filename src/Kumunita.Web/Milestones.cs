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
        new("LS", "Languages seeded — German & French ship enabled on first boot with complete UI + about/terms/help baselines (en stays default & the only code-owned language; ADR 0042)", StatusDone),
        new("SP", "System pages shipped — the five platform surfaces (About, Terms, Help, Privacy, Code of conduct) ship complete in en/de/fr and are reachable from the UI: an unconditional footer Platform column (all five) + the /admin Platform pages section (preview + edit); Privacy and Code of conduct are the two new seeded pages, and about stays the product-story view, not a Markdown page (ADR 0043)", StatusDone),
        new("TZ", "Timezone — platform default (admin) + per-resident override; timestamps rendered in the effective zone (ADR 0019)", StatusDone),
        new("DF", "Date & time format — platform default (admin) + per-resident override + custom; timestamps rendered in the effective format (ADR 0020)", StatusDone),
        new("TR", "Translator role — delegate translation editing to non-admin residents; UI strings + static pages open to GlobalAdmin ∪ Translator (ADR 0021)", StatusDone),
        new("RC", "Rich content — Markdown bodies + in-content images on posts, replies, announcements & static pages (ADR 0025)", StatusDone),
        new("GU", "Guardian controls — a parent adds a child's account and supervises it at the account level (suspend, communities/groups, invitation approval); no standing to read the child's private content (ADR 0028)", StatusDone),
        new("GA", "Guardian assignment — an existing guardian assigns a second guardian to a child's account (email-driven; one IIdentityService ADD + one GuardianController action + the Detail view's assign form + ADR 0038)", StatusDone),
        new("RE", "Rich editor — a WYSIWYG split-view + Markdown-splice toolbar over the RC Markdown lane; tsc-only, no editor dependency (ADR 0031)", StatusDone),
        new("TG", "Tags — free author-set subject labels on posts (community + group) + blog pages; creator-owned per-language display names (Creator ∪ GlobalAdmin reword); one access-scoped read seam for the tag list + by-tag browse + autocomplete (a tag is a label, never a gate — it reuses the content's own `Read` decision); the `TG` named lane (ADR 0044)", StatusDone),
        new("PG", "Pages — a hierarchical, audience-restricted, translatable knowledge tree (a `Page` + `PageTranslation` doc; reuses the `Audience` doc, the frozen `IAuthorizationService` via a `PageToAuditableResource` adapter, the ADR 0022/0027/0029 translation lane, the ADR 0025/0031/0033 WYSIWYG editor; a `MountPoint` string for UI slots; absorbs and retires the legacy static-page lane; the `PG` named lane; ADR 0039)", StatusDone),
        new("UG", "User guides — resident-facing how-to guides in the `help/` subtree of the page tree (en-only floor; the three-layer consistency loop: where / when / how-often; the `UG` named lane; ADR 0057)", StatusDone),
        new("M4", "Events, RSVPs & reminders (a `Event` + `EventRsvp` doc in a new `Kumunita.Core.Events` context; RSVPs + a day-before reminder email (the `EventReminders` §6.4 job); reuses the `Audience` doc, the frozen `IAuthorizationService` via an `EventToAuditableResource` adapter, the ADR 0025/0031 WYSIWYG editor, the ADR 0019/0020 `kw-dt` timestamps, and the M1 durable-email trio; ADR 0054)", StatusDone),
        new("EV-CAL", "Events calendar — a month-anchored overview of the caller's visible events over a rolling 30-day window (overlap pairs highlighted; prev/next/today navigation to go back in time); one additive read seam on `IEventService` + one view; display-only, zero document changes (ADR 0063)", StatusDone),
        new("EV-DWM", "Events calendar day/week/month views — Day, Week (Monday-start, time-ruler), and Month (true calendar month) views over the same authorized events; one additive `?view=` selector on the existing route + per-view window in the controller; zero Core change (ADR 0064)", StatusDone),
        new("EV-NW", "Events calendar quick-create + week default — the calendar defaults to the week view (was month); a \"New event\" button + clicking an empty slot opens a quick-create modal (title + start + end + location) that posts the existing `/events/new` route with a `QuickCreate` shape signal; month grid tightens to single-line separators; zero Core / schema change (ADR 0081)", StatusDone),
        new("M5", "Projects — goals, tasks, contributors", StatusDone),
        new("M6", "Notifications", StatusDone),
        new("M7", "Pagination and filtering", StatusDone),
        new("M8", "Search — one /search surface (nav search box, anonymous + signed-in) over the four resident content surfaces: community + group posts, community + group events, pages, announcements; `all` top-5 per surface, single-surface paged on the M7 HasMore/_Pager discipline, group scope on the frozen ADR 0013 seams, zero schema change (ADR 0091)", StatusDone),
        new("M9", "PWA and responsive design", StatusNext),
        new("M10", "Portability (import/export)", StatusPlanned),
        new("M11", "iCal", StatusPlanned),
        new("M12", "Logging and analytics", StatusPlanned),
        new("M13", "Integration of Events and Projects", StatusPlanned),
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
