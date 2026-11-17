---
id: r17-prompt
title: "Prompt: IMPROVE-UX R17 — community-visible filter for kiosk-link mode (the wall screen never shows group-private items)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0302-shared-kiosk-link.md
  - docs/adr/0285-kiosk-playlist-any-item.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R17 — the community-visible filter that makes the kiosk-link principal
> (R16) see **only** items visible to the whole community, rendering the
> existing denied slide for anything group-private or audience-restricted.
> **Who is it for:** the next coding agent (fresh context). **When to read
> the rest:** when starting R17 (the unit that enforces ADR 0302 D4's
> "community-visible items only" invariant on the item-read surface; R18's
> `/settings/kiosk` create-link surface and R19's SEC review build on the
> filter R17 adds).

# Fresh-context prompt: IMPROVE-UX R17

You are continuing the Kumunita repository on `development`. The lane's
running log is `docs/plans-milestones/in-progress/improve-ux-handoff-notes.md`
— **R16 just landed** (the kiosk-link authentication on `/kiosk` +
`/kiosk/slide`; the middleware mints a restricted owner principal carrying
the `"kiosk-link"` role value, no `Member`/`Moderator`/`GlobalAdmin`). R17
is the **next unit in the kiosk-link lane**: making the item the link
reaches **community-visible-only**.

**Implement IMPROVE-UX unit R17 only.** The plan card (in
`plan-improve-ux.md`, the R17 row):

> R17 — Community-visible filter in kiosk-link mode: the catalog's
> `ResolveAsync` returns the denied slide for anything not visible to the
> whole community; audit `Via` = kiosk link; tests.
> Read first: `KioskItemCatalog` (Web/Services), `AccessVia`.
> Done when: **tests: group-private item → denied slide.**

Decision D4 (ADR 0302) is the invariant you are enforcing: a kiosk link
**shows community-visible items only** (others get the existing denied
slide), and **every read is audited under the owner as via-kiosk-link**.
**R17 is the filter only.** R18 (the `/settings/kiosk` create-link surface)
and R19 (SEC review) are separate later units — **do not start them.**

## Before writing, read (in this order)

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** —
  the doctrine.
- **`docs/adr/0302-shared-kiosk-link.md`** (D4 invariants — "community-
  visible items only", "audited under the owner") and the **R16 section of
  `improve-ux-handoff-notes.md`** (the restricted principal's exact claim
  shape: `Kumunita.Sub` = owner, `"kiosk-link"` role value, no other
  roles).
- **`src/Kumunita.Web/Services/IKioskItemCatalog.cs`** — the
  `ResolveAsync(entry, actorId, roles, ct)` seam and the
  `KioskResolvedItem.Visible` / `KioskResolvedItem.Hidden` shapes.
- **`src/Kumunita.Web/Services/KioskItemCatalog.cs`** — the impl: how
  `ResolveAsync` resolves each `KioskItemType` through the item's own read
  seam. This is where the filter goes.
- **`src/Kumunita.Web/Controllers/KioskController.cs`** — `Classify`
  (~line 376): it calls `ResolveAsync` and renders `DeniedSlide` when
  `!resolved.Visible`. The denied shape already works — R17 just needs
  `ResolveAsync` to return `Hidden` for non-community items in kiosk-link
  mode.
- **`src/Kumunita.Core/Authorization/Decision.cs`** — the `AccessVia`
  enum (Owner / Audience / Delegation / Moderator / Report / BreakGlass /
  Admin). The ADR 0302 D4 "audited under the owner" means the item reads
  the link authenticates carry `Via: Owner` (the owner is the audit
  principal) — **not** a new `AccessVia` value. The "via-kiosk-link" label
  is the *authentication* marker (R16's `"kiosk-link"` role), not an audit
  `Via` value. Do **not** add a new `AccessVia` enum member for this.
- **`docs/dev/testing-and-ci.md`** — the test-runner quirk.
- **`git status`** + the branch (R16's commit must be intact; you are on
  `development`).

## The work

The core question: **how does `ResolveAsync` know it is being called in
kiosk-link mode vs. personal-kiosk mode?**

Answer: the `roles` set. In personal-kiosk mode the viewer is the owner
(their own session, carrying `Member` / possibly `Moderator` /
`GlobalAdmin`). In kiosk-link mode (R16) the principal carries the
`"kiosk-link"` role value and **no** other roles. So the filter is:

**When `roles` contains `"kiosk-link"`, resolve each item with a restricted
standing such that only community-visible items pass.**

The cleanest mechanism (and the one the existing read seams already support):
**resolve as if the viewer has no group membership and no elevated standing.**
Concretely, for each `KioskItemType`:

- **CommunityPost** — the post's audience must be community-wide (not
  group-restricted). The existing post read seam checks the actor's
  standing; with no `Member` role and no group membership, a
  group-audience post is denied.
- **GroupPost** — the group's visibility must be community (not
  private/restricted). A private group's post is denied.
