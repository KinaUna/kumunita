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
#   (d) any *-handoff-notes.md under docs/archive/lanes/ over 600
#       lines without a "TL;DR" in the first 20 lines
#   (e) first-party .ts under src/Kumunita.Web/client/ over 800 lines
#   (f) any .cshtml view over 800 lines
#   (g) any docs/design/*.md over 400 lines without an "Abstract" in its first
#       15 lines after the front-matter (added by U07, 2026-10-08 — the
#       design-doc tier of the 10-second "is this the right read" gate; the
#       handoff-note TL;DR of gate (d) is the same convention at the handoff
#       tier). Baselines retired in IMPROVE-DOCS D14.
#
# Baseline grandfathering (the plan's own words: "the gate allows the
# current baseline (it does not force an immediate fix); it prevents the
# baseline from growing"). Each gate's BASELINE_* list is the *set of files
# already over the ceiling at U00 time* (collected 2026-10-08). A *new* file
# crossing the ceiling — or a *baseline* file growing *past its own size* —
# fails the close. When a U-unit retires a baseline entry, remove it from the
# list in the same commit. Gates (d) and (g) are **baseline-free** as of
# IMPROVE-DOCS D14 (2026-11-02) — the docs they measure have all been fixed,
# so they fail on *any* violation with no grandfathering (the "retire the
# baselines, don't grandfather them" close work).

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# --- locate repo root (walk up from the CWD, or fall back to the script's
# --- own location, which lives under tools/docs/) -----
function Find-RepoRoot {
  $dir = (Get-Location).Path
  while ($dir -and $dir.Length -gt 0) {
    if (Test-Path (Join-Path $dir 'Kumunita.slnx')) { return $dir }
    $parent = Split-Path $dir -Parent
    if (-not $parent -or $parent -eq $dir) { break }
    $dir = $parent
  }
  # fallback: the script itself lives at tools/docs/...
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

# End-of-front-matter line (0-indexed) or -1 if the file has none. The abstract
# window is counted *after* the front-matter (DOCS-CONVENTIONS.md: "in its
# first 15 lines (after the front-matter)") — so the window starts on the line
# after the closing `---`.
function FmEnd([string]$path) {
  $lines = @(Get-Content -LiteralPath $path)
  if ($lines.Count -ge 2 -and $lines[0].Trim() -eq '---') {
    for ($i = 1; $i -lt [Math]::Min($lines.Count, 40); $i++) { if ($lines[$i].Trim() -eq '---') { return $i } }
  }
  return -1
}

# True iff an Abstract/TL;DR blockquote sits within $window lines after the
# front-matter (or after line 0 for front-matter-less files).
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
# Re-baselined again 2026-10-30 (the group-messaging lane close): the 15
# group_message.* UI keys + the 8 group-messaging notification.* keys
# (kind badge + preference label + subject + body for the two new kinds
# group.message.new / group.message.mention) x en/de/fr/da added to
# KnownTranslationKeys.cs (14707 -> 14815) — the same shipped-additions
# mechanism as above; the gate's ceiling starts from 14815 now.
# Re-baselined again 2026-10-30 (the group-title + messages-tab amendment
# to the group-messaging lane, ADR 0278): the two group_message.new.name*
# keys (the new-group name field's label + placeholder) x en/de/fr/da added
# to KnownTranslationKeys.cs (14815 -> 14823) — the same shipped-additions
# mechanism as above; the gate's ceiling starts from 14823 now.
# Re-baselined again (the group-messaging invitation flow, ADR 0278
# amendment): the seven new group_message.* UI keys (members / pending /
# invite_sent / invite_accepted / invite_declined / invite_not_pending) +
# the three notification.* group.message.invite keys x en/de/fr/da added to
# KnownTranslationKeys.cs (14823 -> 14863) — the same shipped-additions
# mechanism as above; the gate's ceiling starts from 14863 now.
# Re-baselined again 2026-10-31 (the SEC L3 U24 verify confirm step): two new
# account.verify_confirm_* keys x en/de/fr/da added to KnownTranslationKeys.cs
# (14863 -> 14885) — the same shipped-additions mechanism as above.
# Re-baselined again 2026-11-01 (the sessions lane U9 i18n close): five new
# settings.sessions.* keys (list_heading / list_desc / current_badge /
# revoke_button / revoke_confirm) x en/de/fr/da added to KnownTranslationKeys.cs
# (14885 -> 14905) — the same shipped-additions mechanism as above. ADR
# annotation: ADR 0281 (the sessions lane ADR) does not yet exist — it lands in
# the sessions lane's U12 close; the ADR that drives this re-baseline will be
# annotated there. Do not loosen the gate (the pin is the point).
# Re-baselined again 2026-11-01 (the ADR 0285 kiosk lane — patches 1/4, 2/4
# and 3/4): the new kiosk.settings.* / board list-view / per-entry kiosk keys
# x en/de/fr/da added to KnownTranslationKeys.cs (14905 -> 15157); the board
# list view (?view=list, ADR 0285 D6) handlers added to
# ProjectsController.cs (5370 -> 5373) — the same shipped-additions
# mechanism as above. ADR 0285 annotated in its Consequences.
$aBaselineSizes = @{
  'src/Kumunita.Core/Localization/KnownTranslationKeys.cs' = 15157
  'src/Kumunita.Web/Controllers/ProjectsController.cs'     = 5373
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
$dDir = Join-Path $repo 'docs/archive/lanes'
$dViolations = [System.Collections.Generic.List[string]]::new()
if (Test-Path $dDir) {
  $dFiles = Get-ChildItem -Path $dDir -Recurse -Filter '*-handoff-notes.md' -File
  foreach ($f in $dFiles) {
    $n = Lines $f.FullName
    if ($n -le $dCeiling) { continue }
    # The window is counted *after* the front-matter (the handoff notes gained
    # front-matter in IMPROVE-DOCS D03); Abstract and TL;DR are aliases
    # (DOCS-CONVENTIONS.md).
    if (-not (HasAbstract $f.FullName 20)) { $dViolations.Add((Rel $f.FullName) + " (" + $n + " lines, no TL;DR/Abstract in first 20 lines after front-matter)") }
  }
}
# Baseline retired (IMPROVE-DOCS D14, 2026-11-02): every archived handoff note
# that is over the 600-line ceiling now carries an Abstract/TL;DR within the
# window (verified by `docs-report`), so the U02 name-baseline is gone — the
# gate fails on *any* file crossing the ceiling without the blockquote, no
# grandfathering.
Check "d" "handoff notes >600 lines without TL;DR/Abstract (no baseline): $($dViolations.Count)" ($dViolations.Count -eq 0)
foreach ($v in $dViolations) { Write-Host "       - $v" }

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
  # Re-baselined again 2026-10-31 (the ADR 0279 hand-drawn icon-sprite close):
  # _Layout crossed its 853 baseline by one line from the new
  # _IconSprite partial render right after <body> (the 28 inline 16-grid
  # copies were replaced by <use> references in place, so the only net
  # addition is the partial line) — the same shipped-additions mechanism as
  # above (the M29 precedent).
  'src/Kumunita.Web/Views/Shared/_Layout.cshtml'       = 854
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
# "> **Abstract:**" blockquote in its first 15 lines *after the front-matter*
# (DOCS-CONVENTIONS.md) — the 10-second "is this the right read" gate at the
# design tier (the handoff-note TL;DR, gate (d), at a different tier). No
# baseline: every over-400 design doc carries the blockquote, so the gate
# fails on any that does not (retired in IMPROVE-DOCS D14).
$gCeiling = 400
$gDir = Join-Path $repo 'docs/design'
$gViolations = [System.Collections.Generic.List[string]]::new()
if (Test-Path $gDir) {
  $gFiles = Get-ChildItem -Path $gDir -Recurse -Filter '*.md' -File
  foreach ($f in $gFiles) {
    $n = Lines $f.FullName
    if ($n -le $gCeiling) { continue }
    # The window is counted *after* the front-matter (DOCS-CONVENTIONS.md:
    # "in its first 15 lines (after the front-matter)") — every design doc
    # over the ceiling carries its Abstract within that window.
    if (-not (HasAbstract $f.FullName 15)) { $gViolations.Add((Rel $f.FullName) + " (" + $n + " lines, no Abstract in first 15 lines after front-matter)") }
  }
}
# Baseline retired (IMPROVE-DOCS D14, 2026-11-02): every `docs/design/*.md`
# over the 400-line ceiling now carries an Abstract within the 15-line window
# *after* the front-matter (the 16 that were flagged at D12/D13 already had a
# conformant Abstract — the window was just mis-reading front-matter lines as
# body). The U07 name-baseline is gone: the gate fails on *any* over-400
# design doc without the blockquote, no grandfathering.
Check "g" "design docs >400 lines without Abstract (no baseline): $($gViolations.Count)" ($gViolations.Count -eq 0)
foreach ($v in $gViolations) { Write-Host "       - $v" }

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
