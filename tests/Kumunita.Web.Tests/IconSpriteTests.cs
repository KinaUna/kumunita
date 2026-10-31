using System.Text.RegularExpressions;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0279 — the hand-drawn icon sprite. Every top-row / rail section icon,
/// the top-bar bell + globe and the account-menu item icons are
/// <c>&lt;svg class="kmb-icon"&gt;&lt;use href="#kmb-i-…"/&gt;&lt;/svg&gt;</c>
/// references into one sprite (<c>Views/Shared/_IconSprite.cshtml</c>),
/// rendered once by <c>_Layout</c>.
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
    private static readonly Regex UseRef = new("<use href=\"#(kmb-i-[a-z0-9-]+)\"", RegexOptions.Compiled);
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

        var referenced = new[] { "_Layout.cshtml", "_AccountNav.cshtml" }
            .SelectMany(f => UseRef.Matches(ReadView(f)).Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.NotEmpty(referenced);
        var missing = referenced.Except(symbols).ToList();
        Assert.True(missing.Count == 0, "Icons referenced but not in _IconSprite.cshtml: " + string.Join(", ", missing));
    }

    [Fact(DisplayName = "ADR 0279: every sprite symbol is used (no dead drawings)")]
    public void Every_Symbol_Is_Referenced()
    {
        var symbols = SymbolId.Matches(ReadView("_IconSprite.cshtml")).Select(m => m.Groups[1].Value);
        var referenced = new[] { "_Layout.cshtml", "_AccountNav.cshtml" }
            .SelectMany(f => UseRef.Matches(ReadView(f)).Select(m => m.Groups[1].Value))
            .ToHashSet();

        // TODO(ADR 0279): when another view starts using the sprite, add it to
        // the referenced-file list above (or scan Views/** instead).
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
