#requires -Version 7
<#
IS04 (IMPROVE-SEC) — the lane's baseline numbers, printed as a table.

The values are the *starting* baselines the lane retires to zero (plan
Evidence table, measured 2026-11-06 on main @ 1970cfc4). They are
hard-coded here on purpose: IS43 replaces this block with live
computation (or deletes it, since the checks then live in the SecGate
xunit tests). Read-only; no build required.
#>

# Walk up to the repo root (the directory containing Kumunita.slnx) so the
# script runs from anywhere in the tree.
$dir = (Get-Location).Path
while (-not (Test-Path (Join-Path $dir "Kumunita.slnx"))) {
    $dir = Split-Path $dir -Parent
    if ([string]::IsNullOrWhiteSpace($dir)) { break }
}
if (-not (Test-Path (Join-Path $dir "Kumunita.slnx"))) {
    Write-Error "Could not locate the repo root (Kumunita.slnx)."
}

$baselines = [ordered]@{
    "M1: control-map rows with a pinned seam"           = "9/24"
    "M2: principal-bearing types with no erasure"       = "25/65"
    "M4: PII log placeholders"                          = "7"
    "M7: unmarked decision-free reads"                  = "52"
    "Inline on* handlers in views"                      = "8"
    "Unaudited admin reads of class (c)/PII"            = "3"
}

Write-Host "IMPROVE-SEC baselines (IS04; the lane retires each to zero):"
Write-Host ("-" * 52)
foreach ($metric in $baselines.Keys) {
    "{0,-44} {1,-8}" -f $metric, $baselines[$metric] | Write-Host
}
Write-Host ("-" * 52)
Write-Host "Gates: tests/Kumunita.Web.Tests/SecGate/ (IS15, IS24, IS38b, IS40)."
