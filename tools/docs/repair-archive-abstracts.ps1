# repair-archive-abstracts.ps1 — D03 of the IMPROVE-DOCS lane (companion to
# stamp-archive-frontmatter.ps1)
#
# Run:
#   pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/repair-archive-abstracts.ps1
#
# Why this exists: the stamper decides whether to add a minimal Abstract by
# testing the ORIGINAL body against the 60-line rule. A file whose body was
# 49–59 lines got no abstract at stamp time, but the 12-line front-matter
# prepend pushes its *final* length past 60, so the harness's own 60-line
# rule then requires one. This script closes that gap.
#
# For every `.md` under docs/archive/lanes/ that is (a) over 60 lines and (b)
# has no `> **Abstract:**` / `> **TL;DR:**` blockquote in its first 20 lines,
# it inserts an Abstract right after the front-matter (before the first body
# line):
#   - if the body already carries a TL;DR/Abstract blockquote (it got pushed
#     down by the prepend), that block is RELOCATED up — the author's own
#     words win over a generated one;
#   - otherwise a minimal kind-appropriate Abstract is inserted (unit/plan/
#     handoff), matching the stamper's text.
#
# Idempotency: a file that already has an Abstract in its first 20 lines is
# skipped, so a second run is a no-op. The stamper and this script never touch
# the same file's body otherwise.

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

function Get-MinimalAbstract([string]$kind) {
  switch ($kind) {
    'handoff' { return @(
      '> **Abstract:** the running handoff note for this lane — the commit-by-commit',
      '> log of what the lane shipped, the seams it created, and the decisions a',
      '> future reader must not re-litigate. See the lane register for the what/why.') }
    'plan'    { return @(
      '> **Abstract:** the lane register — what the lane set out to do, why, and how',
      '> it ships, unit by unit. See the handoff note for the running log and the',
      '> design doc / ADRs for the current truth.') }
    'unit'    { return @(
      '> **Abstract:** a unit plan in this lane — the step-by-step work for one unit.',
      '> See the lane register for the what/why and the handoff note for the',
      '> running log.') }
    'audit'   { return @(
      '> **Abstract:** an audit ledger for this lane — the evidence that the lane''s',
      '> gates were met. See the lane register for the what/why.') }
    default   { return @(
      '> **Abstract:** a supporting note in this lane. See the lane register for the',
      '> what/why and the handoff note for the running log.') }
  }
}

$files = Get-ChildItem -Path $laneRoot -Recurse -Filter '*.md' -File |
  Where-Object { $_.Name -ne 'REDIRECTS.md' -and $_.FullName -notmatch '[/\\](\.serena|\.tmp|obj|bin)[/\\]' }

$repaired = 0
$relocated = 0
foreach ($f in $files) {
  $body = @(Get-Content -LiteralPath $f.FullName)
  if ($body.Count -le 60) { continue }
  $first20 = @($body | Select-Object -First 20)
  if (@($first20 | Select-String -Pattern '^\s*>\s*\*\*(?:Abstract|TL;DR)').Count -gt 0) { continue }

  # find the closing '---' of the front-matter (the 2nd '---' line)
  $closeIdx = -1
  $seen = 0
  for ($i = 0; $i -lt $body.Count; $i++) {
    if ($body[$i].Trim() -eq '---') { $seen++; if ($seen -eq 2) { $closeIdx = $i; break } }
  }
  if ($closeIdx -lt 0) { continue }  # no front-matter — the stamper should have added one
  # insert position: right after the closing '---' and the blank line that follows it
  $insIdx = $closeIdx + 1
  if ($insIdx -lt $body.Count -and $body[$insIdx].Trim() -eq '') { $insIdx++ }

  $kind = Get-Kind $f.Name

  # look for a body TL;DR/Abstract blockquote to relocate (consecutive '>' lines
  # starting with a **TL;DR:** or **Abstract:** marker)
  $absStart = -1
  for ($i = $insIdx; $i -lt $body.Count; $i++) {
    if ($body[$i] -match '^\s*>\s*\*\*(?:Abstract|TL;DR)') { $absStart = $i; break }
  }

  if ($absStart -ge 0) {
    # take the full consecutive '>' block
    $absEnd = $absStart
    while ($absEnd + 1 -lt $body.Count -and $body[$absEnd + 1] -match '^\s*>') { $absEnd++ }
    $insertLines = @($body[$absStart..$absEnd])
    $rest = @()
    $rest += @($body[0..($insIdx-1)])
    $rest += @('')
    $rest += $insertLines
    $rest += @('')
    if ($absEnd + 1 -lt $body.Count) { $rest += @($body[($absEnd+1)..($body.Count-1)]) }
    $relocated++
  } else {
    $insertLines = Get-MinimalAbstract $kind
    $rest = @()
    $rest += @($body[0..($insIdx-1)])
    $rest += $insertLines
    $rest += @('')
    $rest += @($body[$insIdx..($body.Count-1)])
    $repaired++
  }

  Set-Content -LiteralPath $f.FullName -Value $rest -Encoding utf8
}

Write-Host ""
Write-Host "repair-archive-abstracts: $repaired minimal inserted, $relocated body abstracts relocated."
Write-Host "Re-run is a no-op: files already having an Abstract in the first 20 lines are skipped."
