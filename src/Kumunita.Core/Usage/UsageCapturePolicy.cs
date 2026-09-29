namespace Kumunita.Core.Usage;

/// <summary>
/// The pure input record for <see cref="UsageCapturePolicy.Decide"/> —
/// the small projection the Web middleware builds from the
/// <c>HttpContext</c> (so the policy is testable without an
/// <c>HttpContext</c>, D1). <see cref="HasEndpoint"/> is
/// <c>httpContext.GetEndpoint() != null</c>; <see cref="IsStaticFile"/>
/// is the endpoint's metadata containing a
/// <c>StaticFileEndpointMetadata</c>; <see cref="RouteTemplate"/> is the
/// endpoint's <c>RoutePattern.RawText</c> prefixed with the HTTP method
/// (the <c>GET /posts/{id}</c> shape); <see cref="ActorId"/> is the
/// <c>ClaimTypes.Subject</c> value, <c>string.Empty</c> when anonymous.
/// </summary>
public sealed class UsageCaptureInput
{
    public bool HasEndpoint { get; init; }
    public bool IsStaticFile { get; init; }
    public string? RouteTemplate { get; init; }
    public string ActorId { get; init; } = string.Empty;
}

/// <summary>
/// The pure output record for <see cref="UsageCapturePolicy.Decide"/>.
/// <see cref="Record"/> false ⇒ the middleware stores nothing (the
/// C-M13·4 skip — no endpoint, a static file). <see cref="Record"/>
/// true ⇒ the middleware stores one <see cref="UsageEvent"/> with
/// <see cref="RouteTemplate"/> + <see cref="ActorId"/> from this record
/// (the <c>At</c> is the middleware's capture instant, not the policy's).
/// </summary>
public sealed class UsageCaptureDecision
{
    public bool Record { get; init; }
    public string RouteTemplate { get; init; } = string.Empty;
    public string ActorId { get; init; } = string.Empty;
}

/// <summary>
/// The D1 capture policy (ADR 0114). Pure: no store, no clock, no
/// <c>HttpContext</c> — the Web middleware is the thin host adapter.
/// The two skip rules (C-M13·4): <c>!HasEndpoint</c> ⇒ <c>Skip</c> (a
/// true 404, a malformed path); <c>IsStaticFile</c> ⇒ <c>Skip</c> (a
/// static file is not a usage surface). Otherwise <c>Record</c> with the
/// input's <c>RouteTemplate</c> + <c>ActorId</c> (the
/// <c>ActorId</c>-empty-for-anonymous rule, C-M13·2).
/// </summary>
public static class UsageCapturePolicy
{
    public static UsageCaptureDecision Decide(UsageCaptureInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // C-M13·4 — the template is the unit: a request with no
        // recognized endpoint (a true 404, a malformed path) is skipped.
        if (!input.HasEndpoint)
            return new UsageCaptureDecision { Record = false };

        // C-M13·4 — a static file is noise, not a usage surface.
        if (input.IsStaticFile)
            return new UsageCaptureDecision { Record = false };

        // Record — the RouteTemplate + ActorId pass through verbatim
        // (the ActorId-empty-for-anonymous rule, C-M13·2).
        return new UsageCaptureDecision
        {
            Record = true,
            RouteTemplate = input.RouteTemplate ?? string.Empty,
            ActorId = input.ActorId
        };
    }
}