- **GroupEvent / Calendar** — the component/group visibility must be
  community.
- **Page** — the page's audience must include the public/community
  surface.
- **Announcement** — announcements are community-wide by design (no
  extra filter needed).
- **Todo / Board / Project** — check the item's audience setting;
  group-scoped items are denied.

**The simplest correct implementation:** in `KioskItemCatalog.ResolveAsync`,
when `roles` contains `"kiosk-link"`, pass an **empty** role set (or a
`{"kiosk-link"}`-only set, never `Member`) to the per-type read seams,
**and** do not include the owner's group memberships in the standing
check. The existing read seams already enforce "can this actor see this
item?" — with no `Member` role and no group membership, group-private and
audience-restricted items are denied by the existing logic. You are not
adding a new access check; you are **reducing the standing** the existing
check runs under.

If a particular item type's read seam does not distinguish "no roles" from
"Member role" (i.e., it would still pass for the owner even with empty
roles because the owner authored it), add a **targeted community-visible
guard** for that type: check the item's audience/visibility field directly
and return `KioskResolvedItem.Hidden` when it is not community-wide. Note
this in the comment as the "community-visible pin" for that type.

**What NOT to do:**
- Do **not** add a new `AccessVia` enum value (the plan card's "audit
  `Via` = kiosk link" is the *authentication* marker, R16's role value —
  the item reads audit under `Via: Owner` as the ADR 0302 D4 says).
- Do **not** change the `KioskResolvedItem` shape or the
  `DeniedSlide` logic in `KioskController.Classify` — the
  `Hidden` → `DeniedSlide` path already works; R17 just makes
  `ResolveAsync` return `Hidden` for non-community items in kiosk-link
  mode.
- Do **not** touch the personal-kiosk path — the filter applies **only**
  when `roles` contains `"kiosk-link"`. A personal-kiosk viewer (the
  owner's own session) sees the same items as before.

**Tests** in `tests/Kumunita.Web.Tests/` — a `KioskLinkCommunityFilterTests`
class (or add to the existing `KioskControllerTests` if that's the more
natural home):

1. **Group-private item → denied slide** (the plan card's "Done when"
   verbatim): a `GroupPost` in a private group, resolved in kiosk-link
   mode → `KioskResolvedItem.Hidden` → `DeniedSlide`.
2. **Community-visible item → visible**: a `CommunityPost` with community
   audience, resolved in kiosk-link mode → `Visible = true`,
   `Href` set.
3. **Personal-kiosk unaffected**: the same group-private item, resolved
   in personal-kiosk mode (owner's `Member` role present) → `Visible =
   true` (the owner can still see their group's items on their own
   kiosk).
4. **No-role item → denied**: a `Todo` / `Board` in a group-scoped
   component, resolved in kiosk-link mode → denied.
5. **Announcement → visible**: an `Announcement` (community-wide by
   design), resolved in kiosk-link mode → visible.

Follow the existing `KioskControllerTests` harness for how to boot the
host + substitute `IKioskItemCatalog`.

## Scope rules

- **Do not touch** `KioskLinkService` / `IKioskLinkService` / the R16
  middleware / the R18 settings surface. R17 is the **filter in
  `KioskItemCatalog.ResolveAsync`** (+ the targeted per-type guards) +
  tests only.
- **Do not start R18–R19.** The plan card's "Done when" is the only
  acceptance bar.
- **One commit** (the house rule): the filter + any per-type guards + the
  tests land in **one** `feat(ux):` commit
  (`feat(ux): kiosk-link community-visible filter — group-private items
  render the denied slide (IMPROVE-UX R17)`).
- **New strings go to en/de/fr/da** — but R17 reuses the existing
  `kiosk.slide_denied` key (the denied shape), so no new localized strings
  are expected. If you add one, add it to all four languages.

**Before committing:** run the narrowest tests via the in-process runner:
`KioskLinkCommunityFilterTests` (your new class) + `KioskControllerTests`
(the existing kiosk surface intact) + `KioskLinkServiceTests` (the R15 Core
seam) + `KioskLinkMiddlewareTests` (the R16 auth seam), and `dotnet build`.
If any test fails, read the error, adjust, continue.

Then report: which item types needed a targeted community-visible guard
vs. which were covered by the empty-role reduction, the test class name +
number of tests, the commit hash, and any `// TODO ❓` open questions for
R19's SEC review. Do not commit unrelated work.
