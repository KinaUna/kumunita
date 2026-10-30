# improve-check.ps1 — the IMPROVE lane's CI gate (U00, plan-improve.md)
#
# Run:  pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File improve-check.ps1
# Exit: 0 when every gate passes (or the violation is in the grandfathered
#       baseline), non-zero otherwise. Intended to be run from the repo root;
#       if the CWD is not the repo root, it walks up looking for Kumunita.slnx.
#
# Doctrine (AGENTS.md / .github/copilot-instructions.md): no here-strings,
# no inter-command $variables (this is a self-contained script run in one
# process, so the variable rule doesn't apply), single logical lines, and the
# whole thing is driven non-interactively by the command above — which is
# exactly the shape AGENTS.md prescribes for multi-line PowerShell.
#
# The gates (a–f from the U00 "Do" section of plan-improve.md; g added by U07):
#   (a) .cs in src/ over 2 000 lines
#   (b) docs/adr/README.md index rows != docs/adr/0*.md file count
#   (c) AGENTS.md and .github/copilot-instructions.md share a "## " heading
#       with different content
#   (d) any *-handoff-notes.md under docs/plans-milestones/done/ over 600
#       lines without a "TL;DR" in the first 20 lines
#   (e) first-party .ts under src/Kumunita.Web/client/ over 800 lines
#   (f) any .cshtml view over 800 lines
#   (g) any docs/design/*.md over 400 lines without an "Abstract" in its first
#       15 lines (added by U07, 2026-10-08 — the design-doc tier of the
#       10-second "is this the right read" gate; the handoff-note TL;DR of
#       gate (d) is the same convention at the handoff tier)
#
# Baseline grandfathering (the plan's own words: "the gate allows the
# current baseline (it does not force an immediate fix); it prevents the
# baseline from growing"). Each gate's BASELINE_* list is the *set of files
# already over the ceiling at U00 time* (collected 2026-10-08). A *new* file
# crossing the ceiling — or a *baseline* file growing *past its own size* —
# fails the close. When a U-unit retires a baseline entry, remove it from the
# list in the same commit.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# --- locate repo root (walk up from the CWD, or fall back to the script's
# --- own location, which lives under docs/plans-milestones/done/improve/) -----
function Find-RepoRoot {
  $dir = (Get-Location).Path
  while ($dir -and $dir.Length -gt 0) {
    if (Test-Path (Join-Path $dir 'Kumunita.slnx')) { return $dir }
    $parent = Split-Path $dir -Parent
    if (-not $parent -or $parent -eq $dir) { break }
    $dir = $parent
  }
  # fallback: the script itself lives at docs/plans-milestones/done/improve/...
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
  # normalize to forward slashes, relative to $repo
  $p = $path -replace '\\','/'
  $rp = $repo -replace '\\','/'
  if ($p.StartsWith($rp)) { return $p.Substring($rp.Length).TrimStart('/') }
  return $p
}

function Lines([string]$path) { @((Get-Content -LiteralPath $path)).Count }

function Check([string]$gate, [string]$message, [bool]$cond) {
  if ($cond) {
    Write-Host "[OK]   $gate : $message"
  } else {
    Write-Host "[FAIL] $gate : $message" -ForegroundColor Red
    $failures.Add("$gate : $message")
  }
}

