# ATT U10 — Editor: `attachLink` + "Attach file" button (incl. reply composers) + F9 round-trip test

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-file-attachments.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.
> **Precondition:** U8 + U9 shipped (the `POST /attachment` upload + the
> `GET /attachment/{id}` serve both exist). This unit is **code (TS + 1 test)**
> — it ends with a green `dotnet build` (which compiles the TS via the
> `ts:build` step the project runs) **and** a green `npm --prefix
> src/Kumunita.Web run build`.

## Understanding

U8/U9 made attachments **storable** and **servable**. This unit makes them
**composable** — the resident can attach a file from the editor. Concretely:
(a) a pure `attachLink(label, id)` fn (an `<a>` link, not an `<img>` —
C-ATT·2), and (b) an "Attach file" `button[data-md="attach"]` wired in
`bindRichEditor` that uploads via the **U8 `POST /attachment`** lane and splices
`attachLink(label, id)` at the caret. The round-trip (F9) is that the spliced
`[label](/attachment/{id})` survives editor → textarea → server parse (U7's
`ExtractAttachmentIds`) → re-render as a working `<a href>`.

**The nuance (the thing most easily drifted):** reply composers carry
`data-rich-editor-no-image` (the **Image** button is removed there). The
**"Attach file" button must STILL appear in reply composers** — only the Image
button is suppressed there. The existing binder's no-image suppression targets
**`button[data-md="image"]` specifically** — your new
`button[data-md="attach"]` is a **different** `data-md` value, so it is
**already not** caught by that suppression. **Do not add the attach button to
the no-image removal list.**

## The invariants you are implementing

- **C-ATT·2** — `attachLink` produces an **`<a>` link**
  (`[label](/attachment/{id})`), **never** an `<img>`. The splice path is the
  **link** path (`applyLink` / `wrapSelection('a', …)`), not the image's
  `<img>`+blob-preview path.
- **C-ATT·9** — the image lane (`imageLink`, the `data-md="image"` handler,
  `isSafeImageSrc`, the `data-rich-editor-no-image` removal) is **untouched**.
  You add the attach fn + handler **alongside**.
- **F9** — the spliced link round-trips: the body carries the exact
  `/attachment/{id}` form that U7's `ExtractAttachmentIds` parses, and the
  rendered HTML carries a working `<a href="/attachment/{id}">`.

## Entry reads (do NOT scan the repo — read exactly these)

1. `docs/design/file-attachments-design.md` — **§2.8** (the `attachLink` shape
   + the button wiring + the reply-composer nuance) and **§2.9** (the F9 test
   name: `AttachRoundtrip_F9_HrefPreserved`). Read both fully.
2. `src/Kumunita.Web/client/lib/rich-editor.ts` — the **mirror source**. Read
   **three regions**:
   - `imageLink` (≈ L408) + `applyLink` (≈ L387) — the pure splice fns. Your
     `attachLink` sits beside `imageLink`; the attach button reuses the
     **link** splice (the `kind === 'link'` handler at ≈ L1113 uses
     `wrapSelection('a', …)` — the attach handler will be the same shape,
     driven by an upload).
   - The `data-md="image"` handler (≈ L1120–L1190) — the **upload + splice**
     idiom: `fileInput` → `apiFetch('/content-image', …)` → splice at the
     caret → `syncTextarea()`. Your `data-md="attach"` handler mirrors this
     **except** it POSTs to `/attachment`, takes a **label** (not an `alt`),
     and splices an **`<a>`** (not an `<img>`).
   - The `data-rich-editor-no-image` removal (≈ L490–L497) — confirm it
     targets **only** `button[data-md="image"]` (it does). This is the
     **nuance**: the attach button is a different `data-md` and is
     **automatically** left in reply composers. **Do not change this block.**
3. `src/Kumunita.Web/client/lib/api.ts` — the `apiFetch` signature (the
   image handler's `apiFetch<{ id: string }>('/content-image', { method:
   'POST', body: fd })` call). Confirm the shape — your attach handler calls
   `apiFetch<{ id: string }>('/attachment', { method: 'POST', body: fd })`.
4. `src/Kumunita.Web/client/lib/dom-to-markdown.ts` — the serializer. Confirm
   an `<a href="/attachment/{id}">` element serializes to
   `[label](/attachment/{id})` (the generic `<a>` → `[label](url)` rule — the
   F9 "no serializer change" claim). **Read the `<a>` serialization branch**
   to confirm it emits the `href` verbatim (not just `image` serialization).
5. One toolbar view that carries the Image button — e.g.
   `src/Kumunita.Web/Views/Posts/New.cshtml` (grep `data-md="image"` → the
   toolbar partial/inline markup). Read ~30 lines around it to see the exact
   `<button data-md="image">…</button>` markup you are mirroring for
   `<button data-md="attach">…</button>`.

## Deliverables (4 edits: 1 TS fn, 1 TS handler, N view buttons, 1 test)

### 1. `rich-editor.ts` — the `attachLink` pure fn (C-ATT·2)
Add **after** `imageLink` (≈ L411):

```ts
/**
 * ATT U10 (C-ATT·2) — the **exact** attachment link form the server parse
 * (`AttachmentIds.ExtractAttachmentIds` = `/attachment/([0-9a-f]{1,128})(?![0-9a-f])`)
 * already understands. An `<a>` link, never an `<img>` (the difference from
 * `imageLink`): a file is a **download**, not an inline render. Splicing this
 * form is byte-identical to a resident hand-typing the link.
 */
