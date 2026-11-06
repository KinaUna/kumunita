---
id: adr-0288
title: "One shared translation-variant lane: the Core contract plus the Web action helper that every translatable surface uses"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0022-user-added-post-reply-translations.md
  - docs/adr/0029-announcement-translations.md
  - docs/adr/0059-event-translations.md
  - docs/adr/0088-projects-todos-boards-translations.md
  - docs/adr/0026-group-community-name-description-translations.md
  - docs/adr/0048-translation-edit-and-remove-lanes.md
  - docs/archive/lanes/improve-code/plan-improve-code.md
supersedes: []
superseded_by: []
updated: 2026-11-03
---

# ADR [0288](0288-one-translation-variant-lane.md) — One shared translation-variant lane

Status: Accepted
Date: 2026-11-03

> **Abstract:** seven translation ADRs (0022 / 0026 / 0029 / 0039 / 0048 /
> 0059 / 0088) each define a per-surface set of four methods (list / add /
> update / remove) on a different service interface, with the same standing
> matrix, the same audit-row shape, and the same language validation. This
> ADR names the shared contract: a generic `TranslationLane<TDoc, TVariant>`
> in Core, parameterized by a per-surface descriptor, and a
> `TranslationActions` Web helper. **For** a developer adding a
> translatable surface. **When to read the rest:** before adding
> translation methods to a service interface.

## Context

Each translatable surface (posts, replies, announcements, pages, events,
projects, to-dos, boards, groups, communities) has its own four-method set
(`Get<E>TranslationsAsync`, `Add<E>TranslationAsync`,
`Update<E>TranslationAsync`, `Remove<E>TranslationAsync`) on its service
interface. The standing matrix (creator ∪ assignee ∪ Translator ∪
GlobalAdmin), the audit-row shape (`TargetKind`, `Via`), and the language
validation are identical across all surfaces. The E6 evidence in the
IMPROVE-CODE plan counts 133 `*Translation*` methods across 25 files. The
copied pattern is the *Silent coupling* anti-pattern: a rule change must be
made in eight places, and the drift is silent.

## Decision

- **One Core contract.** A generic `TranslationLane<TDoc, TVariant>` in
  `Kumunita.Core` provides the four operations (list, add, update, remove),
  parameterized by a **per-surface descriptor** — a small record naming:
  the parent doc type, the variant doc type, the `TargetKind` string, the
  standing matrix, and the language validation. The descriptor is data, not
  a class hierarchy.
- **One Web helper.** A `TranslationActions` helper in `Kumunita.Web`
  provides the shared action pattern: load the parent doc, check standing,
  render the translation form, handle add / update / remove. A surface with
  a view-specific quirk (say, the ADR [0088](0088-projects-todos-boards-translations.md) body-optional
  adjustment) expresses it in the descriptor, not in a forked helper.
- **Migration (C06).** One surface per commit: posts → events →
  announcements → pages → projects / boards / todos → groups → surface
  labels → the rest. The 133 methods fall to ≤ 40. Each surface's existing
  integration tests pass unmodified.
- **New translatable surfaces.** A new surface adds a descriptor (a few
  lines), not a copied day of work. The descriptor is unit-tested; the lane
  is integration-tested at the seam (add → render variant → remove).

## Consequences

- **Positive:** a new translatable surface is a descriptor, not a copied
  day of work; the standing matrix and audit-row shape are defined once.
- **Positive:** the `*Translation*` method count falls from 133 to ≤ 40.
- **Negative:** eight surfaces share one lane; a surface-specific quirk
  must become an explicit descriptor option.
- **Supersedes the per-surface "frozen seam" convention** for the
  translation methods only: the ADR [0088](0088-projects-todos-boards-translations.md) D2 "Twelve `IProjectService`
  seams" are now three descriptors over one lane, not twelve frozen
  methods.
