# fix-stale-top-level.ps1 — D02 follow-up
#
# The archive move rewrites `plans-milestones/done/<lane>/…` mentions. A
# handful of older references point at a file as if it lived directly under
# `done/` (e.g. `done/m3b-handoff-notes.md`) when it actually lived in a lane
# folder (`done/m3b/m3b-handoff-notes.md`). Those never matched the general
# rule and now dangle.
#
# This script finds every `plans-milestones/done/<basename>` (no lane folder
# after `done/`) in tracked docs/code, resolves `<basename>` to its real home
# `archive/lanes/<lane>/<basename>`, and rewrites the mention. Unresolvable
# (file not found anywhere) mentions are reported, not rewritten.
#
# Run: pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/fix-stale-top-level.ps1

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

# build the basename -> lane-folder index from the archive
$index = @{}
$lanesRoot = Join-Path $repo 'docs/archive/lanes'
foreach ($lane in (Get-ChildItem -Path $lanesRoot -Directory)) {
  foreach ($file in (Get-ChildItem -Path $lane.FullName -File)) {
    if (-not $index.ContainsKey($file.Name)) { $index[$file.Name] = @() }
    $index[$file.Name] += $lane.Name
  }
}

# the same scan set as rewrite-paths.ps1
$excludeDirs = @('.git', 'node_modules', '.serena', '.tmp', 'test-results',
  'wwwroot', 'bin', 'obj')
$selfPath = 'tools/docs/fix-stale-top-level.ps1'
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
$files += @(Get-ChildItem -Path (Join-Path $repo '.github') -Recurse -File -ErrorAction SilentlyContinue)

# Find every `archive/lanes/<basename.md>` mention. A mention is *dangling*
# when `<basename>` sits directly under `lanes/` (the next char after the
# match is NOT `/`); it is *legitimate* when a lane folder follows (the next
# char IS `/`) — those we leave alone.
$pattern = [regex]'archive/lanes/([A-Za-z0-9][A-Za-z0-9._-]*\.md)'
$fixed = [System.Collections.Generic.List[string]]::new()
$unresolved = [System.Collections.Generic.List[string]]::new()
foreach ($f in $files) {
  $content = [System.IO.File]::ReadAllText($f.FullName)
  $rel = ($f.FullName -replace [regex]::Escape($repo + '/'), '').Replace('\','/')
  $ms = $pattern.Matches($content)
  if ($ms.Count -eq 0) { continue }
  $changed = $false
  foreach ($m in $ms) {
    # if the char right after the match is '/', this is the legitimate
    # lanes/<lane>/<file> form — skip it
    if ($m.Index + $m.Length -lt $content.Length -and
        $content[$m.Index + $m.Length] -eq '/') { continue }
    $base = $m.Groups[1].Value
    if (-not $index.ContainsKey($base)) {
      $unresolved.Add("$rel : `archive/lanes/$base` (no such file in any lane folder)")
      continue
    }
    $lanes = @($index[$base])
    if ($lanes.Count -eq 0) {
      $unresolved.Add("$rel : `archive/lanes/$base` (ambiguous: 0 lanes)")
      continue
    }
    $old = "archive/lanes/$base"
    $new = "archive/lanes/$($lanes[0])/$base"
    if ($content.Contains($old)) {
      $content = $content.Replace($old, $new)
      $changed = $true
      $fixed.Add("$rel : $old -> $new")
    }
  }
  if ($changed) { [System.IO.File]::WriteAllText($f.FullName, $content) }
}
Write-Host "fix-stale-top-level: $($fixed.Count) mentions fixed"
foreach ($x in $fixed) { Write-Host "  $x" }
Write-Host ""
Write-Host "unresolved (reported, not rewritten): $($unresolved.Count)"
foreach ($x in $unresolved) { Write-Host "  $x" }
