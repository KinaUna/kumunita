# ADR 0059 — Event user-added translations (the ADR 0022/0029 lane carried to the M4 surface)

Status: Accepted
Date: 2026-09-20
Amends: **0054** (Events) — its "the event-translation row is a follow-on lane
with its own ADR" note is now shipped by this ADR. Additive to **0022** (post /
reply translations), **0029** (announcement translations), **0048** (edit +
remove lanes for user-added translations), **0021** (the Translator standing),
**0027** (display / chip-swap), **0049** (detail default-visible variant) and
**0051** (list-surface variant default).

## Context

The M4 events surface (ADR 0054) shipped with every other M4 lane (feed / detail
/ create / edit / publish / delete / RSVP / reminders) but deliberately excluded
the translation lane:

> **The translation lane (ADR 0022/0026/0029) is NOT extended to events in M4** —
> an `Event` is authored-in-language only; the event-translation row is a
> follow-on lane with its own ADR.

That is the one resident-facing UGC surface on the platform still missing the
**user-added** translation lane: posts / replies (ADR 0022), group / community
name & description (ADR 0026), announcements (ADR 0029 + edit/remove in ADR
0048), and pages (ADR 0039 / 0047) all carry it. Events are the last. This means
a deployment cannot yet be exercised in a second language end-to-end on the
events feed or detail — the stated M6 multilingual goal (ADR 0005) that was
pulled forward. This ADR carries the ADR 0022 / ADR 0029 / ADR 0048 lane over to
the Events bounded context.

**What this is, in one line:** an `Event` gains user-added **title + body
translations** — add / edit / remove — on the event's **author**, a
**Translator** (ADR 0021), or a **GlobalAdmin** (ADR 0017); displayed on the
detail page with the ADR 0027 chip-swap + ADR 0049 default-visible variant, and
on the `/events` feed with the ADR 0051 viewer-language selection.

## Decision

### D1 — The `EventTranslation` row

An **`EventTranslation`** POCO (`Kumunita.Core.Events`) stores a user-added
translation of an event:

