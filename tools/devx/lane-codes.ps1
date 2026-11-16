# lane-codes.ps1 — generate the lane-code glossary (D6, part 1)
#
#        pwsh tools/devx/lane-codes.ps1          # regenerate docs/dev/lane-codes.md
#        pwsh tools/devx/lane-codes.ps1 -Check   # regenerate in memory, diff, exit 0/1
#
# Resolves each lane-code prefix that actually appears in the codebase to its
# archived lane (docs/archive/lanes/<lane>/) in one hop. The prefix set is
# DISCOVERED: the candidate regex below is the same one devx-report.ps1 uses
# for E11 (line ~87, "candidate, not canonical — DX32 pins the canonical
# regex"); we take its letter set and keep only the letters that have at least
# one hit in src/ or tests/. The prefix → lane mapping is the verified one
# from the lane archives (each lane's archive carries its own codes; surveyed
# 2026-11-16 against docs/archive/lanes/): F → prod-hardening; C → improve-code;
# D → improve-docs; IS → improve-sec; U → improve-ux; R/A/O → improve-ux
# (sub-lane series of the same lane). LS / GP / UPL are in the candidate set
# but have 0 hits today, so they get no row (and no guessing row).
#
# The front-matter date is a constant (not Get-Date) so -Check stays
# byte-stable across days — the same trick code-map.ps1 uses.

param([switch]$Check)

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
  throw "could not find repo root (Kumunita.slnx) walking up from $(Get-Location)"
}

$repo = Find-RepoRoot
$target = Join-Path $repo 'docs/dev/lane-codes.md'

# --- the candidate prefix set (source: devx-report.ps1 E11, keep in sync) ----
$laneLineRegex = '\b(?:F|U|C|D|IS|LS|R|A|O|GP|UPL)[0-9]{1,2}[Uu]?[0-9]*(?:-[0-9]+)?\b'
$prefixes = @('F', 'U', 'C', 'D', 'IS', 'LS', 'R', 'A', 'O', 'GP', 'UPL')
# Verified prefix → lane (the lane archive that carries the codes).
$prefixLane = @{
  'F'  = 'prod-hardening'
  'C'  = 'improve-code'
  'D'  = 'improve-docs'
  'IS' = 'improve-sec'
  'U'  = 'improve-ux'
  'R'  = 'improve-ux'
  'A'  = 'improve-ux'
  'O'  = 'improve-ux'
}

# --- discovery: which prefixes actually appear in the codebase ---------------
$present = [System.Collections.Generic.List[string]]::new()
foreach ($scope in @('src', 'tests')) {
  $dir = Join-Path $repo $scope
  if (-not (Test-Path $dir)) { continue }
  foreach ($f in (Get-ChildItem -Path $dir -Filter '*.cs' -File -Recurse | Where-Object { $_.FullName -notmatch '/obj/|/bin/' })) {
    $text = Get-Content -LiteralPath $f.FullName -Raw
    foreach ($p in $prefixes) {
      # -cmatch: case-sensitive. Lane codes are uppercase (F14, C3-06, IS21);
      # a case-insensitive match would let a lowercase "gp12" flag the GP row.
      if ($text -cmatch ('(?<![A-Za-z])' + [regex]::Escape($p) + '[0-9]{1,2}')) {
        if (-not $present.Contains($p)) { $present.Add($p) }
      }
    }
  }
}
# Keep the known prefix order; only the letters that appear.
$active = @($prefixes | Where-Object { $present.Contains($_) })
if ($active.Count -eq 0) { throw "no lane codes found in src/ or tests/ — discovery broken?" }

# --- the archive lane folders + their plan file -------------------------------
$lanesRoot = Join-Path $repo 'docs/archive/lanes'
function Get-LanePlanLink([string]$lane) {
  $d = Join-Path $lanesRoot $lane
  if (-not (Test-Path $d)) { return '—' }
  $plans = @(Get-ChildItem -Path $d -Filter 'plan-*.md' -File | Where-Object { $_.Name -notmatch 'handoff' })
  if ($plans.Count -eq 0) { return '—' }
  # A lane archived in rounds carries one plan file per round; the highest
  # number is the latest round (a plan file with no trailing number scores 0).
  $latest = $plans | Sort-Object {
    $m = [regex]::Match($_.Name, '(\d+)\.md$')
    if ($m.Success) { [int]$m.Groups[1].Value } else { 0 }
  } -Descending | Select-Object -First 1
  return "docs/archive/lanes/$($latest.Directory.Name)/$($latest.Name)"
}

# --- build the document -------------------------------------------------------
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('---')
$lines.Add('id: dev-lane-codes')
$lines.Add('title: "Lane-code glossary — prefix to archived lane"')
$lines.Add('tier: reference')
$lines.Add('kind: guide')
$lines.Add('status: current')
$lines.Add('audiences: [developer, agent]')
$lines.Add('lane: improve-devx')
$lines.Add('updated: 2026-11-16')
$lines.Add('---')
$lines.Add('')
$lines.Add('> **Abstract:** the lane-code glossary (D6, part 1, ADR 0307) — one row')
$lines.Add('> per lane-code prefix that appears in the codebase, resolved to its')
$lines.Add('> archived lane under `docs/archive/lanes/` and that lane''s plan file,')
$lines.Add('> so a code comment''s `F14.U2` / `C3-06` / `IS21` lands in its lane in one')
$lines.Add('> hop. Generated from the tree; the devx `lane-codes` rule fails the moment it goes stale.')
$lines.Add('')
$lines.Add('# Lane-code glossary')
$lines.Add('')
$lines.Add('> generated by `tools/devx/lane-codes.ps1` — do not hand-edit.')
$lines.Add('')
$lines.Add('| Code prefix | Lane | Archive plan |')
$lines.Add('|---|---|---|')
foreach ($p in $active) {
  $lane = $prefixLane[$p]
  if (-not $lane) { continue }   # a prefix with no verified lane gets no row (no guessing)
  $link = Get-LanePlanLink $lane
  $lines.Add('| `' + $p + '<n>` | `docs/archive/lanes/' + $lane + '/` | ' + $link + ' |')
}
$lines.Add('')
$lines.Add('> Prefixes in the candidate set with no hits today (e.g. `LS`, `GP`,')
$lines.Add('> `UPL`) get no row — the table lists only codes the codebase carries.')
$lines.Add('')
$doc = ($lines -join "`n") + "`n"

if ($Check) {
  if (-not (Test-Path $target)) {
    Write-Host "lane-codes: docs/dev/lane-codes.md is missing — run: pwsh tools/devx/lane-codes.ps1" -ForegroundColor Red
    exit 1
  }
  $committed = Get-Content -LiteralPath $target -Raw
  if ($committed -ceq $doc) {
    Write-Host "lane-codes: docs/dev/lane-codes.md is fresh"
    exit 0
  }
  Write-Host "lane-codes: docs/dev/lane-codes.md is stale — run: pwsh tools/devx/lane-codes.ps1" -ForegroundColor Red
  exit 1
}

Set-Content -LiteralPath $target -Value $doc -NoNewline
Write-Host "lane-codes: wrote docs/dev/lane-codes.md ($($active.Count) prefixes → lanes)"
