# ADR 0041 — Page audience scope dropdown: "All residents" as a first-class flag

Status: Accepted
Date: 2026-09-17
Amends: none (additive). Builds on the frozen base of **0006** (the
decision order, the empty-audience-denies invariant C1, the `CanAsync` /
`CanSeeAsync` frozen signatures), **0036** (the `Audience.Community` flag
+ the 4th decision branch), and **0039 §3.4** (pages' audience editor is
the M2 reusable `AudienceEditorModel` + `CommunityId` — the shape this
ADR retires in favor of a top-level `Scope` dropdown).

## Context

The page composer's "visibility" card (ADR 0039 §3.4, amended 2026-09-17
for ADR 0036's community default) was a three-part form:

1. A **Public** switch (off by default — the page is resident-only by
   default, matching posts).
2. A **Community** `<select>` (the page's `ComponentId` — the target of
   the audience's `Community` flag, if any).
3. A detailed **audience editor** (Mode radios + CommunityVisible
   checkbox + grant pickers for users/groups).

This is the correct shape for the *post* lane (a community post is a
common case, the explicit grants are a tail), but for the *page* lane it
is the **wrong default**. Pages are a smaller, more deliberate surface:
the three most common page scopes are (a) *every signed-in resident*
("our about page", "our bylaws", "our help center"), (b) *members of a
specific community* ("the Safety committee's notes", "the Garden
plot's watering schedule"), and (c) *explicitly granted users/groups*
("a board memo for these three people"). The composer's form inverts the
granularity for (a) and (b): the author must toggle *off* the Public
switch, *leave* the Community `<select>` at "No community (flat)",
*uncheck* the CommunityVisible checkbox (or set it to the right
community), and *not* touch the grant pickers — to express the most
common case ("all residents"). That is four deliberate *don'ts* for the
*default*.

The audience model already has the primitives (ADR 0001-B + ADR 0036):
an `Audience` is a grant list + a combine mode + a `Community` flag.
What it lacked was a **first-class "all residents" grant** — a way to say
"any signed-in reader, no community required" without either (i) leaving
the audience null (which is the *public* shape — world-readable,
unauthenticated included), or (ii) enumerating every resident's id into
the grant list (impossible at write time, and the wrong shape — audience
grants are *specific* user/group ids, not a set). It needed to be a
**distinct grant**, independent of the list, like `Community`.

## Decision

- **`Audience.AllResidents` is a new first-class flag on the `Audience`
  document, a *distinct grant* — not a mode of the `Grants` list, not a
  value of `AudienceMode`.** `public bool AllResidents { get; set; }`
  (default `false`, so existing pages / posts / announcements keep their
  behavior verbatim — ADR 0001-B's verbatim-write contract is untouched;
  the flag simply round-trips). A `true` flag with an **empty** `Grants`
  list is the "all residents" shape. The flag and the grant list are
  **independent**: a resource can be all-residents-visible *and* carry
  explicit user/group grants (the union is the visible set). It is
  *not* an `AudienceMode` value — the mode (Any / All) still governs how
  the *grant list* combines, exactly as before.

- **The empty-audience-denies invariant (C1) does not apply to the
  AllResidents branch.** C1 is about the *grant list* being empty
  (vacuous truth over `All` mode would make an empty-`All` resource
  world-readable). The AllResidents flag is a *separate* grant that is
  checked by its own branch, so an `AllResidents: true` + empty-grants
  resource is **not** world-readable — it is visible to exactly the set
  of signed-in actors (a *smaller* set than the world), and denied to
  anonymous. The C1 guard in `EvaluateAudience` (the public static test
  seam) is therefore deliberately **unchanged**: it only looks at the
  grant list, and the AllResidents branch is a *different* branch it
  never sees.

- **`Decide()` gains a 4.5 branch (owner → moderation → break-glass →
  community → **all-residents** → public → grant-match → deny), between
  the existing Community branch (4) and the Public branch (5).** It
  fires when **both** hold:
  1. `target.Audience` is non-null **and** `target.Audience.AllResidents`
     is `true`;
  2. `actorId` is **non-empty** (the actor is signed in — the same
     "signed-in" signal the Community branch uses, via the actor-scoped
     standing pattern).
  On a match it allows via `AccessVia.Resident` (or
  `AccessVia.Delegation` when acting under an in-scope grant). It is
  actor-scoped (the *actor's* own signed-in-ness, like break-glass /
  moderation / community), not effective-principal-scoped — a delegate
  does not inherit the delegator's resident standing for this branch.
  **No `ComponentId` is required** — the flag is the whole decision,
  unlike the Community branch which needs a non-null `ComponentId`
  to short-circuit.

- **`AccessVia.Resident` is a new additive enum value**, the 11th in
  `AccessVia` (after `Owner` / `Audience` / `Delegation` / `Moderator` /
  `Report` / `BreakGlass` / `Admin` / `Group` / `Guardian` / `Community`),
  following the M1 `Admin` / ADR 0013 `Group` / ADR 0028 `Guardian` /
  ADR 0036 `Community` **append** precedent: an additive value, the ten
  frozen values untouched. It is the audit tag for "this decision
  allowed because the resource is all-residents-visible and the actor
  is signed in" — the "who did this, by what right" query the audit log
  exists to answer now has a distinct answer for the common page case.

- **The page composer's form is restructured around a top-level `Scope`
  dropdown.** The `PageComposeViewModel.CommunityId` field (a
  `<select>` of enabled components) is **retired** and replaced by
  `PageComposeViewModel.Scope` (a string: `"AllResidents"` | a component
  id | `"Individual"`). The `<select>` options are:
  1. **All residents** (value `"AllResidents"`, the **default** for a
     fresh composer) — the new branch 4.5.
  2. **Each enabled component** (value = the component id) — the
     existing branch 4 (the `Community` flag + `ComponentId` pair).
  3. **Individual access** (value `"Individual"`) — reveals the detailed
     audience editor (Mode radios + grant pickers) for the explicit
     user/group grants case.

  The Public switch is retained (a public page is world-readable, no
  scope — `Audience = null` + `ComponentId = null`). When Public is **on**,
  the Scope dropdown and the audience editor are both hidden (the public
  branch wins unconditionally). When Public is **off** and Scope is
  `"AllResidents"` or a component id, only the dropdown is shown (the
  editor is hidden — the controller writes the scope directly, not
  through the editor). When Public is **off** and Scope is
  `"Individual"`, the editor is revealed (the author picks Mode +
  grants).

- **The write-side mapping is a single `ResolveAudience(model)` helper**
  on the controller, called once per POST (the single-source pin). The
  four cases:
  | Form state | Stored `(Audience, ComponentId)` |
  |---|---|
  | `IsPublic = true` | `(null, null)` — world-readable |
  | `Scope = "AllResidents"` | `(Audience { AllResidents = true }, null)` |
  | `Scope = <component id>` | `(Audience { Community = true }, <id>)` |
  | `Scope = "Individual"` | `(model.Audience.BuildAudience(), null)` |

  The **read-side** inverse (`DeriveScope(page)`) runs on the Edit GET:
  `Audience = null` ⇒ `IsPublic = true`; `Audience.AllResidents` ⇒
  `"AllResidents"`; `Audience.Community` + non-empty `ComponentId` ⇒
  that component id; anything else (explicit grants) ⇒ `"Individual"`.

- **The editor's `CommunityVisible` checkbox is retired for the page
  composer.** The `AudienceEditorModel.CommunityVisible` property
  remains (the post lane still uses it — ADR 0036's contract is
  untouched), but the page's `_PageForm.cshtml` no longer renders it
  (the Scope dropdown is the single source for community scoping on
  pages). The `AudienceEditorModel.AllResidentsVisible` property is
  **added** (a simple `bool` checkbox, like `CommunityVisible`) — the
  editor's own round-trip of the flag (the `FromAudience` /
  `BuildAudience` seam carries it verbatim, the single-source pin).

- **The existing page audience semantics are preserved.** A page that
  was `Audience = null` (public) stays public. A page that was
  `Audience { Community = true }` + `ComponentId = "x"` stays scoped to
  community `x`. A page that was `Audience { Community = true }` +
  empty `ComponentId` (the pre-ADR-0041 "all residents" shape, if any
  exist) now round-trips to `Scope = "Individual"` (the `DeriveScope`
  fallback) — a deliberate behavior change: the pre-ADR-0041 "all
  residents" was an *inert* community branch (no component id to
  match), and the post-ADR-0041 equivalent is the explicit
  `AllResidents` flag. Any such page should be re-saved with
  `Scope = "AllResidents"` to activate the new branch.

