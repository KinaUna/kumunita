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
/// <c>placeholder</c>/<c>aria-label</c>/<c>title</c> attributes,
/// <c>confirm()</c> dialogs, and strings embedding inline <c>&lt;code&gt;</c>
/// API identifiers or inlined data values are out of the TagHelper's reach and
/// stay hardcoded — registering a key that could drift from the rendered
/// output would make this registry a lie.</li>
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
            ["nav.announcements"] = "Announcements",
            ["nav.community"]     = "Community",
            ["nav.groups"]        = "Groups",
            ["nav.pages"]         = "Pages",
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

            // ── home (the hero + section lead; _Layout-independent) ─────────
            ["home.eyebrow"] = "Where this project stands",
            ["home.lead"] =
                "A private home for one neighbourhood — built in the open, one milestone at a time. " +
                "This is the same list you'll find in the README, and the code behind every item is on " +
                "the public repository.",
            ["home.support"] = "Questions or feedback? Write to",

            // ── account (Login / Signup — titles + primary actions) ─────────
            ["account.login_title"]   = "Sign in",
            ["account.login_submit"]  = "Sign in",
            ["account.login_no_account"] = "No account yet?",
            ["account.signup_title"]  = "Sign up",
            ["account.signup_submit"] = "Sign up",
            ["account.signup_has_account"] = "Already have an account?",

            // ── posts (Index / New / Edit — headings, actions, empty-states) ─
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
            ["groups.posts_heading"]  = "Posts",
            ["groups.new_post"]       = "New post",
            ["groups.posts_empty_can"] =
                "No posts yet. Write the first one — it will be visible to the current members.",
            ["groups.posts_empty"]    = "No posts here yet.",
            ["groups.new_title"]      = "Post to this group",
            ["groups.new_back"]       = "back to the group",
            ["groups.new_submit"]     = "Post to group",

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

            // ── posts (Detail page) ──────────────────────────────────────────
            ["posts.back_to"] = "back to",
            ["posts.detail_edit"] = "Edit",
            ["posts.edited"] = "edited",
            ["posts.detail_why"] =
                "You can see this post because you matched its audience " +
                "(a grant of yours, or you are the author — the \"owner branch\" " +
                "of the C1 empty-audience deny rule).",
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
                "group's detail page (M2, plan U10).",
            ["groups.create_desc_hint"] = "Optional — a short note other residents will see.",
            ["groups.create_private_hint"] =
                "A private group (e.g. a family) is hidden from everyone else's " +
                "grant/access lists — only people you add as members can use it. " +
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
            ["community.translation_add"] = "Add",
            ["community.translation_name_label"] = "Name",
            ["community.translation_desc_label"] = "Description",
            ["community.translation_optional"] = "optional",
            ["community.translation_min_one"] = "At least one of name or description is required.",
            ["community.translation_save"] = "Save translation",

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
            ["moderation.assign_header"] = "Assign to a standing moderator",
            ["moderation.assign_label"] =
                "Standing moderator on this report's component",
            ["moderation.assign_pick"] = "Choose a standing moderator …",
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
                "Every access decision on audience-restricted content — Allow and Deny — plus admin " +
                "actions and bulk list aggregate rows. Always-on; purged on a per-instance tier (§6.4).",
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
            ["admin.breakglass_consumed"] = "consumed — elevation active until expiry",
            ["admin.breakglass_presented"] = "presented but not yet consumed",
            ["admin.breakglass_token_label"] = "One-time token (from the operator)",
            ["admin.breakglass_token_hint"] =
                "Consuming this token is a one-time action. It activates the elevation " +
                "until its expiry.",
            ["admin.breakglass_consume"] = "Consume token",

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
            ["about.stats.neighbors"]  = "neighbors on board",
            ["about.stats.groups"]     = "groups & communities",
            ["about.stats.posts"]      = "posts & threads this month",
            ["about.stats.pinned"]     = "pinned notes out now",
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
            ["nav.announcements"] = "Ankündigungen",
            ["nav.community"]     = "Gemeinschaft",
            ["nav.groups"]        = "Gruppen",
            ["nav.pages"]         = "Seiten",
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

            // ── home (the hero + section lead) ──────────────────────────────
            ["home.eyebrow"] = "Wo das Projekt steht",
            ["home.lead"] =
                "Ein privater Ort für eine Nachbarschaft — in der offenen Entwicklung, Meilenstein für Meilenstein. " +
                "Dies ist dieselbe Liste wie in der README, und der Code hinter jedem Eintrag liegt im öffentlichen Repository.",
            ["home.support"] = "Fragen oder Feedback? Schreibe an",

            // ── account (Login / Signup — titles + primary actions) ─────────
            ["account.login_title"]   = "Anmelden",
            ["account.login_submit"]  = "Anmelden",
            ["account.login_no_account"] = "Noch kein Konto?",
            ["account.signup_title"]  = "Registrieren",
            ["account.signup_submit"] = "Registrieren",
            ["account.signup_has_account"] = "Du hast schon ein Konto?",

            // ── posts (Index / New / Edit) ──────────────────────────────────
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
            ["groups.posts_heading"]  = "Beiträge",
            ["groups.new_post"]       = "Neuer Beitrag",
            ["groups.posts_empty_can"] =
                "Noch keine Beiträge. Schreibe den ersten — er ist für die aktuellen Mitglieder sichtbar.",
            ["groups.posts_empty"]    = "Noch keine Beiträge hier.",
            ["groups.new_title"]      = "Beitrag in dieser Gruppe",
            ["groups.new_back"]       = "zurück zur Gruppe",
            ["groups.new_submit"]     = "In die Gruppe posten",

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

            // ── posts (Detail page) ──────────────────────────────────────────
            ["posts.back_to"] = "zurück zu",
            ["posts.detail_edit"] = "Bearbeiten",
            ["posts.edited"] = "bearbeitet",
            ["posts.detail_why"] =
                "Du kannst diesen Beitrag sehen, weil du auf sein Publikum " +
                "zutrifft (ein Recht von dir, oder du bist Autor:in — der „owner branch“ " +
                "der C1-Regel „leeres Publikum = Verweigerung“).",
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
                "Detailseite der Gruppe hinzufügen und entfernen (M2, Plan U10).",
            ["groups.create_desc_hint"] = "Optional — eine kurze Notiz, die andere Anwohner:innen sehen.",
            ["groups.create_private_hint"] =
                "Eine private Gruppe (z. B. eine Familie) ist aus allen anderen " +
                "Genehmigungs-/Zugriffslisten ausgeblendet — nur Personen, die du " +
                "als Mitglieder hinzufügst, können sie nutzen. " +
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
            ["community.translation_add"] = "Hinzufügen",
            ["community.translation_name_label"] = "Name",
            ["community.translation_desc_label"] = "Beschreibung",
            ["community.translation_optional"] = "optional",
            ["community.translation_min_one"] = "Mindestens Name oder Beschreibung ist erforderlich.",
            ["community.translation_save"] = "Übersetzung speichern",

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
            ["moderation.assign_header"] = "An einen festen Moderator zuweisen",
            ["moderation.assign_label"] =
                "Fester Moderator für die Komponente dieser Meldung",
            ["moderation.assign_pick"] = "Wähle einen festen Moderator …",
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
                "Jede Zugriffsentscheidung auf inhaltsbeschränkte Inhalte — Allow und Deny — plus " +
                "Adminaktionen und aggregierte Gesamtzeilen. Immer aktiv; je nach " +
                "Instanz-Tier gelöscht (§6.4).",
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
            ["admin.breakglass_consumed"] = "verbraucht — Erhöhung bis zum Ablauf aktiv",
            ["admin.breakglass_presented"] = "vorgelegt, aber noch nicht verbraucht",
            ["admin.breakglass_token_label"] = "Einmal-Token (vom Operator)",
            ["admin.breakglass_token_hint"] =
                "Das Verwenden dieses Tokens ist eine Einmalaktion. Es aktiviert die " +
                "Erhöhung bis zum Ablauf.",
            ["admin.breakglass_consume"] = "Token verwenden",

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
            ["about.stats.neighbors"]  = "Anwohner:innen an Bord",
            ["about.stats.groups"]     = "Gruppen & Gemeinschaften",
            ["about.stats.posts"]      = "Beiträge & Threads diesen Monat",
            ["about.stats.pinned"]     = "gepinnte Notizen aktuell",
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
            ["nav.announcements"] = "Annonces",
            ["nav.community"]     = "Communauté",
            ["nav.groups"]        = "Groupes",
            ["nav.pages"]         = "Pages",
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

            // ── guardian (the /me/children child-accounts surface) ─────────
            ["guardian.title"]        = "Tes enfants",
            ["guardian.lead"]         = "Les comptes que tu as créés pour un enfant, et les contrôles que tu exerces sur chacun.",
            ["guardian.empty"]        = "Pas encore d'enfants.",
            ["guardian.add"]          = "Ajouter un compte enfant",
            ["guardian.manage_title"] = "Gérer un compte enfant",

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

            // ── home (the hero + section lead) ──────────────────────────────
            ["home.eyebrow"] = "Où en est ce projet",
            ["home.lead"] =
                "Un chez-soi privé pour un quartier — construit en public, un jalon à la fois. " +
                "C'est la même liste que dans le README, et le code derrière chaque élément est dans le dépôt public.",
            ["home.support"] = "Des questions ou des retours ? Écris à",

            // ── account (Login / Signup — titles + primary actions) ─────────
            ["account.login_title"]   = "Se connecter",
            ["account.login_submit"]  = "Se connecter",
            ["account.login_no_account"] = "Pas encore de compte ?",
            ["account.signup_title"]  = "S'inscrire",
            ["account.signup_submit"] = "S'inscrire",
            ["account.signup_has_account"] = "Tu as déjà un compte ?",

            // ── posts (Index / New / Edit) ──────────────────────────────────
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
            ["groups.posts_heading"]  = "Publications",
            ["groups.new_post"]       = "Nouvelle publication",
            ["groups.posts_empty_can"] =
                "Pas encore de publications. Écris la première — elle sera visible par les membres actuels.",
            ["groups.posts_empty"]    = "Pas encore de publications ici.",
            ["groups.new_title"]      = "Publier dans ce groupe",
            ["groups.new_back"]       = "retour au groupe",
            ["groups.new_submit"]     = "Publier dans le groupe",

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

            // ── posts (Detail page) ──────────────────────────────────────────
            ["posts.back_to"] = "retour à",
            ["posts.detail_edit"] = "Modifier",
            ["posts.edited"] = "modifié",
            ["posts.detail_why"] =
                "Tu peux voir cette publication parce que tu correspondes à son audience " +
                "(une autorisation de ta part, ou tu es l'auteur — la « branche propriétaire » " +
                "de la règle C1 de refus d'audience vide).",
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
                "depuis la page de détail du groupe (M2, plan U10).",
            ["groups.create_desc_hint"] = "Optionnel — une courte note que les autres habitants verront.",
            ["groups.create_private_hint"] =
                "Un groupe privé (p. ex. une famille) est masqué des listes " +
                "d'autorisation/d'accès de tous les autres — seules les personnes que " +
                "tu ajoutes comme membres peuvent l'utiliser. " +
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
            ["community.translation_add"] = "Ajouter",
            ["community.translation_name_label"] = "Nom",
            ["community.translation_desc_label"] = "Description",
            ["community.translation_optional"] = "optionnel",
            ["community.translation_min_one"] = "Au moins un nom ou une description est requis.",
            ["community.translation_save"] = "Enregistrer la traduction",

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
            ["moderation.assign_header"] = "Assigner à un modérateur permanent",
            ["moderation.assign_label"] =
                "Modérateur permanent du composant de ce signalement",
            ["moderation.assign_pick"] = "Choisir un modérateur permanent…",
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
                "Chaque décision d'accès au contenu restreint — Allow et Deny — plus " +
                "les actions admin et les lignes agrégées de listes par lot. " +
                "Toujours actif ; purgé selon le niveau de l'instance (§6.4).",
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
            ["admin.breakglass_consumed"] = "consommé — élévation active jusqu'à expiration",
            ["admin.breakglass_presented"] = "présenté mais pas encore consommé",
            ["admin.breakglass_token_label"] = "Jeton unique (de l'opérateur)",
            ["admin.breakglass_token_hint"] =
                "Consommer ce jeton est une action unique. Il active l'élévation " +
                "jusqu'à son expiration.",
            ["admin.breakglass_consume"] = "Consommer le jeton",

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
            ["about.stats.neighbors"]  = "habitants à bord",
            ["about.stats.groups"]     = "groupes & communautés",
            ["about.stats.posts"]      = "publications & fils ce mois-ci",
            ["about.stats.pinned"]     = "notes épinglées en cours",
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
            ["nav.announcements"] = "Meddelelser",
            ["nav.community"]     = "Fællesskab",
            ["nav.groups"]        = "Grupper",
            ["nav.pages"]         = "Sider",
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

            // ── guardian (the /me/children child-accounts surface) ─────────
            ["guardian.title"]        = "Dine børn",
            ["guardian.lead"]         = "De konti, du har oprettet til et barn, og de kontroller, du har over hver af dem.",
            ["guardian.empty"]        = "Ingen børn endnu.",
            ["guardian.add"]          = "Tilføj en barnkonto",
            ["guardian.manage_title"] = "Administer en barnkonto",

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

            // ── home (the hero + section lead) ──────────────────────────────
            ["home.eyebrow"] = "Hvor projektet står",
            ["home.lead"] =
                "Et privat hjem for ét nabolag — bygget i det åbne, ét milepæl ad gangen. " +
                "Det er den samme liste, du finder i README'en, og koden bag hver post ligger i det offentlige repository.",
            ["home.support"] = "Spørgsmål eller feedback? Skriv til",

            // ── account (Login / Signup — titles + primary actions) ─────────
            ["account.login_title"]   = "Log ind",
            ["account.login_submit"]  = "Log ind",
            ["account.login_no_account"] = "Ingen konto endnu?",
            ["account.signup_title"]  = "Opret konto",
            ["account.signup_submit"] = "Opret konto",
            ["account.signup_has_account"] = "Har du allerede en konto?",

            // ── posts (Index / New / Edit) ──────────────────────────────────
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
            ["groups.posts_heading"]  = "Indlæg",
            ["groups.new_post"]       = "Nyt indlæg",
            ["groups.posts_empty_can"] =
                "Ingen indlæg endnu. Skriv det første — det vises for de nuværende medlemmer.",
            ["groups.posts_empty"]    = "Ingen indlæg her endnu.",
            ["groups.new_title"]      = "Skriv til denne gruppe",
            ["groups.new_back"]       = "tilbage til gruppen",
            ["groups.new_submit"]     = "Skriv til gruppen",

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

            // ── posts (Detail page) ──────────────────────────────────────────
            ["posts.back_to"] = "tilbage til",
            ["posts.detail_edit"] = "Rediger",
            ["posts.edited"] = "redigeret",
            ["posts.detail_why"] =
                "Du kan se dette indlæg, fordi du matcher dets modtagerkreds " +
                "(et af dine tilladelser, eller du er forfatteren — \"ejer-grenen\" " +
                "i C1-reglen om afvisning af tom modtagerkreds).",
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
                "gruppens detailside (M2, plan U10).",
            ["groups.create_desc_hint"] = "Valgfrit — en kort note, andre beboere vil se.",
            ["groups.create_private_hint"] =
                "En privat gruppe (f.eks. en familie) er skjult for alle andres " +
                "tilladelses-/adgangslister — kun de personer, du " +
                "tilføjer som medlemmer, kan bruge den. " +
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
            ["community.translation_add"] = "Tilføj",
            ["community.translation_name_label"] = "Navn",
            ["community.translation_desc_label"] = "Beskrivelse",
            ["community.translation_optional"] = "valgfrit",
            ["community.translation_min_one"] = "Mindst navnet eller beskrivelsen skal udfyldes.",
            ["community.translation_save"] = "Gem oversættelse",

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
            ["moderation.assign_header"] = "Tildel en stående moderator",
            ["moderation.assign_label"] =
                "Stående moderator på rapportens komponent",
            ["moderation.assign_pick"] = "Vælg en stående moderator …",
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
                "Alle adgangsbeslutninger om indhold med begrænset modtagerkreds — Allow og Deny — plus " +
                "admin-handlinger og aggregate rækker for masselister. Altid tændt; slettes pr. " +
                "instans-niveau (§6.4).",
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
            ["admin.breakglass_consumed"] = "forbrugt — forhøjelse aktiv indtil udløb",
            ["admin.breakglass_presented"] = "fremvist, men ikke endnu forbrugt",
            ["admin.breakglass_token_label"] = "Engangstoken (fra operatøren)",
            ["admin.breakglass_token_hint"] =
                "At forbruge denne token er en engangshandling. Den aktiverer forhøjelsen " +
                "indtil dens udløb.",
            ["admin.breakglass_consume"] = "Forbrug token",

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
            ["about.stats.neighbors"]  = "beboere om bord",
            ["about.stats.groups"]     = "grupper & fællesskaber",
            ["about.stats.posts"]      = "indlæg & tråde denne måned",
            ["about.stats.pinned"]     = "faste noter lige nu",
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
        };

    /// <summary>
    /// The closed key set (the admin editor's list, the seeder's loop bound, and
    /// the completeness view's "known" universe). Always equal to
    /// <see cref="EnValues"/>.Keys, in declaration order.
    /// </summary>
    public static IReadOnlyCollection<string> AllKeys => EnValues.Keys.ToList();
}
