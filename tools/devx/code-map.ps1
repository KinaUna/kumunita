# code-map.ps1 — the IMPROVE-DEVX lane's code-map generator (D4, ADR 0307)
#
# Run:   pwsh tools/devx/code-map.ps1          # write docs/dev/code-map.md
#        pwsh tools/devx/code-map.ps1 -Check   # regenerate in memory, diff, exit 0/1
#
# One row per bounded context: a Kumunita.Core folder that has its own
# DependencyInjection.cs (the C10 module self-registration seam). The
# composition-root aggregator (src/Kumunita.Core) and the Announcements
# feature module are excluded BY RULE (AGENTS.md) — not by hand-picking a file
# list — so a newly added context is picked up automatically.
#
# Generated text only: every cell is derived from the code. The `updated:`
# front-matter date is a constant (not Get-Date) so -Check stays byte-stable
# across days; regenerate after a code change to refresh the content.
#
# Style: matches tools/docs/docs-check.ps1 + tools/devx/devx-check.ps1.

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
$esc = [regex]::Escape($repo + '/')
function Rel([string]$p) { return $p -replace $esc, '' -replace '\\', '/' }
function JoinList([object[]]$items) { return ((@($items) | Sort-Object -Unique) -join ', ') }
function Norm([string]$s) { return ($s -replace "`r`n", "`n") }

$core = 'src/Kumunita.Core'
$viewsRoot = 'src/Kumunita.Web/Views'
$seamRe = [regex]"\bAdd(?:Transient|Scoped|Singleton)\s*<\s*([\w.]+)"
$adrRe = [regex]"\bADR[\s:\-]*(\d{3,4})\b|\badr/(\d{3,4})\b"

# --- the contexts (30 rows) ------------------------------------------------------
# A context folder = one with its own DependencyInjection.cs, minus the two
# excluded folders above (the rule is a set, so a third context is not a trap).
$excludedDirs = @{ 'src/Kumunita.Core' = $true; 'src/Kumunita.Core/Announcements' = $true }
$contexts = @()
foreach ($d in (Get-ChildItem -Path (Join-Path $repo $core) -Filter 'DependencyInjection.cs' -File -Recurse |
    Where-Object { $_.FullName -notmatch '/obj/|/bin/' })) {
  $dir = Rel $d.Directory.FullName
  if (-not $excludedDirs.ContainsKey($dir)) { $contexts += $dir }
}
$contexts = @($contexts | Sort-Object)

# --- the Web controllers (name -> text) -----------------------------------------
$ctrlNames = [System.Collections.Generic.List[string]]::new()
$ctrlTexts = @{}
foreach ($c in (Get-ChildItem -Path (Join-Path $repo 'src/Kumunita.Web/Controllers') -Filter '*.cs' -File)) {
  $ctrlNames.Add($c.Name); $ctrlTexts[$c.Name] = Get-Content -LiteralPath $c.FullName -Raw
}

# --- the tests (name -> text) -----------------------------------------------------
$testNames = [System.Collections.Generic.List[string]]::new()
$testTexts = @{}
foreach ($tproj in @('tests/Kumunita.Core.Tests', 'tests/Kumunita.Web.Tests')) {
  foreach ($t in (Get-ChildItem -Path (Join-Path $repo $tproj) -Filter '*.cs' -File -Recurse |
    Where-Object { $_.FullName -notmatch '/obj/|/bin/' })) {
    $testNames.Add($t.Name); $testTexts[$t.Name] = Get-Content -LiteralPath $t.FullName -Raw
  }
}

# --- the *DocTypes.cs set (in-folder, or a root file whose stem names the ctx) ----
$dtFiles = @(Get-ChildItem -Path (Join-Path $repo $core) -Filter '*DocTypes.cs' -File -Recurse |
  Where-Object { $_.FullName -notmatch '/obj/|/bin/' })
function DtMatches([string]$stem, [string]$ctxName) {
  return ($stem -eq $ctxName) -or ($stem -eq ($ctxName + 's')) -or (($stem + 's') -eq $ctxName)
}

# --- helpers per row ---------------------------------------------------------------
function MatchFiles([object[]]$seams, [object[]]$names, $texts) {
  $hits = [System.Collections.Generic.List[string]]::new()
  foreach ($n in $names) {
    $t = $texts[$n]
    foreach ($s in $seams) { if ($t -match ('(?i)\b' + [regex]::Escape([string]$s) + '\b')) { $hits.Add([string]$n); break } }
  }
  return (@($hits) | Sort-Object -Unique)
}

function TopAdrs([string[]]$folderCs) {
  $counts = @{}
  foreach ($f in $folderCs) {
    foreach ($m in $adrRe.Matches((Get-Content -LiteralPath $f -Raw))) {
      $num = $m.Groups[1].Value; if ([string]::IsNullOrEmpty($num)) { $num = $m.Groups[2].Value }
      if ($num) { $counts[$num] = 1 + $counts[$num] }
    }
  }
  return (@($counts.GetEnumerator() | Sort-Object -Property @{ e = 'Value'; descending = $true }, 'Key' |
    Select-Object -First 3 | ForEach-Object { $_.Key }) -join ', ')
}

