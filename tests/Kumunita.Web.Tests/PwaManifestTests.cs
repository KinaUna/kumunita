using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kumunita.Core.Localization;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M10 U01 (ADR 0107) — the PWA static-asset pins, the ADR 0043 SP-U04
/// "string pin, no TestServer" idiom (the house shape is
/// <see cref="StaticPagesSP_U04Tests.ResolveLayout"/> — a pure file read
/// located by walking up to the repo root, the dir that holds
/// <c>Kumunita.slnx</c>, so the pin is correct regardless of the test
/// output depth).
///
/// <para>
/// U01 ships the three U01 pins. U02 extends this same class with the two
/// service-worker pins (<c>ServiceWorker_File_Exists_And_Is_SameOrigin_
/// Wwwroot</c> + <c>ServiceWorker_Allowlist_Matches_Design_Doc_Verbatim</c>)
/// and U06 with the remaining two (<c>Pwa_Install_Kw_L_Key_Registered_In_All_
/// Four_Languages</c> + <c>Site_Css_Media_Block_Boundary_Pinned</c>) — the
/// class shape is additive-friendly (private <see cref="RepoRoot"/> helper,
/// one <c>[Fact]</c> per pinned name, nothing else to refactor).
/// </para>
/// </summary>
public class PwaManifestTests
{
    // ── (1) C-M10·1 — the manifest is honest + complete ────────────────────

    /// <summary>
    /// <c>wwwroot/manifest.webmanifest</c> parses as JSON and carries the
    /// C-M10·1 closed field set with the D3 locked values (the design doc
    /// §manifest's exact text — the two hexes are the <c>site.css</c>
    /// <c>:root</c> tokens, verified U00: <c>--bs-body-bg: #fbfaf7</c>
    /// line 78, <c>--bs-primary: #1c4532</c> line 106).
    /// </summary>
    [Fact(DisplayName = "M10 U01: manifest parses as JSON and carries the C-M10·1 locked field set")]
    public void Manifest_Json_Parses_And_Has_Required_Fields()
    {
        var manifestPath = Path.Combine(RepoRoot, "src", "Kumunita.Web", "wwwroot", "manifest.webmanifest");
        Assert.True(File.Exists(manifestPath), $"manifest.webmanifest not found at {manifestPath}.");

        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = doc.RootElement;

        // The required scalar fields, exact values (the D3 locked text).
        Assert.Equal("Kumunita", root.GetProperty("name").GetString());
        Assert.Equal("Kumunita", root.GetProperty("short_name").GetString());
        Assert.Equal("/", root.GetProperty("start_url").GetString());
        Assert.Equal("/", root.GetProperty("id").GetString());
        Assert.Equal("/", root.GetProperty("scope").GetString());
        Assert.Equal("standalone", root.GetProperty("display").GetString());
        Assert.Equal("#fbfaf7", root.GetProperty("background_color").GetString());
        Assert.Equal("#1c4532", root.GetProperty("theme_color").GetString());

        // The icon pair — the exact closed set (D3: no shortcuts, no
        // screenshots, no categories; the 512 also carries maskable).
        var icons = root.GetProperty("icons").EnumerateArray().ToList();
        Assert.Equal(2, icons.Count);

        Assert.Equal("/images/pwa/icon-192.png", icons[0].GetProperty("src").GetString());
        Assert.Equal("192x192", icons[0].GetProperty("sizes").GetString());
        Assert.Equal("image/png", icons[0].GetProperty("type").GetString());
        Assert.Equal("any", icons[0].GetProperty("purpose").GetString());

        Assert.Equal("/images/pwa/icon-512.png", icons[1].GetProperty("src").GetString());
        Assert.Equal("512x512", icons[1].GetProperty("sizes").GetString());
        Assert.Equal("image/png", icons[1].GetProperty("type").GetString());
        Assert.Equal("any maskable", icons[1].GetProperty("purpose").GetString());

        // The closed set: nothing beyond the required fields + icons (D3's
        // honesty pin — no orientation / lang / dir / shortcuts / screenshots).
        var allowed = new HashSet<string>
        {
            "name", "short_name", "start_url", "id", "scope", "display",
            "background_color", "theme_color", "icons",
        };
        foreach (var property in root.EnumerateObject())
            Assert.True(allowed.Contains(property.Name),
                $"manifest field '{property.Name}' is not in the D3 closed set");
    }