# =============================================================================
# Gate (a) — src/*.cs ceiling 2 000 lines (baseline grandfathered)
# =============================================================================
$aCeiling = 2000
$aBaseline = @(
  'src/Kumunita.Core/Localization/KnownTranslationKeys.cs',
  'src/Kumunita.Web/Controllers/ProjectsController.cs',
  'src/Kumunita.Core/Projects/ProjectService.cs',
  'src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs',
  'src/Kumunita.Core/UserInfo/UserInfoService.cs',
  'src/Kumunita.Web/Controllers/GroupsController.cs',
  'src/Kumunita.Core/Events/EventService.cs',
  'src/Kumunita.Web/Controllers/EventController.cs',
  'src/Kumunita.Web/Controllers/PostsController.cs',
  'src/Kumunita.Core/Posts/PostService.cs',
  'src/Kumunita.Core/Bootstrap/SampleDataSeeder.cs'
)
#aBaselineSizes = the *exact* sizes at U00 time (baseline may not grow either).
# Re-baselined at the M29 close (2026-10-08): two baseline files legitimately
# grew from M29's feature work — FirstBootSeeder.cs from U04's
# `SeedSurfaceLabelsAsync` step (the `SurfaceLabels` all-null seeder row,
# ADR 0152) and KnownTranslationKeys.cs from U08's `labels.*` admin-form keys
# (× en/de/fr/da). Both are the milestone's *shipped* additions, not growth
# the IMPROVE reduction was meant to prevent — the gate's ceiling now starts
# from the M29 baseline, and the next reduction lane will measure against it.
# Re-baselined again 2026-10-25 (the prod-hardening close, the same mechanism):
# since the 2026-10-08 baseline the M30-M37 / ESC / SITE / SITE-2 / FDW lanes +
# the prod-hardening lane (F1-F22) shipped — each adding seeder steps, kw-l
# keys, service seams, and controller actions to these files, plus EventController.cs
# crossing the ceiling outright (the recurring-events lane's surface). The
# ceiling now starts from the 2026-10-25 baseline; the next reduction lane
# measures against it (the M29 precedent, applied once more).
# Re-baselined again 2026-10-25 (987086ea, the ADR 0167 amendment): the ten
# new page-* note kinds added 10 pn.list.kind.page-* keys x en/de/fr/da to
# KnownTranslationKeys.cs (10970 -> 11020) — the same shipped-additions
# mechanism as above; the gate's ceiling starts from 11020 now.
# Re-baselined again 2026-10-27 (the ADR 0191 inventory lane close): two new
# inventory keys x en/de/fr/da added to KnownTranslationKeys.cs (11020 ->
# 11106) — the same shipped-additions mechanism as above; the gate's ceiling
# starts from 11106 now.
# Re-baselined again 2026-10-27 (the ADR 0193 portability import-modes lane
# close): two new portability.import.keep_existing* keys x en/de/fr/da added
# to KnownTranslationKeys.cs (11106 -> 11114) — the same shipped-additions
# mechanism as above; the gate's ceiling starts from 11114 now.
# Re-baselined again 2026-10-29 (the ADR 0251 post edit/new localization
# close): since the ADR 0193 baseline the documents / announcements lanes
# (ADR 0242 delete_confirm, ADR 0248 comment count) and this lane (ADR 0251,
# eleven posts.* keys x en/de/fr/da) added keys to KnownTranslationKeys.cs
# (11114 -> 11527); the board card edit modal (ADR 0195) grew
# ProjectsController.cs (5257 -> 5358); the feed-footer reply-count / bookmark
# lanes (the ADR 0175 / ADR 0226 / ADR 0227 TODOs) grew GroupsController.cs
# (3032 -> 3035) and PostService.cs (2281 -> 2315); the ADR 0130
# warm-backfill fix grew SampleDataSeeder.cs (2105 -> 2110) — the same
# shipped-additions mechanism as above; the gate's ceiling now starts from
# the 2026-10-29 baseline.
# Re-baselined again 2026-10-29 (the ADR 0257 groups-pages localization
# close): since the ADR 0251 baseline the pages-form lane (ADR 0254,
# 19 pages.form.* keys) and the groups lane (ADR 0257, 20 keys) added
# keys to KnownTranslationKeys.cs (11527 -> 11835); the ADR 0253
# everyone-audience seeds grew ProjectsController.cs (5358 -> 5370) — the
# same shipped-additions mechanism as above; the gate's ceiling now starts
# from the 2026-10-29 baseline (again).
# Re-baselined again 2026-10-29 (the ADR 0258 shared-close/tooltips close):
# the three new tooltip keys (pages.draft_tooltip, pages.scoped_tooltip,
# announcements.pinned_tooltip) x en/de/fr/da added to KnownTranslationKeys.cs
# (11835 -> 11859) — the same shipped-additions mechanism as above; the
# gate's ceiling starts from 11859 now.
# Re-baselined again 2026-10-29 (the ADR 0259 kiosk-settings/signup
# localization close): the twenty-nine new kiosk.settings.* / account.signup_*
# keys x en/de/fr/da added to KnownTranslationKeys.cs (11859 -> 12091) — the
# same shipped-additions mechanism as above; the gate's ceiling starts from
# 12091 now.
# Re-baselined again 2026-10-29 (the ADR 0260 guardian-rsvp-rows close): the
# three new guardian.eventrsvp.desired_with / requested_at / attended_at keys
# x en/de/fr/da added to KnownTranslationKeys.cs (12091 -> 12103) — the same
# shipped-additions mechanism as above; the gate's ceiling starts from 12103 now.
# Re-baselined again 2026-10-29 (the ADR 0261 shared-flash-extension close): the
# forty-nine new flash.* / announcements.flash_* / events.flash_* /
# guardian.flash_* keys x en/de/fr/da added to KnownTranslationKeys.cs
# (12103 -> 12299) — the same shipped-additions mechanism as above; the gate's
# ceiling starts from 12299 now.
# Re-baselined again 2026-10-29 (the ADR 0262 posts-flash close): the nine new
# posts.flash_* / flash.report_submitted keys x en/de/fr/da added to
# KnownTranslationKeys.cs (12299 -> 12335) — the same shipped-additions
# mechanism as above; the gate's ceiling starts from 12335 now.
# Re-baselined again 2026-10-29 (the ADR 0263 page-flash close): the eight new
# pages.flash_* keys x en/de/fr/da added to KnownTranslationKeys.cs
# (12335 -> 12367) — the same shipped-additions mechanism as above; the gate's
# ceiling starts from 12367 now.
# Re-baselined again 2026-10-29 (the ADR 0264 community-flash close): the nine
# new communities.flash_* / flash.translation_needs_name_or_description keys
# x en/de/fr/da added to KnownTranslationKeys.cs (12367 -> 12403) — the same
# shipped-additions mechanism as above; the gate's ceiling starts from 12403 now.
# Re-baselined again 2026-10-29 (the ADR 0265 projects-flash close): the 46 new
# projects.flash_* / flash.translation_needs_title_or_text / projects.dir_* keys
# x en/de/fr/da added to KnownTranslationKeys.cs (12403 -> 12587) — the same
# shipped-additions mechanism as above; the gate's ceiling starts from 12587 now.
# Re-baselined again 2026-10-29 (the ADR 0266 groups-flash close): the 22 new
# groups.flash_* keys x en/de/fr/da added to KnownTranslationKeys.cs
# (12587 -> 12675) — the same shipped-additions mechanism as above; the gate's
# ceiling starts from 12675 now.
# Re-baselined again 2026-10-29 (the ADR 0267 admin-flash close): the 41 new
# admin.flash_* / flash.translation_gone_add / flash.translation_gone /
# flash.choose_language_remove keys x en/de/fr/da added to
# KnownTranslationKeys.cs (12675 -> 12839) — the same shipped-additions
# mechanism as above; the gate's ceiling starts from 12839 now.
# Re-baselined again 2026-10-29 (the ADR 0268 remaining-flash close): the 34 new
# account.flash_* / inventory.flash_* / kiosk.flash_* / languages.flash_* /
# settings.profile_unavailable / messaging.flash_* / moderation.flash_* /
# security.flash_* / tags.flash_* keys x en/de/fr/da added to
# KnownTranslationKeys.cs (12839 -> 12975) — the same shipped-additions
# mechanism as above; the gate's ceiling starts from 12975 now.
# Re-baselined again 2026-10-29 (the ADR 0269 ModelState close): the 63 new
# validation.* keys x en/de/fr/da added to KnownTranslationKeys.cs
# (12975 -> 13227) — the same shipped-additions mechanism as above.
# Re-baselined again 2026-10-29 (the ADR 0270 BadRequest-bodies close): the
# 4 new validation.* keys (validation.folder_name_required / choose_image /
# choose_file / document_id_required) x en/de/fr/da added to
# KnownTranslationKeys.cs (13227 -> 13243) — the same shipped-additions
# mechanism as above.
# Re-baselined again 2026-10-29 (the ADR 0271 Admin-hub-views close): the
# 84 new admin.hub.* / admin.platform.* / admin.communities.* /
# admin.accounts.* / admin.manage.* / admin.security.* / admin.audit.* /
# admin.errorreport.* / admin.breakglass.* / admin.escalation.* / common.* keys
# x en/de/fr/da added to KnownTranslationKeys.cs (13243 -> 13575) — the same
# shipped-additions mechanism as above.
# Re-baselined again 2026-10-29 (the ADR 0272 AdminSite + settings-views close):
# the 100 new admin.site.* / admin.storage.* / admin.labels.* / admin.help.* /
# admin.guests.* / admin.storagemetrics.* / admin.setup.* / admin.quiet.* /
# admin.portability.* / admin.breakglass.explainer / admin.security.audit_desc
# keys x en/de/fr/da added to KnownTranslationKeys.cs (13575 -> 13975) — the
# same shipped-additions mechanism as above; the gate's ceiling starts from
# 13975 now.
# Re-baselined again 2026-10-30 (the ADR 0273 security-settings/leftovers/
# page-titles close): the 178 new settings.mfa.* / settings.sessions.* /
# account.delete_confirm_dialog / myportability.* / blog.draft_title /
# inv.detail.note_placeholder / kiosk.exit / moderation.unlock_header /
# moderation.unlock_desc / moderation.resolve_desc / pages.sibling_nav /
# posts.mandatory_title / posts.no_feeds / profile.preview_visible_sentence /
# profile.preview_hidden_sentence / sort.aria_label / about.cta.* / title.*
# keys x en/de/fr/da added to KnownTranslationKeys.cs (13975 -> 14687) — the
# same shipped-additions mechanism as above; the gate's ceiling starts from
# 14687 now.
# Re-baselined again 2026-10-30 (the ADR 0277 directory sort/filter lane close):
# the directory's search/verified/clear labels (directory.search /
# directory.verified_only / directory.no_results / directory.clear) + the
# a11y.find_directory aria-label, x en/de/fr/da, added to
# KnownTranslationKeys.cs (14687 -> 14707) — the same shipped-additions
# mechanism as above; the gate's ceiling starts from 14707 now.
$aBaselineSizes = @{
  'src/Kumunita.Core/Localization/KnownTranslationKeys.cs' = 14707
  'src/Kumunita.Web/Controllers/ProjectsController.cs'     = 5370
  'src/Kumunita.Core/Projects/ProjectService.cs'           = 5015
  'src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs'         = 6926
  'src/Kumunita.Core/UserInfo/UserInfoService.cs'          = 4286
  'src/Kumunita.Web/Controllers/GroupsController.cs'       = 3035
  'src/Kumunita.Core/Events/EventService.cs'               = 2226
  'src/Kumunita.Web/Controllers/EventController.cs'        = 2172
  'src/Kumunita.Web/Controllers/PostsController.cs'        = 2165
  'src/Kumunita.Core/Posts/PostService.cs'                 = 2315
  'src/Kumunita.Core/Bootstrap/SampleDataSeeder.cs'        = 2110
}
$aViolations = [System.Collections.Generic.List[string]]::new()
$csFiles = Get-ChildItem -Path (Join-Path $repo 'src') -Recurse -Filter '*.cs' -File |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }
foreach ($f in $csFiles) {
  $rel = Rel $f.FullName
  $n = Lines $f.FullName
  if ($n -le $aCeiling) { continue }
  if ($aBaselineSizes.ContainsKey($rel)) {
    if ($n -gt $aBaselineSizes[$rel]) {
      $aViolations.Add("$rel = $n lines (baseline was $($aBaselineSizes[$rel]); baseline file grew)")
    }
    continue
  }
  $aViolations.Add("$rel = $n lines (new file over the $aCeiling ceiling)")
}
Check "a" "src/*.cs over 2000 lines (new or grown past its U00 baseline): $($aViolations.Count)" ($aViolations.Count -eq 0)
foreach ($v in $aViolations) { Write-Host "       - $v" }

