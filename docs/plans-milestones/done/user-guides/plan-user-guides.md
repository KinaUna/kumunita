# User guides (`UG`) — resident-facing guides in the `help/` subtree + the three-layer consistency loop

> **The ADR** is `docs/adr/0057-user-guides-in-help-subtree-and-consistency-loop.md`;
> **the human-facing conventions** (what a guide is, the registry, the writing
> rules, the loop) are `docs/guides/CONVENTIONS.md`. When this plan and the ADR
> disagree, **the ADR wins**. The ADR 0057 D4 drift pin
> (`UG_GuideRegistryTests`) is the load-bearing test.

## Understanding

A fresh instance serves **one** resident-facing help surface: the `/help` page
(ADR 0043 D1) — a four-bullet *summary*, not a *guide*. A resident who wants to
**act** (post to only my group, save a draft, pick who sees my post, change my
language, RSVP to an event) has to guess or ask an admin in a side-channel —
an un-integrated seam in the platform's own value chain
(`the-platform-as-integrator.md`: "Diagnose confusion as missing integration,
not missing features").

The platform has shipped a full resident-facing surface (posts/replies +
audience picker, public/private groups, drafts, the multilingual picker,
timezone + date-format, events + RSVP + reminders, the WYSIWYG editor, file
attachments, the Translator role) — **none of it has a resident guide**. The
operator's docs (SECURITY.md, ARCHITECTURE.md, OPS.md, the ADRs) are for the
*team*, not the *resident*.

The fork: build a **new** guide surface (its own module/routes/editor/
translations/standing) **or** hang the guides on the existing page tree under
`help`. **The tree was chosen** (user sign-off 2026-09-21): a new surface
duplicates the page engine, the WYSIWYG editor, the translator lane, the
authorization adapter — the "two editors, two renderers, two translation
lanes" smell ADR 0039's absorb-decision rejected. Hanging them on the tree is
**free**: the guides are `Page` docs, edited by the **one** `bindRichEditor`,
translated by the **one** `PageTranslation` lane, read by the **one**
`IAuthorizationService`, discovered by the **one** `/pages` tree browse. That
is the platform's own spine applied to documentation.

The second half is **consistency**: "keep user guides consistent as more
features are added and things change … a periodic or event based check." A
guide that describes a feature the code no longer has is a **broken seam**.
ADR 0047/0052 show the platform's existing answer to the analogous question
(UI-string drift): a closed registry + a code-owned floor + a pinned parity
test. This lane applies that shape to the guides, plus a periodic review
procedure as the safety net.

## Assumptions

- **Guides are `Page` docs under `help/`** (ADR 0057 D1). `Kind = System`
  (ADR 0040 standing matrix), `Audience = null` (public — a how-to is not
  gated), `LanguageCode = en` (authored-in), `AuthorId` empty (platform
  content), not a draft / not deleted. A guide is a **direct child of `help`**
  — a *grandchild* of `system`, **not** a new root and **not** a direct
  `system` child (the ADR 0043 D1 exact-set pin + the ADR 0040 namespace
  guard).
- **`en`-only floor (ADR 0057 D2).** The guides ship the `en` body (code-
  owned — the seeder's `GuidePages()` is the single source, upsert
  code-wins). A non-`en` guide body is **not** seeded — no `PageTranslation`
  row is attached to a guide at first boot. A non-`en` body, once a human
  Translator (ADR 0021) or a GlobalAdmin adds it, is community-owned and never
  clobbered by a later deploy (the ADR 0042 D1 invariant + the ADR 0005 C
  "never machine-translated" clause). This keeps the ADR 0044 page-baseline
  parity green (the `PageTranslation` count is unchanged by the guides).
- **The three-layer consistency loop (ADR 0057 D3)** — each layer answers a
  different question (the `in-code.md` "or it's ceremony" rule):
  1. **Where** — the guides are *in the tree* (the closed loop; a guide
     outside the tree is a bag of parallel lanes).
  2. **When** — the **event-based** check: a resident-facing change ships
     **with** its guide update, in the same lane (a lane that ships a feature
     without its guide update is not done). The response is the drift pin
     (`UG_GuideRegistryTests`).
  3. **How often** — the **periodic** check: OPS.md §13, a GlobalAdmin
     quarterly review (owner / cadence / response), the safety net.
- **Zero new access surface.** A guide is a `Page` doc (ADR 0039 §3.4 shape),
  read by the frozen `IAuthorizationService` (ADR 0006) via the existing
  `PageToAuditableResource` adapter. The ADR 0040 system-page standing matrix
  (edit/move/delete = GlobalAdmin only; add a translation = GlobalAdmin ∪
  Translator) applies verbatim. No new `AccessAction` / `AccessVia` / branch.
- **No schema change, no new doc type, no migration** — it reuses `Page` +
  `PageTranslation` and the existing `PageKind.System`.

## Approach (units)

- **U1 — The ADR.** `docs/adr/0057-…md`: the decision record (D1 the guides in
  the `help/` subtree; D2 the `en` floor / community-owned non-`en`; D3 the
  three-layer loop; D4 the drift pin; D5 the registry) + the standing + the
  follow-on lanes.
- **U2 — The conventions (the plan + writing rules).**
  `docs/guides/CONVENTIONS.md`: what a guide is/isn't, the registry table, the
  writing rules (plain language, the resident's steps, one guide one thing,
  `en` floor / community-owned non-`en`), the three-layer loop, the drift
  pin, the standing, the adding-a-guide Definition of Done.
- **U3 — The seeder (the registry + the floor).** `FirstBootSeeder.cs`:
  `GuidePages()` (the closed `en` registry — `getting-started`, `posts`,
  `groups`, `drafts`, `audience`, `language`, `events`, `translator`) +
  `SeedUserGuidesAsync` (nested under `help`, `en`-only, idempotent
  code-wins upsert), wired into `SeedTranslationResourcesAsync` (same
  session/commit, C3). One new pointer bullet on the `help` body in **all
  four** language arrays (`en`/`de`/`fr`/`da`) to preserve the ADR 0042 D2
  "structure preserved" parity.
- **U4 — The drift pin.** `tests/Kumunita.Core.Tests/UG_GuideRegistryTests.cs`
  (mirrors the `PG5_*` seeder family): the guides under `help` (not
  `system` children, not new roots), exactly one per slug, `Kind = System`,
  `Audience = null`, `LanguageCode = en`, `AuthorId` empty, not draft/deleted,
  no `PageTranslation` rows at first boot, `en` body parity with
  `GuidePages()`, idempotent across two boots.
- **U5 — The periodic check.** `docs/OPS.md` §13: the owner (GlobalAdmin),
  the cadence (quarterly / per-release), the checklist, the response
  (needs-update → a lane; feature-retired → soft-delete + registry removal;
  new-feature-without-guide → a lane), the **Last reviewed** stamp.
- **U6 — The lane register.** This file.
- **U7 — The roadmap parity.** `Milestones.cs` + the README Roadmap +
  `MilestonesTests` — the `UG` row (a `StatusDone` entry, inserted after `PG`
  and before `M4`), the pinned `Ids` list updated in the same commit; M5
  stays the single in-progress milestone.

## Exit gate

- `dotnet build Kumunita.slnx -c Debug` — 0 errors.
- `UG_GuideRegistryTests` green (the three tests above).
- `PG5_*` green (the ADR 0043 D1 exact-set + only-root pins unchanged — the
  guides are grandchildren, not new roots / not `system` children).
- `ADR_0044_BaselineTests` green (the `PageTranslation` count unchanged — the
  guides add `Page` docs, not translation rows; the `help` body parity holds
  because both the seeder and the test read the same code arrays).
- `MilestonesTests` green (the `UG` row added, M5 the single in-progress).