- **The post / announcement / group-post lanes are structurally
  unaffected.** They do not set `AllResidents = true` (the flag
  defaults `false`), and their `Decide()` branches (the post lane's
  branch 4, the announcement lane's `AnnouncementScope` enum, the group
  lane's ADR 0013 membership lane) are untouched. The new branch 4.5 is
  inert for any resource whose `Audience.AllResidents` is `false`
  (the default).

- **The form's `<select>` is the single source of truth for the page's
  scope.** The Scope dropdown, the Public switch, and the audience
  editor are **mutually exclusive** in their effect on the stored
  shape: Public wins (null audience), then Scope wins (a non-null
  audience with the right flag), then the editor wins (a non-null
  audience with the explicit grants). The JS toggle logic
  (`_PageForm.cshtml`'s `refresh()` function) enforces this on the
  client side (a pure CSS-class flip, no server-side re-render — the
  ADR 0036 posts pattern).

## Consequences

- **The page composer's common case is now a single dropdown pick.**
  A new page defaults to "All residents" (the branch 4.5 shape) — the
  author does not need to *do anything* to express the most common
  page scope. Narrowing to a community is one dropdown change; opening
  to explicit grants is one dropdown change. The pre-ADR-0041 shape
  (toggle Public off, leave Community at "flat", uncheck
  CommunityVisible, don't touch the grants) is gone.

- **The audit trail gains a new `AccessVia.Resident` value.** The
  "who did this, by what right" query now distinguishes "the actor is
  a signed-in resident and the page is all-residents-visible" from
  "the actor is a member of the target community and the page is
  community-visible" (ADR 0036) and from "the actor is on the explicit
  grant list" (the MatchGroups branch). This is the page-lane analog of
  ADR 0036's `AccessVia.Community`.

- **The `PageComposeViewModel.CommunityId` field is removed.** Any
  downstream consumer (a test, a view, a partial) that referenced it
  must be updated to use `Scope` instead. The `Components` picker
  options list (a `[BindNever]` field) is retained — the Scope
  dropdown still renders from it.

- **The `AudienceEditorModel.AllResidentsVisible` property is added.**
  The `FromAudience` / `BuildAudience` seam now round-trips the flag
  verbatim (the single-source pin). The post lane's existing
  `CommunityVisible` round-trip is untouched.

- **The `_PageForm.cshtml` visibility card is restructured.** The
  Public switch, the Scope dropdown, and the audience editor are three
  visually distinct blocks, with JS toggle logic enforcing the mutual
  exclusivity. The pre-ADR-0041 shape (a single "audience block" with
  a Community `<select>` + Mode radios + CommunityVisible checkbox +
  grant pickers) is gone.

## Rejected alternatives

- **Leave the form as-is, just add an "All residents" option to the
  Community `<select>`.** Rejected — the Community `<select>` is
  semantically "the target community of the `Community` flag", not
  "the scope of the page". Adding an "All residents" option to it
  would be a *lie* (it would set `Community = true` + `ComponentId =
  null`, which is the *inert* shape — the branch 4 short-circuits on
  a null `ComponentId`). A top-level `Scope` dropdown is the honest
  shape.

- **Use `AudienceMode` for the new flag.** Rejected — `AudienceMode`
  governs how the *grant list* combines (Any / All). The
  `AllResidents` flag is a *distinct grant*, not a mode of the grant
  list (the ADR 0036 precedent). Making it an `AudienceMode` value
  would collide with the C1 invariant (empty-list semantics) and with
  ADR 0006's "the grant list is the explicit set" contract.

- **Retire the audience editor entirely.** Rejected — the "Individual
  access" scope still needs it (a board memo for three specific people
  is a legitimate page scope). The editor is retained, but only
  revealed when the author explicitly picks "Individual access".

- **Make "All residents" a *default* on the stored `Audience` (i.e.
  flip the flag `true` for every new page's audience).** Rejected —
  the flag is a *first-class grant* the author opts into, not a
  bootstrap default. A page that was `Audience { Community = true }`
  + `ComponentId = "x"` should *stay* community-scoped, not become
  all-residents-visible. The default is a *form* default (the Scope
  dropdown's initial selection), not a *stored-shape* default.

## Migration

The `Audience.AllResidents` flag is a new property on an existing
document type (Marten's versioned schema). It is **additive**: existing
documents round-trip with the flag `false` (the .NET default), so no
data migration is required. The `PageComposeViewModel.CommunityId`
field is removed from the *view model* (the form-bound shape), but the
`Page.ComponentId` document field is **retained** (the stored shape
still needs it for the community scope). The `_PageForm.cshtml`
visibility card is restructured (the Scope dropdown + the
conditional editor); the pre-ADR-0041 form shape (a Community
`<select>` + a CommunityVisible checkbox) is gone.
