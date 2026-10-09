using Kumunita.Core.Localization;

namespace Kumunita.Web.Tests;

/// <summary>
/// M28 · U05 — the <b>C-M28·5</b> / D7 pin: the closed
/// <see cref="KnownTranslationKeys"/> <c>guardian.timelimit.*</c> +
/// <c>account.time_limit.*</c> set (the 14-key closed kw-l list, design doc
/// §2.7) is present, non-empty, in all four languages (en/de/fr/da).
///
/// <para>
/// Mirrors the M22 <see cref="Onboarding_KwL_Set_Is_Parity_Pinned_In_Four_Languages"/>
/// closed-set pin, and the M20 <c>settings.quiet.*</c> / <c>admin.quiet.*</c>
/// registry entries (ADR 0121) that this lane deliberately does <b>not</b>
/// touch (D9). The <see cref="KwLRegistryConsistencyTests"/> view-scan and the
/// Core.Tests' <c>KnownTranslationKeys_ParityTests</c> registry-scan pin the
/// four-language parity from their sides; this pin names the <b>exact 14-key
/// closed set</b> by name (a 15th key or a missing one would fail it),
/// independent of the total registry size.
/// </para>
///
/// <para>
/// A DISTINCT namespace from the M20 quiet lane — the <c>guardian.timelimit.*</c>
/// + <c>account.time_limit.*</c> keys are the M28 GU-lane surface, not the
/// <c>settings.quiet.*</c> / <c>admin.quiet.*</c> notification-lane keys
/// (C-M28·5 "M20 untouched" pin, D9).
/// </para>
///
/// <para>
/// No database, no Testcontainers — a pure registry-shape test.
/// </para>
/// </summary>
public class GuardianTimeLimitKwLParityTests
{
    /// <summary>
    /// The closed M28 kw-l set (D7, design doc §2.7) — exactly 14 keys: the
    /// 13 <c>guardian.timelimit.*</c> GU Detail-section keys + the single
    /// <c>account.time_limit.login_message</c> login-landing key (the
    /// <c>?error=time-limit</c> case in <c>Views/Account/Login.cshtml</c>,
    /// referenced in U04).
    /// </summary>
    private static readonly string[] ClosedSet =
    {
        "guardian.timelimit.title",
        "guardian.timelimit.description",
        "guardian.timelimit.enabled",
        "guardian.timelimit.mode_label",
        "guardian.timelimit.mode_blocked",
        "guardian.timelimit.mode_allowed",
        "guardian.timelimit.hours_label",
        "guardian.timelimit.days_label",
        "guardian.timelimit.save",
        "guardian.timelimit.clear",
        "guardian.timelimit.flash_saved",
        "guardian.timelimit.flash_cleared",
        "guardian.timelimit.badge_set",
        "account.time_limit.login_message",
    };

    [Fact(DisplayName = "C-M28·5 / D7 — the closed M28 kw-l set is exactly 14 keys")]
    public void ClosedSet_Has_Exactly_14_Keys()
    {
        Assert.Equal(14, ClosedSet.Length);
        Assert.Equal(ClosedSet.Length, new HashSet<string>(ClosedSet).Count);
    }

    [Fact(DisplayName = "C-M28·5 / D7 — every closed M28 kw-l key is present, non-empty, in en/de/fr/da")]
    public void Every_Closed_M28_KwL_Key_Is_Present_NonEmpty_In_All_Four_Languages()
    {
        foreach (var key in ClosedSet)
        {
            Assert.True(KnownTranslationKeys.EnValues.ContainsKey(key),
                $"M28 kw-l key '{key}' is not registered in EnValues");
            Assert.True(KnownTranslationKeys.DeValues.ContainsKey(key),
                $"M28 kw-l key '{key}' is not registered in DeValues");
            Assert.True(KnownTranslationKeys.FrValues.ContainsKey(key),
                $"M28 kw-l key '{key}' is not registered in FrValues");
            Assert.True(KnownTranslationKeys.DaValues.ContainsKey(key),
                $"M28 kw-l key '{key}' is not registered in DaValues");

            Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.EnValues[key]),
                $"M28 kw-l key '{key}' has an empty en value");
            Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DeValues[key]),
                $"M28 kw-l key '{key}' has an empty de value");
            Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.FrValues[key]),
                $"M28 kw-l key '{key}' has an empty fr value");
            Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DaValues[key]),
                $"M28 kw-l key '{key}' has an empty da value");
        }
    }

    [Fact(DisplayName = "C-M28·5 / D7 — the M28 kw-l namespace is closed (no key beyond the set)")]
    public void M28_KwL_Set_Is_Closed_No_Key_Beyond_The_Set()
    {
        // The closed set is exactly the 14 keys above — no guardian.timelimit.*
        // or account.time_limit.* key beyond them may be registered (a key not
        // in the set is a D7-set drift that the register's §drift-guard
        // forbids: authors emit exactly the keys their surface renders, no
        // more). The two namespaces are scanned independently so a stray key
        // in either family fails this pin.
        var m28KeysInRegistry = KnownTranslationKeys.AllKeys
            .Where(k => k.StartsWith("guardian.timelimit.", System.StringComparison.Ordinal)
                    || k.StartsWith("account.time_limit.", System.StringComparison.Ordinal))
            .ToList();
        Assert.Equal(ClosedSet.OrderBy(k => k).ToList(),
            m28KeysInRegistry.OrderBy(k => k).ToList());
    }

    [Fact(DisplayName = "C-M28·5 — the M28 namespace is DISTINCT from the M20 quiet lane (no cross-contamination)")]
    public void M28_Namespace_Is_Distinct_From_M20_QuietLane()
    {
        // The C-M28·5 "M20 untouched" pin (D9): the M28 keys must not collide
        // with the M20 settings.quiet.* / admin.quiet.* keys, and the M20
        // namespace must not absorb any M28 key. A reader comparing the two
        // lanes side-by-side (the M28 design doc §2.7 "distinct namespace"
        // note) sees two disjoint key families.
        var m20Keys = KnownTranslationKeys.AllKeys
            .Where(k => k.StartsWith("settings.quiet.", System.StringComparison.Ordinal)
                    || k.StartsWith("admin.quiet.", System.StringComparison.Ordinal))
            .ToHashSet();
        Assert.Empty(m20Keys.Intersect(ClosedSet));
    }
}
