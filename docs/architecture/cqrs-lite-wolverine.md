---
id: arch-cqrs-lite-wolverine
title: "CQRS-lite & side effects (Wolverine)"
tier: reference
kind: architecture
status: current
audiences: [developer, agent, platform-maintainer]
related:
  - docs/ARCHITECTURE.md
  - docs/adr/0001-core-stack-and-identity-model.md
updated: 2026-11-02
---

# CQRS-lite & side effects (Wolverine)

> **Abstract:** the CQRS-lite discipline — writes go through command handlers (domain state + audit in one Marten transaction), reads through Marten queries/projections, side effects through Wolverine handlers. Covers the durable email handler (the `OutboxEmail` / `EmailDeadLetter` shape), the projections that serve ordering only, and the scheduled jobs. **Who is it for:** a developer adding a write lane or a side effect; an agent tracing a failed email or a missing audit row. **When to read the rest:** before adding a new command handler, a new email kind, or a new scheduled job.


Writes go through commands + handlers; reads through Marten queries/projections. Side
effects are handlers, not controller code.

## 6.1 Command handlers — domain state + audit only

  Commands (examples): CreatePost, UpdatePostAudience, HidePost, RsvpToEvent,
    CreateGroup, AddGroupMember, GrantDelegation, RevokeDelegation, FileReport, ResolveReport

A command handler performs **all load-bearing writes** (domain documents + `AccessAudit`
rows) in one Marten transaction. It does **not** send email. Instead, it publishes an
`OutboxEmail` message (Wolverine transactional outbox — only dispatched if the commit
succeeds). This means:

- A failed SMTP connection can never roll back or delay a domain write.
- Audit rows are never gated behind a network call.
- The report-assignment + audit write commits even when mail is down; the moderator
  is notified asynchronously (at most delayed).

## 6.2 Durable email handler

`OutboxEmail` messages are processed by a single Wolverine durable handler
(`AsDurable()`, invocation state in Postgres):

- **Retry:** exponential backoff, capped at **6 attempts over ~24 hours**, then the
  message is dead-lettered.
- **Dead-letter:** on final failure the handler writes an `EmailDeadLetter` document
  (see [the `EmailDeadLetter` doc](data-model.md)) with the recipient, idempotency key, last error, and attempt count.
- **Crash-safe:** because invocation state lives in Postgres, a process restart resumes
  pending sends instead of dropping them.
- **Idempotency key** per email (`report:<id>`, `verify:<userId>:<attempt>`) ensures
  retries are bounded and re-verify (new attempt) is distinct from a replay.

Email kinds (all through the same handler):

| Kind                    | Criticality | Failure tolerance                    |
|-------------------------|-------------|--------------------------------------|
| Verification (signup)   | Load-bearing| Dead-letter + `/health` degraded; admin manual verify is the safety valve (M1 scope) |
| Report notification     | Load-bearing| Dead-letter + `/health` degraded     |
| Audience notification   | Best-effort | Dead-letter is fine; feed is the primary channel |
| Event reminder          | Best-effort | Re-runnable scheduled job; dead-letter if SMTP truly down |

## 6.3 Projections

  Projections: PerUserGroupSet (userId -> [groupId]); FeedIndex (componentId -> recent posts)

Projections serve **ordering and trimming** (recent-first, pinned-first, page size) only.
The visibility filter is **always** applied post-fetch via `CanSeeAsync` against the
source documents — never encoded in a projection, so projection lag can never leak or
hide access.

## 6.4 Scheduled jobs

  EventReminders  — scheduled email (best-effort; re-runnable)
  VerifyDigest    — optional: digest of unverified accounts (admin awareness)
  AuditPurge      — tiered expiry of AccessAudit rows (see [the `AccessAudit` retention rules](data-model.md))

Explicitly not used: event sourcing, distributed workflows.

