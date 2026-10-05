# ADR 0131 — SMTP transport: MailKit in place of the BCL SmtpClient

Status: Accepted
Date: 2026-10-02

## Context

The durable outbox (`OutboxEmail` → `ISmtpSender` → `OutboxEmailHandler`) is
the platform's only outbound email path (verification links, admin handoff,
event reminders, the M6/M7/M8/M9/M11/M12/M13/M14 notification + message lanes).
`SmtpSender` (`src/Kumunita.Core/Identity/SmtpSender.cs`) implemented the seam
on the BCL `System.Net.Mail.SmtpClient`.

The BCL `SmtpClient` had a documented limitation — the .NET API reference for
`SmtpClient.EnableSsl` is explicit that the implicit-TLS / SMTPS model (port
465) is **not** supported. This was recorded in `COOLIFY.md` §5.1B as a
"relay-swap or implementation-change" note.

On the 2026-10-02 VPS deployment (Proton Mail SMTP submission,
`smtp.protonmail.ch:587`, `SMTP__Secure=Tls`, a paid-plan SMTP token for
`admin@kumunita.com`), the live BCL transport failed with a reproducible,
reproducible regression:

> `SmtpFailedRecipientException: Mailbox name not allowed. The server response
> was: 5.7.1 <admin@kumunita.com>: Sender address rejected: not logged in`

The error is a relay-side rejection of the **`MAIL FROM`** line, which happens
*after* `AUTH`. "Not logged in" means the app reached `MAIL FROM` without a
valid session — i.e. the BCL client sent **no effective AUTH**. The evidence
that this was the BCL transport and not the environment, the network, or the
credentials:

| Test | Outcome |
|---|---|
| The container's env (via `docker exec … env`) | all six `SMTP__*` values present, including `SMTP__User` + `SMTP__Pass` set together |
| A live `curl smtp://smtp.protonmail.ch:587` from *inside the same container*, same credentials | `235 Authentication successful` → `250 queued` |
| A live `openssl s_client -starttls smtp` from the VPS host, same credentials | `235 2.7.0 Authentication successful` |
| The app's own `SmtpProbe` (hand-rolled sockets handshake, same bound `SmtpOptions`, same process) on `/health` | `mail: "ok"` (`mailDetail: null`) — i.e. the probe's full `EHLO → STARTTLS → AUTH` sequence succeeded |
| Proton's dashboard "last used" timestamp for the token | did **not** advance across the app's failed sends |

So: same credentials, same host, same port, same TLS shape, same vantage point —
everything works except the BCL `SmtpClient` on .NET 10. The BCL was the
variable. The codebase had already anticipated this escape hatch (`COOLIFY.md`
§5.1B: "it needs a relay swap or a `SmtpSender` implementation change").

## Decision

Replace the BCL `System.Net.Mail.SmtpClient` with **MailKit's**
`MailKit.Net.Smtp.SmtpClient` (the `MailKit` NuGet package, 4.18.1).

**Scope of the change** (deliberately narrow — the seam is the seam):

