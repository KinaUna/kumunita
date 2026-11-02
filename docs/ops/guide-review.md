---
id: ops-guide-review
title: "Review the user guides (periodic check)"
tier: reference
kind: runbook
status: current
audiences: [community-admin]
related:
  - docs/guides/CONVENTIONS.md
  - docs/adr/0057-user-guides-in-help-subtree-and-consistency-loop.md
  - docs/adr/0024-author-soft-delete-lane.md
updated: 2026-11-02
---

> **Abstract:** the *periodic* check on the resident-facing guides — owner,
> cadence, and the seven-step review (cross-check the registry, follow each
> guide as a resident, stamp the date). **Who is it for:** the community
> **GlobalAdmin** (in-product, not the host operator). **When to read the
> rest:** at each quarterly boundary, or after a resident-facing release.

# Review the user guides (periodic check)

The resident-facing guides live in the `help/` subtree of the page tree (ADR [0057](../adr/0057-user-guides-in-help-subtree-and-consistency-loop.md)) —
they are `Page` docs, not a second doc set. Two checks keep them honest; this
procedure is the **periodic** one (the **event-based** one is the lane's own
Definition of Done: a resident-facing change ships *with* its guide update).

**Owner:** the community **GlobalAdmin** (in-product, not the host operator).
**Cadence:** **quarterly**, or after any release that changed a resident-facing
feature, whichever is sooner.

1. Open the guide registry (the single source the seeder and the tests both read):
   `FirstBootSeeder.GuidePages()` in `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs`,
   cross-checked against the registry table in [guides/CONVENTIONS.md](../guides/CONVENTIONS.md). They must
   list the same slugs.
2. For **each** guide, open its current `en` body in the in-app editor
   (`/pages/help/{slug}`), and follow it **as a resident would** — sign in as a demo
   resident, do the steps in order, note where the page and the product disagree.
3. Mark each row **current** / **needs update** / **feature retired** in the review
   notes below.
4. **needs update** → open a lane (a ticket) that ships the guide's `en` body fix in the
   seeder's `GuidePages()` array, the matching `CONVENTIONS.md` row, and the
   `UG_GuideRegistryTests` drift pin in the same commit (ADR [0057](../adr/0057-user-guides-in-help-subtree-and-consistency-loop.md) D3.2 / D4).
5. **feature retired** → soft-delete the guide (a GlobalAdmin, the ADR [0024](../adr/0024-author-soft-delete-lane.md)
   author-lane shape) and remove its row from `GuidePages()` + `CONVENTIONS.md` +
   the drift pin in the same commit.
6. **A new resident-facing feature shipped since the last review** → confirm it has a
   guide row (the event-based check, ADR [0057](../adr/0057-user-guides-in-help-subtree-and-consistency-loop.md) D3.2). If not, open a lane to add one
   (a guide documents a shipped feature; it is added in the feature's lane, not
   silently).
7. Stamp **Last reviewed:** below, and record any open lanes.

> **Last reviewed:** _not yet run — first review due at the first quarterly boundary
> after ADR [0057](../adr/0057-user-guides-in-help-subtree-and-consistency-loop.md) ships._
> **Open lanes from review:** _none._

The periodic check is the **safety net** for the event-based check: a lane that
*should* have updated a guide but didn't is caught by the next quarterly review, not
by a resident hitting a wall (ADR [0057](../adr/0057-user-guides-in-help-subtree-and-consistency-loop.md) D3.3).

## Related

- [Guides conventions](../guides/CONVENTIONS.md) — the registry table and writing rules this check reads.
- [Deploy a demo site](demo-site.md) — the demo resident this procedure signs in as.