# --- build the rows ---------------------------------------------------------------
$rows = [System.Collections.Generic.List[string]]::new()
foreach ($c in $contexts) {
  $ctxName = $c.Split('/')[-1]
  $diText = Get-Content -LiteralPath (Join-Path $repo $c 'DependencyInjection.cs') -Raw

  # col 3: the DI-registered public seam(s) — the context's exposed surface.
  $seams = @($seamRe.Matches($diText) | ForEach-Object { $_.Groups[1].Value.Split('.')[-1] } | Sort-Object -Unique)

  # col 2: *DocTypes.cs — in-folder, or a root file whose stem names the context.
  $dts = [System.Collections.Generic.List[string]]::new()
  foreach ($f in $dtFiles) {
    $fd = Rel $f.Directory.FullName
    $stem = $f.BaseName -replace 'DocTypes$', ''
    if ($fd -eq $c) { $dts.Add($f.Name) }
    elseif ($fd -eq $core -and (DtMatches $stem $ctxName)) { $dts.Add($f.Name) }
  }

  # col 4 / col 6: controllers / tests referencing any of the context's seams.
  $ctlHit = @(MatchFiles $seams $ctrlNames $ctrlTexts)
  $testHit = @(MatchFiles $seams $testNames $testTexts)

  # col 5: view folders that correspond to the matched controllers (name -> Views/<name>/).
  $views = [System.Collections.Generic.List[string]]::new()
  foreach ($ct in $ctlHit) {
    $base = (($ct -replace '\.cs$', '').Split('.')[0]) -replace 'Controller$', ''
    if ($base -and (Test-Path (Join-Path $repo "$viewsRoot/$base"))) { $views.Add($base) }
  }

  # col 7: top 3 ADR numbers cited in the folder's .cs (numbers, not links).
  $folderCs = @(Get-ChildItem -Path (Join-Path $repo $c) -Filter '*.cs' -File | ForEach-Object { $_.FullName } | Sort-Object)
  $adrs = TopAdrs $folderCs

  $rows.Add('| ' + $ctxName + ' | ' + (JoinList $dts) + ' | ' + (JoinList $seams) + ' | ' +
    (JoinList $ctlHit) + ' | ' + (JoinList $views) + ' | ' + (JoinList $testHit) + ' | ' + $adrs + ' |')
}

# --- the document ------------------------------------------------------------------
$doc = [System.Collections.Generic.List[string]]::new()
$doc.Add('---')
$doc.Add('id: dev-code-map')
$doc.Add('title: "Code map — one row per bounded context"')
$doc.Add('tier: reference')
$doc.Add('kind: guide')
$doc.Add('status: current')
$doc.Add('audiences: [developer, agent]')
$doc.Add('lane: improve-devx')
$doc.Add('updated: 2026-11-15')
$doc.Add('---')
$doc.Add('')
$doc.Add('> **Abstract:** the code map (D4, ADR 0307) — one row per bounded context,')
$doc.Add('> linking the concern to the Core folder, its `*DocTypes.cs`, its DI-registered')
$doc.Add('> service seam, the Web controllers and view folders that serve it, the test files')
$doc.Add('> that cover it, and the top 3 ADRs the folder cites. Generated from the code; the')
$doc.Add('> devx `code-map` rule fails the moment it goes stale.')
$doc.Add('')
$doc.Add('# Code map')
$doc.Add('')
$doc.Add('> generated by `tools/devx/code-map.ps1` — do not hand-edit.')
$doc.Add('')
$doc.Add('One row per bounded context: a `Kumunita.Core` folder with its own `DependencyInjection.cs`.')
$doc.Add('The composition-root aggregator (`src/Kumunita.Core`) and the `Announcements` feature')
$doc.Add('module are excluded (by rule). Every cell is derived from the code; the ADR column is')
$doc.Add('a list of the most-cited numbers in the folder, not links.')
$doc.Add('')
$doc.Add('| Context | *DocTypes.cs | I*Service | Controllers | Views | Tests | Top ADRs |')
$doc.Add('|---|---|---|---|---|---|---|')
foreach ($r in $rows) { $doc.Add($r) }
$doc.Add('')
$markdown = ($doc -join "`n") + "`n"

# --- emit ---------------------------------------------------------------------------
$outPath = Join-Path $repo 'docs/dev/code-map.md'
if ($Check) {
  if (-not (Test-Path $outPath)) {
    Write-Host "[FAIL] code-map : docs/dev/code-map.md does not exist — run `code-map.ps1` to generate it" -ForegroundColor Red
    exit 1
  }
  if ((Norm (Get-Content -LiteralPath $outPath -Raw)) -eq (Norm $markdown)) {
    Write-Host "[OK]   code-map : docs/dev/code-map.md is fresh ($($contexts.Count) contexts)"
    exit 0
  }
  Write-Host "[FAIL] code-map : docs/dev/code-map.md is stale — run `pwsh tools/devx/code-map.ps1` to regenerate" -ForegroundColor Red
  exit 1
}
[System.IO.File]::WriteAllText($outPath, $markdown)
Write-Host "[OK]   code-map : wrote docs/dev/code-map.md ($($contexts.Count) contexts)"
