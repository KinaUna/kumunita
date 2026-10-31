# SEC — Security-audit remediation — design doc

> Part 1 (this): Context, Scope, Invariants, Findings register. Written by U1.
> Part 2 (fix shapes, config keys, signatures, test names, acceptance gate,
> drift-guard) is appended by U2. Do not edit Part 1 outside the drift-guard.

## Context

A **static** security review of `main` @ `bdcaff7` (2026-10-30) found **3 High,
5 Medium, 8 Low** issues. The review read the tree at that revision; **nothing was
executed** — no live server, no exploit, no dependency scan (the scan is deferred
to U3). The authorization core, the CSP/XSS posture, SQL parameterization,
file-path handling and the MFA primitives held up. The gaps are places where
code drifted from `docs/SECURITY.md` §5–§6 (the control map and the open-items
register).

This lane's job is to close each finding with **one named regression test**, in
units small enough for a fresh agent with a 32K–64K token window. The source of
the findings is **Appendix A** of `docs/plans-milestones/in-progress/sec/plan-sec-remediation.md`
(the 2026-10-30 register). Its file:line references are from `bdcaff7` and are
marked *re-verify in U2* until U2 re-anchors them against the current tree.

## Scope

- **In:** all of **H1–H3** (High), **M1–M5** (Medium), **L1–L8** (Low).
- **Parked (kept as written units, not on the critical path — owner decision required before they start):**
  - **L1** (U28) — uniform signup/login responses. A privacy-vs-UX call; the
    owner must record `## Decision — L1` in the handoff note before U28 proceeds.
  - **L8c** (within U27's finding, but the log-forging sub-item is *parked*) —
    sanitize user-supplied strings before logging so logs can't be forged. This
    is the `SECURITY.md` §6 open item "Log hygiene … so logs can't be forged";
    it stays open and is named as deferred, not fixed by this lane.
- **Out (explicitly not part of this lane):**
  - New features or capability additions.
  - Dependency upgrades / advisory remediation (U3 records the scan result;
    fixing advisories is a separate decision).
  - Anything not named in the findings register above.

## Invariants

Every fix in this lane must preserve or establish the following. A fix that
violates one is a regression, not a fix.

- **S-1** — An import **never creates or changes a row the actor does not own**.
  (H1: `UserPortabilityService`)
- **S-2** — A GlobalAdmin session **cannot reach any route but enrolment/logout
  without 2FA** enrolled (and not a sample-data account). (H2)
- **S-3** — Client IP and scheme come **only from a configured trusted proxy**;
  unset/empty config never means "trust everyone". (H3)
- **S-4** — Every state-changing request **validates an anti-forgery token**
  unless explicitly token-authenticated (the two inbound/suppress endpoints). (M1)
- **S-5** — Every anonymous write has a **rate limit and a size cap**. (M2, M3)
- **S-6** — A session whose **stamp is missing or stale is rejected after the
  cut-off** (`Security:RevocationStamp:RequiredAfter`). (M5)
- **S-7** — User-controlled content **never produces an off-site link or an
  unvalidated file type**. (L2, L4)
- **S-8** — **Every fix has a named regression test** (pinned in Part 2).

## Findings register

Source: Appendix A of the lane plan (static audit of `main` @ `bdcaff7`,
2026-10-30). **Every file:line is from that revision — *re-verify in U2*.**

| ID | Sev | Finding | Where (audit-time — *re-verify in U2*) | Invariant | Unit(s) |
|----|-----|---------|----------------------------------------|-----------|---------|
| H1 | High | Resident import stores archive rows with no ownership or `Id`-collision check (forge authorship, overwrite documents, join private groups); tests only cover export | `UserPortabilityService.cs:714` (classify), `:1100`, `:1141` (`session.Store(row)`); `UserScopeInventory.cs` | S-1 | U4–U6 |
| H2 | High | GlobalAdmin 2FA is a redirect, not a gate; the admin cookie already exists; nothing checks `TwoFactorEnabled` | `AccountController.cs:802`, `:893-907` | S-2 | U7–U8 |
| H3 | High | `UseForwardedHeaders()` has no options (no-op): one shared rate-limit bucket, HSTS not emitted, cookie not `Secure` (depends on live proxy config) | `Program.cs:948`, `:951`, `:565`, `:599`, `:586` | S-3 | U9–U10 |
| M1 | Med | Seven state-changing POSTs lack anti-forgery validation; no global filter | `CommunityController` `SetMandatory` (114), `AddMember` (136), `RemoveMember` (158), `Leave` (198); `MessagesController` `Open` (162), `Send` (333); `PostsController` `Replies` (1280) | S-4 | U11–U12 |
| M2 | Med | Zip bomb (no entry-count / decompressed-size cap) and imported media bypasses upload gate and quota; import actions unthrottled | `KumunitaArchive.cs:273-300`; `UserPortabilityService.cs:1169` | S-5 | U13–U14 |
| M3 | Med | Anonymous writes without rate limit; error-report text has no length cap | `HomeController.cs` (`/Home/Error/Report`), `IssueController.cs` (`/issues/new`), `/escalations/inbound`, `/api/mail/suppress`, `ErrorReportFormModel.cs:29` | S-5 | U15 |
| M4 | Med | Anonymous `/health` does a DB query + SMTP handshake per request; full detail public unless `Health__Token` set; non-constant-time compare | `HealthController.cs:67-109` | S-5 | U16 |
| M5 | Med | Session revocation fails open when a cookie has no stamp; verify-link sign-in is unstamped; built-in stamp validator interval is 14 days | `RevocationStampMiddleware.cs:130`; `AccountController.cs:719`; `Program.cs:663` | S-6 | U17–U18 |
| L1 | Low | Account enumeration: signup message, lockout message, login timing (**parked — U28, owner decision**) | `AccountController.cs:522`, `:783`, `:855` | (parked) | U28 |
| L2 | Low | Upload type trusted from client; dedup keeps first uploader's declared type; ClamAV off by default | `AttachmentController.cs:100`, `ContentImageController.cs:190`, `ProfileController.cs:463`, `DocumentController.cs:413,646` | S-7 | U20–U22 |
| L3 | Low | Verification token "hash at rest" is accepted as a credential; GET link signs the browser in | `IdentityService.cs:1698`; `AccountController.cs` Verify | S-4 | U23–U24 |
| L4 | Low | Markdown link filter lets `/\host` through; `mailto:` never accepted | `MarkdownRenderer.cs:397` | S-7 | U19 |
| L5 | Low | Container runs as root; floating image tags; CI actions pinned by tag, no `permissions:` | `Dockerfile`, `.github/workflows/ci.yml` | (hardening) | U25 |
| L6 | Low | Dev compose publishes Postgres with default credentials, Development mode | `docker-compose.yml` | (dev hygiene) | U26 |
| L7 | Low | HSTS scope, `AllowedHosts: *`, `security.txt` URL from `Request.Host`, no `Permissions-Policy` | `Program.cs:951`, `appsettings.json:12`, `WellKnownController.cs` | S-3 | U26 |
| L8 | Low | Sample-account limiter exemption and form-body read inside limiter; non-constant-time break-glass compare; **log-forging open item (parked)** | `Program.cs:586-595`, `IdentityService.cs`, `SECURITY.md` | S-6 | U27 (L8c parked) |

**Parked sub-items (counted in the register, deferred by owner decision):**

1. **L1** (U28) — account enumeration / uniform responses. Privacy-vs-UX; needs
   `## Decision — L1` before it starts.
2. **L8c** (within U27) — log-forging hygiene (sanitize user-supplied strings
   before `ILogger`). Named as deferred in the close-out; not fixed by this lane.

**What held up (do not "fix"):** parameterized SQL at the three raw sites; strict
CSP and Markdown escaping; service-level authorization on Announcement actions;
hex-only media paths and safe download headers; lockout, TOTP code checks,
rate-limited second leg; anti-forgery on all other state-changing POSTs.

---

# Part 2 — pins (written by U2, 2026-10-30, against the current tree @ post-`bdcaff7`)

> **Re-verification status.** Every file:line in the Part 1 register was
> re-anchored against the live tree with targeted `grep -n` / `sed -n` (no
> whole-file reads of the five frozen large files). **No finding was already
> fixed or non-reproducible as a whole.** Two **sub-parts** are already-correct /
> not-reproducible and are marked *n/a* below (their units' deliverables shrink
> accordingly, but the unit still runs and records "n/a" in its handoff note):
>
> - **L4 mailto sub-part — already fixed.** `IsSafeUrl` already whitelists
>   `mailto` (`Security/MarkdownRenderer.cs:405` — `scheme is "http" or "https"
>   or "mailto"`). The audit's "`mailto:` never accepted" is stale. U19's work
>   is only the backslash/control-char hardening + the pinned tests.
> - **L8 break-glass constant-time compare — no C# compare exists to fix.**
>   `ConsumeBreakGlassAsync` (`IdentityService.cs:1451`) validates the token in
>   Postgres (`WHERE "token" = @token`, `:1471`, parameterised). There is **no**
>   C# `string.Equals`/`==` on the token value anywhere (confirmed by grep). U27
>   therefore makes **no** `IdentityService.cs` change for this sub-item and
>   records "n/a — no C# compare; comparison is parameterised SQL" in its note.
>   (The *other* two L8 items — the sample-account limiter exemption and the
>   form-body read inside the limiter — are real and still owned by U27.)

## 2.1 Re-verified register

Line refs are from the **current tree** (post-audit; line numbers drift as units
land — U2's numbers are the baseline, later units re-`grep -n` to re-anchor).
"Pinned anchors" name the stable grep targets U25 onward rely on.

| ID | Sev | Re-verified location (current tree) | Drift vs audit |
|----|-----|--------------------------------------|----------------|
| H1 | High | `UserPortabilityService.cs`: `ClassifyAsync` def **:714**; `session.Store(row)` **:1100** and **:1141** (both unchanged); inventory `UserScopeInventory.cs` (Entries **:66**, `IsInScope` **:158**); status enum `UserPortabilityEntityStatus` **`UserPortabilityService.cs:107`** (values: `Clean`, `Duplicate`, `Conflict` — **no `Rejected` yet**) | unchanged |
| H2 | High | `AccountController.cs`: password leg-1 `PasswordSignInAsync` **:802**; the 2FA "redirect, not gate" `return RedirectToAction("Mfa","SecuritySettings")` **:907** (audit `:893-907`); persistent re-sign-in `PasswordSignInAsync(...isPersistent:true)` **:923** | drifted (`:893-907` → `:907`) |
| H3 | High | `Program.cs`: **`app.UseForwardedHeaders()` (no options) :961**; `app.UseHsts()` (default 30-day MaxAge, **no explicit MaxAge** — grep confirms none) **:964**; the production-only block guard `:952-967`; `CookieSecurePolicy` only in a comment (`:958`), **no explicit `SecurePolicy` set** | drifted (`:948`→`:961`, `:951`→`:964`) |
| M1 | Med | `CommunityController.cs` `SetMandatory` **:115**, `AddMember` **:137**, `RemoveMember` **:159**, `Leave` **:199**; `MessagesController.cs` `Open` **:163**, `Send` **:335**; `PostsController.cs` `Replies` attr **:1280** (method `:1282`). **None of the seven carries `[ValidateAntiForgeryToken]`** (confirmed; the translation actions at `CommunityController` 287/347/403 and several `PostsController` actions do) | unchanged (method lines +1 vs attribute) |
| M2 | Med | `KumunitaArchive.cs`: `ReadAsync` **:327**, `ReadEntryBytesAsync` **:353** (copy loop); import media `UserPortabilityService.cs` `mediaStore.PutAsync` **:1172**; import actions `UserPortabilityController.cs` `import` **:129**, `import/resolve` POST **:253** (no `[EnableRateLimiting]`/`[RequestSizeLimit]` today) | drifted (`:273-300`→`:327/353`, `:1169`→`:1172`) |
| M3 | Med | `HomeController.cs` `Report` attr `**[HttpPost("/Home/Error/Report")] :271**` (no limiter today); `IssueController.cs` `[HttpPost("/issues/new")]` **:56** (no limiter today); `EscalationInboundController.cs` `[HttpPost("inbound")]` **:67** (Bearer-token, no limiter); `MailSuppressionController.cs` `[HttpPost("api/mail/suppress")]` **:66** (shared-secret `X-Suppression-Token`, no limiter); `ErrorReportFormModel.cs` `Description` **:29** (**no `[MaxLength]`** — the `:35` `MaxLength(254)` is on a different field) | drifted (`:29` holds; actions unpinned in audit → pinned now) |
| M4 | Med | `HealthController.cs`: `Get` **:36**; token set-check **:67**/**:81**; **non-constant-time** `string.Equals(presented, _healthOptions.Token, Ordinal)` **:73-74**; SMTP probe via `ISmtpHealthCheck` (constructor **:26**) run per request | unchanged |
| M5 | Med | `RevocationStampMiddleware.cs`: `kumunita.stamp` read **:121** (audit `:130`); **fail-open** path (empty stamp passes) — the `:100-168` region; `AccountController.cs` `ConfirmAndSignInAsync` def **:685**, its `HttpContext.SignInAsync` **:719** (unstamped); `Program.cs` `SecurityStampValidatorOptions` `ValidationInterval = 14 days` **:674-676** (audit `:663`) | drifted (`:130`→`:121`, `:663`→`:674`) |
| L1 | Low | *parked (U28)* — `AccountController.cs` signup "exists" message **:521-524**; login `user is null` branch **:781-785**; lockout text `result.IsLockedOut` **:853-857** | unchanged |
| L2 | Low | `AttachmentController.cs` `IsAttachmentAllowed` **:100** (Put `:108`); `ContentImageController.cs` `IsAllowed` **:190** (Put `:197`); `ProfileController.cs` `IsAllowed` **:463** (Put `:470`); `DocumentController.cs` `IsDocumentAllowed` **:413** (New path) and **:646** (edit-lane re-upload, Put `:652`). **All five trust `file.ContentType` from the client** — no magic-byte sniff today | drifted (audit `:413,646` → `:413/:646`, both confirmed) |
| L3 | Low | `IdentityService.cs`: **dual-accept** query `t.Token == presented || t.Token == presentedHash` **:1698-1699** (audit `:1698`); mint `Token = Sha256Hex(NewSecret())` **:1714**; `new IdentityToken` site **:1714-1720** (`ExpiresAt = now.AddDays(verificationOptions.Value.TtlDays)` `:1720`); `Sha256Hex` helper **:1754**; `AccountController.cs` `Verify` GET **:578** (signs in at `:590`), `Verify` POST **:601** (signs in at `:638`), `ConfirmAndSignInAsync` **:685** | unchanged |
| L4 | Low | `Security/MarkdownRenderer.cs` `IsSafeUrl` **:397** (scheme whitelist `:405`); link render site `if (IsSafeUrl(target))` **:299**. **mailto already allowed** (`:405`) — only backslash/control-char hardening remains | path was `Markdown/` in audit → **`Security/MarkdownRenderer.cs`** (file moved) |
| L5 | Low | `Dockerfile` (55 lines): `FROM` runtime **:22**, **no `USER`** (runs as root), curl/wget installed for healthcheck `:24-29`; `.github/workflows/ci.yml` (178 lines): actions `checkout@v5` `:44/110`, `setup-dotnet@v5` `:52`, `setup-node@v5` `:117`, **no top-level `permissions:`** | unchanged (note: actions already use `@v5`, not `@v4` the audit/cheatsheet names) |
| L6 | Low | `docker-compose.yml`: Postgres port `**5433:5432 :15**` (not `5432`), app port `:33`, `ASPNETCORE_ENVIRONMENT: Development` `:42`, weak-by-design creds `:52` | drifted (audit implied `5432` host port → actual **5433**) |
| L7 | Low | `Program.cs` HSTS `:964` (default MaxAge); `appsettings.json` `AllowedHosts: *` `:12`; `WellKnownController.cs` `origin = $"{Request.Scheme}://{Request.Host}"` `:80` | unchanged |
| L8 | Low | `Program.cs` login policy + **sample-account exemption** `:589-608` (`SampleDataSeeder.SampleAccountEmails.Contains(postedEmail)` → `GetNoLimiter("sample-exempt")` `:606-608`); **break-glass compare = n/a** (parameterised SQL, see 2.1 note); log-forging *parked* | drifted (audit `:586-595` → `:589-608`) |

**Pinned controller paths (U11/U12/U15 rely on these exact route strings):**
- escalation-inbound → `src/Kumunita.Web/Controllers/EscalationInboundController.cs`, `POST /escalations/inbound` (route attr `"inbound"` at `:67`; auth = `Authorization: Bearer <token>`).
- mail-suppress → `src/Kumunita.Web/Controllers/MailSuppressionController.cs`, `POST /api/mail/suppress` (route attr at `:66`; auth = `X-Suppression-Token` header).
- **The five upload sites** (L2): `AttachmentController.cs:100/108`, `ContentImageController.cs:190/197`, `ProfileController.cs:463/470`, `DocumentController.cs:413` (New) & `:646` (edit re-upload, Put `:652`).
- **Enrolment routes (H2 allow-list):** `SecuritySettingsController.cs` — `GET /settings/security/mfa` `:268`, `POST /settings/security/mfa` `:351` (`MfaEnable`), `GET /settings/security/mfa/recovery` `:405`, `POST /settings/security/mfa/recovery` `:436`, `POST /settings/security/mfa/disable` `:481`; plus logout `POST /account/logout` (AccountController) and static `/lib`, `/css`, `/js`, `/favicon.ico`.

## 2.2 Fix shapes (per unit)

**Open vetoes → decisions (restate of Assumptions, now authoritative):**
1. **H1 import policy:** `GroupMembership`/`GroupInvitation`/`GroupJoinRequest` → always `Rejected("access-granting row")`. `Group` importable only when `OwnerId == actor` **and** `Id` unused. `Profile` only when `SubjectId == actor`. `Conversation` allowed only when actor is `ParticipantA` or `ParticipantB`. `Message` allowed only when its `ConversationId` ∈ `actorConversationIds` (caller-supplied). All other `Entries` types: every `OwnershipFields` value must `== actorSubjectId`; if `existingOwnerOf(row.Id)` returns a non-null owner `≠ actor` → `Rejected("id collision")`.
2. **H2 gate mechanism:** server-side **middleware** (not a pending cookie). Allow-list = enrolment routes above + logout + static. Sample-data accounts exempt (via `IIdentityService.IsSampleDataAccountAsync`).
3. **H3 trusted proxies:** from config `ReverseProxy:KnownNetworks` (CIDR) + `ReverseProxy:KnownProxies` (IPs); default = loopback + RFC1918 (the Docker network); unset/empty **never** means trust-all.
4. **M4 health:** anonymous + `Health__Token` unset → **minimal payload**; full payload needs the token **or** GlobalAdmin.
5. **M5 stamp:** a cookie with no stamp is rejected once `Security:RevocationStamp:RequiredAfter` (UTC date) has passed; default = deploy date + 14 days (operator-set; do **not** commit a real production date — leave unset in repo, document in COOLIFY.md at U30).

**U4 — `ImportOwnershipGuard`** (new `src/Kumunita.Core/Portability/ImportOwnershipGuard.cs`, pure):
```csharp
public sealed record ImportRowVerdict(bool Allowed, string? RejectionReason);
public static class ImportOwnershipGuard
{
    public static ImportRowVerdict Check(
        string type, object row, string actorSubjectId,
        Func<string, string?> existingOwnerOf,
        IReadOnlySet<string>? actorConversationIds = null);
}
```
Reflection over the row's ownership fields via `UserScopeInventory.Entries[type].OwnershipFields` (string field names) read by a small property-name lookup (or a `IReadOnlyDictionary<string,object>` the caller passes). Reason strings (become user-visible codes — U6 records them): `"access-granting row"`, `"id collision"`, `"not-owned"`, `"conversation-not-actor"` (Message), `"type-not-in-scope"`.

**U5 — wire into `ClassifyAsync`** (`UserPortabilityService.cs:714+`): before a row is marked importable, run `ImportOwnershipGuard.Check`; on `Rejected` set the entity's `UserPortabilityEntityStatus`. **Add a `Rejected` member to the enum at `UserPortabilityService.cs:107`** (append after `Conflict`; do **not** renumber `Clean/Duplicate/Conflict`). Expose `RejectedReason` on the per-entity plan shape (U6 renders it).

**U6 — enforce in `ResolveAsync`** (`UserPortabilityService.cs` around `:1060-1150`, both `session.Store(row)` at `:1100`/`:1141`): re-run the guard immediately before **each** store; on `Rejected` skip + append `import.rejected:<reason>` to the result message list + count. Review view **only if** U5's note says it must show rejected rows.

**U7 — `AdminTwoFactorMiddleware`** (new `src/Kumunita.Web/Security/AdminTwoFactorMiddleware.cs`):
```csharp
public sealed class AdminTwoFactorMiddleware
{
    public AdminTwoFactorMiddleware(
        RequestDelegate next,
        IIdentityService identity,      // IsSampleDataAccountAsync (IIdentityService.cs:392)
        UserManager<Microsoft.AspNetCore.Identity.IdentityUser> userManager); // GetTwoFactorEnabledAsync
    public Task InvokeAsync(HttpContext http);
}
```
Read role from the `Kumunita.Role` claim (`ThinPrincipal.Role` const, `ClaimShaping.cs:36`). GlobalAdmin + `!TwoFactorEnabled` + not sample → allow only the pinned enrolment/logout/static prefixes, else `302 → /settings/security/mfa`. Non-admins and enrolled admins pass through.

**U8 — register + reconcile:** `Program.cs` — one `app.UseMiddleware<AdminTwoFactorMiddleware>()` **after** `UseAuthentication`, **before** `UseAuthorization`. `AccountController.cs:907` — update the comment so the redirect is documented as a *convenience*, the middleware as the *control*. `docs/SECURITY.md` — name the middleware as the 2FA control.

**U9 — `ForwardedHeadersSetup`** (new `src/Kumunita.Web/Security/ForwardedHeadersSetup.cs`):
```csharp
public static class ForwardedHeadersSetup
{
    public static void AddKumunitaForwardedHeaders(this IServiceCollection services, IConfiguration config);
}
```
Builds `ForwardedHeadersOptions`: `ForwardedHeaders.XForwardedFor | XForwardedProto`, `ForwardLimit = 1`, trusted networks from `ReverseProxy:KnownNetworks` (CIDR) + `ReverseProxy:KnownProxies` (IPs). Default = loopback + RFC1918. **Throws `InvalidOperationException` on an unparsable CIDR** (fail the boot, not open).

**U10 — wire + Secure + doc:** `Program.cs:961` → replace `app.UseForwardedHeaders()` with the config-driven setup (move the `builder.Services` call to the DI block near `:41`); outside `Development`, set `Cookie.SecurePolicy = CookieSecurePolicy.Always` for the Identity + antiforgery cookies. `docs/COOLIFY.md` — "Verify forwarded headers" checklist (expected `Strict-Transport-Security`, `Set-Cookie …; secure`, two-IP lockout test). `docs/SECURITY.md` §6 — proxy-trust decision. Test **only if** options resolve from a built `ServiceProvider` without booting; else record "no test, because the host cannot be built in unit scope" and rely on U9's tests.

**U11 — clients send tokens:** the seven actions' **Razor forms already call `@Html.AntiForgeryToken()`** (verified at `Directory/Detail.cshtml:70`, `Messages/Index.cshtml:97`, `Posts/Detail.cshtml:587`, `Groups/PostDetail.cshtml:698`, `Community/Manage.cshtml` via `@Url.Action`), and the client fetch helper `client/lib/api.ts` (`apiFetch`) already sets `RequestVerificationToken`. **No JS module fetches any of the seven routes** (grep confirms). **U11's deliverable is therefore likely empty / a no-op**: it verifies the table `action → file → token-mechanism` and records it; it makes **no** edits unless a client is found without a token. If a raw `<form action="…">` is found *without* `@Html.AntiForgeryToken()`, convert to `asp-controller`/`asp-action` or add the token.

**U12 — global filter + coverage test:** `Program.cs:41` `AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))`. Add `[IgnoreAntiforgeryToken]` (+ one-line reason) to **exactly** `EscalationInboundController.cs:67` and `MailSuppressionController.cs:66`. New test `AntiforgeryCoverageTests` (reflection over every unsafe action).

**U13 — `ArchiveLimits`** (new `src/Kumunita.Core/Portability/ArchiveLimits.cs`):
```csharp
public sealed record ArchiveLimits(int MaxEntries, long MaxEntryBytes, long MaxTotalBytes)
{ public static ArchiveLimits Default { get; } = new(10_000, 100L*1024*1024, 500L*1024*1024); }
```
`KumunitaArchive.ReadAsync`/`ReadEntryBytesAsync` (`:327`/`:353`) take an optional `ArchiveLimits?` (default = `Default`); count **real bytes read** (not `entry.Length`); reject duplicate entry names; throw `InvalidDataException`.

**U14 — import media gate + rate limit:** the media block at `UserPortabilityService.cs:1172` must run the same size/quota/type decision as `IUploadGate` (`Security/UploadGate.cs` → `StorageLimits.Decide`). Because the gate is Web-only (ADR 0006-D keeps Core HTTP-free), add the **one new interface in the lane** `IImportMediaPolicy` in Core with a single method, implemented in Web; or pin the seam if the decision already lives in a reachable Core seam. `UserPortabilityController.cs:129` (`import`) and `:253` (`import/resolve`) gain `[EnableRateLimiting("upload")]` + `[RequestSizeLimit(…)]`.

**U15 — anonymous writes:** add `[EnableRateLimiting("report")]` (new policy, one `AddWindow` line in `Program.cs` near `:629-651`) to `HomeController.cs:271` and to the token-auth `EscalationInboundController.cs:67` / `MailSuppressionController.cs:66` (token-auth but still anonymous-write); `[EnableRateLimiting("write")]` on `IssueController.cs:56`. `[MaxLength(4000)]` on `ErrorReportFormModel.Description` (`:29`) and the issue free-text fields. Skip any action that already has a limiter.

**U16 — `/health`:** `HealthController.cs` — anonymous + token-unset → minimal payload (veto 4); replace `string.Equals` (`:73-74`) with `CryptographicOperations.FixedTimeEquals` on UTF-8 bytes (length-guarded); cache the `ISmtpHealthCheck` result ~60 s (small `IMemoryCache`). Update **only** the `HealthControllerTests` methods that assert the old public-by-default (e.g. `Get_When_DatabaseReachable_Returns_200_And_OkShape` `:55` if it asserts the full payload anonymously). `docs/COOLIFY.md` — the setting + new default.

**U17 — stamp every sign-in:** `AccountController.cs` `ConfirmAndSignInAsync` (`:685`, `HttpContext.SignInAsync` at `:719`) and `SecuritySettingsController.cs` `:250` (`HttpContext.SignInAsync`) must attach `AuthenticationProperties.Items["kumunita.stamp"]` = the user's current security stamp (the key the `RevocationStampMiddleware.cs:121` reads). `VerifySignInStampTests`.

**U18 — fail closed:** `RevocationStampMiddleware.cs` read `Security:RevocationStamp:RequiredAfter` (ISO UTC date); if set and `UtcNow` past it, an **empty** stamp is treated as revoked (same response as a stale stamp). Before the date: unchanged. `appsettings.json` key — **leave unset in the repo** (veto 5), document the default in COOLIFY.md at U30. `RevocationStampFailClosedTests`.

**U19 — Markdown link filter:** `Security/MarkdownRenderer.cs:397` `IsSafeUrl` — **reject backslashes and control characters** (currently `/\host` and `\\evil` pass the `!url.Contains(":")` relative branch); keep the `http/https/mailto` whitelist (`:405`, mailto already allowed — *n/a* sub-part). Pin the tests.

**U20 — `UploadSignatureSniffer`** (new `src/Kumunita.Web/Security/UploadSignatureSniffer.cs`, pure):
```csharp
public static bool UploadSignatureSniffer.Matches(string declaredContentType, ReadOnlySpan<byte> head);
public static SniffVerdict UploadSignatureSniffer.Classify(string declaredContentType, ReadOnlySpan<byte> head);
```
`SniffVerdict` is a three-valued enum: `Match` (head bytes match the declared type's known signature), `Mismatch` (head bytes contradict the declared type), `Unknown` (declared type has no known signature, or the head is too short to decide). PNG `89 50 4E 47 0D 0A 1A 0A`; JPEG `FF D8 FF`; GIF `47 49 46 38`; WebP `RIFF....WEBP`; PDF `25 50 44 46`; plain text/markdown by UTF-8 validity (a NUL byte in the first 16 bytes → `Mismatch` for text types); `PK\x03\x04` zip-based office. `Matches` remains the two-valued wrapper: `Classify(...) == Match` → `true`; `Mismatch` or `Unknown` → `false` (fail-closed — preserved for U20's existing tests). Allowed-type source = `MediaOptions.ResolvedAllowedTypes` (`Core/Media/MediaOptions.cs:42`).

**U21 — sniffer wired (attachments + content images):** `AttachmentController.cs:100` and `ContentImageController.cs:190` — before `media.PutAsync` (`:108`/`:197`), read the first 16 bytes and `UploadSignatureSniffer.Classify`; **on `Mismatch`** return the controller's existing 415/validation response (a declared type whose known signature is contradicted by the bytes — e.g. `image/png` declared but `<script>` bytes → reject). **`Unknown` is allowed** (an allowlisted type with no known signature, or a legacy MIME alias carrying a legitimate payload — e.g. `application/x-zip-compressed` with PDF bytes → pass through, preserving the landed `AttachUpload_Support_ZipLegacyAlias_IsAllowed` test). New `UploadSniffAttachmentImageTests` (the two §2.3 pinned names exercise the `Mismatch` rejection path).

**U22 — sniffer wired (avatars + documents):** `ProfileController.cs:463` (Put `:470`) and `DocumentController.cs:413` (New, Put after `:417`) + `:646` (edit re-upload, Put `:652`). Note: dedup-by-hash keeps the **first uploader's** declared type — U30 records whether a follow-up is needed. New `UploadSniffAvatarDocumentTests`.

**U23 — hash-only tokens: n/a (owner decision, 2026-10-31).** The §2.2 "match only `Sha256Hex(presented)`" is incoherent with the **verify-by-Id** lanes (`AccountController.cs:689` `ConfirmAndSignInAsync` and `:626` `VerifyAndSetPasswordAsync` load the row **by `Id`** and present `token.Token` — the stored value). Dropped, `Sha256Hex(stored)` = hash-of-hash matches nothing → every self-serve and child-lane verify link breaks in production. The **setup lane** (`CompleteSeedAdminSetupAsync`) already stores `Sha256Hex(secret)` and validates the raw secret — hash-at-rest holds there. The verify-by-Id lanes use the stored value **by design**: the link carries the `Id`, the row is loaded server-side, and the stored value is never exposed to the browser. The L3 finding ("hash-at-rest accepted as credential") is therefore **satisfied in the setup lane** and **by-design in the verify lane**. U23 exits **n/a**; the L3 confirm-step work (the genuine "GET link signs the browser in" vector) is carried by **U24**. The landed `IdentityServiceTokenHashTests.VerifyToken_ExistingCleartextRowHandled` test (a §2.3-unnamed test that pins the dual-accept for legacy cleartext setup rows) **stays untouched**.

**U24 — verify confirm step:** `AccountController.cs` `Verify` GET (`:578`) stops signing in — it renders a confirm page; `Verify` POST (`:601`) validates + signs in via `ConfirmAndSignInAsync` (`:685`) + `[ValidateAntiForgeryToken]`. New view; add `KnownTranslationKeys` entries by **appending** (never reformat — the `improve-check.ps1` god-file gate pins that file). `VerifyConfirmStepTests`.

**U25 — container/CI:** `Dockerfile:22` add non-root `USER` + `chown` writable paths; drop curl/wget (`:24-29`) only if the `HEALTHCHECK` can use a built-in, else keep and record why. `ci.yml` add top-level `permissions: { contents: read }`; keep the `@v5` action pins (audit's `@v4` is stale). Digest/SHA pinning **only if the network allows** — otherwise list "to pin" in the note, never invent.

**U26 — compose/hosts/headers:** `docker-compose.yml:15` bind published ports to `127.0.0.1` + a "dev only" comment. `appsettings.json:12` `AllowedHosts` stays `*` in-repo; production value documented in COOLIFY.md. `WellKnownController.cs:80` build `origin` from a configured public base URL when set, else `Request.Host`. `Program.cs:964` HSTS `MaxAge` 1 year, `includeSubDomains` **off** (no owner decision to enable), add `Permissions-Policy` with a restrictive default.

**U27 — L8 smalls:** `Program.cs:589-608` remove the `sample-exempt` branch (`:606-608`) so the login limiter partitions on IP only and **does not read the form body**; **break-glass constant-time compare = n/a** (no C# compare — parameterised SQL at `IdentityService.cs:1471`); log-forging stays *parked*. `LoginLimiterPartitionTests`.

**U28 — enumeration (parked):** exits "parked" unless a `## Decision — L1` line exists in the handoff note. Targets: `AccountController.cs:521-524` (signup), `:781-785` (unknown email), `:853-857` (lockout text).

## 2.3 Test names (final — class, project, names)

> Project shorthand: **Core** = `tests/Kumunita.Core.Tests`, **Web** = `tests/Kumunita.Web.Tests`.
> All names are exact xunit method names; the class names below are the pinned ones (U2 fixed them from the seed table).

| Unit | Class (project) | Test names |
|---|---|---|
| U4 | `ImportOwnershipGuardTests` (Core) | `Allows_RowOwnedByActor`, `Rejects_ForgedAuthorId`, `Rejects_ForgedOwnerId`, `Rejects_ForgedUserId`, `Rejects_IdCollisionWithOtherOwner`, `Rejects_AccessGrantingTypes_Membership_Invitation_JoinRequest`, `Group_RequiresActorAsOwner`, `Message_RequiresActorConversation`, `Conversation_ActorMustBeParticipant`, `Rejects_TypeOutsideInventory` |
| U5 | `UserPortabilityHostileClassifyTests` (Core) | `Classify_ForgedAuthor_IsRejected`, `Classify_IdCollision_IsRejected`, `Classify_ForeignMembership_IsRejected`, `Classify_OwnRows_StillClean` |
| U6 | `UserPortabilityHostileResolveTests` (Core) | `Resolve_NeverStoresForgedAuthorRow`, `Resolve_NeverOverwritesForeignDocument`, `Resolve_NeverAddsForeignGroupMembership`, `Resolve_TamperedPlan_StillGuarded`, `Resolve_GenuineRoundTrip_StillImports` |
| U7 | `AdminTwoFactorMiddlewareTests` (Web) | `UnenrolledAdmin_Admin_RedirectsToEnrolment`, `UnenrolledAdmin_AnyOtherRoute_Redirects`, `UnenrolledAdmin_EnrolmentAndLogout_Allowed`, `EnrolledAdmin_PassesThrough`, `NonAdmin_PassesThrough`, `SampleDataAdmin_Exempt`, `AdminWhoDisables2fa_IsGatedOnNextRequest` |
| U9 | `ForwardedHeadersSetupTests` (Web) | `TrustedProxy_ClientIpAndSchemeHonored`, `UntrustedPeer_HeadersIgnored`, `EmptyConfig_DefaultsToPrivateRangesNotTrustAll`, `InvalidCidr_FailsBoot` |
| U11 | *no new test — because* the Razor forms already send `@Html.AntiForgeryToken()` and `api.ts` already sets the header (verified in U2); U11 records the `action → file → token-mechanism` table in its note instead. | — |
| U12 | `AntiforgeryCoverageTests` (Web) | `EveryUnsafeAction_IsCoveredByAntiforgery`, `OnlyTwoActionsIgnoreAntiforgery` |
| U13 | `KumunitaArchiveLimitsTests` (Core) | `Rejects_TotalDecompressedOverLimit`, `Rejects_EntryCountOverLimit`, `Rejects_DuplicateEntryNames`, `NormalArchive_StillReads` |
| U14 | `UserPortabilityMediaGateTests` (Core) | `ImportedMedia_OverSize_IsSkipped`, `ImportedMedia_DisallowedType_IsSkipped`, `ImportedMedia_RespectsQuota` |
| U15 | `AnonymousWriteLimitsTests` (Web) | `AnonymousWrites_HaveRateLimitPolicy`, `ErrorReportDescription_Over4000_IsInvalid`, `ErrorReportDescription_At4000_IsValid` |
| U16 | `HealthControllerTests` (Web, **extend**) | `Health_Anonymous_TokenUnset_ReturnsMinimalPayload`, `Health_ValidToken_ReturnsFullPayload`, `Health_SmtpProbe_IsCached`, `Health_TokenCompare_IsConstantTime` |
| U17 | `VerifySignInStampTests` (Web) | `VerifySignIn_CarriesCurrentStamp` |
| U18 | `RevocationStampFailClosedTests` (Web) | `NoStamp_BeforeCutoff_PassesThrough`, `NoStamp_AfterCutoff_IsRejected`, `FreshStamp_AfterCutoff_Passes`, `Verify_ThenChangePassword_OldSessionRejected` |
| U19 | `MarkdownRendererTests` (Web, `tests/Kumunita.Web.Tests/MarkdownRendererTests.cs`, **extend**) | `Link_BackslashPrefixed_RendersAsText`, `Link_Mailto_IsAllowed` *(n/a — already passes via the `:405` whitelist; keep as a regression pin)*, `Link_JavascriptAndData_StillRejected` |
| U20 | `UploadSignatureSnifferTests` (Web) | `Png_Jpeg_Gif_WebP_Pdf_Accepted`, `DeclaredPng_ButHtmlBytes_Rejected`, `UnknownDeclaredType_Rejected` |
| U21 | `UploadSniffAttachmentImageTests` (Web) | `Attachment_MismatchedBytes_Rejected` *(declared PNG + script bytes → `Mismatch` → 415)*, `ContentImage_MismatchedBytes_Rejected` *(same)*. The landed `AttachUpload_Support_ZipLegacyAlias_IsAllowed` *(declared zip-alias + PDF bytes → `Unknown` → allowed → success)* must stay green — it is the regression pin that the wire is on `Mismatch`, not `Unknown`. |
| U22 | `UploadSniffAvatarDocumentTests` (Web) | `Avatar_MismatchedBytes_Rejected`, `Document_MismatchedBytes_Rejected` |
| U23 | *n/a (owner decision, 2026-10-31)* — the dual-compare is load-bearing in the verify-by-Id lanes; hash-at-rest already holds in the setup lane (already pinned by `IdentityServiceTokenHashTests`); the L3 work is carried by U24. `VerifyToken_ExistingCleartextRowHandled` (not named here) stays. | — |
| U24 | `VerifyConfirmStepTests` (Web) | `VerifyGet_DoesNotSignIn`, `VerifyPost_SignsIn`, `VerifyGet_LeavesTokenUnconsumed` |
| U25 | *no new test — because* this is build/container/CI surface (Dockerfile, `ci.yml`), validated by a YAML parse + `docker build` where available, not a unit test. | — |
| U26 | *no new test — because* this is config/compose/header surface; U19/U9 tests cover the header behaviour and the compose binding is dev-only. Record "no test, because …" in the note. | — |
| U27 | `LoginLimiterPartitionTests` (Web) + a Core constant-time-compare test **only if** the compare were behind an accessible helper — it is **n/a** (no C# compare; parameterised SQL), so **Web class only** | `LoginLimiter_PartitionsOnIpOnly`, `LoginLimiter_NoSampleAccountExemption` |
| U28 | (parked) `AccountEnumerationTests` (Web) — created **only if** `## Decision — L1` exists | `Signup_ExistingEmail_SameResponse`, `Login_UnknownEmail_DoesEqualWork`, `Lockout_Message_IsGeneric` |

U1–U3, U8, U10 (conditionally), U29–U31 add **no** named tests; each says why in its handoff note (U10/U26 conditionally, see 2.2).

## 2.4 Acceptance gate (U29)

Three gate tests, executed once in U29 against the **full** suites (Core + Web) plus the U3 baseline:

1. **Hostile import (S-1):** a hand-built in-memory archive containing (a) a row with a forged `AuthorId`/`OwnerId`, (b) a row whose `Id` collides with a row owned by a *different* principal, and (c) a foreign `GroupMembership` — imports **zero** of those three rows, while the actor's genuine rows (a `Post` and a `Message` in the actor's `Conversation`) still import. (Composed from U4–U6's `*HostileClassify*` / `*HostileResolve*` tests; U29 runs them together and asserts the round-trip still passes.)
2. **Admin without 2FA (S-2):** an authenticated GlobalAdmin principal with `TwoFactorEnabled == false` (not a sample account) is redirected from `/admin` **and** from every non-allow-listed route to `/settings/security/mfa`; the enrolment + logout + static routes remain reachable. (Composed from U7's `AdminTwoFactorMiddlewareTests`.)
3. **Part-vs-whole (S-8):** every pinned test in §2.3 passes **together**, and the full `Kumunita.Core.Tests` + `Kumunita.Web.Tests` suites show **no failure that is not in the U3 baseline** (U3's pre-existing failures are the floor; anything new is a regression and U29 stops, listing it, without fixing).

## 2.5 Drift-guard

**Pause-on-contradiction rule (unit-series rule 6, made concrete):** if a unit's entry reads contradict this Part 2 — a named method/file has moved, a finding is already fixed, a pinned test name is on a different class than §2.3 says, or a line ref has drifted past what a `grep -n` anchor can re-find — the unit **stops**, appends `## U<n> — Drift pause` to the handoff note stating the contradiction verbatim, and does **not** improvise a fix. It records the exact anchor it tried and the actual value found, then waits.

**Anchor method names each unit greps for** (re-run at unit start; if an anchor is gone, that's a drift pause):

| Unit | Grep anchor(s) |
|---|---|
| U4 | `grep -n "Entries\|IsInScope" src/Kumunita.Core/Portability/UserScopeInventory.cs` |
| U5 | `grep -n "enum UserPortabilityEntityStatus\|public async Task<UserPortabilityImportPlan> ClassifyAsync" src/Kumunita.Core/Portability/UserPortabilityService.cs` |
| U6 | `grep -n "session.Store(row)" src/Kumunita.Core/Portability/UserPortabilityService.cs` |
| U7 | `grep -n "IsSampleDataAccountAsync" src/Kumunita.Core/Identity/IIdentityService.cs`; `grep -n "const string Role" src/Kumunita.Core/Identity/ThinPrincipal.cs` |
| U8 | `grep -n "UseAuthentication\|UseAuthorization" src/Kumunita.Web/Program.cs` |
| U9 | `grep -n "UseForwardedHeaders" src/Kumunita.Web/Program.cs` |
| U10 | `grep -n "UseHsts\|SecurePolicy\|AddControllersWithViews" src/Kumunita.Web/Program.cs` |
| U11 | `grep -rn "community/manage\|/messages/open\|/replies\"" src/Kumunita.Web/Views` |
| U12 | `grep -n "HttpPost(\"inbound\")\|HttpPost(\"api/mail/suppress\")" src/Kumunita.Web/Controllers/*.cs` |
| U13 | `grep -n "ReadAsync\|ReadEntryBytesAsync" src/Kumunita.Core/Portability/KumunitaArchive.cs` |
| U14 | `grep -n "mediaStore.PutAsync" src/Kumunita.Core/Portability/UserPortabilityService.cs`; `grep -n "import\|import/resolve" src/Kumunita.Web/Controllers/UserPortabilityController.cs` |
| U15 | `grep -n "HttpPost(\"/Home/Error/Report\")\|HttpPost(\"/issues/new\")" src/Kumunita.Web/Controllers/*.cs` |
| U16 | `grep -n "X-Health-Token\|FixedTimeEquals\|ISmtpHealthCheck" src/Kumunita.Web/Controllers/HealthController.cs` |
| U17 | `grep -n "ConfirmAndSignInAsync\|HttpContext.SignInAsync" src/Kumunita.Web/Controllers/*.cs` |
| U18 | `grep -n "kumunita.stamp\|RequiredAfter" src/Kumunita.Web/Security/RevocationStampMiddleware.cs` |
| U19 | `grep -n "IsSafeUrl" src/Kumunita.Web/Security/MarkdownRenderer.cs` |
| U20 | `grep -n "ResolvedAllowedTypes\|IsAllowed" src/Kumunita.Core/Media/MediaOptions.cs` |
| U21 | `grep -n "IsAttachmentAllowed\|IsAllowed\|media.PutAsync" src/Kumunita.Web/Controllers/AttachmentController.cs src/Kumunita.Web/Controllers/ContentImageController.cs` |
| U22 | `grep -n "IsAllowed\|IsDocumentAllowed\|media.PutAsync" src/Kumunita.Web/Controllers/ProfileController.cs src/Kumunita.Web/Controllers/DocumentController.cs` |
| U23 | `grep -n "t.Token == presented\|Token = Sha256Hex" src/Kumunita.Core/Identity/IdentityService.cs` |
| U24 | `grep -n "public async Task<IActionResult> Verify" src/Kumunita.Web/Controllers/AccountController.cs` |
| U25 | `grep -n "USER\|FROM" Dockerfile`; `grep -n "permissions" .github/workflows/ci.yml` |
| U26 | `grep -n "ports:\|AllowedHosts\|Request.Host" docker-compose.yml src/Kumunita.Web/appsettings.json src/Kumunita.Web/Controllers/WellKnownController.cs` |
| U27 | `grep -n "SampleAccountEmails\|sample-exempt" src/Kumunita.Web/Program.cs` |
| U28 | `grep -n "EmailAlreadyExists\|IsLockedOut\|user is null" src/Kumunita.Web/Controllers/AccountController.cs` |

**No PII** (unit-series rule 7): all fixtures use synthetic ids/emails (`user-a`, `a@example.test`).
