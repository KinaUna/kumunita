# ADR 0098 — Board audience-edit lane (creator ∪ GlobalAdmin)

Status: Accepted
Date: 2026-09-27
**Amends ADR 0070** (the board-update lane), which had frozen a board's
`Audience` as a creation-time choice and excluded it from `UpdateBoardRequest`.
Extends the **M5 board surface** (ADR 0067 the standing matrix + the frozen
`IProjectService` convention; ADR 0070 the board-update seam + the `CanEdit`
standing preview) and the **audience shape** (ADR 0001-B the verbatim audience
write + `Decide`/`EvaluateAudience` semantics; ADR 0036 the `Community` flag;
ADR 0041 the `AllResidents` flag) and the **Web audience-editor idiom**
(`AudienceEditorModel` + the `_GrantPickers` partial, already used by the
board-creation form and the post/event/reply forms). This ADR lifts the
audience freeze: a board's creator or a GlobalAdmin can now **change the
audience** on the board-edit page. The board's **community and language remain
creation-time choices** (ADR 0070, now only those two).

## Context

The board-creation form (`BoardNew`) already offers the full audience editor —
the `Any`/`All` mode radios, the "Everyone in this community" checkbox, the
user/group grant pickers, and the shared `_GrantPickers` partial. But the
board-*edit* page (`BoardEdit`) offered only title + description. ADR 0070
deliberately froze the audience: "The board's `Audience`, community, and
language are **not** editable — they are creation-time choices." Over the
miles that followed, the audience editor became the standard affordance on
every content form (posts, events, replies, announcements, and boards at
creation) — yet a board whose creator later wanted to *widen* or *narrow*
who can see it had no way to do so without deleting and recreating the board
(and losing its lanes, placements, and to-do history).

The standing and the write-shape are already fixed by the surface:

- **The standing is the board's (C-M5·6).** The board-update lane
  (`UpdateBoardAsync`) already resolves standing over the **board** via
  `CheckBoardStanding` — **creator ∪ GlobalAdmin**, the assignee branch not
  applying. Changing the audience is a board-update, so it inherits that wall
  unchanged; no new standing matrix, no new `AccessAction`/`AccessVia`.
- **The audience write is verbatim (ADR 0001-B).** `KanbanBoard.Audience` is
  written by reference, `Decide`/`EvaluateAudience` resolve it per request.
  There is no projection, no backfill, no side-effect handler to notify — a
  changed audience is live on the very next read. So the edit is a pure field
  write, exactly like the title/description write it joins.
- **The editor already exists (`AudienceEditorModel`).** The board-creation
  form binds `BoardCreatePost.Audience` through the same model + `_GrantPickers`
  partial + grant-picker seeding that this lane reuses. No new Web type, no new
  partial, no new key family.

The one non-obvious question is the **no-op shape**: `UpdateBoardRequest` gains
an `Audience` that must be able to mean both "leave it alone" and "replace it."
The answer is the same `null`-means-unchanged idiom the frozen-surface lanes
already use elsewhere — `null` leaves the stored audience untouched, a non-null
value is the actor's complete choice, written verbatim.

## Decision

