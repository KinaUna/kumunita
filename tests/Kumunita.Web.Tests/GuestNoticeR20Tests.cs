using Kumunita.Core.Localization;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// IMPROVE-UX R20 — the guest-notice closer (finding E18: "Guest notice
/// gives no end date or next step"). Two states of a signed-in guest are
/// handled in <c>Views/Shared/_LayoutNotices.cshtml</c>:
///
/// <list type="number">
///   <item><b>Window live</b> (the <c>Guest</c> role is present) — the
///   existing <c>account.guest_welcome</c> text, PLUS when the window ends
///   (<c>account.guest_window_end</c> + a <c>&lt;kw-dt&gt;</c> of
///   <c>GuestAccess.ValidUntil</c> in the guest's own effective zone, ADR
///   0019/0020), PLUS a "message the admins" affordance to
///   <c>GET /issues/new</c> (the existing resident feedback surface — the
///   R08/R22 channel).</item>
///   <item><b>Window lapsed</b> (the <c>Guest</c> role is ABSENT) — a
///   friendly "your guest access has ended" notice
///   (<c>account.guest_window_expired</c>) + the same "message the admins"
///   affordance, so the guest is not left on a bare shell (the E18 dead end).
///   Detection is <c>Profile.IsGuest == true && now &ge; ValidUntil</c> — the
///   profile + <c>GuestAccess</c> doc, NOT the role (which is absent here) —
///   and the branch is gated on the absence of Member / Moderator /
///   GlobalAdmin so the reads never run for an ordinary resident / admin /
///   moderator.</item>
/// </list>
///
/// House "string pin, no TestServer" idiom (the
/// <see cref="F7F8_HardeningGateTests"/> / AdminGuestsControllerTests
/// precedent): read the
/// partial + the four translation surface files, assert the contract. Static,
/// no Testcontainers.
/// </summary>
public class GuestNoticeR20Tests
{
    // ── Repo-root resolution (the house idiom — walk up to Kumunita.slnx) ─

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
                dir = dir.Parent;
            Assert.True(dir is not null, "Could not locate the repo root (Kumunita.slnx).");
            return dir!.FullName;
        }
    }

    private static string Read(string relative)
        => File.ReadAllText(Path.Combine(RepoRoot, relative));

    private static string Notices()
    {
        var raw = Read("src/Kumunita.Web/Views/Shared/_LayoutNotices.cshtml");
        // Strip Razor comments — they are author documentation, not rendered
        // markup: the pin asserts on the shape the browser sees.
        return System.Text.RegularExpressions.Regex.Replace(
            raw, @"\@\*.*?\*\@", "", System.Text.RegularExpressions.RegexOptions.Singleline);
    }

    [Fact(DisplayName = "R20 live notice: window-end date + a /issues/new 'message the admins' link in the guest block")]
    public void LiveNotice_Has_WindowEnd_And_AdminLink()
    {
        var html = Notices();

        // The live branch is still gated on the Guest role (C-M19·3 — the role
        // is minted only while the window is live).
        Assert.Contains("KumunitaPrincipal.HasRole(User, \"Guest\")", html, StringComparison.Ordinal);

        // The window-end affordance: the new key + the <kw-dt> of ValidUntil in
        // the guest's own effective zone (ADR 0019/0020 — the kw-dt TagHelper
        // is the seam, not a hand-rolled ToString).
        Assert.Contains("key=\"account.guest_window_end\"", html, StringComparison.Ordinal);
        Assert.Contains("kw-dt", html, StringComparison.Ordinal);
        Assert.Contains("_guest.ValidUntil", html, StringComparison.Ordinal);

        // The "message the admins" affordance targets the existing resident
        // feedback surface (GET /issues/new — the R08/R22 channel), not a new
        // form.
        Assert.Contains("/issues/new", html, StringComparison.Ordinal);
        Assert.Contains("key=\"account.guest_reach_admins\"", html, StringComparison.Ordinal);

        // The live notice keeps its existing identity (the kw-l en floor + the
        // data-kmb-notice attribute notice-strip.js keys off).
        Assert.Contains("key=\"account.guest_welcome\"", html, StringComparison.Ordinal);
        Assert.Contains("data-kmb-notice=\"guest\"", html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "R20 lapsed notice: a friendly 'window expired' state + the same /issues/new admin link, detected via Profile.IsGuest + ValidUntil (NOT the role)")]
    public void LapsedNotice_Has_Expiry_And_AdminLink()
    {
        var html = Notices();

        // The lapsed branch is its own strip (a distinct data-kmb-notice so
        // notice-strip.js / a11y can address it separately from the live one).
        Assert.Contains("data-kmb-notice=\"guest-lapsed\"", html, StringComparison.Ordinal);

        // The friendly "window expired" text + the same "message the admins"
        // affordance (the E18 dead end is gone).
        Assert.Contains("key=\"account.guest_window_expired\"", html, StringComparison.Ordinal);
        Assert.Contains("key=\"account.guest_reach_admins\"", html, StringComparison.Ordinal);
        Assert.Contains("/issues/new", html, StringComparison.Ordinal);

        // Detection is the profile + GuestAccess doc (NOT the Guest role, which
        // is absent in the lapsed state): IsGuest read + now ≥ ValidUntil.
        Assert.Contains("UserInfo.GetProfileAsync", html, StringComparison.Ordinal);
        Assert.Contains("IsGuest", html, StringComparison.Ordinal);
        Assert.Contains("ValidUntil", html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "R20 lapsed branch: the reads are gated OFF the Member / Moderator / GlobalAdmin path (no read for an ordinary resident / admin / moderator)")]
    public void LapsedBranch_Is_Gated_Off_OrdinaryResidents()
    {
        var html = Notices();

        // The lapsed branch short-circuits on the absence of the three
        // standing roles (a guest never gets Member — C-M19·2 — and a Member /
        // Moderator / GlobalAdmin is never a guest), so the profile +
        // GuestAccess reads never run for them.
        Assert.Contains("HasRole(User, \"Member\")", html, StringComparison.Ordinal);
        Assert.Contains("HasRole(User, \"Moderator\")", html, StringComparison.Ordinal);
        Assert.Contains("HasRole(User, \"GlobalAdmin\")", html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "R20 keys: account.guest_window_end / account.guest_reach_admins / account.guest_window_expired are present + non-empty in all four languages (en / de / fr / da)")]
    public void R20_Keys_Present_In_All_Four_Languages()
    {
        var keys = new[]
        {
            "account.guest_window_end",
            "account.guest_reach_admins",
            "account.guest_window_expired",
        };
        Assert.Equal(3, keys.Length);

        var dictionaries = new (string Name, IReadOnlyDictionary<string, string> Values)[]
        {
            ("EnValues", KnownTranslationKeys.EnValues),
            ("DeValues", KnownTranslationKeys.DeValues),
            ("FrValues", KnownTranslationKeys.FrValues),
            ("DaValues", KnownTranslationKeys.DaValues),
        };

        foreach (var key in keys)
        {
            foreach (var (name, values) in dictionaries)
            {
                Assert.True(values.ContainsKey(key),
                    $"R20 key '{key}' is missing from {name}");
                Assert.False(string.IsNullOrWhiteSpace(values[key]),
                    $"R20 key '{key}' has an empty value in {name}");
            }
        }
    }
}
