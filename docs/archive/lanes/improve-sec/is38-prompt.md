---
id: is38-prompt
title: "IS38 prompt — autosubmit + copy modules, and the CSP-discipline gate (E9)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS38 — Autosubmit + copy, and the CSP-discipline gate

## What you are doing

You are the **thirty-ninth working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS37 are committed
(IS37 = `b64c2d73`).

**Finding E9 (severity M):** **8 inline `on*` handlers in 6 views** are
**silently blocked** by `script-src 'self'` (CSP line 1083, no
`'unsafe-inline'`). IS37 moved the **6** `onsubmit="return confirm('…')"`
handlers to `data-confirm`. **2 remain**, in different views with different
contracts:

| View | handler | contract |
|---|---|---|
| `src/Kumunita.Web/Views/AdminAnalytics/Index.cshtml` :53 | `onchange="this.form.submit()"` on a `<select>` | **autosubmit**: on change, submit the enclosing form |
| `src/Kumunita.Web/Views/Admin/Escalation/Tokens.cshtml` :100 | `onclick="navigator.clipboard.writeText(document.getElementById('new-token').textContent)"` on a `<button>` | **copy**: on click, copy the `textContent` of the element with `id="new-token"` to the clipboard |

The plan's **IS38** section (`docs/plans-milestones/plan-improve-sec.md`,
see `#### IS38`) is the spec. **No new script tags** beyond the two module
includes; **no CSP change**; **no `confirm.ts` edit**.

## The one thing to get right

**Two new tsc-built ES modules** (the `client/lib/*.ts` → `wwwroot/js/lib/*.js`
pattern, `rootDir: client`, `outDir: wwwroot/js`, `strict: true`,
`module: ES2022`, `lib: [ES2022, DOM, DOM.Iterable]`), **two view swaps**,
**two include lines**, **one gate test**. The modules follow the
`reset-form.ts` precedent (IIFE, `'use strict'`, `querySelectorAll`, no
framework). The view swaps are pure Razor: the inline `on*` handler becomes
a declarative `data-*` attribute. The gate pins both invariants (P-8:
no inline handlers **or** inline executable scripts in views) so the next
drift is a red build.

## The two contracts (exact)

### `client/lib/inline-free.ts` (ONE file — D8: ≤ 4 deliverable files)

The plan lists `autosubmit.ts` and `copy-text.ts` separately but says
*"Or put both in one `client/lib/inline-free.ts`, to stay within 4 files."*
**Use one file.** Two IIFEs, each scoped to its own `data-*` attribute:

```ts
// ── autosubmit ──────────────────────────────────────────────────────────
// [data-autosubmit] on a form control: on `change`, call
// form.requestSubmit() on the nearest <form>. Replaces the
// AdminAnalytics/Index.cshtml onchange="this.form.submit()" handler.
(() => {
  'use strict';
  for (const el of document.querySelectorAll<HTMLSelectElement>('[data-autosubmit]')) {
    el.addEventListener('change', () => {
      const form = el.closest('form');
      if (form) form.requestSubmit();
    });
  }
})();

// ── copy-text ───────────────────────────────────────────────────────────
// [data-copy-target="el-id"] on a button: on click, copy the
// textContent of the element with that id to the clipboard. Replaces the
// Admin/Escalation/Tokens.cshtml onclick="navigator.clipboard.writeText(...)".
// `navigator.clipboard.writeText` is the same API the inline handler used;
// this is a pure relocation, not a behavior change.
(() => {
  'use strict';
  for (const btn of document.querySelectorAll<HTMLButtonElement>('[data-copy-target]')) {
    btn.addEventListener('click', () => {
      const id = btn.getAttribute('data-copy-target');
      if (!id) return;
      const target = document.getElementById(id);
      if (target) navigator.clipboard.writeText(target.textContent);
    });
  }
})();
```

> **Why `requestSubmit()` and not `form.submit()`:** `requestSubmit()`
> triggers the `submit` event (which any `data-confirm` module on the same
> form could intercept); `form.submit()` bypasses it. The AdminAnalytics
> form has no `data-confirm`, so either works, but `requestSubmit()` is the
> correct form-submission API and keeps the door open if a confirm is added
> later. The inline handler used `form.submit()` — this is a deliberate
> improvement, not a behavior change (the form is `method="get"`, no
> validation, no confirm).

### The view swaps

**AdminAnalytics/Index.cshtml :53** (the `<select>`):

```diff
-        <select id="window" name="window" class="form-select form-select-sm" onchange="this.form.submit()">
+        <select id="window" name="window" class="form-select form-select-sm" data-autosubmit>
```

