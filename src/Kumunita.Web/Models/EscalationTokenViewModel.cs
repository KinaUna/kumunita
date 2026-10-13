using Kumunita.Core.ErrorReports;

namespace Kumunita.Web.Models;

/// <summary>
/// The view model for the <c>/admin/escalation/tokens</c> receiving-token
/// surface (ADR 0159, ESC·3 / ESC·9 / ESC·12). The receiving GlobalAdmin's
/// token list (newest first, the <see cref="IEscalationTokenService.ListAsync"/>
/// read re-projected — no audit row, the M31·4 "read is not an access
/// decision" precedent) + the one-time plaintext flash after a
/// <see cref="IEscalationTokenService.GenerateAsync"/>.
/// <para>
/// <b>ESC·3 — the plaintext is shown exactly once.</b>
/// <see cref="NewTokenPlaintext"/> is populated **only** on the immediate
/// re-render following a <c>Generate</c> action; it is **never** re-rendered
/// on a subsequent <c>GET</c> (a fresh <c>ListAsync</c> projects to the
/// <see cref="EscalationTokenSummary"/> read-model, which carries no
/// plaintext and no <c>TokenHash</c> — the hash is the only form the
/// receiving side keeps, ESC·3).
/// </para>
/// </summary>
public sealed class EscalationTokenViewModel
{
    /// <summary>
    /// The receiving admin's token list, newest first (the
    /// <see cref="IEscalationTokenService.ListAsync"/> read, ESC·3 / ESC·9).
    /// Each summary exposes the <c>Label</c> + the masked <c>TokenPrefix</c> +
    /// the <c>Created</c> / <c>LastUsedAt</c> / <c>RevokedAt</c> status — **no**
    /// plaintext, **no** <c>TokenHash</c>. Empty when there are no tokens.
    /// </summary>
    public IReadOnlyList<EscalationTokenSummary> Tokens { get; init; } = [];

    /// <summary>
    /// The plaintext of a just-generated token, shown **once** (the
    /// <c>escalation.tokens.show_once</c> warning, ESC·3). Set only on the
    /// re-render immediately after a <c>Generate</c>; never populated by a
    /// <c>GET</c> (a fresh <c>ListAsync</c> never exposes the plaintext).
    /// </summary>
    public string? NewTokenPlaintext { get; init; }

    /// <summary>
    /// A server-resolved confirmation flash (e.g. the <c>escalation.tokens.
    /// revoked</c> message after a <c>Revoke</c>). Rendered when non-null.
    /// </summary>
    public string? Flash { get; init; }

    /// <summary>
    /// A server-resolved error message (e.g. a blank <c>Label</c> on
    /// <c>Generate</c>). Rendered when non-null.
    /// </summary>
    public string? Error { get; init; }
}