    // ── (2) D4 — the two icon PNGs exist at the manifest's exact paths ─────

    /// <summary>
    /// Both committed icon PNGs exist at the exact <c>icons[].src</c> paths
    /// the manifest declares, and each carries the correct IHDR dimensions
    /// (bytes 16–24 — no image-decode dependency; the same check the U06
    /// Playwright spec asserts over the HTTP response).
    /// </summary>
    [Fact(DisplayName = "M10 U01: icon-192.png + icon-512.png exist at the manifest paths with the right IHDR dimensions")]
    public void Manifest_Icon_192_And_512_Exist_In_Repo()
    {
        var expected = new (string relPath, int width, int height)[]
        {
            ("src/Kumunita.Web/wwwroot/images/pwa/icon-192.png", 192, 192),
            ("src/Kumunita.Web/wwwroot/images/pwa/icon-512.png", 512, 512),
        };

        foreach (var (relPath, width, height) in expected)
        {
            var full = Path.Combine(RepoRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(full), $"icon not found at {full}.");

            var bytes = File.ReadAllBytes(full);
            // The PNG signature + the IHDR chunk: signature(8) + len(4) +
            // "IHDR"(4) + width(4) + height(4) — width at byte 16, height
            // at byte 20 (the unit plan's "bytes 16–24" = width + height).
            // IHDR integers are big-endian (network byte order), so read
            // them with BinaryPrimitives (BitConverter is platform-order).
            Assert.Equal(0x89, bytes[0]);
            Assert.Equal((byte)'P', bytes[1]);
            Assert.Equal((byte)'N', bytes[2]);
            Assert.Equal((byte)'G', bytes[3]);
            Assert.Equal((byte)'I', bytes[12]);
            Assert.Equal((byte)'H', bytes[13]);
            Assert.Equal((byte)'D', bytes[14]);
            Assert.Equal((byte)'R', bytes[15]);
            Assert.Equal(width, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)));
            Assert.Equal(height, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20)));
        }
    }

    // ── (3) the contract-1 witness — the _Layout <head> links it ───────────

    /// <summary>
    /// The <c>&lt;link rel="manifest"&gt;</c> is in
    /// <c>Views/Shared/_Layout.cshtml</c> <c>&lt;head&gt;</c>, pointing at
    /// <c>manifest.webmanifest</c> (the design doc seam contract 1 — the
    /// ADR 0043 SP-U04 string-pin idiom, the same house shape as
    /// <see cref="StaticPagesSP_U04Tests.ResolveLayout"/>).
    /// </summary>
    [Fact(DisplayName = "M10 U01: _Layout.cshtml <head> carries the <link rel=\"manifest\"> for manifest.webmanifest")]
    public void Layout_Contains_Manifest_Link()
    {
        var layoutPath = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", "Shared", "_Layout.cshtml");
        Assert.True(File.Exists(layoutPath), $"_Layout.cshtml not found at {layoutPath}.");
        var layout = File.ReadAllText(layoutPath);

        Assert.True(layout.Contains("<link rel=\"manifest\""),
            "_Layout.cshtml must carry the <link rel=\"manifest\"> in <head>");
        Assert.True(layout.Contains("manifest.webmanifest"),
            "the manifest link must point at manifest.webmanifest");
    }

    // ── (4) C-M10·3 — the SW file is a same-origin wwwroot/ text asset ──────

    /// <summary>
    /// <c>wwwroot/sw.js</c> exists and is a **text JS file** (its first
    /// non-whitespace byte is a printable ASCII char, not a binary marker) —
    /// the C-M10·3 location witness: a same-origin <c>wwwroot/</c> asset that
    /// satisfies <c>script-src 'self'</c> (the ADR 0043 SP-U04 string-pin
    /// idiom; no TestServer, no browser).
    /// </summary>
    [Fact(DisplayName = "M10 U02: wwwroot/sw.js exists and is a text JS file (C-M10·3 location witness)")]
    public void ServiceWorker_File_Exists_And_Is_SameOrigin_Wwwroot()
    {
        var swPath = Path.Combine(RepoRoot, "src", "Kumunita.Web", "wwwroot", "sw.js");
        Assert.True(File.Exists(swPath), $"sw.js not found at {swPath}.");

        var bytes = File.ReadAllBytes(swPath);
        Assert.True(bytes.Length > 0, "sw.js is empty.");

        // First non-whitespace byte: a text JS file starts with a printable
        // ASCII token (a comment `/`, `*`, or an identifier lead). A binary
        // (PNG magic 0x89, a zip 0x50 'PK', etc.) would fail this. This is
        // the "not a binary" witness the unit plan names.
        int i = 0;
        while (i < bytes.Length && (bytes[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
            i++;
        Assert.True(i < bytes.Length, "sw.js is all whitespace.");
        byte first = bytes[i];
        Assert.True(
            (first >= 0x21 && first < 0x7f),
            $"first non-whitespace byte 0x{first:X2} is not a printable ASCII char — not a text JS file.");
    }

    // ── (5) C-M10·2 — the SW allowlist set-equals the design doc §SW ────────

    /// <summary>
    /// The <c>ALLOWLIST</c> array in <c>sw.js</c> **set-equals** the closed
    /// 15-path allowlist in the design doc §SW (the C-M10·2 closed-set
    /// witness — the structural half of the negative pin; the *behavioral*
    /// half is U06's Playwright spec). Both are parsed straight from the
    /// committed text (a closed-set witness, not a behavior pin).
    /// </summary>
    [Fact(DisplayName = "M10 U02: sw.js ALLOWLIST set-equals the design doc §SW closed allowlist")]
    public void ServiceWorker_Allowlist_Matches_Design_Doc_Verbatim()
    {
        var swPath = Path.Combine(RepoRoot, "src", "Kumunita.Web", "wwwroot", "sw.js");
        var docPath = Path.Combine(RepoRoot, "docs", "design", "m10-pwa-responsive-design.md");
        Assert.True(File.Exists(swPath), $"sw.js not found at {swPath}.");
        Assert.True(File.Exists(docPath), $"design doc not found at {docPath}.");

        var jsPaths = ExtractAllowlistFromSw(File.ReadAllText(swPath));
        var docPaths = ExtractAllowlistFromDesignDoc(File.ReadAllText(docPath));

        Assert.True(jsPaths.Count > 0, "no paths extracted from sw.js ALLOWLIST.");
        Assert.True(docPaths.Count > 0, "no paths extracted from the design doc §SW allowlist.");

        // Closed set: exactly 15, and the two sets are equal (order-
        // independent). (xUnit v3: Assert.Equal no longer takes a message
        // string — use the Assert.True(condition, message) shape.)
        Assert.True(jsPaths.Distinct().Count() == 15,
            $"sw.js ALLOWLIST is not 15 distinct paths: [{string.Join(", ", jsPaths)}]");
        Assert.True(docPaths.Distinct().Count() == 15,
            $"design doc allowlist is not 15 distinct paths: [{string.Join(", ", docPaths)}]");

        Assert.True(new HashSet<string>(docPaths).SetEquals(jsPaths),
            "the sw.js ALLOWLIST does not set-equal the design doc §SW allowlist.");
    }

    /// <summary>
    /// Extracts the quoted strings from the <c>const ALLOWLIST = [ ... ];</c>
    /// array literal in <c>sw.js</c> (the design doc §SW's locked code shape).
    /// </summary>
    private static List<string> ExtractAllowlistFromSw(string sw)
    {
        const string marker = "const ALLOWLIST = [";
        int start = sw.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "the `const ALLOWLIST = [` array literal is not present in sw.js.");
        int blockStart = start + marker.Length;
        int blockEnd = sw.IndexOf("];", blockStart, StringComparison.Ordinal);
        Assert.True(blockEnd > blockStart, "the ALLOWLIST closing `];` is not present in sw.js.");
        string block = sw.Substring(blockStart, blockEnd - blockStart);

        var re = new Regex("'([^']*)'");
        var paths = re.Matches(block).Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        return paths;
    }

    /// <summary>
    /// Extracts the closed allowlist code block that follows the
    /// "The closed allowlist" heading in the design doc — the lines that
    /// start with <c>/</c> between the first opening and the next closing
    /// <c>```</c> fence.
    /// </summary>
    private static List<string> ExtractAllowlistFromDesignDoc(string doc)
    {
        const string heading = "The closed allowlist";
        int headIdx = doc.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(headIdx >= 0, "the `The closed allowlist` heading is not present in the design doc.");

        int fenceStart = doc.IndexOf("```", headIdx, StringComparison.Ordinal);
        Assert.True(fenceStart >= headIdx, "no code fence follows the allowlist heading in the design doc.");
        // Skip to the end of the opening-fence line (a bare ``` or ```lang).
        int lineEnd = doc.IndexOf('\n', fenceStart);
        int bodyStart = lineEnd < 0 ? doc.Length : lineEnd + 1;

        int fenceEnd = doc.IndexOf("```", bodyStart, StringComparison.Ordinal);
        Assert.True(fenceEnd >= bodyStart, "no closing code fence for the allowlist block in the design doc.");

        string body = doc.Substring(bodyStart, fenceEnd - bodyStart);
        var paths = body
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("/"))
            .ToList();
        return paths;
    }

    // ── U06 pins (D10 + D6) ────────────────────────────────────────────────

    /// <summary>
    /// U06 / D10 — the <c>pwa.install</c> key is registered in all four
    /// languages with the design doc §install's locked strings (en / de /
    /// fr / da). The U03 install module (<c>wwwroot/js/pwa-install.ts</c>)
    /// reads exactly this key to label the "Install app" button (the D10
    /// single-closed-key contract); a missing or renamed key would leave
    /// the button blank or in the wrong language. This is the xUnit
    /// witness for the key's registration; the Playwright spec's
    /// behavioral witness (the rendered button text in Chromium) is the
    /// author-not-run one (the M3 U13 / M4 U11 / TG U9 precedent, the
    /// <c>kumunita</c> fixture's documented throw).
    /// </summary>
    [Fact(DisplayName = "M10 U06: pwa.install key registered in all four languages (D10)")]
    public void Pwa_Install_Kw_L_Key_Registered_In_All_Four_Languages()
    {
        const string key = "pwa.install";

        // The design doc §install's locked strings (D10).
        Assert.True(KnownTranslationKeys.EnValues.ContainsKey(key),
            $"en: key '{key}' is not in KnownTranslationKeys.EnValues — the D10 single-closed-key contract requires it.");
        Assert.Equal("Install app", KnownTranslationKeys.EnValues[key]);

        Assert.True(KnownTranslationKeys.DeValues.ContainsKey(key),
            $"de: key '{key}' is not in KnownTranslationKeys.DeValues — the D10 single-closed-key contract requires it.");
        Assert.Equal("App installieren", KnownTranslationKeys.DeValues[key]);

        Assert.True(KnownTranslationKeys.FrValues.ContainsKey(key),
            $"fr: key '{key}' is not in KnownTranslationKeys.FrValues — the D10 single-closed-key contract requires it.");
        Assert.Equal("Installer l'application", KnownTranslationKeys.FrValues[key]);

        Assert.True(KnownTranslationKeys.DaValues.ContainsKey(key),
            $"da: key '{key}' is not in KnownTranslationKeys.DaValues — the D10 single-closed-key contract requires it.");
        Assert.Equal("Installér app", KnownTranslationKeys.DaValues[key]);
    }

    /// <summary>
    /// U06 / D6 — the <c>site.css</c> media-block boundary is pinned:
    /// exactly <b>four</b> <c>@media (max-width: 767.98px)</c> blocks +
    /// <b>one</b> <c>@media (max-width: 575.98px)</c> block (the ADR 0111
    /// collapsed-mobile-navbar block — Bootstrap's navbar-expand-sm
    /// breakpoint, narrower than the app's 767.98px block) + <b>one</b>
    /// <c>@media (prefers-reduced-motion: reduce)</c> block, for a total of
    /// <b>six</b> <c>@media</c> occurrences. The 575.98px block is a recorded
    /// drift-guard entry (ADR 0111), so it is expected here. Any OTHER count
    /// change without a recorded entry is a breach (the design doc §drift-guard
    /// frozen pin #7).
    /// </summary>
    [Fact(DisplayName = "M10 U06: site.css media-block boundary pinned (D6)")]
    public void Site_Css_Media_Block_Boundary_Pinned()
    {
        var cssPath = Path.Combine(RepoRoot, "src", "Kumunita.Web", "wwwroot", "css", "site.css");
        Assert.True(File.Exists(cssPath), $"site.css not found at {cssPath}.");
        var css = File.ReadAllText(cssPath);

        // The four 767.98px width blocks (the U00–U05 baseline — the U00 drift
        // note (a) corrected the register's "six" to four; this pin holds four).
        var widthBlocks = Regex.Matches(css, @"@media\s*\(max-width:\s*767\.98px\)");
        Assert.True(widthBlocks.Count == 4,
            $"Expected exactly 4 '@media (max-width: 767.98px)' blocks (the U00–U05 baseline); found {widthBlocks.Count}.");

        // The one collapsed-mobile navbar block (ADR 0111 — the 575.98px
        // breakpoint where navbar-expand-sm collapses; a recorded drift entry).
        var collapsedBlocks = Regex.Matches(css, @"@media\s*\(max-width:\s*575\.98px\)");
        Assert.True(collapsedBlocks.Count == 1,
            $"Expected exactly 1 '@media (max-width: 575.98px)' block (the ADR 0111 collapsed navbar); found {collapsedBlocks.Count}.");

        // The one reduced-motion block.
        var motionBlocks = Regex.Matches(css, @"@media\s*\(prefers-reduced-motion:\s*reduce\)");
        Assert.True(motionBlocks.Count == 1,
            $"Expected exactly 1 '@media (prefers-reduced-motion: reduce)' block; found {motionBlocks.Count}.");

        // The total @media count is six (four + one collapsed + one motion) —
        // the "no new boundary without a record" witness (the design doc
        // §drift-guard frozen pin #7: "a count change without a recorded entry
        // is a breach").
        var allMedia = Regex.Matches(css, @"@media");
        Assert.True(allMedia.Count == 6,
            $"Expected exactly 6 '@media' occurrences total (4 width + 1 collapsed + 1 reduced-motion); found {allMedia.Count}.");
    }

    // ── shared helper ───────────────────────────────────────────────────────

    /// <summary>
    /// Locates the repo root (the dir that holds <c>Kumunita.slnx</c>) by
    /// walking up from the test assembly's output directory — the house
    /// shape (<see cref="StaticPagesSP_U04Tests.ResolveLayout"/> /
    /// <c>KwLRegistryConsistencyTests.ResolveViewsDir</c>), correct
    /// regardless of the output depth (Debug/Release, xunit.v3, etc.).
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
