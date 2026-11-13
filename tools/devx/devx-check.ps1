# devx-check.ps1 — the IMPROVE-DEVX lane's gate (D7)
#
# Run:  pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/devx/devx-check.ps1
# Exit: 0 when every rule passes (right now, zero rules), non-zero otherwise.
#       Intended to be run from the repo root; if the CWD is not the repo
#       root, it walks up looking for Kumunita.slnx.
#
# This is the *check* half of D7 (the report half is devx-report.ps1). It
# reads tools/devx/devx-baseline.json and enforces its rules. DX01 lands the
# skeleton: it reads the baseline, proves the file parses, prints the rule
# count, and exits 0.
#
# Rules land *with the unit that fixes their gap*, never before (D7): the
# first rules are DX04's (ci.yml triggers vs branch-flow.md) and DX08's
# (IClassFixture usages outside the allowlist). Until then a green skeleton
# is the correct state, not a hole.
#
# Style: matches tools/docs/docs-check.ps1 — $ErrorActionPreference = "Stop",
# Set-StrictMode -Version Latest, Find-RepoRoot walking up to Kumunita.slnx,
# [OK]/[FAIL] lines, exit 0/1.

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
$failures = [System.Collections.Generic.List[string]]::new()

function Check([string]$gate, [string]$message, [bool]$cond) {
  if ($cond) { Write-Host "[OK]   $gate : $message" }
  else { Write-Host "[FAIL] $gate : $message" -ForegroundColor Red; $failures.Add("$gate : $message") }
}

# --- the baseline ------------------------------------------------------------
$baselinePath = Join-Path $repo 'tools/devx/devx-baseline.json'
if (-not (Test-Path $baselinePath)) {
  Write-Host "[FAIL] baseline : devx-baseline.json not found at tools/devx/devx-baseline.json" -ForegroundColor Red
  exit 1
}
$baseline = $null
try {
  $baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
  Check "baseline" "devx-baseline.json parses (version $($baseline.version))" $true
} catch {
  Write-Host "[FAIL] baseline : devx-baseline.json failed to parse: $_" -ForegroundColor Red
  exit 1
}

# --- the rules -----------------------------------------------------------------
# D7: each rule lands with the unit that fixes its gap. As of DX01 there are
# none. DX04 adds the first (ci.yml triggers vs branch-flow.md); DX08 the
# second (IClassFixture outside the allowlist). Each is a function appended
# here that reads $baseline and calls Check — no change to this header.
$ruleCount = 0

# --- summary --------------------------------------------------------------------
Write-Host ""
Write-Host "devx-check — IMPROVE-DEVX gate (D7)"
Write-Host "  rules landed : $ruleCount"
if ($ruleCount -eq 0) { Write-Host "  (DX01 skeleton: 0 rules — rules land with their fixing units, D7)" }
if ($failures.Count -eq 0) {
  Write-Host "  result       : PASS (0 rules)"
  exit 0
} else {
  Write-Host "  result       : FAIL ($($failures.Count) failure(s))" -ForegroundColor Red
  foreach ($f in $failures) { Write-Host "    - $f" }
  exit 1
}
