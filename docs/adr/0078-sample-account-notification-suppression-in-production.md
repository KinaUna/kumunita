# ADR 0078 — Sample-account notification suppression in production

Status: Accepted
Date: 2026-09-25
Amends: **0076** (the M6 notification surface — the
`NotificationService.EmitAsync` writer gains a single environment-aware
gate before its profile-lookup step; no signature change, no new document,
no new surface, no new seam on any frozen interface), **0055** (the
sample-data seeder's code-owned account e-mail addresses become the
closed set the gate compares against — a new `SampleAccountEmails`
readonly property on `SampleDataSeeder`, additive on the seeder's
existing constants), and **0056** (the sample-data opt-in / deploy
posture — this ADR settles the *outbound channel* consequence of that
posture that 0056 left implicit: in the Development environment the
sample accounts are full notification citizens; in a Production / Staging
demo instance they are not).

## Context

M6 (ADR 0076) and the admin-lane kinds (ADR 0077) made **every**
resident a full notification citizen: a reply, a group post, an RSVP, a
report, a to-do — each produces an inbox row and, per the recipient's
preference, a durable email. That is exactly right for a real
neighborhood. But ADR 0055 / 0056 introduced a second, distinct kind of
resident — the **sample neighborhood** — which is an explicit opt-in
(`SampleData__Enabled`) that, in the Development loop, is backed by
**Mailpit** (a local SMTP sink that discards mail after showing it). In
that environment, the sample accounts being notified is harmless and
even useful: a developer can watch the durable-outbox pipeline work
end-to-end against the seeded mock neighborhood.

The problem is the **deployed demo** posture (ADR 0056): a Production
instance with `SampleData__Enabled=true` is a *public* instance whose
sample accounts are real, sign-in-able rows (random high-entropy
passwords, a credentials summary e-mailed to the seed admin). In that
environment the durable-outbox pipeline talks to a **real SMTP relay**,
so every notification a sample resident triggers — a reply to a sample
post, a group post in the seeded Street Green, an RSVP on a seeded
event — is delivered as a **real e-mail to a real mailbox** (the
sample e-mail addresses) and a real inbox row. That is noise: the demo
accounts are not people, and a public instance should not be generating
real outbound mail from (or to) its own mock data.

**What the request is:** "exclude sample accounts from sending
notifications in production. Since the development environment uses
Mailpit it should be ok to have notifications for them there." Read
literally: the suppression is **environment-gated** (Development =
off, Production / Staging = on) and **recipient-scoped** (the sample
neighborhood's own accounts, identified by their code-owned e-mail
addresses), not a global off-switch and not a per-resident preference.

## Decision

**One gate, one option, one closed set.** The `NotificationService`
gains a single optional constructor dependency on
`IOptions<NotificationOptions>` (a new, additive options class in the
`Notifications` context), whose single property
`SuppressForSampleAccountsInProduction` defaults to `false` and is
bound by the host (`Program.cs`) to
`!builder.Environment.IsDevelopment()`. When the flag is `true`,
`EmitAsync` — **after** resolving the recipient's `Profile` but
**before** the translation lookup, the inbox-row store, and the
`IMailerStage.StageAsync` call — checks the recipient's profile e-mail
against the closed set
`SampleDataSeeder.SampleAccountEmails` (a new `IReadOnlySet<string>`
property the seeder already has every member of: the seven code-owned
`@examplium.com` addresses). A match is a **complete no-op**: the
method returns `null` (its return type widens from
`Task<Notification>` to `Task<Notification?>`), no inbox row is stored,
no email is staged, and the `ITranslationProvider` is never called. The
caller's own domain write (storing the reply, posting the group post,
recording the RSVP, etc.) is **unaffected** — the caller still runs its
own `SaveChangesAsync` for that write; the gate only suppresses the
notification side-effect.

