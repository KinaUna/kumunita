# ADR 0177 — Groups tile and friendly empty state

Status: **Proposed** (scaffolding landed)
Date: 2026-10-26
Relates to: ADR 0175 (UI refresh), ADR 0094 (public groups / join requests)

## Decisions

**D1 — Group tile.** `/groups` tiles use the feed card language: a 48px rounded-square
monogram (first letter of the name, stable hue per group via `AvatarHue.For(group.Id)`;
rounded square so groups read differently from the round person avatars), the name, and the
member count beneath it. Tiles use `--kmb-surface`, a 16px radius, border + soft shadow on hover,
and an auto-fill grid (min 260px) so phones get one column. No data changes: a group still
carries only Id, Name and MemberCount.

**D2 — Whole tile is the link.** For groups the resident belongs to, the name link is a
`stretched-link`. "Other public groups" tiles are not links (ADR 0094); their "Request to join"
button is now a quiet outline button (amber stays reserved for the page's one primary action,
"Create a group").

**D3 — Empty state.** `.kmb-empty` (dashed border, title, one line of guidance, primary button)
replaces the bare `.alert-info`. Generic, so other empty lists can adopt it.

## Open

- The "member(s)" label is still an inline English string (pre-existing); move to a `kw-l` key.
- Later options if wanted: description line, member avatar stack, last-activity line (each needs data).
