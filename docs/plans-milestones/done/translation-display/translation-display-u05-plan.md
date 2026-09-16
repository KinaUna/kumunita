# TD U05 — TS swap module + layout wiring

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-translation-display.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.

## Goal

Add the one `tsc`-only ES module that **toggles the visible variant** per chip
click (display-only — no HTML injection, no re-fetch, no navigation), and load
it in the shared layout. **Touch no C#, no Razor view markup.**

## Context you need (read these first, in this order)

1. `docs/design/translation-display-design.md` §**Pinned contract** → `### TS
   module contract (exact)` + §**Invariants** TD·3 / TD·4 / TD·8 — the exact
   selector strings and the `closest`/`querySelectorAll` toggle logic to
   implement, verbatim.
2. `src/Kumunita.Web/client/lib/avatar.ts` — the **`tsc`-only ES-module**
   `addEventListener` pattern to mirror (no bundler, no framework, `document.
   querySelectorAll` + `addEventListener`). Note the "a 404 may have already
   resolved" defensive check — this module has no async gap, but the
   *shape* (a module that scopes to its own elements, no global state) is the
   model.
3. `src/Kumunita.Web/client/lib/avatar-upload.ts` — a second module example
   (how a module scopes its listeners to its own `[data-*]` elements, and the
   import style if any — this one has none).
4. `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — the `<script type="module"
   src="~/js/lib/*.js">` load block (around lines 113–120) to append the new
   tag to.
5. `src/Kumunita.Web/tsconfig.json` + `src/Kumunita.Web/package.json` —
   confirm `rootDir=client`, `outDir=wwwroot/js`, `module=ES2022`, and that
   `npm run build` compiles `client/**` (so `client/lib/translation-swap.ts`
   emits to `wwwroot/js/lib/translation-swap.js`).

## Deliverables (2 files — 1 new, 1 modified)

**`src/Kumunita.Web/client/lib/translation-swap.ts`** (new) — the swap, per
the pinned contract:

```ts
/**
 * Translation chip swap (TD lane, ADR 0027).
 *
 * Each language chip on the post/reply detail surface is a
 * `[data-translation-chip]` carrying the item's `data-td-group` and the
 * language's `data-td-variant`. The item's variants are sibling
 * `[data-td-variant]` containers inside the same `data-td-group` wrapper
 * (rendered server-side — the original default-visible, the added ones
 * hidden). This module is the display-only toggle: on a chip click it shows
 * the matching container and hides the rest. No innerHTML, no fetch, no
 * navigation — the swap is an enhancement, not a requirement (TD·8: with JS
 * off the original stays visible and every variant is present in the DOM).
 */
const chips = document.querySelectorAll<HTMLElement>('[data-translation-chip]');

for (const chip of chips) {
  chip.addEventListener('click', () => {
    const group = chip.closest<HTMLElement>('[data-td-group]');
    if (!group) return;
    const target = chip.dataset.tdVariant;
    for (const variant of group.querySelectorAll<HTMLElement>('[data-td-variant]')) {
      variant.style.display = variant.dataset.tdVariant === target ? '' : 'none';
    }
    // Mark the active chip (a11y / affordance) — the toggle itself is
    // container-driven; this is cosmetic.
    for (const sibling of group.querySelectorAll<HTMLElement>('[data-translation-chip]')) {
      sibling.setAttribute('aria-pressed', sibling === chip ? 'true' : 'false');
    }
  });
}
```

Adjust only if the pinned contract's exact selector strings differ — they must
**match U03/U04's markup** (`data-td-group` wrapper, `data-translation-chip`,
`data-td-variant`). ~30–45 LOC, **no imports**.

**`src/Kumunita.Web/Views/Shared/_Layout.cshtml`** (modified) — add one line
to the module-load block (next to the existing `~/js/lib/avatar.js` tag):
`<script type="module" src="~/js/lib/translation-swap.js"></script>`.

## Invariants honored

- **TD·3** — the module toggles **display only**; it never builds or injects
  HTML, never reads a `data-*` HTML blob, never fetches or navigates.
- **TD·2** — clicking any chip (original or added) shows that variant;
  clicking the original returns to it (the toggle is symmetric).
- **TD·8** — with JS disabled the module simply never runs; the server-
  rendered original stays visible and the added variants remain in the DOM.

## Exit

`npm run build` green (emits `wwwroot/js/lib/translation-swap.js`) **and**
`dotnet build` green. Manually (or per the FACES) clicking a chip swaps the
visible variant and clicking the original returns to it; with JS disabled the
original is visible and all variants are present in the DOM (TD·8). Handoff
note (append to `docs/plans-milestones/done/translation-display-handoff-notes.md`):
4–5 lines starting `## U05 — TS swap module` — (a) the **selector strings**
the module uses (verbatim, must match U03/U04), (b) the layout script line
added (file + the exact `<script>` tag), (c) an explicit confirmation there is
**no** `innerHTML` / `fetch` / navigation (TD·3/TD·4), (d) the `npm run build`
+ `dotnet build` result, and (e) the emitted JS path (`wwwroot/js/lib/
translation-swap.js`).
