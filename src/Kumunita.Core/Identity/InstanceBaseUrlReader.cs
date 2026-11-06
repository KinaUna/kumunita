using Microsoft.Extensions.Options;

namespace Kumunita.Core.Identity;

/// <summary>
/// C3-07 (ADR 0297, D5 contract extraction) — the read contract for the
/// instance's public base URL (the <c>VerificationOptions.BaseUrl</c> value),
/// exposed as a narrow seam so that a consumer module (the <c>Notifications</c>
/// e-mail lane, which turns the same-origin relative <c>LinkPath</c> into the
/// absolute link the notification e-mail carries) codes against a *contract*
/// instead of the concrete <c>VerificationOptions</c> policy type. This retires
/// the <c>Notifications→Identity:VerificationOptions</c> boundary edge (the
/// gate's allowed-target rule: an interface is a named contract, not a concrete
/// type crossing).
/// <para>
/// The surface is exactly the one value a cross-module reader needs:
/// <see cref="BaseUrl"/> (the public base URL of this platform instance, no
/// trailing slash). <c>null</c> / empty = the instance's public base URL is
/// unset (a dev-only shape), and the reader's consumer falls back to the
/// same-origin relative path (a human can still copy it into the browser bar).
/// </para>
/// <para>
/// The concrete <see cref="InstanceBaseUrlReader"/> adapter reads the value
/// from the host-bound <c>IOptions&lt;VerificationOptions&gt;</c> — the config
/// source of truth, bound in the host's <c>Program.cs</c> from the
/// <c>Verification__BaseUrl</c> setting (the M1 verification e-mail builds its
/// one-time link from the very same value; this seam does not change how the
/// value is configured, only who may read it across the module boundary).
/// </para>
/// </summary>
public interface IInstanceBaseUrlReader
{
    /// <summary>
    /// The instance's public base URL (no trailing slash), or <c>null</c> /
    /// empty when unset (the dev-only relative-path fallback).
    /// </summary>
    string? BaseUrl { get; }
}

/// <summary>
/// The <see cref="IInstanceBaseUrlReader"/> adapter — reads
/// <c>VerificationOptions.BaseUrl</c> from the host-bound
/// <c>IOptions&lt;VerificationOptions&gt;</c>. Registered in
/// <see cref="IdentityModule"/> (the Identity module owns both the source
/// options and the contract, so the binding stays in the source module —
/// D5 "update the DI registration in the source module").
/// <para>
/// <b>Null-tolerant (the pre-C3-07 shape, preserved):</b> the host configures
/// <c>VerificationOptions</c> in <c>Program.cs</c>, but a test harness that
/// composes the Core DI without that section (a dev-only shape) resolves
/// <c>null</c>. The adapter accepts a <c>null</c> options instance and reports
/// <c>null</c> (the consumer falls back to the relative-path shape) — exactly
/// the <c>options?.Value.BaseUrl ?? empty</c> tolerance the
/// <c>NotificationService</c> lane had before this seam existed.
/// </para>
/// </summary>
public sealed class InstanceBaseUrlReader(
    IOptions<VerificationOptions>? options) : IInstanceBaseUrlReader
{
    /// <inheritdoc />
    public string? BaseUrl => options?.Value.BaseUrl;
}
