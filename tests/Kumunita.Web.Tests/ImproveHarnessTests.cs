using System.Diagnostics;

namespace Kumunita.Web.Tests;

/// <summary>
/// The IMPROVE lane's harness tests: xunit.v3 tests that shell out to the
/// close scripts and assert exit code 0.
///
/// <see cref="ImproveCheck_Gate_Passes"/> runs the IMPROVE lane's doc gate
/// (<c>tools/docs/improve-check.ps1</c>, gates b/c/d/g — ADR-index drift,
/// shared-heading drift, over-600 handoff notes, design-doc abstracts).
/// <see cref="CodeCheck_Gate_Passes"/> runs the IMPROVE-CODE lane's ratchet
/// gate (<c>tools/code/code-check.ps1</c>) — the Roslyn-measured code-size
/// baseline covering .cs/.ts/.cshtml (gates a/e/f of the doc script were
/// retired into this finer gate by C00).
///
/// Fast (shell-out, not Testcontainers), deterministic (reads the repo, not
/// the network). Skip on a machine without <c>pwsh</c> (the CI box this
/// repo targets has PowerShell 7; a bare <c>dotnet test</c> on a box
/// without it should not red the build over a missing tool).
/// </summary>
public class ImproveHarnessTests
{
    private static string RepoRoot()
    {
        // The test assembly is built under tests/Kumunita.Web.Tests/bin/...;
        // walk up from the AppContext base until Kumunita.slnx is found.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "could not find the repo root (Kumunita.slnx) walking up from " + AppContext.BaseDirectory);
    }

    [Fact]
    public void ImproveCheck_Gate_Passes()
    {
        if (!PwshAvailable())
        {
            Assert.Skip("pwsh is not on PATH on this machine; the gate script cannot run (CI has PowerShell 7).");
        }

        var script = Path.Combine(RepoRoot(), "tools", "docs", "improve-check.ps1");
        Assert.True(File.Exists(script), "improve-check.ps1 is missing at " + script);

        var psi = new ProcessStartInfo
        {
            FileName = "pwsh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepoRoot(),
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(script);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("failed to start pwsh");
        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        bool finished = proc.WaitForExit(120_000);
        Assert.True(finished, "improve-check.ps1 did not finish within 120 s");

        Assert.True(proc.ExitCode == 0,
            "improve-check.ps1 failed the close (exit " + proc.ExitCode + ").\n"
            + "---- stdout ----\n" + stdout + "\n---- stderr ----\n" + stderr);
    }

    /// <summary>
    /// The IMPROVE-CODE lane's C00 harness test: shells out to
    /// <c>tools/code/code-check.ps1</c> and asserts exit code 0. This is the
    /// Roslyn-measured ratchet gate (the code-size baseline in
    /// <c>tools/code/code-baseline.json</c>, now covering .cs/.ts/.cshtml):
    /// a new item over its ceiling, a baselined item growing past its max, a
    /// stale baseline entry, or a max raised above origin/main without an
    /// <c>adr:</c> reason each fail the close. The <c>dotnet run</c> inside
    /// the wrapper builds the CodeMetrics tool, so the timeout is longer than
    /// the doc gate's.
    /// </summary>
    [Fact]
    public void CodeCheck_Gate_Passes()
    {
        if (!PwshAvailable())
        {
            Assert.Skip("pwsh is not on PATH on this machine; the gate script cannot run (CI has PowerShell 7).");
        }

        var script = Path.Combine(RepoRoot(), "tools", "code", "code-check.ps1");
        Assert.True(File.Exists(script), "code-check.ps1 is missing at " + script);

        var psi = new ProcessStartInfo
        {
            FileName = "pwsh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepoRoot(),
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(script);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("failed to start pwsh");
        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        bool finished = proc.WaitForExit(300_000);
        Assert.True(finished, "code-check.ps1 did not finish within 300 s");

        Assert.True(proc.ExitCode == 0,
            "code-check.ps1 failed the close (exit " + proc.ExitCode + ").\n"
            + "---- stdout ----\n" + stdout + "\n---- stderr ----\n" + stderr);
    }

    private static bool PwshAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("pwsh", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = Process.Start(psi);
            proc?.StandardOutput.ReadToEnd();
            proc?.StandardError.ReadToEnd();
            proc?.WaitForExit(10_000);
            return proc is not null && proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