**Admin/Escalation/Tokens.cshtml :100** (the `<button>`):

```diff
-                    onclick="navigator.clipboard.writeText(document.getElementById('new-token').textContent)">
+                    data-copy-target="new-token">
```

Keep `type="button"`, `class`, and everything else unchanged.

## The gate test (IS38b — the 5th file)

`tests/Kumunita.Web.Tests/SecGate/CspInlineDisciplineGateTests.cs`

Reuses `SecGateSource` (`AllSrcFiles("*.cshtml")`, `ReadSrc`). The gate
walks every `.cshtml` file under `src/Kumunita.Web/Views/`. **Two tests,
both present in the file:** `NoView_HasAnInlineEventHandler` (green — the
IS38 deliverable) and `NoView_HasAnInlineScriptBlock` (written but
**`[Skipped(…E14…)]`** — see the DRIFT block below; it lands green in
IS38c after the 2 Document inline blocks are extracted to a tsc module,
keeping the committed suite green per DoD-13). It:

**`NoView_HasAnInlineEventHandler`:**
- Pattern: `\son[a-z]+="` (case-sensitive, word-boundary before `on`).
- **Exclude:** hits inside Razor comments (`@* … *@`) and HTML comments
  (`<!-- … -->`). The 2 hits in `_LayoutScripts.cshtml` (lines 19, 22) are
  HTML comments — verify they contain `<!--` and `-->` and are not live
  attributes. The `_Layout.cshtml` and other comment-only hits are the same.
- **Baseline: 8 → 0.** After IS37 (6 confirm) + IS38 (2 autosubmit/copy),
  the count is 0. The test asserts **zero** live handler hits.

**`NoView_HasAnInlineScriptBlock`:**
- Pattern: `<script` tags whose content is **executable JavaScript** —
  i.e. `<script>` or `<script type="module">` or `<script type="text/javascript">`
  **with a non-empty body** (the tag is not self-closing and has content
  before `</script>`).
- **Exclude:** `<script type="application/json">` and
  `<script type="application/ld+json">` data blocks (they are not
  executable; the existing 2 — `_LayoutScripts:143`
  `kumunita-strings` and `Boards/BoardDetail:526`
  `card-edit-prefills` — are legitimate data islands).
- **Exclude:** Razor comments and HTML comments (same rule as the handler
  gate).
- **Baseline: 2 → 0** — the 2 Document blocks (New :246, Edit :245). The
  plan's E9 measured handlers only, so this is **not** in the E9 baseline;
  it is a new finding (E14). **This test is `[Skipped]` in IS38** so the
  suite stays green (DoD-13); it lands green in IS38c after the 2 blocks
  are extracted to a tsc module. The 2 `application/json` data blocks are
  excluded by the `type="application/json"` rule and are never in scope.

> **⚠ DRIFT YOU WILL ENCOUNTER — READ BEFORE IMPLEMENTING THE GATE:**
>
> There are **2 pre-existing executable inline script blocks** that the
> plan's E9 baseline did **not** measure:
>
> - `src/Kumunita.Web/Views/Document/New.cshtml` **:246** —
>   `<script type="module">` with a `fetch('/documents/folders')` call
>   that populates the folder `<select>`. **Silently blocked by CSP.**
> - `src/Kumunita.Web/Views/Document/Edit.cshtml` **:245** — same pattern.
>   **Silently blocked by CSP.**
>
> These are **real latent bugs** (the folder dropdown in both views
> does not populate — the `fetch` never runs). They are **out of IS38's
> scope** (fixing them requires a new tsc module + 2 view edits = 3 more
> files, blowing the D8 ≤ 4 limit).
>
> **What to do (decisive — do not commit a red test):**
>
> The lane's own DoD (point 13) requires *"Full suites show no failure
> outside the IS04 baseline."* A brand-new red gate test is **not** in the
> IS04 baseline, so **it cannot be the committed end state.** Ship only a
> green gate.
>
> 1. **Ship the handler gate green** — `NoView_HasAnInlineEventHandler`
>    (8 → 0). IS37 + IS38 fixed all 8 handlers; this passes. **This is
>    IS38's deliverable gate.**
> 2. **Do NOT ship the `NoView_HasAnInlineScriptBlock` gate yet** — it would
>    be red on the 2 Document blocks, which is a DoD-13 violation. Instead,
>    record it in the handoff as a **NEW FINDING (call it `E14`, or
>    `E9b`)**: *"The 2 pre-existing inline `<script type="module">` blocks
>    (Document/New :246, Document/Edit :245) fetch `/documents/folders` to
>    populate the folder `<select>`. Both are silently blocked by
>    `script-src 'self'` (CSP line 1083) — the dropdown does not populate.
>    Real latent bug (the folder picker is broken in both views). Not in
>    the E9 baseline. A new unit (IS38c) extracts the shared logic to a tsc
>    module and replaces the 2 inline blocks with a `data-*` attribute,
>    THEN lands the `NoView_HasAnInlineScriptBlock` gate green. The 2
>    `application/json` data blocks (`_LayoutScripts:143`,
>    `Boards/BoardDetail:526`) are legitimate data islands and are NOT in
>    scope for that gate (exclude `type="application/json"`)."*
> 3. **Do NOT fix the 2 Document blocks in IS38** (out of scope, D8 file
>    limit) and **do NOT allowlist them** (the gate allowlist stays empty —
>    the IS08 doctrine; the fix is a real module extraction, not an
>    exception).
> 4. **The `NoView_HasAnInlineScriptBlock` test is written but marked
>    `[Skipped("IS38c: lands green after the 2 Document inline blocks are
>    extracted to a tsc module — see E14 in the handoff")]`** so it is
>    present in the codebase (a named pin) but does not fail the suite.
>    This satisfies "the gate exists and names the seam" without shipping
>    a red build. **Verify the skip is the only way to keep the suite
>    green** — if the plan's reviewer objects, the alternative is to defer
>    the whole script-block test to IS38c and ship IS38 with only the
>    handler gate. Record whichever you choose in the handoff.

