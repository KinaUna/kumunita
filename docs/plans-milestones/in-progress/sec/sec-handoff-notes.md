# SEC — rolling handoff notes

Appended per unit; never rewritten. A unit reads only its own entry list plus
the last 1–2 sections it names as `Needs`.

## U1 — design Part 1
- Wrote `docs/design/sec-remediation-design.md` Part 1: Context, Scope, Invariants, Findings register. No code, no build.
- Invariants S-1…S-8 pinned (S-1 import ownership; S-2 admin 2FA gate; S-3 trusted proxy; S-4 anti-forgery; S-5 anon-write limits; S-6 stamp fail-closed; S-7 link/file-type validation; S-8 named regression test).
- Findings register: **16 rows** (H1–H3, M1–M5, L1–L8) + the two parked sub-items (L1/U28, L8c log-forging) = 16+2. All file:line marked *re-verify in U2*.
- U2 next: re-verify register line refs, then append Part 2 (fix shapes, config keys, signatures, test names, §2.4 acceptance gate, §2.5 drift-guard).
