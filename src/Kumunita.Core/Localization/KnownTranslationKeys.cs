using System.Collections.Generic;

namespace Kumunita.Core.Localization;

/// <summary>
/// The canonical, closed set of platform UI-string keys and their <c>en</c>
/// source text (ADR 0005; the <c>ML-UI</c> U1 deliverable — D2, the
/// curated bounded registry).
/// <para>
/// <b>Single source of truth.</b> Three readers, one shape:
/// </para>
/// <list type="number">
/// <item>The <b>seeder</b> (<c>FirstBootSeeder</c>, U1) materializes each key as
/// an <c>en</c> <see cref="TranslationResource"/> row on first boot — this is
/// what makes M·9's "<c>en</c> floor" and M·12's completeness view real the
/// moment a fresh instance boots.</item>
/// <item>The <b>TagHelper</b> (<c>kw-l</c>, U2) and the in-scope views (U2–U5)
/// emit a string through <see cref="ITranslationProvider"/> by this key — a
/// resident with a <c>pl</c> preference sees the <c>pl</c> row, falling back
/// per string to <c>en</c> then to this registry's own <c>en</c> source text
/// (the provider floor, ADR 0015 D1); an unregistered key falls back to the
/// raw key (M·1/M·2).</item>
/// <item>The <b>admin editor</b> (U6) lists <see cref="AllKeys"/> as the closed
/// set with each key's <c>en</c> value as the reference text — no hand-typed
/// key (the plan's Gap 3 fix).</item>
/// </list>
/// <para>
/// <b>Scope discipline (M·3 / the plan's "in-scope surface").</b> This is the
/// <b>curated platform surface only</b> — the shared layout (nav, footer,
/// language-picker labels) plus the page headings, primary action labels, and
/// empty-states of the named resident/admin pages. The full-sweep amendment
/// (ADR 0015, 2026-09-12) extended this to the <b>entire</b> platform UI
/// surface — the completeness universe now equals exactly what the views
/// emit through <c>kw-l</c>. Hard exclusions for the whole set:
/// </para>
/// <ul>
/// <li><b>Never a UGC key</b> (M·3): a post/reply body, a group description, an
/// announcement body, or an author's display name is authored and rendered as
/// written — it is <b>not</b> here.</li>
/// <li><b>Not the setup flow</b> (<c>AdminSetup/Setup</c> — first-boot,
/// single-admin, pre-community) or the FAQ placeholder — recorded in ADR
/// 0015 (full-sweep amendment). The <c>about</c> product-story landing was
/// likewise excluded there but is now <b>in scope</b>: its 17 <c>about.*</c>
/// keys are registered below (ADR 0042 D5, 2026-09-18 — the exclusion is
/// superseded for <c>about</c>, held for the FAQ placeholder).</li>
/// <li><b>Not HTML attributes, JS strings, or C#-built markup</b>:
/// <c>aria-label</c>/<c>title</c> attributes that embed an inlined value,
/// <c>confirm()</c> dialogs, and strings embedding inline <c>&lt;code&gt;</c>
/// API identifiers or inlined data values are out of the TagHelper's reach and
/// stay hardcoded — registering a key that could drift from the rendered
/// output would make this registry a lie.
/// <b>Exception (ADR 0072):</b> a small set of *simple, value-free*
/// <c>placeholder</c> attributes that are fixed UI copy (e.g.
/// <c>common.optional</c>, the three <c>projects.board.lane.*_placeholder</c>
/// keys) are registered and resolved through the provider
/// (<c>Translation.GetAsync</c>) in-scope — not via the TagHelper, which
/// cannot wrap an attribute. The drift guard still applies to any attribute
/// that interpolates data.</li>
/// </ul>
/// <para>
/// <b>Upgrade-safe.</b> The <b>provider floor</b> (ADR 0015 D1) resolves any
/// registered key to the <c>en</c> source text here — code is the floor, so a
/// newly wrapped string renders its English on every instance immediately.
/// The first-boot seeder's <c>en</c> rows (upsert, <b>code-wins for
/// <c>en</c></b>, never touching non-<c>en</c> rows — ADR 0005 B) are a stored
/// copy of this registry, not the source of the floor.
/// </para>
/// </summary>
public static class KnownTranslationKeys
{
    /// <summary>
    /// The closed, curated key → <c>en</c> source-text set (the M·9 floor). The
    /// values are the <b>exact current English</b> strings in the in-scope views,
    /// so a fresh <c>en</c> instance renders identically whether or not the
    /// <c>kw-l</c> TagHelper is in play.
    /// </summary>
    public static IReadOnlyDictionary<string, string> EnValues { get; } =
        new Dictionary<string, string>
        {
            // ── nav (the shared top-nav, _Layout + _AccountNav) ─────────────
            ["nav.home"]          = "Home",

            // ── events (M4 — ADR 0054: the events nav entry + the Detail footer) ──
            ["nav.events"]        = "Events",
            ["events.created"]    = "Created",
            ["events.edited"]     = "edited",

            // ── projects (M5 — ADR 0067: the to-do surface nav entry + labels) ──
            ["nav.projects"]                 = "Projects",
            ["projects.todo.title"]          = "To-dos",
            ["projects.todo.lede"]           = "The neighborhood's shared to-dos — assign work to a neighbor, break it into subtasks, and put it on a board.",
            ["projects.todo.new"]            = "New to-do",
            ["projects.todo.new_lead"]       = "Write a to-do, optionally assign it to a neighbor, and — if needed — break it into subtasks or put it on a board. By default it is visible to everyone; turn that off in the audience section only if you want to narrow who can see it.",
            ["projects.todo.title_hint"]     = "A short label for the to-do — the card label.",
            ["projects.todo.status"]         = "Status",
            ["projects.todo.status_assignee_hint"] = "Status is one of the fixed to-do statuses (None, Not started, In progress, Done, Cancelled). Assigning a to-do gives that resident standing over it — display + standing, never an access limit.",
            ["projects.todo.assignee"]       = "Assignee",
            ["projects.todo.unassigned"]     = "Unassigned",
            ["projects.todo.assign"]         = "Assign",
            ["projects.todo.unassign"]       = "Unassign",
            ["projects.todo.assign_to"]      = "Assign to…",
            ["projects.todo.assign_people"]  = "People",
            ["projects.todo.assign_groups"]  = "Groups",
            ["projects.todo.assign_communities"] = "Communities",
            ["projects.todo.claim"]          = "Claim",
            ["projects.todo.addressed_to"]   = "Addressed to",
            ["projects.todo.filter_unassigned"] = "Unassigned only",
            ["projects.todo.add_subtask"]    = "Add subtask",
            ["projects.todo.delete"]         = "Delete",
            ["projects.todo.parent"]         = "Parent",
            ["projects.todo.top_level"]      = "Top-level to-do",
            ["projects.todo.parent_hint"]    = "Pick a parent to create this to-do as a subtask of it — a subtask is a full to-do with its own status, assignee, and board placement.",
            ["projects.todo.no_parent"]      = "No parent (top-level)",
            ["projects.todo.clear_parent"]   = "Clear parent (make top-level)",
            ["projects.todo.reparent_hint"]  = "A subtask is a full to-do with its own status, assignee, and board placement. Reparenting to a descendant is refused (cycle guard).",
            // ADR 0087 — the "waiting on" dependency lane (the chip, the
            // picker, and the feed toggle — the D8 four keys × 4 languages).
            ["todo.blocked_by"]              = "Waiting on",
            ["todo.blocked_by_none"]         = "No blocker",
            ["todo.blocked_generic"]         = "Another to-do (not visible to you)",
            ["todo.blocked_filter"]          = "Waiting on something",
            // ADR 0079 — optional start/due dates on a to-do.
            ["projects.todo.start"]          = "Start",
            ["projects.todo.due"]            = "Due",
            ["projects.todo.dates_hint"]     = "Both are optional — leave blank for no date. Shown to viewers in their own timezone.",
            ["projects.todo.created"]        = "Created",
            ["projects.todo.modified"]       = "Modified",
            ["projects.todo.no_body"]        = "No body — this to-do is title-only.",
            ["projects.todo.subtasks"]       = "Subtasks",
            ["projects.todo.boards"]         = "Boards",
            ["projects.todo.back"]           = "← Back to to-dos",
            ["projects.todo.untitled"]       = "Untitled to-do",
            ["projects.todo.edit"]           = "Edit",
            ["projects.todo.edit_heading"]   = "Edit to-do",
            ["projects.todo.edit_lead"]      = "Update this to-do's details. Your audience choice is the sole access boundary — the community pick is only a filter.",
            ["projects.todo.all_communities"] = "All communities",
            ["projects.todo.audience_heading"] = "Audience — who can see this to-do",
            ["projects.todo.audience_default"] = "The default — everyone can see this to-do. Turn it off only if you want to narrow who can see it.",
            ["projects.todo.save"]           = "Save changes",
            ["projects.todo.create"]         = "Create to-do",
            ["projects.todo.empty"]          = "No to-dos yet — create one to get the shared work started.",

            // ── projects (M5 — ADR 0067: the board surface (U10) + card actions) ──
            ["projects.todo.copy_to"]        = "Copy to board",
            ["projects.todo.move_to"]        = "Move to board",
            ["projects.todo.move_up"]        = "Move up",
            ["projects.todo.move_down"]      = "Move down",
            ["projects.todo.move_left"]      = "Move left",
            ["projects.todo.move_right"]     = "Move right",
            ["projects.board.title"]         = "Boards",
            ["projects.board.lede"]          = "The neighborhood's shared boards — arrange to-dos in lanes, move them through statuses, and keep the work visible.",
            ["projects.board.new"]           = "New board",
            ["projects.board.new_lead"]      = "Create a board to arrange to-dos in lanes. A lane can carry a status (a to-do moved into it picks that status up) and an optional limit on how many cards it holds. By default the board is visible to everyone; turn that off in the audience section only if you want to narrow who can see it.",
            ["projects.board.title_hint"]    = "A short name for the board — the feed label.",
            ["projects.board.description_hint"] = "An optional description of what this board tracks. A board is usable title-only.",
            ["projects.board.back"]          = "← Back to boards",
            ["projects.board.untitled"]      = "Untitled board",
            ["projects.board.delete"]        = "Delete board",
            ["projects.board.edit"]          = "Edit board",
            ["projects.board.edit_heading"]  = "Edit board",
            ["projects.board.edit_lead"]     = "Update this board's title and description. Its audience, community, and language are fixed when the board is created.",
            ["projects.board.save"]          = "Save changes",
            ["projects.board.create"]        = "Create board",
            ["projects.board.empty"]         = "No boards yet — create one to start arranging the shared work.",
            ["projects.board.no_lanes"]      = "This board has no lanes yet.",
            ["projects.board.lanes"]         = "Lanes",
            ["projects.board.lanes_hint"]    = "Columns across the board — for example \"Planned / Doing / Done\". A lane's status, when set, is applied to a to-do moved into that lane; its max items cap how many cards the lane holds.",
            ["projects.board.single_lane_hint"] = "The board starts with one lane — add more lanes and statuses on the board page after creating it.",
            ["projects.board.lane.title"]    = "Lane title",
            ["projects.board.lane.status"]   = "Status",
            ["projects.board.lane.max_items"] = "Max items",
            ["projects.board.lane.order"]    = "Order",
            ["projects.board.lane.empty"]    = "No cards in this lane.",
            ["projects.board.lane.save"]     = "Save",
            ["projects.board.lane.rename"]   = "Rename",
            ["projects.board.lane.set_limit"] = "Set limit",
            ["projects.board.lane.set_status"] = "Set status",
            ["projects.board.lane.move_left"] = "Move left",
            ["projects.board.lane.move_right"] = "Move right",
            ["projects.board.lane.add_todo"] = "Add to-do",
            ["projects.board.lane.add_lane"] = "Add lane",
            ["projects.board.lane.add_todo_placeholder"] = "To-do title",
            ["projects.board.lane.add_lane_placeholder"] = "New lane title",
            ["projects.board.lane.first_title_placeholder"] = "Planned",
            ["projects.board.status.none"] = "None",
            ["projects.board.status.not_started"] = "Not started",
            ["projects.board.status.in_progress"] = "In progress",
            ["projects.board.status.done"] = "Done",
            ["projects.board.status.cancelled"] = "Cancelled",
            ["projects.board.fullscreen"]    = "Full screen",
            ["projects.board.exit_fullscreen"] = "Exit full screen",
            ["projects.board.audience_heading"] = "Audience — who can see this board",
            ["projects.board.audience_default"] = "The default — everyone can see this board. Turn it off only if you want to narrow who can see it.",

            // ── common (shared action/field labels reused across resident-facing views) ──
            ["common.cancel"]   = "Cancel",
            ["common.save"]     = "Save",
            ["common.title"]    = "Title",
            ["common.body"]     = "Body",
            ["common.language"] = "Language",
            ["common.optional"] = "optional",
            ["common.add"]      = "Add",
            ["common.remove"]   = "Remove",
            ["common.filter"]   = "Filter",
            ["common.name"]         = "Name",
            ["common.display_name"] = "Display name",
            ["common.email"]        = "Email",
            ["common.password"]     = "Password",
            ["common.filter_name"]  = "Filter by name…",
            ["account.confirm_password"] = "Confirm password",
            ["groups.name_label"]     = "Group name",
            ["groups.desc_placeholder"] = "What is this group about? (visible to everyone who can reach this page)",
            ["posts.report_reason_placeholder"] = "Optionally add a reason for a moderator…",
            ["tags.events_example"] = "e.g. cleanup, social, garden",
            ["tags.pages_example"]  = "e.g. sanitation, budget, maple-street",
            ["admin.community_description"] = "Description (optional)",
            ["admin.community_mandatory"]   = "Mandatory",
            ["admin.add_community"]         = "Add community",
            ["admin.roles_heading"]         = "Roles",
            ["admin.roles_independent_hint"] = "Independent — a resident may hold any combination (ADR 0030). Nothing checked = a plain Member.",
            ["admin.moderator_scope"]       = "Moderator scope",
            ["admin.moderator_scope_hint"]  = "The communities this account may moderate. Meaningful only when the Moderator role is checked — the Core lane clears scope rows when the Moderator role is off.",
            ["nav.announcements"] = "Announcements",
            ["nav.community"]     = "Community",
            ["nav.groups"]        = "Groups",
            ["nav.pages"]         = "Pages",
            ["nav.tags"]          = "Tags",
            ["nav.directory"]     = "Directory",
            ["nav.sign_in"]       = "Sign in",
            ["nav.sign_up"]       = "Sign up",
            ["nav.profile"]       = "Profile",
            ["nav.admin"]         = "Admin",
            ["nav.translations"]  = "Translations",
            ["nav.sign_out"]      = "Sign out",
            ["nav.children"]      = "Children",
            ["nav.my_drafts"]     = "My drafts",
            ["nav.account"]       = "Account",

            // ── guardian (the /me/children child-accounts surface, GU ADR 0028) ──
            ["guardian.title"]        = "Your children",
            ["guardian.lead"]         = "The accounts you set up for a child, and the controls you hold over each one.",
            ["guardian.empty"]        = "No children yet.",
            ["guardian.add"]          = "Add a child account",
            ["guardian.manage_title"] = "Manage a child account",

            // ── guardian (per-child surface, GU ADR 0028) ─────────────────
            ["guardian.back"]               = "Back to your children",
            ["guardian.account_label"]       = "Account",
            ["guardian.group_memberships"]   = "Group memberships",
            ["guardian.community_memberships"] = "Community memberships",
            ["guardian.no_groups"]           = "No group memberships.",
            ["guardian.no_communities"]      = "No community memberships.",
            ["guardian.group_id_label"]      = "Group id",
            ["guardian.community_id_label"]  = "Community id",
            ["guardian.pending_invitations"] = "Pending group invitations",
            ["guardian.no_invitations"]      = "No pending invitations.",
            ["guardian.approve"]             = "Approve",
            ["guardian.handover"]            = "Hand over the account",
            ["guardian.handover_hint"]       =
                "Dissolving the guardianship hands the account to the child. " +
                "Their memberships are preserved, and their own controls come " +
                "back on the next read.",
            ["guardian.dissolve"]            = "Dissolve guardianship",
            ["guardian.suspended"]           = "Suspended",
            ["guardian.unsuspend"]           = "Un-suspend",
            ["guardian.suspend"]             = "Suspend",
            ["guardian.display_name"]        = "Display name",
            ["guardian.email"]               = "Email address",
            ["guardian.password"]            = "Password",
            ["guardian.child_email_hint"]    =
                "The child verifies their own email to sign in — the usual sign-up flow.",

            // ── posts (composer helper hints) ──────────────────────────────
            ["posts.title_hint"] =
                "A short headline (up to 120 characters). Leave blank for a " +
                "body-only post — the list will show your first line of the " +
                "body instead.",
            ["posts.language_hint"] =
                "The language you're writing this post in. This is only a tag — " +
                "it is not translated — and it keeps the text findable later " +
                "and lets a reader add their own language version if they want.",

            // ── faq (drop-in FAQ section, _FaqAccordion) ──────────────────
            ["faq.title"]     = "Frequently asked questions",
            ["faq.q1"]        = "Who can see my posts?",
            ["faq.a1"]        =
                "Whoever you choose when you post: just you, your group, your " +
                "community, or everyone. Audience is a choice " +
                "you make per post — it's not a global setting.",
            ["faq.q2"]        = "Where do announcements like water cuts and roadworks live?",
            ["faq.a2_intro"]  = "Pinned announcements, at",
            ["faq.a2_link"]   = "/announcements",
            ["faq.a2_outro"]  =
                "— the read side is open so nobody has to log in to find out " +
                "when the street gets repainted.",
            ["faq.q3"]        = "Is this private by default?",
            ["faq.a3"]        =
                "Yes. Every time someone is allowed (or denied) access to " +
                "restricted content, it's recorded. The data stays on a single " +
                "database owned by the neighbourhood's host, and there are no " +
                "ads or tracking built in.",

            // ── error (shared error page, Shared/Error.cshtml) ────────────
            ["error.title"]           = "Error.",
            ["error.subtitle"]        = "An error occurred while processing your request.",
            ["error.development_title"] = "Development Mode",
            ["error.development_hint"] =
                "Swapping to the Development environment will display more " +
                "detailed information about the error that occurred. The " +
                "Development environment shouldn't be enabled for deployed " +
                "applications — it can reveal sensitive information from " +
                "exceptions to end users.",
            ["error.request_id"]      = "Request ID:",

            // ── guardian assignment (GA ADR 0038) ──
            ["guardian.otherGuardians.title"] = "Other guardians",
            ["guardian.otherGuardians.empty"] = "No other guardians assigned.",
            ["guardian.assign.title"]        = "Assign a guardian",
            ["guardian.assign.email"]        = "Email of the guardian to assign",
            ["guardian.assign.submit"]       = "Assign",
            ["guardian.assign.noAccount"]    = "No account with that email.",
            ["guardian.assign.self"]         = "You are already this child's guardian.",
            ["guardian.assign.success"]      = "Guardian assigned.",

            // ── footer (the shared footer, _Layout) ─────────────────────────
            ["footer.tagline"]  =
                "A private home for one neighborhood — the feed, the groups and the pinned notes. " +
                "What happens on your street stays on your street.",
            ["footer.copyright"] = "· self-hosted by your community",
            ["footer.gtk_heading"] = "Good to know",
            ["footer.gtk_privacy"] =
                "Private by default: each post's audience is chosen by its author, and everything is " +
                "readable by you, not the world.",
            ["footer.gtk_oss"] =
                "Kumunita is open source — the code, the decisions, the docs.",
            // SP U03 (ADR 0043 D4) — the footer "Platform" column: the five
            // shipped platform surfaces (about view + the four Page docs),
            // linked unconditionally for every visitor.
            ["footer.platform.heading"] = "Platform",
            ["footer.platform.about"]   = "About",
            ["footer.platform.terms"]   = "Terms of use",
            ["footer.platform.help"]    = "Help",
            ["footer.platform.privacy"] = "Privacy",
            ["footer.platform.conduct"] = "Code of conduct",
            // The "The project" column (home / about / footer): the heading plus
            // the three RepositoryInfo.Links labels (repo.source_code /
            // repo.documentation / repo.non_technical) — emitted via a dynamic
            // kw-l key from the link list, so the labels resolve per-language.
            ["footer.project.heading"] = "The project",
            ["repo.source_code"]      = "Source code",
            ["repo.documentation"]    = "Documentation",
            ["repo.non_technical"]    = "For non-technical residents",

            // ── settings (the language-picker labels) ───────────────────────
            ["settings.settings"]       = "Settings",
            ["settings.choose_language"] = "Choose your language",

            // ── settings — account help (the help/account mount slot, ADR 0039 §3.8) ──
            ["settings.help_heading"]     = "Help with your account",
            ["settings.help_lede"]        =
                "Stuck on your account — a password, your access, or anything else? " +
                "This guide walks you through it.",

            // ── settings — timezone (ADR 0019: the user-override page + the
            // admin platform-default surface) ───────────────────────────────
            ["settings.timezone_title"]        = "Time zone",
            ["settings.timezone_lede"]         =
                "Pick the time zone the platform shows you. Your choice is saved on your account — " +
                "it takes effect the next time you load a page, and never affects other residents.",
            ["settings.timezone_label"]        = "Your time zone",
            ["settings.timezone_default_marker"] = "— platform default",
            ["settings.timezone_default_note"] = "The platform default is ",
            ["settings.timezone_default_tail"] =
                ". If you reset your preference, the platform default is used.",
            ["settings.timezone_reset"]        = "Reset to platform default",
            ["settings.timezone_save"]         = "Save",
            ["settings.timezone_flash_set"]    = "Time zone set to \"{0}\" — it takes effect on the next request.",
            ["settings.timezone_flash_reset"]  = "Time zone reset — the platform default will be used.",
            ["settings.timezone_unknown"]      = "Unknown time zone",

            // ── settings — date format (ADR 0020: the user-override section +
            // the admin platform-default surface) ──────────────────────────
            ["settings.dateformat_title"]        = "Date & time format",
            ["settings.dateformat_lede"]         =
                "Pick how dates and times are shown to you. Your choice is saved on your account — " +
                "it takes effect the next time you load a page, and never affects other residents.",
            ["settings.dateformat_label"]        = "Your date & time format",
            ["settings.dateformat_default_marker"] = "— platform default",
            ["settings.dateformat_default_note"] = "The platform default is ",
            ["settings.dateformat_default_tail"] =
                ". If you reset your preference, the platform default is used.",
            ["settings.dateformat_custom_label"] = "Custom format",
            ["settings.dateformat_custom_hint"]  =
                "A .NET custom datetime format string (e.g. yyyy-MM-dd HH:mm). Leave blank to use a preset.",
            ["settings.dateformat_reset"]        = "Reset to platform default",
            ["settings.dateformat_save"]         = "Save",
            ["settings.dateformat_flash_set"]    = "Date & time format set — it takes effect on the next request.",
            ["settings.dateformat_flash_reset"]  = "Date & time format reset — the platform default will be used.",

            // ── admin — the platform-default timezone (the /admin/timezone
            // surface, the global-admin control plane) ─────────────────────
            ["admin.timezone_title"]    = "Platform default time zone",
            ["admin.timezone_lede"]     =
                "The time zone residents' timestamps fall back to when they have set no personal " +
                "preference of their own.",
            ["admin.timezone_label"]    = "Default time zone",
            ["admin.timezone_save"]     = "Save",

            // ── admin — the platform-default date format (the /admin/dateformat
            // surface, the global-admin control plane; ADR 0020) ────────────
            ["admin.dateformat_title"]    = "Platform default date & time format",
            ["admin.dateformat_lede"]     =
                "The date & time format residents' timestamps fall back to when they have set no " +
                "personal preference of their own.",
            ["admin.dateformat_label"]    = "Default date & time format",
            ["admin.dateformat_custom_label"] = "Custom format",
            ["admin.dateformat_custom_hint"]  =
                "A .NET custom datetime format string (e.g. yyyy-MM-dd HH:mm). Leave blank to use a preset.",
            ["admin.dateformat_save"]     = "Save",

            // ── admin — the sign-up gate (the /admin/signup surface, the
            // global-admin control plane; ADR 0050) ─────────────────────────
            ["admin.signup_title"]    = "Sign-up",
            ["admin.signup_lede"]     =
                "Whether new residents may create an account on their own. " +
                "Closing the gate makes sign-up invitation-only — existing residents are unaffected.",
            ["admin.signup_open"]     = "Open — residents can sign up",
            ["admin.signup_invitation_only"] = "Invitation-only — new self-service accounts are gated",
            ["admin.signup_save"]     = "Save",
            ["admin.signup_notify_title"]   = "Notify admins",
            ["admin.signup_notify_lede"]    = "When a new resident signs up and when a resident verifies their account, the GlobalAdmins get an inbox notification and a best-effort email. Turning it off silences that — the account lane itself is unaffected.",
            ["admin.signup_notify_on"]      = "On — admins are notified of new sign-ups and verifications",
            ["admin.signup_notify_off"]     = "Off — no admin notifications on sign-up / verification",

            // ── account — sign-up-closed notice (the /account/signup and
            // /account/login surfaces when the admin gate is closed; ADR 0050) ─
            ["account.signup_closed_title"] = "Sign-up is closed",
            ["account.signup_closed_body"]  =
                "Sign-up is currently invitation-only on this instance. " +
                "If you have been invited, an administrator will add your account and send you the sign-in link.",

            // ── home (the hero + section lead; _Layout-independent) ─────────
            ["home.eyebrow"] = "Where this project stands",
            ["home.lead"] =
                "A private home for one neighbourhood — built step by step, in the open. " +
                "Everything listed below is part of that plan, and you're welcome to see how it's made.",
            ["home.support"] = "Questions or feedback? Write to",

            // ── home intro (what Kumunita is + the three surfaces) ──────────
            ["home.intro_eyebrow"]  = "A private home for one neighbourhood",
            ["home.intro_lead"] =
                "One quiet place for everything your street does — the feed, the groups, " +
                "and the notes that deserve better than a group chat. Private, plain-language, and yours.",
            ["home.about_link"]    = "What it is & how it works",
            ["home.feature_feed_title"]  = "One feed for the street",
            ["home.feature_feed_body"] =
                "Posts and threads from your blocks and lanes, in one quiet place — no algorithm, no noise.",
            ["home.feature_groups_title"]  = "Groups that fit",
            ["home.feature_groups_body"] =
                "Garden swap, book club, street watch — a group for whatever the neighbourhood already does.",
            ["home.feature_pinned_title"]  = "Pinned where it matters",
            ["home.feature_pinned_body"] =
                "Water cuts, roadworks, the new speed bumps — notes that stay put instead of scrolling away.",

            // ── home "what's new" feed (signed-in visitors) ─────────────────
            ["home.feed_title"]          = "What's new around the street",
            ["home.feed_posts"]          = "Posts",
            ["home.feed_announcements"]  = "Announcements",
            ["home.feed_pages"]          = "Pages",
            ["home.feed_view_all"]       = "View all",
            ["home.feed_empty"] =
                "Nothing posted yet — be the first to start the conversation in the feed.",
            ["home.feed_badge_pinned"]   = "Pinned",

            // ── home roadmap (the plan, after the intro) ────────────────────
            ["home.roadmap_heading"] = "Built in the open, one milestone at a time",

            // ── account (Login / Signup — titles + primary actions) ─────────
            ["account.login_title"]   = "Sign in",
            ["account.login_submit"]  = "Sign in",
            ["account.login_no_account"] = "No account yet?",
            ["account.login_remember"] = "Remember me",
            ["account.login_setup_hint"] = "Received a first-boot setup token?",
            ["account.login_setup_link"] = "Complete setup",
            ["account.login_signup"] = "Sign up",
            ["account.signup_title"]  = "Sign up",
            ["account.signup_submit"] = "Sign up",
            ["account.signup_has_account"] = "Already have an account?",

            // ── posts (Index / New / Edit — headings, actions, empty-states) ─
            // The all-sections feed (/community) header — the single feed
            // header for the union of every community's posts.
            ["posts.feed_all_sections"] = "Community",
            ["posts.write"]        = "Write a post",
            ["posts.new_title"]    = "Write a post",
            // ADR 0036 — the composer's lede: the default audience is
            // "everyone in the community" (was: "visible only to the
            // people you choose below").
            ["posts.new_intro"] =
                "By default everyone in the community you pick below can see " +
                "your post. Turn that off in the audience section only if you " +
                "want to narrow who can see it to specific people or groups.",
            ["posts.community_hint"] =
                "The community decides which feed your post appears in — and, " +
                "by default, who can see it (every member of that community). " +
                "To narrow the audience, turn off “Everyone in this community” " +
                "in the audience section below.",
            ["posts.new_submit"]   = "Post it",
            ["posts.edit_title"]   = "Edit post",
            ["posts.edit_save"]    = "Save changes",
            // ADR 0037 — draft mode: the composer's "save as draft" toggle.
            ["posts.save_as_draft"] =
                "Save as draft",
            ["posts.save_as_draft_hint"] =
                "A draft is saved but visible to no one — not even admins — " +
                "until you publish it. You can find it under “My drafts”.",
            // ADR 0037 — the detail-page draft badge + publish action.
            ["posts.draft_badge"]   = "Draft",
            ["posts.draft_note"] =
                "This post is a draft — only you can see it. Publish it to " +
                "make it visible under its audience.",
            ["posts.publish"]       = "Publish",
            // ADR 0037 — the "My drafts" list.
            ["my_drafts.title"]     = "My drafts",
            ["my_drafts.empty"]     = "You have no drafts.",
            // ADR 0036 — the composer's audience editor: the "Everyone in
            // this community" default grant (checked by default; the granular
            // picker below is hidden until it is turned off).
            ["posts.audience_all_members"] =
                "Everyone in this community",
            ["posts.audience_all_members_hint"] =
                "The default — every member of the community above can see " +
                "this post. Turn it off only if you want to narrow who can " +
                "see it.",
            // Edit lane — the same grant, seeded from the post's stored
            // audience (not a default), so the wording differs slightly.
            ["posts.audience_all_members_hint_edit"] =
                "When on, every member of this community can see the post. " +
                "Turn it off to narrow the audience to specific people or " +
                "groups.",
            ["posts.audience_combine"] =
                "How the picks combine",
            ["posts.audience_restrict_hint"] =
                "These picks are ADDITIONAL — “Everyone in this community” " +
                "stays on unless you turn it off, so the post is visible to " +
                "the whole community and the picks you make here.",
            ["posts.audience_only_picks"] =
                "Whatever you pick here becomes the post's audience — nothing " +
                "above or below this form is added to it. An empty pick (with " +
                "“Everyone in this community” off) means only you can see the " +
                "post.",
            ["posts.empty_can_post"] =
                "No posts yet here. Write the first one — it will be visible only to the audience you " +
                "choose in the composer below the title.",
            ["posts.empty"]        = "No posts here yet.",

            // ── groups (Index / Detail / New / PostDetail) ──────────────────
            ["groups.title"]          = "Groups",
            ["groups.lead"]           = "The communities you own or belong to.",
            ["groups.create"]         = "Create a group",
            ["groups.empty"]          = "No groups yet.",
            ["groups.back_all"]       = "← All groups",
            // ADR 0083 — the group-detail page's tab labels (Feed / Members /
            // Settings — the Bootstrap nav-tabs idiom from
            // _ProjectsTabs.cshtml; the Settings tab is owner-only and holds
            // the About / Privacy / Translations lanes that previously
            // scrolled below the members section).
            ["groups.tab.feed"]       = "Feed",
            ["groups.tab.members"]    = "Members",
            ["groups.tab.settings"]   = "Settings",
            ["groups.posts_heading"]  = "Posts",
            ["groups.new_post"]       = "New post",
            ["groups.posts_empty_can"] =
                "No posts yet. Write the first one — it will be visible to the current members.",
            ["groups.posts_empty"]    = "No posts here yet.",
            ["groups.members_heading"]  = "Members",
            ["groups.members_empty"]    = "No members yet.",
            ["groups.member_leave"]     = "Leave",
            ["groups.invite_heading"]   = "Invite a resident",
            ["groups.about_heading"]        = "About this group",
            ["groups.about_desc_label"]     = "Description (optional)",
            ["groups.about_desc_clear_hint"] = "Leave blank to clear the description.",
            ["groups.about_desc_save"]      = "Save description",
            ["groups.privacy_heading"]      = "Privacy",
            ["groups.private_label"]        = "Private group",
            ["groups.private_hint"]         =
                "A private group (e.g. a family) is hidden from everyone else; only the people you add as members can see and use it. Clear the box to make the group public again.",
            ["groups.new_title"]      = "Post to this group",
            ["groups.new_back"]       = "back to the group",
            ["groups.new_submit"]     = "Post to group",
            // ── groups list (the Airy layout — the invitation panel + the
            //    member-count word on each group card) ───────────────────────
            ["groups.invitations"]    = "Invitations",
            ["groups.invitations_pending"] = "pending",
            ["groups.invited_by"]     = "Invited by",
            ["groups.invite_accept"]  = "Accept",
            ["groups.invite_decline"] = "Decline",
            ["groups.members_one"]    = "member",
            ["groups.members_many"]   = "members",
            // ── community feed (the Airy layout — the left rail + the
            //    mandatory-community note) ───────────────────────────────────
            ["community.browse"]          = "Communities",
            ["community.all"]             = "All",
            ["community.mandatory_badge"] =
                "Everyone — mandatory",
            ["community.mandatory_note"]  =
                "This is the neighborhood's mandatory community — everyone " +
                "here belongs to it; no one can be removed or leave it.",

            // ── ADR 0026 — group name/description translations ───────────
            ["groups.translations_label"] = "Translations",
            ["groups.translations_none"] = "None yet",
            ["groups.translation_add"] = "Add",
            ["groups.translation_name_label"] = "Name",
            ["groups.translation_desc_label"] = "Description",
            ["groups.translation_optional"] = "optional",
            ["groups.translation_min_one"] = "At least one of name or description is required.",
            ["groups.translation_save"] = "Save translation",

            // ── directory (page heading + lead) ─────────────────────────────
            ["directory.title"] = "Directory",
            ["directory.lead"]  = "Everyone in the neighborhood — every resident on the platform.",
            ["directory.empty"] = "No residents on this neighborhood yet.",

            // ── profile (page heading + primary action) ─────────────────────
            ["profile.title"]      = "Your profile",
            ["profile.save_avatar"] = "Save avatar",

            // ── profile (Edit page — the ML-UI "full sweep", 2026-09-12) ────
            ["profile.edit_lede"] =
                "This is where you decide what other residents can see about you. " +
                "Whatever you choose here is exactly what the neighbor directory " +
                "shows — no surprises.",
            ["profile.avatar_heading"] = "Your avatar",
            ["profile.avatar_hint"] =
                "JPEG, PNG, WebP or GIF · up to 5 MB. Saving replaces the " +
                "avatar currently shown in the directory.",
            ["profile.name_email_heading"] = "Your name + email",
            ["profile.address_heading"] = "Your address + phone (optional)",
            ["profile.address_hint"] =
                "Shown on the directory list and detail only when you've also " +
                "opted in the contact block below; leave empty to keep your " +
                "street private to this profile.",
            ["profile.phone_hint"] =
                "Shown on the directory detail only when you've opted in the " +
                "contact block below; leave empty to keep your number private " +
                "to this profile.",
            ["profile.who_heading"] = "Who can see what",
            ["profile.optin_contact"] = "Share my contact info (address, email, phone)",
            ["profile.optin_contact_note"] =
                "Leave this off if you'd rather keep your address, " +
                "email, and phone completely hidden from the " +
                "directory. When it's on, you choose who can see " +
                "it in the box below.",
            ["profile.save"] = "Save",
            ["profile.preview_link"] = "Preview — how I appear",

            // ── profile (Edit page — field labels + input placeholders) ──────
            ["profile.display_name_label"] = "Display name",
            ["profile.address_label"] = "Address (the street you live at)",
            ["profile.address_placeholder"] =
                "Street — shown to neighbors when you opt in below",
            ["profile.phone_label"] = "Phone number",
            ["profile.phone_placeholder"] =
                "Phone — shown to neighbors when you opt in below",
            ["profile.audience_mode_any_word"] = "Any",
            ["profile.audience_mode_all_word"] = "All",

            // ── profile (Preview page) ───────────────────────────────────────
            ["profile.preview_back"] = "← Back to the editor",
            ["profile.preview_title"] = "Preview — how I appear",
            ["profile.preview_avatar_note"] =
                "Your avatar, as it appears next to your name in the " +
                "neighbor directory.",
            ["profile.preview_readonly_lead"] =
                "This is a read-only preview. It shows how your profile " +
                "appears to ",
            ["profile.preview_readonly_tail"] =
                " in the neighbor directory. It doesn't change anything in your " +
                "saved profile — to make a change, ",
            ["profile.preview_edit_link"] = "edit your profile",
            ["profile.preview_visible_badge"] = "Visible.",
            ["profile.preview_visible_tail"] =
                "you would see this contact block in the neighbor directory.",
            ["profile.preview_hidden_badge"] = "Contact hidden.",
            ["profile.preview_hidden_tail"] =
                "would not see a contact block. Your name (and " +
                "verified badge, if you have one) still shows in the " +
                "directory — only the contact details are hidden.",
            ["profile.contact_address"] = "Address",
            ["profile.contact_email"] = "Email",
            ["profile.contact_phone"] = "Phone",
            ["profile.edit_profile_btn"] = "Edit profile",

            // ── profile (the _AudienceEditor shared partial) ────────────────
            ["profile.audience_visibility"] = "Who can see your profile",
            ["profile.audience_contact"] = "Who can see your contact details",
            ["profile.audience_off_note"] =
                "Your contact details are currently hidden from everyone. " +
                "To change this, switch on \"Share my contact info\" above.",
            ["profile.audience_match_mode"] = "Match mode",
            ["profile.audience_mode_any"] =
                "a person is allowed if they meet any of the people/groups you picked",
            ["profile.audience_mode_all"] =
                "a person is allowed only if they meet all of the people/groups you picked",
            ["profile.audience_all_residents"] =
                "Everyone on the platform (all signed-in residents)",
            ["profile.audience_all_residents_hint"] =
                "New residents can see this automatically — no need to re-edit when someone joins.",

            // ── posts (Detail page) ──────────────────────────────────────────
            ["posts.back_to"] = "back to",
            ["posts.detail_edit"] = "Edit",
            ["posts.edited"] = "edited",
            ["posts.detail_why"] =
                "You can see this post because you were allowed to view it — " +
                "either you're the author, or it was shared with you or your groups.",
            ["posts.report_button"] = "Report this post",
            ["posts.reply_report_button"] = "Report this reply",
            ["posts.report_reason_label"] = "What's wrong?",
            ["posts.report_optional"] = "optional",
            ["posts.report_note"] =
                "Filing a report is an intake action — it doesn't change " +
                "what you can see, and a moderator may follow up.",
            ["posts.report_submit"] = "Report",
            ["posts.replies_heading"] = "Replies",
            ["posts.replies_empty"] =
                "No replies yet. If you can see this post, you can reply to it.",
            ["posts.reply_heading_author"] = "Reply (you are the author of this post)",
            ["posts.reply_heading"] = "Reply",
            ["posts.reply_label"] = "Reply",
            ["posts.reply_language_label"] = "Language",
            ["posts.reply_language_note"] =
                "The language you're replying in — a tag, not a " +
                "translation.",
            ["posts.reply_audience_note"] =
                "Replies have no own audience — they are visible under " +
                "this post's single audience decision (the C-M3·1 " +
                "\"reply-inherits\" rule). You are replying only where " +
                "the post itself is visible.",
            ["posts.reply_submit"] = "Reply",
            ["posts.reply_edit"] = "Edit",
            ["posts.reply_save"] = "Save",
            ["posts.reply_edited"] = "edited",

            // ── posts (Detail page) — author soft-delete (ADR 0024) ──
            ["posts.delete"] = "Delete",
            ["posts.reply_delete"] = "Delete",
            ["posts.deleted_placeholder"] =
                "This post has been deleted by its author.",
            ["posts.reply_deleted_placeholder"] =
                "This reply has been deleted by its author.",

            // ── posts (Detail page) — user-added translations (ADR 0022) ──
            ["posts.translations_label"] = "Translations",
            ["posts.translations_none"] = "none yet",
            ["posts.translation_add"] = "Add",
            ["posts.translation_title_label"] = "title",
            ["posts.translation_body_label"] = "Body",
            ["posts.translation_optional"] = "optional",
            ["posts.translation_save"] = "Save translation",
            ["posts.translation_edit"] = "Edit",
            ["posts.translation_remove"] = "Remove",

            // ── pages (the PG lane — tree browse + post view, ADR 0039) ──────
            // ADR 0039 §3.8 — the pages surface's UI copy. Plain text only:
            // the kw-l TagHelper emits via SetContent (auto-escaped), so a value
            // carrying <b>/<i> markup would render as literal brackets. These
            // were missing from the registry (the views wrapped keys that were
            // never registered) — the resident saw the raw key ("pages.
            // new_button") because an unregistered key falls back to itself.
            ["pages.title"]       = "Pages",
            ["pages.new_button"]  = "New page",
            ["pages.none"] =
                "No pages yet. Global admins can create the first system " +
                "page — an About page is the common starting point — and any " +
                "resident can start their own blog (a page of their own).",
            ["pages.back"]        = "← Back to the pages",
            ["pages.by"]          = "by",
            ["pages.delete"]      = "Delete",
            ["pages.resetSeeded"] = "Reset to seeded text",
            ["pages.untitled"]    = "Untitled page",

            // ── blog (per-resident page feed, ADR 0040) ─────────────────────
            // The /blog/{userId} feed — a resident's own User-kind pages,
            // newest first. Plain text only (the kw-l TagHelper emits via
            // SetContent, auto-escaped — a value carrying <b>/<i> would render
            // as literal brackets).
            ["blog.new_page"]     = "New blog page",
            ["blog.empty_own"] =
                "You have no blog pages yet. Create your first — it becomes the " +
                "root of your blog, and you can nest more under it.",
            ["blog.empty_other"]  = "This resident has no blog pages yet.",
            ["blog.draft"]        = "Draft",

            // ── groups (Create page) ─────────────────────────────────────────
            ["groups.create_back"] = "← Back to groups",
            ["groups.create_title"] = "Create a group",
            ["groups.create_lede"] =
                "A name for a community of residents (e.g. \"Building 4\", " +
                "\"Volunteers\", \"Bike owners\"). " +
                "You own the group — you can add and remove members from the " +
                "group's detail page.",
            ["groups.create_desc_hint"] = "Optional — a short note other residents will see.",
            ["groups.create_private_hint"] =
                "A private group (e.g. a family) is hidden from everyone else — " +
                "only people you add as members can see and use it. " +
                "A public group (e.g. \"Mushroom hunters\") shows up as an option " +
                "in other residents' pickers.",
            ["groups.create_submit"] = "Create group",

            // ── groups (Edit page — the three bug-fix keys, 2026-09-12) ────
            ["groups.edit_back"] = "back to the post",
            ["groups.edit_title"] = "Edit your post",
            ["groups.edit_lede"] =
                "You are editing your own post. Only you can edit it — the group " +
                "membership decides who can see it, but only its author can " +
                "change it. The group this post appears in is fixed; only the " +
                "title, body, and language below are editable.",
            ["groups.edit_title_label"] = "Title",
            ["groups.edit_title_hint"] =
                "A short headline (≤ 120 chars). Leave blank for a " +
                "body-only post — the list will show your first line " +
                "of the body instead.",
            ["groups.edit_body_label"] = "Body",
            ["groups.edit_language_label"] = "Language",
            ["groups.edit_language_hint"] =
                "The language you're writing this post in. This is only a " +
                "tag — it is not translated — and it keeps the text " +
                "findable later and lets a reader add their own language " +
                "version if they want.",
            ["groups.edit_submit"] = "Save changes",
            ["groups.edit_cancel"] = "Cancel",

            // ── directory (Detail page) ──────────────────────────────────────
            ["directory.detail_back"] = "← Back to the directory",
            ["directory.detail_verified"] = "Verified",
            ["directory.detail_contact_address"] = "Address",
            ["directory.detail_contact_email"] = "Email",
            ["directory.detail_contact_phone"] = "Phone",
            ["directory.detail_no_contact"] =
                "This resident hasn't shared a contact method with you (yet). You can " +
                "still see their profile page.",

            // ── community (Manage page) ──────────────────────────────────────
            ["community.manage_back"] = "← Back to the feed",
            ["community.manage_lede"] = "Manage membership for this community.",
            ["community.manage_moderate"] = "You moderate this community",
            ["community.manage_disabled"] = "Disabled",
            ["community.manage_availability"] = "Availability",
            ["community.manage_mandatory_label"] =
                "Mandatory community — everyone in the neighborhood is a member",
            ["community.manage_mandatory_hint"] =
                "Mandatory communities can't have members removed and can't be left; " +
                "clear the checkbox to make membership optional again.",
            ["community.manage_make_optional"] = "Make optional",
            ["community.manage_make_mandatory"] = "Make mandatory",
            ["community.manage_members"] = "Members",
            ["community.manage_mandatory_note"] =
                "This community is mandatory — everyone is a member, so there's no " +
                "one to remove. The listed rows are explicit memberships, kept in " +
                "case the community is made optional again.",
            ["community.manage_no_members"] = "No explicit members yet — add some below.",
            ["community.manage_you"] = "You",
            ["community.manage_remove"] = "Remove",
            ["community.manage_add_member"] = "Add a member",
            ["community.manage_all_members"] =
                "Everyone in the neighborhood is already a member here.",
            ["community.manage_pick_resident"] = "Pick a resident to add…",

            // ── ADR 0026 — community name/description translations ────────
            ["community.translations_label"] = "Translations",
            ["community.translations_none"] = "None yet",
            ["community.translations_page_title"] = "Translations",
            ["community.translations_page_lede"] = "Name and description translations for this community.",
            ["community.translations_view_only"] = "You can view the translations; only a GlobalAdmin or a Translator can add, edit, or remove them.",
            ["community.translation_add"] = "Add",
            ["community.translation_name_label"] = "Name",
            ["community.translation_desc_label"] = "Description",
            ["community.translation_optional"] = "optional",
            ["community.translation_min_one"] = "At least one of name or description is required.",
            ["community.translation_save"] = "Save translation",

            // ── community feed buttons (Posts/Index.cshtml) ─────────────
            ["community.feed_manage_members"] = "Manage members",
            ["community.feed_translations"] = "Translations",

            // ── moderation (Index page) ──────────────────────────────────────
            ["moderation.title"] = "Moderation",
            ["moderation.empty"] = "No reports yet. The queue is empty.",
            ["moderation.th_status"] = "Status",
            ["moderation.th_post"] = "Post",
            ["moderation.th_component"] = "Component",
            ["moderation.th_reporter"] = "Reporter",
            ["moderation.th_filed"] = "Filed",
            ["moderation.th_action"] = "Action",
            ["moderation.review"] = "Review →",

            // ── moderation (Resolve page) ────────────────────────────────────
            ["moderation.resolve_title"] = "Moderation — Review report",
            ["moderation.details"] = "Report details",
            ["moderation.th_post_label"] = "Post",
            ["moderation.th_component_label"] = "Component",
            ["moderation.th_reporter_label"] = "Reporter",
            ["moderation.th_author_label"] = "Post author",
            ["moderation.th_filed_label"] = "Filed",
            ["moderation.th_reason_label"] = "Reason",
            ["moderation.no_reason"] = "(no reason given)",
            ["moderation.th_body_label"] = "Post body",
            ["moderation.body_preview"] = "post preview",
            ["moderation.assign_header"] = "Assign to a moderator",
            ["moderation.assign_label"] =
                "A moderator covering this community",
            ["moderation.assign_pick"] = "Choose a moderator …",
            ["moderation.assign_submit"] = "Assign",
            ["moderation.cancel"] = "Cancel",
            ["moderation.unlock_submit"] = "Unlock",
            ["moderation.resolve_header"] = "Resolve (close this report)",
            ["moderation.resolve_submit"] = "Resolve",
            ["moderation.back_to_queue"] = "← Back to queue",
            // ── reply-report-target lane (ADR 0023) ─────────────────────────
            // A report's target is either a post (ReplyId null — the
            // original M3b shape) or a specific reply (ReplyId non-null).
            // These keys render the reply-target discriminator in the queue
            // ("reply by X") and the resolve view (the reply blockquote
            // preview) — UGC body / author name are never translated (M·3),
            // only these platform labels are.
            ["moderation.queue_reply_by"] = "reply by",
            ["moderation.resolve_reply_label"] = "Reply (target of this report)",
            ["moderation.resolve_reply_by"] = "Reply by",

            // ── account (Verify / Resend / AccessDenied) ─────────────────────
            ["account.verify_title"] = "Verify your account",
            ["account.verify_pending"] =
                "We're confirming your account — you'll be signed in in a moment.",
            ["account.verify_again"] = "Sign up again",
            ["account.resend_title"] = "Resend confirmation email",
            ["account.resend_lede"] =
                "Enter the email you signed up with and we'll send a fresh verification link.",
            ["account.resend_submit"] = "Resend",
            ["account.resend_create"] = "Just creating your account?",
            ["account.resend_signup"] = "Sign up",
            ["account.denied_title"] = "Access denied",
            ["account.denied_lede"] = "You don't have permission to view this page.",
            ["account.denied_home"] = "Back to home",

            // ── admin (Audit page) ───────────────────────────────────────────
            ["admin.audit_title"] = "Access audit",
            ["admin.audit_lede"] =
                "A record of who was allowed or denied access to restricted content, " +
                "plus admin actions. This log is always kept and is periodically " +
                "cleaned up according to the instance's retention policy.",
            ["admin.audit_filter"] = "Filter",
            ["admin.audit_th_at"] = "At (UTC)",
            ["admin.audit_th_actor"] = "Actor",
            ["admin.audit_th_effective"] = "Effective",
            ["admin.audit_th_action"] = "Action",
            ["admin.audit_th_target"] = "Target",
            ["admin.audit_th_aggregate"] = "Aggregate",
            ["admin.audit_th_via"] = "Via",
            ["admin.audit_th_outcome"] = "Outcome",

            // ── admin (Break-glass page) ─────────────────────────────────────
            ["admin.breakglass_title"] = "Break-glass",
            ["admin.breakglass_granted"] = "Granted at (UTC)",
            ["admin.breakglass_expires"] = "Expires at (UTC)",
            ["admin.breakglass_status"] = "Status",
            ["admin.breakglass_consumed"] = "activated — elevation in effect until expiry",
            ["admin.breakglass_presented"] = "set up, but not yet activated",
            ["admin.breakglass_token_label"] = "One-time code (from the operator)",
            ["admin.breakglass_token_hint"] =
                "Using this code is a one-time action. It activates the elevation " +
                "until its expiry.",
            ["admin.breakglass_consume"] = "Activate",

            // ── locale (settings + public picker) ────────────────────────────
            ["locale.settings_title"] = "Your settings",
            ["locale.language_heading"] = "Language",
            ["locale.lede"] =
                "Pick the language the platform shows you. Your choice is saved in a " +
                "browser cookie — it takes effect on the next request, and never " +
                "affects other residents.",
            ["locale.preferred_label"] = "Preferred language",
            ["locale.default_note"] =
                "The instance default is ",
            ["locale.default_note_tail"] =
                ". If your preferred language is later removed by the admin, " +
                "the platform silently falls back to the instance default.",
            ["locale.save"] = "Save",
            ["locale.reset"] = "Reset to instance default",
            ["locale.public_title"] = "Choose your language",
            ["locale.instance_default"] = "— instance default",
            // ADR 0046 — the browser-match suggestion (pre-selection + marker).
            ["locale.browser_matched"] = "— matched from your browser",
            ["locale.browser_note"] =
                "We picked ",
            ["locale.browser_note_tail"] =
                " from your browser settings. Saving makes it your preferred " +
                "language — it stays until you change it.",
            // Flash messages (the toast surface — LocaleController.Save /
            // PublicLocaleController.Save; {0} = the language code).
            ["locale.flash_set"] =
                "Language preference set to \"{0}\" — it takes effect on the next request.",
            ["locale.flash_reset"] =
                "Language preference reset — the instance default will be used.",

            // ── announcements (shared labels + New/Edit compose) ─────────────
            ["announcements.scope_label"] = "Who sees this?",
            ["announcements.scope_public"] =
                "Everyone (public) — visible to visitors and residents",
            ["announcements.scope_resident"] =
                "Residents — visible only when signed in",
            ["announcements.community_label"] = "Send to a specific community (optional)",
            ["announcements.all_residents"] = "All residents",
            ["announcements.community_hint"] =
                "Leave as All residents to send to everyone, or pick a community to limit who sees it.",
            ["announcements.title_label"] = "Title",
            ["announcements.title_hint"] = "A short headline (up to 120 characters).",
            ["announcements.body_label"] = "Body",
            ["announcements.pin_label"] = "Pin to the top of all pages",
            ["announcements.cancel"] = "Cancel",
            ["announcements.new_title"] = "New announcement",
            ["announcements.new_lede"] =
                "Announcements are notices that appear on their own, separate " +
                "from the community feed. A public announcement is " +
                "visible to everyone, including people who are not signed in " +
                "(e.g. a maintenance window). A resident announcement " +
                "is only visible to signed-in residents (e.g. a \"help us with " +
                "X\" call).",
            ["announcements.new_scope_hint"] =
                "Public announcements are visible to everyone, including " +
                "visitors who are not signed in (e.g. a maintenance " +
                "window or an outage notice). Resident announcements " +
                "are only visible to signed-in residents.",
            ["announcements.new_submit"] = "Create announcement",
            ["announcements.edit_title"] = "Edit announcement",
            ["announcements.edit_lede"] =
                "Update this announcement's title, body, and visibility. A " +
                "public announcement is visible to everyone, including " +
                "people who are not signed in (e.g. a maintenance window). A " +
                "resident announcement is only visible to signed-in " +
                "residents (e.g. a \"help us with X\" call).",
            ["announcements.edit_scope_hint"] =
                "Changing who sees this applies immediately for the next reader.",
            ["announcements.edit_submit"] = "Save changes",
            ["announcements.pin_hint"] =
                "A pinned announcement also appears as a banner at the " +
                "very top of every page (including Home), in addition " +
                "to the usual Announcements list. Its visibility still " +
                "follows the audience you picked above: a public pin shows to " +
                "every visitor; a resident pin shows only when a user is signed " +
                "in. If more than one is pinned, the most recently pinned " +
                "one wins.",
            ["announcements.language_note"] =
                "The language you're writing this announcement in. This is only a " +
                "tag — it is not translated — and it keeps the text findable later " +
                "and lets a reader add their own language version if they want.",

            // ── announcements (Index + Detail + pinned banner) ───────────────
            ["announcements.index_title"] = "Announcements",
            ["announcements.index_lede"] =
                "Platform notices: public ones are visible to everyone (e.g. " +
                "scheduled maintenance); resident-only ones are visible to all " +
                "signed-in users (e.g. \"help us with X\" calls).",
            ["announcements.all"] = "All announcements",
            ["announcements.new_button"] = "New announcement",
            ["announcements.empty"] = "No announcements yet.",
            ["announcements.read_more"] = "Read more…",
            ["announcements.scope_everyone"] = "everyone",
            ["announcements.scope_residents"] = "residents",
            ["announcements.pinned_badge"] = "pinned",
            ["announcements.edit_button"] = "Edit",
            ["announcements.delete"] = "Delete",
            ["announcements.detail_back"] = "← Back to the announcements",
            ["announcements.detail_untitled"] = "Untitled announcement",
            ["announcements.by"] = "by",
            ["announcements.edited"] = "edited",
            ["announcements.banner_read_more"] = "Read more",
            ["announcements.banner_all"] = "All announcements",

            // ── static pages (Page — the terms/help shell) ───────────────────
            ["static.last_updated"] = "Last updated:",

            // ── shared (the _GrantPickers partial — the static markup only;
            //    the "Select all" rows and per-list counts are built in C# and
            //    rendered via Html.Raw, where the kw-l TagHelper can't emit, so
            //    they stay as authored English) ─────────────────────────────
            ["grant.heading"] = "Who to grant to",
            ["grant.hint"] =
                "Check one or many — or use the \"Select all\" row above each " +
                "list as a shortcut.",
            ["grant.empty_users"] =
                "You're the only verified resident, so there's no one else " +
                "to grant to yet.",
            ["grant.empty_groups"] =
                "No groups exist on the platform yet — create one under " +
                "\"Groups\" to add group-scoped visibility.",



            // ── admin (page heading + primary action) ───────────────────────
            ["admin.title"]  = "Admin",
            ["admin.verify"] = "Verify",

            // ── rich editor (the RE toolbar button labels, ADR 0031 — RE U08
            // registers the closed `rc.editor.*` set; the values are the exact
            // fallback strings the U04–U06 composer views already emit inside
            // <kw-l>. No `rc.editor.quote` — U1's drift pause removed the
            // blockquote button (MarkdownRenderer has no blockquote branch),
            // so there is no button and no key for it. en floor only.) ──────
            // `rc.editor.preview` (RE, ADR 0031) was never emitted through <kw-l>
            // in any view; IE (ADR 0032) introduced `rc.editor.showPreview` as
            // the canonical "show preview" label and the dead `rc.editor.preview`
            // key was removed (2026-09-15). en floor only.
            // + IE (ADR 0032): rc.editor.source + rc.editor.showPreview —
            // the toggle button's two label states (source hidden / visible).
            ["rc.editor.bold"]    = "B",
            ["rc.editor.italic"]  = "I",
            ["rc.editor.code"]    = "C",
            ["rc.editor.h1"]      = "H1",
            ["rc.editor.h2"]      = "H2",
            ["rc.editor.h3"]      = "H3",
            ["rc.editor.list"]    = "•",
            ["rc.editor.olist"]   = "1.",
            ["rc.editor.link"]    = "Link",
            ["rc.editor.image"]   = "Image",
            ["rc.editor.attach"]  = "Attach file",
            ["rc.editor.source"]      = "</>",
            ["rc.editor.showPreview"] = "Preview",

            // ── about (the About product surface, ADR 0042 D5 — the LS U01
            // registers the closed `about.*` set with the exact current
            // English copy of Views/StaticPages/About.cshtml; U05 wraps the
            // view in kw-l against these names. The TODO(counts) stats
            // values, the @Model.CommunityName hero heading, the
            // @Model.SupportEmail contact strings (C#-built) and the
            // RepositoryInfo.Links labels are data, not keys — D5.) ──────
            ["about.eyebrow"]             = "Private by default",
            ["about.lead"] =
                "One home for everything your neighborhood does — the feed, " +
                "the groups, and the notes that deserve better than a group " +
                "chat. Private, plain-language, and yours.",
            ["about.cta_feed"]            = "See the feed",
            ["about.cta_notes"]           = "Read the pinned notes",
            ["about.features.one.title"]  = "One feed for the street",
            ["about.features.one.body"] =
                "Posts and threads from your blocks and lanes, in one quiet " +
                "place — no algorithm, no noise.",
            ["about.features.groups.title"]  = "Groups that fit",
            ["about.features.groups.body"] =
                "Garden swap, book club, street watch — a group for whatever " +
                "the neighbourhood already does.",
            ["about.features.pinned.title"]  = "Pinned where it matters",
            ["about.features.pinned.body"] =
                "Water cuts, roadworks, the new speed bumps — notes that stay " +
                "put instead of scrolling away.",
            ["about.project.eyebrow"]  = "Open source",
            ["about.project.heading"]  = "The code, the decisions, the design docs",
            ["about.project.lead"] =
                "If you're curious how it works — or if you're about to host " +
                "it for your neighbourhood — everything is public.",

            // ── tags (the TG lane, ADR 0044 — browse + composer affordances) ──
            ["tags.list.heading"]       = "Tags",
            ["tags.list.lede"] =
                "Subjects your posts and blog pages are tagged with — click one " +
                "to browse what's been posted about it.",
            ["tags.list.empty"] =
                "No tags yet — tags appear here once a resident attaches one to a " +
                "post or a blog page.",
            ["tags.bytag.heading"]      = "Posts and pages about",
            ["tags.bytag.posts_heading"] = "Posts",
            ["tags.bytag.pages_heading"] = "Blog pages",
            ["tags.bytag.empty"] =
                "No readable posts or blog pages carry this tag.",
            ["tag.input.placeholder"] = "e.g. sanitation, budget, maple-street",
            ["tag.input.hint"] =
                "Type to search existing tags, or start a new one — it attaches to " +
                "this post.",
            ["tag.suggest.empty"] =
                "No matching tags — keep typing or start a new one.",
            ["tag.translate.heading"] = "Translations",
            ["tag.translate.save"] = "Save",
            ["tag.translate.disabled"] = "Only the tag's creator or a GlobalAdmin can reword it.",

            // ── platform (scope + the FIG philosophy, home/about) ───────────
            ["platform.scope_home"] =
                "Kumunita started out as a home for one neighbourhood — but it is just as at home with a club, a team, or the people behind a big event. The neighbourhood is the default, not the limit.",
            ["platform.fig_home"] =
                "It is built on the Fractal Integration Guidelines (FIG): the idea that a group's real value lives in the links between its people and parts, not in the parts themselves. Kumunita is the software that builds and holds those links.",
            ["platform.scope_heading"] = "Built for a neighbourhood — at home anywhere",
            ["platform.scope_body1"] =
                "Kumunita was originally built for one street: a private, shared home for the people who live there. But the core idea is not tied to a street at all. It is a private home for one group of people who want to coordinate, share, and build trust together — a neighbourhood, a club, a sports team, a workplace, a volunteer body, or even the people behind a single event. What changes is only the name and the details.",
            ["platform.scope_body2"] =
                "Everything that makes it fit a street — the quiet feed, the groups, the audience-scoped posts, the moderation and its audit trail, the multilingual interface — works the same way for any of those. So adapting it is a matter of configuration: the community name, the components that stand in for 'Safety' and 'Social', the groups that fit your world. Not a redesign.",
            ["platform.fig_heading"] = "The idea behind it: the Fractal Integration Guidelines",
            ["platform.fig_body1"] =
                "Kumunita is built on a small set of open guidelines we call the Fractal Integration Guidelines (FIG). Their central claim is that an integrated system is more than the sum of its parts, and that its quality is the quality of the linkage between those parts — a property no single part has on its own. A bag of features that never connect is just noise; the connections are where the value lives.",
            ["platform.fig_body2"] =
                "A neighbourhood is exactly such a system: many different people, households, and concerns, whose linkage — who knows whom, who can rely on whom, how a problem actually gets solved across people — is what produces a community. No single resident is a neighbourhood. Kumunita is the software that builds and holds that linkage.",
            ["platform.fig_body3"] =
                "So we develop Kumunita by the same rule we ask of it: quality is the quality of the linkage, not the count of the features. The guidelines repeat at every scale — a module, a feature, a community, the people who build it — which is why the whole project follows them, and why the philosophy is documented in the open.",
            ["platform.scope_eyebrow"] = "For any group",
            ["platform.fig_eyebrow"] = "The philosophy",

            // ── events (M4 — ADR 0054: the /Events lane; index, detail, composer) ──
            ["events.title"] = "Events",
            ["events.lede"] =
                "Upcoming events around the neighborhood — see what's on, and RSVP.",
            ["events.new_event"] = "New event",
            ["events.new_lead"] =
                "Share an upcoming event with the neighborhood. By default it is visible to everyone; " +
                "turn that off in the audience section only if you want to narrow who can see it.",
            ["events.edit_event"] = "Edit event",
            ["events.edit_lead"] =
                "Update this event's details. Your audience choice is the sole access boundary — " +
                "the community pick is only a filter.",
            ["events.title_hint"] = "A short headline for the event.",
            ["events.start"] = "Start",
            ["events.end"] = "End",
            ["events.time_hint"] =
                "The time the event runs. Shown to viewers in their own timezone.",
            ["events.location"] = "Location",
            ["events.capacity"] = "Capacity",
            ["events.color"] = "Color",
            ["events.location_hint"] =
                "Location, capacity, and color are display details only — they do not limit who can RSVP.",
            ["events.all_communities"] = "All communities",
            ["events.community_hint"] =
                "The community this event appears under — a filter, not an access limit.",
            ["events.tags_placeholder"] = "e.g. cleanup, social, garden",
            ["events.audience_heading"] = "Audience — who can see this event",
            ["events.audience_default"] =
                "The default — everyone can see this event. Turn it off only if you want to narrow who can see it.",
            ["events.audience_mode_any"] = "a viewer matches if they're on any of the picks",
            ["events.audience_mode_all"] =
                "a viewer must be on every pick — an empty list denies everyone",
            ["events.reminder"] = "Send the 24-hour reminder to those who RSVP \"Going\"",
            ["events.reminder_hint"] =
                "A reminder is sent the day before the event to everyone who is going. Turn this off to skip it.",
            ["events.create"] = "Create event",
            ["events.save_changes"] = "Save changes",
            ["events.empty"] = "No upcoming events yet.",
            ["events.back"] = "← Back to events",
            ["events.draft"] = "Draft",
            ["events.draft_title"] = "Only you can see this — it is not yet public",
            ["events.publish"] = "Publish",
            ["events.edit"] = "Edit",
            ["events.delete"] = "Delete",
            ["events.delete_confirm"] = "Delete this event? This cannot be undone.",
            ["events.untitled"] = "Untitled event",
            ["events.rsvp"] = "RSVP",
            ["events.rsvp_you"] = "You're going to:",
            ["events.rsvp_responses"] = "Responses",
            ["events.rsvp_update"] = "Update",
            ["events.remove_translation_confirm"] = "Remove this translation?",

            // ── events.mine (the EV-MINE "your upcoming events" section on /events — ADR 0065) ──
            ["events.mine.title"] = "Your upcoming events",
            ["events.mine.hint"] = "Events you've RSVPed to or organized.",

            // ── events.calendar (the EV-CAL month-anchored calendar view — /events/calendar, ADR 0063) ──
            ["events.calendar.title"] = "Calendar",
            ["events.calendar.prev"] = "Prev",
            ["events.calendar.next"] = "Next",
            ["events.calendar.today"] = "Today",
            ["events.calendar.overlap_hint"] = "Overlaps another event in this window",
            ["events.calendar.empty"] = "No events in this window.",
            ["events.calendar.from"] = "From",
            // EV-DWM (ADR 0064, U05) — the Day/Week/Month toggle labels (the Calendar.cshtml
            // view switch; en is authoritative, de/fr/da below are translations — C-DWM·9).
            ["events.calendar.view.day"] = "Day",
            ["events.calendar.view.week"] = "Week",
            ["events.calendar.view.month"] = "Month",

            // ── grant (the shared "Who to grant to" picker — the C#-built "Select all" + count) ──
            ["grant.select_all"] = "Select all",
            ["grant.label_residences"] = "Residences",
            ["grant.label_groups"] = "Groups",
            ["grant.count_selected"] = "{0} of {1} selected",

            // ── profile (the _AudienceEditor empty-contact warning; the inline <b> words are split) ──
            ["profile.audience_empty_warning_lead"] = "Heads up:",
            ["profile.audience_empty_warning_body"] =
                "you haven't picked anyone or any group below, so your contact details are currently hidden from",
            ["profile.audience_empty_warning_everyone"] = "everyone",
            ["profile.audience_empty_warning_tail"] =
                ". Add a person or group if you'd like to share them.",

            // ── settings (the email & notification-language section of /settings/language) ──
            ["settings.email_title"] = "Email & notification language",
            ["settings.email_lede"] =
                "Pick the language the platform writes to you in — account emails and event reminders. " +
                "Your choice is saved on your account.",
            ["settings.email_label"] = "Email & notification language",
            ["settings.email_note"] =
                "If you choose a language, your emails and reminders are sent in it. " +
                "If you reset it, the instance default is used.",
            ["settings.email_save"] = "Save",
            ["settings.email_reset"] = "Reset to instance default",
            ["settings.email_flash_set"] = "Email & notification language set — your next email will use it.",
            ["settings.email_flash_reset"] = "Email & notification language reset — the instance default will be used.",
            ["settings.email_reset_confirm"] =
                "Reset your email & notification language to the instance default?",

            // ── email (outbound mail bodies — {0}/{1} are the runtime placeholders) ──
            ["email.verify_subject"] = "Verify your Kumunita account",
            ["email.verify_body"] =
                "Hi {0},\n\nYour Kumunita account is set to verify on its first sign-in. " +
                "Open this one-time link to confirm the account (it also signs you in):\n\n{1}\n\n" +
                "If you didn't create this account, you can ignore this message.",
            ["email.reminder_subject"] = "Reminder: {0}",
            ["email.reminder_body"] = "**{0}** is coming up: {1}{2}.",

            // ── notifications (M6 — ADR 0076: the /notifications surface —
            // the inbox (U06), the layout bell, the preferences editor (U07),
            // and the per-kind email subject/body templates the
            // NotificationService resolves at emit time (the frozen
            // `notification.{kind}.subject` / `notification.{kind}.body`
            // shape — NotificationService.cs; the kind is the stored dotted
            // constant, e.g. `post.reply`). A UGC snippet (the sender's
            // authored content, ADR 0018) is **appended after a single
            // space** to the body template — `template + " " + snippet` —
            // so the templates end in a trailing sentence break and read
            // naturally with the snippet attached; no placeholder is
            // substituted. `post.mention` is reserved, not wired (D2): the
            // badge + preference-label keys exist; no email templates (no
            // emitter). All four languages below, en fallback. ──────────────
            ["notifications.inbox"] = "Notifications",
            ["notifications.preferences"] = "Preferences",
            ["notifications.mark_all_read"] = "Mark all as read",
            ["notifications.empty"] = "Nothing yet — things that happen to you will show up here.",
            ["notifications.bell"] = "Notifications",
            ["notifications.view"] = "View",
            ["notifications.preferences.title"] = "Notification preferences",
            ["notifications.preferences.intro"] = "Choose which notifications you also get by email. The inbox always records every notification.",
            ["notifications.preferences.save"] = "Save preferences",
            ["notifications.preferences.coming_soon"] = "coming soon",
            // per-kind inbox badges (the eight wired kinds + the reserved post.mention)
            ["notifications.kind.post.reply"] = "Reply",
            ["notifications.kind.post.mention"] = "Mention",
            ["notifications.kind.group.post"] = "Group post",
            ["notifications.kind.group.added"] = "Group added",
            ["notifications.kind.group.invite"] = "Group invite",
            ["notifications.kind.event.rsvp"] = "RSVP",
            ["notifications.kind.event.reminder"] = "Reminder",
            ["notifications.kind.report.filed"] = "Report",
            ["notifications.kind.report.assigned"] = "Report assigned",
            ["notifications.kind.report.resolved"] = "Report resolved",
            ["notifications.kind.todo.assign"] = "To-do assigned",
            // per-kind preference labels (post.mention included, reserved)
            ["notifications.preference.post.reply.label"] = "Replies to my posts",
            ["notifications.preference.post.mention.label"] = "Mentions of me",
            ["notifications.preference.group.post.label"] = "New posts in my groups",
            ["notifications.preference.group.added.label"] = "When I'm added to a group",
            ["notifications.preference.group.invite.label"] = "When I'm invited to a group",
            ["notifications.preference.event.rsvp.label"] = "RSVPs on my events",
            ["notifications.preference.event.reminder.label"] = "Event reminders",
            ["notifications.preference.report.filed.label"] = "Reports filed against my content",
            ["notifications.preference.report.assigned.label"] = "Reports assigned to me",
            ["notifications.preference.report.resolved.label"] = "Resolutions of reports I'm involved in",
            ["notifications.preference.todo.assign.label"] = "To-dos assigned to me",
            // per-kind email subjects (the eight wired kinds — the ADR 0061
            // recipient-language template)
            ["notification.post.reply.subject"] = "A reply was added to your post",
            ["notification.group.post.subject"] = "A new post in your group",
            ["notification.group.added.subject"] = "You've been added to a group",
            ["notification.group.invite.subject"] = "You've been invited to a group",
            ["notification.event.rsvp.subject"] = "An RSVP on your event",
            ["notification.event.reminder.subject"] = "Event reminder",
            ["notification.report.filed.subject"] = "A report was filed on your post",
            ["notification.report.assigned.subject"] = "A report was assigned to you",
            ["notification.report.resolved.subject"] = "A report was resolved",
            ["notification.todo.assign.subject"] = "A to-do was assigned to you",
            // per-kind email bodies (the eight wired kinds — the UGC snippet
            // is appended after one space at emit time; the trailing period
            // keeps the combined line clean)
            ["notification.post.reply.body"] = "Someone replied to one of your posts: ",
            ["notification.group.post.body"] = "A new post in one of your groups: ",
            ["notification.group.added.body"] = "You've been added to the group ",
            ["notification.group.invite.body"] = "You've been invited to join the group ",
            ["notification.event.rsvp.body"] = "Someone RSVP'd on one of your events. ",
            ["notification.event.reminder.body"] = "Here is your upcoming event: ",
            ["notification.report.filed.body"] = "A resident filed a report on one of your posts. ",
            ["notification.report.assigned.body"] = "A report was assigned to you as a moderator. ",
            ["notification.report.resolved.body"] = "A report you were involved in was resolved. ",
            ["notification.todo.assign.body"] = "A to-do was assigned to you: ",
            // admin-lane account kinds (ADR 0077 — the recipient is a GlobalAdmin,
            // not the resident; gated by the instance NotifyAdminsOnSignup flag)
            ["notifications.kind.account.signup"] = "New resident",
            ["notifications.kind.account.verified"] = "Account verified",
            ["notifications.preference.account.signup.label"] = "When a new resident signs up",
            ["notifications.preference.account.verified.label"] = "When a resident verifies their account",
            ["notification.account.signup.subject"] = "A new resident signed up",
            ["notification.account.signup.body"] = "A new resident signed up: ",
            ["notification.account.verified.subject"] = "A resident verified their account",
            ["notification.account.verified.body"] = "A resident verified their account: ",

            // ── ADR 0084 — per-target subscription kinds + the subscriptions UI ──
            ["notification.announcement.subject"] = "A new announcement",
            ["notification.announcement.body"] = "A new announcement was published: ",
            ["notification.community.post.subject"] = "A new post in your community",
            ["notification.community.post.body"] = "A new post in one of your communities: ",
            ["notification.page.child.subject"] = "A new page was added",
            ["notification.page.child.body"] = "A new page was added under a page you follow: ",
            ["notifications.kind.announcement"] = "New announcement",
            ["notifications.kind.community.post"] = "New community post",
            ["notifications.kind.page.child"] = "New sub-page",
            ["notifications.preference.announcement.label"] = "New announcements",
            ["notifications.preference.community.post.label"] = "New posts in my communities",
            ["notifications.preference.page.child.label"] = "New sub-pages on pages I follow",
            ["notifications.subscriptions.title"] = "Notification subscriptions",
            ["notifications.subscriptions.intro"] = "Choose which communities, groups, and pages notify you. Preferences decide which kinds you also get by email; these switches decide which targets notify you at all.",
            ["notifications.subscription.announcement.label"] = "New announcements",
            ["notifications.subscription.community.post.label"] = "New posts in communities",
            ["notifications.subscription.group.post.label"] = "New posts in groups",
            ["notifications.subscription.page.child.label"] = "New sub-pages",
            ["pages.subscribe"] = "Subscribe to updates",
            ["pages.unsubscribe"] = "Unsubscribe from updates",

            // ── PL (ADR 0086, U05) — the /projects landing + the Projects tab ──
            ["pl.tabs.projects"] = "Projects",
            ["pl.index.title"] = "Projects",
            ["pl.index.lede"] = "The neighborhood's goals and projects — the higher-level work on top of the to-dos and boards.",
            ["pl.index.goals_heading"] = "Goals",
            ["pl.index.projects_heading"] = "Projects",
            ["pl.index.new_goal"] = "New goal",
            ["pl.index.new_project"] = "New project",
            ["pl.index.view_projects"] = "View projects →",
            ["pl.index.goals_empty"] = "No goals yet — create one to give the shared work a direction.",
            ["pl.index.projects_empty"] = "No standalone projects yet — create one to start managing the shared work.",
            ["pl.index.start"] = "Start",
            ["pl.index.due"] = "Due",

            // ── PL (ADR 0086, U06) — the goal detail + composer + edit ──
            ["pl.goal.new_heading"] = "New goal",
            ["pl.goal.new_lede"] = "A goal is a direction for the shared work — an optional description, a community filter, and an audience. Projects can hang off it later.",
            ["pl.goal.create"] = "Create goal",
            ["pl.goal.edit_heading"] = "Edit goal",
            ["pl.goal.edit_lead"] = "Update this goal's title and description. Its audience, community, and language are fixed when the goal is created.",
            ["pl.goal.save"] = "Save changes",
            ["pl.goal.edit"] = "Edit goal",
            ["pl.goal.title_hint"] = "A short name for the goal — the feed label.",
            ["pl.goal.description_hint"] = "An optional description of the direction this goal gives the shared work. A goal is usable title-only.",
            ["pl.goal.audience_heading"] = "Audience — who can see this goal",
            ["pl.goal.audience_public"] = "Visible to everyone on the instance.",
            ["pl.goal.audience_restricted"] = "Restricted to the audience grants below.",
            ["pl.goal.projects_heading"] = "Projects in this goal",
            ["pl.goal.projects_empty"] = "No projects under this goal yet — create one to start managing the shared work.",
            ["pl.goal.empty_description"] = "This goal has no description yet.",
            ["pl.project.new_heading"] = "New project",
            ["pl.project.new_lede"] = "A project is a body of shared work — an optional description, a status, start and due dates, a community filter, and an audience. It can hang off a goal.",
            ["pl.project.create"] = "Create project",
            ["pl.project.edit_heading"] = "Edit project",
            ["pl.project.edit_lead"] = "Update this project's title, description, goal, status, and dates. Its audience, community, and language are fixed when the project is created.",
            ["pl.project.save"] = "Save changes",
            ["pl.project.edit"] = "Edit project",
            ["pl.project.title_hint"] = "A short name for the project — the feed label.",
            ["pl.project.description_hint"] = "An optional description of what this project is about. A project is usable title-only.",
            ["pl.project.status"] = "Status",
            ["pl.project.status_hint"] = "An optional state label — the same fixed vocabulary as to-dos. Leave blank for none.",
            ["pl.project.start_date"] = "Start",
            ["pl.project.due_date"] = "Due",
            ["pl.project.dates_hint"] = "Both are optional — leave blank for no date. Shown to viewers in their own timezone.",
            ["pl.project.audience_heading"] = "Audience — who can see this project",
            ["pl.project.audience_public"] = "Visible to everyone on the instance.",
            ["pl.project.audience_restricted"] = "Restricted to the audience grants below.",
            ["pl.project.goal_heading"] = "Goal",
            ["pl.project.goal_hint"] = "An optional goal to organize this project under. Leave blank for a standalone project.",
            ["pl.project.goal_link"] = "Goal",
            ["pl.project.associated_heading"] = "To-dos & boards in this project",
            ["pl.project.todos_heading"] = "To-dos in this project",
            ["pl.project.boards_heading"] = "Boards in this project",
            ["pl.project.todos_empty"] = "No to-dos are in this project yet.",
            ["pl.project.boards_empty"] = "No boards are in this project yet.",
            ["pl.project.empty_description"] = "This project has no description yet.",
            ["pl.todo.project_link"] = "Project",
            ["pl.todo.set_project"] = "Set project",
            ["pl.board.project_link"] = "Project",
            ["pl.board.set_project"] = "Set project",
            ["pl.board.add_to_project"] = "Add to Project…",
            ["pl.board.project_hint"] = "A project link is a display surface — it groups this board under the project, it never limits who can see it.",
            ["pl.goal.delete"] = "Delete goal",
            ["pl.goal.delete_confirm"] = "Delete this goal? Its projects stay in place — the link to this goal simply stops showing.",
            ["pl.project.delete"] = "Delete project",
            ["pl.project.delete_confirm"] = "Delete this project? Its to-dos and boards stay in place — the link to this project simply stops showing.",
        };

    /// <summary>
    /// The curated German (<c>de</c>) baseline (LS U02, ADR 0042 D2/D5). One
    /// entry per key in <see cref="AllKeys"/> — full registry parity, the ADR
    /// 0015 honesty invariant extended to this dictionary. Idioms per ADR 0042
    /// D2: the familiar <c>du</c> register held everywhere, sentence case, no
    /// trailing period on button labels, <c>ß</c> allowed, and every inlined
    /// data token (<c>yyyy-MM-dd HH:mm</c>, <c>§6.4</c>, <c>Allow</c>/<c>Deny</c>,
    /// the <c>rc.editor.*</c> glyph labels, the on-screen <c>Select all</c>
    /// label that the kw-l TagHelper cannot reach) preserved token-for-token
    /// with the <c>en</c> value. These are <b>initial values</b> — seeded once
    /// on a pristine DB (LS U04), then community-owned via the in-app editor
    /// (ADR 0021); an admin edit is never overwritten (ADR 0042 D1).
    /// </summary>
    public static IReadOnlyDictionary<string, string> DeValues { get; } =
        new Dictionary<string, string>
        {
            // ── nav (the shared top-nav, _Layout + _AccountNav) ─────────────
            ["nav.home"]          = "Start",

            // ── events (M4 — ADR 0054: the events nav entry + the Detail footer) ──
            ["nav.events"]        = "Veranstaltungen",
            ["events.created"]    = "Erstellt",
            ["events.edited"]     = "bearbeitet",

            // ── projects (M5 — ADR 0067: the to-do surface nav entry + labels) ──
            ["nav.projects"]                 = "Projekte",
            ["projects.todo.title"]          = "Aufgaben",
            ["projects.todo.lede"]           = "Die gemeinsamen Aufgaben der Nachbarschaft — Arbeit einem Nachbarn zuweisen, in Unteraufgaben aufteilen und auf einem Board ablegen.",
            ["projects.todo.new"]            = "Neue Aufgabe",
            ["projects.todo.new_lead"]       = "Erstelle eine Aufgabe, weise sie optional einem Nachbarn zu und — falls nötig — teile sie in Unteraufgaben auf oder lege sie auf ein Board. Standardmäßig ist sie für alle sichtbar; deaktiviere das im Abschnitt „Zielgruppe“, wenn du einschränken willst.",
            ["projects.todo.title_hint"]     = "Ein kurzer Name für die Aufgabe — die Kartenbeschriftung.",
            ["projects.todo.status"]         = "Status",
            ["projects.todo.status_assignee_hint"] = "Der Status ist einer der festen Aufgaben-Statuswerte (Keine, Nicht begonnen, In Arbeit, Erledigt, Abgebrochen). Eine Aufgabe zuzuweisen gibt diesem Bewohner Handhabung darüber — Anzeige + Handhabung, nie eine Zugangsgrenze.",
            ["projects.todo.assignee"]       = "Zugewiesen an",
            ["projects.todo.unassigned"]     = "Nicht zugewiesen",
            ["projects.todo.assign"]         = "Zuweisen",
            ["projects.todo.unassign"]       = "Zuweisung aufheben",
            ["projects.todo.assign_to"]      = "Zuweisen an…",
            ["projects.todo.assign_people"]  = "Personen",
            ["projects.todo.assign_groups"]  = "Gruppen",
            ["projects.todo.assign_communities"] = "Gemeinschaften",
            ["projects.todo.claim"]          = "Übernehmen",
            ["projects.todo.addressed_to"]   = "Adressiert an",
            ["projects.todo.filter_unassigned"] = "Nur nicht zugewiesene",
            ["projects.todo.add_subtask"]    = "Unteraufgabe hinzufügen",
            ["projects.todo.delete"]         = "Löschen",
            ["projects.todo.parent"]         = "Elternaufgabe",
            ["projects.todo.top_level"]      = "Top-Level-Aufgabe",
            ["projects.todo.parent_hint"]    = "Wähle eine Elternaufgabe, um diese Aufgabe als Unteraufgabe zu erstellen — eine Unteraufgabe ist eine vollständige Aufgabe mit eigenem Status, Zuweisung und Board-Platzierung.",
            ["projects.todo.no_parent"]      = "Keine Elternaufgabe (Top-Level)",
            ["projects.todo.clear_parent"]   = "Elternaufgabe löschen (zur Top-Level machen)",
            ["projects.todo.reparent_hint"]  = "Eine Unteraufgabe ist eine vollständige Aufgabe mit eigenem Status, Zuweisung und Board-Platzierung. Das Umhängen an einen Nachkommen wird abgelehnt (Zyklusschutz).",
            // ADR 0087 — die "Wartet auf"-Abhängigkeit (Chip, Picker, Feed-Schalter).
            ["todo.blocked_by"]              = "Wartet auf",
            ["todo.blocked_by_none"]         = "Kein Blocker",
            ["todo.blocked_generic"]         = "Eine andere Aufgabe (dir nicht sichtbar)",
            ["todo.blocked_filter"]          = "Wartet auf etwas",
            // ADR 0079 — optionale Start-/Fälligkeitsdaten auf einer Aufgabe.
            ["projects.todo.start"]          = "Beginn",
            ["projects.todo.due"]            = "Fällig",
            ["projects.todo.dates_hint"]     = "Beide sind optional — leer lassen für kein Datum. Wird Zuschauenden in deren eigener Zeitzone angezeigt.",
            ["projects.todo.created"]        = "Erstellt",
            ["projects.todo.modified"]       = "Geändert",
            ["projects.todo.no_body"]        = "Kein Text — diese Aufgabe hat nur einen Titel.",
            ["projects.todo.subtasks"]       = "Unteraufgaben",
            ["projects.todo.boards"]         = "Boards",
            ["projects.todo.back"]           = "← Zurück zu den Aufgaben",
            ["projects.todo.untitled"]       = "Aufgabe ohne Titel",
            ["projects.todo.edit"]           = "Bearbeiten",
            ["projects.todo.edit_heading"]   = "Aufgabe bearbeiten",
            ["projects.todo.edit_lead"]      = "Aktualisiere die Details dieser Aufgabe. Deine Zielgruppenwahl ist die einzige Zugangsgrenze — die Gemeinschaftswahl ist nur ein Filter.",
            ["projects.todo.all_communities"] = "Alle Gemeinschaften",
            ["projects.todo.audience_heading"] = "Zielgruppe — wer diese Aufgabe sehen kann",
            ["projects.todo.audience_default"] = "Standard — jeder kann diese Aufgabe sehen. Deaktiviere es nur, wenn du einschränken willst.",
            ["projects.todo.save"]           = "Änderungen speichern",
            ["projects.todo.create"]         = "Aufgabe erstellen",
            ["projects.todo.empty"]          = "Noch keine Aufgaben — erstelle eine, um die gemeinsame Arbeit zu starten.",

            // ── projects (M5 — ADR 0067: die Board-Oberfläche (U10) + Kartenaktionen) ──
            ["projects.todo.copy_to"]        = "Auf Board kopieren",
            ["projects.todo.move_to"]        = "Auf Board verschieben",
            ["projects.todo.move_up"]        = "Nach oben",
            ["projects.todo.move_down"]      = "Nach unten",
            ["projects.todo.move_left"]      = "Nach links",
            ["projects.todo.move_right"]     = "Nach rechts",
            ["projects.board.title"]         = "Boards",
            ["projects.board.lede"]          = "Die gemeinsamen Boards der Nachbarschaft — Aufgaben in Lanes ordnen, durch Status hindurchschieben und die Arbeit sichtbar halten.",
            ["projects.board.new"]           = "Neues Board",
            ["projects.board.new_lead"]      = "Erstelle ein Board, um Aufgaben in Lanes anzuordnen. Eine Lane kann einen Status tragen (eine Aufgabe, die dorthin verschoben wird, übernimmt diesen Status) und ein optionales Limit für die Anzahl der Karten. Standardmäßig ist das Board für alle sichtbar; deaktiviere das im Abschnitt „Zielgruppe“, wenn du einschränken willst.",
            ["projects.board.title_hint"]    = "Ein kurzer Name für das Board — die Feed-Beschriftung.",
            ["projects.board.description_hint"] = "Eine optionale Beschreibung, was dieses Board nachverfolgt. Ein Board ist allein mit Titel nutzbar.",
            ["projects.board.back"]          = "← Zurück zu den Boards",
            ["projects.board.untitled"]      = "Board ohne Titel",
            ["projects.board.delete"]        = "Board löschen",
            ["projects.board.edit"]          = "Board bearbeiten",
            ["projects.board.edit_heading"]  = "Board bearbeiten",
            ["projects.board.edit_lead"]     = "Aktualisiere Titel und Beschreibung dieses Boards. Zielgruppe, Gemeinschaft und Sprache sind bei der Erstellung festgelegt.",
            ["projects.board.save"]          = "Änderungen speichern",
            ["projects.board.create"]        = "Board erstellen",
            ["projects.board.empty"]         = "Noch keine Boards — erstelle eines, um die gemeinsame Arbeit anzuordnen.",
            ["projects.board.no_lanes"]      = "Dieses Board hat noch keine Lanes.",
            ["projects.board.lanes"]         = "Lanes",
            ["projects.board.lanes_hint"]    = "Spalten über das Board — zum Beispiel „Geplant / In Arbeit / Erledigt“. Der Status einer Lane wird, wenn gesetzt, einer Aufgabe beim Verschieben dorthin zugewiesen; ihr Max-items-Limit begrenzt die Anzahl der Karten.",
            ["projects.board.single_lane_hint"] = "Das Board startet mit einer Lane — weitere Lanes und Status fügst du auf der Board-Seite hinzu.",
            ["projects.board.lane.title"]    = "Lane-Titel",
            ["projects.board.lane.status"]   = "Status",
            ["projects.board.lane.max_items"] = "Max. Elemente",
            ["projects.board.lane.order"]    = "Reihenfolge",
            ["projects.board.lane.empty"]    = "Keine Karten in dieser Lane.",
            ["projects.board.lane.save"]     = "Speichern",
            ["projects.board.lane.rename"]   = "Umbenennen",
            ["projects.board.lane.set_limit"] = "Limit setzen",
            ["projects.board.lane.set_status"] = "Status setzen",
            ["projects.board.lane.move_left"] = "Nach links verschieben",
            ["projects.board.lane.move_right"] = "Nach rechts verschieben",
            ["projects.board.lane.add_todo"] = "To-do hinzufügen",
            ["projects.board.lane.add_lane"] = "Lane hinzufügen",
            ["projects.board.lane.add_todo_placeholder"] = "Aufgabentitel",
            ["projects.board.lane.add_lane_placeholder"] = "Neuer Lane-Titel",
            ["projects.board.lane.first_title_placeholder"] = "Geplant",
            ["projects.board.status.none"] = "Keine",
            ["projects.board.status.not_started"] = "Nicht begonnen",
            ["projects.board.status.in_progress"] = "In Arbeit",
            ["projects.board.status.done"] = "Erledigt",
            ["projects.board.status.cancelled"] = "Abgebrochen",
            ["projects.board.fullscreen"]    = "Vollbild",
            ["projects.board.exit_fullscreen"] = "Vollbild beenden",
            ["projects.board.audience_heading"] = "Zielgruppe — wer dieses Board sehen kann",
            ["projects.board.audience_default"] = "Standard — jeder kann dieses Board sehen. Deaktiviere es nur, wenn du einschränken willst.",

            // ── common (shared action/field labels reused across resident-facing views) ──
            ["common.cancel"]   = "Abbrechen",
            ["common.save"]     = "Speichern",
            ["common.title"]    = "Titel",
            ["common.body"]     = "Text",
            ["common.language"] = "Sprache",
            ["common.optional"] = "optional",
            ["common.add"]      = "Hinzufügen",
            ["common.remove"]   = "Entfernen",
            ["common.filter"]   = "Filter",
            ["common.name"]         = "Name",
            ["common.display_name"] = "Anzeigename",
            ["common.email"]        = "E-Mail",
            ["common.password"]     = "Passwort",
            ["common.filter_name"]  = "Nach Namen filtern…",
            ["account.confirm_password"] = "Passwort bestätigen",
            ["groups.name_label"]     = "Gruppenname",
            ["groups.desc_placeholder"] = "Worum geht es in dieser Gruppe? (sichtbar für alle, die diese Seite erreichen)",
            ["posts.report_reason_placeholder"] = "Optional einen Grund für den Moderator hinzufügen…",
            ["tags.events_example"] = "z. B. Aufräumen, Gesellig, Garten",
            ["tags.pages_example"]  = "z. B. Hygiene, Budget, maple-street",
            ["admin.community_description"] = "Beschreibung (optional)",
            ["admin.community_mandatory"]   = "Pflicht",
            ["admin.add_community"]         = "Gemeinschaft hinzufügen",
            ["admin.roles_heading"]         = "Rollen",
            ["admin.roles_independent_hint"] = "Unabhängig — ein Bewohner kann beliebig viele Rollen kombinieren (ADR 0030). Nichts angekreuzt = ein einfacher Member.",
            ["admin.moderator_scope"]       = "Moderator-Bereich",
            ["admin.moderator_scope_hint"]  = "Die Gemeinschaften, die dieses Konto moderieren darf. Nur relevant, wenn die Moderator-Rolle angehakt ist — die Core-Lane löscht Scope-Zeilen, wenn die Moderator-Rolle aus ist.",
            ["nav.announcements"] = "Ankündigungen",
            ["nav.community"]     = "Gemeinschaft",
            ["nav.groups"]        = "Gruppen",
            ["nav.pages"]         = "Seiten",
            ["nav.tags"]          = "Tags",
            ["nav.directory"]     = "Verzeichnis",
            ["nav.sign_in"]       = "Anmelden",
            ["nav.sign_up"]       = "Registrieren",
            ["nav.profile"]       = "Profil",
            ["nav.admin"]         = "Verwaltung",
            ["nav.translations"]  = "Übersetzungen",
            ["nav.sign_out"]      = "Abmelden",
            ["nav.children"]      = "Kinder",
            ["nav.my_drafts"]     = "Meine Entwürfe",
            ["nav.account"]       = "Konto",

            // ── guardian (the /me/children child-accounts surface) ─────────
            ["guardian.title"]        = "Deine Kinder",
            ["guardian.lead"]         = "Die Konten, die du für ein Kind eingerichtet hast, und die Kontrollen, die du über jedes davon hast.",
            ["guardian.empty"]        = "Noch keine Kinder.",
            ["guardian.add"]          = "Kinderkonto hinzufügen",
            ["guardian.manage_title"] = "Kinderkonto verwalten",

            // ── guardian (per-child surface, GU ADR 0028) ─────────────────
            ["guardian.back"]               = "Zurück zu deinen Kindern",
            ["guardian.account_label"]       = "Konto",
            ["guardian.group_memberships"]   = "Gruppenmitgliedschaften",
            ["guardian.community_memberships"] = "Gemeinschaftsmitgliedschaften",
            ["guardian.no_groups"]           = "Keine Gruppenmitgliedschaften.",
            ["guardian.no_communities"]      = "Keine Gemeinschaftsmitgliedschaften.",
            ["guardian.group_id_label"]      = "Gruppen-ID",
            ["guardian.community_id_label"]  = "Gemeinschafts-ID",
            ["guardian.pending_invitations"] = "Ausstehende Gruppeneinladungen",
            ["guardian.no_invitations"]      = "Keine ausstehenden Einladungen.",
            ["guardian.approve"]             = "Genehmigen",
            ["guardian.handover"]            = "Konto übergeben",
            ["guardian.handover_hint"]       =
                "Die Aufhebung der Vormundschaft übergibt das Konto dem Kind. " +
                "Die Mitgliedschaften bleiben erhalten, und die eigenen " +
                "Einstellmöglichkeiten kommen beim nächsten Lesen zurück.",
            ["guardian.dissolve"]            = "Vormundschaft auflösen",
            ["guardian.suspended"]           = "Gesperrt",
            ["guardian.unsuspend"]           = "Wieder aktivieren",
            ["guardian.suspend"]             = "Sperren",
            ["guardian.display_name"]        = "Anzeigename",
            ["guardian.email"]               = "E-Mail-Adresse",
            ["guardian.password"]            = "Passwort",
            ["guardian.child_email_hint"]    =
                "Das Kind bestätigt seine eigene E-Mail zur Anmeldung — der gewöhnliche Anmeldevorgang.",

            // ── posts (composer helper hints) ──────────────────────────────
            ["posts.title_hint"] =
                "Eine kurze Überschrift (bis zu 120 Zeichen). Leer lassen für " +
                "einen reinen Textbeitrag — die Liste zeigt stattdessen deine " +
                "erste Textzeile.",
            ["posts.language_hint"] =
                "Die Sprache, in der du diesen Beitrag schreibst. Das ist nur " +
                "ein Schlagwort — es wird nicht übersetzt — und es hält den " +
                "Text später auffindbar und erlaubt es Lesern, eigene " +
                "Sprachversionen hinzuzufügen.",

            // ── faq (drop-in FAQ section, _FaqAccordion) ──────────────────
            ["faq.title"]     = "Häufig gestellte Fragen",
            ["faq.q1"]        = "Wer kann meine Beiträge sehen?",
            ["faq.a1"]        =
                "Den, den du wählst, wenn du postest: nur du, deine Gruppe, " +
                "deine Gemeinschaft oder alle. Das Publikum " +
                "ist eine pro-Beitrag-Entscheidung — keine globale Einstellung.",
            ["faq.q2"]        = "Wo stehen Ankündigungen wie Wasserausfälle und Straßenerneuerungen?",
            ["faq.a2_intro"]  = "Feste Ankündigungen unter",
            ["faq.a2_link"]   = "/announcements",
            ["faq.a2_outro"]  =
                " — die Lese-Seite ist offen, damit niemand sich anmelden " +
                "muss, um herauszufinden, wann die Straße neu gestrichen wird.",
            ["faq.q3"]        = "Ist das standardmäßig privat?",
            ["faq.a3"]        =
                "Ja. Jeder Zugriff, bei dem Zugang zu eingeschränkten Inhalten " +
                "gewährt oder verweigert wird, wird aufgezeichnet. Die Daten " +
                "bleiben auf einer einzelnen Datenbank im Besitz des Wirts der " +
                "Nachbarschaft, und es sind keine Werbung oder Tracking " +
                "eingebaut.",

            // ── error (shared error page, Shared/Error.cshtml) ────────────
            ["error.title"]           = "Fehler.",
            ["error.subtitle"]        = "Bei der Verarbeitung Ihrer Anfrage ist ein Fehler aufgetreten.",
            ["error.development_title"] = "Entwicklungsmodus",
            ["error.development_hint"] =
                "Das Umschalten in die Entwicklungsumgebung zeigt weitere " +
                "Details zum aufgetretenen Fehler. Die Entwicklungsumgebung " +
                "sollte für eingesetzte Anwendungen nicht aktiviert sein — " +
                "sie kann sensible Informationen aus Fehlern an Endnutzer " +
                "offenlegen.",
            ["error.request_id"]      = "Anfragen-ID:",

            // ── guardian assignment (GA ADR 0038) ───────────────────────────
            ["guardian.otherGuardians.title"] = "Weitere Vormünder",
            ["guardian.otherGuardians.empty"] = "Keine weiteren Vormünder zugewiesen.",
            ["guardian.assign.title"]        = "Vormund zuweisen",
            ["guardian.assign.email"]        = "E-Mail-Adresse des Vormunds, den du zuweisen möchtest",
            ["guardian.assign.submit"]       = "Zuweisen",
            ["guardian.assign.noAccount"]    = "Kein Konto mit dieser E-Mail.",
            ["guardian.assign.self"]         = "Du bist bereits Vormund dieses Kindes.",
            ["guardian.assign.success"]      = "Vormund zugewiesen.",

            // ── footer (the shared footer, _Layout) ─────────────────────────
            ["footer.tagline"]  =
                "Ein privater Ort für eine Nachbarschaft — der Feed, die Gruppen und die gepinnten Notizen. " +
                "Was auf eurer Straße passiert, bleibt auf eurer Straße.",
            ["footer.copyright"] = "· selbst gehostet von eurer Gemeinschaft",
            ["footer.gtk_heading"] = "Gut zu wissen",
            ["footer.gtk_privacy"] =
                "Privat per Vorgabe: Das Publikum jedes Beitrags wählt dessen Autor:in, " +
                "und alles ist für dich, nicht für die Welt, lesbar.",
            ["footer.gtk_oss"] =
                "Kumunita ist Open Source — der Code, die Entscheidungen, die Doku.",
            // SP U03 (ADR 0043 D4) — die Footer-Spalte "Plattform": die fünf
            // ausgelieferten Plattform-Oberflächen (About-View + die vier
            // Page-Dokumente), für jede:n Besucher:in verlinkt.
            ["footer.platform.heading"] = "Plattform",
            ["footer.platform.about"]   = "Über uns",
            ["footer.platform.terms"]   = "Nutzungsbedingungen",
            ["footer.platform.help"]    = "Hilfe",
            ["footer.platform.privacy"] = "Datenschutz",
            ["footer.platform.conduct"] = "Verhaltenskodex",
            // Die Spalte "Das Projekt" (home / about / footer): Überschriften
            // + die drei RepositoryInfo.Links-Labels (über dynamische kw-l-Keys
            // aus der Linkliste, damit sie pro Sprache aufgelöst werden).
            ["footer.project.heading"] = "Das Projekt",
            ["repo.source_code"]      = "Quellcode",
            ["repo.documentation"]    = "Dokumentation",
            ["repo.non_technical"]    = "Für nicht-technische Anwohnende",

            // ── settings (the language-picker labels) ───────────────────────
            ["settings.settings"]       = "Einstellungen",
            ["settings.choose_language"] = "Wähle deine Sprache",

            // ── settings — account help ─────────────────────────────────────
            ["settings.help_heading"]     = "Hilfe zu deinem Konto",
            ["settings.help_lede"]        =
                "Klemmt bei deinem Konto etwas — ein Passwort, dein Zugriff oder sonst etwas? " +
                "Dieser Leitfaden führt dich Schritt für Schritt durch.",

            // ── settings — timezone (ADR 0019) ──────────────────────────────
            ["settings.timezone_title"]        = "Zeitzone",
            ["settings.timezone_lede"]         =
                "Wähle die Zeitzone, die dir die Plattform anzeigt. Deine Auswahl wird in deinem Konto gespeichert — " +
                "sie wird beim nächsten Seitenaufruf wirksam und betrifft nie andere Anwohner:innen.",
            ["settings.timezone_label"]        = "Deine Zeitzone",
            ["settings.timezone_default_marker"] = "— Plattform-Vorgabe",
            ["settings.timezone_default_note"] = "Die Plattform-Voreinstellung ist ",
            ["settings.timezone_default_tail"] =
                ". Wenn du deine Einstellung zurücksetzt, wird die Plattform-Voreinstellung verwendet.",
            ["settings.timezone_reset"]        = "Auf die Plattform-Voreinstellung zurücksetzen",
            ["settings.timezone_save"]         = "Speichern",
            ["settings.timezone_flash_set"]    = "Zeitzone auf \"{0}\" gesetzt — sie wirkt ab der nächsten Anfrage.",
            ["settings.timezone_flash_reset"]  = "Zeitzone zurückgesetzt — die Plattform-Voreinstellung wird verwendet.",
            ["settings.timezone_unknown"]      = "Unbekannte Zeitzone",

            // ── settings — date format (ADR 0020) ───────────────────────────
            ["settings.dateformat_title"]        = "Datum- und Zeitformat",
            ["settings.dateformat_lede"]         =
                "Wähle, wie dir Datum und Zeit angezeigt werden. Deine Auswahl wird in deinem Konto gespeichert — " +
                "sie wird beim nächsten Seitenaufruf wirksam und betrifft nie andere Anwohner:innen.",
            ["settings.dateformat_label"]        = "Dein Datum- und Zeitformat",
            ["settings.dateformat_default_marker"] = "— Plattform-Vorgabe",
            ["settings.dateformat_default_note"] = "Die Plattform-Voreinstellung ist ",
            ["settings.dateformat_default_tail"] =
                ". Wenn du deine Einstellung zurücksetzt, wird die Plattform-Voreinstellung verwendet.",
            ["settings.dateformat_custom_label"] = "Eigenes Format",
            ["settings.dateformat_custom_hint"]  =
                "Eine .NET-Zeitreihenformatzeichenfolge (z. B. yyyy-MM-dd HH:mm). Leer lassen, um eine Voreinstellung zu verwenden.",
            ["settings.dateformat_reset"]        = "Auf die Plattform-Voreinstellung zurücksetzen",
            ["settings.dateformat_save"]         = "Speichern",
            ["settings.dateformat_flash_set"]    = "Datum- und Zeitformat gesetzt — es wirkt ab der nächsten Anfrage.",
            ["settings.dateformat_flash_reset"]  = "Datum- und Zeitformat zurückgesetzt — die Plattform-Voreinstellung wird verwendet.",

            // ── admin — the platform-default timezone ───────────────────────
            ["admin.timezone_title"]    = "Plattform-Vorgabe: Zeitzone",
            ["admin.timezone_lede"]     =
                "Die Zeitzone, auf die die Zeitstempel der Anwohner:innen zurückfallen, " +
                "wenn sie keine persönliche Vorgabe gesetzt haben.",
            ["admin.timezone_label"]    = "Vorgabe-Zeitzone",
            ["admin.timezone_save"]     = "Speichern",

            // ── admin — the platform-default date format (ADR 0020) ─────────
            ["admin.dateformat_title"]    = "Plattform-Vorgabe: Datum- und Zeitformat",
            ["admin.dateformat_lede"]     =
                "Das Datum- und Zeitformat, auf das die Zeitstempel der Anwohner:innen zurückfallen, " +
                "wenn sie keine persönliche Vorgabe gesetzt haben.",
            ["admin.dateformat_label"]    = "Vorgabe-Datum- und Zeitformat",
            ["admin.dateformat_custom_label"] = "Eigenes Format",
            ["admin.dateformat_custom_hint"]  =
                "Eine .NET-Zeitreihenformatzeichenfolge (z. B. yyyy-MM-dd HH:mm). Leer lassen, um eine Voreinstellung zu verwenden.",
            ["admin.dateformat_save"]     = "Speichern",

            // ── admin — the sign-up gate (ADR 0050) ─────────────────────────
            ["admin.signup_title"]    = "Registrierung",
            ["admin.signup_lede"]     =
                "Ob neue Anwohner:innen ein Konto selbst anlegen dürfen. " +
                "Wird das Tor geschlossen, ist die Registrierung nur noch per Einladung — bestehende Anwohner:innen sind nicht betroffen.",
            ["admin.signup_open"]     = "Offen — Anwohner:innen können sich registrieren",
            ["admin.signup_invitation_only"] = "Nur per Einladung — neue Selbstregistrierungen sind gesperrt",
            ["admin.signup_save"]     = "Speichern",
            ["admin.signup_notify_title"]   = "Admins benachrichtigen",
            ["admin.signup_notify_lede"]    = "Wenn ein neues Mitglied sich registriert und wenn ein Mitglied sein Konto verifiziert, bekommen die GlobalAdmins eine Posteingangsbenachrichtigung und eine best-effort-E-Mail. Ausgestellt, wird das stummgeschaltet — der Account-Vorgang selbst ist davon unberührt.",
            ["admin.signup_notify_on"]      = "An — Admins werden bei neuen Registrierungen und Verifizierungen benachrichtigt",
            ["admin.signup_notify_off"]     = "Aus — keine Admin-Benachrichtigungen bei Registrierung / Verifizierung",

            // ── account — sign-up-closed notice (ADR 0050) ─────────────────
            ["account.signup_closed_title"] = "Die Registrierung ist geschlossen",
            ["account.signup_closed_body"]  =
                "Die Registrierung ist auf dieser Instanz derzeit nur per Einladung möglich. " +
                "Falls du eingeladen wurdest, wird eine:r Administrator:in dein Konto anlegen und dir den Anmelde-Link senden.",

            // ── home (the hero + section lead) ──────────────────────────────
            ["home.eyebrow"] = "Wo das Projekt steht",
            ["home.lead"] =
                "Ein privater Ort für eine Nachbarschaft — Schritt für Schritt und in der offenen Entwicklung. " +
                "Alles, was unten aufgeführt ist, ist Teil dieses Plans — und du darfst gern einen Blick darauf werfen, wie es entsteht.",
            ["home.support"] = "Fragen oder Feedback? Schreibe an",

            // ── home intro (was Kumunita ist + die drei Oberflächen) ─────────
            ["home.intro_eyebrow"]  = "Ein privater Ort für eine Nachbarschaft",
            ["home.intro_lead"] =
                "Ein ruhiger Ort für alles, was eure Straße bewegt — der Feed, die Gruppen und " +
                "die Hinweise, die mehr verdienen als einen Gruppenchat. Privat, verständlich und euer.",
            ["home.about_link"]    = "Was es ist & wie es funktioniert",
            ["home.feature_feed_title"]  = "Ein Feed für die Straße",
            ["home.feature_feed_body"] =
                "Beiträge und Threads aus euren Kiezen, an einem ruhigen Ort — kein Algorithmus, kein Lärm.",
            ["home.feature_groups_title"]  = "Gruppen, die passen",
            ["home.feature_groups_body"] =
                "Gartenswap, Bücherclub, Nachbarschaftswache — eine Gruppe für alles, was die Nachbarschaft schon macht.",
            ["home.feature_pinned_title"]  = "Angepinnt, wo es zählt",
            ["home.feature_pinned_body"] =
                "Wasserausfälle, Baustellen, neue Tempobremsen — Hinweise, die bleiben und nicht davonscrollen.",

            // ── home "Neues"-Feed (angemeldete Nutzer) ─────────────────────
            ["home.feed_title"]          = "Neues um die Ecke",
            ["home.feed_posts"]          = "Beiträge",
            ["home.feed_announcements"]  = "Hinweise",
            ["home.feed_pages"]          = "Seiten",
            ["home.feed_view_all"]       = "Alle ansehen",
            ["home.feed_empty"] =
                "Noch nichts gepostet — sei die erste Person, die die Unterhaltung im Feed anstößt.",
            ["home.feed_badge_pinned"]   = "Angepinnt",

            // ── home Roadmap (der Plan, nach der Intro) ────────────────────
            ["home.roadmap_heading"] = "In der offenen Entwicklung, Meilenstein für Meilenstein",

            // ── account (Login / Signup — titles + primary actions) ─────────
            ["account.login_title"]   = "Anmelden",
            ["account.login_submit"]  = "Anmelden",
            ["account.login_no_account"] = "Noch kein Konto?",
            ["account.login_remember"] = "Angemeldet bleiben",
            ["account.login_setup_hint"] = "Du hast ein Erststart-Setup-Token erhalten?",
            ["account.login_setup_link"] = "Setup abschließen",
            ["account.login_signup"] = "Registrieren",
            ["account.signup_title"]  = "Registrieren",
            ["account.signup_submit"] = "Registrieren",
            ["account.signup_has_account"] = "Du hast schon ein Konto?",

            // ── posts (Index / New / Edit) ──────────────────────────────────
            ["posts.feed_all_sections"] = "Gemeinschaft",
            ["posts.write"]        = "Beitrag schreiben",
            ["posts.new_title"]    = "Beitrag schreiben",
            ["posts.new_intro"] =
                "Standardmäßig kann jede:r in der Gemeinschaft, die du unten wählst, " +
                "deinen Beitrag sehen. Schalte das in der Publikums-Sektion nur aus, " +
                "wenn du einengen möchtest, wer ihn sehen darf — bestimmte Personen oder Gruppen.",
            ["posts.community_hint"] =
                "Die Gemeinschaft bestimmt, in welchem Feed dein Beitrag erscheint — und, " +
                "standardmäßig, wer ihn sehen darf (jedes Mitglied dieser Gemeinschaft). " +
                "Um das Publikum einzuschränken, schalte „Alle in dieser Gemeinschaft“ " +
                "in der Publikums-Sektion unten aus.",
            ["posts.new_submit"]   = "Veröffentlichen",
            ["posts.edit_title"]   = "Beitrag bearbeiten",
            ["posts.edit_save"]    = "Änderungen speichern",
            ["posts.save_as_draft"] =
                "Als Entwurf speichern",
            ["posts.save_as_draft_hint"] =
                "Ein Entwurf wird gespeichert, ist aber für niemanden sichtbar — auch nicht für Admins — " +
                "bis du ihn veröffentlichst. Du findest ihn unter „Meine Entwürfe“.",
            ["posts.draft_badge"]   = "Entwurf",
            ["posts.draft_note"] =
                "Dieser Beitrag ist ein Entwurf — nur du kannst ihn sehen. Veröffentliche " +
                "ihn, um ihn sichtbar zu machen.",
            ["posts.publish"]       = "Veröffentlichen",
            ["my_drafts.title"]     = "Meine Entwürfe",
            ["my_drafts.empty"]     = "Du hast keine Entwürfe.",
            ["posts.audience_all_members"] =
                "Alle in dieser Gemeinschaft",
            ["posts.audience_all_members_hint"] =
                "Die Vorgabe — jedes Mitglied der Gemeinschaft oben kann " +
                "diesen Beitrag sehen. Schalte es nur aus, wenn du einengen möchtest, wer " +
                "ihn sehen darf.",
            ["posts.audience_all_members_hint_edit"] =
                "Wenn aktiv, kann jedes Mitglied dieser Gemeinschaft den Beitrag sehen. " +
                "Schalte es aus, um das Publikum auf bestimmte Personen oder " +
                "Gruppen einzuschränken.",
            ["posts.audience_combine"] =
                "Wie die Auswahl kombiniert wird",
            ["posts.audience_restrict_hint"] =
                "Diese Auswahl ist zusätzlich — „Alle in dieser Gemeinschaft“ " +
                "bleibt an, solange du es nicht ausschaltest, der Beitrag ist also " +
                "für die ganze Gemeinschaft und die hier gewählten Personen sichtbar.",
            ["posts.audience_only_picks"] =
                "Was du hier wählst, wird das Publikum des Beitrags — es wird " +
                "nichts darüber oder darunter hinzugefügt. Eine leere Auswahl (mit " +
                "„Alle in dieser Gemeinschaft“ aus) bedeutet, dass nur du den " +
                "Beitrag sehen kannst.",
            ["posts.empty_can_post"] =
                "Noch keine Beiträge hier. Schreibe den ersten — er ist nur für das Publikum sichtbar, das du " +
                "im Editor unterhalb der Überschrift wählst.",
            ["posts.empty"]        = "Noch keine Beiträge hier.",

            // ── groups (Index / Detail / New / PostDetail) ──────────────────
            ["groups.title"]          = "Gruppen",
            ["groups.lead"]           = "Die Gemeinschaften, die du betreibst oder denen du angehörst.",
            ["groups.create"]         = "Gruppe erstellen",
            ["groups.empty"]          = "Noch keine Gruppen.",
            ["groups.back_all"]       = "← Alle Gruppen",
            ["groups.tab.feed"]       = "Feed",
            ["groups.tab.members"]    = "Mitglieder",
            ["groups.tab.settings"]   = "Einstellungen",
            ["groups.posts_heading"]  = "Beiträge",
            ["groups.new_post"]       = "Neuer Beitrag",
            ["groups.posts_empty_can"] =
                "Noch keine Beiträge. Schreibe den ersten — er ist für die aktuellen Mitglieder sichtbar.",
            ["groups.posts_empty"]    = "Noch keine Beiträge hier.",
            ["groups.members_heading"]  = "Mitglieder",
            ["groups.members_empty"]    = "Noch keine Mitglieder.",
            ["groups.member_leave"]     = "Verlassen",
            ["groups.invite_heading"]   = "Bewohner:in einladen",
            ["groups.about_heading"]        = "Über diese Gruppe",
            ["groups.about_desc_label"]     = "Beschreibung (optional)",
            ["groups.about_desc_clear_hint"] = "Leer lassen, um die Beschreibung zu löschen.",
            ["groups.about_desc_save"]      = "Beschreibung speichern",
            ["groups.privacy_heading"]      = "Privatsphäre",
            ["groups.private_label"]        = "Private Gruppe",
            ["groups.private_hint"]         =
                "Eine private Gruppe (z. B. eine Familie) ist für alle anderen unsichtbar; nur die Menschen, die du als Mitglieder hinzufügst, können sie sehen und nutzen. Das Häkchen aufheben, um die Gruppe wieder öffentlich zu machen.",
            ["groups.new_title"]      = "Beitrag in dieser Gruppe",
            ["groups.new_back"]       = "zurück zur Gruppe",
            ["groups.new_submit"]     = "In die Gruppe posten",
            // ── groups list (the Airy layout — the invitation panel + the
            //    member-count word on each group card) ───────────────────────
            ["groups.invitations"]    = "Einladungen",
            ["groups.invitations_pending"] = "ausstehend",
            ["groups.invited_by"]     = "Eingeladen von",
            ["groups.invite_accept"]  = "Annehmen",
            ["groups.invite_decline"] = "Ablehnen",
            ["groups.members_one"]    = "Mitglied",
            ["groups.members_many"]   = "Mitglieder",
            // ── community feed (the Airy layout — the left rail + the
            //    mandatory-community note) ───────────────────────────────────
            ["community.browse"]          = "Gemeinschaften",
            ["community.all"]             = "Alle",
            ["community.mandatory_badge"] =
                "Alle — verbindlich",
            ["community.mandatory_note"]  =
                "Das ist die verbindliche Gemeinschaft des Viertels — alle " +
                "hier sind Teil davon; niemand kann entfernt oder „Austritt“ " +
                "gewählt werden.",

            // ── ADR 0026 — group name/description translations ───────────
            ["groups.translations_label"] = "Übersetzungen",
            ["groups.translations_none"] = "Noch keine",
            ["groups.translation_add"] = "Hinzufügen",
            ["groups.translation_name_label"] = "Name",
            ["groups.translation_desc_label"] = "Beschreibung",
            ["groups.translation_optional"] = "optional",
            ["groups.translation_min_one"] = "Mindestens Name oder Beschreibung ist erforderlich.",
            ["groups.translation_save"] = "Übersetzung speichern",

            // ── directory (page heading + lead) ─────────────────────────────
            ["directory.title"] = "Verzeichnis",
            ["directory.lead"]  = "Alle in der Nachbarschaft — jede:r Anwohner:in auf der Plattform.",
            ["directory.empty"] = "Noch keine Anwohner:innen in dieser Nachbarschaft.",

            // ── profile (page heading + primary action) ─────────────────────
            ["profile.title"]      = "Dein Profil",
            ["profile.save_avatar"] = "Avatar speichern",

            // ── profile (Edit page) ─────────────────────────────────────────
            ["profile.edit_lede"] =
                "Hier bestimmst du, was andere Anwohner:innen über dich sehen. " +
                "Was du hier wählst, zeigt das Nachbarnverzeichnis " +
                "genau so — ohne Überraschungen.",
            ["profile.avatar_heading"] = "Dein Avatar",
            ["profile.avatar_hint"] =
                "JPEG, PNG, WebP oder GIF · bis zu 5 MB. Speichern ersetzt den " +
                "aktuell im Verzeichnis gezeigten Avatar.",
            ["profile.name_email_heading"] = "Dein Name + E-Mail",
            ["profile.address_heading"] = "Deine Adresse + Telefon (optional)",
            ["profile.address_hint"] =
                "Wird in der Verzeichnisliste und im Detail nur angezeigt, wenn du unten " +
                "auch den Kontaktkasten aktiviert hast; leer lassen, um deine " +
                "Straße für dieses Profil privat zu halten.",
            ["profile.phone_hint"] =
                "Wird im Verzeichnis-Detail nur angezeigt, wenn du unten " +
                "den Kontaktkasten aktiviert hast; leer lassen, um deine Nummer " +
                "für dieses Profil privat zu halten.",
            ["profile.who_heading"] = "Wer was sehen kann",
            ["profile.optin_contact"] = "Meine Kontaktdaten teilen (Adresse, E-Mail, Telefon)",
            ["profile.optin_contact_note"] =
                "Lass dies aus, wenn du deine Adresse, " +
                "E-Mail und Telefon komplett vor dem " +
                "Verzeichnis verbergen möchtest. Wenn es an ist, wählst du " +
                "unten aus, wer es sehen darf.",
            ["profile.save"] = "Speichern",
            ["profile.preview_link"] = "Vorschau — so erscheine ich",

            // ── profile (Edit page — field labels + input placeholders) ──────
            ["profile.display_name_label"] = "Anzeigename",
            ["profile.address_label"] = "Adresse (die Straße, an der du wohnst)",
            ["profile.address_placeholder"] =
                "Straße — wird Nachbarn angezeigt, wenn du unten zustimmst",
            ["profile.phone_label"] = "Telefonnummer",
            ["profile.phone_placeholder"] =
                "Telefon — wird Nachbarn angezeigt, wenn du unten zustimmst",
            ["profile.audience_mode_any_word"] = "Beliebig",
            ["profile.audience_mode_all_word"] = "Alle",

            // ── profile (Preview page) ───────────────────────────────────────
            ["profile.preview_back"] = "← Zurück zum Editor",
            ["profile.preview_title"] = "Vorschau — so erscheine ich",
            ["profile.preview_avatar_note"] =
                "Dein Avatar, wie er neben deinem Namen im " +
                "Nachbarnverzeichnis erscheint.",
            ["profile.preview_readonly_lead"] =
                "Das ist eine schreibgeschützte Vorschau. Sie zeigt, wie dein Profil " +
                "für ",
            ["profile.preview_readonly_tail"] =
                " im Nachbarnverzeichnis erscheint. Es ändert nichts an deinem " +
                "gespeicherten Profil — um etwas zu ändern, ",
            ["profile.preview_edit_link"] = "bearbeite dein Profil",
            ["profile.preview_visible_badge"] = "Sichtbar.",
            ["profile.preview_visible_tail"] =
                "sichtest du diesen Kontaktkasten im Nachbarnverzeichnis.",
            ["profile.preview_hidden_badge"] = "Kontaktdaten verborgen.",
            ["profile.preview_hidden_tail"] =
                "sieht keinen Kontaktkasten. Dein Name (und " +
                "das Verifizierungs-Abzeichen, falls vorhanden) erscheint " +
                "weiterhin im Verzeichnis — nur die Kontaktdaten sind verborgen.",
            ["profile.contact_address"] = "Adresse",
            ["profile.contact_email"] = "E-Mail",
            ["profile.contact_phone"] = "Telefon",
            ["profile.edit_profile_btn"] = "Profil bearbeiten",

            // ── profile (the _AudienceEditor shared partial) ────────────────
            ["profile.audience_visibility"] = "Wer dein Profil sehen kann",
            ["profile.audience_contact"] = "Wer deine Kontaktdaten sehen kann",
            ["profile.audience_off_note"] =
                "Deine Kontaktdaten sind aktuell für alle verborgen. " +
                "Um das zu ändern, schalte oben „Meine Kontaktdaten teilen“ ein.",
            ["profile.audience_match_mode"] = "Abgleichmodus",
            ["profile.audience_mode_any"] =
                "eine Person ist erlaubt, wenn sie eine der gewählten Personen/Gruppen erfüllt",
            ["profile.audience_mode_all"] =
                "eine Person ist nur erlaubt, wenn sie alle gewählten Personen/Gruppen erfüllt",
            ["profile.audience_all_residents"] =
                "Alle auf der Plattform (alle angemeldeten Bewohner:innen)",
            ["profile.audience_all_residents_hint"] =
                "Neue Bewohner:innen sehen das automatisch — du musst nichts neu eintragen, wenn jemand beitritt.",

            // ── posts (Detail page) ──────────────────────────────────────────
            ["posts.back_to"] = "zurück zu",
            ["posts.detail_edit"] = "Bearbeiten",
            ["posts.edited"] = "bearbeitet",
            ["posts.detail_why"] =
                "Du kannst diesen Beitrag sehen, weil er dir freigegeben wurde — " +
                "entweder du bist Autor:in, oder er wurde mit dir oder deinen Gruppen geteilt.",
            ["posts.report_button"] = "Diesen Beitrag melden",
            ["posts.reply_report_button"] = "Diese Antwort melden",
            ["posts.report_reason_label"] = "Was ist schiefgelaufen?",
            ["posts.report_optional"] = "optional",
            ["posts.report_note"] =
                "Eine Meldung ist nur eine Eingangsaktion — sie ändert " +
                "nichts daran, was du sehen kannst, und ein Moderator kann nachfassen.",
            ["posts.report_submit"] = "Melden",
            ["posts.replies_heading"] = "Antworten",
            ["posts.replies_empty"] =
                "Noch keine Antworten. Wenn du diesen Beitrag sehen kannst, kannst du auch darauf antworten.",
            ["posts.reply_heading_author"] = "Antwort (du bist Autor:in dieses Beitrags)",
            ["posts.reply_heading"] = "Antwort",
            ["posts.reply_label"] = "Antwort",
            ["posts.reply_language_label"] = "Sprache",
            ["posts.reply_language_note"] =
                "Die Sprache, in der du antwortest — ein Tag, keine " +
                "Übersetzung.",
            ["posts.reply_audience_note"] =
                "Antworten haben kein eigenes Publikum — sie sind unter " +
                "der einzelnen Publikumsentscheidung dieses Beitrags sichtbar (die " +
                "C-M3·1-Regel „reply-inherits“). Du antwortest nur dort, " +
                "wo der Beitrag selbst sichtbar ist.",
            ["posts.reply_submit"] = "Antworten",
            ["posts.reply_edit"] = "Bearbeiten",
            ["posts.reply_save"] = "Speichern",
            ["posts.reply_edited"] = "bearbeitet",

            // ── posts (Detail page) — author soft-delete (ADR 0024) ──
            ["posts.delete"] = "Löschen",
            ["posts.reply_delete"] = "Löschen",
            ["posts.deleted_placeholder"] =
                "Dieser Beitrag wurde von dessen Autor:in gelöscht.",
            ["posts.reply_deleted_placeholder"] =
                "Diese Antwort wurde von dessen Autor:in gelöscht.",

            // ── posts (Detail page) — user-added translations (ADR 0022) ──
            ["posts.translations_label"] = "Übersetzungen",
            ["posts.translations_none"] = "noch keine",
            ["posts.translation_add"] = "Hinzufügen",
            ["posts.translation_title_label"] = "Überschrift",
            ["posts.translation_body_label"] = "Text",
            ["posts.translation_optional"] = "optional",
            ["posts.translation_save"] = "Übersetzung speichern",
            ["posts.translation_edit"] = "Bearbeiten",
            ["posts.translation_remove"] = "Entfernen",

            // ── pages (the PG lane — tree browse + post view, ADR 0039) ──────
            ["pages.title"]       = "Seiten",
            ["pages.new_button"]  = "Neue Seite",
            ["pages.none"] =
                "Noch keine Seiten. Global-Admins können die erste Systemseite " +
                "erstellen — eine „Über uns“-Seite ist ein üblicher Einstieg — und " +
                "jede:r Anwohner:in kann einen eigenen Blog (eine eigene Seite) starten.",
            ["pages.back"]        = "← Zurück zu den Seiten",
            ["pages.by"]          = "von",
            ["pages.delete"]      = "Löschen",
            ["pages.resetSeeded"] = "Auf Seed-Text zurücksetzen",
            ["pages.untitled"]    = "Unbenannte Seite",

            // ── blog (per-resident page feed, ADR 0040) ─────────────────────
            ["blog.new_page"]     = "Neue Blogseite",
            ["blog.empty_own"] =
                "Du hast noch keine Blogseiten. Erstelle die erste — sie wird die " +
                "Wurzel deines Blogs, und du kannst weitere darunter anordnen.",
            ["blog.empty_other"]  = "Diese:r Anwohner:in hat noch keine Blogseiten.",
            ["blog.draft"]        = "Entwurf",

            // ── groups (Create page) ─────────────────────────────────────────
            ["groups.create_back"] = "← Zurück zu den Gruppen",
            ["groups.create_title"] = "Gruppe erstellen",
            ["groups.create_lede"] =
                "Ein Name für eine Gemeinschaft von Anwohner:innen (z. B. „Gebäude 4“, " +
                "„Ehrenamtliche“, „Fahrradbesitzer“). " +
                "Du bist Inhaber:in der Gruppe — du kannst Mitglieder über die " +
                "Detailseite der Gruppe hinzufügen und entfernen.",
            ["groups.create_desc_hint"] = "Optional — eine kurze Notiz, die andere Anwohner:innen sehen.",
            ["groups.create_private_hint"] =
                "Eine private Gruppe (z. B. eine Familie) ist für alle anderen " +
                "unsichtbar — nur Personen, die du als Mitglieder hinzufügst, " +
                "können sie sehen und nutzen. " +
                "Eine öffentliche Gruppe (z. B. „Pilzesammler“) erscheint als " +
                "Option in den Auswahlmenüs anderer Anwohner:innen.",
            ["groups.create_submit"] = "Gruppe erstellen",

            // ── groups (Edit page) ───────────────────────────────────────────
            ["groups.edit_back"] = "zurück zum Beitrag",
            ["groups.edit_title"] = "Deinen Beitrag bearbeiten",
            ["groups.edit_lede"] =
                "Du bearbeitest deinen eigenen Beitrag. Nur du kannst ihn bearbeiten — die " +
                "Gruppenmitgliedschaft bestimmt, wer ihn sehen kann, aber nur die " +
                "Autor:in kann ihn ändern. Die Gruppe, in der dieser Beitrag erscheint, " +
                "ist festgelegt; nur Titel, Text und Sprache unten sind editierbar.",
            ["groups.edit_title_label"] = "Titel",
            ["groups.edit_title_hint"] =
                "Eine kurze Überschrift (≤ 120 Zeichen). Leer lassen für einen " +
                "reinen Text-Beitrag — die Liste zeigt stattdessen " +
                "deine erste Zeile des Textes.",
            ["groups.edit_body_label"] = "Text",
            ["groups.edit_language_label"] = "Sprache",
            ["groups.edit_language_hint"] =
                "Die Sprache, in der du diesen Beitrag schreibst. Das ist nur ein " +
                "Tag — er wird nicht übersetzt — und hält den Text " +
                "später auffindbar und lässt Leser:innen eine eigene " +
                "Sprachversion hinzufügen, wenn sie wollen.",
            ["groups.edit_submit"] = "Änderungen speichern",
            ["groups.edit_cancel"] = "Abbrechen",

            // ── directory (Detail page) ──────────────────────────────────────
            ["directory.detail_back"] = "← Zurück zum Verzeichnis",
            ["directory.detail_verified"] = "Verifiziert",
            ["directory.detail_contact_address"] = "Adresse",
            ["directory.detail_contact_email"] = "E-Mail",
            ["directory.detail_contact_phone"] = "Telefon",
            ["directory.detail_no_contact"] =
                "Diese:r Anwohner:in hat dir (noch) keine Kontaktmöglichkeit geteilt. Du " +
                "kannst aber immer noch die Profilseite sehen.",

            // ── community (Manage page) ──────────────────────────────────────
            ["community.manage_back"] = "← Zurück zum Feed",
            ["community.manage_lede"] = "Verwalte die Mitgliedschaft in dieser Gemeinschaft.",
            ["community.manage_moderate"] = "Du moderierst diese Gemeinschaft",
            ["community.manage_disabled"] = "Deaktiviert",
            ["community.manage_availability"] = "Verfügbarkeit",
            ["community.manage_mandatory_label"] =
                "Pflichtgemeinschaft — alle in der Nachbarschaft sind Mitglieder",
            ["community.manage_mandatory_hint"] =
                "In einer Pflichtgemeinschaft können keine Mitglieder entfernt und nicht " +
                "ausgetreten werden; das Häkchen entfernen, um die " +
                "Mitgliedschaft wieder optional zu machen.",
            ["community.manage_make_optional"] = "Optional machen",
            ["community.manage_make_mandatory"] = "Zur Pflicht machen",
            ["community.manage_members"] = "Mitglieder",
            ["community.manage_mandatory_note"] =
                "Diese Gemeinschaft ist eine Pflichtgemeinschaft — alle sind Mitglieder, also " +
                "gibt es keine:r zu entfernen. Die aufgeführten Zeilen sind " +
                "explizite Mitgliedschaften, die behalten werden, falls die " +
                "Gemeinschaft wieder optional wird.",
            ["community.manage_no_members"] = "Noch keine expliziten Mitglieder — füge unten welche hinzu.",
            ["community.manage_you"] = "Du",
            ["community.manage_remove"] = "Entfernen",
            ["community.manage_add_member"] = "Mitglied hinzufügen",
            ["community.manage_all_members"] =
                "Alle in der Nachbarschaft sind bereits hier Mitglieder.",
            ["community.manage_pick_resident"] = "Wähle eine:n Anwohner:in zum Hinzufügen…",

            // ── ADR 0026 — community name/description translations ────────
            ["community.translations_label"] = "Übersetzungen",
            ["community.translations_none"] = "Noch keine",
            ["community.translations_page_title"] = "Übersetzungen",
            ["community.translations_page_lede"] = "Name und Beschreibung dieser Gemeinschaft in weiteren Sprachen.",
            ["community.translations_view_only"] = "Sie können die Übersetzungen einsehen; nur ein GlobalAdmin oder ein Übersetzer kann sie hinzufügen, bearbeiten oder entfernen.",
            ["community.translation_add"] = "Hinzufügen",
            ["community.translation_name_label"] = "Name",
            ["community.translation_desc_label"] = "Beschreibung",
            ["community.translation_optional"] = "optional",
            ["community.translation_min_one"] = "Mindestens Name oder Beschreibung ist erforderlich.",
            ["community.translation_save"] = "Übersetzung speichern",

            // ── community feed buttons (Posts/Index.cshtml) ─────────────
            ["community.feed_manage_members"] = "Mitglieder verwalten",
            ["community.feed_translations"] = "Übersetzungen",

            // ── moderation (Index page) ──────────────────────────────────────
            ["moderation.title"] = "Moderation",
            ["moderation.empty"] = "Noch keine Meldungen. Die Warteschlange ist leer.",
            ["moderation.th_status"] = "Status",
            ["moderation.th_post"] = "Beitrag",
            ["moderation.th_component"] = "Komponente",
            ["moderation.th_reporter"] = "Melder",
            ["moderation.th_filed"] = "Eingereicht",
            ["moderation.th_action"] = "Aktion",
            ["moderation.review"] = "Prüfen →",

            // ── moderation (Resolve page) ────────────────────────────────────
            ["moderation.resolve_title"] = "Moderation — Meldung prüfen",
            ["moderation.details"] = "Meldungsdetails",
            ["moderation.th_post_label"] = "Beitrag",
            ["moderation.th_component_label"] = "Komponente",
            ["moderation.th_reporter_label"] = "Melder",
            ["moderation.th_author_label"] = "Autor:in des Beitrags",
            ["moderation.th_filed_label"] = "Eingereicht",
            ["moderation.th_reason_label"] = "Grund",
            ["moderation.no_reason"] = "(kein Grund angegeben)",
            ["moderation.th_body_label"] = "Beitragstext",
            ["moderation.body_preview"] = "Vorschau des Beitrags",
            ["moderation.assign_header"] = "An eine Moderatorin / einen Moderator zuweisen",
            ["moderation.assign_label"] =
                "Eine:r Moderator:in, die:er diese Gemeinschaft abdeckt",
            ["moderation.assign_pick"] = "Wähle eine:n Moderator:in …",
            ["moderation.assign_submit"] = "Zuweisen",
            ["moderation.cancel"] = "Abbrechen",
            ["moderation.unlock_submit"] = "Entsperren",
            ["moderation.resolve_header"] = "Auflösen (diese Meldung schließen)",
            ["moderation.resolve_submit"] = "Auflösen",
            ["moderation.back_to_queue"] = "← Zurück zur Warteschlange",

            // ── reply-report-target lane (ADR 0023) ─────────────────────────
            ["moderation.queue_reply_by"] = "Antwort von",
            ["moderation.resolve_reply_label"] = "Antwort (Ziel dieser Meldung)",
            ["moderation.resolve_reply_by"] = "Antwort von",

            // ── account (Verify / Resend / AccessDenied) ─────────────────────
            ["account.verify_title"] = "Bestätige dein Konto",
            ["account.verify_pending"] =
                "Wir bestätigen dein Konto — in einem Moment bist du angemeldet.",
            ["account.verify_again"] = "Erneut registrieren",
            ["account.resend_title"] = "Bestätigungs-E-Mail neu senden",
            ["account.resend_lede"] =
                "Gib die E-Mail ein, mit der du dich registriert hast, und wir senden dir einen frischen Bestätigungslink.",
            ["account.resend_submit"] = "Neu senden",
            ["account.resend_create"] = "Erstmal dein Konto anlegen?",
            ["account.resend_signup"] = "Registrieren",
            ["account.denied_title"] = "Zugriff verweigert",
            ["account.denied_lede"] = "Du hast keine Berechtigung, diese Seite zu sehen.",
            ["account.denied_home"] = "Zurück zur Startseite",

            // ── admin (Audit page) ───────────────────────────────────────────
            ["admin.audit_title"] = "Zugriffs-Audit",
            ["admin.audit_lede"] =
                "Ein Protokoll darüber, wer Zugang zu eingeschränkten Inhalten " +
                "erhalten oder verweigert wurde, plus Adminaktionen. Dieses Protokoll wird immer " +
                "aufbewahrt und nach der Aufbewahrungsrichtlinie der Instanz regelmäßig aufgeräumt.",
            ["admin.audit_filter"] = "Filter",
            ["admin.audit_th_at"] = "Zeitpunkt (UTC)",
            ["admin.audit_th_actor"] = "Aktor:in",
            ["admin.audit_th_effective"] = "Effektiv",
            ["admin.audit_th_action"] = "Aktion",
            ["admin.audit_th_target"] = "Ziel",
            ["admin.audit_th_aggregate"] = "Aggregat",
            ["admin.audit_th_via"] = "Über",
            ["admin.audit_th_outcome"] = "Ergebnis",

            // ── admin (Break-glass page) ─────────────────────────────────────
            ["admin.breakglass_title"] = "Break-glass",
            ["admin.breakglass_granted"] = "Gewährt am (UTC)",
            ["admin.breakglass_expires"] = "Läuft ab am (UTC)",
            ["admin.breakglass_status"] = "Status",
            ["admin.breakglass_consumed"] = "aktiviert — Erhöhung aktiv bis zum Ablauf",
            ["admin.breakglass_presented"] = "eingerichtet, aber noch nicht aktiviert",
            ["admin.breakglass_token_label"] = "Einmalcode (vom Betreiber)",
            ["admin.breakglass_token_hint"] =
                "Die Verwendung dieses Codes ist eine Einmalaktion. Er aktiviert die " +
                "Erhöhung bis zum Ablauf.",
            ["admin.breakglass_consume"] = "Aktivieren",

            // ── locale (settings + public picker) ────────────────────────────
            ["locale.settings_title"] = "Deine Einstellungen",
            ["locale.language_heading"] = "Sprache",
            ["locale.lede"] =
                "Wähle die Sprache, die dir die Plattform anzeigt. Deine Auswahl wird in einem " +
                "Browser-Cookie gespeichert — sie wird beim nächsten Request wirksam " +
                "und betrifft nie andere Anwohner:innen.",
            ["locale.preferred_label"] = "Bevorzugte Sprache",
            ["locale.default_note"] =
                "Die Instanz-Voreinstellung ist ",
            ["locale.default_note_tail"] =
                ". Wenn deine Wunschsprache später vom Admin entfernt wird, " +
                "fällt die Plattform still auf die Instanz-Voreinstellung zurück.",
            ["locale.save"] = "Speichern",
            ["locale.reset"] = "Auf die Instanz-Voreinstellung zurücksetzen",
            ["locale.public_title"] = "Wähle deine Sprache",
            ["locale.instance_default"] = "— Instanz-Voreinstellung",
            // ADR 0046 — the browser-match suggestion (pre-selection + marker).
            ["locale.browser_matched"] = "— aus deinen Browser-Einstellungen",
            ["locale.browser_note"] =
                "Wir haben ",
            ["locale.browser_note_tail"] =
                " anhand deiner Browser-Einstellungen ausgewählt. " +
                "Speichern macht es zu deiner Wunschsprache — sie bleibt " +
                "bestehen, bis du sie änderst.",
            // Flash-Meldungen (Toast-Oberfläche — LocaleController.Save /
            // PublicLocaleController.Save; {0} = Sprachcode).
            ["locale.flash_set"] =
                "Sprache auf \"{0}\" gesetzt — sie wirkt ab der nächsten Anfrage.",
            ["locale.flash_reset"] =
                "Spracheinstellung zurückgesetzt — die Instanz-Voreinstellung wird verwendet.",

            // ── announcements (shared labels + New/Edit compose) ─────────────
            ["announcements.scope_label"] = "Wer sieht dies?",
            ["announcements.scope_public"] =
                "Alle (öffentlich) — für Besucher und Anwohner:innen sichtbar",
            ["announcements.scope_resident"] =
                "Anwohner:innen — nur bei Anmeldung sichtbar",
            ["announcements.community_label"] = "An eine bestimmte Gemeinschaft senden (optional)",
            ["announcements.all_residents"] = "Alle Anwohner:innen",
            ["announcements.community_hint"] =
                "Lass „Alle Anwohner:innen“, um an alle zu senden, oder wähle eine Gemeinschaft, um einzuschränken, wer es sieht.",
            ["announcements.title_label"] = "Titel",
            ["announcements.title_hint"] = "Eine kurze Überschrift (bis zu 120 Zeichen).",
            ["announcements.body_label"] = "Text",
            ["announcements.pin_label"] = "Oben auf allen Seiten anpinnen",
            ["announcements.cancel"] = "Abbrechen",
            ["announcements.new_title"] = "Neue Ankündigung",
            ["announcements.new_lede"] =
                "Ankündigungen sind Hinweise, die für sich allein erscheinen, " +
                "getrennt vom Gemeinschafts-Feed. Eine öffentliche " +
                "Ankündigung ist für alle sichtbar, auch für Menschen, die " +
                "nicht angemeldet sind (z. B. ein Wartungsfenster). Eine " +
                "Anwohner-Ankündigung ist nur für angemeldete " +
                "Anwohner:innen sichtbar (z. B. ein „Hilf uns bei X“-Aufruf).",
            ["announcements.new_scope_hint"] =
                "Öffentliche Ankündigungen sind für alle sichtbar, auch " +
                "für Besucher, die nicht angemeldet sind (z. B. ein Wartungsfenster " +
                "oder ein Störungshinweis). Anwohner-Ankündigungen " +
                "sind nur für angemeldete Anwohner:innen sichtbar.",
            ["announcements.new_submit"] = "Ankündigung erstellen",
            ["announcements.edit_title"] = "Ankündigung bearbeiten",
            ["announcements.edit_lede"] =
                "Aktualisiere Titel, Text und Sichtbarkeit dieser " +
                "Ankündigung. Eine öffentliche Ankündigung ist für alle " +
                "sichtbar, auch für Menschen, die nicht angemeldet sind " +
                "(z. B. ein Wartungsfenster). Eine Anwohner-Ankündigung ist " +
                "nur für angemeldete Anwohner:innen sichtbar (z. B. ein „Hilf uns bei X“-Aufruf).",
            ["announcements.edit_scope_hint"] =
                "Eine Änderung, wer dies sieht, wirkt sofort für den nächsten Leser.",
            ["announcements.edit_submit"] = "Änderungen speichern",
            ["announcements.pin_hint"] =
                "Eine gepinnte Ankündigung erscheint zusätzlich als Banner " +
                "ganz oben auf jeder Seite (einschließlich Startseite), " +
                "neben der üblichen Ankündigungsliste. Ihre Sichtbarkeit " +
                "folgt weiter dem Publikum, das du oben gewählt hast: ein öffentlicher " +
                "Pin zeigt allen Besuchern; ein Anwohner-Pin nur, wenn ein Nutzer " +
                "angemeldet ist. Wenn mehrere gepinnt sind, " +
                "gewinnt der zuletzt gepinnte.",
            ["announcements.language_note"] =
                "Die Sprache, in der du diese Ankündigung schreibst. Das ist nur ein " +
                "Tag — er wird nicht übersetzt — und hält den Text später " +
                "auffindbar und lässt Leser:innen eine eigene Sprachversion " +
                "hinzufügen, wenn sie wollen.",

            // ── announcements (Index + Detail + pinned banner) ───────────────
            ["announcements.index_title"] = "Ankündigungen",
            ["announcements.index_lede"] =
                "Plattform-Hinweise: öffentliche sind für alle sichtbar (z. B. " +
                "geplante Wartung); nur für Anwohner:innen sind sie für alle " +
                "angemeldeten Nutzer sichtbar (z. B. „Hilf uns bei X“-Aufrufe).",
            ["announcements.all"] = "Alle Ankündigungen",
            ["announcements.new_button"] = "Neue Ankündigung",
            ["announcements.empty"] = "Noch keine Ankündigungen.",
            ["announcements.read_more"] = "Mehr lesen…",
            ["announcements.scope_everyone"] = "alle",
            ["announcements.scope_residents"] = "Anwohner:innen",
            ["announcements.pinned_badge"] = "gepinnt",
            ["announcements.edit_button"] = "Bearbeiten",
            ["announcements.delete"] = "Löschen",
            ["announcements.detail_back"] = "← Zurück zu den Ankündigungen",
            ["announcements.detail_untitled"] = "Unbenannte Ankündigung",
            ["announcements.by"] = "von",
            ["announcements.edited"] = "bearbeitet",
            ["announcements.banner_read_more"] = "Mehr lesen",
            ["announcements.banner_all"] = "Alle Ankündigungen",

            // ── static pages (Page — the terms/help shell) ───────────────────
            ["static.last_updated"] = "Zuletzt aktualisiert:",

            // ── shared (the _GrantPickers partial — static markup only) ─────
            ["grant.heading"] = "Wem zugewiesen",
            ["grant.hint"] =
                "Eine oder mehrere auswählen — oder nutze die „Select all“-Zeile " +
                "über jeder Liste als Kurzbefehl.",
            ["grant.empty_users"] =
                "Du bist der/die einzige:r verifizierte Anwohner:in, also gibt es noch " +
                "niemanden, dem/der hier zugewiesen werden kann.",
            ["grant.empty_groups"] =
                "Auf der Plattform existieren noch keine Gruppen — erstelle eine " +
                "unter „Gruppen“, um gruppenbezogene Sichtbarkeit hinzuzufügen.",

            // ── admin (page heading + primary action) ───────────────────────
            ["admin.title"]  = "Verwaltung",
            ["admin.verify"] = "Verifizieren",

            // ── rich editor (the RE toolbar button labels, ADR 0031) ────────
            ["rc.editor.bold"]    = "B",
            ["rc.editor.italic"]  = "I",
            ["rc.editor.code"]    = "C",
            ["rc.editor.h1"]      = "H1",
            ["rc.editor.h2"]      = "H2",
            ["rc.editor.h3"]      = "H3",
            ["rc.editor.list"]    = "•",
            ["rc.editor.olist"]   = "1.",
            ["rc.editor.link"]    = "Link",
            ["rc.editor.image"]   = "Bild",
            ["rc.editor.attach"]  = "Datei anhängen",
            ["rc.editor.source"]      = "</>",
            ["rc.editor.showPreview"] = "Vorschau",

            // ── about (the About product surface, ADR 0042 D5) ──────────────
            ["about.eyebrow"]             = "Privat per Vorgabe",
            ["about.lead"] =
                "Ein Ort für alles, was eure Nachbarschaft macht — " +
                "der Feed, die Gruppen und die Notizen, die besser verdienen " +
                "als eine Gruppen-Chats. Privat, in einfacher Sprache und euer.",
            ["about.cta_feed"]            = "Zum Feed",
            ["about.cta_notes"]           = "Gepinnte Notizen lesen",
            ["about.features.one.title"]  = "Ein Feed für die Straße",
            ["about.features.one.body"] =
                "Beiträge und Threads aus euren Blöcken und Gassen, an einem " +
                "ruhigen Ort — kein Algorithmus, kein Lärm.",
            ["about.features.groups.title"]  = "Gruppen, die passen",
            ["about.features.groups.body"] =
                "Garten-Tausch, Buchclub, Streifenwache — eine Gruppe für alles, " +
                "was die Nachbarschaft schon tut.",
            ["about.features.pinned.title"]  = "Gepinnt, wo es zählt",
            ["about.features.pinned.body"] =
                "Wasserschnitt, Straßenarbeiten, die neuen Poller — " +
                "Notizen, die stehen bleiben statt wegzuscrollen.",
            ["about.project.eyebrow"]  = "Open Source",
            ["about.project.heading"]  = "Der Code, die Entscheidungen, die Design-Doku",
            ["about.project.lead"] =
                "Wenn du neugierig bist, wie es funktioniert — oder wenn du es " +
                "gleich für deine Nachbarschaft hosten willst — ist alles öffentlich.",

            // ── tags (the TG lane, ADR 0044 — browse + composer affordances) ──
            ["tags.list.heading"]       = "Tags",
            ["tags.list.lede"] =
                "Themen, mit denen eure Beiträge und Blogseiten verschlagwortet sind — " +
                "klicke auf eines, um zu sehen, was dazu geschrieben wurde.",
            ["tags.list.empty"] =
                "Noch keine Tags — sie erscheinen hier, sobald eine:r Anwohner:in " +
                "einem Beitrag oder einer Blogseite ein Tag zuweist.",
            ["tags.bytag.heading"]      = "Beiträge und Seiten über",
            ["tags.bytag.posts_heading"] = "Beiträge",
            ["tags.bytag.pages_heading"] = "Blogseiten",
            ["tags.bytag.empty"] =
                "Keine lesbaren Beiträge oder Blogseiten tragen dieses Tag.",
            ["tag.input.placeholder"] = "z. B. sanitätsdienst, haushalt, maplestreet",
            ["tag.input.hint"] =
                "Tippe, um bestehende Tags zu finden, oder starte ein neues — es wird " +
                "diesem Beitrag zugewiesen.",
            ["tag.suggest.empty"] =
                "Keine passenden Tags — weiter tippen oder ein neues starten.",
            ["tag.translate.heading"] = "Übersetzungen",
            ["tag.translate.save"] = "Speichern",
            ["tag.translate.disabled"] = "Nur der Ersteller oder ein GlobalAdmin kann den Text ändern.",

            // ── platform (scope + the FIG philosophy, home/about) ───────────
            ["platform.scope_home"] =
                "Kumunita entstand als Zuhause für eine Nachbarschaft — passt aber genauso gut zu einem Verein, einem Team oder den Menschen hinter einem großen Event. Die Nachbarschaft ist der Standard, nicht die Grenze.",
            ["platform.fig_home"] =
                "Darin steckt die Idee der Fractal Integration Guidelines (FIG): Der wahre Wert einer Gruppe lebt in den Verbindungen zwischen ihren Menschen und Bausteinen, nicht in den Bausteinen selbst. Kumunita ist die Software, die diese Verbindungen herstellt und hält.",
            ["platform.scope_heading"] = "Für eine Nachbarschaft gebaut — überall daheim",
            ["platform.scope_body1"] =
                "Kumunita wurde ursprünglich für eine Straße gebaut: ein privates, gemeinsames Zuhause für die Menschen, die dort leben. Aber die Kernidee ist gar nicht an eine Straße gebunden. Es ist ein privates Zuhause für eine Gruppe von Menschen, die zusammen koordinieren, teilen und Vertrauen aufbauen wollen — eine Nachbarschaft, ein Verein, ein Sportteam, ein Arbeitsplatz, eine Interessengemeinschaft oder auch nur die Menschen hinter einem einzelnen Event. Was sich ändert, sind nur der Name und die Details.",
            ["platform.scope_body2"] =
                "Alles, was es zu einer Straße passt — der ruhige Feed, die Gruppen, die an Zielgruppen gerichteten Beiträge, die Moderation mit ihrer Prüfhistorie, die mehrsprachige Oberfläche — funktioniert bei all diesen Gruppen genau so. Also ist die Anpassung eine Frage der Konfiguration: der Gemeinschaftsname, die Komponenten, die für 'Sicherheit' und 'Soziales' stehen, die Gruppen, die zu eurer Welt passen. Kein Neuentwurf.",
            ["platform.fig_heading"] = "Die Idee dahinter: die Fractal Integration Guidelines",
            ["platform.fig_body1"] =
                "Kumunita folgt einem kleinen Satz offener Richtlinien, den wir Fractal Integration Guidelines (FIG) nennen. Ihr zentraler Gedanke: Ein integriertes System ist mehr als die Summe seiner Teile, und seine Qualität ist die Qualität der Verbindungen zwischen diesen Teilen — eine Eigenschaft, die kein einzelnes Teil für sich allein hat. Ein Haufen Funktionen, der nie miteinander verknüpft ist, ist nur Rauschen; in den Verbindungen lebt der Wert.",
            ["platform.fig_body2"] =
                "Eine Nachbarschaft ist genau so ein System: viele verschiedene Menschen, Haushalte und Anliegen, deren Verknüpfung — wer wen kennt, wem man vertrauen kann, wie ein Problem über Menschen hinweg tatsächlich gelöst wird — erst eine Gemeinschaft erzeugt. Keine einzelne Person ist eine Nachbarschaft. Kumunita ist die Software, die diese Verknüpfung herstellt und hält.",
            ["platform.fig_body3"] =
                "Also entwickeln wir Kumunita nach derselben Regel, die wir von ihr verlangen: Die Qualität ist die Qualität der Verknüpfung, nicht die Anzahl der Funktionen. Die Richtlinien wiederholen sich in jeder Skalenebene — ein Modul, eine Funktion, eine Gemeinschaft, die Menschen, die sie bauen — deshalb folgt ihnen das ganze Projekt, und deshalb ist die Philosophie öffentlich dokumentiert.",
            ["platform.scope_eyebrow"] = "Für jede Gruppe",
            ["platform.fig_eyebrow"] = "Die Philosophie",

            // ── events (M4 — ADR 0054: die /Events-Fläche; Index, Detail, Composer) ──
            ["events.title"] = "Veranstaltungen",
            ["events.lede"] =
                "Bevorstehende Veranstaltungen in der Nachbarschaft — schau, was los ist, und gib deine Teilnahme an.",
            ["events.new_event"] = "Neue Veranstaltung",
            ["events.new_lead"] =
                "Teile eine bevorstehende Veranstaltung mit der Nachbarschaft. Standardmäßig kann sie jeder sehen; " +
                "schalte dies im Abschnitt „Zielgruppe“ nur aus, wenn du einschränken möchtest, wer sie sehen kann.",
            ["events.edit_event"] = "Veranstaltung bearbeiten",
            ["events.edit_lead"] =
                "Aktualisiere die Details dieser Veranstaltung. Deine Wahl der Zielgruppe ist die einzige Zugangsgrenze — " +
                "die Gemeindeauswahl ist nur ein Filter.",
            ["events.title_hint"] = "Eine kurze Schlagzeile für die Veranstaltung.",
            ["events.start"] = "Beginn",
            ["events.end"] = "Ende",
            ["events.time_hint"] =
                "Die Zeit, in der die Veranstaltung stattfindet. Besucher sehen sie in ihrer eigenen Zeitzone.",
            ["events.location"] = "Ort",
            ["events.capacity"] = "Kapazität",
            ["events.color"] = "Farbe",
            ["events.location_hint"] =
                "Ort, Kapazität und Farbe sind nur Anzeigedetails — sie beschränken nicht, wer eine Teilnahme angeben kann.",
            ["events.all_communities"] = "Alle Gemeinden",
            ["events.community_hint"] =
                "Die Gemeinde, unter der diese Veranstaltung erscheint — ein Filter, keine Zugangsgrenze.",
            ["events.tags_placeholder"] = "z. B. Reinigung, Treffen, Garten",
            ["events.audience_heading"] = "Zielgruppe — wer diese Veranstaltung sehen kann",
            ["events.audience_default"] =
                "Standard — jeder kann diese Veranstaltung sehen. Schalte es nur aus, wenn du einschränken möchtest, wer sie sehen kann.",
            ["events.audience_mode_any"] = "ein Zuschauer trifft zu, wenn er auf einer der Auswahlen steht",
            ["events.audience_mode_all"] =
                "ein Zuschauer muss auf jeder Auswahl stehen — eine leere Liste verweigert allen",
            ["events.reminder"] = "Sende die 24-Stunden-Erinnerung an alle, die \"Going\" angegeben haben",
            ["events.reminder_hint"] =
                "Eine Erinnerung wird einen Tag vor der Veranstaltung an alle Gesandten gesendet. Schalte sie aus, um sie zu überspringen.",
            ["events.create"] = "Veranstaltung erstellen",
            ["events.save_changes"] = "Änderungen speichern",
            ["events.empty"] = "Noch keine bevorstehenden Veranstaltungen.",
            ["events.back"] = "← Zurück zu den Veranstaltungen",
            ["events.draft"] = "Entwurf",
            ["events.draft_title"] = "Nur du kannst dies sehen — es ist noch nicht öffentlich",
            ["events.publish"] = "Veröffentlichen",
            ["events.edit"] = "Bearbeiten",
            ["events.delete"] = "Löschen",
            ["events.delete_confirm"] = "Diese Veranstaltung löschen? Das kann nicht rückgängig gemacht werden.",
            ["events.untitled"] = "Namenlose Veranstaltung",
            ["events.rsvp"] = "Teilnahme",
            ["events.rsvp_you"] = "Du gibst an:",
            ["events.rsvp_responses"] = "Antworten",
            ["events.rsvp_update"] = "Aktualisieren",
            ["events.remove_translation_confirm"] = "Diese Übersetzung entfernen?",

            // ── events.mine (der EV-MINE-Bereich „Deine Veranstaltungen“ auf /events — ADR 0065) ──
            ["events.mine.title"] = "Deine kommenden Veranstaltungen",
            ["events.mine.hint"] = "Veranstaltungen, die du bestätigt hast oder organisiert.",

            // ── events.calendar (die EV-CAL-Monatsansicht — /events/calendar, ADR 0063) ──
            ["events.calendar.title"] = "Kalender",
            ["events.calendar.prev"] = "Zurück",
            ["events.calendar.next"] = "Weiter",
            ["events.calendar.today"] = "Heute",
            ["events.calendar.overlap_hint"] = "Überlappt mit einem anderen Termin in diesem Zeitraum",
            ["events.calendar.empty"] = "Keine Termine in diesem Zeitraum.",
            ["events.calendar.from"] = "Von",
            // EV-DWM (ADR 0064, U05) — die Tag/Woche/Monat-Umschalter-Labels (C-DWM·9).
            ["events.calendar.view.day"] = "Tag",
            ["events.calendar.view.week"] = "Woche",
            ["events.calendar.view.month"] = "Monat",

            // ── grant (der gemeinsame „Wem zugewiesen"-Picker — C#-gebaut „Alle auswählen" + Zähler) ──
            ["grant.select_all"] = "Alle auswählen",
            ["grant.label_residences"] = "Anwohner",
            ["grant.label_groups"] = "Gruppen",
            ["grant.count_selected"] = "{0} von {1} ausgewählt",

            // ── profile (die _AudienceEditor-Warnung; die inline-<b>-Wörter sind getrennt) ──
            ["profile.audience_empty_warning_lead"] = "Achtung:",
            ["profile.audience_empty_warning_body"] =
                "du hast unten niemanden und keine Gruppe ausgewählt, daher sind deine Kontaktdaten derzeit für",
            ["profile.audience_empty_warning_everyone"] = "alle",
            ["profile.audience_empty_warning_tail"] =
                "verborgen. Füge eine Person oder Gruppe hinzu, wenn du sie teilen möchtest.",

            // ── settings (der Abschnitt „E-Mail- und Benachrichtigungssprache" unter /settings/language) ──
            ["settings.email_title"] = "E-Mail- und Benachrichtigungssprache",
            ["settings.email_lede"] =
                "Wähle die Sprache, in der die Plattform dir schreibt — Kontomails und Veranstaltungserinnerungen. " +
                "Deine Wahl wird auf deinem Konto gespeichert.",
            ["settings.email_label"] = "E-Mail- und Benachrichtigungssprache",
            ["settings.email_note"] =
                "Wählst du eine Sprache, werden deine Mails und Erinnerungen in ihr gesendet. " +
                "Setzt du sie zurück, wird die Instanzstandardsprache verwendet.",
            ["settings.email_save"] = "Speichern",
            ["settings.email_reset"] = "Auf Instanzstandard zurücksetzen",
            ["settings.email_flash_set"] = "E-Mail- und Benachrichtigungssprache gesetzt — deine nächste E-Mail wird sie verwenden.",
            ["settings.email_flash_reset"] = "E-Mail- und Benachrichtigungssprache zurückgesetzt — die Instanz-Voreinstellung wird verwendet.",
            ["settings.email_reset_confirm"] =
                "E-Mail- und Benachrichtigungssprache auf den Instanzstandard zurücksetzen?",

            // ── email (ausgehende Mails — {0}/{1} sind die Laufzeit-Platzhalter) ──
            ["email.verify_subject"] = "Verifiziere dein Kumunita-Konto",
            ["email.verify_body"] =
                "Hallo {0},\n\nDein Kumunita-Konto wird bei der ersten Anmeldung verifiziert. " +
                "Öffne diesen einmaligen Link, um das Konto zu bestätigen (dabei wirst du auch angemeldet):\n\n{1}\n\n" +
                "Falls du dieses Konto nicht erstellt hast, kannst du diese Nachricht ignorieren.",
            ["email.reminder_subject"] = "Erinnerung: {0}",
            ["email.reminder_body"] = "**{0}** steht bevor: {1}{2}.",

            // ── notifications (M6 — ADR 0076: the /notifications surface;
            // translations of the en floor above — same key set, same
            // dotted shape, no email templates for the reserved
            // post.mention kind) ──────────────────────────────────────────
            ["notifications.inbox"] = "Benachrichtigungen",
            ["notifications.preferences"] = "Einstellungen",
            ["notifications.mark_all_read"] = "Alle als gelesen markieren",
            ["notifications.empty"] = "Noch nichts — Dinge, die dir passieren, erscheinen hier.",
            ["notifications.bell"] = "Benachrichtigungen",
            ["notifications.view"] = "Ansehen",
            ["notifications.preferences.title"] = "Benachrichtigungseinstellungen",
            ["notifications.preferences.intro"] = "Wähle, welche Benachrichtigungen du zusätzlich per E-Mail bekommst. Der Posteingang erfasst jede Benachrichtigung.",
            ["notifications.preferences.save"] = "Einstellungen speichern",
            ["notifications.preferences.coming_soon"] = "bald verfügbar",
            ["notifications.kind.post.reply"] = "Antwort",
            ["notifications.kind.post.mention"] = "Erwähnung",
            ["notifications.kind.group.post"] = "Gruppenbeitrag",
            ["notifications.kind.group.added"] = "Gruppe hinzugefügt",
            ["notifications.kind.group.invite"] = "Gruppeneinladung",
            ["notifications.kind.event.rsvp"] = "RSVP",
            ["notifications.kind.event.reminder"] = "Erinnerung",
            ["notifications.kind.report.filed"] = "Meldung",
            ["notifications.kind.report.assigned"] = "Meldung zugewiesen",
            ["notifications.kind.report.resolved"] = "Meldung aufgelöst",
            ["notifications.kind.todo.assign"] = "Aufgabe zugewiesen",
            ["notifications.preference.post.reply.label"] = "Antworten auf meine Beiträge",
            ["notifications.preference.post.mention.label"] = "Erwähnungen von mir",
            ["notifications.preference.group.post.label"] = "Neue Beiträge in meinen Gruppen",
            ["notifications.preference.group.added.label"] = "Wenn ich zu einer Gruppe hinzugefügt werde",
            ["notifications.preference.group.invite.label"] = "Wenn ich zu einer Gruppe eingeladen werde",
            ["notifications.preference.event.rsvp.label"] = "RSVPs auf meinen Veranstaltungen",
            ["notifications.preference.event.reminder.label"] = "Veranstaltungserinnerungen",
            ["notifications.preference.report.filed.label"] = "Meldungen über meine Inhalte",
            ["notifications.preference.report.assigned.label"] = "Mir zugewiesene Meldungen",
            ["notifications.preference.report.resolved.label"] = "Auflösung von Meldungen, an denen ich beteiligt bin",
            ["notifications.preference.todo.assign.label"] = "Mir zugewiesene Aufgaben",
            ["notification.post.reply.subject"] = "Es gibt eine Antwort auf deinen Beitrag",
            ["notification.group.post.subject"] = "Neuer Beitrag in deiner Gruppe",
            ["notification.group.added.subject"] = "Du wurdest zu einer Gruppe hinzugefügt",
            ["notification.group.invite.subject"] = "Du wurdest zu einer Gruppe eingeladen",
            ["notification.event.rsvp.subject"] = "Ein RSVP auf deiner Veranstaltung",
            ["notification.event.reminder.subject"] = "Veranstaltungserinnerung",
            ["notification.report.filed.subject"] = "Eine Meldung zu deinem Beitrag",
            ["notification.report.assigned.subject"] = "Eine Meldung wurde dir zugewiesen",
            ["notification.report.resolved.subject"] = "Eine Meldung wurde aufgelöst",
            ["notification.todo.assign.subject"] = "Eine Aufgabe wurde dir zugewiesen",
            ["notification.post.reply.body"] = "Jemand hat auf einen deiner Beiträge geantwortet: ",
            ["notification.group.post.body"] = "Neuer Beitrag in einer deiner Gruppen: ",
            ["notification.group.added.body"] = "Du wurdest zur Gruppe ",
            ["notification.group.invite.body"] = "Du wurdest zur Gruppe ",
            ["notification.event.rsvp.body"] = "Jemand hat auf einer deiner Veranstaltungen RSVP'd. ",
            ["notification.event.reminder.body"] = "Deine anstehende Veranstaltung: ",
            ["notification.report.filed.body"] = "Ein Bewohner hat eine Meldung über einen deiner Beiträge eingereicht. ",
            ["notification.report.assigned.body"] = "Eine Meldung wurde dir als Moderator zugewiesen. ",
            ["notification.report.resolved.body"] = "Eine Meldung, an der du beteiligt warst, wurde aufgelöst. ",
            ["notification.todo.assign.body"] = "Eine Aufgabe wurde dir zugewiesen: ",
            ["notifications.kind.account.signup"] = "Neues Mitglied",
            ["notifications.kind.account.verified"] = "Konto verifiziert",
            ["notifications.preference.account.signup.label"] = "Wenn ein neues Mitglied sich registriert",
            ["notifications.preference.account.verified.label"] = "Wenn ein Mitglied sein Konto verifiziert",
            ["notification.account.signup.subject"] = "Ein neues Mitglied hat sich registriert",
            ["notification.account.signup.body"] = "Ein neues Mitglied hat sich registriert: ",
            ["notification.account.verified.subject"] = "Ein Mitglied hat sein Konto verifiziert",
            ["notification.account.verified.body"] = "Ein Mitglied hat sein Konto verifiziert: ",

            // ── ADR 0084 ──
            ["notification.announcement.subject"] = "Eine neue Ankündigung",
            ["notification.announcement.body"] = "Eine neue Ankündigung wurde veröffentlicht: ",
            ["notification.community.post.subject"] = "Neuer Beitrag in deiner Community",
            ["notification.community.post.body"] = "Neuer Beitrag in einer deiner Communities: ",
            ["notification.page.child.subject"] = "Eine neue Seite wurde hinzugefügt",
            ["notification.page.child.body"] = "Eine neue Seite wurde unter einer Seite hinzugefügt, die du beobachtest: ",
            ["notifications.kind.announcement"] = "Neue Ankündigung",
            ["notifications.kind.community.post"] = "Neuer Community-Beitrag",
            ["notifications.kind.page.child"] = "Neue Unterseite",
            ["notifications.preference.announcement.label"] = "Neue Ankündigungen",
            ["notifications.preference.community.post.label"] = "Neue Beiträge in meinen Communities",
            ["notifications.preference.page.child.label"] = "Neue Unterseiten auf Seiten, die ich beobachte",
            ["notifications.subscriptions.title"] = "Benachrichtigungsabonnements",
            ["notifications.subscriptions.intro"] = "Wähle aus, welche Communities, Gruppen und Seiten dich benachrichtigen. Einstellungen entscheiden, welche Arten du auch per E-Mail erhältst; diese Schalter entscheiden, welche Ziele dich überhaupt benachrichtigen.",
            ["notifications.subscription.announcement.label"] = "Neue Ankündigungen",
            ["notifications.subscription.community.post.label"] = "Neue Beiträge in Communities",
            ["notifications.subscription.group.post.label"] = "Neue Beiträge in Gruppen",
            ["notifications.subscription.page.child.label"] = "Neue Unterseiten",
            ["pages.subscribe"] = "Aktualisierungen abonnieren",
            ["pages.unsubscribe"] = "Abonnierung aufheben",

            // ── PL (ADR 0086, U05) — die /projects-Landing + der Projects-Tab ──
            ["pl.tabs.projects"] = "Projekte",
            ["pl.index.title"] = "Projekte",
            ["pl.index.lede"] = "Die Ziele und Projekte der Nachbarschaft — die übergreifende Arbeit über den Aufgaben und Boards hinaus.",
            ["pl.index.goals_heading"] = "Ziele",
            ["pl.index.projects_heading"] = "Projekte",
            ["pl.index.new_goal"] = "Neues Ziel",
            ["pl.index.new_project"] = "Neues Projekt",
            ["pl.index.view_projects"] = "Projekte ansehen →",
            ["pl.index.goals_empty"] = "Noch keine Ziele — erstelle eines, um die gemeinsame Arbeit eine Richtung zu geben.",
            ["pl.index.projects_empty"] = "Noch keine unabhängigen Projekte — erstelle eines, um die gemeinsame Arbeit zu managen.",
            ["pl.index.start"] = "Start",
            ["pl.index.due"] = "Fällig",

            // ── PL (ADR 0086, U06) — Ziel-Detail + Composer + Bearbeitung ──
            ["pl.goal.new_heading"] = "Neues Ziel",
            ["pl.goal.new_lede"] = "Ein Ziel gibt der gemeinsamen Arbeit eine Richtung — mit optionaler Beschreibung, Community-Filter und Zielgruppe. Projekte können später daran hängen.",
            ["pl.goal.create"] = "Ziel anlegen",
            ["pl.goal.edit_heading"] = "Ziel bearbeiten",
            ["pl.goal.edit_lead"] = "Titel und Beschreibung dieses Ziels aktualisieren. Zielgruppe, Community und Sprache sind bei der Anlage festgelegt.",
            ["pl.goal.save"] = "Änderungen speichern",
            ["pl.goal.edit"] = "Ziel bearbeiten",
            ["pl.goal.title_hint"] = "Ein kurzer Name für das Ziel — die Feed-Bezeichnung.",
            ["pl.goal.description_hint"] = "Eine optionale Beschreibung der Richtung, die dieses Ziel der gemeinsamen Arbeit gibt. Ein Ziel funktioniert auch ohne Beschreibung.",
            ["pl.goal.audience_heading"] = "Zielgruppe — wer dieses Ziel sehen kann",
            ["pl.goal.audience_public"] = "Für alle auf der Instanz sichtbar.",
            ["pl.goal.audience_restricted"] = "Eingeschränkt auf die unten genannten Berechtigungen.",
            ["pl.goal.projects_heading"] = "Projekte in diesem Ziel",
            ["pl.goal.projects_empty"] = "Noch keine Projekte unter diesem Ziel — lege eines an, um die gemeinsame Arbeit zu managen.",
            ["pl.goal.empty_description"] = "Dieses Ziel hat noch keine Beschreibung.",
            ["pl.project.new_heading"] = "Neues Projekt",
            ["pl.project.new_lede"] = "Ein Projekt ist ein Stück gemeinsamer Arbeit — mit optionaler Beschreibung, Status, Start- und Fälligkeitsdatum, Community-Filter und Zielgruppe. Es kann an ein Ziel hängen.",
            ["pl.project.create"] = "Projekt anlegen",
            ["pl.project.edit_heading"] = "Projekt bearbeiten",
            ["pl.project.edit_lead"] = "Titel, Beschreibung, Ziel, Status und Daten dieses Projekts aktualisieren. Zielgruppe, Community und Sprache sind bei der Anlage festgelegt.",
            ["pl.project.save"] = "Änderungen speichern",
            ["pl.project.edit"] = "Projekt bearbeiten",
            ["pl.project.title_hint"] = "Ein kurzer Name für das Projekt — die Feed-Bezeichnung.",
            ["pl.project.description_hint"] = "Eine optionale Beschreibung, worum es in diesem Projekt geht. Ein Projekt funktioniert auch ohne Beschreibung.",
            ["pl.project.status"] = "Status",
            ["pl.project.status_hint"] = "Ein optionales Zustandslabel — dasselbe feste Vokabular wie bei To-dos. Leer lassen für keinen.",
            ["pl.project.start_date"] = "Start",
            ["pl.project.due_date"] = "Fällig",
            ["pl.project.dates_hint"] = "Beide optional — leer lassen für kein Datum. Für Betrachter in ihrer eigenen Zeitzone dargestellt.",
            ["pl.project.audience_heading"] = "Zielgruppe — wer dieses Projekt sehen kann",
            ["pl.project.audience_public"] = "Für alle auf der Instanz sichtbar.",
            ["pl.project.audience_restricted"] = "Eingeschränkt auf die unten genannten Berechtigungen.",
            ["pl.project.goal_heading"] = "Ziel",
            ["pl.project.goal_hint"] = "Ein optionales Ziel, unter dem dieses Projekt organisiert wird. Leer lassen für ein eigenständiges Projekt.",
            ["pl.project.goal_link"] = "Ziel",
            ["pl.project.associated_heading"] = "To-dos & Boards in diesem Projekt",
            ["pl.project.todos_heading"] = "To-dos in diesem Projekt",
            ["pl.project.boards_heading"] = "Boards in diesem Projekt",
            ["pl.project.todos_empty"] = "Noch keine To-dos in diesem Projekt.",
            ["pl.project.boards_empty"] = "Noch keine Boards in diesem Projekt.",
            ["pl.project.empty_description"] = "Dieses Projekt hat noch keine Beschreibung.",
            ["pl.todo.project_link"] = "Projekt",
            ["pl.todo.set_project"] = "Projekt festlegen",
            ["pl.board.project_link"] = "Projekt",
            ["pl.board.set_project"] = "Projekt festlegen",
            ["pl.board.add_to_project"] = "Zu Projekt hinzufügen…",
            ["pl.board.project_hint"] = "Ein Projektlink ist eine Anzeigefläche — er ordnet dieses Board dem Projekt zu, schränkt aber nie ein, wer es sehen kann.",
            ["pl.goal.delete"] = "Ziel löschen",
            ["pl.goal.delete_confirm"] = "Dieses Ziel löschen? Seine Projekte bleiben an ihrem Ort — der Link zu diesem Ziel wird einfach nicht mehr angezeigt.",
            ["pl.project.delete"] = "Projekt löschen",
            ["pl.project.delete_confirm"] = "Dieses Projekt löschen? Seine To-dos und Boards bleiben an ihrem Ort — der Link zu diesem Projekt wird einfach nicht mehr angezeigt.",
        };

    /// <summary>
    /// The curated French (<c>fr</c>) baseline (LS U03, ADR 0042 D2/D5). One
    /// entry per key in <see cref="AllKeys"/> — full registry parity, the ADR
    /// 0015 honesty invariant extended to this dictionary. Idioms per ADR 0042
    /// D2: the familiar <c>tu</c> register held everywhere, sentence case,
    /// no trailing period on button labels, accents and typography per French
    /// convention (plain space before <c>:</c> <c>;</c> <c>?</c> <c>!</c>),
    /// and every inlined data token (<c>yyyy-MM-dd HH:mm</c>,
    /// <c>§6.4</c>, <c>Allow</c>/<c>Deny</c>, the <c>rc.editor.*</c> glyph
    /// labels, the on-screen <c>Select all</c> label that the kw-l TagHelper
    /// cannot reach) preserved token-for-token with the <c>en</c> value.
    /// These are <b>initial values</b> — seeded once on a pristine DB (LS U04),
    /// then community-owned via the in-app editor (ADR 0021); an admin edit is
    /// never overwritten (ADR 0042 D1).
    /// </summary>
    public static IReadOnlyDictionary<string, string> FrValues { get; } =
        new Dictionary<string, string>
        {
            // ── nav (the shared top-nav, _Layout + _AccountNav) ─────────────
            ["nav.home"]          = "Accueil",

            // ── common (shared action/field labels reused across resident-facing views) ──
            ["common.cancel"]   = "Annuler",
            ["common.save"]     = "Enregistrer",
            ["common.title"]    = "Titre",
            ["common.body"]     = "Texte",
            ["common.language"] = "Langue",
            ["common.optional"] = "facultatif",
            ["common.add"]      = "Ajouter",
            ["common.remove"]   = "Retirer",
            ["common.filter"]   = "Filtrer",
            ["common.name"]         = "Nom",
            ["common.display_name"] = "Nom affiché",
            ["common.email"]        = "E-mail",
            ["common.password"]     = "Mot de passe",
            ["common.filter_name"]  = "Filtrer par nom…",
            ["account.confirm_password"] = "Confirmer le mot de passe",
            ["groups.name_label"]     = "Nom du groupe",
            ["groups.desc_placeholder"] = "De quoi s'agit-il ? (visible par tous ceux qui peuvent accéder à cette page)",
            ["posts.report_reason_placeholder"] = "Ajoutez éventuellement une raison pour un modérateur…",
            ["tags.events_example"] = "ex. nettoyage, convivial, jardin",
            ["tags.pages_example"]  = "ex. salubrité, budget, maple-street",
            ["admin.community_description"] = "Description (optionnelle)",
            ["admin.community_mandatory"]   = "Obligatoire",
            ["admin.add_community"]         = "Ajouter une communauté",
            ["admin.roles_heading"]         = "Rôles",
            ["admin.roles_independent_hint"] = "Indépendants — un résident peut cumuler librement les rôles (ADR 0030). Aucune case cochée = un simple Membre.",
            ["admin.moderator_scope"]       = "Portée du modérateur",
            ["admin.moderator_scope_hint"]  = "Les communautés que ce compte peut modérer. N'a de sens que si le rôle Modérateur est coché — la voie Core efface les lignes de portée quand le rôle Modérateur est désactivé.",
            ["nav.announcements"] = "Annonces",
            ["nav.community"]     = "Communauté",
            ["nav.groups"]        = "Groupes",
            ["nav.pages"]         = "Pages",
            ["nav.tags"]          = "Étiquettes",
            ["nav.directory"]     = "Annuaire",
            ["nav.sign_in"]       = "Se connecter",
            ["nav.sign_up"]       = "S'inscrire",
            ["nav.profile"]       = "Profil",
            ["nav.admin"]         = "Administration",
            ["nav.translations"]  = "Traductions",
            ["nav.sign_out"]      = "Se déconnecter",
            ["nav.children"]      = "Enfants",
            ["nav.my_drafts"]     = "Mes brouillons",
            ["nav.account"]       = "Compte",
            ["nav.events"]        = "Événements",

            // ── events (M4 — ADR 0054: the events nav entry + the Detail footer) ──
            ["events.created"]    = "Créé le",
            ["events.edited"]     = "modifié le",

            // ── projects (M5 — ADR 0067: the to-do surface nav entry + labels) ──
            ["nav.projects"]                 = "Projets",
            ["projects.todo.title"]          = "Tâches",
            ["projects.todo.lede"]           = "Les tâches partagées du quartier — assignez du travail à un voisin, décomposez-le en sous-tâches et placez-le sur un tableau.",
            ["projects.todo.new"]            = "Nouvelle tâche",
            ["projects.todo.new_lead"]       = "Rédigez une tâche, assignez-la éventuellement à un voisin, et — si besoin — décomposez-la en sous-tâches ou placez-la sur un tableau. Par défaut, elle est visible par tous ; désactivez cela dans la section public pour restreindre l'accès.",
            ["projects.todo.title_hint"]     = "Un libellé court pour la tâche — l'étiquette de la carte.",
            ["projects.todo.status"]         = "Statut",
            ["projects.todo.status_assignee_hint"] = "Le statut est l'un des statuts fixes des tâches (Aucun, Non commencé, En cours, Fait, Annulé). Assigner une tâche donne à ce résident un droit d'intervention sur elle — affichage + intervention, jamais une limite d'accès.",
            ["projects.todo.assignee"]       = "Assignée à",
            ["projects.todo.unassigned"]     = "Non assignée",
            ["projects.todo.assign"]         = "Assigner",
            ["projects.todo.unassign"]       = "Retirer l'assignation",
            ["projects.todo.assign_to"]      = "Assigner à…",
            ["projects.todo.assign_people"]  = "Personnes",
            ["projects.todo.assign_groups"]  = "Groupes",
            ["projects.todo.assign_communities"] = "Communautés",
            ["projects.todo.claim"]          = "Prendre en charge",
            ["projects.todo.addressed_to"]   = "Adressée à",
            ["projects.todo.filter_unassigned"] = "Non assignées uniquement",
            ["projects.todo.add_subtask"]    = "Ajouter une sous-tâche",
            ["projects.todo.delete"]         = "Supprimer",
            ["projects.todo.parent"]         = "Tâche parente",
            ["projects.todo.top_level"]      = "Tâche de niveau supérieur",
            ["projects.todo.parent_hint"]    = "Choisissez une tâche parente pour créer cette tâche comme sous-tâche — une sous-tâche est une tâche complète avec son propre statut, son assignation et sa position sur un tableau.",
            ["projects.todo.no_parent"]      = "Pas de tâche parente (niveau supérieur)",
            ["projects.todo.clear_parent"]   = "Retirer la tâche parente (rendre de niveau supérieur)",
            ["projects.todo.reparent_hint"]  = "Une sous-tâche est une tâche complète avec son propre statut, son assignation et sa position sur un tableau. Replacer sous un descendant est refusé (garde anti-cycle).",
            // ADR 0087 — la dépendance « en attente de » (puce, sélecteur, bascule du flux).
            ["todo.blocked_by"]              = "En attente de",
            ["todo.blocked_by_none"]         = "Aucun bloquant",
            ["todo.blocked_generic"]         = "Une autre tâche (non visible pour vous)",
            ["todo.blocked_filter"]          = "En attente de quelque chose",
            // ADR 0079 — dates de début/échéance optionnelles sur une tâche.
            ["projects.todo.start"]          = "Début",
            ["projects.todo.due"]            = "Échéance",
            ["projects.todo.dates_hint"]     = "Les deux sont optionnels — laissez vide pour aucune date. Affiché aux visiteurs dans leur propre fuseau horaire.",
            ["projects.todo.created"]        = "Créé le",
            ["projects.todo.modified"]       = "Modifié le",
            ["projects.todo.no_body"]        = "Pas de texte — cette tâche n'a qu'un titre.",
            ["projects.todo.subtasks"]       = "Sous-tâches",
            ["projects.todo.boards"]         = "Tableaux",
            ["projects.todo.back"]           = "← Retour aux tâches",
            ["projects.todo.untitled"]       = "Tâche sans titre",
            ["projects.todo.edit"]           = "Modifier",
            ["projects.todo.edit_heading"]   = "Modifier la tâche",
            ["projects.todo.edit_lead"]      = "Mettez à jour les détails de cette tâche. Votre choix de public est la seule limite d'accès — le choix de la communauté n'est qu'un filtre.",
            ["projects.todo.all_communities"] = "Toutes les communautés",
            ["projects.todo.audience_heading"] = "Public — qui peut voir cette tâche",
            ["projects.todo.audience_default"] = "Par défaut — tout le monde peut voir cette tâche. Désactivez cela uniquement si vous voulez restreindre l'accès.",
            ["projects.todo.save"]           = "Enregistrer les modifications",
            ["projects.todo.create"]         = "Créer la tâche",
            ["projects.todo.empty"]          = "Aucune tâche pour l'instant — créez-en une pour lancer le travail partagé.",

            // ── projects (M5 — ADR 0067 : la surface tableaux (U10) + actions de carte) ──
            ["projects.todo.copy_to"]        = "Copier sur un tableau",
            ["projects.todo.move_to"]        = "Déplacer sur un tableau",
            ["projects.todo.move_up"]        = "Monter",
            ["projects.todo.move_down"]      = "Descendre",
            ["projects.todo.move_left"]      = "Aller à gauche",
            ["projects.todo.move_right"]     = "Aller à droite",
            ["projects.board.title"]         = "Tableaux",
            ["projects.board.lede"]          = "Les tableaux partagés du quartier — organisez les tâches dans des colonnes, faites-les avancer dans les statuts et gardez le travail visible.",
            ["projects.board.new"]           = "Nouveau tableau",
            ["projects.board.new_lead"]      = "Créez un tableau pour organiser les tâches dans des colonnes. Une colonne peut porter un statut (une tâche déplacée dedans en hérite) et une limite optionnelle du nombre de cartes. Par défaut, le tableau est visible par tous ; désactivez cela dans la section public pour restreindre l'accès.",
            ["projects.board.title_hint"]    = "Un libellé court pour le tableau — l'étiquette du flux.",
            ["projects.board.edit"]          = "Modifier le tableau",
            ["projects.board.edit_heading"]  = "Modifier le tableau",
            ["projects.board.edit_lead"]     = "Mettez à jour le titre et la description de ce tableau. Son public, sa communauté et sa langue sont fixés à la création.",
            ["projects.board.save"]          = "Enregistrer les modifications",
            ["projects.board.description_hint"] = "Une description optionnelle de ce que ce tableau suit. Un tableau est utilisable avec un titre seul.",
            ["projects.board.back"]          = "← Retour aux tableaux",
            ["projects.board.untitled"]      = "Tableau sans titre",
            ["projects.board.delete"]        = "Supprimer le tableau",
            ["projects.board.create"]        = "Créer le tableau",
            ["projects.board.empty"]         = "Aucun tableau pour l'instant — créez-en un pour commencer à organiser le travail partagé.",
            ["projects.board.no_lanes"]      = "Ce tableau n'a pas encore de colonnes.",
            ["projects.board.lanes"]         = "Colonnes",
            ["projects.board.lanes_hint"]    = "Des colonnes à travers le tableau — par exemple « Planifié / En cours / Fait ». Le statut d'une colonne, s'il est défini, est appliqué à une tâche déplacée dedans ; sa limite de cartes borne le nombre de cartes.",
            ["projects.board.single_lane_hint"] = "Le tableau démarre avec une colonne — ajoutez d'autres colonnes et statuts sur la page du tableau.",
            ["projects.board.lane.title"]    = "Titre de la colonne",
            ["projects.board.lane.status"]   = "Statut",
            ["projects.board.lane.max_items"] = "Max. d'articles",
            ["projects.board.lane.order"]    = "Ordre",
            ["projects.board.lane.empty"]    = "Aucune carte dans cette colonne.",
            ["projects.board.lane.save"]     = "Enregistrer",
            ["projects.board.lane.rename"]   = "Renommer",
            ["projects.board.lane.set_limit"] = "Définir la limite",
            ["projects.board.lane.set_status"] = "Définir le statut",
            ["projects.board.lane.move_left"] = "Déplacer à gauche",
            ["projects.board.lane.move_right"] = "Déplacer à droite",
            ["projects.board.lane.add_todo"] = "Ajouter une tâche",
            ["projects.board.lane.add_lane"] = "Ajouter une colonne",
            ["projects.board.lane.add_todo_placeholder"] = "Titre de la tâche",
            ["projects.board.lane.add_lane_placeholder"] = "Titre de la nouvelle colonne",
            ["projects.board.lane.first_title_placeholder"] = "Prévu",
            ["projects.board.status.none"] = "Aucun",
            ["projects.board.status.not_started"] = "Non commencé",
            ["projects.board.status.in_progress"] = "En cours",
            ["projects.board.status.done"] = "Fait",
            ["projects.board.status.cancelled"] = "Annulé",
            ["projects.board.fullscreen"]    = "Plein écran",
            ["projects.board.exit_fullscreen"] = "Quitter le plein écran",
            ["projects.board.audience_heading"] = "Public — qui peut voir ce tableau",
            ["projects.board.audience_default"] = "Par défaut — tout le monde peut voir ce tableau. Désactivez cela uniquement si vous voulez restreindre l'accès.",

            // ── guardian (the /me/children child-accounts surface) ─────────
            ["guardian.title"]        = "Tes enfants",
            ["guardian.lead"]         = "Les comptes que tu as créés pour un enfant, et les contrôles que tu exerces sur chacun.",
            ["guardian.empty"]        = "Pas encore d'enfants.",
            ["guardian.add"]          = "Ajouter un compte enfant",
            ["guardian.manage_title"] = "Gérer un compte enfant",

            // ── guardian (per-child surface, GU ADR 0028) ─────────────────
            ["guardian.back"]               = "Retour à vos enfants",
            ["guardian.account_label"]       = "Compte",
            ["guardian.group_memberships"]   = "Adhésions aux groupes",
            ["guardian.community_memberships"] = "Adhésions aux communautés",
            ["guardian.no_groups"]           = "Pas d'adhesions aux groupes.",
            ["guardian.no_communities"]      = "Pas d'adhesions aux communautés.",
            ["guardian.group_id_label"]      = "Identifiant du groupe",
            ["guardian.community_id_label"]  = "Identifiant de la communauté",
            ["guardian.pending_invitations"] = "Invitations de groupe en attente",
            ["guardian.no_invitations"]      = "Pas d'invitations en attente.",
            ["guardian.approve"]             = "Approuver",
            ["guardian.handover"]            = "Transférer le compte",
            ["guardian.handover_hint"]       =
                "Dissoudre la tutelle remet le compte à l'enfant. Ses " +
                "adhesions sont conservées, et ses propres réglages reviennent " +
                "à la prochaine lecture.",
            ["guardian.dissolve"]            = "Dissoudre la tutelle",
            ["guardian.suspended"]           = "Suspendé",
            ["guardian.unsuspend"]           = "Réactiver",
            ["guardian.suspend"]             = "Suspendre",
            ["guardian.display_name"]        = "Nom affiché",
            ["guardian.email"]               = "Adresse e-mail",
            ["guardian.password"]            = "Mot de passe",
            ["guardian.child_email_hint"]    =
                "L'enfant vérifie son propre e-mail pour se connecter — le flux d'inscription habituel.",

            // ── posts (composer helper hints) ──────────────────────────────
            ["posts.title_hint"] =
                "Un court titre (jusqu'à 120 caractères). Laisse vide pour un " +
                "post sans titre — la liste affichera la première ligne du texte.",
            ["posts.language_hint"] =
                "La langue dans laquelle tu écris ce post. C'est seulement une " +
                "étiquette — elle n'est pas traduite — et elle garde le texte " +
                "trouvable plus tard et permet à un lecteur d'ajouter sa " +
                "propre version.",

            // ── faq (drop-in FAQ section, _FaqAccordion) ──────────────────
            ["faq.title"]     = "Questions fréquentes",
            ["faq.q1"]        = "Qui peut voir mes publications ?",
            ["faq.a1"]        =
                "Celui que tu choisis quand tu publies : toi seul, ton " +
                "groupe, ta communauté ou tout le monde. " +
                "L'audience est un choix par post — ce n'est pas un réglage " +
                "global.",
            ["faq.q2"]        = "Où se trouvent les annonces comme les coupures d'eau et les travaux ?",
            ["faq.a2_intro"]  = "Les annonces épinglées, sur",
            ["faq.a2_link"]   = "/announcements",
            ["faq.a2_outro"]  =
                " — la lecture est ouverte pour que personne n'ait à se " +
                "connecter pour savoir quand la rue sera repeinte.",
            ["faq.q3"]        = "Est-ce privé par défaut ?",
            ["faq.a3"]        =
                "Oui. Chaque fois que l'accès à du contenu restreint est accordé " +
                "ou refusé, c'est enregistré. Les données restent sur une " +
                "unique base de données appartenant à l'hôte du quartier, et " +
                "il n'y a ni publicité ni suivi intégré.",

            // ── error (shared error page, Shared/Error.cshtml) ────────────
            ["error.title"]           = "Erreur.",
            ["error.subtitle"]        = "Une erreur s'est produite lors du traitement de votre demande.",
            ["error.development_title"] = "Mode développement",
            ["error.development_hint"] =
                "Passer en environnement Développement affiche plus de " +
                "détails sur l'erreur survenue. Cet environnement ne devrait " +
                "pas être activé pour des applications déployées — il peut " +
                "révéler des informations sensibles aux utilisateurs finaux.",
            ["error.request_id"]      = "Identifiant de la demande :",

            // ── guardian assignment (GA ADR 0038) ───────────────────────────
            ["guardian.otherGuardians.title"] = "Autres tuteurs",
            ["guardian.otherGuardians.empty"] = "Aucun autre tuteur assigné.",
            ["guardian.assign.title"]        = "Assigner un tuteur",
            ["guardian.assign.email"]        = "E-mail du tuteur à assigner",
            ["guardian.assign.submit"]       = "Assigner",
            ["guardian.assign.noAccount"]    = "Aucun compte avec cet e-mail.",
            ["guardian.assign.self"]         = "Tu es déjà tuteur de cet enfant.",
            ["guardian.assign.success"]      = "Tuteur assigné.",

            // ── footer (the shared footer, _Layout) ─────────────────────────
            ["footer.tagline"]  =
                "Un chez-soi privé pour un quartier — le fil, les groupes et les notes épinglées. " +
                "Ce qui se passe dans ta rue reste dans ta rue.",
            ["footer.copyright"] = "· auto-hébergé par ta communauté",
            ["footer.gtk_heading"] = "Bon à savoir",
            ["footer.gtk_privacy"] =
                "Privé par défaut : le public de chaque publication est choisi par son auteur, " +
                "et tout est lisible par toi, pas par le monde.",
            ["footer.gtk_oss"] =
                "Kumunita est open source — le code, les décisions, la documentation.",
            // SP U03 (ADR 0043 D4) — la colonne "Plateforme" du pied de page :
            // les cinq surfaces livrées (la vue à propos + les quatre
            // documents Page), liées pour chaque visiteur·se.
            ["footer.platform.heading"] = "Plateforme",
            ["footer.platform.about"]   = "À propos",
            ["footer.platform.terms"]   = "Conditions d'utilisation",
            ["footer.platform.help"]    = "Aide",
            ["footer.platform.privacy"] = "Confidentialité",
            ["footer.platform.conduct"] = "Règles de conduite",
            // La colonne « Le projet » (home / about / footer) : le titre + les
            // trois libellés RepositoryInfo.Links (émis via une clé kw-l dynamique
            // depuis la liste de liens, résolue selon la langue).
            ["footer.project.heading"] = "Le projet",
            ["repo.source_code"]      = "Code source",
            ["repo.documentation"]    = "Documentation",
            ["repo.non_technical"]    = "Pour les habitants non techniques",

            // ── settings (the language-picker labels) ───────────────────────
            ["settings.settings"]       = "Paramètres",
            ["settings.choose_language"] = "Choisis ta langue",

            // ── settings — account help ─────────────────────────────────────
            ["settings.help_heading"]     = "Aide pour ton compte",
            ["settings.help_lede"]        =
                "Bloqué sur ton compte — un mot de passe, ton accès, ou autre chose ? " +
                "Ce guide t'accompagne.",

            // ── settings — timezone (ADR 0019) ──────────────────────────────
            ["settings.timezone_title"]        = "Fuseau horaire",
            ["settings.timezone_lede"]         =
                "Choisis le fuseau horaire que la plateforme t'affiche. Ton choix est enregistré sur ton compte — " +
                "il prend effet au prochain chargement de page, et n'affecte jamais les autres habitants.",
            ["settings.timezone_label"]        = "Ton fuseau horaire",
            ["settings.timezone_default_marker"] = "— défaut de la plateforme",
            ["settings.timezone_default_note"] = "Le défaut de la plateforme est ",
            ["settings.timezone_default_tail"] =
                ". Si tu réinitialises ta préférence, le défaut de la plateforme est utilisé.",
            ["settings.timezone_reset"]        = "Réinitialiser au défaut de la plateforme",
            ["settings.timezone_save"]         = "Enregistrer",
            ["settings.timezone_flash_set"]    = "Fuseau horaire réglé sur \"{0}\" — il prend effet à la prochaine requête.",
            ["settings.timezone_flash_reset"]  = "Fuseau horaire réinitialisé — le défaut de la plateforme sera utilisé.",
            ["settings.timezone_unknown"]      = "Fuseau horaire inconnu",

            // ── settings — date format (ADR 0020) ───────────────────────────
            ["settings.dateformat_title"]        = "Format de date et d'heure",
            ["settings.dateformat_lede"]         =
                "Choisis le format de date et d'heure que la plateforme t'affiche. Ton choix est enregistré sur ton compte — " +
                "il prend effet au prochain chargement de page, et n'affecte jamais les autres habitants.",
            ["settings.dateformat_label"]        = "Ton format de date et d'heure",
            ["settings.dateformat_default_marker"] = "— défaut de la plateforme",
            ["settings.dateformat_default_note"] = "Le défaut de la plateforme est ",
            ["settings.dateformat_default_tail"] =
                ". Si tu réinitialises ta préférence, le défaut de la plateforme est utilisé.",
            ["settings.dateformat_custom_label"] = "Format personnalisé",
            ["settings.dateformat_custom_hint"]  =
                "Une chaîne de format de date/heure personnalisée .NET (p. ex. yyyy-MM-dd HH:mm). Laisser vide pour utiliser un préréglage.",
            ["settings.dateformat_reset"]        = "Réinitialiser au défaut de la plateforme",
            ["settings.dateformat_save"]         = "Enregistrer",
            ["settings.dateformat_flash_set"]    = "Format de date et d'heure réglé — il prend effet à la prochaine requête.",
            ["settings.dateformat_flash_reset"]  = "Format de date et d'heure réinitialisé — le défaut de la plateforme sera utilisé.",

            // ── admin — the platform-default timezone ───────────────────────
            ["admin.timezone_title"]    = "Fuseau horaire par défaut de la plateforme",
            ["admin.timezone_lede"]     =
                "Le fuseau horaire par défaut pour les horodatages des habitants " +
                "qui n'ont pas défini de préférence personnelle.",
            ["admin.timezone_label"]    = "Fuseau horaire par défaut",
            ["admin.timezone_save"]     = "Enregistrer",

            // ── admin — the platform-default date format (ADR 0020) ─────────
            ["admin.dateformat_title"]    = "Format de date et d'heure par défaut de la plateforme",
            ["admin.dateformat_lede"]     =
                "Le format de date et d'heure par défaut pour les horodatages des habitants " +
                "qui n'ont pas défini de préférence personnelle.",
            ["admin.dateformat_label"]    = "Format de date et d'heure par défaut",
            ["admin.dateformat_custom_label"] = "Format personnalisé",
            ["admin.dateformat_custom_hint"]  =
                "Une chaîne de format de date/heure personnalisée .NET (p. ex. yyyy-MM-dd HH:mm). Laisser vide pour utiliser un préréglage.",
            ["admin.dateformat_save"]     = "Enregistrer",

            // ── admin — the sign-up gate (ADR 0050) ─────────────────────────
            ["admin.signup_title"]    = "Inscription",
            ["admin.signup_lede"]     =
                "S'il est permis aux nouveaux habitants de créer un compte eux-mêmes. " +
                "Fermer le portail rend l'inscription réservée aux invitations — les habitants existants ne sont pas affectés.",
            ["admin.signup_open"]     = "Ouvert — les habitants peuvent s'inscrire",
            ["admin.signup_invitation_only"] = "Sur invitation — les inscriptions auto-service sont verrouillées",
            ["admin.signup_save"]     = "Enregistrer",
            ["admin.signup_notify_title"]   = "Notifier les admins",
            ["admin.signup_notify_lede"]    = "Quand un nouveau résident s'inscrit et quand un résident vérifie son compte, les GlobalAdmins reçoivent une notification de boîte d'arrivée et un courriel au mieux de l'effort. Désactivé, cela se tait — le compte lui-même n'est pas affecté.",
            ["admin.signup_notify_on"]      = "Activé — les admins sont notifiés des nouvelles inscriptions et vérifications",
            ["admin.signup_notify_off"]     = "Désactivé — aucune notification admin sur inscription / vérification",

            // ── account — sign-up-closed notice (ADR 0050) ─────────────────
            ["account.signup_closed_title"] = "L'inscription est fermée",
            ["account.signup_closed_body"]  =
                "L'inscription est actuellement réservée aux invitations sur cette instance. " +
                "Si vous avez été invité·e, une personne administratrice créera votre compte et vous enverra le lien de connexion.",

            // ── home (the hero + section lead) ──────────────────────────────
            ["home.eyebrow"] = "Où en est ce projet",
            ["home.lead"] =
                "Un chez-soi privé pour un quartier — construit pas à pas, en toute transparence. " +
                "Tout ce qui figure plus bas fait partie de ce plan, et tu es le bienvenu à voir comment il est réalisé.",
            ["home.support"] = "Des questions ou des retours ? Écris à",

            // ── home intro (ce qu'est Kumunita + les trois surfaces) ────────
            ["home.intro_eyebrow"]  = "Un chez-soi privé pour un quartier",
            ["home.intro_lead"] =
                "Un seul endroit tranquille pour tout ce que fait votre rue — le flux, les groupes " +
                "et les annonces qui méritent mieux qu'un groupe de chat. Privé, clair et à vous.",
            ["home.about_link"]    = "Ce que c'est & comment ça marche",
            ["home.feature_feed_title"]  = "Un seul flux pour la rue",
            ["home.feature_feed_body"] =
                "Posts et fils de discussion de vos ruelles, en un seul endroit tranquille — pas d'algorithme, pas de bruit.",
            ["home.feature_groups_title"]  = "Des groupes qui collent",
            ["home.feature_groups_body"] =
                "Échange de plants, club de lecture, veille de rue — un groupe pour tout ce que le quartier fait déjà.",
            ["home.feature_pinned_title"]  = "Épinglé là où ça compte",
            ["home.feature_pinned_body"] =
                "Coupures d'eau, travaux, nouveaux ralentisseurs — des annonces qui restent au lieu de défiler.",

            // ── home « nouveautés » (visiteurs connectés) ──────────────────
            ["home.feed_title"]          = "Les nouveautés du quartier",
            ["home.feed_posts"]          = "Posts",
            ["home.feed_announcements"]  = "Annonces",
            ["home.feed_pages"]          = "Pages",
            ["home.feed_view_all"]       = "Tout voir",
            ["home.feed_empty"] =
                "Rien de posté pour l'instant — soyez le premier à lancer la conversation dans le flux.",
            ["home.feed_badge_pinned"]   = "Épinglé",

            // ── home feuille de route (le plan, après l'intro) ─────────────
            ["home.roadmap_heading"] = "Construit en toute transparence, étape par étape",

            // ── account (Login / Signup — titles + primary actions) ─────────
            ["account.login_title"]   = "Se connecter",
            ["account.login_submit"]  = "Se connecter",
            ["account.login_no_account"] = "Pas encore de compte ?",
            ["account.login_remember"] = "Se souvenir de moi",
            ["account.login_setup_hint"] = "Tu as reçu un jeton de configuration du premier démarrage ?",
            ["account.login_setup_link"] = "Finaliser la configuration",
            ["account.login_signup"] = "S'inscrire",
            ["account.signup_title"]  = "S'inscrire",
            ["account.signup_submit"] = "S'inscrire",
            ["account.signup_has_account"] = "Tu as déjà un compte ?",

            // ── posts (Index / New / Edit) ──────────────────────────────────
            ["posts.feed_all_sections"] = "Communauté",
            ["posts.write"]        = "Écrire une publication",
            ["posts.new_title"]    = "Écrire une publication",
            ["posts.new_intro"] =
                "Par défaut, tout le monde dans la communauté que tu choisis ci-dessous peut voir " +
                "ta publication. Désactive-le dans la section audience uniquement si tu " +
                "veux restreindre qui peut la voir à des personnes ou groupes spécifiques.",
            ["posts.community_hint"] =
                "La communauté décide dans quel fil ta publication apparaît — et, " +
                "par défaut, qui peut la voir (tous les membres de cette communauté). " +
                "Pour restreindre l'audience, désactive « Tout le monde dans cette communauté » " +
                "dans la section audience ci-dessous.",
            ["posts.new_submit"]   = "Publier",
            ["posts.edit_title"]   = "Modifier la publication",
            ["posts.edit_save"]    = "Enregistrer les modifications",
            ["posts.save_as_draft"] =
                "Enregistrer en brouillon",
            ["posts.save_as_draft_hint"] =
                "Un brouillon est enregistré mais visible par personne — même les admins — " +
                "jusqu'à ce que tu le publies. Tu le trouveras sous « Mes brouillons ».",
            ["posts.draft_badge"]   = "Brouillon",
            ["posts.draft_note"] =
                "Cette publication est un brouillon — seul tu peux la voir. Publie-la " +
                "pour la rendre visible selon son audience.",
            ["posts.publish"]       = "Publier",
            ["my_drafts.title"]     = "Mes brouillons",
            ["my_drafts.empty"]     = "Tu n'as pas de brouillons.",
            ["posts.audience_all_members"] =
                "Tout le monde dans cette communauté",
            ["posts.audience_all_members_hint"] =
                "Le défaut — tout membre de la communauté ci-dessus peut " +
                "voir cette publication. Désactive-le uniquement si tu veux restreindre qui peut " +
                "la voir.",
            ["posts.audience_all_members_hint_edit"] =
                "Quand actif, tout membre de cette communauté peut voir la publication. " +
                "Désactive-le pour restreindre l'audience à des personnes ou " +
                "groupes spécifiques.",
            ["posts.audience_combine"] =
                "Combinaison des sélections",
            ["posts.audience_restrict_hint"] =
                "Ces sélections sont additionnelles — « Tout le monde dans cette communauté » " +
                "reste actif sauf si tu le désactives, donc la publication est visible par " +
                "toute la communauté et les sélections que tu fais ici.",
            ["posts.audience_only_picks"] =
                "Ce que tu sélectionnes ici devient l'audience de la publication — rien " +
                "au-dessus ou en dessous de ce formulaire n'y est ajouté. Une sélection vide (avec " +
                "« Tout le monde dans cette communauté » désactivé) signifie que seul tu peux " +
                "voir la publication.",
            ["posts.empty_can_post"] =
                "Pas encore de publications ici. Écris la première — elle sera visible uniquement par l'audience que " +
                "tu choisis dans le composeur sous le titre.",
            ["posts.empty"]        = "Pas encore de publications ici.",

            // ── groups (Index / Detail / New / PostDetail) ──────────────────
            ["groups.title"]          = "Groupes",
            ["groups.lead"]           = "Les communautés que tu gères ou auxquelles tu appartiens.",
            ["groups.create"]         = "Créer un groupe",
            ["groups.empty"]          = "Pas encore de groupes.",
            ["groups.back_all"]       = "← Tous les groupes",
            ["groups.tab.feed"]       = "Fil",
            ["groups.tab.members"]    = "Membres",
            ["groups.tab.settings"]   = "Paramètres",
            ["groups.posts_heading"]  = "Publications",
            ["groups.new_post"]       = "Nouvelle publication",
            ["groups.posts_empty_can"] =
                "Pas encore de publications. Écris la première — elle sera visible par les membres actuels.",
            ["groups.posts_empty"]    = "Pas encore de publications ici.",
            ["groups.members_heading"]  = "Membres",
            ["groups.members_empty"]    = "Pas encore de membres.",
            ["groups.member_leave"]     = "Quitter",
            ["groups.invite_heading"]   = "Inviter un habitant",
            ["groups.about_heading"]        = "À propos de ce groupe",
            ["groups.about_desc_label"]     = "Description (optionnelle)",
            ["groups.about_desc_clear_hint"] = "Laisser vide pour effacer la description.",
            ["groups.about_desc_save"]      = "Enregistrer la description",
            ["groups.privacy_heading"]      = "Confidentialité",
            ["groups.private_label"]        = "Groupe privé",
            ["groups.private_hint"]         =
                "Un groupe privé (p. ex. une famille) est masqué à tous les autres ; seules les personnes que tu ajoutes comme membres peuvent le voir et l'utiliser. Décocher la case pour rendre le groupe public à nouveau.",
            ["groups.new_title"]      = "Publier dans ce groupe",
            ["groups.new_back"]       = "retour au groupe",
            ["groups.new_submit"]     = "Publier dans le groupe",
            // ── groups list (the Airy layout — the invitation panel + the
            //    member-count word on each group card) ───────────────────────
            ["groups.invitations"]    = "Invitations",
            ["groups.invitations_pending"] = "en attente",
            ["groups.invited_by"]     = "Invité par",
            ["groups.invite_accept"]  = "Accepter",
            ["groups.invite_decline"] = "Refuser",
            ["groups.members_one"]    = "membre",
            ["groups.members_many"]   = "membres",
            // ── community feed (the Airy layout — the left rail + the
            //    mandatory-community note) ───────────────────────────────────
            ["community.browse"]          = "Communautés",
            ["community.all"]             = "Toutes",
            ["community.mandatory_badge"] =
                "Toutes — obligatoire",
            ["community.mandatory_note"]  =
                "C'est la communauté obligatoire du quartier — tout le monde " +
                "en fait partie ; personne ne peut en être retiré ou la " +
                "quitter.",

            // ── ADR 0026 — group name/description translations ───────────
            ["groups.translations_label"] = "Traductions",
            ["groups.translations_none"] = "Pas encore",
            ["groups.translation_add"] = "Ajouter",
            ["groups.translation_name_label"] = "Nom",
            ["groups.translation_desc_label"] = "Description",
            ["groups.translation_optional"] = "optionnel",
            ["groups.translation_min_one"] = "Au moins un nom ou une description est requis.",
            ["groups.translation_save"] = "Enregistrer la traduction",

            // ── directory (page heading + lead) ─────────────────────────────
            ["directory.title"] = "Annuaire",
            ["directory.lead"]  = "Tout le monde dans le quartier — chaque habitant sur la plateforme.",
            ["directory.empty"] = "Pas encore d'habitants dans ce quartier.",

            // ── profile (page heading + primary action) ─────────────────────
            ["profile.title"]      = "Ton profil",
            ["profile.save_avatar"] = "Enregistrer l'avatar",

            // ── profile (Edit page) ─────────────────────────────────────────
            ["profile.edit_lede"] =
                "C'est ici que tu décides ce que les autres habitants peuvent voir sur toi. " +
                "Ce que tu choisis ici est exactement ce que l'annuaire des voisins " +
                "affiche — aucune surprise.",
            ["profile.avatar_heading"] = "Ton avatar",
            ["profile.avatar_hint"] =
                "JPEG, PNG, WebP ou GIF · jusqu'à 5 Mo. Enregistrer remplace " +
                "l'avatar actuellement affiché dans l'annuaire.",
            ["profile.name_email_heading"] = "Ton nom + e-mail",
            ["profile.address_heading"] = "Ton adresse + téléphone (optionnel)",
            ["profile.address_hint"] =
                "Affiché dans la liste et le détail de l'annuaire uniquement si tu as " +
                "aussi activé le bloc contact ci-dessous ; laisser vide pour garder ta " +
                "rue privée pour ce profil.",
            ["profile.phone_hint"] =
                "Affiché dans le détail de l'annuaire uniquement si tu as " +
                "activé le bloc contact ci-dessous ; laisser vide pour garder ton numéro " +
                "privé pour ce profil.",
            ["profile.who_heading"] = "Qui peut voir quoi",
            ["profile.optin_contact"] = "Partager mes coordonnées (adresse, e-mail, téléphone)",
            ["profile.optin_contact_note"] =
                "Laisse désactivé si tu préfères garder ton adresse, " +
                "ton e-mail et ton téléphone complètement masqués de " +
                "l'annuaire. Quand c'est actif, tu choisis " +
                "qui peut les voir dans le bloc ci-dessous.",
            ["profile.save"] = "Enregistrer",
            ["profile.preview_link"] = "Aperçu — comment je me présente",

            // ── profile (Edit page — field labels + input placeholders) ──────
            ["profile.display_name_label"] = "Nom affiché",
            ["profile.address_label"] = "Adresse (la rue où tu vis)",
            ["profile.address_placeholder"] =
                "Rue — affichée aux voisins si tu actives le partage ci-dessous",
            ["profile.phone_label"] = "Numéro de téléphone",
            ["profile.phone_placeholder"] =
                "Téléphone — affiché aux voisins si tu actives le partage ci-dessous",
            ["profile.audience_mode_any_word"] = "N'importe lequel",
            ["profile.audience_mode_all_word"] = "Tous",

            // ── profile (Preview page) ───────────────────────────────────────
            ["profile.preview_back"] = "← Retour à l'éditeur",
            ["profile.preview_title"] = "Aperçu — comment je me présente",
            ["profile.preview_avatar_note"] =
                "Ton avatar, tel qu'il apparaît à côté de ton nom dans " +
                "l'annuaire des voisins.",
            ["profile.preview_readonly_lead"] =
                "C'est un aperçu en lecture seule. Il montre comment ton profil " +
                "apparaît à ",
            ["profile.preview_readonly_tail"] =
                " dans l'annuaire des voisins. Il ne change rien dans ton " +
                "profil enregistré — pour apporter un changement, ",
            ["profile.preview_edit_link"] = "édite ton profil",
            ["profile.preview_visible_badge"] = "Visible.",
            ["profile.preview_visible_tail"] =
                "verra ce bloc de contact dans l'annuaire des voisins.",
            ["profile.preview_hidden_badge"] = "Contact masqué.",
            ["profile.preview_hidden_tail"] =
                "ne verra pas de bloc de contact. Ton nom (et " +
                "le badge de vérification, si tu en as un) s'affiche " +
                "toujours dans l'annuaire — seules les coordonnées sont masquées.",
            ["profile.contact_address"] = "Adresse",
            ["profile.contact_email"] = "E-mail",
            ["profile.contact_phone"] = "Téléphone",
            ["profile.edit_profile_btn"] = "Modifier le profil",

            // ── profile (the _AudienceEditor shared partial) ────────────────
            ["profile.audience_visibility"] = "Qui peut voir ton profil",
            ["profile.audience_contact"] = "Qui peut voir tes coordonnées",
            ["profile.audience_off_note"] =
                "Tes coordonnées sont actuellement masquées pour tout le monde. " +
                "Pour changer, active « Partager mes coordonnées » ci-dessus.",
            ["profile.audience_match_mode"] = "Mode de correspondance",
            ["profile.audience_mode_any"] =
                "une personne est autorisée si elle correspond à l'une des personnes/groupes que tu as sélectionnés",
            ["profile.audience_mode_all"] =
                "une personne est autorisée uniquement si elle correspond à toutes les personnes/groupes que tu as sélectionnés",
            ["profile.audience_all_residents"] =
                "Tout le monde sur la plateforme (tous les résident·e·s connecté·e·s)",
            ["profile.audience_all_residents_hint"] =
                "Les nouveaux résident·e·s peuvent voir cela automatiquement — pas besoin de rééditer quand quelqu'un rejoint.",

            // ── posts (Detail page) ──────────────────────────────────────────
            ["posts.back_to"] = "retour à",
            ["posts.detail_edit"] = "Modifier",
            ["posts.edited"] = "modifié",
            ["posts.detail_why"] =
                "Tu peux voir cette publication parce qu'elle t'a été accordée — " +
                "soit tu en es l'auteur, soit elle a été partagée avec toi ou tes groupes.",
            ["posts.report_button"] = "Signaler cette publication",
            ["posts.reply_report_button"] = "Signaler cette réponse",
            ["posts.report_reason_label"] = "Quel est le problème ?",
            ["posts.report_optional"] = "optionnel",
            ["posts.report_note"] =
                "Signaler est une action d'intake — cela ne change " +
                "pas ce que tu peux voir, et un modérateur peut suivre.",
            ["posts.report_submit"] = "Signaler",
            ["posts.replies_heading"] = "Réponses",
            ["posts.replies_empty"] =
                "Pas encore de réponses. Si tu peux voir cette publication, tu peux y répondre.",
            ["posts.reply_heading_author"] = "Répondre (tu es l'auteur de cette publication)",
            ["posts.reply_heading"] = "Répondre",
            ["posts.reply_label"] = "Réponse",
            ["posts.reply_language_label"] = "Langue",
            ["posts.reply_language_note"] =
                "La langue dans laquelle tu réponds — un tag, pas une " +
                "traduction.",
            ["posts.reply_audience_note"] =
                "Les réponses n'ont pas d'audience propre — elles sont visibles " +
                "selon la décision d'audience unique de cette publication (la " +
                "règle « reply-inherits » C-M3·1). Tu réponds uniquement là où " +
                "la publication elle-même est visible.",
            ["posts.reply_submit"] = "Répondre",
            ["posts.reply_edit"] = "Modifier",
            ["posts.reply_save"] = "Enregistrer",
            ["posts.reply_edited"] = "modifié",

            // ── posts (Detail page) — author soft-delete (ADR 0024) ──
            ["posts.delete"] = "Supprimer",
            ["posts.reply_delete"] = "Supprimer",
            ["posts.deleted_placeholder"] =
                "Cette publication a été supprimée par son auteur.",
            ["posts.reply_deleted_placeholder"] =
                "Cette réponse a été supprimée par son auteur.",

            // ── posts (Detail page) — user-added translations (ADR 0022) ──
            ["posts.translations_label"] = "Traductions",
            ["posts.translations_none"] = "pas encore",
            ["posts.translation_add"] = "Ajouter",
            ["posts.translation_title_label"] = "Titre",
            ["posts.translation_body_label"] = "Corps",
            ["posts.translation_optional"] = "optionnel",
            ["posts.translation_save"] = "Enregistrer la traduction",
            ["posts.translation_edit"] = "Modifier",
            ["posts.translation_remove"] = "Supprimer",

            // ── pages (the PG lane — tree browse + post view, ADR 0039) ──────
            ["pages.title"]       = "Pages",
            ["pages.new_button"]  = "Nouvelle page",
            ["pages.none"] =
                "Pas encore de pages. Les admins globaux peuvent créer la première " +
                "page système — une page À propos est un bon point de départ — et " +
                "tout habitant peut démarrer son propre blog (une page à lui).",
            ["pages.back"]        = "← Retour aux pages",
            ["pages.by"]          = "par",
            ["pages.delete"]      = "Supprimer",
            ["pages.resetSeeded"] = "R\u00e9initialiser au texte seed\u00e9",
            ["pages.untitled"]    = "Page sans titre",

            // ── blog (per-resident page feed, ADR 0040) ─────────────────────
            ["blog.new_page"]     = "Nouvelle page de blog",
            ["blog.empty_own"] =
                "Tu n'as pas encore de pages de blog. Crée la première — elle devient " +
                "la racine de ton blog, et tu peux en imbriquer d'autres sous elle.",
            ["blog.empty_other"]  = "Cet habitant n'a pas encore de pages de blog.",
            ["blog.draft"]        = "Brouillon",

            // ── groups (Create page) ─────────────────────────────────────────
            ["groups.create_back"] = "← Retour aux groupes",
            ["groups.create_title"] = "Créer un groupe",
            ["groups.create_lede"] =
                "Un nom pour une communauté d'habitants (p. ex. « Bâtiment 4 », " +
                "« Bénévoles », « Cyclistes »). " +
                "Tu possèdes le groupe — tu peux ajouter et retirer des membres " +
                "depuis la page de détail du groupe.",
            ["groups.create_desc_hint"] = "Optionnel — une courte note que les autres habitants verront.",
            ["groups.create_private_hint"] =
                "Un groupe privé (p. ex. une famille) est invisible pour tout le monde — " +
                "seules les personnes que tu ajoutes comme membres peuvent le voir et l'utiliser. " +
                "Un groupe public (p. ex. « Champignonnistes ») apparaît comme " +
                "option dans les sélecteurs des autres habitants.",
            ["groups.create_submit"] = "Créer le groupe",

            // ── groups (Edit page) ───────────────────────────────────────────
            ["groups.edit_back"] = "retour à la publication",
            ["groups.edit_title"] = "Modifier ta publication",
            ["groups.edit_lede"] =
                "Tu modifies ta propre publication. Seul tu peux la modifier — " +
                "l'adhésion au groupe décide qui peut la voir, mais seul son " +
                "auteur peut la changer. Le groupe dans lequel cette publication " +
                "apparaît est fixé ; seul le titre, le corps et la langue " +
                "ci-dessous sont éditables.",
            ["groups.edit_title_label"] = "Titre",
            ["groups.edit_title_hint"] =
                "Un court titre (≤ 120 caractères). Laisser vide pour " +
                "une publication sans titre — la liste affichera " +
                "ta première ligne du corps à la place.",
            ["groups.edit_body_label"] = "Corps",
            ["groups.edit_language_label"] = "Langue",
            ["groups.edit_language_hint"] =
                "La langue dans laquelle tu écris cette publication. C'est seulement " +
                "un tag — elle n'est pas traduite — et elle garde le " +
                "texte trouvable plus tard et permet à un lecteur d'ajouter " +
                "sa propre version dans sa langue s'il le veut.",
            ["groups.edit_submit"] = "Enregistrer les modifications",
            ["groups.edit_cancel"] = "Annuler",

            // ── directory (Detail page) ──────────────────────────────────────
            ["directory.detail_back"] = "← Retour à l'annuaire",
            ["directory.detail_verified"] = "Vérifié",
            ["directory.detail_contact_address"] = "Adresse",
            ["directory.detail_contact_email"] = "E-mail",
            ["directory.detail_contact_phone"] = "Téléphone",
            ["directory.detail_no_contact"] =
                "Cet habitant ne t'a pas (encore) partagé de moyen de contact. Tu " +
                "peux toujours voir sa page de profil.",

            // ── community (Manage page) ──────────────────────────────────────
            ["community.manage_back"] = "← Retour au fil",
            ["community.manage_lede"] = "Gérer l'adhésion à cette communauté.",
            ["community.manage_moderate"] = "Tu modères cette communauté",
            ["community.manage_disabled"] = "Désactivé",
            ["community.manage_availability"] = "Disponibilité",
            ["community.manage_mandatory_label"] =
                "Communauté obligatoire — tout le monde dans le quartier est membre",
            ["community.manage_mandatory_hint"] =
                "Une communauté obligatoire ne peut pas voir ses membres retirés ni " +
                "être quittée ; décoche la case pour " +
                "rendre l'adhésion optionnelle à nouveau.",
            ["community.manage_make_optional"] = "Rendre optionnel",
            ["community.manage_make_mandatory"] = "Rendre obligatoire",
            ["community.manage_members"] = "Membres",
            ["community.manage_mandatory_note"] =
                "Cette communauté est obligatoire — tout le monde est membre, donc " +
                "il n'y a personne à retirer. Les lignes listées sont " +
                "des adhésions explicites, conservées au cas où la " +
                "communauté redeviendrait optionnelle.",
            ["community.manage_no_members"] = "Pas encore de membres explicites — ajoute-en ci-dessous.",
            ["community.manage_you"] = "Toi",
            ["community.manage_remove"] = "Retirer",
            ["community.manage_add_member"] = "Ajouter un membre",
            ["community.manage_all_members"] =
                "Tout le monde dans le quartier est déjà membre ici.",
            ["community.manage_pick_resident"] = "Choisis un habitant à ajouter…",

            // ── ADR 0026 — community name/description translations ────────
            ["community.translations_label"] = "Traductions",
            ["community.translations_none"] = "Pas encore",
            ["community.translations_page_title"] = "Traductions",
            ["community.translations_page_lede"] = "Nom et description de cette communauté dans d'autres langues.",
            ["community.translations_view_only"] = "Vous pouvez consulter les traductions ; seuls un GlobalAdmin ou un Traducteur peuvent les ajouter, modifier ou supprimer.",
            ["community.translation_add"] = "Ajouter",
            ["community.translation_name_label"] = "Nom",
            ["community.translation_desc_label"] = "Description",
            ["community.translation_optional"] = "optionnel",
            ["community.translation_min_one"] = "Au moins un nom ou une description est requis.",
            ["community.translation_save"] = "Enregistrer la traduction",

            // ── community feed buttons (Posts/Index.cshtml) ─────────────
            ["community.feed_manage_members"] = "Gérer les membres",
            ["community.feed_translations"] = "Traductions",

            // ── moderation (Index page) ──────────────────────────────────────
            ["moderation.title"] = "Modération",
            ["moderation.empty"] = "Pas encore de signalements. La file est vide.",
            ["moderation.th_status"] = "Statut",
            ["moderation.th_post"] = "Publication",
            ["moderation.th_component"] = "Composant",
            ["moderation.th_reporter"] = "Signaleur",
            ["moderation.th_filed"] = "Déposé",
            ["moderation.th_action"] = "Action",
            ["moderation.review"] = "Examiner →",

            // ── moderation (Resolve page) ────────────────────────────────────
            ["moderation.resolve_title"] = "Modération — Examiner un signalement",
            ["moderation.details"] = "Détails du signalement",
            ["moderation.th_post_label"] = "Publication",
            ["moderation.th_component_label"] = "Composant",
            ["moderation.th_reporter_label"] = "Signaleur",
            ["moderation.th_author_label"] = "Auteur de la publication",
            ["moderation.th_filed_label"] = "Déposé",
            ["moderation.th_reason_label"] = "Raison",
            ["moderation.no_reason"] = "(aucune raison donnée)",
            ["moderation.th_body_label"] = "Corps de la publication",
            ["moderation.body_preview"] = "aperçu de la publication",
            ["moderation.assign_header"] = "Assigner à un modérateur",
            ["moderation.assign_label"] =
                "Un modérateur couvrant cette communauté",
            ["moderation.assign_pick"] = "Choisir un modérateur…",
            ["moderation.assign_submit"] = "Assigner",
            ["moderation.cancel"] = "Annuler",
            ["moderation.unlock_submit"] = "Déverrouiller",
            ["moderation.resolve_header"] = "Réoudre (clôturer ce signalement)",
            ["moderation.resolve_submit"] = "Réoudre",
            ["moderation.back_to_queue"] = "← Retour à la file",

            // ── reply-report-target lane (ADR 0023) ─────────────────────────
            ["moderation.queue_reply_by"] = "réponse de",
            ["moderation.resolve_reply_label"] = "Réponse (cible de ce signalement)",
            ["moderation.resolve_reply_by"] = "Réponse de",

            // ── account (Verify / Resend / AccessDenied) ─────────────────────
            ["account.verify_title"] = "Vérifier ton compte",
            ["account.verify_pending"] =
                "Nous confirmons ton compte — tu seras connecté dans un instant.",
            ["account.verify_again"] = "S'inscrire à nouveau",
            ["account.resend_title"] = "Renvoyer l'e-mail de confirmation",
            ["account.resend_lede"] =
                "Entre l'e-mail avec lequel tu t'es inscrit et nous enverrons un nouveau lien de vérification.",
            ["account.resend_submit"] = "Renvoyer",
            ["account.resend_create"] = "Tu crées ton compte ?",
            ["account.resend_signup"] = "S'inscrire",
            ["account.denied_title"] = "Accès refusé",
            ["account.denied_lede"] = "Tu n'as pas la permission de voir cette page.",
            ["account.denied_home"] = "Retour à l'accueil",

            // ── admin (Audit page) ───────────────────────────────────────────
            ["admin.audit_title"] = "Audit d'accès",
            ["admin.audit_lede"] =
                "Un journal des accès accordés ou refusés à du contenu restreint, " +
                "plus les actions admin. Ce journal est toujours conservé et nettoyé " +
                "périodiquement selon la politique de rétention de l'instance.",
            ["admin.audit_filter"] = "Filtrer",
            ["admin.audit_th_at"] = "À (UTC)",
            ["admin.audit_th_actor"] = "Acteur",
            ["admin.audit_th_effective"] = "Effectif",
            ["admin.audit_th_action"] = "Action",
            ["admin.audit_th_target"] = "Cible",
            ["admin.audit_th_aggregate"] = "Agrégat",
            ["admin.audit_th_via"] = "Via",
            ["admin.audit_th_outcome"] = "Résultat",

            // ── admin (Break-glass page) ─────────────────────────────────────
            ["admin.breakglass_title"] = "Break-glass",
            ["admin.breakglass_granted"] = "Accordé à (UTC)",
            ["admin.breakglass_expires"] = "Expire à (UTC)",
            ["admin.breakglass_status"] = "Statut",
            ["admin.breakglass_consumed"] = "activé — élévation en cours jusqu'à expiration",
            ["admin.breakglass_presented"] = "configuré, mais pas encore activé",
            ["admin.breakglass_token_label"] = "Code unique (de l'opérateur)",
            ["admin.breakglass_token_hint"] =
                "L'utilisation de ce code est une action unique. Il active l'élévation " +
                "jusqu'à son expiration.",
            ["admin.breakglass_consume"] = "Activer",

            // ── locale (settings + public picker) ────────────────────────────
            ["locale.settings_title"] = "Tes paramètres",
            ["locale.language_heading"] = "Langue",
            ["locale.lede"] =
                "Choisis la langue que la plateforme t'affiche. Ton choix est enregistré dans " +
                "un cookie du navigateur — il prend effet à la prochaine requête, et " +
                "n'affecte jamais les autres habitants.",
            ["locale.preferred_label"] = "Langue préférée",
            ["locale.default_note"] =
                "Le défaut de l'instance est ",
            ["locale.default_note_tail"] =
                ". Si ta langue préférée est supprimée par un admin plus tard, " +
                "la plateforme bascule silencieusement sur le défaut de l'instance.",
            ["locale.save"] = "Enregistrer",
            ["locale.reset"] = "Réinitialiser au défaut de l'instance",
            ["locale.public_title"] = "Choisis ta langue",
            ["locale.instance_default"] = "— défaut de l'instance",
            // ADR 0046 — the browser-match suggestion (pre-selection + marker).
            ["locale.browser_matched"] = "— d'après ton navigateur",
            ["locale.browser_note"] =
                "Nous avons choisi ",
            ["locale.browser_note_tail"] =
                " d'après les réglages de ton navigateur. L'enregistrer en " +
                "fait ta langue préférée — elle reste telle quelle jusqu'à " +
                "ce que tu la changes.",
            // Messages flash (surface toast — LocaleController.Save /
            // PublicLocaleController.Save ; {0} = code de langue).
            ["locale.flash_set"] =
                "Langue réglée sur \"{0}\" — elle prend effet à la prochaine requête.",
            ["locale.flash_reset"] =
                "Préférence de langue réinitialisée — le défaut de l'instance sera utilisé.",

            // ── announcements (shared labels + New/Edit compose) ─────────────
            ["announcements.scope_label"] = "Qui voit cela ?",
            ["announcements.scope_public"] =
                "Tout le monde (public) — visible par les visiteurs et les habitants",
            ["announcements.scope_resident"] =
                "Habitants — visible uniquement quand connecté",
            ["announcements.community_label"] = "Envoyer à une communauté spécifique (optionnel)",
            ["announcements.all_residents"] = "Tous les habitants",
            ["announcements.community_hint"] =
                "Laisser sur « Tous les habitants » pour envoyer à tout le monde, ou choisir une communauté pour restreindre qui le voit.",
            ["announcements.title_label"] = "Titre",
            ["announcements.title_hint"] = "Un court titre (jusqu'à 120 caractères).",
            ["announcements.body_label"] = "Corps",
            ["announcements.pin_label"] = "Épingler en haut de toutes les pages",
            ["announcements.cancel"] = "Annuler",
            ["announcements.new_title"] = "Nouvelle annonce",
            ["announcements.new_lede"] =
                "Les annonces sont des avis qui apparaissent en propre, " +
                "séparés du fil de la communauté. Une annonce publique " +
                "est visible par tout le monde, y compris les personnes non " +
                "connectées (p. ex. une fenêtre de maintenance). Une annonce " +
                "habitant n'est visible que par les habitants connectés " +
                "(p. ex. un appel « aide-nous avec X »).",
            ["announcements.new_scope_hint"] =
                "Les annonces publiques sont visibles par tout le monde, y compris " +
                "les visiteurs non connectés (p. ex. une fenêtre de maintenance " +
                "ou un avis de panne). Les annonces habitants " +
                "ne sont visibles que par les habitants connectés.",
            ["announcements.new_submit"] = "Créer l'annonce",
            ["announcements.edit_title"] = "Modifier l'annonce",
            ["announcements.edit_lede"] =
                "Mets à jour le titre, le corps et la visibilité de cette " +
                "annonce. Une annonce publique est visible par tout le monde, " +
                "y compris les personnes non connectées " +
                "(p. ex. une fenêtre de maintenance). Une annonce habitant " +
                "n'est visible que par les habitants connectés " +
                "(p. ex. un appel « aide-nous avec X »).",
            ["announcements.edit_scope_hint"] =
                "Changer qui voit cela s'applique immédiatement pour le prochain lecteur.",
            ["announcements.edit_submit"] = "Enregistrer les modifications",
            ["announcements.pin_hint"] =
                "Une annonce épinglée apparaît aussi comme bannière " +
                "en haut de chaque page (y compris l'accueil), " +
                "en plus de la liste habituelle des annonces. Sa " +
                "visibilité suit toujours l'audience que tu as choisie ci-dessus : " +
                "un épinglage public s'affiche à tout visiteur ; un épinglage " +
                "habitant uniquement quand un utilisateur est connecté. Si plus " +
                "d'une est épinglée, la plus récemment épinglée l'emporte.",
            ["announcements.language_note"] =
                "La langue dans laquelle tu écris cette annonce. C'est seulement " +
                "un tag — elle n'est pas traduite — et elle garde le texte " +
                "trouvable plus tard et permet à un lecteur d'ajouter sa propre " +
                "version dans sa langue s'il le veut.",

            // ── announcements (Index + Detail + pinned banner) ───────────────
            ["announcements.index_title"] = "Annonces",
            ["announcements.index_lede"] =
                "Avis de la plateforme : les annonces publiques sont visibles par tout le monde (p. ex. " +
                "maintenance planifiée) ; les annonces réservées aux habitants " +
                "sont visibles par tous les utilisateurs connectés " +
                "(p. ex. appels « aide-nous avec X »).",
            ["announcements.all"] = "Toutes les annonces",
            ["announcements.new_button"] = "Nouvelle annonce",
            ["announcements.empty"] = "Pas encore d'annonces.",
            ["announcements.read_more"] = "Lire la suite…",
            ["announcements.scope_everyone"] = "tout le monde",
            ["announcements.scope_residents"] = "habitants",
            ["announcements.pinned_badge"] = "épinglée",
            ["announcements.edit_button"] = "Modifier",
            ["announcements.delete"] = "Supprimer",
            ["announcements.detail_back"] = "← Retour aux annonces",
            ["announcements.detail_untitled"] = "Annonce sans titre",
            ["announcements.by"] = "par",
            ["announcements.edited"] = "modifiée",
            ["announcements.banner_read_more"] = "Lire la suite",
            ["announcements.banner_all"] = "Toutes les annonces",

            // ── static pages (Page — the terms/help shell) ───────────────────
            ["static.last_updated"] = "Dernière mise à jour :",

            // ── shared (the _GrantPickers partial — static markup only) ─────
            ["grant.heading"] = "À qui accorder",
            ["grant.hint"] =
                "Coche une ou plusieurs — ou utilise la ligne « Select all » " +
                "au-dessus de chaque liste comme raccourci.",
            ["grant.empty_users"] =
                "Tu es le seul habitant vérifié, donc il n'y a encore " +
                "personne à qui accorder.",
            ["grant.empty_groups"] =
                "Aucun groupe n'existe encore sur la plateforme — crée-en un " +
                "sous « Groupes » pour ajouter une visibilité par groupe.",

            // ── admin (page heading + primary action) ───────────────────────
            ["admin.title"]  = "Administration",
            ["admin.verify"] = "Vérifier",

            // ── rich editor (the RE toolbar button labels, ADR 0031) ────────
            ["rc.editor.bold"]    = "B",
            ["rc.editor.italic"]  = "I",
            ["rc.editor.code"]    = "C",
            ["rc.editor.h1"]      = "H1",
            ["rc.editor.h2"]      = "H2",
            ["rc.editor.h3"]      = "H3",
            ["rc.editor.list"]    = "•",
            ["rc.editor.olist"]   = "1.",
            ["rc.editor.link"]    = "Lien",
            ["rc.editor.image"]   = "Image",
            ["rc.editor.attach"]  = "Joindre un fichier",
            ["rc.editor.source"]      = "</>",
            ["rc.editor.showPreview"] = "Aperçu",

            // ── about (the About product surface, ADR 0042 D5) ──────────────
            ["about.eyebrow"]             = "Privé par défaut",
            ["about.lead"] =
                "Un chez-soi pour tout ce que ton quartier fait — " +
                "le fil, les groupes et les notes qui méritent mieux " +
                "qu'un groupe de chat. Privé, en langage clair, et à toi.",
            ["about.cta_feed"]            = "Voir le fil",
            ["about.cta_notes"]           = "Lire les notes épinglées",
            ["about.features.one.title"]  = "Un fil pour la rue",
            ["about.features.one.body"] =
                "Publications et fils de discussion de tes blocs et ruelles, en " +
                "un seul endroit calme — pas d'algorithme, pas de bruit.",
            ["about.features.groups.title"]  = "Des groupes qui collent",
            ["about.features.groups.body"] =
                "Échange de jardin, club de lecture, ronde de nuit — un " +
                "groupe pour tout ce que le quartier fait déjà.",
            ["about.features.pinned.title"]  = "Épinglées là où ça compte",
            ["about.features.pinned.body"] =
                "Coupures d'eau, travaux de rue, les nouveaux " +
                "ralentisseurs — des notes qui restent en place au lieu de défiler.",
            ["about.project.eyebrow"]  = "Open source",
            ["about.project.heading"]  = "Le code, les décisions, la doc de design",
            ["about.project.lead"] =
                "Si tu es curieux de savoir comment ça marche — ou si tu " +
                "t'apprêtes à l'héberger pour ton quartier — tout est public.",

            // ── tags (the TG lane, ADR 0044 — browse + composer affordances) ──
            ["tags.list.heading"]       = "Étiquettes",
            ["tags.list.lede"] =
                "Les sujets dont vos publications et pages de blog sont étiquetées — " +
                "clique sur l'une d'elles pour parcourir ce qui en a été publié.",
            ["tags.list.empty"] =
                "Pas encore d'étiquettes — elles apparaissent ici dès qu'un habitant " +
                "en ajoute une à une publication ou à une page de blog.",
            ["tags.bytag.heading"]      = "Publications et pages à propos de",
            ["tags.bytag.posts_heading"] = "Publications",
            ["tags.bytag.pages_heading"] = "Pages de blog",
            ["tags.bytag.empty"] =
                "Aucune publication ni page de blog lisible ne porte cette étiquette.",
            ["tag.input.placeholder"] = "p. ex. salubrité, budget, rue-érable",
            ["tag.input.hint"] =
                "Écris pour chercher des étiquettes existantes, ou en créer une " +
                "nouvelle — elle sera ajoutée à cette publication.",
            ["tag.suggest.empty"] =
                "Aucune étiquette correspondante — continue à écrire ou crée-en une.",
            ["tag.translate.heading"] = "Traductions",
            ["tag.translate.save"] = "Enregistrer",
            ["tag.translate.disabled"] = "Seul le créateur ou un GlobalAdmin peut la reformuler.",

            // ── platform (scope + the FIG philosophy, home/about) ───────────
            ["platform.scope_home"] =
                "Kumunita est née comme un foyer pour un seul quartier — mais elle se sent tout aussi bien chez un club, une équipe, ou les personnes derrière un grand événement. Le quartier est la valeur par défaut, pas la limite.",
            ["platform.fig_home"] =
                "Elle repose sur les Fractal Integration Guidelines (FIG) : l'idée que la vraie valeur d'un groupe vit dans les liens entre ses personnes et ses éléments, pas dans les éléments eux-mêmes. Kumunita est le logiciel qui tisse et maintient ces liens.",
            ["platform.scope_heading"] = "Conçu pour un quartier — à sa place partout",
            ["platform.scope_body1"] =
                "Kumunita a d'abord été construit pour une rue : un foyer privé et partagé pour les gens qui y vivent. Mais l'idée centrale n'est liée à aucune rue. C'est un foyer privé pour un groupe de personnes qui veulent coordonner, partager et tisser la confiance ensemble — un quartier, un club, une équipe sportive, un lieu de travail, une association, ou même les personnes derrière un seul événement. Ce qui change, c'est seulement le nom et les détails.",
            ["platform.scope_body2"] =
                "Tout ce qui lui donne un esprit de rue — le fil paisible, les groupes, les posts adressés à des publics, la modération et sa piste d'audit, l'interface multilingue — fonctionne exactement pareil pour chacun d'entre eux. L'adapter est donc une question de configuration : le nom de la communauté, les composants qui tiennent lieu de « Sécurité » et « Social », les groupes qui vont avec votre monde. Pas une refonte.",
            ["platform.fig_heading"] = "L'idée derrière : les Fractal Integration Guidelines",
            ["platform.fig_body1"] =
                "Kumunita repose sur un petit jeu de lignes directrices ouvertes que nous appelons les Fractal Integration Guidelines (FIG). Leur idée centrale : un système intégré est plus que la somme de ses parties, et sa qualité, c'est la qualité des liens entre ces parties — une propriété qu'aucune partie n'a à elle seule. Un sac de fonctions qui ne se relient jamais n'est que du bruit ; c'est dans les liens que vit la valeur.",
            ["platform.fig_body2"] =
                "Un quartier est exactement un tel système : de nombreuses personnes, foyers et préoccupations différents, dont le tissage — qui connaît qui, à qui l'on peut se fier, comment un problème se résout réellement entre les personnes — produit une communauté. Une seule personne n'est pas un quartier. Kumunita est le logiciel qui tisse et maintient ce lien.",
            ["platform.fig_body3"] =
                "Nous développons donc Kumunita selon la même règle que nous lui demandons : la qualité, c'est la qualité du lien, pas le nombre de fonctions. Les lignes directrices se répètent à chaque échelle — un module, une fonction, une communauté, les gens qui la construisent — c'est pourquoi tout le projet les suit, et pourquoi la philosophie est documentée en public.",
            ["platform.scope_eyebrow"] = "Pour tout groupe",
            ["platform.fig_eyebrow"] = "La philosophie",

            // ── events (M4 — ADR 0054 : la surface /Events ; index, détail, composer) ──
            ["events.title"] = "Événements",
            ["events.lede"] =
                "Les événements à venir dans le quartier — découvre ce qui est prévu et indique ta présence.",
            ["events.new_event"] = "Nouvel événement",
            ["events.new_lead"] =
                "Partage un événement à venir avec le quartier. Par défaut, il est visible par tout le monde ; " +
                "désactive-le dans la section « Public » seulement si tu veux restreindre qui peut le voir.",
            ["events.edit_event"] = "Modifier l'événement",
            ["events.edit_lead"] =
                "Mets à jour les détails de cet événement. Ton choix du public est la seule limite d'accès — " +
                "le choix de la communauté n'est qu'un filtre.",
            ["events.title_hint"] = "Un titre court pour l'événement.",
            ["events.start"] = "Début",
            ["events.end"] = "Fin",
            ["events.time_hint"] =
                "Le moment où l'événement a lieu. Les visiteurs le voient dans leur propre fuseau horaire.",
            ["events.location"] = "Lieu",
            ["events.capacity"] = "Capacité",
            ["events.color"] = "Couleur",
            ["events.location_hint"] =
                "Le lieu, la capacité et la couleur sont des détails d'affichage seulement — ils ne limitent pas qui peut indiquer sa présence.",
            ["events.all_communities"] = "Toutes les communautés",
            ["events.community_hint"] =
                "La communauté sous laquelle cet événement apparaît — un filtre, pas une limite d'accès.",
            ["events.tags_placeholder"] = "ex. nettoyage, rencontre, jardin",
            ["events.audience_heading"] = "Public — qui peut voir cet événement",
            ["events.audience_default"] =
                "Par défaut — tout le monde peut voir cet événement. Désactive-le seulement si tu veux restreindre qui peut le voir.",
            ["events.audience_mode_any"] = "un spectateur correspond s'il est sur l'un des choix",
            ["events.audience_mode_all"] =
                "un spectateur doit être sur chaque choix — une liste vide refuse à tous",
            ["events.reminder"] = "Envoyer le rappel de 24 heures à ceux qui indiquent \"Going\"",
            ["events.reminder_hint"] =
                "Un rappel est envoyé la veille de l'événement à tous ceux qui viennent. Désactive-le pour le passer.",
            ["events.create"] = "Créer l'événement",
            ["events.save_changes"] = "Enregistrer les modifications",
            ["events.empty"] = "Aucun événement à venir pour l'instant.",
            ["events.back"] = "← Retour aux événements",
            ["events.draft"] = "Brouillon",
            ["events.draft_title"] = "Seul tu peux voir cela — ce n'est pas encore public",
            ["events.publish"] = "Publier",
            ["events.edit"] = "Modifier",
            ["events.delete"] = "Supprimer",
            ["events.delete_confirm"] = "Supprimer cet événement ? Cela ne peut pas être annulé.",
            ["events.untitled"] = "Événement sans titre",
            ["events.rsvp"] = "Présence",
            ["events.rsvp_you"] = "Tu indiques :",
            ["events.rsvp_responses"] = "Réponses",
            ["events.rsvp_update"] = "Mettre à jour",
            ["events.remove_translation_confirm"] = "Supprimer cette traduction ?",

            // ── events.mine (la section « Tes prochains événements » sur /events — ADR 0065) ──
            ["events.mine.title"] = "Tes prochains événements",
            ["events.mine.hint"] = "Événements auxquels tu as répondu ou que tu as organisés.",

            // ── events.calendar (la vue calendrier EV-CAL — /events/calendar, ADR 0063) ──
            ["events.calendar.title"] = "Calendrier",
            ["events.calendar.prev"] = "Précédent",
            ["events.calendar.next"] = "Suivant",
            ["events.calendar.today"] = "Aujourd'hui",
            ["events.calendar.overlap_hint"] = "Chevauche un autre événement dans cette période",
            ["events.calendar.empty"] = "Aucun événement dans cette période.",
            ["events.calendar.from"] = "Depuis",
            // EV-DWM (ADR 0064, U05) — les libellés du commutateur Jour/Semaine/Mois (C-DWM·9).
            ["events.calendar.view.day"] = "Jour",
            ["events.calendar.view.week"] = "Semaine",
            ["events.calendar.view.month"] = "Mois",

            // ── grant (le picker « À qui accorder » — C# « Tout sélectionner » + compteur) ──
            ["grant.select_all"] = "Tout sélectionner",
            ["grant.label_residences"] = "Résidents",
            ["grant.label_groups"] = "Groupes",
            ["grant.count_selected"] = "{0} sur {1} sélectionné(s)",

            // ── profile (l'avertissement _AudienceEditor ; les mots <b> inline sont séparés) ──
            ["profile.audience_empty_warning_lead"] = "Attention :",
            ["profile.audience_empty_warning_body"] =
                "tu n'as choisi personne et aucun groupe ci-dessous, donc tes coordonnées sont actuellement cachées de",
            ["profile.audience_empty_warning_everyone"] = "tout le monde",
            ["profile.audience_empty_warning_tail"] =
                ". Ajoute une personne ou un groupe si tu souhaites les partager.",

            // ── settings (la section « Langue des e-mails et des notifications » sous /settings/language) ──
            ["settings.email_title"] = "Langue des e-mails et des notifications",
            ["settings.email_lede"] =
                "Choisis la langue dans laquelle la plateforme t'écrit — e-mails du compte et rappels d'événement. " +
                "Ton choix est enregistré sur ton compte.",
            ["settings.email_label"] = "Langue des e-mails et des notifications",
            ["settings.email_note"] =
                "Si tu choisis une langue, tes e-mails et tes rappels sont envoyés dans celle-ci. " +
                "Si tu réinitialises, la langue par défaut de l'instance est utilisée.",
            ["settings.email_save"] = "Enregistrer",
            ["settings.email_reset"] = "Réinitialiser à la valeur par défaut",
            ["settings.email_flash_set"] = "Langue des e-mails et des notifications réglée — ton prochain e-mail l'utilisera.",
            ["settings.email_flash_reset"] = "Langue des e-mails et des notifications réinitialisée — le défaut de l'instance sera utilisé.",
            ["settings.email_reset_confirm"] =
                "Réinitialiser la langue des e-mails et des notifications à la valeur par défaut ?",

            // ── email (e-mails sortants — {0}/{1} sont les placeholders d'exécution) ──
            ["email.verify_subject"] = "Vérifie ton compte Kumunita",
            ["email.verify_body"] =
                "Bonjour {0},\n\nTon compte Kumunita est à vérifier lors de ta première connexion. " +
                "Ouvre ce lien à usage unique pour confirmer le compte (il te connecte aussi) :\n\n{1}\n\n" +
                "Si tu n'as pas créé ce compte, tu peux ignorer ce message.",
            ["email.reminder_subject"] = "Rappel : {0}",
            ["email.reminder_body"] = "**{0}** arrive : {1}{2}.",

            // ── notifications (M6 — ADR 0076: the /notifications surface;
            // translations of the en floor above — same key set, same
            // dotted shape, no email templates for the reserved
            // post.mention kind) ──────────────────────────────────────────
            ["notifications.inbox"] = "Notifications",
            ["notifications.preferences"] = "Préférences",
            ["notifications.mark_all_read"] = "Tout marquer comme lu",
            ["notifications.empty"] = "Rien pour l'instant — les choses qui t'arrivent apparaîtront ici.",
            ["notifications.bell"] = "Notifications",
            ["notifications.view"] = "Voir",
            ["notifications.preferences.title"] = "Préférences de notification",
            ["notifications.preferences.intro"] = "Choisis quelles notifications tu reçois aussi par courriel. La boîte d'arrivée enregistre toujours chaque notification.",
            ["notifications.preferences.save"] = "Enregistrer les préférences",
            ["notifications.preferences.coming_soon"] = "bientôt",
            ["notifications.kind.post.reply"] = "Réponse",
            ["notifications.kind.post.mention"] = "Mention",
            ["notifications.kind.group.post"] = "Publication de groupe",
            ["notifications.kind.group.added"] = "Ajouté à un groupe",
            ["notifications.kind.group.invite"] = "Invitation à un groupe",
            ["notifications.kind.event.rsvp"] = "RSVP",
            ["notifications.kind.event.reminder"] = "Rappel",
            ["notifications.kind.report.filed"] = "Signalement",
            ["notifications.kind.report.assigned"] = "Signalement assigné",
            ["notifications.kind.report.resolved"] = "Signalement résolu",
            ["notifications.kind.todo.assign"] = "Tâche assignée",
            ["notifications.preference.post.reply.label"] = "Les réponses à mes publications",
            ["notifications.preference.post.mention.label"] = "Les mentions de moi",
            ["notifications.preference.group.post.label"] = "Les nouvelles publications de mes groupes",
            ["notifications.preference.group.added.label"] = "Quand on m'ajoute à un groupe",
            ["notifications.preference.group.invite.label"] = "Quand on m'invite à un groupe",
            ["notifications.preference.event.rsvp.label"] = "Les RSVP à mes événements",
            ["notifications.preference.event.reminder.label"] = "Les rappels d'événements",
            ["notifications.preference.report.filed.label"] = "Les signalements sur mes contenus",
            ["notifications.preference.report.assigned.label"] = "Les signalements qui m'ont été assignés",
            ["notifications.preference.report.resolved.label"] = "La résolution des signalements auxquels je participe",
            ["notifications.preference.todo.assign.label"] = "Les tâches qui m'ont été assignées",
            ["notification.post.reply.subject"] = "Une réponse a été ajoutée à ta publication",
            ["notification.group.post.subject"] = "Une nouvelle publication dans ton groupe",
            ["notification.group.added.subject"] = "Tu as été ajouté à un groupe",
            ["notification.group.invite.subject"] = "Tu as été invité à un groupe",
            ["notification.event.rsvp.subject"] = "Un RSVP à ton événement",
            ["notification.event.reminder.subject"] = "Rappel d'événement",
            ["notification.report.filed.subject"] = "Un signalement a été déposé sur ta publication",
            ["notification.report.assigned.subject"] = "Un signalement t'a été assigné",
            ["notification.report.resolved.subject"] = "Un signalement a été résolu",
            ["notification.todo.assign.subject"] = "Une tâche t'a été assignée",
            ["notification.post.reply.body"] = "Quelqu'un a répondu à l'une de tes publications : ",
            ["notification.group.post.body"] = "Nouvelle publication dans l'un de tes groupes : ",
            ["notification.group.added.body"] = "Tu as été ajouté au groupe ",
            ["notification.group.invite.body"] = "Tu as été invité à rejoindre le groupe ",
            ["notification.event.rsvp.body"] = "Quelqu'un a répondu à l'un de tes événements. ",
            ["notification.event.reminder.body"] = "Voici ton événement à venir : ",
            ["notification.report.filed.body"] = "Un résident a déposé un signalement sur l'une de tes publications. ",
            ["notification.report.assigned.body"] = "Un signalement t'a été assigné en tant que modérateur. ",
            ["notification.report.resolved.body"] = "Un signalement auquel tu étais impliqué a été résolu. ",
            ["notification.todo.assign.body"] = "Une tâche t'a été assignée : ",
            ["notifications.kind.account.signup"] = "Nouveau résident",
            ["notifications.kind.account.verified"] = "Compte vérifié",
            ["notifications.preference.account.signup.label"] = "Quand un nouveau résident s'inscrit",
            ["notifications.preference.account.verified.label"] = "Quand un résident vérifie son compte",
            ["notification.account.signup.subject"] = "Un nouveau résident s'est inscrit",
            ["notification.account.signup.body"] = "Un nouveau résident s'est inscrit : ",
            ["notification.account.verified.subject"] = "Un résident a vérifié son compte",
            ["notification.account.verified.body"] = "Un résident a vérifié son compte : ",

            // ── ADR 0084 ──
            ["notification.announcement.subject"] = "Une nouvelle annonce",
            ["notification.announcement.body"] = "Une nouvelle annonce a été publiée : ",
            ["notification.community.post.subject"] = "Une nouvelle publication dans ta communauté",
            ["notification.community.post.body"] = "Nouvelle publication dans l'une de tes communautés : ",
            ["notification.page.child.subject"] = "Une nouvelle page a été ajoutée",
            ["notification.page.child.body"] = "Une nouvelle page a été ajoutée sous une page que tu suis : ",
            ["notifications.kind.announcement"] = "Nouvelle annonce",
            ["notifications.kind.community.post"] = "Nouvelle publication de communauté",
            ["notifications.kind.page.child"] = "Nouvelle sous-page",
            ["notifications.preference.announcement.label"] = "Nouvelles annonces",
            ["notifications.preference.community.post.label"] = "Nouvelles publications dans mes communautés",
            ["notifications.preference.page.child.label"] = "Nouvelles sous-pages sur les pages que je suis",
            ["notifications.subscriptions.title"] = "Abonnements aux notifications",
            ["notifications.subscriptions.intro"] = "Choisis quelles communautés, quels groupes et quelles pages t'informent. Les préférences décident des types que tu reçois aussi par e-mail ; ces interrupteurs décident des cibles qui t'informent.",
            ["notifications.subscription.announcement.label"] = "Nouvelles annonces",
            ["notifications.subscription.community.post.label"] = "Nouvelles publications dans les communautés",
            ["notifications.subscription.group.post.label"] = "Nouvelles publications dans les groupes",
            ["notifications.subscription.page.child.label"] = "Nouvelles sous-pages",
            ["pages.subscribe"] = "S'abonner aux mises à jour",
            ["pages.unsubscribe"] = "Se désabonner des mises à jour",

            // ── PL (ADR 0086, U05) — la landing /projects + l'onglet Projects ──
            ["pl.tabs.projects"] = "Projets",
            ["pl.index.title"] = "Projets",
            ["pl.index.lede"] = "Les objectifs et projets du quartier — le travail de plus haut niveau au-dessus des tâches et des tableaux.",
            ["pl.index.goals_heading"] = "Objectifs",
            ["pl.index.projects_heading"] = "Projets",
            ["pl.index.new_goal"] = "Nouvel objectif",
            ["pl.index.new_project"] = "Nouveau projet",
            ["pl.index.view_projects"] = "Voir les projets →",
            ["pl.index.goals_empty"] = "Pas encore d'objectifs — crée-en un pour donner une direction au travail partagé.",
            ["pl.index.projects_empty"] = "Pas encore de projets autonomes — crée-en un pour commencer à organiser le travail partagé.",
            ["pl.index.start"] = "Début",
            ["pl.index.due"] = "Échéance",

            // ── PL (ADR 0086, U06) — le détail d'objectif + composer + édition ──
            ["pl.goal.new_heading"] = "Nouvel objectif",
            ["pl.goal.new_lede"] = "Un objectif donne une direction au travail partagé — avec une description optionnelle, un filtre de communauté et une audience. Des projets peuvent s'y rattacher plus tard.",
            ["pl.goal.create"] = "Créer l'objectif",
            ["pl.goal.edit_heading"] = "Modifier l'objectif",
            ["pl.goal.edit_lead"] = "Mets à jour le titre et la description de cet objectif. Son audience, sa communauté et sa langue sont fixés à la création.",
            ["pl.goal.save"] = "Enregistrer les modifications",
            ["pl.goal.edit"] = "Modifier l'objectif",
            ["pl.goal.title_hint"] = "Un court nom pour l'objectif — l'étiquette du flux.",
            ["pl.goal.description_hint"] = "Une description optionnelle de la direction que cet objectif donne au travail partagé. Un objectif fonctionne aussi sans description.",
            ["pl.goal.audience_heading"] = "Audience — qui peut voir cet objectif",
            ["pl.goal.audience_public"] = "Visible par toute l'instance.",
            ["pl.goal.audience_restricted"] = "Restreint aux accords d'audience ci-dessous.",
            ["pl.goal.projects_heading"] = "Projets de cet objectif",
            ["pl.goal.projects_empty"] = "Pas encore de projets sous cet objectif — crée-en un pour commencer à organiser le travail partagé.",
            ["pl.goal.empty_description"] = "Cet objectif n'a pas encore de description.",
            ["pl.project.new_heading"] = "Nouveau projet",
            ["pl.project.new_lede"] = "Un projet est un ensemble de travail partagé — avec une description optionnelle, un statut, des dates de début et d'échéance, un filtre de communauté et une audience. Il peut se rattacher à un objectif.",
            ["pl.project.create"] = "Créer le projet",
            ["pl.project.edit_heading"] = "Modifier le projet",
            ["pl.project.edit_lead"] = "Mets à jour le titre, la description, l'objectif, le statut et les dates de ce projet. Son audience, sa communauté et sa langue sont fixés à la création.",
            ["pl.project.save"] = "Enregistrer les modifications",
            ["pl.project.edit"] = "Modifier le projet",
            ["pl.project.title_hint"] = "Un court nom pour le projet — l'étiquette du flux.",
            ["pl.project.description_hint"] = "Une description optionnelle de ce dont il s'agit dans ce projet. Un projet fonctionne aussi sans description.",
            ["pl.project.status"] = "Statut",
            ["pl.project.status_hint"] = "Un libellé d'état optionnel — le même vocabulaire fixe que les tâches. Laisse vide pour aucun.",
            ["pl.project.start_date"] = "Début",
            ["pl.project.due_date"] = "Échéance",
            ["pl.project.dates_hint"] = "Les deux sont optionnelles — laisse vide pour aucune date. Affichées aux spectateurs dans leur propre fuseau horaire.",
            ["pl.project.audience_heading"] = "Audience — qui peut voir ce projet",
            ["pl.project.audience_public"] = "Visible par toute l'instance.",
            ["pl.project.audience_restricted"] = "Restreint aux accords d'audience ci-dessous.",
            ["pl.project.goal_heading"] = "Objectif",
            ["pl.project.goal_hint"] = "Un objectif optionnel sous lequel organiser ce projet. Laisse vide pour un projet autonome.",
            ["pl.project.goal_link"] = "Objectif",
            ["pl.project.associated_heading"] = "Tâches & tableaux de ce projet",
            ["pl.project.todos_heading"] = "Tâches de ce projet",
            ["pl.project.boards_heading"] = "Tableaux de ce projet",
            ["pl.project.todos_empty"] = "Pas encore de tâches dans ce projet.",
            ["pl.project.boards_empty"] = "Pas encore de tableaux dans ce projet.",
            ["pl.project.empty_description"] = "Ce projet n'a pas encore de description.",
            ["pl.goal.delete"] = "Supprimer l'objectif",
            ["pl.goal.delete_confirm"] = "Supprimer cet objectif ? Ses projets restent en place — le lien vers cet objectif n'apparaît tout simplement plus.",
            ["pl.project.delete"] = "Supprimer le projet",
            ["pl.project.delete_confirm"] = "Supprimer ce projet ? Ses tâches et tableaux restent en place — le lien vers ce projet n'apparaît tout simplement plus.",
            ["pl.todo.project_link"] = "Projet",
            ["pl.todo.set_project"] = "Définir le projet",
            ["pl.board.project_link"] = "Projet",
            ["pl.board.set_project"] = "Définir le projet",
            ["pl.board.add_to_project"] = "Ajouter au projet…",
            ["pl.board.project_hint"] = "Un lien de projet est une surface d'affichage — il rattache ce tableau au projet, il ne limite jamais qui peut le voir.",
        };

    /// <summary>
    /// The curated Danish (<c>da</c>) baseline. One entry per key in
    /// <see cref="AllKeys"/> — full registry parity, the ADR 0015 honesty
    /// invariant extended to this dictionary. Idioms per ADR 0042 D2: the
    /// familiar <c>dig</c> register held everywhere, sentence case, no trailing
    /// period on button labels, and every inlined data token
    /// (<c>yyyy-MM-dd HH:mm</c>, <c>§6.4</c>, <c>Allow</c>/<c>Deny</c>, the
    /// <c>rc.editor.*</c> glyph labels, the on-screen <c>Select all</c> label
    /// that the kw-l TagHelper cannot reach) preserved token-for-token with the
    /// <c>en</c> value. These are <b>initial values</b> — seeded once on a
    /// pristine DB (the seeder's create-if-missing baseline loop), then
    /// community-owned via the in-app editor (ADR 0021); an admin edit is never
    /// overwritten (ADR 0042 D1). The <c>da</c> catalog row is seeded
    /// <b>disabled</b> (ADR 0042 D4 / the Danish lane): the lane is about making
    /// Danish *available* at first boot, not about switching the default.
    /// </summary>
    public static IReadOnlyDictionary<string, string> DaValues { get; } =
        new Dictionary<string, string>
        {
            // ── nav (the shared top-nav, _Layout + _AccountNav) ─────────────
            ["nav.home"]          = "Forside",

            // ── common (shared action/field labels reused across resident-facing views) ──
            ["common.cancel"]   = "Annullér",
            ["common.save"]     = "Gem",
            ["common.title"]    = "Titel",
            ["common.body"]     = "Tekst",
            ["common.language"] = "Sprog",
            ["common.optional"] = "valgfrit",
            ["common.add"]      = "Tilføj",
            ["common.remove"]   = "Fjern",
            ["common.filter"]   = "Filtrér",
            ["common.name"]         = "Navn",
            ["common.display_name"] = "Vistnavn",
            ["common.email"]        = "E-mail",
            ["common.password"]     = "Adgangskode",
            ["common.filter_name"]  = "Filtrér efter navn…",
            ["account.confirm_password"] = "Bekræft adgangskode",
            ["groups.name_label"]     = "Gruppens navn",
            ["groups.desc_placeholder"] = "Hvad handler gruppen om? (synlig for alle, der kan nå denne side)",
            ["posts.report_reason_placeholder"] = "Tilføj evt. en årsag for en moderator…",
            ["tags.events_example"] = "f.eks. oprydning, socialt, have",
            ["tags.pages_example"]  = "f.eks. sanitet, budget, maple-street",
            ["admin.community_description"] = "Beskrivelse (valgfri)",
            ["admin.community_mandatory"]   = "Påkrævet",
            ["admin.add_community"]         = "Tilføj fællesskab",
            ["admin.roles_heading"]         = "Roller",
            ["admin.roles_independent_hint"] = "Uafhængige — en beboer kan frit kombinere roller (ADR 0030). Intet markeret = almindeligt Medlem.",
            ["admin.moderator_scope"]       = "Moderatørens område",
            ["admin.moderator_scope_hint"]  = "De fællesskaber, dette konto kan moderere. Kun meningsfuldt når Moderator-rollen er markeret — Core-lanet rydder scope-rækker, når Moderator-rollen er fra.",
            ["nav.announcements"] = "Meddelelser",
            ["nav.community"]     = "Fællesskab",
            ["nav.groups"]        = "Grupper",
            ["nav.pages"]         = "Sider",
            ["nav.tags"]          = "Tags",
            ["nav.directory"]     = "Kontaktliste",
            ["nav.sign_in"]       = "Log ind",
            ["nav.sign_up"]       = "Opret konto",
            ["nav.profile"]       = "Profil",
            ["nav.admin"]         = "Administration",
            ["nav.translations"]  = "Oversættelser",
            ["nav.sign_out"]      = "Log ud",
            ["nav.children"]      = "Børn",
            ["nav.my_drafts"]     = "Mine udkast",
            ["nav.account"]       = "Konto",
            ["nav.events"]        = "Arrangementer",

            // ── events (M4 — ADR 0054: the events nav entry + the Detail footer) ──
            ["events.created"]    = "Oprettet",
            ["events.edited"]     = "redigeret",

            // ── projects (M5 — ADR 0067: the to-do surface nav entry + labels) ──
            ["nav.projects"]                 = "Projekter",
            ["projects.todo.title"]          = "Opgaver",
            ["projects.todo.lede"]           = "Nabolagets fælles opgaver — tildel arbejde til en nabo, opdel det i delopgaver og læg det på et board.",
            ["projects.todo.new"]            = "Ny opgave",
            ["projects.todo.new_lead"]       = "Skriv en opgave, tildel den evt. til en nabo, og — hvis nødvendigt — opdel den i delopgaver eller læg den på et board. Som udgangspunkt kan alle se den; slå den fra i publikumsafsnittet, hvis du vil begrænse adgang.",
            ["projects.todo.title_hint"]     = "Et kort navn til opgaven — kortets label.",
            ["projects.todo.status"]         = "Status",
            ["projects.todo.status_assignee_hint"] = "Status er et af de faste opgavestatusser (Ingen, Ikke startet, I gang, Færdig, Annulleret). Tildeling af en opgave giver denne beboer håndtering af den — visning + håndtering, aldrig en adgangsbegrænsning.",
            ["projects.todo.assignee"]       = "Tildelt til",
            ["projects.todo.unassigned"]     = "Ikke tildelt",
            ["projects.todo.assign"]         = "Tildel",
            ["projects.todo.unassign"]       = "Fjern tildeling",
            ["projects.todo.assign_to"]      = "Tildel til…",
            ["projects.todo.assign_people"]  = "Personer",
            ["projects.todo.assign_groups"]  = "Grupper",
            ["projects.todo.assign_communities"] = "Fællesskaber",
            ["projects.todo.claim"]          = "Tag på dig",
            ["projects.todo.addressed_to"]   = "Rettet til",
            ["projects.todo.filter_unassigned"] = "Kun ikke tildelte",
            ["projects.todo.add_subtask"]    = "Tilføj delopgave",
            ["projects.todo.delete"]         = "Slet",
            ["projects.todo.parent"]         = "Forældreopgave",
            ["projects.todo.top_level"]      = "Tophangende opgave",
            ["projects.todo.parent_hint"]    = "Vælg en forældreopgave for at oprette denne opgave som en delopgave — en delopgave er en fuldstændig opgave med egen status, tildeling og boardplacering.",
            ["projects.todo.no_parent"]      = "Ingen forældreopgave (tophængende)",
            ["projects.todo.clear_parent"]   = "Fjern forældreopgave (gør top-hængende)",
            ["projects.todo.reparent_hint"]  = "En delopgave er en fuldstændig opgave med egen status, tildeling og boardplacering. Omhængning til en efterkommer nægtes (cyklusvagt).",
            // ADR 0087 — "venter på"-afhængigheden (chip, picker, feed-skift).
            ["todo.blocked_by"]              = "Venter på",
            ["todo.blocked_by_none"]         = "Ingen bloker",
            ["todo.blocked_generic"]         = "En anden opgave (ikke synlig for dig)",
            ["todo.blocked_filter"]          = "Venter på noget",
            // ADR 0079 — valgfri start-/forfaldsdato på en opgave.
            ["projects.todo.start"]          = "Start",
            ["projects.todo.due"]            = "Faldig",
            ["projects.todo.dates_hint"]     = "Begge er valgfri — lad tom for ingen dato. Vises for beskuer i deres egen tidszone.",
            ["projects.todo.created"]        = "Oprettet",
            ["projects.todo.modified"]       = "Ændret",
            ["projects.todo.no_body"]        = "Ingen tekst — denne opgave har kun et navn.",
            ["projects.todo.subtasks"]       = "Delopgaver",
            ["projects.todo.boards"]         = "Boards",
            ["projects.todo.back"]           = "← Tilbage til opgaverne",
            ["projects.todo.untitled"]       = "Opgave uden navn",
            ["projects.todo.edit"]           = "Rediger",
            ["projects.todo.edit_heading"]   = "Rediger opgave",
            ["projects.todo.edit_lead"]      = "Opdater detaljerne for denne opgave. Dit publikumsvalg er den eneste adgangsbegrænsning — fællesskabsvalget er kun en filter.",
            ["projects.todo.all_communities"] = "Alle fællesskaber",
            ["projects.todo.audience_heading"] = "Publikum — hvem der kan se denne opgave",
            ["projects.todo.audience_default"] = "Som udgangspunkt kan alle se denne opgave. Slå den fra kun, hvis du vil begrænse adgang.",
            ["projects.todo.save"]           = "Gem ændringer",
            ["projects.todo.create"]         = "Opret opgave",
            ["projects.todo.empty"]          = "Ingen opgaver endnu — opret en for at komme i gang med det fælles arbejde.",

            // ── projects (M5 — ADR 0067: brætfladen (U10) + kort-handlinger) ──
            ["projects.todo.copy_to"]        = "Kopiér til bræt",
            ["projects.todo.move_to"]        = "Flyt til bræt",
            ["projects.todo.move_up"]        = "Flyt op",
            ["projects.todo.move_down"]      = "Flyt ned",
            ["projects.todo.move_left"]      = "Flyt venstre",
            ["projects.todo.move_right"]     = "Flyt højre",
            ["projects.board.title"]         = "Brætter",
            ["projects.board.lede"]          = "Nabolagets fælles brætter — arranger opgaver i laner, flyt dem igennem statusser og hold arbejdet synligt.",
            ["projects.board.new"]           = "Nyt bræt",
            ["projects.board.new_lead"]      = "Opret et bræt for at arrangerer opgaver i laner. En lane kan bære en status (en opgave flyttet dertil overtarver den) og en valgfri grænse for antal kort. Som udgangspunkt kan alle se brættet; slå det fra i publikumsafsnittet, hvis du vil begrænse adgang.",
            ["projects.board.title_hint"]    = "Et kort navn til brættet — feed-labelen.",
            ["projects.board.edit"]          = "Redigér bræt",
            ["projects.board.edit_heading"]  = "Redigér bræt",
            ["projects.board.edit_lead"]     = "Opdater brættets navn og beskrivelse. Publikum, fællesskab og sprog er fastlagt ved oprettelsen.",
            ["projects.board.save"]          = "Gem ændringer",
            ["projects.board.description_hint"] = "En valgfri beskrivelse af, hvad brættet følger. Et bræt kan bruges kun med et navn.",
            ["projects.board.back"]          = "← Tilbage til brætter",
            ["projects.board.untitled"]      = "Bræt uden navn",
            ["projects.board.delete"]        = "Slet bræt",
            ["projects.board.create"]        = "Opret bræt",
            ["projects.board.empty"]         = "Ingen brætter endnu — opret et for at begynde at arrangere det fælles arbejde.",
            ["projects.board.no_lanes"]      = "Dette bræt har endnu ikke laner.",
            ["projects.board.lanes"]         = "Laner",
            ["projects.board.lanes_hint"]    = "Kolonner tværs over brættet — for eksempel »Planlagt / I gang / Færdig«. En lanes status anvendes, hvis den er angivet, på en opgave flyttet dertil; dens max-items-grænse begrænser antallet af kort.",
            ["projects.board.single_lane_hint"] = "Brættet starter med én lane — tilføj flere laner og statusser på bræt-siden efter oprettelsen.",
            ["projects.board.lane.title"]    = "Lane-titel",
            ["projects.board.lane.status"]   = "Status",
            ["projects.board.lane.max_items"] = "Max. elementer",
            ["projects.board.lane.order"]    = "Rækkefølge",
            ["projects.board.lane.empty"]    = "Ingen kort i denne lane.",
            ["projects.board.lane.save"]     = "Gem",
            ["projects.board.lane.rename"]   = "Omdøb",
            ["projects.board.lane.set_limit"] = "Sæt grænse",
            ["projects.board.lane.set_status"] = "Sæt status",
            ["projects.board.lane.move_left"] = "Flyt venstre",
            ["projects.board.lane.move_right"] = "Flyt højre",
            ["projects.board.lane.add_todo"] = "Tilføj to-do",
            ["projects.board.lane.add_lane"] = "Tilføj lane",
            ["projects.board.lane.add_todo_placeholder"] = "To-do titel",
            ["projects.board.lane.add_lane_placeholder"] = "Ny lane titel",
            ["projects.board.lane.first_title_placeholder"] = "Planlagt",
            ["projects.board.status.none"] = "Ingen",
            ["projects.board.status.not_started"] = "Ikke startet",
            ["projects.board.status.in_progress"] = "I gang",
            ["projects.board.status.done"] = "Færdig",
            ["projects.board.status.cancelled"] = "Annulleret",
            ["projects.board.fullscreen"]    = "Fuldskærm",
            ["projects.board.exit_fullscreen"] = "Afslut fuldskærm",
            ["projects.board.audience_heading"] = "Publikum — hvem der kan se dette bræt",
            ["projects.board.audience_default"] = "Som udgangspunkt kan alle se dette bræt. Slå det fra kun, hvis du vil begrænse adgang.",

            // ── guardian (the /me/children child-accounts surface) ─────────
            ["guardian.title"]        = "Dine børn",
            ["guardian.lead"]         = "De konti, du har oprettet til et barn, og de kontroller, du har over hver af dem.",
            ["guardian.empty"]        = "Ingen børn endnu.",
            ["guardian.add"]          = "Tilføj en barnkonto",
            ["guardian.manage_title"] = "Administer en barnkonto",

            // ── guardian (per-child surface, GU ADR 0028) ─────────────────
            ["guardian.back"]               = "Tilbage til dine børn",
            ["guardian.account_label"]       = "Konto",
            ["guardian.group_memberships"]   = "Gruppemedlemskaber",
            ["guardian.community_memberships"] = "Fællesskabsmedlemskaber",
            ["guardian.no_groups"]           = "Ingen gruppemedlemskaber.",
            ["guardian.no_communities"]      = "Ingen fællesskabsmedlemskaber.",
            ["guardian.group_id_label"]      = "Gruppe-ID",
            ["guardian.community_id_label"]  = "Fællesskab-ID",
            ["guardian.pending_invitations"] = "Afventende gruppeinvitationer",
            ["guardian.no_invitations"]      = "Ingen afventende invitationer.",
            ["guardian.approve"]             = "Godkend",
            ["guardian.handover"]            = "Overtag kontoen",
            ["guardian.handover_hint"]       =
                "Afløsning af værgemodet overgiver kontoen til barnet. " +
                "Medlemskaberne bevares, og barnets egne kontroller kommer " +
                "tilbage ved næste læsning.",
            ["guardian.dissolve"]            = "Afløs værgemodet",
            ["guardian.suspended"]           = "Suspendert",
            ["guardian.unsuspend"]           = "Genopret",
            ["guardian.suspend"]             = "Suspendér",
            ["guardian.display_name"]        = "Vistnavn",
            ["guardian.email"]               = "E-mailadresse",
            ["guardian.password"]            = "Adgangskode",
            ["guardian.child_email_hint"]    =
                "Barnet bekræfter sin egen e-mail for at logge ind — den sædvanlige tilmeldingsproces.",

            // ── posts (composer helper hints) ──────────────────────────────
            ["posts.title_hint"] =
                "En kort overskrift (op til 120 tegn). Slet for et " +
                "kun-tekstindlæg — listen viser i stedet din første tekstlinje.",
            ["posts.language_hint"] =
                "Det sprog, du skriver dette indlæg på. Det er kun et " +
                "mærke — det oversættes ikke — og det holder teksten let " +
                "fundbar senere og giver en læser mulighed for at tilføje " +
                "sin egen version.",

            // ── faq (drop-in FAQ section, _FaqAccordion) ──────────────────
            ["faq.title"]     = "Ofte stillede spørgsmål",
            ["faq.q1"]        = "Hvem kan se mine indlæg?",
            ["faq.a1"]        =
                "Den, du vælger, når du poster: kun dig, din gruppe, dit " +
                "fællesskab eller alle. Modtagerkreds er et " +
                "valg pr. indlæg — ikke en global indstilling.",
            ["faq.q2"]        = "Hvor ligger meddelelser som vandmangel og vejarbejde?",
            ["faq.a2_intro"]  = "Faste meddelelser på",
            ["faq.a2_link"]   = "/announcements",
            ["faq.a2_outro"]  =
                " — læsningen er åben, så ingen behøver at logge ind for at " +
                "finde ud af, hvornår gaden skal males.",
            ["faq.q3"]        = "Er dette privat som standard?",
            ["faq.a3"]        =
                "Ja. Hver gang, der adgang til begrænset indhold gives eller " +
                "nægtes, registreres det. Dataene bliver på én database " +
                "ejet af nabolagets vært, og der er ingen reklame eller " +
                "tracking indbygget.",

            // ── error (shared error page, Shared/Error.cshtml) ────────────
            ["error.title"]           = "Fejl.",
            ["error.subtitle"]        = "Der opstod en fejl under behandling af din anmodning.",
            ["error.development_title"] = "Udviklingsmodus",
            ["error.development_hint"] =
                "Skift til Udviklingsmiljøet viser flere detaljer om den " +
                "opståede fejl. Udviklingsmiljøet bør ikke aktiveres for " +
                "udrullede applikationer — det kan afsløre følsomme " +
                "oplysninger fra fejl til slutbrugere.",
            ["error.request_id"]      = "Anmodnings-ID:",

            // ── guardian assignment (GA ADR 0038) ───────────────────────────
            ["guardian.otherGuardians.title"] = "Andre værgemænd",
            ["guardian.otherGuardians.empty"] = "Ingen andre værgemænd tildelt.",
            ["guardian.assign.title"]        = "Tildel en værgemand",
            ["guardian.assign.email"]        = "E-mail-adresse på den værgemand, du ønsker at tildelte",
            ["guardian.assign.submit"]       = "Tildel",
            ["guardian.assign.noAccount"]    = "Ingen konto med den e-mail.",
            ["guardian.assign.self"]         = "Du er allerede dette barns værgemand.",
            ["guardian.assign.success"]      = "Værgemand tildelt.",

            // ── footer (the shared footer, _Layout) ─────────────────────────
            ["footer.tagline"]  =
                "Et privat hjem for ét nabolag — feeden, grupperne og de faste noter. " +
                "Det, der sker på din gade, bliver på din gade.",
            ["footer.copyright"] = "· selv-hostet af jeres fællesskab",
            ["footer.gtk_heading"] = "Godt at vide",
            ["footer.gtk_privacy"] =
                "Privat som udgangspunkt: hvert indlægs modtagerkreds vælges af dets forfatter, " +
                "og alt kan læses af dig — ikke af verden.",
            ["footer.gtk_oss"] =
                "Kumunita er open source — koden, beslutningerne, dokumentationen.",
            // SP U03 (ADR 0043 D4) — footer-søjlen "Platform": de fem
            // leverede platform-overflader (Om-view + de fire
            // Page-dokumenter), linket for enhver besøger.
            ["footer.platform.heading"] = "Platform",
            ["footer.platform.about"]   = "Om os",
            ["footer.platform.terms"]   = "Brugsbetingelser",
            ["footer.platform.help"]    = "Hjælp",
            ["footer.platform.privacy"] = "Privatliv",
            ["footer.platform.conduct"] = "Adfærdskodeks",
            // Søjlen "Projektet" (home / about / footer): overskriften + de tre
            // RepositoryInfo.Links-labels (udgivet via et dynamisk kw-l-nøgle fra
            // linklisten, løst op efter sprog).
            ["footer.project.heading"] = "Projektet",
            ["repo.source_code"]      = "Kildekode",
            ["repo.documentation"]    = "Dokumentation",
            ["repo.non_technical"]    = "For ikke-tekniske naboer",

            // ── settings (the language-picker labels) ───────────────────────
            ["settings.settings"]       = "Indstillinger",
            ["settings.choose_language"] = "Vælg dit sprog",

            // ── settings — account help ─────────────────────────────────────
            ["settings.help_heading"]     = "Hjælp til din konto",
            ["settings.help_lede"]        =
                "Stå du fast ved din konto — et kodeord, din adgang eller noget andet? " +
                "Denne guide tager dig igennem det.",

            // ── settings — timezone (ADR 0019) ──────────────────────────────
            ["settings.timezone_title"]        = "Tidszone",
            ["settings.timezone_lede"]         =
                "Vælg den tidszone, platformen viser dig. Dit valg gemmes på din konto — " +
                "det træder i kraft næste gang, du indlæser en side, og påvirker aldrig andre beboere.",
            ["settings.timezone_label"]        = "Din tidszone",
            ["settings.timezone_default_marker"] = "— platformstandard",
            ["settings.timezone_default_note"] = "Plattformens standard er ",
            ["settings.timezone_default_tail"] =
                ". Hvis du nulstiller dit valg, bruges platformstandarden.",
            ["settings.timezone_reset"]        = "Nulstil til platformstandard",
            ["settings.timezone_save"]         = "Gem",
            ["settings.timezone_flash_set"]    = "Tidszone indstillet til \"{0}\" — den træder i kraft ved næste anmodning.",
            ["settings.timezone_flash_reset"]  = "Tidszone nulstillet — platformstandarden bruges.",
            ["settings.timezone_unknown"]      = "Ukendt tidszone",

            // ── settings — date format (ADR 0020) ───────────────────────────
            ["settings.dateformat_title"]        = "Dato- og tidsformat",
            ["settings.dateformat_lede"]         =
                "Vælg, hvordan datoer og tidspunkter vises for dig. Dit valg gemmes på din konto — " +
                "det træder i kraft næste gang, du indlæser en side, og påvirker aldrig andre beboere.",
            ["settings.dateformat_label"]        = "Dit dato- og tidsformat",
            ["settings.dateformat_default_marker"] = "— platformstandard",
            ["settings.dateformat_default_note"] = "Plattformens standard er ",
            ["settings.dateformat_default_tail"] =
                ". Hvis du nulstiller dit valg, bruges platformstandarden.",
            ["settings.dateformat_custom_label"] = "Tilpasset format",
            ["settings.dateformat_custom_hint"]  =
                "En tilpasset .NET-dato-/tidsformatstreng (f.eks. yyyy-MM-dd HH:mm). Lad stå tom for at bruge et standardformat.",
            ["settings.dateformat_reset"]        = "Nulstil til platformstandard",
            ["settings.dateformat_save"]         = "Gem",
            ["settings.dateformat_flash_set"]    = "Dato- og tidsformat indstillet — det træder i kraft ved næste anmodning.",
            ["settings.dateformat_flash_reset"]  = "Dato- og tidsformat nulstillet — platformstandarden bruges.",

            // ── admin — the platform-default timezone ───────────────────────
            ["admin.timezone_title"]    = "Platformstandard: tidszone",
            ["admin.timezone_lede"]     =
                "Den tidszone, beboernes tidstempel falder tilbage til, " +
                "når de ikke har sat et personligt valg.",
            ["admin.timezone_label"]    = "Standard tidszone",
            ["admin.timezone_save"]     = "Gem",

            // ── admin — the platform-default date format (ADR 0020) ─────────
            ["admin.dateformat_title"]    = "Platformstandard: dato- og tidsformat",
            ["admin.dateformat_lede"]     =
                "Det dato- og tidsformat, beboernes tidstempel falder tilbage til, " +
                "når de ikke har sat et personligt valg.",
            ["admin.dateformat_label"]    = "Standard dato- og tidsformat",
            ["admin.dateformat_custom_label"] = "Tilpasset format",
            ["admin.dateformat_custom_hint"]  =
                "En tilpasset .NET-dato-/tidsformatstreng (f.eks. yyyy-MM-dd HH:mm). Lad stå tom for at bruge et standardformat.",
            ["admin.dateformat_save"]     = "Gem",

            // ── admin — the sign-up gate (ADR 0050) ─────────────────────────
            ["admin.signup_title"]    = "Tilmelding",
            ["admin.signup_lede"]     =
                "Om nye beboere må oprette en konto selv. " +
                "Lukker man porten, er tilmelding kun på invitation — eksisterende beboere er ikke berørt.",
            ["admin.signup_open"]     = "Åben — beboere kan tilmelde sig",
            ["admin.signup_invitation_only"] = "Kun på invitation — nye selvtjente-kontoer er låst",
            ["admin.signup_save"]     = "Gem",
            ["admin.signup_notify_title"]   = "Giv besked til adminer",
            ["admin.signup_notify_lede"]    = "Når en ny beboer tilmelder sig og når en beboer bekræfter sin konto, får GlobalAdmins en notifikation i indbakken og en e-mail, så vidt muligt. Slår du den fra, bliver den stum — selve konto-processen er ellers uforandret.",
            ["admin.signup_notify_on"]      = "Til — adminer får besked ved nye tilmeldinger og bekræftelser",
            ["admin.signup_notify_off"]     = "Fra — ingen besked til adminer ved tilmelding / bekræftelse",

            // ── account — sign-up-closed notice (ADR 0050) ─────────────────
            ["account.signup_closed_title"] = "Tilmeldingen er lukket",
            ["account.signup_closed_body"]  =
                "Tilmeldingen er på nuværende tidspunkt kun på invitation på denne instance. " +
                "Hvis du har fået invitation, vil en administrator oprette din konto og sende dig logind-linket.",

            // ── home (the hero + section lead) ──────────────────────────────
            ["home.eyebrow"] = "Hvor projektet står",
            ["home.lead"] =
                "Et privat hjem for ét nabolag — bygget skridt for skridt og i det åbne. " +
                "Alt, der er angivet nedenfor, er en del af den plan — og du er velkommen til at se, hvordan den bygges.",
            ["home.support"] = "Spørgsmål eller feedback? Skriv til",

            // ── home intro (hvad Kumunita er + de tre overflader) ──────────
            ["home.intro_eyebrow"]  = "Et privat hjem for ét nabolag",
            ["home.intro_lead"] =
                "Ét roligt sted til alt det, jeres gade laver — fæden, grupperne og " +
                "de meddelelser, der fortjener mere end en gruppechat. Privat, klart og jeres.",
            ["home.about_link"]    = "Hvad det er & hvordan det virker",
            ["home.feature_feed_title"]  = "Én fæde for gaden",
            ["home.feature_feed_body"] =
                "Indlæg og tråde fra jeres stræder, ét roligt sted — ingen algoritme, ingen støj.",
            ["home.feature_groups_title"]  = "Grupper der passer",
            ["home.feature_groups_body"] =
                "Havedeling, bogklub, naboovervågning — en gruppe til alt det nabolaget allerede laver.",
            ["home.feature_pinned_title"]  = "Fastgjort hvor det betyder noget",
            ["home.feature_pinned_body"] =
                "Vandafbrud, vejarbejde, de nye bakkedæmper — notater der bliver hængende i stedet for at forsvinde.",

            // ── home "Nyheder"-fæde (loggede ind brugere) ──────────────────
            ["home.feed_title"]          = "Det nyt i nabolaget",
            ["home.feed_posts"]          = "Indlæg",
            ["home.feed_announcements"]  = "Meddelelser",
            ["home.feed_pages"]          = "Sider",
            ["home.feed_view_all"]       = "Se alle",
            ["home.feed_empty"] =
                "Ingenting er indlagt endnu — vær den første, der starter samtalen i fæden.",
            ["home.feed_badge_pinned"]   = "Fastgjort",

            // ── home milepæler (planen, efter introen) ─────────────────────
            ["home.roadmap_heading"] = "Bygget i det åbne, milepæl for milepæl",

            // ── account (Login / Signup — titles + primary actions) ─────────
            ["account.login_title"]   = "Log ind",
            ["account.login_submit"]  = "Log ind",
            ["account.login_no_account"] = "Ingen konto endnu?",
            ["account.login_remember"] = "Husk mig",
            ["account.login_setup_hint"] = "Har du modtaget et setup-token til første start?",
            ["account.login_setup_link"] = "Fuldfør setup",
            ["account.login_signup"] = "Opret konto",
            ["account.signup_title"]  = "Opret konto",
            ["account.signup_submit"] = "Opret konto",
            ["account.signup_has_account"] = "Har du allerede en konto?",

            // ── posts (Index / New / Edit) ──────────────────────────────────
            ["posts.feed_all_sections"] = "Fællesskab",
            ["posts.write"]        = "Skriv et indlæg",
            ["posts.new_title"]    = "Skriv et indlæg",
            ["posts.new_intro"] =
                "Som udgangspunkt kan alle i det fællesskab, du vælger nedenfor, se " +
                "dit indlæg. Slå det fra i modtagerkreds-sektionen kun, hvis du " +
                "vil indskrænke, hvem der kan se det — specifikke personer eller grupper.",
            ["posts.community_hint"] =
                "Fællesskabet afgør, i hvilken feed dit indlæg vises — og, " +
                "som udgangspunkt, hvem der kan se det (alle medlemmer af fællesskabet). " +
                "Vil du indskrænke modtagerkredsen, så slår du \"Alle i dette fællesskab\" " +
                "fra i modtagerkreds-sektionen nedenfor.",
            ["posts.new_submit"]   = "Udgiv",
            ["posts.edit_title"]   = "Rediger indlæg",
            ["posts.edit_save"]    = "Gem ændringer",
            ["posts.save_as_draft"] =
                "Gem som udkast",
            ["posts.save_as_draft_hint"] =
                "Et udkast gemmes, men er usynligt for alle — også adminer — " +
                "indtil du udgiver det. Du finder det under \"Mine udkast\".",
            ["posts.draft_badge"]   = "Udkast",
            ["posts.draft_note"] =
                "Dette indlæg er et udkast — kun du kan se det. Udgiv det " +
                "for at gøre det synligt for dets modtagerkreds.",
            ["posts.publish"]       = "Udgiv",
            ["my_drafts.title"]     = "Mine udkast",
            ["my_drafts.empty"]     = "Du har ingen udkast.",
            ["posts.audience_all_members"] =
                "Alle i dette fællesskab",
            ["posts.audience_all_members_hint"] =
                "Udgangspunktet — alle medlemmer af fællesskabet ovenfor kan " +
                "se dette indlæg. Slå det fra kun, hvis du vil indskrænke, hvem der kan " +
                "se det.",
            ["posts.audience_all_members_hint_edit"] =
                "Når tændt, kan alle medlemmer af dette fællesskab se indlægget. " +
                "Slå det fra for at indskrænke modtagerkredsen til specifikke personer " +
                "eller grupper.",
            ["posts.audience_combine"] =
                "Sådan kombineres valgene",
            ["posts.audience_restrict_hint"] =
                "Disse valg er yderligere — \"Alle i dette fællesskab\" " +
                "bliver tændt, medmindre du slår det fra, så indlægget er synligt " +
                "for hele fællesskabet og de valg, du træffer her.",
            ["posts.audience_only_picks"] =
                "Det, du vælger her, bliver indlæggets modtagerkreds — der " +
                "tilføjes intet ovenfor eller nedenfor denne formular. Et tomt valg (med " +
                "\"Alle i dette fællesskab\" slukket) betyder, at kun du kan se " +
                "indlægget.",
            ["posts.empty_can_post"] =
                "Ingen indlæg her endnu. Skriv det første — det vises kun for den modtagerkreds, " +
                "du vælger i editoren under overskriften.",
            ["posts.empty"]        = "Ingen indlæg her endnu.",

            // ── groups (Index / Detail / New / PostDetail) ──────────────────
            ["groups.title"]          = "Grupper",
            ["groups.lead"]           = "De fællesskaber, du ejer eller tilhører.",
            ["groups.create"]         = "Opret en gruppe",
            ["groups.empty"]          = "Ingen grupper endnu.",
            ["groups.back_all"]       = "← Alle grupper",
            ["groups.tab.feed"]       = "Feed",
            ["groups.tab.members"]    = "Medlemmer",
            ["groups.tab.settings"]   = "Indstillinger",
            ["groups.posts_heading"]  = "Indlæg",
            ["groups.new_post"]       = "Nyt indlæg",
            ["groups.posts_empty_can"] =
                "Ingen indlæg endnu. Skriv det første — det vises for de nuværende medlemmer.",
            ["groups.posts_empty"]    = "Ingen indlæg her endnu.",
            ["groups.members_heading"]  = "Medlemmer",
            ["groups.members_empty"]    = "Ingen medlemmer endnu.",
            ["groups.member_leave"]     = "Forlad",
            ["groups.invite_heading"]   = "Indbyd en beboer",
            ["groups.about_heading"]        = "Om denne gruppe",
            ["groups.about_desc_label"]     = "Beskrivelse (valgfri)",
            ["groups.about_desc_clear_hint"] = "Slet for at fjerne beskrivelsen.",
            ["groups.about_desc_save"]      = "Gem beskrivelse",
            ["groups.privacy_heading"]      = "Fortrolighed",
            ["groups.private_label"]        = "Privat gruppe",
            ["groups.private_hint"]         =
                "En privat gruppe (f. eks. en familie) er skjult for alle andre; kun de mennesker, du tilføjer som medlemmer, kan se og bruge den. Fjern afkrydsningen for at gøre gruppen offentlig igen.",
            ["groups.new_title"]      = "Skriv til denne gruppe",
            ["groups.new_back"]       = "tilbage til gruppen",
            ["groups.new_submit"]     = "Skriv til gruppen",
            // ── groups list (the Airy layout — the invitation panel + the
            //    member-count word on each group card) ───────────────────────
            ["groups.invitations"]    = "Indladelser",
            ["groups.invitations_pending"] = "ventende",
            ["groups.invited_by"]     = "Inviteret af",
            ["groups.invite_accept"]  = "Acceptér",
            ["groups.invite_decline"] = "Afvis",
            ["groups.members_one"]    = "medlem",
            ["groups.members_many"]   = "medlemmer",
            // ── community feed (the Airy layout — the left rail + the
            //    mandatory-community note) ───────────────────────────────────
            ["community.browse"]          = "Fællesskaber",
            ["community.all"]             = "Alle",
            ["community.mandatory_badge"] =
                "Alle — obligatorisk",
            ["community.mandatory_note"]  =
                "Dette er kvarterets obligatoriske fællesskab — alle her er " +
                "en del af det; ingen kan fjernes eller forlade det.",

            // ── ADR 0026 — group name/description translations ───────────
            ["groups.translations_label"] = "Oversættelser",
            ["groups.translations_none"] = "Ingen endnu",
            ["groups.translation_add"] = "Tilføj",
            ["groups.translation_name_label"] = "Navn",
            ["groups.translation_desc_label"] = "Beskrivelse",
            ["groups.translation_optional"] = "valgfrit",
            ["groups.translation_min_one"] = "Mindst navnet eller beskrivelsen skal udfyldes.",
            ["groups.translation_save"] = "Gem oversættelse",

            // ── directory (page heading + lead) ─────────────────────────────
            ["directory.title"] = "Kontaktliste",
            ["directory.lead"]  = "Alle i nabolaget — alle beboere på platformen.",
            ["directory.empty"] = "Ingen beboere i dette nabolag endnu.",

            // ── profile (page heading + primary action) ─────────────────────
            ["profile.title"]      = "Din profil",
            ["profile.save_avatar"] = "Gem avatar",

            // ── profile (Edit page) ─────────────────────────────────────────
            ["profile.edit_lede"] =
                "Her bestemmer du, hvad andre beboere kan se om dig. " +
                "Det, du vælger her, er præcis det, kontaktlisten " +
                "viser — ingen overraskelser.",
            ["profile.avatar_heading"] = "Din avatar",
            ["profile.avatar_hint"] =
                "JPEG, PNG, WebP eller GIF · op til 5 MB. Gemning erstatter " +
                "avatar'en, der aktuelt vises i kontaktlisten.",
            ["profile.name_email_heading"] = "Dit navn + e-mail",
            ["profile.address_heading"] = "Din adresse + telefon (valgfrit)",
            ["profile.address_hint"] =
                "Vises kun i kontaktlisten og detaljen, hvis du også har slået " +
                "kontaktfeltet nedenfor til; lad stå tom for at holde din " +
                "gade privat for denne profil.",
            ["profile.phone_hint"] =
                "Vises kun i kontaktlistens detalje, hvis du har slået " +
                "kontaktfeltet nedenfor til; lad stå tom for at holde dit nummer " +
                "privat for denne profil.",
            ["profile.who_heading"] = "Hvem kan se hvad",
            ["profile.optin_contact"] = "Del mine kontaktoplysninger (adresse, e-mail, telefon)",
            ["profile.optin_contact_note"] =
                "Lad stå slukket, hvis du hellere vil holde din adresse, " +
                "din e-mail og din telefon fuldstændig skjult for " +
                "kontaktlisten. Når den er tændt, vælger du " +
                "nedenfor, hvem der kan se den.",
            ["profile.save"] = "Gem",
            ["profile.preview_link"] = "Forhåndsvisning — sådan ser jeg ud",

            // ── profile (Edit page — field labels + input placeholders) ──────
            ["profile.display_name_label"] = "Vistnavn",
            ["profile.address_label"] = "Adresse (gaden, du bor på)",
            ["profile.address_placeholder"] =
                "Gade — vises for naboer, når du slår til nedenfor",
            ["profile.phone_label"] = "Telefonnummer",
            ["profile.phone_placeholder"] =
                "Telefon — vises for naboer, når du slår til nedenfor",
            ["profile.audience_mode_any_word"] = "En som helst",
            ["profile.audience_mode_all_word"] = "Alle",

            // ── profile (Preview page) ───────────────────────────────────────
            ["profile.preview_back"] = "← Tilbage til editoren",
            ["profile.preview_title"] = "Forhåndsvisning — sådan ser jeg ud",
            ["profile.preview_avatar_note"] =
                "Din avatar, som den vises ved siden af dit navn i " +
                "kontaktlisten.",
            ["profile.preview_readonly_lead"] =
                "Dette er en skrivebeskyttet forhåndsvisning. Den viser, hvordan din profil " +
                "ser ud for ",
            ["profile.preview_readonly_tail"] =
                " i kontaktlisten. Den ændrer intet i din " +
                "gemte profil — for at foretage en ændring, ",
            ["profile.preview_edit_link"] = "rediger din profil",
            ["profile.preview_visible_badge"] = "Synlig.",
            ["profile.preview_visible_tail"] =
                "ville se dette kontaktfelt i kontaktlisten.",
            ["profile.preview_hidden_badge"] = "Kontakt skjult.",
            ["profile.preview_hidden_tail"] =
                "ville ikke se et kontaktfelt. Dit navn (og " +
                "verificeringstegnet, hvis du har et) vises stadig " +
                "i kontaktlisten — kun kontaktoplysningerne er skjult.",
            ["profile.contact_address"] = "Adresse",
            ["profile.contact_email"] = "E-mail",
            ["profile.contact_phone"] = "Telefon",
            ["profile.edit_profile_btn"] = "Rediger profil",

            // ── profile (the _AudienceEditor shared partial) ────────────────
            ["profile.audience_visibility"] = "Hvem kan se din profil",
            ["profile.audience_contact"] = "Hvem kan se dine kontaktoplysninger",
            ["profile.audience_off_note"] =
                "Dine kontaktoplysninger er i øjeblikket skjult for alle. " +
                "Vil du ændre det, så slå \"Del mine kontaktoplysninger\" til ovenfor.",
            ["profile.audience_match_mode"] = "Sammenligningstilstand",
            ["profile.audience_mode_any"] =
                "en person er tilladt, hvis de opfylder én af de personer/grupper, du har valgt",
            ["profile.audience_mode_all"] =
                "en person er kun tilladt, hvis de opfylder alle de personer/grupper, du har valgt",
            ["profile.audience_all_residents"] =
                "Alle på platformen (alle loggede beboere)",
            ["profile.audience_all_residents_hint"] =
                "Nye beboere kan se dette automatisk — du behøver ikke at redigere igen, når nogen slutter sig.",

            // ── posts (Detail page) ──────────────────────────────────────────
            ["posts.back_to"] = "tilbage til",
            ["posts.detail_edit"] = "Rediger",
            ["posts.edited"] = "redigeret",
            ["posts.detail_why"] =
                "Du kan se dette indlæg, fordi det er blevet delt med dig — " +
                "enten er du forfatteren, eller det er delagt med dig eller dine grupper.",
            ["posts.report_button"] = "Rapportér dette indlæg",
            ["posts.reply_report_button"] = "Rapportér dette svar",
            ["posts.report_reason_label"] = "Hvad er der galt?",
            ["posts.report_optional"] = "valgfrit",
            ["posts.report_note"] =
                "En rapportering er en indtagningshandling — den ændrer " +
                "ikke, hvad du kan se, og en moderator kan følge op.",
            ["posts.report_submit"] = "Rapportér",
            ["posts.replies_heading"] = "Svar",
            ["posts.replies_empty"] =
                "Ingen svar endnu. Hvis du kan se dette indlæg, kan du også svare på det.",
            ["posts.reply_heading_author"] = "Svar (du er forfatteren på dette indlæg)",
            ["posts.reply_heading"] = "Svar",
            ["posts.reply_label"] = "Svar",
            ["posts.reply_language_label"] = "Sprog",
            ["posts.reply_language_note"] =
                "Det sprog, du svarer på — en markering, ikke en " +
                "oversættelse.",
            ["posts.reply_audience_note"] =
                "Svar har ingen egen modtagerkreds — de er synlige under " +
                "dette indlægs ene modtagerkredsbeslutning (C-M3·1-reglen " +
                "\"svaret arver\"). Du svarer kun der, hvor " +
                "indlægget selv er synligt.",
            ["posts.reply_submit"] = "Svar",
            ["posts.reply_edit"] = "Rediger",
            ["posts.reply_save"] = "Gem",
            ["posts.reply_edited"] = "redigeret",

            // ── posts (Detail page) — author soft-delete (ADR 0024) ──
            ["posts.delete"] = "Slet",
            ["posts.reply_delete"] = "Slet",
            ["posts.deleted_placeholder"] =
                "Dette indlæg er slettet af forfatteren.",
            ["posts.reply_deleted_placeholder"] =
                "Dette svar er slettet af forfatteren.",

            // ── posts (Detail page) — user-added translations (ADR 0022) ──
            ["posts.translations_label"] = "Oversættelser",
            ["posts.translations_none"] = "ingen endnu",
            ["posts.translation_add"] = "Tilføj",
            ["posts.translation_title_label"] = "Overskrift",
            ["posts.translation_body_label"] = "Tekst",
            ["posts.translation_optional"] = "valgfrit",
            ["posts.translation_save"] = "Gem oversættelse",
            ["posts.translation_edit"] = "Rediger",
            ["posts.translation_remove"] = "Fjern",

            // ── pages (the PG lane — tree browse + post view, ADR 0039) ──────
            ["pages.title"]       = "Sider",
            ["pages.new_button"]  = "Ny side",
            ["pages.none"] =
                "Ingen sider endnu. Global adminer kan oprette den første systemside " +
                "— en Om-siden er et almindeligt udgangspunkt — og " +
                "enhver beboer kan starte deres egen blog (en side af deres eget).",
            ["pages.back"]        = "← Tilbage til siderne",
            ["pages.by"]          = "af",
            ["pages.delete"]      = "Slet",
            ["pages.resetSeeded"] = "Nulstil til seedet tekst",
            ["pages.untitled"]    = "Side uden titel",

            // ── blog (per-resident page feed, ADR 0040) ─────────────────────
            ["blog.new_page"]     = "Ny blogside",
            ["blog.empty_own"] =
                "Du har ingen blogsider endnu. Opret den første — den bliver " +
                "roden af din blog, og du kan stable flere under den.",
            ["blog.empty_other"]  = "Denne beboer har ingen blogsider endnu.",
            ["blog.draft"]        = "Udkast",

            // ── groups (Create page) ─────────────────────────────────────────
            ["groups.create_back"] = "← Tilbage til grupperne",
            ["groups.create_title"] = "Opret en gruppe",
            ["groups.create_lede"] =
                "Et navn til et fællesskab af beboere (f.eks. \"Bygge 4\", " +
                "\"Frivillige\", \"Cyklejere\"). " +
                "Du ejer gruppen — du kan tilføje og fjerne medlemmer fra " +
                "gruppens detailside.",
            ["groups.create_desc_hint"] = "Valgfrit — en kort note, andre beboere vil se.",
            ["groups.create_private_hint"] =
                "En privat gruppe (f.eks. en familie) er usynlig for alle andre — " +
                "kun de personer, du tilføjer som medlemmer, kan se og bruge den. " +
                "En offentlig gruppe (f.eks. \"Svampesamlere\") vises som et valg " +
                "i andre beboeres valgmuligheder.",
            ["groups.create_submit"] = "Opret gruppe",

            // ── groups (Edit page) ───────────────────────────────────────────
            ["groups.edit_back"] = "tilbage til indlægget",
            ["groups.edit_title"] = "Rediger dit indlæg",
            ["groups.edit_lede"] =
                "Du redigerer dit eget indlæg. Kun du kan redigere det — " +
                "gruppemedlemskabet afgør, hvem der kan se det, men kun " +
                "forfatteren kan ændre det. Den gruppe, indlægget vises i, " +
                "er fast; kun overskrift, tekst og sprog nedenfor kan redigeres.",
            ["groups.edit_title_label"] = "Overskrift",
            ["groups.edit_title_hint"] =
                "En kort overskrift (≤ 120 tegn). Lad stå tom for et " +
                "kun-tekst-indlæg — listen viser i stedet " +
                "din første linje af teksten.",
            ["groups.edit_body_label"] = "Tekst",
            ["groups.edit_language_label"] = "Sprog",
            ["groups.edit_language_hint"] =
                "Det sprog, du skriver dette indlæg på. Det er kun en " +
                "markering — det oversættes ikke — og holder teksten " +
                "findbar senere og giver en læser mulighed for at tilføje " +
                "deres egen sprogversion, hvis de ønsker det.",
            ["groups.edit_submit"] = "Gem ændringer",
            ["groups.edit_cancel"] = "Annuller",

            // ── directory (Detail page) ──────────────────────────────────────
            ["directory.detail_back"] = "← Tilbage til kontaktlisten",
            ["directory.detail_verified"] = "Verifieret",
            ["directory.detail_contact_address"] = "Adresse",
            ["directory.detail_contact_email"] = "E-mail",
            ["directory.detail_contact_phone"] = "Telefon",
            ["directory.detail_no_contact"] =
                "Denne beboer har (endnu) ikke delt en kontaktmulighed med dig. Du " +
                "kan stadig se deres profilside.",

            // ── community (Manage page) ──────────────────────────────────────
            ["community.manage_back"] = "← Tilbage til feeden",
            ["community.manage_lede"] = "Administer medlemskabet i dette fællesskab.",
            ["community.manage_moderate"] = "Du modererer dette fællesskab",
            ["community.manage_disabled"] = "Slukket",
            ["community.manage_availability"] = "Tilgængelighed",
            ["community.manage_mandatory_label"] =
                "Påkrevende fællesskab — alle i nabolaget er medlemmer",
            ["community.manage_mandatory_hint"] =
                "I et påkrevende fællesskab kan der ikke fjernes medlemmer, og man kan ikke forlade det; " +
                "fjern afkrydsningsfeltet for at gøre " +
                "medlemskabet valgfrit igen.",
            ["community.manage_make_optional"] = "Gør valgfri",
            ["community.manage_make_mandatory"] = "Gør påkrævende",
            ["community.manage_members"] = "Medlemmer",
            ["community.manage_mandatory_note"] =
                "Dette fællesskab er påkrevende — alle er medlemmer, så der " +
                "er ingen at fjerne. De listede rækker er " +
                "eksplicitte medlemskaber, der beholdes, i tilfælde af at " +
                "fællesskabet igen gøres valgfrit.",
            ["community.manage_no_members"] = "Ingen eksplicitte medlemmer endnu — tilføj nogle nedenfor.",
            ["community.manage_you"] = "Dig",
            ["community.manage_remove"] = "Fjern",
            ["community.manage_add_member"] = "Tilføj et medlem",
            ["community.manage_all_members"] =
                "Alle i nabolaget er allerede medlemmer her.",
            ["community.manage_pick_resident"] = "Vælg en beboer at tilføje…",

            // ── ADR 0026 — community name/description translations ────────
            ["community.translations_label"] = "Oversættelser",
            ["community.translations_none"] = "Ingen endnu",
            ["community.translations_page_title"] = "Oversættelser",
            ["community.translations_page_lede"] = "Navn og beskrivelse af dette fællesskab på andre sprog.",
            ["community.translations_view_only"] = "Du kan se oversættelserne; kun en GlobalAdmin eller en Oversætter kan tilføje, redigere eller fjerne dem.",
            ["community.translation_add"] = "Tilføj",
            ["community.translation_name_label"] = "Navn",
            ["community.translation_desc_label"] = "Beskrivelse",
            ["community.translation_optional"] = "valgfrit",
            ["community.translation_min_one"] = "Mindst navnet eller beskrivelsen skal udfyldes.",
            ["community.translation_save"] = "Gem oversættelse",

            // ── community feed buttons (Posts/Index.cshtml) ─────────────
            ["community.feed_manage_members"] = "Administrer medlemmer",
            ["community.feed_translations"] = "Oversættelser",

            // ── moderation (Index page) ──────────────────────────────────────
            ["moderation.title"] = "Moderation",
            ["moderation.empty"] = "Ingen rapporteringer endnu. Køen er tom.",
            ["moderation.th_status"] = "Status",
            ["moderation.th_post"] = "Indlæg",
            ["moderation.th_component"] = "Komponent",
            ["moderation.th_reporter"] = "Rapportør",
            ["moderation.th_filed"] = "Indgivet",
            ["moderation.th_action"] = "Handling",
            ["moderation.review"] = "Gennemgå →",

            // ── moderation (Resolve page) ────────────────────────────────────
            ["moderation.resolve_title"] = "Moderation — Gennemgå rapport",
            ["moderation.details"] = "Rapportens detaljer",
            ["moderation.th_post_label"] = "Indlæg",
            ["moderation.th_component_label"] = "Komponent",
            ["moderation.th_reporter_label"] = "Rapportør",
            ["moderation.th_author_label"] = "Forfatter af indlægget",
            ["moderation.th_filed_label"] = "Indgivet",
            ["moderation.th_reason_label"] = "Årsag",
            ["moderation.no_reason"] = "(ingen årsag angivet)",
            ["moderation.th_body_label"] = "Indlægstekst",
            ["moderation.body_preview"] = "indlæg-forhåndsvisning",
            ["moderation.assign_header"] = "Tildel en moderator",
            ["moderation.assign_label"] =
                "En moderator, der dækker dette fællesskab",
            ["moderation.assign_pick"] = "Vælg en moderator …",
            ["moderation.assign_submit"] = "Tildel",
            ["moderation.cancel"] = "Annuller",
            ["moderation.unlock_submit"] = "Lås op",
            ["moderation.resolve_header"] = "Løs (luk denne rapport)",
            ["moderation.resolve_submit"] = "Løs",
            ["moderation.back_to_queue"] = "← Tilbage til køen",

            // ── reply-report-target lane (ADR 0023) ─────────────────────────
            ["moderation.queue_reply_by"] = "svar af",
            ["moderation.resolve_reply_label"] = "Svar (mål for denne rapport)",
            ["moderation.resolve_reply_by"] = "Svar af",

            // ── account (Verify / Resend / AccessDenied) ─────────────────────
            ["account.verify_title"] = "Bekræft din konto",
            ["account.verify_pending"] =
                "Vi bekræfter din konto — du logger ind om et øjeblik.",
            ["account.verify_again"] = "Tilmeld igen",
            ["account.resend_title"] = "Send bekræftelsesmail igen",
            ["account.resend_lede"] =
                "Indtast den e-mail, du tilmeldte dig med, og vi sender et nyt bekræftelseslink.",
            ["account.resend_submit"] = "Send igen",
            ["account.resend_create"] = "Opretter bare din konto?",
            ["account.resend_signup"] = "Opret konto",
            ["account.denied_title"] = "Adgang nægtet",
            ["account.denied_lede"] = "Du har ikke tilladelse til at se denne side.",
            ["account.denied_home"] = "Tilbage til forsiden",

            // ── admin (Audit page) ───────────────────────────────────────────
            ["admin.audit_title"] = "Adgangsaudit",
            ["admin.audit_lede"] =
                "Et log over, hvem der har fået eller nægtet adgang til begrænset indhold, " +
                "plus admin-handlinger. Loggen gemmes altid og ryddes periodisk " +
                "efter instansens opbevaringspolitik.",
            ["admin.audit_filter"] = "Filtrér",
            ["admin.audit_th_at"] = "Tidspunkt (UTC)",
            ["admin.audit_th_actor"] = "Aktør",
            ["admin.audit_th_effective"] = "Virkende",
            ["admin.audit_th_action"] = "Handling",
            ["admin.audit_th_target"] = "Mål",
            ["admin.audit_th_aggregate"] = "Aggregate",
            ["admin.audit_th_via"] = "Via",
            ["admin.audit_th_outcome"] = "Resultat",

            // ── admin (Break-glass page) ─────────────────────────────────────
            ["admin.breakglass_title"] = "Break-glass",
            ["admin.breakglass_granted"] = "Givet (UTC)",
            ["admin.breakglass_expires"] = "Udløber (UTC)",
            ["admin.breakglass_status"] = "Status",
            ["admin.breakglass_consumed"] = "aktiveret — forhøjelse aktiv indtil udløb",
            ["admin.breakglass_presented"] = "opsat, men ikke endnu aktiveret",
            ["admin.breakglass_token_label"] = "Engangskode (fra operatøren)",
            ["admin.breakglass_token_hint"] =
                "Brug af denne kode er en engangshandling. Den aktiverer forhøjelsen " +
                "indtil dens udløb.",
            ["admin.breakglass_consume"] = "Aktivér",

            // ── locale (settings + public picker) ────────────────────────────
            ["locale.settings_title"] = "Dine indstillinger",
            ["locale.language_heading"] = "Sprog",
            ["locale.lede"] =
                "Vælg det sprog, platformen viser dig. Dit valg gemmes i en " +
                "browsercookie — det træder i kraft ved næste anmodning, og " +
                "påvirker aldrig andre beboere.",
            ["locale.preferred_label"] = "Foretrukket sprog",
            ["locale.default_note"] =
                "Instansstandarden er ",
            ["locale.default_note_tail"] =
                ". Hvis dit foretrukne sprog senere fjernes af adminen, " +
                "falder platformen stille tilbage til instansstandarden.",
            ["locale.save"] = "Gem",
            ["locale.reset"] = "Nulstil til instansstandard",
            ["locale.public_title"] = "Vælg dit sprog",
            ["locale.instance_default"] = "— instansstandard",
            // ADR 0046 — the browser-match suggestion (pre-selection + marker).
            ["locale.browser_matched"] = "— fra dine browserindstillinger",
            ["locale.browser_note"] =
                "Vi har valgt ",
            ["locale.browser_note_tail"] =
                " ud fra dine browserindstillinger. Gemmer du det, bliver det " +
                "dit foretrukne sprog — det bliver ved, indtil du ændrer det.",
            // Flash-meddelelser (toast-overflade — LocaleController.Save /
            // PublicLocaleController.Save; {0} = sprogkode).
            ["locale.flash_set"] =
                "Sprog indstillet til \"{0}\" — det træder i kraft ved næste anmodning.",
            ["locale.flash_reset"] =
                "Sprogindstilling nulstillet — instansstandarden bruges.",

            // ── announcements (shared labels + New/Edit compose) ─────────────
            ["announcements.scope_label"] = "Hvem ser dette?",
            ["announcements.scope_public"] =
                "Alle (offentligt) — synligt for besøgende og beboere",
            ["announcements.scope_resident"] =
                "Beboere — kun synligt, når du er logget ind",
            ["announcements.community_label"] = "Send til et specifikt fællesskab (valgfrit)",
            ["announcements.all_residents"] = "Alle beboere",
            ["announcements.community_hint"] =
                "Lad stå på \"Alle beboere\" for at sende til alle, eller vælg et fællesskab for at begrænse, hvem der ser det.",
            ["announcements.title_label"] = "Overskrift",
            ["announcements.title_hint"] = "En kort overskrift (op til 120 tegn).",
            ["announcements.body_label"] = "Tekst",
            ["announcements.pin_label"] = "Fastgør øverst på alle sider",
            ["announcements.cancel"] = "Annuller",
            ["announcements.new_title"] = "Ny meddelelse",
            ["announcements.new_lede"] =
                "Meddelelser er bekendtgørelser, der vises for sig selv, " +
                "adskilt fra fællesskabsfeeden. En offentlig " +
                "meddelelse er synlig for alle, også personer, der " +
                "ikke er logget ind (f.eks. et vedligeholdelsesvindue). En " +
                "beboermeddelelse er kun synlig for loggede " +
                "beboere (f.eks. en \"hjælp os med X\"-opfordring).",
            ["announcements.new_scope_hint"] =
                "Offentlige meddelelser er synlige for alle, også " +
                "besøgende, der ikke er logget ind (f.eks. et vedligeholdelsesvindue " +
                "eller en nedbrudsmeddelelse). Beboermeddelelser " +
                "er kun synlige for loggede beboere.",
            ["announcements.new_submit"] = "Opret meddelelse",
            ["announcements.edit_title"] = "Rediger meddelelse",
            ["announcements.edit_lede"] =
                "Opdater denne meddelelses overskrift, tekst og synlighed. En " +
                "offentlig meddelelse er synlig for alle, også " +
                "personer, der ikke er logget ind (f.eks. et vedligeholdelsesvindue). En " +
                "beboermeddelelse er kun synlig for loggede " +
                "beboere (f.eks. en \"hjælp os med X\"-opfordring).",
            ["announcements.edit_scope_hint"] =
                "En ændring af, hvem der ser dette, gælder straks for den næste læser.",
            ["announcements.edit_submit"] = "Gem ændringer",
            ["announcements.pin_hint"] =
                "En fastgjort meddelelse vises også som en banner " +
                "helt øverst på hver side (inklusive forsiden), " +
                "udover den sædvanlige meddelelsesliste. Dens synlighed " +
                "følgere stadig den modtagerkreds, du valgte ovenfor: en offentlig " +
                "fastgørelse vises for alle besøgende; en beboerfastgørelse kun, når en bruger " +
                "er logget ind. Hvis flere er fastgjort, " +
                "vinder den senest fastgjorte.",
            ["announcements.language_note"] =
                "Det sprog, du skriver denne meddelelse på. Det er kun en " +
                "markering — det oversættes ikke — og holder teksten findbar senere " +
                "og giver en læser mulighed for at tilføje deres egen sprogversion, hvis de ønsker det.",

            // ── announcements (Index + Detail + pinned banner) ───────────────
            ["announcements.index_title"] = "Meddelelser",
            ["announcements.index_lede"] =
                "Platformmeddelelser: de offentlige er synlige for alle (f.eks. " +
                "planlagt vedligeholdelse); de kun for beboere er synlige for alle " +
                "loggede brugere (f.eks. \"hjælp os med X\"-opfordringer).",
            ["announcements.all"] = "Alle meddelelser",
            ["announcements.new_button"] = "Ny meddelelse",
            ["announcements.empty"] = "Ingen meddelelser endnu.",
            ["announcements.read_more"] = "Læs mere…",
            ["announcements.scope_everyone"] = "alle",
            ["announcements.scope_residents"] = "beboere",
            ["announcements.pinned_badge"] = "fastgjort",
            ["announcements.edit_button"] = "Rediger",
            ["announcements.delete"] = "Slet",
            ["announcements.detail_back"] = "← Tilbage til meddelelserne",
            ["announcements.detail_untitled"] = "Meddelelse uden overskrift",
            ["announcements.by"] = "af",
            ["announcements.edited"] = "redigeret",
            ["announcements.banner_read_more"] = "Læs mere",
            ["announcements.banner_all"] = "Alle meddelelser",

            // ── static pages (Page — the terms/help shell) ───────────────────
            ["static.last_updated"] = "Sidst opdateret:",

            // ── shared (the _GrantPickers partial — static markup only) ─────
            ["grant.heading"] = "Hvem du giver adgang til",
            ["grant.hint"] =
                "Afkryds én eller flere — eller brug \"Select all\"-rækken " +
                "over hver liste som en genvej.",
            ["grant.empty_users"] =
                "Du er den eneste verificerede beboer, så der er ingen andre " +
                "at give adgang til endnu.",
            ["grant.empty_groups"] =
                "Der findes ingen grupper på platformen endnu — opret én " +
                "under \"Grupper\" for at tilføje gruppebaseret synlighed.",

            // ── admin (page heading + primary action) ───────────────────────
            ["admin.title"]  = "Administration",
            ["admin.verify"] = "Verificér",

            // ── rich editor (the RE toolbar button labels, ADR 0031) ────────
            ["rc.editor.bold"]    = "B",
            ["rc.editor.italic"]  = "I",
            ["rc.editor.code"]    = "C",
            ["rc.editor.h1"]      = "H1",
            ["rc.editor.h2"]      = "H2",
            ["rc.editor.h3"]      = "H3",
            ["rc.editor.list"]    = "•",
            ["rc.editor.olist"]   = "1.",
            ["rc.editor.link"]    = "Link",
            ["rc.editor.image"]   = "Billede",
            ["rc.editor.attach"]  = "Vedhæft fil",
            ["rc.editor.source"]      = "</>",
            ["rc.editor.showPreview"] = "Forhåndsvisning",

            // ── about (the About product surface, ADR 0042 D5) ──────────────
            ["about.eyebrow"]             = "Privat som udgangspunkt",
            ["about.lead"] =
                "Ét hjem til alt, hvad dit nabolag gør — " +
                "feeden, grupperne og de noter, der fortjener bedre " +
                "end en gruppechat. Privat, i almindeligt sprog, og jeres.",
            ["about.cta_feed"]            = "Se feeden",
            ["about.cta_notes"]           = "Læs de faste noter",
            ["about.features.one.title"]  = "Ét feed til gaden",
            ["about.features.one.body"] =
                "Indlæg og tråde fra jeres blokke og gader, ét " +
                "roligt sted — ingen algoritme, ingen støj.",
            ["about.features.groups.title"]  = "Grupper, der passer",
            ["about.features.groups.body"] =
                "Have-udveksling, bogklub, gadevagt — en gruppe til alt, " +
                "hvad nabolaget allerede gør.",
            ["about.features.pinned.title"]  = "Fastgjort, hvor det betyder noget",
            ["about.features.pinned.body"] =
                "Vandskæringer, vejarbejder, de nye " +
                "hastighedsdæmpere — noter, der bliver ved " +
                "i stedet for at rulle væk.",
            ["about.project.eyebrow"]  = "Open source",
            ["about.project.heading"]  = "Koden, beslutningerne, design-dokumentationen",
            ["about.project.lead"] =
                "Er du nysgerrig på, hvordan det fungerer — eller om du " +
                "er ved at hoste det til dit nabolag — så er alt offentligt.",

            // ── tags (the TG lane, ADR 0044 — browse + composer affordances) ──
            ["tags.list.heading"]       = "Tags",
            ["tags.list.lede"] =
                "Emner, jeres indlæg og blogsider er tagget med — klik på ét, " +
                "for at se, hvad der er skrevet om det.",
            ["tags.list.empty"] =
                "Ingen tags endnu — tags dukker op her, når en beboer knytter " +
                "ét til et indlæg eller en blogside.",
            ["tags.bytag.heading"]      = "Indlæg og sider om",
            ["tags.bytag.posts_heading"] = "Indlæg",
            ["tags.bytag.pages_heading"] = "Blogsider",
            ["tags.bytag.empty"] =
                "Ingen læsbare indlæg eller blogsider bærer dette tag.",
            ["tag.input.placeholder"] = "f.eks. renhold, budget, maplestreet",
            ["tag.input.hint"] =
                "Skriv for at søge i eksisterende tags, eller start et nyt — " +
                "det knyttes til dette indlæg.",
            ["tag.suggest.empty"] =
                "Ingen matchende tags — fortsæt med at skrive eller start et nyt.",
            ["tag.translate.heading"] = "Oversættelser",
            ["tag.translate.save"] = "Gem",
            ["tag.translate.disabled"] = "Kun tags' opretter eller en GlobalAdmin kan omformulere den.",

            // ── platform (scope + the FIG philosophy, home/about) ───────────
            ["platform.scope_home"] =
                "Kumunita startede som et hjem til ét nabolag — men den er til lige så god til en klub, et hold eller de folk bag et stort arrangement. Nabolaget er standarden, ikke grænsen.",
            ["platform.fig_home"] =
                "Den er bygget på Fractal Integration Guidelines (FIG): ideen om, at en gruppes virkelige værdi bor i båndene mellem dens mennesker og dele, ikke i delene selv. Kumunita er softwaren, der bygger og holder disse bånd.",
            ["platform.scope_heading"] = "Bygget til et nabolag — hjemme overalt",
            ["platform.scope_body1"] =
                "Kumunita blev oprindeligt bygget til én gade: et privat, fælles hjem til de mennesker, der bor der. Men den kerneide er slet ikke bundet til en gade. Det er et privat hjem for én gruppe mennesker, der vil koordinere, dele og bygge tillid sammen — et nabolag, en klub, et hold, et arbejdssted, en forening eller endda de folk bag ét enkelt arrangement. Det, der ændrer sig, er kun navnet og detaljerne.",
            ["platform.scope_body2"] =
                "Alt det, der får det til at passe til en gade — den rodlige feed, grupperne, de målrettede indlæg, moderationen og dens audit-log, den flersprogede grænseflade — virker præcis ligeledes for alle dem. Så tilpasningen er et spørgsmål om konfiguration: fællesskabets navn, de komponenter, der træder i stedet for 'Sikkerhed' og 'Socialt', de grupper, der passer til jeres verden. Ikke et nyt design.",
            ["platform.fig_heading"] = "Ide bag det: Fractal Integration Guidelines",
            ["platform.fig_body1"] =
                "Kumunita er bygget på et lille sæt åbne retningslinjer, vi kalder Fractal Integration Guidelines (FIG). Deres centrale påstand er, at et integreret system er mere end summen af sine dele, og at dets kvalitet er kvaliteten af sammenhængene mellem disse dele — en egenskab, ingen enkelt del har for sig alene. En klump funktioner, der aldrig forbinder, er bare støj; værdien bor i sammenhængene.",
            ["platform.fig_body2"] =
                "Et nabolag er netop et sådant system: mange forskellige mennesker, husstande og bekymringer, hvis sammenhæng — hvem kender hvem, hvem man kan stole på, hvordan et problem faktisk løses tværs over mennesker — skaber et fællesskab. Ingen enkelt person er et nabolag. Kumunita er softwaren, der bygger og holder den sammenhæng.",
            ["platform.fig_body3"] =
                "Så udvikler vi Kumunita efter den samme regel, vi stiller til den: kvalitet er sammenhængenes kvalitet, ikke funktionernes antal. Retningslinjerne gentager sig i alle skalaer — en modul, en funktion, et fællesskab, de mennesker, der bygger det — derfor følger hele projektet dem, og derfor er filosofien dokumenteret i det åbne.",
            ["platform.scope_eyebrow"] = "Til enhver gruppe",
            ["platform.fig_eyebrow"] = "Filosofien",

            // ── events (M4 — ADR 0054: /Events-området; index, detaljer, composer) ──
            ["events.title"] = "Arrangementer",
            ["events.lede"] =
                "Kommende arrangementer i kvarteret — se, hvad der sker, og tilmeld dig.",
            ["events.new_event"] = "Nyt arrangement",
            ["events.new_lead"] =
                "Del et kommende arrangement med kvarteret. Som standard kan alle se det; " +
                "slå det fra i sektionen « Publikum » kun, hvis du vil begrænse, hvem der kan se det.",
            ["events.edit_event"] = "Rediger arrangement",
            ["events.edit_lead"] =
                "Opdater dette arrangements detaljer. Dine valg af publikum er den eneste adgangsgrænse — " +
                "fællesskabsvalget er kun et filter.",
            ["events.title_hint"] = "En kort overskrift til arrangementet.",
            ["events.start"] = "Start",
            ["events.end"] = "Slut",
            ["events.time_hint"] =
                "Den tid, arrangementet foregår. Seere ser det i deres egen tidszone.",
            ["events.location"] = "Sted",
            ["events.capacity"] = "Kapacitet",
            ["events.color"] = "Farve",
            ["events.location_hint"] =
                "Sted, kapacitet og farve er kun visningsdetaljer — de begrænser ikke, hvem der kan tilmelde sig.",
            ["events.all_communities"] = "Alle fællesskaber",
            ["events.community_hint"] =
                "Det fællesskab, dette arrangement optræder under — et filter, ikke en adgangsgrænse.",
            ["events.tags_placeholder"] = "fx oprydning, socialt, have",
            ["events.audience_heading"] = "Publikum — hvem der kan se dette arrangement",
            ["events.audience_default"] =
                "Standard — alle kan se dette arrangement. Slå det kun fra, hvis du vil begrænse, hvem der kan se det.",
            ["events.audience_mode_any"] = "en tilskuer matcher, hvis de er på et af valgmulighederne",
            ["events.audience_mode_all"] =
                "en tilskuer skal være på hver valgmulighed — en tom liste nægter alle",
            ["events.reminder"] = "Send påmindelsen 24 timer før til dem, der svarer \"Going\"",
            ["events.reminder_hint"] =
                "En påmindelse sendes dagen før arrangementet til alle, der er med. Slå den fra for at springe den over.",
            ["events.create"] = "Opret arrangement",
            ["events.save_changes"] = "Gem ændringer",
            ["events.empty"] = "Ingen kommende arrangementer endnu.",
            ["events.back"] = "← Tilbage til arrangementer",
            ["events.draft"] = "Udkast",
            ["events.draft_title"] = "Kun du kan se dette — det er ikke endnu offentligt",
            ["events.publish"] = "Udgiv",
            ["events.edit"] = "Rediger",
            ["events.delete"] = "Slet",
            ["events.delete_confirm"] = "Slet dette arrangement? Det kan ikke fortrydes.",
            ["events.untitled"] = "Arrangement uden titel",
            ["events.rsvp"] = "Deltagelse",
            ["events.rsvp_you"] = "Du svarer:",
            ["events.rsvp_responses"] = "Svar",
            ["events.rsvp_update"] = "Opdater",
            ["events.remove_translation_confirm"] = "Fjern denne oversættelse?",

            // ── events.mine (sektionen „Dine kommende arrangementer“ på /events — ADR 0065) ──
            ["events.mine.title"] = "Dine kommende arrangementer",
            ["events.mine.hint"] = "Arrangementer, du har svaret på eller arrangerer.",

            // ── events.calendar (EV-CAL-månedskalenderen — /events/calendar, ADR 0063) ──
            ["events.calendar.title"] = "Kalender",
            ["events.calendar.prev"] = "Forrige",
            ["events.calendar.next"] = "Næste",
            ["events.calendar.today"] = "I dag",
            ["events.calendar.overlap_hint"] = "Overlapper med et andet arrangement i dette område",
            ["events.calendar.empty"] = "Ingen arrangementer i dette område.",
            ["events.calendar.from"] = "Fra",
            // EV-DWM (ADR 0064, U05) — Day/Week/Month-omskifterens labels (C-DWM·9).
            ["events.calendar.view.day"] = "Dag",
            ["events.calendar.view.week"] = "Uge",
            ["events.calendar.view.month"] = "Måned",

            // ── grant (den fælles « Hvem du giver adgang til »-picker — C# « Vælg alle » + tæller) ──
            ["grant.select_all"] = "Vælg alle",
            ["grant.label_residences"] = "Beboere",
            ["grant.label_groups"] = "Grupper",
            ["grant.count_selected"] = "{0} af {1} valgt",

            // ── profile (_AudienceEditor-advarslen; <b>-ordene inline er adskilt) ──
            ["profile.audience_empty_warning_lead"] = "Pas på:",
            ["profile.audience_empty_warning_body"] =
                "du har ikke valgt nogen personer eller grupper nedenunder, så dine kontaktoplysninger er i øjeblikket skjult for",
            ["profile.audience_empty_warning_everyone"] = "alle",
            ["profile.audience_empty_warning_tail"] =
                ". Tilføj en person eller gruppe, hvis du vil dele dem.",

            // ── settings (sektionen « Sprog til e-mail og beskeder » under /settings/language) ──
            ["settings.email_title"] = "Sprog til e-mail og beskeder",
            ["settings.email_lede"] =
                "Vælg det sprog, platformen skriver til dig på — kontoer og arrangementspåmindelser. " +
                "Dit valg gemmes på din konto.",
            ["settings.email_label"] = "Sprog til e-mail og beskeder",
            ["settings.email_note"] =
                "Vælger du et sprog, sendes dine e-mails og påmindelser på det. " +
                "Nulstiller du det, bruges instancens standardsprog.",
            ["settings.email_save"] = "Gem",
            ["settings.email_reset"] = "Nulstil til standardsprog",
            ["settings.email_flash_set"] = "E-mail- og notifikationssprog indstillet — din næste e-mail bruger det.",
            ["settings.email_flash_reset"] = "E-mail- og notifikationssprog nulstillet — instansstandarden bruges.",
            ["settings.email_reset_confirm"] =
                "Nulstil sprog til e-mail og beskeder til standardsproget?",

            // ── email (udgående e-mails — {0}/{1} er kørselsplaceholders) ──
            ["email.verify_subject"] = "Bekræft din Kumunita-konto",
            ["email.verify_body"] =
                "Hej {0},\n\nDin Kumunita-konto skal bekræftes ved din første login. " +
                "Åbn dette engangsklink for at bekræfte kontoen (den logger dig også ind):\n\n{1}\n\n" +
                "Hvis du ikke har oprettet denne konto, kan du ignorere denne besked.",
            ["email.reminder_subject"] = "Påmindelse: {0}",
            ["email.reminder_body"] = "**{0}** er på vej: {1}{2}.",

            // ── notifications (M6 — ADR 0076: the /notifications surface;
            // translations of the en floor above — same key set, same
            // dotted shape, no email templates for the reserved
            // post.mention kind) ──────────────────────────────────────────
            ["notifications.inbox"] = "Notifikationer",
            ["notifications.preferences"] = "Indstillinger",
            ["notifications.mark_all_read"] = "Markér alle som læst",
            ["notifications.empty"] = "Ingenting endnu — ting, der hænder dig, vises her.",
            ["notifications.bell"] = "Notifikationer",
            ["notifications.view"] = "Se",
            ["notifications.preferences.title"] = "Notifikationsindstillinger",
            ["notifications.preferences.intro"] = "Vælg, hvilke notifikationer du også får på e-mail. Indbakken noterer altid hver notifikation.",
            ["notifications.preferences.save"] = "Gem indstillinger",
            ["notifications.preferences.coming_soon"] = "kommer snart",
            ["notifications.kind.post.reply"] = "Svar",
            ["notifications.kind.post.mention"] = "Nævnelse",
            ["notifications.kind.group.post"] = "Gruppeindlæg",
            ["notifications.kind.group.added"] = "Tilføjet til gruppe",
            ["notifications.kind.group.invite"] = "Gruppemedlemsinvitation",
            ["notifications.kind.event.rsvp"] = "RSVP",
            ["notifications.kind.event.reminder"] = "Påmindelse",
            ["notifications.kind.report.filed"] = "Anmeldelse",
            ["notifications.kind.report.assigned"] = "Anmeldelse tilknyttet",
            ["notifications.kind.report.resolved"] = "Anmeldelse behandlet",
            ["notifications.kind.todo.assign"] = "Opgave tilknyttet",
            ["notifications.preference.post.reply.label"] = "Svar på mine indlæg",
            ["notifications.preference.post.mention.label"] = "Nævnelser af mig",
            ["notifications.preference.group.post.label"] = "Nye indlæg i mine grupper",
            ["notifications.preference.group.added.label"] = "Når jeg bliver tilføjet til en gruppe",
            ["notifications.preference.group.invite.label"] = "Når jeg bliver inviteret til en gruppe",
            ["notifications.preference.event.rsvp.label"] = "RSVP'er på mine arrangementer",
            ["notifications.preference.event.reminder.label"] = "Påmindelser om arrangementer",
            ["notifications.preference.report.filed.label"] = "Anmeldelser af mit indhold",
            ["notifications.preference.report.assigned.label"] = "Anmeldelser, der er tilknyttet mig",
            ["notifications.preference.report.resolved.label"] = "Behandling af anmeldelser, jeg er involveret i",
            ["notifications.preference.todo.assign.label"] = "Opgaver, der er tilknyttet mig",
            ["notification.post.reply.subject"] = "Der er kommet et svar på dit indlæg",
            ["notification.group.post.subject"] = "Nyt indlæg i din gruppe",
            ["notification.group.added.subject"] = "Du er blevet tilføjet til en gruppe",
            ["notification.group.invite.subject"] = "Du er blevet inviteret til en gruppe",
            ["notification.event.rsvp.subject"] = "En RSVP på dit arrangement",
            ["notification.event.reminder.subject"] = "Påmindelse om arrangement",
            ["notification.report.filed.subject"] = "En anmeldelse er rejst mod dit indlæg",
            ["notification.report.assigned.subject"] = "En anmeldelse er tilknyttet dig",
            ["notification.report.resolved.subject"] = "En anmeldelse er behandlet",
            ["notification.todo.assign.subject"] = "En opgave er tilknyttet dig",
            ["notification.post.reply.body"] = "Nogen har svaret på ét af dine indlæg: ",
            ["notification.group.post.body"] = "Nyt indlæg i én af dine grupper: ",
            ["notification.group.added.body"] = "Du er blevet tilføjet til gruppen ",
            ["notification.group.invite.body"] = "Du er blevet inviteret til gruppen ",
            ["notification.event.rsvp.body"] = "Nogen har RSVP'et på ét af dine arrangementer. ",
            ["notification.event.reminder.body"] = "Dit kommende arrangement: ",
            ["notification.report.filed.body"] = "En beboer har rejst en anmeldelse mod ét af dine indlæg. ",
            ["notification.report.assigned.body"] = "En anmeldelse er tilknyttet dig som moderator. ",
            ["notification.report.resolved.body"] = "En anmeldelse, du var involveret i, er behandlet. ",
            ["notification.todo.assign.body"] = "En opgave er tilknyttet dig: ",
            ["notifications.kind.account.signup"] = "Ny beboer",
            ["notifications.kind.account.verified"] = "Konto bekræftet",
            ["notifications.preference.account.signup.label"] = "Når en ny beboer tilmelder sig",
            ["notifications.preference.account.verified.label"] = "Når en beboer bekræfter sin konto",
            ["notification.account.signup.subject"] = "En ny beboer har tilmeldt sig",
            ["notification.account.signup.body"] = "En ny beboer har tilmeldt sig: ",
            ["notification.account.verified.subject"] = "En beboer har bekræftet sin konto",
            ["notification.account.verified.body"] = "En beboer har bekræftet sin konto: ",

            // ── ADR 0084 ──
            ["notification.announcement.subject"] = "En ny meddelelse",
            ["notification.announcement.body"] = "En ny meddelelse er blevet udgivet: ",
            ["notification.community.post.subject"] = "Nyt indlæg i dit lokalsamfund",
            ["notification.community.post.body"] = "Nyt indlæg i ét af dine lokalsamfund: ",
            ["notification.page.child.subject"] = "En ny side er tilføjet",
            ["notification.page.child.body"] = "En ny side er tilføjet under en side, du følger: ",
            ["notifications.kind.announcement"] = "Ny meddelelse",
            ["notifications.kind.community.post"] = "Nyt lokalsamfundsindlæg",
            ["notifications.kind.page.child"] = "Ny underside",
            ["notifications.preference.announcement.label"] = "Nye meddelelser",
            ["notifications.preference.community.post.label"] = "Nye indlæg i mine lokalsamfund",
            ["notifications.preference.page.child.label"] = "Nye undersider på sider, jeg følger",
            ["notifications.subscriptions.title"] = "Notifikationsabonnementer",
            ["notifications.subscriptions.intro"] = "Vælg hvilke lokalsamfund, grupper og sider, der giver dig besked. Indstillinger afgør hvilke typer du også får på e-mail; disse indstillingsknapper afgør hvilke mål der giver dig besked.",
            ["notifications.subscription.announcement.label"] = "Nye meddelelser",
            ["notifications.subscription.community.post.label"] = "Nye indlæg i lokalsamfund",
            ["notifications.subscription.group.post.label"] = "Nye indlæg i grupper",
            ["notifications.subscription.page.child.label"] = "Nye undersider",
            ["pages.subscribe"] = "Abonner på opdateringer",
            ["pages.unsubscribe"] = "Opsig abonnement",

            // ── PL (ADR 0086, U05) — /projects-landingen + Projects-taben ──
            ["pl.tabs.projects"] = "Projekter",
            ["pl.index.title"] = "Projekter",
            ["pl.index.lede"] = "Lokalsamfunds mål og projekter — det overordnede arbejde oven på opgaverne og brættene.",
            ["pl.index.goals_heading"] = "Mål",
            ["pl.index.projects_heading"] = "Projekter",
            ["pl.index.new_goal"] = "Nyt mål",
            ["pl.index.new_project"] = "Nyt projekt",
            ["pl.index.view_projects"] = "Se projekter →",
            ["pl.index.goals_empty"] = "Ingen mål endnu — opret et for at give det fælles arbejde en retning.",
            ["pl.index.projects_empty"] = "Ingen selvstændige projekter endnu — opret et for at komme i gang med at styre det fælles arbejde.",
            ["pl.index.start"] = "Start",
            ["pl.index.due"] = "Forfaldt",

            // ── PL (ADR 0086, U06) — måldetal + composer + redigering ──
            ["pl.goal.new_heading"] = "Nyt mål",
            ["pl.goal.new_lede"] = "Et mål giver det fælles arbejde en retning — med valgfri beskrivelse, community-filter og publikum. Projekter kan senere hænge af det.",
            ["pl.goal.create"] = "Opret mål",
            ["pl.goal.edit_heading"] = "Rediger mål",
            ["pl.goal.edit_lead"] = "Opdater titlen og beskrivelsen på dette mål. Dets publikum, community og sprog er fastlagt ved oprettelse.",
            ["pl.goal.save"] = "Gem ændringer",
            ["pl.goal.edit"] = "Rediger mål",
            ["pl.goal.title_hint"] = "Et kort navn på målet — feedens etiket.",
            ["pl.goal.description_hint"] = "En valgfri beskrivelse af den retning, dette mål giver det fælles arbejde. Et mål kan fungere med kun titel.",
            ["pl.goal.audience_heading"] = "Publikum — hvem der kan se dette mål",
            ["pl.goal.audience_public"] = "Synlig for alle på instansen.",
            ["pl.goal.audience_restricted"] = "Begrænset til de tilgange nedenfor.",
            ["pl.goal.projects_heading"] = "Projekter i dette mål",
            ["pl.goal.projects_empty"] = "Ingen projekter under dette mål endnu — opret et for at komme i gang med at styre det fælles arbejde.",
            ["pl.goal.empty_description"] = "Dette mål har endnu ingen beskrivelse.",
            ["pl.project.new_heading"] = "Nyt projekt",
            ["pl.project.new_lede"] = "Et projekt er et stykke fælles arbejde — med valgfri beskrivelse, status, start- og forfaldsdato, community-filter og publikum. Det kan hænge af et mål.",
            ["pl.project.create"] = "Opret projekt",
            ["pl.project.edit_heading"] = "Rediger projekt",
            ["pl.project.edit_lead"] = "Opdater titlen, beskrivelsen, målet, status og datoer på dette projekt. Dets publikum, community og sprog er fastlagt ved oprettelse.",
            ["pl.project.save"] = "Gem ændringer",
            ["pl.project.edit"] = "Rediger projekt",
            ["pl.project.title_hint"] = "Et kort navn på projektet — feedens etiket.",
            ["pl.project.description_hint"] = "En valgfri beskrivelse af, hvad det drejer sig om i dette projekt. Et projekt kan fungere med kun titel.",
            ["pl.project.status"] = "Status",
            ["pl.project.status_hint"] = "Et valgfrit statuslabel — samme faste ordforråd som to-dos. Lad være tom for ingen.",
            ["pl.project.start_date"] = "Start",
            ["pl.project.due_date"] = "Forfald",
            ["pl.project.dates_hint"] = "Begge er valgfri — lad være tom for ingen dato. Vist for seere i deres egen tidszone.",
            ["pl.project.audience_heading"] = "Publikum — hvem der kan se dette projekt",
            ["pl.project.audience_public"] = "Synlig for alle på instansen.",
            ["pl.project.audience_restricted"] = "Begrænset til de tilgange nedenfor.",
            ["pl.project.goal_heading"] = "Mål",
            ["pl.project.goal_hint"] = "Et valgfrit mål, projektet organiseres under. Lad være tom for et selvstændigt projekt.",
            ["pl.project.goal_link"] = "Mål",
            ["pl.project.associated_heading"] = "To-dos & boards i dette projekt",
            ["pl.project.todos_heading"] = "To-dos i dette projekt",
            ["pl.goal.delete"] = "Slet mål",
            ["pl.goal.delete_confirm"] = "Slet dette mål? Dets projekter forbliver, hvor de er — linket til dette mål vises blot ikke længere.",
            ["pl.project.delete"] = "Slet projekt",
            ["pl.project.delete_confirm"] = "Slet dette projekt? Dets to-dos og boards forbliver, hvor de er — linket til dette projekt vises blot ikke længere.",
            ["pl.project.boards_heading"] = "Boards i dette projekt",
            ["pl.project.todos_empty"] = "Ingen to-dos i dette projekt endnu.",
            ["pl.project.boards_empty"] = "Ingen boards i dette projekt endnu.",
            ["pl.project.empty_description"] = "Dette projekt har endnu ingen beskrivelse.",
            ["pl.todo.project_link"] = "Projekt",
            ["pl.todo.set_project"] = "Vælg projekt",
            ["pl.board.project_link"] = "Projekt",
            ["pl.board.set_project"] = "Vælg projekt",
            ["pl.board.add_to_project"] = "Tilføj til projekt…",
            ["pl.board.project_hint"] = "Et projektlink er en visningsflade — den grupperer dette board under projektet, men begrænser aldrig, hvem der kan se det.",
        };
    /// the completeness view's "known" universe). Always equal to
    /// <see cref="EnValues"/>.Keys, in declaration order.
    /// </summary>
    public static IReadOnlyCollection<string> AllKeys => EnValues.Keys.ToList();
}
