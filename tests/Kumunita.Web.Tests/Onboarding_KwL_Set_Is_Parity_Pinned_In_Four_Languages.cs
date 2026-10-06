using Kumunita.Core.Localization;

namespace Kumunita.Web.Tests;

/// <summary>
/// M22 · U03 — the <b>GATE-6</b> / C-M22·6 pin: the closed
/// <see cref="KnownTranslationKeys"/> <c>onboarding.*</c> set (D7) is present,
/// non-empty, in all four languages (en/de/fr/da).
///
/// <para>
/// U02's <see cref="OnboardingControllerTests"/> pins GATE-3 (the walk-through's
/// only write is <c>CompleteOnboardingAsync</c>) and GATE-4 (zero new
/// authorization surface) on the controller side. This file pins the
/// <b>localization surface</b>: the 15-key closed set (D7, the register's
/// §"closed <c>kw-l</c> key set" table) is registered, non-empty, in
/// en/de/fr/da — the four-language parity closure the
/// <see cref="KwLRegistryConsistencyTests"/> + Core.Tests'
/// <c>KnownTranslationKeys_ParityTests</c> pins from the *registry side*
/// (en⇄de⇄fr⇄da key-set equality, non-empty values). This test pins the
/// <b>exact 15-key closed set</b> by name (a 16th key or a missing one
/// would fail it), independent of whatever the total registry size is.
/// </para>
///
/// <para>
/// Non-blocking, no claim read, no field-write lane (D5/D6, C-M22·2/C-M22·5):
/// the pin asserts only the localization registry shape — the
/// <c>OnboardingViewModel.BannerEligible</c> shape + the "no write / no
/// authorization" surface are the U02 controller-side pins.
/// </para>
///
/// <para>
/// No database, no Testcontainers — a pure registry-shape test.
/// </para>
/// </summary>
public class Onboarding_KwL_Set_Is_Parity_Pinned_In_Four_Languages
{
    /// <summary>
    /// The closed M22 <c>onboarding.*</c> set (D7) — exactly 15 keys, the
    /// register's §"closed <c>kw-l</c> key set" table verbatim. The
    /// <see cref="KwLRegistryConsistencyTests.Every_KwL_Key_In_A_View_Is_Registered"/>
    /// scan pins every <c>kw-l key="…"</c> a view emits is registered in
    /// <see cref="KnownTranslationKeys.EnValues"/>; this pin names the exact
    /// 15 and asserts each is present, non-empty, in all four dictionaries.
    /// </summary>
    private static readonly string[] ClosedSet =
    {
        "onboarding.title",
        "onboarding.intro",
        "onboarding.step_displayname",
        "onboarding.step_avatar",
        "onboarding.step_language",
        "onboarding.step_timezone",
        "onboarding.step_dateformat",
        "onboarding.step_email",
        "onboarding.step_contact",
        // M9 amendment (ADR 0139) — the messaging opt-in step, added when the
        // walk-through gained the /settings/messaging step (a deliberate growth
        // of the closed set, not a drift: this pin is updated in the same change).
        "onboarding.step_messaging",
        "onboarding.visit",
        "onboarding.finish",
        "onboarding.skip",
        "onboarding.flash_done",
        "onboarding.banner.text",
        "onboarding.banner.action",
    };

    [Fact(DisplayName = "GATE-6 / C-M22·6 — the closed onboarding.* set is exactly 16 keys")]
    public void ClosedSet_Has_Exactly_16_Keys()
    {
        Assert.Equal(16, ClosedSet.Length);
        Assert.Equal(ClosedSet.Length, new HashSet<string>(ClosedSet).Count);
    }

    [Fact(DisplayName = "GATE-6 / C-M22·6 — every closed onboarding.* key is present, non-empty, in en/de/fr/da")]
    public void Every_Closed_Onboarding_Key_Is_Present_NonEmpty_In_All_Four_Languages()
    {
        foreach (var key in ClosedSet)
        {
            Assert.True(KnownTranslationKeys.EnValues.ContainsKey(key),
                $"onboarding.* key '{key}' is not registered in EnValues");
            Assert.True(KnownTranslationKeys.DeValues.ContainsKey(key),
                $"onboarding.* key '{key}' is not registered in DeValues");
            Assert.True(KnownTranslationKeys.FrValues.ContainsKey(key),
                $"onboarding.* key '{key}' is not registered in FrValues");
            Assert.True(KnownTranslationKeys.DaValues.ContainsKey(key),
                $"onboarding.* key '{key}' is not registered in DaValues");

            Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.EnValues[key]),
                $"onboarding.* key '{key}' has an empty en value");
            Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DeValues[key]),
                $"onboarding.* key '{key}' has an empty de value");
            Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.FrValues[key]),
                $"onboarding.* key '{key}' has an empty fr value");
            Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DaValues[key]),
                $"onboarding.* key '{key}' has an empty da value");
        }
    }

    [Fact(DisplayName = "GATE-6 / C-M22·6 — the onboarding.* set is closed (no 17th key in the registry)")]
    public void Onboarding_Set_Is_Closed_No_Key_Beyond_The_Set()
    {
        // The closed set is exactly the 16 keys above — no onboarding.* key
        // beyond them may be registered (a key not in the set is a D7-set drift
        // that the register's §drift-guard forbids: authors emit exactly the
        // keys their surface renders, no more).
        var onboardingKeysInRegistry = KnownTranslationKeys.AllKeys
            .Where(k => k.StartsWith("onboarding.", System.StringComparison.Ordinal))
            .ToList();
        Assert.Equal(ClosedSet.OrderBy(k => k).ToList(),
            onboardingKeysInRegistry.OrderBy(k => k).ToList());
    }
}
