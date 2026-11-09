using System.IO;

namespace Kumunita.Web.Tests.SecGate;

/// <summary>
/// IS04 (IMPROVE-SEC) — the shared source-reader helper for the <c>SecGate</c>
/// tests. The house "string pin, no TestServer" idiom (the
/// <see cref="F7F8_HardeningGateTests"/> precedent): locate the repo root by
/// walking up from the test's <see cref="AppContext.BaseDirectory"/>, then
/// read/assert against the source tree. Reused by IS15, IS24, IS38b and IS40.
/// </summary>
public static class SecGateSource
{
    /// <summary>
    /// The repository root — the nearest ancestor directory of
    /// <see cref="AppContext.BaseDirectory"/> that contains
    /// <c>Kumunita.slnx</c> (the same walk-up idiom as
    /// <see cref="F7F8_HardeningGateTests.RepoRoot"/>).
    /// </summary>
    public static string RepoRoot
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

    /// <summary>Reads a UTF-8 source file by repo-relative path.</summary>
    public static string ReadSrc(string relativePath)
        => File.ReadAllText(Path.Combine(RepoRoot, relativePath));

    /// <summary>
    /// Enumerates absolute file paths under the repo root matching the
    /// file-name <paramref name="pattern"/> (e.g. <c>*.cs</c>), recursing all
    /// subdirectories. (The <c>**</c> glob form is not supported by
    /// <see cref="Directory.EnumerateFiles"/>; recursion comes from
    /// <see cref="EnumerationOptions.RecurseSubdirectories"/>.)
    /// </summary>
    public static string[] AllSrcFiles(string pattern)
        => Directory.EnumerateFiles(RepoRoot, pattern, new EnumerationOptions
        {
            RecurseSubdirectories = true,
        }).ToArray();

    /// <summary>
    /// Whether a test method named <paramref name="testName"/> exists in any
    /// <c>.cs</c> file under <c>tests/</c>. A full C# parse is overkill for a
    /// gate: the named invariant tests use unique method names, so a
    /// substring scan is the pin (per the IS04 unit spec).
    /// </summary>
    public static bool TestMethodExists(string testName)
        => AllSrcFiles("*.cs")
            .Where(p => p.Replace('\\', '/').Contains("/tests/"))
            .Any(p => ReadSrc(p).Contains(testName));
}
