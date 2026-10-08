using System.Diagnostics;

namespace Kumunita.Web.Tests;

/// <summary>
/// The IMPROVE lane's U00 harness test (plan-improve.md): a single xunit.v3
/// test that shells out to <c>improve-check.ps1</c> and asserts exit code 0.
/// The gate is the CI-able close: a new god-file, new ADR-index drift, new
/// doc duplication, or new over-600 handoff note without a TL;DR each fail
/// the close. The gate *allows* the U00 baseline (it prevents growth, not an
/// immediate fix) — see the six checks + the baseline grandfathering lists
/// in the script itself.
///
/// Fast (shell-out, not Testcontainers), deterministic (reads the repo, not
/// the network). Skips on a machine without <c>pwsh</c> (the CI box this
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

        var script = Path.Combine(RepoRoot(), "docs", "plans-milestones", "improve", "improve-check.ps1");
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
