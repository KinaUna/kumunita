# U4 — `PageController` + the tree browse + the post view + the composer

- **Lane:** Pages (`PG`)
- **Unit:** U4 (of U0–U07)
- **Kind:** Web surface (the first user-facing unit — controller + views +
  tests)

## Goal

Add **`Kumunita.Web/Controllers/PageController.cs`** with the **tree browse**
(`GET /pages`), the **post view** (`GET /pages/{**path**}`), the **composer**
(`GET`/`POST /pages/new`), the **edit lane** (`GET`/`POST /pages/{id}/edit`),
and the standing-gated **`publish` / `delete` / `move`** actions. Wire the
**mount-point resolver** into the layout (the `footer/community` about slot +
the `help/account` slot). Reuse the **`AudienceEditorModel`** verbatim and the
**WYSIWYG** `bindRichEditor` + RC image + ATT attachment surface (the
`PreviewPage.cshtml` wiring pointed at a `Page` body). Then the
**`PageControllerTests`** (NSubstitute `IPageService`, no live Postgres).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/pages-design.md` §3.8 — the **Web surface** contract (the
   route map, the 404-vs-403 split, the mount-point resolver, the composer
   shape).
2. `src/Kumunita.Web/Controllers/AnnouncementController.cs` — the **route
   template** (the 404-vs-403 split, the `[Authorize]` pre-gate, the
   `AudienceEditorModel` round-trip via `FromAudience`/`BuildAudience`, the
   view-model shape).
3. `src/Kumunita.Web/Views/Languages/PreviewPage.cshtml` — the **working
   WYSIWYG page editor** (`bindRichEditor` + RC image + ATT attachment over a
   `textarea[data-rich-editor]`) the composer reuses verbatim.
4. `src/Kumunita.Web/Models/AudienceEditorModel.cs` — the **audience
   round-trip** (`BuildAudience()` / `FromAudience`), reused verbatim for a
   `Page`.
5. `src/Kumunita.Web/Views/Shared/_Layout.cshtml` (or the footer partial) —
   where the **mount-point resolver** plugs in (the `footer/community` about
   slot + the `help/account` slot resolve `GetByMountPointAsync(slot)`).

## Deliverables (closed set)

1. **`GET /pages`** — the **tree browse** (folders/files),
   `CanSeeAsync(Read)`-filtered (the C6 aggregate row — no private-page
   leakage), rendering the forest as a nested list.
2. **`GET /pages/{**path**}`** — the **post view**: title + rendered body via
   the **one** `MarkdownRenderer` + the ADR 0027 chip-swap over the
   `PageTranslation` rows; `CanAsync(Read)`-gated; **404** on absent, **403**
   on denied (the announcement split — the RC serving-route convention).
3. **`GET /pages/new` + `POST /pages/new`** — the **composer**: title, body
   (WYSIWYG `textarea[data-rich-editor]`), parent picker, the
   **`AudienceEditorModel`** verbatim, the ADR 0018 language picker. Calls
   `IPageService.CreateAsync`.
4. **`GET /pages/{id}/edit` + `POST /pages/{id}/edit`** — the **edit lane**:
   round-trips the audience verbatim (the ADR 0036 `FromAudience`/
   `BuildAudience` shape); calls `IPageService.UpdateAsync`.
5. **`POST /pages/{id}/publish` / `delete` / `move`** — the standing-gated
   actions; each a thin call into `IPageService`, the `[Authorize]` a
   convenience pre-gate (the C3 server-side re-check is the source of truth).
6. **Mount-point resolver** in the layout: `footer/community` (the about
   slot) + `help/account` (the account-help slot) resolve
   `GetByMountPointAsync(slot)` and `<a href>` the mounted page (or omit the
   slot when no page is mounted).
7. **Views** under `Views/Pages/` — `Index.cshtml` (tree), `Show.cshtml`
   (post view + chip-swap), `New.cshtml` (composer), `Edit.cshtml` (edit
   lane). The composer/edit reuse the `PreviewPage.cshtml` WYSIWYG markup.
8. **`PageControllerTests`** (`Kumunita.Web.Tests`, NSubstitute `IPageService`,
   no live Postgres — the `AnnouncementControllerTests` shape): the route map,
   the 404-vs-403 split (absent → 404, denied → 403), the tree filter
   (a `CanSeeAsync`-denied page is absent from `/pages`), the mount-point
   resolution (the about slot renders the mounted page's href), and the
   composer/edit audience round-trip (set a community audience → it comes back
   through `BuildAudience` unchanged).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green — the `PageControllerTests` family passes.
- A resident (in a live run) can browse the tree, open a page (post view),
  compose + edit a page in the WYSIWYG editor, set an audience
  (public/community/grants), and the about slot renders the mounted page.
- **`LocalizedPage` still untouched** (U07 retires it).
- Append a `## U4 — PageController + tree + post view + composer` section to
  `pages-handoff-notes.md`.

## Notes / deviations

- The **404-vs-403 split is load-bearing.** A page that *exists but is denied*
  to the actor is **403**; a page that *does not exist* is **404**. Do **not**
  collapse the two into one — the RC serving-route convention + the
  announcement split pin this.
- The **`[Authorize]` is a pre-gate only.** The C3 server-side re-check in
  `IPageService` (U02/U03) is the source of truth. A controller action with no
  `[Authorize]` that calls a standing-gated `IPageService` method is still
  safe — that's the point.
- The composer is the **same editor** posts/announcements use (one
  `bindRichEditor`, one `MarkdownRenderer`). Do **not** introduce a second
  editor binding or a second renderer (the lane's core invariant).
- The **mount-point resolver** is a **display** concern (where to surface a
  link), **not** an access boundary — access is always `Audience` +
  `CanAsync(Read)`. A mounted page that the actor can't read renders as an
  href, and *opening* it is the separate `Read` decision (403 on denied).
