---
id: is33-prompt
title: "IS33 prompt — RetentionOptions + RetentionPolicy, pure (E5, D6)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS33 — `RetentionOptions` + `RetentionPolicy` (pure — D6 retention defaults)

## What you are doing

You are the **thirty-third working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS32 are committed
(IS31+IS32 landed as a **single combined E6 commit** — see the handoff note's
"Commit note").

**Finding E5 (severity M), the foundation:** there is **no retention** for
`SessionDoc`, `OutboxEmail` (full bodies, including verify links and 200-char
UGC/DM snippets), `Notification`, `ErrorReport`, `EmailDeadLetter`, or
consumed/expired `IdentityToken`/`EscalationToken`, and file-log retention
runs **only at boot**. Owner decision **D6** picks the "Moderate" defaults and
says *"each is a config key; one `RetentionTick` runs them all"*. This unit
lays the **pure** foundation only: `RetentionOptions` (the D6 keys + defaults)
and `RetentionPolicy` (the cutoff decision, pure). The spec is the plan's
**IS33** section (`docs/plans-milestones/plan-improve-sec.md`).

This is the **pure policy** unit. The I/O leg — the `RetentionTick` record,
the `RetentionService`, the Wolverine handler, and the `Program.cs` seed — is
**IS34a / IS34b**; part 2 (notifications, error reports, log files) is **IS35**.
Do **not** build the tick, the service, the handler, or the DI registration
here, and do **not** touch `Program.cs`.

## The one thing to get right

**Pure — no store, no I/O, no Postgres, no harness.** `RetentionOptions` and
`RetentionPolicy` must be targetable by a **plain unit test** with no
`PostgresFixture`, no `DocumentStore`, no `*DocTypes.Configure` — exactly the
`UsagePurgePolicy` shape (a `static` over `now` + the retention constant that
a unit test names without a store). This is the C04 / C2-06 decision-extraction
precedent: the *decision* lives pure; the *I/O* (query/delete/commit) lands
later, in IS34, and must never re-derive the cutoff (the single source of the
rule is here).

**`0` means KEEP — the fail-safe default.** A retention key set to `0` (or
unset/absent) means *no retention for that type*: `CutoffFor` must **not**
yield a cutoff that would delete rows. Mirror the `MediaOptions.MaxPlatformBytes
= 0` "0 = unlimited/keep" sentinel. A type whose key is `0` is never purged —
that is the safe side.

**`RetentionOptions` joins the existing `Privacy` module.** It goes in
`src/Kumunita.Core/Privacy/` (the module that already holds
`DataClassAttribute.cs`). It is a config-bound POCO mirroring `MediaOptions`:
`SectionName = "Retention"`, keys bound as `Retention__*`. The **D6** defaults
(§ "What you are doing") are the numbers — pin them in the test, don't
re-derive them.

**The `CutoffFor` seam is key-driven, not type-driven.** The plan names it
`CutoffFor(string key, DateTimeOffset now)` where `0` means keep. Since
`RetentionOptions` is what holds the days-per-key, the pure seam needs that
source in scope — mirror `UsagePurgePolicy`, where the caller supplies the
constant (e.g. `CutoffFor(RetentionOptions opts, string key, DateTimeOffset
now)`, or a `IReadOnlyDictionary<string,int>`). Record the exact chosen
signature in the handoff; it must compile and be nameable by a plain test.

## Constraint that keeps existing tests green

- **Pure, so no harness blast radius.** No `PostgresFixture`, no store, no
  `*DocTypes`. `dotnet build` must stay green; the only new test class is
  `RetentionPolicyTests`.
- **`UsagePurgePolicy` is the precedent, not a dependency** — do not couple the
  two; read it for the *shape* (pure `static`, caller-supplied constant, a
  plain test naming the boundary). Do not change it.
- **No DI registration, no tick, no handler, no `Program.cs`.** DI / the
  `RetentionTick` record / the `RetentionService` / the Wolverine handler are
  IS34a's (its "a 4th file" note owns the DI line). Adding any of that here is
  out of scope.
- **`PortabilityDocTypes` (M11) is untouched** (C-M11·5 closed-set pin) — this
  unit adds no doc type and no doc-type surface.
- **PII rule:** the tests use synthetic key names and `now − N` day offsets;
  no real PII, no `@x.com` addresses, no bodies.

## Deliverables (≤ 3 files)

1. **`src/Kumunita.Core/Privacy/RetentionOptions.cs`** (new) — a config-bound
   POCO in the `Kumunita.Core.Privacy` namespace: `SectionName = "Retention"`,
   and one `int` (days) property per D6 key, each defaulting to the D6 number
   (key names are the `Retention__*` surface — e.g.
   `Retention__Sessions` / `Retention__OutboxBodyNullDays` /
   `Retention__OutboxDays` / `Retention__DeadLetters` /
   `Retention__NotificationsRead` / `Retention__NotificationsUnread` /
   `Retention__ErrorReports` / `Retention__Tokens`). The D6 values, verbatim:

   | D6 line | key | default (days) |
   |---|---|---|
   | Sessions: 30 days after revoke or expiry | `Sessions` | **30** |
   | Outbox: body nulled 7 days after delivery | `OutboxBodyNullDays` | **7** |
   | Outbox: row kept 90 days | `OutboxDays` | **90** |
   | Dead letters: 30 days | `DeadLetters` | **30** |
   | Notifications: read 90 days | `NotificationsRead` | **90** |
   | Notifications: unread 365 | `NotificationsUnread` | **365** |
   | Resolved error reports: 180 days | `ErrorReports` | **180** |
   | Consumed or expired tokens: 7 days | `Tokens` | **7** |

