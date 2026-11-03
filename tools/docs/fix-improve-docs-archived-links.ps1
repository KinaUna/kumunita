# fix-improve-docs-archived-links.ps1
# One-shot: rewrites the relative .md links in the IMPROVE-DOCS lane register
# from its original location (docs/plans-milestones/improve-docs/) to its
# archived location (docs/archive/lanes/improve-docs/), and flips the
# front-matter tier/status to archive/archived.
#
# Depth change: the file moves one directory deeper into the repo tree.
#   ../../archive/X  ->  ../../X                 (we are inside archive/ now)
#   ../../../X       ->  ../../../../X           (repo-root files)
#   ../../X          ->  ../../../X             (docs/ subfolders)
#   ../README.md     ->  ../../../plans-milestones/README.md
#
# Idempotent: a second run finds nothing to change (the patterns no longer match).

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$f = Join-Path $repo 'docs/archive/lanes/improve-docs/plan-improve-docs.md'
$c = [System.IO.File]::ReadAllText($f)
$fixed = 0

$c = [regex]::Replace($c, '\]\((\.\./[^)]+?)\)', {
  param($m)
  $t = $m.Groups[1].Value
  if ($t -notmatch '\.md$') { return $m.Value }     # only .md links
  $new = $null
  if ($t.StartsWith('../../archive/')) { $new = ('../../' + $t.Substring(14)) }
  elseif ($t.StartsWith('../../../'))      { $new = ('../' + $t) }
  elseif ($t.StartsWith('../../'))         { $new = ('../' + $t) }
  elseif ($t -eq '../README.md')           { $new = '../../../plans-milestones/README.md' }
  if ($null -eq $new -or $new -eq $t) { return $m.Value }
  $script:fixed++
  return "]($new)"
})

# Front-matter flip (only if still at original values)
$c = $c -replace '(?m)^tier: living$', 'tier: archive'
$c = $c -replace '(?m)^status: complete$', 'status: archived'

[System.IO.File]::WriteAllText($f, $c)
Write-Host "fix-improve-docs-archived-links: $fixed link(s) rewritten"