**The gate is on the recipient, not the actor.** The check compares the
*recipient's* profile e-mail, so a sample resident *receiving* a
notification is suppressed (they are not a person to be told about
something). A real resident who replies to a sample post is a real
recipient and is notified normally — the gate does not fire on the
author's identity, only the reader's. This matches the request
("exclude sample accounts *from sending* notifications" is shorthand
for "the notification events the sample neighborhood generates should
not produce real mail in production") without over-suppressing real
residents' own notifications.

**Development is untouched.** In `Development` the flag is `false`, so
the gate never fires and the sample neighborhood behaves exactly as it
always has — inbox rows and Mailpit-collected mail for the seeded
accounts. The local dev loop's "watch the outbox pipeline against mock
data" value is preserved.

## Consequences

- **`NotificationService.EmitAsync` now returns
  `Task<Notification?>`.** The null only ever occurs on the
  sample-account-suppression path; every other path returns the stored
  (or pre-existing) row as before. All ten current `EmitAsync` call
  sites (`PostService`, `EventService`, `EventReminderService`,
  `IdentityService`, `ModerationService`, `ProjectService`) are
  statement-level `await`s that ignore the return value, so the
  signature change is source-compatible with the existing tree; the one
  test helper (`NotificationServiceTests.Emit`) that *did* use the
  return is updated to `return row!` with a comment recording why null
  is unreachable there.
- **`SampleDataSeeder.SampleAccountEmails`** is a new code-owned,
  closed, `StringComparer.OrdinalIgnoreCase` set over the seven
  `@examplium.com` addresses the seeder already creates. The four
  residents, the moderator, the translator, and the seed admin are all
  covered. Adding a sample account to the seeder means adding its
  e-mail to this set — a deliberate, visible coupling so the gate and
  the seeder never drift.
- **The option is host-bound, not config-bound.** Unlike
  `SampleDataOptions` / `SeedAdminOptions` (which bind from a
  configuration section), `NotificationOptions` is set in code from
  `IHostEnvironment.IsDevelopment()` because the correct value is a
  function of the *environment*, not a per-instance deployment choice —
  an operator should not have to remember to flip a config flag to stop
  demo-instance mail. `Program.cs` binds it once, next to the other
  option bindings.
- **The DI factory degrades permissively.** The `NotificationService`
  registration passes
  `sp.GetService<IOptions<NotificationOptions>>()` (nullable) rather
  than `GetRequiredService`, so a test harness or future host that
  doesn't register the option gets the ctor's `null` default → `false`
  → sample accounts notified. The safe default is "notify," matching
  the M6 D7 posture that the inbox is the primary record.
- **No change to the recipient's preference lane, the idempotency
  contract, the dead-letter / health posture, or the `M6DocTypes`
  surface.** The gate is a pure early-return on the emission path; the
  durable-outbox trio, the `IMailerStage` idempotency guarantee, the
  `EmailDeadLetterWriter` + `/health` degraded gate, and the
  `notification:{kind}:{stable-source-id}` key contract are all
  untouched.
- **Pinned tests.** Two new tests in `NotificationServiceTests` pin the
  gate: (1) flag `true` + a sample e-mail → `null` return, zero inbox
  rows, zero staged mail; (2) flag `false` + a sample e-mail → a stored
  row + one staged mail (the Development / permissive path). Both run
  against the live scratch-Postgres fixture and construct the service
  directly with an explicit `Options.Create(...)`, so the pin is
  independent of the host's binding.

## Follow-ons

- **Suppressing the M4 event-reminder e-mail for sample attendees** is
  *not* covered by this gate: the §6.4 reminder is staged directly by
  `EventReminderService` with its own `remind:{eventId}:{userId}` key
  (ADR 0054) and does not route through `EmitAsync`. On a deployed demo
  instance with sample RSVPs, those reminder e-mails still go out. That
  is a narrow, acceptable consequence (the reminders are the one M4
  lane that predates M6 and never adopted the M6 seam); if it becomes
  noise it is a follow-on lane, its own ADR.
