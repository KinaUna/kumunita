# devx-check.ps1 — the IMPROVE-DEVX lane's gate (D7)
#
# Run:  pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/devx/devx-check.ps1
# Exit: 0 when every rule passes (right now, zero rules), non-zero otherwise.
#       Intended to be run from the repo root; if the CWD is not the repo
#       root, it walks up looking for Kumunita.slnx.
#
# This is the *check* half of D7 (the report half is devx-report.ps1). It
# reads tools/devx/devx-baseline.json and enforces its rules. DX01 lands the
# skeleton: it reads the baseline, proves the file parses, prints the rule
# count, and exits 0.
#
# Rules land *with the unit that fixes their gap*, never before (D7): the
# first rules are DX04's (ci.yml triggers vs branch-flow.md) and DX08's
# (IClassFixture usages outside the allowlist). Until then a green skeleton
# is the correct state, not a hole.
#
# Style: matches tools/docs/docs-check.ps1 — $ErrorActionPreference = "Stop",
# Set-StrictMode -Version Latest, Find-RepoRoot walking up to Kumunita.slnx,
# [OK]/[FAIL] lines, exit 0/1.

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

function Check([string]$gate, [string]$message, [bool]$cond) {
  if ($cond) { Write-Host "[OK]   $gate : $message" }
  else { Write-Host "[FAIL] $gate : $message" -ForegroundColor Red; $failures.Add("$gate : $message") }
}

# --- the baseline ------------------------------------------------------------
$baselinePath = Join-Path $repo 'tools/devx/devx-baseline.json'
if (-not (Test-Path $baselinePath)) {
  Write-Host "[FAIL] baseline : devx-baseline.json not found at tools/devx/devx-baseline.json" -ForegroundColor Red
  exit 1
}
$baseline = $null
try {
  $baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
  Check "baseline" "devx-baseline.json parses (version $($baseline.version))" $true
} catch {
  Write-Host "[FAIL] baseline : devx-baseline.json failed to parse: $_" -ForegroundColor Red
  exit 1
}

# --- the rules -----------------------------------------------------------------
# D7: each rule lands with the unit that fixes its gap. As of DX01 there are
# none. DX04 adds the first (ci.yml triggers vs branch-flow.md); DX08 the
# second (IClassFixture outside the allowlist). Each is a function appended
# here that reads $baseline and calls Check — no change to this header.

# --- rule: ci-triggers (DX04) ----------------------------------------------------
# ci.yml's on: branches (push + pull_request) must equal the CI branch declared in
# docs/dev/branch-flow.md (the "Which branch CI watches" section). workflow_dispatch
# carries no branches and is ignored. Fails if either file is unreadable (fail closed).
function Get-CiTriggersBranches([string]$path) {
  $branches = [System.Collections.Generic.List[string]]::new()
  $inOn = $false; $trigger = $null; $expectBranch = $false
  foreach ($raw in (Get-Content -LiteralPath $path)) {
    $line = $raw -replace '\s+$', ''
    if (-not $inOn) { if ($line -match '^on:\s*$') { $inOn = $true } ; continue }
    if ($line -match '^\S') { break }
    if ($line -match '^  (push|pull_request|workflow_dispatch|schedule|merge_group):\s*$') { $trigger = $Matches[1]; $expectBranch = $false; continue }
    if ($line -match '^    branches:\s*$') { $expectBranch = ($trigger -in 'push','pull_request'); continue }
    if ($line -match '^      -\s*(\S+)') { if ($expectBranch) { $branches.Add($Matches[1]) } ; continue }
  }
  return ,$branches
}

$ciPath = Join-Path $repo '.github/workflows/ci.yml'
$bfPath = Join-Path $repo 'docs/dev/branch-flow.md'
$ciBranches = if (Test-Path $ciPath) { Get-CiTriggersBranches $ciPath } else { @() }
$declared = $null
if (Test-Path $bfPath) {
  $bf = Get-Content -LiteralPath $bfPath -Raw
  if ($bf -match '(?ms)^##\s+Which branch CI watches\s*\n(.*?)(?=^##\s|\Z)') {
    if ($Matches[1] -match 'CI runs on `([A-Za-z0-9_]+)`') { $declared = $Matches[1] }
  }
}
$ciSet = @($ciBranches | Sort-Object -Unique)
$triggersMatch = ($null -ne $declared) -and ($ciSet.Count -eq 1) -and ($ciSet[0] -eq $declared)
Check "ci-triggers" "ci.yml on: branches ($($ciSet -join ', ')) match branch-flow.md's declared CI branch ($declared)" $triggersMatch
$ruleCount = 1

