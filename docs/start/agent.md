---
id: start-agent
title: "Agent entry"
tier: living
kind: entry
status: current
audiences: [agent]
related:
  - AGENTS.md
  - docs/README.md
  - docs/DOCS-CONVENTIONS.md
  - docs/plans-milestones/improve-docs/plan-improve-docs.md
  - docs/philosophy/START-HERE.md
updated: 2026-11-02
---

> **Abstract:** the links an AI agent working in this repo is most likely
> to want, each with a one-line reason. Links only, no facts — this page
> cannot drift. **Who is it for:** an AI assistant (Copilot, Claude,
> Cursor, etc.) working in this repo. **When to read the rest:** before
> touching code or docs in this session.

# Agent entry

You're an AI agent working in this repo. The docs below are the ones that
matter for *you* — the things to read before you touch code or docs.

| Link | Why |
|---|---|
| [`AGENTS.md`](../../AGENTS.md) | The must-knows — the shape of the code, the conventions, the test-runner quirk, the CI cheatsheet, the Razor doctrine, the terminal-command safety rules, the "don't pause mid-task" rule. Loaded into every agent session. |
| [`docs/DOCS-CONVENTIONS.md`](../DOCS-CONVENTIONS.md) | The shared rules every doc in this repo follows — the front-matter schema, the abstract rule, the ADR cap, the linking rule, the "one fact, one place" rule. Read before your first doc change. |
| [`docs/README.md`](../README.md) | The docs map — where things live, the tier model, the entry pages. |
| [`docs/plans-milestones/improve-docs/plan-improve-docs.md`](../plans-milestones/improve-docs/plan-improve-docs.md) | The lane register for the current doc restructure — the `D`-units, the evidence, the target shape. The unit spec for your current task. |
| [`docs/philosophy/START-HERE.md`](../philosophy/START-HERE.md) | The philosophy in one hour — the "why" behind the conventions, the integration argument that the seams are the point. |
| [`docs/adr/`](../adr/README.md) | The ADR index — the deciding document for any non-obvious rule. The code cites ADR numbers ~9 600 times; the number is the stable key. |
| [`docs/design/m3b-moderation.md`](../design/m3b-moderation.md) | A sample of the living design references — the current truth per subsystem (the `design/` folder; `D10` settles its status). |

The per-audience entry pages live under [`start/`](../README.md#per-audience-entry-pages)
— this page is one of them.
