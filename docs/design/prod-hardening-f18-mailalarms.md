# F18 — MAIL-06: bounce/complaint/volume alarms — Part-0 design pin

> **F18.U1** design pin (Tier 1). F18.U2 and F18.U3 execute against this file.
> No code, no build, no test run — the pin is the deliverable.
>
> Plan entry: `plan-prod-hardening.md` F18 (MAIL-06, **`code+ops`**, dep F12).
> Precedent: F17.U1 / F16.U1 / F15.U1 / F14.U1 / F12.U1 design-pin idiom.
> **This fix is mostly owner-ops** — the code leg (U2) is a thin in-repo
> cross-check tick; the load-bearing leg (U3) is the owner configuring the
> **provider's** alarms. This pin exists so U3 is a **verbatim checklist**,
> not a design decision at ops time.

---

## §1 — The metrics source (PIN: provider console/API, cross-checked in-repo)

**Primary source (owner-ops, U3): the production mail provider's
bounce/complaint/volume metrics.** OPS §7 pins the provider choice as
*"a transactional provider (Resend / Postmark / SES / Mailgun) or the
community's own mail server. Choose one and record it per instance."* The
provider's console/API exposes, per sending domain:

| Metric | What the provider reports | Alarm use (§2) |
|---|---|---|
| **Bounces** | Hard + soft bounce count / rate per recipient, rolling window | bounce-rate threshold |
| **Complaints** (abuse reports) | Complaint count / rate; most providers auto-suppress the recipient | complaint-rate threshold |
| **Volume** | Sent / delivered / failed per hour and per day | volume-spike threshold |

