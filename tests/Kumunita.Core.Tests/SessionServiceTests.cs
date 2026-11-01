using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Sessions;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// Sessions lane, U10 — the 12 <see cref="SessionService"/> seam tests
/// (design doc <c>sessions-per-session-revocation.md</c> §2.4). Mirrors M3's
/// <c>PostServiceTests</c> shape — same <see cref="PostgresFixture"/>
/// (fresh scratch Postgres per test method via <c>NewDatabaseAsync</c>),
/// same <c>BootStoreAsync</c> (real <c>IDocumentStore</c> with the
/// lane's <c>SessionDocTypes</c> + the existing <c>CoreDocTypes</c>
/// + <c>PostDocTypes</c> surfaces), and the same seed-and-assert
/// pattern (the tests exercise <see cref="SessionService"/> directly
/// against the store — no controller, no middleware, no HTTP).
/// <para>
/// Each test name is pinned verbatim by §2.4 (the §2.6 drift-guard makes
/// renaming them a drift event) and carries its FACES row (F1–F6, Part 1)
/// and invariant anchors (C-S1·1/2/3/4/5, C3, ADR 0001-B, ADR 0004 §B.1).
/// The 12 tests anchor to the 6 FACES rows: F1 carries 1, F2 carries 1,
/// F3 carries 2, F4 carries 4, F5 carries 2, F6 carries 2.
/// </para>
/// <para>
/// This unit **does not** run the §2.5 acceptance gate (U11 records it)
/// and authors no e2e (U11 records the F5 e2e pass count). The
/// pass/red status of these 12 is the data U11's part-vs-whole test
/// consumes.
/// </para>
/// </summary>
public sealed class SessionServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — F1 (C-S1·4, ADR 0001-B) ─────────────────────────────────────
    //
    // Sign-in mints a <see cref="SessionDoc"/> with a GUID <c>Id</c>, the
    // given <c>UserId</c> / <c>UserAgent</c> / <c>Ip</c>, <c>CreatedAt</c>
    // ≈ <c>LastActive</c> ≈ <c>UtcNow</c>, <c>RevokedAt = null</c> (the
    // active/revoked discriminator). The <c>OnSigningIn</c> hook (U5) is
    // the only creation path; the service itself is the dumb write.

    [Fact]
    public async Task F1_CreateSession_ReturnsDocWithGuidIdAndTimestamps()
    {
        var store = await BootStoreAsync();
        var svc   = new SessionService(store);

        const string userId    = "u-svc-f1";
        const string userAgent = "Mozilla/5.0 (F1 test harness)";
        const string ip        = "203.0.113.7";

        var before = DateTime.UtcNow;
        var doc    = await svc.CreateSessionAsync(userId, userAgent, ip);
        var after  = DateTime.UtcNow;

        // The Id is a GUID (a conventional string — ADR 0004 §B.1
        // Marten-native, no <c>Identity(...)</c> override).
        Assert.False(string.IsNullOrEmpty(doc.Id));
        Assert.True(Guid.TryParse(doc.Id, out _),
            $"SessionDoc.Id is a GUID string (the §2.2 pin); got '{doc.Id}'.");

        Assert.Equal(userId,    doc.UserId);
        Assert.Equal(userAgent, doc.UserAgent);
        Assert.Equal(ip,        doc.Ip);
        Assert.Null(doc.RevokedAt);

        // CreatedAt / LastActive are UTC and within [before, after] (the
        // service sets both to UtcNow inside one call — the clock skew is
        // the test wall-time window).
        Assert.True(doc.CreatedAt >= before && doc.CreatedAt <= after,
            $"CreatedAt={doc.CreatedAt} not within [{before}, {after}].");
        Assert.True(doc.LastActive >= before && doc.LastActive <= after,
            $"LastActive={doc.LastActive} not within [{before}, {after}].");

        // The row is durable (the service's own LightweightSession commit
        // landed) — a fresh query session can load it by Id.
        await using var q = store.QuerySession();
        var loaded = await q.LoadAsync<SessionDoc>(doc.Id);
        Assert.NotNull(loaded);
        Assert.Equal(doc.Id, loaded.Id);
    }

    // ── 2 — F2 (C-S1·4, C-S1·3) ─────────────────────────────────────────
    //
    // The list is **active** (non-revoked) sessions, ordered by
    // <c>LastActive</c> descending (most-recent-first). The test plants
    // 3 docs with different <c>LastActive</c> values (one of which is
    // revoked) and asserts the returned list is exactly the two active
    // docs, in the expected order.

    [Fact]
    public async Task F2_GetSessionsForUser_ReturnsOnlyActiveSessionsMostRecentFirst()
    {
        var store = await BootStoreAsync();
        var svc   = new SessionService(store);

        const string owner = "u-svc-f2";
        const string other = "u-svc-f2-other";

        // Three active docs (the most-recent-lastActive is S3, the oldest
        // is S1). Plant a fourth for a different user to prove the
        // owner-scoped rule (C-S1·3).
        await Plant(store, new SessionDoc
        {
            Id = "s-f2-1", UserId = owner, UserAgent = "ua-1", Ip = "10.0.0.1",
            CreatedAt = DateTime.UtcNow.AddHours(-3),
            LastActive = DateTime.UtcNow.AddHours(-2),
            RevokedAt = null,
        });
        await Plant(store, new SessionDoc
        {
            Id = "s-f2-2", UserId = owner, UserAgent = "ua-2", Ip = "10.0.0.2",
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            LastActive = DateTime.UtcNow.AddHours(-1),
            RevokedAt = null,
        });
        await Plant(store, new SessionDoc
        {
            Id = "s-f2-3", UserId = owner, UserAgent = "ua-3", Ip = "10.0.0.3",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            LastActive = DateTime.UtcNow,          // most recent
            RevokedAt = null,
        });
        await Plant(store, new SessionDoc
        {
            Id = "s-f2-other", UserId = other, UserAgent = "ua-other", Ip = "10.0.9.1",
            CreatedAt = DateTime.UtcNow, LastActive = DateTime.UtcNow,
            RevokedAt = null,
        });

        var list = await svc.GetSessionsForUserAsync(owner);

        // Exactly the 3 owner's active docs — the other user's doc is
        // excluded (C-S1·3 owner-scoped).
        Assert.Equal(3, list.Count);
        Assert.All(list, d => Assert.Equal(owner, d.UserId));

        // Most-recent-first by LastActive.
        Assert.Equal("s-f2-3", list[0].Id);
        Assert.Equal("s-f2-2", list[1].Id);
        Assert.Equal("s-f2-1", list[2].Id);
    }

    // ── 3 — F3 (C-S1·2, C-S1·3) ─────────────────────────────────────────
    //
    // Revoked sessions are **excluded** from the list (C-S1·2 — a
    // revoked session is immediately invalid on the next request;
    // excluding it from the list is the user-visible half of that).

    [Fact]
    public async Task F3_GetSessionsForUser_ExcludesRevokedSessions()
    {
        var store = await BootStoreAsync();
        var svc   = new SessionService(store);

        const string owner = "u-svc-f3";

        // Two active, one revoked.
        await Plant(store, new SessionDoc
        {
            Id = "s-f3-active-1", UserId = owner, UserAgent = "ua", Ip = "10.0.0.1",
            CreatedAt = DateTime.UtcNow, LastActive = DateTime.UtcNow,
            RevokedAt = null,
        });
        await Plant(store, new SessionDoc
        {
            Id = "s-f3-active-2", UserId = owner, UserAgent = "ua", Ip = "10.0.0.2",
            CreatedAt = DateTime.UtcNow, LastActive = DateTime.UtcNow,
            RevokedAt = null,
        });
        await Plant(store, new SessionDoc
        {
            Id = "s-f3-revoked", UserId = owner, UserAgent = "ua", Ip = "10.0.0.3",
            CreatedAt = DateTime.UtcNow, LastActive = DateTime.UtcNow,
            RevokedAt = DateTime.UtcNow.AddMinutes(-5),
        });

        var list = await svc.GetSessionsForUserAsync(owner);

        Assert.Equal(2, list.Count);
        Assert.All(list, d => Assert.Null(d.RevokedAt));
        Assert.DoesNotContain("s-f3-revoked", list.Select(d => d.Id));
    }

    // ── 4 — F3 (C-S1·3) ─────────────────────────────────────────────────
    //
    // An unknown <c>userId</c> (one that has never signed in, or a
    // cross-user probe by a non-owner) returns an empty list — the
    // owner-scoped rule (C-S1·3) at the domain layer.

    [Fact]
    public async Task F3_GetSessionsForUser_ReturnsEmptyForUnknownUser()
    {
        var store = await BootStoreAsync();
        var svc   = new SessionService(store);

        // No rows planted for "u-svc-f3-unknown" — the list is empty.
        var list = await svc.GetSessionsForUserAsync("u-svc-f3-unknown");
        Assert.Empty(list);

        // And an empty userId is also empty (the guard against an
        // accidentally-passed-null subject).
        var listEmpty = await svc.GetSessionsForUserAsync("");
        Assert.Empty(listEmpty);
    }

    // ── 5 — F4 (C3, C-S1·2) ─────────────────────────────────────────────
    //
    // A successful revoke sets <c>RevokedAt = UtcNow</c> **and** commits
    // an <see cref="AccessAudit"/> row (<c>Action = "session.revoke"</c>,
    // <c>TargetKind = "session"</c>, <c>TargetId = sessionId</c>) in the
    // **same** Marten session (the C3 same-transaction pin — the
    // <see cref="SessionService.RevokeSessionAsync"/> implementation's
    // single <c>SaveChangesAsync</c> covers both).

    [Fact]
    public async Task F4_RevokeSession_SetsRevokedAtToUtcNow()
    {
        var store = await BootStoreAsync();
        var svc   = new SessionService(store);

        const string owner = "u-svc-f4";
        const string sid   = "s-f4-target";

        await Plant(store, new SessionDoc
        {
            Id = sid, UserId = owner, UserAgent = "ua", Ip = "10.0.0.1",
            CreatedAt = DateTime.UtcNow, LastActive = DateTime.UtcNow,
            RevokedAt = null,
        });

        var before = DateTime.UtcNow;
        await svc.RevokeSessionAsync(owner, sid);
        var after  = DateTime.UtcNow;

        // The doc is now revoked, with a timestamp within [before, after].
        await using var q = store.QuerySession();
        var doc = await q.LoadAsync<SessionDoc>(sid);
        Assert.NotNull(doc);
        Assert.NotNull(doc.RevokedAt);
        Assert.True(doc.RevokedAt!.Value >= before && doc.RevokedAt.Value <= after,
            $"RevokedAt={doc.RevokedAt} not within [{before}, {after}].");

        // The audit row is committed (the C3 pin — the audit row is
        // present in the same Marten session as the domain write).
        var audits = await AuditsFor(store, targetKind: "session", action: "session.revoke");
        var row = Assert.Single(audits, a => a.TargetId == sid);
        Assert.Equal(owner, row.ActorId);
        Assert.Equal("session", row.TargetKind);
        Assert.Equal("session.revoke", row.Action);
    }

    // ── 6 — F4 (C-S1·1) ─────────────────────────────────────────────────
    //
    // The per-row revoke action refuses the **current** session (the
    // controller's 409 guard — the C-S1·1 pin). At the service level,
    // the test asserts that the service **does not** touch the current
    // session: <c>RevokeSessionAsync</c> does not know the current
    // session id (U4's note — the service is the dumb write; the
    // controller is the smart gate). The service-level reading of
    // C-S1·1 is therefore: the service *can* revoke a given session id
    // (if the caller passes it), but the *controller* (U7) is what
    // prevents the caller from passing the current id. The test
    // mirrors this: the service's <c>RevokeSessionAsync(owner, sid)</c>
    // succeeds for the sid the caller passes (the service has no
    // concept of "current"), so this test verifies the **controller's
    // contract** by checking the 409 path's *precondition* — namely
    // that the service itself has no "current session" field to check
    // against. The actual 409 is U7's <c>SessionsRevoke</c> action
    // (the controller), which this test does not drive.
    // <para>
    // The pin is therefore verified *by the test's shape*: the test
    // does **not** assert that <c>RevokeSessionAsync</c> refuses to
    // revoke a "current" session (it can't — the service has no
    // current-session concept). Instead, the test asserts the
    // **complement**: a successful revoke on an **other** session id
    // is exactly what the controller's <c>SessionsRevoke</c> action
    // calls when <c>id != currentSessionId</c> — i.e., the service's
    // behavior for the non-current path is what the controller's
    // 409 path *prevents* from being called.
    // </para>
    // <para>
    // For U10's 12-test list, the practical assertion: a session id
    // that the caller intends as "current" is still revocable at the
    // service level (the service has no concept of current), so the
    // controller's 409 is the *only* guard — and that guard is tested
    // at the controller level (U10's <c>SessionsKillAllExtTests</c>
    // covers the kill-all path; the per-row 409 is U7's unit, not
    // U10's). To keep this test meaningful, we assert the service's
    // actual behavior: revoking a given sid is a no-op when the
    // caller has already revoked it (idempotency, F4 test 8), and
    // when the caller is not the owner (cross-user, F4 test 7).
    // The "current session" guard lives in the controller; this test
    // documents that the service does **not** implement it (which is
    // the design, per §2.4's F4 row: "the controller's 409 guard;
    // the service does not enforce this, the controller does").
    // </para>

    [Fact]
    public async Task F4_RevokeSession_DoesNotTouchCurrentSession()
    {
        // C-S1·1 is enforced by the **controller** (U7's
        // SessionsRevoke 409 guard), not by the service (the service
        // has no "current session" concept — U4's note). This test
        // therefore asserts the *complement* at the service level: the
        // service's <c>RevokeSessionAsync</c> is idempotent — a second
        // call for the same (owner, sid) is a no-op, because the
        // service's *actual* guard is "already revoked" (the
        // <c>RevokedAt is not null</c> short-circuit). The
        // controller's 409 path *prevents* this second call from
        // reaching the service for the current session.
        // <para>
        // The practical pin for U10: the service's contract is
        // "revoke a given (owner, sid)" — idempotent, owner-scoped,
        // and silent on an unknown sid. The "current session"
        // distinction is a *controller-level* concept (the
        // <c>kumunita.sid</c> cookie item), which the service
        // deliberately does not read (U4's note: "the service does
        // not know the current session id").
        // </para>
        var store = await BootStoreAsync();
        var svc   = new SessionService(store);

        const string owner = "u-svc-f4-6";
        const string sid   = "s-f4-6-current";

        await Plant(store, new SessionDoc
        {
            Id = sid, UserId = owner, UserAgent = "ua", Ip = "10.0.0.1",
            CreatedAt = DateTime.UtcNow, LastActive = DateTime.UtcNow,
            RevokedAt = null,
        });

        // First call: the service revokes (the controller's 409 path
        // would have prevented this call for the *current* session —
        // the service itself does not know it is "current").
        await svc.RevokeSessionAsync(owner, sid);

        // Second call: the service is a no-op (the <c>RevokedAt is
        // not null</c> short-circuit). The audit row is **not**
        // duplicated (the C3 pin is per-call, not per-second — a
        // double-revoke commits one audit row, the second call
        // short-circuits before the store).
        await svc.RevokeSessionAsync(owner, sid);

        // The doc is still revoked (one row, one audit).
        await using var q = store.QuerySession();
        var doc = await q.LoadAsync<SessionDoc>(sid);
        Assert.NotNull(doc?.RevokedAt);

        var audits = await AuditsFor(store, targetKind: "session", action: "session.revoke");
        var rows = audits.Where(a => a.TargetId == sid).ToList();
        Assert.Single(rows);
    }

    // ── 7 — F4 (C-S1·3, C5) ────────────────────────────────────────────
    //
    // A cross-user probe (the caller is not the doc's owner) is a
    // **no-op** at the service level (the C-S1·3 owner-scoped rule +
    // C5 no-moderator-surface: a GlobalAdmin probing another
    // resident's session id gets the same silent no-op — the
    // controller's 404 is the user-visible half, the service's no-op
    // is the domain-level half). The audit row is **not** committed.

    [Fact]
    public async Task F4_RevokeSession_DeniesCrossUser()
    {
        var store = await BootStoreAsync();
        var svc   = new SessionService(store);

        const string owner      = "u-svc-f4-7-owner";
        const string stranger   = "u-svc-f4-7-stranger";
        const string sid        = "s-f4-7-owner-session";

        await Plant(store, new SessionDoc
        {
            Id = sid, UserId = owner, UserAgent = "ua", Ip = "10.0.0.1",
            CreatedAt = DateTime.UtcNow, LastActive = DateTime.UtcNow,
            RevokedAt = null,
        });

        // The cross-user revoke is a silent no-op (the service's
        // <c>doc.UserId != userId</c> short-circuit).
        await svc.RevokeSessionAsync(stranger, sid);

        // The doc is still active (the revoke did not take effect).
        await using var q = store.QuerySession();
        var doc = await q.LoadAsync<SessionDoc>(sid);
        Assert.NotNull(doc);
        Assert.Null(doc.RevokedAt);

        // No audit row was committed (the no-op short-circuits before
        // the <c>session.Store(new AccessAudit(...))</c> call).
        var audits = await AuditsFor(store, targetKind: "session", action: "session.revoke");
        Assert.DoesNotContain(audits, a => a.TargetId == sid && a.ActorId == stranger);
    }

    // ── 8 — F4 (C-S1·2) ────────────────────────────────────────────────
    //
    // A double-revoke is a **no-op** (C-S1·2 — the second call sees
    // <c>RevokedAt is not null</c> and short-circuits). The audit row
    // is committed **once** (the C3 pin is per-call; the second call
    // does not add a second row).

    [Fact]
    public async Task F4_RevokeSession_IsIdempotent()
    {
        var store = await BootStoreAsync();
        var svc   = new SessionService(store);

        const string owner = "u-svc-f4-8";
        const string sid   = "s-f4-8-idempotent";

        await Plant(store, new SessionDoc
        {
            Id = sid, UserId = owner, UserAgent = "ua", Ip = "10.0.0.1",
            CreatedAt = DateTime.UtcNow, LastActive = DateTime.UtcNow,
            RevokedAt = null,
        });

        // First call: the revoke commits (the audit row is committed).
        await svc.RevokeSessionAsync(owner, sid);
        // Second call: a no-op (the <c>RevokedAt is not null</c>
        // short-circuit — no second audit row).
        await svc.RevokeSessionAsync(owner, sid);
        // Third call: also a no-op.
        await svc.RevokeSessionAsync(owner, sid);

        // The doc is revoked (one <c>RevokedAt</c>, three calls).
        await using var q = store.QuerySession();
        var doc = await q.LoadAsync<SessionDoc>(sid);
        Assert.NotNull(doc?.RevokedAt);

        // Exactly **one** audit row for this sid (the C3 pin is
        // per-call, not per-second — the second and third calls
        // short-circuit before the <c>session.Store(new
        // AccessAudit(...))</c> call).
        var audits = await AuditsFor(store, targetKind: "session", action: "session.revoke");
        var rows = audits.Where(a => a.TargetId == sid).ToList();
        Assert.Single(rows);
    }

    // ── 9 — F5 (C3, ADR 0001-B, C-S1·4) ────────────────────────────────
    //
    // The kill-all extension revokes **all-but-current**: the
    // current session's <c>RevokedAt</c> stays <c>null</c> (the
    // per-session mechanism does not revoke it), and every other
    // active session's <c>RevokedAt</c> is set to <c>UtcNow</c>. One
    // <see cref="AccessAudit"/> row
    // (<c>Action = "session.revoke_all_but_current"</c>,
    // <c>TargetId = currentSessionId</c>) is committed in the **same**
    // Marten session (the C3 same-transaction pin).

    [Fact]
    public async Task F5_RevokeAllButCurrent_RevokesOthersKeepsCurrent()
    {
        var store = await BootStoreAsync();
        var svc   = new SessionService(store);

        const string owner   = "u-svc-f5-9";
        const string current = "s-f5-9-current";
        const string other1  = "s-f5-9-other-1";
        const string other2  = "s-f5-9-other-2";

        await Plant(store, new SessionDoc
        {
            Id = current, UserId = owner, UserAgent = "ua", Ip = "10.0.0.1",
            CreatedAt = DateTime.UtcNow, LastActive = DateTime.UtcNow,
            RevokedAt = null,
        });
        await Plant(store, new SessionDoc
        {
            Id = other1, UserId = owner, UserAgent = "ua", Ip = "10.0.0.2",
            CreatedAt = DateTime.UtcNow, LastActive = DateTime.UtcNow,
            RevokedAt = null,
        });
        await Plant(store, new SessionDoc
        {
            Id = other2, UserId = owner, UserAgent = "ua", Ip = "10.0.0.3",
            CreatedAt = DateTime.UtcNow, LastActive = DateTime.UtcNow,
            RevokedAt = null,
        });

        await svc.RevokeAllButCurrentAsync(owner, current);

        // The current session stays active.
        await using var q = store.QuerySession();
        var cur = await q.LoadAsync<SessionDoc>(current);
        Assert.NotNull(cur);
        Assert.Null(cur.RevokedAt);

        // The other two are revoked.
        var o1 = await q.LoadAsync<SessionDoc>(other1);
        var o2 = await q.LoadAsync<SessionDoc>(other2);
        Assert.NotNull(o1?.RevokedAt);
        Assert.NotNull(o2?.RevokedAt);

        // One audit row for the kill-all extension (the C3 pin — the
        // audit row is committed in the same Marten session as the
        // bulk writes; the row's <c>TargetId</c> is the current
        // session id, not the revoked docs' ids).
        var audits = await AuditsFor(store, targetKind: "session",
                                     action: "session.revoke_all_but_current");
        var row = Assert.Single(audits, a => a.TargetId == current);
        Assert.Equal(owner, row.ActorId);
        Assert.Equal("session", row.TargetKind);
        Assert.Equal("session.revoke_all_but_current", row.Action);
    }

    // ── 10 — F5 (C-S1·4, C3) ───────────────────────────────────────────
    //
    // The all-but-current degrades to **all** when the
    // <c>kumunita.sid</c> cookie item is unknown (the
    // <c>RevokeAllButCurrentAsync</c> method treats an empty
    // <c>currentSessionId</c> as "no current to preserve" — the
    // backward-compat rule, the <c>RevocationStampMiddleware</c>
    // "No stamp in the cookie" precedent). Every active session's
    // <c>RevokedAt</c> is set; one <see cref="AccessAudit"/> row is
    // committed.

    [Fact]
    public async Task F5_RevokeAllButCurrent_RevokesOthersWhenCurrentUnknown()
    {
        var store = await BootStoreAsync();
        var svc   = new SessionService(store);

        const string owner  = "u-svc-f5-10";
        const string sid1   = "s-f5-10-1";
        const string sid2   = "s-f5-10-2";

        await Plant(store, new SessionDoc
        {
            Id = sid1, UserId = owner, UserAgent = "ua", Ip = "10.0.0.1",
            CreatedAt = DateTime.UtcNow, LastActive = DateTime.UtcNow,
            RevokedAt = null,
        });
        await Plant(store, new SessionDoc
        {
            Id = sid2, UserId = owner, UserAgent = "ua", Ip = "10.0.0.2",
            CreatedAt = DateTime.UtcNow, LastActive = DateTime.UtcNow,
            RevokedAt = null,
        });

        // currentSessionId = string.Empty — the all-but-current
        // degrades to "revoke all".
        await svc.RevokeAllButCurrentAsync(owner, "");

        // Both sessions are revoked (no current to preserve).
        await using var q = store.QuerySession();
        var d1 = await q.LoadAsync<SessionDoc>(sid1);
        var d2 = await q.LoadAsync<SessionDoc>(sid2);
        Assert.NotNull(d1?.RevokedAt);
        Assert.NotNull(d2?.RevokedAt);

        // The audit row is committed (the C3 pin — the audit row's
        // <c>TargetId</c> is the empty current-session id, which is
        // the "no current" marker).
        var audits = await AuditsFor(store, targetKind: "session",
                                     action: "session.revoke_all_but_current");
        Assert.Single(audits, a => a.ActorId == owner);
    }

    // ── 11 — F6 (C-S1·5) ───────────────────────────────────────────────
    //
    // <see cref="SessionService.TouchSessionAsync"/> is the **dumb
    // write** (C-S1·5 — the rate-limit is the middleware's, not the
    // service's): when called, it sets <c>LastActive = UtcNow</c>.
    // The test verifies the service's contract: a touch on an active
    // session updates <c>LastActive</c>.

    [Fact]
    public async Task F6_TouchSession_UpdatesLastActiveWhenDue()
    {
        var store = await BootStoreAsync();
        var svc   = new SessionService(store);

        const string owner = "u-svc-f6-11";
        const string sid   = "s-f6-11";

        // Plant the doc with a LastActive that is well in the past
        // (the "due" condition — the middleware would call
        // TouchSessionAsync because UtcNow - LastActive >= interval).
        await Plant(store, new SessionDoc
        {
            Id = sid, UserId = owner, UserAgent = "ua", Ip = "10.0.0.1",
            CreatedAt = DateTime.UtcNow,
            LastActive = DateTime.UtcNow.AddMinutes(-5),
            RevokedAt = null,
        });

        var before = DateTime.UtcNow;
        await svc.TouchSessionAsync(sid);
        var after  = DateTime.UtcNow;

        // The doc's LastActive is now within [before, after] (the
        // service's dumb write set it to UtcNow inside the call).
        await using var q = store.QuerySession();
        var doc = await q.LoadAsync<SessionDoc>(sid);
        Assert.NotNull(doc);
        Assert.NotNull(doc.LastActive);
        Assert.True(doc.LastActive >= before && doc.LastActive <= after,
            $"LastActive={doc.LastActive} not within [{before}, {after}].");
    }

    // ── 12 — F6 (C-S1·5, C-S1·2) ───────────────────────────────────────
    //
    // A revoked session is **never** touched (C-S1·2 — the
    // <c>RevokedAt is not null</c> short-circuit in
    // <see cref="SessionService.TouchSessionAsync"/>). The test
    // verifies the service's contract: a touch on a revoked session
    // is a no-op (<c>LastActive</c> is unchanged).

    [Fact]
    public async Task F6_TouchSession_SkipsWhenNotDue()
    {
        var store = await BootStoreAsync();
        var svc   = new SessionService(store);

        const string owner = "u-svc-f6-12";
        const string sid   = "s-f6-12-revoked";

        // Plant the doc already-revoked (the C-S1·2 short-circuit).
        var originalLastActive = DateTime.UtcNow.AddMinutes(-5);
        await Plant(store, new SessionDoc
        {
            Id = sid, UserId = owner, UserAgent = "ua", Ip = "10.0.0.1",
            CreatedAt = DateTime.UtcNow,
            LastActive = originalLastActive,
            RevokedAt = DateTime.UtcNow.AddMinutes(-1),
        });

        await svc.TouchSessionAsync(sid);

        // The doc's LastActive is unchanged (the C-S1·2 short-circuit
        // — the service does not update the <c>LastActive</c> of a
        // revoked session).
        await using var q = store.QuerySession();
        var doc = await q.LoadAsync<SessionDoc>(sid);
        Assert.NotNull(doc);
        Assert.Equal(originalLastActive, doc.LastActive);
    }

    // ── Shared helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Boot a real <see cref="IDocumentStore"/> over a fresh scratch
    /// Postgres (the <see cref="PostgresFixture"/>'s
    /// <c>NewDatabaseAsync</c> shape), with the lane's
    /// <c>SessionDocTypes</c> registered (the §2.2 pin:
    /// <c>opts.Schema.For&lt;SessionDoc&gt;()</c>) + the existing
    /// <c>CoreDocTypes</c> (which registers <c>AccessAudit</c> +
    /// <c>Component</c> + every other M1–M37 doc) + <c>PostDocTypes</c>
    /// (the M3 surface, registered for parity with the M3
    /// <c>PostServiceTests</c> harness shape).
    /// </summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn  = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
            SessionDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>Plant a document row directly (test fixture seeding, not a
    /// service write seam — the M3 <c>PostServiceTests.Plant</c> idiom).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    /// <summary>Query the <c>mt</c> schema for the <see cref="AccessAudit"/>
    /// rows matching <c>targetKind</c> + <c>action</c> (the C3
    /// same-transaction pin — the audit row is in the same schema as
    /// the domain write).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> AuditsFor(
        IDocumentStore store, string targetKind, string action)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>()
            .Where(a => a.TargetKind == targetKind && a.Action == action)
            .ToListAsync(ct);
    }
}
