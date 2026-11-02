# docs-report.ps1 — the IMPROVE-DOCS lane's one-page human-facing report (D00)
#
# Run:  pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/docs-report.ps1
#
# Reads the repo and prints, top to bottom:
#   1. front-matter coverage (--- in the first line)
#   2. abstract coverage (Abstract/TL;DR blockquote in first 20 lines, docs >60 lines)
#   3. inbound/outbound link counts + orphans
#   4. broken relative links
#   5. ADR length histogram (≤250, 251–400, >400)
#
# This is a *report*, not a gate: it never exits non-zero (unless it cannot
# find the repo). docs-check.ps1 (D14) is the CI-able gate.
#
# Style: matches improve-report.ps1 — $ErrorActionPreference = "Stop",
# Find-RepoRoot walking up to Kumunita.slnx, Rel() for repo-relative paths,
# [OK]/[INFO]-style colour coding, never exits non-zero.

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

function Rel([string]$path) {
  $p = $path -replace '\\','/'
  $rp = $repo -replace '\\','/'
  if ($p.StartsWith($rp)) { return $p.Substring($rp.Length).TrimStart('/') }
  return $p
}
function Lines([string]$path) { @((Get-Content -LiteralPath $path)).Count }

# --- collect the doc set -----------------------------------------------------
$docs = @()
$docs += Get-ChildItem -Path (Join-Path $repo 'docs') -Recurse -Filter '*.md' -File
foreach ($top in @('README.md', 'AGENTS.md', 'SECURITY.md')) {
  $p = Join-Path $repo $top
  if (Test-Path $p) { $docs += [pscustomobject]@{ FullName = $p } }
}
$docs = @($docs | Sort-Object FullName -Unique)

Write-Host "IMPROVE-DOCS report  (repo: $repo)"
Write-Host ("-" * 78)

# --- 1. front-matter coverage ------------------------------------------------
Write-Host "`n[1] front-matter coverage (--- in the first line):"
$withFM = 0
$withoutFM = @()
foreach ($d in $docs) {
  $firstLine = @(Get-Content -LiteralPath $d.FullName -TotalCount 1)
  if ($firstLine.Count -ge 1 -and $firstLine[0].Trim() -eq '---') {
    $withFM++
  } else {
    $withoutFM += [pscustomobject]@{ L=(Lines $d.FullName); F=(Rel $d.FullName) }
  }
}
Write-Host ("  total docs : {0}" -f $docs.Count)
Write-Host ("  with FM    : {0}" -f $withFM)
Write-Host ("  without    : {0}" -f @($withoutFM).Count)
$topNoFM = @($withoutFM | Sort-Object L -Descending | Select-Object -First 20)
foreach ($f in $topNoFM) { Write-Host ("    {0,5}  {1}" -f $f.L, $f.F) }
if (@($withoutFM).Count -gt 20) { Write-Host "    ... and $(@($withoutFM).Count - 20) more" }

# --- 2. abstract coverage ----------------------------------------------------
Write-Host "`n[2] abstract coverage (docs >60 lines, Abstract/TL;DR in first 20 lines):"
$needAbstract = @()
foreach ($d in $docs) {
  if ((Lines $d.FullName) -gt 60) { $needAbstract += $d }
}
$withAbstract = 0
$withoutAbstract = @()
foreach ($d in $needAbstract) {
  $first20 = @(Get-Content -LiteralPath $d.FullName -TotalCount 20)
  $has = ($first20 | Select-String -Pattern '^\s*>\s*\*\*(?:Abstract|TL;DR)' | Select-Object -First 1) -ne $null
  if ($has) {
    $withAbstract++
  } else {
    $withoutAbstract += [pscustomobject]@{ L=(Lines $d.FullName); F=(Rel $d.FullName) }
  }
}
Write-Host ("  docs >60   : {0}" -f @($needAbstract).Count)
Write-Host ("  with abs   : {0}" -f $withAbstract)
Write-Host ("  without    : {0}" -f @($withoutAbstract).Count)
$topNoAbs = @($withoutAbstract | Sort-Object L -Descending | Select-Object -First 20)
foreach ($f in $topNoAbs) { Write-Host ("    {0,5}  {1}" -f $f.L, $f.F) }
if (@($withoutAbstract).Count -gt 20) { Write-Host "    ... and $(@($withoutAbstract).Count - 20) more" }

# --- 3. inbound/outbound link counts -----------------------------------------
Write-Host "`n[3] inbound/outbound link counts (relative .md links between docs):"
# Build a normalized-path -> doc lookup for O(1) target resolution.
$docByPath = @{}
foreach ($d in $docs) {
  $docByPath[($d.FullName -replace '\\','/').ToLowerInvariant()] = $d
}

