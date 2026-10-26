# ADR 0174 — MFA enrollment: QR code pairing + first-class Security tab

Status: Accepted
Date: 2026-10-26

Extends the **per-resident MFA enrollment surface** (F3, AUTH-03, the
`SecuritySettingsController` at `/settings/security/mfa`, shipped in the
prod-hardening lane, ADR 0171 context), the **settings-tab shape** (ADR 0080,
the `_SettingsTabs.cshtml` sub-nav strip), and the **closed
`KnownTranslationKeys` registry + en/de/fr/da parity pins** (ADR 0015 / ADR
0052). This ADR makes the MFA enrollment surface **reachable from the
settings strip** (a new **Security** tab, alongside Language / Time zone /
Date & time format / Items per page / Home page / Quiet hours / Messaging /
Children / Change password / My data / Kiosk) and makes **pairing an
authenticator app** a scan-the-image affair rather than a
copy-the-key affair — **zero new bounded context, doc, or authorization
surface; zero migrations; zero new `AccessAction` / `AccessVia` / `Decide()`
branch / adapter** (the C-M conventions hold).

## Context

The MFA enrollment surface (F3, AUTH-03) ships at
`/settings/security/mfa` — a `[Authorize]`d `GET` (enrollment state: the
one-time setup key + `otpauth://` URI + 10 recovery codes, shown once on
first enable; count-only on a steady state) + the enable / disable / rotate
POSTs. The surface is functional and battle-tested (the F3.U5 pinned tests:
`GlobalAdminLogin_RequiresMfaChallenge` / `GlobalAdminRecoveryCode_WorksOnce`
/ `NonAdmin_MfaOptional`).

Two gaps persisted after the prod-hardening lane closed:

1. **No first-class navigation entry.** The `_SettingsTabs.cshtml` sub-nav
   strip (ADR 0080 shape) lists Language, Time zone, Date & time format,
   Items per page, Home page, Quiet hours, Messaging, Children, Change
   password, My data, Kiosk — but **no Security tab**. A resident who wanted
   to set up or change MFA had to know the URL and type it. The surface was
   discoverable only via the deep link in the admin help guide, which is
   precisely the "reachability is the feature" problem the ADR 0080 tab
   shape exists to close.

2. **Pairing an authenticator app was a copy-the-key affair.** The
   enrollment leg rendered the `otpauth://` URI and the setup key as
   readonly `<input>` fields. A resident had to either (a) manually copy the
   setup key and type it into their authenticator app, or (b) copy the
   `otpauth://` URI and use an app's "import URI" feature. Neither is a
   one-step scan. On a desktop the QR-code flow is the canonical pairing
   pattern (Google Authenticator, 1Password, Authy, Microsoft
   Authenticator — all of them support "scan a QR code" as their primary
   onboarding path).

The constraint that keeps both additions safe: they are **pure
presentation-layer changes** to an existing `[Authorize]`d surface. No new
seam, no new doc, no new bounded context, no new migration, no new
authorization branch. The QR encodes the same `otpauth://` URI the resident
already had to copy — it is a *second encoding of the same data*, not new
data. The Security tab is a *second link to the same URL*, not a new route.

## Decision

### D1 — the QR code: server-side encode, embedded as a data URI

The enrollment leg's `otpauth://` URI is encoded to a PNG QR code
**server-side** using the [QRCoder](https://github.com/konradk/QrCode-generator)
library (v1.8.1, added to `Kumunita.Web.csproj`). The resulting PNG bytes
are base64-encoded and embedded in the view as a `data:image/png;base64,…`
`<img>` `src` attribute.

Rationale:

- **No client JS required.** The QR appears as a plain `<img>` tag; the
  page works with scripts disabled. (The repo's "no client JS" preference
  — ADR 0031, the tsc-only pin — is preserved; the QR is a static image,
  not a JS-rendered canvas.)
- **No new static asset.** The data URI is self-contained; there is no
  `/wwwroot/images/mfa-qr.png` to ship, cache, or version.
- **No cache / identity metadata.** A data URI carries no `ETag`, no
  `Last-Modified`, no `Content-Security-Policy` interaction — the image is
  opaque to the browser's cache layer, which is fine because the QR is
  re-generated on every enrollment GET (the key is fresh each time).
- **Graceful degradation.** If `QRCoder` throws (it should not for a
  short, fixed-shape `otpauth://` payload — the payload is well within
  the QR capacity for any ECC level), `BuildQrDataUri` returns `null` and
  the view renders the setup key + `otpauth://` URI fields (the
  manual-entry fallback) without the `<img>`.

### D2 — the QR appears only on the enrollment leg

The QR is rendered **only when `Model.QrDataUri` is non-null**, which is
**only on the enrollment leg** (the `GET /settings/security/mfa` response
where `Model.Enabled == false` and `Model.SetupKey != null`). On a steady
state (`Enabled == true`, `SetupKey == null`) or a rotate result (`Enabled
== true`, `RecoveryCodes != null`, `SetupKey == null`) there is no fresh
pairing key to encode, so no QR is rendered. This is the correct scoping:
the QR's purpose is to pair a *new* authenticator app; on a steady state
the app is already paired, and on a rotate the resident is replacing
recovery codes, not pairing a new app.

### D3 — the Security tab: a first-class settings strip entry

A new **Security** tab is added to `_SettingsTabs.cshtml` (the ADR 0080
sub-nav strip), linking to `/settings/security/mfa`. The tab's active
detection covers the full `SecuritySettingsController` action family
(`Mfa`, `MfaRecovery`, `MfaRotateRecovery`, `MfaDisable`, `Sessions`),
matching the controller-aware active-detection idiom already in use
(`("Guardian", _) => "Children"`, `("Kiosk", "SettingsGet") => "Kiosk"`,
etc.).

The tab's label is the new `settings.security.title` kw-l key (ADR 0015
registry, × en/de/fr/da). The key is registered in all four language
dictionaries (the ADR 0015 / ADR 0052 parity pins: `DeValues` / `FrValues`
/ `DaValues` must key-for-key match `AllKeys`).

