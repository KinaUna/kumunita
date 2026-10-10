using Kumunita.Core.Authorization;
using Kumunita.Core.ErrorReports;
using Kumunita.Core.Localization;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/escalation</c> outbound-config admin surface (ADR 0159,
/// ESC·4 / ESC·12). The origin GlobalAdmin's own outgoing settings: the
/// receiving platform's inbound <c>Endpoint</c>, the plaintext access
/// <c>Token</c> the origin admin was handed by the receiving admin out-of-band
/// (the ESC·4 outgoing secret — stored because it is a secret the origin admin
/// chose to keep, **not** a secret of the receiving platform, contrast the
/// receiving-side <c>EscalationToken.TokenHash</c> which is hash-only-at-rest,
/// ESC·3), and an <c>Enabled</c> switch.
/// <para>
/// <b>ESC·4 — the read seam.</b> The design doc §2.1.3 names "the read seam
/// the U03 design pins"; U03 added the <see cref="IEscalationTokenService"/>
/// 4-method token seam but **not** an outbound-config read/write seam (the
/// U05 Deliverables list is 6 Web files, no new Core file). This controller
/// therefore reads + writes the <see cref="EscalationOutboundConfig"/>
/// singleton directly through the host-registered Marten
/// <see cref="IDocumentStore"/> (the <see cref="AdminStorageController"/>
/// precedent — the controller owns the write session, the C3 same-transaction
/// lane). The M32·6 env var <c>KUMUNITA_ESCALATION_ENDPOINT</c> remains the
/// unauthenticated operator-override / fallback the Web-layer
/// <see cref="IEscalationForwarder"/> (U06) uses when this row is absent or
/// disabled.
/// </para>
/// <para>
/// <b>The gate (ESC·12).</b> The
/// <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/> role — the standard
/// admin surface (the <see cref="ErrorReportAdminController"/> shape, the ADR
/// 0001-B thin-token rule). **No new <c>AccessAction</c>, <c>Decide()</c>
/// branch, or <c>IAuthorizationService</c> surface** (ESC·12 — the admin gate
/// is the standard role check).
/// </para>
/// <para>
/// <b>The save (ESC·4 single-write-lane).</b> The
/// <see cref="EscalationOutboundConfig"/> doc + exactly one
/// <c>AccessAudit</c> row (<c>Via = Admin</c>, action
/// <c>escalation.outbound.save</c>, <c>TargetKind</c> "escalation-outbound-
/// config") commit in **one** write session (the ADR 0006 C3 shape, the
/// <see cref="ErrorReportService"/> <c>MarkResolvedAsync</c> precedent).
/// Core stays HTTP-free (ADR 0006-D / ESC·6): this is a Web-layer write.
/// </para>
/// </summary>
[Route("admin/escalation")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class EscalationConfigController(
    // The host-registered Marten store (the AdminStorageController /
    // BookmarksController precedent). The EscalationOutboundConfig singleton
    // is read via a QuerySession and written via an OpenSession (the
    // EscalationTokenService shape) — the U05 deliverables list is 6 Web files
    // (no new Core seam), so the Web layer owns the read + the single-write-
    // lane directly (the design doc §2.1.3 read-seam resolution).
    IDocumentStore store,
    // The per-request localization read seams — used to render the
    // escalation.outbound.saved flash in the admin's effective language
    // (the ErrorReportAdminController.FlashAsync idiom, admin-scope).
    // Optional (default null) so any test-construction site that builds this
    // controller without the seams keeps compiling and renders the
    // KnownTranslationKeys.EnValues source text (the kw-l floor, ADR 0015 D1);
    // DI always supplies the live ILocalizationService + ITranslationProvider.
    ILocalizationService? localization = null,
    ITranslationProvider? translationProvider = null) : Controller
{
    /// <summary>
    /// The fixed singleton id for the <see cref="EscalationOutboundConfig"/>
    /// row (the ESC·4 pin — one row per instance; the Web layer sets it, the
    /// U03 doc comment "a fixed singleton id, e.g. outbound-config").
    /// </summary>
    private const string SingletonId = "outbound-config";

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

    // ── GET /admin/escalation — the read-only outbound-config form (ESC·4) ──

    /// <summary>
    /// <c>GET /admin/escalation</c> — load the <see cref="EscalationOutboundConfig"/>
    /// singleton (a read — no audit row, the M31·4 "read is not an access
    /// decision" precedent) and bind it to the
    /// <see cref="EscalationConfigFormModel"/> (the <c>Endpoint</c> +
    /// <c>Token</c> + <c>Enabled</c> fields + the <c>Saved</c> flag). A
    /// missing row reads as an empty form (the <c>Endpoint</c> / <c>Token</c>
    /// blank, <c>Enabled</c> false — the <see cref="AdminStorageController"/>
    /// create-if-missing sentinel shape). <c>[Authorize(Roles = GlobalAdmin)]</c>
    /// (the ESC·12 standard admin gate — no new authz surface).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        EscalationOutboundConfig? existing;
        using (var session = store.QuerySession())
        {
            existing = await session.LoadAsync<EscalationOutboundConfig>(SingletonId);
        }

        var model = new EscalationConfigFormModel
        {
            Endpoint = existing?.Endpoint ?? string.Empty,
            Token    = existing?.Token,
            Enabled  = existing?.Enabled ?? false,
        };
        // A Saved flash (the escalation.outbound.saved kw-l key, the U07
        // closed set, ESC·11) — rendered when the just-processed Save action
        // succeeded. A re-render with a validation error leaves Saved false.
        if (TempData["info"] is string saved)
        {
            model.Saved = true;
        }
        return View(model);
    }

    // ── POST /admin/escalation — the single-write-lane save (ESC·4) ────────

    /// <summary>
    /// <c>POST /admin/escalation</c> — the outbound-config save (ESC·4).
    /// Validates the bound <see cref="EscalationConfigFormModel"/> (the
    /// <c>Endpoint</c> is required + a valid <c>http://</c>/<c>https://</c>
    /// URL when <c>Enabled</c> is <c>true</c>; both are optional when
    /// <c>false</c>) and writes the <see cref="EscalationOutboundConfig"/>
    /// singleton + exactly one <c>AccessAudit</c> row (<c>Via = Admin</c>,
    /// action <c>escalation.outbound.save</c>, <c>TargetKind</c>
    /// "escalation-outbound-config") in **one** write session (the ADR 0006
    /// C3 single-write-lane shape, the <see cref="ErrorReportService"/>
    /// <c>MarkResolvedAsync</c> precedent — the U03
    /// <see cref="EscalationTokenService"/> <c>GenerateAsync</c> /
    /// <c>RevokeAsync</c> single-write-lane idiom). A validation failure
    /// re-renders with the <c>escalation.outbound.save</c> error, no state
    /// change, no audit row. Success → a <c>TempData["info"]</c> flash (the
    /// <c>escalation.outbound.saved</c> kw-l key, the U07 closed set, ESC·11)
    /// + a re-render with the <c>Saved</c> flag (the register's
    /// "re-render with the Saved flag" shape). <c>[ValidateAntiForgeryToken]</c>
    /// + <c>[Authorize(Roles = GlobalAdmin)]</c> (the ESC·12 standard admin
    /// gate).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromForm] EscalationConfigFormModel model)
    {
        var actor = ActorId(User) ?? string.Empty;

        // ESC·4 — validation. When Enabled, the Endpoint is required + a
        // valid URL. When disabled, both are optional (the forwarder, U06,
        // falls back to the KUMUNITA_ESCALATION_ENDPOINT env var — the
        // unauthenticated M32-style forward, the ESC·4 pin).
        if (model.Enabled)
        {
            if (string.IsNullOrWhiteSpace(model.Endpoint) ||
                !Uri.TryCreate(model.Endpoint, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                // A validation failure — re-render the bound form with an
                // error flash (the Index.cshtml "error" flash idiom), no state
                // change, no audit row (the M32·4 / M31·6 no-op shape). The
                // model carries no Error field (the register's 4-field pin);
                // the message is the server-resolved kw-l floor text.
                model.Saved = false;
                TempData["error"] =
                    "The receiving platform endpoint is required and must be a valid http:// or https:// URL when escalation is enabled.";
                return View(model);
            }
        }

        // Load-or-create the singleton (the AdminStorageController create-if-
        // missing sentinel shape — a missing row is written as a fresh doc).
        // (The read session is named readSession to avoid colliding with the
        // write session below — the CS0136 idiom.)
        EscalationOutboundConfig config;
        using (var readSession = store.QuerySession())
        {
            var existing = await readSession.LoadAsync<EscalationOutboundConfig>(SingletonId);
            config = existing ?? new EscalationOutboundConfig { Id = SingletonId };
        }

        var now = DateTimeOffset.UtcNow;
        config.Endpoint  = model.Endpoint ?? string.Empty;
        config.Token     = model.Token ?? string.Empty;
        config.Enabled   = model.Enabled;
        config.Updated   = now;
        config.UpdatedBy = actor;

        // The single-write-lane (ESC·4 / the ADR 0006 C3 shape): the
        // EscalationOutboundConfig doc + exactly one AccessAudit row commit
        // in one write session (the EscalationTokenService.GenerateAsync /
        // RevokeAsync idiom — the M32 MarkResolvedAsync precedent).
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(config);
        session.Store(new AccessAudit
        {
            Id                   = Guid.NewGuid().ToString("N"),
            At                   = now,
            ActorId              = actor,
            EffectivePrincipalId = actor,
            Action               = "escalation.outbound.save",
            TargetKind           = "escalation-outbound-config",
            TargetId             = SingletonId,
            Via                  = AccessVia.Admin,
            Outcome              = AccessOutcome.Allow
        });
        await session.SaveChangesAsync();

        model.Saved = true;
        TempData["info"] = await FlashAsync("escalation.outbound.saved");
        return View(model);
    }
}
