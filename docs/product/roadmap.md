---
id: product-roadmap
title: "Roadmap and deferred-by-design"
tier: living
kind: guide
status: current
audiences: [resident, community-admin, developer]
related:
  - src/Kumunita.Web/Milestones.cs
  - docs/STATUS.md
  - docs/product/features.md
lane: improve-docs
updated: 2026-11-02
---

> **Abstract:** the milestone roadmap's *source of truth* plus the
> deferred-by-design list. This was the `## Roadmap` + `## Deferred` sections
> of the front-page [`README.md`](../../README.md). The milestone list
> itself is **not restated here** — its canonical home is the code
> (`src/Kumunita.Web/Milestones.cs`, which renders the public home page) and
> the detailed narrative is [`STATUS.md`](../STATUS.md); this page names
> those as the source of truth and keeps the deferred-by-design list.
> **Who is it for:** anyone asking "what has shipped, what is next, and what
> is deliberately out of scope?" **When to read the rest:** when you need the
> roadmap's authority or the deferred list, not the milestone-by-milestone
> prose.

# Roadmap

The milestone roadmap is **owned by the code**, not by a doc, so there is
exactly one thing to keep in sync with the code (one fact, one place):

- **`src/Kumunita.Web/Milestones.cs`** — the *source of truth*. It is the
  rendered home-page roadmap (the `Milestones.All` list). `MilestonesTests.cs`
  pins the exact order, the shipped/done statuses, and the "last milestone, no
  successor" framing — so a copy-paste reorder or a forgotten status bump is
  caught by the build, not by memory. **Do not renumber or reorder
  milestones.**
- **[`docs/STATUS.md`](../STATUS.md)** — the *detailed narrative*. The
  milestone-by-milestone prose (the long "what shipped, and how" paragraphs
  that used to live inline in the README Roadmap) lives here, not in this
  file. This page deliberately does **not** restate the list — restating it
  here would make a third copy that can drift from `Milestones.cs` (the
  "one fact, one place" rule in
  [`DOCS-CONVENTIONS.md`](../DOCS-CONVENTIONS.md)).

**The sync contract** (re-pointed by `D07` of the `IMPROVE-DOCS` lane):
`Milestones.cs` is the canonical list; [`STATUS.md`](../STATUS.md) is the narrative; this
page and the front page point at both — neither restates the list. When a
milestone or a named lane ships, the full close flip (which docs to touch,
including the `WhatsNew.cs` registry and the `MilestonesTests.cs` pin) is
defined in [`AGENTS.md`](../../AGENTS.md) ("Keeping the docs in sync with the
code"); that file now names **this page** (the former "README Roadmap") as the
roadmap's doc home. The front page's "where to go next" links into the
per-audience entry pages under [`start/`](../start/resident.md) rather than
restating this.

## Deferred (future, by design)

- **Machine translation of user-generated content** — optional and opt-in if it
  ever ships: per-item, clearly labeled, and the MT provider becomes a third-party
  boundary (ADR [0005](../adr/0005-multilingual-support.md) C, SECURITY.md §6). Until then UGC is rendered as authored.
- **Geographic zones** as metadata + display filtering (not part of the core access model).
- **Cross-neighborhood federation** — a standalone OpenIddict IdP; global identity, local authorization.
- **Group helpers** — suggest/populate groups (neighbors from addresses, family from household).
- **MCP**, calendar integration, cross-neighborhood data migration.
- **Invitation-only sign-up** — the **gate** is now admin-managed (ADR [0050](../adr/0050-admin-managed-signup-gate-open-or-invitation-only.md)),
  and a fresh instance ships **invitation-only** (ADR [0050](../adr/0050-admin-managed-signup-gate-open-or-invitation-only.md), amended 2026-10-05):
  a GlobalAdmin sets up the platform before any resident can self-register,
  then opens the gate to **open** (residents may self-register) from
  `/admin/signup` — closing it back to **invitation-only** (closed to new
  self-service accounts) at any time. The `/account/signup` write lane and the
  nav/login sign-up affordances are all authoritative — existing residents are
  unaffected. What remains to land is the **invitation mechanism** the gate
  closes the door *to*: an invited resident self-serves their password from an
  admin-sent invitation link (token lifecycle, expiry, admin UX) rather than
  registering on their own — so "closed" is a place a resident is invited *to*,
  not a dead end. Land it before the community is open beyond the development
  circle (SECURITY.md §6 open items — the control that answers adversary A2,
  the signup bot).
