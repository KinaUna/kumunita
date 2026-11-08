---
id: is37-prompt
title: "IS37 prompt — move the 8 inline confirm handlers to the existing
 data-confirm module (E9)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS37 — The confirm views move to the existing `data-confirm` module

## What you are doing

You are the **thirty-eighth working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS36 are committed.

**Finding E9 (severity M):** **8 inline `onsubmit="return confirm('…')"`
handlers in 4 views** are **silently blocked** by `script-src 'self'` — the
destructive "Delete note/folder?", "Delete bookmark folder?" and "Reset help
pages?" confirms **never run**, and the form submits immediately with no
confirmation. The existing module `client/lib/confirm.ts` (the
`form[data-confirm]` contract) **already exists** and is **already loaded
globally** in `Views/Shared/_LayoutScripts.cshtml` — no new TS, no new
script, no CSP change. The plan's **IS37** section
(`docs/plans-milestones/plan-improve-sec.md`, see `#### IS37`) is the spec:

> `Notes/Index` (2), `AdminHelp/Index` (2), `Bookmarks/Index` (1),
> `AdminSurfaceLabels/Index` (1) — each `onsubmit="return confirm('…')"`
> becomes `data-confirm="…"` using the existing module with **no new TS**.

## The one thing to get right

**Each of the 8 forms swaps the inline handler for the declarative
attribute** — a pure Razor change, no TS:

- **Before:** `<form method="post" action="…" onsubmit="return confirm('MSG')">`
- **After:**  `<form method="post" action="…" data-confirm="MSG">`

The `MSG` is the exact string between the single quotes of the `confirm(…)`
call — it is **server-rendered** and may contain Razor (`@p.Title`,
`@Model.Pages.Count`) and HTML entities (`&quot;`) — carry it over **verbatim**.
Keep the rest of the form tag (`method`, `action`, `style`, …) unchanged.

The four views and their handler counts (verify with `grep -n` before you
edit — line numbers drift):

| View | handlers | notes |
|---|---|---|
| `src/Kumunita.Web/Views/Notes/Index.cshtml` | 2 | ≈ :157 (folder delete), ≈ :253 (note delete) |
| `src/Kumunita.Web/Views/Bookmarks/Index.cshtml` | 1 | ≈ :145 (folder delete); **≈ :198 is a different pattern — verify it is not an `onsubmit` handler before touching** |
| `src/Kumunita.Web/Views/AdminSurfaceLabels/Index.cshtml` | 1 | ≈ :168 (translation remove) |
| `src/Kumunita.Web/Views/AdminHelp/Index.cshtml` | 2 | ≈ :63 (reset one page), ≈ :88 (reset all) |

The `AdminHelp/Index` :63 handler carries `&quot;@p.Title&quot;` inside the
message — the `data-confirm="Reset &quot;@p.Title&quot; to its seeded text?
This overwrites any hand-edited copy."` attribute keeps the entities
intact; the attribute is delimited by **double** quotes, so no escaping of
the double quotes is needed.

## Constraints

- **No new TypeScript.** No new `client/lib/*` file, no new script tag, no
  `_LayoutScripts.cshtml` change, no CSP change. `confirm.ts` and its global
  include stay untouched.
- **No new test in IS37.** The `NoView_HasAnInlineEventHandler` gate test
  (the "8 → 0" pin) is **IS38's** (it lands in
  `tests/Kumunita.Web.Tests/SecGate/CspInlineDisciplineGateTests.cs`,
  possibly as IS38b). IS37's exit is build-green + the raw grep is clean.
- **Do not touch the 2 raw hits in `_LayoutScripts.cshtml`** — they are
  HTML *comments*, not live handlers (the E9 finding says so; verify).
- **Do not touch `AdminAnalytics/Index.cshtml` or `Admin/Escalation/
  Tokens.cshtml`** — their handlers are **IS38's** (autosubmit + copy,
  different contracts, different modules).
- The message text stays server-rendered (localizable, view-owned) — the
  module only reads the attribute and calls `window.confirm` (the
  view↔JS seam). Do not move the text into JS.

