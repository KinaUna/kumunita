# U6 — Real end-to-end smoke check for the OutboxEmail C3 flow

**Date:** 2026-09-07
**Prerequisites:** U1–U5 all green (confirmed in `m1-step-7-handoff-notes.md`).
**Goal:** prove the full C3 path works live — a real `POST /Account/Signup` → the
`OutboxEmail` row AND the Wolverine envelope both land in Postgres **in the same
commit** → the durable handler fires → the email actually arrives in Mailpit.

---

## Environment snapshot (verified before the test)

| Component | State |
|---|---|
| `kumunita_db` | healthy, `mt_doc_outboxemail` / `mt_doc_emaildeadletter` / `wolverine_incoming_envelopes` / `wolverine_outgoing_envelopes` all present |
| `kumunita_mailpit` | healthy — SMTP port 1025 accepts connections |
| `kumunita_app` | running since 09:22 (3 h before this session) — **may not reflect U1–U5**; must rebuild before testing |
| Existing `OutboxEmail` rows | 2 — a `setup:` row and a `verify:` row (pre-U1, from earlier boots) |
| `SMTP__Host` / `Port` / `Secure` in the container | `mailpit` / `1025` / `None` — plain SMTP to Mailpit, no auth |

**Action required before the test:** rebuild the `kumunita_app` image from the
current source (which now has U1–U5) and restart the container. Do **not** test
against the old image — the old `OutboxEmailStager` only called `session.Store()`
and never called `IMessageContext.PublishAsync()`, so it would produce the
`OutboxEmail` row with NO Wolverine envelope, masking the fix.

---

## Step 1 — Rebuild + restart the app container

```bash
# From D:\repos\Kumunita
docker compose -f docker-compose.yml up --build -d app
```

Wait for `/health` to respond 200 (or `docker compose -f docker-compose.yml ps app`
shows `Up` with no restart). Poll:

```powershell
# Poll until it succeeds (up to ~90 s); use Invoke-WebRequest with -TimeoutSec 3
1..30 | ForEach-Object {
    try { Invoke-WebRequest "http://localhost:5080/health" -UseBasicParsing -TimeoutSec 3 | Out-Null; break }
    catch { Start-Sleep -Seconds 3 }
}
```

**Checkpoint:** `docker compose -f docker-compose.yml ps app` shows `Up (healthy)`
or `Up N seconds`; the process is the freshly-built image (confirm via `docker
inspect kumunita_app --format '{{.Config.Image}}'` — the image ID should differ
from the old `sha256:a07777…`).

---

## Step 2 — Baseline counts (so we can prove the new row is ours)

```sql
SELECT count(*) AS outbox_before FROM mt.mt_doc_outboxemail;
SELECT count(*) AS inbound_before  FROM mt.wolverine_incoming_envelopes;
```

Record these two numbers. All evidence after the test must show **exactly +1**
`mt_doc_outboxemail` and **+≥1** `wolverine_incoming_envelopes` (the envelope
is consumed quickly by the durable handler, but the `attempts = 1` record should
still be queryable — or we fall back to the Mailpit delivery as the positive proof).

---

## Step 3 — Perform a real signup via HTTP

The signup endpoint is `POST /Account/Signup` (MVC controller, `[ValidateAntiForgeryToken]`).
Required fields (from `SignupViewModel`): `DisplayName`, `Email`, `Password`,
`ConfirmPassword` (min 8 chars), plus the `__RequestVerificationToken`.

Use a **unique, identifiable email** so we can find the row unambiguously:
`u6smoke@kumunita.example`

### 3a. GET the form, capture token + cookies

```powershell
# Save cookies + response body
$r = Invoke-WebRequest "http://localhost:5080/Account/Signup" -UseBasicParsing
$token = [regex]::Match($r.Content, 'name="__RequestVerificationToken"\s+value="([^"]+)"').Groups[1].Value
Write-Host "Token length: $($token.Length)"
# Also note the cookie values
```

Save the cookie file:

```powershell
$cookieHeader = $r.Headers["Set-Cookie"] -join "; "
```

Actually it's simpler to use `curl` with a cookie jar — avoids PowerShell cookie
handling issues:

```powershell
# Get antiforgery token from the form HTML
$html = curl.exe -s -c cookies.txt "http://localhost:5080/Account/Signup"
$token = [regex]::Match($html, 'name="__RequestVerificationToken"\s+value="([^"]+)"').Groups[1].Value
Write-Host "Token: $token"
```

### 3b. POST the signup

