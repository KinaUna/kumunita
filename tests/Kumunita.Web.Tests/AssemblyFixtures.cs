// ADR 0307 §D2 — one shared postgres:18 container per test *assembly* instead
// of one per test class. The PostgresFixture (IAsyncLifetime) is Core.Tests'
// (linked from this csproj, not copied — one fact, one place); it is registered
// here so every Web test class receives the same server instance, while each
// test method still gets a fresh scratch database via NewDatabaseAsync, so
// parallel classes don't clobber each other's catalog.
using Kumunita.Core.Tests;

[assembly: AssemblyFixture(typeof(PostgresFixture))]
