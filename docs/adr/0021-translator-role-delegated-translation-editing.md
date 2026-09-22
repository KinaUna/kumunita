# ADR 0021 — Translator role: delegate translation editing to non-admin residents

Status: Accepted
Date: 2026-09-16

## Context

ADR 0005 introduced the multilingual lane: the platform's UI strings and
static pages are `mt` documents (a `TranslationResource` row per key +
language, a `LocalizedPage` row per slug + language), edited in-app under
`/admin/languages`. Until now that surface was **GlobalAdmin-only**.

That creates a real gap for a small neighborhood. The people who run the
instance (the GlobalAdmin) are frequently **not** the people who can
translate — they may not speak the language, or may be too busy to keep up
with new UI strings as features ship. Forcing translation through the admin
means the platform stays half-English, or the admin ends up delegating to a
neighbor who then has to be handed GlobalAdmin standing for something that
should not require touching the language catalog, the audit log, role
assignment, or the rest of the control plane.

So: add a **`Translator`** role. A GlobalAdmin can grant it to a resident,
and that resident can **update and add the translatable platform text** (the
UI strings and the static pages) — but holds **none** of the GlobalAdmin's
other standing. The motivation is explicitly delegation: "the admins don't
speak all the languages, or aren't interested in translating, so I want to be
able to delegate that to other users."

## Decision

- **A fourth role, `Translator`**, added to `Kumunita.Core.Identity.Roles`
  alongside `Member`, `Moderator`, `GlobalAdmin` (ADR 0003). It is a plain
  claim string on the thin principal — no new claim type, no new data
  document, no `ModeratorAssignment`-style scope (translation is
  instance-wide, not component-scoped). `FirstBootSeeder` creates the role
  row idempotently, exactly as it already does for `GlobalAdmin` and
  `Moderator`.

- **Assignment is GlobalAdmin-only, through the existing lane.**
  `IdentityService.SetRoleAsync` gains a `wantsTranslator` branch: when the
  role string is `Translator` it adds/removes the role row and folds that
  change into the `rolesChanged` security-stamp rotation. The method already
  requires the caller to be a GlobalAdmin (`RequireGlobalAdminAsync`), so
  the standing rule — only a GlobalAdmin manages roles — is preserved
  unchanged. `Translator` has no component scope; a `Translator`'s
  `ModeratorAssignment` rows are cleared, as for any non-Moderator. The Admin
  Index role dropdown gains a `Translator` option.

- **The `/admin/languages` surface is split by standing.**
  - **Catalog mutations** — add, enable/disable, reorder, set-default,
    remove — stay **GlobalAdmin-only** (each action carries its own
    `[Authorize(Roles = "GlobalAdmin")]`; the Admin Index's
    `KumunitaPrincipal.IsGlobalAdmin` gates the same views).
  - **Translation editors** — the UI-string editor (`Translations` /
    `SaveTranslation`) and the static-page editor (`PreviewPage` /
    `SavePage`) — open to **both** `GlobalAdmin` and `Translator`. The
    class-level gate on `LanguagesController` therefore admits both roles
    (`[Authorize(Roles = "GlobalAdmin,Translator")]`), and the Languages
    Index view hides the catalog controls from a non-GlobalAdmin while keeping
    the per-language "UI strings" and "Edit pages" links visible to both.

- **Audit attribution is unchanged.** Translation and page saves still write
  exactly one `AccessAudit` row with `Via = AccessVia.Admin` — the standing
  slot the codebase already pins for this lane (ADR 0005 D, the
  `translation.save` / `page.save` FACES tests). The actor's **account** is
  what differentiates a GlobalAdmin from a Translator, and it is already on
  the row as `ActorId` / `EffectivePrincipalId` — so "who saved this string"
  is audited either way. No new `AccessVia` member is added (leaving the
  `AccessVia` enum, the purge tiers, and the six existing `Via = Admin`
  test pins untouched).

- **A `Translator` gets a way in.** `_AccountNav` shows a "Translations" link
  to `/admin/languages` for a signed-in `Translator` who is not a GlobalAdmin
  (a GlobalAdmin already reaches the surface through the Admin shell). The
  label is a new registry key, `nav.translations` ("Translations"), added to
  `KnownTranslationKeys` so the `kw-l` TagHelper resolves it rather than
  falling back to the raw key.

- **Scope boundary — community names are not in it.** The translation surface
  (ADR 0005) is exactly the UI strings and the static pages. Community /
  group names are `Component` documents set in the Admin panel (a UserInfo
  concern), **not** `TranslationResource` / `LocalizedPage` rows — so a
  `Translator` does not edit them. Extending translation to those names is a
  separate feature, not part of this role.

## Consequences

Positive
- The admin can hand translation to the right neighbor without handing them
  the admin seat. The catalog, the audit log, role assignment, and every other
  control-plane action remain GlobalAdmin-only.
- It is additive and low-risk: a new role string, one branch in an existing
  method, a role-row seed, a split on one controller, and two view tweaks. No
  new claim type, no new document, no schema migration, no new `AccessVia`,
  and no change to the frozen Core seam the Web already calls.

Negative / accepted risks
- A `Translator` can overwrite any existing translation, and there is no
  per-language or per-key ownership scoping — any `Translator` edits the same
  shared rows any other can. Accepted for a single-neighborhood instance:
  translation is instance-wide, the audit row records who saved what, and the
  optimistic-concurrency (409) story already covers the "two people at once"
  case (ADR 0005 D).
- One more option in the Admin Index role dropdown and one more role to keep
  in the seeder / claim factory / `Roles` set in sync. Accepted — these are
  the same mechanical sites the other two elevated roles already live in.

## Revisit when

- Translation ownership needs to be **scoped** (per-language or per-key
  "who owns this string") — then a scope row (the `ModeratorAssignment`
  pattern) is the shape, not a blanket role.
- A community needs a **review/approval** step between a `Translator`'s draft
  and its going live (the current model is save-and-immediately-visible) —
  that is a new workflow, not a role.