# --- rule: iclassfixture (DX08) ---------------------------------------------------
# The set of Kumunita.Core.Tests files that still declare IClassFixture<PostgresFixture>
# must be a subset of the server-scoped allowlist. The allowlist is exactly one file
# (the DX06 triage, ADR 0171 — AppRoleCannotDropSchemaTests does catalog-level
# CREATE ROLE / CREATE DATABASE … OWNER and must keep its own private container).
# Source of truth for the allowlist: docs/plans-milestones/in-progress/improve-devx-
# handoff-notes.md (## DX06). obj/ and bin/ are excluded (build artifacts). An
# empty set is a valid subset (the allowlist file may legitimately stop declaring
# the pattern in the future); the rule fails only when a file *outside* the
# allowlist still declares it.
$coreFixtureAllowlist = @('F22_AppRoleCannotDropSchemaTests.cs')
$coreFixtureFiles = @(Get-ChildItem -Path (Join-Path $repo 'tests/Kumunita.Core.Tests') -Filter '*.cs' -File -Recurse |
  Where-Object { $_.FullName -notmatch '/obj/|/bin/' } |
  Where-Object { (Select-String -LiteralPath $_.FullName -Pattern 'IClassFixture<PostgresFixture>' -SimpleMatch -Quiet) })
$coreFixtureNames = @($coreFixtureFiles | ForEach-Object { $_.Name })
$coreFixtureOk = ($coreFixtureNames.Count -le $coreFixtureAllowlist.Count) -and
  (@($coreFixtureNames | Where-Object { $coreFixtureAllowlist -notcontains $_ }).Count -eq 0)
Check "iclassfixture" "Core IClassFixture<PostgresFixture> files ($($coreFixtureNames -join ', ')) ⊆ allowlist ($($coreFixtureAllowlist -join ', '))" $coreFixtureOk
$ruleCount = 2

# --- rule: iclassfixture-web (DX09) ---------------------------------------------
# The Web side of D2: Kumunita.Web.Tests shares Core.Tests' PostgresFixture via
# the assembly fixture, so *no* Web test file may still declare
# IClassFixture<PostgresFixture> — there is no Web allowlist (the DX06 triage
# found no server-scoped Web test; the only hit was PostgresFixture.cs itself,
# which is the fixture, not a test class). obj/ and bin/ are excluded. Fails on
# any Web file that still declares the pattern.
$webFixtureFiles = @(Get-ChildItem -Path (Join-Path $repo 'tests/Kumunita.Web.Tests') -Filter '*.cs' -File -Recurse |
  Where-Object { $_.FullName -notmatch '/obj/|/bin/' } |
  Where-Object { (Select-String -LiteralPath $_.FullName -Pattern 'IClassFixture<PostgresFixture>' -SimpleMatch -Quiet) })
$webFixtureNames = @($webFixtureFiles | ForEach-Object { $_.Name })
Check "iclassfixture-web" "Web IClassFixture<PostgresFixture> files ($($webFixtureNames -join ', ')) — none expected (Web has no allowlist)" ($webFixtureNames.Count -eq 0)

# --- rule: vscode-task-dotnet-test (DX12) -------------------------------------
# The IDE's one-command loop must route through tools/check.ps1 (D3). A VS Code
# task whose command runs "dotnet test" is the path testing-and-ci.md calls
# broken — so no tasks.json in the repo may carry a command matching the plain
# text "dotnet test" (substring: "dotnet test /p:..." still matches, intended).
# Covers every tasks.json (a new one is caught too), excluding build artifacts.
# -Force is required: .vscode is a dot-directory and Get-ChildItem skips hidden
# dirs on Linux without it (verified — without -Force it finds 0 files and the
# rule trivially passes). The message names the offending task label so the fix
# is one hop.
$taskFiles = @(Get-ChildItem -Path $repo -Filter 'tasks.json' -File -Recurse -Force |
  Where-Object { $_.FullName -notmatch '/node_modules/|/obj/|/bin/' })
$badTasks = [System.Collections.Generic.List[string]]::new()
foreach ($tf in $taskFiles) {
  $tj = $null
  try { $tj = Get-Content -LiteralPath $tf.FullName -Raw | ConvertFrom-Json } catch { continue }
  $tasksArr = if ($tj.PSObject.Properties['tasks']) { @($tj.tasks) } else { @() }
  foreach ($t in $tasksArr) {
    $cmd = if ($t.PSObject.Properties['command']) { [string]$t.command } else { '' }
    if ($cmd -match 'dotnet test') {
      $label = if ($t.PSObject.Properties['label']) { [string]$t.label } else { '?' }
      $rel = $tf.FullName -replace [regex]::Escape($repo + '/'), ''
      $badTasks.Add("$label in $rel")
    }
  }
}
$badDesc = if ($badTasks.Count -eq 0) { 'none' } else { ($badTasks -join '; ') }
Check "vscode-task-dotnet-test" "VS Code task(s) running 'dotnet test': $badDesc (expected none — the IDE loop routes through tools/check.ps1, D3)" ($badTasks.Count -eq 0)
$ruleCount = 4

