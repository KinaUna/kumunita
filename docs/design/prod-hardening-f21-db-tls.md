# F21 — SRV-04: app↔DB TLS — Part-0 design pin

> **Fix:** F21 (report row **SRV-04**) — kind **`code+ops`**, **dep: —** (no
> code dependency). F21's **code side is a single Web-host env knob** — a config
> read in `Program.cs` that appends `SslMode`/CA/trust to the **one**
> `ConnectionStrings:Kumunita` value both consumers already share. There is **no
> Core seam, no bounded context, no `<PackageReference>`**: Npgsql is already
> the provider behind both Marten and EF Core, and `SslMode` is a plain
> connection-string keyword. The **owner-ops** half (enable Postgres TLS on the
> prod addon + set the env + CA) is recorded in a **later** unit as an **open
> item, never assumed done**.
> **Status:** this is the **Part-0 design pin** (F21.U1). It is **design-only**:
> no code, no build, no `.cs` touched. F21.U2 lands the **env knob in
> `Program.cs`** (≤ 1 file) so the **same image works with/without TLS**.
> F21.U3 is the **owner-ops step** (enable TLS on the prod Postgres addon + set
> `SslMode`/CA + set the env) — recorded as an **open item**, *never assumed
> done*.
> **Why it's a FAIL today:** the Npgsql connection string
> (`Host=db;Port=5432;Database=kumunita;Username=kumunita;Password=____;Include
> Error Detail=true`) carries **no `SslMode`**. Npgsql's default `SslMode=Prefer`
> means: *try* TLS, and if the server does not offer it, **silently fall back to
> plaintext** — no enforcement, no failure. On a network hop that is anything
> other than the same trusted host, the credentials + all domain reads/writes are
> on the wire unencrypted. The audit's FAIL is the absence of **enforced**
> server-side TLS + certificate validation on the app↔DB channel.

---

## 1. Current state (grounded, not assumed)

**The single connection string, the two consumers (both in `Program.cs`):**

- **`Program.cs:92-95` (Marten):**
  `var kumunitaConnection = builder.Configuration.GetConnectionString("Kumunita")
  ?? throw …`, then inside `AddMarten(opts => …)` the line **`opts.Connection(kumunitaConnection);`**.
- **`Program.cs:474` (EF Core — the only EF in the tree, ADR 0004):**
  `builder.Services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(kumunitaConnection));`.

Both consume the **same** `kumunitaConnection` local. Therefore **one** place to
append the TLS keywords (immediately after `kumunitaConnection` is resolved,
before either consumer is registered) covers **both** — no per-consumer
duplication, no way for one to drift from the other. This is the single point
F21.U2 must touch.

**The baseline shape (to extend, not replace):**
`Host=db;Port=5432;Database=kumunita;Username=kumunita;Password=____;Include
Error Detail=true`. **No `SslMode`, no CA/trust keyword today.** The dev
`docker-compose.yml` Postgres (`postgres:18`, `Host=db` on the compose network)
does **not** serve TLS; the prod Postgres addon (Coolify / managed) **can**.
That is exactly the "same image, with/without TLS" split this pin exists for.

**What's absent (the FAIL):** no enforced `SslMode` anywhere; no CA source; no
config knob to switch the behavior between the TLS-serving prod addon and the
TLS-less dev container.

---

## 2. The `SslMode` value (pin item 1) — the exact value + the decision rule

**Pin the knob to carry `SslMode` from config, with this decision rule:**

| Environment | `SslMode` | Meaning |
|---|---|---|
| **Prod (recommended default)** | **`Verify CA`** | Full TLS **and** full server-certificate validation: the server must present a cert signed by the trusted CA (pin item 3) **and** the cert's name must match `Host=`. This is the security property the audit wants. |
| **Prod (documented weaker fallback)** | **`Require`** | TLS is **enforced** (a plaintext fallback is a hard failure), but the server certificate is **not** validated (no CA check, no name check). Acceptable only if the prod network hop is already trusted (private/VPC) **and** the CA cannot be provisioned; it still fixes the "silent plaintext fallback" hole, it does **not** fix impersonation. **Record the reason for downgrading next to the value.** |
| **Dev / local (the status quo, default)** | **absent → `Prefer`** (Npgsql default) | Do **not** set `SslMode` for the compose stack: the `postgres:18` dev container does not serve TLS, and `Prefer` lets the connection fall back to plaintext on the loopback-equivalent compose network. **This is the dev default, not a prod value.** |

