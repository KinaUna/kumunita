# U0 — Kickoff: verify the two RC gaps + confirm the frozen base

- **Lane:** Rich editor (`RE`)
- **Unit:** U0 (of U0–U8)
- **Kind:** kickoff / verification (no code — authoring + read-only checks)

## Goal

Confirm, against the current code, the two gaps this lane closes (composers
show **raw Markdown only**; there is **no** preview pane or toolbar), confirm
the frozen RC base is intact (so U03 can build against it), and seed the
scratch handoff note with a `## Lane open` section + a `## U0` section. This
unit is the fresh-agent's first read — it exists to make every later unit's
entry reads correct.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/plans-milestones/plan-rich-editor.md` — **this lane's register**
   (the primary source of truth for what this lane is and is not).
2. `docs/plans-milestones/done/rich-content-handoff-notes.md` — the **RC
   close** + its **drift pauses**. **Read the drift-pause sections** — they
   name the per-surface RC image-lane gaps that determine which surfaces
   get the image button ON vs OFF (via U03's `data-rich-editor-no-image`):

   - **Post New** — RC lane complete (write: `PostsController:692` →
     `ImageIds`; serve: post branch). Image button **ON**.
   - **Post Edit** — RC drift pause (c): `UpdatePostAsync` never sets
     `ImageIds`. Image button **OFF** (U04 gates it).
   - **Group New** — RC lane complete (write: `GroupsController:1085` →
     `ImageIds`; serve: post branch). Image button **ON**.
   - **Group Edit** — `UpdateGroupPostAsync` never sets `ImageIds`
     (drift pause (c) analog). Image button **OFF** (U05 gates it).
   - **Announcement New/Edit** — write lanes wired (`AnnouncementController:
     455`, `:575`), but `ContentImageController` has **no** announcement
     serve branch (RC U03: inert-404). Image button **OFF** (U05 gates it).
   - **Static page** — RC lane complete (write: `LanguagesController:271` →
     `ImageIds`; serve: page branch, public). Image button **ON** (U06).
   - **Reply New/Edit** — RC drift pause (a) + RC U03: write lane never sets
     `PostReply.ImageIds`; serve branch inert-404. Image button **OFF**
     (U06 gates it).

   The **text toolbar + preview are ON everywhere** (all those bodies already
   render via the one `MarkdownRenderer`) — the gates are image-button-only.
   The register's scope section has the full matrix as the single source of
   truth.
3. `src/Kumunita.Web/client/lib/insert-image.ts` — the TS module + self-wire +
   cursor-splice pattern U03 will **mirror** (not invent).
4. `src/Kumunita.Web/Views/Posts/New.cshtml` — **one** body textarea + the
   existing `rc-insert-image` block (the surface the toolbar will
   complement/replace).
5. `src/Kumunita.Web/package.json` + `tsconfig.json` — the **`tsc`-only**
   constraint (no bundler, no editor dep) that RE·3/D1 depend on.

## Deliverables (closed set)

1. **`docs/plans-milestones/in-progress/rich-editor-handoff-notes.md`** — the
   `## Lane open` section is already authored (part of the up-front plan
   setup). This unit **appends** a `## U0 — Kickoff verified` section recording:
   - the two gaps, each with the **exact file + line** evidence (e.g. "the
     body `<textarea>` at `Posts/New.cshtml:90` has no adjacent preview pane,
     no toolbar");
   - the frozen RC base, each seam named with its **confirmed** current
     location (`MarkdownRenderer` → `Kumunita.Web.Security`, `ContentImageIds`
     → same, `.rc-body`/`.rc-image` → `site.css:733–790`, `insert-image.ts`
     → `client/lib/`);
   - a one-line confirmation that `package.json` is still `typescript`-only
     (RE·3 holds at lane open).
   - **Nothing else.** No source, test, CSS, or `.csproj` change in this unit.

## Exit

- The handoff note's `## U0 — Kickoff verified` section is present and its
  evidence (file + line) matches what a re-read finds (a fresh agent can
  trust the lines it cites).
- If **either** gap has already been closed (e.g. a preview pane or toolbar
  now exists) **or** a frozen RC seam has moved/changed, this unit **pauses**
  and appends `## U0 — Drift pause` naming the exact seam + what changed.
- **No** build is required (doc-only). **No** `npm run build` (no TS touched).
- Move this plan file `in-progress/` → `done/` (move **last**, after the
  handoff section is appended).
- `git status` clean except the handoff-note append + this file's move.

## Notes / deviations

- If U0 finds the `rc.markdown_hint` key **still** unregistered (RC U05's
  drift pause), it records that as a **known gap** the RE lane will close in
  U08 (the `rc.editor.*` registration — but `rc.markdown_hint` itself is an RC
  debt; flag it, do **not** silently fold it into `rc.editor.*`).
- U0 does **not** author the design doc or ADR — those are U01. U0 does **not**
  write any TS/CSS. Its only write is the one handoff section.
