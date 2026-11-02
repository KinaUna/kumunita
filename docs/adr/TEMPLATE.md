---
id: adr-template
title: "ADR template"
tier: reference
kind: adr
status: proposed
audiences: [developer, agent, platform-maintainer]
related:
  - docs/DOCS-CONVENTIONS.md
updated: 2026-11-02
---

> **Abstract:** the house shape for a new ADR — the front-matter block, the
> Status / Context / Decision / Consequences structure, the ~80-line cap, and
> the "ADR or handoff note?" rule. Copy this file (or read it before starting
> a new ADR); the front-matter and the four sections are the load-bearing
> parts, everything else is guidance. **Who is it for:** anyone adding a new
> ADR under `docs/adr/`. **When to read the rest:** before you write `NNNN-…`
> — the rule of thumb in the last section is the gate.

# ADR NNNN — <title: the decision in one line, not the capability>

Status: **Proposed** | **Accepted** | **Superseded** (by ADR NNNN)
Date: YYYY-MM-DD
Relates to: ADR NNNN, ADR NNNN (only if you need the cross-link for
context; the `related:` front-matter carries the canonical list)

## Context

The *forces* that pushed on the decision — the constraints, the existing
surfaces, the prior ADRs that pin the shape you are working within.
**Not** the options analysis (that lives in the linked design doc — the
ADR's `related:` names it). Keep this to the few paragraphs that make the
Decision legible to someone who has not read the design doc.

## Decision

The decision itself, in imperative form ("we do X, not Y"). Name the seams
(the service, the controller, the doc) and the invariants (what stays true
after this). This is the part a future agent will read when a change touches
the surface and needs to know what is allowed.

## Consequences

What this decision makes easier, what it makes harder, what it rules out
forever. One line per consequence — the reader can follow the design doc
for the "why" and the test files for the "where". If a Consequence is
longer than one sentence, it is usually a design-doc fact that does not
belong in the ADR.

---

## Front-matter reference (D11 shape)

```yaml
---
id: adr-NNNN                    # the permanent ADR number — never changes
title: "<the decision, not the feature>"
tier: reference
kind: adr
status: accepted | proposed | superseded
audiences: [developer, agent, platform-maintainer]
related:                        # the design doc (canonical) + cross-linked ADRs
  - docs/design/<subsystem>.md  # the home of the options analysis and test lists
  - docs/adr/NNNN-….md
supersedes: []                  # ADRs this one rewrites
superseded_by: []               # ADRs that rewrite this one
updated: 2026-11-02
---
```

The `related:` list is the seam: the **design doc** is the canonical
destination for any "why" content the ADR cap will not fit. The ADR index
(`docs/adr/README.md`) is generated from this front-matter — do not hand-edit
the index.

## The cap — what stays, what moves

The target is **~80 lines** (the `docs-report.ps1` bucket is ≤80 / 81–150 /
151–300 / >300; the gate is ≤80). What stays in the ADR: **Status, Context
(the forces), Decision, Consequences.** What moves out — to the linked
design doc (or the archived handoff note if the design doc is in
`archive/lanes/…`):

- **options analysis** — "we considered A and B, we picked A because…" — the
  design doc owns that; the ADR names the choice, not the alternatives;
- **test lists** — "the 12 tests are in `tests/Kumunita.Web.Tests/FooTests.cs`
  and pin X, Y, Z" — the design doc / handoff note owns that;
- **implementation notes** — CSS class names, config keys, route shapes — the
  design doc owns that;
- **verification records** — "gate N/N green, Playwright pass" — the handoff
  note owns that.

**Never delete the moved content** — the move is a relocation, not a
cleanup. The ADR keeps the *decision*; the design doc keeps the *reasoning*
and the *evidence*.

## ADR or handoff note?

The rule of thumb (from the `IMPROVE-DOCS` lane register):

- **ADR** — a decision a **future change could get wrong** — the shape of a
  surface, the standing matrix, the invariant, the "why we do it this way"
  that is not self-evident from the code. If the answer is "because of ADR
  NNNN", it belongs in an ADR.
- **Handoff note** — a **copy / CSS / one-line call** that is self-evident
  from the diff, or a **verification record** ("gate 12/12 green"). If the
  answer is "it is in the commit" or "it is in the test file", it belongs
  in the handoff note, not an ADR.

If you are unsure, default to the handoff note — an ADR you do not need is
a seam you must maintain, a handoff note you do not need is a file nobody
reads.