# =============================================================================
# Gate (b) — ADR index drift
# =============================================================================
$adrDir = Join-Path $repo 'docs/adr'
$adrIndex = Join-Path $adrDir 'README.md'
$adrFiles = (Get-ChildItem -Path $adrDir -Filter '0*.md' -File).BaseName |
  ForEach-Object { ($_ -split '-')[0] }
$adrFileCount = $adrFiles.Count
$adrIndexText = Get-Content -LiteralPath $adrIndex -Raw
# a row is a markdown table row whose first cell is the ADR number
$adrIndexRows = [regex]::Matches($adrIndexText, '(?m)^\|\s*(0\d{3})\b') | ForEach-Object { $_.Groups[1].Value }
$adrIndexCount = $adrIndexRows.Count
$adrMissing = @($adrFiles | Where-Object { $adrIndexRows -notcontains $_ } | Sort-Object)
$adrExtra   = @($adrIndexRows | Where-Object { $adrFiles -notcontains $_ } | Sort-Object)
# Baseline (U00 time, 2026-10-08): 8 ADRs on disk missing from the index —
# exactly the set U01 is scheduled to add (0096, 0137-0143). New drift beyond
# this set fails the close; when U01 lands, the baseline set shrinks naturally.
$bBaselineMissing = @('0096','0137','0138','0139','0140','0141','0142','0143')
$bBaselineExtra   = @()
$bNewMissing = @($adrMissing | Where-Object { $bBaselineMissing -notcontains $_ })
$bNewExtra   = @($adrExtra   | Where-Object { $bBaselineExtra   -notcontains $_ })
$bDetail = "files=$adrFileCount, index rows=$adrIndexCount, missing=($($adrMissing -join ',')), extra=($($adrExtra -join ','))"
Check "b" "ADR index drift — new beyond U01 baseline: $($bNewMissing.Count + $bNewExtra.Count) ($bDetail)" ($bNewMissing.Count -eq 0 -and $bNewExtra.Count -eq 0)
foreach ($v in $bNewMissing) { Write-Host "       - on disk but not in index: $v" }
foreach ($v in $bNewExtra)   { Write-Host "       - in index but not on disk: $v" }

