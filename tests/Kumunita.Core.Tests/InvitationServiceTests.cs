using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Marten;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0304 (IMPROVE-UX A07) — the <see cref="InvitationService"/> pins:
/// the email invitation's Core seam. The unit card's "Done when"
/// (verbatim): "tests: expired/revoked refused, single use."
/// <list type="bullet">
/// <item>(a) an **expired** invitation is refused —
/// <see cref="InvitationService.ValidateAsync"/> returns <c>null</c>
/// past <see cref="Invitation.ExpiresAt"/>.</item>
/// <item>(b) a **revoked** invitation is refused —
/// <see cref="InvitationService.ValidateAsync"/> returns <c>null</c>
/// after <see cref="InvitationService.RevokeAsync"/>.</item>
/// <item>(c) **single use** — a second <see cref
/// "InvitationService.AcceptAsync"/> for the same token is refused once
/// the row is <see cref="EmailInvitationStatus.Accepted"/>.</item>
/// <item>(d) <see cref="InvitationService.AcceptAsync"/> with a
/// **mismatched email** is refused (the token is only good for the
/// invited address, ADR 0304 D4).</item>
/// <item>(e) the **stored value is a hash, never the raw token** (the
/// "hash only at rest" pin — the 0159 ESC·3 / F2 / ADR 0302 D2 rule).</item>
/// </list>
/// Plus the audit pins: exactly one <c>invitation.create</c> row per
/// create, one <c>invitation.revoke</c> row per revoke, and one
/// <c>invitation.accept</c> row per accept (<c>Via = Owner</c>,
/// <c>TargetKind = "invitation"</c> — the <see cref
/// "KioskLinkService"/> one-row convention), and **zero** rows on a
/// validate (the M35·5 "the read is a config load, not an access
/// decision" floor).
/// <para>
/// Harness: the <see cref="PostgresFixture"/> (Testcontainers
/// <c>postgres:18</c>) fresh-scratch-database-per-test shape (the
/// <see cref="KioskLinkServiceTests"/> precedent). The store registers
/// the <see cref="Invitation"/> (the <see cref
/// "Kumunita.Core.CoreDocTypes"/> surface) + <see cref
/// "AccessAudit"/> (the <see cref
/// "Kumunita.Core.CoreDocTypes"/> surface). <see cref
/// "IIdentityService"/> is an NSubstitute (the
/// <see cref="InvitationService.AcceptAsync"/> lane calls
/// <c>RegisterAsync</c> once; the test asserts the call was made).
/// </para>
/// </summary>
public sealed class InvitationServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Inviter = "inviter-001";
    private const string OtherInviter = "inviter-other-002";
    private const string InvitedEmail = "invitee@example.com";
    private const string OtherEmail = "other@example.com";

    // ── Pin (e) — hash only at rest ───────────────────────────────────────

    [Fact]
    public async Task ADR0304_Store_HoldsHashNotRawToken()
    {
        var (svc, store, _) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        var (token, row) = await svc.CreateAsync(
            InvitedEmail, Inviter, DateTimeOffset.UtcNow.AddDays(7), ct);

        // The raw token is returned (the caller shows it, once).
        Assert.False(string.IsNullOrEmpty(token));
        Assert.StartsWith("kinv_", token);   // the ADR 0304 D2 format prefix
        // The row is returned and is the same row the store now holds.
        Assert.False(string.IsNullOrEmpty(row.Id));
        Assert.Equal(InvitedEmail, row.Email);
        Assert.Equal(Inviter, row.InviterSubjectId);

        // The stored value is the SHA-256 hex digest (64 lowercase hex
        // chars — the IdentityService.Sha256Hex shape), NEVER the raw token
        // (the 0159 ESC·3 / F2 / ADR 0302 D2 hash-at-rest rule, ADR 0304
        // D2).
        Assert.NotEqual(token, row.TokenHash);
        Assert.Equal(64, row.TokenHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", row.TokenHash);
        // The stored digest is exactly the SHA-256 hex of the raw token
        // (the IdentityService.Sha256Hex algorithm — the test-local helper
        // mirrors it: same input, same output).
        Assert.Equal(TestSha256Hex(token), row.TokenHash);

        // And the whole row's persisted shape carries no raw token: the
        // raw token appears in NONE of the row's members (the unrecoverable
        // pin — a DB dump of this row cannot yield the credential).
        Assert.DoesNotContain(token, row.Id);
        Assert.DoesNotContain(token, row.Email);
        Assert.DoesNotContain(token, row.InviterSubjectId);
        Assert.DoesNotContain(token, row.TokenHash);
        await store.DisposeAsync();
    }

    // ── Pin (a) — expired invitation refused ───────────────────────────────

    [Fact]
    public async Task ADR0304_Validate_ExpiredToken_IsRefused()
    {
        var (svc, store, _) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        // Create an invitation that expired 1 hour ago.
        var (token, row) = await svc.CreateAsync(
            InvitedEmail, Inviter, DateTimeOffset.UtcNow.AddHours(-1), ct);
        var hash = TestSha256Hex(token);

        // The past-expiry Pending row is refused (the virtual Expired state,
        // ADR 0304 D1 / D6).
        Assert.Null(await svc.ValidateAsync(hash, ct));
        Assert.Null(await svc.ValidateAsync(TestSha256Hex("never-created"), ct));
        await store.DisposeAsync();
    }

    // ── Pin (b) — revoked invitation refused ───────────────────────────────

    [Fact]
    public async Task ADR0304_Validate_RevokedToken_IsRefused()
    {
        var (svc, store, _) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        var (token, row) = await svc.CreateAsync(
            InvitedEmail, Inviter, DateTimeOffset.UtcNow.AddDays(7), ct);
        var hash = TestSha256Hex(token);

        // Control: it validates while live (Pending + not past expiry).
        Assert.NotNull(await svc.ValidateAsync(hash, ct));

        // Revoke (the inviter's own invitation — allowed).
        Assert.True(await svc.RevokeAsync(row.Id, Inviter, ct));

        // ...and is refused from now on (the revoked case, ADR 0304 D3).
        Assert.Null(await svc.ValidateAsync(hash, ct));
        await store.DisposeAsync();
    }

    // ── Pin (c) — single use: second AcceptAsync refused ───────────────────

    [Fact]
    public async Task ADR0304_Accept_SecondAttempt_IsRefused()
    {
        var (svc, store, identity) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        var (token, row) = await svc.CreateAsync(
            InvitedEmail, Inviter, DateTimeOffset.UtcNow.AddDays(7), ct);
        var hash = TestSha256Hex(token);

        // The IIdentityService.RegisterAsync stub returns a sentinel
        // principal (the test does not need a real account).
        identity.RegisterAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
        .Returns(callInfo => Task.FromResult(
            new ThinPrincipal(
                SubjectId: "new-account-001",
                ExternalId: null,
                IsVerifiedResident: false,
                Roles: ThinPrincipal.NoRoles)));

        // First Accept: succeeds, marks the row Accepted.
        var principal = await svc.AcceptAsync(
            hash, InvitedEmail, "Invitee Name", "Sup3rSecret!", ct);
        Assert.NotNull(principal);
        Assert.Equal("new-account-001", principal!.SubjectId);

        // The row is now Accepted (the single-use pin, ADR 0304 D4).
        var reloaded = (await store.OpenSession(new Marten.Services.SessionOptions())
            .LoadAsync<Invitation>(row.Id, ct))!;
        Assert.Equal(EmailInvitationStatus.Accepted, reloaded.Status);
        Assert.NotNull(reloaded.AcceptedAt);
        Assert.Equal("new-account-001", reloaded.AcceptedSubjectId);

        // Second Accept for the SAME token is refused (the row is Accepted,
        // ValidateAsync returns null — the ADR 0304 D4 single-use pin).
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AcceptAsync(
                hash, InvitedEmail, "Invitee Name", "Sup3rSecret!", ct));

        // The IIdentityService.RegisterAsync was called exactly ONCE (the
        // second attempt never reached the RegisterAsync lane — it failed
        // at the ValidateAsync step before step 3).
        await identity.Received(1).RegisterAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await store.DisposeAsync();
    }

    // ── Pin (d) — mismatched email refused ─────────────────────────────────

    [Fact]
    public async Task ADR0304_Accept_MismatchedEmail_IsRefused()
    {
        var (svc, store, identity) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        var (token, _) = await svc.CreateAsync(
            InvitedEmail, Inviter, DateTimeOffset.UtcNow.AddDays(7), ct);
        var hash = TestSha256Hex(token);

        // A different email than the one on the invitation is refused
        // (the ADR 0304 D4 single-address pin — the token is only good for
        // the invited address). The IIdentityService.RegisterAsync is NOT
        // called (the refusal is at step 2, before step 3).
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AcceptAsync(
                hash, OtherEmail, "Other Name", "Sup3rSecret!", ct));

        // RegisterAsync was never reached (the email match check is the
        // step-2 gate, before the RegisterAsync call).
        await identity.DidNotReceive()
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await store.DisposeAsync();
    }

    // ── Revoke — inviter-scoped, idempotent, audited ───────────────────────

    [Fact]
    public async Task ADR0304_Revoke_InviterScoped_Idempotent_Audited()
    {
        var (svc, store, _) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        var (_, row) = await svc.CreateAsync(
            InvitedEmail, Inviter, DateTimeOffset.UtcNow.AddDays(7), ct);

        // A non-inviter cannot revoke (inviter-scope pin, ADR 0304 D3).
        Assert.False(await svc.RevokeAsync(row.Id, OtherInviter, ct));

        // The inviter's revoke succeeds once...
        Assert.True(await svc.RevokeAsync(row.Id, Inviter, ct));
        // ...and is idempotent (already revoked → no-op, the 0159 ESC·9 /
        // ADR 0302 D3 shape).
        Assert.False(await svc.RevokeAsync(row.Id, Inviter, ct));
        // An unknown invitation is a no-op (the missing-row case).
        Assert.False(await svc.RevokeAsync("no-such-invitation", Inviter, ct));

        // Audit: exactly ONE create row + exactly ONE revoke row (the
        // no-op revokes wrote nothing — the ESC·9 no-audit-on-no-op pin).
        var rows = await AllAudits(store);
        Assert.Equal(2, rows.Count);
        Assert.Equal("invitation.create", rows[0].Action);
        Assert.Equal("invitation.revoke", rows[1].Action);
        foreach (var r in rows)
        {
            Assert.Equal(Inviter, r.ActorId);
            Assert.Equal("invitation", r.TargetKind);
            Assert.Equal(row.Id, r.TargetId);
            Assert.Equal(AccessVia.Owner, r.Via);
            Assert.Equal(AccessOutcome.Allow, r.Outcome);
        }
        await store.DisposeAsync();
    }

    // ── Validate — zero audit rows (the M35·5 floor) ───────────────────────

    [Fact]
    public async Task ADR0304_Validate_WritesNoAuditRow()
    {
        var (svc, store, _) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        var (token, _) = await svc.CreateAsync(
            InvitedEmail, Inviter, DateTimeOffset.UtcNow.AddDays(7), ct);
        var hash = TestSha256Hex(token);

        // The validate is the M35·5 "zero audit on the read" lane...
        Assert.NotNull(await svc.ValidateAsync(hash, ct));
        var all = await AllAudits(store);
        Assert.Single(all);                    // only the create row
        Assert.Equal("invitation.create", all[0].Action);

        // A refused validate (unknown token) also writes no audit row.
        await svc.ValidateAsync(TestSha256Hex("never-created"), ct);
        Assert.Equal(1, (await AllAudits(store)).Count);
        await store.DisposeAsync();
    }

    // ── A08 — the presented-token overloads and the hash-based seams agree ──
    // Trap A (ADR 0304 D3, IMPROVE-UX A08): the hashing lives in ONE place
    // (IdentityService.Sha256Hex, internal to Core). The A08 sign-up gate
    // hashes the presented token via ValidatePresentedAsync / AcceptPresentedAsync
    // and must agree with a hash-based ValidateAsync / AcceptAsync lookup on the
    // SAME token — a drift between the two would be a divergence risk the "the
    // hash is one fact" invariant forbids (the KioskLinkService R16 /
    // EscalationTokenService precedent this ADR names as its template).

    [Fact]
    public async Task ADR0304_A08_PresentedOverload_AgreesWithHashBasedValidate()
    {
        var (svc, store, _) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        var (token, row) = await svc.CreateAsync(
            InvitedEmail, Inviter, DateTimeOffset.UtcNow.AddDays(7), ct);
        var hash = TestSha256Hex(token);   // the test-local mirror of Sha256Hex

        // The presented-token overload (owner-less — the sign-up gate has no
        // owner session) returns the SAME row the hash-based seam returns for
        // the same token (the two paths must not diverge — the one-fact
        // invariant).
        var presented = await svc.ValidatePresentedAsync(token, ct);
        Assert.NotNull(presented);
        Assert.Equal(row.Id, presented!.Id);
        Assert.Equal(row.Id, (await svc.ValidateAsync(hash, ct))!.Id);
        await store.DisposeAsync();
    }

    [Fact]
    public async Task ADR0304_A08_PresentedOverload_RefusesBlankRevokedExpiredAndUnknown()
    {
        var (svc, store, _) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        // Blank / empty presented tokens refuse (the ESC·5 guard — no session,
        // no store hit).
        Assert.Null(await svc.ValidatePresentedAsync(string.Empty, ct));
        Assert.Null(await svc.ValidatePresentedAsync("   ", ct));

        // Unknown token refuses (no matching row).
        Assert.Null(await svc.ValidatePresentedAsync("kinv_never-created", ct));

        // Revoked token refuses (the revoked case, owner-less).
        var (token, row) = await svc.CreateAsync(
            InvitedEmail, Inviter, DateTimeOffset.UtcNow.AddDays(7), ct);
        Assert.NotNull(await svc.ValidatePresentedAsync(token, ct));   // valid while live
        Assert.True(await svc.RevokeAsync(row.Id, Inviter, ct));
        Assert.Null(await svc.ValidatePresentedAsync(token, ct));      // refused after revoke

        // Expired token refuses (the virtual-Expired state, ADR 0304 D1/D6).
        var (expired, _) = await svc.CreateAsync(
            InvitedEmail, Inviter, DateTimeOffset.UtcNow.AddHours(-1), ct);
        Assert.Null(await svc.ValidatePresentedAsync(expired, ct));
        await store.DisposeAsync();
    }

    [Fact]
    public async Task ADR0304_A08_PresentedAccept_MatchingEmail_RunsTheLane()
    {
        var (svc, store, identity) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        var (token, row) = await svc.CreateAsync(
            InvitedEmail, Inviter, DateTimeOffset.UtcNow.AddDays(7), ct);

        // The IIdentityService.RegisterAsync stub returns a sentinel principal
        // (the test does not need a real account).
        identity.RegisterAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
        .Returns(callInfo => Task.FromResult(
            new ThinPrincipal(
                SubjectId: "new-account-a08",
                ExternalId: null,
                IsVerifiedResident: false,
                Roles: ThinPrincipal.NoRoles)));

        // The presented-token write seam (the A08 POST's entry point) runs the
        // RegisterAsync lane + marks the row Accepted (the same contract as the
        // hash-based AcceptAsync — the one-fact invariant).
        var principal = await svc.AcceptPresentedAsync(
            token, InvitedEmail, "Invitee Name", "Sup3rSecret!", ct);
        Assert.Equal("new-account-a08", principal!.SubjectId);

        // The row is now Accepted (the single-use pin, ADR 0304 D4).
        var reloaded = (await store.OpenSession(new Marten.Services.SessionOptions())
            .LoadAsync<Invitation>(row.Id, ct))!;
        Assert.Equal(EmailInvitationStatus.Accepted, reloaded.Status);
        await identity.Received(1).RegisterAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await store.DisposeAsync();
    }

    [Fact]
    public async Task ADR0304_A08_PresentedAccept_MismatchedEmail_IsRefused()
    {
        var (svc, store, identity) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        var (token, _) = await svc.CreateAsync(
            InvitedEmail, Inviter, DateTimeOffset.UtcNow.AddDays(7), ct);

        // A different email than the one on the invitation is refused (the
        // ADR 0304 D4 single-address pin — the token is only good for the
        // invited address). The RegisterAsync lane is NOT reached.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AcceptPresentedAsync(
                token, OtherEmail, "Other Name", "Sup3rSecret!", ct));
        await identity.DidNotReceive()
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await store.DisposeAsync();
    }

    [Fact]
    public async Task ADR0304_A08_PresentedAccept_BlankToken_IsRefused()
    {
        var (svc, store, identity) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        // A blank token refuses before any hash/lookup (the ESC·5 guard — the
        // same refusal shape as the hash-based AcceptAsync's invalid-token case).
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AcceptPresentedAsync(string.Empty, InvitedEmail, "N", "P", ct));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AcceptPresentedAsync("   ", InvitedEmail, "N", "P", ct));
        await identity.DidNotReceive()
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await store.DisposeAsync();
    }

    // ── A09 — the read seam (ListAsync) + the delivery seam (InviteAsync) ──
    // IMPROVE-UX A09: the /admin/invitations surface's Core seams. The card's
    // "Done when" pins (verbatim): (a) ListAsync returns the inviter's rows
    // newest-first and surfaces the virtual-Expired state (a Pending row past
    // ExpiresAt shows as expired); (b) ListAsync writes ZERO audit rows (the
    // read floor); (c) ListAsync is owner-scoped (another inviter's rows are
    // not returned). Plus the InviteAsync delivery pins: one create + one
    // staged email (the invite:{id} idempotency key, the invitee's address, a
    // body carrying the ?invite= link), the token shown once, the row
    // hash-only (the A07 pins still hold on the InviteAsync path).

    [Fact]
    public async Task A09_List_ReturnsInviterRows_NewestFirst_SurfacesVirtualExpired()
    {
        var (svc, store, _) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        // Three rows for the inviter: an accepted one (oldest), a pending
        // one, and a Pending row past expiry (newest — the virtual-Expired
        // case). A fourth row belongs to ANOTHER inviter (pin (c) below).
        var (_, accepted) = await svc.CreateAsync(
            "a@example.com", Inviter, DateTimeOffset.UtcNow.AddDays(1), ct);
        await svc.RevokeAsync(accepted.Id, Inviter, ct);   // move it to a terminal state
        var (_, pending) = await svc.CreateAsync(
            "b@example.com", Inviter, DateTimeOffset.UtcNow.AddDays(3), ct);
        var (_, expired) = await svc.CreateAsync(
            "c@example.com", Inviter, DateTimeOffset.UtcNow.AddHours(-1), ct);
        var (_, other) = await svc.CreateAsync(
            OtherEmail, OtherInviter, DateTimeOffset.UtcNow.AddDays(7), ct);

        var list = await svc.ListAsync(Inviter, ct);

        // Pin (c) — owner-scoped: the other inviter's row is NOT returned.
        Assert.Equal(3, list.Count);
        Assert.DoesNotContain(other.Id, list.Select(l => l.InvitationId));

        // Newest-first (pin (a)): expired (newest) → pending → accepted.
        Assert.Equal(expired.Id, list[0].InvitationId);
        Assert.Equal(pending.Id, list[1].InvitationId);
        Assert.Equal(accepted.Id, list[2].InvitationId);

        // The virtual-Expired surface (pin (a)): the stored Status is still
        // Pending (the enum is NEVER written — the ADR 0304 D1/D6 rule)...
        Assert.Equal(EmailInvitationStatus.Pending, list[0].Status);
        // ...but DisplayStatus reads Expired (a Pending row past ExpiresAt).
        Assert.Equal(EmailInvitationStatus.Expired, list[0].DisplayStatus);

        // The live rows surface their stored status unchanged.
        Assert.Equal(EmailInvitationStatus.Pending, list[1].DisplayStatus);
        Assert.Equal(EmailInvitationStatus.Revoked, list[2].DisplayStatus);

        // The read projection carries the row's identity + window...
        Assert.Equal("c@example.com", list[0].Email);
        Assert.Equal(Inviter, list[0].InviterSubjectId);
        Assert.Equal(expired.ExpiresAt, list[0].ExpiresAt);
        // ...and the Accepted mark when set (the accepted row here is
        // revoked — nulls; the single-use test above pins the accepted
        // shape).
        Assert.Null(list[0].AcceptedAt);
        Assert.Null(list[0].AcceptedSubjectId);
        await store.DisposeAsync();
    }

    [Fact]
    public async Task A09_List_WritesZeroAuditRows()
    {
        var (svc, store, _) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        var (token, row) = await svc.CreateAsync(
            InvitedEmail, Inviter, DateTimeOffset.UtcNow.AddDays(7), ct);

        // One create row exists...
        var before = await AllAudits(store);
        Assert.Single(before);
        Assert.Equal("invitation.create", before[0].Action);

        // ...and the list reads write NOTHING (the M35·5 "the read is a
        // config load, not an access decision" floor — the ValidateAsync
        // precedent, the A09 pin (b)).
        var list = await svc.ListAsync(Inviter, ct);
        Assert.Single(list);

        var after = await AllAudits(store);
        Assert.Single(after);   // ListAsync must write zero audit rows (the read floor)
        await store.DisposeAsync();
    }

    [Fact]
    public async Task A09_List_OwnerScoped_EmptyFloorForUnknownInviter()
    {
        var (svc, store, _) = await BootAsync();
        var ct = TestContext.Current.CancellationToken;

        var (_, row) = await svc.CreateAsync(
            InvitedEmail, Inviter, DateTimeOffset.UtcNow.AddDays(7), ct);

        // The inviter sees their row...
        var own = await svc.ListAsync(Inviter, ct);
        Assert.Single(own);
        Assert.Equal(row.Id, own[0].InvitationId);

        // Another inviter sees NOTHING (owner-scoped, the A09 pin (c) — the
        // list is an owner-scoped read, not a community-wide one).
        Assert.Empty(await svc.ListAsync(OtherInviter, ct));

        // A blank inviter id is refused before any store hit (the CreateAsync
        // guard shape).
        await Assert.ThrowsAsync<ArgumentException>(() => svc.ListAsync(string.Empty, ct));
        await store.DisposeAsync();
    }

    // ── A09 — the delivery seam (InviteAsync: create + the staged email) ───

    [Fact]
    public async Task A09_Invite_CreatesRow_StagesOneEmail_WithInviteLinkAndIdempotencyKey()
    {
        var (svc, store, _, mailer) = await BootA09Async();
        var ct = TestContext.Current.CancellationToken;

        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var result = await svc.InviteAsync(
            InvitedEmail, Inviter, "Admin Name", expiresAt,
            inviteLinkOverride: "https://maplewood.example/account/signup?invite="
                + "<TOKEN>");

        // The raw token is returned once (the ADR 0304 D2 shown-once rule)...
        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.StartsWith("kinv_", result.Token);
        // ...the row carries only the hash (the A07 hash-at-rest pin holds on
        // the InviteAsync path too).
        Assert.NotEqual(result.Token, result.Invitation.TokenHash);
        Assert.Equal(64, result.Invitation.TokenHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", result.Invitation.TokenHash);
        Assert.DoesNotContain(result.Token, result.Invitation.Email);

        // Exactly ONE invitation.create audit row (the CreateAsync contract —
        // InviteAsync does not add a second row).
        var rows = await AllAudits(store);
        Assert.Single(rows);
        Assert.Equal("invitation.create", rows[0].Action);
        Assert.Equal(Inviter, rows[0].ActorId);
        Assert.Equal("invitation", rows[0].TargetKind);
        Assert.Equal(result.Invitation.Id, rows[0].TargetId);
        Assert.Equal(AccessVia.Owner, rows[0].Via);

        // Exactly ONE staged email (the A09 delivery pin): the invitee's
        // address, the invite:{invitationId} idempotency key (the §6.2
        // per-email scheme), and a body carrying the ?invite= link.
        var staged = Assert.Single(mailer.Staged);
        Assert.Equal(InvitedEmail, staged.Recipient);
        Assert.Equal($"invite:{result.Invitation.Id}", staged.IdempotencyKey);
        Assert.Contains("account/signup?invite=", staged.Body);
        Assert.Equal("You're invited to Kumunita", staged.Subject);   // the en floor (You're invited to {2})
        // The body names the inviter, the community, + the expiry window (the
        // A09 prompt: "the body must name the inviter's community and the expiry").
        Assert.StartsWith("Hi Admin Name,", staged.Body);
        Assert.Contains("has invited you to join Kumunita", staged.Body);
        Assert.Contains("stays valid until", staged.Body);
        await store.DisposeAsync();
    }

    [Fact]
    public async Task A09_Invite_AssembledLink_UsesBaseUrlReaderAndFallsBackToRelative()
    {
        var (svc, store, _, mailer) = await BootA09Async(
            baseUrl: "https://maplewood.example/");

        var result = await svc.InviteAsync(
            InvitedEmail, Inviter, "Admin Name",
            DateTimeOffset.UtcNow.AddDays(7));   // no override — the assembly

        // The ?invite= link is absolute (the BaseUrl prefix + the A08 gate's
        // path) and carries the raw token — the A08 receiving gate resolves
        // exactly this shape.
        var staged = Assert.Single(mailer.Staged);
        Assert.Contains(
            "https://maplewood.example/account/signup?invite=" + result.Token,
            staged.Body);

        await store.DisposeAsync();
    }

    [Fact]
    public async Task A09_Invite_RelativeLink_FallsBack_WhenBaseUrlUnset()
    {
        var (svc, store, _, mailer) = await BootA09Async(baseUrl: null);

        var result = await svc.InviteAsync(
            InvitedEmail, Inviter, "Admin Name",
            DateTimeOffset.UtcNow.AddDays(7));

        // Unset BaseUrl (a dev-only shape) → the same-origin relative path
        // (the VerificationLink precedent — a human can still copy it into
        // the browser bar).
        var staged = Assert.Single(mailer.Staged);
        Assert.Contains(
            "/account/signup?invite=" + result.Token, staged.Body);
        await store.DisposeAsync();
    }

    [Fact]
    public async Task A09_Invite_TranslatedTemplate_IsFormattedAndFloorsOnMissingKey()
    {
        var (svc, store, _, mailer) = await BootA09Async(
            provider: FakeProvider.Of(
                ("email.invitation_subject", "Einladung zu Kumunita"),
                ("email.invitation_body", "Hallo {0},\n\nDu bist zu Kumunita eingeladen. Link: {1}")));

        var result = await svc.InviteAsync(
            InvitedEmail, Inviter, "Admin Name",
            DateTimeOffset.UtcNow.AddDays(7),
            inviteLinkOverride: "https://example.org/account/signup?invite=tok");

        var staged = Assert.Single(mailer.Staged);
        Assert.Equal("Einladung zu Kumunita", staged.Subject);
        Assert.StartsWith("Hallo Admin Name,", staged.Body);
        Assert.Contains("https://example.org/account/signup?invite=tok", staged.Body);

        // A provider WITHOUT the key → the raw-English floor (no raw-key
        // leak, the BuildVerificationEmailAsync floor).
        var (svc2, store2, _, mailer2) = await BootA09Async(provider: new FakeProvider());
        await svc2.InviteAsync(
            InvitedEmail, Inviter, "Admin Name",
            DateTimeOffset.UtcNow.AddDays(7),
            inviteLinkOverride: "https://example.org/account/signup?invite=tok2");
        var staged2 = Assert.Single(mailer2.Staged);
        Assert.Equal("You're invited to Kumunita", staged2.Subject);
        Assert.StartsWith("Hi Admin Name,", staged2.Body);
        Assert.Contains("has invited you to join Kumunita", staged2.Body);
        await store.DisposeAsync();
        await store2.DisposeAsync();
    }

    [Fact]
    public async Task A09_Invite_NullMailer_RefusesLoudly()
    {
        var (svc, store, _) = await BootAsync();   // the A07 harness — no mailer
        var ct = TestContext.Current.CancellationToken;

        // InviteAsync is the delivery seam: a null mailer is a test shape
        // that did not supply it — refuse loudly (the C3 no-silent-missing-
        // email floor) rather than create an undelivered invitation.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.InviteAsync(
                InvitedEmail, Inviter, "Admin Name",
                DateTimeOffset.UtcNow.AddDays(7), ct: ct));
        await store.DisposeAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Boot a fresh <c>mt</c> schema in a new scratch database — the
    /// doc types these pins read: <see cref="Invitation"/> +
    /// <see cref="AccessAudit"/> (the <see cref
    /// "Kumunita.Core.CoreDocTypes"/> surface). <see cref
    /// "IIdentityService"/> is an NSubstitute (the <see cref
    /// "InvitationService.AcceptAsync"/> lane calls it once).</summary>
    private async Task<(InvitationService Svc, IDocumentStore Store, IIdentityService Identity)> BootAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            Kumunita.Core.CoreDocTypes.Configure(opts);   // Invitation + AccessAudit
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, ct);

        var identity = Substitute.For<IIdentityService>();
        var svc = new InvitationService(store, identity);
        return (svc, store, identity);
    }

    /// <summary>Boot the A09 delivery seam: the A07 harness plus a fake
    /// <see cref="IMailerStage"/> (the staged emails are the assertion
    /// target), a fake <see cref="IInstanceBaseUrlReader"/> (nullable
    /// <paramref name="baseUrl"/>) and a fake
    /// <see cref="ITranslationProvider"/> (nullable
    /// <paramref name="provider"/> — null = the raw-English floor, the
    /// <c>BuildVerificationEmailAsync</c> shape).</summary>
    private async Task<(InvitationService Svc, IDocumentStore Store, IIdentityService Identity, MailerFake Mailer)> BootA09Async(
        string? baseUrl = null, ITranslationProvider? provider = null)
    {
        var (svc, store, identity) = await BootAsync();
        var mailer = new MailerFake();
        svc = new InvitationService(
            store, identity,
            mailer,
            new BaseUrlFake(baseUrl),
            provider,
            null);   // CommunityOptions null → the "Kumunita" floor
        return (svc, store, identity, mailer);
    }

    /// <summary>The whole <see cref="AccessAudit"/> table (unsorted — the
    /// test inspects).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> AllAudits(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        using var session = store.QuerySession();
        return await session.Query<AccessAudit>().ToListAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Test-local SHA-256 hex helper (the <see cref="Kumunita.Core
    /// .Identity.IdentityService.Sha256Hex"/> algorithm — same input, same
    /// output). <c>IdentityService.Sha256Hex</c> is <c>internal</c> to the
    /// Core assembly; the test project cannot see it. The helper mirrors the
    /// repo's <c>TestLocalSha256Hex</c> precedent in
    /// <c>IdentityServiceTokenHashTests</c>.
    /// </summary>
    private static string TestSha256Hex(string value) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(value)))
        .ToLowerInvariant();   // exactly 64 lowercase hex chars
}

