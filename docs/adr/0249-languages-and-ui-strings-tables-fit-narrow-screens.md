# ADR 0249 — The Languages and UI-strings tables fit narrow screens

Status: Accepted
Date: 2026-10-29

## Context
Two admin tables scrolled sideways below desktop width: `/admin/languages` (1050px wide, 78px rows at 768px) and the per-language UI-strings
editor `/admin/languages/{code}/translations` (991px wide, 1,828 rows, the value box squeezed to 138px). On the Languages table the Actions cell
holds up to five buttons in a nowrap button group (710px); on the editor the key column is nowrap (463px) and the browser's automatic column
widths gave all the slack to the English reference. Both fit at 1200px and up.

## Decision
- **Below 1200px** (`palettes.css`, one new media block, classes only): `.kmb-wrap-cells` tables stop forcing width.
  `.kmb-key-cell` lets keys wrap and break (`overflow-wrap: anywhere` on the `code`, which Bootstrap sets itself);
  `.kmb-actions` lets the button group wrap with a 0.375rem gap, keeps each button as its own pill (the joined-group corners looked
  wrong once wrapped) and stops buttons stretching; the editable value input may shrink to 8rem instead of a sliver.
  The 3-column editor also gets `.kmb-fixed-cols` (fixed table layout, columns 28% / 32% / rest), because with automatic layout the key
  and value columns stayed at their minimum while the reference text took the rest.
- **Below 576px** both tables use the existing stacked-card pattern (`kmb-stack-table`, ADR 0222): Languages rows show Enabled and
  UI strings captions and a wrapping action block; editor rows show Key, English reference and "Value in {code}" captions with the input
  and Save on one line. The captions are `data-label` attributes, resolved server-side from existing keys
  (`languages.th_enabled`, `languages.th_ui_strings`, `translations.editor.th_key`, `.th_en_reference`, `.th_value`); no new keys.
- Views: `Languages/Index.cshtml` and `Languages/Translations.cshtml` (both the batch and the one-at-a-time table) add the classes and
  `data-label`s. Desktop (1200px and up) is unchanged.

## Verified
Live, with the classes and the final CSS injected into the running pages (the markup is not rebuilt): Languages at 1000px 123px rows and
no sideways scroll; at 768px about 220px rows (4 lines of buttons) and no scroll; at 390px 406px cards, no scroll. Editor at 1000px 68 to 78px
rows; at 768px key 194px, reference 222px, value 278px, rows 68 to 78px (a long reference row 500px), no scroll; at 390px 204px cards.
The patch itself was not rebuilt or looked at after applying.

## Not covered
At 768px a Languages row is tall (four wrapped lines of buttons) because the other four columns keep their width; a tidier layout would
need the actions in a menu, which is a bigger change. The Moderation, Analytics, Storage and Help tables were not changed (they were not
reported as wide). Razor not compiled.
