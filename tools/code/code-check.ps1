# code-check.ps1 — the IMPROVE-CODE lane's CI gate (C00, plan-improve-code.md)
#
# Run:  pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File code-check.ps1
# Exit: 0 when the code-metrics baseline passes, non-zero otherwise.
#
# It is the gate half of the ratchet (plan-improve-code.md §D1): the baseline
# in tools/code/code-baseline.json only goes DOWN. A new item over its ceiling,
# a baselined item growing past its max, a stale entry that was forgotten, or a
# max raised above origin/main without an `adr:` reason each fail the close.
#
# Runs the CodeMetrics tool in --check mode against the baseline. Intended to
# be run from the repo root; if the CWD is not the repo root, it walks up
# looking for Kumunita.slnx (the same convention improve-check.ps1 uses).
# Self-contained (no here-strings, no inter-command $variables — it runs in one
# process and delegates the measurement to the .NET tool).

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# --- locate repo root (walk up from the CWD, or fall back to the script's
# --- own location, which lives under tools/code/) -----
function Find-RepoRoot {
  $dir = (Get-Location).Path
  while ($dir -and $dir.Length -gt 0) {
    if (Test-Path (Join-Path $dir 'Kumunita.slnx')) { return $dir }
    $parent = Split-Path $dir -Parent
    if (-not $parent -or $parent -eq $dir) { break }
    $dir = $parent
  }
  # fallback: the script itself lives at tools/code/...
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
$baseline = Join-Path $repo 'tools/code/code-baseline.json'
$tool = Join-Path $repo 'tools/code/CodeMetrics'

if (-not (Test-Path $baseline)) { throw "code-baseline.json not found at $baseline (generate it with `--tighten` first)" }
if (-not (Test-Path $tool)) { throw "CodeMetrics project not found at $tool" }

# Run the tool in --check mode. Its exit code IS the gate's exit code.
# (We do not redirect through a variable here — the tool prints its own
# violations and exits 0/1; we only surface its code to the caller.)
dotnet run --project $tool -- --check $baseline $repo
exit $LASTEXITCODE
