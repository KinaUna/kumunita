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
        new("0.26.0", "2026-10-03", new List<string>
        {
            "Appearance — read the platform in a dark room: a dark theme (\"Forest\") now follows your device's appearance by default, and you can pin light or dark instead. The choice lives in the account menu (Appearance) and sticks per browser — a preference, not an account claim; pure CSS, so no first-paint flash and nothing the site's content-security policy would forbid (ADR 0133).",
        }),
        new("0.25.0", "2026-10-02", new List<string>
        {
            "Onboarding — a guided walk-through for your first sign-in: one page walks you through display name, avatar, interface language, time zone, date & time format, email & notification language, and contact details, each step linking into the setting that already owns it, and a dismissible reminder banner nudges you until you finish or skip (ADR 0132).",
        }),
        new("0.24.0", "2026-10-03", new List<string>
        {
            "Messaging, as a conversation — a 1:1 thread now reads as a chat: messages run oldest-to-newest, your own on the right and the other person's on the left, each with the sender's avatar; a \"Load earlier\" button pulls in older messages; and a message you send confirms with the other person's name (\"Sent to …\").",
        }),
        new("0.23.0", "2026-10-03", new List<string>
        {
            "Guides for every surface — every resident surface in the top navigation now has a how-to guide (announcements, directory, finding people, inventory, bookmarks, documents, pages, and tags, alongside the earlier guides), and the Help page now links all of them so it is the one place to start (ADR 0127).",
        }),
        new("0.22.0", "2026-10-03", new List<string>
        {
            "Attachment preview — the browser-displayable attachments (images, PDFs, plain text, and CSV) now open in a new tab and preview in your browser instead of forcing a download; Office files and zip archives still download. The preview is a separate tab — never embedded in the page — and every serve keeps the same stored type, the same nosniff lock, and the same one-row audit trail as a download (ADR 0126).",
        }),
        new("0.21.0", "2026-10-03", new List<string>
        {
            "Document editing — the owner of a document (its uploader) can update it: re-choose who may access it, and replace the file (the title and summary are editable too). Editing is owner-only — the uploader alone — and a document's identity and creation date are unchanged (ADR 0125).",
        }),
        new("0.20.0", "2026-10-02", new List<string>
        {
            "Search, expanded — the nav search box now finds across all ten resident content surfaces: the original four (posts, events, pages, announcements) plus projects, boards, to-dos, inventory items, documents, and people. A search now also matches the names of a post's, event's, page's, or to-do's own tags — a tag is a label, never a gate, so a tag-name match never widens what you can see, only what it finds. The people surface folds the directory's “who is here” into the same box (a search for a neighbour by name or bio word, signed-in only). Zero schema change (ADR 0124).",
        }),
        new("0.19.0", "2026-10-01", new List<string>
        {
            "Extended profiles — add a biography and your own tags (skills, interests, knowledge, expertise) to your profile, visible to the people your profile's visibility allows; find neighbours with something in common by tag or by a word in their bio — a match only surfaces people you can already see, and every such read is audited (ADR 0123).",
        }),
        new("0.18.0", "2026-10-01", new List<string>
        {
            "Documents — a shared repository for the community's official documents (contracts, minutes, notices, bylaws): each document is readable only by the people its uploader chose (an empty audience is a public denial), is served as a download (never rendered in the browser), and is audited every time it is opened; a GlobalAdmin or Moderator uploads a document and chooses who can see it (ADR 0122).",
        }),
        new("0.17.0", "2026-09-30", new List<string>
        {
            "Notification quiet hours — choose when your notification emails are held, in your own time zone: a per-resident schedule of quiet hours of day and days of week. Your emails are held (never lost) and arrive once the quiet window lifts, while your inbox always keeps the full record. An admin sets how often held notifications are re-checked (ADR 0121).",
        }),
        new("0.16.0", "2026-09-30", new List<string>
        {
            "Guest accounts — a GlobalAdmin settles a limited, time-bounded standing for a guest (a consultant, coach, teacher, speaker, or similar outside-the-resident-circle account): the bounded window the standing is live, and a closed set of read surfaces the guest may see (announcements, events, directory). A guest is never a full member — the standing is live only inside the window, settled by data, and read per request; one audited write lane, zero new authorization surface (ADR 0120).",
        }),
        new("0.15.0", "2026-09-30", new List<string>
        {
            "Recurring events — set up an event that repeats daily, weekly, monthly, or yearly (with an interval, and either a fixed number of occurrences or a last date). Each occurrence is its own concrete event, so RSVPs, reminders, the calendar, and the iCal feed all keep working per occurrence; the author can skip or restore a single occurrence without touching the rest of the series.",
        }),
        new("0.14.0", "2026-09-30", new List<string>
        {
            "Bookmarks — save the posts, events, to-dos, announcements, and pages you care about into one private list you can come back to: a one-click pin on each of those pages, your personal list grouped by type, and a pin that quietly greys out (never breaks, never leaks) when its target is removed.",
        }),
        new("0.13.0", "2026-09-29", new List<string>
        {
            "Inventory — check out and check in shared, community-owned, or private items (equipment, sports-team clothes, books, …): the item's page shows who has it now, and its usage history keeps a running record of every check-out — how much it's used and by whom.",
        }),
        new("0.12.0", "2026-09-29", new List<string>
        {
            "Translation bulk — check, update, and extend the platform's translations as a batch rather than one at a time: download the whole closed set as one CSV bundle, upload the edited bundle back (blank cells are skipped — they never erase a translation; a file with an unknown key or language is refused unchanged), and review or extend a new language with one save in the batch editor.",
        }),
        new("0.11.0", "2026-09-29", new List<string>
        {
            "Events & projects, linked — a to-do can now be for one event: the event page shows its linked to-dos, and each to-do shows its linked event with a picker to set it.",
            "To-dos on your calendar — dated to-dos can be added to a calendar app: an \"Add to calendar\" link on each to-do, plus a subscribable to-do calendar feed your calendar can keep up to date.",
        }),
        new("0.10.0", "2026-09-28", new List<string>
        {
            "Search — one /search surface (nav search box, anonymous + signed-in) over the four resident content surfaces: community + group posts, community + group events, pages, announcements.",
            "Messaging — 1:1 resident messaging: a signed-in resident opens a conversation with another resident, exchanges messages, and sees read state; an admin can enable or disable the feature instance-wide.",
            "PWA and responsive design — the site installs and behaves as an app on the device.",
            "Portability — import and export of the community's data for a self-hosted move.",
            "iCal — export your visible events to a calendar app: an \"Add to calendar\" link on each event, plus a subscribable \"Calendar feed (iCal)\" your calendar can keep up to date.",
            "Navigation layout — choose how the site's top navigation looks: a compact top row with a \"More\" menu (the default), or an icon rail beside a slim top bar; picked in the account menu, per browser.",
            "Navigation layout (top row) — in a narrow window the Community and Groups links now tuck into the \"More\" menu so the top row never needs a horizontal scrollbar; they pop back out when there is room.",
            "Logging and analytics — the operator's feedback loop: a dated file log sink (next to `docker logs`) and a GlobalAdmin-gated `/admin/analytics` surface with the surface-rank table, the distinct-account count over the last 7/30/90 days, and a CSV export.",
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