**Secondary source (in-repo, U2's cross-check): the two landed data
structures the alarms protect.** Both are already in the production data
path and are readable in Postgres **without provider access** — this is what
makes U2's code leg meaningful even before/after the owner's U3 wiring:

- **`SuppressedAddress`** (F17, landed — `src/Kumunita.Core/Notifications/SuppressedAddress.cs`,
  on the `NotificationDocTypes` surface). One row per bounce/complaint the
  provider webhook delivered to `POST /api/mail/suppress`. The `Created`
  timestamp + `Reason` (`"bounce"`/`"complaint"`) give an in-repo **rate**:
  new suppressed rows per window. **If the provider's own alarm fires, a
  matching spike in `SuppressedAddress` rows should be visible here** — the
  cross-check leg. (F17's open item — pointing the provider webhook at the
  route — is a **precondition** of this cross-check; recorded, not assumed.)
- **`EmailDeadLetter`** (`mt.email_dead_letters`, the F11/OPS §7-§8 row).
  Delivery **failures** (transient, exhausted retries) — adjacent to bounces
  and already driving the `/health` degraded gate. U2's tick reads the count
  as a second signal; it does **not** change the gate (F12 pin §3 invariant).

**What the metrics source is NOT:** no new telemetry pipeline, no provider
SDK, no new dashboard service. The owner reads **the provider's own console**
(alarm push via the provider's built-in notification email/webhook) and the
app's own `SuppressedAddress` / `EmailDeadLetter` rows (via the in-app
GlobalAdmin view / a Postgres query per the OPS runbook).

---

## §2 — The thresholds (PIN: concrete values, U2 enforces them in-repo, U3 configures them at the provider)

Values are pinned here so U2 (the tick) and U3 (the provider console)
implement the **same** numbers. All windows are rolling, UTC, evaluated at
the tick's cadence (hourly) and at the provider's own alarm cadence.

| # | Alarm | **Pinned threshold** | Window | Severity → action |
|---|---|---|---|---|
| T1 | **Bounce-rate** | **≥ 2 %** of unique recipients sent | last **24 h** | **alert** — owner checks the provider console + the `SuppressedAddress` spike; if sustained, pause non-critical mail (verification is exempt — OPS §7 "verification emails are load-bearing") |
| T1′ | Bounce-rate critical | **≥ 5 %** | last 24 h | **critical** — this is the industry deliverability death-line (the point at which providers like Mailgun/Postmark start rejecting); stop sending non-critical mail, investigate the recipient list source |
| T2 | **Complaint-rate** | **≥ 0.1 %** of unique recipients sent, **or ≥ 1 complaint** (whichever first — at T1 community volume, 1 complaint *is* a signal) | last **7 d** | **alert** — a complaint means a human reported this platform as spam; check the sender volume + the `SuppressedAddress` `Reason="complaint"` rows |
| T3 | **Volume spike** | sent volume in any 1-h window **≥ 3× the median 1-h sent volume of the preceding 7 days** | per 1-h window | **alert** — a 3× spike is either a legitimate burst (an announcement to the whole community) or a compromise/loop; the owner distinguishes by the app's recent activity (audit log, GlobalAdmin view) |
| T4 | **In-repo bounce/complaint ingest rate** (the U2 code leg) | **≥ 5 new `SuppressedAddress` rows** (`Reason` ∈ `"bounce"`/`"complaint"`, `Created` in the last 24 h) | last 24 h | **alert** via the F12 channel (one `AlertEvent`, `Kind="mail.bounce-alarm"`) — the provider-independent leg: fires even if the provider's own alarms are not yet configured (pre-U3) or misconfigured |

**T4 is the only threshold U2 enforces in code** (it is the one computable
entirely in-repo from the F17 data source — the provider's per-recipient
bounce *rate* denominator, "unique recipients sent", is **not** an in-repo
quantity the app tracks, so T1–T3 are **provider-side** — that split is the
`code+ops` split, pinned here). The pin deliberately does **not** invent an
in-app sent-volume ledger to compute T1–T3 (that would be a new write path +
a new doc — scope creep past a hardening fix); the provider already tracks
its own volume.

**Threshold rationale (recorded for the owner, not negotiable per-unit):**
2 % bounce / 0.1 % complaint / 5 % hard-fail are the de-facto transactional-mail
deliverability lines (Gmail's bulk-sender guidelines, Mailgun/Postmark
suppression defaults). 3× volume is a conservative "something changed" line —
high enough that a normal community announcement burst (the platform's
highest-volume event) does not trip it on a quiet baseline, low enough that a
mass-mailing compromise does.

---

## §3 — The alert channel (PIN: reuse F12's `SupportEmailAlertSink` — **no second sink**)

The F11 pin **pre-reserved** this: `AlertEvent.Kind` is a `string` (not an
`enum`) precisely so F18 can add a kind **without a type change**, and its
comment names the value:

- **The new kind: `"mail.bounce-alarm"`** (the exact string — F11 pin §2b:
  *"F18 adds `"mail.bounce-alarm"`, F12 routes regardless of kind"*).
- **The payload mapping (fixed, U2 implements):** `Subject` = the alarm name +
  the breached threshold (e.g. `"SuppressedAddress ingest: 7 new bounce/complaint
  rows in 24h (threshold 5)"`); `Detail` = the `Reason` breakdown
  (`"bounce: 5, complaint: 2"`) — operator-readable, no PII beyond what the
  `SuppressedAddress` list already stores (the emails themselves stay in the
  DB, **not** in the alert body); `Attempts` = **null** (the F11 pin:
  *"nullable so a non-retry alert, e.g. F18's volume alarm, can carry no
  attempt count"*).
- **The channel is F12's, unchanged.** U2 calls the **existing**
  `IAlertSink.ReportAsync(AlertEvent, ct)` (`src/Kumunita.Core/Identity/AlertSink.cs`)
  — in production that resolves to **`SupportEmailAlertSink`**
  (`src/Kumunita.Web/SideEffects/`, F12.U2, registered in `Program.cs` behind
  the `Community:SupportEmail` non-empty gate, winning DI over the
  `NoopAlertSink` floor). **No new `IAlertSink` binding, no new method on the
  seam, no new channel class.** The F12 sink is kind-agnostic (F12 pin §3 —
  it composes the `OutboxEmail` from the `AlertEvent` fields and does not
  switch on `Kind`), so `"mail.bounce-alarm"` rides the **same** binding with
  zero F12/F11/F10 file changes.
- **Best-effort-at-the-boundary honored:** U2's tick wraps the `ReportAsync`
  call in `try/catch` swallow (the `IAlertSink` contract — a channel failure
  must never propagate; the `SuppressedAddress` rows + the provider console
  are the load-bearing signals, the alert is a copy — F11 pin §2a).
- **Off-by-default floor kept:** an unconfigured instance (no
  `SupportEmail`) resolves `IAlertSink` to `NoopAlertSink` → the tick's
  `ReportAsync` is a no-op (the F12 pin §3 "floor" invariant, unchanged).
- **The provider-side alarms (T1–T3) do NOT flow through `IAlertSink` at
  all** — they are the provider's own notification email/webhook, configured
  by the owner at the provider (U3). Their delivery target is pinned in §4
  to the **same monitored mailbox** as the F12 channel
  (`Community__SupportEmail`) so one human inbox receives both legs — but
  the platform's code is not in that path. This is the `code+ops` split made
  explicit: **one shared destination, two independent delivery paths.**

---

## §4 — The pinned owner-ops steps (U3 executes verbatim; **recorded, not assumed done**)

> The provider is owner-chosen (OPS §7). The steps below are pinned
> provider-agnostically with the per-provider console names in parentheses —
> the owner follows the branch matching their recorded provider.

**Precondition (F17's open item must be closed first — it is the data
source's flow):** the provider's bounce/complaint webhook is pointed at
`POST https://<instance>/api/mail/suppress` with the
`X-Suppression-Token` header set, and at least one test bounce has produced
a `SuppressedAddress` row. (F17.U3 handoff note — the F18 alarm has nothing
to cross-check against until this flows.)

1. **Record the provider + alarm IDs per instance** (OPS §7's
   "Choose one and record it per instance"): add to the instance's OPS
   record — provider name, sending domain, the alarm/notification rule IDs
   created in steps 2–4. (One line in the per-instance record; this is where
   the "last verified" date lives, per the F4/F20 runbook idiom.)
2. **Bounce alarm (T1/T1′):** in the provider console
   (SES: *Notifications* → bounce notifications + *Event destinations* → SNS
   topic with an email subscription; Postmark: *Suppression* +
   *Server stats* alerts; Resend: *Alerts* → bounce-rate rule; Mailgun:
   *Sending → Suppressions* + *Routes/notifications*), create a rule:
   **bounce rate ≥ 2 % over 24 h → email notification to
   `<Community__SupportEmail>`**, and a **critical** rule at **≥ 5 % / 24 h**
   to the same address. Confirm the rule is *active* (not draft) and note its
   ID in step 1's record.
3. **Complaint alarm (T2):** create a rule: **complaint rate ≥ 0.1 % over
   7 d, or first complaint of any rolling 7 d → email notification to
   `<Community__SupportEmail>`**. (SES: complaint event on the same
   destination; Postmark: *Suppression* complaint alert; Resend:
   *Alerts* → complaint rule; Mailgun: *Suppressions* notifications.)
4. **Volume alarm (T3):** enable the provider's daily/hourly volume anomaly
   or daily-digest notification (SES: *Event destinations* on `Send`/`Reject`
   counts or the console's *Usage* dashboard + a Uptime-Kuma-style poll per
   OPS §8; Postmark: *Usage* alerts; Resend: *Alerts* → volume rule;
   Mailgun: *Logs* digest) set to **notify `<Community__SupportEmail>` when
   any 1-h window is ≥ 3× the 7-day median 1-h volume**. Where the provider
   has no native "3× median" rule (common), the **in-repo T4 leg is the
   functional substitute** — record that explicitly in step 1's record rather
   than faking a provider-side rule that doesn't exist.
5. **Destination confirmation (F12's open item (a), re-stated as a
   precondition here):** confirm the prod `Community__SupportEmail` is a
   **real, monitored** mailbox a human reads — steps 2–4 deliver to it, and
   U2's T4 alert does too. One unmonitored inbox silently defeats all three
   alarms. (F12 pin §7(a) — the same open item, now load-bearing for F18.)
6. **Cross-check drill:** send one test email to a known-bouncing address
   (or trigger the provider's test-bounce tool); confirm (a) the provider
   console shows the bounce, (b) a `SuppressedAddress` row appears in the
   app's data (via `POST /api/mail/suppress`), (c) if ≥ 5 such rows exist in
   24 h, the F12-channel alert email arrived at the monitored inbox
   (U2's T4 leg — only testable once U2 has landed).
7. **Stamp "last verified"** in step 1's per-instance OPS record (the F4/F20
   runbook "last verified / last drilled" idiom).

**All of the above is recorded as an open item in the handoff note, NOT
assumed done** (the F17.U3 "point the webhook" / F16.U3 "run ClamAV" /
F10.U3 "set `SENTRY_DSN`" / F12.U3 "confirm a real recipient" pattern).
Until the owner runs it, the only active leg is **T4** (U2's in-repo tick,
fired when `SuppressedAddress` flow exists) — T1–T3 do not exist until
configured at the provider.

---

## §5 — Invariants (held by F18.U2/U3)

1. **Design-only U1:** this unit lands **exactly one** doc file — no `src/**`
   change, no `<PackageReference>`, no migration, no `.slnx` change, no build.
2. **One channel, not two:** F18 **reuses F12's** `SupportEmailAlertSink`
   (the existing `IAlertSink` DI binding) — **no new sink class, no new
   `IAlertSink` method, no second channel** (F12 pin §3 kind-agnostic routing;
   the F11 pin's `Kind`-as-`string` is the enabling design, honored).
3. **One data source, not a new one:** the in-repo alarm (T4) reads the
   **F17 `SuppressedAddress`** list (+ the existing `EmailDeadLetter` count) —
   **no new suppression list, no new sent-volume ledger, no new doc type, no
   new doc-surface registration** (the `NotificationDocTypes` surface is
   already booted; U2's tick is read-only over it).
4. **Core stays HTTP-free (ADR 0006-D):** U2's tick is a Core Wolverine
   recurring handler (the F15 `MediaLifecycleTick` idiom — idempotent,
   lock-protected) that calls the Core `IAlertSink` seam; the channel
   (already Web, F12) is untouched. No HTTP types in Core.
5. **The `/health` degraded gate is UNCHANGED** (OPS §8) — F18 is additive
   (push alongside the poll), like F11/F12; the `EmailDeadLetter` → degraded
   mapping, the `NoopAlertSink` floor, and the F10 Sentry deferral all stand
   as-is.
6. **Best-effort at the boundary:** the tick's `ReportAsync` is swallowed on
   failure (the F11 contract); a tick failure is logged, never propagated.
7. **Owner-ops recorded, not assumed done** (§4) — the T1–T3 provider alarms
   and the destination confirmation are **open items** in the handoff note.
8. **No PII in the alert body beyond what F17 already stores** (the
   `SuppressedAddress` emails stay in the DB; the alert carries counts +
   `Reason` breakdown only).

---

## §6 — What U2 and U3 execute (the split, pinned)

- **F18.U2 (code leg):** a `MailAlarmsTick` (Wolverine recurring, hourly,
  idempotent + lock-protected per the F15/JOB-04 advisory-lock idiom) in
  `Kumunita.Core.Notifications` (the F17 bounded context — the
  `SuppressedAddress` list lives there): count `SuppressedAddress` rows with
  `Created` in the last 24 h and `Reason` ∈ `"bounce"`/`"complaint"`; if
  **≥ 5** (T4), fire **one** `AlertEvent(Kind="mail.bounce-alarm",
  Subject=<threshold one-liner>, Detail=<reason breakdown>, Attempts=null)`
  through `IAlertSink.ReportAsync` (try/catch swallow); also read the
  `EmailDeadLetter` count into the `Detail` for operator context (read-only —
  the gate is untouched). One alert per tick (dedup by the tick itself —
  re-fire is one email per hour while breached, which is the intended
  "still broken, still loud" semantics, not a storm). ≤ 2 files.
- **F18.U3 (ops leg):** §4 verbatim — the owner executes steps 1–7 against
  the production provider + mailbox, records the alarm IDs + "last
  verified" date, and the handoff note marks §4 **done** (with the
  evidence: alarm IDs + the test-bounce drill result) or **still open**.
  **The open item stays open until the owner stamps it — not assumed.**

---

## §7 — Open items (owner, recorded, NOT assumed done)

1. **All of §4** (steps 1–7) — provider alarm rules T1/T1′/T2/T3 created and
   active, destination confirmed monitored, cross-check drill run, "last
   verified" stamped. Until then, **T1–T3 do not exist** (provider-side) and
   the only alarm is **T4** (U2's in-repo leg, which itself depends on F17's
   webhook being pointed — F17's open item).
2. **F12 pin §7(a), load-bearing here:** confirm the prod
   `Community__SupportEmail` is a real, monitored mailbox — it is the shared
   destination of both legs.
3. **F17 open item (precondition):** the provider bounce/complaint webhook
   pointed at `POST /api/mail/suppress` with the `X-Suppression-Token`
   header — without it the `SuppressedAddress` list is empty and T4 never
   fires (F17 pin §7, carried).

---

## §8 — Handoff-note contract for F18.U1

This unit appends **exactly one** `## F18.U1` section to
`docs/plans-milestones/in-progress/prod-hardening/hardening-handoff-notes.md`
recording: what landed (this one doc, nothing else), the **five pin targets**
(§1 the metrics source — provider console + the F17 `SuppressedAddress` /
`EmailDeadLetter` in-repo cross-check; §2 the thresholds T1–T4 with values;
§3 the F12 channel + the `"mail.bounce-alarm"` kind; §4 the owner-ops steps;
§5 the invariants), the **open items re-stated** (§7), and the forward
pointer: **F18.U2 next** (the `MailAlarmsTick`, reads §6 + §2 T4 + §3) — the
lane stays **open** (F18.U2–U3 + F19–F22 remain; **no 6-member close-flip**,
that is F22, the lane's last unit).
