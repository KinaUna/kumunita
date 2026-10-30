# Repository instructions for GitHub Copilot

This file is read by GitHub Copilot in Visual Studio (repo-wide instructions).
For the full project context, also read `AGENTS.md` at the repository root —
it covers what this repo is, how to use the docs/snippets/MCP server, and the
version-pinned Critter Stack (Marten/Wolverine/Weasel) notes.

## Shared doctrine (defined once, in `AGENTS.md`)

These are defined in [`AGENTS.md`](../AGENTS.md) — the single source of
truth, read by every agent framework (Copilot, Claude, Cursor, etc.). This
file only points to each section; edit the content in `AGENTS.md`, not here.

- **Running terminal commands safely (WSL2 / Linux agents)** — including the
  `$variables`-don't-survive-between-terminal-calls gotcha and the
  quoting / multi-line-content rules. See
  [AGENTS.md § Running terminal commands safely](../AGENTS.md#running-terminal-commands-safely-wsl2--linux-agents).
- **Razor verification doctrine** — including the four known Razor traps in
  this codebase and the "Getting a live server" paragraph (docker-compose,
  the sample GlobalAdmin credentials, the browser-snapshot evidence rule).
  See [AGENTS.md § Razor verification doctrine](../AGENTS.md#razor-verification-doctrine).
- **Git state gotcha** — "Nothing to commit" after staging usually means the
  commit already landed. See
  [AGENTS.md § Git state gotcha](../AGENTS.md#git-state-gotcha).
- **Using the browser (trusted-folder quirk)** — including the `.tmp/`
  harness rules, the `build-harness.js` rebuild rule, and the
  plans-folder-path rule (`docs/plans-milestones/`). See
  [AGENTS.md § Using the browser](../AGENTS.md#using-the-browser-trusted-folder-quirk).
- **Don't pause mid-task to check in** — stop only on a real blocker, not to
  announce the next step. See
  [AGENTS.md § Don't pause mid-task to check in](../AGENTS.md#dont-pause-mid-task-to-check-in).
- **CI known-failure cheatsheet** — the four environmental CI gotchas
  (`actions/setup-dotnet` owner, Playwright browser install, `libnss3`
  local deps, Node 20 deprecation). See
  [AGENTS.md § CI known-failure cheatsheet](../AGENTS.md#ci-known-failure-cheatsheet).
- **CI gates and anti-regression pins** — the `improve-check.ps1`
  god-file gate and the `@media` block-count pin, both by-design
  anti-regression guards that fail for documented reasons. See
  [AGENTS.md § CI gates and anti-regression pins](../AGENTS.md#ci-gates-and-anti-regression-pins-dont-rediscover-them).
- **Postgres diagnostic conventions** — `mt_doc_` table naming, the
  `postgres` vs `kumunita` database gotcha, and the `/admin/setup`
  404-is-success rule. See
  [AGENTS.md § Postgres diagnostic conventions](../AGENTS.md#postgres-diagnostic-conventions-live-server).

## VS Code / Copilot-specific content

Add VS Code- or Copilot-specific rules **here** (e.g. a new
integrated-browser quirk, a `.tmp/` harness file, a task-runner convention) —
not in `AGENTS.md`, which is read by frameworks that may not have VS Code.
Keep it to the VS Code surface; general doctrine belongs in `AGENTS.md`.