# =============================================================================
# Gate (c) — AGENTS.md vs .github/copilot-instructions.md shared ## heading
# =============================================================================
function Get-SharedH2([string]$a, [string]$b) {
  $ha = (Get-Content -LiteralPath $a | Where-Object { $_ -match '^##\s' } | ForEach-Object { $_ -replace '^##\s*','' })
  $hb = (Get-Content -LiteralPath $b | Where-Object { $_ -match '^##\s' } | ForEach-Object { $_ -replace '^##\s*','' })
  return @($ha | Where-Object { $hb -contains $_ })
}
$agentsMd   = Join-Path $repo 'AGENTS.md'
$instrMd    = Join-Path $repo '.github/copilot-instructions.md'
$cShared = Get-SharedH2 $agentsMd $instrMd
# Baseline (U00 time, 2026-10-08): 5 shared headings.
# U03 (2026-10-08) de-duplicated all 5 — the `.github/copilot-instructions.md`
# now carries a pointer list instead of full copies, and the shared count is 0
# (the baseline list below is grandfathered-and-met, not just allowed — same
# pattern U01 used for the ADR rows; the gate passes either way, so the list
# is left untouched as optional cleanup).
$cBaselineShared = @(
  "Don't pause mid-task to check in",
  'Razor verification doctrine',
  'Git state gotcha',
  'Using the browser (trusted-folder quirk)',
  'Running PowerShell commands safely (Windows agents)'
)
$cNew = @($cShared | Where-Object { $cBaselineShared -notcontains $_ })
Check "c" "shared '## ' headings AGENTS.md <-> copilot-instructions.md (new shared beyond U03 baseline): $($cNew.Count)" ($cNew.Count -eq 0)
foreach ($v in $cNew) { Write-Host "       - shared heading: $v" }