The Security tab is placed **after "Change password" and before "My data"**
in the strip — the security-related tabs (Change password, Security)
cluster together, and the personal-data tab (My data) follows, preserving
the strip's rough grouping (language/timezone → display → messaging →
children → security → data → kiosk).

### D4 — the `settings.security.title` key set

One new key:

| Key | en | de | fr | da |
|-----|----|----|----|----|
| `settings.security.title` | Security | Sicherheit | Sécurité | Sikkerhed |

This is the **entire** new key set — the MFA surface's body text (the
"Pair your authenticator app" heading, the "Enter the 6-digit code" label,
etc.) remains plain English, consistent with the F3.U3 discipline (the
`Mfa.cshtml` view's doc-comment: "Plain-English strings, deliberately not
kw-l keys — the KwLRegistryConsistencyTests view-scan fails the build if a
kw-l key is used but not in KnownTranslationKeys, and registering new keys
would require a 4th file … which exceeds this unit's ≤3-file budget. The
enforcement leg (F3.U4) and the multilingual close-flip may register the
keys then; the surface works en-only today."). The tab *label* is
translated because it appears in the shared settings strip (which is
rendered on every settings page, not just the MFA page), and the ADR 0015
parity discipline requires a registered key for any kw-l usage in a Razor
view.

## Consequences

- **`Kumunita.Web.csproj`** gains one `<PackageReference Include="QRCoder"
  Version="1.8.1" />` (Web-only — Core stays HTTP-free, ADR 0006-D; the
  QR encode is a presentation concern, not a domain concern).
- **`SecuritySettingsController`** gains one private static helper
  (`BuildQrDataUri`) + one new field on `MfaSettingsViewModel`
  (`QrDataUri`) + one line in the enrollment-leg `Mfa()` action
  (`QrDataUri = BuildQrDataUri(otpAuthUrl)`). The enable / disable /
  rotate / recovery actions are **untouched** (they do not render a QR).
- **`MfaSettingsViewModel`** gains one nullable `string? QrDataUri { get;
  init; }` field (the ADR 0004 §B.1 additive-field shape, though this is a
  Web-layer view model, not a Marten doc — the convention is the same:
  additive, nullable, zero-migration).
- **`Mfa.cshtml`** gains a `<img>` block in the enrollment leg (guarded by
  `Model.QrDataUri != null`), and the "Scan the QR-equivalent setup key
  below" wording is updated to "Scan the QR code below" (the QR is now
  the primary affordance; the setup key + URI are the fallback).
- **`_SettingsTabs.cshtml`** gains one tab entry + five active-detection
  arms (the `SecuritySettings` controller action family). The existing
  ten tabs are **untouched** (no re-ordering of the existing entries —
  the Security tab is inserted at its natural position, after "Change
  password" and before "My data").
- **`KnownTranslationKeys.cs`** gains one key (`settings.security.title`)
  in all four language dictionaries (en / de / fr / da) — the ADR 0015
  parity is preserved.
- **`WhatsNew.cs`** gains a new `0.72.0` entry (the required sixth
  close-flip member, ADR 0110 / AGENTS.md). The `WhatsNewTests` head pin
  is updated (the `0.72.0` entry becomes the head; the `0.71.0` entry
  slides one row back).
- **Zero** new `AccessAction` / `AccessVia` / `Decide()` branch /
  `IAuditableResource` adapter (the C-M conventions hold — the QR is a
  rendering of existing data, the tab is a link to an existing URL).
- **Zero** migrations (no Marten doc, no EF Core table, no schema change).
- **Zero** new bounded context (the `SecuritySettingsController` is
  unchanged in its bounded-context standing — F3's AUTH-03 surface, the
  ADR 0006 "thin token, fat authorization" posture).
- **Zero** new `kw-l` keys beyond the one tab label (the MFA body text
  remains en-only, per the F3.U3 discipline).
