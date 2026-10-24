// F19.U2 — the E2E harness (pin §4 U2).
//
// This config is the "separate concern from xunit" pin (§1/§5 invariant #2):
// a Playwright project with its own runner, its own package.json, its own
// TypeScript. NOT in Kumunita.slnx, NOT invoked by `dotnet test`, NOT a
// dependency of the .NET build. The F14 CI gate's landed shape (two
// `dotnet exec` dlls) is untouched — the E2E job is added in U4 (open item),
// never reshaped.
//
// Readiness gate (pin §1): a `globalSetup` polls BOTH the compose app and
// Mailpit on their real published ports (:5080 app, :8025 Mailpit — the
// committed `docker-compose.yml` mapping; :5099 is a DIFFERENT instance per
// repo memory and is NOT the suite's evidence). A wedged boot fails the run
// with a named error, before any spec runs — a harness precondition failure,
// not a test failure.

import { defineConfig } from '@playwright/test';

export default defineConfig({
  // The base URL is the compose app's published port. Overridable via env
  // (pin §1's env-var table) so the suite runs against a non-default
  // mapping without a code change.
  use: {
    baseURL: process.env.KUMUNITA_APP_URL ?? 'http://localhost:5080',
    // Playwright's own timeouts (the suite's per-spec budget is the
    // `timeout` below; the Mailpit poller has its own bounded window).
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },
  // One project — the compose app on :5080, driven by one browser. The
  // suite never needs more.
  projects: [
    {
      name: 'chromium',
      use: { browserName: 'chromium' },
    },
  ],
  // The readiness gate runs before any spec. The suite is single-threaded
  // (the login lockout doctrine — §1/§5 invariant #6 — and the Mailpit
  // extraction both assume one login per run; `workers: 1` enforces that
  // and keeps the run-unique email + Title/Body extraction unambiguous).
  workers: 1,
  fullyParallel: false,
  retries: 0,
  reporter: [['list']],
  testDir: 'specs',
  // The pin's spec names use the `.e2e.ts` suffix (pin §3:
  // `signup_verify_login.e2e.ts`, `post_write.e2e.ts`) — Playwright's
  // default `testMatch` (`*.spec.ts` / `*.test.ts`) would not pick them up.
  testMatch: /.*\.e2e\.ts$/,
  timeout: 90_000, // per spec — the Mailpit 30 s poll + the ~8 s boot + the write leg fit.
  // Readiness gate (pin §1 "Boot + readiness"): `global-setup.ts` polls
  // BOTH :5080/health AND :8025/api/v1/info until HTTP 200, or fails the
  // run with a named error (a wedged boot is a harness PRECONDITION
  // failure, not a test failure). The compose stack is brought up OUTSIDE
  // the suite (the owner/operator runs `docker compose up -d` first — the
  // AGENTS.md "Getting a live server" doctrine); the gate only checks
  // readiness and fails fast if it never arrives.
  globalSetup: require.resolve('./global-setup'),
});
