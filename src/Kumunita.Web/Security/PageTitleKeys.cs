using System.Diagnostics.CodeAnalysis;
using Kumunita.Core.Localization;

namespace Kumunita.Web.Security;

/// <summary>
/// The English page titles that views assign to <c>ViewData["Title"]</c>, mapped to their
/// <c>title.*</c> translation keys (ADR 0273). <c>_Layout</c> resolves the browser-tab
/// <c>&lt;title&gt;</c> through <see cref="TryGetKey"/> with the effective language, so the
/// views stay untouched and an English title is the floor.
/// </summary>
/// <remarks>
/// Titles built from data (a person's name, <c>$"Admin — {name}"</c>, a post title) are not in
/// the map and pass through unchanged. The few generic fallbacks a view uses when an item has
/// no title (<c>"Event"</c>, <c>"Post"</c>…) are mapped; an item literally titled the same
/// would be shown translated in the tab only — accepted.
/// </remarks>
public static class PageTitleKeys
{
    private static readonly Dictionary<string, string> ByEnglish = new(StringComparer.Ordinal)
    {
        ["About"] = "title.about",
        ["Access denied"] = "title.access_denied",
        ["Add a child account"] = "title.add_a_child_account",
        ["Add tag"] = "title.add_tag",
        ["Admin"] = "title.admin",
        ["Admin onboarding"] = "title.admin_onboarding",
        ["Admin — Accounts"] = "title.admin_accounts",
        ["Admin — Analytics"] = "title.admin_analytics",
        ["Admin — Announcement comments"] = "title.admin_announcement_comments",
        ["Admin — Audit"] = "title.admin_audit",
        ["Admin — Break glass"] = "title.admin_break_glass",
        ["Admin — Communities"] = "title.admin_communities",
        ["Admin — Error report detail"] = "title.admin_error_report_detail",
        ["Admin — Error reports"] = "title.admin_error_reports",
        ["Admin — Escalation settings"] = "title.admin_escalation_settings",
        ["Admin — Guests"] = "title.admin_guests",
        ["Admin — Help pages"] = "title.admin_help_pages",
        ["Admin — Messaging"] = "title.admin_messaging",
        ["Admin — Platform"] = "title.admin_platform",
        ["Admin — Platform default date & time format"] = "title.admin_platform_default_date_time_format",
        ["Admin — Platform default time zone"] = "title.admin_platform_default_time_zone",
        ["Admin — Portability"] = "title.admin_portability",
        ["Admin — Quiet-time cadence"] = "title.admin_quiet_time_cadence",
        ["Admin — Receiving tokens"] = "title.admin_receiving_tokens",
        ["Admin — Sample data"] = "title.admin_sample_data",
        ["Admin — Security"] = "title.admin_security",
        ["Admin — Sign-up"] = "title.admin_sign_up",
        ["Admin — Site content"] = "title.admin_site_content",
        ["Admin — Storage"] = "title.admin_storage",
        ["Admin — Storage settings"] = "title.admin_storage_settings",
        ["Admin — Surface labels"] = "title.admin_surface_labels",
        ["Announcement"] = "title.announcement",
        ["Announcements"] = "title.announcements",
        ["Board"] = "title.board",
        ["Boards"] = "title.boards",
        ["Bookmarks"] = "title.bookmarks",
        ["Choose your language"] = "title.choose_your_language",
        ["Complete first-boot setup"] = "title.complete_first_boot_setup",
        ["Create a group"] = "title.create_a_group",
        ["Deleted documents"] = "title.deleted_documents",
        ["Directory"] = "title.directory",
        ["Document"] = "title.document",
        ["Documents"] = "title.documents",
        ["Edit a document"] = "title.edit_a_document",
        ["Edit announcement"] = "title.edit_announcement",
        ["Edit board"] = "title.edit_board",
        ["Edit event"] = "title.edit_event",
        ["Edit goal"] = "title.edit_goal",
        ["Edit group post"] = "title.edit_group_post",
        ["Edit item"] = "title.edit_item",
        ["Edit note"] = "title.edit_note",
        ["Edit project"] = "title.edit_project",
        ["Edit to-do"] = "title.edit_to_do",
        ["Edit your profile"] = "title.edit_your_profile",
        ["Error"] = "title.error",
        ["Event"] = "title.event",
        ["Events"] = "title.events",
        ["Events calendar"] = "title.events_calendar",
        ["Goal"] = "title.goal",
        ["Groups"] = "title.groups",
        ["Home"] = "title.home",
        ["Inventory"] = "title.inventory",
        ["Kiosk"] = "title.kiosk",
        ["Languages"] = "title.languages",
        ["Manage a child account"] = "title.manage_a_child_account",
        ["Messages"] = "title.messages",
        ["Moderation"] = "title.moderation",
        ["Move bookmark"] = "title.move_bookmark",
        ["Move note"] = "title.move_note",
        ["My data"] = "title.my_data",
        ["My drafts"] = "title.my_drafts",
        ["My storage"] = "title.my_storage",
        ["New announcement"] = "title.new_announcement",
        ["New board"] = "title.new_board",
        ["New event"] = "title.new_event",
        ["New folder"] = "title.new_folder",
        ["New goal"] = "title.new_goal",
        ["New item"] = "title.new_item",
        ["New project"] = "title.new_project",
        ["New to-do"] = "title.new_to_do",
        ["Notes"] = "title.notes",
        ["Notification preferences"] = "title.notification_preferences",
        ["Notification subscriptions"] = "title.notification_subscriptions",
        ["Notifications"] = "title.notifications",
        ["Onboarding"] = "title.onboarding",
        ["Page"] = "title.page",
        ["Pages"] = "title.pages",
        ["People"] = "title.people",
        ["Post"] = "title.post",
        ["Post to group"] = "title.post_to_group",
        ["Preview — how I appear"] = "title.preview_how_i_appear",
        ["Project"] = "title.project",
        ["Projects"] = "title.projects",
        ["Remove tag"] = "title.remove_tag",
        ["Rename folder"] = "title.rename_folder",
        ["Report an issue"] = "title.report_an_issue",
        ["Resend confirmation email"] = "title.resend_confirmation_email",
        ["Resolve my import"] = "title.resolve_my_import",
        ["Search"] = "title.search",
        ["Settings — Change password"] = "title.settings_change_password",
        ["Settings — Date & time format"] = "title.settings_date_time_format",
        ["Settings — Delete account"] = "title.settings_delete_account",
        ["Settings — Home page"] = "title.settings_home_page",
        ["Settings — Items per page"] = "title.settings_items_per_page",
        ["Settings — Language"] = "title.settings_language",
        ["Settings — Messaging"] = "title.settings_messaging",
        ["Settings — Quiet hours"] = "title.settings_quiet_hours",
        ["Settings — Security"] = "title.settings_security",
        ["Settings — Time zone"] = "title.settings_time_zone",
        ["Sign in"] = "title.sign_in",
        ["Sign-up is closed"] = "title.sign_up_is_closed",
        ["Tags"] = "title.tags",
        ["To-do"] = "title.to_do",
        ["To-dos"] = "title.to_dos",
        ["Upload a document"] = "title.upload_a_document",
        ["Verify your account"] = "title.verify_your_account",
        ["Write a post"] = "title.write_a_post",
        ["Your children"] = "title.your_children",
    };

    /// <summary>Every <c>title.*</c> key, for the registry-parity test.</summary>
    public static IReadOnlyDictionary<string, string> All => ByEnglish;

    public static bool TryGetKey(string? englishTitle, [NotNullWhen(true)] out string? key)
    {
        key = null;
        return englishTitle is not null && ByEnglish.TryGetValue(englishTitle, out key);
    }
}
