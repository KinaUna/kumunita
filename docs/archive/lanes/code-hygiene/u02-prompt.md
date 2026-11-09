---
id: ch02-prompt
title: "CH02 prompt — Route conflict fix (ASP0023)"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: code-hygiene
updated: 2026-11-09


# CH02 — Route conflict fix

## What you are doing

You are the **second working agent** on the **CODE-HYGIENE** lane.
CH01 is committed (the design doc exists).

This unit fixes the **one route-conflict bug**: `Dissolve` in
`GuardianController.cs` has the route
`{childId}/eventrsvp/{eventId}/approve`, which is the **same route** as
`ApproveEventRsvp` in `GuardianController.RsvpCurate.cs`. A POST to that
URL is ambiguous — half the time it routes to `Dissolve` (dissolves the
guardian link) and half to `ApproveEventRsvp`. The fix: change `Dissolve`'s
route to `{childId}/dissolve`.

## The one thing to get right

`Dissolve` takes only `childId` (no `eventId`). The `{eventId}` segment in
the route is wrong. The `RsvpCurate` partial has the correct
`ApproveEventRsvp`. Update the route, the view that posts to it, and any
test that pins the old URL.

## Entry reads (only these)

- `sed -n '415,500p' src/Kumunita.Web/Controllers/GuardianController.cs`
  (the `Dissolve` action + its route).
- `sed -n '30,80p' src/Kumunita.Web/Controllers/GuardianController.RsvpCurate.cs`
  (the `ApproveEventRsvp` action — the correct route).
- `grep -rn "eventrsvp.*approve" tests/` (tests that pin the route).
- `grep -rn "Dissolve\|dissolve" src/Kumunita.Web/Views/Guardian/` (the
  view that posts to `Dissolve`).

## Deliverables (≤ 3 files)

1. `src/Kumunita.Web/Controllers/GuardianController.cs` — change the route
   on `Dissolve` (line 427) from `"{childId}/eventrsvp/{eventId}/approve"`
   to `"{childId}/dissolve"`.
2. The view that posts to `Dissolve` — update the form action URL.
3. The test file that pins the old route — update the expected URL.

## Exit

`dotnet build` green. ASP0023 is gone:
`dotnet build --no-incremental 2>&1 | grep ASP0023` returns nothing.
Handoff note: 3 lines starting `## U2 — route conflict`.
