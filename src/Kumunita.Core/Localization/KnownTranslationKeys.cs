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
/// per string to <c>en</c> then to the key itself (M·1/M·2).</item>
/// <item>The <b>admin editor</b> (U6) lists <see cref="AllKeys"/> as the closed
/// set with each key's <c>en</c> value as the reference text — no hand-typed
/// key (the plan's Gap 3 fix).</item>
/// </list>
/// <para>
/// <b>Scope discipline (M·3 / the plan's "in-scope surface").</b> This is the
/// <b>curated platform surface only</b> — the shared layout (nav, footer,
/// language-picker labels) plus the page headings, primary action labels, and
/// empty-states of the named resident/admin pages. It is deliberately <b>not</b>
/// "every string in every view". Two hard exclusions hold for the whole set:
/// </para>
/// <ul>
/// <li><b>Never a UGC key</b> (M·3): a post/reply body, a group description, an
/// announcement body, or an author's display name is authored and rendered as
/// written — it is <b>not</b> here.</li>
/// <li><b>No out-of-scope surface</b>: moderation-only controls, the setup-flow
/// (first-boot token) labels, and pages outside the named surface are not keyed
/// (a drift pause if a unit is tempted to add one).</li>
/// </ul>
/// <para>
/// <b>Upgrade-safe.</b> The seeder upserts with <b>code-wins for <c>en</c></b>:
/// adding a key here flows through on the next start (new keys appear on
/// upgrade), and refreshing an <c>en</c> value refreshes the row. The seeder
/// never reads or writes a non-<c>en</c> row (admin translations are data, not
/// config — ADR 0005 B).
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
