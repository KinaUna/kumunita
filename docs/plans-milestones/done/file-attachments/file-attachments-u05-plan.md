# ATT U5 — Core: announcement create/edit write lanes persist `AttachmentIds`

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-file-attachments.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.
> **Precondition:** U3 shipped (`Announcement.AttachmentIds` field +
> `FindByAttachmentIdAsync` exist). This unit is **code** — it ends with a
> green `dotnet build`.

## Understanding

U3 added the `Announcement.AttachmentIds` field and the
`FindByAttachmentIdAsync` seam. This unit makes the **announcement edit lane**
persist that field. The announcement lane is a **flat public surface** (no
audience decision, C-ATT·7 step 4 is just the scope gate), and its write idiom
is **POCO-direct**: `AnnouncementService.CreateAsync` stores the POCO the
controller hands it (so the **create** write happens via the controller's
object-initializer — that's U7's wiring), and `UpdateAsync` does an explicit
field-copy (`existing.ImageIds = updated.ImageIds ?? [];`). This unit adds the
**one matching line** for `AttachmentIds` in `UpdateAsync`. The create
initializer line (`AttachmentIds = AttachmentIds.ExtractAttachmentIds(model.Body)`)
is added in **U7** alongside the other four controller call-sites.

## The invariants you are implementing

- **C-ATT·4** — Core writes the collection verbatim (`existing.AttachmentIds =
  updated.AttachmentIds ?? []`); it does **not** parse `Body`.
- **C-ATT·5** — you copy **`AttachmentIds`**, not `ImageIds`; the existing
  `ImageIds` line stays byte-for-byte.
- **C-ATT·9** — the image lane (the `ImageIds` line, `FindByImageIdAsync`) is
  untouched.

## Entry reads (do NOT scan the repo — read exactly these)

1. `docs/design/file-attachments-design.md` — **§2.3** (the announcement
   write-lane line: `existing.AttachmentIds = updated.AttachmentIds ?? [];`).
2. `src/Kumunita.Core/Announcements/AnnouncementService.cs` — the **mirror
   source**. Read the `UpdateAsync` region **lines ~240–300**: the field-copy
   block where `existing.ImageIds = updated.ImageIds ?? [];` lives (≈ L287),
   and the `changed`-computation block just above it. Also grep for
   `CreateAsync` in this file to confirm the create path stores the POCO
   directly (it does — that's why the create write is the controller's
   initializer, U7's job).
3. `src/Kumunita.Core/Announcements/IAnnouncementService.cs` — confirm
   `CreateAsync` / `UpdateAsync` signatures (you are **not** changing them —
   they already take a full `Announcement` POCO, so no interface edit is
   needed; this read is to confirm that so you don't add a spurious param).
4. `src/Kumunita.Core/Announcements/Announcement.cs` — confirm the
   `AttachmentIds` field exists (U3) and note its `= []` default.

## Deliverables (1 edit, in `Kumunita.Core`)

### `AnnouncementService.UpdateAsync` — persist `AttachmentIds` (C-ATT·4/5)
- `src/Kumunita.Core/Announcements/AnnouncementService.cs` — in the
  `UpdateAsync` field-copy block (≈ L287), add **immediately after** the
  `existing.ImageIds = updated.ImageIds ?? [];` line:

  `existing.AttachmentIds = updated.AttachmentIds ?? [];` with a doc-comment in
  the **exact style** of the `ImageIds` line:
  "ATT U5 (C-ATT·5) — the announcement edit lane persists the
  server-side-parsed attachment ids (POCO-direct: the Web layer parses the
  body, Core writes it verbatim — the same field-copy shape as the
  `ImageIds` line above; null-coalesce to the POCO's non-null empty list)."

- **The create path needs no service edit here.** `CreateAsync` stores the
  POCO the controller builds; the controller's object-initializer (U7) supplies
  `AttachmentIds`. Do **not** add a `changed`-computation term for
  `AttachmentIds` unless the existing `changed` block also lists `ImageIds` —
  it **does not** (the `changed` block lists Title/Body/Scope/Pinned/
  CommunityId/LanguageCode, not `ImageIds`). Match that: leave `changed`
  untouched, exactly as the image lane does. (If you find `ImageIds` *is* in
  the `changed` block in the real tree, then mirror it for `AttachmentIds` and
  record that drift in the handoff — but per the code above, it is not.)

## Build gate (must be green before you finish)

```
dotnet build Kumunita.slnx -c Debug
```
Green on **Core** and **Web**. Web should compile unchanged (you touched one
assignment line in Core; no signature change).

## Risks & open questions

- **Do NOT add `AttachmentIds` to the `changed` block** (unless `ImageIds` is
  already there — it isn't). The image lane deliberately doesn't re-stamp
  `Modified` for image-only edits; matching that keeps `Modified` semantics
  identical across both lanes.
- **Do NOT edit the create path in Core.** The create write is the controller
  object-initializer (U7). If you add a `CreateAsync` line that
  re-assigns `AttachmentIds`, you're duplicating U7's work and creating a
  second source of truth.
- **Do NOT touch `FindByAttachmentIdAsync`** (U3) or the `ImageIds` line
  (C-ATT·9).
- **Do NOT write tests.** U6 owns the announcement Core test
  (`AnnouncementCreate_PersistsAttachmentIds`,
  `AnnouncementEdit_ReparsesAttachmentIds`). If you add a throwaway test,
  delete it before finishing.

## Steps

1. Read the 4 entry reads (design doc §2.3 first, then the `UpdateAsync`
   region, then the interface + POCO confirmations).
2. In `AnnouncementService.UpdateAsync`, add the `existing.AttachmentIds =
   updated.AttachmentIds ?? [];` line immediately after the `ImageIds` line,
   with the style-matched doc-comment.
3. `dotnet build Kumunita.slnx -c Debug` → confirm green on Core **and** Web.
4. Re-read the edit: confirm the `ImageIds` line is unchanged, the new line is
   directly below it, and the `changed` block is untouched.
5. Append a `## U5` section to
   `docs/plans-milestones/file-attachments-handoff-notes.md` recording:
   "`AnnouncementService.UpdateAsync` now copies
   `existing.AttachmentIds = updated.AttachmentIds ?? [];` (mirrors the
   `ImageIds` line). Create path left to the controller initializer (U7).
   `changed` block untouched (matches the image lane). Build green
   (Core+Web). Drift: <none / describe>. Next agent (U6) writes the **Core
   tests** for all three owners' create/edit + reverse-lookup — the 10 pinned
   names in design doc §2.9."
6. Done.
