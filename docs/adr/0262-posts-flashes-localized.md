---
id: adr-0262
title: "Posts controller flashes are localized"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: []
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0262](0262-posts-flashes-localized.md) — Posts controller flashes are localized

Status: Accepted
Date: 2026-10-29

## Context
After the shared `FlashAsync` extension (ADR [0261](0261-shared-flash-extension.md)) the Posts controller was the next in the order: 27 literal English `TempData` messages (post and reply
lifecycle, translation add/update/remove, report submitted).

## Decision
- All 27 go through `this.FlashAsync(key, english, args)`. Translation messages reuse the `flash.translation_*` keys from ADR [0261](0261-shared-flash-extension.md).
- New keys (9, en/de/fr/da): `posts.flash_added` ("Post added to “{0}”."), `_updated`, `_deleted`, `_published`, `_reply_needs_text`, `_reply_added`, `_reply_updated`,
  `_reply_deleted`, and the shared `flash.report_submitted` (the report text recurs in other controllers).
- No constructor or test changes; the English floor keeps existing assertions valid.

## Not covered
Not compiled or run (no .NET SDK here); de/fr/da text machine-written, review parked. Remaining literal flashes: 258 across the other controllers.

## Amendments
2026-10-29: the improve gate (a) KnownTranslationKeys.cs pin re-based 12299 -> 12335 for this lane (see `docs/archive/lanes/improve/improve-check.ps1`).
