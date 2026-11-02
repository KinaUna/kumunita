# AGENTS.md — Kumunita

Kumunita is a **self-hosted community platform for one neighborhood**. Each
deployment serves a single community (its own container + Postgres) — there is
no multi-tenant data model.

> **This file is loaded into every agent session, so its length is a
> per-session cost.** As of `D06` of the `IMPROVE-DOCS` lane it carries only
> the must-knows; the working doctrine (Razor, testing + CI, live-server
> debugging, sandbox quirks) now lives in [`docs/dev/`](docs/dev/README.md),
> and the shared doc rules in [`docs/DOCS-CONVENTIONS.md`](docs/DOCS-CONVENTIONS.md).

## Start here

- `README.md` — what the platform is, current status, principles, tech stack,
  and the Running instructions.
- `docs/ARCHITECTURE.md` — the detailed map: stack, data model, the bounded
  contexts, the module-boundary contracts, the CQRS-lite & Wolverine
  convention.
- `docs/adr/` — decision records. **ADR 0004** (persistence & schema
  evolution) and **ADR 0006** (module boundary contracts) constrain how data
  is stored and how contexts talk — read them before touching persistence,
  schema, or cross-context calls.
- `docs/design/` — the per-milestone design docs; the M1–M3 scope is the
  shipped surface.
- `docs/SECURITY.md` — the privacy model, threat model, and access-control
  rules. This repo is privacy-first; the author's audience choice is the
  authoritative constraint.
- `docs/DOCS-CONVENTIONS.md` — the shared doc rules (front-matter, abstract,
  ADR cap, linking) — read before your first doc change.
- `docs/dev/` — the working doctrine: [razor-verification](docs/dev/razor-verification.md),
  [testing-and-ci](docs/dev/testing-and-ci.md), [live-server-debugging](docs/dev/live-server-debugging.md), [agent-environment-quirks](docs/dev/agent-environment-quirks.md).

## The shape of the code

- **`Kumunita.Core`** — domain + services. Bounded contexts: `Identity/`,
  `UserInfo/`, `Authorization/`, `Posts/`, `Moderation/`, `Localization/`,
  `Bootstrap/` — `DependencyInjection.cs` registers each. The M3b
  "platform announcements" lane (`Announcements/`, in `PostDocTypes` alongside
  `CoreDocTypes`) is a feature module, not a top-level bounded context.
- **`Kumunita.Web`** — Razor Pages / MVC (server-rendered), `Program.cs`
  bootstrap, `Milestones.cs` (home-page roadmap — keep in sync with
  `README.md`), `Models/` + `Security/` (view-models and claim shaping, both
  server-rendered).
- **Persistence** (ADR 0004 §B): **Marten** owns the domain documents and
  versioned schema (the `CoreDocTypes` / `PostDocTypes` registration
  surfaces). **EF Core is used only for ASP.NET Identity tables** — do not
  put domain data in EF.

## The one thing to internalize before writing code here

This repo uses **Marten and Wolverine** on **.NET 10**. The idiomatic
patterns in training data are frequently **older major versions** of both.
Before writing Marten schema/migration code or Wolverine handler code, check
the actual APIs in the current dependency versions in the `.csproj` files —
do not copy a "common" pattern from memory. The ADRs above encode this
project's *specific* choices (CQRS-lite, documents + projections, no event
sourcing, a single durable email handler rather than a saga) — follow them.

## Conventions to preserve

- Server-rendered Razor Pages / MVC over Blazor or SPA patterns.
- Audit-by-default: access to audience-restricted content is always logged.
- Thin token, fat authorization: "may this actor see that resource?" is
  resolved by the `Authorization` service per request, not in an identity claim.
- Keep `Milestones.cs` and the README **Roadmap** section together —
  `Milestones.cs` has a comment that says this is the contract.

## Don't pause mid-task to check in

If you're partway through a multi-step task and a tool batch returns
successfully with no error, keep going — don't stop to announce the next step
and wait for a "continue." That pause-and-confirm habit (reported with some
models: github/orgs/community#184524) is not a safety feature; it just stalls
iterative work. Stop only when the task is done, or on a real blocker (an
error, a missing file, an ambiguous requirement, or something destructive
you genuinely need confirmation for) — say what happened and what you need.
**A failed tool call or terminal error is a recoverable step, not a stopping
point**: read the error, adjust, and continue. If the *same* command has
failed twice, change approach rather than retrying a third time.

**The flip side — long-running things in flight.** When a known-long test
suite, build, or `docker compose up` is running in the background
(~20 s / ~1 min / ~8 s), say so *proactively* in the same turn — name the
duration and that you'll report on completion.

## Keeping the docs in sync with the code

This repo is deliberate about doc ↔ code parity, so keep these pairs together
when you change behavior:

- The **README Roadmap** and `src/Kumunita.Web/Milestones.cs` — the home page
  renders `Milestones.cs` (its doc-comment names the README as source of
  truth). Bump a milestone to "done" in one when it ships and the other
  follows. **Tests pin the exact order + single in-progress milestone
  (`tests/Kumunita.Web.Tests/MilestonesTests.cs`)** — keep in step with a
  roadmap reorder.
- **`src/Kumunita.Web/WhatsNew.cs`** (the "What's new" registry, ADR 0110,
  pinned by `tests/Kumunita.Web.Tests/WhatsNewTests.cs`) — the platform's
  version history. When a milestone **or a named lane ships**, add its version
  (newest-first; or extend the newest row) — a **required sixth member** of
  the close flip above. "Shipped" = the close unit ran, not "in progress."
- When a **new capability lands out of the M-letter order**, it gets a *named
  lane* with a short ID — **not** a renumber. Precedent: media/avatars
  (ADR 0011), **group posts** (`GP`, ADR 0013), **multilingual**
  (`ML`, ADR 0005). M4/M5/M6 are untouched.
- The **bounded-context / persistence layout** in `docs/ARCHITECTURE.md` and
  ADR 0004/0006 — if you add a new context, doc-type surface, or change how
  contexts register in `DependencyInjection.cs`, reflect it in the relevant
  ADR rather than leaving the design doc stale.
- A **new capability that settles a design question** gets an ADR under
  `docs/adr/` (numbered after the current highest), not just a prose note in
  the README.
- Lane history lives in **`docs/archive/lanes/<lane>/`** (moved by `D02`;
  `docs/plans-milestones/` now holds `in-progress/` lanes only). The doc rules
  (front-matter, abstract, "one fact, one place") are in
  [`docs/DOCS-CONVENTIONS.md`](docs/DOCS-CONVENTIONS.md); the working doctrine
  this file used to restate is in [`docs/dev/`](docs/dev/README.md) — link,
  don't restate.
