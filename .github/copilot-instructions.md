# Repository instructions for GitHub Copilot

This file is read by GitHub Copilot in Visual Studio (repo-wide instructions).
The single source of truth is `AGENTS.md` at the repository root — read it for
what this repo is, the code's shape, the conventions, and the Marten/Wolverine notes.
The *working* doctrine lives in [`docs/dev/`](../docs/dev/README.md) —
razor-verification, testing-and-ci, live-server-debugging, agent-environment-quirks.

## VS Code / Copilot-specific content

Add VS Code- or Copilot-specific rules **here** (e.g. an integrated-browser quirk,
a `.tmp/` harness file, a task-runner convention) — not in `AGENTS.md` or
`docs/dev/`, which are read by frameworks that may not have VS Code.