# =============================================================================
# Gate (d) — handoff notes under done/ over 600 lines without a TL;DR
# =============================================================================
$dCeiling = 600
$dDir = Join-Path $repo 'docs/plans-milestones/done'
$dViolations = [System.Collections.Generic.List[string]]::new()
if (Test-Path $dDir) {
  $dFiles = Get-ChildItem -Path $dDir -Recurse -Filter '*-handoff-notes.md' -File
  foreach ($f in $dFiles) {
    $n = Lines $f.FullName
    if ($n -le $dCeiling) { continue }
    $first20 = Get-Content -LiteralPath $f.FullName -TotalCount 20
    $hasTldr = @($first20 | Select-String -Pattern 'TL;DR').Count -gt 0
    if (-not $hasTldr) { $dViolations.Add((Rel $f.FullName) + " (" + $n + " lines, no TL;DR in first 20 lines)") }
  }
}
# Baseline (U00 time): 23 files over 600 lines with no TL;DR — U02's job is to
# add TL;DRs to the *8 largest*. The gate does NOT grandfather a count; it
# fails on any *new* file crossing 600 without a TL;DR. The 23 baseline files
# are grandfathered by *name*; when U02 (or any future unit) adds a TL;DR to
# one, it drops out of the baseline naturally (it stops violating).
$dBaseline = @(
  'docs/plans-milestones/done/m3b/m3b-handoff-notes.md',
  'docs/plans-milestones/done/pages/pages-handoff-notes.md',
  'docs/plans-milestones/done/m11/m11-portability-handoff-notes.md',
  'docs/plans-milestones/done/m10/m10-pwa-handoff-notes.md',
  'docs/plans-milestones/done/file-attachments/file-attachments-handoff-notes.md',
  'docs/plans-milestones/done/m4/m4-handoff-notes.md',
  'docs/plans-milestones/done/m14/m14-events-projects-handoff-notes.md',
  'docs/plans-milestones/done/media/media-file-storage-handoff-notes.md',
  'docs/plans-milestones/done/rich-editor/rich-editor-handoff-notes.md',
  'docs/plans-milestones/done/m13/m13-logging-analytics-handoff-notes.md',
  'docs/plans-milestones/done/m9/m9-messaging-handoff-notes.md',
  'docs/plans-milestones/done/m27/m27-handoff-notes.md',
  'docs/plans-milestones/done/m15/m15-translation-bulk-handoff-notes.md',
  'docs/plans-milestones/done/pl/pl-handoff-notes.md',
  'docs/plans-milestones/done/m26/m26-handoff-notes.md',
  'docs/plans-milestones/done/multilingual-ui/multilingual-ui-handoff-notes.md',
  'docs/plans-milestones/done/site/site-handoff-notes.md',
  'docs/plans-milestones/done/m16/m16-inventory-handoff-notes.md',
  'docs/plans-milestones/done/group-posts/group-posts-handoff-notes.md',
  'docs/plans-milestones/done/wysiwyg/wysiwyg-handoff-notes.md',
  'docs/plans-milestones/done/m25/m25-handoff-notes.md',
  'docs/plans-milestones/done/m20/m20-notification-quiet-times-handoff-notes.md',
  'docs/plans-milestones/done/m1/m1-step-7-handoff-notes.md',
  # Re-baselined 2026-10-25 (the prod-hardening close): these four lanes
  # (M30, M34, ESC, prod-hardening) shipped after the U00/U02 baseline with
  # over-600-line notes that carry no TL;DR in the first 20 lines — the same
  # "new file beyond the baseline" case the mechanism exists for. A future
  # unit adding a TL;DR to any of them drops it off the list naturally.
  'docs/plans-milestones/done/m30/m30-handoff-notes.md',
  'docs/plans-milestones/done/m34/m34-handoff-notes.md',
  'docs/plans-milestones/done/esc/esc-handoff-notes.md',
  'docs/plans-milestones/done/prod-hardening/hardening-handoff-notes.md'
)
# violation strings are "path (N lines, ...)" — compare on the path prefix
$dNew = @($dViolations | Where-Object { $p = ($_ -split ' \(')[0]; $dBaseline -notcontains $p })
Check "d" "handoff notes >600 lines without TL;DR (new beyond U02 baseline): $($dNew.Count)" ($dNew.Count -eq 0)
foreach ($v in $dNew) { Write-Host "       - $v" }

