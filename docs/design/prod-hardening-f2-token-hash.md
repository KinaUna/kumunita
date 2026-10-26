# F2 — AUTH-02: hash auth tokens at rest (Part-0 design pin)

**Fix:** F2 (report row **AUTH-02** — "hash tokens at rest"), severity bucket **data
exposure**. **Kind:** `code`.
**Source plan:** `docs/plans-milestones/in-progress/prod-hardening/plan-prod-hardening.md` § F2.
**Author unit:** **F2.U1** (design-only — this doc pins the design; **no code, no build**).
**Implementing unit:** **F2.U2** (hash on write + compare on read, `IdentityService.cs`
only — see §8 for the one **scope note** that extends U2 by a single file for the setup mint).

This is **Tier 1** (a design pin a later ~32K clean-slate agent re-reads verbatim). It is
self-contained: F2.U2 must not need to re-derive any of these decisions.

---

## 1. The problem (grounded)

`IdentityService` stores single-use auth tokens in **cleartext** at rest. A leaked DB
must not expose usable tokens.

- **Write (verify mint):** `NewVerifyToken` — `src/Kumunita.Core/Identity/IdentityService.cs:1514`,
  sets `Token = NewSecret()` at **`:1519`**. `NewSecret()` (`:1544`) is a 32-byte
  `RandomNumberGenerator` fill, **base64** — stored as-is. Call sites: **`:152`**
  (`RegisterCoreAsync`) and **`:216`** (`ResendVerificationEmailAsync`).
- **Read (all three call sites):** `FindTokenAsync` — **`:1501`**; the comparison is
  `.Where(t => t.Token == tokenValue && t.Kind == kind)` (first `.Where`, `:1506`).
  Call sites:
  - `:256` — `VerifyWithTokenAsync` (self-serve verify lane)
  - `:310` — `VerifyAndSetPasswordAsync` (child / ADR 0146 lane)
  - `:1251` — `CompleteSeedAdminSetupAsync` (seed-admin setup lane)
- **Write (setup mint):** `FirstBootSeeder.cs:271` region — the `KindSetup` row sets
  `Token = token` (the **operator-held env token** `SeedAdmin__Token`), `FirstBootSeeder.cs:~274`.
- **Doc shape:** `src/Kumunita.Core/Identity/IdentityToken.cs` — `public string Token`
  ("the high-entropy secret value"), `KindVerify`/`KindSetup` consts, `IsUsableAt`.
- **Excluded from the value compare:** `IsFirstBootSetupCompleteAsync` (`~:1290`) only
  checks `KindSetup` row **existence** (`ConsumedAt == null && ExpiresAt > now`); it never
  reads `Token`. **Unaffected** by this change — do not touch it.

> **`SHA256` is not a key-stretching KDF** (no salt/iterations). That is *correct* here:
> tokens are **high-entropy secrets** (32 random bytes), not user-chosen passwords, so a
> salted-KDF buy (memory/CPU) is unnecessary. We need **one-way-ness** (a DB leak must not
> yield the token), which SHA-256 provides. This is *not* the F6 password-hashing lane
> (argon2id) — do not conflate.

---

## 2. The hash

- **Algorithm:** **SHA-256** over the **UTF-8** bytes of the secret.
- **Encoding:** **hex, 64 lowercase** characters (`Convert.ToHexString` is uppercase;
  normalize with `.ToLowerInvariant()`). A valid at-rest value is **exactly 64
  lowercase-hex chars** — this property is what the backward-compat rule in §6 keys on.
- **Helper name:** **`Sha256Hex`** (one static, **≤ ~10 LOC**). Pinned shape (U2 owns
  the implementation; this is the contract):

```csharp
// ≤ 10 LOC. Pure function of `value`; no allocation surprises.
private static string Sha256Hex(string value)
{
    var bytes = System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(value));
    return Convert.ToHexString(bytes).ToLowerInvariant();   // exactly 64 lowercase hex chars
}
```

