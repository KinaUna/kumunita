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
            // ── M19 (ADR 0120) — the guest-accounts surface: the /admin/guests
            // admin surface (D6) + the signed-in guest's shell notice (D5) ──
            ["admin.guests_title"]               = "Guest accounts",
            ["admin.guests_empty"]               = "No guest accounts yet.",
            ["admin.guests_create"]              = "Create guest",
            ["admin.guests_window_label"]        = "Access window",
            ["admin.guests_surfaces_label"]      = "Allowed surfaces",
            ["admin.guests_surface_announcements"] = "Announcements",
            ["admin.guests_surface_events"]      = "Events",
            ["admin.guests_surface_directory"]   = "Directory",
            ["admin.guests_saved"]               = "Guest standing saved.",
            ["account.guest_welcome"] =
                "You are signed in as a guest. Your access is limited to the " +
                "surfaces the admin has allowed, for the window they set.",

            // ── M20 (ADR 0121) — notification quiet times: the 5th
            // /settings/quiet resident section (D7) + the /admin/quiet cadence
            // surface (D8). U06 authors the COMPLETE 15-key closed set (11
            // resident settings.quiet.* + 4 admin admin.quiet.*); U06/U07
            // consume, U07 adds none (the register's closed-kw-l table).
            ["settings.quiet.title"]        = "Quiet hours",
            ["settings.quiet.description"]  = "Choose when your notification emails are held. Your quiet " +
                "schedule is saved on your account, applied in your own time zone — it takes " +
                "effect the next time a notification is sent, and never affects other residents.",
            ["settings.quiet.enabled"]      = "Hold notification emails during my quiet hours",
            ["settings.quiet.mode_label"]   = "When should notification emails be held?",
            ["settings.quiet.mode_blocked"] = "Held during the selected hours & days",
            ["settings.quiet.mode_allowed"] = "Held except the selected hours & days",
            ["settings.quiet.hours_label"]  = "Hours of day",
            ["settings.quiet.days_label"]   = "Days of week",
            ["settings.quiet.save"]         = "Save quiet hours",
            ["settings.quiet.flash_saved"]  = "Quiet hours saved — held emails are released when your quiet hours end.",
            ["settings.quiet.flash_cleared"] = "Quiet hours cleared — all notification emails will now be sent immediately.",
            ["admin.quiet.title"]           = "Quiet-time cadence",
            ["admin.quiet.cadence_label"]   = "Re-check held notifications every (minutes)",
            ["admin.quiet.save"]            = "Save cadence",
            ["admin.quiet.flash_saved"]     = "Quiet-time cadence saved.",

            // ── M28 (ADR 0151) — guardian time limits: the 13-key
            // guardian.timelimit.* GU Detail section (D7) + the single
            // account.time_limit.login_message login landing (referenced in
            // U04's Login.cshtml ?error=time-limit case). U05 authors the
            // COMPLETE 14-key closed set; U06 consumes, adds none. A DISTINCT
            // namespace — NOT the M20 settings.quiet.* / admin.quiet.* keys
            // (those are the notification lane's, unchanged — D9).
            ["guardian.timelimit.title"]        = "Time limits",
            ["guardian.timelimit.description"]  = "Choose when your child may use the platform. The schedule " +
                "is saved on their account, applied in their own time zone — it " +
                "takes effect on their next sign-in, and never affects you.",
            ["guardian.timelimit.enabled"]      = "Enforce time limits for this child",
            ["guardian.timelimit.mode_label"]   = "When may the child use the platform?",
            ["guardian.timelimit.mode_blocked"] = "Blocked during the selected hours & days",
            ["guardian.timelimit.mode_allowed"] = "Allowed only during the selected hours & days",
            ["guardian.timelimit.hours_label"]  = "Hours of day",
            ["guardian.timelimit.days_label"]   = "Days of week",
            ["guardian.timelimit.save"]         = "Save time limits",
            ["guardian.timelimit.clear"]        = "Clear time limits",
            ["guardian.timelimit.flash_saved"]  = "Time limits saved — the child will be signed out outside the allowed window.",
            ["guardian.timelimit.flash_cleared"] = "Time limits cleared — the child may now use the platform at any time.",
            ["guardian.timelimit.badge_set"]    = "Time limits set",
            ["account.time_limit.login_message"] = "You are outside your allowed hours. Please check back later.",

            // ── nav (the shared top-nav, _Layout + _AccountNav) ─────────────
            ["nav.home"]          = "Home",

            // ADR 0111 — the nav-variant surface: the compact top-row variant
            // folds the less-frequent sections into one "More" menu, and the
            // resident picks a variant from the account menu (nav_variant.*
            // are the picker labels, the active one marked with a ✓).
            ["nav.more"]          = "More",
            ["nav_variant.label"] = "Navigation style",
            ["nav_variant.row"]   = "Top row",
            ["nav_variant.rail"]  = "Icon rail",

            // ADR 0133 — the appearance (theme) picker: the resident chooses
            // auto (follow the OS) / light / dark (Forest) from the account
            // menu (theme.* are the picker labels, the active one marked ✓).
            ["theme.label"]      = "Appearance",
            ["theme.auto"]       = "Auto (match device)",
            ["theme.light"]      = "Light",
            ["theme.dark"]       = "Dark",

            // ── events (M4 — ADR 0054: the events nav entry + the Detail footer) ──
            ["nav.events"]        = "Events",
            ["events.created"]    = "Created",
            ["events.edited"]     = "edited",

            // ADR 0119 (M18, D9) — the composer recurrence picker keys (U05).
            // The canonical en floor (the D9 closed set's `events.recurrence.*`
            // half; the `events.series.*` detail-page half lands with U06).
            // These are the <c>en</c> source text the provider floor resolves to;
            // the de/fr/da values land in U07 (the M18 kw-l lane).
            ["events.recurrence.none"]       = "Does not repeat",
            ["events.recurrence.daily"]      = "Daily",
            ["events.recurrence.weekly"]     = "Weekly",
            ["events.recurrence.monthly"]    = "Monthly",
            ["events.recurrence.yearly"]     = "Yearly",
            ["events.recurrence.interval"]   = "Every",
            ["events.recurrence.ends_after"] = "Ends after",
            ["events.recurrence.ends_on"]    = "Ends on",
            ["events.recurrence.count"]      = "occurrences",
            ["events.recurrence.until"]      = "until",

            // ADR 0119 (M18, D9) — the detail-page series chip + skip/restore
            // button keys (U06). The canonical en floor (the D9 closed set's
            // `events.series.*` half; the `events.recurrence.*` composer half
            // landed with U05). These are the `en` source text the provider
            // floor resolves to; the de/fr/da values land in U07 (the M18
            // kw-l lane).
            ["events.series.repeats"]  = "Repeats",
            ["events.series.skip"]     = "Skip this occurrence",
            ["events.series.restore"]  = "Restore this occurrence",
            ["events.series.part_of"]  = "Part of a series",

            // ── projects (M5 — ADR 0067: the to-do surface nav entry + labels) ──
            ["nav.projects"]                 = "Projects",
            ["projects.todo.title"]          = "To-dos",
            ["projects.todo.lede"]           = "The neighborhood's shared to-dos — assign work to a neighbor, break it into subtasks, and put it on a board.",
            ["projects.todo.new"]            = "New to-do",
            ["projects.todo.new_lead"]       = "Write a to-do, optionally assign it to a neighbor, and — if needed — break it into subtasks or put it on a board. By default it is visible to everyone; turn that off in the audience section only if you want to narrow who can see it.",
            ["projects.todo.title_hint"]     = "A short label for the to-do — the card label.",
            ["projects.todo.status"]         = "Status",
            ["projects.todo.status_assignee_hint"] = "Status is a free-text label (a string, not a fixed list). Assigning a to-do gives that resident standing over it — display + standing, never an access limit.",
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
            ["projects.todo.filter_assigned_to_me"] = "Assigned to me",
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
            // ADR 0106 — self-assign lane + card details expander.
            ["projects.todo.assign_to_me"]   = "Assign to me",
            ["projects.todo.details"]        = "Details",
            ["projects.todo.community"]      = "Community",
            ["projects.todo.subtasks"]       = "Subtasks",
            ["projects.todo.boards"]         = "Boards",
            // ADR 0100 — the comments + replies section (C-M3·1 — comments
            // inherit the to-do's single Read decision, so the section is
            // visible to exactly the audience the to-do itself is).
            ["projects.todo.comments"]       = "Comments",
            ["projects.todo.comment_empty"]  = "No comments yet. If you can see this to-do, you can comment on it.",
            ["projects.todo.comment_reply"]  = "Comment",
            ["projects.todo.comment_submit"] = "Comment",
            ["projects.todo.comment_deleted"] = "This comment has been deleted by its author.",
            ["projects.todo.comment_delete"] = "Delete",
            ["projects.todo.comment_audience_note"] = "Comments have no own audience — they are visible under this to-do's single audience decision. You are commenting only where the to-do itself is visible.",
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
            ["projects.board.edit_lead"]     = "Update this board's title, description, and audience. Its community and language are fixed when the board is created.",
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
            ["projects.board.lane.delete"] = "Delete lane",
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
            ["common.full_page"]    = "Full page",
            ["common.exit_full_page"] = "Exit full page",
            ["common.open_full_page"] = "Open full page",
            ["common.fullscreen"]   = "Full screen",
            ["common.exit_fullscreen"] = "Exit full screen",
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
            ["admin.roles_independent_hint"] = "Independent — a resident may hold any combination. Nothing checked = a plain Member.",
            ["admin.moderator_scope"]       = "Moderator scope",
            ["admin.moderator_scope_hint"]  = "The communities this account may moderate. Meaningful only when the Moderator role is checked — the platform clears the scope picks when the Moderator role is off.",
            ["nav.announcements"] = "Announcements",
            ["nav.community"]     = "Community",
            ["nav.groups"]        = "Groups",
            ["nav.pages"]         = "Pages",
            ["nav.tags"]          = "Tags",
            ["nav.directory"]     = "Directory",
            ["nav.people"]        = "People",
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
            ["guardian.community_block_note"] =
                "Choose which communities this child can access. Blocking a " +
                "community hides it from them — including its posts — even if it " +
                "is one of the mandatory communities everyone belongs to. This is " +
                "the guardian's control: you decide who joins a community by " +
                "inviting them or through an admin, but you can hide one you " +
                "don't want this child to see.",
            ["guardian.community_blocked"]   = "Blocked & hidden",
            ["guardian.community_unblock"]   = "Unblock & show",
            ["guardian.community_block"]     = "Block access & hide",
            ["guardian.pending_invitations"] = "Pending group invitations",
            ["guardian.no_invitations"]      = "No pending invitations.",
            ["guardian.approve"]             = "Approve",
            ["guardian.handover"]            = "Hand over the account",
            ["guardian.handover_hint"]       =
                "Dissolving the guardianship hands the account to the child. " +
                "Their memberships are preserved, and their own controls come " +
                "back on the next read.",
            ["guardian.dissolve"]            = "Dissolve guardianship",
            // Count-aware steering (ADR 0028 §G·6): a <b>co-guardian</b> (this
            // child has ≥2 active guardians) sees this heading + hint + button
            // instead of the "Hand over the account" lane — removing yourself
            // ends your standing over the child, the other guardian(s)
            // continue, and the child keeps their account. The POST is the
            // <b>same</b> <c>Dissolve</c> action as the sole-guardian hand-over
            // lane (the standing over the child ends either way); only the
            // framing differs (a co-guardian is not handing anything over —
            // the other guardian is still there).
            ["guardian.remove_myself"]       = "Remove myself as guardian",
            ["guardian.remove_myself_hint"]  =
                "You are one of several guardians of this child. Removing yourself " +
                "ends your standing over their account; the other guardian(s) " +
                "continue, and their account is preserved.",
            ["guardian.remove_myself_submit"] = "Remove myself",
            ["guardian.suspended"]           = "Suspended",
            ["guardian.unsuspend"]           = "Un-suspend",
            ["guardian.suspend"]             = "Suspend",
            ["guardian.delete_child"]        = "Delete the child account",
            ["guardian.delete_child_lede"]   =
                "Deleting the account removes the child's sign-in, profile, and group and " +
                "community memberships, and dissolves any other guardianship over it. Their " +
                "past actions in the audit trail are preserved with their identity replaced " +
                "by a placeholder. This cannot be undone.",
            ["guardian.delete_child_confirm_checkbox"] = "I understand the child account will be permanently deleted.",
            ["guardian.delete_child_submit"] = "Delete account",
            ["guardian.display_name"]        = "Display name",
            ["guardian.email"]               = "Email address",
            ["guardian.password"]            = "Password",
            ["guardian.child_email_hint"]    =
                "The child opens the confirmation link in their email to set their own password and sign in — you don't need (or set) their password.",
            ["guardian.consent.intro"]       =
                "By creating this profile, you confirm that you are the legal " +
                "guardian of this child. As their guardian, you maintain full " +
                "control over their account:",
            ["guardian.consent.duties_invitations"] =
                "You must approve or deny all group and event invitations.",
            ["guardian.consent.duties_chat"] =
                "You can enable or disable chat features for this profile at " +
                "any time.",
            ["guardian.consent.duties_data"] =
                "This data is fully isolated to this community's own instance " +
                "and will never be sold, profiled, or used for advertising.",
            ["guardian.consent.checkbox"]    =
                "I consent to the processing of my child's data under these " +
                "terms.",

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
            ["guardian.assign.success"]      = "Guardian assigned — they will be asked to accept.",

            // ── guardian acceptance lane (GA ADR 0038 §F) ──────────────────
            // The "accept/decline" half of the assignment: the assigned
            // guardian sees their pending requests on the /me/children Index
            // page (the pendingRequests card), accepts with the same consent
            // to the child-account terms the creating guardian accepts on the
            // AddChild form, or declines. Until they act, they hold no
            // standing over the child (the standing gates all query Active).
            ["guardian.pending"]              = "Pending",
            ["guardian.pendingRequests.title"] =
                "Guardian requests awaiting your acceptance",
            ["guardian.pendingRequests.lead"] =
                "Another guardian has asked you to become a co-guardian for one of their children. " +
                "Accept (and agree to the child-account terms) or decline — until you act, you hold no standing over the account.",
            ["guardian.pendingRequests.child"]    = "Child",
            ["guardian.pendingRequests.conferrer"] = "Requested by",
            ["guardian.accept"]                   = "Accept & agree to the terms",
            ["guardian.accept.consent.intro"]     =
                "By accepting, you confirm that you are a legal guardian of this child. " +
                "As their guardian, you maintain full control over their account:",
            ["guardian.accept.consent.duties_invitations"] =
                "You must approve or deny all group and event invitations.",
            ["guardian.accept.consent.duties_chat"] =
                "You can enable or disable chat features for this profile at any time.",
            ["guardian.accept.consent.duties_data"] =
                "This data is fully isolated to this community's own instance and will never be sold, profiled, or used for advertising.",
            ["guardian.accept.consent.checkbox"] =
                "I consent to the processing of this child's data under these terms.",
            ["guardian.accept.consent.required"] =
                "You must consent to the child-account terms before accepting.",
            ["guardian.decline"] = "Decline",

            // ── The lane — event attendance (the guardian's three postures) ──
            ["guardian.eventrsvp.title"] = "Event attendance",
            ["guardian.eventrsvp.description"] =
                "Choose how this child's event attendance is handled.",
            ["guardian.eventrsvp.mode_approves_active"] =
                "Currently: Guardian approves — I approve or deny every event the child wants to attend.",
            ["guardian.eventrsvp.mode_notifies_active"] =
                "Currently: Guardian notifies — the child attends freely; I'm told and can remove any of their attendance afterwards.",
            ["guardian.eventrsvp.mode_childdecides_active"] =
                "Currently: Child decides — the child chooses their own attendance; no approval and no notification.",
            ["guardian.eventrsvp.switch_to_approves"] = "Switch to Guardian approves",
            ["guardian.eventrsvp.switch_to_notifies"] = "Switch to Guardian notifies",
            ["guardian.eventrsvp.switch_to_childdecides"] = "Switch to Child decides",
            ["guardian.eventrsvp.pending_title"] = "Pending attendance requests",
            ["guardian.eventrsvp.pending_empty"] = "No pending attendance requests.",
            ["guardian.eventrsvp.desired"] = "Wants to attend",
            ["guardian.eventrsvp.approve"] = "Approve",
            ["guardian.eventrsvp.deny"] = "Deny",
            ["guardian.eventrsvp.rsvps_title"] = "This child's current attendance",
            ["guardian.eventrsvp.rsvps_empty"] = "No current attendance to remove.",
            ["guardian.eventrsvp.veto"] = "Remove",
            ["guardian.eventrsvp.approve_confirm"] =
                "Approve this child's attendance on this event?",
            ["guardian.eventrsvp.deny_confirm"] =
                "Deny this child's attendance on this event? They will not be attending.",
            ["guardian.eventrsvp.veto_confirm"] =
                "Remove this child's attendance on this event? Their RSVP will be deleted.",

            // ── notification kinds (the lane) ────────────────────────────────
            ["notifications.kind.guardian.event_request"] =
                "Attendance request from your child",
            ["notifications.preference.guardian.event_request.label"] =
                "When your child asks to attend an event",
            ["notification.guardian.event_request.subject"] =
                "Your child is asking to attend an event",
            ["notification.guardian.event_request.body"] =
                "Your child is asking to attend an event: ",
            ["notifications.kind.guardian.event_rsvp"] =
                "Your child attended an event",
            ["notifications.preference.guardian.event_rsvp.label"] =
                "When your child attends an event",
            ["notification.guardian.event_rsvp.subject"] =
                "Your child attended an event",
            ["notification.guardian.event_rsvp.body"] =
                "Your child attended an event: ",

            // ── notification kind (GA ADR 0038 §F) ──────────────────────────
            // The "guardian.assign" notification: the assigned guardian
            // (the assignee) gets an inbox row + (best-effort) email when an
            // existing guardian assigns them as a co-guardian. The LinkPath
            // is /me/children (the Index page's pending-requests card).
            // Opt-OUT default (the resident-facing posture).
            ["notifications.kind.guardian.assign"] =
                "A guardian has asked you to become a co-guardian",
            ["notifications.preference.guardian.assign.label"] =
                "When a guardian asks you to become a co-guardian",
            ["notification.guardian.assign.subject"] =
                "A guardian has asked you to become a co-guardian",
            ["notification.guardian.assign.body"] =
                "A guardian has asked you to become a co-guardian for their child: ",

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

            // ── settings — items per page (the resident's page-size
            // preference, additive on the platform default) ────────────────
            ["settings.pagesize_title"]        = "Items per page",
            ["settings.pagesize_lede"]         =
                "Choose how many items each list shows per page (feeds, events, projects and the rest). " +
                "Your choice is saved on your account — it takes effect the next time you load a list, and " +
                "never affects other residents.",
            ["settings.pagesize_label"]        = "Items per page",
            ["settings.pagesize_default_marker"] = "— platform default",
            ["settings.pagesize_reset_confirm"]  = "Reset your items-per-page to the platform default?",
            ["settings.pagesize_reset"]        = "Reset to platform default",
            ["settings.pagesize_save"]         = "Save",
            ["settings.pagesize_flash_set"]    = "Items per page set to \"{0}\" — it takes effect on the next request.",
            ["settings.pagesize_flash_reset"]  = "Items per page reset — the platform default will be used.",

            // ── settings — home page (the resident's hide-home-intro display
            // preference, ADR 0149 D1) ─────────────────────────────────────
            ["settings.home_title"]        = "Home page",
            ["settings.home_lede"]         =
                "The home page opens with two intro sections (what Kumunita is and what it does) " +
                "before the feed. Turn this on to land straight on what's new. " +
                "Your choice is saved on your account and never affects other residents.",
            ["settings.home_label"]        = "Hide the intro sections and show me the feed right away",
            ["settings.home_note"]         = "When off, the home page shows its intro sections as usual.",
            ["settings.home_save"]         = "Save",
            ["settings.home_flash_hide"]   = "Home page updated — the intro sections are now hidden and the feed shows first.",
            ["settings.home_flash_show"]   = "Home page updated — the intro sections will show again.",

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

            // ── admin — the announcement-comments gate (the
            // /admin/announcements/comments surface; ADR 0101) ───────────────
            ["admin.anncomments_title"]     = "Announcement comments",
            ["admin.anncomments_lede"]      =
                "Whether signed-in residents may comment on announcements. " +
                "Closing the gate hides the comment list and composer on every " +
                "announcement — no one can add a comment. Visitors can never " +
                "comment, and existing comments are not removed — the gate " +
                "only controls new comments.",
            ["admin.anncomments_on"]        = "Open — signed-in residents can comment",
            ["admin.anncomments_off"]       = "Closed — no signed-in resident can add a comment",
            ["admin.anncomments_save"]      = "Save",

            // ── admin — the direct-messaging gate (the /admin/messaging
            // surface; M9, ADR 0105) ────────────────────────────────────────
            ["admin.messaging_title"]   = "Direct messaging",
            ["admin.messaging_lede"]    =
                "Whether signed-in residents may open direct 1:1 conversations. " +
                "Closing the gate hides the Messages entry and refuses every " +
                "conversation seam — no one can open a thread or send a message. " +
                "The gate is off by default; it only controls new messaging, and " +
                "existing conversations and messages are never touched by it. " +
                "Even a GlobalAdmin who is not a participant cannot read a " +
                "conversation.",
            ["admin.messaging_on"]      = "Open — signed-in residents can message each other",
            ["admin.messaging_off"]     = "Closed — no resident can open or send messages",

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
            ["home.roadmap.show_more_earlier"] = "Show the {n} earlier milestones",
            ["home.roadmap.show_more_upcoming"]  = "Show the {n} upcoming milestones",
            ["home.roadmap.status.done"]    = "Done",
            ["home.roadmap.status.next"]    = "In progress",
            ["home.roadmap.status.planned"] = "Planned",

            // ── client JS strings bundle (P0-6 translation audit: the
            //    rich editor, image editor, tag-suggest, and the
            //    notifications bell resolve these from the server-rendered
            //    #kumunita-strings JSON block in _Layout.cshtml) ──────────
            ["common.close"]  = "Close",
            ["common.cancel"] = "Cancel",
            ["rc.editor.error_generic"] = "Something went wrong.",
            ["rc.editor.link.title"]    = "Insert link",
            ["rc.editor.link.url"]      = "URL",
            ["rc.editor.link.confirm"]  = "Insert link",
            ["rc.editor.link.busy"]     = "Inserting…",
            ["rc.editor.link.err_empty"]    = "Enter a URL.",
            ["rc.editor.link.err_invalid"]  = "Enter a valid link (web address, email, or site-relative path).",
            ["rc.editor.image.title"]   = "Edit image",
            ["rc.editor.image.err_rejected"] = "Uploaded image source was rejected.",
            ["rc.editor.image.err_upload"]   = "Upload failed.",
            ["rc.editor.attach.title"]  = "Attach file",
            ["rc.editor.attach.file"]   = "File",
            ["rc.editor.attach.link_text"] = "Link text",
            ["rc.editor.attach.default_label"] = "Attachment",
            ["rc.editor.attach.confirm"]  = "Attach",
            ["rc.editor.attach.busy"]     = "Uploading…",
            ["rc.editor.attach.err_no_file"] = "Choose a file to attach.",
            ["img.edit.close"]       = "Close",
            ["img.edit.crop_area"]   = "Crop area",
            ["img.edit.width"]       = "Width",
            ["img.edit.output"]      = "Output",
            ["img.edit.reset_crop"]  = "Reset crop",
            ["img.edit.use_original"] = "Use original",
            ["img.edit.apply"]       = "Apply",
            ["img.edit.title"]       = "Crop your avatar",
            ["img.edit.err_edit"]    = "Edit failed.",
            ["img.edit.err_could"]   = "Could not edit the image.",
            ["img.edit.err_load"]    = "Could not load the image for editing.",
            ["img.edit.err_export"]  = "Image export failed.",
            ["tag.suggest.remove_prefix"] = "Remove tag: ",
            ["notif.fallback"]       = "Notifications",

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

            // ── ADR 0138 — the resident self-serve change-password surface
            //    (/account/password: the form + the locked notice), the
            //    settings-tab link (nav.change_password, moved out of the
            //    account dropdown 2026-10-04), and the
            //    GlobalAdmin /admin/sample toggle (admin.sample.*) ──
            ["account.change_password_title"] = "Change password",
            ["account.change_password_lede"] =
                "Pick a new password for your account. After saving you'll be " +
                "signed out and asked to sign in again with the new password.",
            ["account.change_password_current"] = "Current password",
            ["account.change_password_new"] = "New password",
            ["account.change_password_confirm_new"] = "Confirm new password",
            ["account.change_password_submit"] = "Change password",
            ["account.change_password_locked_title"] = "Password changes are locked",
            ["account.change_password_locked_body"] =
                "This is a demo account and password changes are locked by the " +
                "administrator so everyone can keep using the shared credentials. " +
                "You can still use every other feature of the platform.",
            ["account.change_password_back"] = "Back to your profile",

            ["nav.change_password"] = "Change password",

            // ── ADR 0142 — the resident self-serve delete-account surface
            //    (/account/delete: the form + the ADR 0142 D5 refusal
            //    notice for a non-GlobalAdmin resident), the settings-tab
            //    link (nav.delete_account), and the admin-removal surface
            //    (admin.delete_account.*) ──
            ["account.delete_title"] = "Delete account",
            ["account.delete_lede"] =
                "Deleting your account removes your sign-in, your profile, " +
                "and your group and community memberships. Your past actions " +
                "in the platform's audit trail are preserved with your " +
                "identity replaced by a placeholder (the platform's privacy " +
                "policy, OPS.md §9). This cannot be undone.",
            ["account.delete_password"] = "Password",
            ["account.delete_confirm_checkbox"] =
                "I understand my account will be permanently deleted and this " +
                "cannot be undone.",
            ["account.delete_submit"] = "Delete account",
            ["account.delete_refused"] =
                "The self-serve delete-account lane is only available to a " +
                "GlobalAdmin. A non-GlobalAdmin resident cannot delete their " +
                "own account — contact an administrator to remove the account.",

            ["nav.delete_account"] = "Delete account",

            ["admin.delete_account_label"] = "Delete account",
            ["admin.delete_account_confirm"] =
                "Delete this account permanently? Their audit trail is " +
                "preserved (pseudonymized); their account, profile, and " +
                "memberships are removed. This cannot be undone.",

            ["admin.sample_title"] = "Sample data",
            ["admin.sample_lede"] =
                "This instance runs the demo neighborhood (sample data). Lock the " +
                "sample accounts out of changing their own password so visitors can " +
                "test features without breaking the shared credentials — the demo " +
                "admin keeps its own password lane.",
            ["admin.sample_lock_label"] = "Sample account password changes",
            ["admin.sample_lock_on"] = "Locked — sample accounts can't change their own password",
            ["admin.sample_lock_off"] = "Unlocked — sample accounts can change their own password",
            ["account.login.error.blocked"] =
                "Your account has been temporarily suspended. Contact an administrator.",
            ["account.login.error.removed"] =
                "Your account has been removed. Contact an administrator.",
            ["account.login.error.role_changed"] =
                "Your role has been changed. Please sign in again.",

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
                "These picks are hidden while “Everyone in this community” is " +
                "on — the post is visible to everyone in the community. Turn " +
                "that off to narrow who can see it with the picks here.",
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
            ["groups.danger_heading"]   = "Danger zone",
            ["groups.danger_delete_hint"] =
                "Deleting the group removes it, its members, and any pending invitations. This cannot be undone.",
            ["groups.danger_delete_button"] = "Delete this group",
            ["groups.new_title"]      = "Post to this group",
            ["groups.new_back"]       = "back to the group",
            ["groups.new_submit"]     = "Post to group",
            // ADR 0089 (GE) — the group-events lane: the feed section heading,
            // the empty-state (mirroring the posts lane), the composer +
            // editor headings, and the detail-page back link.
            ["groups.back"]           = "Back to",
            ["groups.events_heading"] = "Events",
            ["groups.new_event"]      = "New event",
            ["groups.events_empty_can"] =
                "No events yet. Plan the first one — it will be visible to the current members.",
            ["groups.events_empty"]    = "No events here yet.",
            ["groups.new_event_title"] = "Event for this group",
            ["groups.new_event_lead"]  =
                "Your event will be visible to the current members of this group only.",
            ["groups.new_event_submit"] = "Add event to group",
            ["groups.edit_event_title"] = "Edit this event",
            // ── groups list (the Airy layout — the invitation panel + the
            //    member-count word on each group card) ───────────────────────
            ["groups.invitations"]    = "Invitations",
            ["groups.invitations_pending"] = "pending",
            ["groups.invited_by"]     = "Invited by",
            ["groups.invite_accept"]  = "Accept",
            ["groups.invite_decline"] = "Decline",
            // ── ADR 0094 — the resident self-initiated join-request lane ──
            ["groups.join_requests"]              = "Join requests",
            ["groups.join_requests_pending"]      = "pending",
            ["groups.join_requests_note"]         = "Waiting for the group owner.",
            ["groups.join_withdraw"]              = "Withdraw",
            ["groups.other_public"]               = "Other public groups",
            ["groups.request_join"]               = "Request to join",
            ["groups.join_requests_pending_heading"] = "Pending join requests",
            ["groups.join_approve"]               = "Approve",
            ["groups.join_decline"]               = "Decline",
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
            ["upload.max_size"] = "Maximum file size: {0}",
            ["profile.avatar_hint"] =
                "JPEG, PNG, WebP or GIF. Saving replaces the " +
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

            // ── M23 (U04) — extended-profile editor + directory detail ──
            ["profile.edit.bio"] = "Bio",
            ["profile.edit.tags"] = "Tags",
            ["profile.edit.tags.placeholder"] = "e.g. gardening, baking, cycling…",
            ["profile.detail.bio"] = "About",
            ["profile.detail.tags"] = "Interests & skills",
            ["profile.detail.tags.empty"] = "No tags set.",
            ["profile.flash.saved"] = "Profile updated.",

            // ── M23 (U05) — the /people find-people surface ─────────────
            ["profile.find.title"]   = "Find people",
            ["profile.find.by_tag"]  = "Find by tag",
            ["profile.find.by_bio"]  = "Find by bio",
            ["profile.find.results"] = "{0} people found",
            ["profile.find.empty"]   = "No one matches — yet.",

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
                "this post's single audience decision. You are replying " +
                "only where the post itself is visible.",
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
            // The /pages browse's two sections (ADR 0040's kind split,
            // surfaced in the UI — resident pages apart from platform pages).
            ["pages.section_community"] = "Community pages",
            ["pages.section_platform"]  = "Platform pages",
            ["pages.section_platform_lede"] =
                "Published by the platform — terms, help, privacy, and the " +
                "code of conduct.",

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
            // M9 amendment (ADR 0139) — the "Send a message" affordance on a
            // directory card / the detail page: rendered only when *both*
            // parties' messaging standing is on (the two-sided gate the
            // DirectoryController computes). One key, used on both surfaces.
            ["directory.send_message"] = "Send a message",

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
            // ADR 0146 — the child-account handoff (the confirmation surface
            // collects the child's own password before activating the account).
            ["account.verify_set_password_lede"] =
                "Set the password for this account — you're the one who will use it.",
            ["account.verify_set_password_submit"] = "Set my password & sign in",
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

            // ── admin (Analytics page — M13, ADR 0114 D4) ─────────────────
            ["admin.analytics_title"] = "Usage analytics",
            ["admin.analytics_lede"] =
                "A local summary of how the platform is used — a request count, " +
                "the signed-in / anonymous split, the number of distinct accounts, " +
                "and the per-surface ranking, over a fixed window. No per-account " +
                "detail is shown; the raw rows are available only to the operator's " +
                "database.",
            ["admin.analytics_window"] = "Window",
            ["admin.analytics_total"] = "Total requests",
            ["admin.analytics_authenticated"] = "Signed-in",
            ["admin.analytics_anonymous"] = "Anonymous",
            ["admin.analytics_distinct"] = "Distinct accounts",
            ["admin.analytics_surface"] = "Surface",
            ["admin.analytics_count"] = "Count",
            ["admin.analytics_export"] = "Export CSV",

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

            // ── announcement comments (the Detail.cshtml comment lane;
            //    signed-in-only, admin-toggleable; ADR 0101) ──────────────────
            ["announcements.comments"] = "Comments",
            ["announcements.comment_empty"] = "No comments yet. Be the first to say something.",
            ["announcements.comment_deleted"] = "This comment has been deleted by its author.",
            ["announcements.comment_delete"] = "Delete",
            ["announcements.comment_reply"] = "Write a comment",
            ["announcements.comment_submit"] = "Comment",
            ["announcements.comment_audience_note"] =
                "Comments are visible to signed-in residents only, even on a " +
                "public announcement, and follow this announcement's own " +
                "audience (a community-targeted one is visible to that " +
                "community's residents).",

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

            // ── what's new (the VN lane, ADR 0110 — version changelog + toast) ──
            ["whatsnew.eyebrow"]        = "What's new",
            ["whatsnew.heading"]        = "What's new, version by version",
            ["whatsnew.lead"] =
                "Every release is dated and listed here — read what landed in " +
                "each minor version of the platform you are using.",
            ["whatsnew.version"]        = "Version",
            ["whatsnew.show_more"]      = "Show more versions",
            ["whatsnew.show_more_remaining"] = "Show the {n} earlier versions",
            ["whatsnew.toast_label"]    = "Kumunita {v} is now in use.",
            ["whatsnew.toast_see"]      = "See what's new",

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
            ["events.mine.show_more"] = "Show the {n} more events",

            // ── events.past (the EV-PAST toggle + empty state on /events — ADR 0109) ──
            ["events.upcoming"] = "Upcoming",
            ["events.past"] = "Past",
            ["events.past_empty"] = "No past events yet.",

            // ── events.ics (the M12 iCal affordances — the detail page's
            //    "Add to calendar" link + the feed/calendar feed link, ADR 0112) ──
            ["events.ics.download"] = "Add to calendar",
            ["events.ics.feed"] = "Calendar feed (iCal)",

            // ── M14 (ADR 0115 D2) — the Events ↔ Projects interlock's two U03
            //    display-link labels (the to-do detail's event chip + the event
            //    detail's linked to-dos section; the closed-key registry +
            //    KnownTranslationKeys_ParityTests enforce the × 4) ──
            ["todo.event_link"] = "Linked event",
            ["events.linked_todos"] = "Linked to-dos",

            // ── M14 (ADR 0115 D3) — the U04 set-event picker labels (the
            //    to-do detail's "Link to event" form label + the
            //    <select>'s placeholder / clear option; the closed-key
            //    registry + KnownTranslationKeys_ParityTests enforce the × 4) ──
            ["todo.set_event.label"] = "Link to event",
            ["todo.set_event.pick"] = "Choose an event",

            // ── M14 (ADR 0115 D4) — the two U06 VTODO affordances (the to-do
            //    detail's "Add to calendar" link + the to-do index's feed
            //    link, the ADR 0112 events.ics.* shape carried to the to-do
            //    surface; plain <a> links, no new JS — the closed-key
            //    registry + KnownTranslationKeys_ParityTests enforce the × 4) ──
            ["projects.todos.ics.download"] = "Add to calendar",
            ["projects.todos.ics.feed"] = "Calendar feed (iCal)",

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
            // ADR 0146 — the child lane's body: the one remaining step is to
            // set the child's own password (the guardian set up the account
            // but never held the credential).
            ["email.verify_child_body"] =
                "Hi {0},\n\nYour Kumunita account is ready. Open this one-time link to " +
                "confirm the account and set your password (it also signs you in):\n\n{1}\n\n" +
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
            ["notifications.mark_read"] = "Mark as read",
            ["notifications.mark_unread"] = "Mark as unread",
            ["notifications.empty"] = "Nothing yet — things that happen to you will show up here.",
            ["notifications.bell"] = "Notifications",
            ["notifications.view"] = "View",
            ["notifications.accept"] = "Accept",
            ["notifications.decline"] = "Decline",
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
            // ── M9 (ADR 0105, U03) — the message.new kind + nudge templates ──
            ["notifications.kind.message.new"] = "New message",
            ["notifications.preference.message.new.label"] = "Messages from other residents",
            ["notification.message.new.subject"] = "A new message",
            ["notification.message.new.body"] = "A resident sent you a message: ",

            // ── GU community-approval lane (ADR 0141) — the guardian-facing
            // kind + nudge templates (the template ends with ": " — the
            // emitter appends the UGC snippet, here the group/community name
            // + the child's display name, after it) ──
            ["notifications.kind.guardian.group_invite"] =
                "Group invitation for your child",
            ["notifications.preference.guardian.group_invite.label"] =
                "When a group invites your child",
            ["notification.guardian.group_invite.subject"] =
                "A group has invited your child",
            ["notification.guardian.group_invite.body"] =
                "A group has invited your child: ",
            ["notifications.kind.guardian.community_invite"] =
                "Community membership for your child",
            ["notifications.preference.guardian.community_invite.label"] =
                "When a community adds your child",
            ["notification.guardian.community_invite.subject"] =
                "A community has added your child",
            ["notification.guardian.community_invite.body"] =
                "A community has added your child: ",

            // ── GU community-approval lane (ADR 0141) — the manage-child
            // page's new sections ──
            ["guardian.pending_community_requests"] = "Pending community memberships",
            ["guardian.no_community_requests"] = "No pending community memberships.",
            ["guardian.reject"] = "Reject",

            // ── M9 (ADR 0105, U04) — the resident surface: nav, list, thread, composer ──
            ["message.nav"] = "Messages",
            ["message.title"] = "Messages",
            ["message.new"] = "New conversation",
            ["message.thread.empty"] = "No messages yet — say hello.",
            ["message.compose.placeholder"] = "Write a message…",
            ["message.compose.send"] = "Send",
            ["message.unread"] = "unread",
            ["message.disabled"] = "Direct messaging is turned off on this instance.",
            ["message.compose.disabled"] = "This resident has turned off direct messaging, so you can't send them a new message.",
            ["message.other"] = "the other person",
            ["message.sent_to"] = "Sent to {0}.",
            ["message.load_earlier"] = "Load earlier messages",
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

            // ── M7 (ADR 0090) — the shared pager's two link labels (D5) ──
            // The feeds are newest-first / earliest-start-first, so *Prev*
            // steps toward the head (the newer rows) and *Next* toward the
            // tail (the older rows) — the locked en strings (design §7.8).
            ["pagination.prev"] = "Newer",
            ["pagination.next"] = "Older",

            // M8 (ADR 0091 D1/D4) — the one /search page + nav entry.
            ["search.nav"] = "Search",
            ["search.title"] = "Search",
            ["search.placeholder"] = "Search posts, events, pages, announcements, projects, boards, to-dos, inventory, documents and people…",
            ["search.no-results"] = "No results for",
            ["search.section.posts"] = "Posts",
            ["search.section.events"] = "Events",
            ["search.section.pages"] = "Pages",
            ["search.section.announcements"] = "Announcements",
            ["search.section.projects"] = "Projects",
            ["search.section.boards"] = "Boards",
            ["search.section.todos"] = "To-dos",
            ["search.section.inventory"] = "Inventory",
            ["search.section.documents"] = "Documents",
            ["search.section.people"] = "People",
            ["search.scope.community"] = "Community",
            ["search.scope.groups"] = "Groups",
            ["search.empty.hint"] = "Find posts, events, pages, announcements, projects, boards, to-dos, inventory, documents and people by text or tag — the results only show content you can already read.",

            // M10 (ADR 0107 D10) — the one quiet install affordance's label
            // (U03's pwa-install.ts renders it on beforeinstallprompt; the
            // closed-key registry + KnownTranslationKeys_ParityTests enforce
            // the × 4). D5: no banner, no modal — one button.
            ["pwa.install"] = "Install app",

            // M11 (ADR 0108 D10) — the portability operator surface (the
            // /admin/portability index: the export button, the import form +
            // its destructive-action guard, and the two status renders).
            // closed-key registry + KnownTranslationKeys_ParityTests enforce
            // the × 4; the confirm() message + the status toasts are
            // attribute/TempData strings (outside the kw-l TagHelper's reach)
            // resolved through the provider (the ADR 0072 attribute idiom).
            ["portability.index.title"]  = "Portability",
            ["portability.export"]       = "Export",
            ["portability.import"]       = "Import",
            ["portability.confirm.import"] = "Import this archive? This replaces the instance's content (the restore path — the operator's pre-import backup is the rollback).",
            ["portability.status.ok"]    = "Done.",
            ["portability.status.failure"] = "Refused — the archive was rejected before anything was written:",

            // M27 (ADR 0148 D9) — the resident's own portability surface (the
            // /account/portability index: the export button + the import upload
            // form + the status area; U07). Deliberately a DISTINCT
            // myportability.* namespace from M11's admin portability.* keys so
            // the resident surface never collides with the operator surface.
            // closed-key registry + KnownTranslationKeys_ParityTests enforce the
            // × 4; myportability.status is a TempData string (the controller's
            // fail-closed render), outside the kw-l TagHelper's reach.
            ["myportability.index.title"]  = "My data",
            ["myportability.export"]       = "Export",
            ["myportability.import"]       = "Import",
            ["myportability.import.resolve"] = "Apply my choices",
            ["myportability.resolve.add_elsewhere"] = "Add elsewhere",
            ["myportability.resolve.discard"] = "Discard",
            ["myportability.status"]       = "Status",

            // M15 U04 (ADR 0116, D8) — the file-facing bulk keys.
            ["translations.bulk.export"]     = "Download translations (CSV)",
            ["translations.bulk.import"]     = "Upload translations (CSV)",
            ["translations.bulk.import_hint"] = "Blank cells are skipped (they never erase a translation); a file with an unknown key or language is refused unchanged.",

            // M15 U05 (ADR 0116, D8) — the editor-facing bulk keys (the batch
            // form's save button + the two mode-toggle labels).
            ["translations.bulk.save_all"]   = "Save all",
            ["translations.bulk.mode_batch"] = "Batch editing",
            ["translations.bulk.mode_single"] = "Edit one at a time",

            // M16 (ADR 0117, D1) — the inventory surface (check-out / check-in
            // + the usage-history section + the nav entry). The closed
            // key set is the design doc §kw-l (the list / detail / create
            // labels U04 consumes; the edit / delete / check-out / check-in
            // action labels U05 consumes; the nav entry U05 consumes).
            ["inv.nav"]                     = "Inventory",
            ["inv.list.title"]              = "Inventory",
            ["inv.list.empty"]              = "No items yet.",
            ["inv.list.ownerKind.shared"]   = "Shared",
            ["inv.list.ownerKind.community"] = "Community",
            ["inv.list.ownerKind.private"]  = "Private",
            ["inv.list.filter"]             = "Filter by type",
            ["inv.create.title"]            = "New item",
            ["inv.create.name"]             = "Name",
            ["inv.create.ownerKind"]        = "Type",
            ["inv.create.description"]      = "Description",
            ["inv.create.component"]        = "Section",
            ["inv.create.submit"]           = "Create item",
            ["inv.detail.title"]            = "Item",
            ["inv.detail.currentHolder"]    = "Currently with",
            ["inv.detail.history"]          = "Usage history",
            ["inv.detail.edit"]             = "Edit",
            ["inv.detail.delete"]           = "Delete",
            ["inv.detail.checkOut"]         = "Check out",
            ["inv.detail.checkIn"]          = "Check in",
            ["inv.edit.title"]              = "Edit item",
            ["inv.edit.submit"]             = "Save changes",

            // M17 (ADR 0118) — Bookmarks: the personal-pin surface (D6: a
            // standing core surface, no admin toggle). The closed key set is
            // the design doc §kw-l (14 keys: the nav entry U03 consumes, the
            // list / degraded / kind labels U03 consumes, the unbookmark
            // action U03 consumes, the toggle button U04 consumes, and the
            // obs-2 redirect-back flash toast keys (the ADR 0118 amendment,
            // 2026-09-30)).
            ["bm.nav"]                      = "Bookmarks",
            ["bm.list.title"]               = "Your bookmarks",
            ["bm.list.empty"]               = "No bookmarks yet.",
            ["bm.list.degraded"]            = "No longer available",
            ["bm.list.kind.post"]           = "Posts",
            ["bm.list.kind.event"]          = "Events",
            ["bm.list.kind.todo"]           = "To-dos",
            ["bm.list.kind.announcement"]   = "Announcements",
            ["bm.list.kind.page"]           = "Pages",
            ["bm.list.unbookmark"]          = "Remove",
            ["bm.button.bookmark"]          = "Bookmark",
            ["bm.button.bookmarked"]        = "Bookmarked",
            // Obs-2 (ADR 0118 amendment) — the redirect-back flash toast keys.
            ["bm.toggle.bookmarked"]        = "Bookmarked.",
            ["bm.toggle.removed"]           = "Bookmark removed.",

            // ── M21 (ADR 0122) — document management: the /documents feed +
            // detail + upload form (D5 standing, D6 download, D7 Deny → 404).
            // U04 authors the COMPLETE 12-key closed set; U03's views consume.
            ["documents.title"]             = "Documents",
            ["documents.empty"]             = "No documents are visible to you yet.",
            ["documents.upload"]            = "Upload a document",
            ["documents.upload_title"]      = "Upload a document",
            ["documents.upload_summary"]    = "One-line description (optional)",
            ["documents.upload_file"]       = "File",
            ["documents.upload_audience"]   = "Who can see this document",
            ["documents.upload.submit"]     = "Upload document",
            ["documents.download"]          = "Download",
            ["documents.detail.type_size"]  = "File type / size",
            ["documents.detail.updated"]    = "Last updated",
            ["documents.flash_uploaded"]    = "Document uploaded.",
            // ADR 0125 (U03) — the owner-only edit lane (D1 standing, D2 title/
            // summary, D3 file-replace, D4 audit-by-owner). The /documents/{id}
            // /edit form (GET + POST); the detail's "Edit" affordance.
            ["documents.edit"]              = "Edit",
            ["documents.edit_title"]        = "Edit a document",
            ["documents.edit_summary"]      = "One-line description (optional)",
            ["documents.edit_file"]         = "Replace the file (optional)",
            ["documents.edit_file_hint"]    = "Leave blank to keep the current file.",
            ["documents.edit_audience"]     = "Who can see this document",
            ["documents.edit.submit"]       = "Save changes",
            ["documents.flash_edited"]      = "Document updated.",

            // The "documents organization" lane (tags + folders) — the
            // documents.folder_* / documents.tags.* keys + the flash keys for
            // the DocumentFolderController routes (create / rename / move /
            // delete / document-move).
            ["documents.folder"]              = "Folder",
            ["documents.folder_unfiled"]      = "Unfiled",
            ["documents.folder_hint"]         = "File this document into a folder to keep the repository organized.",
            ["documents.folder_new"]          = "New folder",
            ["documents.folder_create"]       = "Create",
            ["documents.folder_name_placeholder"] = "Folder name",
            ["documents.tags"]                = "Tags",
            ["documents.tags_hint"]           = "Type to search existing tags, or start a new one.",
            ["documents.flash_moved"]         = "Document moved.",
            ["documents.folder_flash_created"] = "Folder created.",
            ["documents.folder_flash_renamed"] = "Folder renamed.",
            ["documents.folder_flash_moved"]   = "Folder moved.",
            ["documents.folder_flash_deleted"] = "Folder deleted.",

            // ── M22 (ADR 0132) — onboarding: the /onboarding guided
            // walk-through (D4) + the dismissible home/nav banner (D5) + the
            // finish/skip flash (D2/D4). U03 authors the COMPLETE closed set;
            // U02/U03 consume. The parity pin (KwLRegistryConsistencyTests +
            // KnownTranslationKeys_ParityTests) requires every key present,
            // non-empty, in all four languages (C-M22·6, GATE-6). ──
            ["onboarding.title"]            = "Set up your account",
            ["onboarding.intro"]            = "A quick guided tour of the few things that make Kumunita work for you. Everything links into the setting that already owns it — you can finish in a minute or come back any time.",
            ["onboarding.step_displayname"] = "Your display name",
            ["onboarding.step_avatar"]      = "Your avatar",
            ["onboarding.step_language"]    = "Your interface language",
            ["onboarding.step_timezone"]    = "Your time zone",
            ["onboarding.step_dateformat"]  = "Your date & time format",
            ["onboarding.step_email"]       = "Your email & notification language",
            ["onboarding.step_contact"]     = "Your contact details & who can see them",
            // M9 amendment (ADR 0139) — the messaging opt-in step, linking into
            // the /settings/messaging page that owns Profile.MessagingOptIn.
            ["onboarding.step_messaging"]   = "Whether you can use direct messaging",
            ["onboarding.visit"]            = "Go to this setting",
            ["onboarding.finish"]           = "I'm all set — finish setup",
            ["onboarding.skip"]             = "Skip for now",
            ["onboarding.flash_done"]       = "Setup complete — welcome to your neighborhood.",
            ["onboarding.banner.text"]      = "Finish setting up your account?",
            ["onboarding.banner.action"]    = "Start setup",

            // ── M9 amendment — the per-resident messaging control (the
            // /settings/messaging surface) + the guardian's ceiling (the
            // /me/children/{id} curation surface) ──
            ["settings.messaging.title"]           = "Messaging",
            ["settings.messaging.description"]     = "Choose whether other residents can send you direct 1:1 messages. Your choice is saved on your account and takes effect immediately.",
            ["settings.messaging.instance_off"]    = "Messaging is currently turned off on this instance by an administrator. You can opt in now and messaging will be available to you as soon as it is turned on.",
            ["settings.messaging.restricted"]      = "Messaging has been restricted on your account by a guardian. Contact them to change this.",
            ["settings.messaging.optin"]           = "Let others send me direct 1:1 messages",
            ["settings.messaging.save"]            = "Save messaging preference",
            ["guardian.messaging.title"]           = "Messaging",
            ["guardian.messaging.description"]     = "Choose whether this child can use direct 1:1 messaging. When restricted, the child cannot send or receive messages and the choice wins over their own opt-in; when allowed, the child decides for themselves on their own messaging settings page.",
            ["guardian.messaging.current_restricted"] = "Messaging is currently restricted for this child.",
            ["guardian.messaging.current_allowed"]    = "Messaging is currently allowed for this child.",
            ["guardian.messaging.child_optin_on"]     = "The child has opted in to messaging on their own account.",
            ["guardian.messaging.child_optin_off"]    = "The child has not opted in to messaging on their own account — even if you allow it, they will need to opt in on their own settings page.",
            ["guardian.messaging.allow"]              = "Allow messaging",
            ["guardian.messaging.restrict"]           = "Restrict messaging",

            // ── P1 audit (2026-10-04) ── the ~73 keys the P1 translation audit
            // found emitted unregistered: shared confirms, account
            // block/unblock, DataAnnotations fallbacks, the /languages admin +
            // translator surface, the events/posts/announcements/groups
            // confirms, guardian confirms, locale resets, the page composer,
            // the projects board/lane/to-do confirms, the inventory item
            // confirm, and the closed a11y set (server-side attributes).
            ["common.remove_translation_confirm"] =
                "Remove this translation?",
            ["events.skip_occurrence_confirm"] =
                "Skip this occurrence? You can restore it later.",
            ["community.confirm_remove_member"] =
                "Remove {0} from {1}?",
            ["a11y.notifications"]             = "Notifications",
            ["a11y.find_tag"]                  = "Find by tag",
            ["a11y.find_bio"]                  = "Find by bio",
            ["a11y.avatar"]                    = "Avatar image",
            ["a11y.board_actions"]             = "Board actions",
            ["a11y.calendar_view"]             = "Calendar view",
            ["a11y.event_time_range"]          = "Event time range",
            ["a11y.check_out_note"]            = "Check-out note",
            ["a11y.community_pages"]           = "Community pages",
            ["a11y.platform_pages"]            = "Platform pages",
            ["a11y.community_page_tree"]       = "Community page tree",
            ["a11y.platform_page_tree"]        = "Platform page tree",
            ["a11y.breadcrumb"]                = "Breadcrumb",
            ["a11y.about_features"]            = "What Kumunita is",
            ["a11y.about_audience"]            = "Who it's for",
            ["a11y.about_philosophy"]          = "The philosophy",
            ["a11y.about_contact"]             = "Get in touch",
            ["a11y.about_project"]             = "The project",
            ["a11y.set_limit"]                 = "Set limit on {0}",
            ["account.block"]               = "Block",
            ["account.unblock"]             = "Unblock",
            ["account.confirm_block"]       = "Block this account? It loses all standing until unblocked.",
            ["account.confirm_unblock"]     = "Unblock this account? Their standing will be restored.",
            ["account.err.required"]        = "The {0} field is required.",
            ["account.err.email"]           = "The {0} field is not a valid e-mail address.",
            ["account.err.password_min"]    = "{0} must be at least {1} characters.",
            ["account.err.password_mismatch"] = "The {0} and {1} fields do not match.",
            ["footer.feed"]                 = "The feed",
            ["languages.title"]             = "Languages",
            ["languages.lede_admin"]        =
                "Manage the languages this instance supports. Changes take effect on the next request — " +
                "no rebuild, no restart.",
            ["languages.lede_translator"]   =
                "Review the platform's translation coverage and update the UI strings. " +
                "Changes take effect on the next request.",
            ["languages.add_heading"]       = "Add a language",
            ["languages.code_label"]        = "Language code",
            ["languages.native_name_label"] = "Native name",
            ["languages.code_hint"]         = "Short code, e.g. pl for Polish, no-NO for Norwegian.",
            ["languages.supported_heading"] = "Supported languages",
            ["languages.th_code"]           = "Code",
            ["languages.th_native_name"]    = "Native name",
            ["languages.th_enabled"]        = "Enabled",
            ["languages.th_ui_strings"]     = "UI strings",
            ["languages.th_actions"]        = "Actions",
            ["languages.enabled"]           = "enabled",
            ["languages.disabled"]          = "disabled",
            ["languages.present"]           = "{n} present",
            ["languages.missing"]           = "{n} missing",
            ["languages.action_disable"]    = "Disable",
            ["languages.action_enable"]     = "Enable",
            ["languages.action_set_default"] = "Set default",
            ["languages.action_ui_strings"] = "UI strings",
            ["languages.action_remove"]     = "Remove",
            ["languages.reorder_btn"]       = "Reorder (confirm current order)",
            ["languages.reorder_title"]     = "Submit the current order (as shown below) as the new sort order",
            ["languages.reorder_hint"]      =
                "Drag-and-drop reordering is not available; the reorder form accepts the codes in the order " +
                "they appear here. To change the order, the admin should submit the list in the desired " +
                "sequence.",
            ["languages.confirm_remove"]    =
                "Remove {0}? Its translation rows are retained and will be restored if the language is " +
                "re-added.",
            ["translations.editor.back"]         = "← Back to languages",
            ["translations.editor.title"]        = "UI strings for {0}",
            ["translations.editor.lede"]         =
                "The full list of strings the platform shows to its residents. Each row shows the key, " +
                "its English reference text, and the current value in {0}. Saving a row adds or updates " +
                "its translation — visible on the next request.",
            ["translations.editor.mode_label"]   = "Editor mode",
            ["translations.editor.th_key"]       = "Key",
            ["translations.editor.th_en_reference"] = "English reference",
            ["translations.editor.th_value"]     = "Value in {0}",
            ["translations.editor.value_aria"]   = "Value for {0} in {1}",
            ["events.rsvp_status_going"]     = "Going",
            ["events.rsvp_status_maybe"]     = "Maybe",
            ["events.rsvp_status_no"]        = "No",
            ["posts.delete_confirm"]         = "Delete this post? Your replies will remain visible.",
            ["posts.reply_delete_confirm"]   =
                "Delete this reply? It will be replaced by a note. The record is kept.",
            ["announcements.delete_confirm"] = "Delete this announcement? This cannot be undone.",
            ["groups.confirm_delete"]        =
                "Delete this group? This removes the group, its members, and its pending invitations, " +
                "and cannot be undone.",
            ["community.confirm_leave"]      = "Leave {0}?",
            ["guardian.suspend_confirm"]     = "Suspend this child account? They will be blocked until you un-suspend them.",
            ["guardian.messaging.allow_confirm"] =
                "Allow messaging for this child? They will be able to send and receive direct messages " +
                "(their own opt-in must also be on).",
            ["guardian.messaging.restrict_confirm"] =
                "Restrict messaging for this child? They will no longer be able to send or receive direct " +
                "messages, regardless of their own opt-in.",
            ["guardian.handover_confirm"]    = "Hand over this account to the child? This dissolves your guardianship over it.",
            ["guardian.remove_myself_confirm"] =
                "Remove yourself as this child's guardian? You will no longer be " +
                "able to manage their account. They keep their account, and " +
                "their other guardian(s) continue.",
            ["guardian.delete_child_confirm"] =
                "Delete this child account permanently? This removes their sign-in, profile, " +
                "and memberships, and cannot be undone.",
            ["locale.reset_confirm"]         = "Reset your language preference to the instance default?",
            ["locale.email_reset_confirm"]   = "Reset your email &amp; notification language to the instance default?",
            ["settings.quiet.clear_confirm"] =
                "Clear your quiet hours? All notification emails will be sent immediately again.",
            ["settings.timezone_reset_confirm"] = "Reset your time zone to the platform default?",
            ["settings.dateformat_reset_confirm"] = "Reset your date &amp; time format to the platform default?",
            ["pages.form.body_hint"]         =
                "Optional — a folder node may have no body. Written in Markdown via the visual editor " +
                "(the same one posts and announcements use).",
            ["pages.reset_confirm"]          =
                "Reset this page to the seeded text? Any edits you made to the body (English) and to its " +
                "German / French / Danish translations will be overwritten with the seeded baseline. " +
                "The rest of the page (audience, parent, etc.) is untouched.",
            ["pl.board.delete_confirm"]      =
                "Delete this board? Its lanes and card placements are removed — the to-dos themselves " +
                "are kept.",
            ["pl.lane.delete_confirm"]       =
                "Delete this lane? Its cards come off this board — the to-dos themselves are kept.",
            ["pl.todo.assignee_remove_confirm"] = "Remove the assignee from this to-do?",
            ["pl.todo.move_confirm"]         = "Move this to-do to another board? It will no longer be on this board.",
            ["pl.todo.delete_confirm"]       = "Delete this to-do and its subtasks? This cannot be undone.",
            ["inv.item.delete_confirm"]      = "Delete this item? This cannot be undone.",
            ["a11y.close"]                   = "Close",
            ["a11y.toggle_nav"]              = "Toggle navigation",
            ["a11y.primary_nav"]             = "Primary",
            ["a11y.pagination"]              = "Pagination",
            ["a11y.pinned_announcement"]     = "Pinned announcement",
            ["a11y.banner_read_more"]        = "Read this announcement in full",
            ["a11y.banner_all"]              = "See all announcements",
            ["a11y.banner_dismiss"]          = "Dismiss this pinned announcement",
            ["a11y.onboarding_region"]       = "Account setup",
            ["a11y.onboarding_action"]       = "Start setup",
            ["a11y.onboarding_dismiss"]      = "Dismiss this banner",
            ["a11y.whatsnew_region"]         = "What's new",
            ["a11y.actions_for"]             = "Actions for {0}",
            ["a11y.search"]                  = "Search",
            ["a11y.scope"]                   = "Scope",
            ["a11y.back_to_messages"]        = "Back to messages",
            ["a11y.resident"]                = "Resident",
            ["account.storage_title"]        = "My storage",
            ["account.storage_lede"]         =
                "How much of your content the platform counts, your per-user quota, and how much of it " +
                "you still have left.",
            ["account.storage_used"]         = "your content used",
            ["account.storage_quota"]        = "your per-user quota",
            ["account.storage_remaining"]    = "remaining",
            ["account.storage_unlimited_note"] =
                "Your per-user quota is unlimited — there is no total-content cap on your uploads.",
            ["account.storage_remaining_note"] =
                "Your remaining is what's left of your per-user quota ({0}). Ask an administrator to " +
                "raise the quota if you need more.",
            ["translations.bulk.export_title"] = "Download this language's UI strings as a CSV file",
            ["common.delete"]          = "Delete",
            ["pages.delete_confirm"]   = "Delete this page?",

            // ── M26 U16 (the _Sort partial's closed sort.* set, the documents.*/
            // a11y.* precedent) — the label-only kw-l set the one shared
            // Views/Shared/_Sort.cshtml partial emits: exactly the eight sort
            // keys the U10–U15 surfaces' closed allowlists offer (created /
            // modified / title everywhere; size on documents; name on
            // inventory + people-find; start on events; due + status on todos).
            // No sort.label / sort.asc / sort.desc keys — the partial renders
            // per-option links (not direction toggles) and its group
            // aria-label stays hardcoded (the a11y.* attribute precedent:
            // value-free simple attributes are out of kw-l's reach here).
            // sort.priority is NOT a key — U12's drift pause: TodoItem has no
            // Priority property, so no surface offers it.
            ["sort.created"]          = "Created",
            ["sort.modified"]         = "Modified",
            ["sort.title"]            = "Title",
            ["sort.size"]             = "Size",
            ["sort.name"]             = "Name",
            ["sort.start"]            = "Start date",
            ["sort.due"]              = "Due date",
            ["sort.status"]           = "Status",
            ["admin.help.reset_one"]   =
                "Reset \"{0}\" to its seeded text? This overwrites any hand-edited copy.",
            ["admin.help.reset_all"]   =
                "Reset ALL {0} seeded help pages to their seeded text? This overwrites any hand-edited " +
                "copy on every page.",
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
            // ── M19 (ADR 0120) — die Gastkonten-Oberfläche: die /admin/guests
            // Admin-Oberfläche (D6) + die Begrüßung der angemeldeten Gäste ──
            ["admin.guests_title"]               = "Gastkonten",
            ["admin.guests_empty"]               = "Noch keine Gastkonten.",
            ["admin.guests_create"]              = "Gast erstellen",
            ["admin.guests_window_label"]        = "Zugriffsfenster",
            ["admin.guests_surfaces_label"]      = "Erlaubte Bereiche",
            ["admin.guests_surface_announcements"] = "Ankündigungen",
            ["admin.guests_surface_events"]      = "Veranstaltungen",
            ["admin.guests_surface_directory"]   = "Verzeichnis",
            ["admin.guests_saved"]               = "Gastzugang gespeichert.",
            ["account.guest_welcome"] =
                "Du bist als Gast angemeldet. Dein Zugriff ist auf die " +
                "Bereiche beschränkt, die die Verwaltung erlaubt hat, für " +
                "das von ihr gesetzte Zeitfenster.",

            // ── M20 (ADR 0121) — Benachrichtigungs-Stumstunden: die 5. Sektion
            // /settings/quiet (D7) + die /admin/quiet-Takt-Oberfläche (D8).
            // U06 authorisiert den vollständigen 15-Schlüssel-Satz; U06/U07
            // konsumieren, U07 fügt keine Schlüssel hinzu.
            ["settings.quiet.title"]        = "Stumstunden",
            ["settings.quiet.description"]  = "Wähle, wann deine Benachrichtigungs-E-Mails zurückgehalten " +
                "werden. Dein Stumstundenplan wird auf deinem Konto gespeichert, in " +
                "deiner eigenen Zeitzone angewendet — er wirkt beim nächsten " +
                "Absenden einer Benachrichtigung und betrifft nie andere Mitglieder.",
            ["settings.quiet.enabled"]      = "Benachrichtigungs-E-Mails in meinen Stumstunden zurückhalten",
            ["settings.quiet.mode_label"]   = "Wann sollen Benachrichtigungs-E-Mails zurückgehalten werden?",
            ["settings.quiet.mode_blocked"] = "Zurückgehalten während der gewählten Stunden & Tage",
            ["settings.quiet.mode_allowed"] = "Zurückgehalten außer in den gewählten Stunden & Tagen",
            ["settings.quiet.hours_label"]  = "Stunden des Tages",
            ["settings.quiet.days_label"]   = "Wochentage",
            ["settings.quiet.save"]         = "Stumstunden speichern",
            ["settings.quiet.flash_saved"]  = "Stumstunden gespeichert — zurückgehaltene E-Mails werden gesendet, sobald deine Stumstunden enden.",
            ["settings.quiet.flash_cleared"] = "Stumstunden entfernt — alle Benachrichtigungs-E-Mails werden jetzt sofort gesendet.",
            ["admin.quiet.title"]           = "Stumstunden-Takt",
            ["admin.quiet.cadence_label"]   = "Zurückgehaltene Benachrichtigungen alle (Minuten) erneut prüfen",
            ["admin.quiet.save"]            = "Takt speichern",
            ["admin.quiet.flash_saved"]     = "Stumstunden-Takt gespeichert.",

            // ── M28 (ADR 0151) — Betreuer-Nutzungslimits: der 13-Schlüssel-
            // guardian.timelimit.* GU-Detail-Abschnitt (D7) + der einzelne
            // account.time_limit.login_message Login-Landing (in U04s
            // Login.cshtml ?error=time-limit Fall referenziert). U05 autorisiert
            // den VOLLSTÄNDIGEN 14-Schlüssel-Satz; U06 konsumiert, fügt keine
            // hinzu. ABGEGRENZTER Namensraum — NICHT die M20 settings.quiet.*
            // / admin.quiet.* Schlüssel (die gehören der Benachrichtigungslane).
            ["guardian.timelimit.title"]        = "Nutzungslimits",
            ["guardian.timelimit.description"]  = "Wähle, wann dein Kind die Plattform nutzen darf. Der Plan " +
                "wird auf seinem Konto gespeichert, in seiner eigenen Zeitzone " +
                "angewendet — er wirkt bei seinem nächsten Anmelden und betrifft nie dich.",
            ["guardian.timelimit.enabled"]      = "Nutzungslimits für dieses Kind durchsetzen",
            ["guardian.timelimit.mode_label"]   = "Wann darf das Kind die Plattform nutzen?",
            ["guardian.timelimit.mode_blocked"] = "Gesperrt während der gewählten Stunden & Tage",
            ["guardian.timelimit.mode_allowed"] = "Nur erlaubt in den gewählten Stunden & Tagen",
            ["guardian.timelimit.hours_label"]  = "Stunden des Tages",
            ["guardian.timelimit.days_label"]   = "Wochentage",
            ["guardian.timelimit.save"]         = "Nutzungslimits speichern",
            ["guardian.timelimit.clear"]        = "Nutzungslimits entfernen",
            ["guardian.timelimit.flash_saved"]  = "Nutzungslimits gespeichert — das Kind wird außerhalb des erlaubten Fensters abgemeldet.",
            ["guardian.timelimit.flash_cleared"] = "Nutzungslimits entfernt — das Kind darf die Plattform jetzt jederzeit nutzen.",
            ["guardian.timelimit.badge_set"]    = "Nutzungslimits gesetzt",
            ["account.time_limit.login_message"] = "Du bist außerhalb deiner erlaubten Zeiten. Bitte melde dich später erneut an.",

            // ── nav (the shared top-nav, _Layout + _AccountNav) ─────────────
            ["nav.home"]          = "Start",

            // ADR 0111 — the nav-variant surface: the compact top-row variant
            // folds the less-frequent sections into one "More" menu, and the
            // resident picks a variant from the account menu (nav_variant.*
            // are the picker labels, the active one marked with a ✓).
            ["nav.more"]          = "Mehr",
            ["nav_variant.label"] = "Navigation",
            ["nav_variant.row"]   = "Obere Reihe",
            ["nav_variant.rail"]  = "Icon-Leiste",

            // ADR 0133 — the appearance (theme) picker labels.
            ["theme.label"]      = "Erscheinungsbild",
            ["theme.auto"]       = "Automatisch (wie das Gerät)",
            ["theme.light"]      = "Hell",
            ["theme.dark"]       = "Dunkel",

            // ── events (M4 — ADR 0054: the events nav entry + the Detail footer) ──
            ["nav.events"]        = "Veranstaltungen",
            ["events.created"]    = "Erstellt",
            ["events.edited"]     = "bearbeitet",

            // ADR 0119 (M18, D9) — the recurring-events key set. 14 keys, all under
            // the existing `events.*` namespace (the `events.created` / `events.edited`
            // entries above are the anchor; no new `event.*` / `recurrence.*` /
            // `series.*` namespace). Closed set × en/de/fr/da: every key below must
            // appear in all four dictionaries (see the closure test in
            // tests/Kumunita.Core.Tests/KnownTranslationKeysClosureTests.cs, U07).
            ["events.recurrence.none"]       = "Wiederholt sich nicht",
            ["events.recurrence.daily"]      = "Täglich",
            ["events.recurrence.weekly"]     = "Wöchentlich",
            ["events.recurrence.monthly"]    = "Monatlich",
            ["events.recurrence.yearly"]     = "Jährlich",
            ["events.recurrence.interval"]   = "Alle",
            ["events.recurrence.ends_after"] = "Endet nach",
            ["events.recurrence.ends_on"]    = "Endet am",
            ["events.recurrence.count"]      = "Terminen",
            ["events.recurrence.until"]      = "bis",
            ["events.series.repeats"]        = "Wiederholt sich",
            ["events.series.skip"]           = "Diesen Termin überspringen",
            ["events.series.restore"]        = "Diesen Termin wiederherstellen",
            ["events.series.part_of"]        = "Teil einer Serie",

            // ── projects (M5 — ADR 0067: the to-do surface nav entry + labels) ──
            ["nav.projects"]                 = "Projekte",
            ["projects.todo.title"]          = "Aufgaben",
            ["projects.todo.lede"]           = "Die gemeinsamen Aufgaben der Nachbarschaft — Arbeit einem Nachbarn zuweisen, in Unteraufgaben aufteilen und auf einem Board ablegen.",
            ["projects.todo.new"]            = "Neue Aufgabe",
            ["projects.todo.new_lead"]       = "Erstelle eine Aufgabe, weise sie optional einem Nachbarn zu und — falls nötig — teile sie in Unteraufgaben auf oder lege sie auf ein Board. Standardmäßig ist sie für alle sichtbar; deaktiviere das im Abschnitt „Zielgruppe“, wenn du einschränken willst.",
            ["projects.todo.title_hint"]     = "Ein kurzer Name für die Aufgabe — die Kartenbeschriftung.",
            ["projects.todo.status"]         = "Status",
            ["projects.todo.status_assignee_hint"] = "Der Status ist ein freier Text (eine Zeichenkette, keine feste Liste). Eine Aufgabe zuzuweisen gibt diesem Bewohner Handhabung darüber — Anzeige + Handhabung, nie eine Zugangsgrenze.",
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
            ["projects.todo.filter_assigned_to_me"] = "Mir zugewiesen",
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
            // ADR 0106 — Selbstzuweisungs-Spur + Kartendetailaufklapper.
            ["projects.todo.assign_to_me"]   = "Mir zuweisen",
            ["projects.todo.details"]        = "Details",
            ["projects.todo.community"]      = "Community",
            ["projects.todo.subtasks"]       = "Unteraufgaben",
            ["projects.todo.boards"]         = "Boards",
            // ADR 0100 — Kommentare + Antworten (C-M3·1 — Kommentare erben die
            // einzelne Read-Entscheidung der Aufgabe).
            ["projects.todo.comments"]       = "Kommentare",
            ["projects.todo.comment_empty"]  = "Noch keine Kommentare. Wenn du diese Aufgabe sehen kannst, kannst du sie kommentieren.",
            ["projects.todo.comment_reply"]  = "Kommentar",
            ["projects.todo.comment_submit"] = "Kommentieren",
            ["projects.todo.comment_deleted"] = "Dieser Kommentar wurde von seinem Autor gelöscht.",
            ["projects.todo.comment_delete"] = "Löschen",
            ["projects.todo.comment_audience_note"] = "Kommentare haben kein eigenes Publikum — sie sind unter der einzelnen Publikumsentscheidung dieser Aufgabe sichtbar. Du kommentierst nur dort, wo die Aufgabe selbst sichtbar ist.",
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
            ["projects.board.edit_lead"]     = "Aktualisiere Titel, Beschreibung und Zielgruppe dieses Boards. Gemeinschaft und Sprache sind bei der Erstellung festgelegt.",
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
            ["projects.board.lane.delete"] = "Lane löschen",
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
            ["common.full_page"]    = "Vollseite",
            ["common.exit_full_page"] = "Vollseite beenden",
            ["common.open_full_page"] = "Vollseite öffnen",
            ["common.fullscreen"]   = "Vollbild",
            ["common.exit_fullscreen"] = "Vollbild beenden",
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
            ["admin.roles_independent_hint"] = "Unabhängig — ein Bewohner kann beliebig viele Rollen kombinieren. Nichts angekreuzt = ein einfacher Member.",
            ["admin.moderator_scope"]       = "Moderator-Bereich",
            ["admin.moderator_scope_hint"]  = "Die Gemeinschaften, die dieses Konto moderieren darf. Nur relevant, wenn die Moderator-Rolle angehakt ist — die Plattform löscht die Scope-Auswahl, wenn die Moderator-Rolle aus ist.",
            ["nav.announcements"] = "Ankündigungen",
            ["nav.community"]     = "Gemeinschaft",
            ["nav.groups"]        = "Gruppen",
            ["nav.pages"]         = "Seiten",
            ["nav.tags"]          = "Tags",
            ["nav.directory"]     = "Verzeichnis",
            ["nav.people"]        = "Menschen",
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
            ["guardian.community_block_note"] =
                "Wähle aus, auf welche Gemeinschaften dieses Kind Zugriff hat. " +
                "Eine blockierte Gemeinschaft wird ihm verborgen — " +
                "einschließlich ihrer Beiträge — selbst wenn sie eine der " +
                "Pflichtgemeinschaften ist, der alle angehören. Du entscheidest, " +
                "wer eine Gemeinschaft beitreten darf, indem du ihn einlädst oder " +
                "über einen Admin, aber du kannst eine Gemeinschaft verstecken, " +
                "die dieses Kind nicht sehen soll.",
            ["guardian.community_blocked"]   = "Blockiert & verborgen",
            ["guardian.community_unblock"]   = "Freigeben & anzeigen",
            ["guardian.community_block"]     = "Zugriff blockieren & verstecken",
            ["guardian.pending_invitations"] = "Ausstehende Gruppeneinladungen",
            ["guardian.no_invitations"]      = "Keine ausstehenden Einladungen.",
            ["guardian.approve"]             = "Genehmigen",
            ["guardian.handover"]            = "Konto übergeben",
            ["guardian.handover_hint"]       =
                "Die Aufhebung der Vormundschaft übergibt das Konto dem Kind. " +
                "Die Mitgliedschaften bleiben erhalten, und die eigenen " +
                "Einstellmöglichkeiten kommen beim nächsten Lesen zurück.",
            ["guardian.dissolve"]            = "Vormundschaft auflösen",
            // Count-aware steering (ADR 0028 §G·6) — de.
            ["guardian.remove_myself"]       = "Ich nehme meine Vormundstellung zurück",
            ["guardian.remove_myself_hint"]  =
                "Du bist einer von mehreren Vormündern dieses Kindes. " +
                "Wenn du deine Vormundstellung zurücknimmst, enden deine " +
                "Befugnisse über das Konto; die übrigen Vormünder bleiben " +
                "dabei, und das Konto bleibt erhalten.",
            ["guardian.remove_myself_submit"] = "Ich nehme mich zurück",
            ["guardian.suspended"]           = "Gesperrt",
            ["guardian.unsuspend"]           = "Wieder aktivieren",
            ["guardian.suspend"]             = "Sperren",
            ["guardian.delete_child"]        = "Das Kind-Konto löschen",
            ["guardian.delete_child_lede"]   =
                "Das Löschen entfernt die Anmeldung, das Profil und die Gruppen- und " +
                "Gemeinschaftsmitgliedschaften des Kindes und löst jede andere " +
                "Vormundschaft über das Konto auf. Ihre früheren Aktionen in der " +
                "Prüfspur bleiben erhalten, wobei ihre Identität durch einen " +
                "Platzhalter ersetzt wird. Das kann nicht rückgängig gemacht werden.",
            ["guardian.delete_child_confirm_checkbox"] = "Ich verstehe, dass das Kind-Konto dauerhaft gelöscht wird.",
            ["guardian.delete_child_submit"] = "Konto löschen",
            ["guardian.display_name"]        = "Anzeigename",
            ["guardian.email"]               = "E-Mail-Adresse",
            ["guardian.password"]            = "Passwort",
            ["guardian.child_email_hint"]    =
                "Das Kind öffnet den Bestätigungslink in seiner E-Mail, um sein eigenes Passwort festzulegen und sich anzumelden — du brauchst sein Passwort nicht (und setzt es nicht).",
            ["guardian.consent.intro"]       =
                "Mit der Erstellung dieses Profils bestätigst du, dass du die " +
                "gesetzliche Vertretung dieses Kindes bist. Als sein " +
                "Vormund behältst du die volle Kontrolle über sein Konto:",
            ["guardian.consent.duties_invitations"] =
                "Du musst alle Gruppen- und Eventeinladungen genehmigen oder " +
                "ablehnen.",
            ["guardian.consent.duties_chat"] =
                "Du kannst die Chatfunktionen für dieses Profil jederzeit " +
                "einschalten oder ausschalten.",
            ["guardian.consent.duties_data"] =
                "Diese Daten sind vollständig auf die eigene Instanz der " +
                "Gemeinschaft isoliert und werden niemals verkauft, zur " +
                "Profilbildung genutzt oder für Werbung verwendet.",
            ["guardian.consent.checkbox"]    =
                "Ich stimme der Verarbeitung der Daten meines Kindes unter " +
                "diesen Bedingungen zu.",

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
            ["guardian.assign.success"]      = "Vormund zugewiesen — sie werden zur Annahme aufgefordert.",

            // ── guardian acceptance lane (GA ADR 0038 §F) ──────────────────
            ["guardian.pending"]              = "Ausstehend",
            ["guardian.pendingRequests.title"] =
                "Vormundsanträge, die auf deine Annahme warten",
            ["guardian.pendingRequests.lead"] =
                "Ein anderer Vormund hat dich gebeten, Co-Vormund für eines ihrer Kinder zu werden. " +
                "Akzeptiere (und stimme den Bedingungen für Kinderkonten zu) oder lehne ab — bis du handelst, hältst du keine Rechte über das Konto.",
            ["guardian.pendingRequests.child"]    = "Kind",
            ["guardian.pendingRequests.conferrer"] = "Angefragt von",
            ["guardian.accept"]                   = "Annehmen & den Bedingungen zustimmen",
            ["guardian.accept.consent.intro"]     =
                "Indem du akzeptierst, bestätigst du, dass du ein gesetzlicher Vormund dieses Kindes bist. " +
                "Als ihr Vormund behältst du die volle Kontrolle über ihr Konto:",
            ["guardian.accept.consent.duties_invitations"] =
                "Du musst alle Gruppen- und Event-Einladungen genehmigen oder ablehnen.",
            ["guardian.accept.consent.duties_chat"] =
                "Du kannst die Chat-Funktionen für dieses Profil jederzeit aktivieren oder deaktivieren.",
            ["guardian.accept.consent.duties_data"] =
                "Diese Daten sind vollständig isoliert auf der eigenen Instanz dieser Community und werden nie verkauft, profilisiert oder für Werbung verwendet.",
            ["guardian.accept.consent.checkbox"] =
                "Ich stimme der Verarbeitung der Daten dieses Kindes unter diesen Bedingungen zu.",
            ["guardian.accept.consent.required"] =
                "Du musst den Bedingungen für Kinderkonten zustimmen, bevor du akzeptierst.",
            ["guardian.decline"] = "Ablehnen",

            // ── The lane — event attendance (the guardian's three postures) ──
            ["guardian.eventrsvp.title"] = "Event-Teilnahme",
            ["guardian.eventrsvp.description"] =
                "Lege fest, wie die Event-Teilnahme dieses Kindes gehandhabt wird.",
            ["guardian.eventrsvp.mode_approves_active"] =
                "Aktuell: Vormund genehmigt — Ich genehmige oder lehne jedes Event ab, an dem das Kind teilnehmen möchte.",
            ["guardian.eventrsvp.mode_notifies_active"] =
                "Aktuell: Vormund wird informiert — Das Kind nimmt frei teil; ich werde informiert und                 kann jede Teilnahme danach rückgängig machen.",
            ["guardian.eventrsvp.mode_childdecides_active"] =
                "Aktuell: Kind entscheidet — Das Kind wählt seine Teilnahme selbst; keine Genehmigung und keine Benachrichtigung.",
            ["guardian.eventrsvp.switch_to_approves"] = "Auf „Vormund genehmigt“ umstellen",
            ["guardian.eventrsvp.switch_to_notifies"] = "Auf „Vormund wird informiert“ umstellen",
            ["guardian.eventrsvp.switch_to_childdecides"] = "Auf „Kind entscheidet“ umstellen",
            ["guardian.eventrsvp.pending_title"] = "Ausstehende Teilnahmeanfragen",
            ["guardian.eventrsvp.pending_empty"] = "Keine ausstehenden Teilnahmeanfragen.",
            ["guardian.eventrsvp.desired"] = "Möchte teilnehmen",
            ["guardian.eventrsvp.approve"] = "Genehmigen",
            ["guardian.eventrsvp.deny"] = "Ablehnen",
            ["guardian.eventrsvp.rsvps_title"] = "Aktuelle Teilnahme dieses Kindes",
            ["guardian.eventrsvp.rsvps_empty"] = "Keine aktuelle Teilnahme zum Entfernen.",
            ["guardian.eventrsvp.veto"] = "Entfernen",
            ["guardian.eventrsvp.approve_confirm"] =
                "Dieses Kind für dieses Event zur Teilnahme genehmigen?",
            ["guardian.eventrsvp.deny_confirm"] =
                "Dieses Kind für dieses Event ablehnen? Es wird nicht teilnehmen.",
            ["guardian.eventrsvp.veto_confirm"] =
                "Die Teilnahme dieses Kindes an diesem Event entfernen? Ihre Anmeldung wird gelöscht.",

            // ── notification kinds (the lane) ────────────────────────────────
            ["notifications.kind.guardian.event_request"] =
                "Teilnehmungsanfrage von deinem Kind",
            ["notifications.preference.guardian.event_request.label"] =
                "Wenn dein Kind an einem Event teilnehmen möchte",
            ["notification.guardian.event_request.subject"] =
                "Dein Kind möchte an einem Event teilnehmen",
            ["notification.guardian.event_request.body"] =
                "Dein Kind möchte an einem Event teilnehmen: ",
            ["notifications.kind.guardian.event_rsvp"] =
                "Dein Kind hat an einem Event teilgenommen",
            ["notifications.preference.guardian.event_rsvp.label"] =
                "Wenn dein Kind an einem Event teilnimmt",
            ["notification.guardian.event_rsvp.subject"] =
                "Dein Kind hat an einem Event teilgenommen",
            ["notification.guardian.event_rsvp.body"] =
                "Dein Kind hat an einem Event teilgenommen: ",

            // ── notification kind (GA ADR 0038 §F) ──────────────────────────
            ["notifications.kind.guardian.assign"] =
                "Ein Vormund hat dich gebeten, Co-Vormund zu werden",
            ["notifications.preference.guardian.assign.label"] =
                "Wenn ein Vormund dich bittet, Co-Vormund zu werden",
            ["notification.guardian.assign.subject"] =
                "Ein Vormund hat dich gebeten, Co-Vormund zu werden",
            ["notification.guardian.assign.body"] =
                "Ein Vormund hat dich gebeten, Co-Vormund für ihr Kind zu werden: ",

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

            ["settings.pagesize_title"]        = "Einträge pro Seite",
            ["settings.pagesize_lede"]         =
                "Lege fest, wie viele Einträge jede Liste pro Seite zeigt (Feeds, Veranstaltungen, Projekte und der Rest). " +
                "Deine Auswahl wird auf deinem Konto gespeichert — sie wirkt ab dem nächsten Laden einer Liste und " +
                "betrifft nie andere Bewohner.",
            ["settings.pagesize_label"]        = "Einträge pro Seite",
            ["settings.pagesize_default_marker"] = "— Plattform-Voreinstellung",
            ["settings.pagesize_reset_confirm"]  = "Einträge pro Seite auf die Plattform-Voreinstellung zurücksetzen?",
            ["settings.pagesize_reset"]        = "Auf Plattform-Voreinstellung zurücksetzen",
            ["settings.pagesize_save"]         = "Speichern",
            ["settings.pagesize_flash_set"]    = "Einträge pro Seite auf \"{0}\" gesetzt — es wirkt ab der nächsten Anfrage.",
            ["settings.pagesize_flash_reset"]  = "Einträge pro Seite zurückgesetzt — die Plattform-Voreinstellung wird verwendet.",

            // ── settings — home page (hide-home-intro preference, ADR 0149) ─
            ["settings.home_title"]        = "Startseite",
            ["settings.home_lede"]         =
                "Die Startseite öffnet sich mit zwei Intro-Abschnitten (was Kumunita ist und was es kann) " +
                "vor dem Feed. Aktiviere diese Option, um direkt zum Feed zu kommen. " +
                "Deine Auswahl wird auf deinem Konto gespeichert und betrifft nie andere Bewohner.",
            ["settings.home_label"]        = "Intro-Abschnitte ausblenden und mir sofort den Feed zeigen",
            ["settings.home_note"]         = "Wenn aus, zeigt die Startseite ihre Intro-Abschnitte wie üblich.",
            ["settings.home_save"]         = "Speichern",
            ["settings.home_flash_hide"]   = "Startseite aktualisiert — die Intro-Abschnitte werden ausgeblendet, der Feed kommt zuerst.",
            ["settings.home_flash_show"]   = "Startseite aktualisiert — die Intro-Abschnitte werden wieder gezeigt.",

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

            // ── admin — announcement-comments gate (ADR 0101) ────────────────
            ["admin.anncomments_title"]     = "Ankündigungen kommentieren",
            ["admin.anncomments_lede"]      =
                "Ob angemeldete Anwohner:innen Ankündigungen kommentieren dürfen. " +
                "Geschlossen werden die Kommentarliste und das Eingabefeld auf " +
                "jeder Ankündigung ausgeblendet — niemand kann mehr einen " +
                "Kommentar hinzufügen. Besucher:innen konnten nie kommentieren, " +
                "und bereits vorhandene Kommentare bleiben erhalten — das " +
                "Schalterfeld steuert nur neue Kommentare.",
            ["admin.anncomments_on"]        = "Offen — angemeldete Anwohner:innen können kommentieren",
            ["admin.anncomments_off"]       = "Geschlossen — keine:r kann einen Kommentar hinzufügen",
            ["admin.anncomments_save"]      = "Speichern",

            // ── admin — Direkt-Nachrichten-Schalter (M9, ADR 0105) ─────────
            ["admin.messaging_title"]   = "Direktnachrichten",
            ["admin.messaging_lede"]    =
                "Ob angemeldete Anwohner:innen direkte 1:1-Gespräche eröffnen dürfen. " +
                "Geschlossen wird der Nachrichten-Eintrag ausgeblendet und jede " +
                "Gesprächs-Anfrage abgelehnt — niemand kann ein Gespräch eröffnen " +
                "oder eine Nachricht senden. Der Schalter ist standardmäßig aus; " +
                "er steuert nur neue Nachrichten, und bestehende Gespräche und " +
                "Nachrichten bleiben erhalten. Auch eine:r GlobalAdmin ohne " +
                "Gesprächsteilnahme kann ein Gespräch nicht lesen.",
            ["admin.messaging_on"]      = "Offen — angemeldete Anwohner:innen können sich direkt Nachrichten schreiben",
            ["admin.messaging_off"]     = "Geschlossen — niemand kann Gespräche eröffnen oder Nachrichten senden",

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
            ["home.roadmap.show_more_earlier"] = "Die {n} früheren Meilensteine anzeigen",
            ["home.roadmap.show_more_upcoming"]  = "Die {n} kommenden Meilensteine anzeigen",
            ["home.roadmap.status.done"]    = "Fertig",
            ["home.roadmap.status.next"]    = "In Arbeit",
            ["home.roadmap.status.planned"] = "Geplant",

            // ── Client-JS-Strings (P0-6: #kumunita-strings-Bundle) ────────
            ["common.close"]  = "Schließen",
            ["common.cancel"] = "Abbrechen",
            ["rc.editor.error_generic"] = "Etwas ist schiefgelaufen.",
            ["rc.editor.link.title"]    = "Link einfügen",
            ["rc.editor.link.url"]      = "URL",
            ["rc.editor.link.confirm"]  = "Link einfügen",
            ["rc.editor.link.busy"]     = "Wird eingefügt…",
            ["rc.editor.link.err_empty"]    = "Gib eine URL ein.",
            ["rc.editor.link.err_invalid"]  = "Gib einen gültigen Link ein (Webadresse, E-Mail oder Pfad relativ zur Seite).",
            ["rc.editor.image.title"]   = "Bild bearbeiten",
            ["rc.editor.image.err_rejected"] = "Die hochgeladene Bildquelle wurde abgelehnt.",
            ["rc.editor.image.err_upload"]   = "Der Upload ist fehlgeschlagen.",
            ["rc.editor.attach.title"]  = "Datei anhängen",
            ["rc.editor.attach.file"]   = "Datei",
            ["rc.editor.attach.link_text"] = "Linktext",
            ["rc.editor.attach.default_label"] = "Anhang",
            ["rc.editor.attach.confirm"]  = "Anhängen",
            ["rc.editor.attach.busy"]     = "Wird hochgeladen…",
            ["rc.editor.attach.err_no_file"] = "Wähle eine Datei zum Anhängen aus.",
            ["img.edit.close"]       = "Schließen",
            ["img.edit.crop_area"]   = "Zuschneidebereich",
            ["img.edit.width"]       = "Breite",
            ["img.edit.output"]      = "Ergebnis",
            ["img.edit.reset_crop"]  = "Zuschneiden zurücksetzen",
            ["img.edit.use_original"] = "Original verwenden",
            ["img.edit.apply"]       = "Anwenden",
            ["img.edit.title"]       = "Dein Avatar zuschneiden",
            ["img.edit.err_edit"]    = "Die Bearbeitung ist fehlgeschlagen.",
            ["img.edit.err_could"]   = "Das Bild konnte nicht bearbeitet werden.",
            ["img.edit.err_load"]    = "Das Bild konnte nicht zum Bearbeiten geladen werden.",
            ["img.edit.err_export"]  = "Der Bild-Export ist fehlgeschlagen.",
            ["tag.suggest.remove_prefix"] = "Tag entfernen: ",
            ["notif.fallback"]       = "Benachrichtigungen",

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

            // ── ADR 0138 — das Passwort-Wechseln (de) ──
            ["account.change_password_title"] = "Passwort ändern",
            ["account.change_password_lede"] =
                "Wähle ein neues Passwort für dein Konto. Nach dem Speichern wirst du " +
                "abgemeldet und musst dich mit dem neuen Passwort erneut anmelden.",
            ["account.change_password_current"] = "Aktuelles Passwort",
            ["account.change_password_new"] = "Neues Passwort",
            ["account.change_password_confirm_new"] = "Neues Passwort bestätigen",
            ["account.change_password_submit"] = "Passwort ändern",
            ["account.change_password_locked_title"] = "Passwortänderungen sind gesperrt",
            ["account.change_password_locked_body"] =
                "Dies ist ein Demo-Konto und Passwortänderungen sind vom " +
                "Administrator gesperrt, damit alle die gemeinsamen Zugangsdaten " +
                "weiter nutzen können. Alle anderen Funktionen der Plattform kannst du " +
                "weiterhin verwenden.",
            ["account.change_password_back"] = "Zurück zu deinem Profil",

            ["nav.change_password"] = "Passwort ändern",

            // ── ADR 0142 — das Konto-Löschen (de) ──
            ["account.delete_title"] = "Konto löschen",
            ["account.delete_lede"] =
                "Wenn du dein Konto löschst, werden deine Anmeldung, dein " +
                "Profil und deine Gruppen- und Gemeinschaftsmitgliedschaften " +
                "entfernt. Deine früheren Handlungen im Prüfungsverzeichnis " +
                "der Plattform bleiben erhalten — mit einer Platzhalter- " +
                "Kennung anstelle deiner Identität (Datenschutzerklärung der " +
                "Plattform, OPS.md §9). Das kann nicht rückgängig gemacht " +
                "werden.",
            ["account.delete_password"] = "Passwort",
            ["account.delete_confirm_checkbox"] =
                "Ich verstehe, dass mein Konto endgültig gelöscht wird und " +
                "dies nicht rückgängig gemacht werden kann.",
            ["account.delete_submit"] = "Konto löschen",
            ["account.delete_refused"] =
                "Die Selbstlösch-Option steht nur Global-Administratoren " +
                "zur Verfügung. Ein Bewohner ohne Global-Admin-Stellung kann " +
                "sein eigenes Konto nicht löschen — wende dich an einen " +
                "Administrator, um das Konto entfernen zu lassen.",

            ["nav.delete_account"] = "Konto löschen",

            ["admin.delete_account_label"] = "Konto löschen",
            ["admin.delete_account_confirm"] =
                "Dieses Konto endgültig löschen? Das Prüfungsverzeichnis wird " +
                "beibehalten (pseudonymisiert); das Konto, das Profil und die " +
                "Mitgliedschaften werden entfernt. Das kann nicht rückgängig " +
                "gemacht werden.",

            ["admin.sample_title"] = "Beispieldaten",
            ["admin.sample_lede"] =
                "Diese Instanz führt die Demo-Nachbarschaft (Beispieldaten) aus. " +
                "Sperre die Beispielkonten, um ihr eigenes Passwort zu ändern, damit " +
                "Besucher Funktionen testen können, ohne die gemeinsamen Zugangsdaten " +
                "zu brechen — das Demo-Admin behält seinen eigenen Passwort-Zugang.",
            ["admin.sample_lock_label"] = "Passwortänderungen der Beispielkonten",
            ["admin.sample_lock_on"] = "Gesperrt — Beispielkonten können ihr Passwort nicht ändern",
            ["admin.sample_lock_off"] = "Nicht gesperrt — Beispielkonten können ihr Passwort ändern",
            ["account.login.error.blocked"] =
                "Dein Konto wurde vorübergehend gesperrt. Wende dich an einen Administrator.",
            ["account.login.error.removed"] =
                "Dein Konto wurde entfernt. Wende dich an einen Administrator.",
            ["account.login.error.role_changed"] =
                "Deine Rolle wurde geändert. Bitte melde dich erneut an.",

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
                "Diese Auswahl ist ausgeblendet, solange „Alle in dieser " +
                "Gemeinschaft“ an ist — der Beitrag ist für alle in der " +
                "Gemeinschaft sichtbar. Schalte sie ab, um mit den Auswahl " +
                "hier zu bestimmen, wer ihn sehen kann.",
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
            ["groups.danger_heading"]   = "Gefahrenzone",
            ["groups.danger_delete_hint"] =
                "Das Löschen der Gruppe entfernt sie, ihre Mitglieder und ausstehende Einladungen. Dies kann nicht rückgängig gemacht werden.",
            ["groups.danger_delete_button"] = "Diese Gruppe löschen",
            ["groups.new_title"]      = "Beitrag in dieser Gruppe",
            ["groups.new_back"]       = "zurück zur Gruppe",
            ["groups.new_submit"]     = "In die Gruppe posten",
            // ADR 0089 (GE) — the group-events lane (de).
            ["groups.back"]           = "Zurück zu",
            ["groups.events_heading"] = "Veranstaltungen",
            ["groups.new_event"]      = "Neue Veranstaltung",
            ["groups.events_empty_can"] =
                "Noch keine Veranstaltungen. Plane die erste — sie ist den aktuellen Mitgliedern sichtbar.",
            ["groups.events_empty"]    = "Hier sind noch keine Veranstaltungen.",
            ["groups.new_event_title"] = "Veranstaltung für diese Gruppe",
            ["groups.new_event_lead"]  =
                "Deine Veranstaltung ist nur den aktuellen Mitgliedern dieser Gruppe sichtbar.",
            ["groups.new_event_submit"] = "Veranstaltung zur Gruppe hinzufügen",
            ["groups.edit_event_title"] = "Diese Veranstaltung bearbeiten",
            // ── groups list (the Airy layout — the invitation panel + the
            //    member-count word on each group card) ───────────────────────
            ["groups.invitations"]    = "Einladungen",
            ["groups.invitations_pending"] = "ausstehend",
            ["groups.invited_by"]     = "Eingeladen von",
            ["groups.invite_accept"]  = "Annehmen",
            ["groups.invite_decline"] = "Ablehnen",
            // ── ADR 0094 — the resident self-initiated join-request lane ──
            ["groups.join_requests"]              = "Beitrittsanfragen",
            ["groups.join_requests_pending"]      = "ausstehend",
            ["groups.join_requests_note"]         = "Warten auf den Gruppeninhaber.",
            ["groups.join_withdraw"]              = "Zurückziehen",
            ["groups.other_public"]               = "Weitere öffentliche Gruppen",
            ["groups.request_join"]               = "Beitritt anfragen",
            ["groups.join_requests_pending_heading"] = "Ausstehende Beitrittsanfragen",
            ["groups.join_approve"]               = "Genehmigen",
            ["groups.join_decline"]               = "Ablehnen",
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
            ["upload.max_size"] = "Maximale Dateigröße: {0}",
            ["profile.avatar_hint"] =
                "JPEG, PNG, WebP oder GIF. Speichern ersetzt den " +
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

            // ── M23 (U04) — erweiterte Profil-Editor + Verzeichnis-Details ──
            ["profile.edit.bio"] = "Bio",
            ["profile.edit.tags"] = "Tags",
            ["profile.edit.tags.placeholder"] = "z. B. Gärtnern, Backen, Radfahren…",
            ["profile.detail.bio"] = "Über mich",
            ["profile.detail.tags"] = "Interessen & Fähigkeiten",
            ["profile.detail.tags.empty"] = "Keine Tags.",
            ["profile.flash.saved"] = "Profil aktualisiert.",

            // ── M23 (U05) — die /people Personen-Find-Oberfläche ─────────
            ["profile.find.title"]   = "Menschen finden",
            ["profile.find.by_tag"]  = "Nach Tag finden",
            ["profile.find.by_bio"]  = "Nach Bio finden",
            ["profile.find.results"] = "{0} Personen gefunden",
            ["profile.find.empty"]   = "Niemand passt — noch nicht.",

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
                "der einzelnen Publikumsentscheidung dieses Beitrags sichtbar. " +
                "Du antwortest nur dort, wo der Beitrag selbst sichtbar ist.",
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
            ["pages.section_community"] = "Seiten der Gemeinschaft",
            ["pages.section_platform"]  = "Plattformseiten",
            ["pages.section_platform_lede"] =
                "Von der Plattform veröffentlicht — Nutzungsbedingungen, Hilfe, " +
                "Datenschutz und Verhaltenskodex.",

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
            // M9 amendment (ADR 0139) — "Send a message" (two-sided gate).
            ["directory.send_message"] = "Nachricht senden",
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
            // ADR 0146 — die Handübergabe für ein Kind-Konto (die
            // Bestätigungsseite nimmt das Passwort des Kindes selbst ab,
            // bevor das Konto aktiviert wird).
            ["account.verify_set_password_lede"] =
                "Lege das Passwort für dieses Konto fest — du bist der, der es verwenden wird.",
            ["account.verify_set_password_submit"] = "Mein Passwort festlegen & anmelden",
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

            // ── admin (Analytics page — M13, ADR 0114 D4) ─────────────────
            ["admin.analytics_title"] = "Verwendungsanalytik",
            ["admin.analytics_lede"] =
                "Eine lokale Zusammenfassung der Plattfornutzung — Anfragen, " +
                "angemeldet / anonym, eindeutige Konten und Oberflächen-Ranking " +
                "über ein festes Zeitfenster. Keine Kontodetails; die rohen " +
                "Zeilen sind nur in der Datenbank des Betreibers verfügbar.",
            ["admin.analytics_window"] = "Fenster",
            ["admin.analytics_total"] = "Gesamtanfragen",
            ["admin.analytics_authenticated"] = "Angemeldet",
            ["admin.analytics_anonymous"] = "Anonym",
            ["admin.analytics_distinct"] = "Eindeutige Konten",
            ["admin.analytics_surface"] = "Oberfläche",
            ["admin.analytics_count"] = "Anzahl",
            ["admin.analytics_export"] = "CSV exportieren",

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

            // ── announcement comments (ADR 0101) ─────────────────────────────
            ["announcements.comments"] = "Kommentare",
            ["announcements.comment_empty"] = "Noch keine Kommentare. Schreib als Erster etwas.",
            ["announcements.comment_deleted"] = "Dieser Kommentar wurde von seiner Autorin bzw. seinem Autor gelöscht.",
            ["announcements.comment_delete"] = "Löschen",
            ["announcements.comment_reply"] = "Kommentar schreiben",
            ["announcements.comment_submit"] = "Kommentieren",
            ["announcements.comment_audience_note"] =
                "Kommentare sind nur für angemeldete Anwohner:innen sichtbar — " +
                "auch bei einer öffentlichen Ankündigung — und folgen dem " +
                "eigenen Publikum dieser Ankündigung (eine an eine " +
                "Community gerichtete ist deren Anwohner:innen sichtbar).",

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

            // ── what's new (the VN lane, ADR 0110 — version changelog + toast) ──
            ["whatsnew.eyebrow"]        = "Was ist neu",
            ["whatsnew.heading"]        = "Was ist neu — Version für Version",
            ["whatsnew.lead"] =
                "Jede Veröffentlichung ist hier mit Datum gelistet — lies nach, " +
                "was in jeder Minor-Version der Plattform, die du nutzt, gelandet ist.",
            ["whatsnew.version"]        = "Version",
            ["whatsnew.show_more"]      = "Weitere Versionen anzeigen",
            ["whatsnew.show_more_remaining"] = "Die {n} älteren Versionen anzeigen",
            ["whatsnew.toast_label"]    = "Kumunita {v} ist jetzt im Einsatz.",
            ["whatsnew.toast_see"]      = "Was ist neu?",

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
            ["events.mine.show_more"] = "Weitere {n} Veranstaltungen anzeigen",

            // ── events.past (der EV-PAST-Umschalter + Leerzustand auf /events — ADR 0109) ──
            ["events.upcoming"] = "Bevorstehend",
            ["events.past"] = "Vergangen",
            ["events.past_empty"] = "Noch keine vergangenen Veranstaltungen.",

            // ── events.ics (die M12-iCal-Oberflächen — Detailseite + Feed/Kalenderansicht, ADR 0112) ──
            ["events.ics.download"] = "Zum Kalender hinzufügen",
            ["events.ics.feed"] = "Kalender-Feed (iCal)",

            // ── M14 (ADR 0115 D2) — die beiden U03-Verknüpfungsetiketten Events ↔ To-dos ──
            ["todo.event_link"] = "Verknüpfte Veranstaltung",
            ["events.linked_todos"] = "Verknüpfte To-dos",

            // ── M14 (ADR 0115 D3) — die U04 set-event-Picker-Beschriftungen
            //    (To-do-Details: Formularbeschriftung + Platzhalter/Clear-Option) ──
            ["todo.set_event.label"] = "Mit Veranstaltung verknüpfen",
            ["todo.set_event.pick"] = "Veranstaltung wählen",

            // ── M14 (ADR 0115 D4) — die zwei U06 VTODO-Oberflächen
            //    (To-do-Details: „Zum Kalender hinzufügen" + To-do-Index:
            //    Feed-Link; die ADR-0112 events.ics.-Form auf der To-do-Seite,
            //    schlichte <a>-Links, kein neues JS — das geschlossene
            //    Schlüssel-Register + KnownTranslationKeys_ParityTests erzwingen × 4) ──
            ["projects.todos.ics.download"] = "Zum Kalender hinzufügen",
            ["projects.todos.ics.feed"] = "Kalender-Feed (iCal)",

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
            // ADR 0146 — der Kind-Konto-Text: der eine verbleibende Schritt ist,
            // das eigene Passwort zu setzen (die Sorgeberechtigte hat das Konto
            // angelegt, aber nie das Passwort gehalten).
            ["email.verify_child_body"] =
                "Hallo {0},\n\nDein Kumunita-Konto ist bereit. Öffne diesen einmaligen " +
                "Link, um das Konto zu bestätigen und dein Passwort zu setzen (dabei wirst du " +
                "auch angemeldet):\n\n{1}\n\n" +
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
            ["notifications.mark_read"] = "Als gelesen markieren",
            ["notifications.mark_unread"] = "Als ungelesen markieren",
            ["notifications.empty"] = "Noch nichts — Dinge, die dir passieren, erscheinen hier.",
            ["notifications.bell"] = "Benachrichtigungen",
            ["notifications.view"] = "Ansehen",
            ["notifications.accept"] = "Annehmen",
            ["notifications.decline"] = "Ablehnen",
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
            // ── M9 (ADR 0105, U03) — die message.new-Art + Vorlagen ──
            ["notifications.kind.message.new"] = "Neue Nachricht",
            ["notifications.preference.message.new.label"] = "Nachrichten von anderen Bewohnern",
            ["notification.message.new.subject"] = "Neue Nachricht",
            ["notification.message.new.body"] = "Ein Bewohner hat dir eine Nachricht geschickt: ",

            // ── GU community-approval lane (ADR 0141) — die Betreuer-Art ──
            ["notifications.kind.guardian.group_invite"] =
                "Gruppen-Einladung für dein Kind",
            ["notifications.preference.guardian.group_invite.label"] =
                "Wenn eine Gruppe dein Kind einlädt",
            ["notification.guardian.group_invite.subject"] =
                "Eine Gruppe hat dein Kind eingeladen",
            ["notification.guardian.group_invite.body"] =
                "Eine Gruppe hat dein Kind eingeladen: ",
            ["notifications.kind.guardian.community_invite"] =
                "Community-Mitgliedschaft für dein Kind",
            ["notifications.preference.guardian.community_invite.label"] =
                "Wenn eine Community dein Kind hinzufügt",
            ["notification.guardian.community_invite.subject"] =
                "Dein Kind wurde einer Community hinzugefügt",
            ["notification.guardian.community_invite.body"] =
                "Dein Kind wurde einer Community hinzugefügt: ",

            // ── GU community-approval lane (ADR 0141) — die Kind-Seite ──
            ["guardian.pending_community_requests"] = "Offene Community-Mitgliedschaften",
            ["guardian.no_community_requests"] = "Keine offenen Community-Mitgliedschaften.",
            ["guardian.reject"] = "Ablehnen",

            // ── M9 (ADR 0105, U04) — die Bewohner-Fläche: Navigation, Liste, Thread, Composer ──
            ["message.nav"] = "Nachrichten",
            ["message.title"] = "Nachrichten",
            ["message.new"] = "Neue Unterhaltung",
            ["message.thread.empty"] = "Noch keine Nachrichten — melde dich einfach.",
            ["message.compose.placeholder"] = "Nachricht schreiben…",
            ["message.compose.send"] = "Senden",
            ["message.unread"] = "ungelesen",
            ["message.disabled"] = "Direktnachrichten sind auf dieser Instanz deaktiviert.",
            ["message.compose.disabled"] = "Diese Person hat Direktnachrichten deaktiviert, daher kannst du keine neue Nachricht senden.",
            ["message.other"] = "die andere Person",
            ["message.sent_to"] = "Gesendet an {0}.",
            ["message.load_earlier"] = "Frühere Nachrichten laden",
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

            // ── M7 (ADR 0090) — the shared pager's two link labels (D5) ──
            ["pagination.prev"] = "Neuere",
            ["pagination.next"] = "Ältere",

            // M8 (ADR 0091 D1/D4) — die eine /search-Seite + Nav-Eintrag.
            ["search.nav"] = "Suche",
            ["search.title"] = "Suche",
            ["search.placeholder"] = "Beiträge, Events, Seiten, Ankündigungen, Projekte, Boards, To-dos, Inventar, Dokumente und Personen durchsuchen…",
            ["search.no-results"] = "Keine Ergebnisse für",
            ["search.section.posts"] = "Beiträge",
            ["search.section.events"] = "Events",
            ["search.section.pages"] = "Seiten",
            ["search.section.announcements"] = "Ankündigungen",
            ["search.section.projects"] = "Projekte",
            ["search.section.boards"] = "Boards",
            ["search.section.todos"] = "To-dos",
            ["search.section.inventory"] = "Inventar",
            ["search.section.documents"] = "Dokumente",
            ["search.section.people"] = "Personen",
            ["search.scope.community"] = "Gemeinschaft",
            ["search.scope.groups"] = "Gruppen",
            ["search.empty.hint"] = "Finde Beiträge, Events, Seiten, Ankündigungen, Projekte, Boards, To-dos, Inventar, Dokumente und Personen nach Text oder Tag — es werden nur Inhalte gezeigt, die du ohnehin lesen kannst.",

            // M10 (ADR 0107 D10) — das eine stille Install-Affordance-Label
            // (U03, pwa-install.ts). Kein Banner, kein Modal — ein Button.
            ["pwa.install"] = "App installieren",

            // M11 (ADR 0108 D10) — die Portabilitäts-Operator-Oberfläche
            // (der /admin/portability Index: Export-Button, Import-Formular
            // + seine Destruktivitätsabsicherung, die zwei Status-Renderings).
            ["portability.index.title"]  = "Portabilität",
            ["portability.export"]       = "Exportieren",
            ["portability.import"]       = "Importieren",
            ["portability.confirm.import"] = "Dieses Archiv importieren? Es ersetzt den Inhalt der Instanz (der Wiederherstellungspfad — das Vorkopie-Backup des Operators ist der Rollback).",
            ["portability.status.ok"]    = "Fertig.",
            ["portability.status.failure"] = "Abgelehnt — das Archiv wurde abgelehnt, bevor etwas geschrieben wurde:",

            // M27 (ADR 0148 D9) — die Portabilitätsoberfläche der Bewohnerin / des
            // Bewohners (der /account/portability Index: Export-Button +
            // Import-Formular + Statusbereich; U07). Absichtlich ein ANDERER
            // myportability.*-Name als M11s Admin portability.*-Keys, damit die
            // Bewohner-Oberfläche nie mit der Operator-Oberfläche kollidiert.
            ["myportability.index.title"]  = "Meine Daten",
            ["myportability.export"]       = "Exportieren",
            ["myportability.import"]       = "Importieren",
            ["myportability.import.resolve"] = "Meine Auswahl anwenden",
            ["myportability.resolve.add_elsewhere"] = "Woanders hinzufügen",
            ["myportability.resolve.discard"] = "Verwerfen",
            ["myportability.status"]       = "Status",

            // M15 U04 (ADR 0116, D8) — die datei-basierten Bulk-Keys.
            ["translations.bulk.export"]     = "Übersetzungen herunterladen (CSV)",
            ["translations.bulk.import"]     = "Übersetzungen hochladen (CSV)",
            ["translations.bulk.import_hint"] = "Leere Felder werden übersprungen (sie löschen niemals eine Übersetzung); eine Datei mit einem unbekannten Schlüssel oder einer unbekannten Sprache wird unverändert abgelehnt.",

            // M15 U05 (ADR 0116, D8) — die editor-basierten Bulk-Keys
            // (Speichern-alles-Knopf + die beiden Modus-Umschalter).
            ["translations.bulk.save_all"]   = "Alle speichern",
            ["translations.bulk.mode_batch"] = "Stapelbearbeitung",
            ["translations.bulk.mode_single"] = "Einzelne Bearbeitung",

            // M16 (ADR 0117, D1) — die Inventar-Oberfläche (Ausleihe / Rückgabe
            // + die Verlaufs-Sektion + der Nav-Eintrag). Die geschlossene
            // Schlüsselmenge ist der Design-Doc-§kw-l.
            ["inv.nav"]                     = "Inventar",
            ["inv.list.title"]              = "Inventar",
            ["inv.list.empty"]              = "Noch keine Gegenstände.",
            ["inv.list.ownerKind.shared"]   = "Gemeinsam",
            ["inv.list.ownerKind.community"] = "Gemeinde",
            ["inv.list.ownerKind.private"]  = "Privat",
            ["inv.list.filter"]             = "Nach Typ filtern",
            ["inv.create.title"]            = "Neuer Gegenstand",
            ["inv.create.name"]             = "Name",
            ["inv.create.ownerKind"]        = "Typ",
            ["inv.create.description"]      = "Beschreibung",
            ["inv.create.component"]        = "Bereich",
            ["inv.create.submit"]           = "Gegenstand anlegen",
            ["inv.detail.title"]            = "Gegenstand",
            ["inv.detail.currentHolder"]    = "Derzeit bei",
            ["inv.detail.history"]          = "Verwendungshistorie",
            ["inv.detail.edit"]             = "Bearbeiten",
            ["inv.detail.delete"]           = "Löschen",
            ["inv.detail.checkOut"]         = "Ausleihen",
            ["inv.detail.checkIn"]          = "Zurückgeben",
            ["inv.edit.title"]              = "Gegenstand bearbeiten",
            ["inv.edit.submit"]             = "Änderungen speichern",

            // M17 (ADR 0118) — Lesezeichen: die persönliche Merk-Oberfläche
            // (D6: dauerhafte Kerno­berfläche, kein Admin-Toggle). Die
            // geschlossene Schlüsselmenge ist der Design-Doc-§kw-l (14
            // Schlüssel: Nav-Eintrag, Listen-/Degradier-/Typ-Labels,
            // Unmark-Aktion + Umschaltknopf + obs-2 Flash-Toast-Schlüssel).
            ["bm.nav"]                      = "Lesezeichen",
            ["bm.list.title"]               = "Deine Lesezeichen",
            ["bm.list.empty"]               = "Noch keine Lesezeichen.",
            ["bm.list.degraded"]            = "Nicht mehr verfügbar",
            ["bm.list.kind.post"]           = "Beiträge",
            ["bm.list.kind.event"]          = "Veranstaltungen",
            ["bm.list.kind.todo"]           = "Aufgaben",
            ["bm.list.kind.announcement"]   = "Ankündigungen",
            ["bm.list.kind.page"]           = "Seiten",
            ["bm.list.unbookmark"]          = "Entfernen",
            ["bm.button.bookmark"]          = "Merken",
            ["bm.button.bookmarked"]        = "Gemerkt",
            // Obs-2 (ADR 0118 Amendment) — Flash-Toast-Schlüssel.
            ["bm.toggle.bookmarked"]        = "Gemerkt.",
            ["bm.toggle.removed"]           = "Lesezeichen entfernt.",

            // ── M21 (ADR 0122) — Dokumentverwaltung: die /documents-Feed- +
            // Detail- + Upload-Oberfläche (D5 Standing, D6 Download, D7
            // Deny → 404). U04 erstellt das komplette 12-Schlüssel-Satz;
            // U03's Ansichten konsumieren.
            ["documents.title"]             = "Dokumente",
            ["documents.empty"]             = "Noch keine Dokumente sind für dich sichtbar.",
            ["documents.upload"]            = "Dokument hochladen",
            ["documents.upload_title"]      = "Dokument hochladen",
            ["documents.upload_summary"]    = "Einzeilige Beschreibung (optional)",
            ["documents.upload_file"]       = "Datei",
            ["documents.upload_audience"]   = "Wer darf dieses Dokument sehen",
            ["documents.upload.submit"]     = "Dokument hochladen",
            ["documents.download"]          = "Herunterladen",
            ["documents.detail.type_size"]  = "Dateitype / Größe",
            ["documents.detail.updated"]    = "Zuletzt aktualisiert",
            ["documents.flash_uploaded"]    = "Dokument hochgeladen.",
            // ADR 0125 (U03) — die nur-für-den-Inhaber-Bearbeitungslane.
            ["documents.edit"]              = "Bearbeiten",
            ["documents.edit_title"]        = "Dokument bearbeiten",
            ["documents.edit_summary"]      = "Einzeilige Beschreibung (optional)",
            ["documents.edit_file"]         = "Datei ersetzen (optional)",
            ["documents.edit_file_hint"]    = "Leer lassen, um die aktuelle Datei zu behalten.",
            ["documents.edit_audience"]     = "Wer darf dieses Dokument sehen",
            ["documents.edit.submit"]       = "Änderungen speichern",
            ["documents.flash_edited"]      = "Dokument aktualisiert.",

            // Die "Dokumente organisieren"-Lane (Tags + Ordner) — die
            // documents.folder_* / documents.tags.*-Keys + die Flash-Keys der
            // DocumentFolderController-Routen (create / rename / move / delete /
            // document-move).
            ["documents.folder"]              = "Ordner",
            ["documents.folder_unfiled"]      = "Nicht abgelegt",
            ["documents.folder_hint"]         = "Lege dieses Dokument in einen Ordner ab, um das Archiv organisiert zu halten.",
            ["documents.folder_new"]          = "Neuer Ordner",
            ["documents.folder_create"]       = "Erstellen",
            ["documents.folder_name_placeholder"] = "Ordnername",
            ["documents.tags"]                = "Tags",
            ["documents.tags_hint"]           = "Tippe, um bestehende Tags zu finden, oder starte ein neues.",
            ["documents.flash_moved"]         = "Dokument verschoben.",
            ["documents.folder_flash_created"] = "Ordner erstellt.",
            ["documents.folder_flash_renamed"] = "Ordner umbenannt.",
            ["documents.folder_flash_moved"]   = "Ordner verschoben.",
            ["documents.folder_flash_deleted"] = "Ordner gelöscht.",

            // ── M22 (ADR 0132) — Onboarding: die /onboarding-Führung (D4) +
            // die schließbare Home-/Nav-Anzeige (D5) + der Finish/Skip-Flash
            // (D2/D4). U03 erstellt den VOLLSTÄNDIGEN geschlossenen Satz;
            // U02/U03 nutzen ihn. Die Paritäts-Sicherung verlangt jeden
            // Schlüssel in allen vier Sprachen, nicht leer (C-M22·6, GATE-6). ──
            ["onboarding.title"]            = "Dein Konto einrichten",
            ["onboarding.intro"]            = "Ein kurzer geführter Rundgang durch die wenigen Dinge, die Kumunita für dich zum Laufen bringen. Alles führt in die Einstellung, die es bereits besitzt — du schließt in einer Minute ab oder kommst jederzeit wieder.",
            ["onboarding.step_displayname"] = "Dein Anzeigename",
            ["onboarding.step_avatar"]      = "Dein Avatar",
            ["onboarding.step_language"]    = "Deine Schnittstellensprache",
            ["onboarding.step_timezone"]    = "Deine Zeitzone",
            ["onboarding.step_dateformat"]  = "Dein Datums- und Zeitformat",
            ["onboarding.step_email"]       = "Deine E-Mail- und Benachrichtigungssprache",
            ["onboarding.step_contact"]     = "Deine Kontaktdaten & wer sie sehen kann",
            // M9 amendment (ADR 0139) — the messaging opt-in step.
            ["onboarding.step_messaging"]   = "Ob Du Direktnachrichten nutzen kannst",
            ["onboarding.visit"]            = "Zu dieser Einstellung",
            ["onboarding.finish"]           = "Alles klar — Einrichtung abschließen",
            ["onboarding.skip"]             = "Jetzt überspringen",
            ["onboarding.flash_done"]       = "Einrichtung abgeschlossen — willkommen in deiner Nachbarschaft.",
            ["onboarding.banner.text"]      = "Dein Konto fertig einrichten?",
            ["onboarding.banner.action"]    = "Einrichtung starten",

            // ── M9 amendment — die pro-Bewohner-Messaging-Steuerung + die
            // Betreuer-Obergrenze (initial English values, pending de
            // translation; the ADR 0015 provider floor resolves them). ──
            ["settings.messaging.title"]           = "Messaging",
            ["settings.messaging.description"]     = "Wähle, ob andere Bewohner dir direkte 1:1-Nachrichten senden dürfen. Deine Auswahl wird auf deinem Konto gespeichert und wirkt sofort.",
            ["settings.messaging.instance_off"]    = "Direktnachrichten sind derzeit von einem Administrator auf dieser Instanz deaktiviert. Du kannst dich jetzt anmelden, und die Funktion steht dir zur Verfügung, sobald sie wieder aktiviert wird.",
            ["settings.messaging.restricted"]      = "Direktnachrichten wurden auf deinem Konto durch einen Betreuer eingeschränkt. Wende dich an sie, um dies zu ändern.",
            ["settings.messaging.optin"]           = "Erlaube anderen, mir direkte 1:1-Nachrichten zu senden",
            ["settings.messaging.save"]            = "Messaging-Einstellung speichern",
            ["guardian.messaging.title"]           = "Messaging",
            ["guardian.messaging.description"]     = "Wähle, ob dieses Kind 1:1-Direktnachrichten nutzen darf. Bei Einschränkung kann das Kind keine Nachrichten senden oder erhalten — diese Auswahl hat Vorrang vor seiner eigenen Opt-in. Bei Erlaubnis entscheidet das Kind selbst auf seiner eigenen Messaging-Einstellungsseite.",
            ["guardian.messaging.current_restricted"] = "Messaging ist derzeit für dieses Kind eingeschränkt.",
            ["guardian.messaging.current_allowed"]    = "Messaging ist derzeit für dieses Kind erlaubt.",
            ["guardian.messaging.child_optin_on"]     = "Das Kind hat sich auf seinem eigenen Konto für Messaging angemeldet.",
            ["guardian.messaging.child_optin_off"]    = "Das Kind hat sich auf seinem eigenen Konto noch nicht für Messaging angemeldet — selbst wenn du es erlaubst, muss es sich auf seiner eigenen Einstellungsseite anmelden.",
            ["guardian.messaging.allow"]              = "Messaging erlauben",
            ["guardian.messaging.restrict"]           = "Messaging einschränken",

            // ── P1 audit (2026-10-04) ── dieselbe ~73-Schlüssel-Sammlung wie
            // in <see cref="EnValues"/> (die P1-Übersetzungsaudit-Auflistung).
            ["common.remove_translation_confirm"] =
                "Diese Übersetzung entfernen?",
            ["events.skip_occurrence_confirm"] =
                "Diesen Termin überspringen? Du kannst ihn später wiederherstellen.",
            ["community.confirm_remove_member"] =
                "{0} aus {1} entfernen?",
            ["a11y.notifications"]             = "Benachrichtigungen",
            ["a11y.find_tag"]                  = "Nach Tag finden",
            ["a11y.find_bio"]                  = "Nach Bio finden",
            ["a11y.avatar"]                    = "Avatarbild",
            ["a11y.board_actions"]             = "Board-Aktionen",
            ["a11y.calendar_view"]             = "Kalenderansicht",
            ["a11y.event_time_range"]          = "Zeitraum der Veranstaltung",
            ["a11y.check_out_note"]            = "Ausleihe-Hinweis",
            ["a11y.community_pages"]           = "Gemeinschafts-Seiten",
            ["a11y.platform_pages"]            = "Plattform-Seiten",
            ["a11y.community_page_tree"]       = "Gemeinschafts-Seitenbaum",
            ["a11y.platform_page_tree"]        = "Plattform-Seitenbaum",
            ["a11y.breadcrumb"]                = "Brotkrumen-Navigation",
            ["a11y.about_features"]            = "Was Kumunita ist",
            ["a11y.about_audience"]            = "Für wen",
            ["a11y.about_philosophy"]          = "Die Philosophie",
            ["a11y.about_contact"]             = "Kontakt aufnehmen",
            ["a11y.about_project"]             = "Das Projekt",
            ["a11y.set_limit"]                 = "Limit für {0} setzen",
            ["account.block"]               = "Sperren",
            ["account.unblock"]             = "Entsperren",
            ["account.confirm_block"]       = "Dieses Konto sperren? Es verliert alle Berechtigungen, bis es entsperrt wird.",
            ["account.confirm_unblock"]     = "Dieses Konto entsperren? Ihre Berechtigungen werden wiederhergestellt.",
            ["account.err.required"]        = "Das Feld {0} ist erforderlich.",
            ["account.err.email"]           = "Das Feld {0} ist keine gültige E-Mail-Adresse.",
            ["account.err.password_min"]    = "{0} muss mindestens {1} Zeichen lang sein.",
            ["account.err.password_mismatch"] = "Die Felder {0} und {1} stimmen nicht überein.",
            ["footer.feed"]                 = "Der Feed",
            ["languages.title"]             = "Sprachen",
            ["languages.lede_admin"]        =
                "Verwalte die Sprachen, die diese Instanz unterstützt. Änderungen wirken ab der " +
                "nächsten Anfrage — ohne Neubau, ohne Neustart.",
            ["languages.lede_translator"]   =
                "Prüfe die Übersetzungsabdeckung der Plattform und aktualisiere die UI-Strings. " +
                "Änderungen wirken ab der nächsten Anfrage.",
            ["languages.add_heading"]       = "Sprache hinzufügen",
            ["languages.code_label"]        = "Sprachcode",
            ["languages.native_name_label"] = "Eigener Name",
            ["languages.code_hint"]         = "Kurzer Code, z.B. pl für Polnisch, no-NO für Norwegisch.",
            ["languages.supported_heading"] = "Unterstützte Sprachen",
            ["languages.th_code"]           = "Code",
            ["languages.th_native_name"]    = "Eigener Name",
            ["languages.th_enabled"]        = "Aktiv",
            ["languages.th_ui_strings"]     = "UI-Strings",
            ["languages.th_actions"]        = "Aktionen",
            ["languages.enabled"]           = "aktiv",
            ["languages.disabled"]          = "deaktiviert",
            ["languages.present"]           = "{n} vorhanden",
            ["languages.missing"]           = "{n} fehlt",
            ["languages.action_disable"]    = "Deaktivieren",
            ["languages.action_enable"]     = "Aktivieren",
            ["languages.action_set_default"] = "Als Standard setzen",
            ["languages.action_ui_strings"] = "UI-Strings",
            ["languages.action_remove"]     = "Entfernen",
            ["languages.reorder_btn"]       = "Neu sortieren (aktuelle Reihenfolge bestätigen)",
            ["languages.reorder_title"]     = "Die aktuelle Reihenfolge (wie unten gezeigt) als neue Sortierreihenfolge senden",
            ["languages.reorder_hint"]      =
                "Drag-and-Drop-Sortierung ist nicht verfügbar; das Sortierformular akzeptiert die Codes " +
                "in der Reihenfolge, in der sie hier erscheinen. Um die Reihenfolge zu ändern, sollte " +
                "der Administrator die Liste in der gewünschten Reihenfolge senden.",
            ["languages.confirm_remove"]    =
                "{0} entfernen? Ihre Übersetzungszeilen bleiben erhalten und werden wiederhergestellt, " +
                "wenn die Sprache erneut hinzugefügt wird.",
            ["translations.editor.back"]         = "← Zurück zu den Sprachen",
            ["translations.editor.title"]        = "UI-Strings für {0}",
            ["translations.editor.lede"]         =
                "Die vollständige Liste der Strings, die die Plattform ihren Bewohnern zeigt. Jede " +
                "Zeile zeigt den Schlüssel, seinen englischen Referenztext und den aktuellen Wert in " +
                "{0}. Das Speichern einer Zeile fügt ihre Übersetzung hinzu oder aktualisiert sie — " +
                "sichtbar ab der nächsten Anfrage.",
            ["translations.editor.mode_label"]   = "Bearbeitungsmodus",
            ["translations.editor.th_key"]       = "Schlüssel",
            ["translations.editor.th_en_reference"] = "Englische Referenz",
            ["translations.editor.th_value"]     = "Wert in {0}",
            ["translations.editor.value_aria"]   = "Wert für {0} in {1}",
            ["events.rsvp_status_going"]     = "Geht",
            ["events.rsvp_status_maybe"]     = "Vielleicht",
            ["events.rsvp_status_no"]        = "Nein",
            ["posts.delete_confirm"]         = "Diesen Beitrag löschen? Deine Antworten bleiben sichtbar.",
            ["posts.reply_delete_confirm"]   =
                "Diese Antwort löschen? Sie wird durch eine Notiz ersetzt. Der Eintrag bleibt erhalten.",
            ["announcements.delete_confirm"] = "Diese Ankündigung löschen? Das kann nicht rückgängig gemacht werden.",
            ["groups.confirm_delete"]        =
                "Diese Gruppe löschen? Dadurch werden die Gruppe, ihre Mitglieder und ihre " +
                "ausstehenden Einladungen entfernt, und es kann nicht rückgängig gemacht werden.",
            ["community.confirm_leave"]      = "{0} verlassen?",
            ["guardian.suspend_confirm"]     = "Dieses Kind-Konto sperren? Es wird blockiert, bis du es wieder freischaltest.",
            ["guardian.messaging.allow_confirm"] =
                "Messaging für dieses Kind erlauben? Es kann dann direkte Nachrichten senden und " +
                "empfangen (seine eigene Opt-in muss ebenfalls an sein).",
            ["guardian.messaging.restrict_confirm"] =
                "Messaging für dieses Kind einschränken? Es kann dann keine direkten Nachrichten mehr " +
                "senden oder empfangen, unabhängig von seiner eigenen Opt-in.",
            ["guardian.handover_confirm"]    = "Dieses Konto an das Kind übergeben? Damit löst du deine Vormundschaft über es auf.",
            ["guardian.remove_myself_confirm"] =
                "Nimmst du deine Vormundstellung für dieses Kind zurück? Du " +
                "kannst das Konto danach nicht mehr verwalten. Es bleibt " +
                "erhalten, und die übrigen Vormünder bleiben dabei.",
            ["guardian.delete_child_confirm"] =
                "Dieses Kind-Konto dauerhaft löschen? Damit werden Anmeldung, Profil und " +
                "Mitgliedschaften entfernt — das kann nicht rückgängig gemacht werden.",
            ["locale.reset_confirm"]         = "Deine Sprachpräferenz auf den Instanz-Standard zurücksetzen?",
            ["locale.email_reset_confirm"]   = "Deine E-Mail- und Benachrichtigungssprache auf den Instanz-Standard zurücksetzen?",
            ["settings.quiet.clear_confirm"] =
                "Deine Stumstunden löschen? Alle Benachrichtigungs-E-Mails werden wieder sofort gesendet.",
            ["settings.timezone_reset_confirm"] = "Deine Zeitzone auf die Plattform-Vorgabe zurücksetzen?",
            ["settings.dateformat_reset_confirm"] = "Dein Datum- und Zeitformat auf die Plattform-Vorgabe zurücksetzen?",
            ["pages.form.body_hint"]         =
                "Optional — ein Ordnerknoten kann ohne Text sein. Geschrieben in Markdown über den " +
                "visuellen Editor (derselbe, den Beiträge und Ankündigungen nutzen).",
            ["pages.reset_confirm"]          =
                "Diese Seite auf den Seed-Text zurücksetzen? Alle Änderungen, die du am Text " +
                "(Englisch) und an seinen deutschen / französischen / dänischen Übersetzungen " +
                "vorgenommen hast, werden durch die Seed-Baseline überschrieben. Der Rest der Seite " +
                "(Zielgruppe, Elternteil, etc.) bleibt unberührt.",
            ["pl.board.delete_confirm"]      =
                "Dieses Board löschen? Seine Spuren und Kartenplatzierungen werden entfernt — die " +
                "To-dos selbst bleiben erhalten.",
            ["pl.lane.delete_confirm"]       =
                "Diese Spur löschen? Ihre Karten kommen von diesem Board — die To-dos selbst bleiben " +
                "erhalten.",
            ["pl.todo.assignee_remove_confirm"] = "Die Zuweisung von dieser Aufgabe entfernen?",
            ["pl.todo.move_confirm"]         = "Diese To-do zu einem anderen Board verschieben? Sie ist dann nicht mehr auf diesem Board.",
            ["pl.todo.delete_confirm"]       = "Diese To-do und ihre Unteraufgaben löschen? Das kann nicht rückgängig gemacht werden.",
            ["inv.item.delete_confirm"]      = "Dieses Element löschen? Das kann nicht rückgängig gemacht werden.",
            ["a11y.close"]                   = "Schließen",
            ["a11y.toggle_nav"]              = "Navigation umschalten",
            ["a11y.primary_nav"]             = "Primär",
            ["a11y.pagination"]              = "Paginierung",
            ["a11y.pinned_announcement"]     = "Angepinnte Ankündigung",
            ["a11y.banner_read_more"]        = "Diese Ankündigung in vollem Umfang lesen",
            ["a11y.banner_all"]              = "Alle Ankündigungen ansehen",
            ["a11y.banner_dismiss"]          = "Diese angepinnte Ankündigung schließen",
            ["a11y.onboarding_region"]       = "Kontoeinrichtung",
            ["a11y.onboarding_action"]       = "Einrichtung starten",
            ["a11y.onboarding_dismiss"]      = "Dieses Banner schließen",
            ["a11y.whatsnew_region"]         = "Was ist neu",
            ["a11y.actions_for"]             = "Aktionen für {0}",
            ["a11y.search"]                  = "Suche",
            ["a11y.scope"]                   = "Bereich",
            ["a11y.back_to_messages"]        = "Zurück zu den Nachrichten",
            ["a11y.resident"]                = "Bewohner",
            ["account.storage_title"]        = "Mein Speicher",
            ["account.storage_lede"]         =
                "Wie viel deiner Inhalte die Plattform zählt, dein Kontingent pro Benutzer und wie " +
                "viel davon dir noch übrig bleibt.",
            ["account.storage_used"]         = "dein genutzter Inhalt",
            ["account.storage_quota"]        = "dein Kontingent pro Benutzer",
            ["account.storage_remaining"]    = "verbleibend",
            ["account.storage_unlimited_note"] =
                "Dein Kontingent pro Benutzer ist unbegrenzt — es gibt keine Inhaltsbeschränkung für " +
                "deine Uploads.",
            ["account.storage_remaining_note"] =
                "Dein verbleibender Betrag ist, was von deinem Kontingent pro Benutzer übrig ist " +
                "({0}). Bitte einen Administrator, das Kontingent zu erhöhen, wenn du mehr brauchst.",
            ["translations.bulk.export_title"] = "Lade die UI-Strings dieser Sprache als CSV-Datei herunter",
            ["common.delete"]          = "Löschen",
            ["pages.delete_confirm"]   = "Diese Seite löschen?",
            ["admin.help.reset_one"]   =
                "\"{0}\" auf den Seed-Text zurücksetzen? Damit werden alle manuellen Änderungen " +
                "überschrieben.",
            ["admin.help.reset_all"]   =
                "ALLE {0} Seed-Hilfeseiten auf den Seed-Text zurücksetzen? Damit werden alle " +
                "manuellen Änderungen auf jeder Seite überschrieben.",

            // ── M26 U16 — der geschlossene sort.*-Satz der geteilten
            // _Sort-Partial (Label-only; die acht Sortschlüssel der
            // erlaubten Listen der U10–U15-Oberflächen) ──
            ["sort.created"]          = "Erstellt",
            ["sort.modified"]         = "Geändert",
            ["sort.title"]            = "Titel",
            ["sort.size"]             = "Größe",
            ["sort.name"]             = "Name",
            ["sort.start"]            = "Beginn",
            ["sort.due"]              = "Fällig",
            ["sort.status"]           = "Status",
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
            // ── M19 (ADR 0120) — la surface des comptes invités : la surface
            // /admin/guests (D6) + l'accueil de l'invité connecté (D5) ─────
            ["admin.guests_title"]               = "Comptes invités",
            ["admin.guests_empty"]               = "Aucun compte invité pour l'instant.",
            ["admin.guests_create"]              = "Créer un invité",
            ["admin.guests_window_label"]        = "Fenêtre d'accès",
            ["admin.guests_surfaces_label"]      = "Surfaces autorisées",
            ["admin.guests_surface_announcements"] = "Annonces",
            ["admin.guests_surface_events"]      = "Événements",
            ["admin.guests_surface_directory"]   = "Annuaire",
            ["admin.guests_saved"]               = "Statut de l'invité enregistré.",
            ["account.guest_welcome"] =
                "Vous êtes connecté en tant qu'invité. Votre accès est limité " +
                "aux surfaces que l'administrateur a autorisées, pour la " +
                "fenêtre qu'il a définie.",

            // ── M20 (ADR 0121) — heures de silence des notifications : la 5e
            // section /settings/quiet (D7) + la surface /admin/quiet (D8).
            // U06 authorise le jeu fermé de 15 clés ; U06/U07 consomment, U07
            // n'ajoute aucune clé.
            ["settings.quiet.title"]        = "Heures de silence",
            ["settings.quiet.description"]  = "Choisis quand tes e-mails de notification sont retenus. Ton " +
                "horaire de silence est enregistré sur ton compte, appliqué dans " +
                "ton propre fuseau horaire — il prend effet au prochain envoi d'une " +
                "notification et n'affecte jamais les autres résidents.",
            ["settings.quiet.enabled"]      = "Retenir les e-mails de notification pendant mes heures de silence",
            ["settings.quiet.mode_label"]   = "Quand les e-mails de notification doivent-ils être retenus ?",
            ["settings.quiet.mode_blocked"] = "Retenus pendant les heures & jours sélectionnés",
            ["settings.quiet.mode_allowed"] = "Retenus hors des heures & jours sélectionnés",
            ["settings.quiet.hours_label"]  = "Heures de la journée",
            ["settings.quiet.days_label"]   = "Jours de la semaine",
            ["settings.quiet.save"]         = "Enregistrer les heures de silence",
            ["settings.quiet.flash_saved"]  = "Heures de silence enregistrées — les e-mails retenus sont envoyés dès que tes heures de silence se terminent.",
            ["settings.quiet.flash_cleared"] = "Heures de silence retirées — tous les e-mails de notification seront désormais envoyés immédiatement.",
            ["admin.quiet.title"]           = "Cadence des heures de silence",
            ["admin.quiet.cadence_label"]   = "Revérifier les notifications retenues toutes les (minutes)",
            ["admin.quiet.save"]            = "Enregistrer la cadence",
            ["admin.quiet.flash_saved"]     = "Cadence des heures de silence enregistrée.",

            // ── M28 (ADR 0151) — limites d'utilisation tuteur : la section GU
            // Detail à 13 clés guardian.timelimit.* (D7) + la landing de
            // connexion account.time_limit.login_message (référencée dans le
            // cas ?error=time-limit de Login.cshtml, U04). U05 autorise le JEU
            // COMPLET de 14 clés ; U06 consomme, n'en ajoute aucune. Espace de
            // noms DISTINCT — PAS les clés M20 settings.quiet.* / admin.quiet.*
            // (elles appartiennent à la lane de notification).
            ["guardian.timelimit.title"]        = "Limites d'utilisation",
            ["guardian.timelimit.description"]  = "Choisis quand ton enfant peut utiliser la plateforme. " +
                "L'horaire est enregistré sur son compte, appliqué dans son " +
                "propre fuseau horaire — il prend effet à sa prochaine connexion et ne t'affecte jamais.",
            ["guardian.timelimit.enabled"]      = "Appliquer des limites d'utilisation à cet enfant",
            ["guardian.timelimit.mode_label"]   = "Quand l'enfant peut-il utiliser la plateforme ?",
            ["guardian.timelimit.mode_blocked"] = "Bloqué pendant les heures & jours sélectionnés",
            ["guardian.timelimit.mode_allowed"] = "Autorisé seulement pendant les heures & jours sélectionnés",
            ["guardian.timelimit.hours_label"]  = "Heures de la journée",
            ["guardian.timelimit.days_label"]   = "Jours de la semaine",
            ["guardian.timelimit.save"]         = "Enregistrer les limites",
            ["guardian.timelimit.clear"]        = "Effacer les limites",
            ["guardian.timelimit.flash_saved"]  = "Limites enregistrées — l'enfant sera déconnecté hors de la fenêtre autorisée.",
            ["guardian.timelimit.flash_cleared"] = "Limites effacées — l'enfant peut maintenant utiliser la plateforme à tout moment.",
            ["guardian.timelimit.badge_set"]    = "Limites définies",
            ["account.time_limit.login_message"] = "Tu es en dehors de tes heures autorisées. Merci de revenir plus tard.",

            // ── nav (the shared top-nav, _Layout + _AccountNav) ─────────────
            ["nav.home"]          = "Accueil",

            // ADR 0111 — the nav-variant surface: the compact top-row variant
            // folds the less-frequent sections into one "More" menu, and the
            // resident picks a variant from the account menu (nav_variant.*
            // are the picker labels, the active one marked with a ✓).
            ["nav.more"]          = "Plus",
            ["nav_variant.label"] = "Navigation",
            ["nav_variant.row"]   = "Barre supérieure",
            ["nav_variant.rail"]  = "Barre d’icônes",

            // ADR 0133 — the appearance (theme) picker labels.
            ["theme.label"]      = "Apparence",
            ["theme.auto"]       = "Automatique (comme l’appareil)",
            ["theme.light"]      = "Clair",
            ["theme.dark"]       = "Sombre",

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
            ["common.full_page"]    = "Page entière",
            ["common.exit_full_page"] = "Quitter la page entière",
            ["common.open_full_page"] = "Ouvrir la page complète",
            ["common.fullscreen"]   = "Plein écran",
            ["common.exit_fullscreen"] = "Quitter le plein écran",
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
            ["admin.roles_independent_hint"] = "Indépendants — un résident peut cumuler librement les rôles. Aucune case cochée = un simple Membre.",
            ["admin.moderator_scope"]       = "Portée du modérateur",
            ["admin.moderator_scope_hint"]  = "Les communautés que ce compte peut modérer. N'a de sens que si le rôle Modérateur est coché — la plateforme efface les choix de portée quand le rôle Modérateur est désactivé.",
            ["nav.announcements"] = "Annonces",
            ["nav.community"]     = "Communauté",
            ["nav.groups"]        = "Groupes",
            ["nav.pages"]         = "Pages",
            ["nav.tags"]          = "Étiquettes",
            ["nav.directory"]     = "Annuaire",
            ["nav.people"]        = "Personnes",
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

            // ADR 0119 (M18, D9) — the recurring-events key set. 14 keys, all under
            // the existing `events.*` namespace (the `events.created` / `events.edited`
            // entries above are the anchor; no new `event.*` / `recurrence.*` /
            // `series.*` namespace). Closed set × en/de/fr/da: every key below must
            // appear in all four dictionaries (see the closure test in
            // tests/Kumunita.Core.Tests/KnownTranslationKeysClosureTests.cs, U07).
            ["events.recurrence.none"]       = "Ne se répète pas",
            ["events.recurrence.daily"]      = "Quotidien",
            ["events.recurrence.weekly"]     = "Hebdomadaire",
            ["events.recurrence.monthly"]    = "Mensuel",
            ["events.recurrence.yearly"]     = "Annuel",
            ["events.recurrence.interval"]   = "Tous les",
            ["events.recurrence.ends_after"] = "Se termine après",
            ["events.recurrence.ends_on"]    = "Se termine le",
            ["events.recurrence.count"]      = "événements",
            ["events.recurrence.until"]      = "jusqu'au",
            ["events.series.repeats"]        = "Se répète",
            ["events.series.skip"]           = "Passer cet événement",
            ["events.series.restore"]        = "Restaurer cet événement",
            ["events.series.part_of"]        = "Fait partie d'une série",

            // ── projects (M5 — ADR 0067: the to-do surface nav entry + labels) ──
            ["nav.projects"]                 = "Projets",
            ["projects.todo.title"]          = "Tâches",
            ["projects.todo.lede"]           = "Les tâches partagées du quartier — assignez du travail à un voisin, décomposez-le en sous-tâches et placez-le sur un tableau.",
            ["projects.todo.new"]            = "Nouvelle tâche",
            ["projects.todo.new_lead"]       = "Rédigez une tâche, assignez-la éventuellement à un voisin, et — si besoin — décomposez-la en sous-tâches ou placez-la sur un tableau. Par défaut, elle est visible par tous ; désactivez cela dans la section public pour restreindre l'accès.",
            ["projects.todo.title_hint"]     = "Un libellé court pour la tâche — l'étiquette de la carte.",
            ["projects.todo.status"]         = "Statut",
            ["projects.todo.status_assignee_hint"] = "Le statut est un texte libre (une chaîne de caractères, pas une liste fixe). Assigner une tâche donne à ce résident un droit d'intervention sur elle — affichage + intervention, jamais une limite d'accès.",
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
            ["projects.todo.filter_assigned_to_me"] = "Assignées à moi",
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
            // ADR 0106 — voie d'auto-attribution + panneau de détails de carte.
            ["projects.todo.assign_to_me"]   = "M'attribuer",
            ["projects.todo.details"]        = "Détails",
            ["projects.todo.community"]      = "Communauté",
            ["projects.todo.subtasks"]       = "Sous-tâches",
            ["projects.todo.boards"]         = "Tableaux",
            // ADR 0100 — Commentaires + réponses (C-M3·1 — les commentaires
            // héritent de la décision de lecture unique de la tâche).
            ["projects.todo.comments"]       = "Commentaires",
            ["projects.todo.comment_empty"]  = "Pas encore de commentaires. Si vous pouvez voir cette tâche, vous pouvez la commenter.",
            ["projects.todo.comment_reply"]  = "Commentaire",
            ["projects.todo.comment_submit"] = "Commenter",
            ["projects.todo.comment_deleted"] = "Ce commentaire a été supprimé par son auteur.",
            ["projects.todo.comment_delete"] = "Supprimer",
            ["projects.todo.comment_audience_note"] = "Les commentaires n'ont pas d'audience propre — ils sont visibles sous la décision d'audience unique de cette tâche. Tu ne commentes que là où la tâche elle-même est visible.",
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
            ["projects.board.edit_lead"]     = "Mettez à jour le titre, la description et le public de ce tableau. Sa communauté et sa langue sont fixés à la création.",
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
            ["projects.board.lane.delete"] = "Supprimer la colonne",
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
            ["guardian.community_block_note"] =
                "Choisissez les communautés auxquelles cet enfant peut accéder. " +
                "Bloquer une communauté la lui masque — ainsi que ses " +
                "publications — même s'il s'agit d'une des communautés " +
                "obligatoires auxquelles tous appartiennent. C'est le contrôle " +
                "du tuteur : vous décidez qui rejoint une communauté en " +
                "l'invitant ou via un administrateur, mais vous pouvez masquer " +
                "une communauté que vous ne voulez pas que cet enfant voie.",
            ["guardian.community_blocked"]   = "Bloquée & masquée",
            ["guardian.community_unblock"]   = "Débloquer & afficher",
            ["guardian.community_block"]     = "Bloquer l'accès & masquer",
            ["guardian.pending_invitations"] = "Invitations de groupe en attente",
            ["guardian.no_invitations"]      = "Pas d'invitations en attente.",
            ["guardian.approve"]             = "Approuver",
            ["guardian.handover"]            = "Transférer le compte",
            ["guardian.handover_hint"]       =
                "Dissoudre la tutelle remet le compte à l'enfant. Ses " +
                "adhesions sont conservées, et ses propres réglages reviennent " +
                "à la prochaine lecture.",
            ["guardian.dissolve"]            = "Dissoudre la tutelle",
            // Count-aware steering (ADR 0028 §G·6) — fr.
            ["guardian.remove_myself"]       = "Retirer ma tutelle",
            ["guardian.remove_myself_hint"]  =
                "Vous êtes l'un des tuteurs de cet enfant. Retirer votre " +
                "tutelle met fin à vos droits sur le compte ; les autres " +
                "tuteurs restent en place, et le compte est conservé.",
            ["guardian.remove_myself_submit"] = "Me retirer",
            ["guardian.suspended"]           = "Suspendu",
            ["guardian.unsuspend"]           = "Réactiver",
            ["guardian.suspend"]             = "Suspendre",
            ["guardian.delete_child"]        = "Supprimer le compte de l'enfant",
            ["guardian.delete_child_lede"]   =
                "La suppression retire la connexion, le profil et les adhésions aux groupes " +
                "et communautés de l'enfant et dissout toute autre tutelle sur ce compte. " +
                "Ses actions passées sont conservées dans la piste d'audit, avec son " +
                "identité remplacée par un marqueur. Cette action est irréversible.",
            ["guardian.delete_child_confirm_checkbox"] = "Je comprends que le compte de l'enfant sera définitivement supprimé.",
            ["guardian.delete_child_submit"] = "Supprimer le compte",
            ["guardian.display_name"]        = "Nom affiché",
            ["guardian.email"]               = "Adresse e-mail",
            ["guardian.password"]            = "Mot de passe",
            ["guardian.child_email_hint"]    =
                "L'enfant ouvre le lien de confirmation dans son e-mail pour définir son propre mot de passe et se connecter — tu n'as pas besoin (ni à définir) son mot de passe.",
            ["guardian.consent.intro"]       =
                "En créant ce profil, tu confirmes être le représentant " +
                "légal de cet enfant. En tant que tel, tu conserves le " +
                "contrôle complet de son compte :",
            ["guardian.consent.duties_invitations"] =
                "Tu dois approuver ou refuser toutes les invitations à des " +
                "groupes et à des événements.",
            ["guardian.consent.duties_chat"] =
                "Tu peux activer ou désactiver les fonctions de discussion " +
                "pour ce profil à tout moment.",
            ["guardian.consent.duties_data"] =
                "Ces données sont entièrement isolées à l'instance propre de " +
                "la communauté et ne seront jamais vendues, utilisées pour " +
                "créer des profils ou à des fins publicitaires.",
            ["guardian.consent.checkbox"]    =
                "J'accepte le traitement des données de mon enfant sous ces " +
                "conditions.",

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
            ["guardian.assign.success"]      = "Tuteur assigné — il/elle sera invité(e) à accepter.",

            // ── guardian acceptance lane (GA ADR 0038 §F) ──────────────────
            ["guardian.pending"]              = "En attente",
            ["guardian.pendingRequests.title"] =
                "Demandes de tutelle en attente de ton acceptation",
            ["guardian.pendingRequests.lead"] =
                "Un autre tuteur t'a demandé de devenir co-tuteur pour l'un de ses enfants. " +
                "Accepte (et accepte les conditions du compte enfant) ou refuse — tant que tu n'agis pas, tu n'as aucun droit sur le compte.",
            ["guardian.pendingRequests.child"]    = "Enfant",
            ["guardian.pendingRequests.conferrer"] = "Demandé par",
            ["guardian.accept"]                   = "Accepter et accepter les conditions",
            ["guardian.accept.consent.intro"]     =
                "En acceptant, tu confirmes que tu es un tuteur légal de cet enfant. " +
                "En tant que tuteur, tu conserves la pleine contrôle sur son compte :",
            ["guardian.accept.consent.duties_invitations"] =
                "Tu dois approuver ou refuser toutes les invitations à des groupes et événements.",
            ["guardian.accept.consent.duties_chat"] =
                "Tu peux activer ou désactiver les fonctionnalités de discussion pour ce profil à tout moment.",
            ["guardian.accept.consent.duties_data"] =
                "Ces données sont entièrement isolées sur l'instance propre de cette communauté et ne seront jamais vendues, profilées ou utilisées à des fins publicitaires.",
            ["guardian.accept.consent.checkbox"] =
                "Je consens au traitement des données de cet enfant sous ces conditions.",
            ["guardian.accept.consent.required"] =
                "Tu dois accepter les conditions du compte enfant avant d'accepter.",
            ["guardian.decline"] = "Refuser",

            // ── The lane — event attendance (the guardian's three postures) ──
            ["guardian.eventrsvp.title"] = "Participation aux événements",
            ["guardian.eventrsvp.description"] =
                "Choisis comment la participation de cet enfant aux événements est gérée.",
            ["guardian.eventrsvp.mode_approves_active"] =
                "Actuellement : le tuteur approuve — J'approuve ou refuse chaque événement auquel l'enfant veut participer.",
            ["guardian.eventrsvp.mode_notifies_active"] =
                "Actuellement : le tuteur est informé — L'enfant participe librement ; je suis prévenu(e) et peux retirer toute participation par la suite.",
            ["guardian.eventrsvp.mode_childdecides_active"] =
                "Actuellement : l'enfant décide — L'enfant choisit sa propre participation ; aucune approbation et aucune notification.",
            ["guardian.eventrsvp.switch_to_approves"] = "Passer à « le tuteur approuve »",
            ["guardian.eventrsvp.switch_to_notifies"] = "Passer à « le tuteur est informé »",
            ["guardian.eventrsvp.switch_to_childdecides"] = "Passer à « l'enfant décide »",
            ["guardian.eventrsvp.pending_title"] = "Demandes de participation en attente",
            ["guardian.eventrsvp.pending_empty"] = "Aucune demande de participation en attente.",
            ["guardian.eventrsvp.desired"] = "Veut participer",
            ["guardian.eventrsvp.approve"] = "Approuver",
            ["guardian.eventrsvp.deny"] = "Refuser",
            ["guardian.eventrsvp.rsvps_title"] = "Participation actuelle de cet enfant",
            ["guardian.eventrsvp.rsvps_empty"] = "Aucune participation actuelle à retirer.",
            ["guardian.eventrsvp.veto"] = "Retirer",
            ["guardian.eventrsvp.approve_confirm"] =
                "Approuver la participation de cet enfant à cet événement ?",
            ["guardian.eventrsvp.deny_confirm"] =
                "Refuser la participation de cet enfant à cet événement ? Il ne participera pas.",
            ["guardian.eventrsvp.veto_confirm"] =
                "Retirer la participation de cet enfant à cet événement ? Son inscription sera supprimée.",

            // ── notification kinds (the lane) ────────────────────────────────
            ["notifications.kind.guardian.event_request"] =
                "Demande de participation de ton enfant",
            ["notifications.preference.guardian.event_request.label"] =
                "Quand ton enfant veut participer à un événement",
            ["notification.guardian.event_request.subject"] =
                "Ton enfant veut participer à un événement",
            ["notification.guardian.event_request.body"] =
                "Ton enfant veut participer à un événement : ",
            ["notifications.kind.guardian.event_rsvp"] =
                "Ton enfant a participé à un événement",
            ["notifications.preference.guardian.event_rsvp.label"] =
                "Quand ton enfant participe à un événement",
            ["notification.guardian.event_rsvp.subject"] =
                "Ton enfant a participé à un événement",
            ["notification.guardian.event_rsvp.body"] =
                "Ton enfant a participé à un événement : ",

            // ── notification kind (GA ADR 0038 §F) ──────────────────────────
            ["notifications.kind.guardian.assign"] =
                "Un tuteur t'a demandé de devenir co-tuteur",
            ["notifications.preference.guardian.assign.label"] =
                "Quand un tuteur te demande de devenir co-tuteur",
            ["notification.guardian.assign.subject"] =
                "Un tuteur t'a demandé de devenir co-tuteur",
            ["notification.guardian.assign.body"] =
                "Un tuteur t'a demandé de devenir co-tuteur pour son enfant : ",

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

            ["settings.pagesize_title"]        = "Éléments par page",
            ["settings.pagesize_lede"]         =
                "Choisissez le nombre d'éléments affichés par page (flux, événements, projets, etc.). " +
                "Votre choix est enregistré sur votre compte — il prend effet au prochain chargement d'une liste, " +
                "sans jamais affecter les autres résidents.",
            ["settings.pagesize_label"]        = "Éléments par page",
            ["settings.pagesize_default_marker"] = "— défaut de la plateforme",
            ["settings.pagesize_reset_confirm"]  = "Réinitialiser les éléments par page au défaut de la plateforme ?",
            ["settings.pagesize_reset"]        = "Réinitialiser au défaut de la plateforme",
            ["settings.pagesize_save"]         = "Enregistrer",
            ["settings.pagesize_flash_set"]    = "Éléments par page réglés sur \"{0}\" — cela prend effet à la prochaine requête.",
            ["settings.pagesize_flash_reset"]  = "Éléments par page réinitialisés — le défaut de la plateforme sera utilisé.",

            // ── settings — home page (hide-home-intro preference, ADR 0149) ─
            ["settings.home_title"]        = "Page d'accueil",
            ["settings.home_lede"]         =
                "La page d'accueil s'ouvre avec deux sections d'introduction (ce qu'est Kumunita et ce qu'elle fait) " +
                "avant le fil. Activez cette option pour arriver directement au fil. " +
                "Votre choix est enregistré sur votre compte et n'affecte jamais les autres résidents.",
            ["settings.home_label"]        = "Masquer les sections d'introduction et m'afficher directement le fil",
            ["settings.home_note"]         = "Désactivé, la page d'accueil affiche ses sections d'introduction comme d'habitude.",
            ["settings.home_save"]         = "Enregistrer",
            ["settings.home_flash_hide"]   = "Page d'accueil mise à jour — les sections d'introduction sont masquées, le fil apparaît en premier.",
            ["settings.home_flash_show"]   = "Page d'accueil mise à jour — les sections d'introduction réapparaîtront.",

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

            // ── admin — announcement-comments gate (ADR 0101) ────────────────
            ["admin.anncomments_title"]     = "Commentaires sur les annonces",
            ["admin.anncomments_lede"]      =
                "Si les habitants connectés peuvent commenter les annonces. " +
                "Désactivé, la liste des commentaires et le champ de saisie " +
                "sont masqués sur chaque annonce — personne ne peut plus en " +
                "ajouter. Les visiteurs ne pouvaient de toute façon pas " +
                "commenter, et les commentaires existants sont conservés — " +
                "le bouton ne contrôle que les nouveaux commentaires.",
            ["admin.anncomments_on"]        = "Ouvert — les habitants connectés peuvent commenter",
            ["admin.anncomments_off"]       = "Fermé — aucun habitant ne peut ajouter de commentaire",
            ["admin.anncomments_save"]      = "Enregistrer",

            // ── admin — la passerelle de messagerie directe (M9, ADR 0105) ──
            ["admin.messaging_title"]   = "Messagerie directe",
            ["admin.messaging_lede"]    =
                "Si les habitants connectés peuvent ouvrir des conversations directes 1:1. " +
                "Désactivé, l'entrée Messages est masquée et toute demande de " +
                "conversation est refusée — personne ne peut ouvrir un fil ni " +
                "envoyer un message. La passerelle est fermée par défaut ; elle ne " +
                "contrôle que la messagerie nouvelle, et les conversations et " +"messages " +
                "existants ne sont jamais touchés. Même une GlobalAdmin qui n'est " +
                "pas participante ne peut pas lire une conversation.",
            ["admin.messaging_on"]      = "Ouverte — les habitants connectés peuvent s'envoyer des messages directs",
            ["admin.messaging_off"]     = "Fermée — aucun habitant ne peut ouvrir de conversation ni envoyer de message",

            // ── account — sign-up-closed notice (ADR 0050) ─────────────────
            ["account.signup_closed_title"] = "L'inscription est fermée",
            ["account.signup_closed_body"]  =
                "L'inscription est actuellement réservée aux invitations sur cette instance. " +
                "Si tu as été invité·e, une personne administratrice créera ton compte et t'enverra le lien de connexion.",

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
            ["home.roadmap.show_more_earlier"] = "Afficher les {n} jalons précédents",
            ["home.roadmap.show_more_upcoming"]  = "Afficher les {n} prochains jalons",
            ["home.roadmap.status.done"]    = "Terminé",
            ["home.roadmap.status.next"]    = "En cours",
            ["home.roadmap.status.planned"] = "Prévu",

            // ── chaînes du JS client (P0-6 : le bundle #kumunita-strings) ─
            ["common.close"]  = "Fermer",
            ["common.cancel"] = "Annuler",
            ["rc.editor.error_generic"] = "Quelque chose s'est mal passé.",
            ["rc.editor.link.title"]    = "Insérer un lien",
            ["rc.editor.link.url"]      = "URL",
            ["rc.editor.link.confirm"]  = "Insérer le lien",
            ["rc.editor.link.busy"]     = "Insertion…",
            ["rc.editor.link.err_empty"]    = "Saisis une URL.",
            ["rc.editor.link.err_invalid"]  = "Saisis un lien valide (adresse web, courriel ou chemin relatif au site).",
            ["rc.editor.image.title"]   = "Modifier l'image",
            ["rc.editor.image.err_rejected"] = "La source de l'image téléversée a été refusée.",
            ["rc.editor.image.err_upload"]   = "L'envoi a échoué.",
            ["rc.editor.attach.title"]  = "Joindre un fichier",
            ["rc.editor.attach.file"]   = "Fichier",
            ["rc.editor.attach.link_text"] = "Texte du lien",
            ["rc.editor.attach.default_label"] = "Pièce jointe",
            ["rc.editor.attach.confirm"]  = "Joindre",
            ["rc.editor.attach.busy"]     = "Envoi en cours…",
            ["rc.editor.attach.err_no_file"] = "Choisis un fichier à joindre.",
            ["img.edit.close"]       = "Fermer",
            ["img.edit.crop_area"]   = "Zone de rognage",
            ["img.edit.width"]       = "Largeur",
            ["img.edit.output"]      = "Résultat",
            ["img.edit.reset_crop"]  = "Réinitialiser le rognage",
            ["img.edit.use_original"] = "Utiliser l'original",
            ["img.edit.apply"]       = "Appliquer",
            ["img.edit.title"]       = "Rogner ton avatar",
            ["img.edit.err_edit"]    = "La modification a échoué.",
            ["img.edit.err_could"]   = "L'image n'a pas pu être modifiée.",
            ["img.edit.err_load"]    = "L'image n'a pas pu être chargée pour la modification.",
            ["img.edit.err_export"]  = "L'export de l'image a échoué.",
            ["tag.suggest.remove_prefix"] = "Supprimer l'étiquette : ",
            ["notif.fallback"]       = "Notifications",

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

            // ── ADR 0138 — le changement de mot de passe (fr) ──
            ["account.change_password_title"] = "Changer le mot de passe",
            ["account.change_password_lede"] =
                "Choisis un nouveau mot de passe pour ton compte. Après l'enregistrement, " +
                "tu seras déconnecté·e et demandé·e de te reconnecter avec le nouveau mot de passe.",
            ["account.change_password_current"] = "Mot de passe actuel",
            ["account.change_password_new"] = "Nouveau mot de passe",
            ["account.change_password_confirm_new"] = "Confirmer le nouveau mot de passe",
            ["account.change_password_submit"] = "Changer le mot de passe",
            ["account.change_password_locked_title"] = "Les changements de mot de passe sont verrouillés",
            ["account.change_password_locked_body"] =
                "Ceci est un compte de démonstration et les changements de mot de passe " +
                "sont verrouillés par l'administrateur, afin que tout le monde puisse " +
                "continuer à utiliser les identifiants partagés. Tu peux continuer à " +
                "utiliser toutes les autres fonctionnalités de la plateforme.",
            ["account.change_password_back"] = "Retour à ton profil",

            ["nav.change_password"] = "Changer le mot de passe",

            // ── ADR 0142 — la suppression de compte (fr) ──
            ["account.delete_title"] = "Supprimer le compte",
            ["account.delete_lede"] =
                "Supprimer ton compte supprime ta connexion, ton profil et " +
                "tes affiliations à des groupes et à des communautés. Tes " +
                "actions passées dans le journal d'audit de la plateforme " +
                "sont conservées, ton identité étant remplacée par un " +
                "identifiant anonyme (politique de confidentialité de la " +
                "plateforme, OPS.md §9). Cette opération est irréversible.",
            ["account.delete_password"] = "Mot de passe",
            ["account.delete_confirm_checkbox"] =
                "Je comprends que mon compte sera définitivement supprimé et " +
                "que cette opération est irréversible.",
            ["account.delete_submit"] = "Supprimer le compte",
            ["account.delete_refused"] =
                "La voie d'auto-suppression n'est disponible qu'aux " +
                "administrateurs globaux. Un résident non-administrateur " +
                "ne peut pas supprimer son propre compte — contacte un " +
                "administrateur pour supprimer le compte.",

            ["nav.delete_account"] = "Supprimer le compte",

            ["admin.delete_account_label"] = "Supprimer le compte",
            ["admin.delete_account_confirm"] =
                "Supprimer définitivement ce compte ? Son journal d'audit " +
                "est conservé (pseudonymisé) ; le compte, le profil et les " +
                "affiliations sont supprimés. Cette opération est " +
                "irréversible.",

            ["admin.sample_title"] = "Données d'exemple",
            ["admin.sample_lede"] =
                "Cette instance exécute le quartier de démonstration (données d'exemple). " +
                "Verrouille les comptes d'exemple pour qu'ils ne puissent pas changer " +
                "leur propre mot de passe, afin que les visiteurs puissent tester les " +
                "fonctionnalités sans casser les identifiants partagés — l'administrateur " +
                "de démonstration conserve sa propre voie de mot de passe.",
            ["admin.sample_lock_label"] = "Changements de mot de passe des comptes d'exemple",
            ["admin.sample_lock_on"] = "Verrouillé — les comptes d'exemple ne peuvent pas changer leur mot de passe",
            ["admin.sample_lock_off"] = "Déverrouillé — les comptes d'exemple peuvent changer leur mot de passe",
            ["account.login.error.blocked"] =
                "Votre compte a été suspendu temporairement. Contactez un administrateur.",
            ["account.login.error.removed"] =
                "Votre compte a été supprimé. Contactez un administrateur.",
            ["account.login.error.role_changed"] =
                "Votre rôle a été modifié. Veuillez vous reconnecter.",

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
                "Ces sélections sont masquées tant que « Tout le monde dans cette " +
                "communauté » est actif — la publication est visible par toute la " +
                "communauté. Désactive-le pour restreindre l'accès avec les " +
                "sélections ci-dessous.",
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
            ["groups.danger_heading"]   = "Zone de danger",
            ["groups.danger_delete_hint"] =
                "Supprimer le groupe le retire, ainsi que ses membres et toutes les invitations en attente. Cette action est irréversible.",
            ["groups.danger_delete_button"] = "Supprimer ce groupe",
            ["groups.new_title"]      = "Publier dans ce groupe",
            ["groups.new_back"]       = "retour au groupe",
            ["groups.new_submit"]     = "Publier dans le groupe",
            // ADR 0089 (GE) — the group-events lane (fr).
            ["groups.back"]           = "Retour à",
            ["groups.events_heading"] = "Événements",
            ["groups.new_event"]      = "Nouvel événement",
            ["groups.events_empty_can"] =
                "Aucun événement. Prévois le premier — il sera visible par les membres actuels.",
            ["groups.events_empty"]    = "Il n'y a encore aucun événement ici.",
            ["groups.new_event_title"] = "Événement pour ce groupe",
            ["groups.new_event_lead"]  =
                "Ton événement ne sera visible que par les membres actuels de ce groupe.",
            ["groups.new_event_submit"] = "Ajouter l'événement au groupe",
            ["groups.edit_event_title"] = "Modifier cet événement",
            // ── groups list (the Airy layout — the invitation panel + the
            //    member-count word on each group card) ───────────────────────
            ["groups.invitations"]    = "Invitations",
            ["groups.invitations_pending"] = "en attente",
            ["groups.invited_by"]     = "Invité par",
            ["groups.invite_accept"]  = "Accepter",
            ["groups.invite_decline"] = "Refuser",
            // ── ADR 0094 — the resident self-initiated join-request lane ──
            ["groups.join_requests"]              = "Demandes d'adhésion",
            ["groups.join_requests_pending"]      = "en attente",
            ["groups.join_requests_note"]         = "En attente du propriétaire du groupe.",
            ["groups.join_withdraw"]              = "Retirer",
            ["groups.other_public"]               = "Autres groupes publics",
            ["groups.request_join"]               = "Demander à rejoindre",
            ["groups.join_requests_pending_heading"] = "Demandes d'adhésion en attente",
            ["groups.join_approve"]               = "Approuver",
            ["groups.join_decline"]               = "Refuser",
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
            ["upload.max_size"] = "Taille maximale du fichier : {0}",
            ["profile.avatar_hint"] =
                "JPEG, PNG, WebP ou GIF. Enregistrer remplace " +
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

            // ── M23 (U04) — éditeur de profil étendu + détails du répertoire ──
            ["profile.edit.bio"] = "Bio",
            ["profile.edit.tags"] = "Tags",
            ["profile.edit.tags.placeholder"] = "ex. jardinage, pâtisserie, vélo…",
            ["profile.detail.bio"] = "À propos",
            ["profile.detail.tags"] = "Centres d'intérêt & compétences",
            ["profile.detail.tags.empty"] = "Aucun tag.",
            ["profile.flash.saved"] = "Profil mis à jour.",

            // ── M23 (U05) — la surface /people de recherche de personnes ──
            ["profile.find.title"]   = "Trouver des personnes",
            ["profile.find.by_tag"]  = "Trouver par tag",
            ["profile.find.by_bio"]  = "Trouver par bio",
            ["profile.find.results"] = "{0} personnes trouvées",
            ["profile.find.empty"]   = "Personne ne correspond — pour l'instant.",

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
                "selon la décision d'audience unique de cette publication. " +
                "Tu réponds uniquement là où la publication elle-même est visible.",
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
            ["pages.section_community"] = "Pages de la communaut\u00e9",
            ["pages.section_platform"]  = "Pages de la plateforme",
            ["pages.section_platform_lede"] =
                "Publi\u00e9es par la plateforme — conditions, aide, confidentialit\u00e9 " +
                "et r\u00e8gles de conduite.",

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
            // M9 amendment (ADR 0139) — "Send a message" (two-sided gate).
            ["directory.send_message"] = "Envoyer un message",
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
            // ADR 0146 — la prise de relais du compte-enfant (la page de
            // confirmation collecte le mot de passe de l'enfant avant
            // d'activer le compte).
            ["account.verify_set_password_lede"] =
                "Définis le mot de passe de ce compte — c'est toi qui va t'en servir.",
            ["account.verify_set_password_submit"] = "Définir mon mot de passe & me connecter",
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

            // ── admin (Analytics page — M13, ADR 0114 D4) ─────────────────
            ["admin.analytics_title"] = "Analyse d'usage",
            ["admin.analytics_lede"] =
                "Un résumé local de l'usage de la plateforme — nombre de requêtes, " +
                "connecté / anonyme, comptes distincts et classement par surface, " +
                "sur une fenêtre fixe. Aucun détail par compte ; les lignes " +
                "brutes ne sont accessibles que dans la base de données de l'opérateur.",
            ["admin.analytics_window"] = "Fenêtre",
            ["admin.analytics_total"] = "Requêtes totales",
            ["admin.analytics_authenticated"] = "Connectés",
            ["admin.analytics_anonymous"] = "Anonymes",
            ["admin.analytics_distinct"] = "Comptes distincts",
            ["admin.analytics_surface"] = "Surface",
            ["admin.analytics_count"] = "Nombre",
            ["admin.analytics_export"] = "Exporter CSV",

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

            // ── announcement comments (ADR 0101) ─────────────────────────────
            ["announcements.comments"] = "Commentaires",
            ["announcements.comment_empty"] = "Pas encore de commentaires. Sois le premier à dire quelque chose.",
            ["announcements.comment_deleted"] = "Ce commentaire a été supprimé par son auteur.",
            ["announcements.comment_delete"] = "Supprimer",
            ["announcements.comment_reply"] = "Écrire un commentaire",
            ["announcements.comment_submit"] = "Commenter",
            ["announcements.comment_audience_note"] =
                "Les commentaires ne sont visibles que par les habitants " +
                "connectés — même sur une annonce publique — et suivent le " +
                "public de cette annonce (une annonce destinée à une " +
                "communauté est visible par les habitants de celle-ci).",

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

            // ── what's new (the VN lane, ADR 0110 — version changelog + toast) ──
            ["whatsnew.eyebrow"]        = "Nouveautés",
            ["whatsnew.heading"]        = "Nouveautés, version par version",
            ["whatsnew.lead"] =
                "Chaque publication est datée et listée ici — lis ce qui est " +
                "arrivé dans chaque version mineure de la plateforme que tu utilises.",
            ["whatsnew.version"]        = "Version",
            ["whatsnew.show_more"]      = "Afficher plus de versions",
            ["whatsnew.show_more_remaining"] = "Afficher les {n} versions précédentes",
            ["whatsnew.toast_label"]    = "Kumunita {v} est désormais en service.",
            ["whatsnew.toast_see"]      = "Voir les nouveautés",

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
            ["events.mine.show_more"] = "Afficher les {n} événements supplémentaires",

            // ── events.past (le basculement EV-PAST + état vide sur /events — ADR 0109) ──
            ["events.upcoming"] = "À venir",
            ["events.past"] = "Passés",
            ["events.past_empty"] = "Aucun événement passé pour l'instant.",

            // ── events.ics (les surfaces iCal M12 — page de détail + feed/calendrier, ADR 0112) ──
            ["events.ics.download"] = "Ajouter à l'agenda",
            ["events.ics.feed"] = "Flux de calendrier (iCal)",

            // ── M14 (ADR 0115 D2) — les deux libellés de lien U03 événements ↔ to-dos ──
            ["todo.event_link"] = "Événement lié",
            ["events.linked_todos"] = "To-dos liés",

            // ── M14 (ADR 0115 D3) — les deux libellés du sélecteur U04 set-event
            //    (détail de la to-do : libellé du formulaire + option
            //    placeholder / effacement) ──
            ["todo.set_event.label"] = "Lier à un événement",
            ["todo.set_event.pick"] = "Choisir un événement",

            // ── M14 (ADR 0115 D4) — les deux surfaces VTODO U06 (détail de la
            //    to-do : lien « Ajouter à l'agenda » + index des to-dos : lien
            //    de flux ; la forme events.ics. d'ADR 0112 portée à la surface
            //    to-do ; liens <a> simples, pas de nouveau JS — le registre
            //    de clés fermé + KnownTranslationKeys_ParityTests appliquent × 4) ──
            ["projects.todos.ics.download"] = "Ajouter à l'agenda",
            ["projects.todos.ics.feed"] = "Flux de calendrier (iCal)",

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
            // ADR 0146 — le texte du compte-enfant : la seule étape restante
            // est de définir le propre mot de passe de l'enfant (le
            // représentant légal a créé le compte mais ne l'a jamais
            // détenu).
            ["email.verify_child_body"] =
                "Bonjour {0},\n\nTon compte Kumunita est prêt. Ouvre ce lien à usage " +
                "unique pour confirmer le compte et définir ton mot de passe (il te " +
                "connecte aussi) :\n\n{1}\n\n" +
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
            ["notifications.mark_read"] = "Marquer comme lu",
            ["notifications.mark_unread"] = "Marquer comme non lu",
            ["notifications.empty"] = "Rien pour l'instant — les choses qui t'arrivent apparaîtront ici.",
            ["notifications.bell"] = "Notifications",
            ["notifications.view"] = "Voir",
            ["notifications.accept"] = "Accepter",
            ["notifications.decline"] = "Refuser",
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
            // ── M9 (ADR 0105, U04) — la surface résident : navigation, liste, fil, rédaction ──
            ["message.nav"] = "Messages",
            ["message.title"] = "Messages",
            ["message.new"] = "Nouvelle conversation",
            ["message.thread.empty"] = "Pas encore de messages — dites bonjour.",
            ["message.compose.placeholder"] = "Écrire un message…",
            ["message.compose.send"] = "Envoyer",
            ["message.unread"] = "non lu",
            ["message.disabled"] = "La messagerie directe est désactivée sur cette instance.",
            ["message.compose.disabled"] = "Cette personne a désactivé la messagerie directe, vous ne pouvez donc pas lui envoyer de nouveau message.",
            ["message.other"] = "l'autre personne",
            ["message.sent_to"] = "Envoyé à {0}.",
            ["message.load_earlier"] = "Charger les messages antérieurs",
            ["notifications.subscriptions.intro"] = "Choisis quelles communautés, quels groupes et quelles pages t'informent. Les préférences décident des types que tu reçois aussi par e-mail ; ces interrupteurs décident des cibles qui t'informent.",
            ["notifications.subscription.announcement.label"] = "Nouvelles annonces",
            ["notifications.subscription.community.post.label"] = "Nouvelles publications dans les communautés",
            ["notifications.subscription.group.post.label"] = "Nouvelles publications dans les groupes",
            ["notifications.subscription.page.child.label"] = "Nouvelles sous-pages",
            // ── M9 (ADR 0105, U03) — l'espèce message.new + les modèles ──
            ["notifications.kind.message.new"] = "Nouveau message",
            ["notifications.preference.message.new.label"] = "Messages d'autres résidents",
            ["notification.message.new.subject"] = "Nouveau message",
            ["notification.message.new.body"] = "Un résident t'a envoyé un message : ",

            // ── GU community-approval lane (ADR 0141) — l'art du tuteur ──
            ["notifications.kind.guardian.group_invite"] =
                "Invitation à un groupe pour ton enfant",
            ["notifications.preference.guardian.group_invite.label"] =
                "Quand un groupe invite ton enfant",
            ["notification.guardian.group_invite.subject"] =
                "Un groupe a invité ton enfant",
            ["notification.guardian.group_invite.body"] =
                "Un groupe a invité ton enfant : ",
            ["notifications.kind.guardian.community_invite"] =
                "Membre d'une communauté pour ton enfant",
            ["notifications.preference.guardian.community_invite.label"] =
                "Quand une communauté ajoute ton enfant",
            ["notification.guardian.community_invite.subject"] =
                "Une communauté a ajouté ton enfant",
            ["notification.guardian.community_invite.body"] =
                "Une communauté a ajouté ton enfant : ",

            // ── GU community-approval lane (ADR 0141) — la page enfant ──
            ["guardian.pending_community_requests"] = "Communautés en attente",
            ["guardian.no_community_requests"] = "Aucune communauté en attente.",
            ["guardian.reject"] = "Refuser",

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

            // ── M7 (ADR 0090) — the shared pager's two link labels (D5) ──
            ["pagination.prev"] = "Plus récents",
            ["pagination.next"] = "Plus anciens",

            // M8 (ADR 0091 D1/D4) — la page /search unique + l'entrée de navigation.
            ["search.nav"] = "Recherche",
            ["search.title"] = "Recherche",
            ["search.placeholder"] = "Rechercher dans les publications, événements, pages, annonces, projets, tableaux, tâches, inventaire, documents et personnes…",
            ["search.no-results"] = "Aucun résultat pour",
            ["search.section.posts"] = "Publications",
            ["search.section.events"] = "Événements",
            ["search.section.pages"] = "Pages",
            ["search.section.announcements"] = "Annonces",
            ["search.section.projects"] = "Projets",
            ["search.section.boards"] = "Tableaux",
            ["search.section.todos"] = "Tâches",
            ["search.section.inventory"] = "Inventaire",
            ["search.section.documents"] = "Documents",
            ["search.section.people"] = "Personnes",
            ["search.scope.community"] = "Communauté",
            ["search.scope.groups"] = "Groupes",
            ["search.empty.hint"] = "Retrouvez des publications, événements, pages, annonces, projets, tableaux, tâches, inventaire, documents et personnes par texte ou étiquette — seules les contenus que vous pouvez déjà lire s'affichent.",

            // M10 (ADR 0107 D10) — le libellé du seul affordance d'installation
            // (U03, pwa-install.ts). Pas de bannière, pas de modale — un bouton.
            ["pwa.install"] = "Installer l'application",

            // M11 (ADR 0108 D10) — la surface opérateur de portabilité
            // (l'index /admin/portability : le bouton d'export, le formulaire
            // d'import + sa garde destructive, les deux rendus de statut).
            ["portability.index.title"]  = "Portabilité",
            ["portability.export"]       = "Exporter",
            ["portability.import"]       = "Importer",
            ["portability.confirm.import"] = "Importer cette archive ? Elle remplace le contenu de l'instance (le chemin de restauration — la sauvegarde pré-import de l'opérateur est le retour arrière).",
            ["portability.status.ok"]    = "Terminé.",

            // M15 U05 (ADR 0116, D8) — les clés de lot côté éditeur
            // (bouton « tout enregistrer » + les deux bascules de mode).
            ["translations.bulk.save_all"]   = "Tout enregistrer",
            ["translations.bulk.mode_batch"] = "Édition par lot",
            ["translations.bulk.mode_single"] = "Édition une par une",
            ["portability.status.failure"] = "Refusée — l'archive a été rejetée avant toute écriture :",

            // M27 (ADR 0148 D9) — la surface de portabilité du résident / de la
            // résidente (l'index /account/portability : le bouton d'export, le
            // formulaire d'import + l'aire de statut ; U07). Absolument un
            // espace de noms myportability.* DISTINCT de celui de l'admin
            // portability.* (M11), pour que la surface résident ne collisionne
            // jamais avec la surface opérateur.
            ["myportability.index.title"]  = "Mes données",
            ["myportability.export"]       = "Exporter",
            ["myportability.import"]       = "Importer",
            ["myportability.import.resolve"] = "Appliquer mes choix",
            ["myportability.resolve.add_elsewhere"] = "Ajouter ailleurs",
            ["myportability.resolve.discard"] = "Rejeter",
            ["myportability.status"]       = "Statut",

            // M15 U04 (ADR 0116, D8) — les clés de lot côté fichier.
            ["translations.bulk.export"]     = "Télécharger les traductions (CSV)",
            ["translations.bulk.import"]     = "Téléverser les traductions (CSV)",
            ["translations.bulk.import_hint"] = "Les cellules vides sont ignorées (elles n'effacent jamais une traduction) ; un fichier contenant une clé inconnue ou une langue inconnue est refusé sans modification.",

            // M16 (ADR 0117, D1) — la surface inventaire (prêt / retour + la
            // section historique + l'entrée de navigation). L'ensemble fermé
            // des clés est le §kw-l du document de conception.
            ["inv.nav"]                     = "Inventaire",
            ["inv.list.title"]              = "Inventaire",
            ["inv.list.empty"]              = "Aucun article pour l'instant.",
            ["inv.list.ownerKind.shared"]   = "Partagé",
            ["inv.list.ownerKind.community"] = "Communauté",
            ["inv.list.ownerKind.private"]  = "Privé",
            ["inv.list.filter"]             = "Filtrer par type",
            ["inv.create.title"]            = "Nouvel article",
            ["inv.create.name"]             = "Nom",
            ["inv.create.ownerKind"]        = "Type",
            ["inv.create.description"]      = "Description",
            ["inv.create.component"]        = "Section",
            ["inv.create.submit"]           = "Créer l'article",
            ["inv.detail.title"]            = "Article",
            ["inv.detail.currentHolder"]    = "Actuellement chez",
            ["inv.detail.history"]          = "Historique d'utilisation",
            ["inv.detail.edit"]             = "Modifier",
            ["inv.detail.delete"]           = "Supprimer",
            ["inv.detail.checkOut"]         = "Emprunter",
            ["inv.detail.checkIn"]          = "Restituer",
            ["inv.edit.title"]              = "Modifier l'article",
            ["inv.edit.submit"]             = "Enregistrer les modifications",

            // M17 (ADR 0118) — Signets : la surface de repères personnels
            // (D6 : surface cœur permanente, pas de bascule admin). L'ensemble
            // fermé des clés est le §kw-l du document de conception (14 clés :
            // entrée de navigation, libellés de liste / dégradé / type,
            // action de retrait + bouton bascule + clés obs-2 toast).
            ["bm.nav"]                      = "Signets",
            ["bm.list.title"]               = "Tes signets",
            ["bm.list.empty"]               = "Aucun signet pour l'instant.",
            ["bm.list.degraded"]            = "Non plus disponible",
            ["bm.list.kind.post"]           = "Publications",
            ["bm.list.kind.event"]          = "Événements",
            ["bm.list.kind.todo"]           = "À faire",
            ["bm.list.kind.announcement"]   = "Annonces",
            ["bm.list.kind.page"]           = "Pages",
            ["bm.list.unbookmark"]          = "Retirer",
            ["bm.button.bookmark"]          = "Signeter",
            ["bm.button.bookmarked"]        = "Signeté",
            // Obs-2 (ADR 0118 amendement) — clés du toast de redirection.
            ["bm.toggle.bookmarked"]        = "Signeté.",
            ["bm.toggle.removed"]           = "Signet retiré.",

            // ── M21 (ADR 0122) — gestion des documents : la liste /documents +
            // la page détail + le formulaire de dépôt (D5 standing, D6
            // téléchargement, D7 refus → 404). U04 crée le jeu fermé de 12
            // clés ; les vues de U03 le consomment.
            ["documents.title"]             = "Documents",
            ["documents.empty"]             = "Aucun document n'est visible pour l'instant.",
            ["documents.upload"]            = "Téléverser un document",
            ["documents.upload_title"]      = "Téléverser un document",
            ["documents.upload_summary"]    = "Description en une ligne (facultatif)",
            ["documents.upload_file"]       = "Fichier",
            ["documents.upload_audience"]   = "Qui peut voir ce document",
            ["documents.upload.submit"]     = "Téléverser le document",
            ["documents.download"]          = "Télécharger",
            ["documents.detail.type_size"]  = "Type / taille du fichier",
            ["documents.detail.updated"]    = "Dernière mise à jour",
            ["documents.flash_uploaded"]    = "Document téléversé.",
            // ADR 0125 (U03) — la lane d'édition réservée au propriétaire.
            ["documents.edit"]              = "Modifier",
            ["documents.edit_title"]        = "Modifier un document",
            ["documents.edit_summary"]      = "Description en une ligne (facultatif)",
            ["documents.edit_file"]         = "Remplacer le fichier (facultatif)",
            ["documents.edit_file_hint"]    = "Laisser vide pour conserver le fichier actuel.",
            ["documents.edit_audience"]     = "Qui peut voir ce document",
            ["documents.edit.submit"]       = "Enregistrer les modifications",
            ["documents.flash_edited"]      = "Document mis à jour.",

            // La lane "organiser les documents" (tags + dossiers) — les clés
            // documents.folder_* / documents.tags.* + les clés flash des
            // routes DocumentFolderController (create / rename / move / delete /
            // document-move).
            ["documents.folder"]              = "Dossier",
            ["documents.folder_unfiled"]      = "Non classé",
            ["documents.folder_hint"]         = "Classez ce document dans un dossier pour garder l'archive organisée.",
            ["documents.folder_new"]          = "Nouveau dossier",
            ["documents.folder_create"]       = "Créer",
            ["documents.folder_name_placeholder"] = "Nom du dossier",
            ["documents.tags"]                = "Étiquettes",
            ["documents.tags_hint"]           = "Écris pour chercher des étiquettes existantes, ou en créer une nouvelle.",
            ["documents.flash_moved"]         = "Document déplacé.",
            ["documents.folder_flash_created"] = "Dossier créé.",
            ["documents.folder_flash_renamed"] = "Dossier renommé.",
            ["documents.folder_flash_moved"]   = "Dossier déplacé.",
            ["documents.folder_flash_deleted"] = "Dossier supprimé.",

            // ── M22 (ADR 0132) — onboarding : le parcours guidé /onboarding
            // (D4) + le bandeau fermable d'accueil/nav (D5) + le flash
            // finish/skip (D2/D4). U03 crée le jeu FERMÉ COMPLET ; U02/U03 le
            // consomment. La pin de parité exige chaque clé présente, non
            // vide, dans les quatre langues (C-M22·6, GATE-6). ──
            ["onboarding.title"]            = "Configurer ton compte",
            ["onboarding.intro"]            = "Une brève visite guidée des quelques réglages qui font fonctionner Kumunita pour toi. Tout mène au réglage qui l'a déjà — termine en une minute ou reviens quand tu veux.",
            ["onboarding.step_displayname"] = "Ton nom d'affichage",
            ["onboarding.step_avatar"]      = "Ton avatar",
            ["onboarding.step_language"]    = "Ta langue d'interface",
            ["onboarding.step_timezone"]    = "Ton fuseau horaire",
            ["onboarding.step_dateformat"]  = "Ton format de date et d'heure",
            ["onboarding.step_email"]       = "Ta langue des e-mails et des notifications",
            ["onboarding.step_contact"]     = "Tes coordonnées et qui peut les voir",
            // M9 amendment (ADR 0139) — the messaging opt-in step.
            ["onboarding.step_messaging"]   = "Si tu peux utiliser la messagerie directe",
            ["onboarding.visit"]            = "Aller à ce réglage",
            ["onboarding.finish"]           = "Tout est prêt — terminer la configuration",
            ["onboarding.skip"]             = "Passer pour l'instant",
            ["onboarding.flash_done"]       = "Configuration terminée — bienvenue dans ton quartier.",
            ["onboarding.banner.text"]      = "Terminer la configuration de ton compte ?",
            ["onboarding.banner.action"]    = "Démarrer la configuration",

            // ── M9 amendment — le contrôle de messagerie par résident + le
            // plafond du tuteur (valeurs initiales, en fr, à réviser par un
            // traducteur ; le plancher du fournisseur ADR 0015 les résout). ──
            ["settings.messaging.title"]           = "Messagerie",
            ["settings.messaging.description"]     = "Choisis si d'autres résidents peuvent t'envoyer des messages directs 1:1. Ton choix est enregistré sur ton compte et prend effet immédiatement.",
            ["settings.messaging.instance_off"]    = "La messagerie est actuellement désactivée sur cette instance par un administrateur. Tu peux t'inscrire maintenant et la messagerie sera disponible dès qu'elle sera activée.",
            ["settings.messaging.restricted"]      = "La messagerie a été restreinte sur ton compte par un tuteur. Contacte-le pour changer cela.",
            ["settings.messaging.optin"]           = "Autoriser d'autres résidents à m'envoyer des messages directs 1:1",
            ["settings.messaging.save"]            = "Enregistrer la préférence de messagerie",
            ["guardian.messaging.title"]           = "Messagerie",
            ["guardian.messaging.description"]     = "Choisis si cet enfant peut utiliser la messagerie directe 1:1. Si restreinte, l'enfant ne peut ni envoyer ni recevoir de messages — ce choix prime sur son propre opt-in ; si autorisée, l'enfant décide par lui-même sur sa page de réglages de messagerie.",
            ["guardian.messaging.current_restricted"] = "La messagerie est actuellement restreinte pour cet enfant.",
            ["guardian.messaging.current_allowed"]    = "La messagerie est actuellement autorisée pour cet enfant.",
            ["guardian.messaging.child_optin_on"]     = "L'enfant s'est inscrit à la messagerie sur son propre compte.",
            ["guardian.messaging.child_optin_off"]    = "L'enfant ne s'est pas encore inscrit à la messagerie sur son propre compte — même si tu l'autorises, il devra s'inscrire sur sa page de réglages.",
            ["guardian.messaging.allow"]              = "Autoriser la messagerie",
            ["guardian.messaging.restrict"]           = "Restreindre la messagerie",

            // ── P1 audit (2026-10-04) ── même ensemble des ~73 clés que
            // <see cref="EnValues"/> (l'inventaire de l'audit de traduction P1).
            ["common.remove_translation_confirm"] =
                "Supprimer cette traduction ?",
            ["events.skip_occurrence_confirm"] =
                "Passer sur cette occurrence ? Tu peux la restaurer plus tard.",
            ["community.confirm_remove_member"] =
                "Retirer {0} de {1} ?",
            ["a11y.notifications"]             = "Notifications",
            ["a11y.find_tag"]                  = "Chercher par étiquette",
            ["a11y.find_bio"]                  = "Chercher par bio",
            ["a11y.avatar"]                    = "Image du profil",
            ["a11y.board_actions"]             = "Actions du tableau",
            ["a11y.calendar_view"]             = "Vue calendrier",
            ["a11y.event_time_range"]          = "Plage horaire de l'événement",
            ["a11y.check_out_note"]            = "Note d'emprunt",
            ["a11y.community_pages"]           = "Pages de la communauté",
            ["a11y.platform_pages"]            = "Pages de la plateforme",
            ["a11y.community_page_tree"]       = "Arbre des pages de la communauté",
            ["a11y.platform_page_tree"]        = "Arbre des pages de la plateforme",
            ["a11y.breadcrumb"]                = "Fil d'Ariane",
            ["a11y.about_features"]            = "Ce qu'est Kumunita",
            ["a11y.about_audience"]            = "Pour qui",
            ["a11y.about_philosophy"]          = "La philosophie",
            ["a11y.about_contact"]             = "Nous contacter",
            ["a11y.about_project"]             = "Le projet",
            ["a11y.set_limit"]                 = "Définir une limite sur {0}",
            ["account.block"]               = "Bloquer",
            ["account.unblock"]             = "Débloquer",
            ["account.confirm_block"]       = "Bloquer ce compte ? Il perd tous ses droits jusqu'au déblocage.",
            ["account.confirm_unblock"]     = "Débloquer ce compte ? Ses droits seront restaurés.",
            ["account.err.required"]        = "Le champ {0} est requis.",
            ["account.err.email"]           = "Le champ {0} n'est pas une adresse e-mail valide.",
            ["account.err.password_min"]    = "{0} doit contenir au moins {1} caractères.",
            ["account.err.password_mismatch"] = "Les champs {0} et {1} ne correspondent pas.",
            ["footer.feed"]                 = "Le fil",
            ["languages.title"]             = "Langues",
            ["languages.lede_admin"]        =
                "Gère les langues que cette instance prend en charge. Les changements prennent effet " +
                "à la prochaine requête — sans recompiler, sans redémarrage.",
            ["languages.lede_translator"]   =
                "Vérifie la couverture de traduction de la plateforme et mets à jour les chaînes " +
                "d'interface. Les changements prennent effet à la prochaine requête.",
            ["languages.add_heading"]       = "Ajouter une langue",
            ["languages.code_label"]        = "Code de langue",
            ["languages.native_name_label"] = "Nom natif",
            ["languages.code_hint"]         = "Code court, p. ex. pl pour polonais, no-NO pour norvégien.",
            ["languages.supported_heading"] = "Langues prises en charge",
            ["languages.th_code"]           = "Code",
            ["languages.th_native_name"]    = "Nom natif",
            ["languages.th_enabled"]        = "Activ",
            ["languages.th_ui_strings"]     = "Chaînes d'interface",
            ["languages.th_actions"]        = "Actions",
            ["languages.enabled"]           = "activ",
            ["languages.disabled"]          = "désactivé",
            ["languages.present"]           = "{n} présentes",
            ["languages.missing"]           = "{n} manquantes",
            ["languages.action_disable"]    = "Désactiver",
            ["languages.action_enable"]     = "Activer",
            ["languages.action_set_default"] = "Mettre par défaut",
            ["languages.action_ui_strings"] = "Chaînes d'interface",
            ["languages.action_remove"]     = "Retirer",
            ["languages.reorder_btn"]       = "Réordonner (confirmer l'ordre actuel)",
            ["languages.reorder_title"]     = "Envoyer l'ordre actuel (tel qu'affiché ci-dessous) comme nouvel ordre de tri",
            ["languages.reorder_hint"]      =
                "Le réordonnement par glisser-déposer n'est pas disponible ; le formulaire d'ordre " +
                "accepte les codes dans l'ordre où ils apparaissent ici. Pour changer l'ordre, " +
                "l'administrateur doit soumettre la liste dans l'ordre souhaité.",
            ["languages.confirm_remove"]    =
                "Retirer {0} ? Ses lignes de traduction sont conservées et seront restaurées si la " +
                "langue est réajoutée.",
            ["translations.editor.back"]         = "← Retour aux langues",
            ["translations.editor.title"]        = "Chaînes d'interface pour {0}",
            ["translations.editor.lede"]         =
                "La liste complète des chaînes que la plateforme affiche à ses résidents. Chaque ligne " +
                "montre la clé, son texte de référence en anglais et la valeur actuelle dans {0}. " +
                "Enregistrer une ligne ajoute ou met à jour sa traduction — visible à la prochaine " +
                "requête.",
            ["translations.editor.mode_label"]   = "Mode d'édition",
            ["translations.editor.th_key"]       = "Clé",
            ["translations.editor.th_en_reference"] = "Référence en anglais",
            ["translations.editor.th_value"]     = "Valeur dans {0}",
            ["translations.editor.value_aria"]   = "Valeur pour {0} dans {1}",
            ["events.rsvp_status_going"]     = "Va",
            ["events.rsvp_status_maybe"]     = "Peut-être",
            ["events.rsvp_status_no"]        = "Non",
            ["posts.delete_confirm"]         = "Supprimer cette publication ? Tes réponses resteront visibles.",
            ["posts.reply_delete_confirm"]   =
                "Supprimer cette réponse ? Elle sera remplacée par une note. L'enregistrement est " +
                "conservé.",
            ["announcements.delete_confirm"] = "Supprimer cette annonce ? Cela ne peut pas être annulé.",
            ["groups.confirm_delete"]        =
                "Supprimer ce groupe ? Cela supprime le groupe, ses membres et ses invitations en " +
                "attente, et cela ne peut pas être annulé.",
            ["community.confirm_leave"]      = "Quitter {0} ?",
            ["guardian.suspend_confirm"]     = "Suspendre ce compte enfant ? Il sera bloqué jusqu'à ce que tu le réactives.",
            ["guardian.messaging.allow_confirm"] =
                "Autoriser la messagerie pour cet enfant ? Il pourra envoyer et recevoir des messages " +
                "directs (son propre opt-in doit aussi être activé).",
            ["guardian.messaging.restrict_confirm"] =
                "Restreindre la messagerie pour cet enfant ? Il ne pourra plus envoyer ni recevoir de " +
                "messages directs, quelle que soit son propre opt-in.",
            ["guardian.handover_confirm"]    = "Transférer ce compte à l'enfant ? Cela dissout ta tutelle sur ce compte.",
            ["guardian.remove_myself_confirm"] =
                "Retirer ta tutelle sur ce compte ? Tu ne pourras plus gérer " +
                "le compte. Le compte est conservé, et les autres tuteurs " +
                "restent en place.",
            ["guardian.delete_child_confirm"] =
                "Supprimer définitivement ce compte d'enfant ? Cette opération " +
                "retire la connexion, le profil et les adhésions, et ne peut pas " +
                "être annulée.",
            ["locale.reset_confirm"]         = "Réinitialiser ta préférence de langue au défaut de l'instance ?",
            ["locale.email_reset_confirm"]   = "Réinitialiser la langue des e-mails et des notifications au défaut de l'instance ?",
            ["settings.quiet.clear_confirm"] =
                "Effacer tes heures de silence ? Tous les e-mails de notification seront à nouveau " +
                "envoyés immédiatement.",
            ["settings.timezone_reset_confirm"] = "Réinitialiser ta zone horaire à la valeur par défaut de la plateforme ?",
            ["settings.dateformat_reset_confirm"] = "Réinitialiser ton format de date et d'heure à la valeur par défaut de la plateforme ?",
            ["pages.form.body_hint"]         =
                "Facultatif — un nœud de dossier peut ne pas avoir de corps. Écrit en Markdown via " +
                "l'éditeur visuel (le même que les publications et les annonces).",
            ["pages.reset_confirm"]          =
                "Réinitialiser cette page au texte seedé ? Toutes les modifications que tu as " +
                "apportées au corps (anglais) et à ses traductions allemandes / françaises / danoises " +
                "seront écrasées par la baseline seedée. Le reste de la page (public, parent, etc.) " +
                "n'est pas touché.",
            ["pl.board.delete_confirm"]      =
                "Supprimer ce tableau ? Ses couloirs et placements de cartes sont supprimés — les " +
                "tâches elles-mêmes sont conservées.",
            ["pl.lane.delete_confirm"]       =
                "Supprimer ce couloir ? Ses cartes quittent ce tableau — les tâches elles-mêmes sont " +
                "conservées.",
            ["pl.todo.assignee_remove_confirm"] = "Retirer l'assignation de cette tâche ?",
            ["pl.todo.move_confirm"]         = "Déplacer cette tâche sur un autre tableau ? Elle ne sera plus sur ce tableau.",
            ["pl.todo.delete_confirm"]       = "Supprimer cette tâche et ses sous-tâches ? Cela ne peut pas être annulé.",
            ["inv.item.delete_confirm"]      = "Supprimer cet article ? Cela ne peut pas être annulé.",
            ["a11y.close"]                   = "Fermer",
            ["a11y.toggle_nav"]              = "Basculer la navigation",
            ["a11y.primary_nav"]             = "Principal",
            ["a11y.pagination"]              = "Pagination",
            ["a11y.pinned_announcement"]     = "Annonce épinglée",
            ["a11y.banner_read_more"]        = "Lire cette annonce en entier",
            ["a11y.banner_all"]              = "Voir toutes les annonces",
            ["a11y.banner_dismiss"]          = "Fermer cette annonce épinglée",
            ["a11y.onboarding_region"]       = "Configuration du compte",
            ["a11y.onboarding_action"]       = "Démarrer la configuration",
            ["a11y.onboarding_dismiss"]      = "Fermer cette bannière",
            ["a11y.whatsnew_region"]         = "Nouveautés",
            ["a11y.actions_for"]             = "Actions pour {0}",
            ["a11y.search"]                  = "Recherche",
            ["a11y.scope"]                   = "Portée",
            ["a11y.back_to_messages"]        = "Retour aux messages",
            ["a11y.resident"]                = "Résident",
            ["account.storage_title"]        = "Mon stockage",
            ["account.storage_lede"]         =
                "Combien de ton contenu la plateforme compte, ton quota par utilisateur et combien " +
                "il t'en reste encore.",
            ["account.storage_used"]         = "ton contenu utilisé",
            ["account.storage_quota"]        = "ton quota par utilisateur",
            ["account.storage_remaining"]    = "restant",
            ["account.storage_unlimited_note"] =
                "Ton quota par utilisateur est illimité — il n'y a pas de limite de contenu total sur " +
                "tes uploads.",
            ["account.storage_remaining_note"] =
                "Ton restant est ce qui reste de ton quota par utilisateur ({0}). Demande à un " +
                "administrateur d'augmenter le quota si tu en as besoin.",
            ["translations.bulk.export_title"] = "Télécharge les chaînes d'interface de cette langue au format fichier CSV",
            ["common.delete"]          = "Supprimer",
            ["pages.delete_confirm"]   = "Supprimer cette page ?",
            ["admin.help.reset_one"]   =
                "Réinitialiser « {0} » au texte seedé ? Cela écrase tout le contenu modifié " +
                "manuellement.",
            ["admin.help.reset_all"]   =
                "Réinitialiser TOUTES les {0} pages d'aide seedées au texte seedé ? Cela écrase tout " +
                "le contenu modifié manuellement sur chaque page.",

            // ── M26 U16 — le jeu fermé sort.* du partiel _Sort partagé
            // (labels only ; les huit clés de tri des listes autorisées des
            // surfaces U10–U15) ──
            ["sort.created"]          = "Créé",
            ["sort.modified"]         = "Modifié",
            ["sort.title"]            = "Titre",
            ["sort.size"]             = "Taille",
            ["sort.name"]             = "Nom",
            ["sort.start"]            = "Date de début",
            ["sort.due"]              = "Échéance",
            ["sort.status"]           = "Statut",
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
            // ── M19 (ADR 0120) — gæstekonti-overfladen: /admin/guests
            // admin-overfladen (D6) + den tilloggede gæsts modtagelse (D5) ──
            ["admin.guests_title"]               = "Gæstekonto",
            ["admin.guests_empty"]               = "Ingen gæstekonto endnu.",
            ["admin.guests_create"]              = "Opret gæst",
            ["admin.guests_window_label"]        = "Adgangsvindue",
            ["admin.guests_surfaces_label"]      = "Tilladte flader",
            ["admin.guests_surface_announcements"] = "Meddelelser",
            ["admin.guests_surface_events"]      = "Arrangementer",
            ["admin.guests_surface_directory"]   = "Kontaktliste",
            ["admin.guests_saved"]               = "Gæstestatus gemt.",
            ["account.guest_welcome"] =
                "Du er logget ind som gæst. Din adgang er begrænset til de " +
                "flader, administratoren har tilladt, for det vindue de har sat.",

            // ── M20 (ADR 0121) — notifikations-stumtid: den 5. sektion
            // /settings/quiet (D7) + overfladen /admin/quiet (D8). U06
            // authoriserer det fulde sæt på 15 nøgler; U06/U07 forbruger, U07
            // tilføjer ingen nøgler.
            ["settings.quiet.title"]        = "Stumtid",
            ["settings.quiet.description"]  = "Vælg hvornår dine notifikationse-mails holdes tilbage. Din " +
                "stumtid gemmes på din konto og anvendes i din egen tidssone — " +
                "den træder i kraft ved næste afsendelse af en notifikation og " +
                "betræffer aldrig andre beboere.",
            ["settings.quiet.enabled"]      = "Hold notifikationse-mails tilbage i min stumtid",
            ["settings.quiet.mode_label"]   = "Hvornår skal notifikationse-mails holdes tilbage?",
            ["settings.quiet.mode_blocked"] = "Holdes tilbage i de valgte timer & dage",
            ["settings.quiet.mode_allowed"] = "Holdes tilbage undtagen de valgte timer & dage",
            ["settings.quiet.hours_label"]  = "Timer på døgnet",
            ["settings.quiet.days_label"]   = "Ugedage",
            ["settings.quiet.save"]         = "Gem stumtid",
            ["settings.quiet.flash_saved"]  = "Stumtid gemt — tilbageholdte e-mails sendes, når din stumtid slutter.",
            ["settings.quiet.flash_cleared"] = "Stumtid ryddet — alle notifikationse-mails sendes nu straks.",
            ["admin.quiet.title"]           = "Stumtid-takt",
            ["admin.quiet.cadence_label"]   = "Genprøv tilbageholdte notifikationer hver (minut) gang",
            ["admin.quiet.save"]            = "Gem takt",
            ["admin.quiet.flash_saved"]     = "Stumtid-takt gemt.",

            // ── M28 (ADR 0151) — tidsbegrænsninger for formynder: den 13-nøgle-
            // GU-Detail-sektion guardian.timelimit.* (D7) + den enkelt login-
            // landing account.time_limit.login_message (refererenced i U04s
            // Login.cshtml ?error=time-limit tilfælde). U05 forfatter det FULDE
            // 14-nøgle-sæt; U06 forbruger, tilføjer ingen. DISTinkt namespace —
            // IKKE M20 settings.quiet.* / admin.quiet.* nøglerne (de tilhører
            // notifikationssporet).
            ["guardian.timelimit.title"]        = "Brugsgrænser",
            ["guardian.timelimit.description"]  = "Vælg hvornår dit barn må bruge platformen. Tidsplanen " +
                "gemmes på deres konto og anvendes i deres egen tidssone — den " +
                "træder i kraft ved deres næste login og påvirker aldrig dig.",
            ["guardian.timelimit.enabled"]      = "Gennemfør brugsgrænser for dette barn",
            ["guardian.timelimit.mode_label"]   = "Hvornår må barnet bruge platformen?",
            ["guardian.timelimit.mode_blocked"] = "Blokeret i de valgte timer & dage",
            ["guardian.timelimit.mode_allowed"] = "Kun tilladt i de valgte timer & dage",
            ["guardian.timelimit.hours_label"]  = "Timer på døgnet",
            ["guardian.timelimit.days_label"]   = "Ugedage",
            ["guardian.timelimit.save"]         = "Gem brugsgrænser",
            ["guardian.timelimit.clear"]        = "Ryd brugsgrænser",
            ["guardian.timelimit.flash_saved"]  = "Brugsgrænser gemt — barnet logges ud uden for det tilladte vindue.",
            ["guardian.timelimit.flash_cleared"] = "Brugsgrænser ryddet — barnet må nu bruge platformen til enhver tid.",
            ["guardian.timelimit.badge_set"]    = "Brugsgrænser sat",
            ["account.time_limit.login_message"] = "Du er uden for dine tilladte timer. Tjek venligst ind senere.",

            // ── nav (the shared top-nav, _Layout + _AccountNav) ─────────────
            ["nav.home"]          = "Forside",

            // ADR 0111 — the nav-variant surface: the compact top-row variant
            // folds the less-frequent sections into one "More" menu, and the
            // resident picks a variant from the account menu (nav_variant.*
            // are the picker labels, the active one marked with a ✓).
            ["nav.more"]          = "Mere",
            ["nav_variant.label"] = "Navigation",
            ["nav_variant.row"]   = "Øverste række",
            ["nav_variant.rail"]  = "Ikonrail",

            // ADR 0133 — the appearance (theme) picker labels.
            ["theme.label"]      = "Visning",
            ["theme.auto"]       = "Automatisk (som enheden)",
            ["theme.light"]      = "Lys",
            ["theme.dark"]       = "Mørk",

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
            ["common.full_page"]    = "Hele siden",
            ["common.exit_full_page"] = "Afslut hele siden",
            ["common.open_full_page"] = "Åbn hele siden",
            ["common.fullscreen"]   = "Fuldskærm",
            ["common.exit_fullscreen"] = "Afslut fuldskærm",
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
            ["admin.roles_independent_hint"] = "Uafhængige — en beboer kan frit kombinere roller. Intet markeret = almindeligt Medlem.",
            ["admin.moderator_scope"]       = "Moderatørens område",
            ["admin.moderator_scope_hint"]  = "De fællesskaber, dette konto kan moderere. Kun meningsfuldt, når Moderator-rollen er markeret — platformen rydder scope-valgene, når Moderator-rollen er fra.",
            ["nav.announcements"] = "Meddelelser",
            ["nav.community"]     = "Fællesskab",
            ["nav.groups"]        = "Grupper",
            ["nav.pages"]         = "Sider",
            ["nav.tags"]          = "Tags",
            ["nav.directory"]     = "Kontaktliste",
            ["nav.people"]        = "Personer",
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

            // ADR 0119 (M18, D9) — the recurring-events key set. 14 keys, all under
            // the existing `events.*` namespace (the `events.created` / `events.edited`
            // entries above are the anchor; no new `event.*` / `recurrence.*` /
            // `series.*` namespace). Closed set × en/de/fr/da: every key below must
            // appear in all four dictionaries (see the closure test in
            // tests/Kumunita.Core.Tests/KnownTranslationKeysClosureTests.cs, U07).
            ["events.recurrence.none"]       = "Gentages ikke",
            ["events.recurrence.daily"]      = "Dagligt",
            ["events.recurrence.weekly"]     = "Ugentligt",
            ["events.recurrence.monthly"]    = "Månedligt",
            ["events.recurrence.yearly"]     = "Årligt",
            ["events.recurrence.interval"]   = "Hver",
            ["events.recurrence.ends_after"] = "Slutter efter",
            ["events.recurrence.ends_on"]    = "Slutter den",
            ["events.recurrence.count"]      = "arrangementer",
            ["events.recurrence.until"]      = "til",
            ["events.series.repeats"]        = "Gentages",
            ["events.series.skip"]           = "Spring dette arrangement over",
            ["events.series.restore"]        = "Gendan dette arrangement",
            ["events.series.part_of"]        = "En del af en serie",

            // ── projects (M5 — ADR 0067: the to-do surface nav entry + labels) ──
            ["nav.projects"]                 = "Projekter",
            ["projects.todo.title"]          = "Opgaver",
            ["projects.todo.lede"]           = "Nabolagets fælles opgaver — tildel arbejde til en nabo, opdel det i delopgaver og læg det på et board.",
            ["projects.todo.new"]            = "Ny opgave",
            ["projects.todo.new_lead"]       = "Skriv en opgave, tildel den evt. til en nabo, og — hvis nødvendigt — opdel den i delopgaver eller læg den på et board. Som udgangspunkt kan alle se den; slå den fra i publikumsafsnittet, hvis du vil begrænse adgang.",
            ["projects.todo.title_hint"]     = "Et kort navn til opgaven — kortets label.",
            ["projects.todo.status"]         = "Status",
            ["projects.todo.status_assignee_hint"] = "Status er en fri tekstlabel (en streng, ikke en fast liste). Tildeling af en opgave giver denne beboer håndtering af den — visning + håndtering, aldrig en adgangsbegrænsning.",
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
            ["projects.todo.filter_assigned_to_me"] = "Tildelt til mig",
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
            // ADR 0106 — selvtildelings-sporet + kortdetaljs udvidelsesfelt.
            ["projects.todo.assign_to_me"]   = "Tildel mig",
            ["projects.todo.details"]        = "Detaljer",
            ["projects.todo.community"]      = "Fællesskab",
            ["projects.todo.subtasks"]       = "Delopgaver",
            ["projects.todo.boards"]         = "Boards",
            // ADR 0100 — Kommentarer + svar (C-M3·1 — kommentarer arver
            // opgavens enkelte Read-afgørelse).
            ["projects.todo.comments"]       = "Kommentarer",
            ["projects.todo.comment_empty"]  = "Ingen kommentarer endnu. Hvis du kan se denne opgave, kan du kommentere den.",
            ["projects.todo.comment_reply"]  = "Kommentar",
            ["projects.todo.comment_submit"] = "Kommentér",
            ["projects.todo.comment_deleted"] = "Denne kommentar er slettet af dens forfatter.",
            ["projects.todo.comment_delete"] = "Slet",
            ["projects.todo.comment_audience_note"] = "Kommentarer har intet eget publikum — de er synlige under denne opgaves enkelte publikumsafgørelse. Du kommenterer kun hvor opgaven selv er synlig.",
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
            ["projects.board.edit_lead"]     = "Opdater brættets navn, beskrivelse og publikum. Fællesskab og sprog er fastlagt ved oprettelsen.",
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
            ["projects.board.lane.delete"] = "Slet lane",
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
            ["guardian.community_block_note"] =
                "Vælg, hvilke fællesskaber dette barn kan få adgang til. At " +
                "blokke et fællesskab skjuler det for dem — herunder deres " +
                "indlæg — selvom det er et af de obligatoriske fællesskaber, " +
                "alle tilhører. Det er forældrens kontrol: du bestemmer, hvem " +
                "der kan tilslutte sig et fællesskab ved at invitere dem eller " +
                "gennem en admin, men du kan skjule et fællesskab, du ikke " +
                "ønsker, at dette barn skal se.",
            ["guardian.community_blocked"]   = "Blokeret & skjult",
            ["guardian.community_unblock"]   = "Løslas & vis",
            ["guardian.community_block"]     = "Blokér adgang & skjul",
            ["guardian.pending_invitations"] = "Afventende gruppeinvitationer",
            ["guardian.no_invitations"]      = "Ingen afventende invitationer.",
            ["guardian.approve"]             = "Godkend",
            ["guardian.handover"]            = "Overtag kontoen",
            ["guardian.handover_hint"]       =
                "Afløsning af værgemodet overgiver kontoen til barnet. " +
                "Medlemskaberne bevares, og barnets egne kontroller kommer " +
                "tilbage ved næste læsning.",
            ["guardian.dissolve"]            = "Afløs værgemodet",
            // Count-aware steering (ADR 0028 §G·6) — da.
            ["guardian.remove_myself"]       = "Fjern mig selv som værgemand",
            ["guardian.remove_myself_hint"]  =
                "Du er en af flere værgemænd for dette barn. Når du fjerner " +
                "dig selv som værgemand, ophører dine rettigheder over " +
                "kontoen; de øvrige værgemænd bliver ved, og kontoen bevares.",
            ["guardian.remove_myself_submit"] = "Fjern mig selv",
            ["guardian.suspended"]           = "Suspendert",
            ["guardian.unsuspend"]           = "Genopret",
            ["guardian.suspend"]             = "Suspendér",
            ["guardian.delete_child"]        = "Slet barnkontoen",
            ["guardian.delete_child_lede"]   =
                "Sletningen fjerner barnets login, profil og gruppemedlemskaber og " +
                "fællesskabsmedlemskaber, og ophæver enhver anden forældremyndighed over " +
                "kontoen. Deres tidligere handlinger bevares i revisionslogget, hvor " +
                "deres identitet er erstattet af en placeholder. Det kan ikke fortrydes.",
            ["guardian.delete_child_confirm_checkbox"] = "Jeg forstår, at barnkontoen bliver slettet permanent.",
            ["guardian.delete_child_submit"] = "Slet konto",
            ["guardian.display_name"]        = "Vistnavn",
            ["guardian.email"]               = "E-mailadresse",
            ["guardian.password"]            = "Adgangskode",
            ["guardian.child_email_hint"]    =
                "Barnet åbner bekræftelseslinket i sin e-mail for at sætte sit eget adgangskode og logge ind — du behøver (eller sætter) ikke barnets adgangskode.",
            ["guardian.consent.intro"]       =
                "Ved at oprette denne profil bekræfter du, at du er dette " +
                "barns værgemand. Som dets værgemand har du fuldstændig " +
                "kontrol over dens konto:",
            ["guardian.consent.duties_invitations"] =
                "Du skal godkende eller afvise alle gruppe- og eventinvitationer.",
            ["guardian.consent.duties_chat"] =
                "Du kan til- eller fravælge chatfunktionaliteter for denne " +
                "profil til enhver tid.",
            ["guardian.consent.duties_data"] =
                "Disse data er fuldt isoleret til fællesskabets egen instans " +
                "og vil aldrig blive solgt, brugt til profilopbygning eller " +
                "til reklame.",
            ["guardian.consent.checkbox"]    =
                "Jeg samtykker til behandlingen af mit barns data under " +
                "disse vilkår.",

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
            ["guardian.assign.success"]      = "Værgemand tildelt — de vil blive bedt om at acceptere.",

            // ── guardian acceptance lane (GA ADR 0038 §F) ──────────────────
            ["guardian.pending"]              = "Afventer",
            ["guardian.pendingRequests.title"] =
                "Værgemandsansøgninger, der afventer din accept",
            ["guardian.pendingRequests.lead"] =
                "En anden værgemand har bedt dig om at blive medværgemand for ét af deres børn. " +
                "Accepter (og acceptér betingelserne for barnes konto) eller afvis — indtil du handler, har du ingen rettigheder over kontoen.",
            ["guardian.pendingRequests.child"]    = "Barn",
            ["guardian.pendingRequests.conferrer"] = "Anmodet af",
            ["guardian.accept"]                   = "Acceptér og acceptér betingelserne",
            ["guardian.accept.consent.intro"]     =
                "Ved at acceptere bekræfter du, at du er dette barns lovlige værgemand. " +
                "Som dennes værgemand har du fuld kontrol over deres konto:",
            ["guardian.accept.consent.duties_invitations"] =
                "Du skal godkende eller afvise alle inviter til grupper og arrangementer.",
            ["guardian.accept.consent.duties_chat"] =
                "Du kan til- eller fraaktivere chat-funktioner for denne profil når som helst.",
            ["guardian.accept.consent.duties_data"] =
                "Disse data er fuldstændig isoleret på denne communities egen instance og bliver aldrig solgt, profileret eller brugt til reklame.",
            ["guardian.accept.consent.checkbox"] =
                "Jeg accepterer behandlingen af dette barns data under disse betingelser.",
            ["guardian.accept.consent.required"] =
                "Du skal acceptere betingelserne for barnes konto, før du accepterer.",
            ["guardian.decline"] = "Afvis",

            // ── The lane — event attendance (the guardian's three postures) ──
            ["guardian.eventrsvp.title"] = "Deltagelse i arrangementer",
            ["guardian.eventrsvp.description"] =
                "Vælg, hvordan dette barns deltagelse i arrangementer håndteres.",
            ["guardian.eventrsvp.mode_approves_active"] =
                "I øjeblikket: Værge godkender — Jeg godkender eller afviser hvert arrangement, barnet vil deltage i.",
            ["guardian.eventrsvp.mode_notifies_active"] =
                "I øjeblikket: Værge underrettes — Barnet deltager frit; jeg underrettes og kan fjerne enhver deltagelse bagefter.",
            ["guardian.eventrsvp.mode_childdecides_active"] =
                "I øjeblikket: Barnet bestemmer — Barnet vælger selv sin deltagelse; ingen godkendelse og ingen underretning.",
            ["guardian.eventrsvp.switch_to_approves"] = "Skift til \"Værge godkender\"",
            ["guardian.eventrsvp.switch_to_notifies"] = "Skift til \"Værge underrettes\"",
            ["guardian.eventrsvp.switch_to_childdecides"] = "Skift til \"Barnet bestemmer\"",
            ["guardian.eventrsvp.pending_title"] = "Ventende deltagelsesanmodninger",
            ["guardian.eventrsvp.pending_empty"] = "Ingen ventende deltagelsesanmodninger.",
            ["guardian.eventrsvp.desired"] = "Vil deltage",
            ["guardian.eventrsvp.approve"] = "Godkend",
            ["guardian.eventrsvp.deny"] = "Afvis",
            ["guardian.eventrsvp.rsvps_title"] = "Dette barns nuværende deltagelse",
            ["guardian.eventrsvp.rsvps_empty"] = "Ingen nuværende deltagelse at fjerne.",
            ["guardian.eventrsvp.veto"] = "Fjern",
            ["guardian.eventrsvp.approve_confirm"] =
                "Godkende dette barns deltagelse i dette arrangement?",
            ["guardian.eventrsvp.deny_confirm"] =
                "Afvis dette barns deltagelse i dette arrangement? Det vil ikke deltage.",
            ["guardian.eventrsvp.veto_confirm"] =
                "Fjern dette barns deltagelse i dette arrangement? Deltagelsen vil blive slettet.",

            // ── notification kinds (the lane) ────────────────────────────────
            ["notifications.kind.guardian.event_request"] =
                "Deltagelsesanmodning fra dit barn",
            ["notifications.preference.guardian.event_request.label"] =
                "Når dit barn vil deltage i et arrangement",
            ["notification.guardian.event_request.subject"] =
                "Dit barn vil deltage i et arrangement",
            ["notification.guardian.event_request.body"] =
                "Dit barn vil deltage i et arrangement: ",
            ["notifications.kind.guardian.event_rsvp"] =
                "Dit barn har deltaget i et arrangement",
            ["notifications.preference.guardian.event_rsvp.label"] =
                "Når dit barn deltager i et arrangement",
            ["notification.guardian.event_rsvp.subject"] =
                "Dit barn har deltaget i et arrangement",
            ["notification.guardian.event_rsvp.body"] =
                "Dit barn har deltaget i et arrangement: ",

            // ── notification kind (GA ADR 0038 §F) ──────────────────────────
            ["notifications.kind.guardian.assign"] =
                "En værgemand har bedt dig om at blive medværgemand",
            ["notifications.preference.guardian.assign.label"] =
                "Når en værgemand beder dig om at blive medværgemand",
            ["notification.guardian.assign.subject"] =
                "En værgemand har bedt dig om at blive medværgemand",
            ["notification.guardian.assign.body"] =
                "En værgemand har bedt dig om at blive medværgemand for deres barn: ",

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

            ["settings.pagesize_title"]        = "Elementer pr. side",
            ["settings.pagesize_lede"]         =
                "Vælg, hvor mange elementer hver liste viser pr. side (flok, begivenheder, projekter m.m.). " +
                "Dit valg gemmes på din konto — det træder i kraft ved næste indlæsning af en liste, og " +
                "betræffes aldrig andre beboere.",
            ["settings.pagesize_label"]        = "Elementer pr. side",
            ["settings.pagesize_default_marker"] = "— platformstandard",
            ["settings.pagesize_reset_confirm"]  = "Nulstil elementer pr. side til platformstandarden?",
            ["settings.pagesize_reset"]        = "Nulstil til platformstandard",
            ["settings.pagesize_save"]         = "Gem",
            ["settings.pagesize_flash_set"]    = "Elementer pr. side indstillet til \"{0}\" — det træder i kraft ved næste anmodning.",
            ["settings.pagesize_flash_reset"]  = "Elementer pr. side nulstillet — platformstandarden bruges.",

            // ── settings — home page (hide-home-intro preference, ADR 0149) ─
            ["settings.home_title"]        = "Forside",
            ["settings.home_lede"]         =
                "Forsiden åbner med to introduktionssektioner (hvad Kumunita er og hvad den kan) " +
                "før strømmen. Slå til for at lande direkte i strømmen. " +
                "Dit valg gemmes på din konto og berører aldrig andre beboere.",
            ["settings.home_label"]        = "Skjul introduktionssektionerne og vis strømmen med det samme",
            ["settings.home_note"]         = "Er slukket, viser forsiden sine introduktionssektioner som sædvanlig.",
            ["settings.home_save"]         = "Gem",
            ["settings.home_flash_hide"]   = "Forside opdateret — introduktionssektionerne skjules, strømmen vises først.",
            ["settings.home_flash_show"]   = "Forside opdateret — introduktionssektionerne vises igen.",

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

            // ── admin — announcement-comments gate (ADR 0101) ────────────────
            ["admin.anncomments_title"]     = "Kommentarer på meddelelser",
            ["admin.anncomments_lede"]      =
                "Om loggede indboere må kommentere meddelelser. Slukker du " +
                "den, skjules kommentarlisten og skrivefeltet på alle " +
                "meddelelser — ingen kan længere tilføje en kommentar. " +
                "Besøgende kunne aldrig kommentere, og eksisterende " +
                "kommentarer beholdes — knappen styrer kun nye kommentarer.",
            ["admin.anncomments_on"]        = "Åben — loggede indboere kan kommentere",
            ["admin.anncomments_off"]       = "Lukket — ingen kan tilføje en kommentar",
            ["admin.anncomments_save"]      = "Gem",

            // ── admin — direkte besked-skydedæksel (M9, ADR 0105) ──────────
            ["admin.messaging_title"]   = "Direkte beskeder",
            ["admin.messaging_lede"]    =
                "Om loggede indboere må åbne direkte 1:1-samtaler. Slukker du " +
                "den, skjules Beskeder-indgangen og alle samtaleanmodninger " +
                "afvises — ingen kan åbne en tråd eller sende en besked. " +
                "Skydedækningen er lukket som udgangspunkt; den styrer kun " +
                "nye beskeder, og eksisterende samtaler og beskeder røres aldrig. " +
                "Bemærk, at en GlobalAdmin uden deltagelse i samtalen kan ikke " +
                "læse den.",
            ["admin.messaging_on"]      = "Åben — loggede indboere kan sende hinanden direkte beskeder",
            ["admin.messaging_off"]     = "Lukket — ingen kan åbne samtaler eller sende beskeder",

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
            ["home.roadmap.show_more_earlier"] = "Vis de {n} tidligere milepæle",
            ["home.roadmap.show_more_upcoming"]  = "Vis de {n} kommende milepæle",
            ["home.roadmap.status.done"]    = "Færdig",
            ["home.roadmap.status.next"]    = "I gang",
            ["home.roadmap.status.planned"] = "Planlagt",

            // ── klient-JS-strenge (P0-6: #kumunita-strings-bundlen) ───────
            ["common.close"]  = "Luk",
            ["common.cancel"] = "Annuller",
            ["rc.editor.error_generic"] = "Noget gik galt.",
            ["rc.editor.link.title"]    = "Indsæt link",
            ["rc.editor.link.url"]      = "URL",
            ["rc.editor.link.confirm"]  = "Indsæt link",
            ["rc.editor.link.busy"]     = "Indsætter…",
            ["rc.editor.link.err_empty"]    = "Indtast en URL.",
            ["rc.editor.link.err_invalid"]  = "Indtast et gyldigt link (webadresse, e-mail eller en sti relativ til siden).",
            ["rc.editor.image.title"]   = "Rediger billede",
            ["rc.editor.image.err_rejected"] = "Den uploadede billedkilde blev afvist.",
            ["rc.editor.image.err_upload"]   = "Upload mislykkedes.",
            ["rc.editor.attach.title"]  = "Vedhæft fil",
            ["rc.editor.attach.file"]   = "Fil",
            ["rc.editor.attach.link_text"] = "Linktekst",
            ["rc.editor.attach.default_label"] = "Vedhæftelse",
            ["rc.editor.attach.confirm"]  = "Vedhæft",
            ["rc.editor.attach.busy"]     = "Uploader…",
            ["rc.editor.attach.err_no_file"] = "Vælg en fil at vedhæfte.",
            ["img.edit.close"]       = "Luk",
            ["img.edit.crop_area"]   = "Beskæring",
            ["img.edit.width"]       = "Bredde",
            ["img.edit.output"]      = "Resultat",
            ["img.edit.reset_crop"]  = "Nulstil beskæring",
            ["img.edit.use_original"] = "Brug originalen",
            ["img.edit.apply"]       = "Anvend",
            ["img.edit.title"]       = "Beskær dit avatar",
            ["img.edit.err_edit"]    = "Redigeringen mislykkedes.",
            ["img.edit.err_could"]   = "Billedet kunne ikke redigeres.",
            ["img.edit.err_load"]    = "Billedet kunne ikke indlæses til redigering.",
            ["img.edit.err_export"]  = "Billedeksporten mislykkedes.",
            ["tag.suggest.remove_prefix"] = "Fjern tag: ",
            ["notif.fallback"]       = "Notifikationer",

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

            // ── ADR 0138 — adgangskodeændring (da) ──
            ["account.change_password_title"] = "Skift adgangskode",
            ["account.change_password_lede"] =
                "Vælg en ny adgangskode til din konto. Efter gemme bliver du " +
                "logget ud og beder om at logge ind igen med den nye adgangskode.",
            ["account.change_password_current"] = "Aktuel adgangskode",
            ["account.change_password_new"] = "Ny adgangskode",
            ["account.change_password_confirm_new"] = "Bekræft ny adgangskode",
            ["account.change_password_submit"] = "Skift adgangskode",
            ["account.change_password_locked_title"] = "Adgangskodeændringer er låst",
            ["account.change_password_locked_body"] =
                "Dette er en demo-konto, og adgangskodeændringer er låst af " +
                "administratoren, så alle kan fortsætte med at bruge de delte " +
                "login-oplysninger. Du kan fortsat bruge alle de andre " +
                "funktionaliteter på platformen.",
            ["account.change_password_back"] = "Tilbage til din profil",

            ["nav.change_password"] = "Skift adgangskode",

            // ── ADR 0142 — sletning af konto (da) ──
            ["account.delete_title"] = "Slet konto",
            ["account.delete_lede"] =
                "Når du sletter din konto, fjernes din login, din profil og " +
                "dine gruppe- og fællesskabsmedlemskaber. Dine tidligere " +
                "handlinger i platformens audit-log bevares — din identitet " +
                "bliver erstattet af et anonymt pseudonym (platformens " +
                "konfidentialitetspolitik, OPS.md §9). Det kan ikke " +
                "undgås.",
            ["account.delete_password"] = "Adgangskode",
            ["account.delete_confirm_checkbox"] =
                "Jeg forstår, at min konto bliver slettet permanent, og at " +
                "det ikke kan undgås.",
            ["account.delete_submit"] = "Slet konto",
            ["account.delete_refused"] =
                "Den selvbetjente sletningsvej er kun tilgængelig for " +
                "GlobalAdmin. En beboer, der ikke er GlobalAdmin, kan ikke " +
                "slette sin egen konto — kontakt en administrator for at " +
                "fjerne kontoen.",

            ["nav.delete_account"] = "Slet konto",

            ["admin.delete_account_label"] = "Slet konto",
            ["admin.delete_account_confirm"] =
                "Slet denne konto permanent? Audit-protokollen bevares " +
                "(pseudonymiseret); kontoen, profilen og medlemskaberne " +
                "fjernes. Det kan ikke undgås.",

            ["admin.sample_title"] = "Eksempeldata",
            ["admin.sample_lede"] =
                "Denne instans kører det demonstrerende nabolag (eksempeldata). " +
                "Lås eksempelkontoerne, så de ikke kan ændre deres egen " +
                "adgangskode, så besøgende kan teste funktionaliteter uden at " +
                "brænde de delte login-oplysninger — demo-administratoren " +
                "beholder sin egen adgangskodevej.",
            ["admin.sample_lock_label"] = "Eksempelkontos adgangskodeændringer",
            ["admin.sample_lock_on"] = "Låst — eksempelkonti kan ikke ændre deres egen adgangskode",
            ["admin.sample_lock_off"] = "Ulåst — eksempelkonti kan ændre deres egen adgangskode",
            ["account.login.error.blocked"] =
                "Din konto er midlertidigt suspenderet. Kontakt en administrator.",
            ["account.login.error.removed"] =
                "Din konto er blevet fjernet. Kontakt en administrator.",
            ["account.login.error.role_changed"] =
                "Din rolle er blevet ændret. Log venligst ind igen.",

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
                "Disse valg er skjult, mens \"Alle i dette fællesskab\" er " +
                "tændt — indlægget er synligt for alle i fællesskabet. Slå det " +
                "fra for at begrænse adgang med valgene her.",
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
            ["groups.danger_heading"]   = "Farerzone",
            ["groups.danger_delete_hint"] =
                "Sletning af gruppen fjerner den, dens medlemmer og eventuelle afventende invitationer. Dette kan ikke fortrydes.",
            ["groups.danger_delete_button"] = "Slet denne gruppe",
            ["groups.new_title"]      = "Skriv til denne gruppe",
            ["groups.new_back"]       = "tilbage til gruppen",
            ["groups.new_submit"]     = "Skriv til gruppen",
            // ADR 0089 (GE) — the group-events lane (da).
            ["groups.back"]           = "Tilbage til",
            ["groups.events_heading"] = "Arrangementer",
            ["groups.new_event"]      = "Nyt arrangement",
            ["groups.events_empty_can"] =
                "Ingen arrangementer endnu. Plan den første — den vil være synlig for de nuværende medlemmer.",
            ["groups.events_empty"]    = "Der er endnu ingen arrangementer her.",
            ["groups.new_event_title"] = "Arrangement for denne gruppe",
            ["groups.new_event_lead"]  =
                "Dit arrangement vil kun være synlig for de nuværende medlemmer af denne gruppe.",
            ["groups.new_event_submit"] = "Tilføj arrangement til gruppen",
            ["groups.edit_event_title"] = "Redigér dette arrangement",
            // ── groups list (the Airy layout — the invitation panel + the
            //    member-count word on each group card) ───────────────────────
            ["groups.invitations"]    = "Indladelser",
            ["groups.invitations_pending"] = "ventende",
            ["groups.invited_by"]     = "Inviteret af",
            ["groups.invite_accept"]  = "Acceptér",
            ["groups.invite_decline"] = "Afvis",
            // ── ADR 0094 — the resident self-initiated join-request lane ──
            ["groups.join_requests"]              = "Medlemskabsansøgninger",
            ["groups.join_requests_pending"]      = "ventende",
            ["groups.join_requests_note"]         = "Venter på gruppeejeren.",
            ["groups.join_withdraw"]              = "Trække tilbage",
            ["groups.other_public"]               = "Andre offentlige grupper",
            ["groups.request_join"]               = "Ansøg om at deltage",
            ["groups.join_requests_pending_heading"] = "Ventende medlemskabsansøgninger",
            ["groups.join_approve"]               = "Godkend",
            ["groups.join_decline"]               = "Afvis",
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
            ["upload.max_size"] = "Maksimal filstørrelse: {0}",
            ["profile.avatar_hint"] =
                "JPEG, PNG, WebP eller GIF. Gemning erstatter " +
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

            // ── M23 (U04) — udvidet profilredigering + stikordsbog-detaljer ──
            ["profile.edit.bio"] = "Bio",
            ["profile.edit.tags"] = "Tags",
            ["profile.edit.tags.placeholder"] = "f.eks. havearbejde, bagning, cykling…",
            ["profile.detail.bio"] = "Om mig",
            ["profile.detail.tags"] = "Interesser & færdigheder",
            ["profile.detail.tags.empty"] = "Ingen tags.",
            ["profile.flash.saved"] = "Profil opdateret.",

            // ── M23 (U05) — /people-personsøgningsoverfladen ────────────
            ["profile.find.title"]   = "Find personer",
            ["profile.find.by_tag"]  = "Find efter tag",
            ["profile.find.by_bio"]  = "Find efter bio",
            ["profile.find.results"] = "{0} personer fundet",
            ["profile.find.empty"]   = "Ingen matcher — endnu.",

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
                "dette indlægs ene modtagerkredsbeslutning. " +
                "Du svarer kun der, hvor indlægget selv er synligt.",
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
            ["pages.section_community"] = "Sider f\u00f8llesskabet",
            ["pages.section_platform"]  = "Platformsider",
            ["pages.section_platform_lede"] =
                "Udgivet af platformen — vilk\u00e5r, hj\u00e6lp, privatliv og " +
                "adf\u00e6rskode.",

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
            // M9 amendment (ADR 0139) — "Send a message" (two-sided gate).
            ["directory.send_message"] = "Send en besked",
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
            // ADR 0146 — overdragelsen af barnets konto (bekræftelsessiden
            // indsamler barnets egen adgangskode, før kontoen aktiveres).
            ["account.verify_set_password_lede"] =
                "Sæt adgangskoden til denne konto — det er dig, der skal bruge den.",
            ["account.verify_set_password_submit"] = "Sæt min adgangskode & log ind",
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

            // ── admin (Analytics page — M13, ADR 0114 D4) ─────────────────
            ["admin.analytics_title"] = "Brugsanalyse",
            ["admin.analytics_lede"] =
                "Et lokalt overblik over platformens brug — anmodninger, " +
                "tilmeldt / anonym, distinkte konti og overfladeranking over " +
                "et fast vindue. Ingen konto-detaljer; de rå rækker er kun " +
                "tilgængelige i operatørens database.",
            ["admin.analytics_window"] = "Vindue",
            ["admin.analytics_total"] = "Samtlige anmodninger",
            ["admin.analytics_authenticated"] = "Tilmeldte",
            ["admin.analytics_anonymous"] = "Anonyme",
            ["admin.analytics_distinct"] = "Distinkte konti",
            ["admin.analytics_surface"] = "Overflade",
            ["admin.analytics_count"] = "Antal",
            ["admin.analytics_export"] = "Eksportér CSV",

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

            // ── announcement comments (ADR 0101) ─────────────────────────────
            ["announcements.comments"] = "Kommentarer",
            ["announcements.comment_empty"] = "Ingen kommentarer endnu. Vær den første til at sige noget.",
            ["announcements.comment_deleted"] = "Denne kommentar er blevet slettet af dens forfatter.",
            ["announcements.comment_delete"] = "Slet",
            ["announcements.comment_reply"] = "Skriv en kommentar",
            ["announcements.comment_submit"] = "Kommentar",
            ["announcements.comment_audience_note"] =
                "Kommentarer er kun synlige for loggede indboere — også på en " +
                "officiel meddelelse — og følger denne meddelelses eget " +
                "publikum (en meddelelse rettet til et fællesskab er synligt " +
                "for dets indboere).",

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

            // ── what's new (the VN lane, ADR 0110) ──────────────────────────
            ["whatsnew.eyebrow"]        = "Nyheder",
            ["whatsnew.heading"]        = "Nyheder, version for version",
            ["whatsnew.lead"] =
                "Hver udgivelse er datoeret og listet her — læs, hvad der er landet i hver minor-version af platformen, du bruger.",
            ["whatsnew.version"]        = "Version",
            ["whatsnew.show_more"]      = "Vis flere versioner",
            ["whatsnew.show_more_remaining"] = "Vis de {n} tidligere versioner",
            ["whatsnew.toast_label"]    = "Kumunita {v} er nu i brug.",
            ["whatsnew.toast_see"]      = "Se nyhederne",

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
            ["events.mine.show_more"] = "Vis de {n} flere arrangementer",

            // ── events.past (skifteren EV-PAST + tom tilstand på /events — ADR 0109) ──
            ["events.upcoming"] = "Kommende",
            ["events.past"] = "Forløbne",
            ["events.past_empty"] = "Ingen forløbne arrangementer endnu.",

            // ── events.ics (M12's iCal-overflader — detaljesiden + feed/kalender, ADR 0112) ──
            ["events.ics.download"] = "Tilføj til kalender",
            ["events.ics.feed"] = "Kalenderfeed (iCal)",

            // ── M14 (ADR 0115 D2) — de to U03-lænkemærkater arrangementer ↔ to-dos ──
            ["todo.event_link"] = "Knyttet arrangement",
            ["events.linked_todos"] = "Knyttede to-dos",

            // ── M14 (ADR 0115 D3) — de to U04 set-event-vælgermærkater
            //    (to-do-detail: formularmærkat + pladsholder/tøm-option) ──
            ["todo.set_event.label"] = "Knyt til arrangement",
            ["todo.set_event.pick"] = "Vælg et arrangement",

            // ── M14 (ADR 0115 D4) — de to U06 VTODO-overflader (to-do-detail:
            //    "Tilføj til kalender"-link + to-do-index: feed-link; ADR 0112's
            //    events.ics.-form på to-do-overfladen; simple <a>-links, intet
            //    nyt JS — det lukkede nøgle-register +
            //    KnownTranslationKeys_ParityTests håndhæver × 4) ──
            ["projects.todos.ics.download"] = "Tilføj til kalender",
            ["projects.todos.ics.feed"] = "Kalenderfeed (iCal)",

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
            // ADR 0146 — barnet-kontoteksten: det eneste resterende trin er
            // at sætte barnets egen adgangskode (værgeren oprettede kontoen,
            // men beholdte aldrig adgangskoden).
            ["email.verify_child_body"] =
                "Hej {0},\n\nDin Kumunita-konto er klar. Åbn dette engangsklink for at " +
                "bekræfte kontoen og sætte dit adgangskode (den logger dig også " +
                "ind):\n\n{1}\n\n" +
                "Hvis du ikke har oprettet denne konto, kan du ignorere denne besked.",
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
            ["notifications.mark_read"] = "Markér som læst",
            ["notifications.mark_unread"] = "Markér som ulæst",
            ["notifications.empty"] = "Ingenting endnu — ting, der hænder dig, vises her.",
            ["notifications.bell"] = "Notifikationer",
            ["notifications.view"] = "Se",
            ["notifications.accept"] = "Acceptér",
            ["notifications.decline"] = "Afvis",
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
            // ── M9 (ADR 0105, U04) — beboerfladen: navigation, liste, tråd, skrivefelt ──
            ["message.nav"] = "Beskeder",
            ["message.title"] = "Beskeder",
            ["message.new"] = "Nyt samtale",
            ["message.thread.empty"] = "Ingen beskeder endnu — sig hej.",
            ["message.compose.placeholder"] = "Skriv en besked…",
            ["message.compose.send"] = "Send",
            ["message.unread"] = "ulæst",
            ["message.compose.disabled"] = "Denne person har slået direkte beskeder fra, så du kan ikke sende dem en ny besked.",
            ["message.disabled"] = "Direkte beskeder er slået fra på denne instans.",
            ["message.other"] = "den anden person",
            ["message.sent_to"] = "Sendt til {0}.",
            ["message.load_earlier"] = "Indlæs tidligere beskeder",
            ["notifications.subscriptions.intro"] = "Vælg hvilke lokalsamfund, grupper og sider, der giver dig besked. Indstillinger afgør hvilke typer du også får på e-mail; disse indstillingsknapper afgør hvilke mål der giver dig besked.",
            ["notifications.subscription.announcement.label"] = "Nye meddelelser",
            ["notifications.subscription.community.post.label"] = "Nye indlæg i lokalsamfund",
            ["notifications.subscription.group.post.label"] = "Nye indlæg i grupper",
            ["notifications.subscription.page.child.label"] = "Nye undersider",
            // ── M9 (ADR 0105, U03) — message.new-arten + skabeloner ──
            ["notifications.kind.message.new"] = "Ny besked",
            ["notifications.preference.message.new.label"] = "Beskeder fra andre beboere",
            ["notification.message.new.subject"] = "Ny besked",
            ["notification.message.new.body"] = "En beboer har sendt dig en besked: ",

            // ── GU community-approval lane (ADR 0141) — den vagts type ──
            ["notifications.kind.guardian.group_invite"] =
                "Gruppindbydelse til dit barn",
            ["notifications.preference.guardian.group_invite.label"] =
                "Når en gruppe inviterer dit barn",
            ["notification.guardian.group_invite.subject"] =
                "En gruppe har inviteret dit barn",
            ["notification.guardian.group_invite.body"] =
                "En gruppe har inviteret dit barn: ",
            ["notifications.kind.guardian.community_invite"] =
                "Communitymedlemskab til dit barn",
            ["notifications.preference.guardian.community_invite.label"] =
                "Når en community tilføjer dit barn",
            ["notification.guardian.community_invite.subject"] =
                "En community har tilføjet dit barn",
            ["notification.guardian.community_invite.body"] =
                "En community har tilføjet dit barn: ",

            // ── GU community-approval lane (ADR 0141) — børns-siden ──
            ["guardian.pending_community_requests"] = "Community'er i ventetid",
            ["guardian.no_community_requests"] = "Ingen community'er i ventetid.",
            ["guardian.reject"] = "Afvise",

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

            // ── M7 (ADR 0090) — the shared pager's two link labels (D5) ──
            ["pagination.prev"] = "Nyere",
            ["pagination.next"] = "Ældre",

            // M8 (ADR 0091 D1/D4) — den ene /search-side + nav-indgang.
            ["search.nav"] = "Søg",
            ["search.title"] = "Søg",
            ["search.placeholder"] = "Søg i indlæg, arrangementer, sider, meddelelser, projekter, brætter, to-dos, inventar, dokumenter og personer…",
            ["search.no-results"] = "Ingen resultater for",
            ["search.section.posts"] = "Indlæg",
            ["search.section.events"] = "Arrangementer",
            ["search.section.pages"] = "Sider",
            ["search.section.announcements"] = "Meddelelser",
            ["search.section.projects"] = "Projekter",
            ["search.section.boards"] = "Brætter",
            ["search.section.todos"] = "To-dos",
            ["search.section.inventory"] = "Inventar",
            ["search.section.documents"] = "Dokumenter",
            ["search.section.people"] = "Personer",
            ["search.scope.community"] = "Fællesskab",
            ["search.scope.groups"] = "Grupper",
            ["search.empty.hint"] = "Find indlæg, arrangementer, sider, meddelelser, projekter, brætter, to-dos, inventar, dokumenter og personer efter tekst eller tag — kun indhold, du allerede kan læse, vises.",

            // M10 (ADR 0107 D10) — det ene stille installations-affordance
            // (U03, pwa-install.ts). Ingen banner, ingen modal — én knap.
            ["pwa.install"] = "Installér app",

            // M11 (ADR 0108 D10) — portabilitetsoperatøroverfladen
            // (indexen /admin/portability: eksportknap, importformular + dens
            // destruktive vagt, de to status-renderinger).
            ["portability.index.title"]  = "Portabilitet",
            ["portability.export"]       = "Eksportér",
            ["portability.import"]       = "Importér",
            ["portability.confirm.import"] = "Importér denne arkiv? Den erstanser indholdet i instansen (gendannelsesstien — operatørens backup før import er tilbageskrivningen).",
            ["portability.status.ok"]    = "Færdig.",

            // M15 U05 (ADR 0116, D8) — de editorbaserede bulk-nøgler
            // (Gem-alle-knappen + de to tilstandsvippere).
            ["translations.bulk.save_all"]   = "Gem alle",
            ["translations.bulk.mode_batch"] = "Batchredigering",
            ["translations.bulk.mode_single"] = "Redigér én ad gangen",
            ["portability.status.failure"] = "Afvist — arkivet blev afvist, før noget blev skrevet:",

            // M27 (ADR 0148 D9) — beboerens portabilitetsoverflade (indexen
            // /account/portability: eksportknap + importformular + statusområde;
            // U07). Bevidst et ANDET myportability.*-navneområde end M11s admin
            // portability.*-nøgler, så overfladen for beboere aldrig kolliderer
            // med operatørens overflade.
            ["myportability.index.title"]  = "Mine data",
            ["myportability.export"]       = "Eksportér",
            ["myportability.import"]       = "Importér",
            ["myportability.import.resolve"] = "Anvend mine valg",
            ["myportability.resolve.add_elsewhere"] = "Tilføj andre steder",
            ["myportability.resolve.discard"] = "Kassér",
            ["myportability.status"]       = "Status",

            // M15 U04 (ADR 0116, D8) — de filbaserede bulk-nøgler.
            ["translations.bulk.export"]     = "Download translationer (CSV)",
            ["translations.bulk.import"]     = "Upload translationer (CSV)",
            ["translations.bulk.import_hint"] = "Tomme felter springes over (de sletter aldrig en oversættelse); en fil med en ukendt nøgle eller et ukendt sprog afvises uændret.",

            // M16 (ADR 0117, D1) — lageroverfladen (udlån / returnering +
            // historik-sektionen + nav-indekset). Det lukkedes nøglesæt er
            // design-dokumentets §kw-l.
            ["inv.nav"]                     = "Lager",
            ["inv.list.title"]              = "Lager",
            ["inv.list.empty"]              = "Ingen emner endnu.",
            ["inv.list.ownerKind.shared"]   = "Fælles",
            ["inv.list.ownerKind.community"] = "Fællesskab",
            ["inv.list.ownerKind.private"]  = "Privat",
            ["inv.list.filter"]             = "Filtrer efter type",
            ["inv.create.title"]            = "Nyt emne",
            ["inv.create.name"]             = "Navn",
            ["inv.create.ownerKind"]        = "Type",
            ["inv.create.description"]      = "Beskrivelse",
            ["inv.create.component"]        = "Sektion",
            ["inv.create.submit"]           = "Opret emne",
            ["inv.detail.title"]            = "Emne",
            ["inv.detail.currentHolder"]    = "Hos lige nu",
            ["inv.detail.history"]          = "Brughistorik",
            ["inv.detail.edit"]             = "Redigér",
            ["inv.detail.delete"]           = "Slet",
            ["inv.detail.checkOut"]         = "Udlån",
            ["inv.detail.checkIn"]          = "Returnér",
            ["inv.edit.title"]              = "Redigér emne",
            ["inv.edit.submit"]             = "Gem ændringer",

            // M17 (ADR 0118) — Bogmærker: den personlige mærke-overflade
            // (D6: stående kerne­overflade, ingen admin-toggle). Det lukkede
            // nøglesæt er design-dokumentets §kw-l (14 nøgler: nav-indeks,
            // liste-/degraderings-/type-etiketter, fjern-handling +
            // tilstandsknap + obs-2 flash-toast-nøgler).
            ["bm.nav"]                      = "Bogmærker",
            ["bm.list.title"]               = "Dine bogmærker",
            ["bm.list.empty"]               = "Ingen bogmærker endnu.",
            ["bm.list.degraded"]            = "Ikke længere tilgængelig",
            ["bm.list.kind.post"]           = "Indlæg",
            ["bm.list.kind.event"]          = "Begivenheder",
            ["bm.list.kind.todo"]           = "Opgaver",
            ["bm.list.kind.announcement"]   = "Bekendtgørelser",
            ["bm.list.kind.page"]           = "Sider",
            ["bm.list.unbookmark"]          = "Fjern",
            ["bm.button.bookmark"]          = "Bogmærk",
            ["bm.button.bookmarked"]        = "Bogmærket",
            // Obs-2 (ADR 0118 ændring) — flash-toast-nøgler.
            ["bm.toggle.bookmarked"]        = "Bogmærket.",
            ["bm.toggle.removed"]           = "Bogmærke fjernet.",

            // ── M21 (ADR 0122) — dokumenthåndtering: /documents-feed +
            // detalje + upload-formular (D5 standing, D6 download, D7
            // afvisning → 404). U04 skaber det lukkede 12-nøgler-sæt; U03's
            // views forbruger det.
            ["documents.title"]             = "Dokumenter",
            ["documents.empty"]             = "Ingen dokumenter er endnu synlige for dig.",
            ["documents.upload"]            = "Upload et dokument",
            ["documents.upload_title"]      = "Upload et dokument",
            ["documents.upload_summary"]    = "En-linjes beskrivelse (valgfri)",
            ["documents.upload_file"]       = "Fil",
            ["documents.upload_audience"]   = "Hvem kan se dette dokument",
            ["documents.upload.submit"]     = "Upload dokumentet",
            ["documents.download"]          = "Download",
            ["documents.detail.type_size"]  = "Filtype / størrelse",
            ["documents.detail.updated"]    = "Senest opdateret",
            ["documents.flash_uploaded"]    = "Dokument uploadet.",
            // ADR 0125 (U03) — ejeren-kan-ændre-lanen.
            ["documents.edit"]              = "Rediger",
            ["documents.edit_title"]        = "Rediger et dokument",
            ["documents.edit_summary"]      = "En-linjes beskrivelse (valgfri)",
            ["documents.edit_file"]         = "Erstat filen (valgfri)",
            ["documents.edit_file_hint"]    = "Lad være tom for at beholde den nuværende fil.",
            ["documents.edit_audience"]     = "Hvem kan se dette dokument",
            ["documents.edit.submit"]       = "Gem ændringer",
            ["documents.flash_edited"]      = "Dokument opdateret.",

            // "Dokumenter organiseret"-lanen (tags + mapper) —
            // documents.folder_* / documents.tags.* nøgler + flash-nøglerne
            // for DocumentFolderController-ruterne (create / rename / move /
            // delete / document-move).
            ["documents.folder"]              = "Mappe",
            ["documents.folder_unfiled"]      = "Ikke fileret",
            ["documents.folder_hint"]         = "Filér dette dokument i en mappe for at holde arkivet organiseret.",
            ["documents.folder_new"]          = "Ny mappe",
            ["documents.folder_create"]       = "Opret",
            ["documents.folder_name_placeholder"] = "Mappens navn",
            ["documents.tags"]                = "Tags",
            ["documents.tags_hint"]           = "Skriv for at søge i eksisterende tags, eller start et nyt.",
            ["documents.flash_moved"]         = "Dokument flyttet.",
            ["documents.folder_flash_created"] = "Mappe oprettet.",
            ["documents.folder_flash_renamed"] = "Mappe omdøbt.",
            ["documents.folder_flash_moved"]   = "Mappe flyttet.",
            ["documents.folder_flash_deleted"] = "Mappe slettet.",

            // ── M22 (ADR 0132) — onboarding: /onboarding-guideturen (D4) +
            // den lukkelige home-/nav-banner (D5) + finish/skip-flaschen
            // (D2/D4). U03 opretter det FULDE lukkede sæt; U02/U03 forbruger
            // det. Paritets-pin'eren kræver hver nøgle til stede, ikke tom,
            // på alle fire sprog (C-M22·6, GATE-6). ──
            ["onboarding.title"]            = "Opsæt din konto",
            ["onboarding.intro"]            = "En kort guidet tur gennem de få ting, der får Kumunita til at fungere for dig. Alt henviser til den indstilling, der allerede ejer det — du kan færdiggøre det på et minut eller komme tilbage, når du vil.",
            ["onboarding.step_displayname"] = "Dit visningsnavn",
            ["onboarding.step_avatar"]      = "Din avatar",
            ["onboarding.step_language"]    = "Dit grænsefladesprog",
            ["onboarding.step_timezone"]    = "Din tidszone",
            ["onboarding.step_dateformat"]  = "Dit dato- og tidsformat",
            ["onboarding.step_email"]       = "Dit e-mail- og beskedssprog",
            ["onboarding.step_contact"]     = "Dine kontaktoplysninger og hvem der kan se dem",
            // M9 amendment (ADR 0139) — the messaging opt-in step.
            ["onboarding.step_messaging"]   = "Om du kan bruge direkte beskeder",
            ["onboarding.visit"]            = "Gå til denne indstilling",
            ["onboarding.finish"]           = "Alt er klar — afslut opsætningen",
            ["onboarding.skip"]             = "Spring over for nu",
            ["onboarding.flash_done"]       = "Opsætningen er færdig — velkommen til dit nabolag.",
            ["onboarding.banner.text"]      = "Færdiggøre opsætningen af din konto?",
            ["onboarding.banner.action"]    = "Start opsætning",

            // ── M9 amendment — den pro-borger-beskedkontrol + værgens
            // loft (startværdier på da, til revidering af en oversætter;
            // ADR 0015 leverandørens bundløsning løser dem). ──
            ["settings.messaging.title"]           = "Beskeder",
            ["settings.messaging.description"]     = "Vælg, om andre beboere kan sende dig direkte 1:1-beskeder. Dit valg gemmes på din konto og gælder straks.",
            ["settings.messaging.instance_off"]    = "Direkte beskeder er i øjeblikket slået fra på denne instans af en administrator. Du kan tilmelde dig nu, og beskeder vil være tilgængelige, så snart de slås til.",
            ["settings.messaging.restricted"]      = "Beskeder er blevet begrænset på din konto af en værgmand. Kontakt dem for at ændre det.",
            ["settings.messaging.optin"]           = "Lad andre sende mig direkte 1:1-beskeder",
            ["settings.messaging.save"]            = "Gem beskedindstilling",
            ["guardian.messaging.title"]           = "Beskeder",
            ["guardian.messaging.description"]     = "Vælg, om dette barn kan bruge direkte 1:1-beskeder. Ved begrænsning kan barnet hverken sende eller modtage beskeder — dette valg har forrang over dets eget opt-in; ved tilladelse vælger barnet selv på sin egen beskedindstillingsside.",
            ["guardian.messaging.current_restricted"] = "Beskeder er i øjeblikket begrænset for dette barn.",
            ["guardian.messaging.current_allowed"]    = "Beskeder er i øjeblikket tilladt for dette barn.",
            ["guardian.messaging.child_optin_on"]     = "Barnet har tilmeldt sig beskeder på sin egen konto.",
            ["guardian.messaging.child_optin_off"]    = "Barnet har ikke endnu tilmeldt sig beskeder på sin egen konto — selvom du tillader det, skal det tilmelde sig på sin egen indstillingsside.",
            ["guardian.messaging.allow"]              = "Tillad beskeder",
            ["guardian.messaging.restrict"]           = "Begræns beskeder",

            // ── P1 audit (2026-10-04) ── samme ~73-nøglesæt som
            // <see cref="EnValues"/> (P1-oversættelsesaudittens opstilling).
            ["common.remove_translation_confirm"] =
                "Fjern denne oversættelse?",
            ["events.skip_occurrence_confirm"] =
                "Spring over denne forekomst? Du kan gendanne den senere.",
            ["community.confirm_remove_member"] =
                "Fjern {0} fra {1}?",
            ["a11y.notifications"]             = "Notifikationer",
            ["a11y.find_tag"]                  = "Find med tag",
            ["a11y.find_bio"]                  = "Find med bio",
            ["a11y.avatar"]                    = "Avatarbillede",
            ["a11y.board_actions"]             = "Bræt-handlinger",
            ["a11y.calendar_view"]             = "Kalendervisning",
            ["a11y.event_time_range"]          = "Arrangementets tidsrum",
            ["a11y.check_out_note"]            = "Udlånsnote",
            ["a11y.community_pages"]           = "Fællesskabssider",
            ["a11y.platform_pages"]            = "Platformsider",
            ["a11y.community_page_tree"]       = "Fællesskabssidetræ",
            ["a11y.platform_page_tree"]        = "Platformsidetræ",
            ["a11y.breadcrumb"]                = "Brødkrumsnavigation",
            ["a11y.about_features"]            = "Hvad Kumunita er",
            ["a11y.about_audience"]            = "For hvem",
            ["a11y.about_philosophy"]          = "Filosofien",
            ["a11y.about_contact"]             = "Kontakt os",
            ["a11y.about_project"]             = "Projektet",
            ["a11y.set_limit"]                 = "Sæt grænse på {0}",
            ["account.block"]               = "Suspendér",
            ["account.unblock"]             = "Afsuspendér",
            ["account.confirm_block"]       = "Suspendér dette konto? Det mister alle rettigheder, indtil det afsuspenderes.",
            ["account.confirm_unblock"]     = "Afsuspendér dette konto? Dets rettigheder gendannes.",
            ["account.err.required"]        = "Feltet {0} er påkrævet.",
            ["account.err.email"]           = "Feltet {0} er ikke en gyldig e-mailadresse.",
            ["account.err.password_min"]    = "{0} skal være mindst {1} tegn.",
            ["account.err.password_mismatch"] = "Felterne {0} og {1} matcher ikke.",
            ["footer.feed"]                 = "Føden",
            ["languages.title"]             = "Sprog",
            ["languages.lede_admin"]        =
                "Administrér sprogene, denne instans understøtter. Ændringer træder i kraft ved " +
                "næste anmodning — uden genkompilering, uden genstart.",
            ["languages.lede_translator"]   =
                "Tjek platformens oversættelsesdækning og opdater UI-strings. Ændringer træder i " +
                "kraft ved næste anmodning.",
            ["languages.add_heading"]       = "Tilføj sprog",
            ["languages.code_label"]        = "Sprogekode",
            ["languages.native_name_label"] = "Egennavn",
            ["languages.code_hint"]         = "Kort kode, f.eks. pl for polsk, no-NO for norsk.",
            ["languages.supported_heading"] = "Understøttede sprog",
            ["languages.th_code"]           = "Kode",
            ["languages.th_native_name"]    = "Egennavn",
            ["languages.th_enabled"]        = "Aktiveret",
            ["languages.th_ui_strings"]     = "UI-strings",
            ["languages.th_actions"]        = "Handlinger",
            ["languages.enabled"]           = "aktiveret",
            ["languages.disabled"]          = "deaktiveret",
            ["languages.present"]           = "{n} til stede",
            ["languages.missing"]           = "{n} mangler",
            ["languages.action_disable"]    = "Deaktiver",
            ["languages.action_enable"]     = "Aktivér",
            ["languages.action_set_default"] = "Sæt som standard",
            ["languages.action_ui_strings"] = "UI-strings",
            ["languages.action_remove"]     = "Fjern",
            ["languages.reorder_btn"]       = "Ændr rækkefølge (bekræft nuværende rækkefølge)",
            ["languages.reorder_title"]     = "Send den nuværende rækkefølge (som vist nedenfor) som den nye sortering",
            ["languages.reorder_hint"]      =
                "Drag-and-drop-sortering er ikke tilgængelig; sorteringsformularet accepterer koderne " +
                "i den rækkefølge, de optræder her. For at ændre rækkefølgen skal admin indsende " +
                "listen i den ønskede rækkefølge.",
            ["languages.confirm_remove"]    =
                "Fjern {0}? Dens oversættelsesrækker beholdes og gendannes, hvis sproget tilføjes " +
                "igen.",
            ["translations.editor.back"]         = "← Tilbage til sprog",
            ["translations.editor.title"]        = "UI-strings for {0}",
            ["translations.editor.lede"]         =
                "Den fulde liste over de strenge, platformmen viser til sine beboere. Hver linje " +
                "viser nøglen, dens engelske referencetext og den aktuelle værdi i {0}. Gemme en linje " +
                "tilføjer eller opdaterer dens oversættelse — synlig ved næste anmodning.",
            ["translations.editor.mode_label"]   = "Redigerings tilstand",
            ["translations.editor.th_key"]       = "Nøgle",
            ["translations.editor.th_en_reference"] = "Engelsk reference",
            ["translations.editor.th_value"]     = "Værdi i {0}",
            ["translations.editor.value_aria"]   = "Værdi for {0} i {1}",
            ["events.rsvp_status_going"]     = "Går",
            ["events.rsvp_status_maybe"]     = "Måske",
            ["events.rsvp_status_no"]        = "Nej",
            ["posts.delete_confirm"]         = "Slet dette indlæg? Dine svar forbliver synlige.",
            ["posts.reply_delete_confirm"]   =
                "Slet dette svar? Det erstattes af en note. Optagelsen beholdes.",
            ["announcements.delete_confirm"] = "Slet denne meddelelse? Det kan ikke fortrydes.",
            ["groups.confirm_delete"]        =
                "Slet denne gruppe? Dette fjerner gruppen, dens medlemmer og dens ventende " +
                "invitationer, og det kan ikke fortrydes.",
            ["community.confirm_leave"]      = "Forlad {0}?",
            ["guardian.suspend_confirm"]     = "Suspendér denne barnkonto? Den bliver blokeret, indtil du afsuspendérer den.",
            ["guardian.messaging.allow_confirm"] =
                "Tillad beskeder for dette barn? Det vil kunne sende og modtage direkte beskeder " +
                "(dets eget opt-in skal også være til).",
            ["guardian.messaging.restrict_confirm"] =
                "Begræns beskeder for dette barn? Det vil ikke længere kunne sende eller modtage " +
                "direkte beskeder, uanset dets eget opt-in.",
            ["guardian.handover_confirm"]    = "Overtag dette konto til barnet? Dette ophæver din forældremyndighed over det.",
            ["guardian.remove_myself_confirm"] =
                "Fjerner du dig selv som værgemand for dette barn? Du kan " +
                "derefter ikke administrere kontoen. Kontoen bevares, og de " +
                "øvrige værgemænd bliver ved.",
            ["guardian.delete_child_confirm"] =
                "Slet denne barnkonto permanent? Dette fjerner login, profil og " +
                "medlemskaber, og det kan ikke fortrydes.",
            ["locale.reset_confirm"]         = "Nulstil dit sprogvalg til instansstandarden?",
            ["locale.email_reset_confirm"]   = "Nulstil dit e-mail- og beskedssprog til instansstandarden?",
            ["settings.quiet.clear_confirm"] =
                "Ryd dine stille timer? Alle notifikations-e-mails sendes straks igen.",
            ["settings.timezone_reset_confirm"] = "Nulstil din tidszone til platform-standard?",
            ["settings.dateformat_reset_confirm"] = "Nulstil dit dato- og tidsformat til platform-standard?",
            ["pages.form.body_hint"]         =
                "Valgfrit — en mappeknude kan være uden tekst. Skrevet i Markdown via den visuelle " +
                "editor (den samme, indlæg og meddelelser bruger).",
            ["pages.reset_confirm"]          =
                "Nulstil denne side til seedteksten? Alle ændringer, du har foretaget i teksten " +
                "(engelsk) og i dens tyske / franske / danske oversættelser, bliver overskrevet af " +
                "seed-baseline'en. Resten af siden (publikum, forælder, etc.) er uberørt.",
            ["pl.board.delete_confirm"]      =
                "Slet dette board? Dens baner og kortplaceringer fjernes — to-dos'ene selv beholdes.",
            ["pl.lane.delete_confirm"]       =
                "Slet denne bane? Dens kort kommer af dette board — to-dos'ene selv beholdes.",
            ["pl.todo.assignee_remove_confirm"] = "Fjern den tildelte fra denne to-do?",
            ["pl.todo.move_confirm"]         = "Flyt denne to-do til et andet board? Den er ikke længere på dette board.",
            ["pl.todo.delete_confirm"]       = "Slet denne to-do og dens underafgørelser? Det kan ikke fortrydes.",
            ["inv.item.delete_confirm"]      = "Slet dette element? Det kan ikke fortrydes.",
            ["a11y.close"]                   = "Luk",
            ["a11y.toggle_nav"]              = "Skift navigation",
            ["a11y.primary_nav"]             = "Primær",
            ["a11y.pagination"]              = "Paginering",
            ["a11y.pinned_announcement"]     = "Fastgjort meddelelse",
            ["a11y.banner_read_more"]        = "Læs denne meddelelse i fuld længde",
            ["a11y.banner_all"]              = "Se alle meddelelser",
            ["a11y.banner_dismiss"]          = "Luk denne fastgjorte meddelelse",
            ["a11y.onboarding_region"]       = "Kontoopsætning",
            ["a11y.onboarding_action"]       = "Start opsætning",
            ["a11y.onboarding_dismiss"]      = "Luk denne banner",
            ["a11y.whatsnew_region"]         = "Nyt",
            ["a11y.actions_for"]             = "Handlinger for {0}",
            ["a11y.search"]                  = "Søg",
            ["a11y.scope"]                   = "Område",
            ["a11y.back_to_messages"]        = "Tilbage til beskeder",
            ["a11y.resident"]                = "Beboer",
            ["account.storage_title"]        = "Min opbevaring",
            ["account.storage_lede"]         =
                "Hvor meget af dit indhold platformen tæller, dit kontingent pr. bruger og hvor " +
                "meget af det, du stadig har tilbage.",
            ["account.storage_used"]         = "dit brugte indhold",
            ["account.storage_quota"]        = "dit kontingent pr. bruger",
            ["account.storage_remaining"]    = "resterende",
            ["account.storage_unlimited_note"] =
                "Dit kontingent pr. bruger er ubegrænset — der er ingen indholdsgrænse for dine " +
                "uploads.",
            ["account.storage_remaining_note"] =
                "Din resterende andel er, hvad der er tilbage af dit kontingent pr. bruger ({0}). " +
                "Bed en administrator om at hæve kontingentet, hvis du har brug for mere.",
            ["translations.bulk.export_title"] = "Download UI-strings i dette sprog som en CSV-fil",
            ["common.delete"]          = "Slet",
            ["pages.delete_confirm"]   = "Slet denne side?",
            ["admin.help.reset_one"]   =
                "Nulstil \"{0}\" til seedteksten? Dette overskriver al manuelt redigeret tekst.",
            ["admin.help.reset_all"]   =
                "Nulstil ALLE {0} seeded hjælpesider til seedteksten? Dette overskriver al manuelt " +
                "redigeret tekst på hver side.",

            // ── M26 U16 — det lukkede sort.*-sæt fra den delte _Sort-partial
            // (labels kun; de otte sortnøgler fra U10–U15-overfladernes
            // tilladte lister) ──
            ["sort.created"]          = "Oprettet",
            ["sort.modified"]         = "Ændret",
            ["sort.title"]            = "Titel",
            ["sort.size"]             = "Størrelse",
            ["sort.name"]             = "Navn",
            ["sort.start"]            = "Startdato",
            ["sort.due"]              = "Frist",
            ["sort.status"]           = "Status",
        };
    /// the completeness view's "known" universe). Always equal to
    /// <see cref="EnValues"/>.Keys, in declaration order.
    /// </summary>
    public static IReadOnlyCollection<string> AllKeys => EnValues.Keys.ToList();
}
