# ADR 0053 — Community translation surface on its own page

Status: Accepted
Date: 2026-09-21

## Context

ADR 0026 settled the community name/description translation **lane** — the
`CommunityTranslation` document, the standing matrix (GlobalAdmin ∪ Translator,
no owner), the add seam, the audit row, and the **read/write render** — and
ADR 0048 lifted its add-only pin (the `Update…` / `Remove…` lanes on that same
row). Neither ADR settled *where* the community's translation surface lives:
ADR 0026 §D (the "Web surface" bullet) describes it as rendered **on the
community manage page** (`/community/manage/{id}`), and ADR 0048 D5's view
affordance note likewise names `Community/Manage` as the place the edit +
remove forms live.

In practice the community manage page (`Community/Manage`) is the ADR 0012
**membership** surface: the mandatory toggle, the member list with per-row
remove, and the add-picker. The translation block was bolted on to the bottom
of that page, so a resident managing membership is also shown the community
name/description translation machinery (and, for a GlobalAdmin/Translator,
three extra forms) on a page whose stated purpose is member management.

Two concrete problems follow:

- **Cognitive:** the membership page and the translation page are different
  concerns sharing one URL and one view model. A moderator (who manages
  members but has *no* translation standing, ADR 0026 §D) still lands on a
  page that renders translation rows for them to view — the page is no longer
  *about* membership.
- **Routing:** the community write lanes had drifted from ADR 0048 D5's
  documented community shape. D5 spells the community routes as
  `/community/translations/update` + `/community/translations/remove` (i.e. a
  top-level `/community/translations` resource), but the shipped code
  nested them under the manage resource at
  `/community/manage/{id}/translations/{update|remove}`. This ADR aligns the
  code to D5's documented shape while it is already being moved.

**What this is, in one line:** the community name/description translation
surface moves off the membership page onto its own
`/community/translations/{componentId}` page (a dedicated `Translations`
view + action), with a "Translations" link in the feed header next to
"Manage members" — no standing, audit, document, or failure-shape change.

## Decision

### D1 — A dedicated `Translations` page + action

`CommunityController` gains a GET action
`/community/translations/{componentId}` that renders a new
`Views/Community/Translations.cshtml` from a new
`CommunityTranslationViewModel`. The page carries exactly what the
translation block on the manage page used to carry:

- the **available translations** as an expandable chip per language
  (name + description), the read half;
- for each **supported language lacking a row**, an "Add a `<language>`
  translation" form (ADR 0026's at-least-one rule), the write half;
- the **edit** and **remove** forms inside each row's `<details>` (ADR 0048);
- a "← Back to the feed" link and a view-only note for standing-adjacent
  viewers (a manage-standing viewer without translation standing).

The manage page (`Manage.cshtml` + `ManageCommunityViewModel`) drops the
translation block and its helpers (`langNames` / `missingLanguages` /
`LangName`) and the three view-model properties
(`CommunityTranslations` / `Languages` / `CanTranslate`) — the page is the
ADR 0012 membership surface again.

### D2 — Reaches the same people as the manage page did; admits a wider set to *view*

The translation page's **reach** is the union the manage page already
exposed: the **manage standing** (GlobalAdmin ∪ component-moderator, the
`Standing` gate) ∪ the **translation standing** (GlobalAdmin ∪ Translator,
`UserInfoService.CanTranslateCommunity`). Concretely:

- a **GlobalAdmin** or **Translator** → admitted and *may act* (add/edit/
  remove) — identical to the manage page's `CanTranslate` branch;
- a **component-moderator** → admitted but **view-only** (the add/edit/remove
  forms are not offered; the standing gate in `UserInfoService` is the real
  wall, unchanged) — identical to the manage page's behaviour for a
  moderator;
- **everyone else** → **404 `NotFound`** (fail-closed, the ADR 0008 "U10"
  shape) — identical to the manage page for an outsider.

So a resident who could see translations on the manage page can still see
them (now on their own page), and no one who could not sees them now. The
standing matrix, the `AccessVia` tag, and the failure shapes are all
**unchanged**.

### D3 — Routes align to ADR 0048 D5's documented community shape

The community write lanes move to the top-level resource D5 already
described, all still thin over the same Core seams
(`AddCommunityTranslationAsync` / `UpdateCommunityTranslationAsync` /
`RemoveCommunityTranslationAsync`):

| Lane | Route |
| --- | --- |
| GET (new page) | `GET /community/translations/{componentId}` |
| Add | `POST /community/translations/{componentId}` |
| Update | `POST /community/translations/{componentId}/update` |
| Remove | `POST /community/translations/{componentId}/remove` |

The `update` / `remove` routes were previously
`/community/manage/{componentId}/translations/{update,remove}`; the add route
was `/community/manage/{componentId}/translations`. All three now live under
`/community/translations/{componentId}`, matching D5. Success redirects go
from `Manage` to `Translations` (`RedirectToActionResult` → `Translations`).

### D4 — A "Translations" link in the feed header

`Views/Posts/Index.cshtml`'s feed header already offers "Manage members"
(gated on `FeedViewModel.CanManageCommunity`). It gains a "Translations"
link, gated on a new `FeedViewModel.CanTranslateCommunity` (computed in
`PostsController.Index` from `UserInfoService.CanTranslateCommunity`), placed
directly next to "Manage members" — the discoverability seam the request
asks for. `ManageCommunityViewModel` is untouched by this (the link is in
the feed, not the membership page, honouring "separated from the members
page").

### D5 — UI keys

The three page-level labels
(`community.translations_page_title`, `community.translations_page_lede`,
`community.translations_view_only`) are added to the **closed**
`KnownTranslationKeys` registry for all four enabled languages (en, de, fr,
da) — the provider floor covers `en` and the parity tests
(`KnownTranslationKeys_ParityTests`) pin de/fr/da to the same key set. The
row-level keys the block already used
(`community.translations_label`, `community.translation_*`,
`posts.translation_remove`) are reused as-is; nothing is renamed, so the
existing `CommunityControllerTranslationTests` and
`GroupCommunityTranslationTests` still pass.

## Consequences

- **Positive:** the membership page is about membership again; the translation
  surface has a URL and a page of its own with a discoverable link; the
  community write routes now match ADR 0048 D5's documented shape instead of
  diverging from it.
- **Neutral:** no change to the standing matrix, the `CommunityTranslation`
  document, the audit vocabulary, the `AccessAction` values, the failure
  shapes, or any Core seam — this is a Web-layer re-location of an existing
  surface. `CommunityTranslationViewModel` is additive; the three properties
  leave `ManageCommunityViewModel`.
- **Web test coverage note:** `CommunityControllerTranslationTests` calls the
  action methods directly and does not pin the route template, so the route
  moves are transparent to it (still green). The new `Translations` GET
  action is covered the same way its siblings are (thin controller, standing
  + failure-shape contract pinned at the Core seam).
