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
# The gates (a–f from the U00 "Do" section of plan-improve.md; g added by U07):
#   (a) .cs in src/ over 2 000 lines
#   (b) docs/adr/README.md index rows != docs/adr/0*.md file count
#   (c) AGENTS.md and .github/copilot-instructions.md share a "## " heading
#       with different content
#   (d) any *-handoff-notes.md under docs/plans-milestones/done/ over 600
#       lines without a "TL;DR" in the first 20 lines
#   (e) first-party .ts under src/Kumunita.Web/client/ over 800 lines
#   (f) any .cshtml view over 800 lines
#   (g) any docs/design/*.md over 400 lines without an "Abstract" in its first
#       15 lines (added by U07, 2026-10-08 — the design-doc tier of the
#       10-second "is this the right read" gate; the handoff-note TL;DR of
#       gate (d) is the same convention at the handoff tier)
#
# Baseline grandfathering (the plan's own words: "the gate allows the
# current baseline (it does not force an immediate fix); it prevents the
# baseline from growing"). Each gate's BASELINE_* list is the *set of files
# already over the ceiling at U00 time* (collected 2026-10-08). A *new* file
# crossing the ceiling — or a *baseline* file growing *past its own size* —
# fails the close. When a U-unit retires a baseline entry, remove it from the
# list in the same commit.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# --- locate repo root (walk up from the CWD, or fall back to the script's
# --- own location, which lives under docs/plans-milestones/improve/) -------
function Find-RepoRoot {
  $dir = (Get-Location).Path
  while ($dir -and $dir.Length -gt 0) {
    if (Test-Path (Join-Path $dir 'Kumunita.slnx')) { return $dir }
    $parent = Split-Path $dir -Parent
    if (-not $parent -or $parent -eq $dir) { break }
    $dir = $parent
  }
  # fallback: the script itself lives at docs/plans-milestones/improve/...
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

function Check([string]$gate, [string]$message, [bool]$cond) {
  if ($cond) {
    Write-Host "[OK]   $gate : $message"
  } else {
    Write-Host "[FAIL] $gate : $message" -ForegroundColor Red
    $failures.Add("$gate : $message")
  }
}

# =============================================================================
# Gate (a) — src/*.cs ceiling 2 000 lines (baseline grandfathered)
# =============================================================================
$aCeiling = 2000
$aBaseline = @(
  'src/Kumunita.Core/Localization/KnownTranslationKeys.cs',
  'src/Kumunita.Web/Controllers/ProjectsController.cs',
  'src/Kumunita.Core/Projects/ProjectService.cs',
  'src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs',
  'src/Kumunita.Core/UserInfo/UserInfoService.cs',
  'src/Kumunita.Web/Controllers/GroupsController.cs',
  'src/Kumunita.Core/Events/EventService.cs',
  'src/Kumunita.Web/Controllers/PostsController.cs',
  'src/Kumunita.Core/Posts/PostService.cs',
  'src/Kumunita.Core/Bootstrap/SampleDataSeeder.cs'
)
#aBaselineSizes = the *exact* sizes at U00 time (baseline may not grow either)
$aBaselineSizes = @{
  'src/Kumunita.Core/Localization/KnownTranslationKeys.cs' = 9527
  'src/Kumunita.Web/Controllers/ProjectsController.cs'     = 4971
  'src/Kumunita.Core/Projects/ProjectService.cs'           = 4803
  'src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs'         = 4579
  'src/Kumunita.Core/UserInfo/UserInfoService.cs'          = 4213
  'src/Kumunita.Web/Controllers/GroupsController.cs'       = 3032
  'src/Kumunita.Core/Events/EventService.cs'               = 2184
  'src/Kumunita.Web/Controllers/PostsController.cs'        = 2165
  'src/Kumunita.Core/Posts/PostService.cs'                 = 2148
  'src/Kumunita.Core/Bootstrap/SampleDataSeeder.cs'        = 2105
}
$aViolations = [System.Collections.Generic.List[string]]::new()
$csFiles = Get-ChildItem -Path (Join-Path $repo 'src') -Recurse -Filter '*.cs' -File |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }
foreach ($f in $csFiles) {
  $rel = Rel $f.FullName
  $n = Lines $f.FullName
  if ($n -le $aCeiling) { continue }
  if ($aBaselineSizes.ContainsKey($rel)) {
    if ($n -gt $aBaselineSizes[$rel]) {
      $aViolations.Add("$rel = $n lines (baseline was $($aBaselineSizes[$rel]); baseline file grew)")
    }
    continue
  }
  $aViolations.Add("$rel = $n lines (new file over the $aCeiling ceiling)")
}
Check "a" "src/*.cs over 2000 lines (new or grown past its U00 baseline): $($aViolations.Count)" ($aViolations.Count -eq 0)
foreach ($v in $aViolations) { Write-Host "       - $v" }

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
$dDir = Join-Path $repo 'docs/plans-milestones/done'
$dViolations = [System.Collections.Generic.List[string]]::new()
if (Test-Path $dDir) {
  $dFiles = Get-ChildItem -Path $dDir -Recurse -Filter '*-handoff-notes.md' -File
  foreach ($f in $dFiles) {
    $n = Lines $f.FullName
    if ($n -le $dCeiling) { continue }
    $first20 = Get-Content -LiteralPath $f.FullName -TotalCount 20
    $hasTldr = @($first20 | Select-String -Pattern 'TL;DR').Count -gt 0
    if (-not $hasTldr) { $dViolations.Add((Rel $f.FullName) + " (" + $n + " lines, no TL;DR in first 20 lines)") }
  }
}
# Baseline (U00 time): 23 files over 600 lines with no TL;DR — U02's job is to
# add TL;DRs to the *8 largest*. The gate does NOT grandfather a count; it
# fails on any *new* file crossing 600 without a TL;DR. The 23 baseline files
# are grandfathered by *name*; when U02 (or any future unit) adds a TL;DR to
# one, it drops out of the baseline naturally (it stops violating).
$dBaseline = @(
  'docs/plans-milestones/done/m3b/m3b-handoff-notes.md',
  'docs/plans-milestones/done/pages/pages-handoff-notes.md',
  'docs/plans-milestones/done/m11/m11-portability-handoff-notes.md',
  'docs/plans-milestones/done/m10/m10-pwa-handoff-notes.md',
  'docs/plans-milestones/done/file-attachments/file-attachments-handoff-notes.md',
  'docs/plans-milestones/done/m4/m4-handoff-notes.md',
  'docs/plans-milestones/done/m14/m14-events-projects-handoff-notes.md',
  'docs/plans-milestones/done/media/media-file-storage-handoff-notes.md',
  'docs/plans-milestones/done/rich-editor/rich-editor-handoff-notes.md',
  'docs/plans-milestones/done/m13/m13-logging-analytics-handoff-notes.md',
  'docs/plans-milestones/done/m9/m9-messaging-handoff-notes.md',
  'docs/plans-milestones/done/m27-handoff-notes.md',
  'docs/plans-milestones/done/m15/m15-translation-bulk-handoff-notes.md',
  'docs/plans-milestones/done/pl/pl-handoff-notes.md',
  'docs/plans-milestones/done/m26/m26-handoff-notes.md',
  'docs/plans-milestones/done/multilingual-ui/multilingual-ui-handoff-notes.md',
  'docs/plans-milestones/done/site/site-handoff-notes.md',
  'docs/plans-milestones/done/m16/m16-inventory-handoff-notes.md',
  'docs/plans-milestones/done/group-posts/group-posts-handoff-notes.md',
  'docs/plans-milestones/done/wysiwyg/wysiwyg-handoff-notes.md',
  'docs/plans-milestones/done/m25/m25-handoff-notes.md',
  'docs/plans-milestones/done/m20/m20-notification-quiet-times-handoff-notes.md',
  'docs/plans-milestones/done/m1/m1-step-7-handoff-notes.md'
)
# violation strings are "path (N lines, ...)" — compare on the path prefix
$dNew = @($dViolations | Where-Object { $p = ($_ -split ' \(')[0]; $dBaseline -notcontains $p })
Check "d" "handoff notes >600 lines without TL;DR (new beyond U02 baseline): $($dNew.Count)" ($dNew.Count -eq 0)
foreach ($v in $dNew) { Write-Host "       - $v" }

