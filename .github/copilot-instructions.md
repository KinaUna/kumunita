# Repository instructions for GitHub Copilot

This file is read by GitHub Copilot in Visual Studio (repo-wide instructions).
For the full project context, also read `AGENTS.md` at the repository root —
it covers what this repo is, how to use the docs/snippets/MCP server, and the
version-pinned Critter Stack (Marten/Wolverine/Weasel) notes.

## Running PowerShell commands safely (Windows agents)

The terminal on this machine is PowerShell. PowerShell has one trap that
reliably makes agent sessions hang until a human manually interrupts them:
**here-strings**.

A here-string (`@"` ... `"@` or `@'` ... `'@`) only closes when its closing
delimiter is the very first thing on its own line — no leading spaces, no
leading tabs. Any formatting/indentation of generated code breaks that rule
silently. When the closing delimiter never lands in column 0, PowerShell
doesn't error — it drops into a `>>` continuation prompt and waits forever
for input that will never come. This looks exactly like "the command hung
with no output."

Rules to avoid it:

1. **Don't use here-strings to write multi-line content through a terminal
   command.** Write the file directly with your file-editing tool instead of
   shelling out to `Add-Content` / `Set-Content` with an `@"..."@` block.
2. If you must build multi-line text from PowerShell, avoid here-strings:
   join an array of lines instead, or use
   `[System.IO.File]::WriteAllText($path, $content)` with `$content` built by
   concatenation, not a here-string.
3. **Keep PowerShell commands to a single logical line.** Chain statements
   with `;` rather than newlines, so a stray line break can't leave a brace,
   quote, or here-string unterminated.
4. If a script genuinely needs multiple lines, write it to a `.ps1` file
   first (with your file-editing tool, not the terminal), then run it
   non-interactively: `pwsh -NoProfile -NonInteractive -ExecutionPolicy
   Bypass -File .\script.ps1`. A broken script then fails fast with a parse
   error instead of hanging the interactive shell.
5. If a `>>` continuation prompt shows up in terminal output, that's this
   trap — stop and cancel rather than trying to "finish" the statement, and
   don't retry the same multi-line form.

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

Secondary causes worth ruling out if a command still hangs: `git`/`gh`
commands that page output (`git log`, `git diff`, `gh pr view`) can block
waiting for a keypress — prefer `git --no-pager <cmd>`. Commands that are
long-running by design (`dotnet watch`, `npm start`, dev servers) should be
started as background tasks, not awaited as if they will exit on their own.

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
