# ADR 0172 — Password-hash posture: PBKDF2 (Identity default), no custom scheme

Status: Accepted
Date: 2026-10-25

> **History (this is a *confirmation* ADR, not a new decision):** the prod-
> hardening lane (the 2026-10-22 production-readiness audit) carried a **FAIL
> row AUTH-09** — "argon2id/bcrypt password hash." The lane's `## Summary`
> table closed that row by claiming F6 was "superseded by the ASP.NET Identity
> 10.x built-in hashing posture; the F2 SHA-256 token-hash lane carries the
> 'secret-at-rest' control instead." On a 2026-10-25 code-vs-docs verification
> pass that justification was found to be **unsound**: the F2 lane hashes
> *verify/setup tokens* (`IdentityToken.Token`), not passwords — no
> `AddPasswordHasher`, no BCrypt/Argon2 anywhere in `src/`, so the password
> hash is still whatever the .NET 10 ASP.NET Identity default is (PBKDF2-HMAC-
> SHA256). A one-way token hash does not substitute for the password-hash
> control the row names. This ADR records the **correct** disposition of the
> row, grounded in the platform's own authoritative security doc.

## Context

The password hash in this platform is set by one line in
`src/Kumunita.Web/Program.cs` (the `AddIdentity<User, IdentityRole>` block):

```csharp
opts.Password.RequiredLength = 8;
opts.Password.RequireNonAlphanumeric = false;  // the setup-token is a credential, not a password
```

Nothing here registers a custom `PasswordHasher<T>` and nothing pins an
iteration count — so the hash is the ASP.NET Identity default: **PBKDF2
(HMAC-SHA256) at the framework's default work factor** (100 000 iterations in
current releases). This is the same KDF the platform's own
`docs/SECURITY.md` already names, twice, as the **decided** control:

- `SECURITY.md` §5 control table —
  *"PBKDF2 password hashing (Identity default; iteration count documented) |
  A2, A3 | §6 (decided)"*.
- `SECURITY.md` §6 "Password storage (decided 2026-08-27; protects A2/A3)":
  *"ASP.NET Identity hashes passwords with PBKDF2 (HMAC-SHA256); the
  iteration count is configurable via `IdentityOptions` and we document the
  default rather than silently depending on it. **No custom password scheme**
  — Identity's format, reset flow, and lockout are the ones we test and
  operate (OPS.md §10)."*

So the row is not an unmet gap the lane skipped — it is a preference the
platform **already consciously chose against**, three weeks before the audit,
with a documented rationale (operate Identity's tested format + reset + lockout
rather than maintain a second, bespoke scheme). The only defect in the first
pass was the *wording* of the close, not the posture.

## Decision

1. **The password-hash posture is PBKDF2 (HMAC-SHA256) at the ASP.NET
   Identity default work factor, with no custom `PasswordHasher`.** This ADR
   confirms it as the accepted T1 posture for this platform. The AUTH-09 row
   is **satisfied by policy**, not by adding argon2id/bcrypt.
2. **The F6 row is closed by this ADR, not by the F2 token-hash lane.** The
   `## Summary` table's "superseded by … the F2 SHA-256 token-hash lane"
   sentence is corrected to point at this ADR. (The F2 lane is a *different*
   control — high-entropy one-time tokens at rest — and does not carry the
   password-hash row.)
3. **Document the work factor rather than silently depending on it**
   (`SECURITY.md` §6): the default is the .NET 10 ASP.NET Identity default
   (100 000 iterations). This is recorded in `SECURITY.md` (already done) and
   here; if a future release changes the default, the `RequiredLength` /
   hasher block in `Program.cs` is the single seam to re-check.
4. **Revisit trigger (not a defect today):** if the platform later runs a
   *shared* VPS hosting multiple neighborhoods (the `SECURITY.md` §1
   deployment assumption revisit), or if a credible offline-DB-exposure event
   occurs, re-evaluate a stronger KDF (argon2id) *at that seam*. At one
   neighborhood per instance, a self-hosted box, and a deny-by-default
   privacy model, the incremental gain over the tested Identity path is not
   the risk being carried — the box and the backup are.

## Consequences

- **No code change.** `Program.cs` is untouched by this ADR; there is no new
  package, no migration, no hasher class. The password path (register, verify,
  reset, lockout) is byte-identical to the pre-ADR state.
- **One line of doc correction.** The lane's `## Summary` table (and any
  close-flip echo that repeats it) names **ADR 0172** as the AUTH-09
  disposition, replacing the "superseded by the F2 token-hash lane" claim.
- **The "secret-at-rest" audit control is still met** — by the F2 token-hash
  lane (verify/setup tokens) *and* by this PBKDF2 posture (passwords). They
  are two rows, two controls; this ADR keeps them honestly separated.
- **Consistency with the philosophy:** this is the "integrate the boundary,
  don't bolt on a component" move — the decision already lived in
  `SECURITY.md`; the ADR is the record that makes the audit row and that
  decision agree, instead of letting the row dangle as a false "done."
