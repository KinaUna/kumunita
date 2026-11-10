---
id: r21-prompt
title: "Prompt: IMPROVE-UX R21 — plain words for the resident-facing jargon"
tier: living
kind: plan
status: current
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/philosophy/in-code.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R21 — finding E17: 16 resident-facing strings used developer vocabulary
> (GlobalAdmin, standing, lane, intake, slug). Earlier units in this lane have
> since reworded 5 of them, so the live count is **11** — reword the rest into
> plain words a resident understands, in **all four languages**, with the
> `ux-measure.py` `jargon` count driven to **0**.
> **Who is it for:** the next coding agent (fresh context). **When to read the
> rest:** when starting R21 (a `D9` "smaller items" unit — no dependencies,
> self-contained, translation-only).

# Fresh-context prompt: IMPROVE-UX R21

You are continuing the Kumunita repository on `development` (HEAD
`8e13085a`, R20 landed — the guest notice now shows the window end + a way to
reach the admins). The next resident-facing unit is **R21**: plain words. The
plan card (in `plan-improve-ux.md`, the R21 row):

> R21 — Plain words for the 16 jargon strings (en/de/fr/da) — list from
> `ux-measure.py` `jargon`. Read first: the 4 `Localization/Keys/*` files
> involved. **Done when: `jargon` count 0 (or each survivor justified in
> baseline).**

