# ADR 0079 — To-do optional start and due dates

Status: Accepted
Date: 2026-09-25
Amends: **0067** (the M5 to-do surface — the `TodoItem` document gains two
optional `DateTimeOffset?` fields; the create / update request DTOs gain the
matching two optional fields; the standing matrix is untouched) and **0054**
(the M4 Event `Start` / `End` date shape is **reused but made optional** —
the Event requires both, the to-do leaves both blank by default).

## Context

A to-do (M5, ADR 0067) already carries the neighborhood's shared work: a
title, an optional body, a free-text status, an optional assignee (the §2.5
standing branch), an optional parent (a subtask), an optional board
placement, and an audience. What it does **not** carry is any sense of *when*
the work is expected to happen — a to-do that says "repaint the front gate"
has no deadline and no start.

The M4 Event (ADR 0054) settled the repo's date/time idiom, and this feature
reuses it verbatim:

- **`DateTimeOffset`**, stored UTC, rendered by the one `<kw-dt>` TagHelper
  in the viewer's effective timezone (ADR 0019) — so a date a resident sets
  "on the 5th" is *shown* in every viewer's own timezone without any per-row
  storage of a zone.
- **`type="datetime-local"`** form inputs, bound model-side to a nullable
  `DateTimeOffset?`, the value round-tripped as
  `yyyy-MM-ddTHH:mm` of the local instant.

The one deliberate departure from the Event is **optionality**. An Event is
defined by its start and end — an event with no times is a contradiction, so
ADR 0054 makes both `Start` / `End` required. A to-do is not so defined: a
large share of a neighborhood's to-dos are open-ended ("fix the broken
wheelbarrow") and have no date at all. Forcing both dates on every to-do
would tax the common case for the benefit of the exceptional one. So both
dates are **optional** and **default to `null`** (no date).

**What the request is:** "Todos should have optional due date and start
date." Read literally: two new fields on the to-do, each independently
blank-able, with the start / due relationship the same as the Event's start
/ end (due not before start).

## Decision

**Two optional fields, one shape, one rule.** The `TodoItem` document gains
two nullable `DateTimeOffset?` fields:

- **`StartAt`** — the optional start instant.
- **`DueAt`** — the optional due instant (the at-a-glance deadline).

Both default to `null` (no date). This is the ADR 0054 Event `Start` / `End`
shape **minus the requirement** — the storage type, the UTC storage, the
`datetime-local` binding, and the `<kw-dt>` effective-timezone render are all
reused; only the "required" is dropped, becoming "optional, null = no date".

### The partial-update rule (the one non-trivial choice)

On the **create** lane, both fields are simply written verbatim (the
`null`-default = no-date, so a form that leaves them blank stores `null`).

On the **update** lane, the to-do already uses the repo's partial-update
shape (`ComponentId` / `Status`): a non-`null` value is applied, a `null`
leaves the stored value untouched. But the edit form's `datetime-local`
field is **always present** — a resident who clears a due date posts a *blank*
field, which binds to `null`. If we followed the usual partial rule, clearing
a date would be *impossible* (a blank post is indistinguishable from "don't
touch it"). So the update lane treats the two date fields as
**always-assigned**: a `null` **clears** the stored date (back to no-date),
a non-`null` sets it. This matches the `ComponentId` / `Status` *application*
style (always applied, never "skipped"), and it matches the form's actual
intent. The `UpdateTodoRequest` doc-comment states this explicitly so the
seam is not mis-read as the standard partial rule.

### The coherence rule

When **both** dates are set, `DueAt` must not precede `StartAt` (the ADR 0054
Event "the end time must be after the start" shape, *on-or-after* rather than
strictly-after). The Web-boundary `TodoEditorModel.IsValid` enforces this and
the controller surfaces it as the localized form error **"The due date must
be on or after the start."** — the same refusal shape `EventController` uses
for `End < Start`.

### The render lanes (no new mechanism)

- **Feed row** (`TodosIndex`), **detail** (`TodoDetail`), and the **board
  card** (`BoardDetail`) each render the dates through the one `<kw-dt>`
  TagHelper, **gated on non-null** so an unset date shows nothing (no "—"
  placeholder, no empty label) — the at-a-glance value on the kanban card is
  the **due date**.
- The **create** / **edit** forms add a single date card with the two
  `datetime-local` inputs and the `projects.todo.dates_hint` hint ("Both are
  optional — leave blank for no date. Shown to viewers in their own
  timezone.").

### No migration

`StartAt` / `DueAt` are nullable field additions on an existing document.
Per ADR 0004 §B (Marten delta-detection), adding a nullable column to a
document is a no-op against the existing schema — the `M5DocTypes`
registration is unchanged and no explicit migration is required.

### The standing matrix is untouched

The dates carry **no access meaning** (like the Event's `Start` / `End`) —
they are display + scheduling data, not a gate. The §2.5 standing matrix
(creator ∪ assignee ∪ GlobalAdmin over a to-do) is unchanged: a to-do with
dates is edited by exactly the same set of actors as one without.

## Consequences

- **New capability** — the to-do now carries optional start / due dates,
  displayed in the viewer's effective timezone via `<kw-dt>`, set/cleared
  through the existing create / edit / board surfaces.
- **Three new translation keys** — `projects.todo.start`,
  `projects.todo.due`, `projects.todo.dates_hint`, in all four catalog
  languages (en / de / fr / da).
- **One update-lane nuance** — on the to-do, `null` date = clear (not the
  standard partial "leave untouched"), so the `UpdateTodoRequest` doc-comment
  pins the deviation explicitly.
- **No new document, no new seam, no standing change** — the change is
  additive on `TodoItem` + the two request DTOs + the Web models / controller
  / views, all reusing the ADR 0054 date idiom.
