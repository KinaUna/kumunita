# Repository instructions for GitHub Copilot

This file is read by GitHub Copilot in Visual Studio (repo-wide instructions).
For the full project context, also read `AGENTS.md` at the repository root —
it covers what this repo is, the shape of the code, the conventions, and the
Marten/Wolverine version notes.

## Shared doctrine (defined once, in `AGENTS.md` + `docs/dev/`)

`AGENTS.md` carries the must-knows and is the single source of truth read by
every agent framework (Copilot, Claude, Cursor, etc.). Since `D06` of the
`IMPROVE-DOCS` lane, the *working* doctrine lives in [`docs/dev/`](../docs/dev/README.md)
— `AGENTS.md` links there instead of restating it. This file points at each
home; edit the content there, not here.

- **Razor verification doctrine** — the rendered HTML is the evidence, the known
  Razor traps in this codebase, and how to get a live server. See
  [`docs/dev/razor-verification.md`](../docs/dev/razor-verification.md).
- **Testing + CI** — the xunit.v3 test-runner quirk, the CI known-failure
  cheatsheet (`actions/setup-dotnet` owner, Playwright browser install,
  `libnss3` local deps, Node 20 deprecation), and the by-design CI gates /
  anti-regression pins (`improve-check.ps1`, the `@media` block-count pin). See
  [`docs/dev/testing-and-ci.md`](../docs/dev/testing-and-ci.md).
- **Live-server debugging** — the Postgres conventions that trip you up
  (`mt_doc_` naming, the `postgres` vs `kumunita` database, `pgcrypto`, the
  `/admin/setup` 404-is-success rule). See
  [`docs/dev/live-server-debugging.md`](../docs/dev/live-server-debugging.md).
- **Agent sandbox quirks** — the git-state gotcha, the browser trusted-folder
  quirk (the `.tmp/` harness + `build-harness.js` rules), and the
  terminal-command safety rules for the WSL2 / Copilot channel. See
  [`docs/dev/agent-environment-quirks.md`](../docs/dev/agent-environment-quirks.md).
- **Don't pause mid-task to check in** — stop only on a real blocker, not to
  announce the next step. See [AGENTS.md § Don't pause mid-task to check in](../AGENTS.md#dont-pause-mid-task-to-check-in).

## VS Code / Copilot-specific content

Add VS Code- or Copilot-specific rules **here** (e.g. a new
integrated-browser quirk, a `.tmp/` harness file, a task-runner convention) —
not in `AGENTS.md` or `docs/dev/`, which are read by frameworks that may not
have VS Code. Keep it to the VS Code surface; general doctrine belongs in
`docs/dev/`.