## Constraints

- **No new TS beyond `inline-free.ts`.** No `autosubmit.ts` + `copy-text.ts`
  split (that's 5 files, over D8). One file, two IIFEs.
- **No new script tags** beyond the one `<script type="module"
  src="~/js/lib/inline-free.js"></script>` include line in
  `_LayoutScripts.cshtml`.
- **No CSP change.** `script-src 'self'` stays.
- **No `confirm.ts` edit.** IS37's territory, done.
- **No `_Layout.cshtml` change.** The include goes in `_LayoutScripts.cshtml`
  (the C11-extracted partial that holds all global `<script>` includes).
- **Do not touch `Document/New.cshtml` or `Document/Edit.cshtml`.** Their
  inline script blocks are a **new finding** (record in handoff, do not fix).
- **Do not touch the 2 `application/json` data blocks** (`_LayoutScripts:143`,
  `Boards/BoardDetail:526`). They are legitimate data islands, not
  executable scripts. The gate excludes `type="application/json"`.
- **The gate allowlist is empty and stays empty** (the IS08 doctrine). A
  file that genuinely needs an exception is a *pause and record*, never a
  silent allow.

## Deliverables (5 files = IS38 4 + IS38b 1)

| # | File | Change |
|---|---|---|
| 1 | `src/Kumunita.Web/client/lib/inline-free.ts` | **new** — 2 IIFEs (autosubmit + copy) |
| 2 | `src/Kumunita.Web/Views/Shared/_LayoutScripts.cshtml` | +1 `<script>` include line (after the `reset-form.js` include, ≈ :23) |
| 3 | `src/Kumunita.Web/Views/AdminAnalytics/Index.cshtml` | `onchange="this.form.submit()"` → `data-autosubmit` (1 handler) |
| 4 | `src/Kumunita.Web/Views/Admin/Escalation/Tokens.cshtml` | `onclick="navigator.clipboard.writeText(...)"` → `data-copy-target="new-token"` (1 handler) |
| 5 | `tests/Kumunita.Web.Tests/SecGate/CspInlineDisciplineGateTests.cs` | **new** (IS38b) — `NoView_HasAnInlineEventHandler` (8→0, green) + `NoView_HasAnInlineScriptBlock` (present but **skipped** — E14, lands green in IS38c after the 2 Document blocks are extracted) |

## Entry reads (only these — no whole-file reads)

- `src/Kumunita.Web/client/lib/reset-form.ts` (whole — the IIFE +
  `querySelectorAll` + `closest('form')` precedent).
- `src/Kumunita.Web/client/lib/confirm.ts` (whole — the `form[data-confirm]`
  precedent; confirms the view↔JS seam pattern).
- `src/Kumunita.Web/Views/AdminAnalytics/Index.cshtml` lines 48–62 (the
  `<form method="get">` + `<select>` block).
- `src/Kumunita.Web/Views/Admin/Escalation/Tokens.cshtml` lines 92–108 (the
  `<code id="new-token">` + `<button onclick=…>` block).
- `src/Kumunita.Web/Views/Shared/_LayoutScripts.cshtml` lines 18–26 (the
  existing `<script type="module">` includes, for the new include's
  position + style).
- `src/Kumunita.Web/tsconfig.json` (whole — `rootDir`, `outDir`, `strict`,
  `lib`, `module`).
- `tests/Kumunita.Web.Tests/SecGate/SecGateSource.cs` (whole — `RepoRoot`,
  `AllSrcFiles`, `ReadSrc` API).
- `tests/Kumunita.Web.Tests/SecGate/LogTemplatePiiGateTests.cs` lines 1–90
  (the gate-test style: `SecGateSource` usage, `Allowlist` field,
  `[Fact(DisplayName=…)]`, the scan loop).
- `src/Kumunita.Web/Views/Document/New.cshtml` lines 244–262 and
  `Document/Edit.cshtml` lines 243–261 (the **drift** inline script blocks
  — read to confirm they are executable and not data blocks; do **not**
  edit).
- The D8 per-unit contract: `docs/plans-milestones/plan-improve-sec.md`
  (the **IS38** section + the **D8** row).
- Test-runner form (for the gate test, not this unit's exit):
  `docs/archive/lanes/sec/sec-handoff-notes.md` (SEC U3 — xunit.v3
  in-process `-class`, **not** `dotnet test --filter`).

## Verification

- [ ] Build green (`dotnet build`).
- [ ] `tsc` green (`cd src/Kumunita.Web && npm run build`).
- [ ] `grep -rnE "\son[a-z]+=\"" src/Kumunita.Web/Views` → **0 live handler
      hits** (the `_LayoutScripts` comment hits are comments — verified).
- [ ] `grep -rn "<script type=\"module\">" src/Kumunita.Web/Views` → the
      2 Document/New + Document/Edit hits (the **drift** — recorded, not
      fixed).
- [ ] `wwwroot/js/lib/inline-free.js` exists (tsc emitted it).
- [ ] `_LayoutScripts.cshtml` carries the new
      `<script type="module" src="~/js/lib/inline-free.js"></script>` line.
- [ ] `client/lib/confirm.ts`, `client/lib/reset-form.ts`, `tsconfig.json`,
      `csp` config, `Document/New.cshtml`, `Document/Edit.cshtml`, and every
      non-listed view are **untouched**.
- [ ] `NoView_HasAnInlineEventHandler` gate **green** (8 → 0).
- [ ] `NoView_HasAnInlineScriptBlock` gate present but **skipped**
      (`[Skipped(…E14…)]`) so the suite stays green (the 2 Document blocks
      are the E14 new finding — recorded in handoff, fixed in IS38c, NOT
      allowlisted).
- [ ] Full test suite **green** (DoD-13: no failure outside the IS04
      baseline — a brand-new red test is not in that baseline, so it cannot
      be the committed end state).
- [ ] One `## IS38` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`.

## Definition of done (IS38)

- [ ] The 2 remaining inline handlers (AdminAnalytics `onchange`,
      Escalation/Tokens `onclick`) are gone; each element carries its
      `data-*` attribute.
- [ ] `inline-free.ts` compiles to `wwwroot/js/lib/inline-free.js`; the
      include line is in `_LayoutScripts.cshtml`.
- [ ] No new TS beyond `inline-free.ts`; no CSP change; no `confirm.ts`
      edit; no `_Layout.cshtml` change.
- [ ] `Document/New.cshtml` + `Document/Edit.cshtml` untouched (the 2
      inline script blocks are recorded as **E14**, a new finding, in the
      handoff — not fixed in IS38, to be fixed in IS38c).
- [ ] The 2 `application/json` data blocks untouched.
- [ ] Build green; `tsc` green; full test suite **green**; 
      `NoView_HasAnInlineEventHandler` green (8 → 0); 
      `NoView_HasAnInlineScriptBlock` present but skipped (E14 — lands green
      in IS38c after the 2 Document blocks are extracted to a tsc module).
- [ ] One commit (the 5 deliverables + the handoff section); the prompt
      file is **not** part of the unit commit (the lane convention: prompts
      are committed separately, the unit commit carries the deliverables +
      the handoff section — the IS37 precedent, `b64c2d73`).
- [ ] **Next unit: IS39** (control-map rewrite — traceability;
      `docs/security/control-map.md` gets the columns **Control · Answers ·
      Seam · Pinned by · ADR**; the stamp row moves to
      `RevocationStampMiddleware` + `PrivilegedStampMiddleware`; new rows
      for data classes, erasure, retention, operator-gated export,
      decision-free-read markers, log redaction + Sentry floor, CSP
      discipline, admin-read audit; every code row names an existing test).
