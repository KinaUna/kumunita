---
id: arch-extension-seams
title: "Extension seams (deferred, by design)"
tier: reference
kind: architecture
status: current
audiences: [developer, agent]
related:
  - docs/ARCHITECTURE.md
updated: 2026-11-02
---

# Extension seams (deferred, by design)

> **Abstract:** the seams deliberately left open by design — federation (`Profile.externalId`), geo zones, group helpers, MCP / API, calendar, cross-neighborhood migration. Plus the M4 → M5 deferrals carried forward from `design/m4-events-design.md §5`. **Who is it for:** a developer planning a follow-on lane, or an agent checking whether a capability is in scope. **When to read the rest:** before proposing a new bounded context, a new integration, or a new world seam.


- **Federation**: `Profile.externalId` reserved; IdentityModule is the only place that knows
  the identity source, so adding OpenIddict is additive. Global identity, local authorization.
- **Geo zones**: metadata on resources/residents; display filtering only, never core access.
- **Group helpers**: new UserInfoModule methods (SuggestNeighbors, SuggestFamily) that
  populate groups — a convenience, not a new authorization concept.
- **MCP / API**: `Core` has no HTTP dependency, so a minimal-API or MCP project can call 

## M4 → M5 deferrals (carried forward — design/m4-events-design.md §5)

M4 shipped the events surface **additively and reusing** (ADR 0054). The
following were **deliberately not** in M4's own scope and are follow-on lanes
(own design doc + ADR each) or M6 work — the M5 close should carry this list
so the out-of-scope boundary stays honest. (One item below — event
translations — has since shipped as its own lane, ADR 0059; it is listed here
to keep the M4 boundary honest, not as a live deferral.)

- **Event translations** — the `Event` is authored-in-language (ADR 0018);
  the ADR 0022/0026/0029 user-added translation lane **now** extends to events
  as a separate named lane (ADR 0059 — `EventTranslation` + the add / update /
  remove seams + the ADR 0027/0049/0051 display).
- **Group events** — a `Group`-scoped event channel (the ADR 0013
  membership-lane precedent would be the shape).
- **Per-resident reminder settings** — "remind me N hours before" is a
  follow-on lane; the single 24-hour-before email is the M4 surface.
- **iCal export** — the `events.ics` endpoint stays M6 (Portability).
- **No admission queue** — `Capacity` is display metadata; `Going` RSVPs are
  the truth (a waitlist/spot-release would be a new ADR).the
  same services later.
- **Calendar**: an `events.ics` endpoint when needed.
- **Cross-neighborhood migration**: versioned JSON export/import service (not built yet).