export function attachLink(label: string, id: string): string {
  return `[${label}](/attachment/${id})`;
}
```

### 2. `rich-editor.ts` — the `data-md="attach"` handler (C-ATT·2, F9)
In the `bindRichEditor` button-wiring loop (the `for (const btn of
toolbar.querySelectorAll('button[data-md]'))` at ≈ L954 / the per-kind
`else if` chain at ≈ L1113), add an **`else if (kind === 'attach')`** branch
**after** the `kind === 'image'` branch. The shape mirrors the image handler
**minus** the `<img>`/blob-preview:

```ts
} else if (kind === 'attach') {
  // ATT U10 (C-ATT·2) — reuse the **U8 upload lane** (`POST /attachment`),
  // but splice an **<a> link** (a download), not an <img>. The label is
  // prompted (the link convention), the id is the U8 content-hash.
  const label = window.prompt('Link text:', 'Attachment') ?? 'Attachment';
  if (!label) return;
  const fileInput = document.createElement('input');
  fileInput.type = 'file';
  // No `accept` restriction in the editor — the U8 allowlist is the gate
  // (C-ATT·6); a rejected type 415s and the handler alerts.
  fileInput.addEventListener('change', async () => {
    const file = fileInput.files?.[0];
    if (!file) return;
    try {
      const fd = new FormData();
      fd.append('file', file);
      const { id } = await apiFetch<{ id: string }>(
        '/attachment',
        { method: 'POST', body: fd },
      );
      const canonical = `/attachment/${id}`;
      // F9: the value is the exact /attachment/{id} form U7's
      // AttachmentIds.ExtractAttachmentIds parses (zero server change).
      // Splice an <a> at the caret (the link convention), then sync.
      const a = document.createElement('a');
      a.setAttribute('href', canonical);
      a.textContent = label;
      const range = activeRange();
      if (range) {
        range.deleteContents();
        range.insertNode(a);
      } else {
        previewPane!.appendChild(a);
      }
      placeCaretAfter(a);
      syncTextarea();
    } catch (err) {
      window.alert(err instanceof Error ? err.message : 'Upload failed.');
    }
  });
  fileInput.click();
}
```

(Adjust `activeRange` / `placeCaretAfter` / `previewPane` / `syncTextarea` to
the **real** names in this file — they are the same helpers the image handler
uses at ≈ L1170–L1188; read that region to confirm the exact identifiers and
use them verbatim. If the real helpers differ — e.g. the image handler uses
`previewPane!.appendChild` for the no-caret fallback — match **it**, not this
snippet, and record the drift.)

### 3. The toolbar views — add the "Attach file" button (N views)
In **each** composer toolbar that already carries the Image button (grep
`data-md="image"` across `src/Kumunita.Web/Views/**/*.cshtml` to enumerate
them — the post create, the reply composer, the announcement create/edit, the
group-post create/edit), add **next to** the existing `<button
data-md="image">…</button>`:

```html
<button type="button" class="rc-btn" data-md="attach"
        title="Attach a file">Attach file</button>