```powershell
$pw = "U6SmokeTest!2026"
$email = "u6smoke@kumunita.example"
$body = "DisplayName=U6Smoke&Email=$email&Password=$pw&ConfirmPassword=$pw&__RequestVerificationToken=$token"

resp = curl.exe -s -b cookies.txt -c cookies.txt -X POST "http://localhost:5080/Account/Signup" `
    -H "Content-Type: application/x-www-form-urlencoded" `
    -d $body `
    -w "HTTP %{http_code}" 
Write-Host $resp
```

**Expected:** HTTP 302 (redirect to `/Account/Login`). A 200 (back to the form)
with validation errors means the token was wrong — retry from 3a.

### 3c. Confirm the `OutboxEmail` row landed

```powershell
docker exec kumunita_db psql -U kumunita -d kumunita -At -c "
  SELECT id, data->>'Recipient', data->>'IdempotencyKey', data->>'Subject', data->>'QueuedAt'
  FROM mt.mt_doc_outboxemail
  WHERE data->>'IdempotencyKey' LIKE 'verify:%:1'
    AND data::text LIKE '%u6smoke@kumunita.example%'
  ORDER BY mt_last_modified DESC
  LIMIT 1;
"
```

**Expected output (1 row):**
- `Recipient`: `u6smoke@kumunita.example`
- `IdempotencyKey`: `verify:{userId}:1` (where `{userId}` is the new user's Guid)
- `Subject`: `Verify your Kumunita account`
- `QueuedAt`: a timestamp very close to "now"

**Record the `id` and `IdempotencyKey`** for the evidence section.

### 3d. Confirm the Wolverine durable envelope existed (C3 proof)

The envelope lives in `mt.wolverine_incoming_envelopes` (the local durable queue
— `UseDurableLocalQueues()` creates this; it's **incoming** because the handler
consumes the message from its local queue, not from an outgoing transport queue).

The `body` column is a serialized byte array of `Envelope`. We can check the
envelope exists and was consumed by looking at the handler's effect: if the email
was **sent** (Mailpit received it), the handler's `Handle` method consumed the
message and the envelope row may be deleted (Wolverine deletes consumed
envelopes from the durable queue by default). To prove the envelope existed
independently of its later deletion, the Mailpit delivery itself is the positive
proof (step 3e). As a secondary check, look at the log of `attempts` on the
envelope — but since we may need to act before Mailpit delivers, the most
reliable C3 proof is the **Mailpit delivery timestamp matching the `QueuedAt`
within seconds**.

```powershell
# Quick check: are there any pending (unconsumed) envelopes for OutboxEmail?
docker exec kumunita_db psql -U kumunita -d kumunita -At -c "
  SELECT id, destination, message_type, attempts
  FROM mt.wolverine_incoming_envelopes
  ORDER BY deliver_by DESC LIMIT 5;
"
```

If the handler already consumed it (fast delivery), this may show 0 `OutboxEmail`
envelopes — that's fine, step 3e confirms delivery.

### 3e. Confirm Mailpit received the email

Mailpit's web API is on `http://localhost:8025`:

```powershell
# List all received emails via Mailpit API
Invoke-RestMethod "http://localhost:8025/api/v1/messages?limit=5" | Select-Object -ExpandProperty messages |
    Select-Object id, subject, from, to, created_at |
    ConvertTo-Json
```

**Expected (at least 1):**
- `to`: `[u6smoke@kumunita.example]`
- `subject`: `Verify your Kumunita account`
- `created_at`: within seconds of the `QueuedAt` timestamp from step 3c —
  this is the C3 timing proof (envelope was held until the Marten commit, then
  dispatched immediately by the durable handler)

---

## Step 4 — (Optional) Dead-letter path check

The dead-letter path (`Fault<OutboxEmail>` → `EmailDeadLetter` row) requires the
send to fail (e.g. SMTP down) AND the full 6-cooldown retry schedule (5 + 15 +
45 + 120 + 275 + 980 min ≈ 24 h) to exhaust. **This cannot be observed in a
realistic smoke-test window.** The dead-letter path is covered by the unit tests
(`SideEffectHarnessTests`, `EmailDeadLetterCounterTests`) which use a fake
`ISmtpSender` that always throws — those tests already pass (confirmed in U5).

Per the U6 spec, the dead-letter check is listed as an *alternative* to the
positive-path delivery confirmation ("or (if send fails deliberately, e.g. SMTP
down)"). Since the positive path (Mailpit delivery) is simpler, faster, and is
the primary scenario the whole C3 change exists to enable, **U6 uses the
positive-path delivery as its evidence**, and notes that the dead-letter path is
covered by the (passing) unit tests.

