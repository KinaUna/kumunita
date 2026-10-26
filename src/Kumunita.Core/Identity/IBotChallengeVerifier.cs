namespace Kumunita.Core.Identity;

/// <summary>
/// The F9 (AUTH-15) bot-challenge verification seam — a **new** interface on
/// the Identity module's public surface (ADR 0006-E "compatible ADD," the
/// <see cref="IIdentityService"/> / <see cref="IMailerStage"/> precedent).
/// The pin (pin §4b) is authoritative: this is the contract, and F9.U4's
/// test substitutes a stub for it (no live provider needed in the test
/// harness).
/// <para>
/// <b>Enforcement is Web-enforced, not Core-enforced</b> (pin §4b, the
/// <see cref="IMailerStage"/> "designed handoff" precedent): the
/// <see cref="Kumunita.Web.Controllers.AccountController.Signup(SignupViewModel)"/>
/// POST action calls <see cref="IsVerifiedAsync"/> **before**
/// <see cref="IIdentityService.RegisterAsync"/>, and on a <c>false</c>
/// result re-renders the signup form with the
/// <c>SignupViewModel.BotChallengeFailed</c> flag (the
/// <c>EmailDomainDisallowed</c> precedent, F9.U2). <see cref="RegisterCoreAsync"/>
/// and <see cref="IIdentityService"/> are **untouched** — the verification
/// is an I/O act (a provider call), the Web's responsibility. The interface
/// lives in Core so the **test** can substitute a stub (the F9.U4 test does
/// not need a live provider, pin §4b) and so the seam is a named, reviewable
/// surface (not a closed-over <c>Func</c>).
/// </para>
/// <para>
/// <b>Registration is <c>Program.cs</c>-owned</b> (F9.U3): when the
/// <c>KUMUNITA__SIGNUP__BOT_CHECK</c> flag is **off** (the default, the
/// standing owner deferral — OPS.md §10) the
/// <see cref="NoopBotChallengeVerifier"/> is registered (always <c>true</c>,
/// the form posts through unchanged); when **on**, the
/// <see cref="TurnstileBotChallengeVerifier"/> (a plain <c>HttpClient</c>
/// <c>siteverify</c> call — the <see cref="IMailerStage"/> I/O-seam
/// precedent, **no SDK, no new <c>&lt;PackageReference&gt;</c>**) is
/// registered. The live provider key
/// (<c>KUMUNITA__SIGNUP__BOT_CHECK__SITE_KEY</c>) is an **owner-ops open
/// item** (recorded, not assumed — the unit's rule for owner-ops steps).
/// </para>
/// </summary>
public interface IBotChallengeVerifier
{
    /// <summary>
    /// Verify the signup bot-challenge token against the owner's provider.
    /// <paramref name="token"/> is the value the widget wrote into the
    /// form's hidden <c>BotChallengeToken</c> input (a
    /// <c>null</c>/<c>""</c> token = the widget did not run, or the flag is
    /// off).
    /// </summary>
    /// <returns>
    /// <c>true</c> when the token is valid (or the flag is off — the
    /// <see cref="NoopBotChallengeVerifier"/> always returns <c>true</c>),
    /// <c>false</c> when the token is missing or invalid **and the flag is
    /// on**. **Never throws** — the Web maps <c>false</c> to the
    /// <c>BotChallengeFailed</c> re-render (pin §4b, the
    /// <c>EmailDomainDisallowed</c> precedent, §3b shape); a provider
    /// transport error is a <c>false</c> (a bot is not proven human), not
    /// an exception that 500s the signup lane.
    /// </returns>
    Task<bool> IsVerifiedAsync(string? token, CancellationToken ct = default);
}
