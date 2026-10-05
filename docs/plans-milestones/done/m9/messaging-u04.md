# M9 Messaging — U04 · Web surface: resident messaging

> **Unit plan (secondary tier, self-contained).** You are a fresh-context
> agent executing one unit of M9 (Messaging). Read **this file + your entry
> reads below** — that is all the context you need. The register is
> `docs/plans-milestones/plan-m9-messaging.md`; the scratch handoff note is
> `docs/plans-milestones/m9-messaging-handoff-notes.md`. **Atomicity
> contract:** ≤ 5 files, ≤ ~400 LOC, exit = one build + app smoke.
> **Unit-series rule:** never touch files outside your own Deliverables; no
> tests beyond the pinned list (none in this unit — U06 owns the Web tests);
> no new seams on frozen interfaces.

## Goal

F1–F5/F7 rendered: the `/messages` conversation list + `/messages/{id}`
thread (read + compose + mark-read), the nav entry (hidden when the toggle
is off — the `IsMessagingEnabledAsync` read), and the `message.*` `kw-l`
keys (all four languages).

## Context (the [PROPOSED] shape U00 locked)

- **D2 · The surface respects the toggle.** The controller reads
  `IsMessagingEnabledAsync()` first. When **off**: the nav entry is hidden,
  `/messages` and `/messages/{id}` 404 (or redirect to home) — the service
  seams refuse too (U03), so the view is belt-and-braces, not the gate.
  When **on**: the nav entry renders and the surfaces are available.
- **C-M9·1 · Non-participants see nothing.** The controller does **not**
  re-decide access — U03's `GetConversationAsync` already returns a non-leaky
  404 for a non-participant; the controller just maps that to a 404 (or the
  view's empty state). Do **not** add an `IAuthorizationService` call here.
- **F7 · Localized.** Every resident-facing string is a `message.*` `kw-l`
  key, present in **all four languages** (`en`/`de`/`fr`/`da`) —
  `KnownTranslationKeys_ParityTests` enforces the closure.
- **M7 paging.** The list + thread render with `HasMore` + the shared
  `_Pager` partial (the ADR 0090 "no new pager idiom" discipline) — reuse
  `PagedViewModel` + `_Pager` exactly as the M8 search view does.
- **Nav entry.** Where the `_AccountNav.cshtml` conditional already gates a
  link on a service read (`IsSignupOpenAsync` at ~line 275), add the
  messaging entry the same way (gated on `IsMessagingEnabledAsync()`), with
  **no reflow** of the existing nav.

## Entry reads (5)

1. `docs/design/m9-messaging-design.md` §web + §FACES + §keys (the locked
   shapes + the key list).
2. `src/Kumunita.Web/Controllers/AnnouncementController.cs` (a `[Authorize]`
   signed-in controller + the toggle-read gate pattern at ~line 308).
3. `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` (the nav block — the
   `IsSignupOpenAsync` conditional at ~line 275 to mirror).
4. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the closed-key
   registry — new keys need all four languages).
5. `src/Kumunita.Web/Views/Shared/_Pager.cshtml` (the shared pager, M7
   discipline).

## Deliverables (5)

- `src/Kumunita.Web/Controllers/MessagesController.cs` — `[Authorize]`
  signed-in; `IsMessagingEnabledAsync()` gate first (off → 404/redirect);
  `Index` (`ListConversationsAsync`), `Thread` (`GetConversationAsync` +
  `MarkReadAsync` on entry), `Send` (`SendAsync`, POST).
- `src/Kumunita.Web/Views/Messages/Index.cshtml` — the conversation list +
  the "new conversation" picker (a resident picker — reuse the existing
  directory-picker pattern if your entry reads surface one; otherwise a
  simple resident id/name input).
- `src/Kumunita.Web/Views/Messages/Thread.cshtml` — the messages (with
  unread markers), the composer, `_Pager` if `HasMore`.
- `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` — the nav entry only
  (gated on `IsMessagingEnabledAsync()`, no reflow).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the new keys,
  **all four languages**.

## New keys (provisional — the design doc is authoritative)

`message.nav` · `message.title` · `message.new` · `message.thread.empty` ·
`message.compose.placeholder` · `message.compose.send` · `message.unread` ·
`message.disabled` · `message.other` (the "other participant" label).

## Exit

`dotnet build Kumunita.slnx -c Debug` green. App smoke
(`dotnet run --project src/Kumunita.Web` + browser): with the toggle **on**,
`/messages` renders the list, a thread renders messages, the nav entry is
present; with it **off**, the nav entry is hidden and `/messages` 404s or
redirects. `KnownTranslationKeys_ParityTests` still green.

Append a `## U04 — resident surface` section to the handoff note: the keys
added (list), the nav placement (the file + the neighbor it sat next to),
the controller→view-model shapes, any drift (e.g. a picker pattern your entry
reads surfaced that the design doc didn't name).

**Last action:** once the Exit above is satisfied and the `## U04` section is
appended, move **this unit's own plan file** from
`docs/plans-milestones/in-progress/messaging-u04.md` to
`docs/plans-milestones/done/m9/messaging-u04.md`. Each unit moves only its own
file as it completes — U05's plan is already sitting in `in-progress/`, so
the next agent just reads it there.