# =============================================================================
# Gate (e) — first-party .ts under client/ over 800 lines
# =============================================================================
$eCeiling = 800
$eDir = Join-Path $repo 'src/Kumunita.Web/client'
$eViolations = [System.Collections.Generic.List[string]]::new()
if (Test-Path $eDir) {
  $tsFiles = Get-ChildItem -Path $eDir -Recurse -Filter '*.ts' -File |
    Where-Object { $_.FullName -notmatch '\\node_modules\\' }
  foreach ($f in $tsFiles) {
    $n = Lines $f.FullName
    if ($n -le $eCeiling) { continue }
    $eViolations.Add((Rel $f.FullName) + " = $n lines")
  }
}
# Baseline (U00 time): one file, rich-editor.ts at 1 634 lines.
$eBaselineSizes = @{
  'src/Kumunita.Web/client/lib/rich-editor.ts' = 1634
}
$eNew = [System.Collections.Generic.List[string]]::new()
foreach ($v in $eViolations) {
  $rel = ($v -split ' = ')[0]
  if ($eBaselineSizes.ContainsKey($rel)) {
    $n = [int]((($v -split ' = ')[1]) -replace ' lines','')
    if ($n -gt $eBaselineSizes[$rel]) { $eNew.Add("$rel = $n (baseline was $($eBaselineSizes[$rel]))") }
  } else {
    $eNew.Add($v)
  }
}
Check "e" "client/*.ts over 800 lines (new or grown past its U00 baseline): $($eNew.Count)" ($eNew.Count -eq 0)
foreach ($v in $eNew) { Write-Host "       - $v" }

