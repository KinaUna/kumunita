# RC U05 — composer spread: announcements + group posts + static pages

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained. Primary tier = `docs/design/rich-content-design.md`
> (§Pinned contract is authoritative); mismatch → record
> `## U05 — Drift pause` in the handoff note — do not silently pick.

## Goal

Spread U04's composer control (the pinned `data-insert-image` /
`data-image-target` markup + `bindInsertImage`) to the **three**
remaining surfaces — announcements (New + Edit), group posts
(New + Edit), and the static-page editor (the about/terms/help
editor lane) — and wire each surface's write action to populate
`ImageIds` server-side via U04's `ExtractContentImageIds` helper
(R·3 — the body is the source of truth; the ids are parsed, never
client-supplied). After this unit **every** body-authoring surface
can attach an image and **every** write lane stores the ids.

## Entry reads (≤ 5 files)

1. `docs/plans-milestones/done/rich-content-u04-plan.md`'s handoff
   note section (`## U04 — upload lane + post composer`) — the
   **pinned outputs** U04 recorded: the helper's file path + regex,
   the markup shape, the script-include mechanism, the draft type
   names. This note is your contract.
2. `docs/design/rich-content-design.md` — §Invariants R·3,
   §Pinned contract (the announcement/page draft ADDs — note the
   "pinned **once U05's entry reads confirm** the exact binding
   points" clause: **you** confirm and record them in an amendment
   sub-line, U01's sanctioned append-only mechanism).
3. The announcement write lane — grep `Announcement` in
   `src/Kumunita.Web/Controllers/` for the create + edit actions,
   and read the two views (New + Edit). Pin the draft record shape
   (record? POCO? field-by-field mapping?) and the view file paths.
4. The group-post write lane — grep `GroupPostDraft` in
   `src/Kumunita.Web/Controllers/` for the create + edit actions,
   read the two views. (The draft already gained `ImageIds` in U04 —
   confirm by reading the record; the **mapping** into the doc is
   what you add here if U04's note says the group-post mapping was
   not covered.)
5. The static-page editor — `src/Kumunita.Web/Controllers/`
   (grep `LocalizedPage` or the page-edit route) + its editor view.
   Pin: which service owns the write, whether the page doc is
   written field-by-field (the `ImageIds` copy point), and the view
   path.

## Deliverables (≤ 6 files, the count follows the entry reads)

For **each** of the three surfaces, the same three-part change U04
made for posts (the markup is **not** re-pinned — copy U04's exact
markup + script-include mechanism verbatim; the data-attribute
names are the contract):

1. **View (New + Edit) markup** — add the
   `rc-insert-image` control block + set `data-image-target` on the
   body textarea, exactly as U04's note describes it. The static-
   page editor is the one likely to differ (it may be a single
   shared view rather than New/Edit pairs — use whatever it is and
   record it).
2. **Draft record ADD** (where the surface has one):
   - Announcements: **`Announcement.ImageIds` was added in U03**
     (the fourth field owner — named in the design doc's
     §Pinned contract from the start (U01), implemented by U03's
     deliverable 2 — so the field **already exists** on the POCO;
     **no drift pause** and **no amendment** here). The only question is whether the announcement write
     path uses a **draft record** or binds form fields directly
     onto the POCO:
       - if there **is** an announcement draft record (read
         `src/Kumunita.Core/Announcements/` for a `*Draft` type),
         append `IReadOnlyList<string> ImageIds = []` **last** (the
         source-compatible rule, U04's pinned shape) and map it in
         the action;
       - if the write binds form fields **directly onto the
         `Announcement` POCO** (no draft record), **skip the draft
         ADD** and record "no draft record — ids set on the POCO in
         the action" (the field itself is already there from U03).
     Either way the write action must set `ImageIds` from the parsed
     body (deliverable 3 below) **before** the service call — the
     body is the source of truth (R·3), never the client.
   - Group posts: `GroupPostDraft` already has the field (U04).
     If the mapping into the `Post` doc (group posts are `Post`
     docs with a `GroupId`) was done in U04, **verify** the group-
     post action's mapping line is present; if U04's note says the
     mapping is shared with the post action, confirm and move on.
   - Static pages: `LocalizedPage` already has the field (U03).
     The write action must copy the parsed ids onto the doc (the
     field-by-field mapping line — same shape as U04's post
     mapping).
3. **Write action wiring** — in each surface's create + edit action:
   after binding, `var ids = ContentImageIds.ExtractContentImageIds(
   <the body>);` and set `ImageIds = ids` on the draft/doc **before**
   the service call (the body is the source of truth — R·3; the
   client never sends ids). Mirror U04's exact wiring shape.

## Exit criteria

- `dotnet build` green; `npm run build` (the `ts:build` task) green
  (the markup is static HTML + one module call — no new TS file,
  so the build is the `insert-image.ts` re-check; if the page
  include mechanism is a new `<script>` tag the build still must
  pass).
- The design doc carries `§Pinned contract amendment (U05)` naming:
  (a) the announcement draft shape (record vs POCO + the exact type
  name — and, if a draft record exists, the `ImageIds` ADD you made;
  if POCO-direct, the "no draft record" note), (b) the static-page
  write path (service type + action name + the mapping line's file),
  (c) a one-line confirmation that `Announcement.ImageIds` was
  present (named in the design doc's §Pinned contract — all four
  owners — and implemented in U03) — **not** added in U05.
- **No** test files (the ownership tests are U07's — they assume
  exactly these write-lane shapes).
- A manual smoke (record in the handoff note): for **one** of the
  three surfaces (announcement preferred — it exercises the
  draft-vs-POCO binding path, the one most likely to differ from
  the post surface), create a post with an image via the form (or
  curl the action if the form's CSRF blocks curl — record which),
  confirm the stored doc's `ImageIds` contains the id (the app's
  log or a quick `dotnet exec` query is **not** required — the
  `GET /content-image/{id}` route flipping from **404** (U04's
  orphan posture) to **200/404-per-actor** (U03's route posture)
  is the observable proof — run both curls and record the results).
- **Handoff note** (append): `## U05 — composer spread (3
  surfaces)`, 6–8 lines: (a) the three surfaces' view file paths
  (announcement New/Edit, group New/Edit, static-page editor);
  (b) the three write actions (controller + action name each);
  (c) the draft shapes confirmed (record/POCO per surface + the
  exact type names); (d) the `Announcement.ImageIds` confirmation
  ("present — named in the design doc's §Pinned contract, added in
  U03" — it should **not** read "added-here");
  (e) the orphan→owned curl flip result (the 404→200/404-per-actor
  proof, or the stopping point); (f) the drift pause, if any.
- **Then move this file:** `Move-Item
  docs\plans-milestones\in-progress\rich-content-u05-plan.md
  docs\plans-milestones\done\rich-content-u05-plan.md`.
