# ADR 0026 — Group & community name/description translations

Status: Accepted
Date: 2026-09-20

## Context

ADR 0022 added a **user-added translation** lane for *posts* and *replies*:
a human authoring a translation of an existing piece of UGC into another
supported language. That lane is deliberately scoped to a post/reply's
title + body — **not** to the names of the things that organize them.

ADR 0021 (the `Translator` role) carries the same scope boundary, and names
it explicitly:

> **Scope boundary — community names are not in it.** … Community / group
> names are `Component` documents set in the Admin panel (a UserInfo
> concern), **not** `TranslationResource` / `LocalizedPage` rows — so a
> `Translator` does not edit them. **Extending translation to those names is
> a separate feature, not part of this role.**

And ADR 0005 §C keeps out of scope "translating user-generated content … a
separate, later feature." A group's name and description, and a community's
(name and description), are exactly such content: they are authored on the
resident surface (a group's name/description by its owner, ADR 0009) or by a
GlobalAdmin (a community, the `/admin` lane), and rendered as written. Today
a resident whose preferred language is not the language a group or community
was named in still has to read it in the original.

This ADR is the "separate feature" both ADRs deferred. It gives a group and a
community the same **user-added translation lane** posts and replies have —
a translation of the **name** and the **description** into another supported
language — so a resident sees the group/community name they know, in their
language, without any of the control-plane standing.

## Decision

- **Two new documents, one per parent kind**, in `Kumunita.Core.UserInfo`
  (the context that owns `Group` and `Component`):
  - `GroupTranslation` — `GroupId`, `LanguageCode`, optional `Name?`,
    optional `Description?`, `AuthorId`, `Created`.
  - `CommunityTranslation` — `ComponentId`, `LanguageCode`, optional
    `Name?`, optional `Description?`, `AuthorId`, `Created`.

  Both are registered in `M1DocTypes` with a `UniqueIndex` on the business-key
  pair — `(GroupId, LanguageCode)` / `(ComponentId, LanguageCode)` — the same
  convention as `GroupMembership` / `ComponentMembership` (surrogate `Id` is
  the Marten identity, the pair is the DB-enforced business key). A
  group/community carries **at most one translation per language**; adding a
  translation for a language that already has one **overwrites** that row (same
  standing, same actor). Like a `PostTranslation`, they carry **no own
  Audience** and **no own moderation lane** — the row's visibility inherits
  the parent's reach (a group's owner∪member gate, a community's enabled
  visibility).

- **Add-only lane, at-least-one field.** There is no edit or remove of a
  translation row — the supported surface is "add a translation in a language
  that has none yet" (the unique index permits re-saving a language; there is
  no separate "edit" or "delete" seam, matching ADR 0022's add-only shape).
  Unlike a post (which requires a non-blank body), a name/description
  translation allows either field to be blank — a neighbor may translate just
  the name ("Bike owners" → "Wolontariusze rowerów") or just the description.
  But **at least one** of `Name` / `Description` must be non-blank, or the
  write is an `ArgumentException` (a translation with nothing in it is not a
  translation).

- **Standing to add** is owned in Core (`UserInfoService`), per ADR 0006-D
  ("the service owns the decision, not the Web"):
  - **Group lane** — the group's **owner** (an `AccessVia.Owner` audit tag),
    **∪ GlobalAdmin** ∪ **Translator** (both an `AccessVia.Admin` audit tag).
    The group's **member** is *not* a standing: membership is not the right to
    rename the group for others (a member may translate their own *post* in the
    group, ADR 0022's author lane, but the group's *name* is the owner's
    artifact, ADR 0009's owner∪GlobalAdmin edit standing).
  - **Community lane** — **GlobalAdmin ∪ Translator** (both `AccessVia.Admin`).
    A community has **no owner** — its name/description is written by a
    GlobalAdmin (the `/admin` create/update lane, `Via: Admin`), so the
    component-moderator standing does not qualify (a moderator governs a
    community's *members*, ADR 0012, not its name — the same
    "the moderator governs its members, not its standing" rule as the
    mandatory toggle).
  - `Translator` is included in both lanes: this is the explicit realization of
    ADR 0021's scope-boundary deferral — the `Translator` reaches the
    *platform* surface (UI strings + static pages); extending that reach to the
    *UGC names* they would otherwise be unable to touch is what this feature is.
    A component-moderator claim never qualifies, on either lane.
  - Standing is resolved from the row's own facts (lane + the parent's
    owner / the actor's role set), never from a UI affordance — the form's
    visibility is a consequence of the same standing call, not an independent
    gate. Two public display probes mirror `PostService.CanAddTranslation`:
    `UserInfoService.CanTranslateGroup(ownerId, actorId, actorRoles)` and
    `UserInfoService.CanTranslateCommunity(actorId, actorRoles)`.

- **Reads inherit, writes audit.**
  - **Reads** (`GetGroupTranslationsAsync`, `GetCommunityTranslationsAsync`)
    are plain document reads with **no authorization gate and no audit row** —
    the row is only ever rendered on a page that already required the parent's
    reach (the group's owner∪member gate, the community's enabled visibility),
    so a dedicated `Can`/audit would be a redundant second decision (the
    `PostTranslation` read's "a read, not a decision" pin, C-M3·1 carried
    over).
  - **Writes** (`AddGroupTranslationAsync`, `AddCommunityTranslationAsync`)
    each resolve standing (→ `UnauthorizedAccessException` when denied) and,
    on success, write **one hand-written `AccessAudit` row** in the same
    session, single `SaveChangesAsync` (C3):
    - `grouptranslation.add`, `TargetKind = "group"`, `TargetId = <groupId>`
    - `communitytranslation.add`, `TargetKind = "component"`,
      `TargetId = <componentId>`
    - `Via` = `Owner` (group owner) or `Admin` (GlobalAdmin / Translator);
      `ActorId` / `EffectivePrincipalId` = the actor.

- **Web surface — the read side shows what's there; the write side offers
  what's missing.** Mirrors the ADR 0022 post/reply render exactly:
  - The **group detail** page (`/groups/{id}`) and the **community**
    translation surface render: *(ADR 0053 — the community half moved off the
    manage page to its own `/community/translations/{id}` page; the group
    half is unchanged on `/groups/{id}`.)*
    - the **available translations** as a chip per language, each expandable
      to its (optional) name + description — the "show which translations are
      available" half;
    - for each **supported language lacking a translation row**, an "Add a
      `<language>` translation" form (optional name, optional description,
      at least one required) — shown **only when the actor has standing** for
      that lane (the "an option to add a translation should be shown too" half),
      scoped exactly to the standing matrix above.
  - **Supported languages** are the *enabled* `LanguageCatalog` rows ordered
    by `SortOrder` (the same source `kumunita.enabled_languages` reads — the
    same `SeedLanguagePickerAsync` helper the post/group-post surfaces use); a
    disabled or non-seeded language never appears as an option.
  - The new UI labels (heading, "add a … translation", field labels, save)
    are added to the **closed** `KnownTranslationKeys` registry — the seeder
    materializes their `en` rows and the provider floor covers `en`, so no new
    provider seam is needed. The translated **values** (the name/description
    text a neighbor writes) are **never** registry keys — they are UGC
    translation rows, rendered as written (the M·3 exclusion ADR 0015/0021
    both keep).