> If, for operational reasons, a dead-letter row is also desired as evidence:
> stop Mailpit, run a second signup (`u6smoke2@kumunita.example`), verify the
> `OutboxEmail` row exists but no Mailpit delivery arrives, then wait for the
> first retry (5 min cooldown) and confirm `wolverine_incoming_envelopes.attempts
> >= 1` for that envelope. The full dead-letter row won't appear until ~24 h
> have elapsed, so this step can be left as a post-incident follow-up if
> needed.

---

## Step 5 — Cleanup

The smoke-test account `u6smoke@kumunita.example` is a real account in the dev
DB. If it causes issues (e.g. blocks the `FirstBootSeeder` idempotency on
reboots, or appears in the directory), delete it:

```sql
-- Optional: delete the smoke-test account and its artifacts
DELETE FROM mt.mtdoc_profile WHERE subjectid = '{the-smoke-user-id}';
DELETE FROM mt.mtdoc_outboxemail WHERE data->>'Recipient' = 'u6smoke@kumunita.example';
-- (and the IdentityToken row if present)
```

Or leave it — it's a dev-environment test account and does no harm.

---

## Evidence section (for the handoff note)

The handoff note's U6 section should contain, verbatim:

1. The **baseline counts** from step 2.
2. The **new OutboxEmail row** (id, Recipient, IdempotencyKey, Subject, QueuedAt).
3. The **Mailpit delivery** (message id, to, subject, created_at).
4. The **timing delta** (QueuedAt → Mailpit created_at) — expected to be
   0–10 seconds (near-instant after commit).
5. A one-line confirmation: "C3 envelope path verified end-to-end against the
   U1–U5 code; email delivered to Mailpit within N seconds of signup commit."
6. Note the dead-letter path: "covered by SideEffectHarnessTests (7/7) and
   EmailDeadLetterCounterTests (1/1), both green in U5; not re-run here because
   the 24-h cooldown schedule makes live observation impractical."

---

## Exit checklist (mirrors plan line 143)

- [x] `m1-step7-u6-plan.md` created (this file).
- [x] `kumunita_app` container rebuilt from current source and restarted.
- [x] One real signup performed, `OutboxEmail` row confirmed in `mt.mt_doc_outboxemail`
      with correct `IdempotencyKey` shape (`verify:{userId}:1`).
- [x] Email delivery confirmed in Mailpit (message present, recipient + subject
      match, created_at within seconds of QueuedAt).
- [x] Handoff-note section `## U6 — e2e (evidenced)` appended to
      `m1-step-7-handoff-notes.md` with the evidence above.
- [x] No source code changes made (U6 is a read-only smoke test).

---

## U6 e2e evidence (smoke check completed)

**Status: complete** — happy path delivered (round 2); §6.2 retry path observed
live (round 1).

### Environment

- `kumunita_db` (postgres:18) + `kumunita_mailpit` (axllent/mailpit) +
  `kumunita_app` (Kumunita.Web).
- `kumunita_app` rebuilt from current source (U1–U5): new image
  `sha256:cd8a31da…` replaced the stale `sha256:a07777…` (pre-U1, whose
  `OutboxEmailStager` only `Store()`d the row and never enqueued a durable
  envelope). Rebuilt with
  `docker compose -f docker-compose.yml up --build app` (from `D:\repos\Kumunita`).
- A **local-only** `docker-compose.override.yml` in the repo root supplied
  `SMTP__From=kumunita@localhost` for the dev Mailpit relay. This is a
  dev-runtime config gap, **not a source-code change**: the base
  `docker-compose.yml` already sets `SMTP__Host=mailpit / Port=1025 / Secure=None`.
  `SmtpSender.SendAsync` only sets `mailmessage.From` when `SmtpOptions.From` is
  non-empty (lines 250-251); with `From` empty the BCL `SmtpClient` throws
  "A from address must be specified" before reaching the relay. Not committed.

### Round 1 — §6.2 retry path observed (SMTP send failed, retry continuation held)

- New `mt_doc_outboxemail` row: `id=1a35ca7e…`, `Recipient=u6smoke@kumunita.example`,
  `IdempotencyKey=verify:f3891fb0…:1`, `QueuedAt=2026-09-07T12:36:52.102Z`,
  `mt_last_modified=12:36:52.163Z` (commit).
- Durable envelope produced in the same commit: `mt.wolverine_incoming_envelopes`
  id `08df0cdc-b18c-f4f4-8607-5caf16fe0000`, `message_type=Kumunita.Core.Identity.OutboxEmail`.
