# check.ps1 — the IMPROVE-DEVX lane's one-command feedback loop (D3)
#
# Run:  pwsh tools/check.ps1            # the full seven-step loop
#       pwsh tools/check.ps1 -Fast      # skip `core` (the slow step); still runs `web`
#       pwsh tools/check.ps1 -Only tsc  # a single step
#       pwsh tools/check.ps1 -Only docs,devx
#
# The loop is a *runner*, not a gate (D3 / ADR 0307): it runs the seven steps
# in order, times each, and prints a summary table (step | seconds |
# PASS|FAIL|SKIPPED | why). It exits 1 if any step FAILs, 0 otherwise. A
# missing tool is a FAIL with its install line (E6: the sandbox silently
# skipped gates before — a missing tool must never become a silent skip).
#
# The steps match .github/workflows/ci.yml's step names (Test Web, Test Core,
# the docs/code/devx gate jobs):
#   build  — dotnet build Kumunita.slnx -c Debug
#   tsc    — npm --prefix src/Kumunita.Web run build; then the compiled-JS
#            drift check (git diff --exit-code -- src/Kumunita.Web/wwwroot/js)
#   web    — dotnet exec tests/Kumunita.Web.Tests/…/Kumunita.Web.Tests.dll
#   core   — dotnet exec tests/Kumunita.Core.Tests/…/Kumunita.Core.Tests.dll
#   docs   — pwsh tools/docs/docs-check.ps1 + improve-check.ps1 (DX13)
#   code   — pwsh tools/code/code-check.ps1
#   devx   — pwsh tools/devx/devx-check.ps1
#
# A full run is ~3-4 min (the `core` suite is the slow step, ~170-205 s).
# Run it in the background, or use -Fast / -Only for a quick signal.
#
# Style: matches tools/docs/docs-check.ps1 — $ErrorActionPreference = "Stop",
# Set-StrictMode -Version Latest, Find-RepoRoot walking up to Kumunita.slnx,
# [OK]/[FAIL] colour coding, exit 0/1.

