# ADR 0118 — M17: Bookmarks (save posts, events, todos, etc. for quick
personal access)

Status: Accepted
Date: 2026-09-29
Extends the **M9 participant-by-id precedent** (`Kumunita.Core.Messaging`
— the owner/participant read is a **personal read** with **zero audit
surface**, the non-owner 404 commits **no row** — ADR 0105), the **M5
Projects bounded-context precedent** (a standalone core surface on its
own context + `*DocTypes` surface, always available, **no**
off-by-default toggle — the D6 inverse of the ADR 0101/0105
`LocaleSettings` shape), the **ADR 0004 §B.1 delta-detected, idempotent,
zero-migration document-surface shape** (the new `M17DocTypes` surface),
the **M9 `convo_uidx_pair` unique-index-as-witness idiom** (the
`bm_uidx_owner_target` index on `(OwnerId, TargetKind, TargetId)`), the
**frozen `IAuthorizationService` seam** (the 4-method surface + the
`IDocumentSession` overloads, C3 — ADR 0006 §A), the **frozen
`AccessAction` / `AccessVia` sets + the frozen `Decide()`** (ADR 0006 —
"M17 adds **no adapter at all**" pin), the **M16 D5 write-standing
posture** (ADR 0117 — M17's D3 visibility check reuses the target's
*own* frozen `CanAsync(Read)`, not a new standing probe), the **M5
`KanbanStatuses` string-not-enum shape** (the closed `TargetKind` set),
the **M16 create-gate posture** (Deny ⇒ 404, the Deny row does not
survive), and the **closed `KnownTranslationKeys` registry + the
en/de/fr/da parity pins** (ADR 0015 / ADR 0052). This ADR makes
**a resident's quick-access list real** (the ARCHITECTURE.md M17
value-chain row, verbatim): **save posts, events, todos, etc. for quick
personal access** — a standalone core surface over **one document, one
new surface, one service seam, and zero new authorization surface**
(C-M17·1), with **zero migrations for existing surfaces** (D1).

## Context

The neighborhood today has everywhere to **find** content — feeds,
search, the calendar — but nowhere to say *these things are mine to come
back to*. The README roadmap row names M17 exactly: "**Bookmarks: save
posts, events, todos, etc. for quick personal access.**"

M5 (Projects) already proved the core-surface shape M17 copies: a
**standalone core surface** on its own bounded context, its own
`*DocTypes` Marten surface (the ADR 0004 §B.1 parallel-surface shape —
delta-detected, applied idempotently at boot, **zero migrations for
existing surfaces**), always available with **no** off-by-default
toggle. M9 (messaging, ADR 0105) adds the **primary** precedent: a
**participant-by-id record** whose access story is **identity
comparison**, never an audience decision — the owner's own read is a
**personal read** that **never audits** ("reads never audit"), and a
non-participant — an operator included — gets a **non-leaky 404** that
commits **no** `AccessAudit` row. The M9 `convo_uidx_pair` precedent
adds the **unique-index-as-witness** idiom the `M17DocTypes` unique
index on `(OwnerId, TargetKind, TargetId)` copies (F1 — a second
bookmark of the same target is a no-op **at the DB layer**).

The constraint that shapes the decision is the same one that shaped M5 /
M9 / M16, and sharper: **compose the frozen seams, never extend them —
and M17 extends them by adding nothing** (C-M17·1) — no new `AccessAction`,
**no** new `AccessVia`, **no** branch in `Decide()`, **no** method on
`IAuthorizationService`, and **no adapter at all** (D2 — a bookmark is a
*personal-by-id* record, the ADR 0105 shape, not an audience decision).
The **one rule that keeps M17 safe** (D3): you may only bookmark
**what you can see** — the write lane re-runs the target's **own**
frozen `Read` decision (the *same* `CanAsync(Read)` the detail page
uses) and refuses **404** when the target is not visible; a bookmark
**grants nothing**. The surface stays **core** (D6) — M17 is a
**milestone**, not a **lane** (the ADR 0101/0105 `LocaleSettings`
toggle shape **inverted**: there is **no toggle at all**).

## Decision

