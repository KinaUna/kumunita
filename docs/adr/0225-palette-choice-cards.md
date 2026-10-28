# ADR 0225 — Palette choice cards in the admin Appearance form

Status: Accepted

## Context
The platform-default palette was a bare `<select>` (ADR 0175 left "a live swatch preview next to the select" as a
TODO). A native `<option>` cannot show a swatch.

## Decision
`/admin/site` Appearance: the select becomes a `<fieldset>` of two selectable cards (one per `PaletteCookie.All`
entry). Each card is a `<label>` around a radio (`name="DefaultPalette"`, so the posted field, the controller and
its tests are unchanged), a decorative (`aria-hidden`) preview, and the name with its description. The preview is a
mini window split in two halves, light on the left and dark on the right; each half has a canvas, a card, an ink
line, an accent line and an action button, drawn with literal per-palette colours (the page's own tokens follow the
currently active palette, so they cannot preview the other one). Selected: accent border + 1px ring (`:has(:checked)`);
keyboard focus: the accent outline (`:has(:focus-visible)`). CSS in `palettes.css`, no JS.

## Verified (live, same markup and CSS injected into `/admin/site`)
- Both cards render in light and dark; the checked card shows the accent ring (Forest dark: `#9ad0ae`).
- Clicking the Ember card checks its radio. Nothing was saved, so the platform default is unchanged.

## Not changed / open
- The sample colours are copied from the palette tokens (Forest/Ember × light/dark); if a token changes, update the
  four `--p-*` blocks at the end of `palettes.css`.
- The member's own palette picker in the account menu is unchanged.
- Razor is not compiled in this environment.
