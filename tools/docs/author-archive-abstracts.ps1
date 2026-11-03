# author-archive-abstracts.ps1 — D03 of the IMPROVE-DOCS lane
#
# Run:
#   pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/author-archive-abstracts.ps1
#
# Replaces the *generic* stamper Abstract on the large hand-authored set
# (the lane registers `plan-*.md` and the rolling handoff notes, both >600
# lines) with a hand-authored three-part Abstract grounded in each file's own
# H1, governing ADR(s), the bounded context / service it ships, and its design
# doc. The three parts answer, in the archive's own vocabulary:
#   handoff  — what did this lane ship · the one seam it created · the one thing
#              a future reader must not re-litigate.
#   register — what this register is · the seam it defined · the one thing a
#              future reader must not re-litigate.
#
# Only files that currently carry the stamper's *generic* Abstract are touched
# (matched by the exact generic text); a file that already has a real abstract
# (e.g. the m3b, m11, m10, pages, m14, m35, m36 handoffs) is left alone. A
# second run is a no-op (no generic text remains).

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

# --- the hand-authored abstracts (rel path -> 3-part block) ------------------
# Grounded per file: H1 identity + governing ADR + the bounded context /
# service it ships + its design doc (all read from the file itself).
$author = @{}

# ---- handoff notes (rolling / scratch tier) --------------------------------
$author['docs/archive/lanes/esc/esc-handoff-notes.md'] = @(
  '> **Abstract:** the rolling handoff note for the ESC lane — the escalation-authorization + receiving-inbox surface that lets a community receive a resident-filed issue and route it to a named handler (ADRs 0154 / 0159).',
  '> **Seam it created:** the escalation write lane on the `Kumunita.Core.ErrorReports` bounded context + the `/escalation` receiving-inbox queue and the `esc-escalation-authorization-design.md` reference.',
  '> **Do not re-litigate:** escalation is the *receiving-inbox* model — the community takes in the report and assigns it; the M32 issue-submission lane is the intake side, and that split (intake vs. routing) is the frozen decision.'
)
$author['docs/archive/lanes/group-posts/group-posts-handoff-notes.md'] = @(
  '> **Abstract:** the rolling handoff note for the group-posts lane — posts and replies scoped to a group, gated on membership (ADRs 0013 / 0003 / 0004).',
  '> **Seam it created:** the group-membership read/write lanes on the Posts surface (the `GP` lane, no milestone letter — roadmap M4/M5/M6 stay Events/Projects/Portability) + the `group-posts-design.md` reference.',
  '> **Do not re-litigate:** group posts are gated on *group membership* at read time (thin token, fat authorization), not on a membership claim; the `GP` lane ID is permanent and must not be renumbered into an M-letter.'
)
$author['docs/archive/lanes/improve/handoff-notes.md'] = @(
  '> **Abstract:** the rolling handoff note for the first IMPROVE lane — the code-layer audit (long functions/files, the god-file and doc-size gates) that this `improve-docs` lane builds on (ADRs 0096 / 0037 / 0039).',
  '> **Seam it created:** the `improve-check.ps1` / `improve-report.ps1` gate harness (now at `tools/docs/`) with the grandfathered baselines that the reduction lanes measure against.',
  '> **Do not re-litigate:** the baselines are *pins, not ceilings to loosen* — the fix for a gate failure is always "re-baseline + annotate the ADR", never "loosen the gate".'
)
$author['docs/archive/lanes/m1/m1-step-7-handoff-notes.md'] = @(
  '> **Abstract:** the step-7 handoff note for M1 — identity, access and delegation (the `/admin/setup` first-boot flow, the claim-shaping seam, ADR 0006).',
  '> **Seam it created:** the `Identity` / `Authorization` bounded contexts and the "thin token, fat authorization" seam — "may this actor see that resource?" resolved per request, not encoded in a claim.',
  '> **Do not re-litigate:** the M1-frozen `CanAsync` audit shape is authoritative for every later write lane — later lanes adapt to it, not the other way round.'
)
$author['docs/archive/lanes/m13/m13-logging-analytics-handoff-notes.md'] = @(
  '> **Abstract:** the handoff note for M13 — structured logging + usage analytics over the existing audit trail (ADRs 0114 / 0025 / 0062).',
  '> **Seam it created:** the `Usage` bounded context (the analytics surface) + the `m13-logging-analytics-design.md` reference; M34 is the history lane that extends it.',
  '> **Do not re-litigate:** analytics reads the *existing* `AccessAudit` rows — there is no separate analytics write path, and the privacy model (`docs/SECURITY.md`) is the authoritative constraint on what is logged.'
)
$author['docs/archive/lanes/m15/m15-translation-bulk-handoff-notes.md'] = @(
  '> **Abstract:** the handoff note for M15 — translation bulk (import/export and batch review of the UI strings) (ADRs 0116 / 0005 / 0042).',
  '> **Seam it created:** the `TranslationResource` bulk lanes on the `Kumunita.Core.Localization` surface + the `m15-translation-bulk-design.md` reference.',
  '> **Do not re-litigate:** the closed `KnownTranslationKeys` set × en/de/fr/da parity is the contract (the IMPROVE gate pins it) — a bulk import adds keys to that set, it does not create a parallel string store.'
)
$author['docs/archive/lanes/m16/m16-inventory-handoff-notes.md'] = @(
  '> **Abstract:** the handoff note for M16 — inventory (check-out / check-in + per-item usage history) (ADRs 0117 / 0044).',
  '> **Seam it created:** the `Inventory` bounded context + the `m16-inventory-design.md` reference.',
  '> **Do not re-litigate:** a check-out re-runs the item''s own frozen `CanAsync` read — the write lane grants nothing; the inventory surface is core (the ADR 0101 inversion: no off-by-default admin toggle).'
)
$author['docs/archive/lanes/m20/m20-notification-quiet-times-handoff-notes.md'] = @(
  '> **Abstract:** the handoff note for M20 — per-resident notification quiet times (a resident suppresses their own notifications on a schedule) (ADR 0121).',
  '> **Seam it created:** the quiet-time preference on the resident''s profile + the notify-time gate in the messaging/notifications path + the `m20-notification-quiet-times-design.md` reference.',
  '> **Do not re-litigate:** quiet times are a *resident self* setting — they gate delivery to the resident only, they never change whether the event is recorded in the audit trail.'
)
$author['docs/archive/lanes/m25/m25-handoff-notes.md'] = @(
  '> **Abstract:** the rolling handoff note for M25 — upload limits (per-file and per-community size/count ceilings on the media surface) (ADRs 0004 / 0006 / 0134).',
  '> **Seam it created:** the upload-limit checks on the `Kumunita.Core.Media` surface + the `m25-upload-limits-design.md` reference.',
  '> **Do not re-litigate:** the limits are enforced *before* the file is written to disk (fail fast, no partial blob), and they ride the existing ADR 0011 media storage — there is no separate limit store.'
)
$author['docs/archive/lanes/m26/m26-handoff-notes.md'] = @(
  '> **Abstract:** the handoff note for M26 — sorting (per-list sort orders across the directory, groups, posts and boards) (ADRs 0122 / 0015).',
  '> **Seam it created:** the sort-order seam on the query layer (the `Query` surface) + the `m26-sorting-design.md` reference.',
  '> **Do not re-litigate:** sort is applied *after* the frozen `CanAsync` filter — you can only reorder what the actor may already see, and the default order per list is the frozen contract.'
)
$author['docs/archive/lanes/m27/m27-handoff-notes.md'] = @(
  '> **Abstract:** the living handoff note for M27 — user-scoped portability (a resident self-exports, and a per-entity conflict-resolution import) (ADR 0148).',
  '> **Seam it created:** the `Kumunita.Core.Portability` user-scoped lanes (the M11 lane is the community-wide version; M27 is the resident-scoped one) + the `m27-user-scoped-portability-design.md` reference.',
  '> **Do not re-litigate:** the import is *per-entity conflict resolution* (the ADR 0193 `keep_existing` merge mode) — it never silently overwrites; the resident-scoped lane reuses M11''s document shapes, it does not fork them.'
)
$author['docs/archive/lanes/m30/m30-handoff-notes.md'] = @(
  '> **Abstract:** the rolling handoff note for M30 — admin onboarding (the `/admin/setup` first-boot + admin self-service surface) (ADRs 0153 / 0150 / 0005).',
  '> **Seam it created:** the `AdminOnboarding` lanes on the `Kumunita.Web` admin surface + the `m30-admin-onboarding-design.md` reference.',
  '> **Do not re-litigate:** a successful `/admin/setup` POST returns a uniform **404** by design — the setup lane is permanently closed after one success and the 404 is success, not a bug to chase.'
)
$author['docs/archive/lanes/m34/m34-handoff-notes.md'] = @(
  '> **Abstract:** the handoff note for M34 — analytics history (the time-series over the M13 usage surface) (ADRs 0162 / 0114).',
  '> **Seam it created:** the history/time-series lanes on the `Kumunita.Core.Usage` bounded context + the `m34-analytics-history-design.md` reference.',
  '> **Do not re-litigate:** analytics history is *read-only over the existing audit rows* — it never writes a separate history stream, and the privacy model is the authoritative constraint on what is kept.'
)
$author['docs/archive/lanes/m9/m9-messaging-handoff-notes.md'] = @(
  '> **Abstract:** the handoff note for M9 — resident-to-resident messaging (DMs with the existing audit + privacy rules) (ADRs 0105 / 0076).',
  '> **Seam it created:** the messaging write/read lanes on the `Kumunita.Core` messaging surface + the `m9-messaging-design.md` reference; group messaging (ADR 0278) is the later extension.',
  '> **Do not re-litigate:** a message is delivered only between actors who may *already* see each other''s profile (thin token, fat authorization) — there is no messaging-specific grant, and the `AccessAudit` row is the only record that a message was seen.'
)
$author['docs/archive/lanes/multilingual-ui/multilingual-ui-handoff-notes.md'] = @(
  '> **Abstract:** the handoff note for the ML-UI lane — the UI wiring for multilingual (the per-page / per-audience language resolution over the M15 string store) (ADRs 0005 / 0015).',
  '> **Seam it created:** the effective-language resolution seam in `Kumunita.Web` (the `kw-l` / `kw-dt` tag helpers) + the `multilingual-design.md` reference.',
  '> **Do not re-litigate:** language resolution is *platform default + resident override* (ADR 0019/0020 lineage) — the `kw-l` tag helper is the only sanctioned way to emit a localized string in a view; a hard-coded English string in a view is the anti-pattern.'
)
$author['docs/archive/lanes/pl/pl-handoff-notes.md'] = @(
  '> **Abstract:** the rolling handoff note for the PL lane — Goals & Projects (the Kanban board, the to-do / project document model) (ADR 0086).',
  '> **Seam it created:** the `Kumunita.Core.Projects` bounded context (the board + to-do + project docs) + the `pl-goals-projects-design.md` reference.',
  '> **Do not re-litigate:** the board is a *document* (one per project) with the to-dos embedded — not a relational project/issue table; the `ProjectsController` is the one surface that owns that shape (the IMPROVE gate pins its size, so grow carefully).'
)
$author['docs/archive/lanes/prod-hardening/hardening-handoff-notes.md'] = @(
  '> **Abstract:** the rolling handoff note for the prod-hardening lane — the F1–F22 hardening fixes (GDPR deletion, MFA, sessions, error tracking, DLQ alerts, scan, bounce handling) (ADR 0006 + the per-fix ADRs).',
  '> **Seam it created:** the per-fix design docs under `docs/design/prod-hardening-f*.md` + the hardening control map in `docs/SECURITY.md`.',
  '> **Do not re-litigate:** each fix is a *named ADR* (F1…F22) — a change to one is an ADR amendment, not an edit to the handoff note; the `KnownTranslationKeys` re-baselines that this lane drove are the documented reason for the gate ceiling.'
)
$author['docs/archive/lanes/rich-editor/rich-editor-handoff-notes.md'] = @(
  '> **Abstract:** the rolling handoff note for the Rich editor (RE) lane — WYSIWYG body editing + the toolbar over the RC Markdown lane (ADRs 0031 / 0025).',
  '> **Seam it created:** the `wwwroot/js/lib/rich-editor.js` client (the `.tmp/build-harness.js` harness mirrors it) over the RC Markdown surface + the `rich-editor-design.md` reference.',
  '> **Do not re-litigate:** the editor is a *client-side view over the RC Markdown document* — the stored shape is still the RC Markdown body, and the WYSIWYG pane is a projection, never the source of truth.'
)
$author['docs/archive/lanes/wysiwyg/wysiwyg-handoff-notes.md'] = @(
  '> **Abstract:** the rolling handoff note for the WYSIWYG (WY) lane — the rendered pane *is* the editable surface; the Markdown source becomes a read-only mirror (ADRs 0031 / 0032 / 0033).',
  '> **Seam it created:** the inline-editing surface over the RC/Rich-editor lane + the `wysiwyg-editor-design.md` reference.',
  '> **Do not re-litigate:** the Markdown source is the *mirror, not the truth* after this lane — the rendered pane edits the document and the Markdown view follows; do not re-introduce a second editable Markdown surface.'
)