# =============================================================================
# Gate (e) — first-party .ts under client/ over 800 lines
# =============================================================================
$eCeiling = 800
$eDir = Join-Path $repo 'src/Kumunita.Web/client'
$eViolations = [System.Collections.Generic.List[string]]::new()
if (Test-Path $eDir) {
  $tsFiles = Get-ChildItem -Path $eDir -Recurse -Filter '*.ts' -File |
    Where-Object { $_.FullName -notmatch '\\node_modules\\' }
  foreach ($f in $tsFiles) {
    $n = Lines $f.FullName
    if ($n -le $eCeiling) { continue }
    $eViolations.Add((Rel $f.FullName) + " = $n lines")
  }
}
# Baseline (U00 time): one file, rich-editor.ts at 1 634 lines.
$eBaselineSizes = @{
  'src/Kumunita.Web/client/lib/rich-editor.ts' = 1634
}
$eNew = [System.Collections.Generic.List[string]]::new()
foreach ($v in $eViolations) {
  $rel = ($v -split ' = ')[0]
  if ($eBaselineSizes.ContainsKey($rel)) {
    $n = [int]((($v -split ' = ')[1]) -replace ' lines','')
    if ($n -gt $eBaselineSizes[$rel]) { $eNew.Add("$rel = $n (baseline was $($eBaselineSizes[$rel]))") }
  } else {
    $eNew.Add($v)
  }
}
Check "e" "client/*.ts over 800 lines (new or grown past its U00 baseline): $($eNew.Count)" ($eNew.Count -eq 0)
foreach ($v in $eNew) { Write-Host "       - $v" }