[CmdletBinding()]
param(
  [switch]$Fast,
  [string]$Only,
  [switch]$Doctor
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# --- repo root -----------------------------------------------------------------
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

# --- tool availability (E6: a missing tool = FAIL with the install line, never a skip)
$installHints = @{
  dotnet = '.NET 10 SDK (10.0.400 per global.json) — https://dotnet.microsoft.com/download/dotnet/10.0'
  node   = 'Node.js — https://nodejs.org/  (this repo uses v24; npm ships with it)'
  npm    = 'npm ships with Node.js — https://nodejs.org/'
  git    = 'git — e.g. `sudo apt install git` on this sandbox'
  pwsh   = "PowerShell 7 from Microsoft's apt repo — see claude/agent-sandbox-setup.md step 3"
}
function Tool-Ensure([string]$name) {
  if (Get-Command $name -CommandType Application -ErrorAction SilentlyContinue) { return $true }
  Write-Host "[FAIL] tool : $name is not on PATH" -ForegroundColor Red
  Write-Host "       install: $($installHints[$name])"
  return $false
}

# --- -Doctor self-check (DX11) — a mode, not a step: exits before the step loop -------
# Verifies the prerequisites a full `check.ps1` run depends on (D3). Each check
# prints OK (green) or a single-line FAIL with the fix. Reuses Tool-Ensure and
# $installHints above. The plan flags the Ryuk/socket check as unverified — it is
# *detected* by a real container start (alpine), not assumed.
$doctorFailed = [System.Collections.Generic.List[string]]::new()
function Doc-Check([string]$label, [bool]$ok, [string]$fix = '') {
  if ($ok) { Write-Host "  [OK]  $label" -ForegroundColor Green }
  else { Write-Host "  [FAIL]  $label" -ForegroundColor Red; if ($fix) { Write-Host "        $fix" }; $doctorFailed.Add($label) }
}
function Doc-Probe-ContainerStart {
  $cid = ''; $ok = $false
  $eapPrev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
  $name = "dx11-$([guid]::NewGuid().ToString('n').Substring(0,8))"
  try {
    $cid = (& docker create --name $name alpine:3.20 echo ok 2>&1 | Out-String).Trim()
    if ($cid.Length -lt 12) { return $false }
    $null = & docker start $cid 2>&1; $ok = ($LASTEXITCODE -eq 0)
  } finally {
    if ($cid.Length -ge 12) { $null = & docker rm -f $cid 2>&1 }
    $ErrorActionPreference = $eapPrev
  }
  return $ok
}
if ($Doctor) {
  Write-Host "check.ps1 -Doctor — environment self-check (DX11)" -ForegroundColor Cyan
  Write-Host "  repo : $repo"
  Write-Host ""
  Write-Host " 1. dotnet pin" -ForegroundColor Cyan
  $pin = ''; if ((Test-Path global.json) -and [regex]::IsMatch((Get-Content global.json -Raw), '"version"\s*:\s*"\d+\.\d+\.\d+"')) { $pin = [regex]::Match((Get-Content global.json -Raw), '"version"\s*:\s*"(\d+\.\d+\.\d+)"').Groups[1].Value }
  $v   = (dotnet --version 2>&1 | Select-Object -First 1).Trim()
  Doc-Check "dotnet $v == global.json pin $pin" ($pin -ne '' -and $v -eq $pin) "install .NET 10 SDK 10.0.400 — $($installHints.dotnet)"
  Write-Host ""
  Write-Host " 2. node / pwsh / git" -ForegroundColor Cyan
  Doc-Check "node on PATH" (Tool-Ensure 'node') "install Node.js — $($installHints.node)"
  Doc-Check "pwsh on PATH" (Tool-Ensure 'pwsh') "install PowerShell 7 — $($installHints.pwsh)"
  Doc-Check "git on PATH"  (Tool-Ensure 'git')  "install git — $($installHints.git)"
  Write-Host ""
  Write-Host " 3. Docker reachable" -ForegroundColor Cyan
  $dockerOk = Tool-Ensure 'docker'
  $dockerRoot = ''; $dockerInfoOk = $false
  if ($dockerOk) { $eapPrev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'; $dockerRoot = (& docker info --format '{{.DockerRootDir}}' 2>&1 | Out-String).Trim(); $dockerInfoOk = ($LASTEXITCODE -eq 0); $ErrorActionPreference = $eapPrev }
  $rootless = ($dockerRoot -ne '' -and $dockerRoot -notmatch '^/var/lib/docker$')
  $sockOk = $true
  if ($rootless) {
    $xrt      = $env:XDG_RUNTIME_DIR
    $expected = if ($xrt) { "unix://$xrt/docker.sock" } else { 'unix://$XDG_RUNTIME_DIR/docker.sock' }
    $sockOk   = ($env:DOCKER_HOST -eq $expected) -or ($xrt -and (Test-Path (Join-Path $xrt 'docker.sock')))
  }
  $dockerReach = ($dockerOk -and $dockerInfoOk -and $sockOk)
  Doc-Check "docker reachable$(if ($rootless) { ' (rootless)' })" $dockerReach "check DOCKER_HOST points at unix://`$XDG_RUNTIME_DIR/docker.sock (rootless)"
  Write-Host ""
  Write-Host " 4. Testcontainers (real container start)" -ForegroundColor Cyan
  $tcOk = if ($dockerReach) { (Doc-Probe-ContainerStart) } else { $false }
  Doc-Check "a real container starts via the Docker socket" $tcOk 'set TESTCONTAINERS_RYUK_DISABLED=true and TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock'
  Write-Host ""
  Write-Host " 5. shallow clone" -ForegroundColor Cyan
  $baseRef = if ($env:CODE_BASELINE_BASE) { $env:CODE_BASELINE_BASE } else { 'origin/development' }
  $shallow = ((git rev-parse --is-shallow-repository 2>&1 | Out-String).Trim() -eq 'true')
  $baseRefOk = -not ($shallow -and -not (& git rev-parse --verify -q $baseRef 2>$null | Out-String).Trim())
  Doc-Check "not a shallow clone (base ref $baseRef present)" $baseRefOk 'run: git fetch --depth 50 origin development   (bounded — not an unbounded unshallow)'
  Write-Host ""
  if ($doctorFailed.Count -eq 0) { Write-Host "doctor: ALL CHECKS OK — the environment is ready for `check.ps1`." -ForegroundColor Green; exit 0 }
  else { Write-Host "doctor: $($doctorFailed.Count) CHECK(S) FAILED: $($doctorFailed -join '; ')" -ForegroundColor Red; exit 1 }
}

# --- step selection ----------------------------------------------------------------
$validSteps = @('build','tsc','web','core','docs','code','devx')
$selected = $validSteps
if ($Only) {
  $reqd = @($Only -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
  if ($reqd.Count -eq 0) { $reqd = @($validSteps) }
  $unknown = @($reqd | Where-Object { $validSteps -notcontains $_ })
  if ($unknown.Count -gt 0) {
    Write-Host "[FAIL] args : unknown step(s) in -Only: $($unknown -join ', ') (valid: $($validSteps -join ', '))" -ForegroundColor Red
    exit 1
  }
  $selected = @($validSteps | Where-Object { $reqd -contains $_ })
}

# --- results ---------------------------------------------------------------------
$results = [System.Collections.Generic.List[object]]::new()
foreach ($s in $validSteps) {
  $results.Add([PSCustomObject]@{ name = $s; status = 'PENDING'; secs = 0; why = ''; log = ''; tools = @(); cmd = $null })
}
function Get-Result([string]$name) { return @($results | Where-Object { $_.name -eq $name })[0] }

# --- step execution ----------------------------------------------------------------
function Invoke-Step([PSCustomObject]$r) {
  foreach ($t in $r.tools) {
    if (-not (Tool-Ensure $t)) { $r.status = 'FAIL'; $r.why = "missing tool: $t — $($installHints[$t])"; return }
  }
  $eapPrev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
  $sw = [System.Diagnostics.Stopwatch]::StartNew(); $output = ''; $exitCode = 0; $threw = $false
  try {
    $output = & $r.cmd 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
  } catch {
    $threw = $true; $exitCode = 1
    if ($output) { $output = "$output`n" }
    $output += $_.Exception.Message
  }
  $sw.Stop()
  $r.secs = [math]::Round($sw.Elapsed.TotalSeconds, 1)
  $r.log = $output
  $ErrorActionPreference = $eapPrev
  # The table's `why` is the step's own last line, stripped of ANSI colour
  # codes (xunit's summary ends in a colour-reset, which would otherwise
  # show as literal "[0m" in a plain-text table).
  $clean = [regex]::Replace($output, "\x1b\[[0-9;]*m", '')
  $last = @($clean -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_.Length -gt 0 } | Select-Object -Last 1)
  if ($exitCode -eq 0 -and -not $threw) { $r.status = 'PASS'; $r.why = if ($last) { $last[0] } else { '' } }
  else { $r.status = 'FAIL'; $r.why = if ($last) { $last[0] } else { "exit code $exitCode" } }
}

# --- declare the steps (name, tools, command) --------------------------------------
# `build` is the precondition for `web` and `core` (they dotnet-exec the built dlls).
# If you -Only a step that depends on `build` without running it first, the dll may
# be missing and that step will FAIL with a clear reason — run `build` first.
(Get-Result 'build').tools = @('dotnet')
(Get-Result 'build').cmd = { dotnet build Kumunita.slnx -c Debug }
(Get-Result 'tsc').tools = @('npm','git')
(Get-Result 'tsc').cmd = {
  npm --prefix src/Kumunita.Web run build
  git diff --exit-code -- src/Kumunita.Web/wwwroot/js
}
(Get-Result 'web').tools = @('dotnet')
(Get-Result 'web').cmd = { dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll }
(Get-Result 'core').tools = @('dotnet')
(Get-Result 'core').cmd = { dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll }
(Get-Result 'docs').tools = @('pwsh')
# DX13 (ADR 0307 D9): run both docs-family gates; FAIL if either is non-zero.
# NB: set $LASTEXITCODE (not `exit`) — `exit` inside a `&`-invoked scriptblock
# would terminate check.ps1 before the summary table prints (DX13 gotcha).
(Get-Result 'docs').cmd = {
  pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/docs-check.ps1; $d = $LASTEXITCODE
  pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/improve-check.ps1; $i = $LASTEXITCODE
  if ($d -ne 0 -or $i -ne 0) { $LASTEXITCODE = 1 }
}
(Get-Result 'code').tools = @('pwsh','dotnet')
(Get-Result 'code').cmd = { pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/code/code-check.ps1 }
(Get-Result 'devx').tools = @('pwsh')
(Get-Result 'devx').cmd = { pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/devx/devx-check.ps1 }

# --- run the selected steps in order -----------------------------------------------
Write-Host "check.ps1 — IMPROVE-DEVX one-command loop (D3)" -ForegroundColor Cyan
Write-Host "  repo : $repo"
Write-Host "  steps: $($selected -join ', ')"
Write-Host ""
foreach ($s in $selected) {
  $r = Get-Result $s
  if ($s -eq 'core' -and $Fast) {
    $r.status = 'SKIPPED'
    $r.why = '-Fast skips `core` (the slow step)'
    continue
  }
  Write-Host ">> $($r.name)" -NoNewline
  Invoke-Step $r
  Write-Host "  [$(if ($r.status -eq 'PASS') {'OK'} elseif ($r.status -eq 'FAIL') {'FAIL'} else {'skip'})] $($r.secs)s"
}

# --- summary table ------------------------------------------------------------------
Write-Host ""
$header = ("{0,-6}  {1,8}  {2,-9}  {3}" -f 'step', 'seconds', 'result', 'why')
Write-Host $header -ForegroundColor Cyan
Write-Host ('-' * $header.Length)
foreach ($r in $results) {
  $secs = if ($r.secs -gt 0) { "$($r.secs)" } else { '-' }
  Write-Host ("{0,-6}  {1,8}  {2,-9}  {3}" -f $r.name, $secs, $r.status, $r.why)
}

# --- show the full log for each failing step ----------------------------------------
$failed = @($results | Where-Object { $_.status -eq 'FAIL' })
foreach ($r in $failed) {
  Write-Host ""
  Write-Host "---- $($r.name) — full output ----" -ForegroundColor Red
  if ($r.log -and $r.log.Trim().Length -gt 0) { Write-Host $r.log.Trim() }
}

# --- exit -----------------------------------------------------------------------------
Write-Host ""
if ($failed.Count -eq 0) {
  $ran = @($results | Where-Object { $_.status -eq 'PASS' }).Count
  Write-Host "check.ps1: ALL $($ran) STEP(S) PASS" -ForegroundColor Green
  exit 0
} else {
  $names = @($failed | ForEach-Object { $_.name } | Where-Object { $_ })
  Write-Host "check.ps1: $($failed.Count) STEP(S) FAILED: $($names -join ', ')" -ForegroundColor Red
  exit 1
}
