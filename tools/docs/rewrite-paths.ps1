# rewrite-paths.ps1 — the D02 archive-move path rewriter
#
# Run from anywhere (finds the repo root by walking up to Kumunita.slnx):
#   pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/rewrite-paths.ps1
#
# What it does:
#   1. rewrites every `plans-milestones/done/<rest>` mention in tracked text
#      files to `archive/lanes/<rest>` (the D02 archive move). The match is
#      the bare substring `plans-milestones/done/` — it therefore catches
#      `docs/…`, `../…`, backtick-wrapped, and prose mentions alike, and it
#      preserves whatever path prefix the reference already had (so a
#      `../../plans-milestones/done/x/` link becomes `../../archive/lanes/x/`).
#   2. regenerates docs/archive/REDIRECTS.md (one row per moved lane folder).
#
# The archive IS rewritten: the plan's D02 says the rewriter covers "the ~556
# doc-path mentions … across docs and code", and that count includes the
# cross-lane references *inside* the moved files. Left un-rewritten, those
# would all be broken links (their targets moved too). The rewrite is a
# mechanical, few-lines-per-file change that rides in the same commit as the
# `git mv`, so `git log --follow` still resolves the original history.
#
# Exclusions (kept out of the rewrite on purpose):
#   - this script itself (its own rule table contains the literal old path);
#   - docs/archive/REDIRECTS.md — the redirect map; its *old-path* column is
#     the whole point, so the token must stay;
#   - docs/plans-milestones/improve-docs/baseline-report.txt — the D00
#     baseline snapshot at commit 61a3c7e; it documents the pre-move tree,
#     so the old path is the *correct* historical record (frozen residual);
#   - the .git / .serena / node_modules / build-junk folders.
#
# Idempotent: a second run finds nothing to rewrite (the source substring no
# longer matches) and re-emits REDIRECTS.md identically.

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

$oldToken = 'plans-milestones/done/'
$newToken = 'archive/lanes/'

# --- excluded dirs (by folder name) -------------------------------------------
$excludeDirs = @('.git', 'node_modules', '.serena', '.tmp', 'test-results',
  'wwwroot', 'bin', 'obj')
# --- excluded files (repo-root-relative) --------------------------------------
$selfPath = 'tools/docs/rewrite-paths.ps1'
$frozen = @('docs/plans-milestones/improve-docs/baseline-report.txt',
  'docs/archive/REDIRECTS.md')

$files = @()
foreach ($top in @('src', 'tests', 'docs', 'e2e')) {
  $root = Join-Path $repo $top
  if (-not (Test-Path $root)) { continue }
  foreach ($f in (Get-ChildItem -Path $root -Recurse -File |
      Where-Object { $d = $_.Directory.Name; ($excludeDirs -notcontains $d) })) {
    $rel = ($f.FullName -replace [regex]::Escape($repo + '/'), '').Replace('\','/')
    if ($rel -eq $selfPath) { continue }
    if (@($frozen | Where-Object { $rel -eq $_ }).Count -gt 0) { continue }
    $files += $f
  }
}
foreach ($topFile in @('README.md', 'AGENTS.md', 'SECURITY.md')) {
  $p = Join-Path $repo $topFile
  if (Test-Path $p) { $files += (Get-Item -LiteralPath $p) }
}
$gi = @(Get-ChildItem -Path (Join-Path $repo '.github') -Recurse -File -ErrorAction SilentlyContinue)
$files += $gi

# --- rewrite ------------------------------------------------------------------
$changedFiles = [System.Collections.Generic.List[string]]::new()
$changedDetail = [System.Collections.Generic.List[string]]::new()
foreach ($f in $files) {
  $content = [System.IO.File]::ReadAllText($f.FullName)
  if (-not $content.Contains($oldToken)) { continue }
  $n = [regex]::Matches($content, [regex]::Escape($oldToken)).Count
  $content = $content.Replace($oldToken, $newToken)
  [System.IO.File]::WriteAllText($f.FullName, $content)
  $rel = ($f.FullName -replace [regex]::Escape($repo + '/'), '').Replace('\','/')
  $changedFiles.Add($rel)
  $changedDetail.Add("  $rel : x$n")
}
Write-Host "rewrite-paths: $($changedFiles.Count) files changed"
foreach ($c in $changedDetail) { Write-Host $c }

# --- regenerate docs/archive/REDIRECTS.md -------------------------------------
$lanesRoot = Join-Path $repo 'docs/archive/lanes'
$lanes = @(Get-ChildItem -Path $lanesRoot -Directory | Sort-Object Name)
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('---')
[void]$sb.AppendLine('id: archive-redirects')
[void]$sb.AppendLine('title: "Archive redirects"')
[void]$sb.AppendLine('tier: archive')
[void]$sb.AppendLine('kind: guide')
[void]$sb.AppendLine('status: current')
[void]$sb.AppendLine('audiences: [developer, agent, platform-maintainer]')
[void]$sb.AppendLine('lane: improve-docs')
[void]$sb.AppendLine('updated: 2026-11-02')
[void]$sb.AppendLine('---')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('> **Abstract:** the old path -> new path map for every lane folder moved out of')
[void]$sb.AppendLine('> `docs/plans-milestones/done/` into `docs/archive/lanes/` by `D02` of the')
[void]$sb.AppendLine('> IMPROVE-DOCS lane (2026-11-02). If a doc, comment or bookmark still points at')
[void]$sb.AppendLine('> `plans-milestones/done/<lane>/…`, its new home is `archive/lanes/<lane>/…`.')
[void]$sb.AppendLine('> **Who is it for:** anyone following an old path.')
[void]$sb.AppendLine('> **When to read the rest:** when a `plans-milestones/done/` link 404s.')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('# Archive redirects (D02, 2026-11-02)')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('The move was a `git mv` of every lane folder, so `git log --follow` still')
[void]$sb.AppendLine('shows the original history for every moved file. The rows below are the path')
[void]$sb.AppendLine('map; each lane folder moves as a unit, so every file in `<lane>/` follows the')
[void]$sb.AppendLine('folder (no per-file rows are needed).')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Old path (former `plans-milestones/done/`) | New path |')
[void]$sb.AppendLine('|---|---|')
foreach ($l in $lanes) {
  [void]$sb.AppendLine("| `docs/plans-milestones/done/$($l.Name)/…` | [`docs/archive/lanes/$($l.Name)/…`](lanes/$($l.Name)/) |")
}
[void]$sb.AppendLine('')
[void]$sb.AppendLine('Notes:')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("- The `improve` lane's audit harness lives at `tools/docs/` (moved out of the")
[void]$sb.AppendLine('  lane folder by D00); the lane''s register, audit ledger and handoff note')
[void]$sb.AppendLine('  followed the folder to `docs/archive/lanes/improve/`.')
[void]$sb.AppendLine('- `docs/plans-milestones/improve-docs/` (this lane''s register) did **not**')
[void]$sb.AppendLine('  move — it is the in-progress lane and stays put.')
[void]$sb.AppendLine('- `docs/plans-milestones/improve-docs/baseline-report.txt` is the D00 baseline')
[void]$sb.AppendLine('  snapshot of the pre-move tree; its `plans-milestones/done/…` rows are the')
[void]$sb.AppendLine('  correct historical record and are intentionally left untouched (a frozen')
[void]$sb.AppendLine('  residual, not a broken link).')
[void]$sb.AppendLine('')
$outPath = Join-Path $repo 'docs/archive/REDIRECTS.md'
[System.IO.File]::WriteAllText($outPath, $sb.ToString())
Write-Host ""
Write-Host "wrote $outPath ($($lanes.Count) lane rows)"