- **Location (pin, see §8 scope note):** for the **verify** path it is a `private static`
  on `IdentityService` (U2 is `IdentityService.cs`-only). The **setup** mint lives in
  `FirstBootSeeder.cs` (a *different* file, outside U2's scope as written in the plan) —
  so `Sha256Hex` must be **reachable from both**. Pin it as a small **`internal static`
  helper in the `Kumunita.Core.Identity` namespace** (either a new `TokenHash` static
  class or an `internal static` member on `IdentityService`) so `FirstBootSeeder` can call
  the **same** implementation. **Do not** duplicate the hashing in two files.

---

## 3. Where it is stored — **no new column**

**Reuse the existing `IdentityToken.Token` column.** After this fix the column holds the
**hex digest** (`Sha256Hex(secret)`) instead of the cleartext secret. **No schema change,
no migration, no new doc field.** `IsUsableAt`, `ConsumedAt`, `ExpiresAt`, `Id`, `Kind`,
`UserId`, `Attempt` are all untouched.

Consequence: the *presented* value and the *stored* value are no longer the same string —
which is exactly why the read path must be pinned carefully (§4). The **`Id`** remains the
stable row handle and the **only** thing that travels in a verify URL (§6.2: "the
high-entropy secret is never in a link").

---

## 4. Write path

### 4a. Verify mint — `IdentityService.NewVerifyToken` (`:1514`, U2 scope)

- **Before:** `Token = NewSecret()` (`:1519`).
- **After:** `Token = Sha256Hex(NewSecret())`.
- The raw `NewSecret()` is a **throwaway**: it is generated, hashed, and the raw is
  **discarded** (the verify URL carries `token.Id`, not the token — `VerificationLink`
  at `:~1533`). No code retains the raw verify secret after minting. **This is the
  strongest form** — even a DB read yields only the hash.
- The two call sites (`:152`, `:216`) are **unchanged** — they call `NewVerifyToken`,
  whose body changes; no caller edits.

### 4b. Setup mint — `FirstBootSeeder.cs:~271–274` (**§8 scope note**)

- **Before:** `Token = token` (the env `SeedAdmin__Token`, as-is).
- **After:** `Token = Sha256Hex(token)` (the **same** `Sha256Hex` helper, §2).
- The **raw** setup token is what the operator holds: it appears in the email body
  (`SeedAdminBody(email, user.Id, token)`, `FirstBootSeeder.cs:6916`) and in env, and the
  operator types it into the `/admin/setup` form (`model.SetupToken`). We store only its
  hash; the operator is instructed to remove `SeedAdmin__*` from env after first use
  (OPS §2) — that guidance is **unchanged**.

---

## 5. Read path — **the load-bearing part (dual-mode), pin this exactly**

**Drift the plan did not state (recorded, not guessed):** the three `FindTokenAsync`
call sites do **not** all present a raw secret. Tracing the Web layer:

| Call site (`IdentityService.cs`) | Lane | What the **Web** passes in | Value class |
|---|---|---|---|
| `:256` `VerifyWithTokenAsync` | self-serve verify | `token.Token` — the **stored column value** (row loaded by `Id` via `AccountController.LoadUsableVerifyTokenAsync`, `session.LoadAsync<IdentityToken>(id)`) | **stored** |
| `:310` `VerifyAndSetPasswordAsync` | child (ADR 0146) | `token.Token` — the **stored column value** (same `LoadAsync`-by-`Id` path) | **stored** |
| `:1251` `CompleteSeedAdminSetupAsync` | seed-admin setup | `model.SetupToken` — the **raw** env token the operator typed | **raw** |

**Therefore the blanket "compare `Sha256Hex(tokenValue)`" in the plan is wrong for the
verify lane:** the verify lane hands back the *already-stored value*, so comparing
`stored == Sha256Hex(presented)` would be `H == Sha256Hex(H)` → **false → verify broken**.
The Web **cannot** present a raw verify secret (it never has one — the raw was discarded
at mint and only the `Id` is in the URL), so no Web change can fix this. The correct,
**implementable-in-Core** rule is a **dual comparison** that accepts **either** the
stored value **or** its hash:

### Pinned `FindTokenAsync` rewrite (U2 implements)

```csharp
private async Task<IdentityToken?> FindTokenAsync(string tokenValue, string kind, string? subjectId)
{
    await using var session = documentStore.OpenSession(new SessionOptions());
    var now = DateTimeOffset.UtcNow;

    // §5 — DUAL match. The verify lanes present the STORED value; the setup lane
    // presents the RAW secret. Match either so both work against a hashed column.
    // CRITICAL: `Sha256Hex` is a .NET call — it must be computed as a LOCAL CONSTANT
    // BEFORE the query. Marten cannot translate SHA256.HashData into SQL. Do NOT put
    // Sha256Hex(...) inside the .Where(...) expression.
    var presented = tokenValue;
    var presentedHash = Sha256Hex(presented);   // local, precomputed — translatable as a constant

    var query = session
        .Query<IdentityToken>()
        .Where(t => t.Kind == kind
            && (t.Token == presented || t.Token == presentedHash)   // stored-value OR raw-secret
            && t.ConsumedAt == null
            && t.ExpiresAt > now);
    if (subjectId is not null)
        query = query.Where(t => t.UserId == subjectId);
    return await query.FirstOrDefaultAsync().ConfigureAwait(false);
}
```

**Why this is correct for all four (row × lane) combinations:**

| Stored value | Presented | Clause that matches | Lane works? |
|---|---|---|---|
| `H` (new hashed) | `H` | `t.Token == presented` | ✅ verify |
| `H` (new hashed) | raw | `t.Token == presentedHash` (H == H) | ✅ setup |
| `raw` (legacy cleartext) | `raw` | `t.Token == presented` | ✅ verify |
| `raw` (legacy cleartext) | `raw` | `t.Token == presented` | ✅ setup (see §6) |

**No false-positive widening:** an attacker would need to *know* the token value to match
it (clause 1) — that is the same as "knows the token"; the `Id` (verify) / `email`+single-use
(setup) still gates actual use. SHA-256 collision resistance prevents `Sha256Hex(presented)`
from landing on an unrelated row's stored hash.

**Marten/LINQ note (implementation gotcha for U2):** `presentedHash` **must** be a captured
local (a constant) — the `||` of two `==`-constants transposes to `(Token = ? OR Token = ?)`.
Writing `Sha256Hex(...)` *inside* the expression will fail to translate. The repo's Marten
memory note about `OrderBy*` vs `.OrderDescending` does **not** apply here (no ordering), but
the "translate only what SQL can express" principle is the same family — keep the hash
client-side.

---

## 6. Backward-compat rule — **chosen decision (load-bearing)**

**Chosen rule (pin): *Accept-legacy-via-dual-compare.*** A stored value that is **not** 64
lowercase-hex (a pre-upgrade cleartext row) is **accepted** on read *in the lane that
presents it as-is* (clause 1 above), and is **never** silently used in a lane that presents
the raw (clause 2 only fires when the stored value equals the hash of the presented raw —
i.e. only if the operator presents the genuine token).

**Why the plan's *recommended* rule — "treat a non-64-hex stored value as stale/invalid and
**reject**" — is NOT chosen here (record the tension honestly):**
1. **It is not implementable in the pinned scope without breaking the verify lane.** The
   verify lane presents the **stored value**, so a "reject unless stored is 64-hex + compare
   `Sha256Hex(presented)`" rule rejects the very valid live verify rows (stored is the value
   the Web handed back). To reject you'd need a predicate Marten can't express cleanly
   (`IsHex64(t.Token)`), and to *fix* the verify lane you'd need the Web to present a raw
   secret it does not have — forcing a **new column** (forbidden, §3) or a **Web change**
   (Core stays HTTP-free; out of U2 scope).
2. **For the setup lane it would lock the operator out** of completing first-boot setup on
   any pre-upgrade instance that still has a live `KindSetup` token — a functional regression
   for the one credential the operator is holding *right now*.

**Residual risk under the chosen rule (bounded, and record it):**
- **Verify tokens:** the *only* rows the rule accepts that it wouldn't under "reject" are
  **pre-upgrade, un-consumed, unexpired** cleartext verify tokens. These are **single-use**
  and **self-expire within `Verification__TtlDays` (default 14)** of their mint — so the
  weaker-posture window closes on its own within ≤14 days of the upgrade, with no action.
- **Setup tokens:** the rule accepts the **genuinely live** first-boot token (correct — the
  operator must be able to complete setup). In the steady state there is no "leaked but
  should-be-dead" setup token to wrongly accept, because a completed setup has **no** live
  `KindSetup` row and an in-flight one is *supposed* to be usable.
- **Net:** every **newly-minted** token (the actual AUTH-02 goal — "a DB leak must not
  expose a usable token") is **hashed at rest**; only a small, **ephemeral** pre-upgrade set
  remains cleartext and self-limits. If the owner wants zero residual, the follow-up is a
  one-time re-mint (new hashed rows + invalidate old) — **out of scope for F2** (would need
  a migration/owner step). **Record this as an open item in the handoff note, do not assume
  the owner wants it.**

`IsFirstBootSetupCompleteAsync` is **excluded** (existence-only, no `Token` read — §1) and
must not be changed.

---

## 7. Pinned test names (F2.U4 implements)

These three are the contract. Each must hold **after** U2/U3; assert against a real
Postgres `IdentityToken` row (mirror the `PostgresFixture` seam-test shape in
`tests/Kumunita.Core.Tests/`).

1. **`VerifyToken_StoredHashedNotCleartext`** — mint a `KindVerify` token via
   `NewVerifyToken`; load the `IdentityToken` row; assert `Token` is **exactly 64 lowercase
   hex** (`^[0-9a-f]{64}$`) and **not** equal to the (inaccessible) raw base64 — i.e. the
   at-rest value is the digest. Also assert the **setup** mint path stores 64-hex (assert
   `Sha256Hex` is what a known input maps to: `Sha256Hex("x")` is stable/64-hex).
2. **`VerifyToken_ConsumedOrExpiredStillRejected`** — a row with `ConsumedAt != null` **or**
   `ExpiresAt < now` is **not** returned by `FindTokenAsync` even when the presented value
   matches (both the stored-value and raw forms). Proves the dual-compare did not weaken the
   consumed/expired gates.
3. **`VerifyToken_ExistingCleartextRowHandled`** — plant a **legacy cleartext** row (store a
   known raw base64 in `Token`, `KindSetup`), present that same raw; assert the setup lane
   **accepts** it (clause 1) **per the §6 chosen rule** — i.e. this test *documents* the
   accept-legacy behavior, not a reject. (If the owner later flips §6 to "reject," this test
   flips to assert rejection — keep it the single source of truth for the compat decision.)

**Driving note for U4:** `FindTokenAsync` is `private`. The tests drive it through the
public seams — `VerifyWithTokenAsync` (stored-value lane), `CompleteSeedAdminSetupAsync`
(raw lane) — or, if a `PostgresFixture` seam exposes the session, by calling the query shape
directly. Do **not** make `FindTokenAsync` public to test it; keep the public surface as-is.

---

## 8. Drift / scope notes (record for the owner + F2.U2 — do not silently resolve)

1. **Dual-mode read path (the big one).** The plan's F2 pin assumed `FindTokenAsync` always
   receives a raw secret. It does not — the verify lanes receive the **stored value** (Web
   loads the row by `Id` and passes `token.Token`). The §5 dual-compare is the **only**
   implementable-in-Core rule that keeps both lanes working with **no new column** and **no
   Web change**. U2 must implement §5 verbatim (including the precompute-`presentedHash`
   gotcha).
2. **Setup mint is in a second file.** The plan scoped F2.U2 to "`IdentityService.cs` only,"
   but the **setup** write path (`FirstBootSeeder.cs:~274`) is where the `KindSetup` row's
   `Token` is set. To hash *that* at rest, U2 must **also** touch `FirstBootSeeder.cs` (one
   line: `Token = token` → `Token = Sha256Hex(token)`) and make `Sha256Hex` `internal` so
   both files share it (§2). **This extends U2's deliverables by one file.** Flag to the
   owner: leaving the setup mint cleartext defeats half of AUTH-02 (the setup token is the
   one an operator actually holds). If the owner insists U2 stay strictly one file, the setup
   mint becomes a **separate follow-up unit** — record it as an **open item**, do not drop it.
3. **"The raw goes in the URL" framing (plan § write path).** Correct only for the **setup**
   lane (raw token in email/env, typed in the form). For the **verify** lane the **`Id`** —
   not the raw — is in the URL, and the raw is a discarded throwaway. Pinned accurately in §4a.

---

## 9. Exit (what must hold before U2 is considered ready)

- This file exists and pins: the hash (§2), the storage choice / **no new column** (§3), the
  **write path for both mints** (§4), the **dual-mode read path** with the exact
  `FindTokenAsync` rewrite + the precompute gotcha (§5), the **chosen backward-compat rule**
  + the tension with the plan's recommendation + the bounded residual (§6), and the **three
  test names** (§7).
- **No `.cs` file is modified by this unit** (design-only). No build is run.
- The `## F2.U1 — Part-0 design pin` section is **appended** to
  `hardening-handoff-notes.md` (the hash choice, the §6 compat rule, the §8 scope notes,
  and "Next unit = **F2.U2**") — **written before stopping**.
- **F3 is NOT started** — it is the next clean-slate agent after F2 completes.
