# stamp-archive-frontmatter.ps1 — D03 of the IMPROVE-DOCS lane
#
# Run (from the repo root, or anywhere — it finds the root):
#   pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/stamp-archive-frontmatter.ps1
#
# What it does:
#   For every `.md` file under `docs/archive/lanes/<lane>/` that does NOT
#   already begin with a `---` front-matter block, it PREPENDS a
#   front-matter block (schema in docs/DOCS-CONVENTIONS.md) carrying:
#
#     id:        <lane>-<stable-stem>       (unique + stable; the lane prefix
#                                            namespaces the archive)
#     title:     human title (from the filename stem)
#     tier:      archive
#     kind:      handoff | unit | plan | audit  (from the filename pattern)
#     status:    archived
#     lane:      <lane>                       (the folder name)
#     updated:   2026-11-02                   (the D03 stamp date)
#
#   AND, for files over 60 lines that have no `> **Abstract:**` / `> **TL;DR:**`
#   blockquote in the first 20 lines of their *body*, it inserts a minimal,
#   factual Abstract right after the front-matter (before the first body line).
#   The Abstract states what the file is (kind + unit number, if any) and
#   points at the lane register as the home of the what/why.
#
#   The handoff notes (the `*-handoff-notes.md` files) are left with front-matter
#   only — their Abstracts are hand-authored by D03 (each says what the lane
#   shipped, the one seam, the one don't-re-litigate fact). Running this script
#   over a file that already carries a hand-authored Abstract is a no-op (the
#   "already has an abstract" check catches it).
#
# Idempotency: a file whose first line is `---` is skipped entirely, so a
# second run is a no-op. The `updated:` date is a constant, so the output is
# byte-identical across runs.
#
# Exclusions (same convention as the D02 rewriters): `REDIRECTS.md`, the frozen
# `baseline-report.txt`, and anything under `.serena/`, `.tmp/`, `obj/`, `bin/`.
# None of these live under `docs/archive/lanes/`, but the filter is kept for
# parity and safety.
#
# Doctrine: no here-strings, no inter-command $variables (self-contained, one
# process), $ErrorActionPreference = "Stop", Set-StrictMode.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$stampDate = "2026-11-02"

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
$laneRoot = Join-Path $repo 'docs/archive/lanes'
if (-not (Test-Path $laneRoot)) { throw "lane root not found: $laneRoot" }

# --- classify --------------------------------------------------------------
function Get-Kind([string]$name) {
  if ($name -like '*-handoff-notes.md' -or $name -eq 'handoff-notes.md') { return 'handoff' }
  if ($name -match '^U\d{2}\.md$')                                       { return 'unit' }
  if ($name -match '-u\d+(-exec)?-plan\.md$')                           { return 'unit' }
  if ($name -match '-u\d+(-plan)?\.md$')                                { return 'unit' }
  if ($name -match '^plan-u\d+.*\.md$')                                 { return 'unit' }
  if ($name -match '^plan-.+\.md$')                                     { return 'plan' }
  if ($name -match 'audit\.md$')                                        { return 'audit' }
  return 'note'
}

function Get-UnitNum([string]$name) {
  if ($name -match '(?:^|[-])u?(\d{1,2})(?:[-(].*)?$' -and $name -match 'u\d' -or $name -match '^U\d{2}\.md$') {
    # extract the unit number: prefer the leading U\d\d (improve-style), else u\d\d
    $m = [regex]::Match($name, '(?:^U|[-]u)(\d{1,2})')
    if ($m.Success) { return $m.Groups[1].Value }
  }
  return $null
}

function Get-Title([string]$name) {
  $stem = [System.IO.Path]::GetFileNameWithoutExtension($name)
  # strip a leading "plan-" for plans, keep the rest
  $stem = $stem -replace '^plan-',''
  # replace hyphens and parens with spaces for a readable title, then title-case
  $words = ($stem -replace '[()_-]+',' ').Trim() -split '\s+' | Where-Object { $_ -ne '' }
  $out = @()
  foreach ($w in $words) {
    # keep all-caps tokens (like M3B, ADR) as-is; title-case the rest
    if ($w -cmatch '^[A-Z0-9]+$' -and $w -notmatch '\d{2,}$') { $out += $w }
    else { $out += ($w.Substring(0,1).ToUpper() + $w.Substring(1)) }
  }
  return ($out -join ' ')
}

