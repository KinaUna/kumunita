# M12 · U04 — Surface: the two `kw-l` affordances × en/de/fr/da + the phone-width re-check

> **You are the U04 agent.** Read **this file + your entry reads** and
> you can execute. U00–U03 have shipped the two routes — this file
> restates the D5 affordance contract you need.

## Goal

D5 rendered — the two quiet in-app affordances, both **plain links**
(no new JS — the tsc-only discipline holds; the browser harness is not
needed):

1. **Event detail page** — the "Add to calendar" row: a plain
   `<a href="/events/{id}.ics">` (the route U02 registered — read the
   handoff note's `## U02` entry for the exact string) styled as the
   existing secondary action (the ADR 0092 affordance-row idiom),
   labeled by the `kw-l` key **`events.ics.download`**. Gated on
   nothing extra — the page already only renders for a visible event
   (the C-M12·6 "quiet" pin).
2. **`/events` feed header** — one "Calendar feed (iCal)" line: a
   plain `<a href="/events.ics">` (the route U03 registered), labeled
   by the `kw-l` key **`events.ics.feed`**.
3. **Calendar page** — the same feed link, one line, beside the
   existing page-header block (if the design doc pinned the calendar
   page out of scope, this drops and you record it in the handoff).

**Plus** the two keys × **en/de/fr/da** in the closed-key registry
(`KnownTranslationKeys.cs` — the four language sections each gain the
two rows, the **exact strings from the design doc** verbatim — the
`events.past_empty` rows are the shape to copy). The
`KnownTranslationKeys_ParityTests` parity pin moves together — it must
discover the two new keys in all four languages.

**Plus** the **M10 phone-width re-check**: at 360 px the two links
wrap, not overflow, and respect the touch-target floor. **No CSS
change is the default** — one line in `src/Kumunita.Web/site.css` only
if the re-check fails, recorded in the handoff with the reason.

## Entry reads (5)

1. `docs/design/m12-ical-design.md` — §affordances (the two keys + the
   four-language strings, verbatim) + §invariants (C-M12·6).
2. `src/Kumunita.Web/Views/Event/Detail.cshtml` — the action-row block
   the "Add to calendar" link joins (the existing secondary-action
   styling to copy; the `kw-l` TagHelper usage shape).
3. `src/Kumunita.Web/Views/Event/Index.cshtml` +
   `src/Kumunita.Web/Views/Event/Calendar.cshtml` — the two header
   spots (the feed's btn-group / page-header block to place the feed
   link beside).
4. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the
   `events.*` block in **each** of the four language sections (the
   `events.past_empty` rows at the en/de/fr/da blocks — the parity
   shape).
5. `docs/design/m10-pwa-responsive-design.md` — §phone-width (the 360
   px re-check discipline + the "wrap, not overflow" rule + the a11y
   touch-target floor).

## Deliverables (5, +1 if the phone-width re-check fails)

1. `src/Kumunita.Web/Views/Event/Detail.cshtml` — the one link row.
2. `src/Kumunita.Web/Views/Event/Index.cshtml` — the one feed link.
3. `src/Kumunita.Web/Views/Event/Calendar.cshtml` — the one feed link
   (drops only if the design doc pinned the calendar page out of
   scope).
4. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the two
   keys × four languages (eight rows, the exact strings from the
   design doc).
5. `docs/plans-milestones/in-progress/m12-ical-handoff-notes.md` —
   append the `## U04` entry.
   **+** `src/Kumunita.Web/site.css` — only if the phone-width
   re-check fails (the one-line addition + the reason, recorded in the
   handoff).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  green — the `KnownTranslationKeys_ParityTests` (or its M12-pinned
  sibling) discovered + passing with the two new keys (the parity pin
  that proves en/de/fr/da all carry them).
- Handoff entry: the view files touched (exact paths as they exist —
  the drift note if a view path differed from the design doc's), the
  two keys + their eight strings as implemented, the parity test name
  + pass, the phone-width re-check result (the default no-CSS / the
  one-line addition + why), and the link targets as rendered (the
  `/{id}.ics` + `/events.ics` URLs, verbatim — cross-checked against
  the `## U02` / `## U03` entries' exact routes).

**Rules of engagement** (restated): never touch files outside these
deliverables; never rewrite the design doc; no new keys beyond the two
pinned; no new JS; no new authorization surface or content-decision
seam; no new dependency.
