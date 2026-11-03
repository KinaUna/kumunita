# linkify.ps1 — the D12 linking pass
#
# Run from anywhere (finds the repo root by walking up to Kumunita.slnx):
#   dry run (default, reports what would change):
#     pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/linkify.ps1
#   apply the edits:
#     pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/linkify.ps1 -Apply
#
# What it does (the D12 "Linking pass" unit):
#   1. In every *living* doc body, converts a prose `ADR NNNN` reference into
#      a real relative link — `ADR [0005](<rel>/0005-….md)` — pointing at the
#      ADR file that owns that number. The `NNNN` is the stable key; the
#      display text keeps the `ADR NNNN` reading. Sub-refs like `ADR 0006-D`
#      keep their `-D` suffix (only the base number links). Ranges like
#      `ADR 0179-0181` and already-linked `[ADR …](…)` are left alone.
#   2. Converts a backtick code span whose text is an existing doc path
#      (`` `docs/ops/backups.md` ``, `` `OPS.md` ``, …) into a real relative
#      link with backticked display text — the repo convention:
#      `` `docs/ops/backups.md` `` -> [`docs/ops/backups.md`](ops/backups.md).
#      A token that does not resolve to an existing file is left as a code
#      span — the script never introduces a broken link.
#   3. Fixes the 3 pre-existing broken links (relative-depth errors; the
#      targets all exist — only the href depth is wrong).
#
# Neither rule fires on a token that is already the *text* of a markdown link:
# every `[text](href)` / `![alt](src)` on a line is masked out first, the
# rules run, then the masks are restored (e.g. [`docs/x.md`](x) is a valid link
# and stays as-is, not nested).
#
# Both linkify rules are idempotent: a second run finds nothing to change.
#
# Excluded on purpose:
#   - docs/archive/** — the frozen lane history (D09 left its refs alone);
#   - docs/adr/README.md — the generated ADR index (IMPROVE gate `b` pins its
#     row count; it is a list, not a body);
#   - docs/design/adr-handoffs.md — the D11 destination; its `## ADR NNNN`
#     headings are the *key* for each section and must stay plain text;
#   - the front-matter of every file (the `related:` list is YAML paths, not
#     markdown links — linking them would break the YAML);
#   - fenced code blocks (``` … ```);
#   - this script itself, and any non-.md file.
#
# Performance: the ADR / mask / restore helpers use `.Matches()` + `Substring`
# splicing (not `[regex].Replace(…, {…})` — a scriptblock replacement is far
# too slow in PowerShell across ~300 k doc lines, and a nested command
# invocation inside the block mis-parses as a method call). Lines that contain
# neither `ADR` nor a backtick are skipped outright.

param([switch]$Apply)

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
$repo = (Find-RepoRoot).TrimEnd('/')

# --- excluded dirs (by folder name) -------------------------------------------
$excludeDirs = @('.git', 'node_modules', '.serena', '.tmp', 'test-results',
  'wwwroot', 'bin', 'obj')
# --- excluded files (repo-root-relative) --------------------------------------
$selfPath = 'tools/docs/linkify.ps1'
$frozen = @(
  'docs/adr/README.md',          # generated index (gate b)
  'docs/design/adr-handoffs.md'  # ## ADR NNNN = structural key
)