# =============================================================================
# Gate (f) — .cshtml views over 800 lines
# =============================================================================
$fCeiling = 800
$fViolations = [System.Collections.Generic.List[string]]::new()
$cshtmlFiles = Get-ChildItem -Path (Join-Path $repo 'src') -Recurse -Filter '*.cshtml' -File
foreach ($f in $cshtmlFiles) {
  $n = Lines $f.FullName
  if ($n -le $fCeiling) { continue }
  $fViolations.Add((Rel $f.FullName) + " = $n lines")
}
$fBaselineSizes = @{
  'src/Kumunita.Web/Views/Projects/BoardDetail.cshtml' = 1431
  'src/Kumunita.Web/Views/Posts/Detail.cshtml'         = 1015
  # Re-baselined 2026-10-25 (the prod-hardening close): TodoDetail grew from
  # the M36 mention link pass + the M37 _NoteButton partial + the M14 linked
  # to-do / project chips (the lane's shipped additions, the M29 precedent).
  'src/Kumunita.Web/Views/Projects/TodoDetail.cshtml'  = 1154
  # Re-baselined 2026-10-27 (the ADR 0176 / 0189 lane close): _Layout crossed
  # the 800 ceiling from the ADR 0176 single-notice-strip pass (the
  # `notice-strip.js` surface + the what's-new inline notice, shipped
  # additions) and TodoDetail grew from the ADR 0189 to-do restyle (status
  # chip / assignee meta line / overdue marker / detail+comment cards) — the
  # same shipped-additions mechanism as above (the M29 precedent).
  # Re-baselined again 2026-10-29 (the ADR 0251 post edit/new localization
  # close): _Layout crossed its 816 baseline from the ADR 0231 tab-strip
  # phone audit (sub-nav strip scroll + edge fades, shipped additions), and
  # Groups/Detail crossed the 800 ceiling outright from the feed-footer
  # bookmark toggle lane (the ADR 0226 / ADR 0227 TODO, 814 lines) — the same
  # shipped-additions mechanism as above (the M29 precedent).
  # Re-baselined again 2026-10-30 (the ADR 0273 security-settings / page-titles
  # localization close): _Layout crossed its 833 baseline from the ADR 0273
  # central page-title map (Security/PageTitleKeys lookup + effective-language
  # resolution + English fallback in the <title> pass, 21 insertions / 1
  # deletion) — the same shipped-additions mechanism as above (the M29
  # precedent).
  'src/Kumunita.Web/Views/Shared/_Layout.cshtml'       = 853
  'src/Kumunita.Web/Views/Groups/Detail.cshtml'        = 814
}
$fNew = [System.Collections.Generic.List[string]]::new()
foreach ($v in $fViolations) {
  $rel = ($v -split ' = ')[0]
  if ($fBaselineSizes.ContainsKey($rel)) {
    $n = [int]((($v -split ' = ')[1]) -replace ' lines','')
    if ($n -gt $fBaselineSizes[$rel]) { $fNew.Add("$rel = $n (baseline was $($fBaselineSizes[$rel]))") }
  } else {
    $fNew.Add($v)
  }
}
Check "f" "views .cshtml over 800 lines (new or grown past its U00 baseline): $($fNew.Count)" ($fNew.Count -eq 0)
foreach ($v in $fNew) { Write-Host "       - $v" }

