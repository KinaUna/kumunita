namespace Kumunita.Core.Identity;

/// <summary>
/// The <see cref="IBotChallengeVerifier"/> **flag-off default** (F9, AUTH-15,
/// pin §4b) — the seam's identity element. Registered by
/// <c>Program.cs</c> **only when the <c>KUMUNITA__SIGNUP__BOT_CHECK</c> flag
/// is off** (the default), so an unconfigured instance is **unchanged**: the
/// signup form has no widget, no hidden input, no challenge, and this
/// verifier is a no-op (always <c>true</c>) — the standing owner deferral
/// (OPS.md §10, "no CAPTCHA is needed today") is honored by construction,
/// not by an absence of code.
/// <para>
/// <b>Holds no state</b> → registered as a **singleton** (the
/// <c>Program.cs</c> registration, F9.U3). A <c>null</c>/<c>""</c> token is
/// **not an error** when the flag is off (pin §4b) — the method returns
/// <c>true</c> unconditionally, so the <c>AccountController.Signup</c> POST
/// proceeds to <see cref="IIdentityService.RegisterAsync"/> exactly as before
/// this seam existed.
/// </para>
/// </summary>
public sealed class NoopBotChallengeVerifier : IBotChallengeVerifier
{
    /// <inheritdoc />
    public Task<bool> IsVerifiedAsync(string? token, CancellationToken ct = default) =>
        Task.FromResult(true);
}
