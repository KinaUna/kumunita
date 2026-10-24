// F19.U2 — the readiness gate (pin §1 "Boot + readiness", referenced from
// playwright.config.ts as `globalSetup`).
//
// Polls BOTH the compose app's /health AND Mailpit's /api/v1/info until
// each returns HTTP 200 — or fails the whole run with a named error (a
// wedged boot is a harness PRECONDITION failure, not a test failure).
//
// The compose stack is brought up OUTSIDE this file (the owner/operator
// runs `docker compose up -d` first — the AGENTS.md "Getting a live server"
// doctrine; this suite assumes the stack is already up, not that it starts
// it). This file only checks readiness and fails fast if it never arrives.

const APP_URL = process.env.KUMUNITA_APP_URL ?? 'http://localhost:5080';
const MAILPIT_URL = process.env.KUMUNITA_MAILPIT_URL ?? 'http://localhost:8025';

const BUDGET_MS = 60_000; // pin §1: "~60 s"
const POLL_MS = 1_000;

async function probe(url: string): Promise<boolean> {
  try {
    const res = await fetch(url, { method: 'GET', signal: AbortSignal.timeout(5_000) });
    return res.status === 200;
  } catch {
    return false;
  }
}

async function pollUntil(url: string, label: string, budgetMs: number): Promise<void> {
  const deadline = Date.now() + budgetMs;
  for (;;) {
    if (await probe(url)) return;
    if (Date.now() >= deadline) {
      throw new Error(
        `E2E readiness failed: ${label} (${url}) did not answer HTTP 200 within ` +
        `${budgetMs / 1000} s. This is a harness PRECONDITION failure (a wedged ` +
        `compose boot, or a port not published), NOT a test failure. ` +
        `Bring up the compose stack (docker compose up -d) and re-run.`,
      );
    }
    await new Promise((r) => setTimeout(r, POLL_MS));
  }
}

export default async function globalSetup(): Promise<void> {
  // Both endpoints, in parallel — the budget is shared.
  await Promise.all([
    pollUntil(`${APP_URL}/health`, 'compose app (app service, :5080)', BUDGET_MS),
    pollUntil(`${MAILPIT_URL}/api/v1/info`, 'Mailpit (mailpit service, :8025)', BUDGET_MS),
  ]);
  // The compose app needs ~8 s after container start (Marten schema +
  // Wolverine leadership) — a second settle after the first 200 (the
  // AGENTS.md boot doctrine).
  await new Promise((r) => setTimeout(r, 8_000));
}
