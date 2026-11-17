# test-layout.ps1 — the IMPROVE-DEVX lane's test-layout mapping (D5, ADR 0307)
#
# Run:   pwsh tools/devx/test-layout.ps1 -Plan   # write tools/devx/test-layout.json
#        pwsh tools/devx/test-layout.ps1 -Check  # regenerate in memory, diff, exit 0/1
#
# DX25 implements -Apply -Project <Core|Web> -Range <A-F|G-O|P-Z|A-M|N-Z>
# (git mv only — no content edits, no namespace changes, no csproj change).
#
# The context discovery and the seam set are IDENTICAL to code-map.ps1 (same
# context rule, same seam regex, same exclusions) — the mapping and the code
# map agree by construction (the plan: "using the code-map data"). The one
# difference: code-map.ps1's Tests column uses a *does-any-seam-appear* test
# (a file may appear in several rows — fine for a map); the layout mapping
# must be a single folder per file, so it is a COUNT:
#
#   context with the most word-boundary, case-insensitive seam-name hits in
#   the file's text wins; a tie or zero total hits → _Shared/.
#
# The naive any-appears reading was rejected (DX24 brief, fact 2): it sends
# 293 of 413 files to _Shared/ (143 zero-hit + 150 multi-context ties).
# The `generated:` date is a constant (not Get-Date) so -Check stays
# byte-stable across days.
#
# Style: matches tools/devx/code-map.ps1.

param([switch]$Plan, [switch]$Check, [switch]$Apply,
      [string]$Project, [string]$Range)

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

# --- -Apply mode (DX25) -----------------------------------------------------------
# Reads the committed JSON and `git mv`s the project's root-level files mapped to
# a context in the letter range. Subfolder files are skipped (no-ops/candidates).
if ($Apply) {
  if ($Project -notin @('Core', 'Web') -or $Range.Length -ne 3 -or $Range[1] -ne '-') {
    throw "-Apply needs -Project Core|Web and -Range <A>-<Z> (e.g. A-F, G-O, P-Z, A-M, N-Z)"
  }
  $lo = $Range[0]; $hi = $Range[2]

  $jsonPath = Join-Path $repo 'tools/devx/test-layout.json'
  if (-not (Test-Path $jsonPath)) { throw "tools/devx/test-layout.json not found — run `test-layout.ps1 -Plan` first" }
  $json = (Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json)
  $projDir = "tests/Kumunita.$($Project).Tests/"
  $inRange = { param($ctx) $ctx.Length -ge 1 -and $ctx[0] -ge $lo -and $ctx[0] -le $hi }

  $moved = 0; $noops = 0
  $byCtx = @{}
  foreach ($m in $json.mapping) {
    if ($m.file -notlike $projDir + '*') { continue }
    if ($m.context -eq '_Shared/') { continue }
    if (-not (& $inRange $m.context)) { continue }
    # only root-level files — skip subfolder files (no-ops and candidates)
    $rest = $m.file.Substring($projDir.Length)
    if ($rest -match '/') { $noops++; continue }
    $srcPath = Join-Path $repo $m.file
    $destPath = Join-Path $repo ($projDir + $m.context + '/' + $rest)
    $destDir = Split-Path $destPath -Parent
    if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
    & git mv -- $srcPath $destPath
    if ($LASTEXITCODE -ne 0) { throw "git mv failed (exit $LASTEXITCODE): $srcPath -> $destPath" }
    $moved++
    $byCtx[$m.context] = 1 + $byCtx[$m.context]
  }
  if ($moved -eq 0) { throw "no files matched the A-F range for project '$Project' — check test-layout.json" }
  $ctxStr = ($byCtx.Keys | Sort-Object | ForEach-Object { "$_ ($($byCtx[$_]))" }) -join ', '
  Write-Host "[OK]   test-layout : moved $moved file(s) in '$Project' ($Range): $ctxStr; skipped $noops subfolder(s)"
  exit 0
}
if (-not ($Plan -or $Check)) {
  throw "no mode given — run with -Plan (write the JSON), -Check (diff against the committed JSON), or -Apply -Project <Core|Web> -Range <A-F|G-O|P-Z>"
}
$esc = [regex]::Escape($repo + '/')
function Rel([string]$p) { return $p -replace $esc, '' -replace '\\', '/' }
function Norm([string]$s) { return ($s -replace "`r`n", "`n") }

$core = 'src/Kumunita.Core'
$seamRe = [regex]"\bAdd(?:Transient|Scoped|Singleton)\s*<\s*([\w.]+)"

# --- the contexts: SAME rule as code-map.ps1 -------------------------------------
# A context folder = one with its own DependencyInjection.cs, minus the two
# excluded folders (the composition-root aggregator and the Announcements
# feature module — by rule, so a third context is not a trap).
$excludedDirs = @{ 'src/Kumunita.Core' = $true; 'src/Kumunita.Core/Announcements' = $true }
$contexts = @()
foreach ($d in (Get-ChildItem -Path (Join-Path $repo $core) -Filter 'DependencyInjection.cs' -File -Recurse |
    Where-Object { $_.FullName -notmatch '/obj/|/bin/' })) {
  $dir = Rel $d.Directory.FullName
  if (-not $excludedDirs.ContainsKey($dir)) { $contexts += $dir }
}
$contexts = @($contexts | Sort-Object)

