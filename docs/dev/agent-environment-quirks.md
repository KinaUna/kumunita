---
id: dev-agent-environment-quirks
title: "Agent sandbox quirks"
tier: living
kind: guide
status: current
audiences: [developer, agent]
related:
  - AGENTS.md
  - docs/dev/README.md
  - docs/archive/lanes/improve-docs/plan-improve-docs.md
lane: improve-docs
updated: 2026-11-02
---

> **Abstract:** the machine-specific quirks of running in this repo's
> sandbox — the git "nothing to commit" gotcha, the integrated-browser
> trusted-folder rule (and the `.tmp/` harness setup), and the
> terminal-command safety rules for the WSL2 / Copilot agent channel.
> **Who is it for:** an agent (or a developer emulating one) driving the
> terminal and integrated browser on this machine. **When to read the rest:**
> the moment a terminal command looks wrong, a browser harness appears
> broken, or "Nothing to commit" surprises you after staging.

# Agent sandbox quirks

## Git state gotcha

"Nothing to commit" after staging usually means the commit **already landed**
(the GUI or an earlier turn committed on your behalf). Verify with
`git --no-pager log -1` before troubleshooting the working tree.

## Using the browser (trusted-folder quirk)

The integrated browser (Playwright) can only open files in **trusted**
folders. The Linux system temp folder (`/tmp/...`) is not trusted: any
`file://` URL pointing there returns a **403 "Forbidden. File does not
reside within a trusted folder."** error — and overwriting an already-trusted
file in that folder *invalidates* its trust (a previously shared page stops
loading). This wastes a lot of time: the harness looks broken (JS leaks, page
errors, typing "does nothing") when the real cause is the untrusted path.

The reliable setup (already in the repo):

- **`.tmp/`** — a gitignored scratch folder at the repo root (see
  `.gitignore`). Files under the repo root **are** trusted, so a browser
  harness placed here loads and runs fine.
- **`.tmp/build-harness.js`** — regenerates `.tmp/harness.html` by inlining
  the **real** compiled JS (`wwwroot/js/lib/rich-editor.js` +
  `dom-to-markdown.js`, with `import`/`export` stripped and any `</script>`
  in JS doc-comments escaped to `<\/script>` so it doesn't close the inline
  `<script>` early). Run it with `node .tmp/build-harness.js` after every
  `npm --prefix src/Kumunita.Web run build`.
- The harness mirrors the real toolbar/pane/textarea markup (from
  `Views/Announcement/Edit.cshtml`) so `bindRichEditor` self-wires exactly as
  in the app.

Rules:

1. **Never put browser harness files in the system temp folder (`/tmp`).**
   Use `.tmp/` (in-repo, gitignored).
2. **Re-run `node .tmp/build-harness.js` after each TS recompile** so the
   harness picks up the latest `wwwroot/js` output — a stale harness will
   test the wrong code.
3. **Drive with real `page.keyboard.type`** once the page is trusted — the
   in-repo harness gives a working, faithful proxy for contenteditable
   behavior (the old untrusted setup could not).

## Running terminal commands safely (WSL2 / Linux agents)

If you run terminal commands on this machine, the shell is **bash** (inside a
WSL2 sandbox). Bash doesn't have PowerShell's here-string / continuation-prompt
traps, but it has its own class of agent-session hazard worth knowing about.

### `$variables` don't survive between separate terminal commands

Copilot's terminal tool doesn't reliably reuse the same shell process for every
command it runs — it sometimes starts a fresh terminal/process for a later
command (a known, currently-unfixed Copilot bug: microsoft/vscode#286106,
#265881, #265863). When that happens, a variable you set in one command
(`MYVAR=...`) simply doesn't exist in the next one. Bash doesn't error on an
undefined variable — it expands to an empty string. That's exactly what "the
`$` variables got stripped by the terminal wrapper" looks like from the
outside: the value is gone, no error was raised, and the command that used it
just produced nothing.

Treat every terminal command you run as if it might start in a brand-new
process with no memory of any earlier command:

- **Don't split a task across multiple terminal calls that depend on a shell
  variable set in an earlier call.** If step 2 needs a value step 1 computed,
  do both steps in the *same* command (chain with `&&` or `;`), or better,
  put the whole sequence in one script file (e.g. `.tmp/run.sh`) and run it
  once with `bash .tmp/run.sh`.
- If a value genuinely must survive across separate agent-issued commands,
  don't hold it in a shell variable — write it to a file and read it back
  (`echo "$val" > /tmp/kumunita-var` / `val=$(cat /tmp/kumunita-var)`), or
  pass the literal value again on the next command instead of referencing a
  variable name.
- When in doubt, prefer one self-contained script file over a sequence of
  small interdependent snippets — it sidesteps this bug entirely, since the
  whole script runs top to bottom in a single process regardless of how many
  terminals Copilot decides to open.

### Quoting and multi-line content

- **Quote variables**: `"$var"` instead of `$var`, so paths with spaces don't
  split into multiple arguments.
- **Don't paste multi-line command blocks into the terminal across separate
  tool calls without chaining.** If a sequence needs to be atomic, chain it
  with `&&` on a single line, or write a `.sh` file with the file-editing
  tool and run it with `bash script.sh` — a broken script then fails fast
  with a syntax error instead of leaving the interactive shell in a confused
  continuation state.
- If a `>` continuation prompt shows up in terminal output (bash's
  line-continuation marker, meaning an unterminated quote or backslash),
  stop and cancel rather than trying to "finish" the statement, and don't
  retry the same multi-line form.

### When the sync terminal channel wedges (no output at all)

A separate, Copilot-agent-specific failure mode (not a bash/WSL2 one): a
`mode=sync` terminal call can return **"Command produced no output"** — even
for a trivial `echo` — because the agent's *output-capture* layer wedges.
This is the known Copilot agent terminal bug cited above (microsoft/vscode#
286106 / #265881 / #265863); it is not caused by anything in the repo and no
`.vscode/settings.json` key disables it. Treat it as a **channel** failure,
not a command failure, and do not keep retrying the same sync call. The
reliable workarounds, best first:

- **Prefer `mode=async` + `get_terminal_output`** for anything non-trivial
  (builds, test runs, servers). This has been observed to work even while the
  sync channel is dead: the `Kumunita.Core.Tests` suite (1440 tests) completed
  and reported its full `TEST EXECUTION SUMMARY` through the async channel in
  exactly that situation.
- **Redirect to a file as a belt-and-suspenders** so the result survives on
  disk regardless of inline capture: `… 2>&1 > .tmp/result.log`, then read it
  with the file tools.
- **Keep each command self-contained** (single process, no cross-call
  `$vars` — the doctrine above) so a wedged call has a small blast radius.

Secondary causes worth ruling out if a command still hangs: `git`/`gh` commands
that page output (`git log`, `git diff`, `gh pr view`) can block waiting for a
keypress — prefer `git --no-pager <cmd>`, or set `core.pager` to `cat` for
repos this applies to. Commands that are long-running by design
(`dotnet watch`, `npm start`, dev servers) should be started as background
tasks, not awaited as if they will exit on their own.

## Terminal commands

- Never end your turn while a terminal command you depend on is still running.
  "I'll be notified when it finishes" is not an acceptable stopping point.
- Run builds, tests and migrations in the foreground. Use a background terminal
  only for processes that never exit, like `dotnet watch`, dev servers or containers.
- If a command times out or you start it in the background, wait for it with the
  await-terminal tool, or keep checking get_terminal_output until it exits.
  Then continue the task from the result.
- For a long-running server, wait until its output shows it's ready
  (e.g. "Now listening on"), then carry on.
