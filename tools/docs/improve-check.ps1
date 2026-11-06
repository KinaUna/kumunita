# improve-check.ps1 — the IMPROVE lane's CI gate (U00, plan-improve.md)
#
# Run:  pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File improve-check.ps1
# Exit: 0 when every gate passes (or the violation is in the grandfathered
#       baseline), non-zero otherwise. Intended to be run from the repo root;
#       if the CWD is not the repo root, it walks up looking for Kumunita.slnx.
#
# Doctrine (AGENTS.md / .github/copilot-instructions.md): no here-strings,
# no inter-command $variables (this is a self-contained script run in one
# process, so the variable rule doesn't apply), single logical lines, and the
# whole thing is driven non-interactively by the command above — which is
# exactly the shape AGENTS.md prescribes for multi-line PowerShell.
#
# The gates (b, c, d, g remain; a, e, f retired by IMPROVE-CODE C00):
#   (a) RETIRED (IMPROVE-CODE C00) — was: .cs in src/ over 2 000 lines. The
#       code-size ceiling now lives in tools/code/code-check.ps1 (the
#       Roslyn-measured ratchet in tools/code/code-baseline.json, which is
#       finer than a single file-line ceiling and covers .cs/.ts/.cshtml).
#   (b) docs/adr/README.md index rows != docs/adr/0*.md file count
#   (c) AGENTS.md and .github/copilot-instructions.md share a "## " heading
#       with different content
#   (d) any *-handoff-notes.md under docs/archive/lanes/ over 600
#       lines without a "TL;DR" in the first 20 lines
#   (e) RETIRED (IMPROVE-CODE C00) — was: first-party .ts under
#       src/Kumunita.Web/client/ over 800 lines (now the ts_file ceiling in
#       tools/code/code-check.ps1).
#   (f) RETIRED (IMPROVE-CODE C00) — was: any .cshtml view over 800 lines
#       (now the cshtml ceiling in tools/code/code-check.ps1).
#   (g) any docs/design/*.md over 400 lines without an "Abstract" in its first
#       15 lines after the front-matter (added by U07, 2026-10-08 — the
#       design-doc tier of the 10-second "is this the right read" gate; the
#       handoff-note TL;DR of gate (d) is the same convention at the handoff
#       tier). Baselines retired in IMPROVE-DOCS D14.
#
# Baseline grandfathering (the plan's own words: "the gate allows the
# current baseline (it does not force an immediate fix); it prevents the
# baseline from growing"). Gates (b) and (c) each carry a grandfathered
# baseline set (U01's ADR rows, U03's shared headings): a violation *beyond*
# that set fails the close; a violation inside it is the in-flight work the
# baseline exists to allow. Gates (d) and (g) are **baseline-free** as of
# IMPROVE-DOCS D14 (2026-11-02) — the docs they measure have all been fixed,
# so they fail on *any* violation (the "retire the baselines, don't
# grandfather them" close work). Gates (a), (e) and (f) were retired by
# IMPROVE-CODE C00; the code-size ratchet they enforced now lives in
# tools/code/code-check.ps1 (a Roslyn-measured, shrink-only baseline that is
# strictly finer than these file-line ceilings and additionally covers .ts
# and .cshtml).

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# --- locate repo root (walk up from the CWD, or fall back to the script's
# --- own location, which lives under tools/docs/) -----
function Find-RepoRoot {
  $dir = (Get-Location).Path
  while ($dir -and $dir.Length -gt 0) {
    if (Test-Path (Join-Path $dir 'Kumunita.slnx')) { return $dir }
    $parent = Split-Path $dir -Parent
    if (-not $parent -or $parent -eq $dir) { break }
    $dir = $parent
  }
  # fallback: the script itself lives at tools/docs/...
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

function Rel([string]$path) {
  # normalize to forward slashes, relative to $repo
  $p = $path -replace '\\','/'
  $rp = $repo -replace '\\','/'
  if ($p.StartsWith($rp)) { return $p.Substring($rp.Length).TrimStart('/') }
  return $p
}

function Lines([string]$path) { @((Get-Content -LiteralPath $path)).Count }

# End-of-front-matter line (0-indexed) or -1 if the file has none. The abstract
# window is counted *after* the front-matter (DOCS-CONVENTIONS.md: "in its
# first 15 lines (after the front-matter)") — so the window starts on the line
# after the closing `---`.
function FmEnd([string]$path) {
  $lines = @(Get-Content -LiteralPath $path)
  if ($lines.Count -ge 2 -and $lines[0].Trim() -eq '---') {
    for ($i = 1; $i -lt [Math]::Min($lines.Count, 40); $i++) { if ($lines[$i].Trim() -eq '---') { return $i } }
  }
  return -1
}

# True iff an Abstract/TL;DR blockquote sits within $window lines after the
# front-matter (or after line 0 for front-matter-less files).
function HasAbstract([string]$path, [int]$window) {
  $lines = @(Get-Content -LiteralPath $path)
  $fm = FmEnd $path
  $start = if ($fm -ge 0) { $fm + 1 } else { 0 }
  for ($j = $start; $j -lt $start + $window -and $j -lt $lines.Count; $j++) {
    if ($lines[$j] -match '\bAbstract\b' -or $lines[$j] -match 'TL;DR') { return $true }
  }
  return $false
}

function Check([string]$gate, [string]$message, [bool]$cond) {
  if ($cond) {
    Write-Host "[OK]   $gate : $message"
  } else {
    Write-Host "[FAIL] $gate : $message" -ForegroundColor Red
    $failures.Add("$gate : $message")
  }
}


# =============================================================================
# Gate (b) — ADR index drift
# =============================================================================
$adrDir = Join-Path $repo 'docs/adr'
$adrIndex = Join-Path $adrDir 'README.md'
$adrFiles = (Get-ChildItem -Path $adrDir -Filter '0*.md' -File).BaseName |
  ForEach-Object { ($_ -split '-')[0] }
$adrFileCount = $adrFiles.Count
$adrIndexText = Get-Content -LiteralPath $adrIndex -Raw
# a row is a markdown table row whose first cell is the ADR number
$adrIndexRows = [regex]::Matches($adrIndexText, '(?m)^\|\s*(0\d{3})\b') | ForEach-Object { $_.Groups[1].Value }
$adrIndexCount = $adrIndexRows.Count
$adrMissing = @($adrFiles | Where-Object { $adrIndexRows -notcontains $_ } | Sort-Object)
$adrExtra   = @($adrIndexRows | Where-Object { $adrFiles -notcontains $_ } | Sort-Object)
# Baseline (U00 time, 2026-10-08): 8 ADRs on disk missing from the index —
# exactly the set U01 is scheduled to add (0096, 0137-0143). New drift beyond
# this set fails the close; when U01 lands, the baseline set shrinks naturally.
$bBaselineMissing = @('0096','0137','0138','0139','0140','0141','0142','0143')
$bBaselineExtra   = @()
$bNewMissing = @($adrMissing | Where-Object { $bBaselineMissing -notcontains $_ })
$bNewExtra   = @($adrExtra   | Where-Object { $bBaselineExtra   -notcontains $_ })
$bDetail = "files=$adrFileCount, index rows=$adrIndexCount, missing=($($adrMissing -join ',')), extra=($($adrExtra -join ','))"
Check "b" "ADR index drift — new beyond U01 baseline: $($bNewMissing.Count + $bNewExtra.Count) ($bDetail)" ($bNewMissing.Count -eq 0 -and $bNewExtra.Count -eq 0)
foreach ($v in $bNewMissing) { Write-Host "       - on disk but not in index: $v" }
foreach ($v in $bNewExtra)   { Write-Host "       - in index but not on disk: $v" }

# =============================================================================
# Gate (c) — AGENTS.md vs .github/copilot-instructions.md shared ## heading
# =============================================================================
function Get-SharedH2([string]$a, [string]$b) {
  $ha = (Get-Content -LiteralPath $a | Where-Object { $_ -match '^##\s' } | ForEach-Object { $_ -replace '^##\s*','' })
  $hb = (Get-Content -LiteralPath $b | Where-Object { $_ -match '^##\s' } | ForEach-Object { $_ -replace '^##\s*','' })
  return @($ha | Where-Object { $hb -contains $_ })
}
$agentsMd   = Join-Path $repo 'AGENTS.md'
$instrMd    = Join-Path $repo '.github/copilot-instructions.md'
$cShared = Get-SharedH2 $agentsMd $instrMd
# Baseline (U00 time, 2026-10-08): 5 shared headings.
# U03 (2026-10-08) de-duplicated all 5 — the `.github/copilot-instructions.md`
# now carries a pointer list instead of full copies, and the shared count is 0
# (the baseline list below is grandfathered-and-met, not just allowed — same
# pattern U01 used for the ADR rows; the gate passes either way, so the list
# is left untouched as optional cleanup).
$cBaselineShared = @(
  "Don't pause mid-task to check in",
  'Razor verification doctrine',
  'Git state gotcha',
  'Using the browser (trusted-folder quirk)',
  'Running PowerShell commands safely (Windows agents)'
)
$cNew = @($cShared | Where-Object { $cBaselineShared -notcontains $_ })
Check "c" "shared '## ' headings AGENTS.md <-> copilot-instructions.md (new shared beyond U03 baseline): $($cNew.Count)" ($cNew.Count -eq 0)
foreach ($v in $cNew) { Write-Host "       - shared heading: $v" }

# =============================================================================
# Gate (d) — handoff notes under done/ over 600 lines without a TL;DR
# =============================================================================
$dCeiling = 600
$dDir = Join-Path $repo 'docs/archive/lanes'
$dViolations = [System.Collections.Generic.List[string]]::new()
if (Test-Path $dDir) {
  $dFiles = Get-ChildItem -Path $dDir -Recurse -Filter '*-handoff-notes.md' -File
  foreach ($f in $dFiles) {
    $n = Lines $f.FullName
    if ($n -le $dCeiling) { continue }
    # The window is counted *after* the front-matter (the handoff notes gained
    # front-matter in IMPROVE-DOCS D03); Abstract and TL;DR are aliases
    # (DOCS-CONVENTIONS.md).
    if (-not (HasAbstract $f.FullName 20)) { $dViolations.Add((Rel $f.FullName) + " (" + $n + " lines, no TL;DR/Abstract in first 20 lines after front-matter)") }
  }
}
# Baseline retired (IMPROVE-DOCS D14, 2026-11-02): every archived handoff note
# that is over the 600-line ceiling now carries an Abstract/TL;DR within the
# window (verified by `docs-report`), so the U02 name-baseline is gone — the
# gate fails on *any* file crossing the ceiling without the blockquote, no
# grandfathering.
Check "d" "handoff notes >600 lines without TL;DR/Abstract (no baseline): $($dViolations.Count)" ($dViolations.Count -eq 0)
foreach ($v in $dViolations) { Write-Host "       - $v" }



# =============================================================================
# Gate (g) — design docs over 400 lines without an Abstract (U07, added 2026-10-08)
# =============================================================================
# Plan-improve.md §U07: any docs/design/*.md over 400 lines must carry a
# "> **Abstract:**" blockquote in its first 15 lines *after the front-matter*
# (DOCS-CONVENTIONS.md) — the 10-second "is this the right read" gate at the
# design tier (the handoff-note TL;DR, gate (d), at a different tier). No
# baseline: every over-400 design doc carries the blockquote, so the gate
# fails on any that does not (retired in IMPROVE-DOCS D14).
$gCeiling = 400
$gDir = Join-Path $repo 'docs/design'
$gViolations = [System.Collections.Generic.List[string]]::new()
if (Test-Path $gDir) {
  $gFiles = Get-ChildItem -Path $gDir -Recurse -Filter '*.md' -File
  foreach ($f in $gFiles) {
    $n = Lines $f.FullName
    if ($n -le $gCeiling) { continue }
    # The window is counted *after* the front-matter (DOCS-CONVENTIONS.md:
    # "in its first 15 lines (after the front-matter)") — every design doc
    # over the ceiling carries its Abstract within that window.
    if (-not (HasAbstract $f.FullName 15)) { $gViolations.Add((Rel $f.FullName) + " (" + $n + " lines, no Abstract in first 15 lines after front-matter)") }
  }
}
# Baseline retired (IMPROVE-DOCS D14, 2026-11-02): every `docs/design/*.md`
# over the 400-line ceiling now carries an Abstract within the 15-line window
# *after* the front-matter (the 16 that were flagged at D12/D13 already had a
# conformant Abstract — the window was just mis-reading front-matter lines as
# body). The U07 name-baseline is gone: the gate fails on *any* over-400
# design doc without the blockquote, no grandfathering.
Check "g" "design docs >400 lines without Abstract (no baseline): $($gViolations.Count)" ($gViolations.Count -eq 0)
foreach ($v in $gViolations) { Write-Host "       - $v" }

# =============================================================================
# Summary
# =============================================================================
Write-Host ""
if ($failures.Count -eq 0) {
  Write-Host "improve-check: ALL GATES PASS" -ForegroundColor Green
  exit 0
} else {
  Write-Host "improve-check: $($failures.Count) GATE(S) FAILED" -ForegroundColor Red
  exit 1
}