- App log (durable handler firing against that envelope, BCL failing on missing
  From, Wolverine retry continuation picking it up per `RetryWithCooldown`):

  ```
  fail: Kumunita.Core.Identity.OutboxEmail[105]
        Failed to process message ...OutboxEmail#08df0cdc-b18c-... from local://kumunita.core.identity.outboxemail/
        System.Net.Mail.SmtpException: Failure sending mail.
         ---> System.InvalidOperationException: A from address must be specified.
          at System.Net.Mail.SmtpClient.SendMailAsync
          at Kumunita.Core.Identity.SmtpSender.SendAsync(...) in SmtpSender.cs:line 261
          at Kumunita.Web.SideEffects.OutboxEmailHandler.Handle(...) in OutboxEmailHandler.cs:line 68
          at Wolverine.Runtime.MessageContext.RetryExecutionNowAsync() in MessageContext.cs:line 498
          at Wolverine.ErrorHandling.RetryInlineContinuation.ExecuteAsync(...) in RetryInlineContinuation.cs:line 43
  ```

  This proves the C3 path live: a real `OutboxEmail` row + a **real durable
  envelope** (produced in the same commit) reached the handler before the handler's
  own throw. The next retry is held by the cooldown schedule. (Round-1 row +
  envelope were then deleted to give round 2 a clean baseline.)

### Round 2 — happy path, Mailpit delivered (the plan's positive-path deliverable)

- Baseline (just before the test): `outbox=2, inbound-env=4, outbound-env=0, dead-letter=0`.
- HTTP: `GET /Account/Signup` → 200 (form, antiforgery token captured);
  `POST /Account/Signup` → **302 → /Account/Login** (the exact success-path
  response of `AccountController.Signup:70`).
- New `mt_doc_outboxemail` row (the commit the durable handler waits on):
  - `id=d5f6c76f625240b88d035937ce211aea`
  - `Recipient=u6smoke2@kumunita.example`
  - `IdempotencyKey=verify:6220ddfa96b44db4aecc06977e20c21f:1`
  - `Subject=Verify your Kumunita account`
  - `data->>'QueuedAt'=2026-09-07T12:47:04.178Z`; `mt_last_modified=12:47:04.232Z` (commit)
- Durable envelope: `mt.wolverine_incoming_envelopes` id `08df0cde-1e60-458c-e2ec-12452d240000`,
  `status=Handled` (consumed after the successful send), `message_type=Kumunita.Core.Identity.OutboxEmail`.
- Counts after delivery: `outbox=3, inbound-env=5, dead-letter=0` (dead-letter stayed
  0 both rounds — the §6.2 positive path is reachable).
- Mailpit (`GET http://localhost:8025/api/v1/messages?limit=5`):

  ```json
  {
    "ID": "39l77mDCYQ79YhR0IU5hro",
    "From":  { "Address": "kumunita@localhost" },
    "To":    [ { "Address": "u6smoke2@kumunita.example" } ],
    "Subject": "Verify your Kumunita account",
    "Created": "2026-09-07T12:47:06.064Z",
    "Size": 897
  }
  ```

### C3 timing proof

- `QueuedAt` = `2026-09-07T12:47:04.178Z`
- `mt_last_modified` (row commit) = `2026-09-07 12:47:04.232Z`
- Mailpit `Created` = `2026-09-07T12:47:06.064Z`
- Δ (commit → relay accepted) ≈ **1.8 s** — the envelope was held by Wolverine
  **until the caller's `SaveChangesAsync` committed**, then dispatched by the durable
  handler within ~1.8 s. That is exactly the C3 guarantee U1–U5 restored.

### Disposition of U6 scratch artifacts

- `docs\plans-milestones\u6-signup-post.ps1`, `u6-signup-post2.ps1`, `u6-evidence.sql`
  are scratch scripts kept for audit of exactly what ran; they are **not** part of the
  source tree and should **not** be committed.
- `docker-compose.override.yml` is a local-only dev-runtime config gap filler;
  **not committed**, and can be deleted once the dev DB is reset.
- The round-2 `OutboxEmail` row, its envelope, and the Mailpit message were left
  in place as a record of the successful delivery (dev environment; harmless).
- The two pre-existing `OutboxEmail` rows (admin setup + a prior verify) have **no**
  matching durable envelope rows — direct evidence that the pre-U1–U5 code stored the
  domain row without enqueuing the envelope, i.e. the bug this milestone fixes.
