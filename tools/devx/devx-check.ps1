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

# --- rule: ci-triggers (DX04) ----------------------------------------------------
# ci.yml's on: branches (push + pull_request) must equal the CI branch declared in
# docs/dev/branch-flow.md (the "Which branch CI watches" section). workflow_dispatch
# carries no branches and is ignored. Fails if either file is unreadable (fail closed).
function Get-CiTriggersBranches([string]$path) {
  $branches = [System.Collections.Generic.List[string]]::new()
  $inOn = $false; $trigger = $null; $expectBranch = $false
  foreach ($raw in (Get-Content -LiteralPath $path)) {
    $line = $raw -replace '\s+$', ''
    if (-not $inOn) { if ($line -match '^on:\s*$') { $inOn = $true } ; continue }
    if ($line -match '^\S') { break }
    if ($line -match '^  (push|pull_request|workflow_dispatch|schedule|merge_group):\s*$') { $trigger = $Matches[1]; $expectBranch = $false; continue }
    if ($line -match '^    branches:\s*$') { $expectBranch = ($trigger -in 'push','pull_request'); continue }
    if ($line -match '^      -\s*(\S+)') { if ($expectBranch) { $branches.Add($Matches[1]) } ; continue }
  }
  return ,$branches
}

$ciPath = Join-Path $repo '.github/workflows/ci.yml'
$bfPath = Join-Path $repo 'docs/dev/branch-flow.md'
$ciBranches = if (Test-Path $ciPath) { Get-CiTriggersBranches $ciPath } else { @() }
$declared = $null
if (Test-Path $bfPath) {
  $bf = Get-Content -LiteralPath $bfPath -Raw
  if ($bf -match '(?ms)^##\s+Which branch CI watches\s*\n(.*?)(?=^##\s|\Z)') {
    if ($Matches[1] -match 'CI runs on `([A-Za-z0-9_]+)`') { $declared = $Matches[1] }
  }
}
$ciSet = @($ciBranches | Sort-Object -Unique)
$triggersMatch = ($null -ne $declared) -and ($ciSet.Count -eq 1) -and ($ciSet[0] -eq $declared)
Check "ci-triggers" "ci.yml on: branches ($($ciSet -join ', ')) match branch-flow.md's declared CI branch ($declared)" $triggersMatch
$ruleCount = 1

# --- rule: iclassfixture (DX08) ---------------------------------------------------
# The set of Kumunita.Core.Tests files that still declare IClassFixture<PostgresFixture>
# must be a subset of the server-scoped allowlist. The allowlist is exactly one file
# (the DX06 triage, ADR 0171 — AppRoleCannotDropSchemaTests does catalog-level
# CREATE ROLE / CREATE DATABASE … OWNER and must keep its own private container).
# Source of truth for the allowlist: docs/plans-milestones/in-progress/improve-devx-
# handoff-notes.md (## DX06). obj/ and bin/ are excluded (build artifacts). An
# empty set is a valid subset (the allowlist file may legitimately stop declaring
# the pattern in the future); the rule fails only when a file *outside* the
# allowlist still declares it.
$coreFixtureAllowlist = @('F22_AppRoleCannotDropSchemaTests.cs')
$coreFixtureFiles = @(Get-ChildItem -Path (Join-Path $repo 'tests/Kumunita.Core.Tests') -Filter '*.cs' -File -Recurse |
  Where-Object { $_.FullName -notmatch '/obj/|/bin/' } |
  Where-Object { (Select-String -LiteralPath $_.FullName -Pattern 'IClassFixture<PostgresFixture>' -SimpleMatch -Quiet) })
$coreFixtureNames = @($coreFixtureFiles | ForEach-Object { $_.Name })
$coreFixtureOk = ($coreFixtureNames.Count -le $coreFixtureAllowlist.Count) -and
  (@($coreFixtureNames | Where-Object { $coreFixtureAllowlist -notcontains $_ }).Count -eq 0)
Check "iclassfixture" "Core IClassFixture<PostgresFixture> files ($($coreFixtureNames -join ', ')) ⊆ allowlist ($($coreFixtureAllowlist -join ', '))" $coreFixtureOk
$ruleCount = 2

# --- summary --------------------------------------------------------------------
Write-Host ""
Write-Host "devx-check — IMPROVE-DEVX gate (D7)"
Write-Host "  rules landed : $ruleCount"
if ($ruleCount -eq 0) { Write-Host "  (DX01 skeleton: 0 rules — rules land with their fixing units, D7)" }
if ($failures.Count -eq 0) {
  Write-Host "  result       : PASS ($ruleCount rule(s))"
  exit 0
} else {
  Write-Host "  result       : FAIL ($($failures.Count) failure(s))" -ForegroundColor Red
  foreach ($f in $failures) { Write-Host "    - $f" }
  exit 1
}
