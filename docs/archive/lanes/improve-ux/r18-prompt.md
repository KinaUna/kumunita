---
id: r18-prompt
title: "Prompt: IMPROVE-UX R18 — /settings/kiosk create-link surface (the wall-link lifecycle)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0302-shared-kiosk-link.md
  - docs/adr/0164-kiosk-mode.md
  - docs/adr/0285-kiosk-playlist-any-item.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R18 — the `/settings/kiosk` create-link surface: the resident creates a
> shared kiosk link (the raw token is shown **once**, then never again),
> sees the link's "last used" row, and can revoke it. The personal-kiosk
> settings (playlist, timing) stay untouched — the shared-link lifecycle is
> a **new section** on the same page (or a new sub-route — the agent's call).
> **Who is it for:** the next coding agent (fresh context). **When to read
> the rest:** when starting R18 (the unit that gives the resident the
> create / list / revoke lifecycle R15's Core seam already provides; R19's
> SEC review builds on the surface R18 adds).

# Fresh-context prompt: IMPROVE-UX R18

You are continuing the Kumunita repository on `development`. The lane's
running log is `docs/plans-milestones/in-progress/improve-ux-handoff-notes.md`
— **R17 just landed** (the community-visible filter in
`KioskItemCatalog.ResolveAsync`; the shared link now renders the denied
slide for anything not visible to the whole community; group-private items
denied; drafts denied for the link viewer; personal-kiosk untouched). R18 is
the **next unit in the kiosk-link lane**: the resident-facing surface that
creates / lists / revokes the shared link R15's Core seam already provides.

**Implement IMPROVE-UX unit R18 only.** The plan card (in
`plan-improve-ux.md`, the R18 row):

> R18 — `/settings/kiosk`: create link (copy once), list with last used,
> revoke; personal mode untouched; keys |
> `Views/Kiosk/Settings.cshtml`, `client/lib/kiosk-settings.ts` | R15 |
> **create/revoke works**

**R18 is the settings surface only.** R19 (the SEC review of R15–R18) is a
separate later unit — **do not start it.** R19's DoD includes "review
note; findings fixed or filed" — the review itself is R19's job, not R18's.

## Before writing, read (in this order)

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** —
  the doctrine.
- **`docs/adr/0302-shared-kiosk-link.md`** — D1 (one row per link; the
  `TokenHash = SHA-256` digest only), D2 (hash at rest), D3
  (`IKioskLinkService` seams — `CreateAsync` / `RevokeAsync` /
  `ValidateAsync`), D4 (invariants: revocable, read-only, /kiosk + slides
  only, community-visible items only, audited under the owner as
  via-kiosk-link), D5 (personal kiosk 0164/0285 unchanged).
- **`src/Kumunita.Core/Kiosk/IKioskLinkService.cs`** — the 3-method seam.
  R18 calls `CreateAsync(playlistId, ownerSubjectId, ct)` (returns
  `(string Token, KioskLink Link)` — the **raw token** is the copy-once
  value; the row only stores the hash), `RevokeAsync(linkId, ct)`, and
  reads the `KioskLink` row's `LastUsedAt` / `RevokedAt` for the list.
  **Do not change the seam.**
- **`src/Kumunita.Core/Kiosk/KioskLink.cs`** — the doc shape (the fields
  R18 displays: `PlaylistId`, `OwnerSubjectId`, `CreatedAt`, `LastUsedAt`,
  `RevokedAt`).
- **`src/Kumunita.Web/Controllers/KioskController.cs`** — the existing
  personal-kiosk settings surface: `SettingsGet` (~line 162), `SettingsPost`
  (~line 255), `Items` (~line 204), `Index` (the `/kiosk` route), `Slide`.
  R18 adds the shared-link lifecycle to this controller (or a new one — the
  agent's call) **without changing the personal-kiosk actions**.
- **`src/Kumunita.Web/Views/Kiosk/Settings.cshtml`** — the existing
  personal-kiosk settings view (playlist, timing, interval). R18 adds the
  shared-link section to this view (or a new partial — the agent's call).
- **`src/Kumunita.Web/client/lib/kiosk-settings.ts`** — the existing
  personal-kiosk settings JS (the playlist editor, the interval picker).
  R18 adds the shared-link lifecycle JS (the "create link" button, the
  "copy once" reveal, the revoke confirm, the list refresh).
- **`docs/adr/0164-kiosk-mode.md`** — the personal-kiosk invariant R18 must
  not touch (D5 of ADR 0302).
- **`docs/adr/0285-kiosk-playlist-any-item.md`** — the item types the
  personal playlist can hold (the shared link plays the same playlist).
- **`docs/dev/testing-and-ci.md`** — the test-runner quirk (the in-process
  runner, not `dotnet test`).
- **`tests/Kumunita.Web.Tests/KioskControllerTests.cs`** — the existing
  personal-kiosk settings tests. R18 adds the shared-link lifecycle tests
  to this class (or a new one — the agent's call).
- **`git status`** + the branch (R17's commit must be intact; you are on
  `development`).

## The work

The core question: **where does the shared-link lifecycle live, and what is
the exact shape of the "create link" interaction?**

**Answer (the design):**

1. **The settings surface.** The personal-kiosk settings page is
   `GET /settings/kiosk` → `KioskController.SettingsGet` →
   `Views/Kiosk/Settings.cshtml`. The shared-link lifecycle is a **new
   section** on the same page (or a new sub-route
   `GET /settings/kiosk/link` — the agent's call; the personal-kiosk
   settings stay on the original route). The resident (the owner) sees
   both: the personal playlist (untouched) and the shared-link lifecycle
   (create / list / revoke).

2. **Create link (copy once).** The resident clicks "Create link" (or
   "Generate link") → `POST /settings/kiosk/link` (the agent's call on the
   exact route; the anti-forgery token is required — the existing
   `SettingsPost` uses `[ValidateAntiForgeryToken]`). The server calls
   `IKioskLinkService.CreateAsync(playlistId, ownerSubjectId, ct)`, where
   `playlistId` is the owner's personal playlist id (the ADR 0164 row) and
   `ownerSubjectId` is the resident's subject id. The response is **the raw
   token, shown once** — the row only stores the SHA-256 hash (ADR 0302 D2).
   The UI shows the token in a **read-only input** (the resident copies it
   from there; there is no "show again" — the hash at rest means the raw
   token is **never recoverable** after this response). A "Copied" affordance
   is optional (the agent's call; the existing JS has a clipboard
   helper — reuse it).

3. **List with last used.** The resident sees their active links (the
   owner's scope — `OwnerSubjectId = me`) as a list: the link id (a short
   human-readable suffix of the id is fine — the agent's call), `CreatedAt`,
   `LastUsedAt` (the "last used" column the plan card names — the ADR 0302
   D4 invariant "stamped on every successful validate"), and a **Revoke**
   button per row. A revoked link is **not** in the list (the
   `RevokedAt != null` row is terminal — the R15 state machine). A
   "Create another" button at the top of the list (or a re-show of the
   create section) — the agent's call.

4. **Revoke.** The resident clicks "Revoke" on a row → a confirm dialog (the
   existing JS has a `confirm()` helper — reuse it) → `POST /settings/kiosk/link/revoke`
   (the agent's call on the exact route) with the link id → the server calls
   `IKioskLinkService.RevokeAsync(linkId, ct)` → the row disappears from
   the list (the `RevokedAt` is now set). **No undo** — the state machine is
   terminal (ADR 0302 D1). A "revoke all" button is **not** in scope (the
   agent's call: if it's trivial, add it; if it needs a new seam, skip it
   and note it in the R19 SEC-review open questions).

5. **Personal mode untouched.** The personal-kiosk settings (the playlist
   editor, the interval picker, the timing controls) are **unchanged**. The
   shared-link section is additive. The existing
   `SettingsGet` / `SettingsPost` / `Items` actions are not modified
   (they may be extended to carry the shared-link data — the agent's call,
   but the personal-kiosk behavior is unchanged).

**The "create link" flow in one sentence:** the resident clicks "Create
link" → the server mints a CSPRNG token, stores its SHA-256 hash in a new
`KioskLink` row, and returns the raw token **once** → the resident copies
the token (the shared URL is `/kiosk?link=<token>` — the agent's call on
the exact query key; R16's middleware parses it) → the token is **never
shown again** (the hash at rest means the raw token is unrecoverable).

**What NOT to do:**
- Do **not** change the `IKioskLinkService` seam (R15's Core is frozen —
  R18 is the Web surface that calls it).
- Do **not** change the `KioskLink` doc shape (R15's Core is frozen — the
  fields R18 displays are already there: `PlaylistId`, `OwnerSubjectId`,
  `CreatedAt`, `LastUsedAt`, `RevokedAt`).
- Do **not** touch the personal-kiosk settings (the playlist editor, the
  interval picker, the timing controls) — the shared-link section is
  additive (ADR 0302 D5).
- Do **not** add a new `AccessAction` or `AccessVia` enum member (the
  settings surface is the owner's own session — the existing
  `[Authorize]` / `RequireRole` attributes are sufficient).
- Do **not** start R19 (the SEC review of R15–R18) — the review itself is
  R19's job.
- Do **not** add a "max links per playlist" ceiling (the ADR 0302 D1
  "open question" — the R19 SEC review settles it; R18 ships no ceiling).
- Do **not** add a "revoke all" button (not in scope; if the agent adds it,
  note it in the R19 open questions).

**New localized strings** (en/de/fr/da) — the plan card's "keys" line:
the shared-link section's heading, the "Create link" button, the "Copy" /
"Copied" affordance, the "Last used" column header, the "Revoke" button,
the "Revoke?" confirm dialog, the "Revoked" toast (if any), the "Create
another" button, the empty-list message ("No shared links yet"), and the
`/kiosk?link=<token>` URL label (the agent's call on the exact keys; follow
the existing `kiosk.settings.*` naming convention). **Add every new key to
all four languages** (en/de/fr/da) — the i18n gate (`tools/docs/` or the
`KwLRegistryConsistencyTests`) will catch a missing key.

**Tests** in `tests/Kumunita.Web.Tests/` — add to the existing
`KioskControllerTests` class (or a new `KioskLinkSettingsTests` class — the
agent's call):

1. **Create link — the happy path.** A resident with a personal playlist
   calls the create-link action → a new `KioskLink` row is created (the
   owner's scope — `OwnerSubjectId = me`), the response contains the raw
   token **once**, the row stores the SHA-256 hash (not the raw token), and
   the list view now shows the new row.
2. **Create link — the token is not recoverable.** After the create
   response, the resident cannot re-fetch the raw token (the hash at rest
   — there is no "show again" endpoint; the list view shows the row's
   metadata, not the raw token).
3. **List with last used.** A resident with two active links sees both rows,
   each with `CreatedAt` and `LastUsedAt` (the "last used" column the plan
   card names). A revoked link is **not** in the list.
4. **Revoke — the happy path.** The resident calls the revoke action on a
   link id → the row's `RevokedAt` is set → the row disappears from the
   list → a second revoke on the same id is a no-op (the state machine is
   terminal — the R15 `RevokeAsync` returns `bool` and the agent's call is
   whether the UI shows a "already revoked" message or a silent no-op).
5. **Revoke — the owner-scope pin.** A resident cannot revoke another
   resident's link (the `OwnerSubjectId` mismatch — the R15 seam's
   owner-scope check). The UI shows a 404 or a 403 (the agent's call; the
   R15 seam returns `false` on mismatch, the agent's call is how the
   controller maps that to an HTTP status).
6. **Personal mode untouched.** The existing personal-kiosk settings tests
   (the playlist editor, the interval picker) still pass unchanged — the
   shared-link section is additive.
7. **The shared-link section is not visible to a non-owner.** A resident
   without a personal playlist (or a non-resident — the agent's call on
   the exact guard) sees the personal-kiosk settings but **not** the
   shared-link section (the `IKioskLinkService.CreateAsync` requires a
   `playlistId` — the agent's call on the exact guard: a `404` or a
   `403` or a hidden section).

Follow the existing `KioskControllerTests` harness for how to boot the host
+ substitute `IKioskLinkService`.

## Scope rules

- **Do not touch** `IKioskLinkService` / `KioskLinkService` / `KioskLink`
  (R15's Core is frozen). R18 is the **Web surface** that calls the R15
  seam (the settings controller, the settings view, the settings JS, the
  localized strings, the tests).
- **Do not start R19.** The plan card's "Done when" (create/revoke works)
  is the only acceptance bar.
- **One commit** (the house rule): the settings surface + the localized
  strings + the tests land in **one** `feat(ux):` commit
  (`feat(ux): /settings/kiosk create-link surface — the shared kiosk link
  lifecycle (IMPROVE-UX R18)`).
- **New strings go to en/de/fr/da** — the plan card's "keys" line. Every
  new key is added to all four languages. The i18n gate will catch a
  missing key.

**Before committing:** run the narrowest tests via the in-process runner:
`KioskControllerTests` (the existing personal-kiosk surface intact + the
new shared-link lifecycle) + `KioskLinkServiceTests` (the R15 Core seam) +
`KioskLinkCommunityFilterTests` (the R17 filter) + `KioskItemCatalogTests`
(the R17 filter's read seams) + `dotnet build`. If any test fails, read the
error, adjust, continue.

Then report: the route(s) added (the create / list / revoke endpoints), the
exact localized keys added (en/de/fr/da), the test class name + number of
tests, the commit hash, and any `// TODO ❓` open questions for R19's SEC
review (e.g. the "max links per playlist" ceiling, the "revoke all"
button, the revoke-mismatch HTTP status). Do not commit unrelated work.
