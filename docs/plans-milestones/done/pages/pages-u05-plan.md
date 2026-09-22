# U5 — Reference-from-UGC + the seeded default pages + the `/about` fallback

- **Lane:** Pages (`PG`)
- **Unit:** U5 (of U0–U07)
- **Kind:** integration (free reference surface + the absorb seed + the
  `StaticPagesController` retarget — **one store, not two**)

## Goal

Wire the **reference** surface (free — the RC link idiom already renders
`[label](/pages/{path})`, but *named* here with a test for the
link-present-but-target-denied case), **seed the three default pages**
(`about`, `terms`, `help`) as `Page` docs carrying the **current**
`LocalizedPage` `en` bodies/titles (idempotent first-boot seed), and
**retarget `StaticPagesController`** so `/terms`, `/help`, `/about` read from
the **tree** (the `GetByPathAsync` shape) with the *existing* `/about`
product-story fallback when truly absent. After this unit, a fresh instance is
byte-identical to today for those three pages, and there is **one store** (the
`Page` doc) with the old surface kept only as a fallback (not yet removed —
U07 removes it).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/pages-design.md` §3.6 + §3.9 — the **free reference** decision
   (the `MarkdownRenderer` + `IsSafeUrl` already handle `[label](/pages/…)`;
   the only new piece is the audience-gated serving route) + the **absorb
   migration** ordering (seed the three pages, keep `LocalizedPage` readable
   as a fallback, retire it last in U07).
2. `src/Kumunita.Web/Security/MarkdownRenderer.cs` — confirm
   `[label](url)` links render + `IsSafeUrl` accepts the relative `/pages/…`
   path (the "free" claim — **no read-path change** needed).
3. `src/Kumunita.Web/Controllers/StaticPagesController.cs` — the **three
   hard-coded routes** (`terms`/`help`/`about`) + the `/about` product-story
   `HomeViewModel` fallback — the surface to retarget to the tree.
4. `src/Kumunita.Core/Bootstrap/` (the seeder / first-boot idiom) — the
   **idempotent seed** pattern (boot twice, no duplicate rows) the three
   default pages follow.
5. `docs/adr/0005-multilingual-support.md` §A — the **static-page lane** this
   unit absorbs (the `LocalizedPage` `en` bodies the seed carries over, the
   "the single page engine" the absorb preserves).

## Deliverables (closed set)

1. **Reference test** (`Kumunita.Web.Tests` or `Kumunita.Core.Tests`): a
   `PG_*` test for the **link-present-but-target-denied** case — a post/reply
   body containing `[About](/pages/about)` renders the link (the RC idiom),
   and *opening* `/pages/about` is a **separate** `CanAsync(Read)` decision
   that can 403/404. A link in an authorized post does **not** imply the
   target page is authorized.
2. **Seed the three default pages** — `about`, `terms`, `help` — as `Page`
   docs in the seeder (the existing first-boot idiom, **idempotent**), each
   carrying the **current** `LocalizedPage` `en` body + title, `Audience =
   null` (public), `MountPoint` set (`about` → `footer/community`; `terms` +
   `help` → `null` or their own slots), `LanguageCode = "en"`, `IsDeleted =
   false`. A fresh instance is byte-identical to today for those three pages.
3. **Retarget `StaticPagesController`** — `/terms`, `/help`, `/about` resolve
   via `IPageService.GetByPathAsync` (the tree) and render the `Page` body
   (the *one* `MarkdownRenderer`), with the **existing** `/about` product-story
   `HomeViewModel` fallback **only** when the `about` page is genuinely absent
   (a broken seed). **One store, not two** — but the old `LocalizedPage`
   surface still works as a **fallback** path (not yet removed).
4. **The `/about` fallback** — when the `about` `Page` is absent (a broken
   seed), `/about` renders the existing product-story `HomeViewModel`
   (backward-compatible — nothing a resident can see is lost).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green — the reference test (link-present-but-target-denied) + the
  `StaticPagesController` retarget tests pass.
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  green — the seed is **idempotent** (boot twice, no duplicate `Page` rows;
  the `(ParentId, Slug)` unique index prevents a double-seed).
- A fresh instance's `/about`/`/terms`/`/help` are byte-identical to today; a
  post body can link a page and the link is gated separately (the
  link-present-but-target-denied test passes); the seeded pages are idempotent.
- **`LocalizedPage` is still readable** (the fallback path) but is **not yet
  removed** (U07 removes it).
- Append a `## U5 — reference + seed + /about fallback` section to
  `pages-handoff-notes.md`.

## Notes / deviations

- **The reference is free — do not add a read-path change.** The
  `MarkdownRenderer` + `IsSafeUrl` already render `[label](/pages/…)` links.
  This unit only *adds the audience-gated serving route* (U04's
  `GET /pages/{**path**}`) + the test. If you find yourself editing
  `MarkdownRenderer`, stop — that's a drift.
- **The seed is idempotent.** Booting a fresh instance twice must not create
  duplicate `Page` rows (the `(ParentId, Slug)` unique index + a
  "exists-or-create" check in the seeder). A test boots twice and asserts a
  single row per slug.
- **The `/about` fallback is the safety net.** If the seed fails, `/about`
  still renders the product story (the existing `HomeViewModel`) — a resident
  never sees a 500 or an empty about page. The fallback is **retained** until
  U07 (it reads `LocalizedPage`), then **re-pointed** at the tree (or kept as
  a tree-absence fallback).
- **One store, not two.** After this unit, `/terms`/`/help`/`/about` read the
  `Page` doc (the tree), **not** the `LocalizedPage` doc. The `LocalizedPage`
  surface is only a *fallback* for a broken seed — it is **not** the primary
  read path anymore. U07 removes it.
