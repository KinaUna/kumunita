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
# Source of truth for the allowlist: docs/archive/lanes/improve-devx/
# improve-devx-handoff-notes.md (## DX06). obj/ and bin/ are excluded (build artifacts). An
# empty set is a valid subset (the allowlist file may legitimately stop declaring
# the pattern in the future); the rule fails only when a file *outside* the
# allowlist still declares it.
$coreFixtureAllowlist = @('AppRoleSchemaPrivilegeTests.cs')
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

# --- rule: testing-and-ci-messages (DX17) -------------------------------------
# docs/dev/testing-and-ci.md's "Message → script → fix" table quotes each gate's
# failure message *verbatim* so a cold maintainer can copy a red line into a
# grep. That only works if the quoted prefix still exists in the gate — a
# RETIRED message (one that occurs nowhere) would send a cold agent chasing a
# gate that no longer exists (plan E4: the old doc quoted a line-count pin that
# "no longer exists anywhere"). This rule pins the truth: every backtick-quoted
# prefix in the table's message column must be a LITERAL SUBSTRING of the
# combined text of tools/**/*.ps1 + tests/**/*.cs (obj/ and bin/ excluded —
# build artifacts). A missing table (zero prefixes) fails closed — the table is
# the point, so an empty or renamed table cannot silently pass. Fails open to
# "table missing" only when the doc is absent (a cold read of a fresh checkout
# still finds the doc; the rule is about the doc's claims, not the tree).
$tcDocPath = Join-Path $repo 'docs/dev/testing-and-ci.md'
$tcPrefixes = [System.Collections.Generic.List[string]]::new()
if (Test-Path $tcDocPath) {
  $tcDoc = Get-Content -LiteralPath $tcDocPath -Raw
  if ($tcDoc -match '(?ms)^##\s+Message → script → fix\s*\n(.*?)(?=^##\s|\Z)') {
    # The message column is the table's FIRST cell: a row whose first cell is
    # exactly one backtick-quoted prefix. The other columns (script, fix) may
    # also carry backticks (gate names, paths) — those are NOT prefixes and are
    # intentionally not checked (drift fact 8: "each row's message column").
    foreach ($m in [regex]::Matches($Matches[1], '(?m)^\|\s*`([^`]+)`\s*\|')) { $tcPrefixes.Add($m.Groups[1].Value) }
  }
}
$tcFiles = @()
$tcFiles += @(Get-ChildItem -Path (Join-Path $repo 'tools') -Filter '*.ps1' -File -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch '/obj/|/bin/' })
$tcFiles += @(Get-ChildItem -Path (Join-Path $repo 'tests') -Filter '*.cs' -File -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch '/obj/|/bin/' })
$tcCorpus = ($tcFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$tcBad = @($tcPrefixes | Where-Object { -not $tcCorpus.Contains($_) })
$tcOk = ($tcPrefixes.Count -gt 0) -and ($tcBad.Count -eq 0)
$tcDetail = if ($tcPrefixes.Count -eq 0) { 'table missing (zero quoted prefixes found)' } elseif ($tcBad.Count -gt 0) { 'not in tools/ or tests/: ' + ($tcBad -join '; ') } else { 'all ' + $tcPrefixes.Count + ' prefixes resolve in tools/ or tests/' }
Check "testing-and-ci-messages" "every quoted message prefix in testing-and-ci.md's table resolves to tools/ or tests/: $tcDetail" $tcOk
$ruleCount = 8

# --- rule: recipes-paths-exist (DX18) -------------------------------------------
# docs/dev/recipes.md is the six how-tos a cold maintainer copies and runs
# (plan E8: the cold agent's "wished existed" list started with recipes). A
# recipe is only trustworthy if its paths are real — so every backtick-quoted
# REPO PATH in the doc must resolve to an existing file or directory. The
# extractor is pinned so the definition cannot drift (D8 reproducible contract):
# a quoted token counts as a repo path iff it (1) contains a '/' and (2) starts
# with one of the top-level roots src/, tools/, tests/, docs/, or e2e/ — this
# deliberately EXCLUDES prose like `keys/en`, `wwwroot/js`, and `dotnet exec …`
# command spans (a span that starts with a non-root token is not a path). A
# missing doc, or a doc with zero repo-path tokens, FAILS CLOSED — the recipes
# are the point, so an empty doc cannot silently pass.
$recipesPath = Join-Path $repo 'docs/dev/recipes.md'
$recipeRoots = @('src/', 'tools/', 'tests/', 'docs/', 'e2e/')
$recipeTokens = [System.Collections.Generic.List[string]]::new()
if (Test-Path $recipesPath) {
  $recipesRaw = Get-Content -LiteralPath $recipesPath -Raw
  foreach ($m in [regex]::Matches($recipesRaw, '`([^`]+)`')) {
    $tok = $m.Groups[1].Value
    if ($tok.Contains('/')) {
      foreach ($r in $recipeRoots) { if ($tok.StartsWith($r)) { $recipeTokens.Add($tok); break } }
    }
  }
}
$recipeMissing = [System.Collections.Generic.List[string]]::new()
foreach ($t in $recipeTokens) {
  if (-not (Test-Path (Join-Path $repo $t))) { $recipeMissing.Add($t) }
}
$recipeOk = ($recipeTokens.Count -gt 0) -and ($recipeMissing.Count -eq 0)
$recipeDetail = if ($recipeTokens.Count -eq 0) { 'doc missing or zero repo-path tokens (fails closed)' } elseif ($recipeMissing.Count -gt 0) { 'not found: ' + ($recipeMissing -join '; ') } else { 'all ' + $recipeTokens.Count + ' repo paths resolve' }
Check "recipes-paths-exist" "every backtick-quoted repo path in docs/dev/recipes.md resolves to a file or directory: $recipeDetail" $recipeOk
$ruleCount = 9

# --- rule: lane-named-test-files (DX23) ----------------------------------------
# The D5 "flat, lane-named tests" finding: test files named by lane/unit code
# (F22_…, LS_U04_…, ADR_0044_…) are a differentiation smell — the file name
# should say what it tests, not which unit wrote it. DX23 renamed the 16
# existing files to behavior names; this rule keeps it at 0. The regex is the
# same one devx-report.ps1 uses for its E9 "lane" count, so the gate and the
# report agree by construction. obj/ and bin/ are excluded (build artifacts).
$laneNamedRegex = '^(F|U|C|D|IS|LS|R|A|O|GP|UPL)[0-9]+[U]?[0-9]*[_-]|^ADR_0[0-9]{3}_'
$laneNamedFiles = [System.Collections.Generic.List[string]]::new()
foreach ($proj in @('tests/Kumunita.Core.Tests', 'tests/Kumunita.Web.Tests')) {
  $p = Join-Path $repo $proj
  if (-not (Test-Path $p)) { continue }
  foreach ($f in (Get-ChildItem -Path $p -Filter '*.cs' -File -Recurse | Where-Object { $_.FullName -notmatch '/obj/|/bin/' })) {
    if ($f.Name -match $laneNamedRegex) { $laneNamedFiles.Add(($f.FullName -replace [regex]::Escape($repo + '/'), '')) }
  }
}
Check "lane-named-test-files" "lane/unit-coded test file names in Core.Tests + Web.Tests: $($laneNamedFiles.Count) (expected 0 — rename to a behavior name; the lane code belongs in the class XML summary)" ($laneNamedFiles.Count -eq 0)
$ruleCount = 10

# --- rule: test-files-at-root (DX30) --------------------------------------------
# D5 "mirror the source folders" (plan DoD): new test files belong in a context
# folder, not at the project root. DX25–29 moved all 227 non-`_Shared/` root
# files into context folders; the 142 that remain (Core 78 + Web 64) are ALL
# mapped `_Shared/` — cross-cutting tests (gate tests, harnesses, fixture
# infrastructure) that reference multiple or no single context. That 142 is the
# HONEST FLOOR, not the plan's "baseline 0" (those tests don't belong in any
# one context, so D5's single-folder-per-file rule correctly sends them to
# `_Shared/`; the plan's D2 DoD permits "the honest floor recorded with its
# reason"). The limit is the baseline's test_files_at_project_root value
# (shrink-only: it may only go down; raising it needs an adr: reason). Counts
# .cs files at the project root of both test projects (obj/ and bin/ excluded
# — build artifacts). Fails when the count exceeds the limit (a NEW root-level
# test file), not when it equals it (the 142 existing ones are the floor).
$rootTestLimit = 0
if ($baseline.PSObject.Properties['rules'] -and $baseline.rules.PSObject.Properties['test_files_at_project_root']) {
  $rootTestLimit = [int]$baseline.rules.test_files_at_project_root.value
}
$rootTestCount = 0
foreach ($proj in @('tests/Kumunita.Core.Tests', 'tests/Kumunita.Web.Tests')) {
  $p = Join-Path $repo $proj
  if (-not (Test-Path $p)) { continue }
  $rootTestCount += @(Get-ChildItem -Path $p -Filter '*.cs' -File |
    Where-Object { $_.FullName -notmatch '/obj/|/bin/' }).Count
}
Check "test-files-at-root" "test files at the project root of Core.Tests + Web.Tests: $rootTestCount (limit $rootTestLimit, shrink-only — new test files go in a context folder; the 142 existing ones are cross-cutting `_Shared/` tests, the honest floor)" ($rootTestCount -le $rootTestLimit)
$ruleCount = 11

# --- rule: lane-codes (DX31) ---------------------------------------------------
# The lane-code glossary (docs/dev/lane-codes.md, D6 part 1) must stay fresh:
# regenerating it must produce no diff. lane-codes.ps1 -Check regenerates in
# memory and diffs against the committed file (exit 0 = identical, 1 = stale
# or missing) — the same "generated text only, no hand edits" gate as the
# code-map rule above, so a glossary and the tree cannot drift apart. Runs
# lane-codes.ps1 in a child pwsh (the child's `exit` only ends that process,
# so it cannot terminate this one — the DX13 contrast).
$laneCodesScript = Join-Path $repo 'tools/devx/lane-codes.ps1'
$laneCodesFresh = $false
if (Test-Path $laneCodesScript) {
  $null = & pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $laneCodesScript -Check 2>&1
  $laneCodesFresh = ($LASTEXITCODE -eq 0)
}
Check "lane-codes" "docs/dev/lane-codes.md is fresh — regenerate with `pwsh tools/devx/lane-codes.ps1` when it goes stale (D6)" $laneCodesFresh
$ruleCount = 12

# --- rule: lane-codes-ratchet (DX32) --------------------------------------------
# D6 part 2: the lane/unit-code count in src/tests comments is shrink-only.
# DX32 pinned the canonical regex (D-excluded — a `D<n>` in src/tests is an
# ADR-decision ref like `ADR 0307, D1`, the desired form, not the anti-pattern)
# and re-baselined lane_code_hits to the live count under it. This rule keeps
# that floor: a NEW unit code added to a comment (a count ABOVE the floor)
# fails; the existing hits are the floor, like rule 11's 142 test files. Same
# canonical regex as devx-report.ps1's E11 line — source: devx-report.ps1 E11,
# keep in sync (house style is to duplicate the regex, not shell out). Counts
# src/tests *.cs with obj/ and bin/ excluded (build artifacts). The limit is
# the baseline's lane_code_hits value (shrink-only; raising needs an adr:
# reason). Fails when the live count exceeds the limit, not when it equals it.
$laneCodeLineRegex = '\b(?:F|U|C|IS|LS|R|A|O|GP|UPL)[0-9]{1,2}[Uu]?[0-9]*(?:-[0-9]+)?\b'  # source: devx-report.ps1 E11, keep in sync
$laneCodeLimit = 0
if ($baseline.PSObject.Properties['rules'] -and $baseline.rules.PSObject.Properties['lane_code_hits']) {
  $laneCodeLimit = [int]$baseline.rules.lane_code_hits.value
}
$laneCodeHits = 0
foreach ($scope in @('src', 'tests')) {
  $p = Join-Path $repo $scope
  if (-not (Test-Path $p)) { continue }
  foreach ($f in (Get-ChildItem -Path $p -Filter '*.cs' -File -Recurse | Where-Object { $_.FullName -notmatch '/obj/|/bin/' })) {
    $m = Select-String -LiteralPath $f.FullName -Pattern $laneCodeLineRegex -AllMatches
    if ($m) { foreach ($line in $m) { $laneCodeHits += $line.Matches.Count } }
  }
}
Check "lane-codes-ratchet" "lane/unit-code hits in src/tests comments: $laneCodeHits (limit $laneCodeLimit, shrink-only — a code comment cites the ADR number or its decision, not a raw unit code; unit codes belong in the plan or handoff)" ($laneCodeHits -le $laneCodeLimit)
$ruleCount = 13

# --- rule: tracked-artifacts (DX34) ---------------------------------------------
# E12 leftovers: no tracked *.out / *.log / *.trx build scratch. DX34 untracked
# the four tests/*.out files (git rm) and closed the .gitignore gap (*.out /
# *.trx beside the existing *.log). The count is the live `git ls-files` set
# filtered to the three extensions — git tracks, so it is deterministic and
# independent of untracked scratch on disk (the tree is clean of untracked
# *.log / *.trx, but the gate must catch what git tracks, not the filesystem).
# The limit is the baseline's tracked_out_log_trx_artifacts value (shrink-only:
# it may only go down; raising it needs an adr: reason). After DX34 the floor is
# 0, so the rule is green at 0 and FAILs on ANY new tracked *.out / *.log / *.trx.
$trackedArtifactLimit = 0
if ($baseline.PSObject.Properties['rules'] -and $baseline.rules.PSObject.Properties['tracked_out_log_trx_artifacts']) {
  $trackedArtifactLimit = [int]$baseline.rules.tracked_out_log_trx_artifacts.value
}
$trackedArtifactCount = @(& git -C $repo ls-files | Where-Object { $_ -match '\.(out|log|trx)$' }).Count
Check "tracked-artifacts" "tracked *.out / *.log / *.trx build scratch: $trackedArtifactCount (limit $trackedArtifactLimit, shrink-only — build scratch stays untracked; .gitignore carries *.out / *.log / *.trx)" ($trackedArtifactCount -le $trackedArtifactLimit)
$ruleCount = 14

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
