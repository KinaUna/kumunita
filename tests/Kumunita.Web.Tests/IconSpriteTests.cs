using System.Text.RegularExpressions;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0279 — the hand-drawn icon sprite. Every top-row / rail section icon,
/// the top-bar bell + globe and the account-menu item icons are
/// <c>&lt;svg class="kmb-icon"&gt;&lt;use href="#kmb-i-…"/&gt;&lt;/svg&gt;</c>
/// references into one sprite (<c>Views/Shared/_IconSprite.cshtml</c>),
/// rendered once by <c>_Layout</c>. ADR 0283 extends it to the tab strips
/// (whose icon ids live in the partials' tab tuples as <c>"kmb-i-…"</c>
/// strings) and the button-group switches, so references are collected from
/// every view, not just the two nav partials.
///
/// <para>
/// Structural string pins in the house idiom (<see cref="NavMoreFoldTests"/>,
/// "string pin, no TestServer"): a broken reference renders an empty box with
/// no error anywhere, so the pins are the only thing that catches a typo'd id
/// or a symbol deleted while still in use.
/// </para>
/// </summary>
public class IconSpriteTests
{
    // Any kmb-i-… token in a view other than the sprite: a literal
    // <use href="#kmb-i-x"> or a tab tuple's "kmb-i-x" string (ADR 0283).
    private static readonly Regex IconRef = new("kmb-i-([a-z0-9-]+)", RegexOptions.Compiled);
    private static readonly Regex SymbolId = new("<symbol id=\"(kmb-i-[a-z0-9-]+)\"", RegexOptions.Compiled);

    [Fact(DisplayName = "ADR 0279: _Layout renders the icon sprite partial")]
    public void Layout_Renders_The_Sprite()
    {
        var layout = ReadView("_Layout.cshtml");
        Assert.Contains("<partial name=\"_IconSprite\" />", layout, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ADR 0279: every <use href=\"#kmb-i-…\"> in the nav has a matching sprite symbol")]
    public void Every_Referenced_Icon_Has_A_Symbol()
    {
        var symbols = SymbolId.Matches(ReadView("_IconSprite.cshtml")).Select(m => m.Groups[1].Value).ToList();

        // Ids are unique — a duplicate would silently shadow the other drawing.
        Assert.Equal(symbols.Count, symbols.Distinct().Count());

        var referenced = ReferencedIds().ToList();

        Assert.NotEmpty(referenced);
        var missing = referenced.Except(symbols).ToList();
        Assert.True(missing.Count == 0, "Icons referenced but not in _IconSprite.cshtml: " + string.Join(", ", missing));
    }

    [Fact(DisplayName = "ADR 0279: every sprite symbol is used (no dead drawings)")]
    public void Every_Symbol_Is_Referenced()
    {
        var symbols = SymbolId.Matches(ReadView("_IconSprite.cshtml")).Select(m => m.Groups[1].Value);
        var referenced = ReferencedIds();
        var unused = symbols.Where(s => !referenced.Contains(s)).ToList();
        Assert.True(unused.Count == 0, "Sprite symbols nothing references: " + string.Join(", ", unused));
    }

    [Fact(DisplayName = "ADR 0279: the nav no longer carries the old inline 16-grid icons")]
    public void No_Legacy_Inline_Nav_Icons()
    {
        // _AccountNav: none at all.
        Assert.DoesNotContain("viewBox=\"0 0 16 16\"", ReadView("_AccountNav.cshtml"), StringComparison.Ordinal);

        // _Layout: only the rail expander chevron stays inline (it is a control
        // glyph rotated by CSS, not a section icon).
        var inline16 = Regex.Matches(ReadView("_Layout.cshtml"), "viewBox=\"0 0 16 16\"").Count;
        Assert.Equal(1, inline16);
    }

    [Fact(DisplayName = "ADR 0283: every tab strip and button-group switch carries a sprite icon")]
    public void Tabs_And_Switches_Carry_Icons()
    {
        // The tuple-driven strips render the icon from the tuple's Icon field.
        // _ProjectsTabs was moved from Projects/ → Shared/ (view-path reorg).
        foreach (var partial in new[] { "Shared/_ProjectsTabs.cshtml", "Event/_EventsTabs.cshtml",
                                        "Locale/_SettingsTabs.cshtml", "Admin/_AdminNav.cshtml" })
        {
            var src = ReadViewAt(partial);
            Assert.Contains("string Icon)[]", src, StringComparison.Ordinal);
            Assert.Contains("<use href=\"@(\"#\" + tab.Icon)\"/>", src, StringComparison.Ordinal);
        }

        // The hand-written strips + switches reference their symbols literally.
        (string View, string[] Ids)[] literal =
        {
            ("Shared/_MessagesTabs.cshtml",   new[] { "messages", "group-messages" }),
            ("Groups/Detail.cshtml",          new[] { "community", "groups", "settings" }),
            ("Event/Index.cshtml",            new[] { "upcoming", "past" }),
            ("Event/Calendar.cshtml",         new[] { "cal-day", "cal-week", "cal-month" }),
            ("Languages/Translations.cshtml", new[] { "edit-one", "edit-batch" }),
        };
        foreach (var (view, ids) in literal)
        {
            var src = ReadViewAt(view);
            foreach (var id in ids)
                Assert.Contains($"<use href=\"#kmb-i-{id}\"/>", src, StringComparison.Ordinal);
        }
    }

    /// <summary>Every kmb-i-… id referenced from any view except the sprite itself, as full ids.</summary>
    private static HashSet<string> ReferencedIds()
    {
        var views = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views");
        return Directory.EnumerateFiles(views, "*.cshtml", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith("_IconSprite.cshtml", StringComparison.Ordinal))
            .SelectMany(f => IconRef.Matches(File.ReadAllText(f)).Select(m => "kmb-i-" + m.Groups[1].Value))
            .ToHashSet();
    }

    private static string ReadViewAt(string relative)
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"{relative} not found at {path}.");
        return File.ReadAllText(path);
    }

    private static string ReadView(string file)
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", "Shared", file);
        Assert.True(File.Exists(path), $"{file} not found at {path}.");
        return File.ReadAllText(path);
    }

    /// <summary>Repo root located by walking up to <c>Kumunita.slnx</c> (the <see cref="PwaManifestTests"/> idiom).</summary>
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
}