/// <summary>
/// A09 test fake — the <see cref="IMailerStage"/> the
/// <see cref="InvitationService.InviteAsync"/> delivery seam stages into.
/// Records the staged emails (the A09 delivery pins: the recipient, the
/// idempotency key, the body carrying the <c>?invite=</c> link).
/// </summary>
internal sealed class MailerFake : IMailerStage
{
    public List<StagedEmail> Staged { get; } = new();

    public Task StageAsync(
        Marten.IDocumentSession session,
        string idempotencyKey,
        string recipient,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        Staged.Add(new StagedEmail(idempotencyKey, recipient, subject, body));
        return Task.CompletedTask;
    }

    public sealed record StagedEmail(
        string IdempotencyKey, string Recipient, string Subject, string Body);
}

/// <summary>A09 test fake — the <see cref="IInstanceBaseUrlReader"/> (a
/// fixed <c>BaseUrl</c> or null for the relative-path fallback).</summary>
internal sealed class BaseUrlFake(string? baseUrl) : IInstanceBaseUrlReader
{
    public string? BaseUrl { get; } = baseUrl;
}

/// <summary>
/// A09 test fake — the <see cref="ITranslationProvider"/> the
/// <see cref="InvitationService.BuildInvitationEmailAsync"/> template
/// resolves against (a fixed key→value map; a missing key returns the key
/// itself — the floor-detection shape the service pins on).
/// </summary>
internal sealed class FakeProvider(
    params (string Key, string Value)[] entries) : ITranslationProvider
{
    private readonly Dictionary<string, string> _values = entries.ToDictionary(e => e.Key, e => e.Value);

    /// <summary>Test-local builder (the call-site tuple shape).</summary>
    public static FakeProvider Of(params (string Key, string Value)[] entries) => new(entries);

    public Task<string> GetAsync(string key, string? preferredLanguageCode) =>
        Task.FromResult(_values.GetValueOrDefault(key) ?? key);

    public Task<string> GetAsync(string key, IReadOnlyCollection<string>? candidates) =>
        Task.FromResult(_values.GetValueOrDefault(key) ?? key);

    public Task<string> ResolveEffectiveLanguageAsync(string? preferredLanguageCode) =>
        Task.FromResult("en");

    public Task<string> ResolveEffectiveLanguageAsync(IReadOnlyCollection<string>? candidates) =>
        Task.FromResult("en");

    public Task<IReadOnlyDictionary<string, string>> GetManyAsync(
        IReadOnlyCollection<string> keys, string? preferredLanguageCode) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(
            keys.ToDictionary(k => k, k => _values.GetValueOrDefault(k) ?? k));

    public Task<IReadOnlyDictionary<string, string>> GetManyAsync(
        IReadOnlyCollection<string> keys, IReadOnlyCollection<string>? candidates) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(
            keys.ToDictionary(k => k, k => _values.GetValueOrDefault(k) ?? k));
}
