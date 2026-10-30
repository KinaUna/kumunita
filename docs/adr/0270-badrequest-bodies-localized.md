# ADR 0270 — BadRequest bodies are localized; Core exception text stays English

Status: Accepted
Date: 2026-10-29

## Context
Eight `BadRequest("English")` bodies remained in DocumentFolder, ContentImage, Attachment and Document controllers. The remaining English reaching residents comes from
Core service exceptions (~880 `throw new …Exception("English")` sites in 27 files; 71 controller catch blocks show `ex.Message`).

## Decision
- The eight bodies use `await this.FlashAsync(key, english)`; 4 new `validation.*` keys in en/de/fr/da (`folder_name_required`, `choose_image`, `choose_file`,
  `document_id_required`). `DocumentFolderController` gained `using Kumunita.Web.Security;`.
- **Core exception text stays English** (Per's decision, asked before building). Core is HTTP-free with no language, and the options considered were a keyed
  `UserFacingException` migrated per surfaced throw site (more work, a Core API change) or an English-text lookup in Web (brittle). Neither is adopted now.
- Revisit trigger: if a surfaced service message turns out to matter to non-English residents, map that case to a generic localized message in the controller
  (the pattern above) or introduce the keyed exception for that service only.

## Not covered
de/fr/da text machine-written, review parked. Still English: `ex.Message` text from Core, admin view pages (AdminSite, Admin/Platform, Mfa),
Page/Index and page-tree leftovers.

Compiled and run 2026-10-29 (build clean, 0 errors).

The improve-check.ps1 `KnownTranslationKeys.cs` pin was re-based 13227 -> 13243 on 2026-10-29 for the 4 new keys x en/de/fr/da of this lane (the ADR 0265/0266 shipped-additions mechanism).