# =============================================================================
# Gate (f) — .cshtml views over 800 lines
# =============================================================================
$fCeiling = 800
$fViolations = [System.Collections.Generic.List[string]]::new()
$cshtmlFiles = Get-ChildItem -Path (Join-Path $repo 'src') -Recurse -Filter '*.cshtml' -File
foreach ($f in $cshtmlFiles) {
  $n = Lines $f.FullName
  if ($n -le $fCeiling) { continue }
  $fViolations.Add((Rel $f.FullName) + " = $n lines")
}
$fBaselineSizes = @{
  'src/Kumunita.Web/Views/Projects/BoardDetail.cshtml' = 1431
  'src/Kumunita.Web/Views/Posts/Detail.cshtml'         = 1015
  'src/Kumunita.Web/Views/Projects/TodoDetail.cshtml'  = 890
}
$fNew = [System.Collections.Generic.List[string]]::new()
foreach ($v in $fViolations) {
  $rel = ($v -split ' = ')[0]
  if ($fBaselineSizes.ContainsKey($rel)) {
    $n = [int]((($v -split ' = ')[1]) -replace ' lines','')
    if ($n -gt $fBaselineSizes[$rel]) { $fNew.Add("$rel = $n (baseline was $($fBaselineSizes[$rel]))") }
  } else {
    $fNew.Add($v)
  }
}
Check "f" "views .cshtml over 800 lines (new or grown past its U00 baseline): $($fNew.Count)" ($fNew.Count -eq 0)
foreach ($v in $fNew) { Write-Host "       - $v" }

