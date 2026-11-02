# improve-report.ps1 — the IMPROVE lane's one-page human-facing summary (U00)
#
# Run:  pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File improve-report.ps1
#
# Reads the repo and prints, top to bottom:
#   1. top 10 C# files by lines, colour-coded against the 2 000 ceiling
#   2. ADR drift count (index rows vs files on disk, plus the missing list)
#   3. shared '## ' heading count between AGENTS.md and copilot-instructions.md
#   4. handoff-notes-over-600-without-TL;DR count (the U02 backlog)
#   5. client/*.ts-over-800 count (the U00/U06 backlog)
#   6. views-over-800 count (the U06 backlog)
#   7. design-docs-over-400-without-Abstract count (the U07 backlog)
#
# This is a *report*, not a gate: it never exits non-zero (unless it cannot
# find the repo). improve-check.ps1 is the CI-able gate.

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

Write-Host "IMPROVE report  (repo: $repo)"
Write-Host ("-" * 78)

# --- 1. top 10 C# files ------------------------------------------------------
Write-Host "`n[1] top 10 C# files by lines (ceiling 2 000):"
$top = Get-ChildItem -Path (Join-Path $repo 'src') -Recurse -Filter '*.cs' -File |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
  ForEach-Object { [pscustomobject]@{ L=(Get-Content $_.FullName).Count; F=(Rel $_.FullName) } } |
  Sort-Object L -Descending |
  Select-Object -First 10
foreach ($f in $top) {
  $color = if ($f.L -gt 2000) { 'Red' } elseif ($f.L -gt 1500) { 'Yellow' } else { 'Green' }
  $pad = ' ' * ([Math]::Max(0, 6 - $f.L.ToString().Length))
  Write-Host ("  {0}{1,6}  {2}" -f $pad, $f.L, $f.F) -ForegroundColor $color
}

# --- 2. ADR drift ------------------------------------------------------------
Write-Host "`n[2] ADR index drift:"
$adrDir = Join-Path $repo 'docs/adr'
$adrFiles = (Get-ChildItem -Path $adrDir -Filter '0*.md' -File).BaseName | ForEach-Object { ($_ -split '-')[0] }
$adrIndexText = Get-Content -LiteralPath (Join-Path $adrDir 'README.md') -Raw
$adrIndexRows = [regex]::Matches($adrIndexText, '(?m)^\|\s*(0\d{3})\b') | ForEach-Object { $_.Groups[1].Value }
$adrMissing = $adrFiles | Where-Object { $adrIndexRows -notcontains $_ } | Sort-Object
$adrExtra = $adrIndexRows | Where-Object { $adrFiles -notcontains $_ } | Sort-Object
Write-Host ("  files on disk : {0}" -f $adrFiles.Count)
Write-Host ("  index rows    : {0}" -f $adrIndexRows.Count)
Write-Host ("  drift         : {0}" -f (@($adrMissing).Count + @($adrExtra).Count))
if (@($adrMissing).Count) { Write-Host "  missing from index: $($adrMissing -join ', ')" }
if (@($adrExtra).Count)   { Write-Host "  extra in index    : $($adrExtra -join ', ')" }

# --- 3. doc duplication ------------------------------------------------------
Write-Host "`n[3] shared '## ' headings AGENTS.md <-> .github/copilot-instructions.md:"
$ha = (Get-Content (Join-Path $repo 'AGENTS.md') | Where-Object { $_ -match '^##\s' } | ForEach-Object { $_ -replace '^##\s*','' })
$hb = (Get-Content (Join-Path $repo '.github/copilot-instructions.md') | Where-Object { $_ -match '^##\s' } | ForEach-Object { $_ -replace '^##\s*','' })
$shared = @($ha | Where-Object { $hb -contains $_ })
Write-Host ("  shared: {0}" -f $shared.Count)
foreach ($s in $shared) { Write-Host "    - $s" }

# --- 4. handoff TL;DR --------------------------------------------------------
Write-Host "`n[4] handoff notes under done/ over 600 lines without a TL;DR in first 20 lines:"
$dDir = Join-Path $repo 'docs/plans-milestones/done'
$d = Get-ChildItem -Path $dDir -Recurse -Filter '*-handoff-notes.md' -File |
  ForEach-Object { $n = @((Get-Content $_.FullName)).Count; if ($n -gt 600) { [pscustomobject]@{ L=$n; F=(Rel $_.FullName); TLDR=@((Get-Content $_.FullName -TotalCount 20) | Select-String 'TL;DR').Count -gt 0 } } } |
  Where-Object { -not $_.TLDR } |
  Sort-Object L -Descending
Write-Host ("  count: {0}" -f @($d).Count)
foreach ($f in $d) { Write-Host ("    {0,5}  {1}" -f $f.L, $f.F) }

# --- 5. TS over 800 ----------------------------------------------------------
Write-Host "`n[5] client/*.ts over 800 lines:"
$eDir = Join-Path $repo 'src/Kumunita.Web/client'
$e = Get-ChildItem -Path $eDir -Recurse -Filter '*.ts' -File |
  Where-Object { $_.FullName -notmatch '\\node_modules\\' -and @((Get-Content $_.FullName)).Count -gt 800 } |
  ForEach-Object { [pscustomobject]@{ L=@((Get-Content $_.FullName)).Count; F=(Rel $_.FullName) } } |
  Sort-Object L -Descending
Write-Host ("  count: {0}" -f @($e).Count)
foreach ($f in $e) { Write-Host ("    {0,5}  {1}" -f $f.L, $f.F) }

# --- 6. views over 800 -------------------------------------------------------
Write-Host "`n[6] .cshtml views over 800 lines:"
$f = Get-ChildItem -Path (Join-Path $repo 'src') -Recurse -Filter '*.cshtml' -File |
  Where-Object { @((Get-Content $_.FullName)).Count -gt 800 } |
  ForEach-Object { [pscustomobject]@{ L=@((Get-Content $_.FullName)).Count; F=(Rel $_.FullName) } } |
  Sort-Object L -Descending
Write-Host ("  count: {0}" -f @($f).Count)
foreach ($v in $f) { Write-Host ("    {0,5}  {1}" -f $v.L, $v.F) }

# --- 7. design docs over 400 without an Abstract -----------------------------
Write-Host "`n[7] docs/design/*.md over 400 lines without an Abstract in first 15 lines (U07 backlog):"
$gDir = Join-Path $repo 'docs/design'
$g = Get-ChildItem -Path $gDir -Filter '*.md' -File |
  ForEach-Object { $n = @((Get-Content $_.FullName)).Count; if ($n -gt 400) { [pscustomobject]@{ L=$n; F=$_.Name; ABS=@((Get-Content $_.FullName -TotalCount 15) | Select-String 'Abstract').Count -gt 0 } } } |
  Where-Object { -not $_.ABS } |
  Sort-Object L -Descending
Write-Host ("  count: {0}" -f @($g).Count)
$top7 = @($g | Select-Object -First 10)
foreach ($v in $top7) { Write-Host ("    {0,5}  {1}" -f $v.L, $v.F) }
if (@($g).Count -gt 10) { Write-Host "    ... and $(@($g).Count - 10) more (see the gate in improve-check.ps1 for the full list)" }

Write-Host ("-" * 78)
Write-Host "improve-report: done."