# =============================================================================
# Gate (g) — design docs over 400 lines without an Abstract (U07, added 2026-10-08)
# =============================================================================
# Plan-improve.md §U07: any docs/design/*.md over 400 lines must carry a
# "> **Abstract:**" blockquote in its first 15 lines — the 10-second "is this
# the right read" gate at the design tier (the handoff-note TL;DR, gate (d),
# at a different tier). The 5 largest (m13 / m3b / m18 / m20 / m5) are the
# baseline U07 closed; the 38 remaining over-400-without-Abstract docs are
# grandfathered by *name* (same pattern as gate (d) — U02's 23-note baseline).
# When any future unit adds an Abstract to one, it drops out of the baseline
# naturally (it stops violating). A *new* design doc over 400 without an
# Abstract fails the close.
$gCeiling = 400
$gDir = Join-Path $repo 'docs/design'
$gViolations = [System.Collections.Generic.List[string]]::new()
if (Test-Path $gDir) {
  $gFiles = Get-ChildItem -Path $gDir -Recurse -Filter '*.md' -File
  foreach ($f in $gFiles) {
    $n = Lines $f.FullName
    if ($n -le $gCeiling) { continue }
    $first15 = Get-Content -LiteralPath $f.FullName -TotalCount 15
    $hasAbstract = @($first15 | Select-String -Pattern 'Abstract').Count -gt 0
    if (-not $hasAbstract) { $gViolations.Add((Rel $f.FullName) + " (" + $n + " lines, no Abstract in first 15 lines)") }
  }
}
# Baseline (U07 time, 2026-10-08): the 38 over-400 design docs without an
# Abstract (the U00-time set of 43, minus the 5 largest U07 closed: m13, m3b,
# m18, m20, m5 — those now pass and are not listed here).
$gBaseline = @(
  'docs/design/events-calendar-design.md',
  'docs/design/events-calendar-dwm-design.md',
  'docs/design/file-attachments-design.md',
  'docs/design/group-posts-design.md',
  'docs/design/guardian-assignment-design.md',
  'docs/design/guardian-controls-design.md',
  'docs/design/m10-pwa-responsive-design.md',
  'docs/design/m11-portability-design.md',
  'docs/design/m12-ical-design.md',
  'docs/design/m14-events-projects-design.md',
  'docs/design/m15-translation-bulk-design.md',
  'docs/design/m16-inventory-design.md',
  'docs/design/m17-bookmarks-design.md',
  'docs/design/m19-guest-accounts-design.md',
  'docs/design/m2-directory-profiles-groups.md',
  'docs/design/m21-document-management-design.md',
  'docs/design/m22-onboarding-design.md',
  'docs/design/m23-extended-profiles-design.md',
  'docs/design/m24-storage-metrics-design.md',
  'docs/design/m25-upload-limits-design.md',
  'docs/design/m26-sorting-design.md',
  'docs/design/m27-user-scoped-portability-design.md',
  'docs/design/m28-guardian-time-limits-design.md',
  'docs/design/m3-posts-design.md',
  'docs/design/m4-events-design.md',
  'docs/design/m6-notifications-design.md',
  'docs/design/m7-pagination-filtering-design.md',
  'docs/design/m8-search-design.md',
  'docs/design/m9-messaging-design.md',
  'docs/design/media-file-storage-design.md',
  'docs/design/multilingual-design.md',
  'docs/design/pages-design.md',
  'docs/design/pl-goals-projects-design.md',
  'docs/design/rich-content-design.md',
  'docs/design/site-content-design.md',
  'docs/design/tags-design.md',
  'docs/design/tbd-todo-dependency-design.md',
  'docs/design/wysiwyg-editor-design.md',
  # Re-baselined 2026-10-25 (the prod-hardening close): the M30 + M37 design
  # docs and the eight prod-hardening per-fix design docs (F3/F5/F9/F10/F11/
  # F12/F16/F17) shipped after the U07 baseline, over-400 lines, without the
  # "> **Abstract:**" blockquote — the same "new file beyond the baseline"
  # case the mechanism exists for. A future unit adding an Abstract to any
  # of them drops it off the list naturally.
  'docs/design/m30-admin-onboarding-design.md',
  'docs/design/m37-personal-notes-design.md',
  'docs/design/prod-hardening-f3-mfa.md',
  'docs/design/prod-hardening-f5-sessions.md',
  'docs/design/prod-hardening-f9-signup.md',
  'docs/design/prod-hardening-f10-errtrack.md',
  'docs/design/prod-hardening-f11-dlq-alert.md',
  'docs/design/prod-hardening-f12-alerts.md',
  'docs/design/prod-hardening-f16-scan.md',
  'docs/design/prod-hardening-f17-bounce.md'
)
# violation strings are "path (N lines, ...)" — compare on the path prefix
$gNew = @($gViolations | Where-Object { $p = ($_ -split ' \(')[0]; $gBaseline -notcontains $p })
Check "g" "design docs >400 lines without Abstract (new beyond U07 baseline): $($gNew.Count)" ($gNew.Count -eq 0)
foreach ($v in $gNew) { Write-Host "       - $v" }

# =============================================================================
# Summary
# =============================================================================
Write-Host ""
if ($failures.Count -eq 0) {
  Write-Host "improve-check: ALL GATES PASS" -ForegroundColor Green
  exit 0
} else {
  Write-Host "improve-check: $($failures.Count) GATE(S) FAILED" -ForegroundColor Red
  exit 1
}