# =============================================================================
# Gate (g) — design docs over 400 lines without an Abstract (U07, added 2026-10-08)
# =============================================================================
# Plan-improve.md §U07: any docs/design/*.md over 400 lines must carry a
# "> **Abstract:**" blockquote in its first 15 lines — the 10-second "is this
# the right read" gate at the design tier (the handoff-note TL;DR, gate (d),
# at a different tier). The 5 largest (m13 / m3b / m18 / m20 / m5) are the
# baseline U07 closed; the 38 remaining over-400-without-Abstract docs are
# grandfathered by *name* (same pattern as gate (d) — U02's 23-note baseline).
# When any future unit adds an Abstract to one, it drops out of the baseline
# naturally (it stops violating). A *new* design doc over 400 without an
# Abstract fails the close.
$gCeiling = 400
$gDir = Join-Path $repo 'docs/design'
$gViolations = [System.Collections.Generic.List[string]]::new()
if (Test-Path $gDir) {
  $gFiles = Get-ChildItem -Path $gDir -Recurse -Filter '*.md' -File
  foreach ($f in $gFiles) {
    $n = Lines $f.FullName
    if ($n -le $gCeiling) { continue }
    $first15 = Get-Content -LiteralPath $f.FullName -TotalCount 15
    $hasAbstract = @($first15 | Select-String -Pattern 'Abstract').Count -gt 0
    if (-not $hasAbstract) { $gViolations.Add((Rel $f.FullName) + " (" + $n + " lines, no Abstract in first 15 lines)") }
  }
}
# Baseline (U07 time, 2026-10-08): the 38 over-400 design docs without an
# Abstract (the U00-time set of 43, minus the 5 largest U07 closed: m13, m3b,
# m18, m20, m5 — those now pass and are not listed here).
$gBaseline = @(
  'docs/design/events-calendar-design.md',
  'docs/design/events-calendar-dwm-design.md',
  'docs/design/file-attachments-design.md',
  'docs/design/group-posts-design.md',
  'docs/design/guardian-assignment-design.md',
  'docs/design/guardian-controls-design.md',
  'docs/design/m10-pwa-responsive-design.md',
  'docs/design/m11-portability-design.md',
  'docs/design/m12-ical-design.md',
  'docs/design/m14-events-projects-design.md',
  'docs/design/m15-translation-bulk-design.md',
  'docs/design/m16-inventory-design.md',
  'docs/design/m17-bookmarks-design.md',
  'docs/design/m19-guest-accounts-design.md',
  'docs/design/m2-directory-profiles-groups.md',
  'docs/design/m21-document-management-design.md',
  'docs/design/m22-onboarding-design.md',
  'docs/design/m23-extended-profiles-design.md',
  'docs/design/m24-storage-metrics-design.md',
  'docs/design/m25-upload-limits-design.md',
  'docs/design/m26-sorting-design.md',
  'docs/design/m27-user-scoped-portability-design.md',
  'docs/design/m28-guardian-time-limits-design.md',
  'docs/design/m3-posts-design.md',
  'docs/design/m4-events-design.md',
  'docs/design/m6-notifications-design.md',
  'docs/design/m7-pagination-filtering-design.md',
  'docs/design/m8-search-design.md',
  'docs/design/m9-messaging-design.md',
  'docs/design/media-file-storage-design.md',
  'docs/design/multilingual-design.md',
  'docs/design/pages-design.md',
  'docs/design/pl-goals-projects-design.md',
  'docs/design/rich-content-design.md',
  'docs/design/site-content-design.md',
  'docs/design/tags-design.md',
  'docs/design/tbd-todo-dependency-design.md',
  'docs/design/wysiwyg-editor-design.md'
)
# violation strings are "path (N lines, ...)" — compare on the path prefix
$gNew = @($gViolations | Where-Object { $p = ($_ -split ' \(')[0]; $gBaseline -notcontains $p })
Check "g" "design docs >400 lines without Abstract (new beyond U07 baseline): $($gNew.Count)" ($gNew.Count -eq 0)
foreach ($v in $gNew) { Write-Host "       - $v" }

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
