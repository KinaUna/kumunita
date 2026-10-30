# ADR 0260 — Guardian attendance rows show the real RSVP status, localized

Status: Accepted
Date: 2026-10-29

## Context
On Guardian/Detail the pending attendance requests and the child's current attendance both rendered `<kw-l key="guardian.eventrsvp.desired">@(status)</kw-l>`.
The key exists ("Wants to attend"), so the TagHelper rendered that fixed text and the English floor (the enum name) was never shown: every row read
"Wants to attend" whatever the RSVP (Going/Maybe/No). The "Requested …" and "Attended …" lines were English-only.

## Decision
- Status text comes from the shared `events.rsvp_status_going/_maybe/_no` keys through a server-side dictionary (same pattern as Event/Detail).
- Pending rows read "Wants to attend: {0}" (new `guardian.eventrsvp.desired_with`); current-attendance rows show just the status label.
- Date lines use new `guardian.eventrsvp.requested_at` ("Requested {0}") and `guardian.eventrsvp.attended_at` ("Attended {0}") via `string.Format`.
- Three new keys in en/de/fr/da. `guardian.eventrsvp.desired` is now unused and left in place (parity tests, no churn).

## Not covered
Not compiled or run (no .NET SDK in the scratch environment); de/fr/da text machine-written, review parked. The rest of the Guardian views were already
localized (scan found nothing else). Dates are still the pre-formatted strings from the view model.

## Close note
`improve-check.ps1` KnownTranslationKeys.cs pin re-based 12091 -> 12103 (2026-10-29, this ADR).