```

(match the **real** `class`/`title`/markup of the adjacent image button — read
the view to copy its exact attribute set; the `data-md="attach"` value is the
contract, the rest is cosmetic parity). **Do not** add the attach button to
any view that is *not* a composer (a read-only detail view has no toolbar).

**The reply-composer nuance (do not drift):** the reply composers carry
`data-rich-editor-no-image`. That flag removes **only** `button[data-md="image"]`
(the binder at ≈ L490–L497). Your `button[data-md="attach"]` is a **different**
`data-md` and is **not** removed — so the attach button **does** appear in
reply composers, which is correct (the U9 reply-serve resolves the parent, so
a reply's attachment is downloadable). **Do not** add `attach` to the
no-image removal list, and **do not** add `data-rich-editor-no-attach` anywhere.

### 4. The F9 round-trip test (the one test this unit owns)
The F9 test (`AttachRoundtrip_F9_HrefPreserved`) is a **Web** test — but it is
pinned in the **U11** Web test set (§2.9). **This unit does not author it** —
U11 does (it needs the U7 parse + the U9 serve + this unit's editor to be in
place, and it owns the `Kumunita.Web.Tests` file). **However**, you **must**
verify the round-trip yourself before finishing: re-read the
`dom-to-markdown.ts` `<a>` branch (entry read 4) and confirm an `<a
href="/attachment/{id}">` serializes to `[label](/attachment/{id})` — the F9
"no serializer change" claim. If the serializer does **not** emit the `href`
verbatim (e.g. it only special-cases `/content-image/`), you must record that
in the handoff note **and** flag U11, because F9 would then need a serializer
change (a C-ATT·2-adjacent drift). **Do not** add the F9 test to
`Kumunita.Core.Tests` (it references no Web) — that would be the wrong
assembly.

## Build + test gate (both must be green before you finish)

```
npm --prefix src/Kumunita.Web run build
```
Green (the TS compiles). **Then**:

```
dotnet build Kumunita.slnx -c Debug
```
Green on Core + Web (the Razor views that reference the new button still
compile). You do **not** run the Web test suite in this unit (U11 owns the F9
test) — but the **build** must be green.

## Risks & open questions

- **The reply-composer nuance is the top drift point.** If you add the attach
  button to the no-image removal list, or add a `data-rich-editor-no-attach`
  flag, you've broken the lane's intent (reply attachments must be
  composable). The existing no-image block targets **only**
  `button[data-md="image"]` — leave it byte-for-byte (C-ATT·9).
- **The splice is an `<a>`, not an `<img>` (C-ATT·2).** If you mirror the
  image handler's `<img>`+blob-preview path for the attach button, you've
  turned the attachment into a second image. The attach handler uses the
  **link** convention (`<a>` + `applyLink`/`wrapSelection('a', …)`), not the
  image's `<img>`+`data-cid`+`serializeImage` path.
- **The F9 serializer claim must be verified, not assumed.** If
  `dom-to-markdown.ts` doesn't emit `<a href>` verbatim, F9 needs a change —
  record it and flag U11. Do **not** silently "fix" the serializer in this
  unit (that's a renderer-adjacent change and belongs in the drift log).
- **Do NOT touch `imageLink`, `isSafeImageSrc`, the image handler, or the
  no-image removal** (C-ATT·9). Your additions go **after** / **alongside**.
- **The `accept` attribute on the attach `fileInput`.** Leave it **unset**
  (any file) — the U8 allowlist is the gate (C-ATT·6). Setting `accept` to the
  allowlist is a UX nicety but **not** a security boundary; if you add it, use
  the same 11 types as the U8 default for consistency and record it. Do **not**
  rely on `accept` as the gate.
- **Do NOT author the F9 test in this unit** — U11 owns it (it needs U7+U9+this
  unit in place). You **verify** the round-trip by reading the serializer and
  record the result; U11 writes the executable test.

## Steps

1. Read the 5 entry reads (design doc §2.8/2.9 first, then the three
   `rich-editor.ts` regions + `api.ts` + `dom-to-markdown.ts` + one toolbar
   view).
2. Add the `attachLink` pure fn after `imageLink`.
3. Add the `data-md="attach"` handler after the `data-md="image"` handler,
   using the real helper names from the image handler region.
4. Add the `<button data-md="attach">` to each composer toolbar (grep
   `data-md="image"` to enumerate the views). Confirm the reply composers keep
   the attach button (the no-image flag doesn't touch it).
5. **Verify** the F9 round-trip by reading the `dom-to-markdown.ts` `<a>`
   branch (record the result in the handoff note; flag U11 if it needs a
   change).
6. `npm --prefix src/Kumunita.Web run build` → green. `dotnet build
   Kumunita.slnx -c Debug` → green.
7. Re-read the edits: confirm `attachLink` is an `<a>` form, the attach handler
   POSTs to `/attachment` and splices an `<a>`, the image lane is untouched,
   and the reply composers retain the attach button.
8. Append a `## U10` section to
   `docs/plans-milestones/file-attachments-handoff-notes.md` recording:
   "Added `attachLink` (`<a>` form) + the `data-md='attach'` editor handler
   (POSTs `/attachment`, splices an `<a>`); added the 'Attach file' button to
   each composer toolbar (incl. reply composers — the no-image flag doesn't
   touch it). TS build green; Core+Web build green. **F9 serializer check:**
   <dom-to-markdown emits `<a href>` verbatim / needs a change — flag U11>.
   Drift: <none / describe — esp. the real helper names and the F9
   serializer result>. Next agent (U11) writes the **Web tests** — the 10
   pinned names from design doc §2.9, including F9."
9. Done.