# --- front-matter + abstract builder ---------------------------------------
function Build-Insertion([string]$lane, [string]$kind, [string]$title, [bool]$bodyHasAbstract, [array]$bodyLines, [int]$totalLines) {
  # front-matter
  $fm = @()
  $fm += '---'
  # id: lane-prefixed, lower-case, hyphenated — unique + stable across the archive
  $id = ($lane + '-' + ($title.ToLowerInvariant() -replace '[^a-z0-9]+','-' -replace '^-|-+$',''))
  $fm += "id: $id"
  $fm += "title: `"$title`""
  $fm += 'tier: archive'
  $fm += "kind: $kind"
  $fm += 'status: archived'
  $fm += "audiences: [developer, agent]"
  $fm += "lane: $lane"
  $fm += "updated: $stampDate"
  $fm += '---'

  $out = $fm
  $out += ''   # blank line after the front-matter

  # abstract (only if the body doesn't already carry one, and the file is >60 lines)
  if (-not $bodyHasAbstract -and $totalLines -gt 60) {
    $abs = Build-Abstract $kind $title
    $out += $abs
    $out += ''
  }
  return ($out -join "`n")
}

function Build-Abstract([string]$kind, [string]$title) {
  switch ($kind) {
    'handoff' {
      # hand-authored later; placeholder that still passes the harness
      @('> **Abstract:** the running handoff note for this lane — the commit-by-commit',
         '> log of what the lane shipped, the seams it created, and the decisions a',
         '> future reader must not re-litigate. See the lane register for the what/why.')
    }
    'plan' {
      @('> **Abstract:** the lane register — what the lane set out to do, why, and how',
         '> it ships, unit by unit. See the handoff note for the running log and the',
         '> design doc / ADRs for the current truth.')
    }
    'unit' {
      @('> **Abstract:** a unit plan in this lane — the step-by-step work for one unit.',
         '> See the lane register for the what/why and the handoff note for the',
         '> running log.')
    }
    'audit' {
      @('> **Abstract:** an audit ledger for this lane — the evidence that the lane''s',
         '> gates were met. See the lane register for the what/why.')
    }
    default {
      @('> **Abstract:** a supporting note in this lane. See the lane register for the',
         '> what/why and the handoff note for the running log.')
    }
  }
}

# --- main ------------------------------------------------------------------
$files = Get-ChildItem -Path $laneRoot -Recurse -Filter '*.md' -File |
  Where-Object {
    $_.Name -ne 'REDIRECTS.md' -and
    $_.FullName -notmatch '[/\\](\.serena|\.tmp|obj|bin)[/\\]'
  }

$stamped = 0
$skippedAlready = 0
$abstractsAdded = 0
foreach ($f in $files) {
  $first = @(Get-Content -LiteralPath $f.FullName -TotalCount 1)
  if ($first.Count -ge 1 -and $first[0].Trim() -eq '---') { $skippedAlready++; continue }

  $lane = Split-Path -Leaf (Split-Path $f.FullName -Parent)
  $kind = Get-Kind $f.Name
  $title = Get-Title $f.Name
  $body = @(Get-Content -LiteralPath $f.FullName)
  $totalLines = $body.Count
  $first20 = @($body | Select-Object -First 20)
  $hasAbstract = @($first20 | Select-String -Pattern '^\s*>\s*\*\*(?:Abstract|TL;DR)').Count -gt 0

  $insertion = Build-Insertion $lane $kind $title $hasAbstract $body $totalLines
  if (-not $hasAbstract -and $totalLines -gt 60) { $abstractsAdded++ }

  $newContent = @($insertion) + $body
  Set-Content -LiteralPath $f.FullName -Value $newContent -Encoding utf8
  $stamped++
}

Write-Host ""
Write-Host "stamp-archive-frontmatter: $stamped stamped, $skippedAlready already had front-matter, $abstractsAdded minimal abstracts inserted."
Write-Host "Re-run is a no-op: files already starting with '---' are skipped."