**D1 — One document, one new surface, one new bounded context.** M17
adds a new bounded context `Kumunita.Core.Bookmarks` with **one
document**: `Bookmark` (`OwnerId`, `TargetKind`, `TargetId`,
`Created`), registered on a **new `M17DocTypes`** surface (the M9/M5
`*DocTypes` precedent — a parallel additive surface, delta-detected and
applied idempotently at boot, **zero migrations** for existing surfaces;
the **unique** index on `(OwnerId, TargetKind, TargetId)` — the F1
idempotency witness, the M9 `convo_uidx_pair` shape — + a non-unique
`(OwnerId, Created)` list-ordering index). `TargetKind` is a **string**
restricted to the closed set {`post`, `event`, `todo`,
`announcement`, `page`} — the exact vocabulary the existing
`*ToAuditableResource` adapters already emit (the M5 `KanbanStatuses`
string-not-enum shape). **No** `Audience`, **no** `IsDeleted`, **no**
`Modified`, **no** `ComponentId`. *Forbids:* a separate document per
surface; a `Bookmark`-per-kind set of tables; an enum type for
`TargetKind`; a migration of an existing surface.

**D2 — A bookmark is a personal-by-id record, never an audience
decision.** M17 adds **no adapter** (there is no
`BookmarkToAuditableResource`), **no** new `AccessAction`, **no** new
`AccessVia`, **no** branch in `Decide()`, **no** method on
`IAuthorizationService` (the M9 ADR 0105 shape — a *participant-by-id*
/ *owner-by-id* record, not an audience decision). The **owner's** own
list is a personal read (like the M6 inbox / M9 conversation):
`Where(b => b.OwnerId == ownerId)`, **no** `CanSeeAsync` pass over the
bookmark rows, **no** `AccessAudit` row (C-M17·2). *Forbids:* a
`BookmarkToAuditableResource`; running the owner's own bookmark list
through `IAuthorizationService`; a new `AccessAction`/`AccessVia`.

**D3 — Bookmarking requires visibility; bookmarking grants nothing.**
The write lane (bookmark) **re-runs the target's frozen `Read`
decision** (`CanAsync(Read)` over the target's existing adapter — the
*same call the detail page uses*) and **refuses 404** (not 403 — the
Deny row **does not survive**, the M16 create-gate posture) when the
target is not visible. The bookmark row itself **never** appears in any
`CanSeeAsync` pass, never widens an audience, and never confers
standing. *Forbids:* a bookmark row that lets an invisible target be
bookmarked; a bookmark consulted by any read decision anywhere; a
`GlobalAdmin` peek branch into the target's visibility on the write
lane (the detail page's own decision is the truth).

**D4 — One row per (owner, target); toggle = idempotent add / remove.**
The unique index on `(OwnerId, TargetKind, TargetId)` is the witness:
a second bookmark of the same target is a **no-op** (the button
reflects "bookmarked"); unbookmark removes the row. **No** `IsDeleted`
flag — removal is physical (a bookmark is a *pointer*, not content:
there is nothing to preserve, and a re-bookmark after removal is a
fresh `Created` timestamp). *Forbids:* an `IsDeleted` soft-delete on
`Bookmark`; a second row for the same (owner, target); mutating
`Created`.

**D5 — The list degrades, never leaks.** The bookmark list resolves
each row by `TargetKind` + `TargetId` against the owning surface's read
seam; a row whose target is **absent, soft-deleted, or no longer visible
to the owner** renders as a **generic degraded label** (the closed
`bm.list.degraded` `kw-l` key: "No longer available") with **no title,
no link, no id** — and the row remains removable (the unbookmark button
still works on the degraded row — it is keyed on the row's own id, no
target read at all). One list load, **zero** new authorization branches
(C-M17·2). *Forbids:* a 404/403 on a dangling bookmark row; leaking a
removed target's title; a per-row `CanSeeAsync` branch in the list
beyond the owning surface's existing seam.

**D6 — M17 is a core surface, not a lane: no off-by-default toggle.**
M17 is a standing core surface (like M5 Projects): **no** off-by-default
admin toggle, **no** `LocaleSettings`-style admin switch, **no**
`KumunitaFeature` admin flag. It is always available to the community.
(M9's off-by-default toggle was a *lane*; M17 is a *milestone* — the
ADR 0101 admin-toggle shape **inverted**.) *Forbids:* an off-by-default
admin toggle for M17; a `LocaleSettings`-style switch; an admin flag
gating the surface.

**D7 — No notification lane in M17 (deferred).** M17 composes **only**
the frozen `IAuthorizationService` (the D3 write-lane visibility check)
+ the owning surfaces' existing read seams (D5 list resolution) over the
host-registered Marten `IDocumentStore`. It does **not** take an
`INotificationService` emitter and does **not** register a Wolverine
handler. The "nudge the author on bookmark" lane is **deferred** — a
follow-on lane with its own ADR. *Forbids:* an `INotificationService`
in the `BookmarkService` composition; a Wolverine handler for
bookmarks.

**D8 — The close flip is one atomic unit (U05).** `Milestones.cs` M17
`StatusNext` → `StatusDone` + M18 `StatusPlanned` → `StatusNext`;
`MilestonesTests.cs` re-pinned to
`M18_Is_The_Single_InProgress_Milestone` + the shipped list gains M17 +
M19 stays planned; the README **Roadmap** (M17 → done, M18 → in
progress) + the M17 **Status** line; `docs/STATUS.md`; and
`docs/ARCHITECTURE.md`'s value-chain table gains the **M17 row** (the
table currently ends at the M16 row). All four surfaces ship **in the
same unit** (the M15/U10, M16/U06 docs-parity precedent, C-M17·7).
*Forbids:* reordering M18/M19; editing a shipped milestone's text;
breaking the `MilestonesTests` pins; flipping one surface and not the
others.

