---
id: dev-readme
title: "Developer & agent doctrine"
tier: living
kind: entry
status: current
audiences: [developer, agent]
related:
  - AGENTS.md
  - docs/README.md
  - docs/plans-milestones/improve-docs/plan-improve-docs.md
lane: improve-docs
updated: 2026-11-02
---

> **Abstract:** the home for the working doctrine that was split out of
> `AGENTS.md` in `D06` of the `IMPROVE-DOCS` lane — the Razor verification
> doctrine, the testing + CI conventions, the live-server debugging
> conventions, and the sandbox / browser / terminal quirks. Read this to find
> the right doctrine file; `AGENTS.md` now links here instead of carrying a
> second copy. **Who is it for:** a developer or agent writing, testing, or
> debugging code in this repo. **When to read the rest:** when you're about
> to verify a rendering bug, run tests, read CI, debug the live server, or
> work in the sandbox.

# Developer & agent doctrine

These files are the canonical home for the working doctrine that used to live
inline in [`AGENTS.md`](../../AGENTS.md). `AGENTS.md` is loaded into every
agent session, so its length is a per-session cost: as of `D06` it carries
only the must-knows and a pointer here, and each file below owns one topic —
one fact, one place (the [linking rule](../DOCS-CONVENTIONS.md)).

| File | What it covers |
|---|---|
| [`running-locally.md`](running-locally.md) | How to run the app locally — the dev container, the no-app-container loop, the sample-data / demo accounts, and the prod topology (split from the `README.md` Running section in `D07`). |
| [`razor-verification.md`](razor-verification.md) | "The rendered HTML is the evidence" rule, the known Razor traps in this codebase, and how to get a live server to verify against. |
| [`testing-and-ci.md`](testing-and-ci.md) | The xunit.v3 test-runner quirk, the CI known-failure cheatsheet, and the by-design CI gates / anti-regression pins. |
| [`live-server-debugging.md`](live-server-debugging.md) | The Postgres conventions that trip you up (`mt_doc_` naming, the `kumunita` vs `postgres` DB, `pgcrypto`, the `/admin/setup` 404). |
| [`agent-environment-quirks.md`](agent-environment-quirks.md) | The git-state gotcha, the browser trusted-folder quirk, and the terminal-command safety rules for the WSL2 / Copilot sandbox. |

The entry pages that point into this folder are
[`start/developer.md`](../start/developer.md) and
[`start/agent.md`](../start/agent.md); the lane register driving `D06` is
[`plans-milestones/improve-docs/plan-improve-docs.md`](../plans-milestones/improve-docs/plan-improve-docs.md).
