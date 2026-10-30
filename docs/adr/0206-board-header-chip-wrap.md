# ADR 0206 — Board header chips wrap

Status: Accepted

## Context
Verification of the board header (ADR 0200) with a project link in light mode and
at phone width. Desktop and phone were fine with real data. Injecting a long
project title and an event chip showed the chip row overflow: header content 670px
in a 364px card, clipping chips and the description.

## Decision
- `.kmb-board-links .kmb-chip` flows as inline-block, wraps (`white-space: normal`),
  max-width 100%, `overflow-wrap: anywhere`, radius 1rem so wrapped chips are not ellipses.
- Chosen over ellipsis truncation so the full title stays readable.
- CSS only; verified live on the phone viewport (header 364/364, words intact).

## Open
- Other chip rows with titles (event/goal/project headers) were not stress-tested.
- Verified with injected long titles; a real event link was not available in dev data.
