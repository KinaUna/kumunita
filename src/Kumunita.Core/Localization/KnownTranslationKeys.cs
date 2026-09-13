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
/// single-admin, pre-community), the product-story landing, or the FAQ
/// placeholder — recorded in ADR 0015 (full-sweep amendment).</li>
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
            ["nav.directory"]     = "Directory",
            ["nav.sign_in"]       = "Sign in",
            ["nav.sign_up"]       = "Sign up",
            ["nav.profile"]       = "Profile",
            ["nav.admin"]         = "Admin",
            ["nav.sign_out"]      = "Sign out",

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

            // ── settings (the language-picker labels) ───────────────────────
            ["settings.settings"]       = "Settings",
            ["settings.choose_language"] = "Choose your language",

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
            ["posts.new_submit"]   = "Post it",
            ["posts.edit_title"]   = "Edit post",
            ["posts.edit_save"]    = "Save changes",
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
                "title and body below are editable.",
            ["groups.edit_title_label"] = "Title",
            ["groups.edit_title_hint"] =
                "A short headline (≤ 120 chars). Leave blank for a " +
                "body-only post — the list will show your first line " +
                "of the body instead.",
            ["groups.edit_body_label"] = "Body",
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
        };

    /// <summary>
    /// The closed key set (the admin editor's list, the seeder's loop bound, and
    /// the completeness view's "known" universe). Always equal to
    /// <see cref="EnValues"/>.Keys, in declaration order.
    /// </summary>
    public static IReadOnlyCollection<string> AllKeys => EnValues.Keys.ToList();
}
