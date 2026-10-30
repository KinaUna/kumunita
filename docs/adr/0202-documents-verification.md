# ADR 0202 — Documents verification and detail fixes

Status: Accepted

## Context
ADR 0192 (patch 25) restyled Documents but was never previewed. Verified live
with PDF, DOCX, XLSX, CSV, TXT and ZIP uploads in Ember dark/light and phone
width (Forest light list too).

## Decisions
- Tile is top-aligned beside the detail title (long titles wrapped to six lines
  on a phone with the tile floating mid-height).
- The meta row shows a friendly type name ("PDF document", "Spreadsheet", ...)
  with the raw content type in a tooltip; unknown types fall back to the raw MIME.

## Open
- The upload allow-list refuses images and PPTX (HTTP 415), so the IMG, PPT and
  generic FILE tiles are unreachable from uploads today; they were not previewed.
- Friendly names are English literals (TODO: kw-l keys).