# --- gather the living .md files ---------------------------------------------
$files = @()
foreach ($f in (Get-ChildItem -Path $repo -Recurse -File -Filter '*.md')) {
  $rel = ($f.FullName -replace [regex]::Escape($repo + '/'), '').Replace('\', '/')
  if ($rel -eq $selfPath) { continue }
  if ($rel -like 'docs/archive/*') { continue }
  if (@($frozen | Where-Object { $rel -eq $_ }).Count -gt 0) { continue }
  $skip = $false
  foreach ($d in ($f.DirectoryName -split '/')) { if ($excludeDirs -contains $d) { $skip = $true; break } }
  if ($skip) { continue }
  $files += $f
}

# --- build the ADR number -> repo-relative path map ---------------------------
$adrMap = @{}
$adrDir = Join-Path $repo 'docs/adr'
if (Test-Path $adrDir) {
  foreach ($a in (Get-ChildItem -Path $adrDir -File -Filter '*.md')) {
    if ($a.Name -match '^(\d{4})-') { $adrMap[$Matches[1]] = "docs/adr/" + $a.Name }
  }
}

# ADR prose pattern: a standalone `ADR NNNN` (not in a link, not a range, not a
# longer number, not part of a longer word).
$adrPattern = [regex]'(?<![\w\[])ADR (\d{4})(?!\d)(?!-\d)'
# An existing markdown link or image: `[text](href)` / `![alt](src)`. Masked
# out before the rules run, then restored.
$linkPattern = [regex]'(!?)\[[^\[\]]*\]\([^)]*\)'
# The mask sentinel: the SOH control char (0x01), which will not appear in any
# doc body. Build it via [char]1 — the PowerShell escape "`x01" is NOT a hex
# escape (it yields the literal ASCII "x01"), so the mask and the restore must
# both use [char]1 explicitly or they will not agree.
$soh = [string][char]1
# The mask placeholder: <SOH>L<n><SOH>
$maskPattern = [regex]([regex]::Escape($soh) + 'L(\d+)' + [regex]::Escape($soh))

# --- helpers -----------------------------------------------------------------
function ConvertTo-RelPath([string]$fromFile, [string]$to) {
  # Relative path from the *directory containing* $fromFile to $to. (Passing
  # the file itself as the base would make .NET treat it as "inside the file's
  # folder" and add one spurious `../` — a sibling would come out as
  # `../sibling.md` instead of `sibling.md`.)
  $fromDir = Split-Path $fromFile -Parent
  return ([System.IO.Path]::GetRelativePath($fromDir, $to)).Replace('\', '/')
}

function Test-Resolves([string]$srcFile, [string]$token) {
  # returns the repo-root-relative target path if $token (a doc path) resolves,
  # else $null. Cascade: source-relative -> repo-root-qualified -> docs/-bare.
  $srcDir = Split-Path $srcFile -Parent
  $candidates = @()
  if ($token -match '^[./]') { $candidates += (Join-Path $srcDir $token) }
  $candidates += (Join-Path $repo $token)
  if ($token -notmatch '^/') { $candidates += (Join-Path (Join-Path $repo 'docs') $token) }
  foreach ($c in $candidates) {
    if (Test-Path -LiteralPath $c) {
      return (($c -replace '\\', '/') -replace [regex]::Escape($repo + '/'), '')
    }
  }
  return $null
}

# Replace each existing markdown link/image with a `\x01L<n>\x01` placeholder,
# recording the originals in $script:savedLinks (manual splice, fast).
function Mask-Links([string]$s) {
  if (-not $s.Contains('](')) { return $s }
  $ms = $linkPattern.Matches($s)
  if ($ms.Count -eq 0) { return $s }
  $sb = [System.Text.StringBuilder]::new()
  $prev = 0
  foreach ($m in $ms) {
    [void]$sb.Append($s.Substring($prev, $m.Index - $prev))
    $script:savedLinks.Add($m.Value)
    [void]$sb.Append($soh + "L" + ($script:savedLinks.Count - 1) + $soh)
    $prev = $m.Index + $m.Length
  }
  [void]$sb.Append($s.Substring($prev))
  return $sb.ToString()
}

# Restore each `\x01L<n>\x01` placeholder to its original link text (manual
# splice, fast).
function Restore-Masks([string]$s) {
  if ($script:savedLinks.Count -eq 0) { return $s }
  if (-not $s.Contains($soh)) { return $s }
  $ms = $maskPattern.Matches($s)
  if ($ms.Count -eq 0) { return $s }
  $sb = [System.Text.StringBuilder]::new()
  $prev = 0
  foreach ($m in $ms) {
    [void]$sb.Append($s.Substring($prev, $m.Index - $prev))
    [void]$sb.Append($script:savedLinks[[int]$m.Groups[1].Value])
    $prev = $m.Index + $m.Length
  }
  [void]$sb.Append($s.Substring($prev))
  return $sb.ToString()
}

# Apply the ADR rule to a text segment via manual splicing. Increments
# $script:adrCount.
function Convert-AdrSegment([string]$seg, [string]$srcFile) {
  if (-not ($seg -match 'ADR \d{4}')) { return $seg }
  $ms = $adrPattern.Matches($seg)
  if ($ms.Count -eq 0) { return $seg }
  $sb = [System.Text.StringBuilder]::new()
  $prev = 0
  foreach ($m in $ms) {
    [void]$sb.Append($seg.Substring($prev, $m.Index - $prev))
    $num = $m.Groups[1].Value
    if ($script:adrMap.ContainsKey($num)) {
      $target = Join-Path $script:repo $script:adrMap[$num]
      $rel = ConvertTo-RelPath $srcFile $target
      $script:adrCount += 1
      [void]$sb.Append("ADR [" + $num + "](" + $rel + ")")
    } else {
      [void]$sb.Append($m.Value)
    }
    $prev = $m.Index + $m.Length
  }
  [void]$sb.Append($seg.Substring($prev))
  return $sb.ToString()
}

# Process one line:
#   1. mask existing links;
#   2. split on backticks — ADR rule on the text (even) segments, path rule on
#      the code (odd) segments; a resolved `X.md` becomes [`X.md`](rel);
#   3. restore the masks.
function Convert-Line([string]$line, [string]$srcFile) {
  if (-not $line.Contains('ADR') -and -not $line.Contains([char]96)) { return $line }

  $script:savedLinks = [System.Collections.Generic.List[string]]::new()
  $work = Mask-Links $line

  $parts = $work.Split([char]96)   # 96 = `
  if ($parts.Count % 2 -eq 0) {
    # even number of parts => an ODD number of backticks => an unbalanced
    # (unterminated) code span. Refuse to linkify.
    return Restore-Masks $work
  }
  if ($parts.Count -lt 3) {
    return Restore-Masks (Convert-AdrSegment $work $srcFile)
  }
  $sb = [System.Text.StringBuilder]::new()
  for ($i = 0; $i -lt $parts.Count; $i++) {
    if ($i % 2 -eq 0) {
      # text segment -> ADR rule
      [void]$sb.Append((Convert-AdrSegment $parts[$i] $srcFile))
    } else {
      # code segment (delimited by the backticks around it) -> path rule
      $tok = $parts[$i].Trim()
      $pathPart = $tok
      $frag = ''
      $hash = $tok.IndexOf([char]35)
      if ($hash -ge 0) { $pathPart = $tok.Substring(0, $hash); $frag = $tok.Substring($hash) }
      if (($pathPart -match '\.md$') -and ($tok -notmatch '\s') -and ($tok -notmatch '[*]')) {
        $resolved = Test-Resolves $srcFile $pathPart
        if ($resolved) {
          $target = Join-Path $script:repo $resolved
          $rel = ConvertTo-RelPath $srcFile $target
          $script:pathCount += 1
          [void]$sb.Append('[' + [char]96 + $tok + [char]96 + '](' + $rel + $frag + ')')
          continue
        }
      }
      # not converted: preserve the code span verbatim (backticks + content)
      [void]$sb.Append([char]96)
      [void]$sb.Append($parts[$i])
      [void]$sb.Append([char]96)
    }
  }
  return Restore-Masks $sb.ToString()
}

# =============================================================================
# Pass 1 — fix the 3 pre-existing broken links (relative-depth errors).
# Only the link target is replaced; the display text is left untouched. The
# 3 target files are in docs/archive/ and docs/philosophy/, so this is a
# standalone pass (the main loop below skips docs/archive/).
# =============================================================================
$brokenFixes = @(
  @{ file = 'docs/archive/lanes/improve/improve-audit.md';
     old = '](../../philosophy/anti-patterns.md)';
     new = '](../../../philosophy/anti-patterns.md)' },
  @{ file = 'docs/archive/lanes/improve/plan-improve.md';
     old = '](../philosophy/README.md)';
     new = '](../../../philosophy/README.md)' },
  @{ file = 'docs/philosophy/templates/design-doc.md';
     old = '](../../docs/OPS.md)';
     new = '](../../OPS.md)' }
)

$changedFiles = [System.Collections.Generic.List[string]]::new()
$perFile = [System.Collections.Generic.List[string]]::new()
$totalAdr = 0
$totalPath = 0
$totalBroken = 0

foreach ($fix in $brokenFixes) {
  $p = Join-Path $repo $fix.file
  if (-not (Test-Path -LiteralPath $p)) { continue }
  $content = [System.IO.File]::ReadAllText($p)
  if (-not $content.Contains($fix.old)) { continue }
  $count = $content.Split($fix.old).Count - 1
  $content = $content.Replace($fix.old, $fix.new)
  if ($Apply) { [System.IO.File]::WriteAllText($p, $content) }
  $changedFiles.Add($fix.file)
  $perFile.Add("  $($fix.file) : $count broken-link fix(es)")
  $totalBroken += $count
}

# =============================================================================
# Pass 2 — linkify the living doc bodies.
# =============================================================================
foreach ($f in $files) {
  $rel = ($f.FullName -replace [regex]::Escape($repo + '/'), '').Replace('\', '/')
  $lines = [System.IO.File]::ReadAllLines($f.FullName)
  $out = New-Object System.Collections.Generic.List[string]

  # front-matter range: if line[0] is '---', to the next '---' line
  $fmEnd = -1
  if ($lines.Count -gt 0 -and $lines[0].Trim() -eq '---') {
    for ($i = 1; $i -lt $lines.Count; $i++) { if ($lines[$i].Trim() -eq '---') { $fmEnd = $i; break } }
  }

  $inFence = $false
  $script:adrCount = 0
  $script:pathCount = 0

  for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    $trimmed = $line.TrimStart()
    if ($trimmed -match '^(`{3,}|~{3,})') { $inFence = -not $inFence; $out.Add($line); continue }
    if ($inFence) { $out.Add($line); continue }
    if ($i -le $fmEnd -and $fmEnd -ge 0) { $out.Add($line); continue }
    $out.Add((Convert-Line $line $f.FullName))
  }

  $oldContent = [System.IO.File]::ReadAllText($f.FullName)
  $isCRLF = $oldContent.Contains("`r`n")
  $eol = if ($isCRLF) { "`r`n" } else { "`n" }
  $newContent = ($out -join $eol)
  # `ReadAllLines` + `join` loses exactly one trailing newline for any file
  # that ends in a newline (e.g. `x\n\n` -> lines [x,''] -> `x\n`). Add one
  # back when the original ended in a newline, so the only diff we ever write
  # is a real edit — never a line-ending or blank-line change.
  if ($oldContent.Length -gt 0 -and $oldContent[-1] -eq [char]10) {
    $newContent = $newContent + $eol
  }
  # Only write (and report) a file if it actually got an ADR or path edit —
  # never let a line-ending or blank-line difference register as a "change".
  if (($script:adrCount -gt 0 -or $script:pathCount -gt 0) -and $newContent -ne $oldContent) {
    if ($Apply) { [System.IO.File]::WriteAllText($f.FullName, $newContent) }
    $changedFiles.Add($rel)
    $perFile.Add("  $rel : +$($script:adrCount) ADR, +$($script:pathCount) path")
  }
  $totalAdr += $script:adrCount
  $totalPath += $script:pathCount
}

# --- report ------------------------------------------------------------------
$mode = if ($Apply) { 'APPLIED' } else { 'dry-run' }
Write-Host "linkify [$mode]: $($changedFiles.Count) files would change"
Write-Host "  ADR refs linked  : $totalAdr"
Write-Host "  path tokens      : $totalPath"
Write-Host "  broken-link fixes: $totalBroken"
Write-Host ""
foreach ($c in $perFile) { Write-Host $c }
if (-not $Apply) { Write-Host "" ; Write-Host "Re-run with -Apply to write the changes." }