# ---- lane registers (plan-*.md, the what/why/how-the-lane-ships record) -----
$author['docs/archive/lanes/esc/plan-esc-escalation-authorization.md'] = @(
  '> **Abstract:** the ESC lane register — escalation-authorization + the receiving inbox, unit by unit (ADRs 0154 / 0155); the handoff note is the running log and `esc-escalation-authorization-design.md` is the current truth.',
  '> **Seam it defined:** the escalation write lane on the `Kumunita.Core.ErrorReports` bounded context + the `/escalation` receiving-inbox queue.',
  '> **Do not re-litigate:** escalation is the *receiving-inbox* model (the community takes in a resident-filed issue and routes it) — the split from M32''s intake lane is the frozen decision.'
)
$author['docs/archive/lanes/events-calendar/plan-events-calendar.md'] = @(
  '> **Abstract:** the events-calendar (EV-CAL) lane register — the week/month calendar view of the visible events, unit by unit (ADRs 0054 / 0019); the design doc is `events-calendar-design.md`.',
  '> **Seam it defined:** the calendar query/view over the `Kumunita.Core.Events` surface (the events the actor may already see — no calendar-specific grant).',
  '> **Do not re-litigate:** the calendar is a *read-only projection of the existing events* gated on `CanAsync(Read)` — there is no calendar-specific write path and the timezone default + resident override (ADR 0019) governs the display.'
)
$author['docs/archive/lanes/file-attachments/plan-file-attachments.md'] = @(
  '> **Abstract:** the file-attachments lane register — attachments on posts / replies / announcements, unit by unit (ADRs 0034 / 0011 / 0025); the design doc is `file-attachments-design.md`.',
  '> **Seam it defined:** the attachment document + the `Kumunita.Core.Media` storage lanes (ADR 0011) that ride under the post/reply/announcement read gates.',
  '> **Do not re-litigate:** an attachment is *only* readable through its parent''s frozen `CanAsync(Read)` — there is no standalone attachment grant, and the media storage (ADR 0011) is the single home for bytes.'
)
$author['docs/archive/lanes/guardian-assignment/plan-guardian-assignment.md'] = @(
  '> **Abstract:** the Guardian-assignment (GA) lane register — a guardian assigns a second guardian to an existing child, unit by unit (ADRs 0038 / 0028 / 0013); the design doc is `guardian-assignment-design.md`.',
  '> **Seam it defined:** the guardian-assignment write lane on the `Kumunita.Core.UserInfo` surface (the guardian relationship, distinct from the time-limits lane).',
  '> **Do not re-litigate:** a guardian may assign a *second* guardian only (the ADR 0028 scope) — the guardian relationship is a *delegation* under the existing parent-child link, never a new account-ownership edge.'
)
$author['docs/archive/lanes/m10/plan-m10-pwa-responsive.md'] = @(
  '> **Abstract:** the M10 lane register — PWA + responsive design (installable, offline-capable, responsive), unit by unit (ADRs 0107 / 0076); the design doc is `m10-pwa-responsive-design.md`.',
  '> **Seam it defined:** the `manifest.webmanifest` + service-worker surface in `Kumunita.Web` + the `@media` blocks in `site.css` (the PwaManifestTests pin counts them — a by-design anti-regression guard).',
  '> **Do not re-litigate:** the `@media` block count in `site.css` is *pinned by `PwaManifestTests`* — a new block is a re-baseline + ADR annotation, not a loosened gate.'
)
$author['docs/archive/lanes/m11/plan-m11-portability.md'] = @(
  '> **Abstract:** the M11 lane register — portability (community-wide import/export), unit by unit (ADRs 0108 / 0011 / 0101); the design doc is `m11-portability-design.md`.',
  '> **Seam it defined:** the `Kumunita.Core.Portability` bounded context (the export/import document shapes) — M27 is the resident-scoped version that reuses these shapes.',
  '> **Do not re-litigate:** portability is a *snapshot of the documents at a point in time* (the ADR 0011 storage) — it is not a live replica, and the import re-runs the existing write lanes, it does not bypass `CanAsync`.'
)
$author['docs/archive/lanes/m12/plan-m12-ical.md'] = @(
  '> **Abstract:** the M12 lane register — iCal (calendar export of the visible events), unit by unit (ADRs 0112 / 0054 / 0085); the design doc is `m12-ical-design.md`.',
  '> **Seam it defined:** the iCal export endpoint over the `Kumunita.Core.Events` surface (the events the actor may already see).',
  '> **Do not re-litigate:** the iCal feed is a *read-only projection* gated on the existing event `CanAsync(Read)` — it exposes exactly the visible events and nothing more (the privacy model is the authoritative constraint).'
)
$author['docs/archive/lanes/m13/plan-m13-logging-analytics.md'] = @(
  '> **Abstract:** the M13 lane register — logging + usage analytics over the existing audit trail, unit by unit (ADR 0114); the design doc is `m13-logging-analytics-design.md` and M34 is the history lane that extends it.',
  '> **Seam it defined:** the `Kumunita.Core.Usage` bounded context (the analytics surface that reads `AccessAudit`).',
  '> **Do not re-litigate:** analytics is *read-only over the existing audit rows* — there is no separate analytics write path, and the privacy model (`docs/SECURITY.md`) is the authoritative constraint on what is logged.'
)
$author['docs/archive/lanes/m14/plan-m14-events-projects.md'] = @(
  '> **Abstract:** the M14 lane register — the integration of Events and Projects (cross-linking the two surfaces), unit by unit (ADRs 0115 / 0028 / 0034); the design doc is `m14-events-projects-design.md`.',
  '> **Seam it defined:** the event↔project cross-link lanes on the `Kumunita.Core.Events` / `Projects` surfaces (a to-do linked to an event, a board linked to an event).',
  '> **Do not re-litigate:** the cross-link is a *reference between two existing documents* — it never grants a new permission; each side stays gated on its own frozen `CanAsync`.'
)
$author['docs/archive/lanes/m15/plan-m15-translation-bulk.md'] = @(
  '> **Abstract:** the M15 lane register — translation bulk (import/export + batch review of the UI strings), unit by unit (ADRs 0116 / 0005 / 0042); the design doc is `m15-translation-bulk-design.md`.',
  '> **Seam it defined:** the `TranslationResource` bulk lanes on the `Kumunita.Core.Localization` surface (the closed `KnownTranslationKeys` set).',
  '> **Do not re-litigate:** the closed `KnownTranslationKeys` × en/de/fr/da set is the contract (the IMPROVE gate pins its count) — a bulk lane adds to that set, it does not create a parallel string store.'
)
$author['docs/archive/lanes/m16/plan-m16-inventory.md'] = @(
  '> **Abstract:** the M16 lane register — inventory (check-out / check-in + per-item usage history), unit by unit (ADRs 0117 / 0044); the design doc is `m16-inventory-design.md`.',
  '> **Seam it defined:** the `Kumunita.Core.Inventory` bounded context (the item + check-out documents).',
  '> **Do not re-litigate:** a check-out re-runs the item''s own frozen `CanAsync(Read)` — the write lane grants nothing, and the inventory surface is core (the ADR 0101 inversion: no off-by-default admin toggle).'
)
$author['docs/archive/lanes/m20/plan-m20-notification-quiet-times.md'] = @(
  '> **Abstract:** the M20 lane register — per-resident notification quiet times, unit by unit (ADRs 0121 / 0076 / 0019); the design doc is `m20-notification-quiet-times-design.md`.',
  '> **Seam it defined:** the quiet-time preference on the resident''s profile + the notify-time gate in the messaging/notifications path.',
  '> **Do not re-litigate:** quiet times are a *resident self* setting — they gate delivery to the resident only and never change whether the event is recorded in the audit trail.'
)
$author['docs/archive/lanes/m24/plan-m24-storage-metrics.md'] = @(
  '> **Abstract:** the M24 lane register — storage metrics (per-community disk usage over the media surface), unit by unit (ADRs 0011 / 0006); the design doc is `m24-storage-metrics-design.md` and M33 is the history lane that extends it.',
  '> **Seam it defined:** the storage-metrics query on the `Kumunita.Core.Media` surface (ADR 0011).',
  '> **Do not re-litigate:** storage metrics are a *read-only projection* over the existing media blobs — they never write a separate metrics store, and M33''s history reuses this shape.'
)
$author['docs/archive/lanes/m25/plan-m25-upload-limits.md'] = @(
  '> **Abstract:** the M25 lane register — upload limits (per-file and per-community ceilings on the media surface), unit by unit (ADRs 0011 / 0006); the design doc is `m25-upload-limits-design.md`.',
  '> **Seam it defined:** the upload-limit checks on the `Kumunita.Core.Media` surface (ADR 0011).',
  '> **Do not re-litigate:** the limits are enforced *before* the file is written to disk (fail fast, no partial blob) and ride the existing ADR 0011 media storage — there is no separate limit store.'
)
$author['docs/archive/lanes/m26/plan-m26-sorting.md'] = @(
  '> **Abstract:** the M26 lane register — per-list sorting across the directory, groups, posts and boards, unit by unit (ADRs 0006 / 0090 / 0145); the design doc is `m26-sorting-design.md`.',
  '> **Seam it defined:** the sort-order seam on the query layer (the `Query` surface) applied after the authorization filter.',
  '> **Do not re-litigate:** sort is applied *after* the frozen `CanAsync` filter — you can only reorder what the actor may already see, and the default order per list is the frozen contract.'
)
$author['docs/archive/lanes/m27/plan-m27-user-scoped-portability.md'] = @(
  '> **Abstract:** the M27 lane register — user-scoped portability (resident self-export + per-entity conflict-resolution import), unit by unit (ADRs 0148 / 0108); the design doc is `m27-user-scoped-portability-design.md`.',
  '> **Seam it defined:** the resident-scoped `Kumunita.Core.Portability` lanes (M11 is the community-wide version; M27 reuses its document shapes).',
  '> **Do not re-litigate:** the import is *per-entity conflict resolution* (the ADR 0193 `keep_existing` merge) — it never silently overwrites, and the resident-scoped lane reuses M11''s shapes, it does not fork them.'
)
$author['docs/archive/lanes/m28/plan-m28-guardian-time-limits.md'] = @(
  '> **Abstract:** the M28 lane register — guardian time limits (a per-child allow/block schedule that gates the child''s access to the platform), unit by unit (ADRs 0028 / 0038 / 0121); the design doc is `m28-guardian-time-limits-design.md`.',
  '> **Seam it defined:** the time-limit schedule on the `Kumunita.Core.UserInfo` surface (the guardian relationship''s delivery gate).',
  '> **Do not re-litigate:** a time limit gates *delivery/access for the child* only — it never changes the audit trail, and the ADR 0028 scope (guardian-set, per-child) is the frozen boundary.'
)
$author['docs/archive/lanes/m29/plan-m29-admin-surface-labels.md'] = @(
  '> **Abstract:** the M29 lane register — admin-surface labels (the localized admin UI copy), unit by unit (ADRs 0150 / 0152 / 0050); the design doc is `m29-admin-surface-labels-design.md`.',
  '> **Seam it defined:** the `SurfaceLabels` seeder rows + the `admin.*` / `labels.*` keys on the closed `KnownTranslationKeys` set.',
  '> **Do not re-litigate:** the admin labels ride the *closed translation-key set* (the IMPROVE gate pins its count) — a label change is a new key × en/de/fr/da, not a hard-coded string in a view.'
)
$author['docs/archive/lanes/m30/plan-m30-admin-onboarding.md'] = @(
  '> **Abstract:** the M30 lane register — admin onboarding (the `/admin/setup` first-boot + admin self-service surface), unit by unit (ADRs 0005 / 0150 / 0153); the design doc is `m30-admin-onboarding-design.md`.',
  '> **Seam it defined:** the `AdminOnboarding` lanes on the `Kumunita.Web` admin surface.',
  '> **Do not re-litigate:** a successful `/admin/setup` POST returns a uniform **404** by design — the setup lane is permanently closed after one success and the 404 is success, not a bug to chase.'
)
$author['docs/archive/lanes/m31/plan-m31-production-error-handling.md'] = @(
  '> **Abstract:** the M31 lane register — production error handling (the error-report intake + the break-glass path), unit by unit (ADR 0154); the design doc is `m31-production-error-handling-design.md`.',
  '> **Seam it defined:** the `Kumunita.Core.ErrorReports` bounded context (the intake + the break-glass lane).',
  '> **Do not re-litigate:** the error-report intake is *audit-by-default* (a resident-filed report is the gate, not a privileged write) and the break-glass path is a named, logged lane — not an untracked admin shortcut.'
)
$author['docs/archive/lanes/m32/plan-m32-issue-submission-escalation.md'] = @(
  '> **Abstract:** the M32 lane register — issue submission & escalation (the resident-facing intake + the escalation hand-off to ESC), unit by unit (ADRs 0155 / 0006 / 0004); the design doc is `m32-issue-submission-escalation-design.md`.',
  '> **Seam it defined:** the issue-submission write lane on the `Kumunita.Core.ErrorReports` surface — the *intake* side; ESC (the register at `lanes/esc/`) is the *routing* side.',
  '> **Do not re-litigate:** the intake-vs-routing split is the frozen decision — M32 takes the report in, ESC routes it; the `PostStatus` / `AccessAudit` shape M3b pinned is authoritative for both.'
)
$author['docs/archive/lanes/m33/plan-m33-storage-metrics-history.md'] = @(
  '> **Abstract:** the M33 lane register — storage-metrics history (the time-series over the M24 storage surface), unit by unit (ADRs 0134 / 0156 / 0004); the design doc is `m33-storage-metrics-history-design.md`.',
  '> **Seam it defined:** the history/time-series lanes on the `Kumunita.Core.Media` storage surface (M24 is the current-state version).',
  '> **Do not re-litigate:** storage-metrics history is a *read-only projection* over the existing media blobs — it never writes a separate metrics store, and the ADR 0011 storage contract is the single home for bytes.'
)
$author['docs/archive/lanes/m34/plan-m34-analytics-history.md'] = @(
  '> **Abstract:** the M34 lane register — analytics history (the time-series over the M13 usage surface), unit by unit (ADRs 0114 / 0156 / 0162); the design doc is `m34-analytics-history-design.md`.',
  '> **Seam it defined:** the history/time-series lanes on the `Kumunita.Core.Usage` bounded context (M13 is the current-state version).',
  '> **Do not re-litigate:** analytics history is *read-only over the existing audit rows* — it never writes a separate history stream, and the privacy model is the authoritative constraint on what is kept.'
)
$author['docs/archive/lanes/m35/plan-m35-kiosk-mode.md'] = @(
  '> **Abstract:** the M35 lane register — kiosk mode (a full-page display that auto-cycles a GlobalAdmin-set playlist of views), unit by unit (ADRs 0104 / 0164); the design doc is `m35-kiosk-mode-design.md`.',
  '> **Seam it defined:** the `Kumunita.Core.Kiosk` bounded context (the `KioskPlaylist` singleton + `IKioskService` seam) + the `/kiosk` surface.',
  '> **Do not re-litigate:** the three kiosk slide surfaces are **full MVC views, not partials** (the U04 drift pause) — and the kiosk is a GlobalAdmin-set playlist, not a per-resident setting.'
)
$author['docs/archive/lanes/m36/plan-m36-mentions.md'] = @(
  '> **Abstract:** the M36 lane register — @mentions (mentioning a resident in a post/reply and notifying them), unit by unit (ADRs 0165 / 0031 / 0076); the design doc is `m36-mentions-design.md`.',
  '> **Seam it defined:** the mention parse/notify lanes on the messaging surface (over the M9 DMs) + the `Mentions` surface.',
  '> **Do not re-litigate:** a mention notifies only a resident the actor may *already* see (thin token, fat authorization) — there is no mention-specific grant, and the mention is a *parse of the existing body*, not a new field.'
)
$author['docs/archive/lanes/m7/plan-m7-pagination-filtering.md'] = @(
  '> **Abstract:** the M7 lane register — pagination + filtering across the list surfaces, unit by unit (ADR 0090); the design doc is `m7-pagination-filtering-design.md`.',
  '> **Seam it defined:** the pagination/filter query seam on the list surfaces (directory, groups, posts, boards).',
  '> **Do not re-litigate:** pagination/filter is applied *after* the frozen `CanAsync` filter — a page may only contain what the actor may already see, and the page-size ceiling is the frozen contract.'
)
$author['docs/archive/lanes/rich-content/plan-rich-content.md'] = @(
  '> **Abstract:** the Rich-content (RC) lane register — Markdown bodies + in-content images, unit by unit (ADRs 0011 / 0005); the design doc is `rich-content-design.md` and the RE/WY lanes are the editors over it.',
  '> **Seam it defined:** the Markdown body + in-content image document shapes on the post/reply/announcement surfaces (ADR 0011 storage for the images).',
  '> **Do not re-litigate:** the RC Markdown body is the *single source of truth* for rich content — the WYSIWYG/RE panes are projections over it, and an in-content image rides ADR 0011 media storage, never a separate store.'
)
$author['docs/archive/lanes/rich-editor/plan-rich-editor.md'] = @(
  '> **Abstract:** the Rich-editor (RE) lane register — WYSIWYG body editing + the toolbar over the RC Markdown lane, unit by unit (ADRs 0025 / 0031); the design doc is `rich-editor-design.md`.',
  '> **Seam it defined:** the `wwwroot/js/lib/rich-editor.js` client over the RC Markdown surface.',
  '> **Do not re-litigate:** the editor is a *client-side view over the RC Markdown document* — the stored shape is still the RC Markdown body and the WYSIWYG pane is a projection, never the source of truth.'
)
$author['docs/archive/lanes/sessions/plan-sessions.md'] = @(
  '> **Abstract:** the Sessions lane register — the per-session list + revoke-one-device surface, unit by unit (ADRs 0281 / 0006); the design doc is `sessions-per-session-revocation.md`.',
  '> **Seam it defined:** the session list/revoke lanes on the `Kumunita.Core.Identity` surface (the ASP.NET Identity session store).',
  '> **Do not re-litigate:** revoking one session is a *targeted* operation (the `settings.sessions.*` surface) — it does not log out all devices, and the audit row for a revocation is the only record that it happened.'
)
$author['docs/archive/lanes/site/plan-site.md'] = @(
  '> **Abstract:** the SITE lane register — the `/home` + `/about` landing surfaces become admin-editable, unit by unit (ADRs 0043 / 0005 / 0042); the design doc is `site-content-design.md`.',
  '> **Seam it defined:** the `SiteContent` document + the admin-editable landing surface (over the M15 string store).',
  '> **Do not re-litigate:** the landing pages are *admin-authored documents* (the `SiteContent` shape) — the in-app guide registry (`docs/guides/`) is the *resident-facing* help and is a separate surface; do not conflate them.'
)
$author['docs/archive/lanes/site/site-handoff-notes.md'] = @(
  '> **Abstract:** the rolling handoff note for the SITE lane — the admin-editable `/home` + `/about` landing surfaces (ADRs 0050 / 0004 / 0005).',
  '> **Seam it created:** the `SiteContent` document + the admin-edit surface + the `site-content-design.md` reference.',
  '> **Do not re-litigate:** the landing pages are *admin-authored documents* — the in-app guide registry (`docs/guides/`) is the resident-facing help and is a separate surface; the closed translation-key set (ADR 0005) is the string contract, not the page body.'
)
$author['docs/archive/lanes/tags/plan-tags.md'] = @(
  '> **Abstract:** the Tags (TG) lane register — the tag surface over posts/replies (filter + group posts by tag), unit by unit (ADRs 0006 / 0011 / 0004); the design doc is `tags-design.md`.',
  '> **Seam it defined:** the `Kumunita.Core.Tags` surface (the tag document + the tag↔post link) over the post/reply read gates.',
  '> **Do not re-litigate:** a tag is a *reference on an existing post* — it grants no new permission (the post stays gated on its frozen `CanAsync`), and the tag is localized through the closed `KnownTranslationKeys` set, not a free-text string.'
)
$author['docs/archive/lanes/wysiwyg/plan-wysiwyg.md'] = @(
  '> **Abstract:** the WYSIWYG (WY) lane register — the rendered pane is the editable surface and the Markdown source becomes a read-only mirror, unit by unit (ADRs 0032 / 0031); the design doc is `wysiwyg-editor-design.md`.',
  '> **Seam it defined:** the inline-editing surface over the RC/Rich-editor lane.',
  '> **Do not re-litigate:** the Markdown source is the *mirror, not the truth* after this lane — the rendered pane edits the document and the Markdown view follows; do not re-introduce a second editable Markdown surface.'
)