## Consequences

**Positive:**

- **A resident's quick-access list is real** (the README M17 row,
  end-to-end): a resident pins a post, event, todo, announcement, or
  page from the detail surface's bookmark button; the pin appears in
  `/bookmarks` under its kind group; toggling again is a **no-op** (one
  row, one `Created` — the unique index is the F1 witness);
  unbookmarking removes the row (D4). A dead pin degrades to the closed
  `bm.list.degraded` label and is still removable (D5 / F4).
- **Zero new authorization surface — the strongest form** (C-M17·1):
  M17 adds **no adapter at all** (D2), **no** new `AccessAction`, **no**
  new `AccessVia`, **no** branch in `Decide()`, **no** method on
  `IAuthorizationService` — the **only** authorization call in the
  entire milestone is the D3 write-lane check, which is the **target's
  own** frozen `CanAsync(Read)` (M17 adds no decision of its own). The
  frozen seams are **byte-identical** after M17 (§drift-guard).
- **The bookmark is a subset of the visible world, never a window into
  it** (C-M17·3): a pin can only exist for a target the actor could
  see at pin time, and the pin never appears in, widens, or is
  consulted by any `CanSeeAsync` pass anywhere — a `GlobalAdmin` who is
  not the owner cannot read a resident's list (a non-leaky 404, no
  `AccessAudit` row — the ADR 0105 "operator has no read standing"
  precedent, C-M17·2).
- **Zero migrations for existing surfaces** (D1): the `M17DocTypes`
  surface is **parallel** (the ADR 0004 §B.1 shape — delta-detected and
  applied idempotently at boot) — the one new table + the two indexes
  are additive; the `M1DocTypes` … `M16DocTypes` surfaces are
  **byte-identical** after M17 (§drift-guard).
- **The leak surface of a dangling row is exactly one static string**
  (the named trade, C-M17·5): the degraded label is the closed
  `bm.list.degraded` key — no title, no link, no id — so the row
  survives its target's death (a silently-removed bookmark is worse
  than a greyed-out one) without leaking anything about it.
- **A milestone, not a lane** (D6 / C-M17·6): M17 is **always**
  available to the community (the M5 Projects shape) — **no**
  off-by-default admin toggle, **no** `LocaleSettings`-style switch —
  the surface does not depend on an admin's opt-in (C-M17·6).
