namespace Kumunita.Core.SiteContent;

/// <summary>
/// One row in the platform's landing-surface content (ADR 0150): the two
/// heroes' editable eyebrow + lead text and the show/hide toggle for every
/// other section of <c>/home</c> and <c>/about</c>. A **singleton** — one row
/// per instance, <see cref="Id"/> fixed to the sentinel <c>singleton</c> (the
/// exact <c>LocaleSettings</c> shape, ADR 0005 B), so re-resolving it is a
/// plain identity-keyed load. The 13 fields are the **ceiling** for this
/// lane's scope (the ADR 0150 D1 pin); a future SITE-2 lane **adds** fields
/// (additive per ADR 0004 §B.1), it does not re-shape the existing 13. The 4
/// text-field defaults are the **exact** <c>en</c> source text from
/// <c>KnownTranslationKeys.cs</c> (the <c>home.intro_eyebrow</c> /
/// <c>home.intro_lead</c> / <c>about.eyebrow</c> / <c>about.lead</c> keys),
/// byte-identical (SITE·3); the 9 toggle-field defaults are <c>true</c>
/// (every section shown). The <c>LocaleSettings</c> doc is **untouched** —
/// this is a new doc, not a new field on the existing one (SITE·6, the ADR
/// 0150 D6 pin).
/// </summary>
public sealed class SiteContent
{
    public const string SingletonId = "singleton";

    public string Id { get; set; } = SingletonId;

    // ── /home hero (editable text — the exact en source text, SITE·3) ──

    /// <summary>The home hero's eyebrow (default = the <c>home.intro_eyebrow</c>
    /// English text, byte-identical to the shipped <c>kw-l</c> string).</summary>
    public string HomeHeroEyebrow { get; set; } = "A private home for one neighbourhood";

    /// <summary>The home hero's lead (default = the <c>home.intro_lead</c>
    /// English text, byte-identical to the shipped <c>kw-l</c> string).</summary>
    public string HomeHeroLead { get; set; } =
        "One quiet place for everything your street does — the feed, the groups, and the notes that deserve better than a group chat. Private, plain-language, and yours.";

    // ── /home hero sub-copy (editable text — the exact en source text) ──
    // The two supporting lines under the home hero's lead (the <c>kmb-hero-sub</c>
    // paragraphs in Views/Home/Index.cshtml). Admin-editable + translatable like
    // the hero eyebrow + lead (SITE lane extension, ADR 0150 §D5 / ADR 0157).
    // The defaults are the <b>exact</b> <c>en</c> source text from
    // <c>KnownTranslationKeys.cs</c> (the <c>platform.scope_home</c> /
    // <c>platform.fig_home</c> keys), byte-identical (SITE·3).

    /// <summary>The home hero's first sub line — the "scope" line (default =
    /// the <c>platform.scope_home</c> English text, byte-identical to the shipped
    /// <c>kw-l</c> string).</summary>
    public string HomeHeroSub1 { get; set; } =
        "Kumunita started out as a home for one neighbourhood — but it is just as at home with a club, a team, or the people behind a big event. The neighbourhood is the default, not the limit.";

    /// <summary>The home hero's second sub line — the "FIG" line (default = the
    /// <c>platform.fig_home</c> English text, byte-identical to the shipped
    /// <c>kw-l</c> string).</summary>
    public string HomeHeroSub2 { get; set; } =
        "It is built on the Fractal Integration Guidelines (FIG): the idea that a group's real value lives in the links between its people and parts, not in the parts themselves. Kumunita is the software that builds and holds those links.";

    // ── /home section toggles (server-side show/hide, SITE·9) ──

    /// <summary>The home hero's "What it is &amp; how it works" CTA button is shown.</summary>
    public bool HomeShowAboutButton { get; set; } = true;

    /// <summary>The home's two intro sections (hero band + feature-cards band) are
    /// shown — the ADR 0149 "they hide and show as a pair" shape; the ADR 0149
    /// <c>Profile.HideHomeIntro</c> per-resident preference is additive (SITE·5).</summary>
    public bool HomeShowFeatures { get; set; } = true;

    /// <summary>The home's "The plan" roadmap section is shown — governed only by
    /// this platform flag; the ADR 0149 <c>Profile.HideHomeIntro</c> never reaches
    /// this section (SITE·5).</summary>
    public bool HomeShowRoadmap { get; set; } = true;

    // ── /about hero (editable text — the exact en source text, SITE·3) ──

    /// <summary>The about hero's eyebrow (default = the <c>about.eyebrow</c>
    /// English text, byte-identical to the shipped <c>kw-l</c> string).</summary>
    public string AboutHeroEyebrow { get; set; } = "Private by default";

    /// <summary>The about hero's lead (default = the <c>about.lead</c>
    /// English text, byte-identical to the shipped <c>kw-l</c> string).</summary>
    public string AboutHeroLead { get; set; } =
        "One home for everything your neighborhood does — the feed, the groups, and the notes that deserve better than a group chat. Private, plain-language, and yours.";

    // ── /about section toggles (server-side show/hide, SITE·9) ──

    /// <summary>The about's three feature cards are shown.</summary>
    public bool AboutShowFeatures { get; set; } = true;

    /// <summary>The about's scope band is shown.</summary>
    public bool AboutShowScope { get; set; } = true;

    /// <summary>The about's FIG-philosophy band is shown.</summary>
    public bool AboutShowPhilosophy { get; set; } = true;

    /// <summary>The about's code/docs band is shown.</summary>
    public bool AboutShowProject { get; set; } = true;

    /// <summary>The about's "What's new" changelog is shown.</summary>
    public bool AboutShowWhatsNew { get; set; } = true;

    /// <summary>The about's contact CTA band is shown.</summary>
    public bool AboutShowContactCta { get; set; } = true;

    // ── Appearance (ADR 0175 — an additive field, ADR 0004 §B.1) ──

    /// <summary>The platform's default colour palette (<c>forest</c> | <c>ember</c>).
    /// A resident may override it from the account menu (the <c>kumunita.palette</c>
    /// cookie); this is what a resident sees until they do, and what lets
    /// members of several platforms tell them apart. Default <c>forest</c> (the
    /// shipped look); an unknown stored value is normalized to <c>forest</c> by
    /// the Web layer (<c>PaletteCookie.Normalize</c>), never an error.</summary>
    public string DefaultPalette { get; set; } = "forest";
}
