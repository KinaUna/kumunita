# code-report.ps1 — the human-facing IMPROVE-CODE report (C00)
#
# Run:  pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File code-report.ps1
# Exit: always 0 (informational — the GATE is code-check.ps1, which exits 0/1).
#
# Prints top-N red/amber/green for each metric category, the comment-ratio
# summary (files where comment_lines > code_lines), the clone count, the
# boundary/controller/Marten summary, and the seam-test counts (report-only
# in C00 — it becomes a gate in C12). It never fails a close on its own.
#
# Self-contained (no here-strings, no inter-command $variables). Delegates the
# measurement to the CodeMetrics tool in --report mode and surfaces its output.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Find-RepoRoot {
  $dir = (Get-Location).Path
  while ($dir -and $dir.Length -gt 0) {
    if (Test-Path (Join-Path $dir 'Kumunita.slnx')) { return $dir }
    $parent = Split-Path $dir -Parent
    if (-not $parent -or $parent -eq $dir) { break }
    $dir = $parent
  }
  $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
  for ($i = 0; $i -lt 5 -and $scriptDir; $i++) {
    if (Test-Path (Join-Path $scriptDir 'Kumunita.slnx')) { return $scriptDir }
    $parent = Split-Path $scriptDir -Parent
    if (-not $parent -or $parent -eq $scriptDir) { break }
    $scriptDir = $parent
  }
  throw "could not find repo root (Kumunita.slnx) walking up from $(Get-Location)"
}

$repo = Find-RepoRoot
$tool = Join-Path $repo 'tools/code/CodeMetrics'
if (-not (Test-Path $tool)) { throw "CodeMetrics project not found at $tool" }

Write-Host "IMPROVE-CODE code report (report-only; the gate is code-check.ps1)"
Write-Host "repo root: $repo"
Write-Host ""
# --report is informational: its exit code is not the gate's, so swallow a
# non-zero (e.g. a missing tool) into a message rather than a red close.
dotnet run --project $tool -- --report $repo
if ($LASTEXITCODE -ne 0) {
  Write-Host "code-report: the report tool exited non-zero (no gate impact)."
}
exit 0
