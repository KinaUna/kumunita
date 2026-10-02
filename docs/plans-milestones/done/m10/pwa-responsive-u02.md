# M10 U02 — Service worker: the allowlist + the negative pin

**Unit plan (secondary tier, self-contained).** Read **this file + your
entry reads** and you can execute. The register is
`docs/plans-milestones/plan-m10-pwa-responsive.md`; the scratch handoff
note is `docs/plans-milestones/m10-pwa-handoff-notes.md`; the atomicity
contract and unit-series rule are in the register header. **Runs after
U01** — the design doc §SW is the locked authority; copy its allowlist
verbatim, do not re-derive.

## Goal

D2 rendered: `wwwroot/sw.js` — the GET-only allowlist interception, the
cache versioning rule (a versioned cache name `kumunita-shell-v1`; the
SW clears the prior version on `activate` — the standard shape), the
**fall-through** for every non-allowlisted request (the C-M10·2 pin), and
the **structural** pins (the file exists at a same-origin `wwwroot/`
path, the allowlist in the JS matches the design doc verbatim). **The
behavioral negative pin** (a signed-in route is not in the cache after a
visit) is **U06's Playwright spec's job, not U02's** — U02's job is to
make the JS *structurally* fall-through and to pin that structure.

## Context (the locked set — from the design doc §SW)

- **C-M10·2 (the SW never caches signed-in content):** the allowlist is
  closed + named in the design doc §SW; **every other request** (any
  method other than GET, any path off the list, any request carrying an
  `Authorization` header) passes through. The negative pin: a signed-in
  route (`/notifications`, `/messages`, `/events`, `/projects/todos`,
  `/posts/{id}`, `/search`, `/admin/*`) is **not** in the SW cache after
  a visit. U02 pins the *structure*; U06 pins the *behavior*.
- **C-M10·3 (the SW survives the CSP):** `script-src 'self'` unchanged;
  the SW file is a same-origin `wwwroot/` asset (the `'self'` source);
  no inline `eval`, no remote code, no `unsafe-inline`. **Do not edit
  `Program.cs` in U02** — the CSP is already correct; U02's job is to
  satisfy it, not change it.
- **D2 (the versioning rule):** a versioned cache name
  `kumunita-shell-v1`; on `activate`, clear any cache not matching the
  current version (the standard stale-cache cleanup). The design doc §SW
  locks the exact cache name string.
- **The fall-through shape (the code pin):** every request not on the
  allowlist (or not a same-origin GET) must `event.respondWith(fetch(
  event.request))` — a *pass-through*, never a cache-lookup-then-fallback
  that could serve stale signed-in content. The design doc §SW locks the
  exact code shape (U02's structural pin asserts it).

## Entry reads (5)

1. `docs/design/m10-pwa-responsive-design.md` — §SW (the closed allowlist
   + the versioning rule + the fall-through code shape + the negative
   pin's name). **The authority — copy the allowlist verbatim.**
2. `src/Kumunita.Web/Program.cs` — the CSP block (~line 486–512). **Read
   only, do not edit** — confirm the `wwwroot/` SW path satisfies
   `script-src 'self'`; confirm no `unsafe-inline` is granted (there is
   none — U02 must not need it).
3. `src/Kumunita.Web/wwwroot/css/site.css` — read only (the drift check
   that U02 does **not** touch `site.css`; the SW has no CSS coupling).
4. `tests/Kumunita.Web.Tests/PwaManifestTests.cs` — U01's 3 pins.
   **Extend, do not rewrite** (unit-series rule).
5. `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — confirm U01's
   manifest link is present (the drift check). **U02 does NOT add the SW
   registration here** — the SW is registered by the browser when
   `navigator.serviceWorker.register('/sw.js')` runs, and that
   registration call is part of U03's `pwa-install.ts` module (the ADR
   0031 self-wiring shape), not a `<script>` in the layout. Read the
   layout only to confirm the manifest link is present (a U01
   regression check).

## Deliverables (2)

- `src/Kumunita.Web/wwwroot/sw.js` — **new.** The allowlist (exactly the
  design doc §SW's closed set), the `kumunita-shell-v1` cache, the
  `activate` clear-prior-versions logic, the GET-only + same-origin
  guard, and the fall-through `event.respondWith(fetch(event.request))`
  for everything else. Plain ES2017 `sw.js` (no imports, no modules —
  service workers in `wwwroot/` are classic scripts here; the design
  doc §SW's code shape is the authority).
- `tests/Kumunita.Web.Tests/PwaManifestTests.cs` — extend with the two
  structural pins below (the file is U01's; U02 adds to it; do not
  rewrite U01's 3 pins).

## Pinned tests (2) — in `PwaManifestTests.cs` (extend U01's 3)

- `ServiceWorker_File_Exists_And_Is_SameOrigin_Wwwroot` — the string
  pin: `wwwroot/sw.js` exists + its first non-whitespace bytes are a JS
  comment or `/*` or `//` or an identifier (not a binary — i.e. the file
  is a text JS file, satisfying the `script-src 'self'` file-location
  assertion, the ADR 0043 SP-U04 idiom).
- `ServiceWorker_Allowlist_Matches_Design_Doc_Verbatim` — read the
  design doc's §SW allowlist (the exact set of path prefixes, as written
  in the doc) + the `sw.js` file, extract the allowlist array from the
  JS (a regex on the known array-literal shape, the design doc's exact
  code shape), and assert the two sets are equal. **A closed-set
  witness** — the C-M10·2 structural pin.

**The negative pin (C-M10·2's behavior) is in U06's Playwright spec** —
`SignedIn_Route_Not_In_ServiceWorker_Cache` — U02 does not author it
here. U02's job is structural; U06's is behavioral. Do not add a
behavioral `caches`-API test in `PwaManifestTests` (that needs a browser
+ a running app, the Playwright half).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green; then
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green with the 5 pins passing (U01's 3 + U02's 2).
- **The structural fall-through is verifiable by reading `sw.js`:**
  every code path that is not (same-origin + GET + on-allowlist) ends in
  `fetch(event.request)` — no `caches.match` on a signed-in path.
- Handoff entry: the SW allowlist as implemented (the exact set of path
  prefixes, verbatim), the cache version string, the `activate` clear
  logic (the exact code), the fall-through code shape (the
  `event.respondWith(fetch(event.request))` line, verbatim), and any
  drift (a path the design doc's allowlist named that U02 found
  unnecessary, or vice versa — a `## U02 — Drift pause` if the design
  doc's list is wrong; U02 must not silently "fix" the design doc —
  record it, let the user decide).
- **Last action:** move this unit's own plan file
  (`docs/plans-milestones/in-progress/pwa-responsive-u02.md`) to
  `docs/plans-milestones/done/` (a `git mv`).