2. **`src/Kumunita.Core/Privacy/RetentionPolicy.cs`** (new) — a pure `static`
   class: `CutoffFor(...)` (key → cutoff instant) and a `Keep(key)` /
   "is-keep" predicate. `0` (or an absent key) → **keep** (no cutoff). A
   positive key → `now − key` days. No store, no I/O, no `Marten`, no
   `Wolverine`.

3. **`tests/Kumunita.Core.Tests/RetentionPolicyTests.cs`** (new) — the three
   tests below, a **plain** xunit class (no `PostgresFixture`).

## Entry reads (only these — no whole-file reads)

- `sed -n '1,80p' src/Kumunita.Core/Usage/UsagePurgePolicy.cs` (the
  **pure-policy precedent** — `ComputeCutoff(now, retentionDays)` +
  `ShouldPurge(rowAt, cutoff)`; the caller-supplies-the-constant shape + the
  "they must never drift" comment style).
- `cat src/Kumunita.Core/Media/MediaLifecycleTick.cs` (a ~15-line
  `Wolverine.TimeoutMessage` record — **shape awareness only**; the tick
  itself is IS34a's, do not write it).
- `sed -n '1,30p' src/Kumunita.Core/Media/MediaOptions.cs` (the `SectionName`
  + `__*` config-key + `0`-sentinel pattern to mirror).
- `sed -n '1,20p' src/Kumunita.Core/Privacy/DataClassAttribute.cs` (the
  `Privacy` module + namespace this joins; confirm
  `RetentionOptions`/`RetentionPolicy` do **not** already exist — `grep -rn
  "RetentionOptions\|RetentionPolicy" src/ tests/` → expect zero hits).
- Doc rules + the per-unit contract (D8): `docs/plans-milestones/
  plan-improve-sec.md` (the **IS33** section + the **D6** decision row) +
  `docs/DOCS-CONVENTIONS.md`.
- Test-runner form: `docs/archive/lanes/sec/sec-handoff-notes.md` (SEC U3 —
  xunit.v3 in-process `-class`, **not** `dotnet test --filter`).

## Tests (3 new, in `RetentionPolicyTests`)

- `Defaults_MatchD6` — a default-constructed `RetentionOptions` carries the D6
  numbers (sessions 30, outbox body-null 7 / row 90, dead letters 30,
  notifications read 90 / unread 365, error reports 180, tokens 7).
- `ZeroMeansKeep` — a key set to `0` (and/or an absent key) → `CutoffFor`
  yields **no cutoff** / the keep predicate is true (the row is kept; the
  safe side).
- `Cutoff_IsNowMinusDays` — for a positive key, `CutoffFor(key, now)` ==
  `now − <days>` (deterministic — inject `now`).

## Verification

- [ ] Build green (`dotnet build`).
- [ ] `RetentionPolicyTests` green (3/3) via the xunit.v3 runner form.
- [ ] `UsagePurgePolicy`'s existing test class (if any) **still** green
      (you read it, you did not change it).
- [ ] `grep -rn "RetentionOptions\|RetentionPolicy" src/ tests/` — these are
      **new**; confirm they are not duplicating an existing seam.
- [ ] **No** `PostgresFixture` / `DocumentStore` / `*DocTypes` in the new test
      (pure). **No** `Program.cs` touch. **No** DI registration.
- [ ] `PortabilityDocTypes` (M11) is **untouched** (C-M11·5 closed-set pin).
- [ ] No PII in code, tests or notes.
- [ ] One `## IS33` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
      (files, the `CutoffFor` signature as chosen, the `RetentionOptions` keys
      + D6 defaults, the `0`=keep rule, tests + pass count, next-unit notes).

## Definition of done (IS33)

- [ ] `RetentionOptions` (D6 keys + defaults) and `RetentionPolicy`
      (`CutoffFor`, `0` = keep) are **pure** `Kumunita.Core.Privacy` seams —
      targetable by a plain test, no store, no I/O.
- [ ] A `0`-valued / absent key keeps its rows (fail-safe); a positive key
      gives `now − days`.
- [ ] Defaults match D6 exactly (pinned by `Defaults_MatchD6`).
- [ ] No tick, no service, no handler, no DI, no `Program.cs`;
      `PortabilityDocTypes` unchanged; build green; no PII.
- [ ] One commit, one handoff section.
- [ ] **Next unit: IS34a** (`RetentionTick` record + the Core `RetentionService`,
      part 1: sessions, tokens, outbox, dead letters — the I/O leg that
      consumes this unit's `RetentionOptions` + `RetentionPolicy`).
