# ADR 0110 — Platform versioning + the "What's new" lane (the `VN` lane)

Status: Accepted
Date: 2026-09-28
Extends the **`kw-l` registry** (ADR 0015) + the **en/de/fr/da parity pins**,
the **`Milestones.cs` static-platform-data precedent** (the "source-controlled
static data — not configuration" convention the home page's roadmap and
`RepositoryInfo` both follow), the **`_FlashToast` / `_PinnedAnnouncement`
seam** (the fixed top-right toast / the top-of-`<body>` partial +
`client/lib/*.ts` module + `localStorage` dismissal convention), and the
**`KwLRegistryConsistencyTests`** view↔registry pin. This ADR adds the
platform's **version history** and makes it visible: **a "What's new" section
on the About page** (a dated, version-by-version changelog, anchored at
`#whats-new`) **and a dismissible toast** that announces the current
platform version with a link straight to that section.

## Context

A self-hosted deployment is upgraded by its operator over time; a resident
signing in after an upgrade has no signal of **what changed** — there is no
version anywhere in the product, no changelog, and no announcement. The
roadmap (`Milestones.cs`) is the *forward* view (what is shipping next); the
shipped surface has no *backward* view (what has already shipped, version by
version).

The shape is already fixed by the surface's own precedents:

- **Static platform data is source-controlled C#, not configuration, and not
  a document.** `Milestones.cs` and `RepositoryInfo.cs` are both `public
  static class` + `record` + `static IReadOnlyList<…>` in `Kumunita.Web`,
  pinned by Web tests (`MilestonesTests`). Versions are decided in the repo
  (they are a fact about the code that ships), not per-deployment — so a
  version registry belongs with them, not in Marten (ADR 0004 §B keeps
  Marten for domain documents; a changelog is not one) and not in the
  translation registry (changelog entries are copy, not UI chrome).
- **UI chrome strings go through the closed `kw-l` registry (ADR 0015).**
  The section's eyebrow/heading/lead, the per-version "Version {v}" label,
  and the toast's label + link text are the six `whatsnew.*` keys —
  registered in **en/de/fr/da** (the parity pins move together). The
  `KwLRegistryConsistencyTests` text scan and the `D5 about.*` contract are
  unaffected (the keys are a new `whatsnew.*` family, not `about.*`).
- **The toast follows the established seam.** `_FlashToast` is the fixed
  top-right `.toast-container` + `client/lib/flash-toast.ts` module +
  "the `show` class in the markup so a JS-less user still sees it"
  convention; `_PinnedAnnouncement` adds the `localStorage` dismissal +
  `EffectiveLanguageCode.ResolveAsync` per-request language chain. The
  what's-new toast is exactly that combination: server-rendered
  unconditionally (the server cannot read `localStorage`), dismissed per
  browser by the client module.
- **The `kw-l` TagHelper does not interpolate.** `{v}` must be substituted
  server-side in C# before output (the toast partial resolves the
  value and `.Replace("{v}", …)` it; the About section renders the label
  and the version as adjacent segments).

## Decision

The platform gains a **version registry + two view surfaces + one
client module**, all additive:

1. **`src/Kumunita.Web/WhatsNew.cs`** — `public static class WhatsNew` with
   `sealed record Version(string Version, string Date,
   IReadOnlyList<string> Changes)`, `static IReadOnlyList<Version> All`
   (ordered **newest-first**) and `static Version Latest` (the head).
   Version dates are fixed **ISO `yyyy-MM-dd`** strings — a fact about when
   the release shipped, not a user-visible timestamp, so **no `kw-dt`**
   (ADR 0019/0020) and no timezone involvement.
2. **About page — the "What's new" section**
   (`Views/StaticPages/About.cshtml`, `id="whats-new"` anchor, between
   "The project" and the contact CTA band): eyebrow/heading/lead via the
   new `kw-l` keys; one entry per `WhatsNew.All` row — the
   `whatsnew.version` label + version, a `<time datetime="…">` ISO date,
   and the change bullets verbatim (copy, en floor, the `Milestones`
   title convention — no per-bullet `kw-l`).
3. **`Views/Shared/_WhatsNewToast.cshtml`** — rendered at the top of
   `<body>` in `_Layout.cshtml` after `_FlashToast` /
   `_PinnedAnnouncement`. Server-rendered unconditionally; carries
   `data-whatsnew-version="@WhatsNew.Latest.Version"`; resolves the
   `whatsnew.toast_label` / `whatsnew.toast_see` values in the request's
   effective language (the ADR 0049 chain, the `_PinnedAnnouncement` seam)
   with `{v}` substituted; the `show` class in the markup (the `_FlashToast`
   contract — a JS-less first-run visitor still gets it); link
   `href="/about#whats-new"`; `role="status"`.
4. **`client/lib/whatsnew-toast.ts`** (tsc-built, the ADR 0031
   convention) — on load: if `localStorage["kumunita-whatsnew-seen-version"]`
   equals the toast's version, remove the toast silently; otherwise record
   the version and arm the Bootstrap `Toast` auto-hide (8 s, the
   `flash-toast.ts` `getOrCreateInstance(…, { autohide: true, delay: … })`
   idiom, the same `window.bootstrap` graceful-degradation guard).
5. **Six new `whatsnew.*` `kw-l` keys × en/de/fr/da** (the parity pins move
   together; the `KwLRegistryConsistencyTests` scan covers the view usage).
6. **`tests/Kumunita.Web.Tests/WhatsNewTests.cs`** — pins the registry:
   non-empty, newest-first, `Latest` is the head, unique versions, every
   row well-formed (non-blank version, ISO `yyyy-MM-dd` date, ≥1 non-blank
   change), and the six UI keys registered with non-blank en values.
   Static, no Testcontainers (a pure in-memory check).

**No schema change, no new bounded context, no new `AccessAction` /
`AccessVia` / audit verb, no service seam, no routes** (the section rides
the existing `/about` route; the toast rides the existing layout). **The
`Milestones.cs` / README Roadmap / `MilestonesTests.cs` triple is
untouched** — the `VN` lane is a **named lane on the shipped surface**,
not a milestone (the ADR 0013 GP / 0089 / 0093 / 0109 precedent), and the
roadmap stays the *forward* view while `WhatsNew` is the *backward* one.

## Consequences

- A resident of an upgraded deployment sees **what's new** in exactly two
  places: once (the toast, per browser, first run per version) and
  permanently (the About page's dated changelog) — with the link between
  the two (`#whats-new`).
- **The changelog copy is en floor** (the `Milestones` / `RepositoryInfo`
  convention for repo copy); the *chrome* (section header, "Version", the
  toast) is fully localized × 4. Extending a version's bullets is a one-file
  edit (`WhatsNew.cs`); adding a version is one list entry.
- The toast is **unconditional server-side, dismissible client-side** — a
  JS-less visitor always sees the announcement (never silently misses a
  version), a JS'd visitor sees it once per version. `localStorage`-blocked
  environments (private mode) degrade to "shows again on the next load" —
  never to "never seen".
- **`WhatsNew.All` is newest-first and `Latest` is its head** — pinned by
  `WhatsNewTests`; the toast announces `Latest`, so reordering without
  re-sorting would silently announce the wrong version (the test is the
  guard).
- Amends nothing; additive on 0015 (the registry) + 0031 (tsc-only) +
  0049 (the effective-language chain) + the `_FlashToast` /
  `_PinnedAnnouncement` seam (ADR 0015 D1 + ADR 0006-D) + the
  `Milestones.cs` static-data precedent.