- **Render scope.** The translated name/description is offered on the
  **detail / manage** surface (where the owner or the GlobalAdmin is already
  acting). The composer's community picker, the feed's community grouping, and
  the group *list* are **out of scope** here — matching ADR 0022's
  detail-view scope, a name on a list row staying in the authored language
  until its own per-language render lane is a later decision (see "Revisit
  when"). A resident's **preferred** language is what their browser already
  uses for the UI (ADR 0005); whether a *list* should surface a group's name in
  that language is a separate, larger render decision.

## Consequences

Positive
- A group or a community can now carry a human-authored translation of its
  name and description into any enabled language, visible to everyone who can
  already reach the parent — no new audience, no new moderation lane, no
  machine-translation (ADR 0005 §C stays intact: this is *user-authored*, not
  *machine* translation).
- `Translator` finally has the reach ADR 0021 deferred: it can now translate
  the *names* it is forbidden to set — the scope boundary becomes a shipped
  feature instead of a permanent wall.
- Standing is decided in Core and reused for both the decision and the UI
  affordance (the two `CanTranslate*` probes delegate to the same resolver the
  write lanes use), so the "who can add" rule cannot drift between the form's
  visibility and the server's enforcement.
- Audit-by-default is preserved for writes (each add is a logged, attributed,
  standing-tagged row), while reads stay as cheap as the `PostTranslation` read
  they mirror.

Negative / accepted risks
- **Add-only** means there is no way to *correct* a bad translation after the
  fact other than overwriting the same language — and no way to remove one
  entirely. Accepted for the single-neighborhood trust model; a remove/correct
  lane is the natural next step if it's ever needed (see "Revisit when").
- A group's **owner** (and, on both lanes, a `Translator`) can add a
  translation of a name they did not author. Accepted — a translation is a
  *new* artifact in a language the actor speaks, not an edit of the owner's
  text, and the row is attributed to the actor on the audit line. A plain
  group **member** still cannot (membership ≠ the right to rename for others).
- The two new documents are a real schema delta. It is applied idempotently at
  boot by Marten (`ApplyAllConfiguredChanges`), the seeder is unaffected (no
  seed rows — UGC translation rows are never seeded, ADR 0022 precedent), and
  the new UI keys are added to the closed `KnownTranslationKeys` registry, so
  no new provider seam is needed.
- The name in a **list** (group list, community feed grouping) is still shown
  in its authored language — only the detail / manage surface renders a
  translated name. A per-language list render is the accepted deferral.

## Revisit when

- A **remove** or **correct** lane is wanted for a bad translation — that is a
  new seam (an `Update*TranslationAsync` / `Delete*TranslationAsync` pair), not
  a change to this add-only shape.
- A **list** surface (the group list, the feed's community grouping, the
  composer's community picker) should show a name in the resident's preferred
  language — then the detail-page-only render scope breaks and the name needs a
  per-language resolution at read time (a `TranslationProvider`-style seam), a
  much larger change than the detail-surface lane this ADR ships.
- A **community** should have an *owner* standing (rather than
  GlobalAdmin∪Translator) — then the community lane's standing matrix gains an
  `Owner` branch, which is a new decision (communities have no owner today).
