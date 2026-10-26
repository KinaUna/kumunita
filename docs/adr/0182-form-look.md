# ADR 0182 — One calm look for card-per-field-group forms

Status: **Proposed** (scaffolding landed)
Date: 2026-10-26
Relates to: ADR 0173/0175 (UI refresh), ADR 0179-0181 (events)

## Context

About twenty forms (events, posts, projects, documents, profile, announcements, pages ...) share one
pattern: `<form>` > `.card.mb-4` > `.card-body`, one card per field group, with Bootstrap defaults.

## Decision

CSS only (`palettes.css`), no markup changes, scoped to a card that is a **direct child of a form**
so detail pages and lists are untouched:

- cards: `--kmb-surface`, `--kmb-border`, 16px radius, 1rem gap (was 1.5rem), 1.25/1.5rem padding
  (1rem on phones);
- labels in the heading font, 0.875rem/600; hints in `--kmb-muted`;
- inputs and selects at the 12px input radius (ADR 0173 D4), keeping the 3:1 border and accent focus
  ring from the non-text contrast pass;
- the shared rich editor: toolbar and pane at the input radius on palette tints; the active-button
  and WYSIWYG-focus colours use the accent instead of Bootstrap blue/green;
- the Cancel link next to the submit button is quiet (muted, no underline).

## Open

- Event forms could be regrouped into ~4 headed sections (When, Where, Details, Audience); that is a
  markup change, deferred.
- Forms that do not use the `form > .card` pattern (modals, inline forms, the admin screens) are
  not covered.
- Date/time inputs still use the browser's native `datetime-local` widget.
