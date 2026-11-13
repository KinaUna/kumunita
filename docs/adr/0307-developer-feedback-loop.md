---
id: adr-0307
title: "The developer feedback loop — the branch flow, one Postgres container
 per test assembly, check.ps1 as the one entry point, the generated code map,
 mirrored tests, the lane-code ratchet, and devx-check"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/plans-milestones/plan-improve-devx.md
  - docs/adr/0004-data-persistence-and-schema-evolution.md
supersedes: []
superseded_by: []
updated: 2026-11-13
---

# ADR 0307 — The developer feedback loop

Status: **Accepted**
Date: 2026-11-13

## Context

The IMPROVE-DOCS, CODE and SEC lanes built strong parts — gates, ratchets,
doctrine docs. What a maintainer meets first is the *linkage* between them:
the branch CI watches, the command that runs everything, the map from a
concern to its folder, the doc that explains a red gate. Each was a seam with
no test, so it drifted. Two severity-S gaps: CI runs only on `release`, but
five PRs since 1 Nov went `development` → `main` with no CI (the `docs` and
`code` jobs had never run in CI); and the Core suite took ~7 min because
147 + 34 test classes each started their own Postgres container. A
cold-start agent needed 32 calls, 7 wrong turns, ~123K tokens for three
ordinary tasks.

## Decision

- **D1** — The branch flow is `development` → `release` → `main`. CI stays
  on `release` (push + PR). A guard workflow fails any PR into `main` whose
  head is not `release`. Branch protection is owner-ops.
- **D2** — One Postgres container per test assembly, via
  `[assembly: AssemblyFixture(typeof(PostgresFixture))]`; each class keeps a
  fresh database and classes keep running in parallel. Server-scoped tests
  (roles, `ALTER SYSTEM`) stay on a class fixture, on a named allowlist.
- **D3** — `tools/check.ps1` is the one entry point
  (build → tsc → web → core → docs → code → devx). A missing tool is a FAIL
  with the install line, never a silent skip. `-Fast`, `-Only`, `-Doctor`.
- **D4** — `docs/dev/code-map.md` is *generated* from the code (context →
  Core folder, DocTypes, service, controllers, views, tests, top ADRs);
  `AGENTS.md` links it instead of listing contexts; it is checked for
  staleness.
- **D5** — The test trees mirror the source folders (context folders in
  Core.Tests, controller folders in Web.Tests). Namespaces are unchanged.
- **D6** — The lane-code ratchet: the count only goes down; new comments
  cite ADR numbers, not unit codes; a generated glossary resolves each code
  prefix to its archived lane.
- **D7** — `devx-check.ps1` (exit 0/1) + `devx-report.ps1` + a shrink-only
  baseline + a `devx` CI job. Each rule lands *with the unit that fixes its
  gap*, never before.

## Consequences

- CI signals on the merge path again (E1 closed).
- The Core suite target is ≤ 90 s locally (E2); the Web suite ≤ 20 s.
- One command runs the whole loop; no gate silently skips.
- Unit codes remain in comments until the ratchet drives them out — the
  glossary (D6) is the lookup.
- Held decisions: the `ImproveHarnessTests` pwsh shell-out (D9, `// TODO ❓`
  at DX13) and the translation-surface rename (D10, `// TODO ❓` at DX21).
- D8 (sealed units) and D10 (held) are unit-shape / held decisions, not part
  of the loop itself — they are named here so a reader does not look for
  them among D1–D7.
