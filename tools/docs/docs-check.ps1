# docs-check.ps1 — the IMPROVE-DOCS lane's CI gate (D14)
#
# Run:  pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/docs-check.ps1
# Exit: 0 when every gate passes, non-zero otherwise. Intended to be run from
#       the repo root; if the CWD is not the repo root, it walks up looking
#       for Kumunita.slnx.
#
# This is the *close* gate: baselines retired, not grandfathered (the plan's
# D14 "retire the baselines, don't grandfather them"). It enforces the six
# rules the D00–D13 work landed:
#   (1) front-matter present on every doc
#   (2) an Abstract on every non-ADR doc over 60 lines (ADR is the cap, not
#       the abstract — see gate 3)
#   (3) ADR length ≤250 lines (the ADR cap; ADRs carry Status/Context/Decision/
#       Consequences, not an Abstract)
#   (4) zero broken relative .md links
#   (5) every living doc reachable from an entry page (no living orphans)
#   (6) nothing new under docs/archive/ except at a lane close (the archive is
#       frozen — every archive doc is stamped tier:archive and none is
#       tier:living|reference)
#
# Scope: docs/** + the three repo-root docs (README.md, AGENTS.md,
# SECURITY.md), excluding node_modules/ and wwwroot/ (third-party, not the
# docs layer). This matches docs-report.ps1's "doc set" (the D00 baseline is
# computed over the same set, so the two agree).
#
# Style: matches improve-check.ps1 — $ErrorActionPreference = "Stop",
# Set-StrictMode -Version Latest, Find-RepoRoot walking up to Kumunita.slnx,
# Rel() for repo-relative paths, [OK]/[FAIL] colour coding, exit 0/1.

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
$failures = [System.Collections.Generic.List[string]]::new()

function Rel([string]$path) {
  $p = $path -replace '\\','/'
  $rp = $repo -replace '\\','/'
  if ($p.StartsWith($rp)) { return $p.Substring($rp.Length).TrimStart('/') }
  return $p
}
function Lines([string]$path) { @((Get-Content -LiteralPath $path)).Count }

# End-of-front-matter line (0-indexed), or -1 if the file has none.
function FmEnd([string]$path) {
  $lines = @(Get-Content -LiteralPath $path)
  if ($lines.Count -ge 2 -and $lines[0].Trim() -eq '---') {
    for ($i = 1; $i -lt [Math]::Min($lines.Count, 40); $i++) { if ($lines[$i].Trim() -eq '---') { return $i } }
  }
  return -1
}
function HasFm([string]$path) { $e = FmEnd $path; $e -ge 0 }

# True iff an Abstract/TL;DR blockquote sits within $window lines after the
# front-matter (DOCS-CONVENTIONS.md: "in its first 15 lines (after the
# front-matter)"); for front-matter-less files the window starts at line 0.
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
  if ($cond) { Write-Host "[OK]   $gate : $message" }
  else { Write-Host "[FAIL] $gate : $message" -ForegroundColor Red; $failures.Add("$gate : $message") }
}

# --- the doc set: docs/** + the three repo-root docs -------------------------
$all = Get-ChildItem -Path (Join-Path $repo 'docs') -Recurse -Filter '*.md' -File | ForEach-Object { $_.FullName }
foreach ($t in @('README.md', 'AGENTS.md', 'SECURITY.md')) {
  $p = Join-Path $repo $t
  if (Test-Path $p) { $all += $p }
}
$all = @($all | Where-Object { $_ -notmatch 'node_modules|wwwroot' } | Sort-Object -Unique)
$relAll = [System.Collections.Generic.Dictionary[string,string]]::new()   # repo-relative -> full
foreach ($d in $all) { $relAll[(Rel $d)] = $d }

# =============================================================================
# Gate (1) — front-matter present on every doc
# =============================================================================
$noFm = [System.Collections.Generic.List[string]]::new()
foreach ($d in $all) {
  $fm = FmEnd $d
  if ($fm -lt 0) { $noFm.Add((Rel $d)) }
}
Check "1" "front-matter on every doc (missing: $($noFm.Count))" ($noFm.Count -eq 0)
foreach ($v in $noFm) { Write-Host "       - $v" }

# =============================================================================
# Gate (2) — Abstract on every non-ADR doc over 60 lines
# =============================================================================
$noAbs = [System.Collections.Generic.List[string]]::new()
foreach ($d in $all) {
  $rel = Rel $d
  if ($rel -match '^docs/archive/') { continue }    # the archive is frozen; its own rule is gate 6
  if ($rel -match '^docs/adr/') { continue }        # ADRs are cap-governed (gate 3), not abstract-governed
  if ((Lines $d) -le 60) { continue }
  $has = HasAbstract $d 15
  if (-not $has) { $noAbs.Add("$rel (line $((Lines $d)))") }
}
Check "2" "Abstract on every non-ADR doc >60 lines (missing: $($noAbs.Count))" ($noAbs.Count -eq 0)
foreach ($v in $noAbs) { Write-Host "       - $v" }

# =============================================================================
# Gate (3) — ADR length ≤250 lines
# =============================================================================
$adrOver = [System.Collections.Generic.List[string]]::new()
$adrFiles = Get-ChildItem -Path (Join-Path $repo 'docs/adr') -Filter '0*.md' -File
foreach ($a in $adrFiles) {
  $n = Lines $a.FullName
  if ($n -gt 250) { $adrOver.Add("$($a.Name) ($n lines)") }
}
Check "3" "ADR ≤250 lines (over: $($adrOver.Count))" ($adrOver.Count -eq 0)
foreach ($v in $adrOver) { Write-Host "       - $v" }

