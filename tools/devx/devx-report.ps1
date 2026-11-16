# devx-report.ps1 — the IMPROVE-DEVX lane's baseline report (D7)
#
# Run:  pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/devx/devx-report.ps1
# Exit: 0 (the report never fails; it prints numbers). Intended to be run from
#       the repo root; if the CWD is not the repo root, it walks up looking
#       for Kumunita.slnx.
#
# This is the *report* half of D7 (the check half is devx-check.ps1). It
# recomputes the plan's Evidence table (E2, E9, E11, E12) over the live tree
# so a future unit can see the drift since the plan's 2026-11-09 measurement
# and can re-baseline honestly. The CI seconds and the cold-start numbers are
# recorded values (they come from GitHub Actions runs 2026-10-27 … 10-31 and
# from a measured agent session, not from the tree); they are printed with
# their source in a comment.
#
# DX01 drift note: the plan's DX01 entry read for `tools/devx/devx-report.mjs`
# is stale — DX00 never landed a .mjs (verified: the file is absent in this
# checkout). The numbers' source of truth is the plan's Evidence table
# (plan-improve-devx.md, "## Evidence / starting numbers"), not a sibling
# script. This file *is* the baseline generator going forward.
#
# Style: matches tools/docs/docs-check.ps1 — $ErrorActionPreference = "Stop",
# Set-StrictMode -Version Latest, Find-RepoRoot walking up to Kumunita.slnx.

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

# --- E2 — IClassFixture<PostgresFixture> classes ---------------------------
# The count of *files* that opt a test class into the per-class Postgres
# fixture (the S2 finding). A file may opt in more than one class; the plan's
# E2 number (147 Core + 34 Web = 181) counts classes, not files, and was
# measured 2026-11-09. The tree has grown since; this number is the live
# file count, which is the same order and is what DX07–DX09 will drive down.
# obj/ and bin/ are excluded (build artifacts, not source).
$fixtureFilter = {
  $_.FullName -notmatch '/obj/|/bin/' -and
  (Select-String -LiteralPath $_.FullName -Pattern 'IClassFixture<PostgresFixture>' -SimpleMatch -Quiet)
}
$coreFixtureFiles = @(Get-ChildItem -Path (Join-Path $repo 'tests/Kumunita.Core.Tests') -Filter '*.cs' -File -Recurse | Where-Object $fixtureFilter)
$webFixtureFiles = @(Get-ChildItem -Path (Join-Path $repo 'tests/Kumunita.Web.Tests') -Filter '*.cs' -File -Recurse | Where-Object $fixtureFilter)

# --- E9 — test files (all, at project root, lane-named) ---------------------
# obj/ and bin/ are excluded (build artifacts, not source).
$testFileFilter = { $_.FullName -notmatch '/obj/|/bin/' }
$coreTestFiles = @(Get-ChildItem -Path (Join-Path $repo 'tests/Kumunita.Core.Tests') -Filter '*.cs' -File -Recurse | Where-Object $testFileFilter)
$webTestFiles = @(Get-ChildItem -Path (Join-Path $repo 'tests/Kumunita.Web.Tests') -Filter '*.cs' -File -Recurse | Where-Object $testFileFilter)
$allTestFiles = @($coreTestFiles + $webTestFiles)
$rootTestFiles = @($allTestFiles | Where-Object { $_.Directory.Name -match 'Kumunita\.(Core|Web)\.Tests' })
# Lane/unit-coded file names (E9's "17 named by lane/unit"): a digit-lane
# prefix followed by an underscore (F12_U3, LS_U04, ADR_0044, C3-06, …).
$laneNamed = @($allTestFiles | Where-Object {
  $_.Name -match '^(F|U|C|D|IS|LS|R|A|O|GP|UPL)[0-9]+[U]?[0-9]*[_-]|^ADR_0[0-9]{3}_'
})

# --- E11 — lane-code hits ----------------------------------------------------
# Lane/unit codes inside comments (the D6 ratchet's starting number). The
# plan's E11 number is 3391 across 1305 src/test files, measured 2026-11-09
# with a regex the plan does NOT pin. This file's regex is a candidate, not
# the canonical one — DX32 pins the canonical regex and re-baselines when it
# lands the ratchet. The candidate matches a lane letter, a 1–2 digit lane
# number, an optional U/u/unit segment, an optional dash + unit, not
# followed by a word char (so "Unit" in prose does not count). Name hits
# are counted separately (DX23 renames the file names; DX32 ratchets the
# line hits). Treat the live line count below as a measurement, not a
# baseline: devx-baseline.json carries the plan's 3391 as the shrink-only
# value until DX32 replaces it with the canonical regex's number.
$laneLineRegex = '\b(?:F|U|C|D|IS|LS|R|A|O|GP|UPL)[0-9]{1,2}[Uu]?[0-9]*(?:-[0-9]+)?\b'
$srcDir = Join-Path $repo 'src'
$testDir = Join-Path $repo 'tests'
$lineHits = 0
foreach ($scope in @($srcDir, $testDir)) {
  if (-not (Test-Path $scope)) { continue }
  foreach ($f in (Get-ChildItem -Path $scope -Filter '*.cs' -File -Recurse)) {
    $m = Select-String -LiteralPath $f.FullName -Pattern $laneLineRegex -AllMatches
    if ($m) { foreach ($line in $m) { $lineHits += $line.Matches.Count } }
  }
}
# Name hits: the lane-named test files above (DX23's target).
$nameHits = $laneNamed.Count