This is a small **L**-severity "closer" unit (the `D9` smaller-items row): one
seam — the translation strings — the 11 keys sit in **7 surface files**
(`community`, `guardian`, `moderation`, `pages`, `posts`, `projects`,
`settings`), each in `en`/`de`/`fr`/`da` — up to ~28 small JSON edits, **no ADR,
no code logic**. It is an
honest-wording fix (the `E17` finding: "16 resident strings use developer
vocabulary (GlobalAdmin, standing, lane, intake, slug)"). The code is
**unchanged** — you are only editing the curated platform copy, and the
copy is the single source of truth (the `kw-l` TagHelper resolves it, ADR
0015 D1).

## What counts as "jargon" (read this before you write)

The `jargon` count in `tools/ux/ux-measure.py` is a **regex over the `en`
strings only** (it skips `admin.json`). The pattern is:

```
\b(?:GlobalAdmin|slug|intake action|standing moderator|standing over|account lane|deletion lane)\b
```

(case-insensitive, word-bounded). A string "counts" if its **`en` value**
contains any of those words/phrases. So the fix is: **reword the `en` value to
drop the term**, then keep `de`/`fr`/`da` in step (the four-language parity the
`kw-l` floor and the `C02`/`KnownTranslationKeys` suites pin). You do **not**
need to rename the key — only change the *value* a resident reads.

Two honest notes on the doctrine:

- **Keep the meaning.** Each of these strings explains something a resident
  must still understand (who can do what, what a report does, what a slug is).
  Plain words, not euphemisms that lose the point. The goal is a resident
  reading their own neighbourhood's UI, not the platform's plan vocabulary.
- **The check only scans `en`.** You *could* "pass" by only editing `en` —
  don't. A resident reading the `de`/`fr`/`da` UI would still see the jargon.
  Rewrite all four languages (de/fr/da machine-written, flagged for the parked
  native review in the UI-refresh lane).

## The 11 surviving strings (measured 2026-11-10, `python3 tools/ux/ux-measure.py`)

The plan said 16; earlier units in this lane have already reworded 5 of them,
so **11** remain (the baseline `ux-baseline.json` still says `jargon: 13` — a
re-base lag; the **live count is the source of truth**). Each row is
`surface.json / key → the offending term`. The **direction** column is guidance — the final
plain wording is yours, as long as it drops the term and keeps the meaning:

| Surface file | Key | Term(s) | Plain direction (your words) |
|---|---|---|---|
| `community.json` | `community.translations_view_only` | GlobalAdmin | "the administrator" / "an administrator" (a Translator stays "a Translator" if that's the resident-facing name, else "a translator") |
| `guardian.json` | `guardian.remove_myself_hint` | standing over | "your guardianship of their account" / "your oversight of the account" |
| `guardian.json` | `guardian.pendingRequests.lead` | standing over | "you hold no guardianship of the account yet" |
| `moderation.json` | `moderation.flash_not_moderator` | standing moderator | "an active moderator on this report's component" |
| `pages.json` | `pages.form.title_hint` | slug | "the short URL name (its address leaf) is derived from this" |
| `posts.json` | `posts.report_note` | intake action | "Filing a report is the first step — it doesn't change what you can see, and a moderator may follow up" |
| `projects.json` | `projects.todo.status_assignee_hint` | standing over | "…gives that resident ownership of it — they see it and can act, never an access limit" |
| `projects.json` | `projects.todo.mutation_hint` | GlobalAdmin | "…or the administrator" |
| `settings.json` | `settings.mfa.intro` | GlobalAdmin | "The administrator must have this on before signing in; for everyone else it is optional" |
| `settings.json` | `settings.mfa.disable_desc` | GlobalAdmin | "If you are the only administrator on this instance, doing so removes your last defence against a stolen password" |
| `settings.json` | `settings.mfa.disable_confirm` | GlobalAdmin | "…If you are the only administrator on this instance, you will lose your last defence against a stolen password" |

Re-run the measurement after each edit batch — the list above is the state at
the start of your session; if any of these were already reworded, the count is
lower. **Stop when `jargon` reports 0** (the DoD). If a single string genuinely
cannot be reworded without changing what it tells the resident, that is the
"justified survivor" path — but expect all 11 to be cleanly rewordable; the
terms above all have natural plain equivalents.

## The trap that will bite you — the "pass the check" illusion

The check reads **only `en`** and **skips `admin.json`**. Two ways to get a
false green:

- **Editing `en` but leaving `de`/`fr`/`da` jargoned** — the count passes, the
  resident still sees the jargon in their language. This fails the unit's intent
  and the four-language parity. Always edit all four files for every key you
  touch.
- **Breaking the word boundary instead of rewording** — e.g. "Global Admin"
  (with a space) or "sluggish" to dodge the `\b…\b` regex. That's gaming the
  ratchet, not fixing the wording. The ratchet (AGENTS.md / `docs/philosophy/
  in-code.md`) is a floor on honest code shape; a "fixed" string that is
  *worse* English to dodge a regex is exactly the drift it guards against.
  Reword properly.

Also: **do not rename the keys.** The `kw-l` keys are referenced by the views
and pinned by the `KwLRegistryConsistencyTests` / parity family. Change the
*value* only. A key rename is a different, larger change (R21 is the value fix).

## Before writing, read (in this order)

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** — the
  doctrine (the comment rule, the ratchet, the don't-pause-mid-task rule).
- **`tools/ux/ux-measure.py`** — `count_jargon()` (the `JARGON` regex + the
  "en only, skip admin.json" rule) and `measure()`/`report()` — so you know
  exactly what the ratchet counts and how `--check` fails.
- **`tools/ux/ux-baseline.json`** — the current `jargon` baseline (13) and the
  `generated_from` note (how a re-base / justified survivor is annotated).
- **`docs/philosophy/in-code.md`** — the ratchet doctrine (the floor goes down;
  you are *lowering* a tracked count, which is the honest direction).
- **`src/Kumunita.Core/Localization/Keys/en/{community,guardian,moderation,pages,posts,projects,settings}.json`** — the
  11 `en` strings (the values above). Skim the neighbours in each file so your
  rewrite reads like the surrounding copy, not a bolt-on.
- **`docs/dev/testing-and-ci.md`** — the test-runner quirk + the in-process
  `dotnet exec` invocation.
- **`git status`** (R20's commit must be intact; you are on `development`).

## The work

1. **Reword the 11 `en` strings** across the seven surface files
   (`community`, `guardian`, `moderation`, `pages`, `posts`, `projects`,
   `settings`), dropping every `JARGON` term while keeping the meaning and the
   register of the surrounding copy. Confirm with
   `python3 tools/ux/ux-measure.py` that `jargon` now reports **0**.
2. **Mirror each change into `de`/`fr`/`da`** in the same seven surface files
   (machine-written translations, flagged for the parked native review). The
   `C02_DataAsEmbeddedResourcesTests` + `KnownTranslationKeys_ParityTests` /
   `ClosureTests` family enforces the key-for-key four-language parity and
   non-empty values — run it.
3. **No survivors.** The DoD is `jargon` 0. If you genuinely hit one string
   that can't be reworded without losing its meaning, that is the documented
   escape hatch: keep the honest `en` wording and annotate the survivor in the
   `ux-baseline.json` `generated_from` note (bumping the `jargon` count to the
   survivor count) — but expect not to need it.

## Scope rules

- **Translation values only.** Do not rename keys, do not touch the views, do
  not touch code, do not touch `admin.json` (it is outside the `jargon` count
  and is admin-facing copy — the resident never reads it). The only files that
  should change are the seven `en` + seven `de`/`fr`/`da` surface files that
  carry the 11 keys.
- **One commit per change** (the house rule): the `en` reword + the `de`/`fr`/
  `da` mirrors land in **one** `feat(ux):` commit
  (`feat(ux): plain words for the resident-facing jargon strings (IMPROVE-UX R21)`).
- **The four-language parity + the `ux-measure.py --check` gate** must stay
  green (you are lowering a tracked count — `--check` compares against the
  baseline, and 0 ≤ 13, so it passes).

**Before committing:** run the narrowest checks (see `testing-and-ci.md`):
`python3 tools/ux/ux-measure.py` (report: `jargon` 0) and
`python3 tools/ux/ux-measure.py --check` (green); the Core translation-parity
family via the in-process runner (the reliable path — `dotnet test` discovery
is broken here):

```bash
dotnet build Kumunita.slnx -c Debug
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -class "Kumunita.Core.Tests.C02_DataAsEmbeddedResourcesTests" \
  -class "Kumunita.Core.Tests.KnownTranslationKeys_ParityTests" \
  -class "Kumunita.Core.Tests.KnownTranslationKeysClosureTests"
dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll \
  -class "Kumunita.Web.Tests.KwLRegistryConsistencyTests"
```

If a test fails, read the error, adjust, continue. Note: `Kumunita.Web.Tests`
is expected to fail **3** known-reds (the two `SecGate.DataClassCoverageTests`
M2 gates + `ImproveHarnessTests.CodeCheck_Gate_Passes`) — recorded drift
(SEC-lane M2 + the ratchet), **not** regressions; don't "fix" them (different
unit's scope).

Then report: the 11 (or fewer) keys you reworded, one before → after example
for each distinct term (GlobalAdmin / standing over / standing moderator /
intake action / slug), the `jargon` count before (11) and after (0), whether
you needed the "justified survivor" escape hatch (you shouldn't), the test
class names + pass counts, the commit hash, and which of the 3 known Web reds
(if any) your run reproduced. Do not commit unrelated work.
