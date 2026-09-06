# AGENTS.md — Kumunita

Kumunita is a **self-hosted community platform for one neighborhood**. Each
deployment serves a single community (its own container + Postgres) — there is
no multi-tenant data model.

## Start here

- `README.md` — what the platform is, its current status (M3 shipped, M4 next),
  principles, tech stack, and the **Running** instructions.
- `docs/ARCHITECTURE.md` — the detailed map: stack, data model, the three
  bounded contexts (Identity / UserInfo / Authorization), the module-boundary
  contracts, and the CQRS-lite & side-effects (Wolverine) convention.
- `docs/adr/` — decision records. **ADR 0004** (persistence & schema evolution)
  and **ADR 0006** (module boundary contracts) constrain how data is stored and
  how contexts talk to each other — read them before touching persistence,
  schema, or cross-context calls.
- `docs/design/` — the per-milestone design docs (M1 identity/access, M2
  directory/profiles/groups, M3/m3b posts + moderation). The M1–M3 scope is the
  shipped surface.
- `docs/SECURITY.md` — the privacy model, threat model, and access-control
  rules. This repo is privacy-first; treat the author's audience choice as the
  authoritative constraint.

## The shape of the code

- **`Kumunita.Core`** — domain + services. Bounded contexts live in
  `Identity/`, `UserInfo/`, `Authorization/`, `Posts/`, `Announcements/`,
  `Moderation/`, `Bootstrap/`. `DependencyInjection.cs` registers each feature.
- **`Kumunita.Web`** — Razor Pages / MVC (server-rendered), `Program.cs`
  bootstrap, `Milestones.cs` (home-page roadmap — keep in sync with `README.md`).
- **Persistence** (ADR 0004 §B): **Marten** owns the domain documents and
  versioned schema (the `M1DocTypes` / `M3DocTypes` / `M4DocTypes` registration
  surfaces). **EF Core is used only for ASP.NET Identity tables** — do not put
  domain data in EF.

## The one thing to internalize before writing code here

This repo uses **Marten and Wolverine** on **.NET 10**. The idiomatic patterns
in training data are frequently **older major versions** of both libraries. Before
writing Marten schema/migration code or Wolverine handler code, check the actual
APIs in the current dependency versions in the `.csproj` files — do not copy a
"common" Marten or Wolverine pattern from memory. The ADRs above encode this
project's *specific* choices (CQRS-lite, documents + projections, no event sourcing,
a single durable email handler rather than a saga) — follow them.

## Conventions to preserve

- Server-rendered Razor Pages / MVC over Blazor or SPA patterns.
- Audit-by-default: access to audience-restricted content is always logged.
- Thin token, fat authorization: "may this actor see that resource?" is resolved
  by the `Authorization` service per request, not encoded in an identity claim.
- Keep `Milestones.cs` and the README **Roadmap** section together — `Milestones.cs`
  has a comment that says this is the contract.

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

## Keeping the docs in sync with the code

This repo is deliberate about doc ↔ code parity, so keep these pairs together
when you change behavior:

- The **README Roadmap** and `src/Kumunita.Web/Milestones.cs` — the home page
  renders `Milestones.cs`, and its doc-comment names the README as the source
  of truth. Bump a milestone to "done" in one when it ships and the other
  follows.
- The **bounded-context / persistence layout** described in `docs/ARCHITECTURE.md`
  and ADR 0004/0006 — if you add a new context, doc-type surface, or change how
  contexts are registered in `DependencyInjection.cs`, reflect it in the relevant
  ADR rather than leaving the design doc stale.
- A **new capability that settles a design question** gets an ADR under `docs/adr/`
  (numbered after the current highest), not just a prose note in the README.

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