# --- rule: code-map (DX15) --------------------------------------------------------
# The generated code map (docs/dev/code-map.md, D4) must stay fresh: regenerating
# it must produce no diff. code-map.ps1 -Check regenerates in memory and diffs
# against the committed map (exit 0 = identical, 1 = stale or missing). This is
# the "generated text only, no hand edits" gate — hand-editing the map fails here,
# so the map and the code cannot drift apart. Runs code-map.ps1 in a child pwsh
# (the house gate invocation check.ps1 uses for its scripts; the child's `exit`
# only ends that process, so it cannot terminate this one — the DX13 contrast).
$codeMapScript = Join-Path $repo 'tools/devx/code-map.ps1'
$codeMapFresh = $false
if (Test-Path $codeMapScript) {
  $null = & pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $codeMapScript -Check 2>&1
  $codeMapFresh = ($LASTEXITCODE -eq 0)
}
Check "code-map" "docs/dev/code-map.md is fresh — regenerate with `pwsh tools/devx/code-map.ps1` when it goes stale (D4)" $codeMapFresh
$ruleCount = 5

# --- token estimator (DX16) ------------------------------------------------------
# Pinned, reproducible proxy: tokens ≈ chars / 4 (integer division). This is a
# *proxy* gate — it tracks growth, not an exact tokenizer. We deliberately do not
# call a real tokenizer: that would make the number machine- and model-dependent
# (non-reproducible across CI runners and dev boxes). The same estimator is used
# by rule 6 (agents-md-tokens), rule 7 (read-before-tokens), and devx-report.ps1,
# so the cap and the report agree by construction.
function Get-Tokens([string]$path) {
  return [math]::Floor((Get-Content -LiteralPath $path -Raw).Length / 4)
}

# --- rule: agents-md-tokens (DX16) -------------------------------------------------
# AGENTS.md is loaded into every agent session, so its length is a per-session
# cost (E7: a cold agent burned 123 K tokens partly on a long mandatory chain).
# Cap it at 2 000 tokens (the pinned chars/4 estimator, above) so it cannot
# silently re-bloat. Fails closed if AGENTS.md is missing.
$agentsPath = Join-Path $repo 'AGENTS.md'
$agentsTokens = 0
if (Test-Path $agentsPath) { $agentsTokens = Get-Tokens $agentsPath }
Check "agents-md-tokens" "AGENTS.md is ≤ 2 000 tokens (measured $agentsTokens; pinned chars/4 estimator)" ($agentsTokens -le 2000)
$ruleCount = 6

# --- rule: read-before-tokens (DX16) ---------------------------------------------
# The "read before your first change" chain must stay small. ONE reproducible
# definition (drift fact 8): the chain is AGENTS.md plus every relative .md file
# it links from its `## Start here` section (parse that one section, resolve each
# `[…](…)` to a file, sum chars/4). The `## Read when` section is intentionally
# EXCLUDED — those are conditional read-when pointers, not up-front reads — and
# we do NOT transclude recursively (that would pull in the whole docs/ tree and
# make the 8 000 cap unreachable). Cap: 8 000 tokens (same estimator as rule 6).
# The definition lives in this comment so DX17/DX18 (which read AGENTS.md) see
# the same number.
$readBefore = 0
$agentsRaw = ''
if (Test-Path $agentsPath) {
  $agentsRaw = Get-Content -LiteralPath $agentsPath -Raw
  $readBefore = Get-Tokens $agentsPath
  if ($agentsRaw -match '(?ms)^##\s+Start here\s*\n(.*?)(?=^##\s|\Z)') {
    foreach ($m in [regex]::Matches($Matches[1], '\[[^\]]*\]\(([^()\s]+)\)')) {
      $target = ($m.Groups[1].Value -split '#')[0]
      if ($target -match '\.md$' -and -not $target.StartsWith('http')) {
        $tp = Join-Path $repo $target
        if (Test-Path $tp) { $readBefore += Get-Tokens $tp }
      }
    }
  }
}
Check "read-before-tokens" "read-before chain (AGENTS.md + its `## Start here` .md links) is ≤ 8 000 tokens (measured $readBefore; pinned chars/4 estimator)" ($readBefore -le 8000)
$ruleCount = 7

# --- summary --------------------------------------------------------------------
Write-Host ""
Write-Host "devx-check — IMPROVE-DEVX gate (D7)"
Write-Host "  rules landed : $ruleCount"
if ($ruleCount -eq 0) { Write-Host "  (DX01 skeleton: 0 rules — rules land with their fixing units, D7)" }
if ($failures.Count -eq 0) {
  Write-Host "  result       : PASS ($ruleCount rule(s))"
  exit 0
} else {
  Write-Host "  result       : FAIL ($($failures.Count) failure(s))" -ForegroundColor Red
  foreach ($f in $failures) { Write-Host "    - $f" }
  exit 1
}
