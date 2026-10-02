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

## Razor verification doctrine

When the user reports a rendering bug, **the rendered HTML is the evidence,
not the source file** — and the user's report outranks your source reading.

- **Never conclude "stale build / stale process / stale deployment"** unless
  you have confirmed the served bytes differ from what the current source
  would produce. If you're about to tell the user "this is stale, refresh
  it", you own that claim and it has been wrong before.
- **Verify against the served page**, not the `.cshtml`: fetch the real HTML
  (`Invoke-WebRequest` with a session cookie, or the integrated browser) and
  inspect the actual markup. "The view file looks right" is not verification.
- Known Razor traps in this codebase (each of these has actually bit us):
  - **Un-awaited `async Task<string>` helpers** interpolated into a string
    render the type name (`System.Runtime.CompilerServices.AsyncTaskMethodBuilder...`).
    When a banner/label looks wrong, grep every call site of the async helper
    for a missing `await` (this hit `SeedLanguageName` in 9 places).
  - **Razor HTML-encodes expressions inside attribute values** — building a
    `style`/`data-*` attribute with `@(cond ? " style=\"display:none\"" : null)`
    produces broken HTML. Concatenate or use `Html.Raw`; never interpolate
    quoted attribute strings via `@(...)`.
  - **A TagHelper tag inside a quoted attribute value renders as raw HTML
    text** — `<kw-l>` cannot live inside `title="..."`. Put the localized
    label in a proper inline element (`<span>`/`<button>`) instead.
  - **Boolean TagHelper flags bind differently as bare vs. quoted**
    (`<kw-dt TimeOnly>` vs `TimeOnly="true"`) in .NET 10's Razor source
    generator. When a flag "doesn't take effect", check the generated
    descriptor or the rendered output — don't trust the markup at face value.
- A URL or link showing a **literal `@Model.X`** (404s,
  `/groups/@Model.GroupId/posts`) is the same class of bug — an uninterpolated
  Razor attribute value, not a routing problem. Fix the markup; don't debug
  the route.

**Getting a live server.** `dotnet run` requires a `ConnectionStrings:Kumunita`
value the agent does not have; the docker-compose stack carries all credentials
and seeds sample data on first boot. To render a page end-to-end:

```bash
docker compose build app && docker compose up -d app   # rebuild + recreate (volumes persist)
```

The app needs ~8 s to finish booting (Marten schema + Wolverine leadership).
App URL: `http://localhost:5080`. Sample-data GlobalAdmin: `admin@examplium.com`
/ `Admin123!` (the `SampleDataSeeder` constants in `Kumunita.Core/Bootstrap/`).
Sign in in the integrated browser (or `Invoke-WebRequest` with a session cookie
+ anti-forgery token), then navigate to the target page. The browser snapshot
is the evidence.

## Git state gotcha

"Nothing to commit" after staging usually means the commit **already landed**
(the GUI or an earlier turn committed on your behalf). Verify with
`git --no-pager log -1` before troubleshooting the working tree.

## Using the browser (trusted-folder quirk)

The integrated browser (Playwright) can only open files in **trusted**
folders. The Windows system temp folder (e.g. `%LOCALAPPDATA%\Temp\...`) is
not trusted: any `file://` URL pointing there returns a **403 "Forbidden.
File does not reside within a trusted folder."** error — and overwriting an
already-trusted file in that folder *invalidates* its trust (a previously
shared page stops loading). This wastes a lot of time: the harness looks
broken (JS leaks, page errors, typing "does nothing") when the real cause is
the untrusted path.

The reliable setup (already in the repo):

- **`d:\repos\kumunita\.tmp\`** — a gitignored scratch folder (see
  `.gitignore`). Files under the repo root **are** trusted, so a browser
  harness placed here loads and runs fine.
- **`.tmp\build-harness.js`** — regenerates `.tmp\harness.html` by inlining
  the **real** compiled JS (`wwwroot/js/lib/rich-editor.js` +
  `dom-to-markdown.js`, with `import`/`export` stripped and any `</script>`
  in JS doc-comments escaped to `<\/script>` so it doesn't close the inline
  `<script>` early). Run it with `node .tmp\build-harness.js` after every
  `npm --prefix src/Kumunita.Web run build`.
- The harness mirrors the real toolbar/pane/textarea markup (from
  `Views/Announcement/Edit.cshtml`) so `bindRichEditor` self-wires exactly as
  in the app.

Rules:

1. **Never put browser harness files in the system temp folder.** Use
   `.tmp/` (in-repo, gitignored).
2. **Re-run `node .tmp\build-harness.js` after each TS recompile** so the
   harness picks up the latest `wwwroot/js` output — a stale harness will
   test the wrong code.
3. **Drive with real `page.keyboard.type`** once the page is trusted — the
   in-repo harness gives a working, faithful proxy for contenteditable
   behavior (the old untrusted setup could not).
4. **Plans-folder path**: the canonical folder is `docs/plans-milestones/`
   (with an "s"), holding `in-progress/` and `done/<lane>/`. Older handoff
   notes sometimes say `docs/plan-milestones` — that's a stale path, treat it
   as the same folder.
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
in those cases say what happened and what you need. **A failed tool call or
a terminal command error is a recoverable step, not a stopping point**: read
the error, adjust, and continue the same task. If the *same* command has
failed twice, change approach (different command form, different tool)
rather than retrying it a third time. In VS Code, note the workspace's
`.vscode/settings.json` also raises `chat.agent.maxRequests`
from its default of 25, since that cap alone can force a stop mid-task even
when you intend to keep going.
