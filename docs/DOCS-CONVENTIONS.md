---
id: docs-conventions
title: "Documentation conventions"
tier: living
kind: guide
status: current
audiences: [developer, agent, platform-maintainer]
related:
  - docs/plans-milestones/improve-docs/plan-improve-docs.md
updated: 2026-11-02
---

> **Abstract:** the shared rules every doc in this repo follows — the
> front-matter schema, the abstract rule, the ADR cap, the linking rule,
> the "one fact, one place" rule, and the tier model. Read this before
> adding or moving a doc; it is referenced by every unit of the
> `IMPROVE-DOCS` lane ([plan-improve-docs.md](plans-milestones/improve-docs/plan-improve-docs.md)).
> **Who is it for:** anyone touching `docs/`, `README.md` or `AGENTS.md`.
> **When to read the rest:** before your first doc change in a session, or
> when a later unit's exit criterion names one of these rules.

# Documentation conventions

This repo is deliberate about doc ↔ code parity. The conventions below are
the *seam* the doc layer keeps green — they are enforced by the harness
(`tools/docs/`) and by the close gate (`D14` of the plan), not by memory.

## Front-matter schema

Every doc carries a YAML front-matter block. The schema, finalized in
`D00` of the lane register, is:

```yaml
---
id: ops-backups                 # unique, stable; ADRs use adr-0242
title: "Backups"
tier: living | reference | archive
kind: entry | guide | runbook | architecture | security | adr | design | plan | handoff | philosophy
status: current | proposed | accepted | superseded | archived
audiences: [resident, community-admin, platform-maintainer, developer, agent]
related: [docs/adr/0004-data-persistence-and-schema-evolution.md]
supersedes: []                  # ADRs / design docs
superseded_by: []
lane: m3b                       # where it came from, if anywhere
updated: 2026-11-02
---
```

The front-matter is machine-readable (the harness reads `id`, `tier`,
`kind`, `status`, `lane`). The human-facing summary is the abstract below.

## The abstract rule

Every doc over **60 lines** carries a `> **Abstract:**` blockquote in its
first 15 lines (after the front-matter) answering three questions:

- **what is this** — the one-line identity of the doc;
- **who is it for** — the audience(s) that should read it;
- **when should I read the rest** — the trigger that makes the full read
  worth the time.

The old `TL;DR` name (the previous IMPROVE gate `d` used `TL;DR` for
handoff notes) is an **accepted alias** — the harness accepts either
`> **Abstract:**` or `> **TL;DR:**`. New docs should use `Abstract`; the
name `Abstract` wins.

## The ADR cap

ADRs are decision records, **~250-line target** (the gate's bucketing is
≤250 / 251–400 / >400). The shape:

- **Status / Context / Decision / Consequences** stay in the ADR;
- **Context** shrinks to the *forces* (what pushed on the decision), not
  the options analysis;
- **options analysis, test lists, implementation notes** move to the
  linked design doc (the ADR's front-matter `related:` names it), or to the
  shared `docs/design/adr-handoffs.md` when the ADR has no dedicated design
doc.

ADR **numbers are permanent** — the code cites them ~9 600 times, so a
trimmed ADR keeps its number, its `id` (`adr-NNNN`), and its position in
the index.

## The linking rule — "link, don't backtick"

A mention of another doc that **names its path** becomes a relative Markdown
link. A backtick-wrapped path in prose is *prose about a path*, not a link —
the reader can't click it, and the harness doesn't count it as an outbound
link.

- **Inside docs:** a path mention becomes a Markdown link whose target is
  the real, relative path (e.g. the link above to the lane register).
- **Inside code comments:** `ADR NNNN` stays as-is — the number is the
  stable key (the code doesn't depend on the file name; the ADR number does).
- **Broken links are a gate failure** — a Markdown link whose target file
  doesn't exist (relative to the source file's directory) fails the close.

## The "one fact, one place" rule

When a topic appears in two docs, **one is the canonical home** and the
other **links to it**. The canonical home owns the fact and is updated in
place when it changes; the other doc is a pointer. This is what keeps the
linkage honest: if both docs restate the fact, they will drift, and the
drift is silent coupling in the doc layer.

## The tier model

- **`living`** — the *current truth* for its subject, updated in place as
  the platform changes. Examples: `README.md`, `AGENTS.md`,
  `docs/DOCS-CONVENTIONS.md`, `docs/start/*.md`.
- **`reference`** — a *stable reference* that is correct as of a point in
  time and is not expected to change without a deliberate revision.
  Examples: ADRs, design docs (after `D10` settles their status).
- **`archive`** — *lane history*, frozen at the lane's close. Examples:
  `docs/archive/lanes/<lane>/…` (the former `archive/lanes/`).

The tier is a **contract about how the doc is maintained**, not about
content. A `living` doc that has not been updated in a while is a *drift
risk* (the philosophy's "integration decays"); an `archive` doc that is
edited is a *gate violation* (D14: nothing new under `docs/archive/` except
at a lane close).

## Pointer

The lane register for the work this conventions doc is written against is
[`plan-improve-docs.md`](plans-milestones/improve-docs/plan-improve-docs.md).
The previous IMPROVE lane (the code-layer audit) is
[`plan-improve.md`](archive/lanes/improve/plan-improve.md).
