# ADR 0088 — Projects / to-dos / Kanban-board user-added translations (the ADR 0022/0029/0059 lane carried to the M5/PL surface)

Status: Accepted
Date: 2026-09-21
Additive to: **0022** (post / reply translations), **0029** (announcement
translations), **0048** (edit + remove lanes for user-added translations),
**0059** (event translations — the closest template), **0021** (the Translator
standing), **0027** (display / chip-swap), **0049** (detail default-visible
variant), **0051** (list-surface variant default), **0067** (the M5 Projects
context), **0086** (the PL goals & projects lane). Additive on **0004 §B.1**
(delta-detect schema) and **0006** (module boundary contracts).

## Context

The M5 Projects surface (ADR 0067) and the PL goals & projects lane (ADR 0086)
shipped their full feature sets — to-dos, subtasks, Kanban boards + lanes +
cards, goals, projects — but, like the M4 events surface did before ADR 0059,
they **excluded the user-added translation lane**. Every other resident-facing
UGC surface on the platform now carries it: posts / replies (ADR 0022), group /
community name & description (ADR 0026), announcements (ADR 0029 + edit/remove
in ADR 0048), pages (ADR 0039 / 0047), and events (ADR 0059). The M5 to-do /
board / project detail pages are the remaining UGC surfaces still authored-in
language only.

This means a deployment cannot yet be exercised in a second language end-to-end
on the **projects** feed or detail — the stated multilingual goal (ADR 0005)
that was pulled forward. A resident who wrote a to-do, a Kanban board, or a
project in one language cannot have it translated by another resident (a
Translator, ADR 0021) the way they can every other UGC surface. This ADR
carries the ADR 0022 / ADR 0029 / ADR 0048 / ADR 0059 lane over to the M5/PL
bounded context, for the three surfaces the user named: **projects, to-dos,
and Kanban boards**.

**What this is, in one line:** a `TodoItem`, a `KanbanBoard`, and a `Project`
each gain user-added **title + body translations** — add / edit / remove — on
the row's **creator**, a **Translator** (ADR 0021), a **GlobalAdmin** (ADR
0017), and (to-dos only) the **assignee** (the ADR 0067 collaborator branch);
displayed on the detail page with the ADR 0027 chip-swap + ADR 0049
default-visible variant, and on the `/projects` feeds with the ADR 0051
viewer-language selection.