| Field | Meaning |
| --- | --- |
| `Id` | surrogate key (Marten default). |
| `EventId` | the parent event (the target of the audit row). |
| `LanguageCode` | the **target** language the row is written in. |
| `Title?` | a translation may re-title the event or not (blank → `null`, the ADR 0022/0029 shape — the display falls back to the event's own title). |
| `Body` | the translated body (non-empty — a translation needs some text). |
| `AuthorId` | the last actor to add / edit the row. |
| `Created` | the last add / edit stamp. |

Registered on the **`M4DocTypes`** document surface (the ADR 0054 M4 doc-type
registration — the events docs already register there) with a
**`(EventId, LanguageCode)` unique index**, mirroring the
`PostTranslation` / `AnnouncementTranslation` / `GroupTranslation` /
`CommunityTranslation` business-key convention (one row per event per language).
The auto-derived index name stays under Postgres's 64-char `NAMEDATALEN` limit,
so no explicit rename is needed (the `ann_tr_uidx_ann_lang` ADR 0029 case does
not arise here).

### D2 — Four `IEventService` seams

`IEventService` gains exactly four methods, mirroring the add / read / update /
remove shape ADR 0048 settled for every other surface:

| Seam | Signature | Role |
| --- | --- | --- |
| **read** | `GetEventTranslationsAsync(eventId)` → `IReadOnlyList<EventTranslation>` | rows ordered by `LanguageCode`. A **read, not a decision**: no authorization, no audit. The event is still read through `IEventService.GetAsync`'s `CanAsync(Read)` (ADR 0054) before its translations are reachable at all. |
| **add** | `AddEventTranslationAsync(eventId, languageCode, title?, body, actorId, actorRoles, ct)` | stores the row + its `AccessAudit` row; `UnauthorizedAccessException` on a denied actor, `KeyNotFoundException` for an unknown event. |
| **update** | `UpdateEventTranslationAsync(eventId, languageCode, title?, body, actorId, actorRoles, ct)` | edits the `(eventId, languageCode)` row; re-stamps `Created` + `AuthorId`; `KeyNotFoundException` for a missing row. |
| **remove** | `RemoveEventTranslationAsync(eventId, languageCode, actorId, actorRoles, ct)` | deletes the `(eventId, languageCode)` row; `KeyNotFoundException` for a missing row. |

**Seam shape — the M4 convention (ADR 0054 §4), not the M3 one.** Unlike
`PostService` / `AnnouncementService` (which take the caller's
`IDocumentSession` — the ADR 0048 D1 "caller's session" shape), the M4
`EventService` write lanes **self-compose** their own C3 session
(`store.OpenSession(...)`); the four event-translation seams are the same shape
and carry **no `session` parameter**. The domain write and the `AccessAudit` row
commit atomically in that one self-composed session (invariant C3).

### D3 — The standing: author ∪ Translator ∪ GlobalAdmin

The standing is a **pure role-claim check** (the ADR 0054 §3.4 shape), enforced
server-side by the one helper `EventService.ResolveTranslationStanding` (the
C3 single-source pin — no second copy of the matrix):

- **The event's author** may add / edit / remove a translation of their event.
  `AccessVia.Owner`.
- **A Translator** (ADR 0021 delegated editor) may add / edit / remove a
  translation of **anyone's** event. `AccessVia.Admin`.
- **A GlobalAdmin** (ADR 0017) may add / edit / remove a translation of
  **anyone's** event. `AccessVia.Admin`.
- **A plain Member** (not the author, no elevated role) has **no** standing.

**There is no component-moderator branch** — events have no component-scope
standing (ADR 0054 §5: the M4 community lane has no component-moderator
branch; events live on the `Community` / grant / component audience model, but
moderator scoping is not an events concept). This is the one difference from the
announcement lane (ADR 0029), whose standing matrix *does* include a
community-moderator case. No new `AccessVia` member, no new role — the
`Owner` / `Admin` split and the `Translator` / `GlobalAdmin` claims already
exist (the ADR 0030 composable elevated roles).

A **pure display helper** `EventService.CanAddTranslation(eventAuthorId,
actorId, actorRoles)` (the ADR 0022 / ADR 0048 precedent) returns
`ResolveTranslationStanding(...) is not null`, so the Web can decide whether to
render the add / edit / remove controls with the exact same matrix the write
lanes re-check.

### D4 — Audit

Each write lane stores one `AccessAudit` row in the self-composed session,
committing atomically with the write (C3). `TargetKind = "event"` (the exact
string, the ADR 0054 discriminator), `TargetId = <eventId>` (the event the row
is *about*, the ADR 0017/0022 convention), `Outcome = AccessOutcome.Allow`, and
`Via` from the D3 standing:

| Action | `Via` (author) | `Via` (Translator) | `Via` (GlobalAdmin) |
| --- | --- | --- | --- |
| `eventtranslation.add` | `Owner` | `Admin` | `Admin` |
| `eventtranslation.update` | `Owner` | `Admin` | `Admin` |
| `eventtranslation.remove` | `Owner` | `Admin` | `Admin` |

The **read** lane (`GetEventTranslationsAsync`) writes **no** audit row (a read,
not a decision — C-M3·1). **No new `AccessAction`** is introduced — these are
write standing decisions resolved by the service, not authorization decisions
routed through the frozen `IAuthorizationService` (ADR 0006), consistent with
every other ADR 0022/0029/0048 translation lane.

### D5 — The Web surface

`EventController` (`Kumunita.Web.Controllers`) gains three thin POST lanes
(thin Web, fat Core — ADR 0006-D; the controller never re-derives the standing
split itself):

| Route | Action | Seam |
| --- | --- | --- |
| `POST /events/{id}/translations` | `AddTranslation` | `AddEventTranslationAsync` |
| `POST /events/{id}/translations/update` | `UpdateTranslation` | `UpdateEventTranslationAsync` |
| `POST /events/{id}/translations/remove` | `RemoveTranslation` | `RemoveEventTranslationAsync` |

All are `[ValidateAntiForgeryToken]`. Each: an empty `id` → `NotFound()`; an
unauthenticated caller (`SubjectId` blank) → `ForbidResult`; a blank
`languageCode` (add / update / remove) or blank `body` (add / update) → a shape
error, `TempData["error"]` + a redirect back to `/events/{id}` with **no**
service write; a `KeyNotFoundException` from the `GetAsync` precondition or the
write lane → `NotFound()`; an `UnauthorizedAccessException` → `ForbidResult`; a
success → `TempData["info"]` + a redirect back to `/events/{id}`. The
`GetAsync` precondition (C-M3·1) re-runs the event's read decision so the write
never touches a dangling / not-visible event.

**Display:**
- **Detail** (`/events/{id}`): the `EventDetailViewModel` carries the event's
  translations + the enabled-language catalog (as `LanguageOption`s with
  `HasTranslation`) + `CanTranslate` (from `EventService.CanAddTranslation`) +
  `OriginalLanguageCode`. The title + body are wrapped in the ADR 0027
  `data-td-group="event"` chip-swap markup — the authored-in variant is the
  first / default-visible one, one hidden variant per translation, a chip row
  (original always present, TD·1), and one add / edit / remove form per language
  (edit + remove per existing row, add per missing enabled language) gated on
  `CanTranslate`. **ADR 0049** applies: when the viewer's current language has a
  translation, that variant is the default-visible one (the original
  hidden-behind-its-chip); the authored-in language is always still reachable.
- **Feed** (`/events`): per **ADR 0051**, each event's feed row shows the
  title / body in the viewer's current language when a translation into that
  language exists, else the authored-in text — the same per-request
  `EffectiveLanguageCode` resolution the detail page uses, so the feed, the
  detail, and the UI text default can never disagree for the same request.

**Dependencies:** `EventController` gains an optional `ITranslationProvider?`
constructor parameter (last position, defaulting to `null` — the ADR 0049 /
0051 feed/detail variant resolution needs it), keeping the existing test
construction sites compiling. It also gains a private `SeedLanguageName(code)`
helper (the `PostsController` / `AnnouncementController` idiom) that resolves a
language code to its catalog native name (falling back to the raw code).

## Consequences

- **The platform's last UGC surface without the translation lane gains it**, so a
  deployment can be exercised in a second language end-to-end across posts,
  replies, announcements, groups, pages, **and events** — the goal the M6
  milestone pulled forward (ADR 0005).
- **One row per (event, language) pair.** The `(EventId, LanguageCode)` unique
  index enforces this at the DB layer, mirroring the existing translation
  business-key convention.
- **Full lane (add / edit / remove), the ADR 0048 shape** — an event's
  translation can be corrected (update) or withdrawn (remove), not just
  re-added. This is *new* for events (the other surfaces already gained it in
  ADR 0048); the standing is unchanged from the add lane.
- **The standing is the ADR 0021 / ADR 0030 matrix with no moderator branch.**
  author ∪ Translator ∪ GlobalAdmin. This is the one difference from ADR 0029
  (announcements add a community-moderator case); events have no component-scope
  standing (ADR 0054 §5). No new role, no new `AccessVia`.
- **Audit-by-default is preserved**: every add / edit / remove writes its own
  `AccessAudit` row (`eventtranslation.*`, `TargetKind = "event"`) recording the
  event, the actor, the standing branch, and the language.
- **Display reuses ADR 0027 / ADR 0049 / ADR 0051**, not a new mechanism: the
  detail chip-swap, the detail default-visible variant, and the feed
  viewer-language selection are all existing, tested machinery — the events
  surface adds no new display rule.
- **The M4 self-composed-session convention is preserved** (ADR 0054 §4): the
  four lanes take no `IDocumentSession`, so the `EventController` never opens a
  session around them (unlike the M3 `PostsController` / `AnnouncementController`
  which wrap their M3 seams in a caller session).
