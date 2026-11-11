using Kumunita.Core.Identity;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/invitations</c> surface (IMPROVE-UX A09, ADR 0304) — the
/// GlobalAdmin's control plane over the invitation lane (the ADR 0304 D4
/// invariants): invite one or more emails, list sent / accepted / expired /
/// revoked, revoke, and resend. Mirrors <see
/// cref="AdminGuestsController"/> (ADR 0120 D6) exactly:
/// <see cref="Roles.GlobalAdmin"/>-gated at the boundary (the Web owns the
/// standing check; the Core seam does not re-check <c>User</c> — the ADR
/// 0050 split), a dedicated controller (the <c>AdminController</c>
/// constructor is pinned by two Web test harnesses — the ADR 0050
/// dedicated-controller rationale), and a thin wrapper over the
/// <b>invitation</b> seams on <see cref="IInvitationService"/>:
/// <see cref="IInvitationService.ListAsync"/> (the read — zero audit
/// rows), <see cref="IInvitationService.InviteAsync"/> (create + the
/// staged invitation email, one transaction, the service's
/// <c>invitation.create</c> audit row <c>Via = Owner</c>), and
/// <see cref="IInvitationService.RevokeAsync"/> (owner-scoped,
/// idempotent, the service's <c>invitation.revoke</c> audit row).
/// <para>
/// **The receiving side (A08) is already built** — this controller only
/// *produces* the <c>?invite=</c> link + email that the A08 sign-up gate
/// consumes; it never re-wires the gate.
/// </para>
/// </summary>
[Route("admin/invitations")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminInvitationsController(IInvitationService invitations) : Controller
{
    /// <summary>ADR 0304 D6(a) — the invitation expiry default (the ADR
    /// "ships no default — the A09/A12 surfaces will set it; the proposed
    /// default is 7 days"; this call site settles it for the admin
    /// surface). <c>// TODO ❓</c> whether it is admin-configurable
    /// (default 7 days) — the SEC review may settle it (recorded here as
    /// the A09 prompt asks, for the review note).</summary>
    internal const int DefaultExpiryDays = 7;

    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /admin/invitations</c> — the list: one section of rows (email,
    /// status — pending / accepted / revoked / expired, created, expires),
    /// newest first. The read seam (<see cref
    /// "IInvitationService.ListAsync"/>) is the sole reader (zero audit
    /// rows — the M35·5 "the read is a config load, not an access decision"
    /// floor; the access decision is this controller's
    /// <c>[Authorize(Roles = Roles.GlobalAdmin)]</c> boundary's job). The
    /// virtual-Expired state is computed in the read (a <c>Pending</c> row
    /// past <c>ExpiresAt</c> surfaces <c>Expired</c> in
    /// <see cref="InvitationListItem.DisplayStatus"/> — the ADR 0304 D1/D6
    /// pin). A friendly empty state renders when there are no invitations
    /// (the <c>.kmb-empty</c> shape, the R29–R33 reference).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var actor = ActorId(User) ?? string.Empty;
        var model = new InvitationsAdminViewModel
        {
            Items = await invitations.ListAsync(actor)
        };
        return View(model);
    }

    /// <summary>
    /// <c>POST /admin/invitations/invite</c> — invite one or more emails.
    /// The textarea is split on newlines / commas / whitespace; each email
    /// is validated (a shape check — the A09 card: "invalid / already-used
    /// / duplicate emails are reported, not silently dropped") and, when
    /// valid, <see cref="IInvitationService.InviteAsync"/> is called once
    /// per email (create + stage the invitation email in one transaction —
    /// the <c>invitation.create</c> audit row is the service's,
    /// <c>Via = Owner</c>; the email's idempotency key is
    /// <c>invite:{invitationId}</c>). The actor's own existing rows are
    /// pre-checked (a same-email <c>Pending</c> row is reported as
    /// "already invited", not silently duplicated). Success → a
    /// <c>TempData["info"]</c> flash + redirect (the ADR 0050
    /// redirect-after-write shape).
    /// </summary>
    [HttpPost("invite")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(string emails)
    {
        var actor = ActorId(User) ?? string.Empty;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var invited = new List<string>();

        // The actor's own rows (the duplicate pre-check — a same-email
        // **live** Pending row is reported as "already invited", not
        // silently re-created). DisplayStatus == Pending means a Pending
        // row that has NOT passed its expiry (the virtual-Expired state is
        // already computed in the Core read, the ADR 0304 D1/D6 pin) — so an
        // expired / accepted / revoked row does NOT block a fresh invitation
        // (the re-invite / resend path, A09).
        var existing = await invitations.ListAsync(actor);
        var pendingEmails = existing
            .Where(i => i.DisplayStatus == EmailInvitationStatus.Pending)
            .Select(i => i.Email)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in SplitEmails(emails))
        {
            var email = raw.Trim().ToLowerInvariant();
            if (!IsPlausibleEmail(email))
            {
                errors.Add($"{raw}: not a valid email");
                continue;
            }
            if (!seen.Add(email))
            {
                errors.Add($"{email}: listed more than once");
                continue;
            }
            if (pendingEmails.Contains(email))
            {
                errors.Add($"{email}: already invited (a live invitation exists)");
                continue;
            }
            // One InviteAsync per valid email (create + the staged email,
            // one transaction — the C3 no-silent-missing-email guarantee).
            var (token, row) = await invitations.InviteAsync(
                email, actor, await ActorDisplayNameAsync(),
                DateTimeOffset.UtcNow.AddDays(DefaultExpiryDays));
            invited.Add(email);
            _ = token;   // the email carries the ?invite= link — the raw token is not shown (ADR 0304 D2)
        }

        if (invited.Count > 0)
        {
            TempData["info"] = await this.FlashAsync(
                "admin.invitations.invited",
                "Invited: " + string.Join(", ", invited) + ".");
        }
        if (errors.Count > 0)
        {
            TempData["error"] = await this.FlashAsync(
                "admin.invitations.issues",
                string.Join(" ", errors));
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <c>POST /admin/invitations/revoke</c> — revoke a pending
    /// invitation. Delegates to <see
    /// cref="IInvitationService.RevokeAsync"/> (owner-scoped, idempotent —
    /// a missing / already-revoked / already-accepted invitation is a no-op
    /// returning <c>false</c>; the <c>invitation.revoke</c> audit row is
    /// the service's, <c>Via = Owner</c>).
    /// </summary>
    [HttpPost("revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(string invitationId)
    {
        var actor = ActorId(User) ?? string.Empty;
        var revoked = await invitations.RevokeAsync(invitationId, actor);
        if (revoked)
        {
            TempData["info"] = await this.FlashAsync(
                "admin.invitations.revoked", "Invitation revoked.");
        }
        else
        {
            // The no-op (missing / already terminal / not the actor's) —
            // reported, not an error page (the idempotent shape).
            TempData["error"] = await this.FlashAsync(
                "admin.invitations.revoke_failed",
                "The invitation could not be revoked (it may already be used, revoked, or expired).");
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <c>POST /admin/invitations/resend</c> — the resend semantics
    /// (IMPROVE-UX A09, settled): the token is shown-once + hashed (ADR
    /// 0304 D2), so a *resend* is a **fresh invitation** (a new token) for
    /// the same email — not a re-email of a dead token. Creates a new
    /// row, stages a new email, and revokes the superseded
    /// <c>Pending</c> row when one is supplied (the A09 card's
    /// "optionally revoke the superseded one"). <c>// TODO ❓</c> the
    /// SEC review may settle whether resends are rate-limited (ADR 0304
    /// D6(b) — the ceiling is A11's concern); recorded for the review
    /// note.
    /// </summary>
    [HttpPost("resend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resend(string email, string? supersededId)
    {
        var actor = ActorId(User) ?? string.Empty;
        var normalized = email.Trim().ToLowerInvariant();
        if (!IsPlausibleEmail(normalized))
        {
            TempData["error"] = await this.FlashAsync(
                "admin.invitations.resend_failed",
                "Not a valid email to resend to.");
            return RedirectToAction(nameof(Index));
        }

        // A fresh invitation (new token, the 7-day window) + the staged
        // email (the same lane as the invite action).
        await invitations.InviteAsync(
            normalized, actor, await ActorDisplayNameAsync(),
            DateTimeOffset.UtcNow.AddDays(DefaultExpiryDays));

        // Optionally revoke the superseded pending row (owner-scoped
        // no-op if it is already terminal or not the actor's — the ADR
        // 0302 D3 shape).
        if (!string.IsNullOrWhiteSpace(supersededId))
        {
            await invitations.RevokeAsync(supersededId, actor);
        }

        TempData["info"] = await this.FlashAsync(
            "admin.invitations.resent",
            $"A fresh invitation was sent to {normalized}.");
        return RedirectToAction(nameof(Index));
    }

    // ── private helpers ────────────────────────────────────────────────────

    private async Task<string> ActorDisplayNameAsync()
    {
        // The inviter's display name (the email's {0}) — the actor is the
        // GlobalAdmin; their Profile's DisplayName is the natural name.
        // Read through the request services (no ctor change, the
        // ControllerFlashExtensions shape) and floor to empty (the
        // service floors to "there").
        var services = HttpContext.RequestServices;
        if (services is null) return string.Empty;
        var userInfo = services.GetService<Kumunita.Core.UserInfo.IUserInfoService>();
        var actor = ActorId(User);
        if (userInfo is null || actor is null) return string.Empty;
        try { return (await userInfo.GetProfileAsync(actor))?.DisplayName ?? string.Empty; }
        catch { return string.Empty; }   // fail-open (the email lane is best-effort on the name)
    }

    /// <summary>Split a textarea / repeated field into email candidates
    /// (newlines, commas, and whitespace are all separators — the A09
    /// card: "split on newlines/commas").</summary>
    internal static IEnumerable<string> SplitEmails(string raw) =>
        (raw ?? string.Empty)
            .Split(new[] { '\n', '\r', ',', ' ', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0);

    /// <summary>A shape check for an invited address (the A09 card's
    /// "invalid emails are reported, not silently dropped"): a
    /// <c>local@domain</c> with a dot in the domain — no regex library
    /// dependency (the house "no new package" rule).</summary>
    internal static bool IsPlausibleEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var at = email.LastIndexOf('@');
        if (at <= 0 || at == email.Length - 1) return false;
        var local = email[..at];
        var domain = email[(at + 1)..];
        if (local.Length == 0 || local.Contains(" ")) return false;
        if (domain.Length < 3 || !domain.Contains('.')) return false;
        if (domain.StartsWith('.') || domain.EndsWith('.')) return false;
        return !domain.Contains("..");
    }
}
