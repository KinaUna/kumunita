# AGENTS.md — Kumunita

Kumunita is a **self-hosted community platform for one neighborhood**. Each
deployment serves a single community (its own container + Postgres) — there is
no multi-tenant data model.

## Start here

- `README.md` — what the platform is, its current status (M3 + group posts
  shipped; **multilingual is next** — pulled forward from M6 so the platform can
  be exercised in more than one language before the circle widens; then M4
  events/RSVPs, M5 projects), principles, tech stack, and the **Running** instructions.
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
  `Identity/`, `UserInfo/`, `Authorization/`, `Posts/`, `Moderation/`,
  `Localization/`, `Bootstrap/`. The M3b "platform announcements" lane
  (`Announcements/`, registered in `M3DocTypes` alongside `M1DocTypes`) is a
  feature module within that surface, not a top-level bounded context.
  `DependencyInjection.cs` registers each feature.
- **`Kumunita.Web`** — Razor Pages / MVC (server-rendered), `Program.cs`
  bootstrap, `Milestones.cs` (home-page roadmap — keep in sync with `README.md`),
  `Models/` + `Security/` (view-models and claim shaping, both server-rendered).
- **Persistence** (ADR 0004 §B): **Marten** owns the domain documents and
  versioned schema (the `M1DocTypes` / `M3DocTypes` registration
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
in those cases say what happened and what you need. **A failed tool call or
a terminal command error is a recoverable step, not a stopping point**: read
the error, adjust, and continue the same task. If the *same* command has
failed twice, change approach (different command form, different tool)
rather than retrying it a third time. In VS Code, note the workspace's
`.vscode/settings.json` also raises `chat.agent.maxRequests`
from its default of 25, since that cap alone can force a stop mid-task even
when you intend to keep going.

## Keeping the docs in sync with the code

This repo is deliberate about doc ↔ code parity, so keep these pairs together
when you change behavior:

- The **README Roadmap** and `src/Kumunita.Web/Milestones.cs` — the home page
  renders `Milestones.cs`, and its doc-comment names the README as the source
  of truth. Bump a milestone to "done" in one when it ships and the other
  follows. **Tests pin the exact order + the single-in-progress milestone
  (`tests/Kumunita.Web.Tests/MilestonesTests.cs`)** — keep that file in step
  with any roadmap reorder.
- **`src/Kumunita.Web/WhatsNew.cs`** (the "What's new" registry, ADR 0110,
  pinned by `tests/Kumunita.Web.Tests/WhatsNewTests.cs`) — the platform's
  version history. When a milestone **or a named lane ships**, add its version
  (newest-first; or extend the newest row) here — this is a **required sixth
  member** of the close flip above. The milestone-flip contract
  (`Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs`) does **not** name it, which is why it is easy to miss
  (M27 shipped 2026-10-07 with no entry until caught in review — added as
  0.40.0). "Shipped" = the close unit ran, not "in progress."
- When a **new capability lands out of the M-letter order**, it gets a *named
  lane* with a short ID — **not** a renumber. Precedent: the media/avatars
  lane (ADR 0011, "M4-adjacent") and **group posts** (`GP`, ADR 0013, "no
  milestone letter; roadmap M4/M5/M6 stay Events/Projects/Portability"). The
  same rule now applies to **multilingual** (`ML`, ADR 0005, pulled forward to
  *next* in 2026-09-12 so the platform can be exercised in more than one
  language before the circle widens) — M4/M5/M6 are untouched.
- The **bounded-context / persistence layout** described in `docs/ARCHITECTURE.md`
  and ADR 0004/0006 — if you add a new context, doc-type surface, or change how
  contexts are registered in `DependencyInjection.cs`, reflect it in the relevant
  ADR rather than leaving the design doc stale.
- A **new capability that settles a design question** gets an ADR under `docs/adr/`
  (numbered after the current highest), not just a prose note in the README.
- The plans folder is **`docs/plans-milestones/`** (with an "s"), holding `in-progress/`
  and `done/<lane>/`. Older handoff notes sometimes say `docs/plan-milestones` —
  that's a stale path, treat it as the same folder.

## Razor verification doctrine

When a user reports a rendering bug, **the rendered HTML is the evidence, not
the source file** — and the user's report outranks your source reading.

- **Never conclude "stale build / stale process / stale deployment"** unless you
  have confirmed the served bytes differ from what the current source would
  produce. If you're about to tell the user "this is stale, refresh it", you
  own that claim and it has been wrong before.
- **Verify against the served page**, not the `.cshtml`: fetch the real HTML
  (`curl` with a session cookie, or the integrated browser) and
  inspect the actual markup. "The view file looks right" is not verification.
- Known Razor traps in this codebase (each of these has actually bit us):
  - **Un-awaited `async Task<string>` helpers** interpolated into a string render
    the type name (`System.Runtime.CompilerServices.AsyncTaskMethodBuilder...`).
    When a banner/label looks wrong, grep every call site of the async
    helper for a missing `await` (this hit `SeedLanguageName` in 9 places).
  - **Razor HTML-encodes expressions inside attribute values** — building a
    `style`/`data-*` attribute with `@(cond ? " style=\"display:none\"" : null)`
    produces broken HTML (`style= display:none`). Concatenate or use `Html.Raw`
    for such attributes; never interpolate quoted attribute strings via `@(...)`.
  - **A TagHelper tag inside a quoted attribute value renders as raw HTML text**
    — `<kw-l>` cannot live inside `title="..."`. Put the localized label in a
    proper inline element (`<span>`/`<button>`) instead.
  - **Boolean TagHelper flags bind differently as bare vs. quoted**
    (`<kw-dt TimeOnly>` vs `TimeOnly="true"`) in .NET 10's Razor source
    generator. When a flag "doesn't take effect", check the generated
    descriptor or the rendered output — don't trust the markup at face value.
- A URL or link that shows a **literal `@Model.X`** (404s, `/groups/@Model.GroupId/posts`)
is the same class of bug — an uninterpolated Razor attribute value, not a
  routing problem. Fix the markup; don't debug the route.

**Getting a live server.** `dotnet run` requires a `ConnectionStrings:Kumunita`
value the agent does not have; the docker-compose stack carries all credentials
and seeds sample data on first boot. To render a page end-to-end:

```bash
docker compose build app && docker compose up -d app   # rebuild + recreate (volumes persist)
```

The app needs ~8 s to finish booting (Marten schema + Wolverine leadership).
App URL: `http://localhost:5080`. Sample-data GlobalAdmin: `admin@examplium.com`
/ `Admin123!` (the `SampleDataSeeder` constants in `Kumunita.Core/Bootstrap/`).
Sign in in the integrated browser (or `curl` with a session cookie
+ anti-forgery token), then navigate to the target page. The browser snapshot
is the evidence.

## Running the tests (test-runner quirk)

Both test projects use **xunit.v3** (`Microsoft.Testing.Platform`), surfaced to VS
Test Explorer via `Microsoft.Testing.Extensions.VSTestBridge`. On this machine the
discovery path reliably goes wrong: VS Test Explorer (the `run_tests` tool) shows
"Discovered: N Tests found … No tests found to run", and `dotnet test` fails with
`Zero tests ran / Exit code: 5` — even though the same discovery run found all the
tests moments earlier. This is a runner/discovery bug, **not** a real test failure;
don't keep retrying those paths or "fix" the tests.

The reliable path is to build, then run each test assembly in-process through
xunit.v3's own runner (this is what actually reports pass/fail here):

```bash
dotnet build Kumunita.slnx -c Debug
dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll
```

`Kumunita.Web.Tests` runs in well under a second; `Kumunita.Core.Tests` takes ~20 s
because it starts `postgres:18` via Testcontainers (and leaves Docker containers
behind if the process is killed — clean up with `docker container prune`).

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

Secondary causes worth ruling out if a command still hangs: `git`/`gh` commands
that page output (`git log`, `git diff`, `gh pr view`) can block waiting for a
keypress — prefer `git --no-pager <cmd>`, or set `core.pager` to `cat` for
repos this applies to. Commands that are long-running by design
(`dotnet watch`, `npm start`, dev servers) should be started as background
tasks, not awaited as if they will exit on their own.
