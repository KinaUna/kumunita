---
id: adr-0299
title: "The instance export is operator-gated — content-free by default; full only under an active AdminOverride"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0003-roles-and-moderator-scoping.md
  - docs/design/m11-portability-design.md
  - docs/design/m9-messaging-design.md
  - docs/design/improve-sec-design.md
  - docs/ops/OPS.md
supersedes: [C-M11·6 (full scope only, docs/design/m11-portability-design.md §surface)]
superseded_by: []
updated: 2026-11-07
---

# ADR 0299 — The instance export is operator-gated

## Context

IMPROVE-SEC finding **E1** (severity S): the GlobalAdmin instance export
(`GET admin/portability/export` → `PortabilityService.ExportAsync`) carries
**every private message** (`Conversation`/`Message`, audience-restricted
posts, profile contact fields, reports) — one audit row, no second party,
nothing residents can see. That contradicts the messaging **D3 wall** (a
conversation is visible to exactly its two participants; a GlobalAdmin gets a
non-leaky 404) and ADR 0003's privacy default (no standing to read private
content, not even for the operator). Part-vs-whole: an operator surface
undoes a resident guarantee.

## Decision — operator-gated (D1, owner, 2026-11-06)

1. **Content-free by default.** A GlobalAdmin alone gets structure, public
   content, and their own data; private content (messaging threads,
   audience-restricted rows, PII contact fields) is excluded — not a breach,
   the same posture the surface holds for its other world-seams.
2. **Full only under an active `AdminOverride`.** The operator-granted
   break-glass `AdminOverride` (ADR 0003 path: time-limited, single-use,
   direct DB write — **no in-app endpoint can grant it**). Gated by
   `BreakGlassProbe.HasActiveAsync` (IS26); scope = `PortabilityExportScope`
   (IS27); 403 for `scope=full` without override (IS28).
3. **Audited `Via: BreakGlass`.** The audit row records `Action =
   portability.export.{content-free|full}` and `Via = Admin` (content-free) or
   `BreakGlass` (full), with the scope recorded.
4. **Resident transparency (D10, default).** A full export emits a notification
   (`kind: instance.full-export`) to every non-guest resident — the existing
   inbox, no new surface — stating the date and that the operator authorised
   it, no actor name.

## Supersedes

The M11 design constraint **C-M11·6** ("the surface is GlobalAdmin + audited +
quiet", `m11-portability-design.md` §surface) is superseded **for the full
scope only**: a full export is no longer a quiet surface — it requires the
operator override, is audited `Via: BreakGlass`, and leaves a transparency
entry residents can see. The **content-free default is unchanged** — it
remains quiet (one audit row, no second party, no transparency entry).

## Rejected alternatives

- **Strip private content entirely** — loses the migration use-case and pushes
  the operator to a raw `pg_dump` (unstructured, un-audited — worse).
- **Accept and disclose** — keeps a standing GlobalAdmin content read of every
  conversation, contradicting the messaging D3 wall and the ADR 0003 privacy
  default. The D3 wall is a resident guarantee, not a UI courtesy — the
  anti-pattern *the side door*.

## Constraints

- No in-app endpoint can grant or extend the `AdminOverride` (ADR 0003 —
  operator-granted out-of-band by direct DB write).
- The gate seam is `BreakGlassProbe.HasActiveAsync(IQuerySession, userId)`
  (IS26) — extracted from `AuthorizationService`, no `IAuthorizationService`
  change (ADR 0006).
- `PortabilityExportScope { ContentFree, Full }` (IS27) encodes the scope; the
  old `ExportAsync(actor, ct)` overload delegates to `ContentFree`.
- The full branch also emits the D10 transparency notification (IS29); the OPS
  runbook section (IS30); the control-map row lands in IS39's format.
- CODE-HYGIENE U13 (2026-11-09): boundary edges + method baselines
  re-baselined to the current count (ratchet gate, `tools/code/code-baseline.json`);
  the grown edges this lane re-baselines carry this ADR as their `adr:` reason.
