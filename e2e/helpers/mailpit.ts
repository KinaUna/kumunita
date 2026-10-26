// F19.U2 — the Mailpit extraction (pin §1 + §2.1 step 2).
//
// The pinned extraction:
//   1. Poll `GET {MAILPIT}/api/v1/messages` (the Mailpit HTTP API — the
//      compose-published API port, :8025, NOT the sandbox's shared :5099
//      instance per pin §1's port note) until a message with `To` containing
//      the run-unique email appears. Bounded (~30 s) — a miss is a REAL
//      failure of the mail pipeline, which is exactly what this flow must
//      catch (pin §2.1 step 2).
//   2. Extract the verify link from the message body with the PINNED regex
//      `https?://[^\s]+/account/verify\?id=[A-Za-z0-9-]+` (pin §1/§2.1 — the
//      link shape is pinned by the source: `IdentityService.VerificationLink`
//      builds `{Verification__BaseUrl}/account/verify?id={tokenRowId}`; the
//      `id` is the token row id — the high-entropy secret is never in the URL).
//
// This helper is the SHARED extraction U3 (Flow B) re-uses (pin §4 — "the
// Mailpit message poller + the verify-link extractor + the §2.1 step 5
// write-body" are the shared helpers; Flow B does not call the Mailpit
// poller — it signs in with a known sample account — but the helpers are
// here so U3's import paths are stable).

const MAILPIT_URL = process.env.KUMUNITA_MAILPIT_URL ?? 'http://localhost:8025';

// The PINNED verify-link regex (pin §1/§2.1).
export const VERIFY_LINK_REGEX = /https?:\/\/[^\s]+\/account\/verify\?id=[A-Za-z0-9-]+/;

/**
 * A single Mailpit message as returned by the `/api/v1/messages` API. The
 * suite reads only the fields it needs — `To` (the recipient, used to filter
 * by the run-unique email) and `Body-Text` (the plain-text body, where the
 * verify link is embedded). The `Body-HTML` field is NOT read (the pin's
 * regex is grounded in the plain-text shape of the `VerificationLink`).
 */
export interface MailpitMessage {
  id: string;
  To: string;
  'Body-Text'?: string;
}

/**
 * Poll the Mailpit API until a message addressed to `recipient` appears
 * (bounded by `timeoutMs`, default ~30 s). A miss is a real failure — the
 * mail pipeline is broken, which is what this flow must catch (pin §2.1
 * step 2). Throws a named error on timeout; never retries in a loop past
 * the budget (the failed-twice doctrine, pin §5 invariant #6).
 */
export async function waitForMailMessage(opts: {
  recipient: string;
  timeoutMs?: number;
}): Promise<MailpitMessage> {
  const { recipient, timeoutMs = 30_000 } = opts;
  const deadline = Date.now() + timeoutMs;
  for (;;) {
    const res = await fetch(`${MAILPIT_URL}/api/v1/messages`, {
      method: 'GET',
      signal: AbortSignal.timeout(10_000),
    });
    if (res.status === 200) {
      const body = (await res.json()) as { messages?: MailpitMessage[] };
      const hits = (body.messages ?? []).filter(
        (m) => (m.To ?? '').toLowerCase().includes(recipient.toLowerCase()),
      );
      if (hits.length > 0) {
        // The most recent match (Mailpit returns newest-first).
        return hits[0];
      }
    }
    if (Date.now() >= deadline) {
      throw new Error(
        `Mailpit poll timed out after ${timeoutMs / 1000} s waiting for a message ` +
          `to ${recipient}. This is a REAL mail-pipeline failure (the durable ` +
          `outbox handler did not stage + send the verification email), which is ` +
          `exactly what the flow must catch — NOT retried (pin §5 invariant #6).`,
      );
    }
    await new Promise((r) => setTimeout(r, 2_000));
  }
}

/**
 * Extract the verify link from a Mailpit message body using the PINNED
 * regex (pin §1/§2.1). Throws a named error if the link is not present —
 * the message body did not render the `IdentityService.VerificationLink`
 * shape, which is a real failure (the mail template or the Core link
 * builder diverged from the pin).
 */
export function extractVerifyLink(message: MailpitMessage): string {
  const bodyText = message['Body-Text'] ?? '';
  const match = bodyText.match(VERIFY_LINK_REGEX);
  if (!match) {
    throw new Error(
      `Verify link not found in the Mailpit message to ${message.To}. ` +
        `The message body did not contain a URL matching the pinned regex ` +
        `${VERIFY_LINK_REGEX}. The mail template or IdentityService.VerificationLink ` +
        `diverged from the pin (pin §2.1 step 2).`,
    );
  }
  return match[0];
}
