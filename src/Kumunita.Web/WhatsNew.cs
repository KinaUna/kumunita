namespace Kumunita.Web;

/// <summary>
/// The platform's version history, as shown in the "What's new" section of the
/// About page and announced once per version by the dismissible toast (the
/// <c>VN</c> lane, ADR 0110). This is source-controlled static data — not
/// configuration — because versions are decided in the repo, not per-deployment,
/// following the <see cref="Milestones"/> precedent.
///
/// Keep this in sync with the shipped surface: when a named lane or milestone
/// ships, add its version (or extend the newest one) here. The <see cref="All"/>
/// list is ordered newest-first; <see cref="Latest"/> is its head. Version
/// dates are fixed, timezone-independent ISO dates (they are a fact about when
/// the release shipped, not a user-visible timestamp — so no <c>kw-dt</c>).
/// </summary>
public static class WhatsNew
{
    public sealed record Release(string Version, string Date, IReadOnlyList<string> Changes);

    public static IReadOnlyList<Release> All { get; } = new List<Release>
    {
        new("1.0.0", "2026-09-28", new List<string>
        {
            "Search — one /search surface (nav search box, anonymous + signed-in) over the four resident content surfaces: community + group posts, community + group events, pages, announcements.",
            "Messaging — 1:1 resident messaging: a signed-in resident opens a conversation with another resident, exchanges messages, and sees read state; an admin can enable or disable the feature instance-wide.",
            "PWA and responsive design — the site installs and behaves as an app on the device.",
            "Portability — import and export of the community's data for a self-hosted move.",
            "iCal — event calendars available as a standard iCal feed.",
            "The \"What's new\" section on the About page and the version toast — you are reading this.",
        }),
        new("0.9.0", "2026-09-18", new List<string>
        {
            "Notifications — residents get notified about the things they opted into.",
            "Pagination and filtering across the longer lists.",
        }),
        new("0.8.0", "2026-09-14", new List<string>
        {
            "Projects — goals, tasks, and contributors the whole community can see and join.",
        }),
        new("0.7.0", "2026-09-12", new List<string>
        {
            "Events, RSVPs & reminders — post an event, RSVP to it, and get a day-before reminder email.",
            "Events calendar — a month-anchored overview of your visible events with prev/next/today navigation.",
            "Calendar day/week/month views — Day, Week (Monday-start, time-ruler), and Month views over the same authorized events.",
            "Calendar quick-create — the calendar defaults to the week view; a \"New event\" button and clicking an empty slot opens a quick-create modal.",
        }),
        new("0.6.0", "2026-09-09", new List<string>
        {
            "Pages — a hierarchical, audience-restricted, translatable knowledge tree for the community's durable content.",
            "User guides — resident-facing how-to guides in the help subtree of the page tree.",
            "Tags — free author-set subject labels on posts and pages; browse by tag, autocomplete as you type.",
        }),
        new("0.5.0", "2026-09-06", new List<string>
        {
            "Rich editor — a WYSIWYG split-view + Markdown-splice toolbar over the Markdown lane.",
            "Rich content — Markdown bodies + in-content images on posts, replies, announcements & static pages.",
            "Guardian controls — a parent adds a child's account and supervises it at the account level (suspend, communities/groups, invitation approval); no standing to read the child's private content.",
            "Guardian assignment — an existing guardian assigns a second guardian to a child's account.",
        }),
        new("0.4.0", "2026-09-03", new List<string>
        {
            "Multilingual, live UI — every in-scope view resolves per request; seeded en floor; key-managed admin editor; public language picker.",
            "Languages seeded — German & French ship enabled on first boot with complete UI baselines.",
            "System pages shipped — About, Terms, Help, Privacy, and Code of conduct ship complete in en/de/fr.",
            "Timezone — platform default + per-resident override; timestamps rendered in the effective zone.",
            "Date & time format — platform default + per-resident override + custom format.",
            "Translator role — delegate translation editing to non-admin residents.",
        }),
        new("0.3.0", "2026-08-30", new List<string>
        {
            "Posts & announcements — a community feed with group-scoped posts and a pinned-announcement lane.",
            "Moderation + reports — residents report content; moderators act within their scope.",
            "Group posts — the membership-scoped post channel inside a group.",
        }),
        new("0.2.0", "2026-08-28", new List<string>
        {
            "Directory of residents, profile visibility & group management — the first view of your neighbours and the groups they form.",
        }),
        new("0.1.0", "2026-08-25", new List<string>
        {
            "Identity, groups, delegation & the authorization model — accounts, sign-in, and the thin-token / fat-authorization core the platform is built on.",
        }),
    };

    public static Release Latest => All[0];
}