# =============================================================================
# Gate (4) — zero broken relative .md links
# =============================================================================
$linkRegex = [regex]'\[([^\]]*)\]\(([^)]+)\)'
$broken = [System.Collections.Generic.List[string]]::new()
foreach ($d in $all) {
  $content = Get-Content -LiteralPath $d -Raw
  if (-not $content) { continue }
  foreach ($m in $linkRegex.Matches($content)) {
    $target = $m.Groups[2].Value.Trim()
    $hashIdx = $target.IndexOf('#')
    if ($hashIdx -ge 0) { $target = $target.Substring(0, $hashIdx) }
    if ($target -notmatch '\.md$') { continue }
    if ($target -match '^(?:https?://|//)') { continue }
    if ($target.StartsWith('/')) { continue }
    $srcDir = Split-Path $d -Parent
    $resolved = [System.IO.Path]::GetFullPath((Join-Path $srcDir $target))
    if (-not (Test-Path -LiteralPath $resolved)) { $broken.Add("$(Rel $d)  ->  $target") }
  }
}
Check "4" "zero broken relative .md links (broken: $($broken.Count))" ($broken.Count -eq 0)
foreach ($v in $broken) { Write-Host "       - $v" }

# =============================================================================
# Gate (5) — every living doc reachable from an entry page (no living orphans)
# =============================================================================
# Build the doc -> reachable-doc adjacency from relative .md links (the same
# link set gate 4 walks), then BFS from the docs/start/ entry pages. A
# tier:living doc that no path from an entry page reaches is a living orphan.
$adj = @{}
foreach ($d in $all) {
  $content = Get-Content -LiteralPath $d -Raw
  if (-not $content) { $adj[$d] = @(); continue }
  $targets = [System.Collections.Generic.List[string]]::new()
  foreach ($m in $linkRegex.Matches($content)) {
    $target = $m.Groups[2].Value.Trim()
    $hashIdx = $target.IndexOf('#')
    if ($hashIdx -ge 0) { $target = $target.Substring(0, $hashIdx) }
    if ($target -notmatch '\.md$') { continue }
    if ($target -match '^(?:https?://|//)') { continue }
    if ($target.StartsWith('/')) { continue }
    $srcDir = Split-Path $d -Parent
    $resolved = [System.IO.Path]::GetFullPath((Join-Path $srcDir $target))
    $r = Rel $resolved
    if ($relAll.ContainsKey($r)) { $targets += $relAll[$r] }
  }
  $adj[$d] = @($targets)
}
$entryPages = @($all | Where-Object { (Rel $_) -match '^docs/start/' })
$reachable = @{}
$q = [System.Collections.Generic.Queue[string]]::new()
foreach ($e in $entryPages) { $reachable[$e] = $true; $q.Enqueue($e) }
while ($q.Count -gt 0) {
  $cur = $q.Dequeue()
  foreach ($nxt in $adj[$cur]) { if (-not $reachable.ContainsKey($nxt)) { $reachable[$nxt] = $true; $q.Enqueue($nxt) } }
}
$livingOrphans = [System.Collections.Generic.List[string]]::new()
foreach ($d in $all) {
  $rel = Rel $d
  if ($rel -match '^docs/archive/') { continue }
  $first = Get-Content -LiteralPath $d -TotalCount 20
  $isLiving = @($first | Where-Object { $_ -match '^\s*tier:\s*living\b' }).Count -gt 0
  if (-not $isLiving) { continue }
  if (-not $reachable.ContainsKey($d)) { $livingOrphans.Add($rel) }
}
Check "5" "every living doc reachable from an entry page (orphans: $($livingOrphans.Count))" ($livingOrphans.Count -eq 0)
foreach ($v in $livingOrphans) { Write-Host "       - $v" }

# =============================================================================
# Gate (6) — the archive is frozen (nothing new/active under docs/archive/)
# =============================================================================
# Two conditions: (a) every archive doc is stamped tier:archive, and (b) no
# archive doc carries tier:living|reference (an active doc that has drifted
# into the frozen tier). A lane close stamps new files tier:archive in the
# same commit, so a clean tree always satisfies both.
$archActive = [System.Collections.Generic.List[string]]::new()
$archFiles = Get-ChildItem -Path (Join-Path $repo 'docs/archive') -Recurse -Filter '*.md' -File -ErrorAction SilentlyContinue
foreach ($f in $archFiles) {
  $first = Get-Content -LiteralPath $f.FullName -TotalCount 20
  $tierArchive = @($first | Where-Object { $_ -match '^\s*tier:\s*archive\b' }).Count -gt 0
  $livingRef = @($first | Where-Object { $_ -match '^\s*tier:\s*(living|reference)\b' }).Count -gt 0
  if ($livingRef) { $archActive.Add("$((Rel $f.FullName)) (tier: living|reference)") }
  elseif (-not $tierArchive) { $archActive.Add("$((Rel $f.FullName)) (not stamped tier:archive)") }
}
Check "6" "archive frozen — every archive doc tier:archive, none active (violations: $($archActive.Count))" ($archActive.Count -eq 0)
foreach ($v in $archActive) { Write-Host "       - $v" }

# =============================================================================
# Summary
# =============================================================================
Write-Host ""
if ($failures.Count -eq 0) {
  Write-Host "docs-check: ALL GATES PASS" -ForegroundColor Green
  exit 0
} else {
  Write-Host "docs-check: $($failures.Count) GATE(S) FAILED" -ForegroundColor Red
  exit 1
}
