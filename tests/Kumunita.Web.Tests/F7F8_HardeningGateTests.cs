using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// F7 (WEB-07) + F8 (WEB-10) — the two prod-hardening rows the 2026-10-22
/// audit flagged and the lane's first pass did not close (verified 2026-10-25):
/// the global <c>X-Content-Type-Options: nosniff</c> + <c>Referrer-Policy</c>
/// headers, and the per-IP rate-limit on the four upload lanes.
///
/// The house "string pin, no TestServer" idiom (the
/// <see cref="BookmarkButtonTests.BmButton_Partial_Renders_Correct_Lane_For_State"/>
/// precedent): read the source, assert the contract. These are pipeline-level
/// seams (middleware order, per-endpoint attribute resolution) that a
/// direct-construction harness cannot exercise end-to-end; the rendered-header
/// evidence is the live-server curl / integrated-browser snapshot per the
/// Razor-verification doctrine (AGENTS.md), recorded as an owner-ops step.
/// </summary>
public class F7F8_HardeningGateTests
{
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

    [Fact(DisplayName = "F7 (WEB-07): the CSP middleware block sets the two remaining global headers")]
    public void F7_GlobalNosniffAndReferrerPolicy_In_CspMiddlewareBlock()
    {
        var program = Read("src/Kumunita.Web/Program.cs");

        // The single global-header middleware is the CSP block
        // (app.Use(async (context, next) => { … }) that sets
        // Content-Security-Policy). F7's contract: the two headers the audit
        // flagged are set in that same block, so every response carries them.
        var block = Regex.Match(
            program,
            "app\\.Use\\(async \\(context, next\\).*?await next\\(\\);\\s*\\}\\);",
            RegexOptions.Singleline);
        Assert.True(block.Success, "The CSP app.Use(…) block (Content-Security-Policy) was not found in Program.cs.");
        var b = block.Value;

        Assert.Contains("Content-Security-Policy", b);
        Assert.Contains("X-Content-Type-Options", b);
        Assert.Contains("nosniff", b);
        Assert.Contains("Referrer-Policy", b);
    }

    [Fact(DisplayName = "F8 (WEB-10): the 'upload' rate-limit policy is defined AND attached to all four upload lanes")]
    public void F8_UploadLane_IsRateLimited()
    {
        var program = Read("src/Kumunita.Web/Program.cs");
        Assert.True(
            Regex.IsMatch(program, "AddWindow\\(opts, \\\"upload\\\""),
            "The 'upload' fixed-window policy is not defined in the AddRateLimiter block.");

        // The four upload POST lanes — each must carry [EnableRateLimiting("upload")].
        string ReadCtl(string name) => Read($"src/Kumunita.Web/Controllers/{name}");

        var attachment = ReadCtl("AttachmentController.cs");
        Assert.Matches(
            @"\[HttpPost\(""/attachment""\)\]\s*\[Microsoft\.AspNetCore\.Authorization\.Authorize\]\s*\[EnableRateLimiting\(""upload""\)\]",
            attachment);

        var contentImage = ReadCtl("ContentImageController.cs");
        Assert.Matches(
            @"\[HttpPost\(""/content-image""\)\]\s*\[Microsoft\.AspNetCore\.Authorization\.Authorize\]\s*\[EnableRateLimiting\(""upload""\)\]",
            contentImage);

        var document = ReadCtl("DocumentController.cs");
        Assert.Matches(
            @"\[HttpPost\(""/documents""\)\]\s*\[Authorize\]\s*\[EnableRateLimiting\(""upload""\)\]",
            document);

        var profile = ReadCtl("ProfileController.cs");
        Assert.Matches(
            @"\[HttpPost\(""/profile/avatar""\)\]\s*\[EnableRateLimiting\(""upload""\)\]",
            profile);
    }
}