# --- the seam set per context: SAME regex as code-map.ps1 -------------------------
$ctxSeams = @{}
foreach ($c in $contexts) {
  $diText = Get-Content -LiteralPath (Join-Path $repo $c 'DependencyInjection.cs') -Raw
  $seams = @($seamRe.Matches($diText) | ForEach-Object { $_.Groups[1].Value.Split('.')[-1] } | Sort-Object -Unique)
  $ctxSeams[$c] = @($seams)
}

# --- the test files: both projects, obj/ and bin/ excluded -------------------------
$testFiles = [System.Collections.Generic.List[object]]::new()
foreach ($tproj in @('tests/Kumunita.Core.Tests', 'tests/Kumunita.Web.Tests')) {
  foreach ($t in (Get-ChildItem -Path (Join-Path $repo $tproj) -Filter '*.cs' -File -Recurse |
    Where-Object { $_.FullName -notmatch '/obj/|/bin/' })) {
    $rel = Rel $t.FullName
    $parent = ($rel -split '/')[-2]
    $alreadyInSub = $parent -notin @('Kumunita.Core.Tests', 'Kumunita.Web.Tests')
    $testFiles.Add([pscustomobject]@{
      Rel = $rel
      Text = (Get-Content -LiteralPath $t.FullName -Raw)
      AlreadyInSub = $alreadyInSub
      ExistingSub = if ($alreadyInSub) { $parent } else { $null }
    })
  }
}

# --- the attribution: count-based (DX24 fact 2) ------------------------------------
# For each context, count word-boundary, case-insensitive hits of its seam names
# in the file's text; the highest count wins. A TIE (including all-zero) →
# _Shared/. This is the only reading that produces a PARTITION of the test tree
# (the code map's any-appears reading allows a file in several rows, which is
# fine for a map but not for a single folder per file).
function CountHits([string]$text, [string[]]$seams) {
  $total = 0
  foreach ($s in $seams) { $total += [regex]::Matches($text, '\b' + [regex]::Escape([string]$s) + '\b', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase).Count }
  return $total
}

$attributionRule = 'context with the most word-boundary seam-name hits in the file''s text; ties or zero → _Shared/'
$mapping = [System.Collections.Generic.List[object]]::new()
foreach ($tf in $testFiles) {
  $best = '_Shared/'; $bestHits = 0; $tie = $true
  foreach ($c in $contexts) {
    $h = CountHits $tf.Text $ctxSeams[$c]
    if ($h -gt $bestHits) { $best = ($c.Split('/')[-1]); $bestHits = $h; $tie = $false }
    elseif ($h -eq $bestHits) { $tie = $true }
  }
  if ($tie -and $bestHits -gt 0) { $best = '_Shared/' }
  $entry = [ordered]@{
    file = $tf.Rel
    context = $best
    hits = $bestHits
    already_in_subfolder = $tf.AlreadyInSub
  }
  if ($tf.AlreadyInSub) { $entry['existing_subfolder'] = $tf.ExistingSub }
  $mapping.Add([pscustomobject]$entry)
}
$mapping = @($mapping | Sort-Object { $_.file })

# --- the JSON (schema: DX24 brief, fact 8) ------------------------------------------
$obj = [ordered]@{
  version = 1
  generated = '2026-11-16'
  generated_by = 'tools/devx/test-layout.ps1 -Plan (DX24)'
  attribution_rule = $attributionRule
  contexts = $contexts.Count
  total_test_files = $testFiles.Count
  mapping = $mapping
}
$json = ($obj | ConvertTo-Json -Depth 6) + "`n"

$outPath = Join-Path $repo 'tools/devx/test-layout.json'
if ($Check) {
  if (-not (Test-Path $outPath)) {
    Write-Host "[FAIL] test-layout : tools/devx/test-layout.json does not exist — run `test-layout.ps1 -Plan` to generate it" -ForegroundColor Red
    exit 1
  }
  if ((Norm (Get-Content -LiteralPath $outPath -Raw)) -eq (Norm $json)) {
    Write-Host "[OK]   test-layout : tools/devx/test-layout.json is fresh ($($testFiles.Count) test files, $($contexts.Count) contexts)"
    exit 0
  }
  Write-Host "[FAIL] test-layout : tools/devx/test-layout.json is stale — run `pwsh tools/devx/test-layout.ps1 -Plan` to regenerate" -ForegroundColor Red
  exit 1
}
[System.IO.File]::WriteAllText($outPath, $json)
$shared = @($mapping | Where-Object { $_.context -eq '_Shared/' })
$byCtx = @{}
foreach ($m in $mapping) { if ($m.context -ne '_Shared/') { $byCtx[$m.context] = 1 + $byCtx[$m.context] } }
Write-Host "[OK]   test-layout : wrote tools/devx/test-layout.json ($($testFiles.Count) test files, $($contexts.Count) contexts, $($shared.Count) → _Shared/)"