$inbound  = @{}
$outbound = @{}
foreach ($d in $docs) {
  $inbound[$d.FullName]  = 0
  $outbound[$d.FullName] = 0
}

$linkRegex = [regex]'\[([^\]]*)\]\(([^)]+)\)'
$totalLinks = 0
foreach ($d in $docs) {
  $content = Get-Content -LiteralPath $d.FullName -Raw
  if (-not $content) { continue }
  $linkMatches = $linkRegex.Matches($content)
  foreach ($m in $linkMatches) {
    $target = $m.Groups[2].Value.Trim()
    # strip anchor
    $hashIdx = $target.IndexOf('#')
    if ($hashIdx -ge 0) { $target = $target.Substring(0, $hashIdx) }
    # filter: must end in .md, not a URL, not absolute
    if ($target -notmatch '\.md$') { continue }
    if ($target -match '^(?:https?://|//)') { continue }
    if ($target.StartsWith('/')) { continue }
    # resolve relative to the source file's directory
    $srcDir = Split-Path $d.FullName -Parent
    $resolved = [System.IO.Path]::GetFullPath((Join-Path $srcDir $target))
    $resolvedNorm = ($resolved -replace '\\','/').ToLowerInvariant()
    if ($docByPath.ContainsKey($resolvedNorm)) {
      $targetDoc = $docByPath[$resolvedNorm]
      $outbound[$d.FullName] = $outbound[$d.FullName] + 1
      $inbound[$targetDoc.FullName] = $inbound[$targetDoc.FullName] + 1
      $totalLinks++
    }
  }
}

$orphans = 0
$noOutbound = 0
foreach ($d in $docs) {
  if ($inbound[$d.FullName] -eq 0) { $orphans++ }
  if ($outbound[$d.FullName] -eq 0) { $noOutbound++ }
}
Write-Host ("  total doc links : {0}" -f $totalLinks)
Write-Host ("  orphans (0 in)  : {0}" -f $orphans)
Write-Host ("  no outbound     : {0}" -f $noOutbound)

# --- 4. broken relative links -------------------------------------------------
Write-Host "`n[4] broken relative links (target .md does not exist):"
$brokenList = @()
foreach ($d in $docs) {
  $content = Get-Content -LiteralPath $d.FullName -Raw
  if (-not $content) { continue }
  $linkMatches = $linkRegex.Matches($content)
  foreach ($m in $linkMatches) {
    $target = $m.Groups[2].Value.Trim()
    $hashIdx = $target.IndexOf('#')
    if ($hashIdx -ge 0) { $target = $target.Substring(0, $hashIdx) }
    if ($target -notmatch '\.md$') { continue }
    if ($target -match '^(?:https?://|//)') { continue }
    if ($target.StartsWith('/')) { continue }
    $srcDir = Split-Path $d.FullName -Parent
    $resolved = [System.IO.Path]::GetFullPath((Join-Path $srcDir $target))
    if (-not (Test-Path -LiteralPath $resolved)) {
      $brokenList += "  $(Rel $d.FullName)  ->  $target"
    }
  }
}
Write-Host ("  count: {0}" -f @($brokenList).Count)
$topBroken = @($brokenList | Select-Object -First 20)
foreach ($b in $topBroken) { Write-Host $b }
if (@($brokenList).Count -gt 20) { Write-Host "  ... and $(@($brokenList).Count - 20) more" }

# --- 5. ADR length histogram ---------------------------------------------------
Write-Host "`n[5] ADR length histogram (target ~250 lines):"
$adrDir = Join-Path $repo 'docs/adr'
$adrFiles = Get-ChildItem -Path $adrDir -Filter '0*.md' -File
$b1 = 0; $b2 = 0; $b3 = 0
$over400 = @()
foreach ($a in $adrFiles) {
  $n = Lines $a.FullName
  if ($n -le 250) { $b1++ }
  elseif ($n -le 400) { $b2++ }
  else { $b3++; $over400 += [pscustomobject]@{ L=$n; F=$a.Name } }
}
Write-Host ("  total ADRs : {0}" -f @($adrFiles).Count)
Write-Host ("  ≤250       : {0}" -f $b1)
Write-Host ("  251–400    : {0}" -f $b2)
Write-Host ("  >400       : {0}" -f $b3)
$topOver400 = @($over400 | Sort-Object L -Descending)
foreach ($a in $topOver400) { Write-Host ("    {0,5}  {1}" -f $a.L, $a.F) }

Write-Host ("-" * 78)
Write-Host "docs-report: done."
