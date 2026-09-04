# AGENTS.md — Critter Stack docs & tools

This repo is a documentation + tooling companion for projects built on the JasperFx
Critter Stack (**Marten**, **Wolverine**, **Weasel**), written specifically to stop AI
coding agents from burning time and context re-deriving these libraries' APIs from
general training data — which for this stack is unusually likely to be **stale**: see
`docs/marten/marten-9-breaking-changes.md` for why.

## What's here

- `docs/` — condensed, version-pinned reference docs. Start at `docs/README.md`.
- `snippets/` — small, runnable-style code templates matching a real project's
  conventions (not generic library samples).
- `mcp-server/` — an MCP server that serves `docs/` and `snippets/` on demand
  (`search_docs`, `get_doc`, `list_docs`) so you don't have to load the whole corpus
  into context. Setup instructions for VS Code, Claude Code, and Cursor are in
  `mcp-server/README.md`.

## If you have MCP tool access

Register the `critter-stack-docs` MCP server (see `mcp-server/README.md`) and prefer
`search_docs`/`get_doc` over reading files directly — it's already indexed and
returns just the relevant section.

## If you don't have MCP tool access

Read `docs/README.md` first — it's a short index/table mapping "what I'm trying to do"
to the one doc file to open. Don't read the whole `docs/` tree; open only the file(s)
the index points you to.

## The one thing to internalize before writing any code here

**Version numbers matter more than usual for this stack.** Marten 9 removed APIs
(`IMigration`, all synchronous data access, lambda-based projections) that are still
extremely common in blog posts, Stack Overflow answers, and therefore training data.
Code that looks completely idiomatic for "Marten" is frequently Marten 7/8 code that
no longer compiles against 9.x. Check `docs/versions.md` for the versions this doc set
targets, and `docs/marten/marten-9-breaking-changes.md` before trusting a remembered
Marten pattern.

## Project-specific vs general-purpose docs

Some docs in here encode one real project's architecture decisions, not general
Critter Stack advice — most notably `docs/wolverine/cqrs-lite-patterns.md`, which is
explicitly the CQRS-lite convention *that one project's ADRs settled on* (documents +
projections, no event sourcing, a single durable email handler instead of a saga).
Each such doc says so at the top. If you're using this repo for a different project
built on the same stack, treat those as worked examples to adapt, not rules to copy —
the version-specific API docs (Marten/Wolverine/Weasel proper) are the general-purpose
part.

## Don't pause mid-task to check in

If you're partway through a multi-step task and a tool batch returns
successfully with no error, keep going — don't stop to announce what you're
about to do next and wait for a "continue." That pause-and-confirm habit
(reported specifically with some models: github/orgs/community#184524) is
not a safety feature here; it just stalls iterative work like looping over
records, running several build/test rounds, or applying a change across
multiple files. Stop only when the task is actually done, or when you hit a
real blocker (an error, a missing file, an ambiguous requirement, or
something destructive/irreversible you genuinely need confirmation for) —
in those cases say what happened and what you need. In VS Code, note the
workspace's `.vscode/settings.json` also raises `chat.agent.maxRequests`
from its default of 25, since that cap alone can force a stop mid-task even
when you intend to keep going.

## Keeping this repo current

See "Refreshing this doc set" in `docs/versions.md`.

## Running PowerShell commands safely (Windows agents)

If you run terminal commands on this machine, the shell is PowerShell. PowerShell
has one trap that reliably makes agent sessions hang until a human manually
interrupts them: **here-strings**.

A here-string (`@"` ... `"@` or `@'` ... `'@`) only closes when its closing
delimiter is the very first thing on its own line — no leading spaces, no leading
tabs. Any formatting pass that indents generated code (common for AI-generated
snippets) breaks that rule silently. When the closing delimiter never lands in
column 0, PowerShell doesn't error — it drops into a `>>` continuation prompt and
waits forever for input that will never come. From the agent's side this looks
exactly like "the command hung with no output."

Rules to avoid it:

1. **Don't use here-strings to write multi-line content through a terminal
   command.** Write the file directly with your file-editing tool instead of
   shelling out to `Add-Content` / `Set-Content` with an `@"..."@` block.
2. If you must build multi-line text from PowerShell, avoid here-strings: join an
   array of lines instead (e.g. `($lines -join "`r`n") | Set-Content -Path $path`),
   or use `[System.IO.File]::WriteAllText($path, $content)` with `$content` built
   by concatenation, not a here-string.
3. **Keep PowerShell commands to a single logical line.** Chain statements with
   `;` rather than newlines, so a stray line break can't leave a brace, quote, or
   here-string unterminated.
4. If a script genuinely needs multiple lines, write it to a `.ps1` file first
   (with your file-editing tool, not the terminal), then run it non-interactively:
   `pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\script.ps1`. A
   broken script then fails fast with a parse error instead of hanging the
   interactive shell.
5. If a `>>` continuation prompt shows up in terminal output, that's this trap —
   stop and cancel rather than trying to "finish" the statement, and don't retry
   the same multi-line form.

### `$variables` don't survive between separate terminal commands

Copilot's terminal tool doesn't reliably reuse the same shell process for every
command it runs — it sometimes starts a fresh terminal/process for a later
command (a known, currently-unfixed Copilot bug: microsoft/vscode#286106,
#265881, #265863). When that happens, a variable you set in one command
(`$u12 = ...`) simply doesn't exist in the next one. PowerShell doesn't error
on an undefined variable — it silently evaluates to `$null`, which stringifies
to an empty string. That's exactly what "the `$` variables got stripped by the
terminal wrapper" looks like from the outside: the value is gone, no error was
raised, and the command that used it just produced nothing.

Treat every terminal command you run as if it might start in a brand-new
process with no memory of any earlier command:

- **Don't split a task across multiple terminal calls that depend on a
  PowerShell variable set in an earlier call.** If step 2 needs a value step 1
  computed, do both steps in the *same* command (chain with `;`, or better,
  put the whole sequence in one `.ps1` file per the rule above and run it once).
- If a value genuinely must survive across separate agent-issued commands,
  don't hold it in a `$variable` — write it to a file and read it back
  (`Set-Content` / `Get-Content`), or pass the literal value again on the next
  command instead of referencing a variable name.
- When in doubt, prefer one self-contained script file over a sequence of
  small interdependent snippets — it sidesteps this bug entirely, since the
  whole script runs top to bottom in a single process regardless of how many
  terminals Copilot decides to open.

Secondary causes worth ruling out if a command still hangs: `git`/`gh` commands
that page output (`git log`, `git diff`, `gh pr view`) can block waiting for a
keypress — prefer `git --no-pager <cmd>`, or set `core.pager` to `cat` for repos
this applies to. Commands that are long-running by design (`dotnet watch`,
`npm start`, dev servers) should be started as background tasks, not awaited as
if they will exit on their own.
