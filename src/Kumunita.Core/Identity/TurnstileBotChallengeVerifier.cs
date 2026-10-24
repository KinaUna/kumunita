using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.Identity;

/// <summary>
/// The F9 (AUTH-15) bot-challenge options (pin §4b) — the owner's provider
/// site key. Bound by <c>Program.cs</c> from
/// <c>KUMUNITA__SIGNUP__BOT_CHECK__SITE_KEY</c> (the
/// <see cref="VerificationOptions"/> / <see cref="SmtpOptions"/> precedent —
/// Core owns the class, the Web host binds the value; the
/// <c>IOptions&lt;T&gt;</c> registration is <c>Program.cs</c>-owned).
/// Consulted **only when the <c>KUMUNITA__SIGNUP__BOT_CHECK</c> flag is
/// on**; the <see cref="NoopBotChallengeVerifier"/> (flag off, the default)
/// never reads it, so an unconfigured instance's behavior is unchanged.
/// <para>
/// The site key is an **owner-ops open item** (pin §4b — a Turnstile site
/// key registered to the instance's domain, "not assumed set"): when
/// absent/blank the <see cref="TurnstileBotChallengeVerifier"/> cannot
/// verify and returns <c>false</c> (fail-closed, a bot is not proven human).
/// </para>
/// </summary>
public sealed class SignupBotChallengeOptions
{
    /// <summary>The owner's provider site key. Blank = unconfigured (the
    /// challenge cannot verify — see the class doc).</summary>
    public string? SiteKey { get; set; }
}

/// <summary>
/// The <see cref="IBotChallengeVerifier"/> **flag-on** implementation (F9,
/// AUTH-15, pin §4b) — a **plain <c>HttpClient</c> POST** to the Cloudflare
/// Turnstile <c>/sites/verify</c> endpoint (the <see cref="IMailerStage"/>
/// I/O-seam precedent, the <see cref="SmtpSender"/> "Core-agnostic BCL
/// implementation" shape). **No SDK, no new <c>&lt;PackageReference&gt;</c>**
/// (pin §7): <c>System.Net.Http</c> + <c>System.Text.Json</c> are BCL
/// (<c>System.Text.Json</c> is already in <c>Kumunita.Core</c> —
/// <c>SampleDataSeeder</c> / <c>KumunitaArchive</c> use it).
/// <para>
/// Registered by <c>Program.cs</c> **only when the
/// <c>KUMUNITA__SIGNUP__BOT_CHECK</c> flag is on** (the
/// <see cref="SiteKey"/> bound from
/// <c>KUMUNITA__SIGNUP__BOT_CHECK__SITE_KEY</c> into
/// <see cref="SignupBotChallengeOptions"/>). Holds no per-request state → a
/// **singleton** (the <see cref="HttpClient"/> is a static — the recommended
/// BCL pattern, no socket exhaustion).
/// </para>
/// <para>
/// <b>Fail-closed (a bot is not proven human):</b> a missing/blank
/// <see cref="SignupBotChallengeOptions.SiteKey"/> (the owner-ops open item
/// is unset), a missing/blank token, a provider timeout, a transport error,
/// a non-success HTTP status, or a response whose <c>success</c> field is not
/// <c>true</c> all return <c>false</c> — **never a throw** (pin §4b: "Never
/// throws — the Web maps <c>false</c> to the <c>BotChallengeFailed</c>
/// re-render"). A <c>false</c> on the <c>AccountController.Signup</c> POST
/// re-renders the form with <c>BotChallengeFailed</c> (the
/// <c>EmailDomainDisallowed</c> precedent, F9.U2); it does **not** leak the
/// reason (the Web's uniform-rejection shape — the operator's log is the
/// signal, the resident sees only the challenge again).
/// </para>
/// </summary>
public sealed class TurnstileBotChallengeVerifier(
    IOptions<SignupBotChallengeOptions> options) : IBotChallengeVerifier
{
    /// <summary>The verify timeout (the <c>EscalationForwarder</c> 10 s
    /// precedent — bounded so a hung provider cannot wedge the signup lane).</summary>
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private const string SiteVerifyUrl =
        "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    /// <inheritdoc />
    public async Task<bool> IsVerifiedAsync(string? token, CancellationToken ct = default)
    {
        // Fail-closed: the flag is on but the owner has not set a site key
        // (the owner-ops open item) → the challenge cannot be verified.
        var siteKey = options.Value.SiteKey;
        if (string.IsNullOrWhiteSpace(siteKey))
        {
            return false;
        }
        // A missing/blank token = the widget did not run (or the client
        // disabled JS) → a bot is not proven human.
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var json = JsonSerializer.Serialize(new { secret = siteKey, token = token });
            var response = await Http.PostAsync(
                SiteVerifyUrl,
                new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
                ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            using var doc = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return doc.RootElement.TryGetProperty("success", out var success)
                && success.ValueKind == JsonValueKind.True;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // A provider timeout is a failed verify, not a 500 (the
            // EscalationForwarder catch shape, M32·7).
            return false;
        }
        catch (HttpRequestException)
        {
            // A transport error is a failed verify, not a 500.
            return false;
        }
        catch (JsonException)
        {
            // A malformed provider response is a failed verify, not a 500.
            return false;
        }
    }
}
