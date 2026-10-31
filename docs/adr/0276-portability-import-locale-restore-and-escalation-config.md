# ADR 0276 — Portability import: restore the archive's locale settings, and carry the escalation outbound config (endpoint + enabled only)

Status: Accepted
Date: 2026-10-30
Extends the **ADR 0193 portability import modes** lane — specifically the
`config.json` surface that travels alongside the `docs/` payload — and leans on
**ADR 0159** (the escalation forwarding surface: `EscalationOutboundConfig`,
the `/admin/escalation` endpoint + token + enabled switch), **ADR 0019**
(platform default time zone — the `LocaleSettings.DefaultTimezone` field) and
**ADR 0050** (sign-up surface — the `LocaleSettings.IsSignupOpen` /
`NotifyAdminsOnSignup` fields). ADR 0274 / ADR 0275 (the `KeepExisting` tag +
principal merges) set the recent precedent this ADR follows: a deliberate,
documented extension of the M11 "locked `§config` field set" (community /
locale / `languages[]`), not a re-shape of the existing contract.

## Context

ADR 0193's `CleanSlate` mode is the **restore** semantic: the archive becomes
the instance's state (wipe, then re-materialize). Two parts of that state were
not in fact being restored.

**Locale settings — a real bug.** The archive's `config.json` carries the
community's `Locale` block (default language, the **default time zone**
(ADR 0019), the **default date format** (ADR 0020), and the **sign-up**
switches — `IsSignupOpen` / `NotifyAdminsOnSignup` — plus the
`AnnouncementCommentsEnabled` / `MessagingEnabled` surface toggles). But the
apply path, `PortabilityApplyConfig`, wrote the `LocaleSettings` singleton row
**only when the target had none** (`?? new LocaleSettings { … }` on a `null`
read). In the real app that condition never held: `FirstBootSeeder` runs on
boot and **always** creates the singleton row with the platform floors
(`UTC` / `DateFormat.FloorFormat` / `IsSignupOpen = false` /
`MessagingEnabled = false`). So a CleanSlate restore of a community that had
set its default time zone to `America/New_York` and opened sign-up left the
target at the seeder's `UTC` / sign-up-closed — the operator's visible
complaint, "the restore is missing the time zone and the sign-up settings."
The export side was correct (the archive *did* carry the values); the apply
side silently dropped them because the seeder's row was in the way.

**Escalation outbound config — a design gap.** The `EscalationOutboundConfig`
singleton (the `/admin/escalation` forwarding **endpoint**, the **enabled**
switch, and the **plaintext outbound token**) is a first-class, operator-set
row (ADR 0159), registered on the `EscalationDocTypes` surface and wired in
`Program.cs`. It was absent from the portability surface in three places: it
was not in the 44-doc inventory (so not exported, not wiped), not in the
`config.json` POCO (so `ConfigExport` never read it), and not in the apply
path (so a restore never re-materialized it). A restore therefore left the
target's `/admin/escalation` exactly as it was before — for a genuine restore
of the community's own backup that is the wrong shape: the archive's
endpoint + enabled switch are part of the community's state and should come
back with it.

The token is the complicating factor. It is a **plaintext outbound secret**
the target posts to its forwarding endpoint (the same "credential" class the
M11 `§principals` surface keeps out of the archive — the `PortabilityPrincipal`
POCO is structurally incapable of carrying a password, ADR 0113 C-M11·2).
The decision here applies that same boundary to the escalation surface: the
**endpoint + enabled** travel; the **token does not**.

## Decision

**D1 — A CleanSlate restore applies the archive's `LocaleSettings`; KeepExisting keeps the target's intact.**
`PortabilityApplyConfig.ApplyAsync`, in `CleanSlate`, now **overwrites** the
`LocaleSettings` singleton row with the archive's `Locale` values — default
language, **default time zone**, default date format, `IsSignupOpen`,
`NotifyAdminsOnSignup`, `AnnouncementCommentsEnabled` and `MessagingEnabled` —
so the seeder's floors no longer stick after a restore. (The old "write only
when the row is null" shape saw the seeder's already-present row and skipped
the write — the exact failure above.) A null `Locale` field on the archive
falls back to the row's existing value (the seeder's floor), so a partial
block still restores the fields it does carry. In `KeepExisting` the target's
`LocaleSettings` row is **left untouched** — the merge import never rewrites
the community's own time zone / sign-up / surface toggles.

**D2 — the escalation outbound config travels in `config.json` as endpoint + enabled only; the token is structurally absent.**
`config.json` gains an `escalation` block (`PortabilityConfigEscalation`)
carrying **`endpoint` + `enabled` only**. The plaintext `Token` is
**structurally absent from the POCO** (the C-M11·2 boundary applied to the
escalation surface — the same "no-secret set" the `PortabilityPrincipal`
enforces on the credential columns; a `PortabilityNoSecret_EscalationBlockHasNoTokenField`
pin guards it). Consequences, matching the "re-enter your credential" shape
the platform already uses for passwords:
- A **CleanSlate** restore re-materializes the singleton with the archive's
  **endpoint + enabled**, and a **blank token** — the admin then re-enters
  the forwarding token on `/admin/escalation` (the `Updated` / `UpdatedBy`
  stamp reflects the restore, `UpdatedBy = actorId`).
- A re-import over a target that **holds a token** leaves that token intact
  (the apply path never writes `Token`, so an existing value survives).
- A **null** `escalation` block (the source never set one up) leaves the
  target's row untouched.
- `KeepExisting` **never** overwrites the target's escalation config — the
  merge import keeps the target's own endpoint + enabled + token as-is.

**D3 — the apply path takes the actor id for the restore stamp.**
`PortabilityApplyConfig.ApplyAsync` gains an `actorId` parameter (between
`mode` and `ct`) so the CleanSlate restore can stamp `UpdatedBy` on the
re-materialized escalation row with the importing admin's id. The only code
caller (`PortabilityService`, step 4) passes the existing `actorId`.

## Why endpoint + enabled but not the token

The escalation **endpoint** ("where to forward") and the **enabled** switch
("is forwarding on") are configuration — the same class as the community name,
the default language, and the sign-up toggles that already travel in
`config.json`. They are what an operator expects to come back with a restore.
The **token** is a credential: plaintext, secret, and re-issuable. The
platform's existing posture for credentials across the portability surface is
to **not carry them** and to have the operator re-enter them on the target
(passwords are re-set, not copied; ADR 0113 C-M11·2). Re-using that posture
keeps the "no-secret set" invariant uniform: an archive can never exfiltrate
a secret by construction, not by a check that could regress. Re-entering the
one forwarding token after a restore is cheap; a leaked token in every backup
zip is not.

## Zero new surface

No new bounded context, no new document (the `EscalationOutboundConfig`
singleton is already registered and persisted — this only reads and re-writes
it), no new index, no new authorization branch, no migration. The change is a
config-surface extension (the `config.json` POCO + the apply path) + a bug fix
(the locale restore), both within the already-shipped portability + escalation
surfaces. `Milestones.cs` / README Roadmap / `MilestonesTests.cs` are
byte-identical (a named lane on the shipped portability surface, ADR 0193).
