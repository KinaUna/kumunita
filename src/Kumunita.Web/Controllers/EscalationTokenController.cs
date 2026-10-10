using Kumunita.Core.ErrorReports;
using Kumunita.Core.Localization;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/escalation/tokens</c> receiving-token admin surface (ADR 0159,
/// ESC·3 / ESC·9 / ESC·12). The receiving GlobalAdmin's token lifecycle: a
/// list of the labeled, individually-revocable tokens (newest first, the
/// <see cref="IEscalationTokenService.ListAsync"/> read — no audit row, the
/// M31·4 "read is not an access decision" precedent), a generate action
/// (the <see cref="IEscalationTokenService.GenerateAsync"/> single-write-lane —
/// the plaintext shown **once**, the SHA-256 hash row + one
/// <c>AccessAudit</c> row stored), and a revoke action (the
/// <see cref="IEscalationTokenService.RevokeAsync"/> idempotent write lane —
/// immediate, one <c>AccessAudit</c> row).
/// <para>
/// <b>ESC·3 — the plaintext is shown exactly once.</b> The <c>Generate</c>
/// action calls <see cref="IEscalationTokenService.GenerateAsync"/> (Core
/// stays HTTP-free, ADR 0006-D / ESC·6 — the Core lane generates the CSPRNG
/// plaintext + the hash; the Web lane only displays the returned plaintext).
/// The plaintext is surfaced **only** on the immediate re-render following a
/// generate (a <c>TempData</c>-style one-shot carried into the view model's
/// <see cref="EscalationTokenViewModel.NewTokenPlaintext"/>), and is **never**
/// re-rendered on a subsequent <c>GET</c> (a fresh <c>ListAsync</c> projects
/// to the <see cref="EscalationTokenSummary"/> read-model — no plaintext, no
/// <c>TokenHash</c>, ESC·3).
/// </para>
/// <para>
/// <b>The gate (ESC·12).</b> The
/// <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/> role — the standard
/// admin surface (the <see cref="ErrorReportAdminController"/> shape, the ADR
/// 0001-B thin-token rule). **No new <c>AccessAction</c>, <c>Decide()</c>
/// branch, or <c>IAuthorizationService</c> surface** (ESC·12). The token
/// *validation* stays in Core (the <see cref="IEscalationTokenService.
/// ValidateAsync"/> seam, used by the inbound endpoint, U06) — this surface
/// only generates / lists / revokes.
/// </para>
/// </summary>
[Route("admin/escalation/tokens")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class EscalationTokenController(
    // The receiving-side token seam (U03's sealed 4-method surface — the
    // ESC·1 pin: this controller **calls** it, it does not re-shape it).
    // Optional (default null) — the M31 optional-ctor-param precedent — so a
    // test-construction site that builds this controller without the seam
    // keeps compiling; the actions then render the source-text floor (the kw-
    // l provider floor, ADR 0015 D1). DI always supplies the live
    // EscalationTokenService (Program.cs / DependencyInjection.cs, U03).
    IEscalationTokenService? tokens = null,
    // The per-request localization read seams — used to render the
    // escalation.tokens.* flashes in the admin's effective language (the
    // ErrorReportAdminController.FlashAsync idiom, admin-scope). Optional
    // (default null) so a test-construction site keeps compiling and renders
    // the KnownTranslationKeys.EnValues source text (the kw-l floor, ADR 0015
    // D1); DI always supplies the live ILocalizationService +
    // ITranslationProvider.
    ILocalizationService? localization = null,
    ITranslationProvider? translationProvider = null) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// Resolve an <c>escalation.*</c> kw-l key to the admin's effective
    /// language (the house
    /// <see cref="EffectiveLanguageCode.ResolveAsync"/> +
    /// <see cref="ITranslationProvider.GetAsync"/> seam — the same chain the
    /// view's <c>&lt;kw-l&gt;</c> TagHelper uses). Falls back to the
    /// <see cref="KnownTranslationKeys.EnValues"/> source text when the
    /// translation seam is absent (the test-construction floor, ADR 0015 D1 —
    /// code is the floor, so an admin never sees a raw key).
    /// </summary>
    private async Task<string> FlashAsync(string key)
    {
        if (localization is null || translationProvider is null)
            return KnownTranslationKeys.EnValues.GetValueOrDefault(key) ?? key;
        return await translationProvider.GetAsync(
            key,
            await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider));
    }

    // ── GET /admin/escalation/tokens — the token list (ESC·3 / ESC·9) ──────

    /// <summary>
    /// <c>GET /admin/escalation/tokens</c> — the receiving admin's token list
    /// (newest first, the <see cref="IEscalationTokenService.ListAsync"/>
    /// read — a read, no audit row, the M31·4 precedent). Each row exposes the
    /// <c>Label</c> + the masked <c>TokenPrefix</c> + the <c>Created</c> /
    /// <c>LastUsedAt</c> / <c>RevokedAt</c> status (the
    /// <see cref="EscalationTokenSummary"/> read-model — **no** plaintext, **no**
    /// <c>TokenHash</c>, ESC·3). A <c>TempData["info"]</c> / <c>["error"]</c>
    /// flash (the <c>escalation.tokens.revoked</c> / a validation message, the
    /// U07 kw-l key set, ESC·11) is surfaced when the just-processed
    /// <c>Revoke</c> / <c>Generate</c> action set one. <c>[Authorize(Roles =
    /// GlobalAdmin)]</c> (the ESC·12 standard admin gate — no new authz
    /// surface).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // The test-construction floor (the M31 optional-ctor-param precedent —
        // the tokens seam is optional, default null). Renders an empty list +
        // the source-text floor; no exception.
        if (tokens is null)
        {
            return View(new EscalationTokenViewModel
            {
                Tokens = [],
                Flash  = null,
                Error  = null,
            });
        }

        var list = await tokens.ListAsync();
        return View(new EscalationTokenViewModel
        {
            Tokens = list,
            // A one-shot flash carried over from the just-processed Revoke
            // action (the escalation.tokens.revoked kw-l key, the U07 closed
            // set, ESC·11). A Generate re-render is the View(model) path
            // below (the NewTokenPlaintext one-shot), not a flash.
            Flash = TempData["info"] as string,
            Error = TempData["error"] as string,
        });
    }

    // ── POST /admin/escalation/tokens/generate — the single-write-lane
    //    (ESC·3 / ESC·9) ──────────────────────────────────────────────────

    /// <summary>
    /// <c>POST /admin/escalation/tokens/generate</c> — generate a new labeled
    /// token (ESC·3 / ESC·9). Calls
    /// <see cref="IEscalationTokenService.GenerateAsync"/> (the ADR 0006 C3
    /// single-write-lane — the Core lane stores the <c>EscalationToken</c>
    /// row, hash-only-at-rest, + exactly one <c>AccessAudit</c> row
    /// (<c>Via = Admin</c>, action <c>escalation.token.generate</c>,
    /// <c>TargetKind</c> "escalation-token") and returns the CSPRNG
    /// plaintext). The plaintext is shown **once** (a one-shot
    /// <see cref="EscalationTokenViewModel.NewTokenPlaintext"/>, the
    /// <c>escalation.tokens.show_once</c> kw-l key, the U07 closed set, ESC·11)
    /// + the refreshed list (a masked row, the <c>TokenPrefix</c> only,
    /// ESC·3); it is **never** re-rendered on a subsequent <c>GET</c>.
    /// A blank <c>Label</c> re-renders with an <c>Error</c> (the source-text
    /// floor, the M32·4 no-op shape), no state change, no audit row.
    /// <c>[ValidateAntiForgeryToken]</c> + <c>[Authorize(Roles = GlobalAdmin)]</c>
    /// (the ESC·12 standard admin gate).
    /// </summary>
    [HttpPost("generate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(string? label)
    {
        var actor = ActorId(User) ?? string.Empty;

        // The test-construction floor (the M31 optional-ctor-param precedent —
        // the tokens seam is optional, default null).
        if (tokens is null)
        {
            return View(new EscalationTokenViewModel
            {
                Tokens = [],
                Error  = "The token service is unavailable.",
            });
        }

        // A blank label — a no-op (the M32·4 / M31·6 shape — no state change,
        // no audit row). Re-render the list with the error.
        if (string.IsNullOrWhiteSpace(label))
        {
            var blankList = await tokens.ListAsync();
            return View(new EscalationTokenViewModel
            {
                Tokens = blankList,
                Error  = await FlashAsync("escalation.tokens.label.label"),
            });
        }

        // The single-write-lane (ESC·3 / ESC·9 — the ADR 0006 C3 shape, the
        // U03 EscalationTokenService.GenerateAsync): the Core lane stores the
        // hash row + one AccessAudit row and returns the plaintext (shown
        // once, never persisted — ESC·3).
        var (plaintext, _) = await tokens.GenerateAsync(label.Trim(), actor);
        var generatedList = await tokens.ListAsync();
        return View(new EscalationTokenViewModel
        {
            Tokens            = generatedList,
            NewTokenPlaintext = plaintext,   // the ESC·3 one-shot (show once)
            Flash             = null,
            Error             = null,
        });
    }

    // ── POST /admin/escalation/tokens/{id}/revoke — the idempotent write
    //    lane (ESC·9) ─────────────────────────────────────────────────────

    /// <summary>
    /// <c>POST /admin/escalation/tokens/{id}/revoke</c> — revoke a token
    /// (ESC·9). Calls
    /// <see cref="IEscalationTokenService.RevokeAsync"/> (the idempotent write
    /// lane — a no-op, no audit row, no state change, when the token is
    /// missing or already revoked, the M32·8 precedent; on an effective revoke
    /// it stores the <c>RevokedAt</c> / <c>RevokedBy</c> + exactly one
    /// <c>AccessAudit</c> row (<c>Via = Admin</c>, action
    /// <c>escalation.token.revoke</c>, <c>TargetKind</c> "escalation-token"),
    /// and the token **no longer validates** — immediate, ESC·9). After an
    /// effective revoke, a <c>TempData["info"]</c> flash (the
    /// <c>escalation.tokens.revoked</c> kw-l key, the U07 closed set, ESC·11)
    /// + a redirect to the list (the <see cref="ErrorReportAdminController.
    /// MarkTriaged"/> redirect idiom). A no-op re-renders the list with **no**
    /// flash (no second audit row, no state change). <c>[ValidateAntiForgeryToken]</c>
    /// + <c>[Authorize(Roles = GlobalAdmin)]</c> (the ESC·12 standard admin
    /// gate).
    /// </summary>
    [HttpPost("{id}/revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(string id)
    {
        var actor = ActorId(User) ?? string.Empty;

        // The test-construction floor (the M31 optional-ctor-param precedent —
        // the tokens seam is optional, default null).
        if (tokens is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var effective = await tokens.RevokeAsync(id, actor);
        if (effective)
        {
            // An effective revoke (ESC·9) — flash (the
            // escalation.tokens.revoked kw-l key, the U07 closed set).
            TempData["info"] = await FlashAsync("escalation.tokens.revoked");
        }
        // A no-op (a missing or already-revoked token, ESC·9, the M32·8
        // idempotency pin) → no flash; the redirect is the same either way.
        return RedirectToAction(nameof(Index));
    }
}