**Decision rule (pin this exactly):** `SslMode` is an **optional** config value
read from `ConnectionStrings:Kumunita` (or a sibling key — see §4). **If present
and non-empty, it is appended verbatim to the connection string; if absent, the
string is left exactly as today** (`Prefer`, i.e. the current dev behavior).
**Prod must be set to `Verify CA`**; `Require` is the only sanctioned weaker
value and must carry a written justification; **an unset value in prod is the
FAIL** (it is `Prefer`). The knob is **additive and idempotent** — appending a
keyword to the existing string changes nothing when the value is empty, which is
what keeps the dev image working unchanged.

**Why `Verify CA` and not just `Require`:** `Require` still lets a MITM on an
untrusted hop present *any* self-signed cert and succeed. `Verify CA` is the only
value that validates the server's identity, which is the property the audit's
"app↔DB TLS" row is actually checking. `Require` is retained **only** as a
recorded fallback for a locked-down private network where CA provisioning is not
yet possible.

---

## 3. Where the CA is provided (pin item 2) — the CA-source question + tradeoff

`Verify CA` needs a CA the client trusts. Npgsql/Postgres has exactly two sane
ways; pin **one**:

- **Option A — CA bundle path (RECOMMENDED, pin this):** the owner supplies a
  CA bundle (the root/intermediate that signed the prod addon's server cert)
  and the connection string carries **`Root Certificate=/<path>/ca.crt`** (Npgsql
  keyword) so `Verify CA` validates against that specific CA. This is the full
  security property: trust is anchored to a specific CA the owner controls, not
  to "whatever the server says." The owner provisions the bundle once and mounts
  it into the app container (it is a **file on the app volume**, not a secret —
  a CA is public by design; the *private* key stays on the Postgres side).
- **Option B — `Trust Server Certificate=true` (recorded fallback, NOT the pin):**
  Npgsql keyword `Trust Server Certificate=true` tells the client to **accept
  and trust the first cert the server presents**, skipping CA + name
  validation. It is functionally weaker than `Verify CA` (it validates that TLS
  is on, but not *whose* cert it is — closer to `Require` + a local CA pin).
  Record it as the documented fallback **only if** the owner cannot distribute
  a bundle into the app container; **it must not be the default.**

**Pinned choice: Option A (`Root Certificate=` CA bundle path).** Rationale:
it is the only option that gives the full "verify the server's identity"
property the audit wants, it reuses a public artifact (no secret handling on the
app side), and it is the standard Postgres client pattern. Option B is recorded
so the owner *can* fall back without re-opening this pin, but the **pin is A** —
F21.U2's knob must read a **CA path**, and the prod value must be `Verify CA` +
that path. `Trust Server Certificate` is a **dev/escape-hatch** value, never the
prod recommendation.

**Tradeoff recorded (owner's call, pin this framing):** CA-bundle validation
(Option A) costs the owner one provisioning step (distribute the bundle into the
app container + set the path) and a re-provision when the addon's CA rotates.
`Trust Server Certificate` (Option B) costs nothing but gives up identity
verification. **The pin chooses the cost for the property.**

---

## 4. The env-knob shape (pin item 3) — F21.U2 will add this to `Program.cs`

**Single knob, single point, both consumers.** F21.U2 modifies **only**
`Program.cs`, in the **one** block immediately after `kumunitaConnection` is
resolved (between `Program.cs:95`'s resolution and the two consumer
registrations), so both Marten (`opts.Connection`) and EF Core (`UseNpgsql`)
receive the identical extended string:

- **Read an optional value** from config — pin the source key as
  **`ConnectionStrings:Kumunita` itself** (the owner appends the keywords to the
  string they already manage — **no new key, no new section, no new options
  class**). Concretely: the owner's prod connection string becomes
  `…;Include Error Detail=true;SslMode=Verify CA;Root Certificate=/etc/ssl/.../ca.crt`,
  and the dev string stays exactly as it is today (no `SslMode`, no CA).
- **Guard against double-append:** because the knob is *append-to-the-existing
  string*, F21.U2 must **not** unconditionally append a second `SslMode=…` if the
  owner already placed it in the string. Pin the implementation rule: **the value
  comes from the owner's connection string, so the app does not append it at all
  in the "extend, not replace" case.** If F21.U2 prefers a *sibling* key (e.g.
  `DbTls:SslMode` + `DbTls:CaPath`) that it appends when non-empty, that is also
  acceptable — **pin: pick ONE of the two shapes and state it** (recommended:
  the sibling-key shape, because it keeps the owner's `ConnectionStrings:Kumunita`
  value untouched in the repo and centralizes the append in one guarded block):
  read `builder.Configuration["DbTls:SslMode"]` +
  `builder.Configuration["DbTls:CaPath"]`; if **either** is non-empty, append
  `;SslMode=<value>` and (when a CA path is set) `;Root Certificate=<path>` to
  `kumunitaConnection` **once**, before either consumer registers. **Either way,
  the net effect is one extended string consumed by both Marten and EF Core.**
- **The "same image works with/without TLS" property:** when neither knob is set
  (the dev/compose case), `kumunitaConnection` is **byte-for-byte unchanged** —
  `Prefer`, no CA — so the dev container boots exactly as it does today. When the
  knobs are set (prod), both consumers enforce `Verify CA` + CA. **No code branch,
  no new DI, no new type in `Kumunita.Core`** — the Core-stays-HTTP-free and
  ADR 0006 invariants hold because this is a Web-host config read, not a new
  bounded context or Core seam.

**What F21.U2 does NOT do (pin the boundary):** no new `<PackageReference>`
(Npgsql is already the provider for both consumers); no new file; no change to
`Kumunita.Core`; no `.slnx` change; no migration. It is **≤ 1 file** (`Program.cs`),
a guarded append of at most two keywords to the one shared connection string.

---

## 5. The pinned test name (pin item 4)

**`DbTls_SslModeAppliedFromEnvKnob`** — a **connection-string shape test**.

Shape (pin exactly; this is what F21.U3's test — if the owner wants one in the
Web harness — asserts): build a `Program`/host-config scenario with the F21 knobs
set (`SslMode=Verify CA` + a CA path, or the sibling-key equivalents), resolve the
connection string that **both** consumers would receive, and assert:
(1) it **contains `SslMode=Verify CA`**, (2) it **contains the CA keyword**
(`Root Certificate=` with the pinned path, per the §3 Option A pin), and
(3) a **negative** case — with the knobs **unset** (the dev shape), the resolved
string is **byte-for-byte the baseline** (`…;Include Error Detail=true`, **no
`SslMode`, no CA keyword**) proving the "same image works without TLS" property.
The test asserts **string shape** (the knobs land in the shared string both
consumers read), **not** a live TLS handshake — a real `Verify CA` handshake
against the prod addon is the **owner-ops** open item (F21.U3), *never* assumed
done, and is not runnable in the unit-test harness (no prod CA, no prod host).

**Pin the exact name:** `DbTls_SslModeAppliedFromEnvKnob` (do not rename).

---

## 6. Invariants held (this pin + the unit it drives)

- **ADR 0004 forward-only + "app role owns the DB"** — **preserved, not touched.**
  F21 changes **how** the app connects (TLS), **not** who owns the DB, **not**
  the schema, **not** the migration path. F22 (the role demotion) is the unit that
  touches the ownership question; F21 does not.
- **ADR 0006 module boundaries + Core stays HTTP-free** — **held.** The knob is a
  **Web `Program.cs` config read**, not a new bounded context, not a Core seam,
  not a new type in `Kumunita.Core`. The Core consumers (Marten) receive the
  extended string through the **existing** `opts.Connection(…)` call — no Core
  surface is added or reshaped.
- **The baseline `ConnectionStrings:Kumunita` shape** — **extended, not replaced.**
  When the knobs are unset, the string is byte-for-byte the baseline (the dev
  image is unchanged). When set, two keywords are appended; nothing existing is
  removed or re-ordered.
- **C3 single-write-lane / server-rendered stack** — **untouched** (F21 adds no
  write lane, no route, no view; it is a config keyword on an existing connection).

---

## 7. Why the code side is a config knob, not a feature

There is **no mechanism to build** — Npgsql already speaks TLS; `SslMode` +
`Root Certificate` are **connection-string keywords**, not features. The audit's
FAIL is that the **value is not enforced** (default `Prefer` → silent plaintext
fallback) and that **one image** has to serve both a TLS-less dev container and a
TLS-serving prod addon. The code side is therefore **one guarded append of the
owner-supplied value to the one shared connection string** (F21.U2, `Program.cs`,
≤ 1 file), and the security property itself is **delivered by the owner** setting
that value + the CA on the prod addon (F21.U3, an **open item, never assumed
done**). This mirrors the F4 "no code needed / the code side is the runbook"
discipline, with the difference that F21's code side **is** a small env knob
(the app reads a value the owner sets) rather than an empty seam.

---

## 8. Open items (owner — recorded, NOT assumed done)

1. **Enable Postgres TLS on the prod Postgres addon** (managed-DB TLS or a
   self-managed `postgres:18` with a cert) so the server actually *serves* TLS —
   a client-side `SslMode=Verify CA` against a server that does not offer TLS is
   a **hard connection failure**, so this must be true **before** the prod value
   is flipped from `Prefer` to `Verify CA`. **(F21.U3.)**
2. **Provision + distribute the CA bundle** (pin §3 Option A) into the prod app
   container and set the CA path in the prod config; **and set** the prod
   `SslMode=Verify CA` (pin §2). If the owner cannot distribute a bundle, record
   the justification for the `Require` fallback (pin §2) **or** the
   `Trust Server Certificate` escape (pin §3 Option B) — **do not leave prod at
   `Prefer`.**
3. **Re-provision on CA rotation** — when the prod addon's server CA rotates, the
   distributed bundle + `Root Certificate=` path must be updated or the
   `Verify CA` handshake fails. (Recorded as the standing cost of pin §3 Option A.)

**None of these is assumed done.** F21.U3 records them as the owner's remaining
steps before SRV-04 can be re-verified as PASS; the code agent does **not** run
them.

---

## 9. Exit contract + forward pointer

**F21.U1 is design-only:** no code, no build, no `.cs` touched, no
`<PackageReference>`, no migration, no `.slnx` change, no test run. The pin
exists and pins the **four** items: (1) the `SslMode` value + decision rule
(**`Verify CA`** recommended prod default, **`Require`** documented weaker
fallback, **absent/`Prefer`** the dev default — §2); (2) the CA/trust-source
choice + tradeoff (**`Root Certificate=` CA bundle** pinned, `Trust Server
Certificate` recorded fallback — §3); (3) the **env-knob shape** covering
**both** the Marten (`Program.cs:95`) and EF Core (`Program.cs:474`) consumers —
the same image works with/without TLS (§4); (4) the **exact pinned test name**
`DbTls_SslModeAppliedFromEnvKnob` (§5). The **owner-ops open items** are
recorded, **not assumed done** (§8). The invariants are held (§6).

**F21 is now at U2.** The prod-hardening lane stays **open** — **F21.U2 + F21.U3 +
F22 remain**. **No 6-member close-flip** (that is **F22**, the lane's last unit).
Next unit = **F21.U2** (add the env knob — `SslMode` + CA/trust — to
`Program.cs`; **≤ 1 file, no C# feature, no `<PackageReference>`, no `.slnx`
change, no build**; the net effect is one guarded append to the single shared
`kumunitaConnection` so **both** consumers get it) — a fresh clean-slate agent
reads this entry + **§4** (the exact knob shape + the single-point, both-consumers
contract) + **§2 / §3** (the values the knob may carry) + **§7** (why the code
side is a knob, not a feature) + `Program.cs` **~84–95** and **~474** (the two
consumers + the single shared `kumunitaConnection`).
