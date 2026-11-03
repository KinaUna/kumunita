---
id: philosophy-in-code
title: "Integration in Code"
tier: living
kind: philosophy
status: current
audiences: [developer, agent]
related:
  - docs/philosophy/anti-patterns.md
  - docs/philosophy/START-HERE.md
  - docs/adr/0001-core-stack-and-identity-model.md
  - docs/adr/0003-roles-and-moderator-scoping.md
  - docs/dev/testing-and-ci.md
  - docs/dev/razor-verification.md
updated: 2026-11-02
---

> **Abstract:** the six principles turned into practice on this
> stack (a modular monolith: ASP.NET Core 10, Marten, Wolverine) —
> boundaries as contracts, seams as where we test, short feedback loops,
> coupling discipline, judging the whole, and the integration clauses of
> the Definition of Done. **Who is it for:** anyone who writes code here.
> **When to read the rest:** before a design or review, and before calling
> a change done.
>

# Integration in Code

How the six principles become practice on Kumunita's stack: a modular
monolith (ASP.NET Core 10, Marten, Wolverine, plain TypeScript, one Postgres
per neighborhood). The stack is a detail (ADR [0001](../adr/0001-core-stack-and-identity-model.md)); the seams it exposes are
not.

## Boundaries are contracts

- Every public surface — a command, an event, a handler, a projection, a
  module interface, a config key — is a linkage point between parts. We treat
  module interfaces as more precious than their implementation: they outlast
  it.
- **Public surface is permanent integration cost.** The three modules
  (Identity, UserInfo, Authorization) sit behind interfaces precisely so their
  contracts are small and stable. Every public thing we add is a cost paid by
  every other part, forever. Add with care.
- **The access model is the most load-bearing contract in the codebase.**
  "Can this person see that post?" is never a claim in the token — it is a
  query resolved per request (thin token, fat authorization service). That
  query is a seam everyone depends on; changes to audience semantics, group
  resolution, or delegation resolution are breaking changes and belong in an
  ADR.
- Record boundary decisions in [`../adr/`](../adr/). A seam chosen silently
  will be re-litigated by whoever hits it next.

## Seams are where we test

- Unit tests for parts (a command handler, a projection). Integration tests
  for seams (AuthorizationModule resolving an effective principal through a
  delegation grant). End-to-end for the critical path (a resident posts to an
  audience; a moderator files a report; access is audited). All three; each
  answers a different question.
- Test the access model at its seams specifically: the empty-audience-denies
  invariant under `All`, delegation scoped narrower than the grant, moderator
  access off-by-default, a group whose membership changed after a post was
  granted. These are where privacy actually lives or leaks.
- **Green CI with red production means we tested the parts, not the seams.**
  The seams that matter here are not network boundaries — they are *semantic*
  ones: does the effective principal compute correctly when a delegate acts?

## Shorten the feedback loops

- The goal is the shortest honest path from change to signal:
  `tsc` → build → unit → integration → deploy → the audit log.
- Each layer must answer a *different* question, or it's ceremony.
- **The audit log is the production feedback loop for the access model.** It
  is always on by design — that is not a feature, it is the loop that lets us
  know the authorization seam behaved. New access paths get audit coverage by
  default, not as a follow-up.
- Wolverine handlers are the side-effect seams (email, reminders,
  notifications). They get their own test harness, because a side effect that
  fails silently is a broken loop.

## Coupling discipline

- Minimize coupling, but preserve necessary coupling. We cut coupling to
  *maintain* a module boundary, not to create ceremony.
- Watch both failure modes — the code names of two general anti-patterns
  (see [`anti-patterns.md`](anti-patterns.md)):
  - **Under-differentiation: the god module** — one place everyone reaches
    into, nothing testable in isolation.
  - **Over-differentiation: the distributed monolith** — many parts, no
    integration; every change touches five of them.
- Kumunita is deliberately a **modular monolith**: few, stable module
  interfaces over one process. That is the coupling discipline applied — we
  pay for boundaries we keep, and we do not pay for a network we don't need.
- Prefer few stable interfaces over many unstable ones.

## The ratchet, the split, the comment

Three code-shape rules that the IMPROVE-CODE gate (`tools/code/code-check.ps1`)
enforces or the close applies. Each is stated here once; `AGENTS.md` links
here rather than restating it.

- **The ratchet (shrink-only).** The code-size baseline
  (`tools/code/code-baseline.json`) only ever goes **down**. A new item over
  its ceiling, a baselined item growing past its recorded max, a stale entry
  left behind, or a max raised above `origin/main` — each fails the close.
  Lowering an item re-tightens the baseline (`code-check.ps1 --tighten`).
  Raising a number is only ever legitimate with an `adr:` reason on that entry
  — the baseline is the point: a ceiling that can drift up protects nothing.
- **The part-vs-whole test (for every new type).** A new type must satisfy at
  least one of: (a) it has two or more callers, (b) it is a decision that gets
  its own unit test, (c) it owns a route family or an aggregate. A type that
  meets none of these is ceremony and is folded back. (The U04 per-surface
  views were the example: 12 properties, zero consumers outside tests.)
- **The comment rule (D4: contract + link, applied on touch).** A doc-comment
  states **what, when, and throws**, plus the ADR number. History stays in the
  ADR or the handoff note — not in a running change-log above the method. The
  comment ratio is *reported* by the gate, not gated; the rule is about
  *what* a comment says, not how many there are.

## Judge the whole

- Privacy, trust, reliability, and "does the neighborhood actually use it" are
  emergent. They only exist in the integrated system.
- Verify with the system: the full flow (post → audience → notification →
  audit), the access model end to end, a real neighborhood's worth of data.
  "My handler is correct" is not a system property.
- At a few dozen to a few hundred users, load is not the risk. The risk is a
  *wrong* access decision, a leaked private detail, or a community that stops
  trusting the place. Judge those.

## Definition of Done (integration clauses)

- Works in context, not in isolation — the access decision was checked against
  the real audience model, not a stub.
- Seams tested; failure paths exercised (denied, empty-audience, delegate,
  moderator).
- Observable: we can tell it worked, and the audit trail shows *who* and
  *why*.
- Contracts documented; dependents notified before an access-model change ships.
- No silent assumptions across module boundaries.
- The [three tests](START-HERE.md) are run and recorded: closed-loop?
  handoff? part-vs-whole? — and the [FACES check](START-HERE.md#the-five-faces-of-a-healthy-system--faces)
  names at least one face this design strengthens and one it consumes.