# --- apply ------------------------------------------------------------------
$applied = 0
$skipped = 0
foreach ($rel in $author.Keys) {
  $full = Join-Path $repo $rel
  if (-not (Test-Path -LiteralPath $full)) { throw "missing: $rel" }
  $body = @(Get-Content -LiteralPath $full)
  # locate the first '> **Abstract:**' or '> **TL;DR:**' block and confirm it is the generic one
  $startIdx = -1
  for ($i = 0; $i -lt [Math]::Min(40, $body.Count); $i++) {
    if ($body[$i] -match '^\s*>\s*\*\*(?:Abstract|TL;DR)') { $startIdx = $i; break }
  }
  if ($startIdx -lt 0) { throw "no abstract block found in $rel" }
  $isGeneric = ($body[$startIdx] -match 'a unit plan in this lane|the lane register — what the lane set out to do, why, and how|the running handoff note for this lane')
  if (-not $isGeneric) { $skipped++; continue }   # already hand-authored — leave it
  # the block is the consecutive '>' lines starting at $startIdx
  $endIdx = $startIdx
  while ($endIdx + 1 -lt $body.Count -and $body[$endIdx + 1] -match '^\s*>') { $endIdx++ }
  # rebuild: before + new block + after
  $new = @()
  if ($startIdx -gt 0) { $new += @($body[0..($startIdx-1)]) }
  $new += $author[$rel]
  if ($endIdx + 1 -lt $body.Count) { $new += @($body[($endIdx+1)..($body.Count-1)]) }
  Set-Content -LiteralPath $full -Value $new -Encoding utf8
  $applied++
}
Write-Host ""
Write-Host "author-archive-abstracts: $applied hand-authored, $skipped already real (left alone)."
Write-Host "Re-run is a no-op: the generic marker text is gone after the first run."
