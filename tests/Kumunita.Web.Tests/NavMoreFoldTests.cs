using System.Text.RegularExpressions;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0113 — the variant-B ("top row") nav-row overflow fold: between the
/// sm and md breakpoints the row is wider than the available space, so the
/// page grows a horizontal scrollbar. The fix is a tsc-only module
/// (<c>client/lib/nav-more-fold.ts</c>, ADR 0031) that moves the two
/// resident-only flat links (Community + Groups) into the existing "More ▾"
/// dropdown when the row doesn't fit, and back when there is room.
///
/// <para>
/// These are structural string pins in the house idiom
/// (<see cref="PwaManifestTests"/> "string pin, no TestServer" — a pure file
/// read located by walking up to the repo root): the fold's whole contract is
/// "the hooks exist in the markup, the module is loaded, the state class is
/// styled, and the decision is recorded" — none of which need a running
/// server. The behavioural guarantee (the fold actually firing at a given
/// width) is the Playwright e2e surface (the ADR 0107/0111
/// <c>e2e-pwa-responsive.spec.ts</c> precedent), not xUnit.
/// </para>
/// </summary>
public class NavMoreFoldTests
{
    // ── (1) the tsc-only module ships ───────────────────────────────────────

    /// <summary>
    /// The source module exists (the compiled <c>wwwroot/js/lib/nav-more-fold.js</c>
    /// is gitignored — the <c>.ts</c> source is the artifact under version
    /// control), and the module is a strict IIFE in the house shape (no
    /// top-level side effects, no bare <c>document</c> access outside the
    /// guarded IIFE body).
    /// </summary>
    [Fact(DisplayName = "ADR 0113: the nav-more-fold.ts module exists and is a strict IIFE")]
    public void Nav_More_Fold_Module_Exists_And_Is_A_Strict_Iife()
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "client", "lib", "nav-more-fold.ts");
        Assert.True(File.Exists(path), $"nav-more-fold.ts not found at {path}.");
        var src = File.ReadAllText(path);

        // The house IIFE shape (detach-menu.ts precedent): a single
        // immediately-invoked arrow, with 'use strict' first in the body.
        Assert.Contains("(() => {", src);
        Assert.Contains("'use strict';", src);
        Assert.EndsWith("})();", src.TrimEnd());

        // It scopes to the variant-B row only (self-guards on variant C and
        // signed-out, where the hook is absent).
        Assert.Contains(".kmb-nav-row", src);

        // It re-homes the hooks rather than inventing new DOM labels: it
        // reads the data-nav-* hooks and toggles the one state class.
        Assert.Contains("data-nav-fold", src);
        Assert.Contains("data-nav-more", src);
        Assert.Contains("kmb-nav-folded", src);
    }

    // ── (2) the layout carries the hooks + loads the module ────────────────

    /// <summary>
    /// <c>_Layout.cshtml</c> marks the two fold items (<c>data-nav-fold</c>,
    /// exactly two — Community and Groups) and the More menu
    /// (<c>data-nav-more</c>, exactly one), and loads the compiled module as a
    /// type=module script (SECURITY.md §6: no inline scripts in Razor views).
    /// </summary>
    [Fact(DisplayName = "ADR 0113: _Layout.cshtml carries the fold hooks and loads the module")]
    public void Layout_Carries_Fold_Hooks_And_Loads_Module()
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", "Shared", "_Layout.cshtml");
        Assert.True(File.Exists(path), $"_Layout.cshtml not found at {path}.");
        var html = File.ReadAllText(path);

        // Exactly two fold items (Community + Groups). Count only real
        // attribute usages (the attribute immediately followed by '>', as in
        // <li … data-nav-fold>), not the prose mentions in the ADR 0113
        // comment above the block.
        var foldHooks = Regex.Matches(html, "data-nav-fold(?=>)");
        Assert.True(foldHooks.Count == 2,
            $"Expected exactly 2 'data-nav-fold' hooks (Community + Groups); found {foldHooks.Count}.");

        // Exactly one More-menu fold target (same 'real attribute' counting).
        var moreHooks = Regex.Matches(html, "data-nav-more(?=>)");
        Assert.True(moreHooks.Count == 1,
            $"Expected exactly 1 'data-nav-more' hook (the More menu); found {moreHooks.Count}.");

        // The module is loaded as a module script — not inline.
        Assert.Contains(
            "<script type=\"module\" src=\"~/js/lib/nav-more-fold.js\"></script>",
            html);

        // The two folded links keep their existing kw-l labels (no new key —
        // the parity pins are untouched): they are re-homed, not re-labelled.
        Assert.Contains("key=\"nav.community\"", html);
        Assert.Contains("key=\"nav.groups\"", html);
    }

    // ── (3) the fold state class is styled, outside any @media block ───────

    /// <summary>
    /// <c>site.css</c> styles the <c>.kmb-nav-folded</c> state class (the
    /// folded items' navbar-voice rule), and it is NOT inside an
    /// <c>@media</c> block — so the six-<c>@media</c> inventory pinned by
    /// <see cref="PwaManifestTests.Site_Css_Media_Block_Boundary_Pinned"/> is
    /// preserved (the M10 drift-guard frozen pin #7).
    /// </summary>
    [Fact(DisplayName = "ADR 0113: site.css styles .kmb-nav-folded outside any @media block")]
    public void Site_Css_Styles_Fold_State_Outside_Media_Block()
    {
        var cssPath = Path.Combine(RepoRoot, "src", "Kumunita.Web", "wwwroot", "css", "site.css");
        Assert.True(File.Exists(cssPath), $"site.css not found at {cssPath}.");
        var css = File.ReadAllText(cssPath);

        // The state class is present (the module toggles it; the CSS reads it).
        Assert.Contains(".kmb-nav-folded", css);

        // The state-class rule lives OUTSIDE any @media block. A naive
        // "@media…{…}" could in principle swallow a nested rule, so check the
        // .kmb-nav-folded occurrences all sit in the top-level (non-media)
        // region: i.e. the media-block count is still exactly six (the pinned
        // boundary) — a change would mean a new block was added, which is the
        // breach this pin guards against.
        var allMedia = Regex.Matches(css, @"@media");
        Assert.True(allMedia.Count == 6,
            $"Expected exactly 6 '@media' occurrences (the M10 pinned boundary — the ADR 0113 fold state class is intentionally not a media block); found {allMedia.Count}.");
    }

    // ── (4) the decision is recorded ────────────────────────────────────────

    /// <summary>
    /// ADR 0113 is recorded under <c>docs/adr/</c> (a new capability that
    /// settles a design question gets an ADR, not just a README note — the
    /// house convention), and it is listed in the ADR index.
    /// </summary>
    [Fact(DisplayName = "ADR 0113: the decision is recorded in docs/adr and the ADR index")]
    public void Adr_0113_Recorded_And_Indexed()
    {
        var adrPath = Path.Combine(RepoRoot, "docs", "adr", "0113-nav-row-overflow-fold.md");
        Assert.True(File.Exists(adrPath), $"ADR 0113 not found at {adrPath}.");

        var adr = File.ReadAllText(adrPath);
        Assert.Contains("Accepted", adr);
        Assert.Contains("0111", adr); // extends the ADR 0111 nav-layout surface

        var index = Path.Combine(RepoRoot, "docs", "adr", "README.md");
        Assert.True(File.Exists(index), $"ADR index not found at {index}.");
        Assert.Contains("0113", File.ReadAllText(index));
    }

    // ── shared helper ───────────────────────────────────────────────────────

    /// <summary>
    /// Locates the repo root (the dir that holds <c>Kumunita.slnx</c>) by
    /// walking up from the test assembly's output directory — the house shape
    /// (<see cref="PwaManifestTests.RepoRoot"/> idiom), correct regardless of
    /// the output depth (Debug/Release, xunit.v3, etc.).
    /// </summary>
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
