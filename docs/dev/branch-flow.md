---
id: dev-branch-flow
title: "Branch flow — development → release → main"
tier: living
kind: guide
status: current
audiences: [developer, agent, platform-maintainer]
related:
  - AGENTS.md
  - docs/dev/README.md
  - docs/adr/0307-developer-feedback-loop.md
  - docs/plans-milestones/plan-improve-devx.md
lane: improve-devx
updated: 2026-11-14
---

> **Abstract:** the three-branch flow, which branch CI watches (today `release`
> only), how to get a CI signal on a feature branch, and the owner-ops
> branch-protection ledger. Decision: [ADR 0307 D1](../adr/0307-developer-feedback-loop.md).
> **Who is it for:** a developer or agent about to open a PR, and the owner
> keeping the branch-protection rules in place. **When to read the rest:**
> before opening a PR, or when a CI job is (or is not) running on your branch.

# Branch flow

## The three branches

| Branch | Role | Merged from |
|---|---|---|
| `development` | feature work, the day-to-day head of a branch | feature branches |
| `release` | the CI + gate gate — PRs into `main` must come from here | `development` |
| `main` | shipped | `release` only |

Feature branches merge into `development`. `development` merges into
`release`. `release` is the **only** branch that may open a PR into `main`.

## Which branch CI watches

`ci.yml` `on:` is `push: { branches: [release] }` +
`pull_request: { branches: [release] }` — **CI runs on `release` only**
(push and PR). There is no `workflow_dispatch` yet (lands in **DX04**);
until then, the only way to get a CI signal on a feature branch is to merge
it into `release` and watch that branch.

## Owner-ops branch-protection ledger

These are owner-ops on GitHub, recorded here so a maintainer can audit them
in one place. Each is a target state, not a claim that it is already in
effect:

- **`main`** — a ruleset that accepts PRs **from `release` only**. The
  CI-side tripwire is `guard-main.yml` (DX05): a `pull_request` workflow that
  fails any PR into `main` whose head is not `release`.
- **`release`** — a ruleset that **requires** the `test`, `docs`, `code`,
  `e2e` **and `devx`** checks to pass. `devx` joins the required set once
  **DX33** lands (the `devx` CI job in `ci.yml`).
