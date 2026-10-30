# ADR 0242 — The document delete confirmation points to Deleted documents

Status: Accepted
Date: 2026-10-29
Amends: ADR 0240 (the text of `documents.delete_confirm`)

## Context
A live pass after ADR 0241 found that the delete confirmation still said the document "cannot be restored from the app". Restore now
exists (ADR 0241), so the text was wrong, and it would put people off a delete they can undo.

## Decision
`documents.delete_confirm` (en, de, fr, da; `{0}` is the title) now says the document disappears for everyone and can be restored from
Deleted documents. Wording only: no code, route or test changes (no test pins the text; the controller tests see the raw key).

## Not covered
The de, fr and da strings are machine translations and need native review (as for ADR 0240). Not compiled or run here.