## Deliverables (≤ 4 files — exactly the 4 views)

1. `src/Kumunita.Web/Views/Notes/Index.cshtml` — 2 handlers.
2. `src/Kumunita.Web/Views/Bookmarks/Index.cshtml` — 1 handler.
3. `src/Kumunita.Web/Views/AdminSurfaceLabels/Index.cshtml` — 1 handler.
4. `src/Kumunita.Web/Views/AdminHelp/Index.cshtml` — 2 handlers.

## Entry reads (only these — no whole-file reads)

- `src/Kumunita.Web/client/lib/confirm.ts` (whole — the existing
  `form[data-confirm]` contract: reads the attribute on `submit`, calls
  `window.confirm`, `preventDefault` + `stopPropagation` on cancel).
- `grep -rnE "\son[a-z]+=\"" src/Kumunita.Web/Views` — the raw hits; count
  and confirm which are live handlers vs HTML comments (the 2 in
  `_LayoutScripts` are comments).
- For each of the 4 views: `grep -n "onsubmit" <view>` then `sed -n 'A-2,B+2p'`
  around each hit (the exact form tag, so the swap is anchored).
- The D8 per-unit contract: `docs/plans-milestones/plan-improve-sec.md`
  (the **IS37** section + the **D8** row) + `docs/DOCS-CONVENTIONS.md`.
- Test-runner form (for the IS38 handoff, not this unit):
  `docs/archive/lanes/sec/sec-handoff-notes.md` (SEC U3 — xunit.v3
  in-process `-class`, **not** `dotnet test --filter`).

## Verification

- [ ] Build green (`dotnet build`).
- [ ] `grep -rnE "\son[a-z]+=\"" src/Kumunita.Web/Views` returns **0 live
      handler hits** (the 2 `_LayoutScripts` comment hits, if still present,
      are comments — verify they contain `<` / `-->` and are not attributes).
- [ ] Each of the 8 forms still carries `method="post"` + its original
      `action` + any `style` — only the `onsubmit` attribute was replaced.
- [ ] The `data-confirm` message text is byte-identical to the old
      `confirm('…')` payload (Razor + entities included).
- [ ] `client/lib/confirm.ts`, `_LayoutScripts.cshtml`, `tsconfig.json`,
      `csp` config, and every non-listed view are **untouched**.
- [ ] One `## IS37` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
      (the 4 views + handler counts, the exact `data-confirm` strings,
      the "no new TS" decision, the "IS38 owns the gate" handoff, anything
      noticed).

## Definition of done (IS37)

- [ ] The 8 inline `onsubmit="return confirm('…')"` handlers in the 4 listed
      views are gone; each form carries `data-confirm="…"` with the same
      message text.
- [ ] No new TS, no new script, no `_LayoutScripts` change, no CSP change,
      no `confirm.ts` edit.
- [ ] The 2 `_LayoutScripts` comment hits are left alone; the
      `AdminAnalytics/Index` + `Admin/Escalation/Tokens` handlers are
      untouched (IS38's).
- [ ] Build green; the raw `on*` grep across `src/Kumunita.Web/Views` is
      clean of live handlers.
- [ ] One commit (the 4 views + the handoff section); the prompt file is
      **not** part of the unit commit (the lane convention: prompts are
      committed separately, the unit commit carries the deliverables + the
      handoff section — the IS36 precedent, `git show 4cb5ebc5 --stat`).
- [ ] **Next unit: IS38** (autosubmit + copy in the two remaining views +
      the CSP-discipline gate — `client/lib/autosubmit.ts` +
      `client/lib/copy-text.ts` or one `client/lib/inline-free.ts`, the
      `_LayoutScripts.cshtml` include line, `AdminAnalytics/Index` +
      `Admin/Escalation/Tokens` one handler each, and the gate test in
      `tests/Kumunita.Web.Tests/SecGate/CspInlineDisciplineGateTests.cs` —
      `NoView_HasAnInlineEventHandler` (8 → 0) +
      `NoView_HasAnInlineScriptBlock`, ignoring Razor and HTML comments;
      build + `tsc` green).
