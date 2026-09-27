using System.Buffers.Binary;
using System.Text.Json;
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