- **Quiet by default** (D7): no nudge, no handler, no emission — the
  pin is private to the owner by construction, so it cannot become an
  engagement lever or a notification surface in M17.

**Neutral / cost:**

- **The named trade — the dangling-row UX vs. the leak surface**: the
  row survives its target's death (soft-deleted post, removed event,
  hidden todo) instead of being silently removed, and the invariant
  that keeps that safe is **C-M17·5** — the closed
  `bm.list.degraded` label with no title, link, or id.
- **The list resolution is one extra read per row**, bounded by the
  resident's own pin count; each row resolves through the owning
  surface's **existing** read seam (D5 — no new branch).
- **The closed `TargetKind` set is deliberately five-wide, not open**
  (D1): a new content surface joins the pin world with one vocabulary
  word + one list-resolution branch; adding a sixth kind is a **drift
  event** (the design doc's §drift-guard).

**Follow-on lanes (each its own ADR — the design doc's §deferred):** the
"nudge the author on bookmark" lane (D7); bookmark folders / manual
ordering; bookmark sharing / per-surface "top bookmarked" stats;
bookmarks on **groups** themselves (the ADR 0013 membership-lane
surfaces have their own visibility world).

## Amendments

### 2026-09-30 — D3/F2 write-lane: the announcement branch resolves the owner's real standing in-Core (the obs-4 lapse closed)

The **D3** write-lane rule — *"re-runs the target's frozen `Read`
decision … the *same call the detail page uses*"* — is amended in its
**application to the `announcement` target kind only**, and is a
**composition detail, not a new frozen surface**. The original
implementation passed an **empty** role set to
`IAnnouncementService.GetAsync` (the Bookmarks seam hands us only an
owner id — roles never cross the frozen `IBookmarkService` seam), so a
`GlobalAdmin` or community-moderator who could see a **community-targeted**
announcement on the detail page was **denied** the bookmark of it — a
direct D3/F2 violation (the write lane was re-running a *stricter* read
than the detail page uses, the exact divergence D3 forbids).

The amendment: the `announcement` branch of
`BookmarkService.ResolveTargetAsync` now resolves the owner's real
standing **in-Core** via the frozen `IIdentityService.GetBySubjectAsync`
(the ADR 0006 §A principal-read seam — the **same category of
composition** as the already-composed `IUserInfoService`, which
`GetBySubjectAsync` itself reads), and hands that thin-principal role set
to the announcement read. A `GlobalAdmin` therefore resolves a
community-targeted row (matching the detail page) instead of the old
empty-set denial. **Fail-closed shape preserved:** no EF account row (or
no principal) ⇒ empty set ⇒ the read degrades exactly as before.

**The frozen surfaces are untouched** (C-M17·1 holds): the
`IBookmarkService` **3-method public signature is byte-identical**
(`ListAsync` / `ToggleAsync` / `RemoveAsync` — roles never cross the
seam); **no** new `AccessAction` / `AccessVia` / `Decide()` branch;
**no** new method on `IAuthorizationService`; the only change is one
extra **frozen-seam** composition (the `BookmarkService` ctor gains an
eighth dependency — `IIdentityService` — and the `DependencyInjection.cs`
M17 factory passes it), exactly the category the original seven
(`IUserInfoService`, `IAuthorizationService`, `IDocumentStore`,
`IAnnouncementService`, `IPageService`, `IEventService`,
`IProjectService`) already established. Pinned by the 15th
`BookmarkServiceTests` case
`GlobalAdmin_Beats_CommunityScope_On_Bookmark_Toggle` (it **fails** under
the pre-amendment empty-roles code and **passes** under this one — the
witness). The §drift-guard frozen list in the design doc is amended to
count **15** pinned `BookmarkServiceTests` names and to name
`IIdentityService` among the frozen seams the `BookmarkService` composes.

### 2026-09-30 — obs-2: redirect-back + flash toast on the bookmark toggle

The original M17 `Toggle` action always redirected to `/bookmarks`
(the "redirect after write" precedent). This is a **Web-layer
usability gap**, not a decision-lane change: the user who clicked
"Bookmark" on a post detail page (or event, or page, or group post, or
announcement) was **bounced off the surface** they were reading, losing
their place. The F1 "button reflects bookmarked on reload" pin
(satisfied by the pre-existing `BookmarkState` ViewData exception path)
is unchanged — this amendment adds the **post-toggle confirmation** the
user was previously missing.

The amendment (the house flash-toast + redirect-back idiom — the
`AdminPortabilityController` precedent):

- **`_BookmarkButton` partial** — the model changes from
  `(string Kind, string Id)` to `(string Kind, string Id, string?
  ReturnUrl)`; a new `<input type="hidden" name="returnUrl"
  value="@Model.ReturnUrl" />` is added to the form. All six render
  sites pass the surface's own detail URL as the third element (e.g.
  `("post", Model.Post.Id, $"/posts/{Model.Post.Id}")` for the post
  detail; `("page", Model.Id, Model.Path)` for the page surface —
  `Model.Path` is the `PagePaths.Href` the view model already carries).
- **`BookmarksController.Toggle`** — the signature changes from
  `Toggle(string kind, string id)` to `Toggle(string kind, string id,
  string? returnUrl = null)`. On success (`Bookmarked` /
  `AlreadyBookmarked`), the action sets
  `TempData["info"] = await T("bm.toggle.bookmarked")` and redirects
  to `returnUrl` (or falls back to `/bookmarks` when the field is
  absent — the original M17 behavior, unchanged for callers that don't
  pass it). `Refused` → 404 (unchanged).
- **`BookmarksController.Remove`** — sets
  `TempData["info"] = await T("bm.toggle.removed")` before its
  existing `return Redirect("/bookmarks")` (the remove is initiated
  from the `/bookmarks` list — the redirect target is unchanged; the
  flash toast confirms the removal).
- **`BookmarksController` ctor** — gains two **optional** parameters:
  `ILocalizationService? localization = null` and
  `ITranslationProvider? translationProvider = null` (the
  `AdminPortabilityController` house idiom). The `T(string key)`
  private helper resolves the kw-l key to the operator's effective
  language; when either parameter is null (the test-harness floor),
  `T` returns the raw key — the tests assert the raw key directly.
- **Two new `bm.toggle.*` kw-l keys** added to all four registries
  (en / de / fr / da): `bm.toggle.bookmarked` and
  `bm.toggle.removed`. The closed `bm.*` key set is now **14 keys**
  (the original 12 + the obs-2 flash pair). The `BmKeys` array in
  `BookmarksControllerTests.cs` is extended accordingly; the
  `BmKeys_AreTheClosedTwelveKeySet` test is renamed
  `BmKeys_AreTheClosedFourteenKeySet` and asserts
  `Assert.Equal(14, actualBmKeys.Count)`. The
  `KnownTranslationKeys_ParityTests` (de/fr counts == en count) pass
  automatically since both keys are added to all four languages.

**The frozen surfaces are untouched** (C-M17·1 holds): the
`IBookmarkService` **3-method public signature is byte-identical**;
**no** new `AccessAction` / `AccessVia` / `Decide()` branch; **no**
new method on `IAuthorizationService`. The only change is in the
**Web layer** (the `BookmarksController` + the `_BookmarkButton`
partial + the six render sites) and the **key registry** (two new
`bm.toggle.*` keys × 4 languages). Pinned by the retargeted
`F1_Toggle_Idempotent_One_Row_Second_Call_Returns_AlreadyBookmarked`
(redirect to the detail URL + `TempData["info"] == "bm.toggle.bookmarked"`),
the `F4_Unbookmark_On_Degraded_Row_Still_Works` flash pin
(`TempData["info"] == "bm.toggle.removed"`), the
`M17AcceptanceGateTests.ClosedLoop` retarget (toggle → detail URL,
remove → `/bookmarks` + flash), the `BmKeys_AreTheClosedFourteenKeySet`
(14-key closed set), and the `BmButton_Partial_Form_Posts_To_Toggle_Endpoint`
structural pin (the `returnUrl` hidden input). The design doc
§drift-guard frozen list is amended to count **14 keys** in the §kw-l
key list and to carry the obs-2 drift-log row.