# --- E12 — ADRs unreachable by link -----------------------------------------
# An ADR is "unreachable" if no other doc under docs/ links to its filename.
# DX22: the index (docs/adr/README.md) *is* a reach — its data rows each link
# to their ADR file, so the index now reaches every ADR (exit is 0).
# Measured 2026-11-09: 104 of 299. The tree has grown since (more ADRs, more
# docs); this is the live number.
$adrFiles = @(Get-ChildItem -Path (Join-Path $repo 'docs/adr') -Filter '*.md' -File |
  Where-Object { $_.Name -match '^0[0-9]{3}-.*\.md$' })
$allDocs = @(Get-ChildItem -Path (Join-Path $repo 'docs') -Filter '*.md' -File -Recurse |
  Where-Object { $_.FullName -notmatch 'node_modules|wwwroot' })
# Load each doc's content once (matching ADR names across ~150K pairs of
# per-pair file reads would take minutes; one load each is seconds).
# DX22: the ADR index is included — its rows are links, so it counts as a reach.
$docContents = [System.Collections.Generic.Dictionary[string, string]]::new()
foreach ($d in $allDocs) {
  $docContents[$d.FullName] = Get-Content -LiteralPath $d.FullName -Raw
}
$unreachable = 0
foreach ($adr in $adrFiles) {
  $linked = $false
  foreach ($key in $docContents.Keys) {
    if ($key -eq $adr.FullName) { continue }
    if ($docContents[$key] -match [regex]::Escape($adr.Name)) { $linked = $true; break }
  }
  if (-not $linked) { $unreachable++ }
}

# --- E12 — tracked *.out / *.log / *.trx artifacts ---------------------------
$trackedArtifacts = 0
try {
  $trackedArtifacts = @(git -C $repo ls-files | Where-Object { $_ -match '\.(out|log|trx)$' }).Count
} catch { $trackedArtifacts = -1 }

# --- ADR total ---------------------------------------------------------------
$adrTotal = $adrFiles.Count

# --- AGENTS.md size (E7) -----------------------------------------------------
$agentsLines = 0; $agentsWords = 0
$agentsPath = Join-Path $repo 'AGENTS.md'
if (Test-Path $agentsPath) {
  $agentsLines = (Get-Content -LiteralPath $agentsPath).Count
  $agentsWords = (@(Get-Content -LiteralPath $agentsPath -Raw) -split '\s+' | Where-Object { $_ }).Count
}

# --- output ------------------------------------------------------------------
Write-Host ""
Write-Host "devx-report — IMPROVE-DEVX baseline (computed at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))"
Write-Host "repo: $repo"
Write-Host ""
Write-Host "E2  IClassFixture<PostgresFixture> files"
Write-Host ("      Core : {0}   (plan E2: 147, 2026-11-09)" -f $coreFixtureFiles.Count)
Write-Host ("      Web  : {0}   (plan E2: 34, 2026-11-09)" -f $webFixtureFiles.Count)
Write-Host "E9  test files"
Write-Host ("      all  : {0}   (plan E9: 381, 2026-11-09)" -f $allTestFiles.Count)
Write-Host ("      root : {0}   (plan E9: 343 at project root, 2026-11-09)" -f $rootTestFiles.Count)
Write-Host ("      lane : {0}   (plan E9: 17 lane-named, 2026-11-09)" -f $laneNamed.Count)
Write-Host ("E11 lane-code hits (regex: {0})" -f $laneLineRegex)
Write-Host ("      lines: {0}   (plan E11: 3391, 2026-11-09)" -f $lineHits)
Write-Host ("      names: {0}   (the lane-named test files above; DX23's target)" -f $nameHits)
Write-Host "E12 ADRs"
Write-Host ("      total       : {0}   (plan E12: 299, 2026-11-09)" -f $adrTotal)
Write-Host ("      unreachable : {0}   (plan E12: 104, 2026-11-09)" -f $unreachable)
Write-Host ("      tracked *.out/*.log/*.trx artifacts : {0}   (plan E12: 4, 2026-11-09)" -f $trackedArtifacts)
Write-Host "E7  AGENTS.md"
Write-Host ("      lines : {0}" -f $agentsLines)
Write-Host ("      words : {0}   (token proxy; DX16's cap is 2000 tokens, plan E7: 2052)" -f $agentsWords)
Write-Host ""
Write-Host "CI seconds — recorded, not computed (GitHub Actions, 2026-10-27 … 10-31, last 11 runs):"
Write-Host "      build : ~72 s    core: 391–428 s    web: 60–66 s"
Write-Host "      e2e   : 127–146 s    test job: 468–580 s"
Write-Host ""
Write-Host "Cold-start baseline — recorded (a fresh agent, three tasks, 2026-11-09):"
Write-Host "      32 calls / 7 wrong turns / ~123 K tokens   (DX36's comparison target)"
Write-Host ""
Write-Host "The numbers above that are computed are the live baseline for"
Write-Host "tools/devx/devx-check.ps1 (DX01's skeleton; rules land with their"
Write-Host "fixing units, DX04 onward). See tools/devx/devx-baseline.json."
exit 0
