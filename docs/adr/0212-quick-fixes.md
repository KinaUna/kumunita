# ADR 0212 — Quick fixes: multi-day event time, member count, inventory date, community hint

Status: Accepted

## Context
Five small defects found during the UI refresh review.

## Changes
- Event card (`Event/_EventCard.cshtml`): a multi-day event printed "Fri 18:00–14:00" and lost
  the end day. When the end falls on a later calendar day it now reads "Fri 18:00 – Sun 14:00";
  same-day events are unchanged. Day comparison uses the stored (UTC) date.
- Groups index: the member count was hard-coded English. New keys `groups.member_count_one` and
  `groups.member_count_other` (`{0}` = the count) in en/de/fr/da.
- Inventory detail: the "created" row showed the formatted date as the label and the raw
  DateTime as the value. Now a translated "Created" label and one formatted date.
- Goal, board and project forms reused `events.community_hint`, which says "this event". They
  now use a neutral `events.community_hint_generic` (en/de/fr/da); event forms keep the original.
- Rich-editor toolbar focus outline: already overridden by `palettes.css` (accent); only the
  `site.css` fallback uses `--bs-primary`. No change.

## Not verified
Nothing was compiled (no dotnet in the scratch environment); the translation parity test
should be run.
