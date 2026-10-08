# Repository instructions for GitHub Copilot

This file is read by GitHub Copilot in Visual Studio (repo-wide instructions).
For the full project context, also read `AGENTS.md` at the repository root —
it covers what this repo is, how to use the docs/snippets/MCP server, and the
version-pinned Critter Stack (Marten/Wolverine/Weasel) notes.

## Shared doctrine (defined once, in `AGENTS.md`)

The following sections are **defined in [`AGENTS.md`](../AGENTS.md)** — the
source of truth read by every agent framework (Copilot, Claude, Cursor, etc.).
This file no longer duplicates them; it links to each section. Update the
content in `AGENTS.md`, not here. (De-duplicated by `U03`, IMPROVE lane —
the previous file carried full copies of each section below; the two had
drifted in subtle ways.)

- **Running PowerShell commands safely (Windows agents)** — including the
  `$variables`-don't-survive-between-terminal-calls gotcha and the
  here-string trap. See
  [AGENTS.md § Running PowerShell commands safely](../AGENTS.md#running-powershell-commands-safely-windows-agents).
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

## This file's unique content (VS Code / Copilot specific)

The pointer list above is the *whole* of the shared doctrine; everything else
a reader of this file needs is already covered by `AGENTS.md`. If a future
lane adds a new VS Code- or Copilot-specific rule (e.g. a new
integrated-browser quirk, a new `.tmp/` harness file, a new task-runner
convention), add it **here**, in this section, not in `AGENTS.md` (which is
read by frameworks that may not have VS Code). Keep it to the VS-Code surface
— general doctrine belongs in `AGENTS.md`, and the `improve-check.ps1` gate
(c) fails the close if a `##` heading is duplicated between the two files.
