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

Secondary causes worth ruling out if a command still hangs: `git`/`gh`
commands that page output (`git log`, `git diff`, `gh pr view`) can block
waiting for a keypress — prefer `git --no-pager <cmd>`. Commands that are
long-running by design (`dotnet watch`, `npm start`, dev servers) should be
started as background tasks, not awaited as if they will exit on their own.
