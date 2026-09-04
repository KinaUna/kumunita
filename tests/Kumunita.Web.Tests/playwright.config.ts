import { defineConfig, devices } from '@playwright/test';

// M3 Playwright runtime — the config that lets `e2e-m2.spec.ts` be
// enumerated and (eventually) executed.
//
// `testDir` is this folder; the spec lives right here alongside the
// xUnit C# project (the csproj's `<None Include>` comment documents
// the coexistence). `testMatch` is a narrow glob so the xUnit `.cs`
// and `.config` files are never picked up.
//
// `baseURL` is the ASP.NET Core development-server default on this
// machine. Override by editing `use.baseURL` below (or set
// `PW_BASE_URL` and have Playwright read it via the env var the
// config picks up).

export default defineConfig({
  testDir: '.',
  testMatch: ['**/*.spec.ts'],
  // The M2 spec is a single serial file (no parallelism benefit —
  // the kumunita fixture shares the same browser context per test).
  fullyParallel: false,
  workers: 1,
  retries: 0,
  // U13's contract: the kumunita fixture throws a descriptive Error
  // until the M3 runtime supplies the implementation. Surface that
  // message in the test output so the failure is self-documenting.
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:5199',
    trace: 'retain-on-failure',
    // The Razor views are plain server-rendered HTML; no extra
    // storageState needed for the fixture-less M1/M2 surface.
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    // `dotnet run` with the default Development launch profile.
    // `cwd` is two levels up (repo root) so we pick up the
    // .slnx and the Kumunita.Web project.
    command: 'dotnet run --project src/Kumunita.Web --no-build',
    cwd: '../..',
    url: 'http://localhost:5199',
    reuseExistingServer: true,
    timeout: 60_000,
  },
});
