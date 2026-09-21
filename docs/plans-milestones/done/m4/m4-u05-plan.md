# U05 — `EventController` + feed / detail / composer views

- **Lane:** Events (`M4`)
- **Unit:** U05 (of U00–U12)
- **Kind:** surface (controller + views — no tests)

## Goal

Create the `EventController` (namespace `Kumunita.Web.Controllers`) +
the three Razor views (feed / detail / composer). Mirror the
`AnnouncementController` + its views (the feed / detail / composer
shape). The composer reuses the existing WYSIWYG editor
(`bindRichEditor` — ADR 0031) + the `AudienceEditorModel` (M2) + the
`kw-dt` TagHelper (ADR 0019 / 0020). **No new renderer, no new editor,
no new TS module.**

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Web/Controllers/AnnouncementController.cs` — the
   controller shape to mirror (the `Index` / `Detail` / `Create` /
   `Edit` / `Delete` actions; the `ViewBag` setup; the `Redirect`
   shape).
2. `src/Kumunita.Web/Views/Announcement/Index.cshtml` — the feed view
   to mirror (the card list; the `kw-dt` timestamp; the audience
   badge).
3. `src/Kumunita.Web/Views/Announcement/Detail.cshtml` — the detail
   view to mirror (the `MarkdownRenderer` output; the
   `kw-dt` timestamps; the edit / delete buttons).
4. `src/Kumunita.Web/Views/Announcement/Create.cshtml` — the composer
   view to mirror (the `bindRichEditor` script block; the
   `AudienceEditorModel` form fields; the tag picker; the image /
   attachment picker).
5. `docs/design/m4-events-design.md` §3.1 + §3.7 — the `Event` field
   set + the 23 test names (the primary source for this unit).

## Deliverables (≤ 6 files)

1. **`src/Kumunita.Web/Controllers/EventController.cs`** — the
   controller (the `AnnouncementController` shape to mirror):
   - `Index(string? componentId, string? tagId)` — the feed (the
     `IEventService.ListUpcomingAsync` call; the `ViewBag` setup).
   - `Detail(string id)` — the detail (the `IEventService.GetAsync`
     call; the `ViewBag` setup).
   - `Create(string? componentId)` — the composer (the
     `AudienceEditorModel` + the `bindRichEditor` script block).
   - `Edit(string id)` — the edit (the `IEventService.GetAsync` call;
     the `AudienceEditorModel` + the `bindRichEditor` script block).
   - `Publish(string id)` — the publish (the `IEventService.
     PublishAsync` call; the `Redirect` shape).
   - `Delete(string id)` — the soft-delete (the `IEventService.
     DeleteAsync` call; the `Redirect` shape).
   - `Rsvp(string id, EventRsvpStatus status)` — the RSVP (the
     `IEventService.RsvpAsync` call; the `Redirect` shape).
2. **`src/Kumunita.Web/Views/Event/Index.cshtml`** — the feed view
   (the `AnnouncementController` feed view to mirror — the card list;
   the `kw-dt` timestamp; the audience badge).
3. **`src/Kumunita.Web/Views/Event/Detail.cshtml`** — the detail view
   (the `AnnouncementController` detail view to mirror — the
   `MarkdownRenderer` output; the `kw-dt` timestamps; the edit /
   delete / RSVP buttons).
4. **`src/Kumunita.Web/Views/Event/Create.cshtml`** — the composer
   view (the `AnnouncementController` composer view to mirror — the
   `bindRichEditor` script block; the `AudienceEditorModel` form
   fields; the tag picker; the image / attachment picker; the `Start` /
   `End` datetime fields; the `Location` / `Capacity` fields).
5. **`src/Kumunita.Web/Views/Event/Edit.cshtml`** — the edit view
   (the `Create.cshtml` shape to mirror, pre-filled with the existing
   event's values).
6. **`src/Kumunita.Web/Models/EventEditorModel.cs`** — the editor
   model (the `AnnouncementEditorModel` shape to mirror — the `Title` /
   `Body` / `ComponentId?` / `Start` / `End` / `Location?` / `Capacity?`
   / `AudienceEditorModel` / `ReminderEnabled` / `LanguageCode` /
   `TagIds` / `ImageIds` / `AttachmentIds` fields).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `npm --prefix src/Kumunita.Web run build` green (the TS build — no
  new TS, but the `bindRichEditor` script block references the existing
  `wwwroot/js/lib/rich-editor.js`).
- The `EventController` + the five views + the `EventEditorModel`
  compile.
- **No new test** (U10's controller tests are the first M4 Web tests).
- Handoff note: 6 lines starting `## U05 — EventController + views` —
  (a) the `EventController` action count (7: `Index` / `Detail` /
  `Create` / `Edit` / `Publish` / `Delete` / `Rsvp`), (b) the view count
  (5: `Index` / `Detail` / `Create` / `Edit` + the `EventEditorModel`),
  (c) the `bindRichEditor` script block (the ADR 0031 WYSIWYG editor
  reused — no new TS), (d) the `AudienceEditorModel` form fields (the
  M2 audience editor reused), (e) the `kw-dt` TagHelper usage (the ADR
  0019 / 0020 timezone / format resolvers reused).

## Notes / deviations

- The composer reuses the **existing WYSIWYG editor**
  (`bindRichEditor` — ADR 0031) + the `AudienceEditorModel` (M2) + the
  `kw-dt` TagHelper (ADR 0019 / 0020). **No new renderer, no new
  editor, no new TS module.**
- The `EventController` actions are the **`AnnouncementController`
  shape to mirror** (the `Index` / `Detail` / `Create` / `Edit` /
  `Delete` actions; the `ViewBag` setup; the `Redirect` shape). **No
  new controller mechanism.**
- The `EventEditorModel` is the **`AnnouncementEditorModel` shape to
  mirror** (the `Title` / `Body` / `ComponentId?` / `Start` / `End` /
  `Location?` / `Capacity?` / `AudienceEditorModel` / `ReminderEnabled`
  / `LanguageCode` / `TagIds` / `ImageIds` / `AttachmentIds` fields).
  **No new editor model mechanism.**
- The RSVP action is the **last-write-wins** concurrency exception (the
  `docs/ARCHITECTURE.md` §5 exception — keyed per `(EventId, UserId)`;
  a conflicting RSVP is a no-op, the resident's latest status is the
  truth). **No `AccessAudit` row** on an RSVP (a routine resident
  action, not an access decision).