- `src/Kumunita.Core/Identity/SmtpSender.cs` — the `SmtpSender` implementation
  is rewritten on MailKit. The `ISmtpSender` interface, the
  `SmtpOptions` POCO (bound from the `SMTP__*` env section), the
  `CreateClient` configuration guard ("exactly one or zero" of
  `User`/`Pass"), and the `ConnectAndAuthenticateAsync` handshake order
  (EHLO → STARTTLS → AUTH) are all preserved. The `SmtpProbe` (the /health
  diagnostic) is **unchanged** — it is a hand-rolled sockets handshake and
  already validates the exact sequence the real send now drives, so it
  continues to serve as a working reference for the operator.
- `src/Kumunita.Web/Program.cs` — the Wolverine retry policy's exception
  inspection moves from the BCL pair (`System.Net.Mail.SmtpException` +
  `TimeoutException`) to MailKit's two SMTP failure classes
  (`MailKit.Net.Smtp.SmtpCommandException` +
  `MailKit.Net.Smtp.SmtpProtocolException`, plus the base
  `MailKit.ProtocolException`). The cooldown list (6 attempts / ~24 h, the
  §6.2 shape) and the `Fault<OutboxEmail>` dead-letter path are untouched.
- `Kumunita.Core.csproj` — adds `MailKit` 4.18.1 (pulls `MimeKit`).
- `COOLIFY.md` §5.1 / §5.1B — the "465 is not supported" note is superseded;
  `SMTP__Secure=Ssl` is now a first-class value
  (`SecureSocketOptions.SslOnConnect`).

**What does not change:**

- The `SMTP__*` env shape — `Host`, `Port`, `User`, `Pass`, `Secure`, `From`.
  The operator's configuration is byte-for-byte the same; only the `Secure`
  value space grows from `{Tls, None}` to `{Tls, Ssl, None}`.
- The `SMTP__User` / `SMTP__Pass` "exactly one or zero" invariant (the
  `InvalidOperationException` guard in `CreateClient`).
- The `SMTP__From` behavior (only set `msg.From` when non-empty — MailKit's
  `MimeMessage.From` is read-only in 4.x, so the `From` header is set through
  the 4-arg `MimeMessage` constructor instead).
- The `X-Message-Id` / `X-Kumunita-Recipient` headers (the relay-side
  idempotency belt, the §6.2 contract).
- The durable outbox + retry + dead-letter pipeline (the `OutboxEmail` row,
  the `Fault<OutboxEmail>` handler, the `EmailDeadLetter` domain document,
  the `/health` `emailDeadLetters` counter).
- The `SmtpProbe` (the /health diagnostic — it is the operator's "the relay
  is reachable + the credentials work" signal and was never the failure path).
- `SmtpOptions.UseDefaultCredentials` — a documented **no-op** under MailKit
  (MailKit has no equivalent of the BCL's OS-credential relay shape). The
  property stays for env compatibility; a relay that relies on it predates the
  swap and needs the explicit `User`/`Pass` pair.

**Why MailKit specifically** (and not another library):

- It is the de-facto .NET SMTP/IMAP client (the same project as MimeKit); a
  single package, no transitive surprises beyond MimeKit.
- It drives the exact `EHLO → STARTTLS → AUTH (PLAIN/LOGIN)` sequence the BCL
  was supposed to drive (and the `SmtpProbe` already validates), so the swap
  is a transport change, not a protocol change.
- It supports **all three** TLS shapes: STARTTLS (587, the default), implicit
  TLS (465, the BCL's documented gap), and "no enforced encryption" (the
  Mailpit / loopback shape). The BCL could only do STARTTLS.
- It is `ConfigureAwait(false)`-friendly and `CancellationToken`-aware on
  `ConnectAsync` / `AuthenticateAsync` / `SendAsync`, so the existing async
  contract on `ISmtpSender.SendAsync` is preserved.

## Consequences

- **Positive:**
  - The 2026-10-02 VPS failure is fixed: the same credentials + host + port
    that worked via curl/openssl/probe now work through the app's real send
    path. The 8 queued `OutboxEmail` rows (the `Fail:` lines in the boot log)
    drain on the next handler tick under the existing retry policy.
  - **465 / implicit TLS (SMTPS) is now a first-class shape** — the
    `COOLIFY.md` §5.1B "relay swap or implementation change" escape hatch is
    retired; `SMTP__Secure=Ssl` + `SMTP__Port=465` is a legitimate
    configuration for a 465-only relay.
  - The `SmtpProbe` (the /health diagnostic) and the real send now share the
    same protocol sequence (EHLO → STARTTLS → AUTH), so the /health `mail` /
    `mailDetail` signal is a faithful proxy for "the real send will work."
  - The retry policy's exception inspection is still narrow (the two
    SMTP-specific exception classes + the base protocol exception) — a real
    programming error (a `NullReferenceException` in a handler, a
    `MailAddressException` for a malformed recipient, an
    `InvalidOperationException` for a half-set credential pair) is still
    **not** retried, preserving the §6.2 "narrower than Exception"
    discipline.

- **Negative / costs:**
  - A new dependency (`MailKit` 4.18.1, pulling `MimeKit`) in
    `Kumunita.Core`. The package is a single, well-maintained,
    MIT-licensed library; no transitive surprises.
  - `SmtpOptions.UseDefaultCredentials` is now a documented no-op. A relay
    that relies on the BCL's OS-credential shape (a rare Windows-service
    shape) needs the explicit `User`/`Pass` pair — a configuration change,
    not a code change.
  - The BCL `TimeoutException` catch in the retry policy is replaced by
    MailKit's `ProtocolException` family. A DNS-resolution failure or a
    socket-level reset that the BCL would have thrown as `TimeoutException`
    now surfaces as `MailKit.ProtocolException` (or a
    `SmtpProtocolException`) — both are in the retry set, so the observable
    behavior is the same (retry with cooldown), but the exception type in the
    dead-letter row's `LastError` text will differ.

- **Non-consequences:**
  - No schema change. The `OutboxEmail` / `EmailDeadLetter` domain documents
    are untouched; the `M1DocTypes` surface is untouched.
  - No new `AccessAction` / `AccessVia` / `IAuditableResource` surface (the
    SMTP seam is not an authorization lane).
  - No new `kw-l` key, no view change, no route change.
  - `Milestones.cs` / the README Roadmap / `MilestonesTests.cs` are
    **untouched** (a transport-swap on an already-shipped seam, not a
    milestone — the ADR 0086/0088/0089/0093 "named lane on the shipped
    surface" precedent).

## References

- The live failure trail (the 2026-10-02 VPS session): the curl transcript
  (`235 Authentication successful` → `250 queued`), the openssl transcript
  (`235 2.7.0 Authentication successful`), the /health probe (`mail: "ok"`,
  `mailDetail: null`), and the app's `SmtpFailedRecipientException` on
  `OutboxEmail#08df20a5` (and 7 sibling rows) — all reproduced on the same
  container, same credentials, same network path.
- `COOLIFY.md` §5.1 / §5.1B (the pre-swap "465 is not supported" note, now
  superseded).
- `src/Kumunita.Core/Identity/SmtpProbe.cs` (the hand-rolled sockets
  handshake the /health diagnostic uses — unchanged, now a faithful proxy for
  the real send).
- `src/Kumunita.Web/Program.cs` (the Wolverine retry policy, updated for
  MailKit's exception types).
- `docs/adr/0004-data-persistence-and-schema-evolution.md` (the durable outbox
  + dead-letter shape this ADR leaves untouched).
- The .NET API reference for `System.Net.Mail.SmtpClient.EnableSsl` (the
  original "implicit TLS is not supported" statement that motivated the
  escape-hatch note in `COOLIFY.md` §5.1B).