**Scope — `ProjectGoal` is deliberately out.** The user named *projects,
to-dos, Kanban boards*. `ProjectGoal` is the same PL lane and *would* be cheap
to add (it has the identical `Title` + `Description` shape as a `Project`),
but this ADR does not add it: the lane is carried to exactly the three surfaces
named, and a goal gains the identical lane by a trivial follow-on ADR (the same
`ProjectGoalTranslation` POCO + 4 seams + 3 Web lanes as `Project`'s). Keeping
scope to the three named entities keeps this ADR reviewable and its tests
bounded; `ProjectGoal` is recorded here as the explicit follow-on rather than
sneaking in a fourth entity family.

## Decision

### D1 — The three translation rows

Three POCOs in `Kumunita.Core.Projects`, each mirroring `EventTranslation`
(ADR 0059) but with the **Projects body-optional** adjustment:

| POCO | parent |
| --- | --- |
| `TodoTranslation` | a `TodoItem` (`TodoItemId`) |
| `BoardTranslation` | a `KanbanBoard` (`BoardId`) |
| `ProjectTranslation` | a `Project` (`ProjectId`) |

Each row:

| Field | Meaning |
| --- | --- |
| `Id` | surrogate key (Marten default). |
| `<Parent>Id` | the parent (the target of the audit row). |
| `LanguageCode` | the **target** language the row is written in. |
| `Title?` | a translation may re-title the parent or not (blank → `null`; the parent's own title is the display fallback). |
| `Body?` | the translated body (the parent's `Body` for a to-do, its `Description` for a board / project). **Optional here — unlike `EventTranslation`, whose `Body` is required — because the M5 surfaces are title-usable** (a to-do / board / project is valid with no body; the ADR 0079 / ADR 0070 title-only precedent). At least one of `Title` / `Body` must be non-blank at write time (enforced server-side; the DB unique index only pins (parent, language)). |
| `AuthorId` | the last actor to add / edit the row. |
| `Created` | the last add / edit stamp. |

Registered on the **`M5DocTypes`** document surface (the M5/PL docs already
register there) with a **`(ParentId, LanguageCode)` unique index** per POCO,
mirroring the `PostTranslation` / `AnnouncementTranslation` / `EventTranslation`
business-key convention (one row per parent per language). The auto-derived
index names stay under Postgres's 64-char `NAMEDATALEN` limit, so no explicit
rename is needed (the `ann_tr_uidx_ann_lang` ADR 0029 case does not arise here).
**Zero migration for existing rows** (ADR 0004 §B.1 delta-detect).

### D2 — Twelve `IProjectService` seams (four per entity)

`IProjectService` gains exactly **12 methods** (3 entities × 4 seams), mirroring
the add / read / update / remove shape ADR 0048 / ADR 0059 settled for every
other surface. For `<E>` ∈ {`Todo`, `Board`, `Project`}:

| Seam | Signature | Role |
| --- | --- | --- |
| **read** | `Get<E>TranslationsAsync(<e>Id)` → `IReadOnlyList<<E>Translation>` | rows ordered by `LanguageCode`. A **read, not a decision**: no authorization, no audit. The parent is still read through the existing `GetTodoAsync` / `GetBoardAsync` / `GetProjectAsync` (the M5/PL `CanAsync(Read)` gate) before its translations are reachable at all. |
| **add** | `Add<E>TranslationAsync(<e>Id, languageCode, title?, body?, actorId, actorRoles, ct)` | stores the row + its `AccessAudit` row; `UnauthorizedAccessException` on a denied actor, `KeyNotFoundException` for an unknown parent, `ArgumentException` for a blank (title, body) pair. |
| **update** | `Update<E>TranslationAsync(<e>Id, languageCode, title?, body?, actorId, actorRoles, ct)` | edits the `(<e>Id, languageCode)` row; re-stamps `Created` + `AuthorId`; `KeyNotFoundException` for a missing row, `ArgumentException` for a blank (title, body) pair (the same write-time invariant as add — an empty row is meaningless). |
| **remove** | `Remove<E>TranslationAsync(<e>Id, languageCode, actorId, actorRoles, ct)` | deletes the `(<e>Id, languageCode)` row; `KeyNotFoundException` for a missing row. |

**Seam shape — the M5/PL convention (ADR 0067 §4), not the M3 one.** Like the
existing `ProjectService` write lanes (and the M4 `EventService`), these write
lanes **self-compose** their own C3 session (`store.OpenSession(...)`); they
carry **no `IDocumentSession` parameter**. The domain write and the
`AccessAudit` row commit atomically in that one self-composed session
(invariant C3).

### D3 — The standing: creator ∪ assignee (to-do) ∪ Translator ∪ GlobalAdmin

The standing is a **pure role-claim check** (the M5 §3.4 shape), enforced
server-side by three new pure helpers `ProjectService.ResolveTodoTranslationStanding`
/ `ResolveBoardTranslationStanding` / `ResolveProjectTranslationStanding` (the
C3 single-source pin — no second copy of the matrix). The matrix is the
**translation** matrix, *not* the edit standing — it is the ADR 0059 author ∪
Translator ∪ GlobalAdmin matrix **plus the to-do's existing ADR 0067
assignee branch**:

- **The parent's creator** (`AuthorId`) may add / edit / remove a translation.
  `AccessVia.Owner`.
- **The to-do's assignee** (to-do only — a board / project is not assignable)
  may add / edit / remove a translation. `AccessVia.Admin` (the ADR 0067
  assignee branch, the `TodoAuditViaFor` "least-distortion slot" precedent).
- **A Translator** (ADR 0021 delegated editor) may add / edit / remove a
  translation of **anyone's** row. `AccessVia.Admin`.
- **A GlobalAdmin** (ADR 0017) may add / edit / remove a translation of
  **anyone's** row. `AccessVia.Admin`.
- **A plain Member** (not the creator, not the assignee, no elevated role) has
  **no** standing.

This is *broader than* the existing edit standing (the M5 edit lanes —
`CheckTodoStanding` / `CheckBoardStanding` / `CheckProjectStanding` — are
creator ∪ assignee (to-do) ∪ GlobalAdmin and **do not** include Translator):
a Translator who cannot *edit* a to-do's title may still *translate* it, exactly
as ADR 0059 let a Translator translate an event the Translator could not
otherwise edit. No new `AccessVia` member, no new role — the `Owner` / `Admin`
split and the `Translator` / `GlobalAdmin` claims already exist (the ADR 0030
composable elevated roles). A **pure display helper**
`ProjectService.CanAdd<E>Translation(<parent>, actorId, actorRoles)` (the ADR
0022 / ADR 0048 / ADR 0059 precedent) returns the matching resolver's
`... is not null`, so the Web renders the add / edit / remove controls with the
exact matrix the write lanes re-check.

### D4 — Audit

Each write lane stores one `AccessAudit` row in the self-composed session,
committing atomically with the write (C3). `TargetId = <parent id>` (the
parent the row is *about*, the ADR 0017/0022 convention), `Outcome =
AccessOutcome.Allow`, and `Via` from the D3 standing. The existing
`TargetKind` constants are reused verbatim (`TargetKindTodo` = `"todo"`,
`TargetKindBoard` = `"board"`, `TargetKindProject` = `"project"`). The action
strings are the ADR 0059 `eventtranslation.*` idiom per entity:

| Action (to-do / board / project) | `Via` (creator) | `Via` (assignee, to-do) | `Via` (Translator) | `Via` (GlobalAdmin) |
| --- | --- | --- | --- | --- |
| `todotranslation.add` / `.update` / `.remove` | `Owner` | `Admin` | `Admin` | `Admin` |
| `boardtranslation.add` / `.update` / `.remove` | `Owner` | — | `Admin` | `Admin` |
| `projecttranslation.add` / `.update` / `.remove` | `Owner` | — | `Admin` | `Admin` |

The **read** lane (`Get<E>TranslationsAsync`) writes **no** audit row (a read,
not a decision — C-M3·1). **No new `AccessAction`** is introduced — these are
write standing decisions resolved by the service, not authorization decisions
routed through the frozen `IAuthorizationService` (ADR 0006), consistent with
every other ADR 0022/0029/0048/0059 translation lane.

### D5 — The Web surface

`ProjectsController` (`Kumunita.Web.Controllers`) gains **nine** thin POST
lanes (3 entities × 3) (thin Web, fat Core — ADR 0006-D; the controller never
re-derives the standing split itself):

| Route | Action | Seam |
| --- | --- | --- |
| `POST /projects/todos/{id}/translations[/update\|/remove]` | `AddTodoTranslation` / `UpdateTodoTranslation` / `RemoveTodoTranslation` | `Add/Update/RemoveTodoTranslationAsync` |
| `POST /projects/boards/{id}/translations[/update\|/remove]` | `AddBoardTranslation` / `UpdateBoardTranslation` / `RemoveBoardTranslation` | `Add/Update/RemoveBoardTranslationAsync` |
| `POST /projects/projects/{id}/translations[/update\|/remove]` | `AddProjectTranslation` / `UpdateProjectTranslation` / `RemoveProjectTranslation` | `Add/Update/RemoveProjectTranslationAsync` |

All are `[ValidateAntiForgeryToken]`. Each: an empty `id` → `NotFound()`; an
unauthenticated caller (`SubjectId` blank) → `ForbidResult`; a blank
`languageCode` (all) or a blank (title, body) pair (add / update) → a shape
error, `TempData["error"]` + a redirect back to the detail page with **no**
service write; a `KeyNotFoundException` from the parent precondition or the
write lane → `NotFound()`; an `UnauthorizedAccessException` → `ForbidResult`; a
success → `TempData["info"]` + a redirect back to the detail page. The parent
`GetAsync` precondition (C-M3·1) re-runs the row's read decision so the write
never touches a dangling / not-visible parent.

**Display:**
- **Detail** (`/projects/todos/{id}`, `/projects/boards/{id}`,
  `/projects/projects/{id}`): the detail view models carry the parent's
  translations + the enabled-language catalog (as `LanguageOption`s with
  `HasTranslation`) + `CanTranslate` (from `ProjectService.CanAdd<E>Translation`)
  + `OriginalLanguageCode`. The title + body are wrapped in the ADR 0027
  chip-swap markup — the to-do and board wrap their title + body in a single
  `data-td-group` (`"todo"` / `"board"`); the project keeps its title and
  description in the page's separate header / card regions, so it uses two
  groups (`"project-title"` / `"project-description"`) that share the same
  variant set + chip row. Either way the authored-in variant is the first /
  default-visible one, one hidden variant per translation, a chip row
  (original always present, TD·1). The add / edit / remove controls are
  **presented with the ADR 0082 idiom** — the shared `⋮` action dropdown +
  Bootstrap modal (the Community-post / Event / Announcement surface's
  translation idiom, not inline `<details>` blocks): one **Edit** dropdown item
  per existing translation and one **Add** item per enabled-but-missing
  language, each opening a modal form (Edit: title + a plain body `<textarea>`;
  Add: a title input prefilled from the parent's title + the ADR 0025
  `rc-editor` body, `data-rich-editor-no-image`), plus a **Remove** form per
  existing translation in the menu (CSRF-tokenized, `data-confirm`), all gated
  on `CanTranslate`. The routes, form `action`s, field names, and the
  server-side standing are unchanged — only the presentation of these
  three surfaces (Project already shipped this way; To-do and Board now match
  it). **ADR 0049** applies: when
  the viewer's current language has a translation, that variant is the
  default-visible one.
- **Feed** (`/projects/todos`, `/projects/boards`, `/projects`): per **ADR
  0051**, each feed row shows the title / body in the viewer's current language
  when a translation into that language exists, else the authored-in text — the
  same per-request `EffectiveLanguageCode` resolution the detail page uses.

**Dependencies:** `ProjectsController` gains an optional `ITranslationProvider?`
constructor parameter (last position, defaulting to `null` — the ADR 0049 / 0051
feed/detail variant resolution needs it), keeping the existing test construction
sites compiling. It also gains a private `SeedLanguageName(code)` helper (the
`PostsController` / `EventController` idiom) that resolves a language code to
its catalog native name (falling back to the raw code), and a
`SeedLanguagePickerAsync()` mirror if not already present.

## Consequences

- **The three remaining named UGC surfaces gain the translation lane**, so a
  deployment can be exercised in a second language end-to-end across posts,
  replies, announcements, groups, pages, events, **and now projects / to-dos /
  boards** — the goal the M6 milestone pulled forward (ADR 0005). `ProjectGoal`
  is the recorded follow-on (D-scope).
- **One row per (parent, language) pair.** The `(ParentId, LanguageCode)` unique
  index enforces this at the DB layer, mirroring the existing translation
  business-key convention.
- **Full lane (add / edit / remove), the ADR 0048 shape** — a row's translation
  can be corrected (update) or withdrawn (remove), not just re-added.
- **The standing is the ADR 0021 / ADR 0030 translation matrix, plus the to-do's
  ADR 0067 assignee branch** — the one difference from ADR 0059's events
  (events have no assignee concept), and deliberately broader than the M5 *edit*
  standing (a Translator translates without editing). No new role, no new
  `AccessVia`.
- **Audit-by-default is preserved**: every add / edit / remove writes its own
  `AccessAudit` row (`todotranslation.*` / `boardtranslation.*` /
  `projecttranslation.*`, `TargetKind = "todo"` / `"board"` / `"project"`)
  recording the parent, the actor, the standing branch, and the language.
- **Display reuses ADR 0027 / ADR 0049 / ADR 0051**, not a new mechanism: the
  detail chip-swap, the detail default-visible variant, and the feed
  viewer-language selection are all existing, tested machinery — the Projects
  surface adds no new display rule.
- **The M5/PL self-composed-session convention is preserved** (ADR 0067 §4): the
  12 write lanes take no `IDocumentSession`, so the `ProjectsController` never
  opens a session around them (unlike the M3 `PostsController` /
  `AnnouncementController` which wrap their M3 seams in a caller session).
- **`Milestones.cs` / the README Roadmap / `MilestonesTests.cs` are untouched** —
  a named lane on the M5/PL surface, not a milestone (the ADR 0086 `PL`
  precedent).