The board-update lane gains **one** field — the audience — on the existing
seam, the existing standing, the existing audit row. No schema change, no new
index, no new bounded context, no new `kw-l` key *family* (the audience card
reuses the existing `projects.board.*` / `posts.audience_all_members` /
`events.audience_mode_*` keys), and the `Milestones.cs` / README Roadmap /
`MilestonesTests.cs` triple is **untouched** (a named lane on the already-shipped
M5 surface, not a milestone — the ADR 0086/0088/0093/0097 precedent). The
frozen-`IProjectService` rule (ADR 0067 §2.3: "an ADD beyond this list is a new
ADR") is honoured — this ADR *is* that new ADR; it amends 0070 rather than
silently extending 0067.

**Core — `UpdateBoardRequest` (additive, ADR 0084 additive-lane precedent):**
gains `public Audience? Audience { get; init; }`, the same nullable `Audience`
`CreateBoardRequest` already carries. The contract is explicit:

- **`null` = leave the stored audience unchanged.** A lane that posts only
  title/description (every pre-0098 caller, and the five existing F16 board-edit
  tests) leaves the audience exactly as it was. `null` is *not* "clear it."
- **non-null = the actor's complete choice, written verbatim** (ADR 0001-B) —
  replacing the stored audience wholesale. The editor builds a complete audience
  (`BuildAudience` always returns a non-null `Audience` with the `Community` /
  `AllResidents` flags set from the checkboxes), so the stored value is the
  full new choice, not a patch.

**Core — `UpdateBoardAsync` (changed-detection + apply):** the audience joins
the change check. A non-null request audience that **differs** from the stored
one (a `AudiencesEqual` comparison of `Mode`, `Community`, `AllResidents`, and
the grant list element-for-element — `Audience` has no value `Equals`) counts
as a change; an identical audience does not. On a real change the board's
`Audience` is replaced and `KanbanBoard.Modified` is stamped (the existing
"stamp on a real change only" no-op shape); a title/description-only no-op still
leaves `Modified` untouched. The board's **community and language are not part
of the request** and are not written here — they stay creation-time (ADR 0070).
The same single `board.update` `AccessAudit` row (board id as target,
`AccessVia.Owner` for the creator / `AccessVia.Admin` for the GlobalAdmin, the
`BoardAuditViaFor` shape) covers the audience change — one row, as before.

**Web — the board-edit form:** `BoardUpdateModel` gains `AudienceEditorModel
Audience` and `IsValid` requires a valid editor (mode set + grants parse).
`BoardEditGet` prefills the editor from the stored audience
(`AudienceEditorModel.FromAudience(board.Audience)` — a `null` stored audience
yields the "Everyone / `Any` / no grants" shape the creation form defaults to)
and seeds the grant-picker options (`SeedGrantPickerOptionsAsync`, the same call
the creation form makes). `BoardEditPost` re-seeds the pickers, validates the
audience (a bad mode adds an `Audience.Mode` error, the creation-form shape), and
passes `model.Audience.BuildAudience()` on the request. The `BoardEdit.cshtml`
gains the **same audience card** the `BoardNew` form renders (the
"Everyone in this community" checkbox with its `?? true` default, the `Any`/`All`
mode radios, and the `_GrantPickers` partial with `EditorName = "Audience"`),
plus the `_GrantPickerScripts` partial. `projects.board.edit_lead` is reworded
to name the audience among the editable fields and to keep the community/language
freeze explicit, in all four languages (ADR 0015 registry, en/de/fr/da parity).

**Tests:** two Core pins on the service (audience applied + `Modified` stamped;
`null` audience leaves the stored audience untouched and `Modified` un-stamped)
and two Web pins on the controller (the editor prefills from a stored audience
with the pickers seeded; a posted editor is written verbatim onto the request).

## Consequences

- A board's creator (or a GlobalAdmin) can now widen or narrow who sees the
  board — including its lanes and placed to-dos, which read the board's
  audience — without recreating the board. The change is live on the next
  read (no projection to settle), and it is already audited as `board.update`.
- ADR 0070's "audience, community, and language are not editable" narrows to
  "**community and language** are not editable" — those two genuinely remain
  creation-time (re-pointing a board at another community or language would be
  a different, larger lane and is still out of scope).
- `null` audience on `UpdateBoardRequest` keeps every existing caller and the
  five F16 board-edit tests compiling and behaving as before (a
  title/description-only update never touches the audience).
- The board-edit form now matches the board-creation form's audience surface —
  no new Web type, partial, or key family; the editor and pickers are the same
  `AudienceEditorModel` + `_GrantPickers` the rest of the app uses.
- No new standing, audit action, schema, index, or context — the frozen
  `IProjectService` surface (ADR 0067) is amended by this ADR, and the additive
  `UpdateBoardRequest` field follows the ADR 0084 precedent.
